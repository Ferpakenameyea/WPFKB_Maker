using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
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

        public Sheet Sheet { get; set; } = new HashSheet(6, 3, 3);

        public double Width { get => this.bitmap.Width; }
        public double Height { get => this.bitmap.Height; }
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

        public double ColumnWidth
        {
            get
            {
                if (this.Sheet == null)
                {
                    return 0;
                }

                return this.Width / this.Sheet.Column;
            }
        }

        private int fps = 0;
        public int FPS
        {
            get => this.fps;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("FPS must be a positive integer");
                }

                this.fps = value;
                this.RenderIntervalMilliseconds = 1000 / this.fps;
            }
        }
        public double RenderFromY { get; set; } = 0.0;

        public long RenderIntervalMilliseconds { get; private set; } = 0;
        private readonly Stopwatch stopwatch = new Stopwatch();

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
        };

        public SheetRenderer(Image image, int initialWidth, int initialHeight, double dpiX, double dpiY, int fps = 144)
        {
            this.Image = image;
            this.dpiX = dpiX;
            this.dpiY = dpiY;
            this.FPS = fps;
            
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
                }
            }

            this.bitmap.Clear();
            this.bitmap.Render(this.drawingVisual);
            stopwatch.Restart();
        }

        private void DrawTriggerLine(DrawingContext context)
        {
            context.DrawLine(this.Style.TriggerLinePen,
                new Point(0, this.Height - this.TriggerLineY),
                new Point(this.Width, this.Height - this.TriggerLineY));
        }

        private void DrawSheet(DrawingContext context, Sheet sheet)
        {
            context.DrawRectangle(Style.BackgroundBrush, null, new Rect(0, 0, Width, Height));
            DrawFrames(context, sheet);
        }

        private void DrawFrames(DrawingContext context, Sheet sheet)
        {
            double interval = this.Width / sheet.Column;
            var start = new Point(0, 0);
            var end = new Point(0, this.Height);
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
    }

    public abstract class BeatRenderStrategy
    {
        public abstract void Render(SheetRenderer sheetRenderer, DrawingContext context);
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
        R1_4, 
        R1_3,
    }

    public class Strategy_1_4 : BeatRenderStrategy
    {
        public override void Render(SheetRenderer sheetRenderer, DrawingContext context)
        {
            double verticalDistance = sheetRenderer.Height / (sheetRenderer.Zoom * 4);
            double horizontalDistance = sheetRenderer.ColumnWidth;
            
            int row = (int)Math.Ceiling(sheetRenderer.RenderFromY / verticalDistance);
            
            double y = sheetRenderer.Height - (row * verticalDistance - sheetRenderer.RenderFromY);
            double startX = sheetRenderer.ColumnWidth / 2;
            
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
    }
}
