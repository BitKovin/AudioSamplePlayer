// AudioFile.cs
using System;
using System.IO;

namespace SimpleAudioPlayer
{
    public class AudioFile
    {
        public string Path { get; set; }
        public string FileName => System.IO.Path.GetFileName(Path);
        public string Description { get; set; }
        public double Duration { get; set; }
        public string DurationString => TimeSpan.FromSeconds(Duration).ToString(@"mm\:ss");
    }
}