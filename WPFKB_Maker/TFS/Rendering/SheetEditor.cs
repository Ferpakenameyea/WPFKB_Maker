using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WPFKB_Maker.TFS.KBBeat;

namespace WPFKB_Maker.TFS.Rendering
{
    public class SheetEditor
    {
        public SheetRenderer Renderer { get; }
        public SheetPlayer Player { get; }
        public Sheet Sheet { get => Renderer.Sheet; }
        public Project Project { get => Renderer.Project; set => Renderer.Project = value; }
        public (int, int)? Selector { get => Renderer.Selector; }
        public (int, int)? HoldStart { get; set; } = null;
        public ICollection<Note> SelectedNotes { get; } = new HashSet<Note>();
        private List<(int, int)> removingPositions = new List<(int, int)>();
        public bool IsInSelection { get => SelectedNotes.Count > 0; }
        public SheetEditor(SheetRenderer renderer, SheetPlayer player)
        {
            this.Renderer = renderer;
            this.Renderer.SelectedNotesProvider = () => this.SelectedNotes;
            this.Player = player;
        }
        ~SheetEditor()
        {
            this.Renderer.SelectedNotesProvider = () => Array.Empty<Note>();
        }
        public Note GetNote(int row, int column) => this.Sheet?.GetNote(row, column);
        public bool PutNote(int row, int column, Note note) => this.Sheet?.PutNote(row, column, note) ?? false;
        public bool DeleteNote(int row, int column) => this.Sheet?.DeleteNote(row, column) ?? false;
        public bool HasSheet() => this.Sheet != null;
        public (int, int)? ScreenToSheetPosition(Point screenPoint) => this.Renderer.Agent.GetPositionScreen(this.Renderer, screenPoint);
        public void RemoveNote((int, int) selector)
        {
            var query = from note in this.Renderer.NotesToRender
                        where IsSelectedNote(note, selector)
                        orderby SelectNotePriority(note)
                        select note;

            if (!query.Any())
            {
                return;
            }

            var position = query.First().BasePosition;
            lock (this.Renderer.Sheet)
            {
                this.Renderer.Sheet.DeleteNote(position.Item1, position.Item2);
            }
        }
        public void PutHoldNote((int, int) selector)
        {
            if (HoldStart == null)
            {
                HoldStart = selector;
                return;
            }

            if (selector.Item2 != HoldStart.Value.Item2)
            {
                MessageBox.Show("HOLD 音符的起始和终止位置应当位于同一列");
                return;
            }

            if (selector.Item1 <= HoldStart.Value.Item1)
            {
                MessageBox.Show("HOLD 音符的终止位置应当在起始位置之后");
                return;
            }

            Note note = new HoldNote((HoldStart.Value, selector));
            lock (this.Sheet)
            {
                this.Sheet.PutNote(
                    HoldStart.Value.Item1,
                    HoldStart.Value.Item2,
                    note);
            }
            HoldStart = null;
        }
        public void PutHitNode((int, int) selector)
        {
            lock (this.Sheet)
            {
                this.Sheet.PutNote(
                    selector.Item1,
                    selector.Item2, new HitNote(selector));
            }
        }
        private bool IsSelectedNote(Note note, (int, int) selector)
        {
            if (note is HitNote)
            {
                return note.BasePosition == selector;
            }

            var hold = note as HoldNote;
            return hold.BasePosition.Item2 == selector.Item2 &&
                hold.BasePosition.Item1 <= selector.Item1 &&
                hold.End.Item1 >= selector.Item1;
        }
        private int SelectNotePriority(Note note)
        {
            if (note is HitNote)
            {
                return 1;
            }
            return 2;
        }
        public void SelectNotesByDragging((int, int) from, (int, int) to)
        {
            int minRow = Math.Min(from.Item1, to.Item1);
            int maxRow = Math.Max(from.Item1, to.Item1);
            int minColumn = Math.Min(from.Item2, to.Item2);
            int maxColumn = Math.Max(from.Item2, to.Item2);

            var query = from note in this.Renderer.NotesToRender
                        where NoteInDraggingSelectionRange(note, minRow, maxRow, minColumn, maxColumn)
                        select note;
            this.SelectedNotes.Clear();

            foreach (var note in query.AsEnumerable())
            {
                this.removingPositions.Add(note.BasePosition);
                this.SelectedNotes.Add(note);
            }

            Debug.console.Write($"Selected {this.SelectedNotes.Count} notes:");
            foreach (var note in this.SelectedNotes)
            {
                Debug.console.Write(note.ToString());
            }
        }
        private bool NoteInDraggingSelectionRange(Note note, int minRow, int maxRow, int minColumn, int maxColumn)
        {
            if (note is HitNote)
            {
                return PositionInRange(note.BasePosition, minRow, maxRow, minColumn, maxColumn);
            }

            var hold = note as HoldNote;
            return
                PositionInRange(hold.Start, minRow, maxRow, minColumn, maxColumn) ||
                PositionInRange(hold.End, minRow, maxRow, minColumn, maxColumn);
        }
        private bool PositionInRange((int, int) position, int minRow, int maxRow, int minColumn, int maxColumn)
        {
            return position.Item1 >= minRow && position.Item1 <= maxRow
                    && position.Item2 >= minColumn && position.Item2 <= maxColumn;
        }
        public void SelectSingleNote(Note note)
        {
            this.SelectedNotes.Clear();
            this.removingPositions.Add(note.BasePosition);
            this.SelectedNotes.Add(note);
        }
        public void MoveSelectionShadow((int, int) delta)
        {
            foreach (var note in this.SelectedNotes)
            {
                note.Move(delta);
            }
        }
        public void FlushSelectionMoving()
        {
            foreach (var position in this.removingPositions)
            {
                this.Sheet.DeleteNote(position.Item1, position.Item2);
            }

            foreach (var note in this.SelectedNotes)
            {
                this.Sheet.PutNote(note.BasePosition.Item1, note.BasePosition.Item2, note);
            }

            Debug.console.Write("Moving flushed");
            this.SelectedNotes.Clear();
            this.removingPositions.Clear();
        }
    }
}
