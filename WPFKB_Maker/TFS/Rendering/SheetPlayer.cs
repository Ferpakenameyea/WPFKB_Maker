using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Media;
using WPFKB_Maker.TFS.KBBeat;
using WPFKB_Maker.TFS.Sound;

namespace WPFKB_Maker.TFS.Rendering
{
    public class SheetPlayer
    {
        private SheetRenderer Renderer { get; }
        public double CurrentTimeSecondsInPlay => this.timer.Seconds;
        public float Volume { get => device.Volume; set => device.Volume = value; }
        public bool Playing { get; private set; } = false;
        private WasapiOut device;
        private WaveStream waveStream;
        public event EventHandler<StoppedEventArgs> OnPlayBackStopped
        {
            add => device.PlaybackStopped += value;
            remove => device.PlaybackStopped -= value;
        }
        private int[] lastTrigggered = new int[10];
        private StopWatchTimer timer = new StopWatchTimer();
        private Project project;
        public Project Project
        {
            get => project;
            set
            {
                project = value;
                this.waveStream = KBMakerWaveStream.GetWaveStream(project.Meta.Ext, new MemoryStream(project.Meta.MusicFile));
                this.device = new WasapiOut();
                device.Init(this.waveStream);

                Debug.console.Write("播放器已加载项目");
            }
        }
        public SheetPlayer(SheetRenderer renderer)
        {
            this.Renderer = renderer;
            CompositionTarget.Rendering += Update;
        }
        private void Update(object sender, EventArgs e)
        {
            if (!this.Playing)
            {
                return;
            }
            var beat = this.CurrentTimeSecondsInPlay * this.Project.Meta.Bpm / 60;

            this.Renderer.TriggerAbsoluteY = beat * this.Renderer.BitmapHeightPerBeat;
            var triggerRow = this.Renderer.TriggerLineRow;

            int sum = 0;
            var triggering = (from note in this.Renderer.Sheet.Values.AsParallel()
                                where note.BasePosition.Item1 > lastTrigggered[note.BasePosition.Item2] &&
                                    note.BasePosition.Item1 <= triggerRow
                                select note).ToList();
            sum += triggering.Count;
            foreach (var note in triggering)
            {
                lastTrigggered[note.BasePosition.Item2] = Math.Max(
                    lastTrigggered[note.BasePosition.Item2],
                    note.BasePosition.Item1);
            }
            StrikeSoundEffectPlayer.PlayBatch(sum);
        }
        ~SheetPlayer()
        {
            this.device?.Dispose();
        }
        public void Play()
        {
            for (int i = 0; i < Project.Sheet.Column; i++)
            {
                this.lastTrigggered[i] = this.Renderer.TriggerLineRow - 1;
            }
            if (this.Renderer.TriggerAbsoluteY < 0)
            {
                this.Renderer.TriggerAbsoluteY = 0;
            }

            this.Playing = true;
            var beat = this.Renderer.TriggerAbsoluteY / this.Renderer.BitmapHeightPerBeat;
            var timeSeconds = beat * 60.0 / this.Project.Meta.Bpm;
            this.waveStream.CurrentTime = TimeSpan.FromSeconds(timeSeconds);
            this.timer.Reset();
            this.timer.StartFromSeconds = timeSeconds;
            this.timer.Start();
            this.device.Play();
        }
        public void Pause()
        {
            this.Playing = false;
            this.timer.Stop();
            this.device.Pause();
        }
    }

    public class StopWatchTimer
    {
        private Stopwatch stopwatch = new Stopwatch();
        private double start = 0;
        public double StartFromSeconds
        {
            get => start;
            set
            {
                start = value;
                this.stopwatch.Reset();
            }
        }
        public double Seconds { get => StartFromSeconds + stopwatch.Elapsed.TotalMilliseconds / 1000; }
        public void Start() => stopwatch.Start();
        public void Restart() => stopwatch.Restart();
        public void Stop() => stopwatch.Stop();
        public void Reset() => stopwatch.Reset();
    }
}
