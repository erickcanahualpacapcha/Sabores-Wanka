using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using System.ComponentModel;
using sabores_wanka.Models;
using sabores_wanka.Services;

namespace sabores_wanka.Views.PuntoDeVenta
{
    // Plato del catalogo (unidades simples, sin variantes). Es el modelo que pinta
    // el GridView estilo Nachis: x:Bind lee estas props y el badge reacciona a Cantidad.
    public sealed class PlatoItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string Nombre { get; }
        public double Precio { get; }
        public string PrecioTexto => $"S/ {Precio:0.00}";
        public string Categoria { get; }
        public SolidColorBrush CategoriaBrush { get; }
        public SolidColorBrush CategoriaLetra { get; }

        // Stock vivo: inicial menos lo reservado en órdenes abiertas (todas las mesas).
        // Sobregiro permitido (Nachis AllowOverdraft): puede dar negativo y la
        // venta nunca se bloquea; en rojo para alertar que hay que reponer.
        public string UnidadStock { get; }
        public int StockDisponible => StockInicial - Reservado;
        public string StockTexto => $"STOCK: {StockDisponible} {UnidadStock}";
        public SolidColorBrush StockColor => StockDisponible < 0
            ? new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xF4, 0x3F, 0x5E))
            : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x00, 0xA3, 0xFF));

        private int _stockInicial;
        public int StockInicial
        {
            get => _stockInicial;
            set
            {
                _stockInicial = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StockInicial)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StockDisponible)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StockTexto)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StockColor)));
            }
        }

        private int _reservado;
        public int Reservado
        {
            get => _reservado;
            set
            {
                _reservado = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Reservado)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StockDisponible)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StockTexto)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StockColor)));
            }
        }
        public string IconGlyph { get; }
        public ImageSource? FotoSource { get; private set; }

        private Visibility _fotoVisible = Visibility.Collapsed;
        public Visibility FotoVisible
        {
            get => _fotoVisible;
            private set { _fotoVisible = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FotoVisible))); }
        }

        private Visibility _iconoVisible = Visibility.Visible;
        public Visibility IconoVisible
        {
            get => _iconoVisible;
            private set { _iconoVisible = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconoVisible))); }
        }

        private int _cantidad;
        public int Cantidad
        {
            get => _cantidad;
            set
            {
                _cantidad = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Cantidad)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BadgeVisible)));
            }
        }

        public Visibility BadgeVisible => Cantidad > 0 ? Visibility.Visible : Visibility.Collapsed;

        public PlatoItem(string nombre, double precio, string categoria, SolidColorBrush categoriaBrush, SolidColorBrush categoriaLetra, string glyph, int stockInicial = 0, string unidad = "porc.", string fotoLocal = "")
        {
            Nombre = nombre;
            Precio = precio;
            Categoria = categoria;
            CategoriaBrush = categoriaBrush;
            CategoriaLetra = categoriaLetra;
            UnidadStock = string.IsNullOrWhiteSpace(unidad) ? "porc." : unidad;
            IconGlyph = glyph;
            _stockInicial = stockInicial;
            _reservado = 0;
            // Foto: primero la imagen local del plato; si no hay, el PNG por
            // slug; si tampoco existe, queda el FontIcon (sin excepciones).
            if (!string.IsNullOrWhiteSpace(fotoLocal) && System.IO.File.Exists(fotoLocal))
            {
                try
                {
                    var bmp = new BitmapImage();
                    FotoSource = bmp;
                    _ = CargarFotoLocalAsync(bmp, fotoLocal);
                    return;
                }
                catch { FotoSource = null; }
            }
            // Foto opcional: Assets/Platos/{slug}.png. Si no existe, ImageFailed la
            // descarta y queda el FontIcon (mismo fallback del ejemplo Nachis).
            string slug = NormalizarSlug(nombre);
            if (!string.IsNullOrEmpty(slug))
            {
                try
                {
                    var bmp = new BitmapImage(new Uri($"ms-appx:///Assets/Platos/{slug}.png"));
                    bmp.ImageOpened += (_, _) => { FotoVisible = Visibility.Visible; IconoVisible = Visibility.Collapsed; };
                    bmp.ImageFailed += (_, _) => { FotoSource = null; };
                    FotoSource = bmp;
                }
                catch { FotoSource = null; }
            }
        }

        private static string NormalizarSlug(string nombre)
        {
            string s = nombre.ToLowerInvariant()
                .Replace('á', 'a').Replace('é', 'e').Replace('í', 'i')
                .Replace('ó', 'o').Replace('ú', 'u').Replace('ñ', 'n');
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                if (c is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(c);
                else if (c is ' ' or '-' or '_') sb.Append('_');
            }
            string slug = sb.ToString();
            while (slug.Contains("__")) slug = slug.Replace("__", "_");
            return slug.Trim('_');
        }

        // Carga la foto local en el bitmap y la muestra; si falla queda el icono.
        private async System.Threading.Tasks.Task CargarFotoLocalAsync(BitmapImage bmp, string ruta)
        {
            try
            {
                var archivo = await Windows.Storage.StorageFile.GetFileFromPathAsync(ruta);
                using var flujo = await archivo.OpenReadAsync();
                await bmp.SetSourceAsync(flujo);
                FotoVisible = Visibility.Visible;
                IconoVisible = Visibility.Collapsed;
            }
            catch { FotoSource = null; }
        }
    }

    public sealed partial class PuntoDeVentaView : UserControl
    {
        private static readonly TimeSpan ToastVisibleTime = TimeSpan.FromMilliseconds(1800);
        private Storyboard? _mesaToastStoryboard;
        private Storyboard? _mesaAbiertaStoryboard;
        private Storyboard? _sinMesaStoryboard;
        private string _mesaActual = "";

        // Mesa actual ("": sin mesa) + aviso a la ventana para la pildora del titulo.
        public string MesaActual => _mesaActual;
        public event EventHandler? MesaActualCambiada;

        // Fija la mesa y notifica (la pildora del titulo vive en MainWindow).
        private void FijarMesaActual(string mesa)
        {
            _mesaActual = mesa;
            MesaActualCambiada?.Invoke(this, EventArgs.Empty);
        }

        // Modo cierre: split productos + pago en tono oscuro, y cierre parcial por seleccion.
        private bool _enModoCierre = false;
        private readonly Dictionary<string, List<CartItem>> _mesasOrdenes = new();
        private readonly HashSet<string> _cierreSeleccionIds = new();
        private string _pagoMetodo = "Tarjeta";
        private int _pagoCuotas = 1;
        private List<CartItem> _ultimoCierreSeleccion = new();
        private double _ultimoCierreTotal = 0;

        private record CartItem(string Id, string Nombre, double Precio, int Cantidad);

        // Cliente / Vendedor seleccion (dialogos oscuros con colores distintos)
        private readonly List<ClienteItem> _clientes = new();
        private ClienteItem? _clienteSeleccionado;
        private string _comprobanteTipo = "Nota";
        private bool _modoNuevoCliente = false;
        private readonly List<VendedorItem> _vendedores = new();
        private VendedorItem? _vendedorSeleccionado;

        private record ClienteItem(string Id, string Nombre);
        private record VendedorItem(string Id, string Nombre);

        // Catalogo de platos (fuente del GridView estilo Nachis). La cantidad por
        // tarjeta se sincroniza con la orden de la mesa actual, no vive aqui.
        private readonly List<PlatoItem> _platos = new();

        // Puerta del autoguardado: se abre tras restaurar (el constructor pinta
        // ceros y no debe borrar el archivo de una venta por recuperar).
        private bool _ventasRestauradas;

        // Filtro activo del catalogo (Tag de la pill; "todos" = sin filtro).
        private string _categoriaPosActiva = "todos";

        // Brushes para el boton inferior (normal vs cierre).
        private static readonly SolidColorBrush CerrarNormalBrush = new(ColorHelper.FromArgb(0xFF, 0x00, 0xA8, 0xE8));
        private static readonly SolidColorBrush ConfirmarCierreBrush = new(ColorHelper.FromArgb(0xFF, 0x1C, 0x27, 0x3A));

        public PuntoDeVentaView()
        {
            this.InitializeComponent();
            MesasPanel.CerrarRequested += MesasPanel_CerrarRequested;
            MesasPanel.MesaSeleccionada += MesasPanel_MesaSeleccionada;
            MesasPanel.MesaAbierta += MesasPanel_MesaAbierta;
            _mesaActual = "";
            InicializarOrdenesMock();
            ActualizarCartUI();
            InicializarPlatos();
            this.Loaded += PuntoDeVentaView_Loaded;
            RestaurarAnchoCarrito();
            MostrarMesasSiTodasLibres();
            ActualizarEstadoCatalogo();
        }

        private void InicializarOrdenesMock()
        {
            // Sin mesas precargadas: cada mesa se registra sola al usarse.
            _mesasOrdenes.Clear();
        }

        private List<CartItem> ObtenerOrden(string mesa)
        {
            if (!_mesasOrdenes.TryGetValue(mesa, out var list))
            {
                list = new List<CartItem>();
                _mesasOrdenes[mesa] = list;
            }
            return list;
        }

        private void ActualizarCartUI()
        {
            var orden = ObtenerOrden(_mesaActual);
            CartProductosPanel.Items.Clear();
            double total = 0;
            foreach (var item in orden)
            {
                var fila = CrearFilaCart(item);
                fila.Tag = item.Id;
                CartProductosPanel.Items.Add(fila);
                total += item.Precio * item.Cantidad;
            }
            CartCountText.Text = $"{orden.Count} producto{(orden.Count == 1 ? "" : "s")}";
            CartTotalText.Text = $"{total:0.00}";
            // Si no hay productos, mostrar vacio
            if (orden.Count == 0)
            {
                CartCountText.Text = "0 productos";
                CartTotalText.Text = "0.00";
            }
            // Sincroniza aro azul + contador de cada tarjeta con la orden real.
            ActualizarCatalogoSeleccion();
            // Autoguardado local: cada cambio del carrito queda en disco por si
            // se cierra el programa o se va la luz (ordenes abiertas por mesa).
            _ = PersistirOrdenesAsync();
        }

        // Sincroniza el badge X+contador de cada tarjeta con la orden de la mesa.
        // Cantidad = lo de la mesa actual; Reservado = todo lo abierto (todas
        // las mesas) para que el pill de stock baje al pedir y no se sobrevenda.
        private void ActualizarCatalogoSeleccion()
        {
            if (PlatoGrid == null) return;
            var orden = ObtenerOrden(_mesaActual);
            var todas = _mesasOrdenes.Values.SelectMany(l => l).ToList();
            foreach (var plato in _platos)
            {
                plato.Cantidad = orden.Where(i => i.Nombre == plato.Nombre).Sum(i => i.Cantidad);
                plato.Reservado = todas.Where(i => i.Nombre == plato.Nombre).Sum(i => i.Cantidad);
            }
        }

        // Al soltar un arrastre, la orden adopta el orden visual (por Id de fila).
        // Los totales no cambian con el orden, pero el cierre y el ticket si lo respetan.
        private void CartProductos_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            var orden = ObtenerOrden(_mesaActual);
            var nuevo = new List<CartItem>();
            foreach (var visual in sender.Items)
            {
                if (visual is Border fila && fila.Tag is string id)
                {
                    var encontrado = orden.FirstOrDefault(i => i.Id == id);
                    if (encontrado != null) nuevo.Add(encontrado);
                }
            }
            if (nuevo.Count == orden.Count && nuevo.Count > 0)
            {
                orden.Clear();
                orden.AddRange(nuevo);
                ActualizarCartUI();
            }
        }

        // Estirar el carrito (igual que el ejemplo Nachis): el tirador captura el
        // puntero y la columna CartColumn cambia entre su MinWidth y MaxWidth.
        private bool _cartAgrandando;
        private double _cartAnchoOrigen;
        private double _cartXOrigen;

        private void CartResize_Pressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement tirador)
            {
                tirador.CapturePointer(e.Pointer);
                _cartAgrandando = true;
                _cartAnchoOrigen = CartColumn.ActualWidth;
                _cartXOrigen = e.GetCurrentPoint(this).Position.X;
                e.Handled = true;
            }
        }

        private void CartResize_Moved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!_cartAgrandando) return;
            double nuevo = _cartAnchoOrigen + (_cartXOrigen - e.GetCurrentPoint(this).Position.X);
            if (nuevo >= CartColumn.MinWidth && nuevo <= CartColumn.MaxWidth)
            {
                CartColumn.Width = new GridLength(nuevo);
            }
            e.Handled = true;
        }

        private void CartResize_Released(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!_cartAgrandando) return;
            if (sender is FrameworkElement tirador) tirador.ReleasePointerCapture(e.Pointer);
            _cartAgrandando = false;
            e.Handled = true;
            GuardarAnchoCarrito(CartColumn.ActualWidth);
        }

        // Memoria del ancho del carrito (igual que el ejemplo: persiste entre sesiones).
        // Guarda en %LocalAppData%/SaboresWanka/cart.json, misma carpeta del diag de mesas.
        private static string RutaAnchoCarrito()
        {
            string carpeta = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "SaboresWanka");
            System.IO.Directory.CreateDirectory(carpeta);
            return System.IO.Path.Combine(carpeta, "cart.json");
        }

        private void RestaurarAnchoCarrito()
        {
            try
            {
                string ruta = RutaAnchoCarrito();
                if (!System.IO.File.Exists(ruta) || CartColumn == null) return;
                string texto = System.IO.File.ReadAllText(ruta).Trim();
                if (double.TryParse(texto, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double ancho))
                {
                    ancho = System.Math.Clamp(ancho, CartColumn.MinWidth, CartColumn.MaxWidth);
                    CartColumn.Width = new GridLength(ancho);
                }
            }
            catch { }
        }

        private static void GuardarAnchoCarrito(double ancho)
        {
            try
            {
                System.IO.File.WriteAllText(RutaAnchoCarrito(), ancho.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
            }
            catch { }
        }

        private void CartResize_Hover(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            CartResizeGrip.Fill = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x00, 0xC2, 0xFF));
        }

        private void CartResize_Unhover(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_cartAgrandando) return;
            CartResizeGrip.Fill = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x2A, 0x36, 0x47));
        }

        private Border CrearFilaCart(CartItem item)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x17, 0x1D, 0x2A)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10),
                BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x23, 0x2C, 0x3F)),
                BorderThickness = new Thickness(1)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var iconBorder = new Border { Width = 56, Height = 56, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x0E, 0x12, 0x19)), BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x33, 0x41, 0x55)), BorderThickness = new Thickness(1) };
            PintarMiniatura(iconBorder, item.Nombre);
            Grid.SetColumn(iconBorder, 0);
            grid.Children.Add(iconBorder);
            var sp = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            sp.Children.Add(new TextBlock { Text = item.Nombre, Foreground = new SolidColorBrush(Colors.White), FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
            var precioPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            var precioText = new TextBlock { Text = $"S/ {item.Precio:0.00}", Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x22, 0xD3, 0xEE)), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Tag = item.Id };
            precioText.Tapped += PrecioCart_Tapped;
            precioPanel.Children.Add(precioText);
            precioPanel.Children.Add(new FontIcon { Glyph = "\uE70F", FontSize = 10, Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x5A, 0x6B, 0x7A)), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(precioPanel);
            var qtyBorder = new Border { Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x0E, 0x12, 0x19)), CornerRadius = new CornerRadius(8), BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x26, 0x31, 0x47)), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left };
            var qtySp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var btnMenos = new Button { Content = new TextBlock { Text = "−", Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x94, 0xA3, 0xB8)), FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.Bold }, Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0), Padding = new Thickness(8, 2, 8, 2), Tag = item.Id, MinWidth = 0, MinHeight = 0 };
            btnMenos.Click += CartCantidadMenos_Click;
            var qtyTextBorder = new Border { Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x00, 0x84, 0xFF)), CornerRadius = new CornerRadius(4), Padding = new Thickness(10, 2, 10, 2), Child = new TextBlock { Text = item.Cantidad.ToString(), Foreground = new SolidColorBrush(Colors.White), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold } };
            var btnMas = new Button { Content = new TextBlock { Text = "+", Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x94, 0xA3, 0xB8)), FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.Bold }, Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0), Padding = new Thickness(8, 2, 8, 2), Tag = item.Id, MinWidth = 0, MinHeight = 0 };
            btnMas.Click += CartCantidadMas_Click;
            qtySp.Children.Add(btnMenos);
            qtySp.Children.Add(qtyTextBorder);
            qtySp.Children.Add(btnMas);
            qtyBorder.Child = qtySp;
            sp.Children.Add(qtyBorder);
            Grid.SetColumn(sp, 1);
            grid.Children.Add(sp);
            var rightSp = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            var btnEliminar = new Button { Content = new TextBlock { Text = "✕", Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xF4, 0x3F, 0x5E)), FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.Bold }, Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0), Padding = new Thickness(6), Tag = item.Id, HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 0, MinHeight = 0 };
            btnEliminar.Click += CartEliminar_Click;
            rightSp.Children.Add(btnEliminar);
            var totalText = new TextBlock { Text = $"S/ {item.Precio * item.Cantidad:0.00}", Foreground = new SolidColorBrush(Colors.White), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Right };
            totalText.Tag = item.Id;
            totalText.Tapped += PrecioCart_Tapped;
            rightSp.Children.Add(totalText);
            Grid.SetColumn(rightSp, 2);
            grid.Children.Add(rightSp);
            border.Child = grid;
            return border;
        }

        // Miniatura del carrito: foto del plato si tiene, icono si no.
        // La foto va como fondo del borde (respeta las esquinas redondeadas).
        // Si aún carga, se suscribe una sola vez y se suelta al mostrar o
        // al destruir la fila (las filas se reconstruyen en cada cambio).
        private void PintarMiniatura(Border contenedor, string nombrePlato)
        {
            var fondoVacio = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x0E, 0x12, 0x19));
            var icono = new FontIcon { Glyph = "\uEB4B", FontSize = 20, Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x64, 0x74, 0x8B)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var plato = _platos.FirstOrDefault(p => string.Equals(p.Nombre, nombrePlato, StringComparison.OrdinalIgnoreCase));
            if (plato == null || plato.FotoSource == null)
            {
                contenedor.Background = fondoVacio;
                contenedor.Child = icono;
                return;
            }
            if (plato.FotoVisible == Visibility.Visible)
            {
                contenedor.Background = new ImageBrush { ImageSource = plato.FotoSource, Stretch = Stretch.UniformToFill };
                contenedor.Child = null;
            }
            else
            {
                contenedor.Background = fondoVacio;
                contenedor.Child = icono;
                System.ComponentModel.PropertyChangedEventHandler? h = null;
                h = (s, e) =>
                {
                    if (e.PropertyName == nameof(PlatoItem.FotoVisible) && plato.FotoVisible == Visibility.Visible)
                    {
                        contenedor.Background = new ImageBrush { ImageSource = plato.FotoSource, Stretch = Stretch.UniformToFill };
                        contenedor.Child = null;
                        if (h != null) plato.PropertyChanged -= h;
                    }
                };
                plato.PropertyChanged += h;
                contenedor.Unloaded += (_, _) => { if (h != null) plato.PropertyChanged -= h; };
            }
        }

        // Abre el picker de mesas (lo usa la pildora de la barra de titulo).
        public void AbrirSelectorMesas()
        {
            if (_enModoCierre)
            {
                SalirModoCierre();
            }
            CartRoot.Visibility = Visibility.Collapsed;
            MesasRoot.Visibility = Visibility.Visible;
            ActualizarEstadoCatalogo();
        }

        private void MesasPanel_CerrarRequested(object? sender, EventArgs e)
        {
            MesasRoot.Visibility = Visibility.Collapsed;
            ActualizarEstadoCatalogo();
            CartRoot.Visibility = Visibility.Visible;
        }

        private void MesasPanel_MesaSeleccionada(object? sender, string mesa)
        {
            FijarMesaActual(mesa);
            if (_enModoCierre)
            {
                CierreMesaTitleText.Text = $"Cerrar · {mesa}";
                var orden = ObtenerOrden(mesa);
                CierreProductosPanel.Children.Clear();
                _cierreSeleccionIds.Clear();
                foreach (var item in orden)
                {
                    CierreProductosPanel.Children.Add(CrearFilaCierre(item));
                    _cierreSeleccionIds.Add(item.Id);
                }
                ActualizarCierreTotales();
                ActualizarCartUI();
                ActualizarCatalogoSeleccion();
                MesasRoot.Visibility = Visibility.Collapsed;
                ActualizarEstadoCatalogo();
                return;
            }
            MesasRoot.Visibility = Visibility.Collapsed;
            ActualizarEstadoCatalogo();
            CartRoot.Visibility = Visibility.Visible;
            ActualizarCartUI();
            ActualizarCatalogoSeleccion();
            ShowMesaToast(mesa);
        }

        private void MesasPanel_MesaAbierta(object? sender, string mesa)
        {
            FijarMesaActual(mesa);
            if (_enModoCierre)
            {
                CierreMesaTitleText.Text = $"Cerrar · {mesa}";
                var orden = ObtenerOrden(mesa);
                CierreProductosPanel.Children.Clear();
                _cierreSeleccionIds.Clear();
                foreach (var item in orden)
                {
                    CierreProductosPanel.Children.Add(CrearFilaCierre(item));
                    _cierreSeleccionIds.Add(item.Id);
                }
                ActualizarCierreTotales();
                ActualizarCartUI();
                ActualizarCatalogoSeleccion();
                MesasRoot.Visibility = Visibility.Collapsed;
                ActualizarEstadoCatalogo();
                return;
            }
            MesasRoot.Visibility = Visibility.Collapsed;
            ActualizarEstadoCatalogo();
            CartRoot.Visibility = Visibility.Visible;
            ActualizarCartUI();
            ActualizarCatalogoSeleccion();
            ShowMesaAbiertaToast();
        }

        private void ShowMesaAbiertaToast()
        {
            MesaAbiertaToast.Visibility = Visibility.Visible;
            _mesaAbiertaStoryboard ??= BuildMesaAbiertaToastStoryboard();
            _mesaAbiertaStoryboard.Stop();
            _mesaAbiertaStoryboard.Begin();
        }

        private Storyboard BuildMesaAbiertaToastStoryboard()
        {
            var sb = new Storyboard();
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };
            var tIn = TimeSpan.FromMilliseconds(180);
            var tEnd = TimeSpan.FromMilliseconds(2200);

            var opacity = new DoubleAnimationUsingKeyFrames();
            opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 0 });
            opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = tIn, Value = 1, EasingFunction = easeOut });
            opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(1800), Value = 1 });
            opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = tEnd, Value = 0, EasingFunction = easeIn });
            Storyboard.SetTarget(opacity, MesaAbiertaToast);
            Storyboard.SetTargetProperty(opacity, "Opacity");

            var slide = new DoubleAnimationUsingKeyFrames();
            slide.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = -14 });
            slide.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = tIn, Value = 0, EasingFunction = easeOut });
            slide.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(1800), Value = 0 });
            slide.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = tEnd, Value = -10, EasingFunction = easeIn });
            Storyboard.SetTarget(slide, MesaAbiertaToastTranslate);
            Storyboard.SetTargetProperty(slide, "Y");

            sb.Children.Add(opacity);
            sb.Children.Add(slide);
            sb.Completed += (_, __) => MesaAbiertaToast.Visibility = Visibility.Collapsed;
            return sb;
        }

        private void CerrarMesaButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_enModoCierre)
            {
                var orden = ObtenerOrden(_mesaActual);
                if (orden.Count == 0) return;
                EntrarModoCierre();
                return;
            }
            // Ya en modo cierre -> confirmar la seleccion
            ConfirmarCierreParcial();
        }

        private void EntrarModoCierre()
        {
            _enModoCierre = true;
            _cierreSeleccionIds.Clear();
            CatalogoRoot.Visibility = Visibility.Collapsed;
            CierreMesaRoot.Visibility = Visibility.Visible;
            CierreMesaTitleText.Text = $"Cerrar · {_mesaActual}";
            // Poblar productos
            var orden = ObtenerOrden(_mesaActual);
            CierreProductosPanel.Children.Clear();
            foreach (var item in orden)
            {
                CierreProductosPanel.Children.Add(CrearFilaCierre(item));
            }
            ActualizarCierreTotales();
            // Transformar boton
            CerrarMesaButton.Background = ConfirmarCierreBrush;
            CerrarMesaButtonText.Text = "Confirmar cierre";
            CerrarMesaButtonIcon.Glyph = "\uE73E";
            // Reset pago seleccion
            _pagoCuotas = 1;
            _pagoMetodo = "Tarjeta";
            ActualizarPagoCuotasUI();
            ActualizarPagoMetodoUI();
        }

        private void SalirModoCierre()
        {
            _enModoCierre = false;
            CierreMesaRoot.Visibility = Visibility.Collapsed;
            CatalogoRoot.Visibility = Visibility.Visible;
            CerrarMesaButton.Background = CerrarNormalBrush;
            CerrarMesaButtonText.Text = "Cerrar Mesa";
            CerrarMesaButtonIcon.Glyph = "\uE8C7";
            ActualizarCartUI();
            ActualizarEstadoCatalogo();
        }

        private Border CrearFilaCierre(CartItem item)
        {
            var border = new Border { Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x16, 0x1C, 0x24)), CornerRadius = new CornerRadius(10), BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x2A, 0x36, 0x47)), BorderThickness = new Thickness(1), Padding = new Thickness(10) };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var iconBorder = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x0F, 0x17, 0x2A)), BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x2A, 0x36, 0x47)), BorderThickness = new Thickness(1) };
            iconBorder.Child = new FontIcon { Glyph = "\uEB4B", FontSize = 16, Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x64, 0x74, 0x8B)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(iconBorder, 0);
            grid.Children.Add(iconBorder);
            var sp = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = item.Nombre.ToUpper(), Foreground = new SolidColorBrush(Colors.White), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
            sp.Children.Add(new TextBlock { Text = $"$ {item.Precio:0.00}", Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x94, 0xA3, 0xB8)), FontSize = 11 });
            Grid.SetColumn(sp, 1);
            grid.Children.Add(sp);
            var cb = new CheckBox { IsChecked = true, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Tag = item.Id, MinWidth = 0 };
            cb.Checked += CierreCheck_Changed;
            cb.Unchecked += CierreCheck_Changed;
            _cierreSeleccionIds.Add(item.Id);
            Grid.SetColumn(cb, 2);
            grid.Children.Add(cb);
            border.Child = grid;
            return border;
        }

        private void CierreCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is string id)
            {
                if (cb.IsChecked == true) _cierreSeleccionIds.Add(id);
                else _cierreSeleccionIds.Remove(id);
                ActualizarCierreTotales();
            }
        }

        private void ActualizarCierreTotales()
        {
            var orden = ObtenerOrden(_mesaActual);
            var seleccionados = orden.Where(i => _cierreSeleccionIds.Contains(i.Id)).ToList();
            double total = seleccionados.Sum(i => i.Precio * i.Cantidad);
            int count = seleccionados.Count;
            if (count == 0)
            {
                PagoSeleccionInfoText.Text = "Selecciona productos para pagar";
                CerrarMesaButtonText.Text = "Confirmar cierre";
            }
            else
            {
                PagoSeleccionInfoText.Text = $"{count} producto{(count == 1 ? "" : "s")} seleccionado{(count == 1 ? "" : "s")} · S/ {total:0.00}";
                CerrarMesaButtonText.Text = $"Confirmar cierre   S/ {total:0.00}";
            }
            // Actualizar total del carrito visible (opcional)
            // CartTotalText muestra el total de la mesa; en modo cierre mostramos el seleccionado en el boton
        }

        // Confirma el cierre de lo seleccionado: genera UNA boleta con su número
        // (cierre total = 1 boleta; cada parcial = 1 boleta distinta), la registra
        // en el historial y muestra la factura. Sin backend de nube: JSON local.
        private async void ConfirmarCierreParcial()
        {
            var orden = ObtenerOrden(_mesaActual);
            var seleccionados = orden.Where(i => _cierreSeleccionIds.Contains(i.Id)).ToList();
            if (seleccionados.Count == 0) return;
            double total = seleccionados.Sum(i => i.Precio * i.Cantidad);
            string ticket;
            Venta venta;
            try
            {
                ticket = await VentasStore.SiguienteTicketAsync();
                venta = new Venta
                {
                    Ticket = ticket,
                    FechaHora = DateTime.Now,
                    MesaEtiqueta = _mesaActual,
                    Cliente = _clienteSeleccionado?.Nombre ?? "Cliente Ocasional",
                    Mozo = _vendedorSeleccionado?.Nombre ?? "Mostrador",
                    MetodoPago = (_pagoMetodo ?? "").Trim(),
                    Comprobante = _comprobanteTipo,
                    Items = seleccionados.Select(i => new ItemVenta { Nombre = i.Nombre, Precio = i.Precio, Cantidad = i.Cantidad }).ToList(),
                    Total = Math.Round(total, 2),
                    // Solo Credito es fiado: va a POR COBRAR. Cualquier otro
                    // metodo (Efectivo/Tarjeta/Yape/Otros) es PAGADO directo.
                    Estado = EsMetodoCredito(_pagoMetodo) ? "POR COBRAR" : "PAGADO",
                    MesaBadgeBg = BadgeFondoDe(_mesaActual),
                    MesaBadgeBorde = BadgeBordeDe(_mesaActual),
                    MesaBadgeFg = BadgeLetraDe(_mesaActual),
                };
                await VentasStore.RegistrarAsync(venta);
            }
            catch (InvalidOperationException)
            {
                return;
            }
            _ultimoCierreSeleccion = seleccionados;
            _ultimoCierreTotal = total;
            // Descuento definitivo: lo cobrado sale del stock del catálogo
            // (mejor esfuerzo; si falla igual se muestra la factura ya registrada).
            _ = DescontarStockCatalogoAsync(seleccionados);
            // Remover los seleccionados de la orden
            foreach (var it in seleccionados) orden.Remove(it);
            // Mostrar factura para este parcial
            FacturaTotalText.Text = $"{total:0.00}";
            FacturaNumeroText.Text = $"{ticket}  •  CONFIRMAR";
            FacturaDialog.Visibility = Visibility.Visible;
            // Impresion automatica al confirmar (solo si el switch esta prendido).
            if (ImprimirTicketSwitch?.IsOn == true)
                _ = ImprimirCierreAsync(venta);
        }

        // Imprime la boleta recien cerrada sin bloquear; avisa con el toast.
        private async System.Threading.Tasks.Task ImprimirCierreAsync(Venta venta)
        {
            try
            {
                var (ok, error) = await Services.ImpresionTickets.ImprimirTextoAsync(Services.ImpresionTickets.TextoVenta(venta));
                DispatcherQueue.TryEnqueue(() => ShowMesaToast(ok ? $"Ticket {venta.Ticket} impreso" : $"No se pudo imprimir: {error}"));
            }
            catch { }
        }

        // Switch de impresion: se recuerda entre turnos (LocalSettings).
        private void ImprimirTicketSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (ImprimirTicketSwitch == null) return;
            bool prendido = ImprimirTicketSwitch.IsOn;
            try { Windows.Storage.ApplicationData.Current.LocalSettings.Values["Pos.ImprimirAlConfirmar"] = prendido; }
            catch { }
            if (ImprimirTicketSubText != null)
                ImprimirTicketSubText.Text = prendido ? "Sale en papel al confirmar" : "No se imprimirá";
        }

        // Restaura el switch (por defecto prendido).
        private void RestaurarSwitchImpresion()
        {
            try
            {
                if (ImprimirTicketSwitch != null && Windows.Storage.ApplicationData.Current.LocalSettings.Values["Pos.ImprimirAlConfirmar"] is bool b)
                {
                    ImprimirTicketSwitch.IsOn = b;
                    if (ImprimirTicketSubText != null)
                        ImprimirTicketSubText.Text = b ? "Sale en papel al confirmar" : "No se imprimirá";
                }
            }
            catch { }
        }

        // Descuenta del JSON lo cobrado por nombre y refresca la tarjeta en memoria.
        // Los 12 de la casa son fijos (demo) y no tienen stock persistente.
        private async System.Threading.Tasks.Task DescontarStockCatalogoAsync(System.Collections.Generic.List<CartItem> cobrados)
        {
            try
            {
                foreach (var grupo in cobrados.GroupBy(i => i.Nombre))
                {
                    var plato = await PlatosStore.ObtenerPorNombreAsync(grupo.Key);
                    if (plato == null) continue;
                    int nuevo = plato.Porciones - grupo.Sum(i => i.Cantidad);
                    if (nuevo == plato.Porciones) continue;
                    await PlatosStore.ActualizarStockAsync(plato.Plu, nuevo);
                    var tarjeta = _platos.FirstOrDefault(p => string.Equals(p.Nombre, grupo.Key, StringComparison.OrdinalIgnoreCase));
                    if (tarjeta != null) tarjeta.StockInicial = nuevo;
                }
            }
            catch { }
        }

        // Badge de mesa para la boleta: naranja salón, verde llevar, celeste barra.
        private static string BadgeFondoDe(string mesa)
        {
            if (mesa.StartsWith("Llevar", StringComparison.OrdinalIgnoreCase) || mesa.StartsWith("Para llevar", StringComparison.OrdinalIgnoreCase)) return "#10b98126";
            if (mesa.StartsWith("Barra", StringComparison.OrdinalIgnoreCase)) return "#0ea5e926";
            if (mesa.StartsWith("Mesa", StringComparison.OrdinalIgnoreCase)) return "#ea580c26";
            return "#64748b26";
        }

        private static string BadgeBordeDe(string mesa)
        {
            if (mesa.StartsWith("Llevar", StringComparison.OrdinalIgnoreCase) || mesa.StartsWith("Para llevar", StringComparison.OrdinalIgnoreCase)) return "#10b9814d";
            if (mesa.StartsWith("Barra", StringComparison.OrdinalIgnoreCase)) return "#0ea5e94d";
            if (mesa.StartsWith("Mesa", StringComparison.OrdinalIgnoreCase)) return "#ea580c4d";
            return "#64748b4d";
        }

        private static string BadgeLetraDe(string mesa)
        {
            if (mesa.StartsWith("Llevar", StringComparison.OrdinalIgnoreCase) || mesa.StartsWith("Para llevar", StringComparison.OrdinalIgnoreCase)) return "#6ee7b7";
            if (mesa.StartsWith("Barra", StringComparison.OrdinalIgnoreCase)) return "#7dd3fc";
            if (mesa.StartsWith("Mesa", StringComparison.OrdinalIgnoreCase)) return "#fb923c";
            return "#cbd5e1";
        }

        private void CierreVolverButton_Click(object sender, RoutedEventArgs e)
        {
            SalirModoCierre();
        }

        private void PagoCuota_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string tag) return;
            if (int.TryParse(tag, out var n)) _pagoCuotas = n; else _pagoCuotas = 1;
            ActualizarPagoCuotasUI();
        }

        private void ActualizarPagoCuotasUI()
        {
            foreach (var child in PagoCuotasPanel.Children)
            {
                if (child is not Button b || b.Tag is not string t) continue;
                bool sel = t == _pagoCuotas.ToString() || (t == "Otro" && _pagoCuotas > 6);
                b.Background = sel ? new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x00, 0x80, 0xFF)) : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x16, 0x1C, 0x24));
                b.BorderBrush = sel ? new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x00, 0x80, 0xFF)) : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x2A, 0x36, 0x47));
                if (b.Content is TextBlock tb) tb.Foreground = sel ? new SolidColorBrush(Colors.White) : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xCB, 0xD5, 0xE1));
            }
        }

        private void PagoMetodo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string tag) return;
            _pagoMetodo = (tag ?? "").Trim();
            ActualizarPagoMetodoUI();
        }

        // Solo el boton Credito es fiado (case-insensible, sin espacios).
        // Todo lo demas cobra al instante y debe guardarse PAGADO.
        private static bool EsMetodoCredito(string? metodo) =>
            string.Equals((metodo ?? "").Trim(), "Crédito", StringComparison.OrdinalIgnoreCase)
            || string.Equals((metodo ?? "").Trim(), "Credito", StringComparison.OrdinalIgnoreCase);

        // Resalta el método elegido (azul; Crédito en rojo por ser fiado).
        private void ActualizarPagoMetodoUI()
        {
            if (PagoMetodosGrid == null) return;
            foreach (var child in PagoMetodosGrid.Children)
            {
                if (child is not Button b || b.Tag is not string tag) continue;
                bool sel = string.Equals((tag ?? "").Trim(), (_pagoMetodo ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
                bool esCredito = EsMetodoCredito(tag);
                b.Background = sel
                    ? new SolidColorBrush(esCredito ? ColorHelper.FromArgb(0xFF, 0x3D, 0x0F, 0x19) : ColorHelper.FromArgb(0xFF, 0x1A, 0x3A, 0x4A))
                    : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x16, 0x1C, 0x24));
                b.BorderBrush = sel
                    ? new SolidColorBrush(esCredito ? ColorHelper.FromArgb(0xFF, 0xF4, 0x3F, 0x5E) : ColorHelper.FromArgb(0xFF, 0x00, 0x80, 0xFF))
                    : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x2A, 0x36, 0x47));
                if (b.Content is StackPanel sp)
                {
                    foreach (var sub in sp.Children)
                    {
                        if (sub is FontIcon icono) icono.Foreground = sel
                            ? new SolidColorBrush(esCredito ? ColorHelper.FromArgb(0xFF, 0xFB, 0x71, 0x85) : ColorHelper.FromArgb(0xFF, 0x00, 0xC2, 0xFF))
                            : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x94, 0xA3, 0xB8));
                        if (sub is TextBlock texto) texto.Foreground = sel ? new SolidColorBrush(Colors.White) : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xCB, 0xD5, 0xE1));
                    }
                }
            }
        }

        private void CerrarMesaButton_Old(object sender, RoutedEventArgs e)
        {
            FacturaTotalText.Text = "50.00";
            FacturaNumeroText.Text = $"TM{DateTime.Now:yyMMddHHmmss}  •  CONFIRMAR";
            FacturaDialog.Visibility = Visibility.Visible;
        }

        private void FacturaContinuarButton_Click(object sender, RoutedEventArgs e)
        {
            FacturaDialog.Visibility = Visibility.Collapsed;
            // Si estabamos en modo cierre parcial, decidir si la mesa se cierra del todo
            if (_enModoCierre)
            {
                var orden = ObtenerOrden(_mesaActual);
                ActualizarCartUI();
                if (orden.Count == 0)
                {
                    // Mesa vacia -> cerrarla y volver al flujo inicial sin mesa.
                    string cerrada = _mesaActual;
                    MesasPanel.MarcarMesaCerrada(cerrada);
                    SalirModoCierre();
                    FijarMesaActual("");
                    MesasRoot.Visibility = Visibility.Visible;
                    ActualizarCartUI();
                    ActualizarCatalogoSeleccion();
                    ActualizarEstadoCatalogo();
                    ShowMesaToast($"{cerrada} cerrada");
                }
                else
                {
                    // Quedan productos -> refrescar la lista de cierre
                    CierreProductosPanel.Children.Clear();
                    foreach (var item in orden) CierreProductosPanel.Children.Add(CrearFilaCierre(item));
                    _cierreSeleccionIds.Clear();
                    foreach (var it in orden) _cierreSeleccionIds.Add(it.Id);
                    ActualizarCierreTotales();
                }
            }
            else
            {
                // Cierre simple (sin modo split) -> marcar cerrada
                MesasPanel.MarcarMesaCerrada(_mesaActual);
            }
        }

        // ========== Cliente / Vendedor (dialogos oscuros con colores distintos) ==========
        private void ClienteSelectorButton_Click(object sender, RoutedEventArgs e)
        {
            if (_clientes.Count == 0) InicializarClientesMock();
            PoblarClientesList(ClienteBusquedaTextBox?.Text ?? "");
            ComprobanteSelectorPanel.Visibility = Visibility.Visible;
            ClientesSistemaPanel.Visibility = Visibility.Collapsed;
            SeleccionarClienteSistemaChevron.Glyph = "\uE70D";
            ClienteDatosPanel.Visibility = Visibility.Collapsed;
            BoletaDatosPanel.Visibility = Visibility.Collapsed;
            FacturaDatosPanel.Visibility = Visibility.Collapsed;
            AceptarClienteButton.IsEnabled = false;
            AceptarClienteButton.Opacity = 0.5;
            _clienteSeleccionado = null;
            _modoNuevoCliente = false;
            _comprobanteTipo = "Nota";
            ActualizarComprobanteUI();
            ActualizarClienteDatosVisibilidad();
            ActualizarClienteAceptarEstado();
            SeleccionarClienteDialog.Visibility = Visibility.Visible;
        }

        private void SeleccionarClienteSistemaButton_Click(object sender, RoutedEventArgs e)
        {
            bool isVisible = ClientesSistemaPanel.Visibility == Visibility.Visible;
            ClientesSistemaPanel.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
            SeleccionarClienteSistemaChevron.Glyph = isVisible ? "\uE70D" : "\uE70E";
        }

        private void CerrarClienteDialog_Click(object sender, RoutedEventArgs e) => SeleccionarClienteDialog.Visibility = Visibility.Collapsed;

        private void ClienteBusqueda_TextChanged(object sender, TextChangedEventArgs e) => PoblarClientesList(ClienteBusquedaTextBox.Text);

        private void InicializarClientesMock()
        {
            _clientes.Clear();
            _clientes.Add(new ClienteItem("0", "Cliente Ocasional"));
        }

        private void PoblarClientesList(string filtro)
        {
            ClientesListPanel.Children.Clear();
            var q = string.IsNullOrWhiteSpace(filtro) ? _clientes : _clientes.Where(c => c.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase) || c.Id.Contains(filtro)).ToList();
            foreach (var c in q) ClientesListPanel.Children.Add(CrearFilaCliente(c));
            if (q.Count == 0)
                ClientesListPanel.Children.Add(new TextBlock { Text = "Sin resultados", Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x5A, 0x6B, 0x7A)), FontSize = 12, Margin = new Thickness(16, 20, 0, 0) });
        }

        private Border CrearFilaCliente(ClienteItem cliente)
        {
            bool sel = _clienteSeleccionado?.Id == cliente.Id;
            var bg = sel ? new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x14, 0x2E, 0x2B)) : new SolidColorBrush(Colors.Transparent);
            var border = new Border { Background = bg, CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10, 12, 10) };
            border.Tapped += (s, e) => SeleccionarCliente(cliente);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var avatar = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x1C, 0x2E, 0x35)), BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x1A, 0x3A, 0x3E)), BorderThickness = new Thickness(1) };
            var avatarText = cliente.Nombre.Length > 0 ? cliente.Nombre.Substring(0, 1).ToUpper() : "C";
            var avatarGrid = new Grid();
            avatarGrid.Children.Add(new TextBlock { Text = avatarText, Foreground = new SolidColorBrush(Colors.White), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            avatarGrid.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x00, 0xC9, 0x82)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, -2, -2) });
            avatar.Child = avatarGrid;
            Grid.SetColumn(avatar, 0);
            grid.Children.Add(avatar);
            var sp = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = cliente.Nombre, Foreground = new SolidColorBrush(Colors.White), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            var idSp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            idSp.Children.Add(new TextBlock { Text = $"ID: {cliente.Id}", Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x5A, 0x6B, 0x7A)), FontSize = 11 });
            idSp.Children.Add(new TextBlock { Text = "• Activo", Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x00, 0xC9, 0x82)), FontSize = 11 });
            sp.Children.Add(idSp);
            Grid.SetColumn(sp, 1);
            grid.Children.Add(sp);
            var more = new TextBlock { Text = "…", Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x5A, 0x6B, 0x7A)), FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            Grid.SetColumn(more, 2);
            grid.Children.Add(more);
            border.Child = grid;
            return border;
        }

        private void SeleccionarCliente(ClienteItem cliente)
        {
            _clienteSeleccionado = cliente;
            _modoNuevoCliente = false;
            // Comprobante ya esta visible arriba; solo refrescar datos y estado
            ActualizarClienteDatosVisibilidad();
            PoblarClientesList(ClienteBusquedaTextBox.Text);
            ActualizarClienteAceptarEstado();
        }

        private void Comprobante_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string tag) return;
            _comprobanteTipo = tag;
            ActualizarComprobanteUI();
            ActualizarClienteDatosVisibilidad();
            ActualizarClienteAceptarEstado();
        }

        private void ActualizarComprobanteUI()
        {
            if (ComprobanteNotaButton == null) return;
            var selBg = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x14, 0xB8, 0xA6));
            var idleBg = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x0A, 0x15, 0x18));
            var selFg = new SolidColorBrush(Colors.White);
            var idleFg = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x94, 0xA3, 0xB8));
            var idleBr = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x1A, 0x2E, 0x33));
            var selBr = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x14, 0xB8, 0xA6));
            void Set(Button btn, string tag)
            {
                bool sel = tag == _comprobanteTipo;
                btn.Background = sel ? selBg : idleBg;
                btn.BorderBrush = sel ? selBr : idleBr;
                if (btn.Content is TextBlock tb) tb.Foreground = sel ? selFg : idleFg;
            }
            Set(ComprobanteNotaButton, "Nota");
            Set(ComprobanteBoletaButton, "Boleta");
            Set(ComprobanteFacturaButton, "Factura");
        }

        private void AceptarClienteButton_Click(object sender, RoutedEventArgs e)
        {
            // Si es Boleta/Factura y hay datos de nuevo cliente, crear/actualizar
            if (_comprobanteTipo == "Boleta" || _comprobanteTipo == "Factura")
            {
                bool esNuevo = _modoNuevoCliente || _clienteSeleccionado == null || _clienteSeleccionado.Id == "0";
                if (esNuevo)
                {
                    if (_comprobanteTipo == "Boleta")
                    {
                        string dni = BoletaDniTextBox.Text.Trim();
                        string nombre = BoletaNombreTextBox.Text.Trim();
                        if (dni.Length != 8 || string.IsNullOrWhiteSpace(nombre)) return;
                        var nuevo = new ClienteItem(dni, nombre);
                        _clientes.Add(nuevo);
                        _clienteSeleccionado = nuevo;
                    }
                    else
                    {
                        string ruc = FacturaRucTextBox.Text.Trim();
                        string razon = FacturaRazonTextBox.Text.Trim();
                        if (ruc.Length != 11 || string.IsNullOrWhiteSpace(razon)) return;
                        var nuevo = new ClienteItem(ruc, razon);
                        _clientes.Add(nuevo);
                        _clienteSeleccionado = nuevo;
                    }
                }
            }
            if (_clienteSeleccionado == null) return;
            string sufijo = _comprobanteTipo == "Nota" ? "" : $" · {_comprobanteTipo}";
            ClienteNombreText.Text = _clienteSeleccionado.Nombre + sufijo;
            SeleccionarClienteDialog.Visibility = Visibility.Collapsed;
        }

        private void NuevoClienteButton_Click(object sender, RoutedEventArgs e)
        {
            _modoNuevoCliente = true;
            _clienteSeleccionado = null;
            ClienteBusquedaTextBox.Text = "";
            PoblarClientesList("");
            ActualizarClienteDatosVisibilidad();
            if (_comprobanteTipo == "Boleta") BoletaDniTextBox.Focus(FocusState.Programmatic);
            else if (_comprobanteTipo == "Factura") FacturaRucTextBox.Focus(FocusState.Programmatic);
            ActualizarClienteAceptarEstado();
        }

        private void ClienteDatos_TextChanged(object sender, TextChangedEventArgs e) => ActualizarClienteAceptarEstado();

        private void ActualizarClienteDatosVisibilidad()
        {
            bool esBoleta = _comprobanteTipo == "Boleta";
            bool esFactura = _comprobanteTipo == "Factura";
            ClienteDatosPanel.Visibility = (esBoleta || esFactura) ? Visibility.Visible : Visibility.Collapsed;
            BoletaDatosPanel.Visibility = esBoleta ? Visibility.Visible : Visibility.Collapsed;
            FacturaDatosPanel.Visibility = esFactura ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ActualizarClienteAceptarEstado()
        {
            bool clienteOk = _clienteSeleccionado != null || _modoNuevoCliente;
            bool datosOk = true;
            if (_comprobanteTipo == "Boleta")
                datosOk = BoletaDniTextBox.Text.Trim().Length == 8 && !string.IsNullOrWhiteSpace(BoletaNombreTextBox.Text);
            else if (_comprobanteTipo == "Factura")
                datosOk = FacturaRucTextBox.Text.Trim().Length == 11 && !string.IsNullOrWhiteSpace(FacturaRazonTextBox.Text);
            bool enabled;
            if (_comprobanteTipo == "Nota")
                enabled = _clienteSeleccionado != null;
            else
                enabled = datosOk;
            AceptarClienteButton.IsEnabled = enabled;
            AceptarClienteButton.Opacity = enabled ? 1 : 0.5;
        }

        private void VendedorSelectorButton_Click(object sender, RoutedEventArgs e)
        {
            if (_vendedores.Count == 0) InicializarVendedoresMock();
            PoblarVendedoresList(VendedorBusquedaTextBox?.Text ?? "");
            AceptarVendedorButton.IsEnabled = false;
            AceptarVendedorButton.Opacity = 0.5;
            _vendedorSeleccionado = null;
            SeleccionarVendedorDialog.Visibility = Visibility.Visible;
        }

        private void CerrarVendedorDialog_Click(object sender, RoutedEventArgs e) => SeleccionarVendedorDialog.Visibility = Visibility.Collapsed;

        private void VendedorBusqueda_TextChanged(object sender, TextChangedEventArgs e) => PoblarVendedoresList(VendedorBusquedaTextBox.Text);

        private void InicializarVendedoresMock()
        {
            // Sin vendedores de muestra: se usa "Mostrador" hasta registrar personal.
            _vendedores.Clear();
        }

        private void PoblarVendedoresList(string filtro)
        {
            VendedoresListPanel.Children.Clear();
            var q = string.IsNullOrWhiteSpace(filtro) ? _vendedores : _vendedores.Where(v => v.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var v in q) VendedoresListPanel.Children.Add(CrearFilaVendedor(v));
            if (q.Count == 0)
                VendedoresListPanel.Children.Add(new TextBlock { Text = "Sin personal registrado", Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x5A, 0x6B, 0x7A)), FontSize = 12, Margin = new Thickness(16, 20, 0, 0) });
        }

        private Border CrearFilaVendedor(VendedorItem vendedor)
        {
            bool sel = _vendedorSeleccionado?.Id == vendedor.Id;
            var bg = sel ? new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x2A, 0x1F, 0x0A)) : new SolidColorBrush(Colors.Transparent);
            var border = new Border { Background = bg, CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10, 12, 10) };
            border.Tapped += (s, e) => SeleccionarVendedor(vendedor);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var avatar = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x2A, 0x1F, 0x0A)), BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x5A, 0x3E, 0x1A)), BorderThickness = new Thickness(1) };
            var avText = vendedor.Nombre.Length > 0 ? vendedor.Nombre.Substring(0, 1).ToUpper() : "V";
            avatar.Child = new TextBlock { Text = avText, Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xF5, 0x9E, 0x0B)), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(avatar, 0);
            grid.Children.Add(avatar);
            var sp = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = vendedor.Nombre, Foreground = new SolidColorBrush(Colors.White), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            sp.Children.Add(new TextBlock { Text = "Vendedor · Activo", Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xF5, 0x9E, 0x0B)), FontSize = 11 });
            Grid.SetColumn(sp, 1);
            grid.Children.Add(sp);
            border.Child = grid;
            return border;
        }

        private void SeleccionarVendedor(VendedorItem vendedor)
        {
            _vendedorSeleccionado = vendedor;
            PoblarVendedoresList(VendedorBusquedaTextBox.Text);
            AceptarVendedorButton.IsEnabled = true;
            AceptarVendedorButton.Opacity = 1;
        }

        private void AceptarVendedorButton_Click(object sender, RoutedEventArgs e)
        {
            if (_vendedorSeleccionado == null) return;
            VendedorNombreText.Text = _vendedorSeleccionado.Nombre;
            SeleccionarVendedorDialog.Visibility = Visibility.Collapsed;
        }

        // Muestra el toast con la mesa elegida y lo desvanece (efecto fantasma). Reutiliza el storyboard.
        private void ShowMesaToast(string mesa)
        {
            MesaToastText.Text = mesa;
            MesaToast.Visibility = Visibility.Visible;
            _mesaToastStoryboard ??= BuildMesaToastStoryboard();
            _mesaToastStoryboard.Stop();   // reinicia si ya estaba corriendo
            _mesaToastStoryboard.Begin();
        }

        // Catalogo vacio al arrancar: solo entra lo del JSON local (sin demo).
        // La tarjeta visual vive en el XAML (copia del ejemplo Nachis); aqui solo datos.
        private void InicializarPlatos()
        {
            if (PlatoGrid == null) return;
            _platos.Clear();
            PlatoGrid.ItemsSource = _platos;
            ReconstruirPillsPos();
            ActualizarCatalogoSeleccion();
        }

        // Pills desde los badges reales del catalogo (TODOS + los que haya).
        private void ReconstruirPillsPos()
        {
            if (CategoriaPillsPanel == null) return;
            var insignias = _platos.Select(p => p.Categoria).Distinct().OrderBy(c => c).ToList();
            CategoriaPillsPanel.Children.Clear();
            CategoriaPillsPanel.Children.Add(PildoraPos("todos", "TODOS", true));
            foreach (string insignia in insignias)
            {
                if (string.IsNullOrWhiteSpace(insignia)) continue;
                CategoriaPillsPanel.Children.Add(PildoraPos(insignia, EtiquetaPillPos(insignia), false));
            }
            _categoriaPosActiva = "todos";
        }

        // Etiqueta plural de cada badge compacto del POS.
        private static string EtiquetaPillPos(string insignia) => insignia switch
        {
            "CALDO" => "CALDOS",
            "ENTRADA" => "ENTRADAS",
            "BEBIDA" => "BEBIDAS",
            _ => "PLATOS",
        };

        // Pildora con la misma pinta de las fijas anteriores.
        private Button PildoraPos(string tag, string texto, bool activa)
        {
            var fondo = activa
                ? new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x00, 0x84, 0xFF))
                : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x16, 0x1C, 0x28));
            var tinta = activa
                ? new SolidColorBrush(Colors.White)
                : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xCB, 0xD5, 0xE1));
            var b = new Button
            {
                Tag = tag,
                Background = fondo,
                Foreground = tinta,
                BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x23, 0x2C, 0x3F)),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(16, 8, 16, 8),
                FontSize = 11,
                FontWeight = activa ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                BorderThickness = activa ? new Thickness(0) : new Thickness(1),
                Content = texto,
            };
            b.Click += CategoriaPosPill_Click;
            return b;
        }

        // Al mostrarse, suma al Grid los platos del JSON local (pausados y
        // agotados no entran). El catalogo arranca vacio: sin demo.
        private async void PuntoDeVentaView_Loaded(object sender, RoutedEventArgs e)
        {
            try { await RestaurarVentasPendientesAsync(); }
            catch { }
            try { await AgregarPlatosDelCatalogoAsync(); }
            catch { }
            RestaurarSwitchImpresion();
            ActualizarCartUI();
            ActualizarEstadoCatalogo();
        }

        // Escribe en disco la mesa actual + todas las ordenes con items.
        // No guarda hasta terminar la restauración inicial (ver _ventasRestauradas).
        private async System.Threading.Tasks.Task PersistirOrdenesAsync()
        {
            if (!_ventasRestauradas) return;
            try
            {
                var snapshot = new Dictionary<string, List<ItemOrdenGuardado>>();
                foreach (var kv in _mesasOrdenes)
                {
                    if (kv.Value == null || kv.Value.Count == 0) continue;
                    snapshot[kv.Key] = kv.Value
                        .Select(i => new ItemOrdenGuardado { Id = i.Id, Nombre = i.Nombre, Precio = i.Precio, Cantidad = i.Cantidad })
                        .ToList();
                }
                await VentasPendientesStore.GuardarAsync(_mesaActual, snapshot);
            }
            catch { }
        }

        // Al abrir, recupera las ordenes del disco: repone items, marca las
        // mesas OCUPADA y vuelve a la mesa que estaba atendida para continuar.
        private async System.Threading.Tasks.Task RestaurarVentasPendientesAsync()
        {
            var (mesa, ordenes) = await VentasPendientesStore.CargarAsync();
            _ventasRestauradas = true;
            if (ordenes.Count == 0) return;
            foreach (var kv in ordenes)
            {
                var lista = ObtenerOrden(kv.Key);
                lista.Clear();
                foreach (var it in kv.Value)
                    lista.Add(new CartItem(it.Id, it.Nombre, it.Precio, it.Cantidad));
                MesasPanel.MarcarMesaOcupada(kv.Key);
            }
            if (!string.IsNullOrEmpty(mesa) && ordenes.ContainsKey(mesa))
            {
                FijarMesaActual(mesa);
                MesasRoot.Visibility = Visibility.Collapsed;
                CartRoot.Visibility = Visibility.Visible;
                ActualizarCartUI();
                ActualizarCatalogoSeleccion();
                ShowMesaToast($"{mesa} recuperada");
            }
        }

        // Lee el almacen local y agrega lo que no este (por nombre) y tenga stock.
        // Los pausados/agotados (0 porciones) no se venden y no aparecen.
        private async System.Threading.Tasks.Task AgregarPlatosDelCatalogoAsync()
        {
            if (PlatoGrid == null) return;
            var catalogo = await PlatosStore.ListarAsync();
            var existentes = new HashSet<string>(_platos.Select(p => p.Nombre.Trim().ToLowerInvariant()));
            bool agrego = false;
            foreach (var item in catalogo)
            {
                if (item.Pausado) continue;
                if (existentes.Contains((item.Nombre ?? "").Trim().ToLowerInvariant())) continue;
                _platos.Add(PlatoItemDesdeCatalogo(item));
                agrego = true;
            }
            if (!agrego) return;
            ReconstruirPillsPos();
            AplicarFiltroCatalogo();
            ActualizarCatalogoSeleccion();
        }

        // Convierte un Plato del almacen a tarjeta del POS (badge y color POS).
        private static PlatoItem PlatoItemDesdeCatalogo(Models.Plato item)
        {
            string badge = BadgePosDe(item.Categoria);
            var (fondo, letra) = ColoresPosDe(badge);
            string glifo = badge switch
            {
                "CALDO" => "\uE896",
                "ENTRADA" => "\uE7EF",
                "BEBIDA" => "\uE8C3",
                _ => "\uEB4B",
            };
            return new PlatoItem(item.Nombre, item.Precio, badge, fondo, letra, glifo, item.Porciones, item.Unidad, item.FotoPath ?? "");
        }

        // La carta del POS solo maneja 4 badges; la categoria de tienda se compacta.
        private static string BadgePosDe(string categoria)
        {
            categoria ??= "";
            if (categoria.Contains("Bebida") || categoria.Contains("Cerveza") || categoria.Contains("ctel")) return "BEBIDA";
            if (categoria.Contains("Entrada") || categoria.Contains("Guarnici")) return "ENTRADA";
            return "PLATO";
        }

        // Mismos colores de badge de los 12 platos de la casa.
        private static (SolidColorBrush fondo, SolidColorBrush letra) ColoresPosDe(string badge) => badge switch
        {
            "CALDO" => (new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE6, 0x7E, 0x00)), new SolidColorBrush(Colors.White)),
            "ENTRADA" => (new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x32, 0x39, 0x4C)), new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xCB, 0xD5, 0xE1))),
            "BEBIDA" => (new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x70, 0x38, 0xE8)), new SolidColorBrush(Colors.White)),
            _ => (new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x00, 0xB0, 0x74)), new SolidColorBrush(Colors.White)),
        };

        // Buscador funcional: filtra por nombre y respeta la pill activa.
        private void BuscadorPlatosTextBox_TextChanged(object sender, TextChangedEventArgs e) => AplicarFiltroCatalogo();

        private void LimpiarBuscadorButton_Click(object sender, RoutedEventArgs e)
        {
            if (BuscadorPlatosTextBox != null) BuscadorPlatosTextBox.Text = "";
            AplicarFiltroCatalogo();
        }

        // Pills de categoria: pintan la activa en azul y filtran el Grid.
        private void CategoriaPosPill_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button pill || pill.Tag is not string tag) return;
            _categoriaPosActiva = tag;
            var azul = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x00, 0x84, 0xFF));
            var fondo = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x16, 0x1C, 0x28));
            var textoOn = new SolidColorBrush(Colors.White);
            var textoOff = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xCB, 0xD5, 0xE1));
            var borde = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x23, 0x2C, 0x3F));
            if (CategoriaPillsPanel != null)
            {
                foreach (var child in CategoriaPillsPanel.Children)
                {
                    if (child is not Button b) continue;
                    bool activa = b == pill;
                    b.Background = activa ? azul : fondo;
                    b.Foreground = activa ? textoOn : textoOff;
                    b.BorderBrush = borde;
                    b.BorderThickness = activa ? new Thickness(0) : new Thickness(1);
                }
            }
            AplicarFiltroCatalogo();
        }

        // Combina pill activa + texto del buscador; las tarjetas son los mismos
        // objetos, asi los badges de cantidad de la mesa no se pierden.
        private void AplicarFiltroCatalogo()
        {
            if (PlatoGrid == null) return;
            string query = (BuscadorPlatosTextBox?.Text ?? "").Trim().ToLowerInvariant();
            PlatoGrid.ItemsSource = _platos
                .Where(p => (_categoriaPosActiva == "todos" || p.Categoria == _categoriaPosActiva)
                    && (string.IsNullOrEmpty(query) || p.Nombre.ToLowerInvariant().Contains(query)))
                .ToList();
        }

        // Hay mesa activa cuando se eligió una (no vacío), el picker esta
        // cerrado y no se esta cobrando. Sin mesa el catalogo no vende.
        private bool HayMesaActiva() => !string.IsNullOrEmpty(_mesaActual) && MesasRoot != null && MesasRoot.Visibility == Visibility.Collapsed && !_enModoCierre;

        private void ActualizarEstadoCatalogo()
        {
            bool activa = HayMesaActiva();
            if (PlatoGrid != null) PlatoGrid.Opacity = activa ? 1 : 0.4;
            if (VeloSinMesa != null) VeloSinMesa.Visibility = activa ? Visibility.Collapsed : Visibility.Visible;
            if (AvisoSinMesa != null) AvisoSinMesa.Visibility = activa ? Visibility.Collapsed : Visibility.Visible;
        }

        // Toast glass minimalista: aparece y se desvanece sin bloquear la venta.
        private void ShowSinMesaToast()
        {
            if (SinMesaToast == null) return;
            SinMesaToast.Visibility = Visibility.Visible;
            _sinMesaStoryboard ??= BuildSinMesaToastStoryboard();
            _sinMesaStoryboard.Stop();
            _sinMesaStoryboard.Begin();
        }

        private Storyboard BuildSinMesaToastStoryboard()
        {
            var sb = new Storyboard();
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };
            var tIn = TimeSpan.FromMilliseconds(200);
            var tEnd = ToastVisibleTime + TimeSpan.FromMilliseconds(420);

            var opacity = new DoubleAnimationUsingKeyFrames();
            opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 0 });
            opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = tIn, Value = 1, EasingFunction = easeOut });
            opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = ToastVisibleTime, Value = 1 });
            opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = tEnd, Value = 0, EasingFunction = easeIn });
            Storyboard.SetTarget(opacity, SinMesaToast);
            Storyboard.SetTargetProperty(opacity, "Opacity");

            var slide = new DoubleAnimationUsingKeyFrames();
            slide.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 16 });
            slide.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = tIn, Value = 0, EasingFunction = easeOut });
            slide.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = ToastVisibleTime, Value = 0 });
            slide.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = tEnd, Value = -12, EasingFunction = easeIn });
            Storyboard.SetTarget(slide, SinMesaToastTranslate);
            Storyboard.SetTargetProperty(slide, "Y");

            sb.Children.Add(opacity);
            sb.Children.Add(slide);
            sb.Completed += (_, __) => SinMesaToast.Visibility = Visibility.Collapsed;
            return sb;
        }

        // Tap en la tarjeta (ItemClick del GridView): suma uno a la orden de la mesa.
        // Sin mesa activa no vende: muestra el aviso minimalista y no toca la orden.
        private void PlatoGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (!HayMesaActiva())
            {
                ShowSinMesaToast();
                return;
            }
            if (e.ClickedItem is not PlatoItem plato) return;
            AgregarProductoACarrito(plato.Nombre, plato.Precio, plato.Categoria);
        }

        // X de la tarjeta: vacia ese plato de la orden (unidades simples, todo de una vez).
        private void PlatoEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not PlatoItem plato) return;
            QuitarProductoDelCarrito(plato.Nombre);
        }

        // Elimina todas las lineas de ese plato en la mesa actual (platos = unidades simples).
        private void QuitarProductoDelCarrito(string nombre)
        {
            var orden = ObtenerOrden(_mesaActual);
            orden.RemoveAll(i => i.Nombre == nombre);
            ActualizarCartUI();
            ActualizarCatalogoSeleccion();
        }

        private void MostrarMesasSiTodasLibres()
        {
            bool todasLibres = _mesasOrdenes.Values.All(list => list.Count == 0);
            if (todasLibres)
            {
                CartRoot.Visibility = Visibility.Collapsed;
                MesasRoot.Visibility = Visibility.Visible;
                ActualizarEstadoCatalogo();
            }
        }

        private void AgregarProductoACarrito(string nombre, double precio, string categoria)
        {
            var orden = ObtenerOrden(_mesaActual);
            var existente = orden.FirstOrDefault(i => i.Nombre == nombre && i.Precio == precio);
            string id;
            if (existente != null)
            {
                int idx = orden.IndexOf(existente);
                orden[idx] = existente with { Cantidad = existente.Cantidad + 1 };
                id = existente.Id;
            }
            else
            {
                id = $"{_mesaActual}_{nombre}_{Guid.NewGuid().ToString()[..4]}";
                orden.Add(new CartItem(id, nombre, precio, 1));
            }
            ActualizarCartUI();
            LlevarCarritoAlItem(id);
        }

        // Autoscroll del carrito a la fila recién agregada (patrón Nachis).
        private void LlevarCarritoAlItem(string id)
        {
            if (CartProductosPanel == null) return;
            try
            {
                CartProductosPanel.UpdateLayout();
                foreach (var visual in CartProductosPanel.Items)
                {
                    if (visual is Border fila && fila.Tag is string tag && tag == id)
                    {
                        CartProductosPanel.ScrollIntoView(visual);
                        break;
                    }
                }
            }
            catch { }
        }

        private void CartCantidadMenos_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string id) return;
            var orden = ObtenerOrden(_mesaActual);
            var item = orden.FirstOrDefault(i => i.Id == id);
            if (item == null) return;
            if (item.Cantidad > 1)
            {
                int idx = orden.IndexOf(item);
                orden[idx] = item with { Cantidad = item.Cantidad - 1 };
            }
            else
            {
                orden.Remove(item);
            }
            ActualizarCartUI();
        }

        private void CartCantidadMas_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string id) return;
            var orden = ObtenerOrden(_mesaActual);
            var item = orden.FirstOrDefault(i => i.Id == id);
            if (item == null) return;
            int idx = orden.IndexOf(item);
            orden[idx] = item with { Cantidad = item.Cantidad + 1 };
            ActualizarCartUI();
        }

        private void CartEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string id) return;
            var orden = ObtenerOrden(_mesaActual);
            var item = orden.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                orden.Remove(item);
            }
            ActualizarCartUI();
        }

        private async void PrecioCart_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not string id) return;
            var orden = ObtenerOrden(_mesaActual);
            var item = orden.FirstOrDefault(i => i.Id == id);
            if (item == null) return;
            var tb = new TextBox { Text = item.Precio.ToString("0.00") };
            var dialog = new ContentDialog
            {
                Title = $"Precio - {item.Nombre}",
                Content = tb,
                PrimaryButtonText = "Guardar",
                CloseButtonText = "Cancelar",
                XamlRoot = this.XamlRoot
            };
            var res = await dialog.ShowAsync();
            if (res == ContentDialogResult.Primary)
            {
                if (double.TryParse(tb.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double nuevo) && nuevo > 0)
                {
                    int idx = orden.IndexOf(item);
                    orden[idx] = item with { Precio = nuevo };
                    ActualizarCartUI();
                }
            }
        }

        // Un solo storyboard con KEYFRAMES: una animacion por propiedad (in -> hold -> out).
        // OJO: no se puede poner 2 animaciones sobre la misma propiedad/elemento en el mismo Storyboard.
        private Storyboard BuildMesaToastStoryboard()
        {
            var sb = new Storyboard();
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };
            var tIn = TimeSpan.FromMilliseconds(200);
            var tEnd = ToastVisibleTime + TimeSpan.FromMilliseconds(420);

            // Opacity: 0 -> 1, se mantiene, -> 0
            var opacity = new DoubleAnimationUsingKeyFrames();
            opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 0 });
            opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = tIn, Value = 1, EasingFunction = easeOut });
            opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = ToastVisibleTime, Value = 1 });
            opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = tEnd, Value = 0, EasingFunction = easeIn });
            Storyboard.SetTarget(opacity, MesaToast);
            Storyboard.SetTargetProperty(opacity, "Opacity");

            // Y: 16 -> 0, se mantiene, -> -12
            var slide = new DoubleAnimationUsingKeyFrames();
            slide.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 16 });
            slide.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = tIn, Value = 0, EasingFunction = easeOut });
            slide.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = ToastVisibleTime, Value = 0 });
            slide.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = tEnd, Value = -12, EasingFunction = easeIn });
            Storyboard.SetTarget(slide, MesaToastTranslate);
            Storyboard.SetTargetProperty(slide, "Y");

            sb.Children.Add(opacity);
            sb.Children.Add(slide);
            sb.Completed += (_, __) => MesaToast.Visibility = Visibility.Collapsed;
            return sb;
        }
    }
}
