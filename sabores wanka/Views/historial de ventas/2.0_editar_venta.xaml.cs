using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using sabores_wanka.Models;
using sabores_wanka.Services;

namespace sabores_wanka.Views.HistorialDeVentas
{
    // Edicion de boleta cerrada (patron Nachis adaptado: la venta se carga en
    // un formulario precargado y se actualiza por ticket, sin ir al POS).
    // Solo datos e items: el Estado nunca cambia aqui (cobrar = boton Cobrar).
    public sealed partial class VentaEdicionView : UserControl
    {
        // Fila editable en memoria (copia de ItemVenta para no tocar el JSON).
        private sealed class ItemEdit
        {
            public string Nombre { get; set; } = "";
            public double Precio { get; set; }
            public int Cantidad { get; set; }
        }

        private readonly List<ItemEdit> _items = new();
        private Dictionary<string, int> _cantidadesOriginales = new(StringComparer.OrdinalIgnoreCase);
        private Venta? _original;
        private string _comprobante = "Nota";
        private string _metodo = "Efectivo";

        // Arrastre de la ventana flotante (igual que 2.0_gastos).
        private bool _moviendo;
        private Windows.Foundation.Point _agarre;
        private double _baseX;
        private double _baseY;

        // Avisa al historial para ocultar el overlay y recargar (no navega solo).
        public event EventHandler? CerrarSolicitado;

        public VentaEdicionView()
        {
            this.InitializeComponent();
        }

        // Precarga la boleta en el formulario; llamar antes de mostrar.
        public void Cargar(Venta venta)
        {
            _original = venta;
            _cantidadesOriginales = (venta.Items ?? new List<ItemVenta>())
                .GroupBy(i => i.Nombre ?? "")
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Cantidad), StringComparer.OrdinalIgnoreCase);
            _items.Clear();
            foreach (var it in venta.Items ?? new List<ItemVenta>())
                _items.Add(new ItemEdit { Nombre = it.Nombre, Precio = it.Precio, Cantidad = it.Cantidad });

            TicketBadgeText.Text = venta.Ticket;
            EstadoBadgeText.Text = venta.Estado;
            bool pagada = venta.EsPagada;
            EstadoBadge.Background = Pincel(pagada ? "#10b98133" : "#fb923c26");
            EstadoBadgeText.Foreground = Pincel(pagada ? "#10b981" : "#fb923c");
            FechaBadgeText.Text = venta.FechaHora.ToString("dd/MM/yyyy HH:mm");
            ClienteTextBox.Text = venta.Cliente;
            MozoTextBox.Text = venta.Mozo;
            _comprobante = string.IsNullOrWhiteSpace(venta.Comprobante) ? "Nota" : venta.Comprobante.Trim();
            _metodo = venta.EsPorCobrar ? "Crédito" : (string.IsNullOrWhiteSpace(venta.MetodoPago) ? "Efectivo" : venta.MetodoPago.Trim());
            // La deuda no cambia de metodo aqui: se cobra con Cobrar.
            MetodoPanel.Visibility = venta.EsPorCobrar ? Visibility.Collapsed : Visibility.Visible;
            MetodoAvisoText.Visibility = venta.EsPorCobrar ? Visibility.Visible : Visibility.Collapsed;
            PintarComprobante();
            PintarMetodo();
            ReconstruirItems();
        }

        // Dibuja las filas de productos con cantidad, precio y quitar.
        private void ReconstruirItems()
        {
            ItemsPanel.Children.Clear();
            foreach (var item in _items)
                ItemsPanel.Children.Add(FilaItem(item));
            ActualizarTotal();
        }

        private Border FilaItem(ItemEdit item)
        {
            var borde = new Border { Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x1B, 0x23, 0x29)), CornerRadius = new CornerRadius(10), BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x2B, 0x35, 0x40)), BorderThickness = new Thickness(1), Padding = new Thickness(12, 8, 12, 8) };
            var grid = new Grid { ColumnSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var izq = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            izq.Children.Add(new TextBlock { Text = (item.Nombre ?? "").ToUpperInvariant(), Foreground = new SolidColorBrush(Colors.White), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
            izq.Children.Add(new TextBlock { Text = $"S/ {item.Precio * item.Cantidad:0.00}", Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x9C, 0xA3, 0xAF)), FontSize = 10, FontFamily = new FontFamily("Consolas") });
            grid.Children.Add(izq);

            var cant = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(cant, 1);
            var menos = new Button { Content = new TextBlock { Text = "−", FontSize = 12 }, Padding = new Thickness(10, 2, 10, 2), MinWidth = 0, MinHeight = 0, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x24, 0x2C, 0x33)), BorderThickness = new Thickness(0) };
            menos.Click += (_, __) => { item.Cantidad = Math.Max(1, item.Cantidad - 1); ReconstruirItems(); };
            var num = new TextBlock { Text = item.Cantidad.ToString(), Foreground = new SolidColorBrush(Colors.White), FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, MinWidth = 20, TextAlignment = TextAlignment.Center };
            var mas = new Button { Content = new TextBlock { Text = "+", FontSize = 12 }, Padding = new Thickness(10, 2, 10, 2), MinWidth = 0, MinHeight = 0, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x24, 0x2C, 0x33)), BorderThickness = new Thickness(0) };
            mas.Click += (_, __) => { item.Cantidad++; ReconstruirItems(); };
            cant.Children.Add(menos);
            cant.Children.Add(num);
            cant.Children.Add(mas);
            grid.Children.Add(cant);

            var der = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(der, 2);
            var precio = new TextBox { Text = item.Precio.ToString("0.00"), FontSize = 11, FontFamily = new FontFamily("Consolas"), Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x14, 0x18, 0x1C)), BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x2B, 0x35, 0x40)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 4, 8, 4), MinWidth = 70 };
            precio.TextChanged += (_, __) => { if (TryParseMonto(precio.Text, out double p) && p > 0) { item.Precio = Math.Round(p, 2); ActualizarTotal(); } };
            var quitar = new Button { Content = new FontIcon { Glyph = "\uE74D", FontSize = 12, Foreground = Pincel("#f87171") }, Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0), Padding = new Thickness(6, 2, 6, 2), MinWidth = 0, MinHeight = 0 };
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(quitar, "Quitar producto");
            quitar.Click += (_, __) => { _items.Remove(item); ReconstruirItems(); };
            der.Children.Add(precio);
            der.Children.Add(quitar);
            grid.Children.Add(der);

            borde.Child = grid;
            return borde;
        }

        private void ActualizarTotal()
        {
            double total = _items.Sum(i => i.Precio * i.Cantidad);
            TotalText.Text = $"S/ {total:0.00}";
        }

        private void Comprobante_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string tag) return;
            _comprobante = tag;
            PintarComprobante();
        }

        private void PintarComprobante()
        {
            foreach (var child in ComprobantePanel.Children)
            {
                if (child is not Button b || b.Tag is not string tag) continue;
                bool sel = string.Equals(tag, _comprobante, StringComparison.OrdinalIgnoreCase);
                b.Background = sel ? new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x14, 0xB8, 0xA6)) : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x14, 0x18, 0x1C));
                b.BorderBrush = sel ? new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x14, 0xB8, 0xA6)) : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x2B, 0x35, 0x40));
                if (b.Content is TextBlock tb)
                {
                    tb.Foreground = sel ? new SolidColorBrush(Colors.White) : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x9C, 0xA3, 0xAF));
                    tb.FontWeight = sel ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
                }
            }
        }

        private void Metodo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string tag) return;
            _metodo = tag;
            PintarMetodo();
        }

        private void PintarMetodo()
        {
            foreach (var child in MetodoPanel.Children)
            {
                if (child is not Button b || b.Tag is not string tag) continue;
                bool sel = string.Equals(tag, _metodo, StringComparison.OrdinalIgnoreCase);
                b.Background = sel ? new SolidColorBrush(ColorHelper.FromArgb(0x26, 0x10, 0xB9, 0x81)) : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x1B, 0x23, 0x29));
                b.BorderBrush = sel ? new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x10, 0xB9, 0x81)) : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x2B, 0x35, 0x40));
                if (b.Content is TextBlock tb)
                {
                    tb.Foreground = sel ? new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x6E, 0xE7, 0xB7)) : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x9C, 0xA3, 0xAF));
                    tb.FontWeight = sel ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
                }
            }
        }

        // Guarda: valida, actualiza la boleta por ticket y devuelve stock.
        private async void GuardarButton_Click(object sender, RoutedEventArgs e)
        {
            if (_original == null) return;
            if (_items.Count == 0)
            {
                await MostrarDialogoAsync("Sin productos", "La boleta debe tener al menos un producto.", "Entendido");
                return;
            }
            foreach (var it in _items)
            {
                if (it.Cantidad < 1 || it.Precio <= 0)
                {
                    await MostrarDialogoAsync("Dato inválido", $"Revisa cantidad y precio de {it.Nombre}.", "Entendido");
                    return;
                }
            }
            double total = Math.Round(_items.Sum(i => i.Precio * i.Cantidad), 2);
            if (total <= 0)
            {
                await MostrarDialogoAsync("Total inválido", "El total debe ser mayor a cero.", "Entendido");
                return;
            }
            string cliente = (ClienteTextBox.Text ?? "").Trim();
            if (cliente.Length == 0) cliente = "Cliente Ocasional";
            string mozo = (MozoTextBox.Text ?? "").Trim();
            if (mozo.Length == 0) mozo = "Mostrador";
            var actualizada = new Venta
            {
                Ticket = _original.Ticket,
                FechaHora = _original.FechaHora,
                MesaEtiqueta = _original.MesaEtiqueta,
                Cliente = cliente,
                Mozo = mozo,
                MetodoPago = _original.EsPorCobrar ? _original.MetodoPago : _metodo,
                Comprobante = _comprobante,
                Items = _items.Select(i => new ItemVenta { Nombre = i.Nombre, Precio = Math.Round(i.Precio, 2), Cantidad = i.Cantidad }).ToList(),
                Total = total,
                Estado = _original.Estado,
                FechaCobro = _original.FechaCobro,
                MesaBadgeBg = _original.MesaBadgeBg,
                MesaBadgeBorde = _original.MesaBadgeBorde,
                MesaBadgeFg = _original.MesaBadgeFg,
            };
            try
            {
                await VentasStore.ActualizarAsync(actualizada);
            }
            catch (InvalidOperationException ex)
            {
                await MostrarDialogoAsync("No se pudo guardar", ex.Message, "Entendido");
                return;
            }
            // Devuelve o descuenta stock por la diferencia (mejor esfuerzo).
            _ = AjustarStockDiferenciaAsync();
            await MostrarDialogoAsync($"{actualizada.Ticket} actualizada", $"Boleta guardada por S/ {total:0.00}.", "Volver al historial");
            CerrarSolicitado?.Invoke(this, EventArgs.Empty);
        }

        // Diferencia nueva menos original: positiva descuenta, negativa devuelve.
        private async System.Threading.Tasks.Task AjustarStockDiferenciaAsync()
        {
            try
            {
                var nuevas = _items.GroupBy(i => i.Nombre ?? "")
                    .ToDictionary(g => g.Key, g => g.Sum(i => i.Cantidad), StringComparer.OrdinalIgnoreCase);
                var nombres = new HashSet<string>(nuevas.Keys, StringComparer.OrdinalIgnoreCase);
                foreach (var k in _cantidadesOriginales.Keys) nombres.Add(k);
                foreach (string nombre in nombres)
                {
                    nuevas.TryGetValue(nombre, out int nuevo);
                    _cantidadesOriginales.TryGetValue(nombre, out int viejo);
                    int delta = nuevo - viejo;
                    if (delta == 0) continue;
                    var plato = await PlatosStore.ObtenerPorNombreAsync(nombre);
                    if (plato == null) continue;
                    await PlatosStore.ActualizarStockAsync(plato.Plu, plato.Porciones - delta);
                }
            }
            catch { }
        }

        private void DescartarButton_Click(object sender, RoutedEventArgs e) =>
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

        // Acepta punto o coma decimal (teclados ES-PE usan coma).
        private static bool TryParseMonto(string? texto, out double valor)
        {
            texto = (texto ?? "").Trim().Replace("S/", "").Trim();
            if (double.TryParse(texto, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valor)) return true;
            string normalizado = texto.Replace(',', '.');
            return double.TryParse(normalizado, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valor);
        }

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
