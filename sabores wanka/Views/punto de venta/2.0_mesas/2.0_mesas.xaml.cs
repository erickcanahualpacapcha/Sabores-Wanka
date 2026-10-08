using System;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using Microsoft.UI.Xaml.Media.Animation;
using System.ComponentModel;

namespace sabores_wanka.Views.PuntoDeVenta
{
    // Mesa del plano: persiste en MesasStore (nombre/sala/capacidad).
    // Es el modelo del GridView estilo Nachis: el x:Bind repinta solo al
    // cambiar props, sin VisualTreeHelper. El estado es solo del turno.
    public sealed class MesaItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _nombre = "";
        public string Nombre
        {
            get => _nombre;
            set { _nombre = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Nombre))); }
        }

        public string Sala { get; set; } = "";

        private string _capacidadTexto = "2 Personas";
        public string CapacidadTexto
        {
            get => _capacidadTexto;
            set { _capacidadTexto = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CapacidadTexto))); }
        }

        private string _estado = "LIBRE";
        public string Estado
        {
            get => _estado;
            set
            {
                _estado = value;
                RefrescarVisualEstado();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Estado)));
            }
        }

        private string _estadoTexto = "Disponible";
        public string EstadoTexto
        {
            get => _estadoTexto;
            set { _estadoTexto = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EstadoTexto))); }
        }

        private string _detalle = "Sin comanda";
        public string Detalle
        {
            get => _detalle;
            set { _detalle = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Detalle))); }
        }

        private bool _esSeleccionada;
        public bool EsSeleccionada
        {
            get => _esSeleccionada;
            set
            {
                _esSeleccionada = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EsSeleccionada)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarcaVisible)));
            }
        }

        public Visibility MarcaVisible => EsSeleccionada ? Visibility.Visible : Visibility.Collapsed;

        private bool _configVisible;
        public bool ConfigVisible
        {
            get => _configVisible;
            set
            {
                _configVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConfigVisible)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorrarVisible)));
            }
        }

        // Fija = mesa base (no se borra ni renombra); se marca al cargar.
        public bool Fija { get; set; }

        // La papelera solo se ve en modo config y si no es fija.
        public Visibility BorrarVisible => ConfigVisible && !Fija ? Visibility.Visible : Visibility.Collapsed;

        public SolidColorBrush EstadoPillFondo { get; private set; } = LibrePillFondo;
        public SolidColorBrush EstadoColor { get; private set; } = LibreLetra;
        public SolidColorBrush FondoVisual { get; private set; } = LibreFondo;
        public SolidColorBrush IconoColor { get; private set; } = LibreLetra;
        public string Icono => "\uE8A9";

        private static readonly SolidColorBrush LibrePillFondo = new(ColorHelper.FromArgb(0xFF, 0x05, 0x32, 0x1F));
        private static readonly SolidColorBrush LibreLetra = new(ColorHelper.FromArgb(0xFF, 0x00, 0xC9, 0x82));
        private static readonly SolidColorBrush LibreFondo = new(ColorHelper.FromArgb(0xFF, 0x10, 0x1A, 0x14));
        private static readonly SolidColorBrush OcupadaPillFondo = new(ColorHelper.FromArgb(0xFF, 0x3D, 0x0F, 0x19));
        private static readonly SolidColorBrush OcupadaLetra = new(ColorHelper.FromArgb(0xFF, 0xF4, 0x3F, 0x5E));
        private static readonly SolidColorBrush OcupadaFondo = new(ColorHelper.FromArgb(0xFF, 0x1A, 0x10, 0x14));
        private static readonly SolidColorBrush ReservaPillFondo = new(ColorHelper.FromArgb(0xFF, 0x3A, 0x2E, 0x0A));
        private static readonly SolidColorBrush ReservaLetra = new(ColorHelper.FromArgb(0xFF, 0xEA, 0xB3, 0x08));
        private static readonly SolidColorBrush ReservaFondo = new(ColorHelper.FromArgb(0xFF, 0x1A, 0x15, 0x0E));

        // El badge siempre muestra el estado real (LIBRE/OCUPADA/RESERVA); la marca
        // ESTA ORDEN es aparte. Regla heredada: badge nunca es ACTIVA.
        private void RefrescarVisualEstado()
        {
            if (_estado == "OCUPADA")
            {
                EstadoPillFondo = OcupadaPillFondo; EstadoColor = OcupadaLetra;
                FondoVisual = OcupadaFondo; IconoColor = OcupadaLetra;
            }
            else if (_estado == "RESERVA")
            {
                EstadoPillFondo = ReservaPillFondo; EstadoColor = ReservaLetra;
                FondoVisual = ReservaFondo; IconoColor = ReservaLetra;
            }
            else
            {
                EstadoPillFondo = LibrePillFondo; EstadoColor = LibreLetra;
                FondoVisual = LibreFondo; IconoColor = LibreLetra;
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EstadoPillFondo)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EstadoColor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FondoVisual)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconoColor)));
        }

        public MesaItem(string nombre, string sala, string capacidad)
        {
            _nombre = nombre;
            Sala = sala;
            _capacidadTexto = capacidad;
            RefrescarVisualEstado();
        }
    }

    public sealed partial class MesasView : UserControl
    {
        public event EventHandler? CerrarRequested;
        public event EventHandler<string>? MesaSeleccionada;
        public event EventHandler<string>? MesaAbierta;
        private bool _configMode = false;

        // Estado del dialogo "Abrir mesa".
        private int _abrirMesaPersonas = 1;
        private bool _abrirMesaOtro = false;

        private string _selectedMesa = "";
        private string _selectedSala = "";
        // Fuente de mesas del GridView. El filtro por salon reconstruye el ItemsSource;
        // el filtro NUNCA deja huecos (el ItemsWrapGrid fluye solo, sin reflow manual).
        private readonly System.Collections.Generic.List<MesaItem> _mesas = new();
        // Salones del plano guardado (MesasStore, sin demo: arranca vacio).
        private readonly System.Collections.Generic.List<string> _salones = new();

        public MesasView()
        {
            this.InitializeComponent();
            this.Loaded += MesasView_Loaded;
        }

        private async void MesasView_Loaded(object sender, RoutedEventArgs e)
        {
            long t0 = MesasDiag.Stamp();
            await CargarPlanoAsync();
            AplicarFiltroSala(_selectedSala);
            ActualizarMarcaSeleccion();
            UpdateIrButtonText();
            MesasDiag.Log($"MesasView_Loaded total {MesasDiag.Since(t0):0.00} ms (mesas={_mesas.Count})");
        }

        // Lee el plano guardado (salones + mesas) y arma pills y combos.
        // Sin demo: si no hay nada, queda vacio para crear desde cero.
        private async System.Threading.Tasks.Task CargarPlanoAsync()
        {
            try
            {
                var plano = await Services.MesasStore.CargarAsync();
                _salones.Clear();
                _salones.AddRange(plano.Salones ?? new System.Collections.Generic.List<string>());
                _mesas.Clear();
                foreach (var m in plano.Mesas ?? new System.Collections.Generic.List<Services.MesaDato>())
                {
                    if (string.IsNullOrWhiteSpace(m.Nombre)) continue;
                    _mesas.Add(new MesaItem(m.Nombre.Trim(), m.Sala ?? "", string.IsNullOrWhiteSpace(m.Capacidad) ? "2 Personas" : m.Capacidad)
                    {
                        ConfigVisible = _configMode,
                        Fija = m.Fija,
                    });
                }
                if (!string.IsNullOrEmpty(_selectedSala) && !_salones.Contains(_selectedSala))
                    _selectedSala = "";
                if (string.IsNullOrEmpty(_selectedSala) && _salones.Count > 0)
                    _selectedSala = _salones[0];
                if (!string.IsNullOrEmpty(_selectedMesa) && !_mesas.Any(m => m.Nombre == _selectedMesa))
                    _selectedMesa = "";
                ReconstruirSalones();
                PoblarCombosSalones();
            }
            catch { }
        }

        // Guarda salones + mesas tras cada alta/baja/cambio.
        private async System.Threading.Tasks.Task PersistirPlanoAsync()
        {
            try
            {
                await Services.MesasStore.GuardarAsync(
                    new System.Collections.Generic.List<string>(_salones),
                    _mesas.Select(m => new Services.MesaDato { Nombre = m.Nombre, Sala = m.Sala, Capacidad = m.CapacidadTexto, Fija = m.Fija }).ToList());
            }
            catch { }
        }

        // Pills de salon con conteo; la activa en celeste, resto neutro.
        private void ReconstruirSalones()
        {
            if (SalonesPanel == null) return;
            SalonesPanel.Children.Clear();
            foreach (string sala in _salones)
            {
                int n = _mesas.Count(m => m.Sala == sala);
                var b = new Button
                {
                    Tag = sala,
                    Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x16, 0x1C, 0x28)),
                    BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x23, 0x2C, 0x3F)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(14, 6, 14, 6),
                };
                b.Content = new TextBlock { Text = $"{sala} ({n})", Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xCB, 0xD5, 0xE1)), FontSize = 11 };
                b.Click += SalonButton_Click;
                SalonesPanel.Children.Add(b);
            }
            PintarSalonActivo();
        }

        // Pinta la pill del salon activo (misma pinta de las fijas anteriores).
        private void PintarSalonActivo()
        {
            if (SalonesPanel == null) return;
            foreach (var child in SalonesPanel.Children)
            {
                if (child is not Button btn || btn.Tag is not string sala) continue;
                bool activo = sala == _selectedSala;
                btn.Background = activo
                    ? new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x00, 0xA8, 0xE8))
                    : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x16, 0x1C, 0x28));
                btn.BorderBrush = activo
                    ? new SolidColorBrush(Colors.Transparent)
                    : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x23, 0x2C, 0x3F));
                btn.BorderThickness = activo ? new Thickness(0) : new Thickness(1);
                if (btn.Content is TextBlock tb)
                {
                    tb.Foreground = activo
                        ? new SolidColorBrush(Colors.Black)
                        : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xCB, 0xD5, 0xE1));
                    tb.FontWeight = activo ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
                }
            }
        }

        // Combos de sala de los dialogos (nueva / editar mesa).
        private void PoblarCombosSalones()
        {
            foreach (var combo in new[] { SalaComboBox, EditarSalaComboBox })
            {
                if (combo == null) continue;
                combo.Items.Clear();
                foreach (string sala in _salones)
                    combo.Items.Add(new ComboBoxItem { Content = sala });
            }
        }

        // El boton dice ABRIR si la mesa elegida esta LIBRE; IR A LA si esta ocupada/reservada.
        // Sin mesas elegidas pide elegir (no dispara seleccion vacia al POS).
        private void UpdateIrButtonText()
        {
            if (IrMesaTextBlock == null) return;
            if (string.IsNullOrEmpty(_selectedMesa))
            {
                IrMesaTextBlock.Text = "SIN MESAS";
                return;
            }
            bool libre = IsSelectedMesaLibre();
            IrMesaTextBlock.Text = $"{(libre ? "ABRIR" : "IR A LA")} {_selectedMesa.ToUpper()}";
            if (IrMesaIcon != null) IrMesaIcon.Glyph = libre ? "\uE710" : "\uE8A9";
            if (IrMesaButton != null) IrMesaButton.Background = libre ? IrButtonOpenBrush : IrButtonEnterBrush;
        }

        private void SalonButton_Click(object sender, RoutedEventArgs e)
        {
            long t0 = MesasDiag.Stamp();
            var selected = sender as Button;
            if (selected == null) return;
            if (selected.Tag is string sala)
            {
                _selectedSala = sala;
                PintarSalonActivo();
                AplicarFiltroSala(sala);
            }
            MesasDiag.Log($"SalonButton_Click sala='{_selectedSala}' {MesasDiag.Since(t0):0.00} ms");
        }

        // Filtra por salon reconstruyendo el ItemsSource: sin huecos, sin reflow manual.
        // El GridView fluye solo (ItemsWrapGrid 2 columnas); el reflow anterior murio aqui.
        private void AplicarFiltroSala(string sala)
        {
            long t0 = MesasDiag.Stamp();
            if (MesaGrid == null) return;
            var visibles = _mesas.Where(m => m.Sala == sala).ToList();
            if (visibles.Count == 0) visibles = _mesas.ToList();
            MesaGrid.ItemsSource = visibles;
            MesasDiag.Log($"AplicarFiltroSala sala='{sala}' visibles={visibles.Count} {MesasDiag.Since(t0):0.00} ms");
        }

        // Marca ESTA ORDEN solo en la mesa elegida (O(1) sobre el modelo, sin visual-tree).
        private void ActualizarMarcaSeleccion()
        {
            foreach (var m in _mesas) m.EsSeleccionada = m.Nombre == _selectedMesa;
        }

        // Tap en la tarjeta del GridView: selecciona al instante (el press 0.96 + glow
        // del contenedor ya dio feedback en el down, igual que el ejemplo Nachis).
        private void MesaGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MesaItem mesa) SelectMesa(mesa.Nombre);
        }

        // Aplica la seleccion una sola vez (idempotente) y mide el costo real.
        private void SelectMesa(string mesa)
        {
            if (_selectedMesa == mesa) return;
            long t0 = MesasDiag.Stamp();
            _selectedMesa = mesa;
            UpdateIrButtonText();
            ActualizarMarcaSeleccion();
            MesasDiag.Log($"SelectMesa '{mesa}' {MesasDiag.Since(t0):0.00} ms");
        }

        // Colores del boton inferior: verde = ABRIR (libre), cian = IR A LA (ocupada).
        private static readonly SolidColorBrush IrButtonOpenBrush = new(ColorHelper.FromArgb(0xFF, 0x00, 0xC9, 0x82));
        private static readonly SolidColorBrush IrButtonEnterBrush = new(ColorHelper.FromArgb(0xFF, 0x00, 0xA8, 0xE8));

        // Pildoras de "Numero de personas" del dialogo Abrir mesa.
        private static readonly SolidColorBrush PersonaIdleBrush = new(ColorHelper.FromArgb(0xFF, 0x0F, 0x17, 0x2A));
        private static readonly SolidColorBrush PersonaIdleBorderBrush = new(ColorHelper.FromArgb(0xFF, 0x33, 0x41, 0x55));
        private static readonly SolidColorBrush PersonaIdleTextBrush = new(ColorHelper.FromArgb(0xFF, 0xCB, 0xD5, 0xE1));
        private static readonly SolidColorBrush PersonaSelectedBrush = new(ColorHelper.FromArgb(0xFF, 0xFB, 0xBF, 0x24));
        private static readonly SolidColorBrush PersonaSelectedTextBrush = new(ColorHelper.FromArgb(0xFF, 0x11, 0x10, 0x0B));

        private string? _mesaAEditar;
        private string? _mesaAEliminar;

        private void ConfigButton_Click(object sender, RoutedEventArgs e)
        {
            long t0 = MesasDiag.Stamp();
            _configMode = !_configMode;
            if (_configMode)
            {
                AgregarSalaButton.Visibility = Visibility.Visible;
                AgregarMesaCard.Visibility = Visibility.Visible;
                StartBlink(AgregarSalaButton, AgregarSalaTranslate);
                StartBlink(AgregarMesaCard, AgregarMesaTranslate);
            }
            else
            {
                AgregarSalaButton.Visibility = Visibility.Collapsed;
                AgregarMesaCard.Visibility = Visibility.Collapsed;
            }
            RefrescarBotonesConfig();
            MesasDiag.Log($"ConfigButton_Click configMode={_configMode} {MesasDiag.Since(t0):0.00} ms");
        }

        // Muestra u oculta los botones editar/eliminar de cada tarjeta (binding ConfigVisible).
        private void RefrescarBotonesConfig()
        {
            foreach (var m in _mesas) m.ConfigVisible = _configMode;
        }

        private void EditarMesaIcon_Click(object sender, RoutedEventArgs e)
        {
            // La tarjeta enlazada trae el item en DataContext; Tag queda como respaldo.
            string? mesa = null;
            if (sender is Button btn && btn.DataContext is MesaItem item) mesa = item.Nombre;
            else if (sender is Button b2 && b2.Tag is string t) mesa = t;
            if (!string.IsNullOrEmpty(mesa))
            {
                _mesaAEditar = mesa;
                EditarNombreMesaTextBox.Text = mesa;
                // Las fijas no se renombran (nombre bloqueado, sala/capacidad si).
                var fijada = _mesas.FirstOrDefault(m => m.Nombre == mesa);
                EditarNombreMesaTextBox.IsEnabled = fijada == null || !fijada.Fija;
                var actual = fijada;
                if (actual != null)
                {
                    bool hallada = false;
                    foreach (ComboBoxItem opt in EditarSalaComboBox.Items)
                    {
                        if (opt.Content?.ToString() == actual.Sala) { EditarSalaComboBox.SelectedItem = opt; hallada = true; break; }
                    }
                    if (!hallada) EditarSalaComboBox.SelectedIndex = -1;
                }
                else EditarSalaComboBox.SelectedIndex = -1;
                EditarMesaDialog.Visibility = Visibility.Visible;
            }
        }

        private void EliminarMesaIcon_Click(object sender, RoutedEventArgs e)
        {
            string? mesa = null;
            if (sender is Button btn && btn.DataContext is MesaItem item) mesa = item.Nombre;
            else if (sender is Button b2 && b2.Tag is string t) mesa = t;
            // Las fijas no muestran papelera; esto es defensa extra.
            if (!string.IsNullOrEmpty(mesa)
                && _mesas.Any(m => m.Nombre == mesa && m.Fija)) return;
            if (!string.IsNullOrEmpty(mesa))
            {
                _mesaAEliminar = mesa;
                EliminarMesaNombreText.Text = mesa;
                EliminarMesaConfirmDialog.Visibility = Visibility.Visible;
            }
        }

        private void StartBlink(FrameworkElement element, Microsoft.UI.Xaml.Media.TranslateTransform translate)
        {
            var sb = new Storyboard();
            var opacityAnim = new DoubleAnimation
            {
                From = 1.0,
                To = 0.35,
                Duration = TimeSpan.FromMilliseconds(300),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(4)
            };
            Storyboard.SetTarget(opacityAnim, element);
            Storyboard.SetTargetProperty(opacityAnim, "Opacity");
            sb.Children.Add(opacityAnim);
            var shakeAnim = new DoubleAnimation
            {
                From = -4,
                To = 4,
                Duration = TimeSpan.FromMilliseconds(80),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(6)
            };
            Storyboard.SetTarget(shakeAnim, translate);
            Storyboard.SetTargetProperty(shakeAnim, "X");
            sb.Children.Add(shakeAnim);
            sb.Begin();
        }

        private void AgregarSalaButton_Click(object sender, RoutedEventArgs e)
        {
            NuevaSalaDialog.Visibility = Visibility.Visible;
        }

        private void CerrarDialogButton_Click(object sender, RoutedEventArgs e)
        {
            NuevaSalaDialog.Visibility = Visibility.Collapsed;
        }

        private async void CrearSalaButton_Click(object sender, RoutedEventArgs e)
        {
            string nombre = NombreSalaTextBox.Text?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(nombre)
                && !_salones.Any(s => string.Equals(s, nombre, StringComparison.OrdinalIgnoreCase)))
            {
                _salones.Add(nombre);
                _selectedSala = nombre;
                await PersistirPlanoAsync();
                ReconstruirSalones();
                PoblarCombosSalones();
                AplicarFiltroSala(_selectedSala);
            }
            NombreSalaTextBox.Text = "";
            NuevaSalaDialog.Visibility = Visibility.Collapsed;
        }

        private void AgregarMesaCard_Click(object sender, RoutedEventArgs e)
        {
            NuevaMesaDialog.Visibility = Visibility.Visible;
        }

        private void CerrarMesaDialogButton_Click(object sender, RoutedEventArgs e)
        {
            NuevaMesaDialog.Visibility = Visibility.Collapsed;
        }

        private async void CrearMesaButton_Click(object sender, RoutedEventArgs e)
        {
            string nombre = NombreMesaTextBox.Text?.Trim() ?? "";
            string sala = (SalaComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? _selectedSala;
            if (string.IsNullOrWhiteSpace(nombre)) return;
            // Sin salones no hay donde ponerla: primero se crea la sala.
            if (string.IsNullOrWhiteSpace(sala) || !_salones.Any(s => string.Equals(s, sala, StringComparison.OrdinalIgnoreCase))) return;
            if (_mesas.Any(m => string.Equals(m.Nombre, nombre, StringComparison.OrdinalIgnoreCase))) return; // ya existe

            var nueva = new MesaItem(nombre, sala, "2 Personas") { ConfigVisible = _configMode };
            _mesas.Add(nueva);
            _selectedMesa = nombre;
            await PersistirPlanoAsync();
            ReconstruirSalones();
            // Aplicar filtro actual
            AplicarFiltroSala(_selectedSala);
            ActualizarMarcaSeleccion();
            UpdateIrButtonText();
            NombreMesaTextBox.Text = "";
            SalaComboBox.SelectedIndex = -1;
            NuevaMesaDialog.Visibility = Visibility.Collapsed;
        }

        private void CerrarEditarMesaButton_Click(object sender, RoutedEventArgs e)
        {
            EditarMesaDialog.Visibility = Visibility.Collapsed;
        }

        private async void GuardarMesaButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_mesaAEditar) || string.IsNullOrWhiteSpace(EditarNombreMesaTextBox.Text)) return;
            string nuevo = EditarNombreMesaTextBox.Text.Trim();
            var enEdicion = _mesas.FirstOrDefault(m => m.Nombre == _mesaAEditar);
            // Defensa: la fija nunca cambia de nombre (el campo va bloqueado).
            if (enEdicion != null && enEdicion.Fija) nuevo = _mesaAEditar;
            // Sin duplicar el nombre de otra mesa existente.
            if (!string.Equals(nuevo, _mesaAEditar, StringComparison.OrdinalIgnoreCase)
                && _mesas.Any(m => string.Equals(m.Nombre, nuevo, StringComparison.OrdinalIgnoreCase))) return;
            string? comboSala = (EditarSalaComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string nuevaSala;
            if (!string.IsNullOrEmpty(comboSala)) nuevaSala = comboSala!;
            else nuevaSala = _mesas.FirstOrDefault(m => m.Nombre == _mesaAEditar)?.Sala ?? _selectedSala;
            string antiguo = _mesaAEditar!;
            // Renombrar en el modelo: el x:Bind repinta nombre y sala sin tocar el arbol.
            var mesa = _mesas.FirstOrDefault(m => m.Nombre == antiguo);
            if (mesa != null)
            {
                mesa.Nombre = nuevo;
                mesa.Sala = nuevaSala;
            }
            if (_selectedMesa == antiguo)
            {
                _selectedMesa = nuevo;
                UpdateIrButtonText();
                ActualizarMarcaSeleccion();
            }
            await PersistirPlanoAsync();
            ReconstruirSalones();
            // Re-filtrar por sala actual
            AplicarFiltroSala(_selectedSala);
            EditarMesaDialog.Visibility = Visibility.Collapsed;
            _mesaAEditar = null;
        }

        private void CancelarEliminarButton_Click(object sender, RoutedEventArgs e)
        {
            EliminarMesaConfirmDialog.Visibility = Visibility.Collapsed;
            _mesaAEliminar = null;
        }

        private async void ConfirmarEliminarButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_mesaAEliminar))
            {
                // Defensa: la fija nunca sale (no muestra papelera).
                if (_mesas.Any(m => m.Nombre == _mesaAEliminar && m.Fija))
                {
                    EliminarMesaConfirmDialog.Visibility = Visibility.Collapsed;
                    _mesaAEliminar = null;
                    return;
                }
                // Sale del modelo: el GridView la quita y fluye sin huecos.
                _mesas.RemoveAll(m => m.Nombre == _mesaAEliminar);
                if (_selectedMesa == _mesaAEliminar)
                {
                    _selectedMesa = "";
                    UpdateIrButtonText();
                    ActualizarMarcaSeleccion();
                }
                await PersistirPlanoAsync();
                ReconstruirSalones();
                AplicarFiltroSala(_selectedSala);
            }
            EliminarMesaConfirmDialog.Visibility = Visibility.Collapsed;
            _mesaAEliminar = null;
        }

        // Muta el estado del modelo a OCUPADA (tras "Abrir mesa"): el x:Bind repinta
        // capacidad, estado, detalle y badge sin buscar TextBlocks en el arbol.
        private void AplicarEstadoAbierta(string mesa, int personas, string empleado)
        {
            var item = _mesas.FirstOrDefault(m => m.Nombre == mesa);
            if (item == null) return;
            item.CapacidadTexto = personas == 1 ? "1 Persona" : $"{personas} Personas";
            item.EstadoTexto = "S/ 0.00";
            item.Detalle = $"Mozo: {empleado} (0m)";
            item.Estado = "OCUPADA";
        }

        public void MarcarMesaCerrada(string mesa)
        {
            var item = _mesas.FirstOrDefault(m => m.Nombre == mesa);
            if (item == null) return;
            item.CapacidadTexto = "2 Personas";
            item.EstadoTexto = "Disponible";
            item.Detalle = "Sin comanda";
            item.Estado = "LIBRE";
            UpdateIrButtonText();
            MesasDiag.Log($"MarcarMesaCerrada '{mesa}' -> LIBRE");
        }

        // Marca OCUPADA al recuperar una venta del disco (sin dialogo de apertura).
        public void MarcarMesaOcupada(string mesa)
        {
            var item = _mesas.FirstOrDefault(m => m.Nombre == mesa);
            if (item == null) return;
            item.EstadoTexto = "Recuperada";
            item.Detalle = "Comanda recuperada";
            item.Estado = "OCUPADA";
            UpdateIrButtonText();
        }

        private void CerrarButton_Click(object sender, RoutedEventArgs e)
        {
            CerrarRequested?.Invoke(this, EventArgs.Empty);
        }

        private bool IsSelectedMesaLibre() =>
            _mesas.FirstOrDefault(m => m.Nombre == _selectedMesa) is MesaItem item &&
            string.Equals(item.Estado, "LIBRE", StringComparison.OrdinalIgnoreCase);

        // ABRIR (mesa libre) -> abre el dialogo; IR A LA (ocupada) -> entra directo.
        // Sin mesa elegida no hace nada (plano vacio recien creado).
        private void IrMesaButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedMesa)) return;
            if (IsSelectedMesaLibre()) { AbrirMesaDialog_Show(); return; }
            MesaSeleccionada?.Invoke(this, _selectedMesa);
        }

        // Prepara y muestra el dialogo de apertura de mesa con valores por defecto.
        private void AbrirMesaDialog_Show()
        {
            AbrirMesaTitleText.Text = _selectedMesa;
            EmpleadoComboBox.SelectedIndex = -1;
            ComentarioTextBox.Text = "";
            OtroPersonasTextBox.Text = "";
            OtroPersonasTextBox.Visibility = Visibility.Collapsed;
            _abrirMesaOtro = false;
            _abrirMesaPersonas = 1;
            UpdatePersonaPills();
            AbrirMesaDialog.Visibility = Visibility.Visible;
            MesasDiag.Log($"AbrirMesaDialog abierto para '{_selectedMesa}'");
        }

        private void CerrarAbrirMesaButton_Click(object sender, RoutedEventArgs e)
        {
            AbrirMesaDialog.Visibility = Visibility.Collapsed;
        }

        private void PersonaPill_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string tag) return;
            if (tag == "Otro")
            {
                _abrirMesaOtro = true;
                OtroPersonasTextBox.Visibility = Visibility.Visible;
            }
            else
            {
                _abrirMesaOtro = false;
                if (int.TryParse(tag, out var n)) _abrirMesaPersonas = n;
                OtroPersonasTextBox.Visibility = Visibility.Collapsed;
            }
            UpdatePersonaPills();
        }

        // Pinta las pildoras segun la seleccion (numero fijo u "Otro").
        private void UpdatePersonaPills()
        {
            foreach (var child in PersonasPanel.Children)
            {
                if (child is not Button b || b.Tag is not string t) continue;
                bool selected = t == "Otro"
                    ? _abrirMesaOtro
                    : (!_abrirMesaOtro && int.TryParse(t, out var n) && n == _abrirMesaPersonas);
                b.Background = selected ? PersonaSelectedBrush : PersonaIdleBrush;
                b.BorderBrush = selected ? PersonaSelectedBrush : PersonaIdleBorderBrush;
                if (b.Content is TextBlock tb) tb.Foreground = selected ? PersonaSelectedTextBrush : PersonaIdleTextBrush;
            }
        }

        // Valida y confirma la apertura: emite MesaSeleccionada para cerrar el grid.
        private void ConfirmarAbrirMesaButton_Click(object sender, RoutedEventArgs e)
        {
            int personas = _abrirMesaOtro
                ? (int.TryParse(OtroPersonasTextBox.Text, out var n) ? n : 0)
                : _abrirMesaPersonas;
            if (personas <= 0)
            {
                MesasDiag.Log($"AbrirMesa '{_selectedMesa}' bloqueado: numero de personas invalido");
                return; // "Numero de personas" es requerido
            }
            string empleado = (EmpleadoComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "(sin asignar)";
            MesasDiag.Log($"AbrirMesa '{_selectedMesa}' personas={personas} empleado='{empleado}' comentario='{ComentarioTextBox.Text}'");
            AplicarEstadoAbierta(_selectedMesa, personas, empleado);
            UpdateIrButtonText();
            var mesaAbierta = _selectedMesa;
            AbrirMesaDialog.Visibility = Visibility.Collapsed;
            MesaAbierta?.Invoke(this, mesaAbierta);
        }
    }
}
