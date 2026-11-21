using Xunit;

namespace JWFixMonitor.Tests
{
    public class TvSwapTests
    {
        private static WindowFacts Pdf() => new WindowFacts
        {
            Title = "Programma.pdf - Adobe Acrobat Reader", ClassName = "AcrobatSDIWindow", Visible = true
        };

        [Fact]
        public void Preview_fits_box_keeping_proportions_centered()
        {
            var box = new Box(10, 20, 230, 144); // 220x124
            var wide = Geometry.Fit(1920, 1080, box);  // 16:9 riempie in larghezza
            Assert.Equal(new[] { 10, 20, 230, 144 }, new[] { wide.Left, wide.Top, wide.Right, wide.Bottom });

            var tall = Geometry.Fit(600, 1200, box);   // verticale: centrata, bande ai lati
            Assert.Equal(124, tall.Height);
            Assert.Equal(62, tall.Width);
            Assert.Equal(10 + (220 - 62) / 2, tall.Left);

            Assert.True(Geometry.Fit(0, 0, box).IsEmpty);
        }

        [Fact]
        public void Normal_app_window_can_go_on_tv()
        {
            Assert.True(WindowFilter.CanSend(Pdf()));
            var zoom = new WindowFacts { Title = "Zoom Meeting", ClassName = "ConfMultiTabContentWndClass", Visible = true };
            Assert.True(WindowFilter.CanSend(zoom));
        }

        [Fact]
        public void Minimized_pdf_still_listed()
        {
            var f = Pdf();
            f.Minimized = true;
            Assert.True(WindowFilter.CanSend(f));
        }

        [Fact]
        public void Hidden_cloaked_tool_or_untitled_windows_excluded()
        {
            var f = Pdf(); f.Visible = false; Assert.False(WindowFilter.CanSend(f));
            f = Pdf(); f.Cloaked = true; Assert.False(WindowFilter.CanSend(f));
            f = Pdf(); f.ToolWindow = true; Assert.False(WindowFilter.CanSend(f));
            f = Pdf(); f.Title = "  "; Assert.False(WindowFilter.CanSend(f));
        }

        [Fact]
        public void Owned_dialog_excluded_unless_app_window()
        {
            var f = Pdf(); f.Owned = true;
            Assert.False(WindowFilter.CanSend(f));
            f.AppWindow = true;
            Assert.True(WindowFilter.CanSend(f));
        }

        [Fact]
        public void Own_window_jw_media_and_shell_excluded()
        {
            var f = Pdf(); f.OwnProcess = true; Assert.False(WindowFilter.CanSend(f));
            f = Pdf(); f.IsMediaWindow = true; Assert.False(WindowFilter.CanSend(f));
            foreach (var cls in new[] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW" })
            {
                f = Pdf(); f.ClassName = cls;
                Assert.False(WindowFilter.CanSend(f));
            }
        }
    }
}
