using NAudio.Wave;
using SoundTouch;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace WPFKB_Maker.TFS.Sound
{
    public class BPMGetter
    {
        private readonly string path;
        private byte[] bytebuffer = new byte[4096];
        private float[] floatbuffer = new float[1024];
        public BPMGetter(string path)
        {
            this.path = path;
        }

        public async Task<float> Run()
        {
            var file = WaveStreamFactory.GetWaveStream(path);
            var inputStream = new WaveChannel32(file);
            inputStream.PadWithZeroes = false;
            var channel = inputStream.WaveFormat.Channels;
            return await Task.Run(() =>
            {
                using (var detect = new BPMDetect(channel, inputStream.WaveFormat.SampleRate))
                {
                    while (true)
                    {
                        int nbytes = inputStream.Read(bytebuffer, 0, bytebuffer.Length);
                        if (nbytes == 0)
                        {
                            break;
                        }

                        Buffer.BlockCopy(bytebuffer, 0, floatbuffer, 0, nbytes);
                        detect.PutSamples(floatbuffer, (uint)(nbytes / 4 / channel));
                    }
                    return detect.Bpm;
                }
            });
        }
    }
    public static class WaveStreamFactory
    {
        public static WaveStream GetWaveStream(string path)
        {
            FileInfo fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException($"file with path {path} not found");
            }

            switch(fileInfo.Extension)
            {
                case ".mp3":
                    return new Mp3FileReader(path);
                case ".wav":
                    return new WaveFileReader(path);
                default:
                    throw new NotSupportedException($"string format of {fileInfo.Extension} is not supported");
            }
        }
    }
}
