using Microsoft.Extensions.ObjectPool;
using NAudio.Wave;
using System.IO;
using System.Runtime.CompilerServices;

namespace WPFKB_Maker.TFS.Sound
{
    public static class StrikeSoundEffectPlayer
    {
        private static ObjectPool<StrikePlayer> players;
        private static byte[] data;

        public static void Play()
        {
            players.Get().Play();
        }

        public static void Initialize()
        {
            data = File.ReadAllBytes("./strike.wav");
            players = new DefaultObjectPool<StrikePlayer>(
                new DefaultPooledObjectPolicy<StrikePlayer>(),
                40);
        }

        private class StrikePlayer
        {
            private WaveOutEvent waveout;
            private WaveFileReader reader;
            private MemoryStream stream;

            public StrikePlayer()
            {
                this.waveout = new WaveOutEvent();
                this.stream = new MemoryStream(data);
                this.reader = new WaveFileReader(stream);
                this.waveout.Init(reader);
                this.waveout.PlaybackStopped += (sender, e) =>
                {
                    this.stream.Position = 0;
                    players.Return(this);
                };
            }

            ~StrikePlayer()
            {
                this.waveout.Dispose();
                this.stream.Dispose();
                this.reader.Dispose();
            }

            public void Play() => waveout.Play();
        }
    }
}
