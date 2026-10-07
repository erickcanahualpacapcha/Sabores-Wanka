using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace sabores_wanka.Views.InformacionNegocio
{
    // Ficha del negocio (replica de informacion de negocio/code.html): datos
    // fiscales + redes + ticket termico en vivo + tab de impresoras mock.
    // Persistencia en LocalSettings; el logo se previsualiza sin copiarse aun.
    public sealed partial class InformacionNegocioView : UserControl
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        // Avisa al shell para volver al POS (boton "Volver al POS").
        public event EventHandler? VolverPosSolicitado;

        private static readonly SolidColorBrush TabActivoFondo = new(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xDD, 0xB1, 0x59));
        private static readonly SolidColorBrush TabActivoTexto = new(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x12, 0x1F, 0x1A));
        private static readonly SolidColorBrush TabInactivoFondo = new(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x09, 0x15, 0x12));
        private static readonly SolidColorBrush TabInactivoTexto = new(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x6E, 0xE7, 0xB7));
        private static readonly SolidColorBrush TabInactivoBorde = new(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x16, 0x36, 0x2E));

        public InformacionNegocioView()
        {
            this.InitializeComponent();
            this.Loaded += InformacionNegocioView_Loaded;
        }

        // Al mostrarse: restaura lo guardado (incluye logo) y pinta la tab de negocio.
        private void InformacionNegocioView_Loaded(object sender, RoutedEventArgs e)
        {
            CargarGuardado();
            MostrarTabNegocio(true);
            ActualizarPreview();
            _ = ActualizarLogoAsync();
        }

        // Pinta el logo guardado en el picker y en el papel (oculto si no hay).
        private async System.Threading.Tasks.Task ActualizarLogoAsync()
        {
            var bmp = await Services.LogoTicket.CargarAsync();
            if (LogoPreview != null)
            {
                LogoPreview.Source = bmp;
                LogoPreview.Visibility = bmp != null ? Visibility.Visible : Visibility.Collapsed;
            }
            if (LogoPlaceholderIcon != null)
                LogoPlaceholderIcon.Visibility = bmp != null ? Visibility.Collapsed : Visibility.Visible;
            if (PreviewLogo != null)
            {
                PreviewLogo.Source = bmp;
                PreviewLogo.Visibility = bmp != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // Lee LocalSettings (si no hay nada deja los valores del XAML).
        private void CargarGuardado()
        {
            try
            {
                var local = ApplicationData.Current.LocalSettings;
                if (local.Values["Negocio.Nombre"] is string n && NombreTextBox != null) NombreTextBox.Text = n;
                if (local.Values["Negocio.Ruc"] is string r && RucTextBox != null) RucTextBox.Text = r;
                if (local.Values["Negocio.Telefono"] is string t && TelefonoTextBox != null) TelefonoTextBox.Text = t;
                if (local.Values["Negocio.Direccion"] is string d && DireccionTextBox != null) DireccionTextBox.Text = d;
                if (local.Values["Negocio.Instagram"] is string ig && InstagramTextBox != null) InstagramTextBox.Text = ig;
                if (local.Values["Negocio.Email"] is string em && EmailTextBox != null) EmailTextBox.Text = em;
                if (local.Values["Negocio.Mensaje"] is string m && MensajeTextBox != null) MensajeTextBox.Text = m;
                if (local.Values["Negocio.MonedaIdx"] is int idx && MonedaComboBox != null && idx >= 0 && idx < MonedaComboBox.Items.Count)
                    MonedaComboBox.SelectedIndex = idx;
            }
            catch { }
        }

        // Guarda los campos en LocalSettings (el ticket lee de aqui).
        private async void GuardarButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var local = ApplicationData.Current.LocalSettings;
                local.Values["Negocio.Nombre"] = NombreTextBox?.Text ?? "";
                local.Values["Negocio.Ruc"] = RucTextBox?.Text ?? "";
                local.Values["Negocio.Telefono"] = TelefonoTextBox?.Text ?? "";
                local.Values["Negocio.Direccion"] = DireccionTextBox?.Text ?? "";
                local.Values["Negocio.Instagram"] = InstagramTextBox?.Text ?? "";
                local.Values["Negocio.Email"] = EmailTextBox?.Text ?? "";
                local.Values["Negocio.Mensaje"] = MensajeTextBox?.Text ?? "";
                local.Values["Negocio.MonedaIdx"] = MonedaComboBox?.SelectedIndex ?? 0;
                await MostrarDialogoAsync("Configuración guardada", "Los datos del negocio se aplicarán en boletas y tickets.", "Entendido");
            }
            catch
            {
                await MostrarDialogoAsync("No se pudo guardar", "Revisa los campos e inténtalo de nuevo.", "Entendido");
            }
        }

        // Descarta: recarga lo ultimo guardado en vez de limpiar a mano.
        private void DescartarButton_Click(object sender, RoutedEventArgs e)
        {
            CargarGuardado();
            ActualizarPreview();
            _ = ActualizarLogoAsync();
        }

        // Cualquier campo fiscal re-pinta el ticket en vivo (patron del mock HTML).
        private void CampoNegocio_TextChanged(object sender, TextChangedEventArgs e) => ActualizarPreview();

        // La moneda solo cambia el simbolo del total (el demo suma S/ 62.00).
        private void MonedaComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ActualizarPreview();

        // Copia los inputs al papel termico; vacios muestran guiones.
        private void ActualizarPreview()
        {
            if (PreviewNombre == null) return;
            PreviewNombre.Text = string.IsNullOrWhiteSpace(NombreTextBox?.Text) ? "NOMBRE COMERCIAL" : NombreTextBox.Text.ToUpperInvariant();
            if (PreviewRuc != null) PreviewRuc.Text = "RUC: " + (string.IsNullOrWhiteSpace(RucTextBox?.Text) ? "----------" : RucTextBox.Text.Trim());
            if (PreviewDireccion != null) PreviewDireccion.Text = string.IsNullOrWhiteSpace(DireccionTextBox?.Text) ? "Dirección fiscal" : DireccionTextBox.Text.Trim();
            if (PreviewTelefono != null) PreviewTelefono.Text = "Tel: " + (string.IsNullOrWhiteSpace(TelefonoTextBox?.Text) ? "-" : TelefonoTextBox.Text.Trim());
            if (PreviewMensaje != null) PreviewMensaje.Text = MensajeTextBox?.Text ?? "";
            if (PreviewTotal != null) PreviewTotal.Text = SimboloMoneda() + " 62.00";
        }

        // Lee el Tag del ComboBox (S/, $, euro); por defecto S/.
        private string SimboloMoneda()
        {
            if (MonedaComboBox?.SelectedItem is ComboBoxItem item && item.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
                return tag;
            return "S/";
        }

        // Tabs: negocio (formularios) vs impresoras (3 tarjetas mock del HTML).
        private void NegocioTabButton_Click(object sender, RoutedEventArgs e) => MostrarTabNegocio(true);

        private void ImpresorasTabButton_Click(object sender, RoutedEventArgs e) => MostrarTabNegocio(false);

        private void MostrarTabNegocio(bool esNegocio)
        {
            if (NegocioPanel != null) NegocioPanel.Visibility = esNegocio ? Visibility.Visible : Visibility.Collapsed;
            if (ImpresorasPanel != null) ImpresorasPanel.Visibility = esNegocio ? Visibility.Collapsed : Visibility.Visible;
            if (!esNegocio) CargarImpresoras();
            PintarTab(NegocioTabButton, esNegocio);
            PintarTab(ImpresorasTabButton, !esNegocio);
        }

        // Tab activa en dorado #DDB159; inactiva en verde oscuro del mock.
        private static void PintarTab(Button? boton, bool activo)
        {
            if (boton == null) return;
            boton.Background = activo ? TabActivoFondo : TabInactivoFondo;
            boton.BorderBrush = activo ? TabActivoFondo : TabInactivoBorde;
            boton.Foreground = activo ? TabActivoTexto : TabInactivoTexto;
        }

        // Formato papel: 80mm ancho completo, 58mm angosto (solo visual).
        private void Formato80Button_Click(object sender, RoutedEventArgs e) => CambiarFormato(true);

        private void Formato58Button_Click(object sender, RoutedEventArgs e) => CambiarFormato(false);

        private void CambiarFormato(bool es80mm)
        {
            if (TicketPaper != null) TicketPaper.MaxWidth = es80mm ? 360 : 240;
            if (Formato80Button != null)
            {
                Formato80Button.Background = es80mm ? TabActivoFondo : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                Formato80Button.Foreground = es80mm ? TabActivoTexto : TabInactivoTexto;
            }
            if (Formato58Button != null)
            {
                Formato58Button.Background = !es80mm ? TabActivoFondo : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                Formato58Button.Foreground = !es80mm ? TabActivoTexto : TabInactivoTexto;
            }
        }

        // Logo: picker del sistema, se guarda en disco y se previsualiza
        // en el picker y en el papel. Asi sale tambien en la impresion.
        private async void LogoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                WinRT.Interop.InitializeWithWindow.Initialize(picker, GetActiveWindow());
                var archivo = await picker.PickSingleFileAsync();
                if (archivo == null) return;
                await Services.LogoTicket.GuardarAsync(archivo.Path);
                await ActualizarLogoAsync();
            }
            catch (InvalidOperationException ex)
            {
                await MostrarDialogoAsync("Logo no válido", ex.Message, "Entendido");
            }
            catch
            {
                await MostrarDialogoAsync("No se pudo abrir", "No se pudo elegir la imagen. Intenta con un JPG o PNG.", "Entendido");
            }
        }

        private void VolverPosButton_Click(object sender, RoutedEventArgs e) => VolverPosSolicitado?.Invoke(this, EventArgs.Empty);

        // Ticket de prueba REAL al driver (patron Nachis PrinterService):
        // imprime a la guardada o a la predeterminada; el error dice el por que.
        private async void ProbarImpresionButton_Click(object sender, RoutedEventArgs e)
        {
            var (ok, error) = await Services.ImpresionTickets.ImprimirTextoAsync(Services.ImpresionTickets.TextoPrueba());
            if (ok)
                await MostrarDialogoAsync("Ticket de prueba", "Ticket enviado a la impresora.", "Entendido");
            else
                await MostrarDialogoAsync("No se pudo imprimir", error, "Entendido");
        }

        // Lista las impresoras de Windows y marca la guardada o la predeterminada.
        private void CargarImpresoras()
        {
            if (ImpresoraComboBox == null) return;
            var (predeterminada, todas) = Services.ImpresionTickets.ListarImpresorasSistema();
            ImpresoraComboBox.Items.Clear();
            foreach (string nombre in todas)
                ImpresoraComboBox.Items.Add(nombre);
            string guardada = Services.ImpresionTickets.LeerImpresora();
            string actual = todas.Contains(guardada) ? guardada : predeterminada;
            if (actual.Length > 0) ImpresoraComboBox.SelectedItem = actual;
            if (ImpresoraEstadoText != null)
                ImpresoraEstadoText.Text = todas.Count == 0
                    ? "Sin impresoras instaladas en Windows."
                    : $"Usando: {(actual.Length > 0 ? actual : "—")} ({todas.Count} instalada(s))";
        }

        // Guarda la elegida para tickets, comandas y pruebas.
        private async void GuardarImpresoraButton_Click(object sender, RoutedEventArgs e)
        {
            string elegida = (ImpresoraComboBox?.SelectedItem as string) ?? "";
            if (elegida.Length == 0)
            {
                await MostrarDialogoAsync("Sin impresora", "No hay impresoras instaladas en Windows.", "Entendido");
                return;
            }
            Services.ImpresionTickets.GuardarImpresora(elegida);
            if (ImpresoraEstadoText != null) ImpresoraEstadoText.Text = $"Usando: {elegida}";
            await MostrarDialogoAsync("Impresora guardada", $"Los tickets saldrán por '{elegida}'.", "Entendido");
        }

        private async void AgregarImpresoraButton_Click(object sender, RoutedEventArgs e)
        {
            // Accion real: abre Dispositivos e impresoras de Windows para instalarla.
            try { await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:printers")); }
            catch { await MostrarDialogoAsync("Agregar impresora", "Abre Configuración de Windows > Bluetooth y dispositivos > Impresoras.", "Entendido"); }
        }

        // Dialogo modal anclado a este UserControl (patron de Productos).
        private async System.Threading.Tasks.Task MostrarDialogoAsync(string titulo, string mensaje, string primario)
        {
            var dialogo = new ContentDialog
            {
                Title = titulo,
                Content = mensaje,
                CloseButtonText = primario,
                XamlRoot = this.XamlRoot
            };
            try { await dialogo.ShowAsync(); } catch { }
        }
    }
}
