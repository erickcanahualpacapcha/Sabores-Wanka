using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace sabores_wanka.Views.HistorialDeVentas
{
    // Calendario del filtro por periodo: días (Diario/Semanal), meses
    // (Mensual) o años (Anual). Emite la fecha elegida con DateSelected.
    public sealed partial class CalendarioPeriodoControl : UserControl
    {
        private static readonly CultureInfo EsPe = new("es-PE");
        private static readonly SolidColorBrush Azul = new(Windows.UI.Color.FromArgb(0xFF, 0x00, 0x84, 0xFF));
        private static readonly SolidColorBrush AzulSuave = new(Windows.UI.Color.FromArgb(0x26, 0x00, 0x84, 0xFF));
        private static readonly SolidColorBrush Transparente = new(Windows.UI.Color.FromArgb(0x00, 0x00, 0x00, 0x00));
        private static readonly SolidColorBrush Texto = new(Windows.UI.Color.FromArgb(0xFF, 0xE2, 0xE8, 0xF0));
        private static readonly SolidColorBrush Apagado = new(Windows.UI.Color.FromArgb(0xFF, 0x64, 0x74, 0x8B));
        private static readonly SolidColorBrush Blanco = new(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));

        // Mes en pantalla (días/meses) o pivote de década (años).
        private DateTime _vistaMes = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private string _mode = "Diario";
        private DateTime _selectedDate = DateTime.Today;

        public event EventHandler<DateTime>? DateSelected;

        public string Mode
        {
            get => _mode;
            set { _mode = string.IsNullOrEmpty(value) ? "Diario" : value; Refrescar(); }
        }

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set { _selectedDate = value.Date; _vistaMes = new DateTime(value.Year, value.Month, 1); Refrescar(); }
        }

        public CalendarioPeriodoControl()
        {
            this.InitializeComponent();
            this.Loaded += (_, _) => Refrescar();
        }

        // Reconstruye la vista que toca según el periodo.
        public void Refrescar()
        {
            if (CeldasGrid == null || MesTituloText == null || DiasHeader == null) return;
            CeldasGrid.Children.Clear();
            CeldasGrid.RowDefinitions.Clear();
            CeldasGrid.ColumnDefinitions.Clear();
            if (_mode == "Anual")
            {
                DiasHeader.Visibility = Visibility.Collapsed;
                int decada = (_vistaMes.Year / 10) * 10;
                MesTituloText.Text = $"{decada} – {decada + 11}";
                PintarAnios(decada);
            }
            else if (_mode == "Mensual")
            {
                DiasHeader.Visibility = Visibility.Collapsed;
                MesTituloText.Text = _vistaMes.Year.ToString();
                PintarMeses();
            }
            else
            {
                DiasHeader.Visibility = Visibility.Visible;
                MesTituloText.Text = _vistaMes.ToString("MMMM yyyy", EsPe).ToUpper();
                PintarDias();
            }
        }

        // Grilla de días lunes-domingo; en Semanal se resalta la semana elegida.
        private void PintarDias()
        {
            for (int c = 0; c < 7; c++)
                CeldasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int r = 0; r < 6; r++)
                CeldasGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            DateTime lunes = InicioSemana(new DateTime(_vistaMes.Year, _vistaMes.Month, 1));
            DateTime hoy = DateTime.Today;
            DateTime iniSemanaSel = InicioSemana(_selectedDate);
            DateTime finSemanaSel = iniSemanaSel.AddDays(7);
            for (int i = 0; i < 42; i++)
            {
                DateTime dia = lunes.AddDays(i);
                bool delMes = dia.Month == _vistaMes.Month;
                bool elegido = dia.Date == _selectedDate.Date;
                bool enSemana = _mode == "Semanal" && dia >= iniSemanaSel && dia < finSemanaSel;
                var b = new Button
                {
                    Content = new TextBlock { Text = dia.Day.ToString(), FontSize = 12, Foreground = elegido ? Blanco : delMes ? Texto : Apagado },
                    Background = elegido ? Azul : enSemana ? AzulSuave : Transparente,
                    BorderThickness = new Thickness(dia.Date == hoy.Date && !elegido ? 1 : 0),
                    BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x64, 0x74, 0x8B)),
                    CornerRadius = new CornerRadius(18),
                    Width = 36,
                    Height = 36,
                    Padding = new Thickness(0),
                    MinWidth = 0,
                    MinHeight = 0,
                    Tag = dia,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                b.Click += DiaButton_Click;
                Grid.SetRow(b, i / 7);
                Grid.SetColumn(b, i % 7);
                CeldasGrid.Children.Add(b);
            }
        }

        // Solo meses ( Ene–Dic del año en pantalla ).
        private void PintarMeses()
        {
            for (int c = 0; c < 3; c++)
                CeldasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int r = 0; r < 4; r++)
                CeldasGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            string[] nombres = EsPe.DateTimeFormat.AbbreviatedMonthNames;
            for (int m = 1; m <= 12; m++)
            {
                bool elegido = _selectedDate.Year == _vistaMes.Year && _selectedDate.Month == m;
                var b = new Button
                {
                    Content = new TextBlock { Text = Capitalizar(nombres[m - 1].TrimEnd('.')), FontSize = 12, Foreground = elegido ? Blanco : Texto },
                    Background = elegido ? Azul : Transparente,
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(0, 8, 0, 8),
                    MinWidth = 0,
                    MinHeight = 0,
                    Tag = m,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                b.Click += MesButton_Click;
                Grid.SetRow(b, (m - 1) / 3);
                Grid.SetColumn(b, (m - 1) % 3);
                CeldasGrid.Children.Add(b);
            }
        }

        // Solo años ( docena desde la década en pantalla ).
        private void PintarAnios(int decada)
        {
            for (int c = 0; c < 3; c++)
                CeldasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int r = 0; r < 4; r++)
                CeldasGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int i = 0; i < 12; i++)
            {
                int anio = decada + i;
                bool elegido = _selectedDate.Year == anio;
                var b = new Button
                {
                    Content = new TextBlock { Text = anio.ToString(), FontSize = 12, Foreground = elegido ? Blanco : Texto },
                    Background = elegido ? Azul : Transparente,
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(0, 8, 0, 8),
                    MinWidth = 0,
                    MinHeight = 0,
                    Tag = anio,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                b.Click += AnioButton_Click;
                Grid.SetRow(b, i / 3);
                Grid.SetColumn(b, i % 3);
                CeldasGrid.Children.Add(b);
            }
        }

        private void DiaButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is DateTime dia)
            {
                _selectedDate = dia.Date;
                _vistaMes = new DateTime(dia.Year, dia.Month, 1);
                Refrescar();
                DateSelected?.Invoke(this, dia.Date);
            }
        }

        private void MesButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is int mes)
            {
                var fecha = new DateTime(_vistaMes.Year, mes, 1);
                _selectedDate = fecha;
                _vistaMes = fecha;
                Refrescar();
                DateSelected?.Invoke(this, fecha);
            }
        }

        private void AnioButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is int anio)
            {
                var fecha = new DateTime(anio, 1, 1);
                _selectedDate = fecha;
                _vistaMes = fecha;
                Refrescar();
                DateSelected?.Invoke(this, fecha);
            }
        }

        private void AnteriorButton_Click(object sender, RoutedEventArgs e)
        {
            _vistaMes = _mode == "Anual" ? _vistaMes.AddYears(-10) : _vistaMes.AddMonths(-1);
            Refrescar();
        }

        private void SiguienteButton_Click(object sender, RoutedEventArgs e)
        {
            _vistaMes = _mode == "Anual" ? _vistaMes.AddYears(10) : _vistaMes.AddMonths(1);
            Refrescar();
        }

        private void HoyButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = DateTime.Today;
            _vistaMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            Refrescar();
            DateSelected?.Invoke(this, DateTime.Today);
        }

        // Lunes de la semana de la fecha (semana peruana lunes-domingo).
        private static DateTime InicioSemana(DateTime d)
        {
            int dif = ((int)d.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return d.Date.AddDays(-dif);
        }

        private static string Capitalizar(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], EsPe) + s.Substring(1);
    }
}
