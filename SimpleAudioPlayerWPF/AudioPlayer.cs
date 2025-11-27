// AudioPlayer.cs
using System;
using System.Threading.Tasks;
using NAudio.Wave;

namespace SimpleAudioPlayer
{
    public class AudioPlayer
    {
        private IWavePlayer wavePlayer;
        private AudioFileReader audioFileReader;
        private bool isPaused;
        private long pausedPosition;

        public event EventHandler PlaybackStopped;

        public bool IsPlaying => wavePlayer != null && wavePlayer.PlaybackState == PlaybackState.Playing;
        public bool IsPaused => isPaused;
        public double CurrentTime => audioFileReader?.CurrentTime.TotalSeconds ?? 0;
        public double TotalTime => audioFileReader?.TotalTime.TotalSeconds ?? 0;

        public void Play(string filePath)
        {
            Stop();
            audioFileReader = new AudioFileReader(filePath);
            wavePlayer = new WaveOutEvent();
            wavePlayer.Init(audioFileReader);
            wavePlayer.PlaybackStopped += OnPlaybackStopped;
            wavePlayer.Play();
            isPaused = false;
        }

        public void PlaySegment(string filePath, double startSeconds, double endSeconds)
        {
            Stop();
            audioFileReader = new AudioFileReader(filePath);
            audioFileReader.CurrentTime = TimeSpan.FromSeconds(startSeconds);
            double total = audioFileReader.TotalTime.TotalSeconds;
            if (startSeconds >= total) return;
            double segmentLength = Math.Min(endSeconds - startSeconds, total - startSeconds);
            wavePlayer = new WaveOutEvent();
            wavePlayer.Init(audioFileReader);
            wavePlayer.PlaybackStopped += OnPlaybackStopped;
            wavePlayer.Play();
            isPaused = false;
            Task.Delay((int)(segmentLength * 1000)).ContinueWith(t => Stop());
        }

        public void Pause()
        {
            if (IsPlaying)
            {
                wavePlayer.Pause();
                pausedPosition = audioFileReader.Position;
                isPaused = true;
            }
        }

        public void Resume()
        {
            if (isPaused)
            {
                audioFileReader.Position = pausedPosition;
                wavePlayer.Play();
                isPaused = false;
            }
        }

        public void Stop()
        {
            if (wavePlayer != null)
            {
                wavePlayer.Stop();
                wavePlayer.Dispose();
                wavePlayer = null;
            }
            if (audioFileReader != null)
            {
                audioFileReader.Dispose();
                audioFileReader = null;
            }
            isPaused = false;
            pausedPosition = 0;
        }

        public void Seek(double timeSeconds)
        {
            if (audioFileReader != null)
            {
                audioFileReader.CurrentTime = TimeSpan.FromSeconds(Math.Clamp(timeSeconds, 0, TotalTime));
            }
        }

        public void SetVolume(float volume)
        {
            if (audioFileReader != null)
            {
                audioFileReader.Volume = volume;
            }
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
            Stop();
        }
    }
}