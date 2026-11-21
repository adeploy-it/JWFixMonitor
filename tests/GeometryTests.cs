using System.Collections.Generic;
using Xunit;

namespace JWFixMonitor.Tests
{
    public class GeometryTests
    {
        private static MonitorDesc M(string dev, int l, int t, int w, int h, bool primary = false) =>
            new MonitorDesc { Device = dev, Bounds = new Box(l, t, l + w, t + h), IsPrimary = primary };

        private static List<MonitorDesc> PcAndTv() => Geometry.Number(new[]
        {
            M(@"\\.\DISPLAY1", 0, 0, 1920, 1080, primary: true),
            M(@"\\.\DISPLAY2", 1920, 0, 3840, 2160),
        });

        [Fact]
        public void Insets_from_dwm_frame()
        {
            var wr = new Box(1912, -1, 5768, 2168);
            var visible = new Box(1920, 0, 5760, 2160);
            var i = Geometry.Insets(wr, visible);
            Assert.Equal(new[] { 8, 1, 8, 8 }, new[] { i.Left, i.Top, i.Right, i.Bottom });
        }

        [Fact]
        public void Border_pushed_off_screen_and_clipped_to_monitor()
        {
            // TV 1920x1080 a destra del PC, bordo Windows 11 da 1px, bordi invisibili 7/0/7/7
            var tv = new Box(1920, 0, 3840, 1080);
            var want = Geometry.TargetWindowRect(Geometry.Grow(tv, 1), new Box(7, 0, 7, 7));
            Assert.Equal(new[] { 1912, -1, 3848, 1088 }, new[] { want.Left, want.Top, want.Right, want.Bottom });

            var clip = Geometry.RelativeTo(tv, want);
            Assert.Equal(new[] { 8, 1, 1928, 1081 }, new[] { clip.Left, clip.Top, clip.Right, clip.Bottom });
        }

        [Fact]
        public void Insets_default_when_dwm_missing_or_absurd()
        {
            var wr = new Box(0, 0, 100, 100);
            Assert.Equal(Geometry.DefaultInsets.Left, Geometry.Insets(wr, null).Left);
            Assert.Equal(Geometry.DefaultInsets.Top, Geometry.Insets(wr, new Box(500, 500, 600, 600)).Top);
        }

        [Fact]
        public void Target_rect_makes_visible_frame_cover_monitor()
        {
            var tv = PcAndTv()[1].Bounds;
            var insets = new Box(8, 1, 8, 8);
            var want = Geometry.TargetWindowRect(tv, insets);
            Assert.Equal(1912, want.Left);
            Assert.Equal(-1, want.Top);
            Assert.Equal(3840 + 16, want.Width);
            Assert.Equal(2160 + 9, want.Height);

            // la cornice visibile risultante coincide con lo schermo
            var visible = new Box(want.Left + 8, want.Top + 1, want.Right - 8, want.Bottom - 8);
            Assert.True(Geometry.Matches(visible, tv));
        }

        [Fact]
        public void Half_screen_window_does_not_match()
        {
            var tv = PcAndTv()[1].Bounds;
            Assert.False(Geometry.Matches(new Box(1920, 0, 1920 + 1920, 1080), tv));
        }

        [Fact]
        public void Choose_preferred_when_connected()
        {
            var m = Geometry.ChooseMonitor(PcAndTv(), @"\\.\DISPLAY1", @"\\.\DISPLAY2");
            Assert.Equal(@"\\.\DISPLAY1", m.Device);
        }

        [Fact]
        public void Choose_auto_secondary_when_preferred_missing()
        {
            var m = Geometry.ChooseMonitor(PcAndTv(), @"\\.\DISPLAY7", @"\\.\DISPLAY1");
            Assert.Equal(@"\\.\DISPLAY2", m.Device);
        }

        [Fact]
        public void Choose_none_with_single_monitor()
        {
            var one = Geometry.Number(new[] { M(@"\\.\DISPLAY1", 0, 0, 1920, 1080, true) });
            Assert.Null(Geometry.ChooseMonitor(one, "", @"\\.\DISPLAY1"));
        }

        [Fact]
        public void Choose_current_secondary_among_many()
        {
            var three = Geometry.Number(new[]
            {
                M(@"\\.\DISPLAY1", 0, 0, 1920, 1080, true),
                M(@"\\.\DISPLAY2", 1920, 0, 1920, 1080),
                M(@"\\.\DISPLAY3", -1920, 0, 1920, 1080),
            });
            Assert.Equal(@"\\.\DISPLAY2", Geometry.ChooseMonitor(three, "", @"\\.\DISPLAY2").Device);
            // finestra sul principale: primo secondario da sinistra
            Assert.Equal(@"\\.\DISPLAY3", Geometry.ChooseMonitor(three, "", @"\\.\DISPLAY1").Device);
            Assert.Equal(1, three[0].Number);
            Assert.Equal(@"\\.\DISPLAY3", three[0].Device);
        }

        [Fact]
        public void Settings_roundtrip_and_defaults()
        {
            var s = new Settings { Enabled = false, TopMost = false, TargetDevice = @"\\.\DISPLAY2" };
            var back = Settings.Parse(s.Serialize());
            Assert.False(back.Enabled);
            Assert.False(back.TopMost);
            Assert.True(back.Notifications);
            Assert.Equal(@"\\.\DISPLAY2", back.TargetDevice);

            var junk = Settings.Parse("rubbish\nEnabled=forse\n=x");
            Assert.True(junk.Enabled);
            Assert.Equal("", junk.TargetDevice);
        }
    }
}
