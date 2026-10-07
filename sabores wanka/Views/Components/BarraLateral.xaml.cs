using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace sabores_wanka.Views.Components
{
    public sealed partial class BarraLateral : UserControl
    {
        private static readonly SolidColorBrush ActiveItemBackgroundBrush = new(Microsoft.UI.ColorHelper.FromArgb(0x20, 0xFF, 0xFF, 0xFF));
        private static readonly SolidColorBrush ActiveItemIconBrush = new(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x3B, 0x82, 0xF6));
        private static readonly SolidColorBrush ActiveItemTextBrush = new(Microsoft.UI.Colors.White);
        private static readonly SolidColorBrush InactiveItemBackgroundBrush = new(Microsoft.UI.Colors.Transparent);
        private static readonly SolidColorBrush InactiveItemIconBrush = new(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xA1, 0xA1, 0xAA));
        private static readonly SolidColorBrush InactiveItemTextBrush = new(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xD4, 0xD4, 0xD8));

        public event EventHandler? PuntoDeVentaSelected;
        public event EventHandler? HistorialSelected;
        public event EventHandler? ProductosSelected;
        public event EventHandler? WhatsappSelected;
        public event EventHandler? InformacionNegocioSelected;

        public BarraLateral()
        {
            this.InitializeComponent();
            SidebarPanel.IsHitTestVisible = false;
            SetActiveNavigation(string.Empty);
        }

        public void SetActiveNavigation(string key)
        {
            SetNavigationButtonState(HistorialButton, key == "Historial");
            SetNavigationButtonState(ProductosButton, key == "Productos");
        }

        private static void SetNavigationButtonState(Button button, bool isActive)
        {
            button.Background = isActive ? ActiveItemBackgroundBrush : InactiveItemBackgroundBrush;

            if (button.Content is not StackPanel content || content.Children.Count < 2)
            {
                return;
            }

            if (content.Children[0] is FontIcon icon)
            {
                icon.Foreground = isActive ? ActiveItemIconBrush : InactiveItemIconBrush;
            }

            if (content.Children[1] is TextBlock label)
            {
                label.Foreground = isActive ? ActiveItemTextBrush : InactiveItemTextBrush;
                label.FontWeight = isActive ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
            }
        }

        private void TriggerZone_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            OpenSidebar();
        }

        private void SidebarPanel_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            OpenSidebar();
        }

        private void SidebarPanel_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            CloseSidebar();
        }

        private void OpenSidebar()
        {
            SidebarPanel.IsHitTestVisible = true;
            AnimateSidebar(0);
        }

        private void CloseSidebar()
        {
            SidebarPanel.IsHitTestVisible = false;
            AnimateSidebar(-280);
        }

        private void AnimateSidebar(double targetX)
        {
            var storyboard = new Storyboard();
            var animation = new DoubleAnimation
            {
                To = targetX,
                Duration = new Duration(TimeSpan.FromSeconds(0.4)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard.SetTarget(animation, SidebarTranslate);
            Storyboard.SetTargetProperty(animation, "X");

            storyboard.Children.Add(animation);
            storyboard.Begin();
        }

        private void WhatsappButton_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNavigation(string.Empty);
            CloseSidebar();
            WhatsappSelected?.Invoke(this, EventArgs.Empty);
        }

        // Encabezado "Sabores Wanka": abre la ficha del negocio.
        private void NegocioButton_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNavigation(string.Empty);
            CloseSidebar();
            InformacionNegocioSelected?.Invoke(this, EventArgs.Empty);
        }

        private void TerminalButton_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNavigation(string.Empty);
            CloseSidebar();
            PuntoDeVentaSelected?.Invoke(this, EventArgs.Empty);
        }

        private void HistorialButton_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNavigation("Historial");
            CloseSidebar();
            HistorialSelected?.Invoke(this, EventArgs.Empty);
        }

        private void ProductosButton_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNavigation("Productos");
            CloseSidebar();
            ProductosSelected?.Invoke(this, EventArgs.Empty);
        }
    }
}
