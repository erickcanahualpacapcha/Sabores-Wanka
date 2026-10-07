using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.UI.Xaml.Media.Imaging;

namespace sabores_wanka.Services
{
    // Fotos de platos en disco local (patrón Nachis: Images/Productos con guid).
    // Se copian al guardar el formulario; el formulario solo previsualiza.
    public static class FotosPlatos
    {
        private static readonly string[] Extensiones = { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };

        // Carpeta local: %LocalAppData%\SaboresWanka\Imagenes\Platos.
        public static string DirFotos()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaboresWanka", "Imagenes", "Platos");
            Directory.CreateDirectory(dir);
            return dir;
        }

        // Copia la foto elegida con nombre único por PLU; borra la anterior si
        // era nuestra. Devuelve la ruta destino para guardar en Plato.FotoPath.
        public static async Task<string> GuardarAsync(string origenPath, string plu, string? anteriorRuta)
        {
            if (string.IsNullOrWhiteSpace(origenPath) || !File.Exists(origenPath))
                throw new InvalidOperationException("La imagen elegida ya no existe.");
            string ext = Path.GetExtension(origenPath).ToLowerInvariant();
            if (!Extensiones.Contains(ext))
                throw new InvalidOperationException("Formato no soportado. Usa JPG, PNG o WebP.");
            string dir = DirFotos();
            string destino = Path.Combine(dir, $"{SanearPlu(plu)}_{Guid.NewGuid().ToString("N").Substring(0, 8)}{ext}");
            await Task.Run(() => File.Copy(origenPath, destino, true));
            BorrarAnterior(anteriorRuta, destino);
            return destino;
        }

        // Carga un bitmap desde disco (null si no hay o falla; nunca lanza).
        public static async Task<BitmapImage?> CargarAsync(string? ruta)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta)) return null;
                StorageFile archivo = await StorageFile.GetFileFromPathAsync(ruta);
                var bmp = new BitmapImage();
                using var flujo = await archivo.OpenReadAsync();
                await bmp.SetSourceAsync(flujo);
                return bmp;
            }
            catch { return null; }
        }

        private static void BorrarAnterior(string? anteriorRuta, string destino)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(anteriorRuta)) return;
                if (string.Equals(Path.GetFullPath(anteriorRuta), Path.GetFullPath(destino), StringComparison.OrdinalIgnoreCase)) return;
                BorrarSiEsNuestra(anteriorRuta);
            }
            catch { }
        }

        // Borra un archivo solo si vive en nuestra carpeta (nunca toca origen del usuario).
        public static void BorrarSiEsNuestra(string? ruta)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta)) return;
                if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(ruta))?.TrimEnd(Path.DirectorySeparatorChar), DirFotos().TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) return;
                File.Delete(ruta);
            }
            catch { }
        }

        private static string SanearPlu(string plu)
        {
            string limpio = new string((plu ?? "").Where(c => char.IsLetterOrDigit(c)).ToArray());
            return string.IsNullOrEmpty(limpio) ? "plato" : limpio.ToUpperInvariant();
        }
    }
}
