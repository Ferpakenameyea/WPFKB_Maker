using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WPFKB_Maker.TFS
{
    public class SheetRenderer
    {
        public Image Image { get; }

        private RenderTargetBitmap bitmap;
        private readonly DrawingVisual drawingVisual = new DrawingVisual();

        private double dpiX;
        private double dpiY;

        public double Width { get => this.bitmap.Width; }
        public double Height { get => this.bitmap.Height; }
        private int fps = 60;
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
        public long RenderIntervalMilliseconds { get; private set; } = 0;
        private readonly Stopwatch stopwatch = new Stopwatch();

        public SheetRenderer(Image image, int initialWidth, int initialHeight, double dpiX, double dpiY, int fps = 60)
        {
            this.Image = image;
            this.dpiX = dpiX;
            this.dpiY = dpiY;

            this.bitmap = new RenderTargetBitmap(initialWidth, initialHeight, dpiX, dpiY, PixelFormats.Pbgra32);
            this.Image.Source = this.bitmap;

            CompositionTarget.Rendering += this.Render;
            this.Image.SizeChanged += ChangeBitmap;
            this.stopwatch.Start();
            this.FPS = fps;
        }


        private void ChangeBitmap(object sender, EventArgs e)
        {
            this.bitmap = new RenderTargetBitmap(
                        (int)this.Image.ActualWidth,
                        (int)this.Image.ActualHeight,
                        dpiX, dpiY, PixelFormats.Pbgra32);
            this.Image.Source = this.bitmap;
        }

        private void Render(object sender, EventArgs e)
        {
            if (stopwatch.ElapsedMilliseconds < this.RenderIntervalMilliseconds)
            {
                return;
            }

            using (var context = this.drawingVisual.RenderOpen())
            {
                context.DrawRectangle(Brushes.LightBlue, null, new Rect(0, 0, this.Image.ActualWidth, this.Image.ActualHeight));
            }

            this.bitmap.Clear();
            this.bitmap.Render(this.drawingVisual);
            stopwatch.Restart();
        }

        ~SheetRenderer()
        {
            try
            {
                CompositionTarget.Rendering -= Render;
                this.Image.SizeChanged -= ChangeBitmap;
            }
            catch (Exception)
            {
            }
        }
    }
}
