using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using sabores_wanka.Models;
using sabores_wanka.Services;

namespace sabores_wanka.Views.Productos
{
    // Catalogo con persistencia local JSON (Services/PlatosStore, sin nube).
    // Al cargar reconstruye la tabla desde disco; pausar/reactivar tocan stock.
    public sealed partial class ProductosView : UserControl
    {
        // Categoria activa de las pills (Tag del boton; "todos" = sin filtro).
        private string _categoriaActiva = "todos";

        public ProductosView()
        {
            this.InitializeComponent();
            this.Loaded += ProductosView_Loaded;
        }

        // Al mostrarse, pinta la tabla desde el JSON local (con semilla inicial).
        // Si el disco falla, deja las filas estaticas del XAML en vez de crashear.
        private async void ProductosView_Loaded(object sender, RoutedEventArgs e)
        {
            try { await RecargarCatalogoAsync(); }
            catch { }
        }

        // Lee el almacen y reconstruye las filas; luego aplica buscador y pills.
        private async System.Threading.Tasks.Task RecargarCatalogoAsync()
        {
            var platos = await PlatosStore.ListarAsync();
            ReconstruirTabla(platos);
            await ReconstruirPillsAsync(platos);
            ActualizarKpis(platos);
            AplicarFiltros();
        }

        // KPIs reales del catalogo (cero si esta vacio, sin numeros fijos).
        private void ActualizarKpis(System.Collections.Generic.List<Plato> platos)
        {
            int total = platos.Count;
            int activos = platos.Count(p => !p.Pausado && p.Porciones > 0);
            int criticos = platos.Count(p => !p.Pausado && p.Porciones <= 5);
            double valorizado = platos.Sum(p => p.Precio * p.Porciones);
            int categorias = platos.Select(p => (p.Categoria ?? "").Trim()).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            int estaciones = platos.Select(p => (p.EstacionTag ?? "").Trim()).Where(e => e.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (ValorizacionText != null) ValorizacionText.Text = $"S/ {valorizado:N2}";
            if (TotalCatalogoText != null) TotalCatalogoText.Text = total.ToString();
            if (ActivosText != null) ActivosText.Text = $"Activos en Carta: {activos}";
            if (CriticosText != null) CriticosText.Text = $"Críticos: {criticos}";
            if (CategoriasKpiText != null) CategoriasKpiText.Text = $"{categorias} Categoría{(categorias == 1 ? "" : "s")}";
            if (EstacionesKpiText != null) EstacionesKpiText.Text = estaciones == 0 ? "Sin estaciones" : $"{estaciones} Estación{(estaciones == 1 ? "" : "es")}";
            if (InsumosCriticosText != null) InsumosCriticosText.Text = $"{criticos} Insumo{(criticos == 1 ? "" : "s")}";
            if (InsumosSubText != null) InsumosSubText.Text = criticos == 0 ? "Stock al día" : "Requieren reposición inmediata";
            if (TotalFooterText != null) TotalFooterText.Text = $"de {total} plato{(total == 1 ? "" : "s")} y productos";
        }

        // Pills desde las categorias con productos (Todos + las que haya).
        private async System.Threading.Tasks.Task ReconstruirPillsAsync(System.Collections.Generic.List<Plato> platos)
        {
            if (PillsPanel == null) return;
            PillsPanel.Children.Clear();
            PillsPanel.Children.Add(PildoraCategoria("todos", $"Todos los Platos ({platos.Count})", true));
            List<string> categorias;
            try { categorias = await CategoriasStore.ListarAsync(); }
            catch { categorias = new System.Collections.Generic.List<string>(); }
            foreach (string cat in categorias)
            {
                int n = platos.Count(p => string.Equals((p.Categoria ?? "").Trim(), cat, StringComparison.OrdinalIgnoreCase));
                if (n == 0) continue;
                PillsPanel.Children.Add(PildoraCategoria(cat, $"{cat} ({n})", false));
            }
            _categoriaActiva = "todos";
        }

        // Pildora con la misma pinta de las fijas anteriores.
        private Button PildoraCategoria(string tag, string texto, bool activa)
        {
            var b = new Button
            {
                Tag = tag,
                Background = new SolidColorBrush(activa ? Windows.UI.Color.FromArgb(0xFF, 0x00, 0x78, 0xD4) : Windows.UI.Color.FromArgb(0xFF, 0x23, 0x27, 0x31)),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x2E, 0x34, 0x42)),
                BorderThickness = activa ? new Thickness(0) : new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 6, 14, 6),
            };
            b.Content = new TextBlock
            {
                Text = texto,
                Foreground = new SolidColorBrush(activa ? Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) : Windows.UI.Color.FromArgb(0xFF, 0x9C, 0xA3, 0xAF)),
                FontSize = 11,
                FontWeight = activa ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            };
            b.Click += CategoriaPill_Click;
            return b;
        }

        // Reconstruye TablaProductos fila por fila (9 columnas como el XAML fijo).
        private void ReconstruirTabla(System.Collections.Generic.List<Plato> platos)
        {
            if (TablaProductos == null) return;
            TablaProductos.Children.Clear();
            TablaProductos.RowDefinitions.Clear();
            for (int i = 0; i < platos.Count; i++)
            {
                TablaProductos.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid fila = CrearFila(platos[i], i == platos.Count - 1);
                Grid.SetRow(fila, i);
                TablaProductos.Children.Add(fila);
            }
        }

        // Fila de 9 columnas con PLU, plato + etiqueta, categoria, estacion,
        // costo, precio, stock, estado y acciones (editar/receta/pausar).
        private Grid CrearFila(Plato p, bool esUltima)
        {
            var fila = new Grid { Padding = new Thickness(12, 10, 12, 10) };
            if (!esUltima)
            {
                fila.BorderBrush = Pincel("#2e3442");
                fila.BorderThickness = new Thickness(0, 0, 0, 1);
            }
            double[] anchos = { 50, -1, 130, 110, 90, 90, 90, 100, 110 };
            for (int i = 0; i < anchos.Length; i++)
                fila.ColumnDefinitions.Add(new ColumnDefinition { Width = anchos[i] < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(anchos[i]) });

            var plu = new TextBlock { Text = p.Plu, Foreground = Pincel("#2b88d8"), FontFamily = new FontFamily("Consolas"), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            fila.Children.Add(plu);

            var nombreCol = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(nombreCol, 1);
            var tituloFila = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            tituloFila.Children.Add(new TextBlock { Text = p.Nombre, Foreground = Pincel(p.Pausado ? "#6b7280" : "#FFFFFF"), FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            var (etiqueta, etBg, etFg) = EtiquetaDe(p);
            var etBorde = new Border { Background = Pincel(etBg), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 1) };
            etBorde.Child = new TextBlock { Text = etiqueta, Foreground = Pincel(etFg), FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
            tituloFila.Children.Add(etBorde);
            nombreCol.Children.Add(tituloFila);
            nombreCol.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(p.Descripcion) ? "Sin descripción en carta" : p.Descripcion, Foreground = Pincel(p.Porciones > 0 && p.Porciones <= 5 ? "#fbbf24" : "#6b7280"), FontSize = 10, Opacity = p.Porciones > 0 && p.Porciones <= 5 ? 0.8 : 1 });
            fila.Children.Add(nombreCol);

            var cat = new TextBlock { Text = p.Categoria, Foreground = Pincel("#9ca3af"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(cat, 2);
            fila.Children.Add(cat);

            var (estTexto, estBg, estBorde, estFg) = EstacionDe(p);
            var estPill = new Border { Background = Pincel(estBg), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), BorderBrush = Pincel(estBorde), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            estPill.Child = new TextBlock { Text = estTexto, Foreground = Pincel(estFg), FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
            Grid.SetColumn(estPill, 3);
            fila.Children.Add(estPill);

            var costo = new TextBlock { Text = Services.Moneda.Texto(p.Costo), Foreground = Pincel("#9ca3af"), FontFamily = new FontFamily("Consolas"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(costo, 4);
            fila.Children.Add(costo);

            var precio = new TextBlock { Text = Services.Moneda.Texto(p.Precio), Foreground = Pincel(p.Pausado ? "#6b7280" : "#FFFFFF"), FontFamily = new FontFamily("Consolas"), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(precio, 5);
            fila.Children.Add(precio);

            string stockBg = p.Porciones <= 0 ? "#e11d4833" : p.Porciones <= 5 ? "#f59e0b33" : "#10b9811a";
            string stockFg = p.Porciones <= 0 ? "#fb7185" : p.Porciones <= 5 ? "#fbbf24" : "#34d399";
            var stockPill = new Border { Background = Pincel(stockBg), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            if (p.Porciones > 0 && p.Porciones <= 5) { stockPill.BorderBrush = Pincel("#f59e0b66"); stockPill.BorderThickness = new Thickness(1); }
            stockPill.Child = new TextBlock { Text = $"{p.Porciones} {p.Unidad}", Foreground = Pincel(stockFg), FontFamily = new FontFamily("Consolas"), FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
            Grid.SetColumn(stockPill, 6);
            fila.Children.Add(stockPill);

            string est2Bg = p.Estado == "En Carta" ? "#10b9811a" : p.Estado == "Stock Bajo" ? "#f59e0b33" : p.Estado == "Pausado" ? "#64748b33" : "#e11d4833";
            string est2Fg = p.Estado == "En Carta" ? "#34d399" : p.Estado == "Stock Bajo" ? "#fbbf24" : p.Estado == "Pausado" ? "#9ca3af" : "#fb7185";
            var estadoPill = new Border { Background = Pincel(est2Bg), CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 3, 8, 3), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var estadoFila = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            estadoFila.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 6, Height = 6, Fill = Pincel(est2Fg), VerticalAlignment = VerticalAlignment.Center });
            estadoFila.Children.Add(new TextBlock { Text = p.Estado, Foreground = Pincel(est2Fg), FontSize = 10 });
            estadoPill.Child = estadoFila;
            Grid.SetColumn(estadoPill, 7);
            fila.Children.Add(estadoPill);

            var acciones = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(acciones, 8);
            acciones.Children.Add(BotonAccion(p.Plu, "\uE70F", "#9ca3af", "Editar plato", AccionEditarPlato_Click));
            acciones.Children.Add(BotonAccion(p.Plu, "\uE8FD", "#9ca3af", "Receta y escandallo", AccionReceta_Click));
            if (p.Pausado)
                acciones.Children.Add(BotonAccion(p.Plu, "\uE768", "#34d399", "Reactivar ítem", AccionReactivar_Click));
            else
                acciones.Children.Add(BotonAccion(p.Plu, "\uE769", "#9ca3af", "Pausar ítem", AccionPausar_Click));
            fila.Children.Add(acciones);

            return fila;
        }

        // Etiqueta del plato según estación y categoría (colores del XAML fijo).
        private static (string texto, string fondo, string frente) EtiquetaDe(Plato p)
        {
            if (p.Categoria.Contains("Bebidas") || p.Categoria.Contains("Cervezas")) return ("BOTELLA", "#10b98133", "#34d399");
            if (p.Categoria.Contains("Postres")) return ("POSTRES", "#ec489933", "#f9a8d4");
            return p.EstacionTag switch
            {
                "fria" => ("FRÍA", "#22d3ee33", "#67e8f9"),
                "bar" => ("BAR", "#818cf833", "#a5b4fc"),
                "postres" => ("POSTRES", "#ec489933", "#f9a8d4"),
                "salon" => ("SERVICIO", "#6b728033", "#9ca3af"),
                _ => ("CALIENTE", "#f59e0b33", "#fbbf24"),
            };
        }

        // Pill de estación con el mismo texto y color del catálogo de muestra.
        private static (string texto, string fondo, string borde, string frente) EstacionDe(Plato p) => p.EstacionTag switch
        {
            "fria" => ("COCINA FRÍA", "#22d3ee1a", "#22d3ee33", "#67e8f9"),
            "bar" => ("BARRA PRINCIPAL", "#a855f71a", "#a855f733", "#c4b5fd"),
            "postres" => ("ESTACIÓN REPOSTERÍA", "#ec48991a", "#ec489933", "#f9a8d4"),
            "salon" => ("SALÓN", "#6b72801a", "#6b728033", "#9ca3af"),
            _ => ("COCINA 1", "#3b82f61a", "#3b82f633", "#60a5fa"),
        };

        // Botón de acción por fila (icono + handler del catálogo).
        private static Button BotonAccion(string plu, string glifo, string color, string ayuda, RoutedEventHandler alClic)
        {
            var b = new Button { Tag = plu, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), Padding = new Thickness(6, 2, 6, 2), MinWidth = 0, MinHeight = 0 };
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(b, ayuda);
            b.Content = new FontIcon { Glyph = glifo, FontSize = 12, Foreground = Pincel(color) };
            b.Click += alClic;
            return b;
        }

        // Convierte #RRGGBB o #RRGGBBAA (CSS) a pincel WinUI; nunca lanza
        // (un nombre invalido devuelve gris neutro en vez de crashear la vista).
        private static SolidColorBrush Pincel(string hex)
        {
            try
            {
                hex = (hex ?? "").Trim().TrimStart('#');
                if (hex.Equals("White", StringComparison.OrdinalIgnoreCase)) return new SolidColorBrush(Microsoft.UI.Colors.White);
                if (hex.Equals("Transparent", StringComparison.OrdinalIgnoreCase)) return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
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

        // Dialogo oscuro reutilizable (mock sin backend, con el PLU en el titulo).
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

        private static string PluDe(object sender) =>
            (sender as Button)?.Tag as string ?? "PLU";

        private void BuscadorProductosTextBox_TextChanged(object sender, TextChangedEventArgs e) => AplicarFiltros();

        // Pinta la pill activa en azul y filtra por su Tag (contiene en categoria).
        private void CategoriaPill_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button pill || pill.Tag is not string tag) return;
            _categoriaActiva = tag;
            var azul = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x78, 0xD4));
            var fondo = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x23, 0x27, 0x31));
            var textoActivo = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
            var textoIdle = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x9C, 0xA3, 0xAF));
            foreach (var child in PillsPanel.Children)
            {
                if (child is not Button b) continue;
                bool activa = b == pill;
                b.Background = activa ? azul : fondo;
                if (b.Content is TextBlock tb) tb.Foreground = activa ? textoActivo : textoIdle;
            }
            AplicarFiltros();
        }

        // Combina texto (nombre/categoria/PLU) y categoria activa sobre TablaProductos.
        private void AplicarFiltros()
        {
            if (TablaProductos == null || MostrandoProductosText == null) return;
            string query = (BuscadorProductosTextBox?.Text ?? "").Trim().ToLowerInvariant();
            int visibles = 0;
            foreach (var child in TablaProductos.Children)
            {
                if (child is not Grid fila) continue;
                string texto = string.Join(" ", TextosDe(fila)).ToLowerInvariant();
                bool porTexto = string.IsNullOrEmpty(query) || texto.Contains(query);
                bool porCategoria = _categoriaActiva == "todos" || texto.Contains(_categoriaActiva.ToLowerInvariant());
                bool mostrar = porTexto && porCategoria;
                fila.Visibility = mostrar ? Visibility.Visible : Visibility.Collapsed;
                if (mostrar) visibles++;
            }
            MostrandoProductosText.Text = visibles.ToString();
        }

        private static System.Collections.Generic.List<string> TextosDe(DependencyObject padre)
        {
            var lista = new System.Collections.Generic.List<string>();
            int c = VisualTreeHelper.GetChildrenCount(padre);
            for (int i = 0; i < c; i++)
            {
                var hijo = VisualTreeHelper.GetChild(padre, i);
                if (hijo is TextBlock tb) lista.Add(tb.Text);
                else lista.AddRange(TextosDe(hijo));
            }
            return lista;
        }

        // Nuevo plato: abre la vista de creacion 2.0 en el MainContent principal.
        private void NuevoPlatoButton_Click(object sender, RoutedEventArgs e) =>
            NavegarA(new ProductosCreacionView());

        // Navega sustituyendo el MainContent de la ventana (patron MainWindow).
        private void NavegarA(UserControl destino)
        {
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

        private async void ExportarCartaButton_Click(object sender, RoutedEventArgs e) =>
            await MostrarDialogoAsync("Exportar carta", "Se generará el PDF de la carta con los ítems del catálogo local.", "Exportar", "Cancelar");

        // Recarga la tabla desde el JSON local (los creados ya aparecen solos).
        private async void RefrescarButton_Click(object sender, RoutedEventArgs e) => await RecargarCatalogoAsync();

        // Editar: abre el formulario 2.0 precargado; al guardar vuelve y recarga.
        private async void AccionEditarPlato_Click(object sender, RoutedEventArgs e)
        {
            string plu = PluDe(sender);
            var plato = await PlatosStore.ObtenerPorPluAsync(plu);
            if (plato == null)
            {
                await MostrarDialogoAsync($"PLU {plu}", "Ese plato ya no está en el catálogo. Se recargará la lista.", "Entendido");
                await RecargarCatalogoAsync();
                return;
            }
            var edicion = new ProductosCreacionView();
            edicion.CargarParaEdicion(plato);
            NavegarA(edicion);
        }

        private async void AccionReceta_Click(object sender, RoutedEventArgs e) =>
            await MostrarDialogoAsync($"Receta PLU {PluDe(sender)}", "Escandallo e insumos de la receta. Disponible al conectar el backend.", "Entendido");

        // Pausar saca de carta sin tocar el stock; reactivar lo devuelve tal cual.
        private async void AccionPausar_Click(object sender, RoutedEventArgs e)
        {
            string plu = PluDe(sender);
            var confirmar = new ContentDialog
            {
                Title = $"Pausar PLU {plu}",
                Content = new TextBlock { Text = "El ítem saldrá de carta temporalmente (su stock se conserva).", TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = "Pausar",
                CloseButtonText = "Cancelar",
                XamlRoot = this.XamlRoot
            };
            if (await confirmar.ShowAsync() != ContentDialogResult.Primary) return;
            await PlatosStore.ActualizarPausadoAsync(plu, true);
            await RecargarCatalogoAsync();
        }

        private async void AccionReactivar_Click(object sender, RoutedEventArgs e)
        {
            string plu = PluDe(sender);
            await PlatosStore.ActualizarPausadoAsync(plu, false);
            await RecargarCatalogoAsync();
        }
    }
}
