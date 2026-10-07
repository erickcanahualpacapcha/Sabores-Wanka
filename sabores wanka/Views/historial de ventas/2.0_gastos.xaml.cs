using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using sabores_wanka.Models;
using sabores_wanka.Services;

namespace sabores_wanka.Views.HistorialDeVentas
{
    // Nuevo gasto con guardado local en JSON (Services/GastosStore).
    // Valida, asigna comprobante correlativo y vuelve a Egresos recargado.
    public sealed partial class GastoCreacionView : UserControl
    {
        // Método activo del grupo single-select (Tag del botón elegido).
        private string _metodoActivo = "efectivo";

        // Arrastre de la ventana flotante (posición acumulada + punto de agarre).
        private bool _moviendo;
        private Windows.Foundation.Point _agarre;
        private double _baseX;
        private double _baseY;

        // Se muestra como ventana flotante sobre el historial: avisa al cerrar
        // para que el padre oculte el overlay y recargue (no navega solo).
        public event EventHandler? CerrarSolicitado;

        public GastoCreacionView()
        {
            this.InitializeComponent();
            try
            {
                FechaBadgeText.Text = DateTime.Now.ToString("ddd, dd MMM yyyy", new System.Globalization.CultureInfo("es-PE"));
            }
            catch { FechaBadgeText.Text = DateTime.Now.ToString("dd/MM/yyyy"); }
        }

        // Métodos: pinta solo el botón elegido (efectivo en verde, resto neutro).
        private void MetodoButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button elegido || elegido.Tag is not string tag) return;
            _metodoActivo = tag;
            var activoBg = new SolidColorBrush(Windows.UI.Color.FromArgb(0x26, 0x10, 0xB9, 0x81));
            var activoBorde = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x10, 0xB9, 0x81));
            var activoFg = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x6E, 0xE7, 0xB7));
            var neutroBg = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x1B, 0x23, 0x29));
            var neutroBorde = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x2B, 0x35, 0x40));
            var neutroFg = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x9C, 0xA3, 0xAF));
            foreach (var child in MetodoPanel.Children)
            {
                if (child is not Button b) continue;
                bool activo = string.Equals(b.Tag as string, tag, StringComparison.OrdinalIgnoreCase);
                b.Background = activo ? activoBg : neutroBg;
                b.BorderBrush = activo ? activoBorde : neutroBorde;
                if (b.Content is TextBlock tb)
                {
                    tb.Foreground = activo ? activoFg : neutroFg;
                    tb.FontWeight = activo ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
                }
            }
        }

        // Crear: valida, guarda en el JSON local y vuelve a Egresos.
        private async void CrearGastoButton_Click(object sender, RoutedEventArgs e)
        {
            string concepto = (NombreTextBox?.Text ?? "").Trim();
            if (string.IsNullOrEmpty(concepto))
            {
                await MostrarDialogoAsync("Falta el concepto", "Escribe el nombre del gasto para continuar.", "Entendido");
                NombreTextBox?.Focus(FocusState.Programmatic);
                return;
            }
            if (!TryParseMonto(MontoTextBox?.Text, out double monto) || monto <= 0)
            {
                await MostrarDialogoAsync("Monto inválido", "El valor debe ser mayor a cero.", "Entendido");
                return;
            }
            string tipo = ((TipoComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Otros").ToUpperInvariant();
            string responsable = (ResponsableComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string metodo = _metodoActivo switch
            {
                "yape" => "Yape",
                "otros" => "Otros",
                _ => "Efectivo / Caja chica",
            };
            string comprobante;
            try
            {
                comprobante = await GastosStore.SiguienteComprobanteAsync();
                await GastosStore.RegistrarAsync(new Gasto
                {
                    Comprobante = comprobante,
                    FechaHora = DateTime.Now,
                    Tipo = tipo,
                    Concepto = concepto,
                    Autorizo = AbreviarResponsable(responsable),
                    Monto = Math.Round(monto, 2),
                    Metodo = metodo,
                    Nota = (NotaTextBox?.Text ?? "").Trim(),
                    Estado = "EGRESO",
                });
            }
            catch (InvalidOperationException ex)
            {
                await MostrarDialogoAsync("No se pudo crear", ex.Message, "Entendido");
                return;
            }
            await MostrarDialogoAsync($"{comprobante} creado", $"Gasto de S/ {monto:0.00} registrado en egresos.", "Volver a egresos");
            VolverAEgresos();
        }

        // "Carlos Morales" -> "Carlos M." (estilo de las filas); sin responsable -> "Caja".
        private static string AbreviarResponsable(string responsable)
        {
            responsable = (responsable ?? "").Trim();
            if (responsable.Length == 0 || responsable.StartsWith("(")) return "Caja";
            string[] partes = responsable.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 1) return partes[0];
            return $"{partes[0]} {partes[1][0]}.";
        }

        // Acepta punto o coma decimal (teclados ES-PE usan coma).
        private static bool TryParseMonto(string? texto, out double valor)
        {
            texto = (texto ?? "").Trim().Replace("S/", "").Trim();
            if (double.TryParse(texto, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valor)) return true;
            string normalizado = texto.Replace(',', '.');
            return double.TryParse(normalizado, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valor);
        }

        private void DescartarButton_Click(object sender, RoutedEventArgs e) => VolverAEgresos();

        // Mover la ventana: la cabecera captura el puntero (no pelean ni el
        // scroll ni los botones) y el desplazamiento se acumula en el transform.
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

        // Cierra la ventana flotante (el padre oculta el overlay y recarga Egresos).
        private void VolverAEgresos()
        {
            CerrarSolicitado?.Invoke(this, EventArgs.Empty);
        }

        // Diálogo oscuro reutilizable con el XamlRoot propio de la vista.
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
