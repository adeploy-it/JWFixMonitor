using System;
using Xunit;

namespace JWFixMonitor.Tests
{
    public class UpdatesTests
    {
        [Theory]
        [InlineData("v1.2.0", "1.1.0", true)]
        [InlineData("1.1.1", "1.1.0", true)]
        [InlineData("v2.0", "1.9.9", true)]
        [InlineData("v1.1.0", "1.1.0", false)]
        [InlineData("v1.1", "1.1.0.0", false)]
        [InlineData("v1.0.9", "1.1.0", false)]
        [InlineData("v1.2.0-beta", "1.1.0", true)]
        [InlineData("", "1.1.0", false)]
        [InlineData("latest", "1.1.0", false)]
        public void Newer_version_is_detected(string tag, string current, bool expected)
        {
            Assert.Equal(expected, Updates.IsNewer(tag, new Version(current)));
        }

        [Fact]
        public void Release_json_is_read()
        {
            const string json = "{\"url\":\"https://api.github.com/x\",\"html_url\":\"https://github.com/adeploy-it/JWFixMonitor/releases/tag/v1.2.0\"," +
                                "\"tag_name\":\"v1.2.0\",\"draft\":false,\"prerelease\":false," +
                                "\"assets\":[{\"browser_download_url\":\"https://github.com/adeploy-it/JWFixMonitor/releases/download/v1.2.0/JWFixMonitor.exe\"}]}";

            var r = Updates.Parse(json);

            Assert.NotNull(r);
            Assert.Equal("v1.2.0", r.Tag);
            Assert.Equal("https://github.com/adeploy-it/JWFixMonitor/releases/tag/v1.2.0", r.PageUrl);
        }

        [Fact]
        public void Release_page_outside_github_is_ignored()
        {
            const string json = "{\"html_url\":\"https://evil.example/x\",\"tag_name\":\"v9.0.0\"}";

            Assert.Null(Updates.Parse(json));
        }

        [Theory]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("{\"message\":\"Not Found\"}")]
        public void Bad_json_gives_nothing(string json)
        {
            Assert.Null(Updates.Parse(json));
        }
    }
}
