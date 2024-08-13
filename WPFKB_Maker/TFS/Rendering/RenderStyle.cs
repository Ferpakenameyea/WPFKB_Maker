using System;
using System.Windows;
using System.Drawing;
using Point = System.Windows.Point;
using System.Windows.Controls.Primitives;

namespace WPFKB_Maker.TFS.Rendering
{
    public class SheetRenderStyle
    {
        public Color BackgroundColor { get; set; }
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

        public float FullNotePercentage { get; set; }
        public float NotFullNotePercentage { get; set; }
        public Font BeatFont { get; set; }
        public Brush BeatBrush { get; set; }
        public Func<PointF, RectangleF> SelectorProvider { get; set; }
        public Brush SelectorBrush { get; set; }
        public Pen SelectorPen { get; set; }
        public Func<PointF, RectangleF> NoteProvider { get; set; }
        public Brush NoteBrush { get; set; }
        public Pen NotePen { get; set; }
        public Brush SelectedNoteBrush { get; set; }
        public Pen SelectedNotePen { get; set; }
        public Brush DiscardZoneBrush { get; set; }
    }

    public static class SheetRenderStyles
    {
        public static SheetRenderStyle Default { get; } = new SheetRenderStyle()
        {
            BackgroundColor = Color.FromArgb(5, 6, 2),
            SeperatorPen = new Pen(new SolidBrush(Color.FromArgb(50, 51, 59)), 1),
            BorderPen = new Pen(new SolidBrush(Color.FromArgb(80, 81, 89)), 2),
            TriggerLinePen = new Pen(new SolidBrush(Color.FromArgb(255, 46, 65)), 2),

            NotePen1_1 = new Pen(new SolidBrush(Color.FromArgb(90, 91, 99)), 2),
            NotePen1_2 = new Pen[]
            {
                new Pen(new SolidBrush(Color.FromArgb(50, 80, 50)), 2)
            },
            NotePen1_3 = new Pen[]
            {
                new Pen(new SolidBrush(Color.FromArgb(30, 60, 30)), 2)
            },
            NotePen1_4 = new Pen[]
            {
                new Pen(new SolidBrush(Color.FromArgb(65, 38, 69)), 2),
                new Pen(new SolidBrush(Color.FromArgb(21, 52, 58)), 2)
            },
            NotePen1_6 = new Pen[]
            {
                new Pen(new SolidBrush(Color.FromArgb(20, 80, 20)), 2),
                new Pen(new SolidBrush(Color.FromArgb(20, 60, 80)), 2)
            },
            NotePen1_8 = new Pen[]
            {
                new Pen(new SolidBrush(Color.FromArgb(85, 58, 89)), 2),
                new Pen(new SolidBrush(Color.FromArgb(41, 72, 78)), 2),
                new Pen(new SolidBrush(Color.FromArgb(30, 30, 30)), 2)
            },
            NotePen1_12 = new Pen[]
            {
                new Pen(new SolidBrush(Color.FromArgb(85, 58, 89)), 2),
                new Pen(new SolidBrush(Color.FromArgb(41, 72, 78)), 2),
                new Pen(new SolidBrush(Color.FromArgb(30, 30, 30)), 2)
            },
            NotePen1_16 = new Pen[]
            {
                new Pen(new SolidBrush(Color.FromArgb(85, 58, 89)), 2),
                new Pen(new SolidBrush(Color.FromArgb(41, 72, 78)), 2),
                new Pen(new SolidBrush(Color.FromArgb(30, 30, 30)), 2)
            },
            NotePen1_24 = new Pen[]
            {
                new Pen(new SolidBrush(Color.FromArgb(85, 58, 89)), 2),
                new Pen(new SolidBrush(Color.FromArgb(41, 72, 78)), 2),
                new Pen(new SolidBrush(Color.FromArgb(30, 30, 30)), 2)
            },
            NotePen1_32 = new Pen[]
            {
                new Pen(new SolidBrush(Color.FromArgb(85, 58, 89)), 2),
                new Pen(new SolidBrush(Color.FromArgb(41, 72, 78)), 2),
                new Pen(new SolidBrush(Color.FromArgb(30, 30, 30)), 2)
            },
            FullNotePercentage = 1,
            NotFullNotePercentage = 0.8f,

            BeatFont = new Font("Consolas", 30),
            BeatBrush = Brushes.White,

            SelectorBrush = new SolidBrush(Color.FromArgb(90, 179, 234, 255)),

            SelectorPen = null,
            SelectorProvider = (center) => new RectangleF(
                    new PointF(center.X - 40, center.Y - 10), 
                    new SizeF(80, 20)),

            NoteBrush = Brushes.White,
            NotePen = new Pen(new SolidBrush(Color.FromArgb(208, 221, 234)), 4),
            NoteProvider = (center) => new RectangleF(
                    new PointF(center.X - 40, center.Y - 10),
                    new SizeF(80, 20)),

            SelectedNoteBrush = new SolidBrush(Color.FromArgb(255, 255, 102)),
            SelectedNotePen = new Pen(new SolidBrush(Color.FromArgb(229, 232, 107)), 4),
            DiscardZoneBrush = new SolidBrush(Color.FromArgb(100, 200, 30, 30))
        };
    }
}
