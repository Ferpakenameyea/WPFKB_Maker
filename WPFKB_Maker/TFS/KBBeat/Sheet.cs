using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Threading;

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
        public abstract ICollection<Note> Values { get; }
        public void WriteJsonData(JsonWriter jsonWriter, JsonSerializer jsonSerializer)
        {
            jsonWriter.WriteStartArray();
            foreach (var note in Values)
            {
                jsonSerializer.Serialize(jsonWriter, note);
            }
            jsonWriter.WriteEndArray();
        }
        public void ReadJsonData(JToken dataToken, JsonSerializer jsonSerializer)
        {
            var data = dataToken as JArray;
            foreach (var item in data)
            {
                var note = item.ToObject<Note>(jsonSerializer);
                this.PutNote(note.BasePosition.Item1, note.BasePosition.Item2, note);
            }
        }
        public static JsonSerializerSettings SheetJsonSerializerSettings { get; }
        static Sheet()
        {
            SheetJsonSerializerSettings = new JsonSerializerSettings();
            SheetJsonSerializerSettings.Converters.Add(new SheetJsonConverter());
            SheetJsonSerializerSettings.Converters.Add(new NoteConverter());
        }
    }

    public class HashSheet : Sheet
    {
        public static HashSheet Default { get; } = new HashSheet(6, 3, 3);
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
        public override ICollection<Note> Values => this.notes.Values;
    }

    public class SheetJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => typeof(Sheet).IsAssignableFrom(objectType);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            var type = jObject["type"] ?? throw new ArgumentException("sheet missing \"type\" property");
            var typeName = type.ToString();
            
            var col = jObject["col"] as JValue;
            var l = jObject["l"] as JValue;
            var r = jObject["r"] as JValue;

            if (nameof(HashSheet).Equals(typeName))
            {
                var result = new HashSheet(col.Value<int>(), l.Value<int>(), r.Value<int>());
                var data = jObject["data"];
                
                result.ReadJsonData(data, serializer);
                return result;
            }

            throw new NotSupportedException("unknown sheet implementation: " + typeName.ToString());
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var sheet = value as HashSheet;
            writer.WriteStartObject();

            writer.WritePropertyName("col");
            writer.WriteValue(sheet.Column);
            writer.WritePropertyName("l");
            writer.WriteValue(sheet.LeftSize);
            writer.WritePropertyName("r");
            writer.WriteValue(sheet.RightSize);

            writer.WritePropertyName("type");
            if (value is HashSheet)
            {
                var hashSheet = value as HashSheet;
                writer.WriteValue(value.GetType().Name);
                writer.WritePropertyName("data");
                hashSheet.WriteJsonData(writer, serializer);
            }
            else
            {
                throw new NotSupportedException($"not supported sheet implementation type: {value.GetType().Name}");
            }

            writer.WriteEndObject();
        }
    }
}
