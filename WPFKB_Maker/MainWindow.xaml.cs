using Newtonsoft.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xaml;
using WPFKB_Maker.TFS;
using WPFKB_Maker.TFS.KBBeat;

namespace WPFKB_Maker
{
    public partial class MainWindow : Window
    {
        public const bool debug = true;

        private double dpiX;
        private double dpiY;

        private SheetRenderer sheetRenderer;

        public double ScrollSensitivity { get; set; } = 0.1;

        public MainWindow()
        {
            InitializeComponent();
            if (debug)
            {
                (new DebugConsole()).Show();
            }
            versionBox.Content = $"KBBeat Maker WPF {Version.version}";

            var settings = new JsonSerializerSettings();
            settings.TypeNameHandling = TypeNameHandling.Auto;

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

                this.zoomSlider.Value = this.sheetRenderer.Zoom;
            };

            this.LayoutUpdated += (sender, e) =>
            {
                Canvas.SetBottom(this.renderer, 0);
                Canvas.SetLeft(this.renderer, 
                    (this.imageCanvas.ActualWidth - this.renderer.Width) / 2);
                var x = this.renderer.ActualHeight / this.renderer.ActualWidth;
                this.renderer.Width = this.imageCanvas.ActualWidth;
                this.renderer.Height = this.renderer.Width * x;
            };
        }
        
        private void ScrollSheetRenderer(object sender, MouseWheelEventArgs e)
        {
            if (this.sheetRenderer != null)
            {
                this.sheetRenderer.RenderFromY += e.Delta * ScrollSensitivity;
            }
        }

        private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.sheetRenderer != null && this.zoomText != null)
            {
                this.sheetRenderer.Zoom = e.NewValue;
                this.zoomText.Text = string.Format("缩放 {0:0.0}|4.0", e.NewValue);
            }
        }
    }
}
