using System.Windows;
using System.Windows.Controls;
using WPFKB_Maker.TFS;

namespace WPFKB_Maker
{
    public partial class MainWindow : Window
    {
        private double dpiX;
        private double dpiY;

        private SheetRenderer sheetRenderer;

        public MainWindow()
        {
            InitializeComponent();
            versionBox.Content = $"KBBeat Maker WPF {Version.version}";

            this.Loaded += (sender, e) =>
            {
                PresentationSource source = PresentationSource.FromVisual(this);
                if (source != null)
                {
                    this.dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
                    this.dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
                }

                this.sheetRenderer = new SheetRenderer(this.renderer, 
                    (int)this.rendererBorder.ActualWidth, 
                    (int)this.rendererBorder.ActualHeight, 
                    dpiX, dpiY);
            };
        }

        private void DropdownButtonBehaviour(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            button.ContextMenu.IsOpen = true;
        }
    }
}
