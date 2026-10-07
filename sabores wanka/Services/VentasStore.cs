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
    // Ventas cerradas en JSON local (full local, sin nube). Cada confirmación
    // de cierre genera UNA boleta con correlativo B001-#######: cierre total
    // = 1 boleta, cierres parciales = 1 boleta distinta por parte.
    public static class VentasStore
    {
        private const string Serie = "B001";
        private static readonly SemaphoreSlim _candado = new(1, 1);
        private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

        // Ruta local: %LocalAppData%\SaboresWanka\Data\ventas.json.
        public static string RutaArchivo()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaboresWanka", "Data");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "ventas.json");
        }

        // Lee todo (más reciente primero); siembra las 5 de muestra la primera vez.
        public static async Task<List<Venta>> ListarAsync()
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
                    var lista = JsonSerializer.Deserialize<List<Venta>>(await File.ReadAllTextAsync(ruta), _json) ?? new List<Venta>();
                    return lista.OrderByDescending(v => v.FechaHora).ToList();
                }
                catch (JsonException)
                {
                    try { File.Move(ruta, ruta + ".bak", true); } catch { }
                    return new List<Venta>();
                }
            }
            finally { _candado.Release(); }
        }

        // Registra una boleta ya armada (ticket asignado por SiguienteTicketAsync).
        public static async Task RegistrarAsync(Venta venta)
        {
            if (venta == null) throw new ArgumentNullException(nameof(venta));
            if (string.IsNullOrWhiteSpace(venta.Ticket)) throw new InvalidOperationException("La venta necesita número de ticket.");
            if (venta.Total <= 0) throw new InvalidOperationException("La venta debe tener un total mayor a cero.");
            await _candado.WaitAsync();
            try
            {
                string ruta = RutaArchivo();
                var lista = new List<Venta>();
                if (File.Exists(ruta))
                {
                    try { lista = JsonSerializer.Deserialize<List<Venta>>(await File.ReadAllTextAsync(ruta), _json) ?? new List<Venta>(); }
                    catch (JsonException) { lista = new List<Venta>(); }
                }
                if (lista.Any(v => string.Equals(v.Ticket, venta.Ticket, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"El ticket {venta.Ticket} ya existe.");
                if (venta.FechaHora == default) venta.FechaHora = DateTime.Now;
                lista.Add(venta);
                await File.WriteAllTextAsync(ruta, JsonSerializer.Serialize(lista, _json));
            }
            finally { _candado.Release(); }
        }

        // Busca una boleta por ticket (copia; null si no existe).
        public static async Task<Venta?> ObtenerPorTicketAsync(string ticket)
        {
            string clave = (ticket ?? "").Trim();
            if (clave.Length == 0) return null;
            var lista = await ListarAsync();
            return lista.FirstOrDefault(v => string.Equals(v.Ticket, clave, StringComparison.OrdinalIgnoreCase));
        }

        // Siguiente correlativo de la serie sobre el máximo existente (B001-0029841 -> 42).
        public static async Task<string> SiguienteTicketAsync()
        {
            await _candado.WaitAsync();
            try
            {
                string ruta = RutaArchivo();
                int max = 0;
                if (File.Exists(ruta))
                {
                    List<Venta>? lista = null;
                    try { lista = JsonSerializer.Deserialize<List<Venta>>(await File.ReadAllTextAsync(ruta), _json); } catch (JsonException) { }
                    foreach (var v in lista ?? new List<Venta>())
                    {
                        string t = v.Ticket ?? "";
                        if (!t.StartsWith(Serie + "-", StringComparison.OrdinalIgnoreCase)) continue;
                        string digitos = new string(t.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
                        if (int.TryParse(digitos, out int n) && n > max) max = n;
                    }
                }
                return $"{Serie}-{(max + 1).ToString().PadLeft(7, '0')}";
            }
            finally { _candado.Release(); }
        }

        // Actualiza una boleta existente por ticket (edicion desde el historial).
        // No cambia Estado ni fechas: solo datos e items. El total debe ser > 0.
        public static async Task ActualizarAsync(Venta venta)
        {
            if (venta == null) throw new ArgumentNullException(nameof(venta));
            string clave = (venta.Ticket ?? "").Trim();
            if (clave.Length == 0) throw new InvalidOperationException("La venta necesita número de ticket.");
            if (venta.Items == null || venta.Items.Count == 0) throw new InvalidOperationException("La venta debe tener al menos un producto.");
            if (venta.Total <= 0) throw new InvalidOperationException("La venta debe tener un total mayor a cero.");
            await _candado.WaitAsync();
            try
            {
                string ruta = RutaArchivo();
                List<Venta>? lista = null;
                if (File.Exists(ruta))
                {
                    try { lista = JsonSerializer.Deserialize<List<Venta>>(await File.ReadAllTextAsync(ruta), _json); } catch (JsonException) { }
                }
                lista ??= new List<Venta>();
                int idx = lista.FindIndex(v => string.Equals(v.Ticket, clave, StringComparison.OrdinalIgnoreCase));
                if (idx < 0) throw new InvalidOperationException($"El ticket {clave} ya no existe.");
                var actual = lista[idx];
                // La edicion nunca mueve el estado: para cobrar se usa MarcarCobradaAsync.
                venta.Estado = actual.Estado;
                venta.FechaHora = actual.FechaHora;
                venta.FechaCobro = actual.FechaCobro;
                lista[idx] = venta;
                await File.WriteAllTextAsync(ruta, JsonSerializer.Serialize(lista, _json));
            }
            finally { _candado.Release(); }
        }

        // Marca un POR COBRAR como cobrado: pasa a PAGADO con su metodo de cobro
        // real y fecha de cobro (asi entra a Ingresos del dia del cobro).
        public static async Task<bool> MarcarCobradaAsync(string ticket, string metodoCobro)
        {
            string clave = (ticket ?? "").Trim();
            if (clave.Length == 0) return false;
            await _candado.WaitAsync();
            try
            {
                string ruta = RutaArchivo();
                if (!File.Exists(ruta)) return false;
                List<Venta>? lista;
                try { lista = JsonSerializer.Deserialize<List<Venta>>(await File.ReadAllTextAsync(ruta), _json) ?? new List<Venta>(); }
                catch (JsonException) { return false; }
                var venta = lista.FirstOrDefault(v => string.Equals(v.Ticket, clave, StringComparison.OrdinalIgnoreCase));
                if (venta == null) return false;
                if (venta.EsPagada) return true;
                venta.Estado = "PAGADO";
                if (!string.IsNullOrWhiteSpace(metodoCobro)) venta.MetodoPago = metodoCobro.Trim();
                venta.FechaCobro = DateTime.Now;
                await File.WriteAllTextAsync(ruta, JsonSerializer.Serialize(lista, _json));
                return true;
            }
            finally { _candado.Release(); }
        }

        // 5 filas que hoy están fijas en el XAML de Historial/Ingresos.
        private static List<Venta> Semilla() => new()
        {
            new Venta { Ticket = "B001-0029841", FechaHora = new DateTime(2024, 10, 24, 14, 42, 0), MesaEtiqueta = "Mesa 04", Cliente = "Familia Quispe", Mozo = "Luis Huamán", MetodoPago = "Yape / Plin", Comprobante = "Boleta", Total = 85.00, Estado = "PAGADO", MesaBadgeBg = "#ea580c26", MesaBadgeBorde = "#ea580c4d", MesaBadgeFg = "#fb923c", Items = new List<ItemVenta> { new() { Nombre = "Venta mostrador", Precio = 85.00, Cantidad = 1 } } },
            new Venta { Ticket = "B001-0029840", FechaHora = new DateTime(2024, 10, 24, 14, 28, 0), MesaEtiqueta = "Mesa 02", Cliente = "Ing. Marco Ramos", Mozo = "Rosa Cárdenas", MetodoPago = "Efectivo", Comprobante = "Boleta", Total = 64.00, Estado = "PAGADO", MesaBadgeBg = "#0ea5e926", MesaBadgeBorde = "#0ea5e94d", MesaBadgeFg = "#7dd3fc", Items = new List<ItemVenta> { new() { Nombre = "Venta mostrador", Precio = 64.00, Cantidad = 1 } } },
            new Venta { Ticket = "T002-0004128", FechaHora = new DateTime(2024, 10, 24, 14, 15, 0), MesaEtiqueta = "Mesa 08", Cliente = "Cliente Ocasional", Mozo = "Carlos Meléndez", MetodoPago = "Tarjeta VISA", Comprobante = "Ticket", Total = 28.00, Estado = "EN MESA", MesaBadgeBg = "#a855f726", MesaBadgeBorde = "#a855f74d", MesaBadgeFg = "#d8b4fe", Items = new List<ItemVenta> { new() { Nombre = "Cuenta abierta", Precio = 28.00, Cantidad = 1 } } },
            new Venta { Ticket = "B001-0029839", FechaHora = new DateTime(2024, 10, 24, 13, 50, 0), MesaEtiqueta = "Llevar #12", Cliente = "Sonia Baldeón", Mozo = "Caja Mostrador", MetodoPago = "Efectivo", Comprobante = "Boleta", Total = 42.00, Estado = "PAGADO", MesaBadgeBg = "#10b98126", MesaBadgeBorde = "#10b9814d", MesaBadgeFg = "#6ee7b7", Items = new List<ItemVenta> { new() { Nombre = "Venta mostrador", Precio = 42.00, Cantidad = 1 } } },
            new Venta { Ticket = "B001-0029838", FechaHora = new DateTime(2024, 10, 24, 13, 20, 0), MesaEtiqueta = "Mesa 09", Cliente = "Lic. David Tovar", Mozo = "Luis Mesa", MetodoPago = "Tarjeta Mastercard", Comprobante = "Boleta", Total = 96.00, Estado = "PAGADO", MesaBadgeBg = "#ea580c26", MesaBadgeBorde = "#ea580c4d", MesaBadgeFg = "#fb923c", Items = new List<ItemVenta> { new() { Nombre = "Venta mostrador", Precio = 96.00, Cantidad = 1 } } },
        };
    }
}
