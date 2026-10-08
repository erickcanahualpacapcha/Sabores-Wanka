using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace sabores_wanka.Services
{
    // Mesa del plano: nombre + sala + capacidad (el estado LIBRE/OCUPADA es
    // de turno y no se guarda; al recuperar ventas se marcan OCUPADA solas).
    public sealed class MesaDato
    {
        public string Nombre { get; set; } = "";
        public string Sala { get; set; } = "";
        public string Capacidad { get; set; } = "2 Personas";
        // Fija = mesa base del sistema (no se puede eliminar ni renombrar).
        public bool Fija { get; set; }
    }

    // Plano completo: salones + mesas. Semilla minima: Salon Principal con
    // sus 5 mesas fijas; el resto lo crea el usuario (Agregar sala/mesa).
    public sealed class PlanoDato
    {
        public List<string> Salones { get; set; } = new();
        public List<MesaDato> Mesas { get; set; } = new();
    }

    public static class MesasStore
    {
        private static readonly SemaphoreSlim _candado = new(1, 1);
        private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

        // Ruta local: %LocalAppData%\SaboresWanka\Data\mesas.json.
        public static string RutaArchivo()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaboresWanka", "Data");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "mesas.json");
        }

        // Lee el plano; si no existe o esta sin mesas, siembra el Salon
        // Principal con sus 5 mesas fijas (base no eliminable del sistema).
        public static async Task<PlanoDato> CargarAsync()
        {
            await _candado.WaitAsync();
            try
            {
                string ruta = RutaArchivo();
                PlanoDato? plano = null;
                if (File.Exists(ruta))
                {
                    try { plano = JsonSerializer.Deserialize<PlanoDato>(await File.ReadAllTextAsync(ruta), _json); }
                    catch (JsonException)
                    {
                        try { File.Move(ruta, ruta + ".bak", true); } catch { }
                    }
                }
                plano ??= new PlanoDato();
                if (plano.Mesas.Count == 0)
                {
                    plano = Semilla();
                    await File.WriteAllTextAsync(ruta, JsonSerializer.Serialize(plano, _json));
                }
                return plano;
            }
            finally { _candado.Release(); }
        }

        // Salon Principal + Mesa 01..05 fijas (no se pueden eliminar).
        private static PlanoDato Semilla() => new()
        {
            Salones = new List<string> { "Salón Principal" },
            Mesas = new List<MesaDato>
            {
                new() { Nombre = "Mesa 01", Sala = "Salón Principal", Capacidad = "2 Personas", Fija = true },
                new() { Nombre = "Mesa 02", Sala = "Salón Principal", Capacidad = "4 Personas", Fija = true },
                new() { Nombre = "Mesa 03", Sala = "Salón Principal", Capacidad = "6 Personas", Fija = true },
                new() { Nombre = "Mesa 04", Sala = "Salón Principal", Capacidad = "4 Personas", Fija = true },
                new() { Nombre = "Mesa 05", Sala = "Salón Principal", Capacidad = "2 Personas", Fija = true },
            },
        };

        // Guarda salones + mesas tras cada alta/baja/cambio.
        public static async Task GuardarAsync(List<string> salones, List<MesaDato> mesas)
        {
            await _candado.WaitAsync();
            try
            {
                var plano = new PlanoDato
                {
                    Salones = salones ?? new List<string>(),
                    Mesas = mesas ?? new List<MesaDato>(),
                };
                await File.WriteAllTextAsync(RutaArchivo(), JsonSerializer.Serialize(plano, _json));
            }
            finally { _candado.Release(); }
        }
    }
}
