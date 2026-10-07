using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace sabores_wanka.Views.Components
{
    // Banner de actualizacion: MainWindow muestra, oculta y reporta progreso.
    // La descarga real vive en ActualizacionService (MainWindow la ejecuta).
    public sealed partial class ActualizacionBanner : UserControl
    {
        private string _version = "";

        // MainWindow descarga el release indicado y relanza la app.
        public event EventHandler<string>? ActualizarSolicitado;

        public ActualizacionBanner()
        {
            this.InitializeComponent();
        }

        // Muestra el banner para la version remota (ej. "1.0.1").
        public void Mostrar(string version)
        {
            _version = (version ?? "").Trim();
            VersionText.Text = "v" + _version;
            EstadoText.Text = "Hay una versión nueva lista para instalar.";
            DescargaBar.Value = 0;
            DescargaBar.Visibility = Visibility.Collapsed;
            ActualizarBtn.IsEnabled = true;
            this.Visibility = Visibility.Visible;
        }

        // Progreso 0-100 durante la descarga del ZIP.
        public void MostrarProgreso(double porcentaje)
        {
            DescargaBar.Visibility = Visibility.Visible;
            DescargaBar.Value = Math.Clamp(porcentaje, 0, 100);
            EstadoText.Text = $"Descargando actualización... {DescargaBar.Value:0}%";
        }

        // Error de red o ZIP invalido: deja reintentar.
        public void MostrarError(string mensaje)
        {
            DescargaBar.Visibility = Visibility.Collapsed;
            EstadoText.Text = mensaje;
            ActualizarBtn.IsEnabled = true;
        }

        public void Ocultar() => this.Visibility = Visibility.Collapsed;

        private void ActualizarBtn_Click(object sender, RoutedEventArgs e)
        {
            ActualizarBtn.IsEnabled = false;
            DescargaBar.Visibility = Visibility.Visible;
            EstadoText.Text = "Preparando descarga...";
            ActualizarSolicitado?.Invoke(this, _version);
        }

        private void PosponerBtn_Click(object sender, RoutedEventArgs e) => Ocultar();
    }
}
