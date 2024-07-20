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
using GDI = System.Drawing;
using Pen = System.Drawing.Pen;
using WPFKB_Maker.TFS.KBBeat;
using System.Windows.Threading;
using WPFKB_Maker.TFS.Rendering;
using MethodTimer;

namespace WPFKB_Maker.TFS
{
    public class SheetRenderer
    {
        public Image Image { get; }

        private readonly WriteableBitmap bitmap;
        private IntPtr bitmapPtr;
        private GDI.Bitmap gdiBitmap;
        private GDI.Graphics graphics;
        
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
        public double BitmapWidth { get => this.gdiBitmap.Width; }
        public double BitmapHeight { get => this.gdiBitmap.Height; }
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
        public SheetRenderStyle Style { get; set; } = SheetRenderStyles.Default;
        public SheetRenderer(Image image, int initialWidth, int initialHeight, double dpiX, double dpiY, int fps = 60)
        {
            if (fps <= 0)
            {
                throw new ArgumentException("fps must be positive");
            }

            this.Image = image;
            this.dpiX = dpiX;
            this.dpiY = dpiY;

            this.bitmap = new WriteableBitmap(initialWidth, initialHeight, dpiX, dpiY, PixelFormats.Bgra32, null);
            this.Image.Source = this.bitmap;

            this.gdiBitmap = new GDI.Bitmap(initialWidth, initialHeight, GDI.Imaging.PixelFormat.Format32bppArgb);
            this.graphics = GDI.Graphics.FromImage(this.gdiBitmap);

            GDI.Imaging.BitmapData bitmapData = this.gdiBitmap.LockBits(
                new GDI.Rectangle(0, 0, gdiBitmap.Width, gdiBitmap.Height),
                GDI.Imaging.ImageLockMode.ReadWrite,
                this.gdiBitmap.PixelFormat);
            this.bitmapPtr = bitmapData.Scan0;
            gdiBitmap.UnlockBits(bitmapData);

            this.RenderType = RenderStrategyType.R1_4;

            CompositionTarget.Rendering += OnRender;
        }

        private async void OnRender(object sender, EventArgs e)
        {
            graphics.Clear(Style.BackgroundColor);

            this.NotesToRender.Clear();
            if (stopwatch.ElapsedMilliseconds < this.RenderIntervalMilliseconds)
            {
                return;
            }

            var renderFromRow = this.RenderFromRow;
            var renderToRow = this.RenderToRow;

            if (this.Sheet != null)
            {
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
                this.DrawSheet(graphics, this.Sheet);
                this.DrawTriggerLine(graphics);
                this.DrawSelector(graphics);
                await task;
                this.DrawNotes(graphics);
            }
            this.bitmap.Lock();

            this.bitmap.WritePixels(
                new Int32Rect(0, 0, this.bitmap.PixelWidth, this.bitmap.PixelHeight),
                bitmapPtr,
                gdiBitmap.Width * gdiBitmap.Height * 4,
                bitmap.BackBufferStride);
            this.bitmap.Unlock();
            
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
        private GDI.PointF NotePositionToBitmapPoint((int, int) position)
        {
            var x = (position.Item2 * this.BitmapColumnWidth) + this.BitmapColumnWidth / 2;
            var y = (position.Item1 * this.BitmapVerticalHiddenRowDistance - this.RenderFromY);
            return new GDI.PointF(
                (float)x, 
                (float)(this.BitmapHeight - y));
        }
        private void DrawTriggerLine(GDI.Graphics graphics)
        {
            graphics.DrawLine(this.Style.TriggerLinePen,
                new GDI.PointF(0, (float)(this.BitmapHeight - this.TriggerLineY)),
                new GDI.PointF(
                    (float)this.BitmapWidth, 
                    (float)(this.BitmapHeight - this.TriggerLineY)));
        }
        private void DrawSheet(GDI.Graphics graphics, Sheet sheet)
        {
            DrawFrames(graphics, sheet);
        }
        private void DrawFrames(GDI.Graphics graphics, Sheet sheet)
        {
            float interval = (float)this.BitmapWidth / sheet.Column;
            var start = new GDI.PointF(0, 0);
            var end = new GDI.PointF(0, (float)this.BitmapHeight);
            graphics.DrawLine(Style.SeperatorPen, start, end);
            for (int i = 1; i <= sheet.Column; i++)
            {
                start.X += interval;
                end.X += interval;
                graphics.DrawLine(
                    i == sheet.LeftSize ? Style.BorderPen : Style.SeperatorPen,
                    start, end);
            }
            this.strategy?.Render(this, graphics);
        }
        private void DrawSelector(GDI.Graphics graphics)
        {
            this.Selector = this.strategy.Agent.GetMousePosition(this);
            if (Selector == null)
            {
                return;
            }

            var rectCenter = new GDI.PointF(
                (float)(this.BitmapColumnWidth / 2 + (this.BitmapColumnWidth) * this.Selector.Value.Item2),
                (float)(this.BitmapHeight - (this.BitmapVerticalHiddenRowDistance * this.Selector.Value.Item1 - this.RenderFromY))
            );

            var rectangle = Style.SelectorProvider(rectCenter);
            graphics.FillRectangle(
                Style.SelectorBrush,
                rectangle);
        }
        private void DrawNotes(GDI.Graphics graphics)
        {
            try
            {
                foreach (var note in NotesToRender) 
                {
                    RenderNote(graphics, note, this.SelectedNotesProvider().Contains(note));
                }
            }
            catch (Exception e)
            {
                Debug.console.Write($"rendering error: {e}");
            }
        }
        private void RenderNote(GDI.Graphics graphics, Note note, bool isSelected)
        {
            var pen = isSelected ? Style.SelectedNotePen : Style.NotePen;
            var brush = isSelected ? Style.SelectedNoteBrush : Style.NoteBrush;

            if (note is HitNote)
            {
                var rect = Style.NoteProvider(this.NotePositionToBitmapPoint((note as HitNote).BasePosition));
                graphics.FillRectangle(brush, rect);
                graphics.DrawRectangle(pen, 
                    rect.X, rect.Y,
                    rect.Width,
                    rect.Height);
                return;
            }
            if (note is HoldNote)
            {
                var holdnote = note as HoldNote;
                var rect1 = Style.NoteProvider(this.NotePositionToBitmapPoint(holdnote.Start));
                var rect2 = Style.NoteProvider(this.NotePositionToBitmapPoint(holdnote.End));

                graphics.FillRectangle(
                    brush,
                    rect2.X, rect2.Y,
                    rect2.Width,
                    rect2.Top - rect1.Bottom);

                graphics.DrawRectangle(
                    pen,
                    rect2.X, rect2.Y,
                    rect2.Width,
                    rect2.Top - rect1.Bottom);
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
        public void Render(SheetRenderer sheetRenderer, GDI.Graphics graphics)
        {
            float verticalDistance = GetVerticalDistance(sheetRenderer);
            float horizontalDistance = (float)sheetRenderer.BitmapColumnWidth;

            int row = (int)Math.Ceiling(sheetRenderer.RenderFromY / verticalDistance);

            float y = (float)(sheetRenderer.BitmapHeight - (row * verticalDistance - sheetRenderer.RenderFromY));

            float halfColumn = (float)(sheetRenderer.BitmapColumnWidth / 2);
            float startX = halfColumn;

            float offset = this.PickOffset(row, halfColumn, sheetRenderer.Style);

            GDI.PointF start = new GDI.PointF(startX - offset, y);
            GDI.PointF end = new GDI.PointF(startX + offset, y);

            while (start.Y >= 0)
            {
                if (ShouldRenderText(row))
                {
                    graphics.DrawString(this.PickBeatIndex(row).ToString(), 
                        sheetRenderer.Style.BeatFont, 
                        sheetRenderer.Style.BeatBrush,
                        new GDI.PointF(30, start.Y));
                }

                var pen = PickPen(row, sheetRenderer.Style);

                for (int i = 0; i < sheetRenderer.Sheet.Column; i++)
                {
                    graphics.DrawLine(pen, start, end);
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
        public abstract float GetVerticalDistance(SheetRenderer sheetRenderer);
        protected abstract float PickOffset(int row, float halfColumn, SheetRenderStyle style);
        protected abstract Pen PickPen(int row, SheetRenderStyle style);
        protected abstract bool ShouldRenderText(int row);
        protected abstract int PickBeatIndex(int row);

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
        public override float GetVerticalDistance(SheetRenderer sheetRenderer) => (float)(sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 4));

        protected override bool ShouldRenderText(int row) => row % 4 == 0;

        protected override float PickOffset(int row, float halfColumn, SheetRenderStyle style)
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

        protected override int PickBeatIndex(int row)
        {
            return row / 4;
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
        public override float GetVerticalDistance(SheetRenderer sheetRenderer) => (float)(sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 3));
        protected override bool ShouldRenderText(int row) => row % 3 == 0;
        protected override float PickOffset(int row, float halfColumn, SheetRenderStyle style)
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

        protected override int PickBeatIndex(int row)
        {
            return row / 3;
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
        public override float GetVerticalDistance(SheetRenderer sheetRenderer) => (float)(sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 2));
        protected override bool ShouldRenderText(int row) => row % 2 == 0;
        protected override float PickOffset(int row, float halfColumn, SheetRenderStyle style)
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

        protected override int PickBeatIndex(int row)
        {
            return row / 2;
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
        public override float GetVerticalDistance(SheetRenderer sheetRenderer) => (float)(sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 6));
        protected override bool ShouldRenderText(int row) => row % 6 == 0;
        protected override float PickOffset(int row, float halfColumn, SheetRenderStyle style)
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

        protected override int PickBeatIndex(int row)
        {
            return row / 6;
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
        public override float GetVerticalDistance(SheetRenderer sheetRenderer) => (float)(sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 8));
        protected override bool ShouldRenderText(int row) => row % 8 == 0;
        protected override float PickOffset(int row, float halfColumn, SheetRenderStyle style)
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

        protected override int PickBeatIndex(int row)
        {
            return row / 8;
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
        public override float GetVerticalDistance(SheetRenderer sheetRenderer) => (float)(sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 12));
        protected override bool ShouldRenderText(int row) => row % 12 == 0;
        protected override float PickOffset(int row, float halfColumn, SheetRenderStyle style)
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

        protected override int PickBeatIndex(int row)
        {
            return row / 12;
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
        public override float GetVerticalDistance(SheetRenderer sheetRenderer) => (float)(sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 16));
        protected override bool ShouldRenderText(int row) => row % 16 == 0;
        protected override float PickOffset(int row, float halfColumn, SheetRenderStyle style)
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

        protected override int PickBeatIndex(int row)
        {
            return row / 16;
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
        public override float GetVerticalDistance(SheetRenderer sheetRenderer) => (float)(sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 24));
        protected override bool ShouldRenderText(int row) => row % 24 == 0;
        protected override float PickOffset(int row, float halfColumn, SheetRenderStyle style)
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

        protected override int PickBeatIndex(int row)
        {
            return row / 24;
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
        public override float GetVerticalDistance(SheetRenderer sheetRenderer) => (float)(sheetRenderer.BitmapHeight / (sheetRenderer.Zoom * 32));
        protected override bool ShouldRenderText(int row) => row % 32 == 0;
        protected override float PickOffset(int row, float halfColumn, SheetRenderStyle style)
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

        protected override int PickBeatIndex(int row)
        {
            return row / 32;
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
