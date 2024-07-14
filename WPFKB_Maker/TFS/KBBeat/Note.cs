using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace WPFKB_Maker.TFS.KBBeat
{
    abstract public class Note
    {
        [JsonProperty("t")] public NoteType Type { get; protected set; }
        [JsonProperty("d")] public (int, int)[] Datas { get; protected set; }
        [JsonIgnore] public virtual (int, int) BasePosition { get => this.Datas[0]; set => this.Datas[0] = value; }

        protected Note(int dataArraySize, NoteType noteType)
        {
            this.Datas = new (int, int)[dataArraySize];
            this.Type = noteType;
        }

        public override string ToString()
        {
            return $"KBBeat-abstract-note: {this.BasePosition}";
        }

        public virtual void Move((int, int) delta)
        {
            this.BasePosition = this.BasePosition.Add(delta);
        }

        public abstract Note Clone();
    }

    public class HitNote : Note
    {
        public HitNote((int, int) position) : base(1, NoteType.Hit)
        {
            this.Datas[0] = position;
            base.Type = NoteType.Hit;
        }
        [JsonIgnore] public (int, int) Position { get => this.Datas[0]; }
        public override string ToString()
        {
            return $"KBBeat-Hit: {this.BasePosition}";
        }
        public override Note Clone()
        {
            return new HitNote(this.BasePosition);
        }
    }

    public class HoldNote : Note
    {
        public HoldNote(((int, int), (int, int)) value) : base(2, NoteType.Hold)
        {
            this.Value = value;
        }
        [JsonIgnore] public (int, int) Start { get => this.Datas[0]; set => this.Datas[0] = value; }
        [JsonIgnore] public (int, int) End { get => this.Datas[1]; set => this.Datas[1] = value; }
        
        [JsonIgnore] public override (int, int) BasePosition 
        { 
            get => base.BasePosition;
            set
            {
                var len = this.End.Item1 - this.Start.Item1;
                base.BasePosition = value;

                this.End = (base.BasePosition.Item1 + len, base.BasePosition.Item2);
            }
        }

        [JsonIgnore]
        public ((int, int), (int, int)) Value
        {
            get => (this.Start, this.End);
            set
            {
                var start = value.Item1;
                var end = value.Item2;

                if (end.Item2 != start.Item2)
                {
                    throw new ArgumentException(
                        "A hold note must have the start and end position at the same column");
                }

                if (end.Item1 <= start.Item1)
                {
                    throw new ArgumentException(
                        "A hold note's end position must be later than the start position");
                }
                this.Datas[0] = start;
                this.Datas[1] = end;
            }
        }
        public override string ToString()
        {
            return $"KBBeat-Hold: From {this.Start} to {this.End}";
        }

        public override void Move((int, int) delta)
        {
            this.Start = this.Start.Add(delta);
            this.End = this.End.Add(delta);
        }

        public override Note Clone()
        {
            return new HoldNote((this.Start, this.End));
        }
    }

    public enum NoteType
    {
        Hit,
        Hold
    }

    public class NoteConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(Note).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jsonObject = JToken.ReadFrom(reader);

            Note value = null;
            JArray array = jsonObject["val"] as JArray;

            switch (jsonObject["type"].ToString())
            {
                case nameof(NoteType.Hit):
                    value = new HitNote((
                        array[0].Value<int>(),
                        array[1].Value<int>()));
                    break;

                case nameof(NoteType.Hold):
                    value = new HoldNote(
                        (
                            (array[0].Value<int>(), array[1].Value<int>()),
                            (array[2].Value<int>(), array[3].Value<int>())
                        ));
                    break;

                default:
                    throw new JsonSerializationException($"Unknown note type: {jsonObject["type"]}");
            }

            return value;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var note = value as Note;
            writer.WriteStartObject();

            writer.WritePropertyName("type");
            writer.WriteValue(note.Type.ToString());

            writer.WritePropertyName("val");
            writer.WriteStartArray();

            foreach (var pos in note.Datas)
            {
                writer.WriteValue(pos.Item1);
                writer.WriteValue(pos.Item2);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }
}
