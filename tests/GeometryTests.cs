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

        // sala: PC principale, TV 1280x720, timer 1920x1080
        private const string TvPath = @"\\?\DISPLAY#SAM0F9E#5&1a2b&0&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
        private const string TimerPath = @"\\?\DISPLAY#DEL41A8#5&1a2b&0&UID4353#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

        private static MonitorDesc Hw(MonitorDesc m, string path, string name)
        {
            m.Path = path;
            m.Model = Geometry.ModelOf(path);
            m.Name = name;
            return m;
        }

        private static List<MonitorDesc> Sala(string tvDev = @"\\.\DISPLAY2", string timerDev = @"\\.\DISPLAY3") =>
            Geometry.Number(new[]
            {
                M(@"\\.\DISPLAY1", 0, 0, 1920, 1080, primary: true),
                Hw(M(tvDev, 1920, 0, 1280, 720), TvPath, "SAMSUNG"),
                Hw(M(timerDev, -1920, 0, 1920, 1080), TimerPath, "DELL P2219H"),
            });

        private static MonitorChoice ChoiceOf(MonitorDesc m) => MonitorChoice.From(m);

        [Fact]
        public void Model_parsed_from_monitor_path()
        {
            Assert.Equal("SAM0F9E", Geometry.ModelOf(TvPath));
            Assert.Equal("", Geometry.ModelOf(""));
            Assert.Equal("", Geometry.ModelOf(null));
            Assert.Equal("", Geometry.ModelOf("garbage"));
        }

        [Fact]
        public void Chosen_tv_found_by_path_when_windows_renames_it()
        {
            var tv = ChoiceOf(Sala()[2]); // TV a destra: schermo 3
            Assert.Equal(TvPath, tv.Path);

            // dopo un riavvio Windows scambia DISPLAY2 e DISPLAY3
            var m = Geometry.ChooseMonitor(Sala(tvDev: @"\\.\DISPLAY3", timerDev: @"\\.\DISPLAY2"), tv, @"\\.\DISPLAY2");
            Assert.Equal(TvPath, m.Path);
        }

        [Fact]
        public void Chosen_tv_found_by_model_when_port_changes()
        {
            var tv = ChoiceOf(Sala()[2]);
            tv.Path = TvPath.Replace("UID4352", "UID9999"); // altra porta: percorso diverso
            tv.Device = @"\\.\DISPLAY9";
            var m = Geometry.ChooseMonitor(Sala(), tv, @"\\.\DISPLAY3");
            Assert.Equal("SAM0F9E", m.Model);
        }

        [Fact]
        public void Same_model_twice_falls_back_to_windows_name()
        {
            var twins = Geometry.Number(new[]
            {
                M(@"\\.\DISPLAY1", 0, 0, 1920, 1080, primary: true),
                Hw(M(@"\\.\DISPLAY2", 1920, 0, 1280, 720), TvPath, "SAMSUNG"),
                Hw(M(@"\\.\DISPLAY3", -1280, 0, 1280, 720), TvPath.Replace("UID4352", "UID4353"), "SAMSUNG"),
            });
            var choice = new MonitorChoice { Device = @"\\.\DISPLAY2", Path = "x", Model = "SAM0F9E", Width = 1280, Height = 720 };
            Assert.Equal(@"\\.\DISPLAY2", Geometry.ChooseMonitor(twins, choice, @"\\.\DISPLAY3").Device);
        }

        [Fact]
        public void Unique_resolution_is_last_resort_before_auto()
        {
            var choice = new MonitorChoice { Device = @"\\.\DISPLAY9", Path = "x", Model = "ZZZ0000", Width = 1280, Height = 720 };
            Assert.Equal(TvPath, Geometry.ChooseMonitor(Sala(), choice, @"\\.\DISPLAY3").Path);

            // risoluzione del timer = quella del principale, ma il principale non conta
            choice.Width = 1920; choice.Height = 1080;
            Assert.Equal(TimerPath, Geometry.ChooseMonitor(Sala(), choice, @"\\.\DISPLAY2").Path);
        }

        [Fact]
        public void Nothing_matches_means_auto()
        {
            var choice = new MonitorChoice { Device = @"\\.\DISPLAY9", Path = "x", Model = "ZZZ0000", Width = 800, Height = 600 };
            // automatico: resta sul secondario dove già si trova
            Assert.Equal(@"\\.\DISPLAY2", Geometry.ChooseMonitor(Sala(), choice, @"\\.\DISPLAY2").Device);
            Assert.Equal(@"\\.\DISPLAY2", Geometry.ChooseMonitor(Sala(), MonitorChoice.Auto, @"\\.\DISPLAY2").Device);
        }

        [Fact]
        public void Label_shows_monitor_name()
        {
            L.Lang = "it";
            var sala = Sala();
            Assert.Equal("Schermo 3 — SAMSUNG 1280×720", sala[2].Label);
            Assert.Equal("Schermo 2 — 1920×1080 (principale)", sala[1].Label);
        }

        [Fact]
        public void Settings_keep_chosen_monitor_identity()
        {
            var s = new Settings
            {
                TargetDevice = @"\\.\DISPLAY2", TargetPath = TvPath, TargetModel = "SAM0F9E", TargetWidth = 1280, TargetHeight = 720
            };
            var back = Settings.Parse(s.Serialize());
            Assert.Equal(TvPath, back.TargetPath);
            Assert.Equal("SAM0F9E", back.TargetModel);
            Assert.Equal(1280, back.TargetWidth);
            Assert.Equal(720, back.TargetHeight);

            // impostazioni vecchie: solo il nome Windows
            var old = Settings.Parse("TargetDevice=\\\\.\\DISPLAY2");
            Assert.Equal(@"\\.\DISPLAY2", old.Choice.Device);
            Assert.Equal("", old.Choice.Path);
            Assert.False(old.Choice.IsAuto);
            Assert.True(new Settings().Choice.IsAuto);
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
