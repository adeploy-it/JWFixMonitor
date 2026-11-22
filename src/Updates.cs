using System;
using System.Text.RegularExpressions;

namespace JWFixMonitor
{
    /// <summary>Ultima versione pubblicata su GitHub.</summary>
    internal sealed class Release
    {
        public string Tag;      // es. "v1.2.0"
        public string PageUrl;  // pagina della release, da aprire nel browser
    }

    /// <summary>Lettura della risposta di GitHub e confronto delle versioni (senza rete, testabile).</summary>
    internal static class Updates
    {
        public const string Repo = "adeploy-it/JWFixMonitor";
        public const string LatestApi = "https://api.github.com/repos/" + Repo + "/releases/latest";
        public const string ReleasesPage = "https://github.com/" + Repo + "/releases";

        /// <summary>Legge tag e pagina da releases/latest; null se la risposta non va bene.</summary>
        public static Release Parse(string json)
        {
            var tag = Field(json, "tag_name");
            var page = Field(json, "html_url");
            if (string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(page))
            {
                return null;
            }

            // apro nel browser solo pagine del progetto su GitHub
            if (!page.StartsWith("https://github.com/" + Repo + "/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new Release { Tag = tag, PageUrl = page };
        }

        /// <summary>Vero se il tag (es. "v1.2.0") indica una versione più recente di quella in uso.</summary>
        public static bool IsNewer(string tag, Version current)
        {
            var v = ToVersion(tag);
            return v != null && current != null && Normalize(v) > Normalize(current);
        }

        public static Version ToVersion(string tag)
        {
            var m = Regex.Match(tag ?? "", @"^\s*v?(\d+(?:\.\d+){1,3})");
            return m.Success && Version.TryParse(m.Groups[1].Value, out var v) ? v : null;
        }

        // 1.1 e 1.1.0.0 sono la stessa versione
        private static Version Normalize(Version v) =>
            new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

        private static string Field(string json, string name)
        {
            var m = Regex.Match(json ?? "", "\"" + name + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return m.Success ? Regex.Unescape(m.Groups[1].Value) : null;
        }
    }
}
