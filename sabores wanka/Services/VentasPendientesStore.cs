using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace sabores_wanka.Services
{
    // Item de una orden guardada (copia plana del CartItem del POS).
    public sealed class ItemOrdenGuardado
    {
        public string Id { get; set; } = "";
        public string Nombre { get; set; } = "";
        public double Precio { get; set; }
        public int Cantidad { get; set; }
    }

    // Orden completa de una mesa con su última modificación.
    public sealed class MesaGuardada
    {
        public string Mesa { get; set; } = "";
        public List<ItemOrdenGuardado> Items { get; set; } = new();
        public DateTime ActualizadaEl { get; set; }
    }

    // Ventas abiertas: mesa actual + órdenes con items (solo se guardan mesas con algo).
    public sealed class VentasPendientesGuardadas
    {
        public string MesaActual { get; set; } = "";
        public List<MesaGuardada> Mesas { get; set; } = new();
        public DateTime GuardadaEl { get; set; }
    }

    // Persiste las órdenes abiertas en JSON local para sobrevivir cierres y
    // cortes de luz. Se guarda en cada cambio del carrito; al abrir se restaura.
    public static class VentasPendientesStore
    {
        private static readonly SemaphoreSlim _candado = new(1, 1);
        private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

        // Ruta local: %LocalAppData%\SaboresWanka\Data\ordenes_activas.json.
        public static string RutaArchivo()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaboresWanka", "Data");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "ordenes_activas.json");
        }

        // Guarda el snapshot (solo mesas con items). Si no hay nada, borra el archivo.
        public static async Task GuardarAsync(string mesaActual, Dictionary<string, List<ItemOrdenGuardado>> ordenes)
        {
            await _candado.WaitAsync();
            try
            {
                string ruta = RutaArchivo();
                var datos = new VentasPendientesGuardadas
                {
                    MesaActual = mesaActual ?? "",
                    GuardadaEl = DateTime.Now,
                    Mesas = ordenes
                        .Where(kv => kv.Value != null && kv.Value.Count > 0)
                        .Select(kv => new MesaGuardada { Mesa = kv.Key, Items = kv.Value, ActualizadaEl = DateTime.Now })
                        .ToList()
                };
                if (datos.Mesas.Count == 0 && string.IsNullOrEmpty(datos.MesaActual))
                {
                    try { if (File.Exists(ruta)) File.Delete(ruta); } catch { }
                    return;
                }
                await File.WriteAllTextAsync(ruta, JsonSerializer.Serialize(datos, _json));
            }
            finally { _candado.Release(); }
        }

        // Carga (mesaActual, ordenes). Sin archivo o corrupto devuelve vacío.
        public static async Task<(string mesaActual, Dictionary<string, List<ItemOrdenGuardado>> ordenes)> CargarAsync()
        {
            var vacio = new Dictionary<string, List<ItemOrdenGuardado>>();
            string ruta = RutaArchivo();
            if (!File.Exists(ruta)) return ("", vacio);
            await _candado.WaitAsync();
            try
            {
                try
                {
                    var datos = JsonSerializer.Deserialize<VentasPendientesGuardadas>(await File.ReadAllTextAsync(ruta), _json);
                    if (datos == null) return ("", vacio);
                    var dict = new Dictionary<string, List<ItemOrdenGuardado>>();
                    foreach (var m in datos.Mesas ?? new List<MesaGuardada>())
                    {
                        if (string.IsNullOrWhiteSpace(m.Mesa) || m.Items == null || m.Items.Count == 0) continue;
                        dict[m.Mesa] = m.Items;
                    }
                    return (datos.MesaActual ?? "", dict);
                }
                catch (JsonException)
                {
                    try { File.Move(ruta, ruta + ".bak", true); } catch { }
                    return ("", vacio);
                }
            }
            finally { _candado.Release(); }
        }
    }
}
