using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WPFKB_Maker.TFS;
using WPFKB_Maker.TFS.KBBeat;

namespace WPFKB_Maker
{
    public partial class MainWindow : Window
    {
        public const bool debug = true;

        private double dpiX;
        private double dpiY;

        public PutMode Mode { get; set; } = MainWindow.PutMode.VIEW;

        private SheetRenderer sheetRenderer;
        private DebugConsole debugConsole;

        public double ScrollSensitivity { get; set; } = 0.1;
        public double ZoomSensitivity { get; set; } = 0.001;

        public MainWindow()
        {
            InitializeComponent();
            if (debug)
            {
                (this.debugConsole = new DebugConsole()).Show();
            }
            versionBox.Content = $"KBBeat Maker WPF {TFS.Version.version}";

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

            this.Closing += (sender, e) =>
            {
                this.debugConsole?.Close();
            };
            this.InitializeToggleButtons();
        }
        
        private void ScrollSheetRenderer(object sender, MouseWheelEventArgs e)
        {
            if (this.sheetRenderer != null)
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl))
                {
                    double newZoom = this.zoomSlider.Value - e.Delta * ZoomSensitivity;
                    newZoom = Math.Min(newZoom, this.zoomSlider.Maximum);
                    newZoom = Math.Max(newZoom, this.zoomSlider.Minimum);
                    this.zoomSlider.Value = newZoom;
                }
                else
                {
                    this.sheetRenderer.RenderFromY += e.Delta * ScrollSensitivity;
                }
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

        private void HandleNoteProcessing(object sender, MouseButtonEventArgs e)
        {
            if (this.sheetRenderer == null)
            {
                return;
            }

            var selector = this.sheetRenderer.Selector;
            var res = this.sheetRenderer.Sheet?.PutNote(
                selector.Value.Item1,
                selector.Value.Item2, new HitNote(selector.Value));
            if (res.HasValue && res.Value == true)
            {
                Debug.console.Write($"Put note at: {selector}");
            }
            else
            {
                Debug.console.Write($"Cannot put note at: {selector}");
            }
        }
        private void InitializeToggleButtons()
        {
            hitModeButton.Checked += (sender, e) =>
            {
                this.Mode = PutMode.HIT;
                holdModeButton.IsChecked = false;
            };

            hitModeButton.Unchecked += (sender, e) =>
            {
                if (this.Mode == PutMode.HIT)
                {
                    this.Mode = PutMode.VIEW;
                }
            };

            holdModeButton.Checked += (sender, e) =>
            {
                this.Mode = PutMode.HOLD;
                hitModeButton.IsChecked = false;
            };

            holdModeButton.Unchecked += (sender, e) =>
            {
                if (this.Mode == PutMode.HOLD)
                {
                    this.Mode = PutMode.VIEW;
                }
            };
        }

        public enum PutMode
        {
            HIT, HOLD, VIEW
        }
    }
}
