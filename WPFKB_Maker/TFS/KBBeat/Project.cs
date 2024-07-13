using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using WPFKB_Maker.TFS.Sound;

namespace WPFKB_Maker.TFS.KBBeat
{
    public class Project
    {
        public static Project Current
        {
            get => ObservableCurrentProject.Value;
            set => ObservableCurrentProject.Value = value;
        }
        public static ObservableObject<Project> ObservableCurrentProject { get; }
            = new ObservableObject<Project>(null);
        public Meta Meta { get; set; }
        public Sheet Sheet { get; set; }
        [JsonIgnore] public string SavingPath { get; set; } = null;

        public Project(Meta meta, Sheet sheet, string savingPath)
        {
            Meta = meta;
            Sheet = sheet;
            this.SavingPath = savingPath;
        }
        private Project(Meta meta, Sheet sheet)
        {
            Meta = meta;
            Sheet = sheet;
        }
        public static async Task SaveNew(Project project, string savepath = null)
        {
            if (savepath == null)
            {
                savepath = project.SavingPath;
            }
            else
            {
                project.SavingPath = savepath;
            }
           
            Debug.console.Write($"正在创建项目文件 {savepath}");
            using (var stream = new FileStream(savepath, FileMode.Create))
            {
                stream.Seek(0, SeekOrigin.Begin);

                using (var zipArchive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    await SaveToZipNew(zipArchive, project)
                        .ConfigureAwait(true);
                }
            }
        }
        private static async Task SaveToZipNew(ZipArchive zipArchive, Project project)
        {
            byte[] metaByte = Array.Empty<byte>(), sheetByte = Array.Empty<byte>();
            Debug.console.Write("正在序列化信息……");
            await Task.Run(() =>
            {
                var metaJson = JsonConvert.SerializeObject(project.Meta);
                var sheetJson = JsonConvert.SerializeObject(project.Sheet, Sheet.SheetJsonSerializerSettings);
                metaByte = Encoding.UTF8.GetBytes(metaJson);
                sheetByte = Encoding.UTF8.GetBytes(sheetJson);
            }).ConfigureAwait(true);
            Debug.console.Write("正在写入文件……");
            var metaEntry = zipArchive.CreateEntry("meta");
            using (var stream = metaEntry.Open())
            {
                await stream.WriteAsync(metaByte, 0, metaByte.Length)
                    .ConfigureAwait(true);
            }
            var sheetEntry = zipArchive.CreateEntry("sheet");
            using (var stream = sheetEntry.Open())
            {
                await stream.WriteAsync(sheetByte, 0, sheetByte.Length)
                    .ConfigureAwait(true);
            }
            var musicEntry = zipArchive.CreateEntry("music");
            using (var stream = musicEntry.Open())
            {
                await stream.WriteAsync(project.Meta.MusicFile, 0, project.Meta.MusicFile.Length)
                    .ConfigureAwait(true);
            }
            Debug.console.Write("保存成功");
        }
        public async static Task<Project> LoadProjectFromFile(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open))
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    try
                    {
                        var project = await LoadProjectFromZip(archive);
                        project.SavingPath = path;
                        return project;
                    }
                    catch (Exception err)
                    {
                        MessageBox.Show($"项目加载失败：{err}");
                        Debug.console.Write(err);
                        return null;
                    }
                }
            }
        }
        private static async Task<Project> LoadProjectFromZip(ZipArchive zipArchive)
        {
            var entries = zipArchive.Entries;
            Meta metaBuffer = null;
            Sheet sheetBuffer = null;
            byte[] musicBuffer = null;
            byte[] buffer = null;

            await Task.Run(() =>
            {
                foreach (var entry in entries)
                {
                    using (var stream = entry.Open())
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            stream.CopyTo(memoryStream);
                            buffer = memoryStream.ToArray();
                        }
                    }
                    switch (entry.Name)
                    {
                        case "meta":
                            metaBuffer = JsonConvert.DeserializeObject<Meta>(Encoding.UTF8.GetString(buffer));
                            break;
                        case "sheet":
                            sheetBuffer = JsonConvert.DeserializeObject<Sheet>(
                                Encoding.UTF8.GetString(buffer),
                                Sheet.SheetJsonSerializerSettings);
                            break;
                        case "music":
                            musicBuffer = buffer;
                            break;
                    }
                }
                if (metaBuffer == null || sheetBuffer == null || metaBuffer == null)
                {
                    throw new Exception("项目文件不全，项目可能被人为损坏");
                }
                metaBuffer.MusicFile = musicBuffer;
                using (var memoryStream = new MemoryStream(musicBuffer))
                {
                    using (var waveStream = KBMakerWaveStream.GetWaveStream(metaBuffer.Ext, memoryStream))
                    {
                        metaBuffer.WaveFormat = waveStream.WaveFormat;
                    }
                }
            });
            return new Project(metaBuffer, sheetBuffer);
        }
        public static async Task SaveProjectAsKBBeatPackageAsync(Project project, string savepath)
        {
            Level levelPart = Export(project);
            byte[] music = project.Meta.MusicFile;
            DirectoryInfo dir = new DirectoryInfo(savepath);
            if (!dir.Exists)
            {
                dir.Create();
            }
            Task text, audio;

            FileInfo
                inplayingFile = new FileInfo(savepath + "\\" + "inPlaying.json"),
                metaFile = new FileInfo(savepath + "\\" + "meta.json");

            text = Task.Run(async () =>
            {
                var inplaying = JsonConvert.SerializeObject(levelPart.InPlaying, InPlayingEnvironment.JsonSerializerSettings);
                var meta = JsonConvert.SerializeObject(levelPart.Meta);
                Task t1, t2;
                
                using (var stream1 = inplayingFile.Open(FileMode.Create))
                using (var stream2 = metaFile.Open(FileMode.Create))
                {
                    var bytes1 = Encoding.UTF8.GetBytes(inplaying);
                    t1 = stream1.WriteAsync(bytes1, 0, bytes1.Length);

                    var bytes2 = Encoding.UTF8.GetBytes(meta);
                    t2 = t1 = stream2.WriteAsync(bytes2, 0, bytes2.Length);
                    
                    await Task.WhenAll(t1, t2);
                }
            });
            string tempPath = "./temp" + project.Meta.Ext;

            using (var tempStream = File.Create(tempPath))
            {
                await tempStream.WriteAsync(music, 0, music.Length);
            }
            audio = OggTransformer.TransformToOGGAsync(tempPath, savepath + "\\" + "mus.ogg");

            await Task.WhenAll(text, audio);
        }
        private static Level Export(Project kbmakerProject)
        {
            InPlayingEnvironment env;
            Meta meta;

            meta = kbmakerProject.Meta;
            List<InPlayingEnvironment.ExportedNote>
                left = new List<InPlayingEnvironment.ExportedNote>(),
                right = new List<InPlayingEnvironment.ExportedNote>();

            float timePerRow = 60.0f / (meta.Bpm * 24f);

            kbmakerProject.Sheet.Values.AsParallel()
                .ForAll(note =>
                {
                    InPlayingEnvironment.ExportedNote exportedNote;

                    float strikeTime = note.BasePosition.Item1 * timePerRow;

                    if (note is HitNote)
                    {
                        var hit = note as HitNote;
                        exportedNote = new InPlayingEnvironment.ExportedHitNote(strikeTime, hit.Position.Item2);
                    }
                    else
                    {
                        var hold = note as HoldNote;
                        exportedNote = new InPlayingEnvironment.ExportedHoldNote(
                            strikeTime,
                            hold.BasePosition.Item2,
                            hold.End.Item1 * timePerRow - strikeTime
                        );
                    }

                    if (note.BasePosition.Item2 < meta.LeftTrackSize)
                    {
                        lock (left)
                        {
                            left.Add(exportedNote);
                        }
                    }
                    else
                    {
                        exportedNote.TrackIndex -= meta.LeftTrackSize;
                        lock (right)
                        {
                            right.Add(exportedNote);
                        }
                    }
                });

            left.Sort((n1, n2) => n1.StrikeTime.CompareTo(n2.StrikeTime));
            right.Sort((n1, n2) => n1.StrikeTime.CompareTo(n2.StrikeTime));

            env = new InPlayingEnvironment(left.ToArray(), right.ToArray());

            return new Level(meta, env);
        }
    }

    public class ObservableObject<T> : INotifyPropertyChanged
    {
        private T value;
        public event PropertyChangedEventHandler PropertyChanged;
        
        public ObservableObject(T initialValue) {
            this.value = initialValue;
        }
    
        public T Value
        {
            get => value;
            set
            {
                this.value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.value)));
            }
        }
    }
}
