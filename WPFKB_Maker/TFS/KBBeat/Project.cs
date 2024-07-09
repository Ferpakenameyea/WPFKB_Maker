using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace WPFKB_Maker.TFS.KBBeat
{
    public class Project
    {
        public static Project Current { get; set; } = null;
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
            foreach (var entry in entries)
            {
                using (var stream = entry.Open())
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await stream.CopyToAsync(memoryStream);
                        buffer = memoryStream.ToArray();
                    }
                }
                await Task.Run(() =>
                {
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
                });
            }
            if (metaBuffer == null || sheetBuffer == null || metaBuffer == null)
            {
                throw new Exception("项目文件不全，项目可能被人为损坏");
            }
            metaBuffer.MusicFile = musicBuffer;

            return new Project(metaBuffer, sheetBuffer);
        }
    }
}
