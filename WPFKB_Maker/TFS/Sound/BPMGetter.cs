using NAudio.Wave;
using SoundTouch;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace WPFKB_Maker.TFS.Sound
{
    public class BPMGetter
    {
        private readonly Stream stream;
        private readonly string ext;
        private byte[] bytebuffer = new byte[4096];
        private float[] floatbuffer = new float[1024];
        
        
        
        public BPMGetter(string path) : 
            this(new FileStream(path, FileMode.Open), new FileInfo(path).Extension)
        {}

        public BPMGetter(Stream stream, string ext)
        {
            this.stream = stream;
            this.ext = ext;
        }

        public async Task<float> Run()
        {
            var file = WaveStreamFactory.GetWaveStream(stream, ext);
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
        public static WaveStream GetWaveStream(Stream stream, string ext)
        {
            switch(ext)
            {
                case ".mp3":
                    return new Mp3FileReader(stream);
                case ".wav":
                    return new WaveFileReader(stream);
                default:
                    throw new NotSupportedException($"string format of {ext} is not supported");
            }
        }
    }
}
