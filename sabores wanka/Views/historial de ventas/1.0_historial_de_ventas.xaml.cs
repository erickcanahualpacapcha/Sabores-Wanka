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
    public sealed partial class HistorialDeVentasView : UserControl
    {
        // Caja del turno (mock): alterna entre cerrada y abierta con dialogo previo.
        private bool _cajaAbierta;

        // Periodo del reporte (Nachis: Diario/Semanal/Mensual/Anual + fecha de
        // referencia). Filtra tablas y KPIs por el rango resultante.
        private string _periodo = "Diario";
        private DateTime _fechaRef = DateTime.Today;
        private List<Venta> _ventasCache = new();
        private List<Gasto> _gastosCache = new();

        public HistorialDeVentasView()
        {
            this.InitializeComponent();
            this.Loaded += HistorialDeVentasView_Loaded;
        }

        // Al mostrarse, fija hoy en el calendario y pinta todo desde el JSON local.
        private async void HistorialDeVentasView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _fechaRef = DateTime.Today;
                if (CalendarioPeriodo != null)
                {
                    CalendarioPeriodo.Mode = _periodo;
                    CalendarioPeriodo.SelectedDate = _fechaRef;
                }
                ActualizarChipFecha();
                await RecargarTodoAsync();
            }
            catch { }
        }

        // Lee los almacenes y reconstruye tablas; el periodo filtra KPIs y filas.
        // Ingresos = solo PAGADO; Por cobrar = POR COBRAR + EN MESA (legado).
        private async System.Threading.Tasks.Task RecargarTodoAsync()
        {
            _ventasCache = await VentasStore.ListarAsync();
            _gastosCache = await GastosStore.ListarAsync();
            ReconstruirTablaVentas(_ventasCache.Where(v => v.EsPagada).ToList());
            ReconstruirTablaPorCobrar(_ventasCache.Where(v => v.EsPorCobrar).ToList());
            ReconstruirTablaEgresos(_gastosCache);
            AplicarPeriodo();
        }

        // Cambio de periodo o fecha: recalcula KPIs y refiltra sin releer disco.
        private void AplicarPeriodo()
        {
            var (ini, fin) = ResolverRango();
            ActualizarKpis(_ventasCache, ini, fin);
            double sumaVentas = _ventasCache.Where(v => v.EsPagada && v.FechaEfectiva >= ini && v.FechaEfectiva < fin).Sum(v => v.Total);
            ActualizarKpisGastos(_gastosCache, sumaVentas, ini, fin);
            AplicarFiltros();
        }

        private void PeriodoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string? sel = ((sender as ComboBox)?.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (string.IsNullOrEmpty(sel)) return;
            _periodo = sel;
            if (CalendarioPeriodo != null) CalendarioPeriodo.Mode = _periodo;
            ActualizarChipFecha();
            AplicarPeriodo();
        }

        // El calendario emite la fecha elegida (día, 1° del mes o 1° de enero).
        private void CalendarioPeriodo_DateSelected(object sender, DateTime fecha)
        {
            _fechaRef = fecha.Date;
            if (CalendarioPeriodo != null) CalendarioPeriodo.SelectedDate = _fechaRef;
            ActualizarChipFecha();
            AplicarPeriodo();
            FechaFlyout?.Hide();
        }

        // Chip de fecha: "Hoy" o "06 oct 2026" según corresponda.
        private void ActualizarChipFecha()
        {
            if (FechaChipText == null) return;
            FechaChipText.Text = _fechaRef.Date == DateTime.Today
                ? "Hoy"
                : _fechaRef.ToString("dd MMM yyyy", new System.Globalization.CultureInfo("es-PE")).Replace(".", "");
        }

        // Convierte periodo + fecha de referencia en rango [inicio, fin).
        private (DateTime inicio, DateTime fin) ResolverRango()
        {
            DateTime d = _fechaRef.Date;
            return _periodo switch
            {
                "Semanal" => (InicioSemana(d), InicioSemana(d).AddDays(7)),
                "Mensual" => (new DateTime(d.Year, d.Month, 1), new DateTime(d.Year, d.Month, 1).AddMonths(1)),
                "Anual" => (new DateTime(d.Year, 1, 1), new DateTime(d.Year, 1, 1).AddYears(1)),
                _ => (d, d.AddDays(1)),
            };
        }

        // Lunes de la semana de la fecha (semana peruana lunes-domingo).
        private static DateTime InicioSemana(DateTime d)
        {
            int dif = ((int)d.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return d.AddDays(-dif);
        }

        // Reconstruye TablaEgresos conservando la cabecera y agregando una fila por gasto.
        private void ReconstruirTablaEgresos(System.Collections.Generic.List<Gasto> gastos)
        {
            if (TablaEgresos == null) return;
            Grid? cabecera = null;
            foreach (var child in TablaEgresos.Children)
            {
                if (child is Grid g && EsCabecera(g)) { cabecera = g; break; }
            }
            TablaEgresos.Children.Clear();
            TablaEgresos.RowDefinitions.Clear();
            TablaEgresos.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (cabecera != null)
            {
                Grid.SetRow(cabecera, 0);
                TablaEgresos.Children.Add(cabecera);
            }
            for (int i = 0; i < gastos.Count; i++)
            {
                TablaEgresos.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid fila = CrearFilaGasto(gastos[i], i == gastos.Count - 1);
                Grid.SetRow(fila, i + 1);
                TablaEgresos.Children.Add(fila);
            }
        }

        // Fila de 7 columnas con la misma pinta de las filas fijas del mock.
        private Grid CrearFilaGasto(Gasto g, bool esUltima)
        {
            var fila = new Grid { Padding = new Thickness(16, 12, 16, 12), Tag = g };
            if (!esUltima)
            {
                fila.BorderBrush = PincelCss("#232b33");
                fila.BorderThickness = new Thickness(0, 0, 0, 1);
            }
            double[] anchos = { 140, 120, -1, 120, 110, 110, 200 };
            for (int i = 0; i < anchos.Length; i++)
                fila.ColumnDefinitions.Add(new ColumnDefinition { Width = anchos[i] < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(anchos[i]) });

            var compCol = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            compCol.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 6, Height = 6, Fill = PincelCss("#f43f5e") });
            compCol.Children.Add(new TextBlock { Text = g.Comprobante, Foreground = PincelCss("#FFFFFF"), FontFamily = new FontFamily("Consolas"), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
            fila.Children.Add(compCol);

            var fecha = new TextBlock { Text = g.FechaHora.ToString("dd/MM/yyyy HH:mm"), Foreground = PincelCss("#d1d5db"), FontFamily = new FontFamily("Consolas"), FontSize = 11 };
            Grid.SetColumn(fecha, 1);
            fila.Children.Add(fecha);

            var conceptoCol = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var tipoBorde = new Border { Background = PincelCss("#e11d4826"), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), BorderBrush = PincelCss("#e11d484d"), BorderThickness = new Thickness(1) };
            tipoBorde.Child = new TextBlock { Text = (g.Tipo ?? "").ToUpperInvariant(), Foreground = PincelCss("#fda4af"), FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
            conceptoCol.Children.Add(tipoBorde);
            conceptoCol.Children.Add(new TextBlock { Text = g.Concepto, Foreground = PincelCss("#FFFFFF"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(conceptoCol, 2);
            fila.Children.Add(conceptoCol);

            var autorizo = new TextBlock { Text = g.Autorizo, Foreground = PincelCss("#9ca3af"), FontStyle = Windows.UI.Text.FontStyle.Italic, FontSize = 11 };
            Grid.SetColumn(autorizo, 3);
            fila.Children.Add(autorizo);

            var montoCol = new StackPanel();
            montoCol.Children.Add(new TextBlock { Text = $"S/ {g.Monto:0.00}", Foreground = PincelCss("#f43f5e"), FontWeight = Microsoft.UI.Text.FontWeights.Black, FontFamily = new FontFamily("Consolas"), FontSize = 12 });
            montoCol.Children.Add(new TextBlock { Text = g.Metodo, Foreground = PincelCss("#9ca3af"), FontSize = 9 });
            Grid.SetColumn(montoCol, 4);
            fila.Children.Add(montoCol);

            var pill = new Border { Background = PincelCss("#f43f5e26"), CornerRadius = new CornerRadius(12), Padding = new Thickness(8, 4, 8, 4), BorderBrush = PincelCss("#f43f5e66"), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center };
            var pillFila = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            pillFila.Children.Add(new FontIcon { Glyph = "\uE73E", FontSize = 10, Foreground = PincelCss("#f43f5e") });
            pillFila.Children.Add(new TextBlock { Text = g.Estado, Foreground = PincelCss("#fda4af"), FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.Black });
            pill.Child = pillFila;
            Grid.SetColumn(pill, 5);
            fila.Children.Add(pill);

            var acciones = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(acciones, 6);
            acciones.Children.Add(BotonFila(g.Comprobante, "\uE74E", "#38bdf8", "Imprimir", AccionImprimir_Click));
            acciones.Children.Add(BotonFila(g.Comprobante, "\uE7B3", "#d1d5db", "Ver detalle", AccionVer_Click));
            acciones.Children.Add(BotonFila(g.Comprobante, "\uE74D", "#f87171", "Eliminar", AccionEliminar_Click));
            fila.Children.Add(acciones);

            return fila;
        }

        // KPIs de egresos del periodo + balance (ventas pagadas menos gastos).
        private void ActualizarKpisGastos(System.Collections.Generic.List<Gasto> gastos, double sumaVentas, DateTime ini, DateTime fin)
        {
            var enRango = gastos.Where(g => g.FechaHora >= ini && g.FechaHora < fin).ToList();
            double suma = enRango.Sum(g => g.Monto);
            if (GastosTotalText != null) GastosTotalText.Text = $"S/ {suma:N2}";
            if (GastosCountText != null) GastosCountText.Text = $"{enRango.Count} compra{(enRango.Count == 1 ? "" : "s")}";
            if (BalanceText != null) BalanceText.Text = $"S/ {(sumaVentas - suma):N2}";
        }

        // Abre el formulario como ventana flotante sobre el historial.
        private void AgregarGastoButton_Click(object sender, RoutedEventArgs e)
        {
            if (GastoOverlayHost == null) return;
            var formulario = new GastoCreacionView();
            formulario.CerrarSolicitado += FormularioGasto_CerrarSolicitado;
            GastoOverlayHost.Content = formulario;
            GastoOverlayHost.Visibility = Visibility.Visible;
        }

        // Al cerrar el flotante se oculta y se recarga todo (ahí cae el gasto nuevo).
        private async void FormularioGasto_CerrarSolicitado(object? sender, EventArgs e)
        {
            if (sender is GastoCreacionView formulario)
                formulario.CerrarSolicitado -= FormularioGasto_CerrarSolicitado;
            if (GastoOverlayHost != null)
            {
                GastoOverlayHost.Visibility = Visibility.Collapsed;
                GastoOverlayHost.Content = null;
            }
            try { await RecargarTodoAsync(); }
            catch { }
        }

        // Reconstruye TablaVentas: solo entran PAGADAS (Ingresos nunca muestra fiado).
        private void ReconstruirTablaVentas(System.Collections.Generic.List<Venta> ventas)
        {
            if (TablaVentas == null) return;
            Grid? cabecera = null;
            foreach (var child in TablaVentas.Children)
            {
                if (child is Grid g && EsCabecera(g)) { cabecera = g; break; }
            }
            TablaVentas.Children.Clear();
            TablaVentas.RowDefinitions.Clear();
            TablaVentas.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (cabecera != null)
            {
                Grid.SetRow(cabecera, 0);
                TablaVentas.Children.Add(cabecera);
            }
            for (int i = 0; i < ventas.Count; i++)
            {
                TablaVentas.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid fila = CrearFilaVenta(ventas[i], i == ventas.Count - 1);
                Grid.SetRow(fila, i + 1);
                TablaVentas.Children.Add(fila);
            }
        }

        // Reconstruye TablaPorCobrar con POR COBRAR + EN MESA (cuentas abiertas).
        private void ReconstruirTablaPorCobrar(System.Collections.Generic.List<Venta> deudas)
        {
            if (TablaPorCobrar == null) return;
            Grid? cabecera = null;
            foreach (var child in TablaPorCobrar.Children)
            {
                if (child is Grid g && EsCabecera(g)) { cabecera = g; break; }
            }
            TablaPorCobrar.Children.Clear();
            TablaPorCobrar.RowDefinitions.Clear();
            TablaPorCobrar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (cabecera != null)
            {
                Grid.SetRow(cabecera, 0);
                TablaPorCobrar.Children.Add(cabecera);
            }
            for (int i = 0; i < deudas.Count; i++)
            {
                TablaPorCobrar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid fila = CrearFilaPorCobrar(deudas[i], i == deudas.Count - 1);
                Grid.SetRow(fila, i + 1);
                TablaPorCobrar.Children.Add(fila);
            }
        }

        // Fila de deuda: pill ambar POR COBRAR (o celeste EN MESA legado) + Cobrar real.
        private Grid CrearFilaPorCobrar(Venta v, bool esUltima)
        {
            bool enMesa = string.Equals((v.Estado ?? "").Trim(), "EN MESA", StringComparison.OrdinalIgnoreCase);
            string acento = enMesa ? "#38bdf8" : "#fb923c";
            string acentoClaro = enMesa ? "#7dd3fc" : "#fb923c";
            string pillBg = enMesa ? "#38bdf833" : "#fb923c26";
            string pillBorde = enMesa ? "#38bdf866" : "#fb923c66";
            var fila = new Grid { Padding = new Thickness(16, 12, 16, 12), Tag = v };
            if (!esUltima)
            {
                fila.BorderBrush = PincelCss("#232b33");
                fila.BorderThickness = new Thickness(0, 0, 0, 1);
            }
            double[] anchos = { 140, 120, -1, 120, 110, 110, 200 };
            for (int i = 0; i < anchos.Length; i++)
                fila.ColumnDefinitions.Add(new ColumnDefinition { Width = anchos[i] < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(anchos[i]) });

            var ticketCol = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            ticketCol.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 6, Height = 6, Fill = PincelCss(acento) });
            ticketCol.Children.Add(new TextBlock { Text = v.Ticket, Foreground = PincelCss("#FFFFFF"), FontFamily = new FontFamily("Consolas"), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
            fila.Children.Add(ticketCol);

            var fecha = new TextBlock { Text = v.FechaHora.ToString("dd/MM/yyyy HH:mm"), Foreground = PincelCss("#d1d5db"), FontFamily = new FontFamily("Consolas"), FontSize = 11 };
            Grid.SetColumn(fecha, 1);
            fila.Children.Add(fecha);

            var mesaCol = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var mesaBorde = new Border { Background = PincelCss(v.MesaBadgeBg), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), BorderBrush = PincelCss(v.MesaBadgeBorde), BorderThickness = new Thickness(1) };
            mesaBorde.Child = new TextBlock { Text = (v.MesaEtiqueta ?? "").ToUpperInvariant(), Foreground = PincelCss(v.MesaBadgeFg), FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
            mesaCol.Children.Add(mesaBorde);
            mesaCol.Children.Add(new TextBlock { Text = v.Cliente, Foreground = PincelCss("#FFFFFF"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(mesaCol, 2);
            fila.Children.Add(mesaCol);

            var mozo = new TextBlock { Text = v.Mozo, Foreground = PincelCss("#9ca3af"), FontStyle = Windows.UI.Text.FontStyle.Italic, FontSize = 11 };
            Grid.SetColumn(mozo, 3);
            fila.Children.Add(mozo);

            var totalCol = new StackPanel();
            totalCol.Children.Add(new TextBlock { Text = $"S/ {v.Total:0.00}", Foreground = PincelCss(acento), FontWeight = Microsoft.UI.Text.FontWeights.Black, FontFamily = new FontFamily("Consolas"), FontSize = 12 });
            totalCol.Children.Add(new TextBlock { Text = v.MetodoPago, Foreground = PincelCss("#9ca3af"), FontSize = 9 });
            Grid.SetColumn(totalCol, 4);
            fila.Children.Add(totalCol);

            var pill = new Border { Background = PincelCss(pillBg), CornerRadius = new CornerRadius(12), Padding = new Thickness(8, 4, 8, 4), BorderBrush = PincelCss(pillBorde), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center };
            var pillFila = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            pillFila.Children.Add(new FontIcon { Glyph = "\uE823", FontSize = 10, Foreground = PincelCss(acentoClaro) });
            pillFila.Children.Add(new TextBlock { Text = v.Estado, Foreground = PincelCss(acentoClaro), FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.Black });
            pill.Child = pillFila;
            Grid.SetColumn(pill, 5);
            fila.Children.Add(pill);

            var acciones = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(acciones, 6);
            var cobrar = new Button { Tag = v.Ticket, Background = PincelCss("#10b98133"), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 4, 8, 4), BorderThickness = new Thickness(0), MinWidth = 0, MinHeight = 0 };
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(cobrar, "Cobrar deuda (pasa a Ingresos)");
            cobrar.Content = new TextBlock { Text = "Cobrar", Foreground = PincelCss("#10b981"), FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
            cobrar.Click += AccionCobrar_Click;
            acciones.Children.Add(cobrar);
            acciones.Children.Add(BotonFila(v.Ticket, "\uE70F", "#fb923c", "Editar", AccionEditar_Click));
            acciones.Children.Add(BotonFila(v.Ticket, "\uE7B3", "#d1d5db", "Ver detalle", AccionVer_Click));
            fila.Children.Add(acciones);

            return fila;
        }

        // Fila de 7 columnas con la misma pinta de las filas fijas del mock.
        private Grid CrearFilaVenta(Venta v, bool esUltima)
        {
            bool pagada = v.EsPagada;
            bool enMesa = string.Equals((v.Estado ?? "").Trim(), "EN MESA", StringComparison.OrdinalIgnoreCase);
            string acento = pagada ? "#10b981" : enMesa ? "#38bdf8" : "#fb923c";
            string acentoClaro = pagada ? "#10b981" : enMesa ? "#7dd3fc" : "#fb923c";
            var fila = new Grid { Padding = new Thickness(16, 12, 16, 12), Tag = v };
            if (!esUltima)
            {
                fila.BorderBrush = PincelCss("#232b33");
                fila.BorderThickness = new Thickness(0, 0, 0, 1);
            }
            double[] anchos = { 140, 120, -1, 120, 110, 110, 200 };
            for (int i = 0; i < anchos.Length; i++)
                fila.ColumnDefinitions.Add(new ColumnDefinition { Width = anchos[i] < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(anchos[i]) });

            var ticketCol = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            ticketCol.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 6, Height = 6, Fill = PincelCss(acento) });
            ticketCol.Children.Add(new TextBlock { Text = v.Ticket, Foreground = PincelCss("#FFFFFF"), FontFamily = new FontFamily("Consolas"), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
            fila.Children.Add(ticketCol);

            var fecha = new TextBlock { Text = v.FechaHora.ToString("dd/MM/yyyy HH:mm"), Foreground = PincelCss("#d1d5db"), FontFamily = new FontFamily("Consolas"), FontSize = 11 };
            Grid.SetColumn(fecha, 1);
            fila.Children.Add(fecha);

            var mesaCol = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var mesaBorde = new Border { Background = PincelCss(v.MesaBadgeBg), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), BorderBrush = PincelCss(v.MesaBadgeBorde), BorderThickness = new Thickness(1) };
            mesaBorde.Child = new TextBlock { Text = (v.MesaEtiqueta ?? "").ToUpperInvariant(), Foreground = PincelCss(v.MesaBadgeFg), FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
            mesaCol.Children.Add(mesaBorde);
            mesaCol.Children.Add(new TextBlock { Text = v.Cliente, Foreground = PincelCss("#FFFFFF"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(mesaCol, 2);
            fila.Children.Add(mesaCol);

            var mozo = new TextBlock { Text = v.Mozo, Foreground = PincelCss("#9ca3af"), FontStyle = Windows.UI.Text.FontStyle.Italic, FontSize = 11 };
            Grid.SetColumn(mozo, 3);
            fila.Children.Add(mozo);

            var totalCol = new StackPanel();
            totalCol.Children.Add(new TextBlock { Text = $"S/ {v.Total:0.00}", Foreground = PincelCss(acento), FontWeight = Microsoft.UI.Text.FontWeights.Black, FontFamily = new FontFamily("Consolas"), FontSize = 12 });
            totalCol.Children.Add(new TextBlock { Text = v.MetodoPago, Foreground = PincelCss("#9ca3af"), FontSize = 9 });
            Grid.SetColumn(totalCol, 4);
            fila.Children.Add(totalCol);

            string pillBg = pagada ? "#10b98133" : enMesa ? "#38bdf833" : "#fb923c26";
            string pillBorde = pagada ? "#10b98166" : enMesa ? "#38bdf866" : "#fb923c66";
            var pill = new Border { Background = PincelCss(pillBg), CornerRadius = new CornerRadius(12), Padding = new Thickness(8, 4, 8, 4), BorderBrush = PincelCss(pillBorde), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center };
            var pillFila = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            pillFila.Children.Add(new FontIcon { Glyph = pagada ? "\uE73E" : "\uE823", FontSize = 10, Foreground = PincelCss(acentoClaro) });
            pillFila.Children.Add(new TextBlock { Text = v.Estado, Foreground = PincelCss(acentoClaro), FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.Black });
            pill.Child = pillFila;
            Grid.SetColumn(pill, 5);
            fila.Children.Add(pill);

            var acciones = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(acciones, 6);
            acciones.Children.Add(BotonFila(v.Ticket, "\uE74E", "#38bdf8", "Imprimir", AccionImprimir_Click));
            acciones.Children.Add(BotonFila(v.Ticket, "\uE7B3", "#d1d5db", "Ver detalle", AccionVer_Click));
            if (pagada)
            {
                acciones.Children.Add(BotonFila(v.Ticket, "\uE70F", "#fb923c", "Editar", AccionEditar_Click));
                acciones.Children.Add(BotonFila(v.Ticket, "\uE8C8", "#60a5fa", "Copiar", AccionCopiar_Click));
                acciones.Children.Add(BotonFila(v.Ticket, "\uE74D", "#f87171", "Eliminar", AccionEliminar_Click));
            }
            else
            {
                // Defensa: Ingresos ya no recibe fiado, pero si llegara uno se cobra aqui.
                var ir = new Button { Tag = v.Ticket, Background = PincelCss("#10b98133"), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 4, 8, 4), BorderThickness = new Thickness(0), MinWidth = 0, MinHeight = 0 };
                Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(ir, "Cobrar deuda (pasa a Ingresos)");
                ir.Content = new TextBlock { Text = "Cobrar", Foreground = PincelCss("#10b981"), FontSize = 9 };
                ir.Click += AccionCobrar_Click;
                acciones.Children.Add(ir);
            }
            fila.Children.Add(acciones);

            return fila;
        }

        // Botón icono de la columna acciones (usa los handlers existentes por Tag).
        private static Button BotonFila(string ticket, string glifo, string color, string ayuda, RoutedEventHandler alClic)
        {
            var b = new Button { Tag = ticket, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), Padding = new Thickness(6, 2, 6, 2), MinWidth = 0, MinHeight = 0 };
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(b, ayuda);
            b.Content = new FontIcon { Glyph = glifo, FontSize = 12, Foreground = PincelCss(color) };
            b.Click += alClic;
            return b;
        }

        // KPIs del periodo: solo PAGADO suma (por fecha efectiva de cobro).
        // Por cobrar y sus badges se calculan por fecha de emision.
        private void ActualizarKpis(System.Collections.Generic.List<Venta> ventas, DateTime ini, DateTime fin)
        {
            var pagadas = ventas.Where(v => v.EsPagada && v.FechaEfectiva >= ini && v.FechaEfectiva < fin).ToList();
            double suma = pagadas.Sum(v => v.Total);
            if (VentasTotalesText != null) VentasTotalesText.Text = $"S/ {suma:N2}";
            if (TransaccionesText != null) TransaccionesText.Text = pagadas.Count.ToString();
            if (TicketPromedioText != null) TicketPromedioText.Text = pagadas.Count > 0 ? $"S/ {(suma / pagadas.Count):N2}" : "S/ 0.00";
            var deudas = ventas.Where(v => v.EsPorCobrar && v.FechaHora >= ini && v.FechaHora < fin).ToList();
            double sumaDeuda = deudas.Sum(v => v.Total);
            if (PorCobrarTotalText != null) PorCobrarTotalText.Text = $"S/ {sumaDeuda:N2}";
            if (TabIngresosBadgeText != null) TabIngresosBadgeText.Text = pagadas.Count.ToString();
            if (TabPorCobrarBadgeText != null) TabPorCobrarBadgeText.Text = deudas.Count.ToString();
            if (TabEgresosBadgeText != null) TabEgresosBadgeText.Text = _gastosCache.Count(g => g.FechaHora >= ini && g.FechaHora < fin).ToString();
        }

        // Convierte #RRGGBB o #RRGGBBAA (CSS) a pincel; nunca lanza.
        private static SolidColorBrush PincelCss(string hex)
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

        private void TabIngresos_Click(object sender, RoutedEventArgs e) => SetActiveTab("ingresos");
        private void TabEgresos_Click(object sender, RoutedEventArgs e) => SetActiveTab("egresos");
        private void TabPorCobrar_Click(object sender, RoutedEventArgs e) => SetActiveTab("porcobrar");

        // Pestaña superior: solo cambia el resaltado verde, sin dialogos ni backend.
        private void TopTab_Click(object sender, RoutedEventArgs e)
        {
            string tab = (sender as Button)?.Tag as string ?? "transacciones";
            SetActiveTopTab(tab);
        }

        // Resalta la pestaña superior activa en verde esmeralda.
        // El resto queda gris neutro para no introducir otro color chillón.
        private void SetActiveTopTab(string tab)
        {
            var activeBg = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x10, 0xB9, 0x81));
            var inactiveBg = new SolidColorBrush(Windows.UI.Color.FromArgb(0x00, 0x00, 0x00, 0x00));
            var activeFg = new SolidColorBrush(Colors.White);
            var inactiveFg = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x9C, 0xA3, 0xAF));

            bool isTrans = tab == "transacciones";
            bool isCierres = tab == "cierres";
            bool isCocina = tab == "cocina";

            TopTransaccionesBtn.Background = isTrans ? activeBg : inactiveBg;
            TopTransaccionesText.Foreground = isTrans ? activeFg : inactiveFg;
            TopTransaccionesIcon.Foreground = isTrans ? activeFg : inactiveFg;
            TopTransaccionesText.FontWeight = isTrans ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TopCierresBtn.Background = isCierres ? activeBg : inactiveBg;
            TopCierresText.Foreground = isCierres ? activeFg : inactiveFg;
            TopCierresIcon.Foreground = isCierres ? activeFg : inactiveFg;
            TopCierresText.FontWeight = isCierres ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TopCocinaBtn.Background = isCocina ? activeBg : inactiveBg;
            TopCocinaText.Foreground = isCocina ? activeFg : inactiveFg;
            TopCocinaIcon.Foreground = isCocina ? activeFg : inactiveFg;
            TopCocinaText.FontWeight = isCocina ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }

        // Pestañas Ingresos/Egresos/Por cobrar: conmuta paneles y resalta la activa.
        // Por cobrar usa celeste (no ámbar) para mantener la paleta fría del dashboard.
        // Es público para que otras vistas puedan dejar una pestaña activa.
        public void SetActiveTab(string tab)
        {
            // Visibilidad de paneles
            PanelIngresos.Visibility = tab == "ingresos" ? Visibility.Visible : Visibility.Collapsed;
            PanelEgresos.Visibility = tab == "egresos" ? Visibility.Visible : Visibility.Collapsed;
            PanelPorCobrar.Visibility = tab == "porcobrar" ? Visibility.Visible : Visibility.Collapsed;

            // Colores
            var activeGreen = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x10, 0xB9, 0x81));
            var activeRed = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xF4, 0x3F, 0x5E));
            var activeSky = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x38, 0xBD, 0xF8));
            var inactive = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x9C, 0xA3, 0xAF));
            var transparent = new SolidColorBrush(Windows.UI.Color.FromArgb(0x00, 0x00, 0x00, 0x00));

            bool isIngresos = tab == "ingresos";
            bool isEgresos = tab == "egresos";
            bool isPorCobrar = tab == "porcobrar";

            TabIngresosText.Foreground = isIngresos ? activeGreen : inactive;
            TabIngresosIcon.Foreground = isIngresos ? activeGreen : inactive;
            TabIngresosIndicator.Background = isIngresos ? activeGreen : transparent;
            TabIngresosText.FontWeight = isIngresos ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TabEgresosText.Foreground = isEgresos ? activeRed : inactive;
            TabEgresosIcon.Foreground = isEgresos ? activeRed : inactive;
            TabEgresosIndicator.Background = isEgresos ? activeRed : transparent;
            TabEgresosText.FontWeight = isEgresos ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            TabPorCobrarText.Foreground = isPorCobrar ? activeSky : inactive;
            TabPorCobrarIcon.Foreground = isPorCobrar ? activeSky : inactive;
            TabPorCobrarIndicator.Background = isPorCobrar ? activeSky : transparent;
            TabPorCobrarText.FontWeight = isPorCobrar ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

            // Badges background
            TabIngresosBadge.Background = isIngresos ? new SolidColorBrush(Windows.UI.Color.FromArgb(0x33, 0x10, 0xB9, 0x81)) : new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x24, 0x2C, 0x33));
            TabEgresosBadge.Background = isEgresos ? new SolidColorBrush(Windows.UI.Color.FromArgb(0x33, 0xF4, 0x3F, 0x5E)) : new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x24, 0x2C, 0x33));
            TabPorCobrarBadge.Background = isPorCobrar ? new SolidColorBrush(Windows.UI.Color.FromArgb(0x33, 0x38, 0xBD, 0xF8)) : new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x24, 0x2C, 0x33));
        }

        // Dialogo oscuro reutilizable: los botones de accion no tienen backend,
        // asi que confirman/informan con el ticket en el titulo. XamlRoot propio.
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

        private static string TicketDe(object sender) =>
            (sender as Button)?.Tag as string ?? "ticket";

        // CERRAR/ABRIR CAJA: alterna el estado del turno con resumen previo.
        private async void CajaButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_cajaAbierta)
            {
                var resumen = new ContentDialog
                {
                    Title = "Cerrar caja — Turno Huancayo #12",
                    Content = new TextBlock { Text = "Ventas: S/ 1,480.00\nEgresos: S/ 120.00\nBalance: S/ 1,360.00\n\nSe cerrará la caja del turno actual.", TextWrapping = TextWrapping.Wrap },
                    PrimaryButtonText = "Cerrar caja",
                    CloseButtonText = "Cancelar",
                    XamlRoot = this.XamlRoot
                };
                if (await resumen.ShowAsync() != ContentDialogResult.Primary) return;
                _cajaAbierta = true;
                CajaButtonText.Text = "ABRIR CAJA";
                CajaButton.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xB4, 0x53, 0x09));
                await MostrarDialogoAsync("Caja cerrada", "Turno #12 cerrado con S/ 1,360.00 de balance.");
            }
            else
            {
                var abrir = new ContentDialog
                {
                    Title = "Abrir caja",
                    Content = new TextBlock { Text = "Se abrirá un nuevo turno de caja.", TextWrapping = TextWrapping.Wrap },
                    PrimaryButtonText = "Abrir caja",
                    CloseButtonText = "Cancelar",
                    XamlRoot = this.XamlRoot
                };
                if (await abrir.ShowAsync() != ContentDialogResult.Primary) return;
                _cajaAbierta = false;
                CajaButtonText.Text = "CERRAR CAJA";
                CajaButton.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x05, 0x96, 0x69));
                await MostrarDialogoAsync("Caja abierta", "Nuevo turno iniciado. Registra la base inicial en el POS.");
            }
        }

        // Buscador + filtro de cliente: combinan texto (ticket/cliente) y tipo.
        // "mesas" = badge MESA/BARRA, "llevar" = badge LLEVAR, "todos" = sin filtro.
        private string _filtroCliente = "todos";

        private void BuscadorTextBox_TextChanged(object sender, TextChangedEventArgs e) => AplicarFiltros();

        private void ClienteFiltroButton_Click(object sender, RoutedEventArgs e)
        {
            _filtroCliente = _filtroCliente switch
            {
                "todos" => "mesas",
                "mesas" => "llevar",
                _ => "todos"
            };
            ClienteFiltroText.Text = _filtroCliente switch
            {
                "mesas" => "Solo mesas",
                "llevar" => "Solo llevar",
                _ => "Todos los clientes / Mesas"
            };
            AplicarFiltros();
        }

        private void FunnelButton_Click(object sender, RoutedEventArgs e)
        {
            _filtroCliente = "todos";
            ClienteFiltroText.Text = "Todos los clientes / Mesas";
            if (BuscadorTextBox != null) BuscadorTextBox.Text = "";
            AplicarFiltros();
        }

        private void AplicarFiltros()
        {
            var (ini, fin) = ResolverRango();
            if (TablaVentas == null || MostrandoText == null) return;
            string query = (BuscadorTextBox?.Text ?? "").Trim().ToLowerInvariant();
            int visibles = 0, enRango = 0;
            foreach (var child in TablaVentas.Children)
            {
                if (child is not Grid fila) continue;
                if (EsCabecera(fila)) continue;
                bool fechaOk = fila.Tag is not Venta v || (v.FechaEfectiva >= ini && v.FechaEfectiva < fin);
                string texto = TextoDeFila(fila).ToLowerInvariant();
                bool porTexto = string.IsNullOrEmpty(query) || texto.Contains(query);
                bool porTipo = _filtroCliente switch
                {
                    "mesas" => !texto.Contains("llevar"),
                    "llevar" => texto.Contains("llevar"),
                    _ => true
                };
                bool mostrar = porTexto && porTipo && fechaOk;
                fila.Visibility = mostrar ? Visibility.Visible : Visibility.Collapsed;
                if (fechaOk) enRango++;
                if (mostrar) visibles++;
            }
            MostrandoText.Text = visibles.ToString();
            if (TotalVentasText != null) TotalVentasText.Text = enRango.ToString();
            if (TablaPorCobrar != null)
            {
                int deudaVisible = 0, deudaRango = 0;
                foreach (var child in TablaPorCobrar.Children)
                {
                    if (child is not Grid fila) continue;
                    if (EsCabecera(fila)) continue;
                    bool fechaOk = fila.Tag is not Venta v || (v.FechaHora >= ini && v.FechaHora < fin);
                    string texto = TextoDeFila(fila).ToLowerInvariant();
                    bool porTexto = string.IsNullOrEmpty(query) || texto.Contains(query);
                    bool porTipo = _filtroCliente switch
                    {
                        "mesas" => !texto.Contains("llevar"),
                        "llevar" => texto.Contains("llevar"),
                        _ => true
                    };
                    bool mostrar = porTexto && porTipo && fechaOk;
                    fila.Visibility = mostrar ? Visibility.Visible : Visibility.Collapsed;
                    if (fechaOk) deudaRango++;
                    if (mostrar) deudaVisible++;
                }
                if (MostrandoPorCobrarText != null) MostrandoPorCobrarText.Text = deudaVisible.ToString();
                if (TotalPorCobrarText != null) TotalPorCobrarText.Text = deudaRango.ToString();
            }
            if (TablaEgresos != null)
            {
                int gastosVisibles = 0;
                foreach (var child in TablaEgresos.Children)
                {
                    if (child is not Grid fila) continue;
                    if (EsCabecera(fila)) continue;
                    bool fechaOk = fila.Tag is not Gasto g || (g.FechaHora >= ini && g.FechaHora < fin);
                    fila.Visibility = fechaOk ? Visibility.Visible : Visibility.Collapsed;
                    if (fechaOk) gastosVisibles++;
                }
                if (MostrandoEgresosText != null) MostrandoEgresosText.Text = gastosVisibles.ToString();
                if (TotalEgresosText != null) TotalEgresosText.Text = gastosVisibles.ToString();
            }
        }

        // La primera fila del grid es la cabecera (sin ticket): se detecta por sus
        // encabezados fijos y nunca se oculta con el buscador.
        private static bool EsCabecera(Grid fila)
        {
            foreach (var texto in TextosDe(fila))
            {
                if (texto == "TICKET / BOLETA #" || texto == "COMPROBANTE #") return true;
            }
            return false;
        }

        private static string TextoDeFila(Grid fila) => string.Join(" ", TextosDe(fila));

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

        // Ver detalle 3.0: abre la vista flotante precargada (PAGADO y POR COBRAR).
        // Solo lectura: al cerrar se oculta sin recargar (nada cambio).
        private async void AccionVer_Click(object sender, RoutedEventArgs e)
        {
            string ticket = TicketDe(sender);
            var venta = await VentasStore.ObtenerPorTicketAsync(ticket);
            if (venta == null)
            {
                await MostrarDialogoAsync($"Ticket {ticket}", "Ese comprobante ya no está en el registro.", "Entendido");
                return;
            }
            if (GastoOverlayHost == null) return;
            var detalle = new VentaDetalleView();
            detalle.Cargar(venta);
            detalle.CerrarSolicitado += DetalleVenta_CerrarSolicitado;
            GastoOverlayHost.Content = detalle;
            GastoOverlayHost.Visibility = Visibility.Visible;
        }

        // Al cerrar el detalle se oculta (sin recargar: es solo lectura).
        private void DetalleVenta_CerrarSolicitado(object? sender, EventArgs e)
        {
            if (sender is VentaDetalleView detalle)
                detalle.CerrarSolicitado -= DetalleVenta_CerrarSolicitado;
            if (GastoOverlayHost != null)
            {
                GastoOverlayHost.Visibility = Visibility.Collapsed;
                GastoOverlayHost.Content = null;
            }
        }

        // Imprimir venta REAL al driver (mismo servicio que el ticket de prueba).
        private async void AccionImprimir_Click(object sender, RoutedEventArgs e)
        {
            string ticket = TicketDe(sender);
            var venta = await VentasStore.ObtenerPorTicketAsync(ticket);
            if (venta == null)
            {
                await MostrarDialogoAsync($"Imprimir {ticket}", "Ese comprobante ya no está en el registro.", "Entendido");
                return;
            }
            var (ok, error) = await ImpresionTickets.ImprimirTextoAsync(ImpresionTickets.TextoVenta(venta));
            await MostrarDialogoAsync(
                ok ? $"Imprimir {ticket}" : "No se pudo imprimir",
                ok ? "Comprobante enviado a la impresora." : error,
                "Entendido");
        }

        // Editar venta (patron Nachis adaptado): solo boletas de los ultimos 30
        // dias; se cargan en el formulario flotante y se actualizan por ticket.
        private async void AccionEditar_Click(object sender, RoutedEventArgs e)
        {
            string ticket = TicketDe(sender);
            var venta = await VentasStore.ObtenerPorTicketAsync(ticket);
            if (venta == null)
            {
                await MostrarDialogoAsync($"Editar {ticket}", "Ese comprobante ya no está en el registro.", "Entendido");
                return;
            }
            if ((DateTime.Now - venta.FechaHora).TotalDays > 30)
            {
                await MostrarDialogoAsync("Edición no permitida", "Solo se pueden editar ventas de los últimos 30 días.", "Entendido");
                return;
            }
            if (venta.Items == null || venta.Items.Count == 0)
            {
                await MostrarDialogoAsync($"Editar {ticket}", "La boleta no tiene productos para editar.", "Entendido");
                return;
            }
            if (GastoOverlayHost == null) return;
            var formulario = new VentaEdicionView();
            formulario.Cargar(venta);
            formulario.CerrarSolicitado += FormularioVenta_CerrarSolicitado;
            GastoOverlayHost.Content = formulario;
            GastoOverlayHost.Visibility = Visibility.Visible;
        }

        // Al cerrar el editor se oculta y se recarga todo (ahi cae la editada).
        private async void FormularioVenta_CerrarSolicitado(object? sender, EventArgs e)
        {
            if (sender is VentaEdicionView formulario)
                formulario.CerrarSolicitado -= FormularioVenta_CerrarSolicitado;
            if (GastoOverlayHost != null)
            {
                GastoOverlayHost.Visibility = Visibility.Collapsed;
                GastoOverlayHost.Content = null;
            }
            try { await RecargarTodoAsync(); }
            catch { }
        }

        // Copiar factura al portapapeles (patron Nachis ExecuteCopyInvoiceAsync):
        // carga la venta por ticket, redacta con WhatsappEnlace.TextoFactura y
        // copia el texto. Confirma con el estado pagada/pendiente como Nachis.
        private async void AccionCopiar_Click(object sender, RoutedEventArgs e)
        {
            string ticket = TicketDe(sender);
            var venta = await VentasStore.ObtenerPorTicketAsync(ticket);
            if (venta == null)
            {
                await MostrarDialogoAsync("No permitido", "Solo se pueden copiar facturas de ventas validas.", "Entendido");
                return;
            }
            string mensaje = WhatsappEnlace.TextoFactura(venta);
            if (!await WhatsappEnlace.CopiarFacturaAlPortapapelesAsync(mensaje))
            {
                await MostrarDialogoAsync("No se pudo copiar", "No fue posible copiar la factura al portapapeles.", "Entendido");
                return;
            }
            string detalle = venta.EsPagada
                ? "ESTA FACTURA ESTA COMPLETAMENTE PAGADA."
                : $"DEUDA PENDIENTE: S/ {venta.Total:0.00}";
            await MostrarDialogoAsync("Factura copiada", $"{detalle}\nPuedes pegarla manualmente en WhatsApp o en una carpeta.", "Entendido");
        }

        private async void AccionEliminar_Click(object sender, RoutedEventArgs e)
        {
            string ticket = TicketDe(sender);
            var confirmar = new ContentDialog
            {
                Title = $"Eliminar {ticket}",
                Content = new TextBlock { Text = "La fila se ocultará de la lista de muestra.", TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = "Eliminar",
                CloseButtonText = "Cancelar",
                XamlRoot = this.XamlRoot
            };
            if (await confirmar.ShowAsync() != ContentDialogResult.Primary) return;
            var fila = FilaDe(sender as DependencyObject);
            if (fila != null)
            {
                fila.Visibility = Visibility.Collapsed;
                ActualizarConteo();
            }
        }

        // Cobrar deuda real: pide metodo de cobro, marca PAGADO y la mueve a Ingresos.
        private async void AccionCobrar_Click(object sender, RoutedEventArgs e)
        {
            string ticket = TicketDe(sender);
            var venta = await VentasStore.ObtenerPorTicketAsync(ticket);
            if (venta == null)
            {
                await MostrarDialogoAsync($"Cobrar {ticket}", "Ese comprobante ya no está en el registro.", "Entendido");
                return;
            }
            if (venta.EsPagada)
            {
                await MostrarDialogoAsync($"{ticket} ya está cobrado", "Ese comprobante ya figura en Ingresos.", "Entendido");
                return;
            }
            var metodos = new ComboBox { SelectedIndex = 0, MinWidth = 220 };
            metodos.Items.Add("Efectivo");
            metodos.Items.Add("Tarjeta");
            metodos.Items.Add("Yape");
            metodos.Items.Add("Plin");
            metodos.Items.Add("Otros");
            var confirmar = new ContentDialog
            {
                Title = $"Cobrar {ticket} — S/ {venta.Total:0.00}",
                Content = metodos,
                PrimaryButtonText = "Cobrar",
                CloseButtonText = "Cancelar",
                XamlRoot = this.XamlRoot
            };
            if (await confirmar.ShowAsync() != ContentDialogResult.Primary) return;
            string metodoCobro = (metodos.SelectedItem as string) ?? "Efectivo";
            if (!await VentasStore.MarcarCobradaAsync(ticket, metodoCobro))
            {
                await MostrarDialogoAsync($"Cobrar {ticket}", "No se pudo registrar el cobro.", "Entendido");
                return;
            }
            await RecargarTodoAsync();
            await MostrarDialogoAsync($"{ticket} cobrado", $"Se registró el cobro en {metodoCobro} y pasó a Ingresos.", "Entendido");
        }

        // Sube por padres hasta la fila (Grid hijo directo de su tabla).
        private Grid? FilaDe(DependencyObject? control)
        {
            DependencyObject? actual = control;
            Grid? ultimaGrilla = null;
            while (actual != null && actual != TablaVentas && actual != TablaPorCobrar && actual != TablaEgresos)
            {
                if (actual is Grid g) ultimaGrilla = g;
                actual = VisualTreeHelper.GetParent(actual);
            }
            return ultimaGrilla;
        }

        private void ActualizarConteo()
        {
            AplicarFiltros();
        }
    }
}
