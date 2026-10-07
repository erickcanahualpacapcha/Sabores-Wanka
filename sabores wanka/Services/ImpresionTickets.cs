using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using sabores_wanka.Models;

namespace sabores_wanka.Services
{
    // Impresion termica por driver Windows (patron Nachis PrinterService):
    // texto plano 80mm (43 columnas, Courier 7) a la impresora guardada
    // o a la predeterminada. Sin dialogos del sistema: directo al driver.
    public static class ImpresionTickets
    {
        private const int AnchoTicket = 43;
        private const string ClaveImpresora = "Impresora.Nombre";

        // Impresoras instaladas en Windows + cual es la predeterminada.
        public static (string predeterminada, List<string> todas) ListarImpresorasSistema()
        {
            var todas = new List<string>();
            string predeterminada = "";
            try
            {
                foreach (string nombre in PrinterSettings.InstalledPrinters)
                    todas.Add(nombre);
                predeterminada = new PrinterSettings().PrinterName ?? "";
            }
            catch { }
            return (predeterminada, todas);
        }

        // Impresora elegida en la tab Impresoras ("" = usar la de Windows).
        public static string LeerImpresora()
        {
            try
            {
                if (ApplicationData.Current.LocalSettings.Values[ClaveImpresora] is string n)
                    return n.Trim();
            }
            catch { }
            return "";
        }

        public static void GuardarImpresora(string nombre)
        {
            try { ApplicationData.Current.LocalSettings.Values[ClaveImpresora] = (nombre ?? "").Trim(); }
            catch { }
        }

        // Resuelve a que impresora ir: guardada (si sigue valida) o la de Windows.
        public static string ResolverImpresora()
        {
            string guardada = LeerImpresora();
            if (guardada.Length > 0 && EsValida(guardada)) return guardada;
            try { return new PrinterSettings().PrinterName ?? ""; }
            catch { return ""; }
        }

        private static bool EsValida(string nombre)
        {
            try
            {
                var doc = new PrintDocument { PrinterSettings = { PrinterName = nombre } };
                return doc.PrinterSettings.IsValid;
            }
            catch { return false; }
        }

        // Imprime texto plano; devuelve (ok, error legible para el dialogo).
        public static async Task<(bool ok, string error)> ImprimirTextoAsync(string texto, string? impresora = null)
        {
            string destino = string.IsNullOrWhiteSpace(impresora) ? ResolverImpresora() : impresora.Trim();
            if (destino.Length == 0)
                return (false, "No hay impresoras instaladas en Windows. Conecta la térmica e instálala primero.");
            if (!EsValida(destino))
            {
                var (_, todas) = ListarImpresorasSistema();
                string lista = todas.Count > 0 ? string.Join(", ", todas) : "ninguna";
                return (false, $"La impresora '{destino}' no está disponible. Instaladas: {lista}.");
            }

            bool terminado = false, conError = false;
            Exception? excepcion = null;
            var doc = new PrintDocument();
            doc.PrinterSettings.PrinterName = destino;
            doc.DefaultPageSettings.Margins = new Margins(5, 5, 5, 5);

            // Logo del negocio (si hay): se dibuja arriba en escala de grises.
            System.Drawing.Image? logo = CargarLogoGris(out float logoAncho, out float logoAlto);
            // QR a la web (si hay): se dibuja al final, centrado y con su
            // zona blanca; minimo ~38mm para que el celular lo lea.
            System.Drawing.Bitmap? qr = QrTicket.GenerarBitmap();
            const float QrLado = 150;

            doc.PrintPage += (_, e) =>
            {
                try
                {
                    if (e.Graphics == null) return;
                    using var fuente = FuenteMono(7);
                    float y = 5;
                    if (logo != null)
                    {
                        float x = (280 - logoAncho) / 2;
                        if (x < 5) x = 5;
                        e.Graphics.DrawImage(logo, x, y, logoAncho, logoAlto);
                        y += logoAlto + 6;
                    }
                    float alto = fuente.GetHeight(e.Graphics);
                    foreach (string linea in (texto ?? "").Replace("\r", "").Split('\n'))
                    {
                        string recorte = linea.Length > AnchoTicket ? linea.Substring(0, AnchoTicket) : linea;
                        e.Graphics.DrawString(recorte, fuente, System.Drawing.Brushes.Black, 5, y);
                        y += alto;
                    }
                    if (qr != null)
                    {
                        float qx = (280 - QrLado) / 2;
                        if (qx < 5) qx = 5;
                        e.Graphics.FillRectangle(System.Drawing.Brushes.White, qx - 4, y, QrLado + 8, QrLado + 8);
                        e.Graphics.DrawImage(qr, qx, y + 4, QrLado, QrLado);
                        y += QrLado + 12;
                    }
                    e.HasMorePages = false;
                }
                catch (Exception ex) { conError = true; excepcion = ex; }
            };
            doc.EndPrint += (_, e) =>
            {
                terminado = true;
                if (e.Cancel) conError = true;
            };

            await Task.Run(() =>
            {
                try { doc.Print(); }
                catch (Exception ex) { conError = true; excepcion = ex; }
            });

            var limite = DateTime.Now.AddSeconds(15);
            while (!terminado && !conError && DateTime.Now < limite)
                await Task.Delay(100);
            try { logo?.Dispose(); } catch { }
            try { qr?.Dispose(); } catch { }

            if (excepcion != null) return (false, $"Error al imprimir en '{destino}': {excepcion.Message}");
            if (!terminado || conError) return (false, $"La impresora '{destino}' no respondió. Revisa papel, conexión y que no esté en pausa.");
            return (true, "");
        }

        // Logo a gris termico: max 200 de ancho (~50mm), alto proporcional.
        private static System.Drawing.Image? CargarLogoGris(out float ancho, out float alto)
        {
            ancho = 0; alto = 0;
            try
            {
                string ruta = LogoTicket.RutaActual();
                if (ruta.Length == 0) return null;
                using var original = System.Drawing.Image.FromFile(ruta);
                float escala = original.Width > 200 ? 200f / original.Width : 1f;
                ancho = original.Width * escala;
                alto = original.Height * escala;
                if (alto > 90) { escala = 90f / alto; ancho *= escala; alto = 90; }
                var gris = new System.Drawing.Bitmap((int)ancho, (int)alto);
                using (var g = System.Drawing.Graphics.FromImage(gris))
                {
                    var matriz = new System.Drawing.Imaging.ColorMatrix(new float[][]
                    {
                        new float[] { 0.3f, 0.3f, 0.3f, 0, 0 },
                        new float[] { 0.59f, 0.59f, 0.59f, 0, 0 },
                        new float[] { 0.11f, 0.11f, 0.11f, 0, 0 },
                        new float[] { 0, 0, 0, 1, 0 },
                        new float[] { 0, 0, 0, 0, 1 },
                    });
                    using var atributos = new System.Drawing.Imaging.ImageAttributes();
                    atributos.SetColorMatrix(matriz);
                    g.DrawImage(original, new System.Drawing.Rectangle(0, 0, gris.Width, gris.Height),
                        0, 0, original.Width, original.Height, System.Drawing.GraphicsUnit.Pixel, atributos);
                }
                return gris;
            }
            catch { return null; }
        }

        private static System.Drawing.Font FuenteMono(float tamano)
        {
            try { return new System.Drawing.Font("Courier New", tamano); }
            catch { return new System.Drawing.Font(System.Drawing.FontFamily.GenericMonospace, tamano); }
        }

        // Ticket de prueba 80mm con los datos del negocio (misma demo del preview).
        public static string TextoPrueba()
        {
            var (nombre, ruc, telefono, direccion, mensaje) = DatosNegocio();
            var t = new List<string>
            {
                Centrar(nombre.ToUpperInvariant()),
                Centrar("RUC: " + ruc),
                Centrar(direccion),
                Centrar("Tel: " + telefono),
                new string('-', AnchoTicket),
                Centrar("TICKET DE PRUEBA"),
                Centrar(DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
                new string('-', AnchoTicket),
                Fila("C", "PRODUCTO", "P.U.", "TOTAL"),
                Fila("1", "Pachamanca 2 Sabores", "32.00", "32.00"),
                Fila("1", "Papa a la Huancaina", "14.00", "14.00"),
                Fila("1", "Chicha de Jora 1L", "12.00", "12.00"),
                Fila("1", "Cancha Serrana", "4.00", "4.00"),
                new string('-', AnchoTicket),
                $"TOTAL:                     S/ 62.00",
                new string('-', AnchoTicket),
                Centrar(mensaje),
                "",
            };
            if (QrTicket.LeerWeb().Length > 0)
                t.Insert(t.Count - 2, Centrar("Escanea el QR para visitarnos"));
            return string.Join("\n", t);
        }

        // Ticket de una venta real del historial (pagada o por cobrar).
        public static string TextoVenta(Venta v)
        {
            var (nombre, ruc, telefono, direccion, mensaje) = DatosNegocio();
            var t = new List<string>
            {
                Centrar(nombre.ToUpperInvariant()),
                Centrar("RUC: " + ruc),
                Centrar(direccion),
                Centrar("Tel: " + telefono),
                new string('-', AnchoTicket),
                Centrar($"{v.Comprobante.ToUpperInvariant()} {v.Ticket}"),
                Centrar(v.FechaHora.ToString("dd/MM/yyyy HH:mm")),
                Centrar($"{v.MesaEtiqueta} | {v.Cliente}"),
                new string('-', AnchoTicket),
            };
            foreach (var it in v.Items ?? new List<ItemVenta>())
            {
                string prod = it.Nombre.Length > 22 ? it.Nombre.Substring(0, 22) : it.Nombre;
                t.Add(Fila(it.Cantidad.ToString(), prod, it.Precio.ToString("0.00"), (it.Precio * it.Cantidad).ToString("0.00")));
            }
            t.Add(new string('-', AnchoTicket));
            t.Add($"TOTAL:                S/ {v.Total:0.00}");
            t.Add($"Pago: {v.MetodoPago} | {v.Estado}");
            t.Add(new string('-', AnchoTicket));
            if (QrTicket.LeerWeb().Length > 0)
                t.Add(Centrar("Escanea el QR para visitarnos"));
            t.Add(Centrar(mensaje));
            t.Add("");
            return string.Join("\n", t);
        }

        // Lee los mismos LocalSettings que la vista de negocio (una sola fuente).
        private static (string nombre, string ruc, string telefono, string direccion, string mensaje) DatosNegocio()
        {
            string Leer(string clave, string defecto)
            {
                try
                {
                    if (ApplicationData.Current.LocalSettings.Values[clave] is string s && !string.IsNullOrWhiteSpace(s))
                        return s.Trim();
                }
                catch { }
                return defecto;
            }
            return (
                Leer("Negocio.Nombre", "SABORES WANKA"),
                Leer("Negocio.Ruc", "----------"),
                Leer("Negocio.Telefono", "-"),
                Leer("Negocio.Direccion", "Huancayo"),
                Leer("Negocio.Mensaje", "Gracias por su visita."));
        }

        private static string Centrar(string texto)
        {
            texto = (texto ?? "").Trim();
            if (texto.Length >= AnchoTicket) return texto.Substring(0, AnchoTicket);
            int izq = (AnchoTicket - texto.Length) / 2;
            return new string(' ', izq) + texto;
        }

        private static string Fila(string cant, string producto, string pu, string total)
        {
            // C(2) + producto(22) + P.U.(8) + total(8) = 43 columnas.
            return Recorte(cant.PadRight(2), 2) + " "
                + Recorte(producto.PadRight(22), 22) + " "
                + Recorte(pu.PadLeft(8), 8) + " "
                + Recorte(total.PadLeft(8), 8);
        }

        private static string Recorte(string texto, int ancho)
        {
            texto ??= "";
            if (texto.Length == ancho) return texto;
            return texto.Length > ancho ? texto.Substring(0, ancho) : texto.PadRight(ancho);
        }
    }
}
