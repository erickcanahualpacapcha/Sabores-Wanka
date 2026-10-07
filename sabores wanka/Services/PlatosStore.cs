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
    // Almacén local de platos en JSON (full local, sin nube ni SQLite).
    // Patrón de Nachis simplificado: semilla inicial, correlativo PLU y
    // validación de duplicados por nombre normalizado. Solo unidades.
    public static class PlatosStore
    {
        private static readonly SemaphoreSlim _candado = new(1, 1);
        private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

        // Ruta local: %LocalAppData%\SaboresWanka\Data\platos.json (vale MSIX y unpackaged).
        public static string RutaArchivo()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaboresWanka", "Data");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "platos.json");
        }

        // Lista todo el catálogo; siembra los 8 de muestra la primera vez.
        // Si el JSON se corrompe, lo respalda como .bak y resiembra (no crashea).
        public static async Task<List<Plato>> ListarAsync()
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
                    string texto = await File.ReadAllTextAsync(ruta);
                    return JsonSerializer.Deserialize<List<Plato>>(texto, _json) ?? new List<Plato>();
                }
                catch (JsonException)
                {
                    try { File.Move(ruta, ruta + ".bak", true); } catch { }
                    var semilla = Semilla();
                    await File.WriteAllTextAsync(ruta, JsonSerializer.Serialize(semilla, _json));
                    return semilla;
                }
            }
            finally { _candado.Release(); }
        }

        // Alta con validación simple: nombre, PLU único y precio mayor a cero.
        public static async Task<Plato> AgregarAsync(Plato plato)
        {
            if (plato == null) throw new ArgumentNullException(nameof(plato));
            plato.Nombre = (plato.Nombre ?? "").Trim();
            plato.Plu = (plato.Plu ?? "").Trim().ToUpperInvariant().Replace("PLU-", "");
            if (plato.Plu.Length == 0) plato.Plu = await SiguientePluAsync();
            if (plato.Nombre.Length == 0) throw new InvalidOperationException("El nombre del plato es obligatorio.");
            if (plato.Precio <= 0) throw new InvalidOperationException("El precio de venta (PVP) debe ser mayor a cero.");
            if (plato.Costo < 0) throw new InvalidOperationException("El costo no puede ser negativo.");
            if (plato.Porciones < 0) throw new InvalidOperationException("Las porciones no pueden ser negativas.");

            await _candado.WaitAsync();
            try
            {
                string ruta = RutaArchivo();
                var lista = new List<Plato>();
                if (File.Exists(ruta))
                    lista = JsonSerializer.Deserialize<List<Plato>>(await File.ReadAllTextAsync(ruta), _json) ?? new List<Plato>();
                string clave = Normalizar(plato.Nombre);
                if (lista.Any(p => Normalizar(p.Nombre) == clave))
                    throw new InvalidOperationException("Ya existe un plato con ese nombre.");
                if (lista.Any(p => string.Equals((p.Plu ?? "").Trim(), plato.Plu, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"Ya existe el PLU {plato.Plu}.");
                plato.CreadoEl = DateTime.Now;
                lista.Add(plato);
                await File.WriteAllTextAsync(ruta, JsonSerializer.Serialize(lista, _json));
                return plato;
            }
            finally { _candado.Release(); }
        }

        // Busca un plato por nombre normalizado (copia; null si no existe).
        public static async Task<Plato?> ObtenerPorNombreAsync(string nombre)
        {
            string clave = Normalizar(nombre);
            if (clave.Length == 0) return null;
            var lista = await ListarAsync();
            return lista.FirstOrDefault(p => Normalizar(p.Nombre) == clave);
        }
        // Busca un plato por PLU (copia; null si no existe).
        public static async Task<Plato?> ObtenerPorPluAsync(string plu)
        {
            string clave = (plu ?? "").Trim().ToUpperInvariant().Replace("PLU-", "");
            var lista = await ListarAsync();
            return lista.FirstOrDefault(p => string.Equals((p.Plu ?? "").Trim(), clave, StringComparison.OrdinalIgnoreCase));
        }

        // Actualiza un plato existente (misma validación que el alta, pero el
        // duplicado de nombre ignora su propio PLU). No cambia el PLU ni CreadoEl.
        public static async Task ActualizarAsync(Plato plato)
        {
            if (plato == null) throw new ArgumentNullException(nameof(plato));
            plato.Nombre = (plato.Nombre ?? "").Trim();
            plato.Plu = (plato.Plu ?? "").Trim().ToUpperInvariant().Replace("PLU-", "");
            if (plato.Plu.Length == 0) throw new InvalidOperationException("El PLU es obligatorio para actualizar.");
            if (plato.Nombre.Length == 0) throw new InvalidOperationException("El nombre del plato es obligatorio.");
            if (plato.Precio <= 0) throw new InvalidOperationException("El precio de venta (PVP) debe ser mayor a cero.");
            if (plato.Costo < 0) throw new InvalidOperationException("El costo no puede ser negativo.");
            // Sin tope inferior: el sobregiro por ventas deja negativos reales.

            await _candado.WaitAsync();
            try
            {
                string ruta = RutaArchivo();
                if (!File.Exists(ruta)) throw new InvalidOperationException("El plato ya no existe en el catálogo.");
                var lista = JsonSerializer.Deserialize<List<Plato>>(await File.ReadAllTextAsync(ruta), _json) ?? new List<Plato>();
                var actual = lista.FirstOrDefault(p => string.Equals((p.Plu ?? "").Trim(), plato.Plu, StringComparison.OrdinalIgnoreCase));
                if (actual == null) throw new InvalidOperationException("El plato ya no existe en el catálogo.");
                string clave = Normalizar(plato.Nombre);
                if (lista.Any(p => !string.Equals((p.Plu ?? "").Trim(), plato.Plu, StringComparison.OrdinalIgnoreCase) && Normalizar(p.Nombre) == clave))
                    throw new InvalidOperationException("Ya existe otro plato con ese nombre.");
                actual.Nombre = plato.Nombre;
                actual.Categoria = plato.Categoria;
                actual.EstacionTag = plato.EstacionTag;
                actual.Descripcion = plato.Descripcion ?? "";
                actual.Precio = plato.Precio;
                actual.Costo = plato.Costo;
                actual.Porciones = plato.Porciones;
                actual.Unidad = string.IsNullOrWhiteSpace(plato.Unidad) ? "porc." : plato.Unidad;
                actual.FotoPath = plato.FotoPath ?? "";
                await File.WriteAllTextAsync(ruta, JsonSerializer.Serialize(lista, _json));
            }
            finally { _candado.Release(); }
        }
        // Elimina un plato por PLU (con su foto si era nuestra). Falla si no existe.
        public static async Task EliminarAsync(string plu)
        {
            string clave = (plu ?? "").Trim().ToUpperInvariant().Replace("PLU-", "");
            if (clave.Length == 0) throw new InvalidOperationException("El PLU es obligatorio para eliminar.");
            await _candado.WaitAsync();
            try
            {
                string ruta = RutaArchivo();
                if (!File.Exists(ruta)) throw new InvalidOperationException("El plato ya no existe en el catálogo.");
                var lista = JsonSerializer.Deserialize<List<Plato>>(await File.ReadAllTextAsync(ruta), _json) ?? new List<Plato>();
                var actual = lista.FirstOrDefault(p => string.Equals((p.Plu ?? "").Trim(), clave, StringComparison.OrdinalIgnoreCase));
                if (actual == null) throw new InvalidOperationException("El plato ya no existe en el catálogo.");
                lista.Remove(actual);
                await File.WriteAllTextAsync(ruta, JsonSerializer.Serialize(lista, _json));
                FotosPlatos.BorrarSiEsNuestra(actual.FotoPath);
            }
            finally { _candado.Release(); }
        }
        // Cambia el stock en porciones. Acepta negativos (sobregiro Nachis: la
        // venta nunca se bloquea); pausar/reactivar van por ActualizarPausadoAsync.
        public static async Task ActualizarStockAsync(string plu, int porciones)
        {
            await _candado.WaitAsync();
            try
            {
                string ruta = RutaArchivo();
                if (!File.Exists(ruta)) return;
                var lista = JsonSerializer.Deserialize<List<Plato>>(await File.ReadAllTextAsync(ruta), _json) ?? new List<Plato>();
                var item = lista.FirstOrDefault(p => string.Equals((p.Plu ?? "").Trim(), (plu ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
                if (item == null) return;
                item.Porciones = porciones;
                await File.WriteAllTextAsync(ruta, JsonSerializer.Serialize(lista, _json));
            }
            finally { _candado.Release(); }
        }

        // Pausa o reanuda un plato (fuera de carta sin tocar su stock).
        public static async Task ActualizarPausadoAsync(string plu, bool pausado)
        {
            string clave = (plu ?? "").Trim().ToUpperInvariant().Replace("PLU-", "");
            if (clave.Length == 0) throw new InvalidOperationException("El PLU es obligatorio.");
            await _candado.WaitAsync();
            try
            {
                string ruta = RutaArchivo();
                if (!File.Exists(ruta)) throw new InvalidOperationException("El plato ya no existe en el catálogo.");
                var lista = JsonSerializer.Deserialize<List<Plato>>(await File.ReadAllTextAsync(ruta), _json) ?? new List<Plato>();
                var actual = lista.FirstOrDefault(p => string.Equals((p.Plu ?? "").Trim(), clave, StringComparison.OrdinalIgnoreCase));
                if (actual == null) throw new InvalidOperationException("El plato ya no existe en el catálogo.");
                actual.Pausado = pausado;
                await File.WriteAllTextAsync(ruta, JsonSerializer.Serialize(lista, _json));
            }
            finally { _candado.Release(); }
        }
        // Siguiente correlativo numérico sobre el máximo existente (101..412 -> 413).
        public static async Task<string> SiguientePluAsync()
        {
            string ruta = RutaArchivo();
            int max = 100;
            if (File.Exists(ruta))
            {
                var lista = JsonSerializer.Deserialize<List<Plato>>(await File.ReadAllTextAsync(ruta), _json) ?? new List<Plato>();
                foreach (var p in lista)
                {
                    string digitos = new string((p.Plu ?? "").Where(char.IsDigit).ToArray());
                    if (int.TryParse(digitos, out int n) && n > max) max = n;
                }
            }
            return (max + 1).ToString();
        }

        // Normaliza para duplicados: mayúsculas, trim y espacios colapsados.
        private static string Normalizar(string texto) =>
            string.Join(" ", (texto ?? "").Trim().ToUpperInvariant().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));

        // 8 platos de muestra que hoy están fijos en el XAML de Productos.
        private static List<Plato> Semilla() => new()
        {
            new Plato { Plu = "101", Nombre = "Lomo Saltado Criollo", Categoria = "Carnes & Parrillas", EstacionTag = "caliente", Descripcion = "Receta: Lomo fino 220g, cebolla, tomate, papas amarillas", Precio = 42.00, Costo = 14.50, Porciones = 35 },
            new Plato { Plu = "102", Nombre = "Ceviche Clásico Mixto", Categoria = "Ceviches & Pescados", EstacionTag = "fria", Descripcion = "Pesca del día, pulpo, calamar, leche de tigre artesanal", Precio = 38.00, Costo = 12.00, Porciones = 18 },
            new Plato { Plu = "205", Nombre = "Pisco Sour Quebranta 4oz", Categoria = "Bebidas & Cócteles", EstacionTag = "bar", Descripcion = "Pisco Quebranta 4oz, limón de Chulucanas, jarabe de goma", Precio = 22.00, Costo = 4.50, Porciones = 80, Unidad = "copas" },
            new Plato { Plu = "108", Nombre = "Arroz con Mariscos Meloso", Categoria = "Ceviches & Pescados", EstacionTag = "caliente", Descripcion = "Insumo crítico: Langostino u15 (solo 4 porciones restantes)", Precio = 40.00, Costo = 15.00, Porciones = 4 },
            new Plato { Plu = "412", Nombre = "Suspiro a la Limeña", Categoria = "Postres Caseros", EstacionTag = "postres", Descripcion = "Sin stock de manjar de yemas en mise en place", Precio = 16.00, Costo = 3.20, Porciones = 0 },
            new Plato { Plu = "301", Nombre = "Cerveza Artesanal Cumbres IPA 330ml", Categoria = "Cervezas & Vinos", EstacionTag = "bar", Descripcion = "Lote L-2026-B con rotación FIFO al día", Precio = 18.00, Costo = 7.00, Porciones = 45, Unidad = "bot." },
            new Plato { Plu = "115", Nombre = "Fettuccine a la Huancaína con Lomo", Categoria = "Pastas Artesanales", EstacionTag = "caliente", Descripcion = "Pasta al huevo artesanal, salsa huancaína, trozos de lomo", Precio = 36.00, Costo = 11.50, Porciones = 22 },
            new Plato { Plu = "120", Nombre = "Causa Limeña Rellena de Pollo & Palta", Categoria = "Entradas", EstacionTag = "fria", Descripcion = "Papa amarilla prensada con ají amarillo y limón sutil", Precio = 20.00, Costo = 5.00, Porciones = 15 },
        };
    }
}
