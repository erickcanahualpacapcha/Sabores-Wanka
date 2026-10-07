using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using sabores_wanka.Models;

namespace sabores_wanka.Services
{
    // Canal WhatsApp sin motor ni bot (a diferencia de Nachis, sin Node/Baileys):
    // abre WhatsApp (app o web) con el mensaje del comprobante ya redactado.
    // Solo se guarda el número destino local; no hay estados de envío.
    public static class WhatsappEnlace
    {
        // Abre WhatsApp con el texto listo (al número guardado o a elegir contacto).
        public static async Task<bool> EnviarTextoAsync(string mensaje)
        {
            if (string.IsNullOrWhiteSpace(mensaje)) return false;
            string numero = await LeerNumeroAsync();
            string destino = numero.Length > 0 ? $"https://wa.me/{numero}?text=" : "https://wa.me/?text=";
            try
            {
                return await Windows.System.Launcher.LaunchUriAsync(new Uri(destino + Uri.EscapeDataString(mensaje)));
            }
            catch { return false; }
        }

        // Texto plano de la boleta para el cliente (sin formato rico).
        public static string TextoBoleta(Venta v) => TextoFactura(v);

        // Factura estilo Nachis (BuildDebtMessage): mismo encabezado, totales
        // con cultura es-PE y linea de estado pagada/pendiente. El detalle de
        // items se incluye porque Wanka no genera imagen adjunta como Nachis.
        public static string TextoFactura(Venta v)
        {
            var cultura = new System.Globalization.CultureInfo("es-PE");
            string nombre = string.IsNullOrWhiteSpace(v.Cliente) ? "cliente" : v.Cliente.Trim();
            double pendiente = v.EsPagada ? 0 : v.Total;
            var lineas = new System.Collections.Generic.List<string>
            {
                $"Hola {nombre}, te compartimos tu factura #{v.Ticket}.",
                $"Fecha: {v.FechaHora:dd/MM/yyyy HH:mm}",
                $"Mesa: {v.MesaEtiqueta} | Mozo: {v.Mozo}",
                $"Comprobante: {v.Comprobante} | Pago: {v.MetodoPago}",
                "Detalle:",
            };
            lineas.AddRange(v.Items.Select(i => $"- {i.Nombre} x{i.Cantidad}  S/ {(i.Precio * i.Cantidad).ToString("N2", cultura)}"));
            lineas.Add($"Total: S/ {v.Total.ToString("N2", cultura)}");
            lineas.Add($"Pendiente: S/ {pendiente.ToString("N2", cultura)}");
            lineas.Add(v.EsPagada ? "ESTA FACTURA ESTA COMPLETAMENTE PAGADA." : $"DEUDA PENDIENTE: S/ {pendiente.ToString("N2", cultura)}");
            lineas.Add("Gracias por tu compra.");
            return string.Join("\n", lineas);
        }

        // Copia la factura al portapapeles (patron Nachis CopyToClipboardAsync,
        // solo texto: Wanka no genera imagen/PDF adjunto). Devuelve false si falla.
        public static async Task<bool> CopiarFacturaAlPortapapelesAsync(string mensaje)
        {
            if (string.IsNullOrWhiteSpace(mensaje)) return false;
            try
            {
                var paquete = new Windows.ApplicationModel.DataTransfer.DataPackage();
                paquete.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
                paquete.SetText(mensaje);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(paquete);
                Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
                await Task.CompletedTask;
                return true;
            }
            catch { return false; }
        }

        private static string RutaArchivo()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaboresWanka", "Data");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "whatsapp.json");
        }

        // Número destino (solo dígitos con código país, ej. 51987654321; "" = elegir).
        public static async Task<string> LeerNumeroAsync()
        {
            try
            {
                string ruta = RutaArchivo();
                if (!File.Exists(ruta)) return "";
                using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(ruta));
                if (doc.RootElement.TryGetProperty("numero", out var n)) return Normalizar(n.GetString() ?? "");
                return "";
            }
            catch { return ""; }
        }

        public static async Task GuardarNumeroAsync(string numero)
        {
            string limpio = Normalizar(numero);
            if (limpio.Length > 0 && (limpio.Length < 9 || limpio.Length > 15))
                throw new InvalidOperationException("Número inválido. Usa solo dígitos con código de país (ej. 51987654321).");
            await File.WriteAllTextAsync(RutaArchivo(), JsonSerializer.Serialize(new { numero = limpio }));
        }

        private static string Normalizar(string numero) =>
            new string((numero ?? "").Where(char.IsDigit).ToArray());
    }
}
