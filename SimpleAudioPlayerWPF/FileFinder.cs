// FileFinder.cs
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TagLib;

namespace SimpleAudioPlayer
{
    public static class FileFinder
    {
        private static readonly string[] AudioExtensions = { ".mp3", ".wav", ".ogg", ".flac", ".aac" };

        public static List<AudioFile> FindAudioFiles(string directory)
        {
            var files = new List<AudioFile>();
            foreach (var filePath in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(filePath).ToLower();
                if (AudioExtensions.Contains(ext))
                {
                    string description = "";
                    double duration = 0;
                    try
                    {
                        using (var tagFile = TagLib.File.Create(filePath))
                        {
                            description = tagFile.Tag.Comment ?? "";
                            duration = tagFile.Properties.Duration.TotalSeconds;
                        }
                    }
                    catch
                    {
                        // Skip
                    }

                    if (string.IsNullOrEmpty(description) && ext == ".wav")
                    {
                        description = GetBextDescription(filePath);
                    }

                    files.Add(new AudioFile { Path = filePath, Description = description, Duration = duration });
                }
            }
            return files;
        }

        private static string GetBextDescription(string filePath)
        {
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(fs))
                {
                    if (new string(reader.ReadChars(4)) != "RIFF") return "";
                    reader.ReadUInt32(); // fileSize
                    if (new string(reader.ReadChars(4)) != "WAVE") return "";

                    while (fs.Position < fs.Length)
                    {
                        string chunkId = new string(reader.ReadChars(4));
                        if (string.IsNullOrEmpty(chunkId) || chunkId.Length < 4) break;
                        uint chunkSize = reader.ReadUInt32();

                        if (chunkId == "bext")
                        {
                            char[] descChars = reader.ReadChars(256);
                            return new string(descChars).TrimEnd('\0');
                        }

                        long skip = chunkSize + (chunkSize % 2 == 1 ? 1 : 0);
                        fs.Seek(skip, SeekOrigin.Current);
                    }
                }
            }
            catch
            {
                // Ignore
            }
            return "";
        }
    }
}