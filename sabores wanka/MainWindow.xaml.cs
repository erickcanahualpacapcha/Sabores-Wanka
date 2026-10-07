using System;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace sabores_wanka
{
    // Shell principal: banda de titulo oscura + sidebar overlay + contenido.
    // NO usar Frame; la navegacion sustituye MainContent.Content.
    public sealed partial class MainWindow : Window
    {
        // Ancho reservado a la izquierda para no pelear con el trigger del sidebar.
        private const double SidebarTriggerZoneWidth = 15d;
        private AppWindow? _appWindow;
        private InputNonClientPointerSource? _inputNonClientPointerSource;
        private bool _isCustomTitleBarEnabled;
        private Views.PuntoDeVenta.PuntoDeVentaView? _posView;
        private Services.ActualizacionService? _actualizador;

        public MainWindow()
        {
            InitializeComponent();
            InitializeCustomTitleBar();
            TitleBarDragHost.Loaded += (_, _) => UpdateTitleBarMetrics();
            MainContent.SizeChanged += (_, _) => UpdateDragRegion();
            SizeChanged += (_, _) => UpdateTitleBarMetrics();
            if (_appWindow is not null)
            {
                _appWindow.Changed += (_, _) => DispatcherQueue.TryEnqueue(UpdateTitleBarMetrics);
            }
            MainSidebar.PuntoDeVentaSelected += MainSidebar_PuntoDeVentaSelected;
            MainSidebar.HistorialSelected += MainSidebar_HistorialSelected;
            MainSidebar.ProductosSelected += MainSidebar_ProductosSelected;
            MainSidebar.WhatsappSelected += MainSidebar_WhatsappSelected;
            MainSidebar.InformacionNegocioSelected += MainSidebar_InformacionNegocioSelected;
            // Arranca maximizada y directo en punto de venta (sin bienvenida).
            MaximizarVentana();
            MostrarPos();
            // Auto-update GitHub (patron Nachis): completa swaps pendientes y
            // revisa el latest en segundo plano; el banner avisa si hay version.
            try
            {
                _actualizador = new Services.ActualizacionService();
                _actualizador.TryRecoverPendingUpdate();
                _actualizador.ActualizacionDisponible += (_, version) =>
                    DispatcherQueue.TryEnqueue(() => UpdateBanner.Mostrar(version));
                UpdateBanner.ActualizarSolicitado += UpdateBanner_ActualizarSolicitado;
                _ = _actualizador.CheckForUpdatesAsync();
            }
            catch { }
        }

        // Descarga el release con progreso y relanza (el servicio cierra la app).
        private async void UpdateBanner_ActualizarSolicitado(object? sender, string version)
        {
            if (_actualizador == null) return;
            var progreso = new Progress<double>(p =>
                DispatcherQueue.TryEnqueue(() => UpdateBanner.MostrarProgreso(p)));
            bool ok = await _actualizador.DownloadAndApplyUpdateAsync(version, progreso);
            if (!ok)
                UpdateBanner.MostrarError("No se pudo descargar. Revisa tu conexión e inténtalo de nuevo.");
        }

        // Revisión manual (para un futuro botón "Buscar actualizaciones").
        public void TriggerManualUpdateCheck()
        {
            if (_actualizador != null) _ = _actualizador.CheckForUpdatesAsync();
        }

        // Maximiza con el presenter (si ya esta maximizada no hace nada).
        private void MaximizarVentana()
        {
            try
            {
                if (AppWindow.Presenter is OverlappedPresenter presentador)
                {
                    presentador.Maximize();
                }
            }
            catch { }
        }

        // Activa la barra de titulo oscura: extiende el contenido y pinta los botones en blanco.
        // Copia el patron de Tiendas Nachis (banda #1E2433 + botones transparentes).
        private void InitializeCustomTitleBar()
        {
            if (!AppWindowTitleBar.IsCustomizationSupported())
            {
                return;
            }
            _appWindow = AppWindow;
            _inputNonClientPointerSource = InputNonClientPointerSource.GetForWindowId(_appWindow.Id);
            AppWindowTitleBar titleBar = _appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonInactiveForegroundColor = Colors.White;
            // Hover/pressed oscuros para que no aparezca el blanco del sistema.
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF);
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF);
            _isCustomTitleBarEnabled = true;
        }

        // Sincroniza la altura real del caption del sistema con la banda XAML.
        // Debe correr en Loaded/SizeChanged porque TitleBar.Height depende de DPI.
        private void UpdateTitleBarMetrics()
        {
            if (!_isCustomTitleBarEnabled || _appWindow is null || MainContent.XamlRoot is null)
            {
                return;
            }
            double scale = MainContent.XamlRoot.RasterizationScale;
            if (scale <= 0)
            {
                scale = 1d;
            }
            double titleBarHeight = _appWindow.TitleBar.Height / scale;
            TitleBarDragHost.Height = titleBarHeight;
            MainContent.Margin = new Thickness(0, titleBarHeight, 0, 0);
            UpdateDragRegion();
        }

        // Define la zona arrastrable dejando libre el trigger del sidebar y los botones.
        // Sin esto la ventana no se puede mover desde la banda oscura.
        private void UpdateDragRegion()
        {
            if (!_isCustomTitleBarEnabled || _appWindow is null || _inputNonClientPointerSource is null || MainContent.XamlRoot is null)
            {
                return;
            }
            double scale = MainContent.XamlRoot.RasterizationScale;
            if (scale <= 0)
            {
                scale = 1d;
            }
            double hostWidth = MainContent.ActualWidth;
            double hostHeight = _appWindow.TitleBar.Height / scale;
            if (hostWidth <= 0 || hostHeight <= 0)
            {
                return;
            }
            int leftInset = _appWindow.TitleBar.LeftInset;
            int rightInset = _appWindow.TitleBar.RightInset;
            int reservedLeftWidth = (int)Math.Round(SidebarTriggerZoneWidth * scale);
            int dragStartX = Math.Max(leftInset, reservedLeftWidth);
            int dragEndX = Math.Max(dragStartX, (int)Math.Round(hostWidth * scale) - rightInset);
            // La pildora de mesa es clicable: se recorta de la zona arrastrable.
            var captionRects = new System.Collections.Generic.List<RectInt32>();
            if (MesaTitlePill != null && MesaTitlePill.Visibility == Visibility.Visible)
            {
                double pillW = MesaTitlePill.ActualWidth > 0 ? MesaTitlePill.ActualWidth : 150;
                int pillL = (int)Math.Round((hostWidth - pillW) / 2 * scale);
                int pillR = (int)Math.Round((hostWidth + pillW) / 2 * scale);
                if (pillL > dragStartX)
                    captionRects.Add(GetPhysicalRect(dragStartX / scale, 0, (pillL - dragStartX) / scale, hostHeight, scale));
                if (dragEndX > pillR)
                    captionRects.Add(GetPhysicalRect(pillR / scale, 0, (dragEndX - pillR) / scale, hostHeight, scale));
            }
            else if (dragEndX > dragStartX)
            {
                captionRects.Add(GetPhysicalRect(dragStartX / scale, 0, (dragEndX - dragStartX) / scale, hostHeight, scale));
            }
            _inputNonClientPointerSource.SetRegionRects(NonClientRegionKind.Caption, captionRects.ToArray());
        }

        private static RectInt32 GetPhysicalRect(double x, double y, double width, double height, double scale)
        {
            return new RectInt32(
                (int)Math.Round(x * scale),
                (int)Math.Round(y * scale),
                (int)Math.Round(width * scale),
                (int)Math.Round(height * scale));
        }

        private void MainSidebar_PuntoDeVentaSelected(object? sender, EventArgs e)
        {
            MostrarPos();
        }

        private void MainSidebar_HistorialSelected(object? sender, EventArgs e)
        {
            SoltarPos();
            MainContent.Content = new Views.HistorialDeVentas.HistorialDeVentasView();
        }

        private void MainSidebar_ProductosSelected(object? sender, EventArgs e)
        {
            SoltarPos();
            MainContent.Content = new Views.Productos.ProductosView();
        }

        // WhatsApp sin motor: vista propia (la píldora de mesa se oculta fuera del POS).
        private void MainSidebar_WhatsappSelected(object? sender, EventArgs e)
        {
            SoltarPos();
            MainContent.Content = new Views.Whatsapp.WhatsappView();
        }

        // Ficha del negocio desde el encabezado del sidebar (píldora oculta).
        private void MainSidebar_InformacionNegocioSelected(object? sender, EventArgs e)
        {
            SoltarPos();
            var vista = new Views.InformacionNegocio.InformacionNegocioView();
            vista.VolverPosSolicitado += (_, _) => MostrarPos();
            MainContent.Content = vista;
        }

        // Muestra el POS y enlaza la pildora de mesa del titulo (solo vive aqui).
        private void MostrarPos()
        {
            SoltarPos();
            _posView = new Views.PuntoDeVenta.PuntoDeVentaView();
            _posView.MesaActualCambiada += PosView_MesaActualCambiada;
            MainContent.Content = _posView;
            MesaTitlePill.Visibility = Visibility.Visible;
            RefrescarPildoraMesa();
        }

        // Suelta el POS al salir: la pildora se oculta y no quedan handlers colgados.
        private void SoltarPos()
        {
            if (_posView != null)
            {
                _posView.MesaActualCambiada -= PosView_MesaActualCambiada;
                _posView = null;
            }
            MesaTitlePill.Visibility = Visibility.Collapsed;
            UpdateTitleBarMetrics();
        }

        private void PosView_MesaActualCambiada(object? sender, EventArgs e) => RefrescarPildoraMesa();

        // Pildora del titulo: mesa en blanco + icono teal, sin mesa en plomo.
        private void RefrescarPildoraMesa()
        {
            string mesa = _posView?.MesaActual ?? "";
            if (string.IsNullOrEmpty(mesa))
            {
                MesaTitleText.Text = "Sin mesa";
                MesaTitleText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x64, 0x74, 0x8B));
                MesaTitleIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x64, 0x74, 0x8B));
            }
            else
            {
                MesaTitleText.Text = mesa;
                MesaTitleText.Foreground = new SolidColorBrush(Colors.White);
                MesaTitleIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x00, 0xC2, 0xFF));
            }
            UpdateTitleBarMetrics();
        }

        // La pildora abre el selector de mesas del POS.
        private void MesaTitleButton_Click(object sender, RoutedEventArgs e)
        {
            _posView?.AbrirSelectorMesas();
        }
    }
}
