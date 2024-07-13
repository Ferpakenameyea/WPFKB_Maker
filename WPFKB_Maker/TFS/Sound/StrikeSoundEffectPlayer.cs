using Microsoft.Extensions.ObjectPool;
using NAudio.Wave;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Documents;

namespace WPFKB_Maker.TFS.Sound
{
    public static class StrikeSoundEffectPlayer
    {
        private static ObjectPool<StrikePlayer> players;
        private static byte[] data;
        private const int preload = 40;
        private static Queue<StrikePlayer> queue = new Queue<StrikePlayer>();

        public static void Play()
        {
            players.Get().Play();
        }

        public static void PlayBatch(int count)
        {
            for (int i = 0; i < count; i++)
            {
                queue.Enqueue(players.Get());
            }
            while(queue.Count > 0)
            {
                queue.Dequeue().Play();
            }
        }

        public static void Initialize()
        {
            data = File.ReadAllBytes("./strike.wav");
            players = new DefaultObjectPool<StrikePlayer>(
                new DefaultPooledObjectPolicy<StrikePlayer>());
            List<StrikePlayer> preloadPlayers = new List<StrikePlayer>();
            for (int i = 0; i < preload; i++)
            {
                preloadPlayers.Add(players.Get());
            }
            foreach (var preloaded in preloadPlayers)
            {
                players.Return(preloaded);
            }
        }

        private class StrikePlayer
        {
            private WasapiOut device;
            private WaveFileReader reader;
            private MemoryStream stream;

            public StrikePlayer()
            {
                this.device = new WasapiOut();
                this.stream = new MemoryStream(data);
                this.reader = new WaveFileReader(stream);
                this.device.Init(reader);
                this.device.PlaybackStopped += (sender, e) =>
                {
                    this.stream.Position = 0;
                    players.Return(this);
                };
            }

            ~StrikePlayer()
            {
                this.device.Dispose();
                this.stream.Dispose();
                this.reader.Dispose();
            }

            public void Play() => device.Play();
        }
    }
}
