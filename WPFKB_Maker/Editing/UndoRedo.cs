using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace WPFKB_Maker.Editing
{
    public static class UndoRedo
    {
        private static readonly LinkedList<Command> list = new LinkedList<Command>();
        private static LinkedListNode<Command> cur = null;
        
        public static int MaxSize { get; set; } = 100;

        public static void PushCommand(Action undoAction, Action redoAction)
        {
            Debug.console.Write("Pushed command");

            if (cur == null)
            {
                list.Clear();

                list.AddLast(new Command(undoAction, redoAction));
                cur = list.First;
            }
            else
            {
                while(cur.Next != null)
                {
                    list.RemoveLast();
                }

                list.AddLast(new Command(undoAction, redoAction));
                cur = cur.Next;
            }

            while(list.Count > MaxSize)
            {
                list.RemoveFirst();
            }
        }

        public static void Clear()
        {
            list.Clear();
        }

        public static void Undo()
        {
            if (cur == null)
            {
                return;
            }

            cur.Value.UndoAction.Invoke();
            cur = cur.Previous;
        }

        public static void Redo()
        {
            if (list.Count == 0)
            {
                return;
            }

            if (cur == null)
            {
                list.First.Value.RedoAction.Invoke();
                cur = list.First;
            }
            else if (cur.Next != null)
            {
                cur.Next.Value.RedoAction.Invoke();
                cur = cur.Next;
            }
        }

        private struct Command
        {
            public Action UndoAction;
            public Action RedoAction;

            public Command(Action undoAction, Action redoAction)
            {
                UndoAction = undoAction;
                RedoAction = redoAction;
            }
        }
    }
}
