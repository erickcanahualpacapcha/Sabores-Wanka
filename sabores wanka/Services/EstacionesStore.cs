using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace sabores_wanka.Services
{
    // Estación de preparación: Tag tecnico + Nombre visible.
    public sealed class Estacion
    {
        public string Tag { get; set; } = "";
        public string Nombre { get; set; } = "";
    }

    // Areas de preparacion: 5 base fijas + las que cree el usuario (lapiz).
    // Solo las personalizadas van al JSON local; el Tag nunca cambia al
    // renombrar (los platos apuntan al Tag, no al nombre).
    public static class EstacionesStore
    {
        private static readonly Estacion[] Base =
        {
            new() { Tag = "caliente", Nombre = "Cocina Caliente" },
            new() { Tag = "fria", Nombre = "Cocina Fría" },
            new() { Tag = "bar", Nombre = "Bar" },
            new() { Tag = "salon", Nombre = "Salón / Servicio" },
            new() { Tag = "postres", Nombre = "Repostería / Postres" },
        };

        private static readonly SemaphoreSlim _candado = new(1, 1);
        private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

        // Ruta local: %LocalAppData%\SaboresWanka\Data\estaciones.json.
        public static string RutaArchivo()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaboresWanka", "Data");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "estaciones.json");
        }

        // Base + personalizadas (sin duplicar tags).
        public static async Task<List<Estacion>> ListarAsync()
        {
            var propias = await LeerPropiasAsync();
            var lista = new List<Estacion>(Base);
            foreach (var e in propias)
            {
                if (!lista.Any(x => string.Equals(x.Tag, e.Tag, StringComparison.OrdinalIgnoreCase)))
                    lista.Add(e);
            }
            return lista;
        }

        // true si es una de las 5 base (esas no se tocan).
        public static bool EsBase(string? tag) =>
            Base.Any(x => string.Equals(x.Tag, (tag ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

        // Agrega una personalizada (Tag generado del nombre); devuelve la creada.
        public static async Task<Estacion> AgregarAsync(string nombre)
        {
            string limpio = (nombre ?? "").Trim();
            if (limpio.Length == 0) throw new InvalidOperationException("Escribe el nombre del área.");
            if (limpio.Length > 40) throw new InvalidOperationException("Máximo 40 caracteres.");
            await _candado.WaitAsync();
            try
            {
                var propias = await LeerPropiasSinCandadoAsync();
                if (Base.Any(x => string.Equals(x.Nombre, limpio, StringComparison.OrdinalIgnoreCase))
                    || propias.Any(x => string.Equals(x.Nombre, limpio, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"El área «{limpio}» ya existe.");
                string tag = GenerarTag(limpio, propias.Select(p => p.Tag));
                var nueva = new Estacion { Tag = tag, Nombre = limpio };
                propias.Add(nueva);
                await File.WriteAllTextAsync(RutaArchivo(), JsonSerializer.Serialize(propias, _json));
                return nueva;
            }
            finally { _candado.Release(); }
        }

        // Renombra una personalizada (el Tag no cambia) y mueve sus platos.
        public static async Task<int> RenombrarAsync(string tag, string nuevo)
        {
            string clave = (tag ?? "").Trim();
            string limpio = (nuevo ?? "").Trim();
            if (limpio.Length == 0) throw new InvalidOperationException("Escribe el nuevo nombre.");
            if (limpio.Length > 40) throw new InvalidOperationException("Máximo 40 caracteres.");
            if (EsBase(clave))
                throw new InvalidOperationException("Las áreas base del sistema no se pueden renombrar.");
            await _candado.WaitAsync();
            try
            {
                var propias = await LeerPropiasSinCandadoAsync();
                var actual = propias.FirstOrDefault(x => string.Equals(x.Tag, clave, StringComparison.OrdinalIgnoreCase));
                if (actual == null)
                    throw new InvalidOperationException("El área ya no existe.");
                if (string.Equals(actual.Nombre, limpio, StringComparison.OrdinalIgnoreCase)) return 0;
                if (Base.Any(x => string.Equals(x.Nombre, limpio, StringComparison.OrdinalIgnoreCase))
                    || propias.Any(x => string.Equals(x.Nombre, limpio, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"El área «{limpio}» ya existe.");
                actual.Nombre = limpio;
                await File.WriteAllTextAsync(RutaArchivo(), JsonSerializer.Serialize(propias, _json));
            }
            finally { _candado.Release(); }
            int movidos = 0;
            // El Tag no cambia: los platos siguen apuntando solos. Solo se
            // cuenta cuantos usan el area para el mensaje de confirmacion.
            foreach (var p in await PlatosStore.ListarAsync())
            {
                if (string.Equals((p.EstacionTag ?? "").Trim(), clave, StringComparison.OrdinalIgnoreCase))
                    movidos++;
            }
            return movidos;
        }

        // Elimina una personalizada (las base lanzan; en uso se bloquea).
        public static async Task EliminarAsync(string tag)
        {
            string clave = (tag ?? "").Trim();
            if (EsBase(clave))
                throw new InvalidOperationException("Las áreas base del sistema no se pueden eliminar.");
            int usos = (await PlatosStore.ListarAsync())
                .Count(p => string.Equals((p.EstacionTag ?? "").Trim(), clave, StringComparison.OrdinalIgnoreCase));
            if (usos > 0)
                throw new InvalidOperationException($"Hay {usos} producto(s) en esa área. Cambia su área primero.");
            await _candado.WaitAsync();
            try
            {
                var propias = await LeerPropiasSinCandadoAsync();
                int quitadas = propias.RemoveAll(x => string.Equals(x.Tag, clave, StringComparison.OrdinalIgnoreCase));
                if (quitadas == 0)
                    throw new InvalidOperationException("El área ya no existe.");
                await File.WriteAllTextAsync(RutaArchivo(), JsonSerializer.Serialize(propias, _json));
            }
            finally { _candado.Release(); }
        }

        private static string GenerarTag(string nombre, IEnumerable<string> ocupados)
        {
            string base_ = new string(nombre.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c)).ToArray());
            if (base_.Length == 0) base_ = "area";
            if (base_.Length > 20) base_ = base_.Substring(0, 20);
            var tomados = new HashSet<string>(Base.Select(b => b.Tag).Concat(ocupados ?? Enumerable.Empty<string>()), StringComparer.OrdinalIgnoreCase);
            string tag = base_;
            int n = 2;
            while (tomados.Contains(tag)) tag = base_ + n++;
            return tag;
        }

        private static async Task<List<Estacion>> LeerPropiasAsync()
        {
            await _candado.WaitAsync();
            try { return await LeerPropiasSinCandadoAsync(); }
            finally { _candado.Release(); }
        }

        private static async Task<List<Estacion>> LeerPropiasSinCandadoAsync()
        {
            try
            {
                string ruta = RutaArchivo();
                if (!File.Exists(ruta)) return new List<Estacion>();
                return JsonSerializer.Deserialize<List<Estacion>>(await File.ReadAllTextAsync(ruta), _json) ?? new List<Estacion>();
            }
            catch { return new List<Estacion>(); }
        }
    }
}
