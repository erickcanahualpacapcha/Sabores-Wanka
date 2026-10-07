using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using sabores_wanka.Models;
using sabores_wanka.Services;

namespace sabores_wanka.Views.HistorialDeVentas
{
    // Detalle de venta 3.0 (replica de detalles de venta/code.html):
    // vista flotante de solo lectura para boletas PAGADAS y POR COBRAR.
    // Se precarga con Cargar(venta) y se cierra con CerrarSolicitado.
    public sealed partial class VentaDetalleView : UserControl
    {
        private Venta? _venta;

        // Arrastre de la ventana flotante (igual que 2.0_editar_venta).
        private bool _moviendo;
        private Windows.Foundation.Point _agarre;
        private double _baseX;
        private double _baseY;

        // Avisa al historial para ocultar el overlay (no recarga: no edita nada).
        public event EventHandler? CerrarSolicitado;

        public VentaDetalleView()
        {
            this.InitializeComponent();
        }

        // Precarga la boleta en la vista; llamar antes de mostrar.
        public void Cargar(Venta venta)
        {
            _venta = venta;
            bool pagada = venta.EsPagada;

            TituloTicketText.Text = "#" + venta.Ticket;
            EstadoBadgeText.Text = (venta.Estado ?? "").Trim().ToUpperInvariant();
            string fondo = pagada ? "#10b98133" : "#fb923c26";
            string tinta = pagada ? "#10b981" : "#fb923c";
            EstadoBadge.Background = Pincel(fondo);
            EstadoBadgeText.Foreground = Pincel(tinta);

            string comp = string.IsNullOrWhiteSpace(venta.Comprobante) ? "Boleta" : venta.Comprobante.Trim();
            SubtipoText.Text = string.Equals(comp, "Factura", StringComparison.OrdinalIgnoreCase)
                ? "Factura Electrónica de Venta - Sabores Wanka"
                : $"{comp} de Venta - Sabores Wanka";
            FechaText.Text = venta.FechaHora.ToString("dd/MM/yyyy HH:mm");

            MesaText.Text = string.IsNullOrWhiteSpace(venta.MesaEtiqueta) ? "-" : venta.MesaEtiqueta.ToUpperInvariant();
            MozoText.Text = string.IsNullOrWhiteSpace(venta.Mozo) ? "Mostrador" : venta.Mozo;
            ClienteText.Text = string.IsNullOrWhiteSpace(venta.Cliente) ? "Cliente Ocasional" : venta.Cliente;
            MetodoText.Text = string.IsNullOrWhiteSpace(venta.MetodoPago) ? "—" : venta.MetodoPago;

            var items = venta.Items ?? new List<ItemVenta>();
            ConteoText.Text = $"{items.Count} artículo{(items.Count == 1 ? "" : "s")}";
            ItemsPanel.Children.Clear();
            foreach (var item in items)
                ItemsPanel.Children.Add(FilaItem(item));

            // Desglose IGV incluido en el total (total = gravada + 18%).
            double total = venta.Total;
            double subtotal = Math.Round(total / 1.18, 2);
            double igv = Math.Round(total - subtotal, 2);
            SubtotalText.Text = $"S/ {subtotal:0.00}";
            IgvText.Text = $"S/ {igv:0.00}";
            TotalText.Text = $"S/ {total:0.00}";

            // Panel de cobro: a credito muestra lo pendiente, pagada lo cobrado.
            if (pagada)
            {
                CobroTituloText.Text = "COBRADO";
                DateTime cuando = venta.FechaCobro ?? venta.FechaHora;
                CobroDetalleText.Text = $"{MetodoText.Text} · {cuando:dd/MM/yyyy HH:mm}";
                CobroMontoText.Text = $"S/ {total:0.00}";
                CobroMontoText.Foreground = Pincel("#10b981");
                NotaText.Text = "Comprobante emitido correctamente.";
            }
            else
            {
                CobroTituloText.Text = "PENDIENTE DE COBRO";
                CobroDetalleText.Text = $"Venta a crédito · {venta.MetodoPago}";
                CobroMontoText.Text = $"S/ {total:0.00}";
                CobroMontoText.Foreground = Pincel("#fb923c");
                NotaText.Text = "Venta a crédito pendiente de cobro.";
            }
        }

        // Fila de item: cantidad, nombre, precio unitario e importe.
        private static Grid FilaItem(ItemVenta item)
        {
            var fila = new Grid { Padding = new Thickness(12, 10, 12, 10) };
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });

            fila.Children.Add(new TextBlock { Text = $"{item.Cantidad}x", Foreground = new SolidColorBrush(Colors.White), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            var nombre = new TextBlock { Text = item.Nombre, Foreground = new SolidColorBrush(Colors.White), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(nombre, 1);
            fila.Children.Add(nombre);
            var unit = new TextBlock { Text = $"S/ {item.Precio:0.00}", Foreground = Pincel("#9ca3af"), FontSize = 11, FontFamily = new FontFamily("Consolas"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(unit, 2);
            fila.Children.Add(unit);
            var importe = new TextBlock { Text = $"S/ {item.Precio * item.Cantidad:0.00}", Foreground = new SolidColorBrush(Colors.White), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontFamily = new FontFamily("Consolas"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(importe, 3);
            fila.Children.Add(importe);
            return fila;
        }

        // Reimprimir: dialogo informativo (igual que AccionImprimir del historial).
        private async void ReimprimirButton_Click(object sender, RoutedEventArgs e)
        {
            if (_venta == null) return;
            await MostrarDialogoAsync($"Imprimir {_venta.Ticket}", "Comprobante enviado a la impresora predeterminada.", "Entendido");
        }

        // Copiar: misma factura estilo Nachis del boton Copiar del historial.
        private async void CopiarButton_Click(object sender, RoutedEventArgs e)
        {
            if (_venta == null) return;
            string mensaje = WhatsappEnlace.TextoFactura(_venta);
            if (!await WhatsappEnlace.CopiarFacturaAlPortapapelesAsync(mensaje))
            {
                await MostrarDialogoAsync("No se pudo copiar", "No fue posible copiar la factura al portapapeles.", "Entendido");
                return;
            }
            await MostrarDialogoAsync("Factura copiada", "Puedes pegarla manualmente en WhatsApp o en una carpeta.", "Entendido");
        }

        private void CerrarButton_Click(object sender, RoutedEventArgs e) =>
            CerrarSolicitado?.Invoke(this, EventArgs.Empty);

        // Mover la ventana: la cabecera captura el puntero (igual que 2.0_gastos).
        private void ModalMover_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is not FrameworkElement zona || ModalDesplazar == null) return;
            _moviendo = true;
            _agarre = e.GetCurrentPoint(null).Position;
            _baseX = ModalDesplazar.X;
            _baseY = ModalDesplazar.Y;
            zona.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void ModalMover_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!_moviendo || ModalDesplazar == null) return;
            var pos = e.GetCurrentPoint(null).Position;
            ModalDesplazar.X = _baseX + (pos.X - _agarre.X);
            ModalDesplazar.Y = _baseY + (pos.Y - _agarre.Y);
            e.Handled = true;
        }

        private void ModalMover_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _moviendo = false;
            if (sender is FrameworkElement zona) zona.ReleasePointerCapture(e.Pointer);
        }

        // Convierte #RRGGBB o #RRGGBBAA (CSS) a pincel; nunca lanza.
        private static SolidColorBrush Pincel(string hex)
        {
            try
            {
                hex = (hex ?? "").Trim().TrimStart('#');
                byte a = 0xFF, r, g, b;
                if (hex.Length == 8)
                {
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                    a = Convert.ToByte(hex.Substring(6, 2), 16);
                }
                else if (hex.Length == 6)
                {
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                }
                else return new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x9C, 0xA3, 0xAF));
                return new SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
            }
            catch
            {
                return new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x9C, 0xA3, 0xAF));
            }
        }

        private async System.Threading.Tasks.Task MostrarDialogoAsync(string titulo, string mensaje, string primario = "Aceptar", string? secundario = null)
        {
            var dialogo = new ContentDialog
            {
                Title = titulo,
                Content = new TextBlock { Text = mensaje, TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = primario,
                XamlRoot = this.XamlRoot
            };
            if (secundario != null) dialogo.CloseButtonText = secundario;
            await dialogo.ShowAsync();
        }
    }
}
