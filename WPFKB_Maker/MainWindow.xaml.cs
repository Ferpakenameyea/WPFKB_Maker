using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WPFKB_Maker.TFS;
using WPFKB_Maker.TFS.KBBeat;
using WPFKB_Maker.TFS.Rendering;
using WPFKB_Maker.TFS.Sound;

namespace WPFKB_Maker
{
    public partial class MainWindow : Window
    {
        public const bool debug = true;
        private double dpiX;
        private double dpiY;

        public PutMode Mode { get; set; } = MainWindow.PutMode.VIEW;

        public SheetRenderer SheetRenderer { get => SheetEditor?.Renderer; }
        public SheetPlayer SheetPlayer { get => SheetEditor?.Player; }
        public SheetEditor SheetEditor { get; private set; }
        private DebugConsole debugConsole;
        
        private (int, int)? dragStart = null;
        private bool isInDraggingSelection = false;
        private bool isInDraggingMoving = false;

        private Point mouseDownPosition;
        private const double DragThresholdSquared = 100;

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
            StrikeSoundEffectPlayer.Initialize();

            Project.ObservableCurrentProject.PropertyChanged += (sender, e) =>
            {
                this.projectName.Content = Project.Current.Meta.Name;
                this.currentBpm.Content = string.Format(
                    "BPM:{0:F2}", Project.Current.Meta.Bpm.ToString());
            };

            this.Loaded += (sender, e) =>
            {
                PresentationSource source = PresentationSource.FromVisual(this);
                if (source != null)
                {
                    this.dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
                    this.dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
                }

                var renderer = new SheetRenderer(this.renderer,
                    (int)this.rendererBorder.ActualWidth,
                    (int)this.rendererBorder.ActualHeight,
                    dpiX, dpiY);
                var player = new SheetPlayer(renderer);
                this.SheetEditor = new SheetEditor(renderer, player);

                this.zoomSlider.Value = this.SheetRenderer.Zoom;
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
            if (this.SheetRenderer != null)
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
                    this.SheetRenderer.RenderFromY += e.Delta * ScrollSensitivity;
                }
            }
        }
        private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.SheetRenderer != null && this.zoomText != null)
            {
                this.SheetRenderer.Zoom = e.NewValue;
                this.zoomText.Text = string.Format("缩放 {0:0.0}|4.0", e.NewValue);
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
                this.SheetEditor.HoldStart = null;
            };

            holdModeButton.Unchecked += (sender, e) =>
            {
                if (this.Mode == PutMode.HOLD)
                {
                    this.Mode = PutMode.VIEW;
                }
                this.SheetEditor.HoldStart = null;
            };
        }
        public enum PutMode
        {
            HIT, HOLD, VIEW
        }
        private void CanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isInDraggingSelection)
            {
                isInDraggingSelection = false;
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
            var start = this.SheetEditor.ScreenToSheetPosition(rect.BottomLeft);
            var end = this.SheetEditor.ScreenToSheetPosition(rect.TopRight);
            if (start.HasValue && end.HasValue)
            {
                this.SheetEditor.SelectNotesByDragging(start.Value, end.Value);
            }
        }
        private void CanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (this.isInDraggingMoving)
                {
                    var selector = this.SheetEditor.Selector;
                    if (selector != this.dragStart)
                    {
                        this.SheetEditor.MoveSelectionShadow((
                            selector.Value.Item1 - this.dragStart.Value.Item1,
                            selector.Value.Item2 - this.dragStart.Value.Item2));

                        this.dragStart = selector;
                    }
                    return;
                }

                var pos = e.GetPosition(this.imageCanvas);
                var diff = pos - this.mouseDownPosition;
                if (!isInDraggingSelection && diff.LengthSquared > DragThresholdSquared)
                {
                    isInDraggingSelection = true;
                    draggingBox.Visibility = Visibility.Visible;
                }

                if (isInDraggingSelection)
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
            this.draggingBox.Visibility = Visibility.Hidden;
            this.dragStart = this.SheetEditor.Selector;
            isInDraggingSelection = false;

            var selector = this.SheetEditor.Selector;
            var selectedNote = this.SheetEditor.Sheet.GetNote(selector.Value.Item1, selector.Value.Item2);

            if (this.SheetEditor.IsInSelection)
            {
                if (this.SheetEditor.SelectedNotes.Where((note) =>
                {
                    if (note is HitNote)
                    {
                        return note.BasePosition == selector;
                    }
                    var hold = note as HoldNote;
                    return hold.Start.Item2 == selector.Value.Item2 &&
                        hold.Start.Item1 <= selector.Value.Item1 &&
                        hold.End.Item2 >= selector.Value.Item2;
                }).Any())
                {
                    this.isInDraggingMoving = true;
                }
                else
                {
                    this.isInDraggingMoving = false;
                    this.SheetEditor.FlushSelectionMoving();
                }
            }
        }
        private void HandleLeftClick()
        {
            if (!this.SheetEditor.HasSheet() ||
                this.Mode == PutMode.VIEW)
            {
                return;
            }
            var selector = this.SheetEditor.Selector;
            if (selector == null)
            {
                return;
            }

            if (this.isInDraggingMoving)
            {
                return;
            }

            var existingNote = this.SheetEditor.Sheet.Values
                .Where((note) =>
                {
                    if (note is HitNote)
                    {
                        return note.BasePosition == selector;
                    }
                    var hold = note as HoldNote;
                    return hold.Start.Item2 == selector.Value.Item2 &&
                        hold.Start.Item1 <= selector.Value.Item1 &&
                        hold.End.Item1 >= selector.Value.Item1;
                }).FirstOrDefault();

            if (existingNote != null)
            {
                this.SheetEditor.SelectSingleNote(existingNote);
                return;
            }

            if (this.Mode == PutMode.HIT)
            {
                this.SheetEditor.PutHitNode(selector.Value);
            }
            else
            {
                this.SheetEditor.PutHoldNote(selector.Value);
            }
        }
        private void HandleRightClick()
        {
            if (!this.SheetEditor.HasSheet() || this.Mode == PutMode.VIEW)
            {
                return;
            }
            var selector = this.SheetEditor.Selector;
            if (selector == null)
            {
                return;
            }
            this.SheetEditor.RemoveNote(selector.Value);
        }
        private void HandleKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                if (this.isInDraggingMoving)
                {
                    this.isInDraggingMoving = false;
                    this.SheetEditor.FlushSelectionMoving();
                }
            }
        }
        private async void NewProjectButtonDown(object sender, RoutedEventArgs e)
        {
            if (!await TryAskForSave())
            {
                return;
            }

            new NewProjectWindow(this).Show();
        }
        private async void SaveButtonClick(object sender, RoutedEventArgs e)
        {
            if (Project.Current == null)
            {
                MessageBox.Show("当前没有创建任何项目");
                return;
            }

            var button = sender as Button;
            button.IsEnabled = false;
            await Project.SaveNew(Project.Current).ConfigureAwait(true);
            button.IsEnabled = true;
        }
        private async void OpenAnotherProjectButtonDown(object sender, RoutedEventArgs e)
        {
            if (!await TryAskForSave())
            {
                return;
            }

            var dialog = new OpenFileDialog()
            {
                Title = "Open project",
                Filter = "KBBeat wpf project (*.kbpwpf)|*.kbpwpf"
            };
            var result = dialog.ShowDialog(this);

            if (result == true)
            {
                var project = await Project.LoadProjectFromFile(dialog.FileName);
                Project.Current = project;
                this.SheetPlayer.Project = project;
                this.SheetEditor.Project = project;
            }
        }
        private async Task<bool> TryAskForSave()
        {
            if (Project.Current == null)
            {
                return true;
            }
            var result = MessageBox.Show("是否保存现有的项目？未保存的更改将被丢失", "切换项目", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            switch (result)
            {
                case MessageBoxResult.Yes:
                    await Project.SaveNew(Project.Current);
                    return true;
                case MessageBoxResult.No:
                    return true;
                case MessageBoxResult.Cancel:
                default:
                    return false;
            }
        }
        private void PlayButtonClicked(object sender, RoutedEventArgs e)
        {
            if (Project.Current == null)
            {
                MessageBox.Show("当前没有创建项目！");
                return;
            }

            if (SheetPlayer.Playing)
            {
                SheetPlayer.Pause();
            }
            else
            {
                SheetPlayer.Play();
            }
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            StrikeSoundEffectPlayer.Play();
            watch.Stop();

            Debug.console.Write($"Sound playing used {watch.ElapsedMilliseconds} ms");
        }

        private async void ExportLevel(object sender, RoutedEventArgs e)
        {
            if (Project.Current == null)
            {
                MessageBox.Show("当前没有项目可以导出！", "无项目", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var button = sender as Button;
            button.IsEnabled = false;

            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择保存路径"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                await Project.SaveProjectAsKBBeatPackageAsync(Project.Current, dialog.SelectedPath);
            }
            button.IsEnabled = true;
        }

        private async void SaveAnotherPathClick(object sender, RoutedEventArgs e)
        {
            if (Project.Current == null)
            {
                MessageBox.Show("当前没有创建项目！");
                return;
            }

            var menuItem = sender as MenuItem;
            menuItem.IsEnabled = false;

            SaveFileDialog saveFileDialog = new SaveFileDialog()
            {
                Title = "Save project",
                Filter = "KBBeat wpf project (*.kbpwpf)|*.kbpwpf",
                FileName = "新建项目.kbpwpf"
            };
            try
            {
                if (saveFileDialog.ShowDialog() == true)
                {
                    await Project.SaveNew(Project.Current, saveFileDialog.FileName)
                        .ConfigureAwait(true);
                }
            }
            catch (Exception err)
            {
                MessageBox.Show("另存项目时失败：" + err.Message);
            }
            finally
            {
                menuItem.IsEnabled = true;
            }
        }
    }
}
