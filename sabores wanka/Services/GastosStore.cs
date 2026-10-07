using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using sabores_wanka.Models;

namespace sabores_wanka.Services
{
    // Gastos de caja en JSON local (full local, sin nube). Correlativo EG-0000xx
    // sobre el máximo existente; cada registro suma al KPI de egresos y resta
    // al balance del día.
    public static class GastosStore
    {
        private const string Serie = "EG";
        private static readonly SemaphoreSlim _candado = new(1, 1);
        private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

        // Ruta local: %LocalAppData%\SaboresWanka\Data\gastos.json.
        public static string RutaArchivo()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaboresWanka", "Data");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "gastos.json");
        }

        // Lee todo (más reciente primero); siembra los 2 de muestra la primera vez.
        public static async Task<List<Gasto>> ListarAsync()
        {
            await _candado.WaitAsync();
            try
            {
                string ruta = RutaArchivo();
                if (!File.Exists(ruta))
                {
                    var semilla = Semilla();
                    await File.WriteAllTextAsync(ruta, JsonSerializer.Serialize(semilla, _json));
                    return semilla;
                }
                try
                {
                    var lista = JsonSerializer.Deserialize<List<Gasto>>(await File.ReadAllTextAsync(ruta), _json) ?? new List<Gasto>();
                    return lista.OrderByDescending(g => g.FechaHora).ToList();
                }
                catch (JsonException)
                {
                    try { File.Move(ruta, ruta + ".bak", true); } catch { }
                    return new List<Gasto>();
                }
            }
            finally { _candado.Release(); }
        }

        // Registra un gasto con comprobante ya asignado por SiguienteComprobanteAsync.
        public static async Task RegistrarAsync(Gasto gasto)
        {
            if (gasto == null) throw new ArgumentNullException(nameof(gasto));
            if (string.IsNullOrWhiteSpace(gasto.Comprobante)) throw new InvalidOperationException("El gasto necesita número de comprobante.");
            if (string.IsNullOrWhiteSpace(gasto.Concepto)) throw new InvalidOperationException("El concepto del gasto es obligatorio.");
            if (gasto.Monto <= 0) throw new InvalidOperationException("El monto debe ser mayor a cero.");
            await _candado.WaitAsync();
            try
            {
                string ruta = RutaArchivo();
                var lista = new List<Gasto>();
                if (File.Exists(ruta))
                {
                    try { lista = JsonSerializer.Deserialize<List<Gasto>>(await File.ReadAllTextAsync(ruta), _json) ?? new List<Gasto>(); }
                    catch (JsonException) { lista = new List<Gasto>(); }
                }
                if (lista.Any(g => string.Equals(g.Comprobante, gasto.Comprobante, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"El comprobante {gasto.Comprobante} ya existe.");
                if (gasto.FechaHora == default) gasto.FechaHora = DateTime.Now;
                lista.Add(gasto);
                await File.WriteAllTextAsync(ruta, JsonSerializer.Serialize(lista, _json));
            }
            finally { _candado.Release(); }
        }

        // Siguiente correlativo de la serie sobre el máximo existente (EG-000022 -> 23).
        public static async Task<string> SiguienteComprobanteAsync()
        {
            await _candado.WaitAsync();
            try
            {
                string ruta = RutaArchivo();
                int max = 0;
                if (File.Exists(ruta))
                {
                    List<Gasto>? lista = null;
                    try { lista = JsonSerializer.Deserialize<List<Gasto>>(await File.ReadAllTextAsync(ruta), _json); } catch (JsonException) { }
                    foreach (var g in lista ?? new List<Gasto>())
                    {
                        string t = g.Comprobante ?? "";
                        if (!t.StartsWith(Serie + "-", StringComparison.OrdinalIgnoreCase)) continue;
                        string digitos = new string(t.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
                        if (int.TryParse(digitos, out int n) && n > max) max = n;
                    }
                }
                return $"{Serie}-{(max + 1).ToString().PadLeft(6, '0')}";
            }
            finally { _candado.Release(); }
        }

        // 2 filas que hoy están fijas en el XAML de Historial/Egresos.
        private static List<Gasto> Semilla() => new()
        {
            new Gasto { Comprobante = "EG-000021", FechaHora = new DateTime(2024, 10, 24, 9, 15, 0), Tipo = "COMPRA", Concepto = "Mercado Mayorista — Verduras", Autorizo = "Carlos M.", Monto = 75.00, Metodo = "Efectivo / Caja chica", Estado = "EGRESO" },
            new Gasto { Comprobante = "EG-000022", FechaHora = new DateTime(2024, 10, 24, 10, 40, 0), Tipo = "SERVICIO", Concepto = "Gas — Recarga balón", Autorizo = "Carlos M.", Monto = 45.00, Metodo = "Yape", Estado = "EGRESO" },
        };
    }
}
