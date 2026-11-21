using System.Globalization;
using Xunit;

// la lingua (L.Lang) è condivisa: i test non vanno in parallelo
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace JWFixMonitor.Tests
{
    public class StringsTests
    {
        [Theory]
        [InlineData("it-IT", "it")]
        [InlineData("it-CH", "it")]
        [InlineData("es-ES", "es")]
        [InlineData("es-MX", "es")]
        [InlineData("en-US", "en")]
        [InlineData("fr-FR", "en")]   // lingua non tradotta: inglese
        [InlineData("", "en")]
        public void Language_follows_windows_with_english_fallback(string culture, string expected)
        {
            Assert.Equal(expected, L.LanguageFor(culture.Length == 0 ? CultureInfo.InvariantCulture : new CultureInfo(culture)));
        }

        [Fact]
        public void Every_text_exists_in_all_three_languages()
        {
            foreach (var lang in new[] { "it", "en", "es" })
            {
                L.Lang = lang;
                foreach (var key in L.Keys)
                {
                    Assert.False(string.IsNullOrWhiteSpace(L.T(key)), $"{key} vuoto in {lang}");
                }
            }

            L.Lang = "it";
        }

        [Fact]
        public void Texts_change_with_language_and_keep_placeholders()
        {
            L.Lang = "en";
            Assert.Equal("Exit", L.T("Exit"));
            Assert.Equal("Screen 2 — 1920×1080 (primary)", new MonitorDesc
            {
                Number = 2, Bounds = new Box(0, 0, 1920, 1080), IsPrimary = true
            }.Label);

            L.Lang = "es";
            Assert.Equal("Salir", L.T("Exit"));
            Assert.Contains("x", L.F("SendFailed", "x"));

            L.Lang = "it";
            Assert.Equal("Esci", L.T("Exit"));
        }
    }
}
