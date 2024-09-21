using Microsoft.Extensions.ObjectPool;
using CSCore;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CSCore.SoundOut;
using CSCore.Codecs.WAV;
using System;

namespace WPFKB_Maker.TFS.Sound
{
    public static class StrikeSoundEffectPlayer
    {
        private static ObjectPool<StrikePlayer> players;
        private static List<StrikePlayer> playerList = new List<StrikePlayer>();
        private static byte[] data;
        private const int preload = 40;
        public static float Volume { get; set; } = 1.0f;

        public static void Play()
        {
            players.Get().Play();
        }

        public static void PlayBatch(int count)
        {
            var player = players.Get();
            player.Volume = Math.Min(1.0f, 0.3f + (count - 1) * 0.2f);
            player.Play();
        }

        public static async void Initialize()
        {
            try
            {
                await Task.Run(() =>
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
                });
            }
            catch (System.Exception e)
            {
                MessageBox.Show($"音频系统启动失败：{e}");
            }
        }

        private class StrikePlayerPool : ObjectPool<StrikePlayer>
        {
            private Queue<StrikePlayer> playerList = new Queue<StrikePlayer>();
            public int Size { get => playerList.Count; }

            public override StrikePlayer Get()
            {
                return playerList.Count > 0 ? playerList.Dequeue() : new StrikePlayer();
            }

            public override void Return(StrikePlayer obj)
            {
                playerList.Enqueue(obj);
            }
        }

        private class StrikePlayer
        {
            private ISoundOut device;
            private IWaveSource reader;
            private MemoryStream stream;

            public float Volume { get => this.device.Volume; set => this.device.Volume = value; }
            public StrikePlayer()
            {
                this.device = new WasapiOut(
                    eventSync: true, 
                    shareMode: CSCore.CoreAudioAPI.AudioClientShareMode.Shared, 
                    latency: 30);

                this.stream = new MemoryStream(data);
                this.reader = new WaveFileReader(this.stream)
                    .ToSampleSource()
                    .ToWaveSource();
                this.device.Initialize(this.reader);
                this.device.Stopped += (sender, e) =>
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
            
            public void Play() 
            {
                device.Play();
            }
        }
    }
}
