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
    // Linea del ticket con estilo propio (la termica solo imprime negro,
    // asi que el enfasis se logra con tamano + negrita, no con color).
    public enum EstiloTicket { Normal, Negrita, Titulo, Total }

    public sealed class LineaTicket
    {
        public string Texto { get; set; } = "";
        public EstiloTicket Estilo { get; set; } = EstiloTicket.Normal;
    }

    // Impresion termica por driver Windows (patron Nachis PrinterService):
    // texto 80mm a la impresora guardada o a la predeterminada.
    // Cuerpo en Courier 9, titulos y totales en grande + negrita.
    public static class ImpresionTickets
    {
        private const int AnchoTicket = 40;
        // Capacidad real por tamano (Courier fijo): titulo 14pt = 23 col,
        // total 11pt = 29 col. Sin esto las letras grandes se cortan a la
        // derecha (asi se perdia el TOTAL en el papel).
        private const int CapTitulo = 23;
        private const int CapTotal = 29;
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

        // Imprime las lineas; devuelve (ok, error legible para el dialogo).
        public static async Task<(bool ok, string error)> ImprimirTextoAsync(List<LineaTicket> lineas, string? impresora = null)
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
                    using var normal = FuenteMono(8, false);
                    using var negrita = FuenteMono(8, true);
                    using var titulo = FuenteMono(14, true);
                    using var total = FuenteMono(11, true);
                    float y = 5;
                    if (logo != null)
                    {
                        float x = (280 - logoAncho) / 2;
                        if (x < 5) x = 5;
                        e.Graphics.DrawImage(logo, x, y, logoAncho, logoAlto);
                        y += logoAlto + 8;
                    }
                    foreach (var linea in lineas ?? new List<LineaTicket>())
                    {
                        var fuente = linea.Estilo switch
                        {
                            EstiloTicket.Titulo => titulo,
                            EstiloTicket.Total => total,
                            EstiloTicket.Negrita => negrita,
                            _ => normal,
                        };
                        string recorte = (linea.Texto ?? "").Replace("\r", "");
                        if (recorte.Length > AnchoTicket) recorte = recorte.Substring(0, AnchoTicket);
                        e.Graphics.DrawString(recorte, fuente, System.Drawing.Brushes.Black, 5, y);
                        y += fuente.GetHeight(e.Graphics) + 1;
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

        private static System.Drawing.Font FuenteMono(float tamano, bool negrita)
        {
            var estilo = negrita ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular;
            try { return new System.Drawing.Font("Courier New", tamano, estilo); }
            catch { return new System.Drawing.Font(System.Drawing.FontFamily.GenericMonospace, tamano, estilo); }
        }

        // Logo a blanco y negro puro (igual que el QR): la termica apaga
        // los grises, asi que se umbraliza. Max 240 de ancho (~60mm).
        private static System.Drawing.Image? CargarLogoGris(out float ancho, out float alto)
        {
            ancho = 0; alto = 0;
            try
            {
                string ruta = LogoTicket.RutaActual();
                if (ruta.Length == 0) return null;
                using var original = System.Drawing.Image.FromFile(ruta);
                float escala = original.Width > 240 ? 240f / original.Width : 1f;
                ancho = original.Width * escala;
                alto = original.Height * escala;
                if (alto > 110) { escala = 110f / alto; ancho *= escala; alto = 110; }
                var mono = new System.Drawing.Bitmap((int)ancho, (int)alto);
                using (var g = System.Drawing.Graphics.FromImage(mono))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(original, 0, 0, mono.Width, mono.Height);
                }
                // Umbral: cada pixel pasa a negro o blanco puro (sin grises).
                for (int y = 0; y < mono.Height; y++)
                {
                    for (int x = 0; x < mono.Width; x++)
                    {
                        var p = mono.GetPixel(x, y);
                        int luz = (int)(p.R * 0.299 + p.G * 0.587 + p.B * 0.114);
                        mono.SetPixel(x, y, luz < 140 ? System.Drawing.Color.Black : System.Drawing.Color.White);
                    }
                }
                return mono;
            }
            catch { return null; }
        }

        // Ticket de prueba 80mm con los datos del negocio (misma demo del preview).
        public static List<LineaTicket> TextoPrueba()
        {
            var (nombre, ruc, telefono, direccion, mensaje) = DatosNegocio();
            var t = new List<LineaTicket>
            {
                L(Centrar(nombre.ToUpperInvariant(), CapTitulo), EstiloTicket.Titulo),
                L("RUC: " + ruc),
                L(direccion),
                L("Tel: " + telefono),
                L(new string('-', AnchoTicket)),
                L("TICKET DE PRUEBA", EstiloTicket.Negrita),
                L(DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
                L(new string('-', AnchoTicket)),
                L(Fila("C", "PRODUCTO", "P.U.", "TOTAL"), EstiloTicket.Negrita),
                L(Fila("1", "Pachamanca 2 Sabores", "32.00", "32.00")),
                L(Fila("1", "Papa a la Huancaina", "14.00", "14.00")),
                L(Fila("1", "Chicha de Jora 1L", "12.00", "12.00")),
                L(Fila("1", "Cancha Serrana", "4.00", "4.00")),
                L(new string('-', AnchoTicket)),
                L(TotalLinea("S/ 62.00"), EstiloTicket.Total),
                L(new string('-', AnchoTicket)),
            };
            if (QrTicket.HayQr())
                t.Add(L(Centrar("Escanea el QR para visitarnos"), EstiloTicket.Negrita));
            t.Add(L(Centrar(mensaje), EstiloTicket.Negrita));
            t.Add(L(""));
            return t;
        }

        // Ticket de una venta real del historial (pagada o por cobrar).
        public static List<LineaTicket> TextoVenta(Venta v)
        {
            var (nombre, ruc, telefono, direccion, mensaje) = DatosNegocio();
            var t = new List<LineaTicket>
            {
                L(Centrar(nombre.ToUpperInvariant(), CapTitulo), EstiloTicket.Titulo),
                L("RUC: " + ruc),
                L(direccion),
                L("Tel: " + telefono),
                L(new string('-', AnchoTicket)),
                L($"{v.Comprobante.ToUpperInvariant()} {v.Ticket}", EstiloTicket.Negrita),
                L(v.FechaHora.ToString("dd/MM/yyyy HH:mm")),
                L($"{v.MesaEtiqueta} | {v.Cliente}"),
                L(new string('-', AnchoTicket)),
            };
            foreach (var it in v.Items ?? new List<ItemVenta>())
            {
                string prod = it.Nombre.Length > 20 ? it.Nombre.Substring(0, 20) : it.Nombre;
                t.Add(L(Fila(it.Cantidad.ToString(), prod, it.Precio.ToString("0.00"), (it.Precio * it.Cantidad).ToString("0.00"))));
            }
            t.Add(L(new string('-', AnchoTicket)));
            t.Add(L(TotalLinea($"S/ {v.Total:0.00}"), EstiloTicket.Total));
            t.Add(L($"Pago: {v.MetodoPago} | {v.Estado}", EstiloTicket.Negrita));
            t.Add(L(new string('-', AnchoTicket)));
            if (QrTicket.HayQr())
                t.Add(L(Centrar("Escanea el QR para visitarnos"), EstiloTicket.Negrita));
            t.Add(L(Centrar(mensaje), EstiloTicket.Negrita));
            t.Add(L(""));
            return t;
        }

        private static LineaTicket L(string texto, EstiloTicket estilo = EstiloTicket.Normal) =>
            new() { Texto = texto ?? "", Estilo = estilo };

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

        // Total alineado a la derecha dentro de su capacidad (29 col a 11pt).
        private static string TotalLinea(string monto) =>
            "TOTAL:" + monto.PadLeft(CapTotal - 6);

        private static string Centrar(string texto) => Centrar(texto, AnchoTicket);

        private static string Centrar(string texto, int capacidad)
        {
            texto = (texto ?? "").Trim();
            if (texto.Length >= capacidad) return texto.Substring(0, capacidad);
            int izq = (capacidad - texto.Length) / 2;
            return new string(' ', izq) + texto;
        }

        private static string Fila(string cant, string producto, string pu, string total)
        {
            // C(2) + producto(20) + P.U.(7) + total(8) = 40 columnas.
            return Recorte(cant.PadRight(2), 2) + " "
                + Recorte(producto.PadRight(20), 20) + " "
                + Recorte(pu.PadLeft(7), 7) + " "
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
