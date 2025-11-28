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
            // Assuming AudioExtensions is a HashSet<string> for O(1) lookups
            var audioExtensions = AudioExtensions; // Ensure it's a HashSet

            // Custom recursive file enumeration to handle access denied
            IEnumerable<string> GetAllFiles(string root)
            {
                var files = new List<string>();
                var directories = new Stack<string>();
                directories.Push(root);

                while (directories.Count > 0)
                {
                    string currentDir = directories.Pop();
                    try
                    {
                        // Add files in current directory
                        files.AddRange(Directory.GetFiles(currentDir, "*.*")
                                               .Where(file => audioExtensions.Contains(Path.GetExtension(file).ToLowerInvariant())));

                        // Enqueue subdirectories
                        foreach (var subDir in Directory.GetDirectories(currentDir))
                        {
                            directories.Push(subDir);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip inaccessible directories
                        continue;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        // Skip if directory was deleted or not found
                        continue;
                    }
                    catch (IOException ex)
                    {
                        // Handle other IO errors if needed, or log
                        Console.WriteLine($"IO error in {currentDir}: {ex.Message}");
                        continue;
                    }
                }
                return files;
            }

            var filePaths = GetAllFiles(directory);

            // Process files in parallel with limited concurrency
            var audioFiles = new List<AudioFile>();
            var lockObject = new object(); // For thread-safe addition

            Parallel.ForEach(filePaths, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount / 2 }, filePath =>
            {
                string description = "";
                double duration = 0;
                string ext = Path.GetExtension(filePath).ToLowerInvariant();

                try
                {
                    using (var tagFile = TagLib.File.Create(filePath))
                    {
                        description = tagFile.Tag.Comment ?? "";
                        duration = tagFile.Properties.Duration.TotalSeconds;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip access denied files
                    return;
                }
                catch (IOException ex)
                {
                    // Handle or log IO errors
                    Console.WriteLine($"Error processing {filePath}: {ex.Message}");
                    return;
                }
                catch (Exception ex)
                {
                    // General catch for other exceptions, like corrupted files
                    Console.WriteLine($"Unexpected error in {filePath}: {ex.Message}");
                    return;
                }

                if (string.IsNullOrEmpty(description) && ext == ".wav")
                {
                    try
                    {
                        description = GetBextDescription(filePath);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip if access denied
                        return;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error getting BEXT for {filePath}: {ex.Message}");
                    }
                }

                var audioFile = new AudioFile { Path = filePath, Description = description, Duration = duration };

                lock (lockObject)
                {
                    audioFiles.Add(audioFile);
                }
            });

            return audioFiles;
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