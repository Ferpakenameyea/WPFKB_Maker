using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WPFKB_Maker.TFS.KBBeat;

namespace WPFKB_Maker.TFS
{
    public class SheetRenderer
    {
        public Image Image { get; }

        private RenderTargetBitmap bitmap;
        private readonly DrawingVisual drawingVisual = new DrawingVisual();

        private double dpiX;
        private double dpiY;

        private BeatRenderStrategy strategy;
        public double BitmapVerticalRenderDistance { get => strategy.GetVerticalDistance(this); }
        public double BitmapColumnWidth
        {
            get
            {
                if (this.Sheet == null)
                {
                    return 0;
                }

                return this.BitmapWidth / this.Sheet.Column;
            }
        }
        public double BitmapVerticalHiddenRowDistance { get => this.BitmapHeight / (96 * zoom); }
        public int RenderFromRow { get => (int)Math.Ceiling(this.RenderFromY / BitmapVerticalHiddenRowDistance); }
        public int RenderToRow { get => (int)Math.Floor((this.RenderFromY + this.BitmapHeight) / BitmapVerticalHiddenRowDistance); }
        private RenderStrategyType renderType;
        public RenderStrategyType RenderType
        {
            get => renderType;
            set
            {
                try
                {
                    this.strategy = RenderStrategyFactory.Get(value);
                    this.renderType = value;
                }
                catch (NotSupportedException)
                {
                    MessageBox.Show($"渲染策略{value}不支持");
                }
            }
        }

        public Sheet Sheet { get; set; } = new ConcurrentHashSheet(6, 3, 3);

        public double BitmapWidth { get => this.bitmap.Width; }
        public double BitmapHeight { get => this.bitmap.Height; }
        public double ImageWidth { get => this.Image.ActualWidth; }
        public double ImageHeight { get => this.Image.ActualHeight; }
        public (int, int)? Selector { get; set; }

        public double TriggerLineY { get; set; } = 50;

        public const double minZoom = 0.5;
        public const double maxZoom = 4.0;

        private double zoom = 1.0f;
        public double Zoom
        {
            get => zoom;
            set
            {
                value = Math.Max(value, minZoom);
                value = Math.Min(value, maxZoom);
                this.zoom = value;
            }
        }

        private int? fpsLimit = null;
        public int? FPSLimit
        {
            get => this.fpsLimit;
            set
            {
                if (value == null)
                {
                    this.fpsLimit = null;
                    this.RenderIntervalMilliseconds = 0;
                    return;
                }

                if (value <= 0)
                {
                    throw new ArgumentException("FPSLimit must be a positive integer");
                }

                this.fpsLimit = value;
                this.RenderIntervalMilliseconds = 1000 / this.fpsLimit.Value;
            }
        }
        public double RenderFromY { get; set; } = 0.0;

        public long RenderIntervalMilliseconds { get; private set; } = 0;
        private readonly Stopwatch stopwatch = new Stopwatch();

        #region Concurrent
        private Queue<Note> notesToRender = new Queue<Note>();
        private List<Task> tasks = new List<Task>() { Capacity = 20 };
        #endregion

        public SheetRenderStyle Style { get; set; } = new SheetRenderStyle()
        {
            BackgroundBrush = Brushes.LightGray,
            SeperatorPen = new Pen(Brushes.Cyan, 1),
            BorderPen = new Pen(Brushes.Red, 1),
            TriggerLinePen = new Pen(Brushes.Lime, 2),

            NotePen1_1 = new Pen(Brushes.Purple, 2),
            NotePen1_4 = new Pen(Brushes.Blue, 2),

            Note_1_1Offset = 40,
            Note_1_4Offset = 30,

            BeatTextFormatProvider = (content) => new FormattedText(
                content, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Consolas"), 30, Brushes.Black, 1),

            SelectorBrush = new SolidColorBrush(new Color()
            {
                R = 0,
                G = 255,
                B = 150,
                A = 100
            }),

            SelectorPen = null,
            SelectorProvider = (center) => new Rect(
                    new Point(center.X - 40, center.Y - 10),
                    new Point(center.X + 40, center.Y + 10)),

            NoteBrush = Brushes.White,
            NotePen = new Pen(Brushes.Cyan, 4),
            NoteProvider = (center) => new Rect(
                    new Point(center.X - 40, center.Y - 10),
                    new Point(center.X + 40, center.Y + 10)),
        };
        public SheetRenderer(Image image, int initialWidth, int initialHeight, double dpiX, double dpiY, int? fpsLimit = null)
        {
            this.Image = image;
            this.dpiX = dpiX;
            this.dpiY = dpiY;
            this.FPSLimit = fpsLimit;
            
            this.bitmap = new RenderTargetBitmap(initialWidth, initialHeight, dpiX, dpiY, PixelFormats.Pbgra32);
            this.Image.Source = this.bitmap;
            this.RenderType = RenderStrategyType.R1_4;

            CompositionTarget.Rendering += this.OnRender;
            this.stopwatch.Start();
        }
        private void OnRender(object sender, EventArgs e)
        {
            if (stopwatch.ElapsedMilliseconds < this.RenderIntervalMilliseconds)
            {
                return;
            }


            using (var context = this.drawingVisual.RenderOpen())
            {
                if (this.Sheet != null)
                {
                    this.DrawSheet(context, this.Sheet);
                    this.DrawTriggerLine(context);
                    this.DrawSelector(context);
                    this.DrawNotes(context);
                }
            }

            this.bitmap.Clear();
            this.bitmap.Render(this.drawingVisual);
            stopwatch.Restart();
        }
        private Point NotePositionToBitmapPoint((int, int) position)
        {
            var x = (position.Item2 * this.BitmapColumnWidth) + this.BitmapColumnWidth / 2;
            var y = (position.Item1 * this.BitmapVerticalHiddenRowDistance - this.RenderFromY);
            return new Point(x, this.BitmapHeight - y);
        }
        private void DrawTriggerLine(DrawingContext context)
        {
            context.DrawLine(this.Style.TriggerLinePen,
                new Point(0, this.BitmapHeight - this.TriggerLineY),
                new Point(this.BitmapWidth, this.BitmapHeight - this.TriggerLineY));
        }
        private void DrawSheet(DrawingContext context, Sheet sheet)
        {
            context.DrawRectangle(Style.BackgroundBrush, null, new Rect(0, 0, BitmapWidth, BitmapHeight));
            DrawFrames(context, sheet);
        }
        private void DrawFrames(DrawingContext context, Sheet sheet)
        {
            double interval = this.BitmapWidth / sheet.Column;
            var start = new Point(0, 0);
            var end = new Point(0, this.BitmapHeight);
            context.DrawLine(Style.SeperatorPen, start, end);
            for (int i = 1; i <= sheet.Column; i++)
            {
                start.X += interval;
                end.X += interval;
                context.DrawLine(
                    i == sheet.LeftSize ? Style.BorderPen : Style.SeperatorPen,
                    start, end);
            }
            this.strategy?.Render(this, context);
        }
        private void DrawSelector(DrawingContext context)
        {

            this.Selector = this.strategy.Agent.GetPosition(this);
            if (Selector == null)
            {
                return;
            }

            var rectCenter = new Point(
                this.BitmapColumnWidth / 2 + (this.BitmapColumnWidth) * this.Selector.Value.Item2,
                this.BitmapHeight - (this.BitmapVerticalHiddenRowDistance * this.Selector.Value.Item1 - this.RenderFromY)
            );

            context.DrawRectangle(
                Style.SelectorBrush,
                null,
                Style.SelectorProvider(rectCenter)
                );
        }
        [Obsolete]
        private void DrawNotes(DrawingContext context)
        {
            this.tasks.Clear();
            this.notesToRender.Clear();
            int hiddenRowStart = (int)Math.Ceiling(this.RenderFromY / this.BitmapVerticalHiddenRowDistance);
            double y = this.BitmapHeight - (hiddenRowStart * this.BitmapVerticalHiddenRowDistance - this.RenderFromY);
            var bitmapVerticalHiddenRowDistance = this.BitmapVerticalHiddenRowDistance;
            for (int i = 0; i < this.Sheet.Column; i++)
            {
                int thisTaskColumn = i;
                int thisTaskRow = hiddenRowStart;
                double thisTaskY = y;
                tasks.Add(Task.Run(() =>
                {
                    while(thisTaskY >= 0)
                    {
                        var note = this.Sheet.GetNote(thisTaskRow, thisTaskColumn);
                        if (note != null)
                        {
                            lock(this.notesToRender)
                            {
                                this.notesToRender.Enqueue(note);
                            }
                        }
                        thisTaskY -= bitmapVerticalHiddenRowDistance;
                        thisTaskRow++;
                    }
                }));
            }
            foreach (var task in tasks)
            {
                task.Wait();
            }
            while(notesToRender.Count > 0)
            {
                RenderNote(context, notesToRender.Dequeue());
            }
        }
        private void DrawNotes(DrawingContext context, Func<Queue<Note>> notesProvider)
        {

        }
        private void RenderNote(DrawingContext context, Note note)
        {
            if (note is HitNote)
            {
                context.DrawRectangle(
                    Style.NoteBrush,
                    Style.NotePen,
                    Style.NoteProvider(this.NotePositionToBitmapPoint(
                        (note as HitNote).BasePosition
                        ))
                    );
                return;
            }
            if (note is HoldNote)
            {
                var holdnote = note as HoldNote;
                var rect1 = Style.NoteProvider(this.NotePositionToBitmapPoint(holdnote.Start));
                var rect2 = Style.NoteProvider(this.NotePositionToBitmapPoint(holdnote.End));

                var finalRect = new Rect(rect1.TopLeft, rect2.BottomRight);

                context.DrawRectangle(
                    Style.NoteBrush,
                    Style.NotePen,
                    finalRect);
            }
        }
        ~SheetRenderer()
        {
            try
            {
                CompositionTarget.Rendering -= OnRender;
            }
            catch (Exception)
            {
            }
        }
    }

    public class SheetRenderStyle
    {
        public Brush BackgroundBrush { get; set; }
        public Pen SeperatorPen { get; set; }
        public Pen BorderPen { get; set; }
        public Pen TriggerLinePen { get; set; }
        public Pen NotePen1_1 { get; set; }
        public Pen NotePen1_4 { get; set; }
        public double Note_1_1Offset { get; set; }
        public double Note_1_4Offset { get; set; }
        public Func<string, FormattedText> BeatTextFormatProvider { get; set; }
        public Func<Point, Rect> SelectorProvider { get; set; }
        public Brush SelectorBrush { get; set; }
        public Pen SelectorPen { get; set; }
        public Func<Point, Rect> NoteProvider { get; set; }
        public Brush NoteBrush { get; set; }
        public Pen NotePen { get; set; }
    }

    public abstract class BeatRenderStrategy
    {
        public abstract void Render(SheetRenderer sheetRenderer, DrawingContext context);
        public abstract double GetVerticalDistance(SheetRenderer sheetRenderer);
        public abstract InteractAgent Agent { get; }
        public abstract class InteractAgent
        {
            public abstract (int, int)? GetPosition(SheetRenderer sheetRenderer);
        }
    }

    public static class RenderStrategyFactory
    {
        public static BeatRenderStrategy Get(RenderStrategyType type)
        {
            switch (type)
            {
                case RenderStrategyType.R1_4:
                    return new Strategy_1_4();
                default:
                    throw new NotSupportedException();
            };
        }
    }

    public enum RenderStrategyType
    {
        R1_2,
        R1_3,
        R1_4,
        R1_6,
        R1_8,
        R1_12,
        R1_16,
        R1_24,
        R1_32,
    }

    public class Strategy_1_4 : BeatRenderStrategy
    {
        private InteractAgent agent = new Agent_1_4();
        public override double GetVerticalDistance(SheetRenderer sheetRenderer) => sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 4);

        public override void Render(SheetRenderer sheetRenderer, DrawingContext context)
        {
            double verticalDistance = GetVerticalDistance(sheetRenderer);
            double horizontalDistance = sheetRenderer.BitmapColumnWidth;
            
            int row = (int)Math.Ceiling(sheetRenderer.RenderFromY / verticalDistance);
            
            double y = sheetRenderer.BitmapHeight - (row * verticalDistance - sheetRenderer.RenderFromY);
            double startX = sheetRenderer.BitmapColumnWidth / 2;
            
            double offset = row % 4 == 0 ? sheetRenderer.Style.Note_1_1Offset : sheetRenderer.Style.Note_1_4Offset;

            Point start = new Point(startX - offset, y);
            Point end = new Point(startX + offset, y);

            while(start.Y >= 0)
            {
                if (row % 4 == 0)
                {
                    context.DrawText(sheetRenderer.Style.BeatTextFormatProvider((row / 4).ToString()),
                        new Point(20, start.Y));
                }

                var pen = row % 4 == 0 ? sheetRenderer.Style.NotePen1_1 : sheetRenderer.Style.NotePen1_4;

                for (int i = 0; i < sheetRenderer.Sheet.Column; i++)
                {
                    context.DrawLine(pen, start, end);
                    start.X += horizontalDistance;
                    end.X += horizontalDistance;
                }

                row++;

                offset = row % 4 == 0 ? sheetRenderer.Style.Note_1_1Offset : sheetRenderer.Style.Note_1_4Offset;
                start.Y -= verticalDistance;
                end.Y -= verticalDistance;
                start.X = startX - offset;
                end.X = startX + offset;
            }
        }

        public override InteractAgent Agent => agent;
        private class Agent_1_4 : InteractAgent
        {
            public override (int, int)? GetPosition(SheetRenderer sheetRenderer)
            {
                var pos = Mouse.GetPosition(sheetRenderer.Image);
                pos.X *= sheetRenderer.BitmapWidth / sheetRenderer.ImageWidth;
                pos.Y *= sheetRenderer.BitmapHeight / sheetRenderer.ImageHeight;

                if (pos.X < 0 || pos.Y < 0 || pos.X > sheetRenderer.BitmapWidth || pos.Y > sheetRenderer.BitmapHeight)
                {
                    return null;
                }

                var absolute = new Point(pos.X, sheetRenderer.BitmapHeight - pos.Y + sheetRenderer.RenderFromY);

                var result = (
                    (int)Math.Round(absolute.Y / sheetRenderer.BitmapVerticalRenderDistance) * 24,
                    (int)Math.Floor(absolute.X / sheetRenderer.BitmapColumnWidth)
                    );

                return result;
            }
        }
    }
}
