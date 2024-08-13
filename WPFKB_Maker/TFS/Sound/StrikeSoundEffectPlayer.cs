using Microsoft.Extensions.ObjectPool;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

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
            player.Volume = 0.3f + (count - 1) * 0.2f;
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
            private IWavePlayer device;
            private WaveFileReader reader;
            private MemoryStream stream;
            private VolumeWaveProvider16 volumeProvider;

            public float Volume { get => volumeProvider.Volume; set => volumeProvider.Volume = value; }

            public StrikePlayer()
            {
                this.device = new WaveOutEvent();
                this.stream = new MemoryStream(data);
                this.reader = new WaveFileReader(stream);
                this.volumeProvider = new VolumeWaveProvider16(reader);
                this.device.Init(this.volumeProvider);
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
            
            public void Play() 
            {
                device.Play();
            }
        }
    }
}
