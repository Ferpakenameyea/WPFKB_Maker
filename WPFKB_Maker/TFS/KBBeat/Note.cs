using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WPFKB_Maker.TFS.KBBeat
{
    abstract public class Note
    {
        public NoteType Type { get; set; }
        public (int, int)[] Datas { get; set; }
        
        private void OnBasePositionChange((int, int) basePosition)
            => this.Datas[0] = basePosition;
        public (int, int) BasePosition 
        { 
            get => this.Datas[0]; 
            set
            {
                this.OnBasePositionChange(value);
                this.Datas[0].Item1 = value.Item1;
                this.Datas[0].Item2 = value.Item2;
            }
        }
    }

    public class HitNote : Note
    {
        public HitNote((int, int) position)
        {
            Datas = new (int, int)[1];
            base.Type = NoteType.Hit;
            this.BasePosition = position;
        }
    }

    public enum NoteType
    {
        Hit,
        Hold
    }
}
