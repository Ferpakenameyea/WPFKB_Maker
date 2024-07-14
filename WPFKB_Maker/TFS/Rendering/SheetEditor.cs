using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WPFKB_Maker.Editing;
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
        private List<Note> clipBoard = null;
        private bool IsClipBoardSourceFromCut { get; set; } = false;

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
            int row = HoldStart.Value.Item1;
            int col = HoldStart.Value.Item2;
            HoldStart = null;
            UndoRedo.PushCommand(
                () => this.Sheet.DeleteNote(row, col),
                () => this.Sheet.PutNote(row, col, note));
        }
        public void PutHitNode((int, int) selector)
        {
            var note = new HitNote(selector);
            lock (this.Sheet)
            {
                this.Sheet.PutNote(
                    selector.Item1,
                    selector.Item2,
                    note);
            }
            int row = selector.Item1;
            int col = selector.Item2;
            UndoRedo.PushCommand(
                () => this.Sheet.DeleteNote(row, col),
                () => this.Sheet.PutNote(row, col, note));
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
            if (!this.SelectedNotes.Any() || 
                this.SelectedNotes.First().BasePosition == this.removingPositions.First())
            {
                this.SelectedNotes.Clear();
                this.removingPositions.Clear();
                return;
            }

            foreach (var position in this.removingPositions)
            {
                this.Sheet.DeleteNote(position.Item1, position.Item2);
            }

            foreach (var note in this.SelectedNotes)
            {
                this.Sheet.PutNote(note.BasePosition.Item1, note.BasePosition.Item2, note);
            }

            var add = this.SelectedNotes.Select(note => note.BasePosition).ToArray();
            var remov = this.removingPositions.ToArray();
            var targets = this.SelectedNotes.ToArray();

            Debug.console.Write("Moving flushed");
            
            UndoRedo.PushCommand(
                () =>
                {
                    for (int i = 0; i < add.Length; i++)
                    {
                        this.Sheet.DeleteNote(
                            add[i].Item1,
                            add[i].Item2
                            );

                        this.Sheet.PutNote(
                            remov[i].Item1,
                            remov[i].Item2,
                            targets[i]
                            );
                    }
                },
                () => 
                {
                    foreach (var position in remov)
                    {
                        this.Sheet.DeleteNote(position.Item1, position.Item2);
                    }

                    for (int i = 0; i < targets.Length; i++)
                    {
                        this.Sheet.PutNote(add[i].Item1, add[i].Item2, targets[i]);
                    }
                }
            );
            
            this.SelectedNotes.Clear();
            this.removingPositions.Clear();
        }
        public void ClearSheet()
        {
            this.SelectedNotes.Clear();
            this.removingPositions.Clear();

            var collection = this.Sheet.Values.ToArray();
            this.Sheet.Clear();
            UndoRedo.PushCommand(
                () =>
                {
                    foreach (var note in collection)
                    {
                        Sheet.PutNote(note.BasePosition.Item1, note.BasePosition.Item2, note);
                    }
                },
                () =>
                {
                    this.SelectedNotes.Clear();
                    this.removingPositions.Clear();
                    this.Sheet.Clear();
                });

        }
        public void DeleteSelectedNotes()
        {
            FlushSelectionNoCleaning(out var before, out var after, out var targets);

            foreach (var note in this.SelectedNotes)
            {
                this.Sheet.DeleteNote(note.BasePosition.Item1, note.BasePosition.Item2);
            }

            UndoRedo.PushCommand(
                () =>
                {
                    for (int i = 0; i < targets.Length; i++)
                    {
                        this.Sheet.PutNote(after[i].Item1, after[i].Item2, targets[i]);
                    }
                },
                () =>
                {
                    foreach (var note in targets)
                    {
                        this.Sheet.DeleteNote(note.BasePosition.Item1, note.BasePosition.Item2);
                    }
                });

            this.SelectedNotes.Clear();
            this.removingPositions.Clear();
        }
        public void CopySelectedNotes()
        {
            if (this.SelectedNotes.Count == 0)
            {
                return;
            }

            this.clipBoard = this.SelectedNotes.Select(note => note.Clone()).ToList();
            this.IsClipBoardSourceFromCut = false;
        }
        public void PasteSelectedNotes()
        {
            if (this.clipBoard == null || this.clipBoard.Count == 0)
            {
                return;
            }

            var targetPosition = this.Renderer.Agent.GetMousePosition(this.Renderer);
            if (targetPosition == null)
            {
                return;
            }
            
            this.FlushSelectionMoving();

            var firstPosition = this.clipBoard[0].BasePosition;
            var delta =
                (targetPosition.Value.Item1 - firstPosition.Item1,
                targetPosition.Value.Item2 - firstPosition.Item2);

            Note[] cloned = new Note[this.clipBoard.Count];
            int i = 0;

            clipBoard.ForEach(note =>
            {
                var clone = note.Clone();
                cloned[i++] = clone;
                clone.Move(delta);
                this.SelectedNotes.Add(clone);
                this.removingPositions.Add(clone.BasePosition);
                Sheet.PutNote(clone.BasePosition.Item1, clone.BasePosition.Item2, clone);
            });

            UndoRedo.PushCommand(
                () =>
                {
                    foreach (var cl in cloned)
                    {
                        Sheet.DeleteNote(cl.BasePosition.Item1, cl.BasePosition.Item2);
                    }
                },
                () =>
                {
                    foreach (var cl in cloned)
                    {
                        Sheet.PutNote(cl.BasePosition.Item1, cl.BasePosition.Item2, cl);
                    }
                });
            if (IsClipBoardSourceFromCut)
            {
                this.clipBoard = null;
            }
        }
        public void CutSelectedNotes()
        {
            FlushSelectionNoCleaning(out var before, out var after, out var targets);
            this.clipBoard = targets.ToList();
            this.SelectedNotes.Clear();
            this.removingPositions.Clear();
        
            foreach (var n in targets)
            {
                this.Sheet.DeleteNote(n.BasePosition.Item1, n.BasePosition.Item2);
            }
            this.IsClipBoardSourceFromCut = true;
            UndoRedo.PushCommand(
                () =>
                {
                    for (int i = 0; i < targets.Length; i++)
                    {
                        this.Sheet.PutNote(after[i].Item1, after[i].Item2, targets[i]);
                    }
                },
                () =>
                {
                    foreach (var n in targets)
                    {
                        this.Sheet.DeleteNote(n.BasePosition.Item1, n.BasePosition.Item2);
                    }
                });
        }

        private void FlushSelectionNoCleaning(out (int, int)[] _before, out (int, int)[] _after, out Note[] _targets)
        {
            if (this.SelectedNotes.Count == 0)
            {
                _before = Array.Empty<(int, int)>();
                _after = Array.Empty<(int, int)>();
                _targets = Array.Empty<Note>();

                return;
            }

            // flush first
            foreach (var position in this.removingPositions)
            {
                this.Sheet.DeleteNote(position.Item1, position.Item2);
            }

            foreach (var note in this.SelectedNotes)
            {
                this.Sheet.PutNote(note.BasePosition.Item1, note.BasePosition.Item2, note);
            }

            var after = this.SelectedNotes.Select(note => note.BasePosition).ToArray();
            var before = this.removingPositions.ToArray();
            var targets = this.SelectedNotes.ToArray();

            UndoRedo.PushCommand(
                () =>
                {
                    for (int i = 0; i < after.Length; i++)
                    {
                        this.Sheet.DeleteNote(
                            after[i].Item1,
                            after[i].Item2
                            );

                        this.Sheet.PutNote(
                            before[i].Item1,
                            before[i].Item2,
                            targets[i]
                            );
                    }
                },
                () =>
                {
                    foreach (var position in before)
                    {
                        this.Sheet.DeleteNote(position.Item1, position.Item2);
                    }

                    for (int i = 0; i < targets.Length; i++)
                    {
                        this.Sheet.PutNote(after[i].Item1, after[i].Item2, targets[i]);
                    }
                }
            );

            _after = after;
            _before = before;
            _targets = targets;
        }
    }
}
