using System;
using System.IO;

namespace JWFixMonitor
{
    internal static class Log
    {
        private const long MaxBytes = 512 * 1024;
        private static readonly object Sync = new object();

        public static string FilePath => Path.Combine(Settings.Folder, "log.txt");

        public static void Write(string message)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(Settings.Folder);

                    var info = new FileInfo(FilePath);
                    if (info.Exists && info.Length > MaxBytes)
                    {
                        var old = Path.Combine(Settings.Folder, "log.old.txt");
                        File.Delete(old);
                        File.Move(FilePath, old);
                    }

                    File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // il log non deve mai far cadere il programma
            }
        }
    }
}
