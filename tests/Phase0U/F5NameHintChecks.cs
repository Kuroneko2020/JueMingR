using System;
using System.Linq;
using System.Collections.Generic;
using JueMingR.Features.Information;
using JueMingR.Features.Items;
using JueMingR.Platform.Information;
using JueMingR.Platform.Items;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Information;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;

namespace Terraria
{
    internal static class F5NameHintChecks
    {
        internal static void Run()
        {
            var layout = new F5Layout();
            layout.Ensure(1920, 1080, 1, 9, new object(), text => new F5Size(text.Length * 18, 24));
            var name = layout.Elements.First(e => e.Text == "世界感染");
            Check(name.Description != null,
                "implemented information name carries its own description without becoming a command");
            Check(name.Command == F5Command.None && name.HotkeyTarget == null, "name remains display-only");
            Check(new F5Renderer().ButtonHint(layout.Elements.First(e => e.Command == F5Command.EnableBiome), false) == null,
                "clear enable button no longer repeats the feature introduction");
            var descriptions = layout.Elements.Where(e => e.Description != null).Select(e => e.Description).Distinct().ToArray();
            Check(descriptions.Length == 16 && descriptions.Select(d => d.Id).Distinct().Count() == 16,
                "only sixteen implemented information rows have distinct stable descriptions");
            string chest = Description(layout, "宝箱显名"), luck = Description(layout, "幸运值"), angler = Description(layout, "渔夫任务");
            Check(chest.Contains("始终") && chest.Contains("开过") && chest.Contains("本角色在此世界") && chest.Contains("无需金属探测"), "chest modes preserve positional eligibility and different detector conditions");
            Check(name.Description.Text == "显示世界感染比例。需要当前世界存在树妖。", "infection summary retains the current Dryad requirement");
            Check(luck.Contains("在此世界解救过巫师，或当前有巫师") && angler.Contains("在此世界解救过渔夫，或当前有渔夫") && angler.Contains("累计") && angler.Contains("地点"), "unlock alternatives and angler purpose");
            Check(!Description(layout, "群系显示").Contains("单人") && !Description(layout, "动物显名").Contains("金属"), "unrelated restrictions do not spread");
            Check(Description(layout, "显示碎岩龟").Contains("睡眠中的碎岩龟") && Description(layout, "显示龙蛋").Contains("巨型龙蛋") && Description(layout, "牌子显示").Contains("十行"), "world targets and bounded text semantics");
            Check(layout.Elements.Where(e => e.Text == "完整鱼获" || e.Text == "过滤鱼获").All(e => e.Description == null), "future samples receive no supported-feature copy");
            Geometry(); Interaction(); Cache(descriptions);
            Console.WriteLine("PASS: ordinary feature name hints.");
        }
        private static string Description(F5Layout layout, string label) { return layout.Elements.First(e => e.Text == label).Description.Text; }
        private static void Geometry()
        {
            var elements = new List<F5Element>(); float y = 0;
            var description = new F5RowDescription("module.test", "内容");
            new F5RowLayout(elements, (s, scale) => new F5Size(s.Length * 18 * scale, 24 * scale)).Row(ref y, 0, 180,
                "一项足够长而需要换行的普通功能", new[] { "开启", "关闭", "配置" }, description: description);
            var names = elements.Where(e => e.Description != null).ToArray();
            Check(names.Length > 1, "long names wrap");
            var button = elements.First(e => e.Kind == F5ElementKind.Button);
            Check(button.Rect.Y > names.Last().HintRect.Bottom, "buttons below never overlap name hit regions");
            F5Rect target; var view = new F5Rect(200, 100, 180, 60);
            foreach (var n in names)
            {
                var visible = F5HintLayout.Intersect(n.HintRect.Offset(200, 90), view);
                if (visible.Height <= 0) continue;
                Check(F5HintLayout.HitName(elements, view, 200, 90, visible.X + 1, visible.Y + 1, out target) != null, "padded visible line hit follows window and scroll");
            }
            Check(F5HintLayout.HitName(elements, view, 200, 90, 210, 99, out target) == null &&
                F5HintLayout.HitName(elements, view, 200, 90, 381, 120, out target) == null, "cropped text and scrollbar do not hit");
            var one = new List<F5Element> { names[0] };
            var above = new F5Rect(0, names[0].Rect.Bottom, 180, 60);
            var below = new F5Rect(0, names[0].Rect.Y - 60, 180, 60);
            Check(F5HintLayout.HitName(one, above, 0, 0, names[0].Rect.X + 1, above.Y + 1, out target) == null &&
                F5HintLayout.HitName(one, below, 0, 0, names[0].Rect.X + 1, below.Bottom - 1, out target) == null,
                "fully cropped glyphs cannot hit through padding at either edge");
        }
        private static void Interaction()
        {
            var state = new F5Interaction { Ready = true }; var renderer = new F5Renderer();
            var input = new F5Input { Active = true, Focused = true, Width = 1280, Height = 720, Scale = 1.5f, F5 = true };
            state.Update(input); input.F5 = false;
            object font = new object();
            state.Layout.Ensure(input.Width, input.Height, input.Scale, state.Page, font, s => new F5Size(s.Length * 18, 24));
            F5Element first = state.Layout.Elements.First(e => e.Description != null);
            input.X = state.X + state.Layout.Viewport.X + first.Rect.X + 3;
            input.Y = state.Y + state.Layout.Viewport.Y + first.Rect.Y + 3; state.Update(input);
            F5Rect target; string hint = renderer.ResolveHint(state, null, false, false, out target);
            Check(hint == first.Description.Text && renderer.ResolveHint(state, null, false, true, out target) == hint, "description is available even when business is unavailable");
            Check(renderer.ResolveHint(state, null, true, false, out target) == null, "visible modal owner blocks base hints outside popup bounds too");
            int generation = state.Layout.Generation, measurements = state.Layout.MeasurementCount;
            for (int i = 0; i < 100; i++) { state.Update(input); renderer.ResolveHint(state, null, false, false, out target); }
            Check(state.Layout.Generation == generation && state.Layout.MeasurementCount == measurements, "hover never prepares the page");
            for (int click = 0; click < 2; click++)
            {
                input.Left = true; state.Update(input); Check(renderer.ResolveHint(state, null, false, false, out target) == null, "pressed gesture yields help");
                input.Left = false; state.Update(input);
                Check(state.Command == F5Command.None && state.ClickedHotkey == null && state.ConsumeLeft, "name click and double-click neither execute nor leak release");
            }
            input.Wheel = -120; state.Update(input); input.Wheel = 0;
            Check(renderer.ResolveHint(state, null, false, false, out target) != hint, "stationary pointer uses new scroll geometry");
            state.ScrollTo(first.Rect.Bottom);
            input.X = state.X + state.Layout.Viewport.X + first.Rect.X + 1;
            input.Y = state.Y + state.Layout.Viewport.Y + 1; state.Update(input);
            Check(renderer.ResolveHint(state, null, false, false, out target) != hint,
                "real scrolled page cannot resolve an undrawn row through its padding");
            var controls = new InformationControls(new InformationState()); renderer.InformationControls = controls;
            var enable = state.Layout.Elements.First(e => e.Command == F5Command.EnableInfection);
            Check(renderer.ButtonHint(enable, false) == null, "normal information button has no duplicate copy");
            var unavailable = new InformationState { CanConfigure = false }; renderer.InformationControls = new InformationControls(unavailable);
            Check(renderer.ButtonHint(enable, false) == "设置文件受保护", "current failure remains on disabled button");
            Check(renderer.ButtonHint(state.Layout.Elements.First(e => e.Command == F5Command.EnableBiome), true) == "群系显示暂不可用", "legacy biome fault is retained");
            state.ScrollTo(0); var key = state.Layout.Elements.First(e => e.HotkeyTarget != null);
            input.X = state.X + state.Layout.Viewport.X + key.Rect.X + 3; input.Y = state.Y + state.Layout.Viewport.Y + key.Rect.Y + 3; state.Update(input);
            Check(renderer.ResolveHint(state, null, false, false, out target) == "双击设置功能开关快捷键", "icon explains its real double-click gesture and toggle purpose");
            key = state.Layout.Elements.First(e => e.HotkeyTarget == JueMingR.TerrariaHost.Hotkeys.HotkeyActionIds.AdjustInformation);
            state.ScrollTo(key.Rect.Y);
            input.X = state.X + state.Layout.Viewport.X + key.Rect.X + 3;
            input.Y = state.Y + state.Layout.Viewport.Y + key.Rect.Y - state.Scroll + 3; state.Update(input);
            Check(renderer.ResolveHint(state, null, false, false, out target) == "双击设置调整信息窗位置的快捷键", "one-shot action is never described as a feature toggle");
            state.Navigate(7); state.Layout.Ensure(input.Width, input.Height, input.Scale, state.Page, font, s => new F5Size(s.Length * 18, 24));
            Check(renderer.ResolveHint(state, null, false, false, out target) == null, "page change cannot retain old hint");
            input.Focused = false; state.Update(input);
            Check(renderer.ResolveHint(state, null, false, false, out target) == null, "focus loss and closed F5 have no hints");
        }
        private static void Cache(F5RowDescription[] descriptions)
        {
            var layout = new F5HintLayout(); object font = new object(); int measures = 0;
            Func<string, float, F5Size> measure = (s, scale) => { measures++; return new F5Size(s.Length * 18 * scale + 4, 24 * scale + 4); };
            var bounds = new F5Rect(8, 8, 838, 464); var anchor = new F5Rect(60, 160, 100, 20);
            layout.Prepare(descriptions[0].Text, anchor, bounds, font, measure); int warm = measures;
            for (int i = 0; i < 200; i++) layout.Prepare(descriptions[0].Text, anchor.Offset(i % 80, 0), bounds, font, measure);
            Check(measures == warm, "stable content and pointer movement reuse all wrapping and measurements");
            foreach (var description in descriptions.Concat(new[] { ItemsPresentation.Description(ItemActionKind.Stack), ItemsPresentation.Description(ItemActionKind.Sell), ItemsPresentation.Description(ItemActionKind.Discard) }))
            {
                foreach (float targetY in new[] { 8f, 220f, 450f })
                {
                    layout.Prepare(description.Text, new F5Rect(30, targetY, 100, 18), bounds, font, measure);
                    Check(string.Concat(layout.Lines.Select(e => e.Text)) == description.Text, "all conditions remain in wrapped output");
                    Check(layout.Panel.X >= bounds.X && layout.Panel.Right <= bounds.Right && layout.Panel.Y >= bounds.Y && layout.Panel.Bottom <= bounds.Bottom, "top/bottom viewport edges clamp full hint");
                    Check(layout.Panel.Height == layout.Lines.Last().Rect.Bottom + 16, "height follows actual lines with no reserved blank area");
                }
            }
            warm = measures; layout.Prepare(descriptions[0].Text, anchor, bounds, new object(), measure);
            Check(measures > warm, "font replacement invalidates hint layout even with equal metrics");
            warm = measures; layout.Prepare(descriptions[0].Text, anchor, new F5Rect(8, 8, 604, 204), font, measure);
            Check(measures > warm, "available width and viewport change rebuild hint");
            layout.Hide(); Check(!layout.Visible, "hide immediately removes rendered hint");
        }
        internal static void Items(HostItems host, F5Interaction shell, ItemsPresentation presentation)
        {
            var renderer = new F5Renderer(); long revision = host.Preferences.Revision; int builds = presentation.LayoutBuildCount;
            object font = new object(); int measures = 0;
            Func<string, float, F5Size> measure = (s, scale) => { measures++; return new F5Size(s.Length * 18 * scale + 4, 24 * scale + 4); };
            F5Rect target;
            // Inspect the real projected elements, then traverse the exact resolver
            // called by the final Shell drawing layer, without running business.
            var field = typeof(ItemsPresentation).GetField("elements", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var elements = (List<F5Element>)field.GetValue(presentation);
            var names = elements.Where(e => e.Description != null).ToArray(); Check(names.Length == 3, "three real item names have descriptions");
            foreach (var name in names)
            {
                var input = new F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1, X = name.Rect.X + 2, Y = name.Rect.Y + 2 };
                shell.Update(input);
                string text = renderer.ResolveHint(shell, presentation, false, false, out target);
                Check(text == name.Description.Text && text.Contains(name == names[0] ? "刚拾取的" : "背包中未收藏的") && text.Contains("出售→丢弃→存放"), "storage acquisition and sale/trash inventory scope reach the actual name hint");
                Prepare(renderer, shell, presentation, font, measure);
                Check(renderer.HintLayout.Visible && string.Concat(renderer.HintLayout.Lines.Select(e => e.Text)) == text,
                    "real projected item name reaches the shared production preparation");
                int warm = measures;
                for (int i = 0; i < 100; i++) Prepare(renderer, shell, presentation, font, measure);
                Check(measures == warm, "stable item consumer never rewraps or measures the hint");
                Check(renderer.ResolveHint(shell, presentation, true, false, out target) == null, "item hints yield to modal owner");
            }
            Check(names[1].Description.Text.Contains("已打开的商店") && names[2].Description.Text.Contains("垃圾桶"), "sale prerequisite and discard destination remain explicit");
            Check(host.Preferences.Revision == revision && presentation.LayoutBuildCount == builds, "hover neither saves settings nor rebuilds Items");
            // Reuse the SAME shell-owned renderer/cache for the other real page.
            var information = new F5Interaction { Ready = true };
            var infoInput = new F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1, F5 = true };
            information.Update(infoInput); infoInput.F5 = false;
            information.Layout.Ensure(1920, 1080, 1, information.Page, font, s => new F5Size(s.Length * 18, 24));
            var infoName = information.Layout.Elements.First(e => e.Description != null);
            var view = information.Layout.Viewport.Offset(information.X, information.Y);
            infoInput.X = view.X + infoName.Rect.X + 1; infoInput.Y = view.Y + infoName.Rect.Y + 1; information.Update(infoInput);
            int before = measures; Prepare(renderer, information, null, font, measure);
            Check(measures > before && string.Concat(renderer.HintLayout.Lines.Select(e => e.Text)) == infoName.Description.Text,
                "switching real pages replaces the same current hint with information content");
            before = measures;
            for (int i = 0; i < 100; i++) { infoInput.X += .01f; information.Update(infoInput); Prepare(renderer, information, null, font, measure); }
            Check(measures == before, "information consumer also reuses production preparation while moving");
            Prepare(renderer, information, null, font, measure, true);
            Check(!renderer.HintLayout.Visible, "modal clears the same current hint");
            Prepare(renderer, information, null, new object(), measure);
            Check(measures > before && renderer.HintLayout.Visible, "real preparation refreshes changed font and clears stale hiding");

            var nameRect = infoName.Rect.Offset(view.X, view.Y);
            var clipped = new F5Rect(view.X, nameRect.Bottom, view.Width, view.Bottom - nameRect.Bottom);
            infoInput.X = nameRect.X + 1; infoInput.Y = nameRect.Bottom + 1; information.Update(infoInput);
            renderer.PrepareHints(information, null, false, false, new F5Rect(8, 8, 1904, 1064), clipped, font, measure);
            Check(!renderer.HintLayout.Visible, "final scissor cannot expose padding after cropping all glyphs");
        }
        private static void Prepare(F5Renderer renderer, F5Interaction state, ItemsPresentation items, object font,
            Func<string, float, F5Size> measure, bool blocked = false)
        {
            var view = state.Layout.Viewport.Offset(state.X, state.Y);
            var matrix = Matrix.CreateScale(1.5f, 1.5f, 1);
            var clip = F5ControlRenderer.LogicalClip(F5ControlRenderer.ContentClip(view, matrix,
                new Rectangle(0, 0, 2880, 1620)), matrix);
            renderer.PrepareHints(state, items, blocked, false, new F5Rect(8, 8, 1904, 1064), clip, font, measure);
        }
        internal static void Picker(HostItems host, F5Interaction shell, ItemsPresentation presentation, F5Rect candidate)
        {
            var renderer = new F5Renderer(); object font = new object();
            Func<string, float, F5Size> measure = (s, scale) => new F5Size(s.Length * 18 * scale + 4, 24 * scale + 4);
            var elements = (List<F5Element>)typeof(ItemsPresentation).GetField("elements", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(presentation);
            var view = shell.Layout.Viewport.Offset(shell.X, shell.Y);
            var name = elements.First(e => e.Description != null && e.Rect.Y >= view.Y && e.Rect.Bottom <= view.Bottom);
            var input = new F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1, X = name.Rect.X + 1, Y = name.Rect.Y + 1 };
            long revision = host.Preferences.Revision; int builds = presentation.LayoutBuildCount;
            shell.Update(input); Prepare(renderer, shell, presentation, font, measure);
            Check(presentation.Selecting && renderer.HintLayout.Visible, "inline picker preserves visible ordinary names through the shared path");
            input.X = candidate.X + 1; input.Y = candidate.Y + 1; shell.Update(input);
            Prepare(renderer, shell, presentation, font, measure);
            Check(!renderer.HintLayout.Visible && presentation.Selecting && revision == host.Preferences.Revision && builds == presentation.LayoutBuildCount,
                "candidate grid yields to its original item help without changing selection or settings");
        }
        private sealed class InformationState : IInformationControls
        {
            public InformationPreferences Settings { get { return InformationPreferences.Default; } }
            public bool CanConfigure { get; set; } = true;
            public bool PositionReady { get { return CanConfigure; } }
            public string PreferenceMessage { get { return "设置文件受保护"; } }
            public string PositionMessage { get { return "位置尚未载入"; } }
            public bool Enabled(InformationKind kind) { return false; }
            public bool SetEnabled(InformationKind kind, bool enabled) { throw new Exception("hover ran a command"); }
            public bool SetColor(InformationKind kind, int rgb) { throw new Exception("hover ran a command"); }
            public bool StepSize(InformationKind kind, int direction) { throw new Exception("hover ran a command"); }
            public void ResetStyle(InformationKind kind) { throw new Exception("hover ran a command"); }
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Name hint: " + message); }
    }
}
