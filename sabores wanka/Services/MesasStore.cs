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
    }

    // Plano completo: salones + mesas. Sin semilla demo: arranca vacio y el
    // usuario crea sus salas/mesas con Agregar sala / Agregar mesa.
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

        // Lee el plano; si no existe lo crea vacio (sistema nuevo, sin demo).
        public static async Task<PlanoDato> CargarAsync()
        {
            await _candado.WaitAsync();
            try
            {
                string ruta = RutaArchivo();
                if (!File.Exists(ruta))
                {
                    var vacio = new PlanoDato();
                    await File.WriteAllTextAsync(ruta, JsonSerializer.Serialize(vacio, _json));
                    return vacio;
                }
                try
                {
                    return JsonSerializer.Deserialize<PlanoDato>(await File.ReadAllTextAsync(ruta), _json) ?? new PlanoDato();
                }
                catch (JsonException)
                {
                    try { File.Move(ruta, ruta + ".bak", true); } catch { }
                    return new PlanoDato();
                }
            }
            finally { _candado.Release(); }
        }

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
