using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WPFKB_Maker.TFS.KBBeat;
using WPFKB_Maker.TFS.Rendering;

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
        public BeatRenderStrategy.InteractAgent Agent { get => strategy.Agent; }
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
        public double BitmapHeightPerBeat { get => this.BitmapHeight / (this.zoom * 4); }
        public double TriggerAbsoluteY 
        { 
            get => TriggerLineY + this.RenderFromY; 
            set => this.RenderFromY = value - TriggerLineY;
        }
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
        public Project Project { get; set; }
        public Sheet Sheet
        {
            get
            {
                if (this.Project == null)
                {
                    return HashSheet.Default;
                }
                return this.Project.Sheet;
            }
        }
        public double BitmapWidth { get => this.bitmap.Width; }
        public double BitmapHeight { get => this.bitmap.Height; }
        public double ImageWidth { get => this.Image.ActualWidth; }
        public double ImageHeight { get => this.Image.ActualHeight; }
        public Func<ICollection<Note>> SelectedNotesProvider { get; set; } = () => Array.Empty<Note>();
        public (int, int)? Selector { get; set; }
        public double TriggerLineY { get; set; } = 50;
        public int TriggerLineRow { get => (int)Math.Floor((this.RenderFromY + this.TriggerLineY) / this.BitmapVerticalHiddenRowDistance); }
        public const double minZoom = 0.5;
        public const double maxZoom = 4.0;

        private SemaphoreSlim renderMutex = new SemaphoreSlim(1, 1);

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
        public List<Note> NotesToRender { get; set; } = new List<Note>();
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

            SelectedNoteBrush = Brushes.White,
            SelectedNotePen = new Pen(Brushes.Red, 4),
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
        private async void OnRender(object sender, EventArgs e)
        {
            await renderMutex.WaitAsync();
            this.NotesToRender.Clear();
            if (stopwatch.ElapsedMilliseconds < this.RenderIntervalMilliseconds)
            {
                return;
            }
            var watch = new Stopwatch();
            watch.Start();

            var renderFromRow = this.RenderFromRow;
            var renderToRow = this.RenderToRow;
            Task task = Task.Run(() =>
            {
                var query = from note in this.Sheet.Values.AsParallel()
                            where
                                ShouldRenderNote(note, renderFromRow, renderToRow)
                            select note;
                lock (this.Sheet)
                {
                    foreach (var note in query)
                    {
                        this.NotesToRender.Add(note);
                    }
                }
            });

            using (var context = this.drawingVisual.RenderOpen())
            {
                if (this.Sheet != null)
                {
                    this.DrawSheet(context, this.Sheet);
                    this.DrawTriggerLine(context);
                    this.DrawSelector(context);
                    await task.ConfigureAwait(true);
                    this.DrawNotes(context);
                }
            }

            this.bitmap.Clear();
            this.bitmap.Render(this.drawingVisual);
            
            watch.Stop();
            stopwatch.Restart();
            renderMutex.Release();
        }
        private bool ShouldRenderNote(Note note, int start, int end)
        {
            if (note is HitNote)
            {
                return note.BasePosition.Item1 >= start && note.BasePosition.Item1 <= end;
            }

            var hold = note as HoldNote;
            return
                (hold.BasePosition.Item1 >= start && hold.BasePosition.Item1 <= end) ||
                (hold.End.Item1 >= start && hold.End.Item1 <= end);
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

            this.Selector = this.strategy.Agent.GetMousePosition(this);
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
        private void DrawNotes(DrawingContext context)
        {
            try
            {
                foreach (var note in NotesToRender) 
                {
                    RenderNote(context, note, this.SelectedNotesProvider().Contains(note));
                }
            }
            catch (Exception e)
            {
                Debug.console.Write($"rendering error: {e}");
            }
        }
        private void RenderNote(DrawingContext context, Note note, bool isSelected)
        {
            var pen = isSelected ? Style.SelectedNotePen : Style.NotePen;
            var brush = isSelected ? Style.SelectedNoteBrush : Style.NoteBrush;

            if (note is HitNote)
            {
                context.DrawRectangle(
                    brush,
                    pen,
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

                var finalRect = new Rect(rect1.BottomLeft, rect2.TopRight);

                context.DrawRectangle(
                    brush,
                    pen,
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
        public Brush SelectedNoteBrush { get; set; }
        public Pen SelectedNotePen { get; set; }
    }

    public abstract class BeatRenderStrategy
    {
        public abstract void Render(SheetRenderer sheetRenderer, DrawingContext context);
        public abstract double GetVerticalDistance(SheetRenderer sheetRenderer);
        public abstract InteractAgent Agent { get; }
        public abstract class InteractAgent
        {
            public (int, int)? GetMousePosition(SheetRenderer sheetRenderer)
            {
                var pos = Mouse.GetPosition(sheetRenderer.Image);
                pos.X *= sheetRenderer.BitmapWidth / sheetRenderer.ImageWidth;
                pos.Y *= sheetRenderer.BitmapHeight / sheetRenderer.ImageHeight;

                return GetPositionBitmap(sheetRenderer, pos);
            }
            public abstract (int, int)? GetPositionBitmap(SheetRenderer sheetRenderer, Point bitmapPosition);
            public (int, int)? GetPositionScreen(SheetRenderer sheetRenderer, Point screenPosition)
            {
                var bitmapPosition = screenPosition;
                bitmapPosition.X *= sheetRenderer.BitmapWidth / sheetRenderer.ImageWidth;
                bitmapPosition.Y *= sheetRenderer.BitmapHeight / sheetRenderer.ImageHeight;

                if (bitmapPosition.X < 0 ||
                    bitmapPosition.Y < 0 ||
                    bitmapPosition.X > sheetRenderer.BitmapWidth ||
                    bitmapPosition.Y > sheetRenderer.BitmapHeight)
                {
                    return null;
                }

                return GetPositionBitmap(sheetRenderer, bitmapPosition);
            }
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

            while (start.Y >= 0)
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
            public override (int, int)? GetPositionBitmap(SheetRenderer sheetRenderer, Point bitmapPosition)
            {
                var absolute = new Point(bitmapPosition.X, sheetRenderer.BitmapHeight - bitmapPosition.Y + sheetRenderer.RenderFromY);

                var result = (
                    (int)Math.Round(absolute.Y / sheetRenderer.BitmapVerticalRenderDistance) * 24,
                    (int)Math.Floor(absolute.X / sheetRenderer.BitmapColumnWidth)
                    );
                return result;
            }
        }
    }
}
