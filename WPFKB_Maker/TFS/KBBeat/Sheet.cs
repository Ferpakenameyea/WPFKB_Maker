using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace WPFKB_Maker.TFS.KBBeat
{
    public abstract class Sheet
    {
        public int Column { get; private set; }
        public int LeftSize { get; private set; }
        public int RightSize { get; private set; }
        public abstract Note GetNote(int row, int column);
        public abstract bool PutNote(int row, int column, Note note);
        public abstract bool DeleteNote(int row, int column);
        public Sheet(int column, int leftSize, int rightSize)
        {
            if (column <= 0 || column > 10)
            {
                throw new ArgumentOutOfRangeException("row should be in range of (0, 10]");
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

        public override bool DeleteNote(int row, int column)
            => notes.Remove((row, column));

        public override Note GetNote(int row, int column)
        {
            this.notes.TryGetValue((row, column), out var note);
            return note;
        }

        public override bool PutNote(int row, int column, Note note)
        {
            if (!notes.ContainsKey((row, column)))
            {
                notes[(row, column)] = note;
                return true;
            }
            else
            {
                return false;
            }
        }
    }
    public class ConcurrentHashSheet : HashSheet
    {
        private ReaderWriterLockSlim rwlock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        public ConcurrentHashSheet(int column, int left, int right) : base(column, left, right)
        {
        }

        public override bool DeleteNote(int row, int column)
        {
            try
            {
                this.rwlock.EnterWriteLock();
                base.DeleteNote(row, column);
                return true;
            }
            finally
            {
                this.rwlock.ExitWriteLock();
            }
        }

        public override Note GetNote(int row, int column)
        {
            try
            {
                rwlock.EnterReadLock();
                return base.GetNote(row, column);
            }
            finally
            {
                rwlock.ExitReadLock();
            }
        }

        public override bool PutNote(int row, int column, Note note)
        {
            try
            {
                rwlock.EnterWriteLock();
                return base.PutNote(row, column, note);
            }
            finally
            {
                rwlock.ExitWriteLock();
            }
        }
    }
}
