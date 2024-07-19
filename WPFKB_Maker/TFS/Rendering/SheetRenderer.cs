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

namespace WPFKB_Maker.TFS
{
    public class SheetRenderer
    {
        public Image Image { get; }

        private RenderTargetBitmap bitmap;
        private readonly DrawingVisual drawingVisual = new DrawingVisual();
        
        private double dpiX;
        private double dpiY;

        private GridRenderStrategy strategy;
        public GridRenderStrategy.InteractAgent Agent { get => strategy.Agent; }
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
        public double BitmapHeightPerBeat { get => this.BitmapHeight / (this.zoom); }
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
        public int TriggerLineRow { 
            get => (int)Math.Floor((this.RenderFromY + this.TriggerLineY) / this.BitmapVerticalHiddenRowDistance);
            set
            {
                this.TriggerAbsoluteY = value * this.BitmapVerticalHiddenRowDistance;
            }
        }
        public const double minZoom = 0.5;
        public const double maxZoom = 12.0;

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
        public double TriggerLineCurrentTimeSecond
        {
            get
            {
                if (Project.Current == null)
                {
                    return 0.0;
                }

                double beat = this.TriggerAbsoluteY / (this.BitmapVerticalHiddenRowDistance * 96);

                return beat * (60 / Project.Current.Meta.Bpm);
            }

            set
            {
                if (Project.Current == null)
                {
                    return;
                }

                this.TriggerAbsoluteY = (value * BitmapVerticalHiddenRowDistance * 96 * Project.Current.Meta.Bpm) / 60;
            }
        }
        private readonly Stopwatch stopwatch = new Stopwatch();
        public List<Note> NotesToRender { get; set; } = new List<Note>();
        public SheetRenderStyle Style { get; set; } = new SheetRenderStyle()
        {
            BackgroundBrush = new SolidColorBrush(Color.FromRgb(5, 6, 2)),
            SeperatorPen = new Pen(new SolidColorBrush(Color.FromRgb(50, 51, 59)), 1),
            BorderPen = new Pen(new SolidColorBrush(Color.FromRgb(80, 81, 89)), 2),
            TriggerLinePen = new Pen(new SolidColorBrush(Color.FromRgb(255, 46, 65)), 2),

            NotePen1_1 = new Pen(new SolidColorBrush(Color.FromRgb(90, 91, 99)), 2),
            NotePen1_2 = new Pen[]
            {
                new Pen(new SolidColorBrush(Color.FromRgb(50, 80, 50)), 2)
            },
            NotePen1_3 = new Pen[]
            {
                new Pen(new SolidColorBrush(Color.FromRgb(30, 60, 30)), 2)
            },
            NotePen1_4 = new Pen[]
            {
                new Pen(new SolidColorBrush(Color.FromRgb(65, 38, 69)), 2),
                new Pen(new SolidColorBrush(Color.FromRgb(21, 52, 58)), 2)
            },
            NotePen1_6 = new Pen[]
            {
                new Pen(new SolidColorBrush(Color.FromRgb(20, 80, 20)), 2),
                new Pen(new SolidColorBrush(Color.FromRgb(20, 60, 80)), 2)
            },
            NotePen1_8 = new Pen[]
            {
                new Pen(new SolidColorBrush(Color.FromRgb(85, 58, 89)), 2),
                new Pen(new SolidColorBrush(Color.FromRgb(41, 72, 78)), 2),
                new Pen(new SolidColorBrush(Color.FromRgb(30, 30, 30)), 2)
            },
            NotePen1_12 = new Pen[]
            {
                new Pen(new SolidColorBrush(Color.FromRgb(85, 58, 89)), 2),
                new Pen(new SolidColorBrush(Color.FromRgb(41, 72, 78)), 2),
                new Pen(new SolidColorBrush(Color.FromRgb(30, 30, 30)), 2)
            },
            NotePen1_16 = new Pen[]
            {
                new Pen(new SolidColorBrush(Color.FromRgb(85, 58, 89)), 2),
                new Pen(new SolidColorBrush(Color.FromRgb(41, 72, 78)), 2),
                new Pen(new SolidColorBrush(Color.FromRgb(30, 30, 30)), 2)
            },
            NotePen1_24 = new Pen[]
            {
                new Pen(new SolidColorBrush(Color.FromRgb(85, 58, 89)), 2),
                new Pen(new SolidColorBrush(Color.FromRgb(41, 72, 78)), 2),
                new Pen(new SolidColorBrush(Color.FromRgb(30, 30, 30)), 2)
            },
            NotePen1_32 = new Pen[]
            {
                new Pen(new SolidColorBrush(Color.FromRgb(85, 58, 89)), 2),
                new Pen(new SolidColorBrush(Color.FromRgb(41, 72, 78)), 2),
                new Pen(new SolidColorBrush(Color.FromRgb(30, 30, 30)), 2)
            },
            FullNotePercentage = 1,
            NotFullNotePercentage = 0.8,

            BeatTextFormatProvider = (content) => new FormattedText(
                content, CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight,
                new Typeface("Consolas"), 30, Brushes.White, 1),

            SelectorBrush = new SolidColorBrush(new Color()
            {
                R = 179,
                G = 234,
                B = 255,
                A = 90
            }),

            SelectorPen = null,
            SelectorProvider = (center) => new Rect(
                    new Point(center.X - 40, center.Y - 10),
                    new Point(center.X + 40, center.Y + 10)),

            NoteBrush = Brushes.White,
            NotePen = new Pen(new SolidColorBrush(Color.FromRgb(208, 221, 234)), 4),
            NoteProvider = (center) => new Rect(
                    new Point(center.X - 40, center.Y - 10),
                    new Point(center.X + 40, center.Y + 10)),

            SelectedNoteBrush = new SolidColorBrush(Color.FromRgb(255, 255, 102)),
            SelectedNotePen = new Pen(new SolidColorBrush(Color.FromRgb(229, 232, 107)), 4),
        };
        private readonly Stopwatch performanceWatcher = new Stopwatch();
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
            performanceWatcher.Restart();

            await renderMutex.WaitAsync();
            this.NotesToRender.Clear();
            if (stopwatch.ElapsedMilliseconds < this.RenderIntervalMilliseconds)
            {
                return;
            }

            var renderFromRow = this.RenderFromRow;
            var renderToRow = this.RenderToRow;
            Task task = Task.Run(() =>
            {
                var query = from note in this.Sheet.Values.AsParallel()
                            where
                                ShouldRenderNote(note, renderFromRow, renderToRow)
                            orderby (note.BasePosition.Item1 + note.BasePosition.Item2)
                            select note;
                lock (this.NotesToRender)
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
                    await task;
                    this.DrawNotes(context);
                    this.DrawSelector(context);
                }
            }

            this.bitmap.Clear();
            this.bitmap.Render(this.drawingVisual);
            
            stopwatch.Restart();
            renderMutex.Release();

            performanceWatcher.Stop();
            Debug.console.Write($"Rendering elapsed {performanceWatcher.ElapsedMilliseconds} ms");
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
                var rect = Style.NoteProvider(this.NotePositionToBitmapPoint((note as HitNote).BasePosition));
                context.DrawRectangle(brush, pen, rect);
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
        public Pen[] NotePen1_2 { get; set; }
        public Pen[] NotePen1_3 { get; set; }
        public Pen[] NotePen1_4 { get; set; }
        public Pen[] NotePen1_6 { get; set; }
        public Pen[] NotePen1_8 { get; set; }
        public Pen[] NotePen1_12 { get; set; }
        public Pen[] NotePen1_16 { get; set; }
        public Pen[] NotePen1_24 { get; set; }
        public Pen[] NotePen1_32 { get; set; }

        public double FullNotePercentage { get; set; }
        public double NotFullNotePercentage { get; set; }
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


    public static class RenderStrategyFactory
    {
        public static GridRenderStrategy Get(RenderStrategyType type)
        {
            switch (type)
            {
                case RenderStrategyType.R1_2:
                    return new Strategy_1_2();
                case RenderStrategyType.R1_3:
                    return new Strategy_1_3();
                case RenderStrategyType.R1_4:
                    return new Strategy_1_4();
                case RenderStrategyType.R1_6:
                    return new Strategy_1_6();
                case RenderStrategyType.R1_8:
                    return new Strategy_1_8();
                case RenderStrategyType.R1_12:
                    return new Strategy_1_12();
                case RenderStrategyType.R1_16:
                    return new Strategy_1_16();
                case RenderStrategyType.R1_24:
                    return new Strategy_1_24();
                case RenderStrategyType.R1_32:
                    return new Strategy_1_32();
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
    public abstract class GridRenderStrategy
    {
        public void Render(SheetRenderer sheetRenderer, DrawingContext context)
        {
            double verticalDistance = GetVerticalDistance(sheetRenderer);
            double horizontalDistance = sheetRenderer.BitmapColumnWidth;

            int row = (int)Math.Ceiling(sheetRenderer.RenderFromY / verticalDistance);

            double y = sheetRenderer.BitmapHeight - (row * verticalDistance - sheetRenderer.RenderFromY);

            double halfColumn = sheetRenderer.BitmapColumnWidth / 2;
            double startX = halfColumn;

            double offset = this.PickOffset(row, halfColumn, sheetRenderer.Style);

            Point start = new Point(startX - offset, y);
            Point end = new Point(startX + offset, y);

            while (start.Y >= 0)
            {
                if (ShouldRenderText(row))
                {
                    context.DrawText(sheetRenderer.Style.BeatTextFormatProvider((row / 4).ToString()),
                        new Point(20, start.Y));
                }

                var pen = PickPen(row, sheetRenderer.Style);

                for (int i = 0; i < sheetRenderer.Sheet.Column; i++)
                {
                    context.DrawLine(pen, start, end);
                    start.X += horizontalDistance;
                    end.X += horizontalDistance;
                }

                row++;

                offset = this.PickOffset(row, halfColumn, sheetRenderer.Style);

                start.Y -= verticalDistance;
                end.Y -= verticalDistance;
                start.X = startX - offset;
                end.X = startX + offset;
            }
        }
        public abstract double GetVerticalDistance(SheetRenderer sheetRenderer);
        protected abstract double PickOffset(int row, double halfColumn, SheetRenderStyle style);
        protected abstract Pen PickPen(int row, SheetRenderStyle style);
        protected abstract bool ShouldRenderText(int row);

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

    public class Strategy_1_4 : GridRenderStrategy
    {
        private InteractAgent agent = new Agent_1_4();
        public override double GetVerticalDistance(SheetRenderer sheetRenderer) => sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 4);

        protected override bool ShouldRenderText(int row) => row % 4 == 0;

        protected override double PickOffset(int row, double halfColumn, SheetRenderStyle style)
        {
            if (row % 4 == 0)
            {
                return halfColumn * style.FullNotePercentage;
            }
            return halfColumn * style.NotFullNotePercentage;
        }
        protected override Pen PickPen(int row, SheetRenderStyle style)
        {
            switch (Math.Abs(row % 4))
            {
                case 0:
                    return style.NotePen1_1;
                case 2:
                    return style.NotePen1_4[1];
                default:
                    return style.NotePen1_4[0];
            }
        }

        public override InteractAgent Agent => agent;
        private class Agent_1_4 : InteractAgent
        {
            public override (int, int)? GetPositionBitmap(SheetRenderer sheetRenderer, Point bitmapPosition)
            {
                var absolute = new Point(
                    bitmapPosition.X, 
                    sheetRenderer.BitmapHeight - bitmapPosition.Y + sheetRenderer.RenderFromY);

                var result = (
                    (int)Math.Round(absolute.Y / sheetRenderer.BitmapVerticalRenderDistance) * 24,
                    (int)Math.Floor(absolute.X / sheetRenderer.BitmapColumnWidth)
                    );
                return result;
            }
        }
    }

    public class Strategy_1_3 : GridRenderStrategy
    {
        private InteractAgent agent = new Agent_1_3();
        public override double GetVerticalDistance(SheetRenderer sheetRenderer) => sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 3);
        protected override bool ShouldRenderText(int row) => row % 3 == 0;
        protected override double PickOffset(int row, double halfColumn, SheetRenderStyle style)
        {
            if (row % 3 == 0)
            {
                return halfColumn * style.FullNotePercentage;
            }
            return halfColumn * style.NotFullNotePercentage;
        }
        protected override Pen PickPen(int row, SheetRenderStyle style)
        {
            if (row % 3 == 0)
            {
                return style.NotePen1_1;
            }

            return style.NotePen1_3[0];
        }
        public override InteractAgent Agent => agent;
        private class Agent_1_3 : InteractAgent
        {
            public override (int, int)? GetPositionBitmap(SheetRenderer sheetRenderer, Point bitmapPosition)
            {
                var absolute = new Point(
                    bitmapPosition.X,
                    sheetRenderer.BitmapHeight - bitmapPosition.Y + sheetRenderer.RenderFromY);

                var result = (
                    (int)Math.Round(absolute.Y / sheetRenderer.BitmapVerticalRenderDistance) * 32,
                    (int)Math.Floor(absolute.X / sheetRenderer.BitmapColumnWidth)
                    );
                return result;
            }
        }
    }

    public class Strategy_1_2 : GridRenderStrategy
    {
        private InteractAgent agent = new Agent_1_2();
        public override double GetVerticalDistance(SheetRenderer sheetRenderer) => sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 2);
        protected override bool ShouldRenderText(int row) => row % 2 == 0;
        protected override double PickOffset(int row, double halfColumn, SheetRenderStyle style)
        {
            if (row % 2 == 0)
            {
                return halfColumn * style.FullNotePercentage;
            }
            return halfColumn * style.NotFullNotePercentage;
        }
        protected override Pen PickPen(int row, SheetRenderStyle style)
        {
            if (row % 2 == 0)
            {
                return style.NotePen1_1;
            }

            return style.NotePen1_2[0];
        }
        public override InteractAgent Agent => agent;
        private class Agent_1_2 : InteractAgent
        {
            public override (int, int)? GetPositionBitmap(SheetRenderer sheetRenderer, Point bitmapPosition)
            {
                var absolute = new Point(
                    bitmapPosition.X,
                    sheetRenderer.BitmapHeight - bitmapPosition.Y + sheetRenderer.RenderFromY);

                var result = (
                    (int)Math.Round(absolute.Y / sheetRenderer.BitmapVerticalRenderDistance) * 48,
                    (int)Math.Floor(absolute.X / sheetRenderer.BitmapColumnWidth)
                    );
                return result;
            }
        }
    }

    public class Strategy_1_6 : GridRenderStrategy
    {
        private InteractAgent agent = new Agent_1_6();
        public override double GetVerticalDistance(SheetRenderer sheetRenderer) => sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 6);
        protected override bool ShouldRenderText(int row) => row % 6 == 0;
        protected override double PickOffset(int row, double halfColumn, SheetRenderStyle style)
        {
            if (row % 6 == 0)
            {
                return halfColumn * style.FullNotePercentage;
            }
            return halfColumn * style.NotFullNotePercentage;
        }
        protected override Pen PickPen(int row, SheetRenderStyle style)
        {
            switch (Math.Abs(row % 6))
            {
                case 0:
                    return style.NotePen1_1;
                case 3:
                    return style.NotePen1_6[1];
                default:
                    return style.NotePen1_6[0];
            }
        }
        public override InteractAgent Agent => agent;
        private class Agent_1_6 : InteractAgent
        {
            public override (int, int)? GetPositionBitmap(SheetRenderer sheetRenderer, Point bitmapPosition)
            {
                var absolute = new Point(
                    bitmapPosition.X,
                    sheetRenderer.BitmapHeight - bitmapPosition.Y + sheetRenderer.RenderFromY);

                var result = (
                    (int)Math.Round(absolute.Y / sheetRenderer.BitmapVerticalRenderDistance) * 16,
                    (int)Math.Floor(absolute.X / sheetRenderer.BitmapColumnWidth)
                    );
                return result;
            }
        }
    }

    public class Strategy_1_8 : GridRenderStrategy
    {
        private InteractAgent agent = new Agent_1_8();
        public override double GetVerticalDistance(SheetRenderer sheetRenderer) => sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 8);
        protected override bool ShouldRenderText(int row) => row % 8 == 0;
        protected override double PickOffset(int row, double halfColumn, SheetRenderStyle style)
        {
            if (row % 8 == 0)
            {
                return halfColumn * style.FullNotePercentage;
            }
            return halfColumn * style.NotFullNotePercentage;
        }
        protected override Pen PickPen(int row, SheetRenderStyle style)
        {
            switch (Math.Abs(row % 8))
            {
                case 2:
                case 6:
                    return style.NotePen1_8[0];
                case 4:
                    return style.NotePen1_8[1];
                case 0:
                    return style.NotePen1_1;
                default:
                    return style.NotePen1_8[2];
            }
        }
        public override InteractAgent Agent => agent;
        private class Agent_1_8 : InteractAgent
        {
            public override (int, int)? GetPositionBitmap(SheetRenderer sheetRenderer, Point bitmapPosition)
            {
                var absolute = new Point(
                    bitmapPosition.X,
                    sheetRenderer.BitmapHeight - bitmapPosition.Y + sheetRenderer.RenderFromY);

                var result = (
                    (int)Math.Round(absolute.Y / sheetRenderer.BitmapVerticalRenderDistance) * 12,
                    (int)Math.Floor(absolute.X / sheetRenderer.BitmapColumnWidth)
                    );
                return result;
            }
        }
    }

    public class Strategy_1_12 : GridRenderStrategy
    {
        private InteractAgent agent = new Agent_1_12();
        public override double GetVerticalDistance(SheetRenderer sheetRenderer) => sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 12);
        protected override bool ShouldRenderText(int row) => row % 12 == 0;
        protected override double PickOffset(int row, double halfColumn, SheetRenderStyle style)
        {
            if (row % 12 == 0)
            {
                return halfColumn * style.FullNotePercentage;
            }
            return halfColumn * style.NotFullNotePercentage;
        }
        protected override Pen PickPen(int row, SheetRenderStyle style)
        {
            switch (Math.Abs(row % 12))
            {
                case 3:
                case 9:
                    return style.NotePen1_12[0];
                case 6:
                    return style.NotePen1_12[1];
                case 0:
                    return style.NotePen1_1;
                default:
                    return style.NotePen1_12[2];
            }
        }
        public override InteractAgent Agent => agent;
        private class Agent_1_12 : InteractAgent
        {
            public override (int, int)? GetPositionBitmap(SheetRenderer sheetRenderer, Point bitmapPosition)
            {
                var absolute = new Point(
                    bitmapPosition.X,
                    sheetRenderer.BitmapHeight - bitmapPosition.Y + sheetRenderer.RenderFromY);

                var result = (
                    (int)Math.Round(absolute.Y / sheetRenderer.BitmapVerticalRenderDistance) * 8,
                    (int)Math.Floor(absolute.X / sheetRenderer.BitmapColumnWidth)
                    );
                return result;
            }
        }
    }

    public class Strategy_1_16 : GridRenderStrategy
    {
        private InteractAgent agent = new Agent_1_16();
        public override double GetVerticalDistance(SheetRenderer sheetRenderer) => sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 16);
        protected override bool ShouldRenderText(int row) => row % 16 == 0;
        protected override double PickOffset(int row, double halfColumn, SheetRenderStyle style)
        {
            if (row % 16 == 0)
            {
                return halfColumn * style.FullNotePercentage;
            }
            return halfColumn * style.NotFullNotePercentage;
        }
        protected override Pen PickPen(int row, SheetRenderStyle style)
        {
            switch (Math.Abs(row % 16))
            {
                case 4:
                case 12:
                    return style.NotePen1_16[0];
                case 8:
                    return style.NotePen1_16[1];
                case 0:
                    return style.NotePen1_1;
                default:
                    return style.NotePen1_16[2];
            }
        }
        public override InteractAgent Agent => agent;
        private class Agent_1_16 : InteractAgent
        {
            public override (int, int)? GetPositionBitmap(SheetRenderer sheetRenderer, Point bitmapPosition)
            {
                var absolute = new Point(
                    bitmapPosition.X,
                    sheetRenderer.BitmapHeight - bitmapPosition.Y + sheetRenderer.RenderFromY);

                var result = (
                    (int)Math.Round(absolute.Y / sheetRenderer.BitmapVerticalRenderDistance) * 6,
                    (int)Math.Floor(absolute.X / sheetRenderer.BitmapColumnWidth)
                    );
                return result;
            }
        }
    }

    public class Strategy_1_24 : GridRenderStrategy
    {
        private InteractAgent agent = new Agent_1_24();
        public override double GetVerticalDistance(SheetRenderer sheetRenderer) => sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 24);
        protected override bool ShouldRenderText(int row) => row % 24 == 0;
        protected override double PickOffset(int row, double halfColumn, SheetRenderStyle style)
        {
            if (row % 24 == 0)
            {
                return halfColumn * style.FullNotePercentage;
            }
            return halfColumn * style.NotFullNotePercentage;
        }
        protected override Pen PickPen(int row, SheetRenderStyle style)
        {
            switch (Math.Abs(row % 24))
            {
                case 6:
                case 18:
                    return style.NotePen1_24[0];
                case 12:
                    return style.NotePen1_24[1];
                case 0:
                    return style.NotePen1_1;
                default:
                    return style.NotePen1_24[2];
            }
        }
        public override InteractAgent Agent => agent;
        private class Agent_1_24 : InteractAgent
        {
            public override (int, int)? GetPositionBitmap(SheetRenderer sheetRenderer, Point bitmapPosition)
            {
                var absolute = new Point(
                    bitmapPosition.X,
                    sheetRenderer.BitmapHeight - bitmapPosition.Y + sheetRenderer.RenderFromY);

                var result = (
                    (int)Math.Round(absolute.Y / sheetRenderer.BitmapVerticalRenderDistance) * 4,
                    (int)Math.Floor(absolute.X / sheetRenderer.BitmapColumnWidth)
                    );
                return result;
            }
        }
    }

    public class Strategy_1_32 : GridRenderStrategy
    {
        private InteractAgent agent = new Agent_1_32();
        public override double GetVerticalDistance(SheetRenderer sheetRenderer) => sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 32);
        protected override bool ShouldRenderText(int row) => row % 32 == 0;
        protected override double PickOffset(int row, double halfColumn, SheetRenderStyle style)
        {
            if (row % 32 == 0)
            {
                return halfColumn * style.FullNotePercentage;
            }
            return halfColumn * style.NotFullNotePercentage;
        }
        protected override Pen PickPen(int row, SheetRenderStyle style)
        {
            switch (Math.Abs(row % 32))
            {
                case 8:
                case 24:
                    return style.NotePen1_32[0];
                case 16:
                    return style.NotePen1_32[1];
                case 0:
                    return style.NotePen1_1;
                default:
                    return style.NotePen1_32[2];
            }
        }
        public override InteractAgent Agent => agent;
        private class Agent_1_32 : InteractAgent
        {
            public override (int, int)? GetPositionBitmap(SheetRenderer sheetRenderer, Point bitmapPosition)
            {
                var absolute = new Point(
                    bitmapPosition.X,
                    sheetRenderer.BitmapHeight - bitmapPosition.Y + sheetRenderer.RenderFromY);

                var result = (
                    (int)Math.Round(absolute.Y / sheetRenderer.BitmapVerticalRenderDistance) * 3,
                    (int)Math.Floor(absolute.X / sheetRenderer.BitmapColumnWidth)
                    );
                return result;
            }
        }
    }
    public static class RectExtension
    {
        public static Rect Shrink(this Rect rect, double xShrinkRate, double yShrinkRate)
        {
            double xoff = rect.Width / 2 * xShrinkRate;
            double yoff = rect.Height / 2 * yShrinkRate;

            double cx = rect.Top + rect.Bottom / 2;
            double cy = rect.Left + rect.Right / 2;

            return new Rect(
                new Point(cx - xoff, cy - yoff),
                new Point(cx + xoff, cy + yoff)
                );
        }
    }
}
