using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using WPFKB_Maker.TFS.KBBeat.Unity;

namespace WPFKB_Maker.TFS.KBBeat
{
    public class Meta
    {
        [JsonProperty("assetBundleName")]
        public string AssetBundleName { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("levelAuthors")]
        public string[] LevelAuthors { get; set; }

        [JsonProperty("composers")]
        public string[] Composers { get; set; }

        [JsonProperty("difficulty")]
        public int Difficulty { get; set; }

        [JsonProperty("leftTrackSize")]
        public int LeftTrackSize { get; set; }

        [JsonProperty("rightTrackSize")]
        public int RightTrackSize { get; set; }

        [JsonProperty("noteAppearPosition")]
        public UnityVector3 NoteAppearPosition { get; set; }

        [JsonProperty("bpm")]
        public float Bpm { get; set; }
        [JsonProperty("ext")]
        public string Ext { get; set; }
        [JsonProperty("length")]
        public double LengthSeconds { get; set; }
        [JsonIgnore]
        public byte[] MusicFile { get; set; }
        [JsonIgnore]
        public WaveFormat WaveFormat { get; set; }

        public Meta(
                string assetBundleName,
                string name,
                string description,
                string[] levelAuthors,
                string[] composers,
                int difficulty,
                int leftTrackSize,
                int rightTrackSize,
                UnityVector3 noteAppearPosition,
                float bpm,
                byte[] musicFile,
                WaveFormat waveFormat,
                string ext,
                double lengthSeconds)
        {
            this.AssetBundleName = assetBundleName;
            this.Name = name;
            this.Description = description;
            this.LevelAuthors = levelAuthors;
            this.Composers = composers;
            this.Difficulty = difficulty;
            this.LeftTrackSize = leftTrackSize;
            this.RightTrackSize = rightTrackSize;
            this.NoteAppearPosition = noteAppearPosition;
            this.Bpm = bpm;
            this.MusicFile = musicFile;
            this.WaveFormat = waveFormat;
            this.Ext = ext;
            this.LengthSeconds = lengthSeconds;
        }
    }
    public class MetaBuilder
    {
        public string AssetBundleName { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public List<string> LevelAuthors { get; private set; } = new List<string>();
        public List<string> Composers { get; private set; } = new List<string>();
        public int Difficulty { get; private set; }
        public int LeftTrackSize { get; private set; }
        public int RightTrackSize { get; private set; }
        public UnityVector3 NoteAppearPosition { get; private set; }
        public byte[] MusicFile { get; private set; }
        public float Bpm { get; private set; }
        public WaveFormat WaveFormat { get; private set; }
        public string Ext { get; private set; }
        public double LengthSeconds { get; private set; }

        public MetaBuilder SetLengthSeconds(double lengthSeconds)
        {
            LengthSeconds = lengthSeconds;
            return this;
        }

        public MetaBuilder SetAssetBundleName(string assetBundleName)
        {
            AssetBundleName = assetBundleName;
            return this;
        }

        public MetaBuilder SetName(string name)
        {
            Name = name;
            return this;
        }

        public MetaBuilder SetDescription(string description)
        {
            Description = description;
            return this;
        }

        public MetaBuilder SetLevelAuthors(List<string> levelAuthors)
        {
            LevelAuthors = levelAuthors;
            return this;
        }

        public MetaBuilder SetComposers(List<string> composers)
        {
            Composers = composers;
            return this;
        }

        public MetaBuilder SetDifficulty(int difficulty)
        {
            Difficulty = difficulty;
            return this;
        }

        public MetaBuilder SetLeftTrackSize(int leftTrackSize)
        {
            LeftTrackSize = leftTrackSize;
            return this;
        }

        public MetaBuilder SetRightTrackSize(int rightTrackSize)
        {
            RightTrackSize = rightTrackSize;
            return this;
        }

        public MetaBuilder SetNoteAppearPosition(UnityVector3 noteAppearPosition)
        {
            NoteAppearPosition = noteAppearPosition;
            return this;
        }

        public MetaBuilder SetMusicFile(byte[] musicFile)
        {
            this.MusicFile = musicFile;
            return this;
        }

        public MetaBuilder SetBpm(float bpm)
        {
            Bpm = bpm;
            return this;
        }

        public MetaBuilder SetWaveFormat(WaveFormat waveFormat)
        {
            WaveFormat = waveFormat;
            return this;
        }

        public MetaBuilder SetExtensionName(string ext)
        {
            this.Ext = ext;
            return this;
        }

        public Meta Build()
        {
            if (string.IsNullOrEmpty(AssetBundleName))
                throw new InvalidOperationException("AssetBundleName不能为空。");
            if (string.IsNullOrEmpty(Name))
                throw new InvalidOperationException("Name不能为空。");
            if (string.IsNullOrEmpty(Description))
                throw new InvalidOperationException("Description不能为空。");
            if (LevelAuthors == null)
                throw new InvalidOperationException("LevelAuthors不能为空。");
            if (Composers == null)
                throw new InvalidOperationException("Composers不能为空。");
            if (Difficulty == 0)
                throw new InvalidOperationException("未设置Difficulty。");
            if (LeftTrackSize == 0)
                throw new InvalidOperationException("未设置LeftTrackSize。");
            if (RightTrackSize == 0)
                throw new InvalidOperationException("未设置RightTrackSize。");
            if (NoteAppearPosition == null)
                throw new InvalidOperationException("NoteAppearPosition不能为空。");
            if (MusicFile == null)
                throw new InvalidOperationException("MusicFilePath不能为空。");
            if (Bpm == 0)
                throw new InvalidOperationException("未设置Bpm。");
            if (WaveFormat == null)
                throw new InvalidOperationException("未提供Waveformat信息");
            if (string.IsNullOrEmpty(Ext))
                throw new InvalidOperationException("未提供音频格式信息");

            return new Meta(
                AssetBundleName,
                Name,
                Description,
                LevelAuthors.ToArray(),
                Composers.ToArray(),
                Difficulty,
                LeftTrackSize,
                RightTrackSize,
                NoteAppearPosition,
                Bpm,
                MusicFile,
                WaveFormat,
                Ext,
                LengthSeconds
            );
        }
    }
    public class InPlayingEnvironment
    {
        [JsonProperty("leftNotes")] private ExportedNote[] LeftNotes { get; set; }
        [JsonProperty("rightNotes")] private ExportedNote[] RightNotes { get; set; }
        public InPlayingEnvironment(ExportedNote[] leftNotes, ExportedNote[] rightNotes)
        {
            this.LeftNotes = leftNotes;
            this.RightNotes = rightNotes;
        }
        public abstract class ExportedNote
        {
            [JsonProperty("type")] public NoteType Type { get; set; }
            [JsonProperty("strikeTime")] public float StrikeTime { get; set; }
            [JsonProperty("trackIndex")] public int TrackIndex { get; set; }

            public ExportedNote(float strikeTime, int trackIndex)
            {
                this.StrikeTime = strikeTime;
                this.TrackIndex = trackIndex;
            }
            public override string ToString()
            {
                return $"{{StrikeTime:{this.StrikeTime}; track:{this.TrackIndex}}}";
            }
        }
        public class ExportedHitNote : ExportedNote
        {
            public ExportedHitNote(float strikeTime, int trackIndex) : base(strikeTime, trackIndex)
            {
                this.Type = NoteType.Hit;
            }
        }
        public class ExportedHoldNote : ExportedNote
        {
            [JsonProperty("length")] public float Length { get; set; }

            public ExportedHoldNote(float strikeTime, int trackIndex, float length) : base(strikeTime, trackIndex)
            {
                this.Type = NoteType.Hold;
                this.Length = length;
            }
        }

        [JsonIgnore] public static JsonSerializerSettings JsonSerializerSettings
        {
            get
            {
                var setting = new JsonSerializerSettings();
                setting.Converters.Add(new NoteConverter());

                return setting;
            }
        }

        private class NoteConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return typeof(Note).IsAssignableFrom(objectType);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value is ExportedHitNote)
                {
                    var hit = value as ExportedHitNote;
                    serializer.Serialize(writer, hit);
                }
                else
                {
                    var hold = value as ExportedHoldNote;
                    serializer.Serialize(writer, hold);
                }
            }
        }
    }
    public class Level
    {
        [JsonProperty("meta")]
        public Meta Meta { get; set; }
        [JsonProperty("inPlaying")]
        public InPlayingEnvironment InPlaying { get; set; }

        public Level(Meta meta, InPlayingEnvironment notes)
        {
            this.Meta = meta;
            this.InPlaying = notes;
        }
    }
}