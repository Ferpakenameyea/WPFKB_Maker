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
        [JsonIgnore]
        public byte[] MusicFile { get; set; }

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
                byte[] musicFile
            )
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
                MusicFile
            );
        }
    }
    public class InPlayingEnvironment
    {
        [JsonProperty("leftNotes")] private Note[] LeftNotes { get; set; }
        [JsonProperty("rightNotes")] private Note[] RightNotes { get; set; }
        public InPlayingEnvironment(Note[] leftNotes, Note[] rightNotes)
        {
            this.LeftNotes = leftNotes;
            this.RightNotes = rightNotes;
        }
        public struct Note
        {
            [JsonProperty("strikeTime")] public float StrikeTime;
            [JsonProperty("trackIndex")] public int TrackIndex;


            public Note(float strikeTime, int trackIndex)
            {
                this.StrikeTime = strikeTime;
                this.TrackIndex = trackIndex;
            }
            public override string ToString()
            {
                return $"{{StrikeTime:{this.StrikeTime}; track:{this.TrackIndex}}}";
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