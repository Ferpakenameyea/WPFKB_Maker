using Newtonsoft.Json;
using System;
using System.Linq;
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

        private bool isDragging = false;
        private Point mouseDownPosition;
        private const double DragThresholdSquared = 100;

        public double ScrollSensitivity { get; set; } = 0.1;
        public double ZoomSensitivity { get; set; } = 0.001;
        public (int, int)? HoldStart = null;

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
        private void HandlePutHoldNote((int, int) selector)
        {
            if (HoldStart == null)
            {
                HoldStart = selector;
                return;
            }

            if (selector.Item2 != HoldStart.Value.Item2)
            {
                MessageBox.Show("HOLD 音符的起始和终止位置应当位于同一列");
                return;
            }

            if (selector.Item1 <= HoldStart.Value.Item1)
            {
                MessageBox.Show("HOLD 音符的终止位置应当在起始位置之后");
                return;
            }

            Note note = new HoldNote((HoldStart.Value, selector));
            lock (this.sheetRenderer.Sheet)
            {
                this.sheetRenderer.Sheet.PutNote(
                    HoldStart.Value.Item1,
                    HoldStart.Value.Item2,
                    note);
            }
            HoldStart = null;
        }
        private void HandlePutHitNode((int, int) selector)
        {
            lock (this.sheetRenderer.Sheet)
            {
                this.sheetRenderer.Sheet.PutNote(
                    selector.Item1,
                    selector.Item2, new HitNote(selector));
            }
        }
        private void HandleRemoveNote((int, int) selector)
        {
            var query = from note in this.sheetRenderer.NotesToRender
                        where IsSelectedNote(note, selector)
                        orderby SelectNotePriority(note)
                        select note;
            
            if (!query.Any())
            {
                return;
            }

            var position = query.First().BasePosition;
            lock (this.sheetRenderer.Sheet)
            {
                this.sheetRenderer.Sheet.DeleteNote(position.Item1, position.Item2);
            }
        }
        private bool IsSelectedNote(Note note, (int, int) selector)
        {
            if (note is HitNote)
            {
                return note.BasePosition == selector;
            }

            var hold = note as HoldNote;
            return hold.BasePosition.Item2 == selector.Item2 && 
                hold.BasePosition.Item1 <= selector.Item1 &&
                hold.End.Item1 >= selector.Item1;
        }
        private int SelectNotePriority(Note note)
        {
            if (note is HitNote)
            {
                return 1;
            }
            return 2;
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
                this.HoldStart = null;
            };

            holdModeButton.Unchecked += (sender, e) =>
            {
                if (this.Mode == PutMode.HOLD)
                {
                    this.Mode = PutMode.VIEW;
                }
                this.HoldStart = null;
            };
        }
        public enum PutMode
        {
            HIT, HOLD, VIEW
        }

        private void CanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                this.draggingBox.Visibility = Visibility.Hidden;
                HandleDragging(new Rect(
                    this.mouseDownPosition,
                    e.GetPosition(this.imageCanvas)                    
                ));
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                HandleLeftClick();
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                HandleRightClick();
            }
        }

        private void HandleDragging(Rect rect)
        {
            
        }

        private void CanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(this.imageCanvas);
                var diff = pos - this.mouseDownPosition;
                if (!isDragging && diff.LengthSquared > DragThresholdSquared)
                {
                    isDragging = true;
                    draggingBox.Visibility = Visibility.Visible;
                }

                if (isDragging)
                {
                    double x = Math.Min(pos.X, this.mouseDownPosition.X);
                    double y = Math.Min(pos.Y, this.mouseDownPosition.Y);

                    double width = Math.Abs(pos.X - this.mouseDownPosition.X);
                    double height = Math.Abs(pos.Y - this.mouseDownPosition.Y);

                    Canvas.SetLeft(draggingBox, x);
                    Canvas.SetTop(draggingBox, y);

                    draggingBox.Width = width;
                    draggingBox.Height = height;
                }
            }
        }
        private void CanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            this.mouseDownPosition = e.GetPosition(this.imageCanvas);
            isDragging = false;
        }
        private void HandleLeftClick()
        {
            if (this.sheetRenderer == null || 
                this.sheetRenderer.Sheet == null ||
                this.Mode == PutMode.VIEW)
            {
                return;
            }
            var selector = this.sheetRenderer.Selector;
            if (selector == null)
            {
                return;
            }
            if (this.Mode == PutMode.HIT)
            {
                HandlePutHitNode(selector.Value);
            }
            else
            {
                HandlePutHoldNote(selector.Value);
            }
        }
        private void HandleRightClick()
        {
            if (this.sheetRenderer == null ||
                this.sheetRenderer.Sheet == null ||
                this.Mode == PutMode.VIEW)
            {
                return;
            }
            var selector = this.sheetRenderer.Selector;
            if (selector == null)
            {
                return;
            }
            this.HandleRemoveNote(selector.Value);
        }
    }
}
