using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFKB_Maker.TFS.KBBeat
{
    public abstract class Sheet
    {
        public abstract Note GetNote(int column, int row);
        public abstract bool PutNote(int column, int row, Note note);
        public abstract bool DeleteNote(int column, int row);
    }

    public class HashSheet : Sheet
    {
        private Dictionary<(int, int), Note> notes;

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
