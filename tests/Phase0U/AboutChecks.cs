using System;
using System.Linq;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.About;
namespace Terraria
{
    internal static class AboutChecks
    {
        internal static void Run()
        {
            var state = new F5Interaction { Ready = true };
            var input = new F5Input { Width = 1280, Height = 1080, Scale = 1, Active = true, Focused = true, F5 = true };
            state.Update(input); input.F5 = false; state.Update(input); state.Navigate(5);
            int copies = 0, behavior = 0; string copied = null; double now = 1000;
            state.Layout.About.Attach(text => { copies++; copied = text; if (behavior == 2) throw new InvalidOperationException(); return behavior == 0; }, () => now);
            object font = new object(); Func<string, F5Size> measure = t => new F5Size(t.Length * 18, 24);
            Action prepare = () => state.Layout.Ensure(input.Width, input.Height, input.Scale, state.Page, font, measure);
            prepare(); var page = state.Layout.About;
            Require(!state.Layout.Elements.Any(e => e.Text == "使用帮助 / 版本信息" || e.Text == "微信" || e.Text == "支付宝"), "About has no removed help/version entry or QR captions");
            Require(state.Layout.Elements.Any(e => e.Text == "决明R") && state.Layout.Elements.Count(e => e.Kind == F5ElementKind.Image) == 2, "Real page introduction and both image elements");
            Require(state.Layout.MaxScroll == 0, "Home presents its whole composition without scrolling");
            Require(state.Layout.Elements.Count(e => e.Kind == F5ElementKind.Button) == 1, "Only the requested group action remains");
            F5Element group = state.Layout.Elements.First(e => e.Command == F5Command.AboutCopyGroup);
            Click(state, ref input, group, page); Require(copies == 1 && copied == "915753352", "Real release dispatch copies exactly the displayed group number");
            int generation = state.Layout.Generation, measurements = state.Layout.MeasurementCount;
            for (int i = 0; i < 1000; i++) prepare();
            Require(page.Display(group).Text == "复制成功" && page.Display(group).Rect.Equals(group.Rect) && generation == state.Layout.Generation && measurements == state.Layout.MeasurementCount, "Temporary feedback has stable geometry and idle layout cost");
            Require(page.Hint(group.Command) == "点击复制", "Hover remains the requested short instruction");
            now = 3999; Require(page.Display(group).Text == "复制成功", "Success remains visible for three seconds");
            now = 4000; Require(page.Display(group).Text == "915753352" && copies == 1, "Deadline restores group without re-copying");
            behavior = 1; Click(state, ref input, group, page); Require(page.Display(group).Text == "复制失败" && page.Hint(group.Command) == "点击复制", "Failed copy is accurately shown with the same short hover");
            for (int i = 0; i < 50; i++) prepare(); Require(page.Display(group).Text == "复制失败" && copies == 2, "Failure is not retried by polling");
            now = 7000; Require(page.Display(group).Text == group.Text, "Failure also restores the visible group after its reading period");
            behavior = 2; Click(state, ref input, group, page); Require(copies == 3 && page.Display(group).Text == "复制失败", "Thrown clipboard result is failure");
            behavior = 0; now = 8000; Click(state, ref input, group, page); now = 10001;
            Require(page.Display(group).Text == "复制成功", "A later explicit click starts its own full feedback period");
            page.Leave(); Require(page.Display(group).Text == group.Text, "Leaving clears transient feedback");
            // A reflow during a held click must revoke its old geometry.
            Place(state, ref input, group); input.Left = true; state.Update(input); int before = copies;
            font = new object(); measure = t => new F5Size(t.Length * 17, 24); prepare(); input.Left = false; state.Update(input); page.Execute(state.Command);
            Require(copies == before, "Font reflow cancels old press");
            Place(state, ref input, group); input.Left = true; state.Update(input); input.Focused = false; state.Update(input); input.Left = false; state.Update(input); page.Execute(state.Command);
            Require(copies == before, "Focus loss cannot release into copy");
            font = new object(); measure = t => new F5Size(t.Length * 36, 24); prepare();
            Require(state.Layout.Elements.Where(e => e.Kind == F5ElementKind.Button).All(e => e.Rect.X >= 0 && e.Rect.Right <= 522), "Larger replacement glyphs keep all action geometry in the content width");
            page.SetImageFailure(true); prepare(); Require(state.Layout.Elements.Any(e => e.Text != null && e.Text.Contains("赞助图片")), "Resource failure is visible");
            page.SetImageFailure(false); prepare(); Require(!state.Layout.Elements.Any(e => e.Text != null && e.Text.Contains("赞助图片")), "Actual resource recovery clears the stale error");
            input.Height = 720; input.Scale = 1.5f; prepare();
            Require(state.Layout.MaxScroll > 0 && state.Layout.Elements.Where(e => e.Kind == F5ElementKind.Image).All(e => e.Rect.Width >= 76), "Short viewport scrolls while preserving readable QR dimensions");
            Require(generation < state.Layout.Generation && copies == before, "Real changes reflow; passive timer expiry never changes clipboard");
            Console.WriteLine("PASS: About single action, shared input safety, three-second copy feedback, short hover, unchanged layout and readable small-window scrolling.");
        }
        private static void Place(F5Interaction state, ref F5Input input, F5Element button)
        { state.ScrollTo(Math.Max(0, button.Rect.Bottom - state.Layout.Viewport.Height + 8)); input.X = state.X + state.Layout.Viewport.X + button.Rect.X + 5; input.Y = state.Y + state.Layout.Viewport.Y + button.Rect.Y - state.Scroll + 5; }
        private static void Click(F5Interaction state, ref F5Input input, F5Element button, AboutPage page)
        {
            Place(state, ref input, button); input.Left = false; state.Update(input); input.Left = true; state.Update(input);
            for (int i = 0; i < 10; i++) { state.Update(input); Require(state.Command == F5Command.None, "Held button never fires"); }
            input.Left = false; state.Update(input); Require(state.ConsumeLeft, "Release tail shields gameplay"); page.Execute(state.Command);
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
