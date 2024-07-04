using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace WPFKB_Maker.TFS.KBBeat
{
    public abstract class Sheet
    {
        public int Column { get; private set; }
        public int LeftSize { get; private set; }
        public int RightSize { get; private set; }
        public abstract Note GetNote(int column, int row);
        public abstract bool PutNote(int column, int row, Note note);
        public abstract bool DeleteNote(int column, int row);
        public Sheet(int column, int leftSize, int rightSize)
        {
            if (column <= 0 || column > 10)
            {
                throw new ArgumentOutOfRangeException("Column should be in range of (0, 10]");
            }

            if (leftSize + rightSize != column)
            {
                throw new ArgumentException("Sum of leftSize and rightSize must equal with column");
            }
            
            if (leftSize < 0 || rightSize < 0)
            {
                throw new ArgumentException("Can't have negative group size");
            }
            this.Column = column;
            this.LeftSize = leftSize;
            this.RightSize = rightSize;
        }
    }

    public class HashSheet : Sheet
    {
        private readonly Dictionary<(int, int), Note> notes;
        public HashSheet(int column, int left, int right) : base(column, left, right)
        {
            this.notes = new Dictionary<(int, int), Note>();
        }

        public override bool DeleteNote(int column, int row)
            => notes.Remove((column, row));

        public override Note GetNote(int column, int row)
        {
            this.notes.TryGetValue((column, row), out var note);
            return note;
        }

        public override bool PutNote(int column, int row, Note note)
        {
            if (!notes.ContainsKey((column, row)))
            {
                notes[(column, row)] = note;
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
