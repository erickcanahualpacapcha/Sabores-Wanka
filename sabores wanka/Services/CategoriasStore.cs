using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace sabores_wanka.Services
{
    // Categorias culinarias: 9 base fijas + las que cree el usuario en el
    // formulario (boton +). Solo las personalizadas van al JSON local.
    public static class CategoriasStore
    {
        private static readonly string[] Base =
        {
            "Carnes & Parrillas", "Entradas", "Platos de Fondo",
            "Ceviches & Pescados", "Pastas Artesanales", "Bebidas & Cócteles",
            "Postres Caseros", "Guarniciones", "Menú Ejecutivo",
        };

        private static readonly SemaphoreSlim _candado = new(1, 1);
        private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

        // Ruta local: %LocalAppData%\SaboresWanka\Data\categorias.json.
        public static string RutaArchivo()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaboresWanka", "Data");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "categorias.json");
        }

        // Base + personalizadas (sin duplicados por nombre).
        public static async Task<List<string>> ListarAsync()
        {
            var propias = await LeerPropiasAsync();
            var lista = new List<string>(Base);
            foreach (string c in propias)
            {
                if (!lista.Any(x => string.Equals(x, c, StringComparison.OrdinalIgnoreCase)))
                    lista.Add(c);
            }
            return lista;
        }

        // Agrega una personalizada; devuelve el nombre guardado.
        public static async Task<string> AgregarAsync(string nombre)
        {
            string limpio = (nombre ?? "").Trim();
            if (limpio.Length == 0) throw new InvalidOperationException("Escribe el nombre de la categoría.");
            if (limpio.Length > 40) throw new InvalidOperationException("Máximo 40 caracteres.");
            await _candado.WaitAsync();
            try
            {
                var propias = await LeerPropiasSinCandadoAsync();
                if (Base.Any(x => string.Equals(x, limpio, StringComparison.OrdinalIgnoreCase))
                    || propias.Any(x => string.Equals(x, limpio, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"La categoría «{limpio}» ya existe.");
                propias.Add(limpio);
                await File.WriteAllTextAsync(RutaArchivo(), JsonSerializer.Serialize(propias, _json));
                return limpio;
            }
            finally { _candado.Release(); }
        }

        private static async Task<List<string>> LeerPropiasAsync()
        {
            await _candado.WaitAsync();
            try { return await LeerPropiasSinCandadoAsync(); }
            finally { _candado.Release(); }
        }

        private static async Task<List<string>> LeerPropiasSinCandadoAsync()
        {
            try
            {
                string ruta = RutaArchivo();
                if (!File.Exists(ruta)) return new List<string>();
                return JsonSerializer.Deserialize<List<string>>(await File.ReadAllTextAsync(ruta), _json) ?? new List<string>();
            }
            catch { return new List<string>(); }
        }
    }
}
