using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using WPFKB_Maker.TFS;

namespace WPFKB_Maker
{
    public static class Debug
    {
        public static UIConsole console { get; } = new Console();

        private class Console : UIConsole
        {
            public void Clean() => DebugConsole.Console?.Clean();
            public void Write(object content) => DebugConsole.Console?.Write(content);
        }
    }

    public partial class DebugConsole : Window
    {
        internal static UIConsole Console { get; set; }

        public DebugConsole()
        {
            InitializeComponent();
            Console = new TextBlockConsole(this.consoleScroller, this.consoleBox);
            Console.Write("Debug console initialized");
            Console.Write($"KBMaker {TFS.Version.version} WPF version");
            Console.Write("Programmed by MCDaxia1472, this is a opensource software");
        }
    }

    public interface UIConsole
    {
        void Clean();
        void Write(object content);
    }

    public class TextBlockConsole : UIConsole
    {
        private TextBlock textBlock;
        private ScrollViewer scrollViewer;
        private Queue<String> messages = new Queue<string>();
        private StringBuilder stringBuilder = new StringBuilder();

        internal int ReserveLines { get; set; }
        internal int CleanTrigger { get; set; }

        public TextBlockConsole(ScrollViewer scrollViewer, TextBlock textBlock, int reserveLines = 100, int cleanTrigger = 200)
        {
            this.textBlock = textBlock;
            this.ReserveLines = reserveLines;
            this.CleanTrigger = cleanTrigger;
            this.scrollViewer = scrollViewer;
        }

        public void Clean()
        {
            this.messages.Clear();
            this.stringBuilder.Clear();
            this.textBlock.Text = string.Empty;
        }

        private void Write(string content)
        {
            this.messages.Enqueue(content);
            if (this.messages.Count > this.CleanTrigger)
            {
                while (this.messages.Count > ReserveLines)
                {
                    _ = this.messages.Dequeue();
                }
                stringBuilder.Clear();
                foreach (var line in this.messages)
                {
                    stringBuilder.AppendLine(line);
                }
                this.textBlock.Text = stringBuilder.ToString();
            }
            else
            {
                stringBuilder.AppendLine(content);
                this.textBlock.Text = stringBuilder.ToString();
            }

            this.scrollViewer.ScrollToEnd();
        }

        public void Write(object content)
        {
            if (content == null)
            {
                this.Write("null");
            }
            else
            {
                this.Write(content.ToString());
            }
        }
    }
}
