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

        // true si es una de las 9 base (esas no se pueden eliminar).
        public static bool EsBase(string? nombre) =>
            Base.Any(x => string.Equals(x, (nombre ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

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

        // Renombra una personalizada y actualiza los productos que la usan.
        // Las base lanzan; el nuevo nombre no debe existir ya.
        public static async Task<int> RenombrarAsync(string anterior, string nuevo)
        {
            string viejo = (anterior ?? "").Trim();
            string limpio = (nuevo ?? "").Trim();
            if (limpio.Length == 0) throw new InvalidOperationException("Escribe el nuevo nombre.");
            if (limpio.Length > 40) throw new InvalidOperationException("Máximo 40 caracteres.");
            if (EsBase(viejo))
                throw new InvalidOperationException($"«{viejo}» es categoría base del sistema y no se puede renombrar.");
            if (string.Equals(viejo, limpio, StringComparison.OrdinalIgnoreCase)) return 0;
            await _candado.WaitAsync();
            try
            {
                var propias = await LeerPropiasSinCandadoAsync();
                if (!propias.Any(x => string.Equals(x, viejo, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"La categoría «{viejo}» ya no existe.");
                if (Base.Any(x => string.Equals(x, limpio, StringComparison.OrdinalIgnoreCase))
                    || propias.Any(x => string.Equals(x, limpio, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"La categoría «{limpio}» ya existe.");
                propias.RemoveAll(x => string.Equals(x, viejo, StringComparison.OrdinalIgnoreCase));
                propias.Add(limpio);
                await File.WriteAllTextAsync(RutaArchivo(), JsonSerializer.Serialize(propias, _json));
            }
            finally { _candado.Release(); }
            // Mueve los productos a la nueva (fuera del candado, otro semaforo).
            int movidos = 0;
            foreach (var p in await PlatosStore.ListarAsync())
            {
                if (!string.Equals((p.Categoria ?? "").Trim(), viejo, StringComparison.OrdinalIgnoreCase)) continue;
                p.Categoria = limpio;
                try { await PlatosStore.ActualizarAsync(p); movidos++; }
                catch { }
            }
            return movidos;
        }
        public static async Task EliminarAsync(string nombre)
        {
            string limpio = (nombre ?? "").Trim();
            if (EsBase(limpio))
                throw new InvalidOperationException($"«{limpio}» es categoría base del sistema y no se puede eliminar.");
            // Fuera del candado: otro semaforo (PlatosStore) sin anidar cerrojos.
            int usos = (await PlatosStore.ListarAsync())
                .Count(p => string.Equals((p.Categoria ?? "").Trim(), limpio, StringComparison.OrdinalIgnoreCase));
            if (usos > 0)
                throw new InvalidOperationException($"«{limpio}» está en uso por {usos} producto(s). Cambia su categoría primero.");
            await _candado.WaitAsync();
            try
            {
                var propias = await LeerPropiasSinCandadoAsync();
                int quitados = propias.RemoveAll(x => string.Equals(x, limpio, StringComparison.OrdinalIgnoreCase));
                if (quitados == 0)
                    throw new InvalidOperationException($"La categoría «{limpio}» ya no existe.");
                await File.WriteAllTextAsync(RutaArchivo(), JsonSerializer.Serialize(propias, _json));
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
