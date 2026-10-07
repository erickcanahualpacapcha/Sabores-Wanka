using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace sabores_wanka.Views.Whatsapp
{
    // WhatsApp Web embebido (patrón Nachis 1.0_whatsap): el QR se escanea una
    // vez en el WebView2 y la sesión persiste en el perfil local del equipo.
    public sealed partial class WhatsappView : UserControl
    {
        private static readonly Uri WhatsappWeb = new("https://web.whatsapp.com/");

        public WhatsappView()
        {
            this.InitializeComponent();
            this.Loaded += WhatsappView_Loaded;
        }

        // Inicializa el motor y navega; si el equipo no tiene WebView2 usa el fallback.
        private async void WhatsappView_Loaded(object sender, RoutedEventArgs e)
        {
            await ConectarAsync();
        }

        private async System.Threading.Tasks.Task ConectarAsync()
        {
            if (WspWebView == null) return;
            try
            {
                await WspWebView.EnsureCoreWebView2Async();
                WspWebView.Source = WhatsappWeb;
                if (FallbackPanel != null) FallbackPanel.Visibility = Visibility.Collapsed;
                WspWebView.Visibility = Visibility.Visible;
            }
            catch
            {
                WspWebView.Visibility = Visibility.Collapsed;
                if (FallbackPanel != null) FallbackPanel.Visibility = Visibility.Visible;
            }
        }

        private async void RecargarButton_Click(object sender, RoutedEventArgs e)
        {
            if (WspWebView?.CoreWebView2 != null)
            {
                WspWebView.Reload();
                return;
            }
            await ConectarAsync();
        }

        // Abre la misma sesión en el navegador del equipo.
        private async void AbrirNavegadorButton_Click(object sender, RoutedEventArgs e)
        {
            try { await Windows.System.Launcher.LaunchUriAsync(WhatsappWeb); }
            catch { }
        }
    }
}
