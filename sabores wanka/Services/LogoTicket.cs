using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.UI.Xaml.Media.Imaging;

namespace sabores_wanka.Services
{
    // Logo del ticket en disco local (mismo patron que FotosPlatos).
    // Un solo archivo fijo; al cambiarlo se reemplaza. La ruta vive en
    // LocalSettings "Negocio.LogoPath" y lo usan el preview y la impresion.
    public static class LogoTicket
    {
        private const string Clave = "Negocio.LogoPath";
        private static readonly string[] Extensiones = { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };

        // Carpeta local: %LocalAppData%\SaboresWanka\Imagenes.
        public static string DirLogo()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaboresWanka", "Imagenes");
            Directory.CreateDirectory(dir);
            return dir;
        }

        // Guarda el logo elegido como logo_ticket.<ext>; devuelve la ruta.
        public static async Task<string> GuardarAsync(string origenPath)
        {
            if (string.IsNullOrWhiteSpace(origenPath) || !File.Exists(origenPath))
                throw new InvalidOperationException("La imagen elegida ya no existe.");
            string ext = Path.GetExtension(origenPath).ToLowerInvariant();
            if (!Extensiones.Contains(ext))
                throw new InvalidOperationException("Formato no soportado. Usa JPG, PNG o WebP.");
            string anterior = RutaActual();
            string destino = Path.Combine(DirLogo(), "logo_ticket" + ext);
            await Task.Run(() => File.Copy(origenPath, destino, true));
            try
            {
                if (!string.IsNullOrWhiteSpace(anterior) && !string.Equals(Path.GetFullPath(anterior), Path.GetFullPath(destino), StringComparison.OrdinalIgnoreCase) && File.Exists(anterior))
                    File.Delete(anterior);
            }
            catch { }
            try { ApplicationData.Current.LocalSettings.Values[Clave] = destino; }
            catch { }
            return destino;
        }

        // Ruta guardada si el archivo sigue existiendo ("" si no hay logo).
        public static string RutaActual()
        {
            try
            {
                if (ApplicationData.Current.LocalSettings.Values[Clave] is string r && !string.IsNullOrWhiteSpace(r) && File.Exists(r))
                    return r;
            }
            catch { }
            return "";
        }

        // Bitmap para el preview XAML (null si no hay; nunca lanza).
        // El stream queda retenido por la imagen (no se libera aqui).
        public static async Task<BitmapImage?> CargarAsync()
        {
            try
            {
                string ruta = RutaActual();
                if (ruta.Length == 0) return null;
                StorageFile archivo = await StorageFile.GetFileFromPathAsync(ruta);
                using var neto = await archivo.OpenReadAsync();
                var flujo = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                await Windows.Storage.Streams.RandomAccessStream.CopyAsync(neto, flujo);
                flujo.Seek(0);
                var bmp = new BitmapImage();
                await bmp.SetSourceAsync(flujo);
                return bmp;
            }
            catch { return null; }
        }
    }
}
