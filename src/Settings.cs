using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JWFixMonitor
{
    internal sealed class Settings
    {
        public bool Enabled = true;
        public bool TopMost = true;
        public bool Notifications = true;
        public string TargetDevice = "";   // vuoto (e niente percorso/modello) = automatico
        public string TargetPath = "";     // percorso del monitor scelto
        public string TargetModel = "";    // modello del monitor scelto
        public int TargetWidth, TargetHeight;

        public MonitorChoice Choice
        {
            get => new MonitorChoice
            {
                Device = TargetDevice ?? "", Path = TargetPath ?? "", Model = TargetModel ?? "",
                Width = TargetWidth, Height = TargetHeight
            };
            set
            {
                var c = value ?? MonitorChoice.Auto;
                TargetDevice = c.Device; TargetPath = c.Path; TargetModel = c.Model;
                TargetWidth = c.Width; TargetHeight = c.Height;
            }
        }

        public Settings Clone() => (Settings)MemberwiseClone();

        public static string Folder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JWFixMonitor");

        private static string FilePath => Path.Combine(Folder, "settings.ini");

        public static Settings Load()
        {
            try
            {
                return File.Exists(FilePath) ? Parse(File.ReadAllText(FilePath, Encoding.UTF8)) : new Settings();
            }
            catch (Exception ex)
            {
                Log.Write("Impostazioni non leggibili, uso i valori predefiniti: " + ex.Message);
                return new Settings();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(FilePath, Serialize(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log.Write("Impossibile salvare le impostazioni: " + ex.Message);
            }
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Enabled=" + Enabled);
            sb.AppendLine("TopMost=" + TopMost);
            sb.AppendLine("Notifications=" + Notifications);
            sb.AppendLine("TargetDevice=" + TargetDevice);
            sb.AppendLine("TargetPath=" + TargetPath);
            sb.AppendLine("TargetModel=" + TargetModel);
            sb.AppendLine("TargetSize=" + TargetWidth + "x" + TargetHeight);
            return sb.ToString();
        }

        public static Settings Parse(string text)
        {
            var s = new Settings();
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in (text ?? "").Split('\n'))
            {
                var line = raw.Trim();
                var eq = line.IndexOf('=');
                if (line.Length == 0 || line.StartsWith("#") || eq <= 0)
                {
                    continue;
                }

                values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }

            bool b(string key, bool def) =>
                values.TryGetValue(key, out var v) && bool.TryParse(v, out var r) ? r : def;

            s.Enabled = b("Enabled", s.Enabled);
            s.TopMost = b("TopMost", s.TopMost);
            s.Notifications = b("Notifications", s.Notifications);
            string str(string key) => values.TryGetValue(key, out var v) ? v : "";
            s.TargetDevice = str("TargetDevice");
            s.TargetPath = str("TargetPath");
            s.TargetModel = str("TargetModel");

            var size = str("TargetSize").Split('x');
            if (size.Length == 2 && int.TryParse(size[0], out var w) && int.TryParse(size[1], out var h) && w > 0 && h > 0)
            {
                s.TargetWidth = w;
                s.TargetHeight = h;
            }

            return s;
        }
    }
}
