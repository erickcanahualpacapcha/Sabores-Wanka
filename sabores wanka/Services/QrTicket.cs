using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;
using QRCoder;

namespace sabores_wanka.Services
{
    // QR del ticket hacia la pagina web (se genera local, sin internet).
    // La URL vive en LocalSettings "Negocio.Web"; vacio = sin QR.
    // En termica se dibuja con zona blanca y minimo ~38mm para que escanee.
    public static class QrTicket
    {
        private const string Clave = "Negocio.Web";

        // URL guardada, normalizada con https:// ("" si no hay).
        public static string LeerWeb()
        {
            try
            {
                if (ApplicationData.Current.LocalSettings.Values[Clave] is string w && !string.IsNullOrWhiteSpace(w))
                    return Normalizar(w);
            }
            catch { }
            return "";
        }

        public static void GuardarWeb(string url)
        {
            try { ApplicationData.Current.LocalSettings.Values[Clave] = (url ?? "").Trim(); }
            catch { }
        }

        public static string Normalizar(string url)
        {
            url = (url ?? "").Trim();
            if (url.Length == 0) return "";
            if (!url.Contains("://")) url = "https://" + url;
            return url;
        }

        // Bitmap para la impresora termica (null si no hay URL; nunca lanza).
        public static System.Drawing.Bitmap? GenerarBitmap(string? url = null)
        {
            try
            {
                string destino = Normalizar(url ?? LeerWeb());
                if (destino.Length == 0) return null;
                using var generador = new QRCodeGenerator();
                using var datos = generador.CreateQrCode(destino, QRCodeGenerator.ECCLevel.M);
                using var qr = new QRCode(datos);
                return qr.GetGraphic(5, System.Drawing.Color.Black, System.Drawing.Color.White, drawQuietZones: true);
            }
            catch { return null; }
        }

        // Imagen para el preview XAML (null si no hay URL; nunca lanza).
        public static async Task<BitmapImage?> GenerarParaPreviewAsync(string? url = null)
        {
            try
            {
                using var bmp = GenerarBitmap(url ?? LeerWeb());
                if (bmp == null) return null;
                using var memoria = new MemoryStream();
                bmp.Save(memoria, System.Drawing.Imaging.ImageFormat.Png);
                memoria.Position = 0;
                var imagen = new BitmapImage();
                using var flujo = memoria.AsRandomAccessStream();
                await imagen.SetSourceAsync(flujo);
                return imagen;
            }
            catch { return null; }
        }
    }
}
