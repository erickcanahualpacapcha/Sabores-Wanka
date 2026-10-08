using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using sabores_wanka.Models;
using sabores_wanka.Services;
using Windows.Storage.Pickers;

namespace sabores_wanka.Views.Productos
{
    // Alta y edición de producto con guardado local en JSON (Services/PlatosStore).
    // Crear valida y genera PLU; editar carga los datos y guarda por el mismo PLU.
    public sealed partial class ProductosCreacionView : UserControl
    {
        // Estacion activa del grupo single-select (Tag del boton elegido).
        private string _estacionActiva = "caliente";

        // PLU en edición (null = alta nueva). El PLU no se cambia al editar.
        private string? _pluEnEdicion;

        // Categoría del plato en edición (se aplica al cargar las listas).
        private string? _categoriaPendiente;

        // Foto: pendiente (elegida, aún no copiada) y actual (ya guardada).
        private string? _fotoPendiente;
        private string _fotoActual = "";

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        public ProductosCreacionView()
        {
            this.InitializeComponent();
            this.Loaded += (_, _) => ActualizarMargen();
            PrecioTextBox.TextChanged += (_, _) => ActualizarMargen();
            CostoTextBox.TextChanged += (_, _) => ActualizarMargen();
            _ = CargarCategoriasAsync();
        }

        // Llena el combo con base + personalizadas (sin duplicar las del XAML).
        private async System.Threading.Tasks.Task CargarCategoriasAsync()
        {
            try
            {
                if (CategoriaComboBox == null) return;
                foreach (string c in await CategoriasStore.ListarAsync())
                {
                    bool existe = CategoriaComboBox.Items.OfType<ComboBoxItem>()
                        .Any(i => string.Equals(i.Content?.ToString(), c, StringComparison.OrdinalIgnoreCase));
                    if (!existe) CategoriaComboBox.Items.Add(new ComboBoxItem { Content = c });
                }
                if (!string.IsNullOrWhiteSpace(_categoriaPendiente))
                    SeleccionarCategoria(_categoriaPendiente);
            }
            catch { }
        }

        // Selecciona la categoría; si no está en el combo la agrega (vale para
        // las creadas en otro equipo o datos viejos: existen en el JSON).
        private void SeleccionarCategoria(string? categoria)
        {
            if (CategoriaComboBox == null || string.IsNullOrWhiteSpace(categoria)) return;
            string nombre = categoria.Trim();
            foreach (var item in CategoriaComboBox.Items)
            {
                if (item is ComboBoxItem ci && string.Equals(ci.Content?.ToString(), nombre, StringComparison.OrdinalIgnoreCase))
                {
                    CategoriaComboBox.SelectedItem = item;
                    return;
                }
            }
            var nuevo = new ComboBoxItem { Content = nombre };
            CategoriaComboBox.Items.Add(nuevo);
            CategoriaComboBox.SelectedItem = nuevo;
        }

        // Papelera: elimina la categoria SELECCIONADA (solo personalizadas).
        // El clic derecho dentro del desplegable no sirve en WinUI (el popup
        // se cierra antes de mostrar el menu), por eso es un boton directo.
        private async void EliminarCategoriaButton_Click(object sender, RoutedEventArgs e)
        {
            string? nombre = (CategoriaComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (string.IsNullOrWhiteSpace(nombre)) return;
            nombre = nombre.Trim();
            if (CategoriasStore.EsBase(nombre))
            {
                await MostrarDialogoAsync("No se puede eliminar", $"«{nombre}» es categoría base del sistema.", "Entendido");
                return;
            }
            var confirmar = new ContentDialog
            {
                Title = $"Eliminar «{nombre}»",
                Content = new TextBlock { Text = "La categoría saldrá de la lista. Los productos que la usen deben cambiarse primero.", TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = "Eliminar",
                CloseButtonText = "Cancelar",
                XamlRoot = this.XamlRoot
            };
            if (await confirmar.ShowAsync() != ContentDialogResult.Primary) return;
            try
            {
                await CategoriasStore.EliminarAsync(nombre);
            }
            catch (InvalidOperationException ex)
            {
                await MostrarDialogoAsync("No se pudo eliminar", ex.Message, "Entendido");
                return;
            }
            if (CategoriaComboBox != null)
            {
                var quitar = CategoriaComboBox.Items.OfType<ComboBoxItem>()
                    .FirstOrDefault(i => string.Equals(i.Content?.ToString(), nombre, StringComparison.OrdinalIgnoreCase));
                if (quitar != null)
                {
                    bool eraElegida = Equals(CategoriaComboBox.SelectedItem, quitar);
                    CategoriaComboBox.Items.Remove(quitar);
                    if (eraElegida && CategoriaComboBox.Items.Count > 0)
                        CategoriaComboBox.SelectedIndex = 0;
                }
            }
        }

        // Modo edición: precarga el formulario y cambia los textos de acción.
        // Llamar justo después de construir la vista, antes de mostrarla.
        public void CargarParaEdicion(Plato plato)
        {
            if (plato == null) return;
            _pluEnEdicion = (plato.Plu ?? "").Trim();
            NombreTextBox.Text = plato.Nombre ?? "";
            foreach (var item in TipoComboBox.Items)
            {
                if (item is ComboBoxItem ti && string.Equals(ti.Content?.ToString(), TipoDeUnidad(plato.Unidad), StringComparison.OrdinalIgnoreCase))
                {
                    TipoComboBox.SelectedItem = item;
                    break;
                }
            }
            _fotoActual = plato.FotoPath ?? "";
            _fotoPendiente = null;
            _ = MostrarFotoActualAsync();
            DescripcionTextBox.Text = plato.Descripcion ?? "";
            PrecioTextBox.Text = plato.Precio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            CostoTextBox.Text = plato.Costo.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            PorcionesTextBox.Text = plato.Porciones.ToString();
            _categoriaPendiente = plato.Categoria;
            SeleccionarCategoria(_categoriaPendiente);
            SeleccionarEstacion(string.IsNullOrWhiteSpace(plato.EstacionTag) ? "caliente" : plato.EstacionTag);
            if (RutaActualText != null) RutaActualText.Text = "Editar Producto";
            if (AccionPrincipalText != null) AccionPrincipalText.Text = "Guardar cambios";
            if (EliminarProductoButton != null) EliminarProductoButton.Visibility = Visibility.Visible;
            ActualizarMargen();
        }

        // Estaciones: pinta solo el boton del tag elegido en azul, resto neutro.
        private void EstacionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button elegido || elegido.Tag is not string tag) return;
            SeleccionarEstacion(tag);
        }

        private void SeleccionarEstacion(string tag)
        {
            _estacionActiva = tag;
            var azul = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x78, 0xD4));
            var neutro = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x27, 0x2A, 0x31));
            var textoOn = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
            var textoOff = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xC0, 0xC7, 0xD4));
            foreach (var child in EstacionesPanel.Children)
            {
                if (child is not Button b) continue;
                bool activo = string.Equals(b.Tag as string, tag, StringComparison.OrdinalIgnoreCase);
                b.Background = activo ? azul : neutro;
                if (b.Content is TextBlock tb)
                {
                    tb.Foreground = activo ? textoOn : textoOff;
                    tb.FontWeight = activo ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
                }
            }
        }

        // Tipo visible <-> unidad de stock ("Plato" <-> "platos", "Vaso" <-> "vasos"...).
        private static string UnidadDeTipo(string tipo) => tipo switch
        {
            "Plato" => "platos",
            "Vaso" => "vasos",
            "Jarra" => "jarras",
            "Copa" => "copas",
            "Botella" => "bot.",
            "Unidad" => "unid.",
            "Taza" => "tazas",
            _ => "porc.",
        };

        private static string TipoDeUnidad(string? unidad) => (unidad ?? "").ToLowerInvariant() switch
        {
            "platos" => "Plato",
            "vasos" => "Vaso",
            "jarras" => "Jarra",
            "copas" => "Copa",
            "bot." => "Botella",
            "unid." => "Unidad",
            "tazas" => "Taza",
            _ => "Porción",
        };

        // Margen en vivo: PVP menos costo, con porcentaje sobre el precio.
        private void ActualizarMargen()
        {
            if (MargenTextBlock == null) return;
            if (double.TryParse(PrecioTextBox?.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double precio)
                && double.TryParse(CostoTextBox?.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double costo)
                && precio > 0)
            {
                double margen = precio - costo;
                double pct = margen / precio * 100;
                MargenTextBlock.Text = $"Margen estimado: S/ {margen:0.00} ({pct:0.0}%)";
            }
            else
            {
                MargenTextBlock.Text = "Margen estimado: —";
            }
        }

        // Foto: abre el picker del sistema y previsualiza (se copia al guardar).
        private async void CambiarFotoButton_Click(object sender, RoutedEventArgs e) => await ElegirFotoAsync();

        private async void SubirImagenButton_Click(object sender, RoutedEventArgs e) => await ElegirFotoAsync();

        private async System.Threading.Tasks.Task ElegirFotoAsync()
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".webp");
                picker.FileTypeFilter.Add(".bmp");
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                WinRT.Interop.InitializeWithWindow.Initialize(picker, GetActiveWindow());
                var archivo = await picker.PickSingleFileAsync();
                if (archivo == null) return;
                _fotoPendiente = archivo.Path;
                MostrarFoto(await FotosPlatos.CargarAsync(archivo.Path));
            }
            catch
            {
                await MostrarDialogoAsync("No se pudo abrir", "No se pudo elegir la imagen. Intenta con un JPG o PNG.", "Entendido");
            }
        }

        // Muestra la foto guardada (al editar) en la vista previa.
        private async System.Threading.Tasks.Task MostrarFotoActualAsync()
        {
            MostrarFoto(await FotosPlatos.CargarAsync(_fotoActual));
        }

        // Alterna entre la imagen y el placeholder según haya bitmap o no.
        private void MostrarFoto(Microsoft.UI.Xaml.Media.Imaging.BitmapImage? bmp)
        {
            if (FotoPreview == null || FotoVacia == null) return;
            if (bmp == null)
            {
                FotoPreview.Source = null;
                FotoPreview.Visibility = Visibility.Collapsed;
                FotoVacia.Visibility = Visibility.Visible;
            }
            else
            {
                FotoPreview.Source = bmp;
                FotoPreview.Visibility = Visibility.Visible;
                FotoVacia.Visibility = Visibility.Collapsed;
            }
        }

        // Boton + Nueva: mini dialogo para crear la categoria sin salir del
        // formulario; al agregarla queda seleccionada en el combo.
        private async void AgregarCategoriaButton_Click(object sender, RoutedEventArgs e)
        {
            var campo = new TextBox
            {
                PlaceholderText = "Ej. Pollos a la Brasa",
                MaxLength = 40,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x19, 0x1C, 0x22)),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x40, 0x47, 0x52)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
            };
            var dialogo = new ContentDialog
            {
                Title = "Nueva categoría",
                Content = campo,
                PrimaryButtonText = "Agregar",
                CloseButtonText = "Cancelar",
                XamlRoot = this.XamlRoot
            };
            if (await dialogo.ShowAsync() != ContentDialogResult.Primary) return;
            string nombre = (campo.Text ?? "").Trim();
            if (nombre.Length == 0)
            {
                await MostrarDialogoAsync("Falta el nombre", "Escribe el nombre de la categoría.", "Entendido");
                return;
            }
            try
            {
                await CategoriasStore.AgregarAsync(nombre);
            }
            catch (InvalidOperationException ex)
            {
                await MostrarDialogoAsync("No se pudo agregar", ex.Message, "Entendido");
                return;
            }
            SeleccionarCategoria(nombre);
        }

        // Guardar: valida y crea (alta) o actualiza (edición por el mismo PLU).
        // Solo unidades/porciones enteras; el estado deriva del stock.
        private async void CrearProductoButton_Click(object sender, RoutedEventArgs e)
        {
            string nombre = (NombreTextBox?.Text ?? "").Trim();
            if (string.IsNullOrEmpty(nombre))
            {
                await MostrarDialogoAsync("Falta el nombre", "Escribe el nombre comercial del plato para continuar.", "Entendido");
                NombreTextBox?.Focus(FocusState.Programmatic);
                return;
            }
            if (!TryParseMonto(PrecioTextBox?.Text, out double precio) || precio <= 0)
            {
                await MostrarDialogoAsync("Precio inválido", "El precio de venta (PVP) debe ser mayor a cero.", "Entendido");
                return;
            }
            TryParseMonto(CostoTextBox?.Text, out double costo);
            if (!int.TryParse((PorcionesTextBox?.Text ?? "").Trim(), out int porciones))
            {
                await MostrarDialogoAsync("Stock inválido", "Las porciones deben ser un número entero.", "Entendido");
                return;
            }
            string categoria = (CategoriaComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Platos de Fondo";
            string tipo = (TipoComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Porción";
            bool esEdicion = !string.IsNullOrEmpty(_pluEnEdicion);
            // El PLU es interno: se genera solo al crear y nunca se muestra ni edita.
            string pluClave = esEdicion ? _pluEnEdicion! : await PlatosStore.SiguientePluAsync();
            // Foto: si se eligió una nueva se copia al almacén local ahora.
            string fotoFinal = esEdicion ? _fotoActual : "";
            if (!string.IsNullOrEmpty(_fotoPendiente))
            {
                try
                {
                    fotoFinal = await FotosPlatos.GuardarAsync(_fotoPendiente, pluClave, esEdicion ? _fotoActual : null);
                }
                catch (InvalidOperationException ex)
                {
                    await MostrarDialogoAsync("Foto no válida", ex.Message, "Entendido");
                    return;
                }
            }
            var plato = new Plato
            {
                Plu = pluClave,
                Nombre = nombre,
                Categoria = categoria,
                EstacionTag = _estacionActiva,
                Descripcion = (DescripcionTextBox?.Text ?? "").Trim(),
                Precio = Math.Round(precio, 2),
                Costo = Math.Round(Math.Max(0, costo), 2),
                Porciones = porciones,
                Unidad = UnidadDeTipo(tipo),
                FotoPath = fotoFinal
            };
            try
            {
                if (esEdicion) await PlatosStore.ActualizarAsync(plato);
                else await PlatosStore.AgregarAsync(plato);
            }
            catch (InvalidOperationException ex)
            {
                await MostrarDialogoAsync(esEdicion ? "No se pudo guardar" : "No se pudo crear", ex.Message, "Entendido");
                return;
            }
            await MostrarDialogoAsync(
                esEdicion ? "Cambios guardados" : "Producto creado",
                esEdicion ? $"«{plato.Nombre}» ya quedó actualizado en el catálogo local." : $"«{plato.Nombre}» ya está en el catálogo local.",
                "Volver al catálogo");
            VolverAlCatalogo();
        }

        // Acepta punto o coma decimal (teclados ES-PE usan coma).
        private static bool TryParseMonto(string? texto, out double valor)
        {
            texto = (texto ?? "").Trim().Replace("S/", "").Trim();
            if (double.TryParse(texto, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valor)) return true;
            string normalizado = texto.Replace(',', '.');
            return double.TryParse(normalizado, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out valor);
        }

        private void CancelarButton_Click(object sender, RoutedEventArgs e) => VolverAlCatalogo();

        private void VolverButton_Click(object sender, RoutedEventArgs e) => VolverAlCatalogo();

        // Eliminar (solo edición): pide confirmación, borra del JSON con su
        // foto y vuelve al catálogo que recarga solo.
        private async void EliminarProductoButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pluEnEdicion)) return;
            string nombre = (NombreTextBox?.Text ?? "").Trim();
            var confirmar = new ContentDialog
            {
                Title = $"Eliminar PLU {_pluEnEdicion}",
                Content = new TextBlock { Text = $"«{nombre}» saldrá del catálogo y del punto de venta. Esta acción no se puede deshacer.", TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = "Eliminar",
                CloseButtonText = "Cancelar",
                XamlRoot = this.XamlRoot
            };
            if (await confirmar.ShowAsync() != ContentDialogResult.Primary) return;
            try
            {
                await PlatosStore.EliminarAsync(_pluEnEdicion);
            }
            catch (InvalidOperationException ex)
            {
                await MostrarDialogoAsync("No se pudo eliminar", ex.Message, "Entendido");
                return;
            }
            VolverAlCatalogo();
        }

        // Vuelve al catalogo buscando el MainContent de la ventana (patron MainWindow).
        private void VolverAlCatalogo()
        {
            var destino = new ProductosView();
            DependencyObject? actual = this;
            while (actual != null)
            {
                if (actual is ContentControl host && host.Name == "MainContent")
                {
                    host.Content = destino;
                    return;
                }
                actual = VisualTreeHelper.GetParent(actual);
            }
        }

        // Dialogo oscuro reutilizable con el XamlRoot propio de la vista.
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
