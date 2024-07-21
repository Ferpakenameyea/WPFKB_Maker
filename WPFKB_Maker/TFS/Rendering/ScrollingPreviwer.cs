using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WPFKB_Maker.TFS.KBBeat;

namespace WPFKB_Maker.TFS.Rendering
{
    public class ScrollingPreviwer
    {
        private readonly Image mapImage;
        private readonly Image lineImage;
        private readonly List<int> notes = new List<int>();
        private readonly List<Point> points = new List<Point>();
        private readonly DrawingVisual drawingVisual = new DrawingVisual();
        private readonly SheetEditor editor;
        private RenderTargetBitmap bitmap;

        private readonly DrawingVisual lineDrawingVisual = new DrawingVisual();
        private RenderTargetBitmap lineBitmap;

        public double LineBitmapWidth { get => lineBitmap.Width; }
        public double LineBitmapHeight { get => lineBitmap.Height; }
        public double LineImageWidth { get => lineImage.ActualWidth; }
        public double LineImageHeight { get => lineImage.ActualHeight; }

        private readonly Stopwatch stopwatch = new Stopwatch();

        private double Width { get => bitmap.Width; }
        private double Height { get => bitmap.Height; }

        public int Top { get; private set; } = 10;

        public const int windowSizeBeat = 4;

        private readonly ScrollingPreviwerStyle style = new ScrollingPreviwerStyle()
        {
            ShapeBorder = new Pen(Brushes.Red, 1),
            Brush = new SolidColorBrush()
            {
                Color = Color.FromArgb(180, 255, 0, 0),
            },
            LinePen = new Pen(Brushes.LightGreen, 2),
        };

        public ScrollingPreviwer(
            SheetEditor sheetEditor,
            Image image,
            Image lineImage,
            int initialWidth,
            int initialHeight,
            double dpiX,
            double dpiY)
        {
            this.editor = sheetEditor;

            sheetEditor.OnSheetPut += Update;
            sheetEditor.OnSheetDelete += Update;
            sheetEditor.OnSheetClear += UpdateClear;

            this.bitmap = new RenderTargetBitmap(initialWidth, initialHeight, dpiX, dpiY, PixelFormats.Pbgra32);

            this.mapImage = image;
            this.mapImage.Source = this.bitmap;

            this.lineBitmap = new RenderTargetBitmap(initialWidth, initialHeight, dpiX, dpiY, PixelFormats.Pbgra32);

            this.lineImage = lineImage;
            this.lineImage.Source = this.lineBitmap;

            Project.ObservableCurrentProject.PropertyChanged += (sender, e) =>
            {
                double secPerBeat = 60 / Project.Current.Meta.Bpm;
                int recs = (int)Math.Ceiling(Project.Current.Meta.LengthSeconds / (secPerBeat * windowSizeBeat));
                notes.Clear();
                for (int i = 0; i < recs; i++)
                {
                    notes.Add(0);
                }

                foreach (var pos in Project.Current.Sheet.Values.Select(note => note.BasePosition))
                {
                    int i = pos.Item1 / (windowSizeBeat * 96);
                    notes[i]++;
                }

                this.FlushRender();
            };

            CompositionTarget.Rendering += RenderLine;
        }

        private void RenderLine(object sender, EventArgs e)
        {

            this.lineBitmap.Clear();

            double y = 0;
            if (Project.Current != null)
            {
                double time = this.editor.Renderer.TriggerLineCurrentTimeSecond;
                double percentage = time / Project.Current.Meta.LengthSeconds;

                y = this.Height * (1 - percentage);
            }

            using (var context = this.lineDrawingVisual.RenderOpen())
            {
                context.DrawLine(
                    style.LinePen, new Point(0, y), new Point(Width, y));
            }

            this.lineBitmap.Render(this.lineDrawingVisual);
        }

        ~ScrollingPreviwer()
        {
            this.editor.OnSheetPut += Update;
            this.editor.OnSheetDelete += Update;
            this.editor.OnSheetClear += UpdateClear;
        }

        private void Update(object sender, SheetChangeEventArgs e)
        {
            if (notes.Count == 0)
            {
                return;
            }

            if (e.Add)
            {
                foreach (var position in e.Target)
                {
                    int i = position.Item1 / (windowSizeBeat * 96);
                    if (i >= 0 && i < notes.Count)
                    {
                        notes[i]++;
                    }

                }
            }
            else
            {
                foreach (var position in e.Target)
                {
                    int i = position.Item1 / (windowSizeBeat * 96);
                    if (i >= 0 && i < notes.Count)
                    {
                        notes[i]--;
                    }
                }
            }

            FlushRender();
        }

        private void UpdateClear()
        {
            for (int i = 0; i < notes.Count; i++)
            {
                notes[i] = 0;
            }
            this.FlushRender();
        }

        private void FlushRender()
        {
            int max = notes.Max();

            if (max % 5 == 0)
            {
                Top = Math.Max(max, 10);
            }
            else
            {
                Top = ((max / 5) + 1) * 5;
            }

            double totalWidth = Width;
            double totalHeight = Height;

            double verticalStep = this.Height / this.notes.Count;
            double y = this.Height;
            bitmap.Clear();
            using (var context = this.drawingVisual.RenderOpen())
            {
                points.Clear();
                this.notes.ForEach(p =>
                {
                    points.Add(new Point(totalWidth * p / Top, y));
                    y -= verticalStep;
                });
                points.Add(new Point(0, 0));
                points.Add(new Point(0, totalHeight));

                var geometry = new StreamGeometry();

                using (var geoContext = geometry.Open())
                {
                    geoContext.BeginFigure(points[0], true, true);
                    geoContext.PolyLineTo(points, true, true);
                }

                context.DrawGeometry(style.Brush, style.ShapeBorder, geometry);
            }

            this.bitmap.Render(this.drawingVisual);
        }

        private class ScrollingPreviwerStyle
        {
            public Pen ShapeBorder { get; set; }
            public Brush Brush { get; set; }
            public Pen LinePen { get; set; }
        }
    }
}
