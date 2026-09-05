using System;
using JueMingR.TerrariaHost.F5;

namespace Terraria
{
    internal static class F5LayoutChecks
    {
        internal static void Run()
        {
            CheckRefinementContract();
            CheckVisualRows();
            var layout = new F5Layout();
            var font = new object();
            layout.Ensure(1920, 1080, 1, 9, font, Measure);
            Equal(580, layout.Window.Width, "normal window width");
            Equal(740, layout.Window.Height, "normal window height");
            Equal(522, layout.Viewport.Width, "shared page viewport width");
            Equal(581, layout.Viewport.Height, "normal page viewport height");
            int generation = layout.Generation, measurements = layout.MeasurementCount;
            for (int i = 0; i < 100; i++) layout.Ensure(1920, 1080, 1, 9, font, Measure);
            Equal(generation, layout.Generation, "ordinary updates do not reflow");
            Equal(measurements, layout.MeasurementCount, "ordinary updates do not measure text");
            layout.Ensure(1920, 1080, 1, 9, new object(), Measure);
            Equal(generation, layout.Generation, "font replacement with identical metrics preserves layout");
            layout.Ensure(1920, 1080, 1, 9, new object(), text => new F5Size(text.Length * 18, 24, -3, 9));
            Equal(generation + 1, layout.Generation, "equal-size glyph offset change refreshes positioning");
            layout.Ensure(1920, 1080, 1, 9, new object(), text => new F5Size(text.Length * 20, 26));
            Equal(generation + 2, layout.Generation, "actual font metric change triggers one reflow");
            ValidateBounds(layout);
            layout.Ensure(1280, 720, 1.5f, 7, font, Measure);
            Equal(580, layout.Window.Width, "constrained window retains width");
            Equal(456, layout.Window.Height, "constrained viewport clips content only");
            Equal(297, layout.Viewport.Height, "constrained page viewport height");
            ValidateBounds(layout);
            for (int page = 0; page < 12; page++)
            { layout.Ensure(1280, 720, 1.5f, page, font, Measure); ValidateBounds(layout); }
            Console.WriteLine("PASS: Phase 0-U production layout geometry.");
        }

        private static void CheckRefinementContract()
        {
            var layout = new F5Layout();
            layout.Ensure(1920, 1080, 1, 9, new object(), Measure);
            F5Element row = null;
            float ordinaryHeight = layout.Elements[0].Rect.Height;
            int keyboardSlots = 0, dividers = 0;
            bool afterDragon = false, beforePosition = true;
            foreach (F5Element element in layout.Elements)
            {
                if (element.Kind == F5ElementKind.Panel) row = element;
                if (element.Kind.ToString() == "BiomeStatus")
                    throw new InvalidOperationException("Normal biome status must not retain a second-line layout element.");
                if (element.Text == "键") throw new InvalidOperationException("Hotkey entry must be artwork, not a text button.");
                if (element.Text == "显示龙蛋") afterDragon = true;
                if (element.Text == "调整信息窗位置") beforePosition = false;
                if (element.Kind.ToString() == "Divider")
                {
                    if (!afterDragon || !beforePosition || element.Command != F5Command.None)
                        throw new InvalidOperationException("Information divider must be inert between dragon egg and position controls.");
                    dividers++;
                }
                if (element.Kind.ToString() == "Hotkey")
                {
                    if (element.Command != F5Command.None || element.Text != null || row == null ||
                        element.Rect.Y < row.Rect.Y || element.Rect.Bottom > row.Rect.Bottom)
                        throw new InvalidOperationException("Artwork slot must stay in its row and have no hotkey business.");
                    keyboardSlots++;
                }
                if (element.Text == "群系显示")
                    Equal(ordinaryHeight, row.Rect.Height, "biome row has no special normal-status space");
            }
            Equal(1, dividers, "exactly one semantic content divider");
            Equal(14, keyboardSlots, "existing fourteen information hotkey entries are restored");
            if (F5Layout.DisplayTitle != "决明R" || layout.TitleDivider.Y <= layout.Title.Bottom ||
                layout.TitleDivider.Bottom >= layout.Navigation(0).Y)
                throw new InvalidOperationException("Display-only Chinese title has its own fixed separator before navigation.");
            var input = new F5Input { Width = 1920, Height = 1080, Scale = 1, Active = true, Focused = true, F5 = true };
            var state = new F5Interaction { Ready = true };
            state.Update(input); state.Layout.Ensure(1920, 1080, 1, 9, new object(), Measure);
            foreach (F5Element element in state.Layout.Elements)
            {
                if (element.Kind != F5ElementKind.Hotkey || element.Rect.Bottom > state.Layout.Viewport.Height) continue;
                input.F5 = false; input.Left = true;
                input.X = state.X + state.Layout.Viewport.X + element.Rect.X + element.Rect.Width / 2;
                input.Y = state.Y + state.Layout.Viewport.Y + element.Rect.Y + element.Rect.Height / 2;
                state.Update(input);
                if (!state.ConsumeLeft) throw new InvalidOperationException("Inert keyboard slot must still shield the world.");
                input.Left = false; state.Update(input);
                if (state.Command != F5Command.None) throw new InvalidOperationException("Keyboard appearance cannot execute shortcut business.");
            }
            bool rejected = false;
            try { new F5Layout().Ensure(1920, 1080, 1, 9, new object(), text => new F5Size(text.Length * 18, 32)); }
            catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("An over-tall header font must fail safely instead of painting a line on its text or outer rim.");
        }

        private static void CheckVisualRows()
        {
            var layout = new F5Layout();
            var failures = new System.Collections.Generic.List<string>();
            for (int page = 0; page < 12; page++)
            {
                layout.Ensure(1920, 1080, 1, page, new object(), text => new F5Size(text.Length * 18,
                    Array.IndexOf(F5Layout.Pages, text) >= 0 ? 24 : 32));
                F5Element panel = null;
                foreach (F5Element element in layout.Elements)
                {
                    if (element.Text != null && (element.Text.Contains("占位") || element.Text.Contains("未接入") ||
                        element.Text.Contains("请选择")))
                        failures.Add("development guidance remains on page " + page);
                    if (element.Kind == F5ElementKind.Panel) panel = element;
                    if (page != 9 || element.Kind != F5ElementKind.Button || panel == null) continue;
                    if (Math.Abs(element.Rect.Y + element.Rect.Height / 2 - panel.Rect.Y - panel.Rect.Height / 2) > 0.01f)
                        failures.Add("information button group is not vertically centered");
                    if (element.Command == F5Command.None && F5Layout.HintIndex(element, false) >= 0)
                        failures.Add("inactive control still has a development hover hint");
                }
                if (page == 9 && layout.Elements[0].Rect.Height > 44)
                    failures.Add("ordinary row retains the removed second-line space");
            }
            if (failures.Count != 0) throw new InvalidOperationException("Visual contract: " + string.Join("; ",
                new System.Collections.Generic.HashSet<string>(failures)));
        }

        private static void ValidateBounds(F5Layout layout)
        {
            for (int i = 0; i < 12; i++)
            {
                F5Rect nav = layout.Navigation(i), icon = layout.NavigationIcon(i), label = layout.NavigationLabel(i);
                F5Rect line = layout.NavigationUnderline(i);
                Equal((icon.X + label.Right) / 2, line.X + line.Width / 2, "short underline uses icon-label center");
                if (line.X < nav.X + 9.99f || line.Right > nav.Right - 9.99f || line.Y < label.Bottom + 0.49f || line.Bottom > nav.Bottom - 1.99f)
                    throw new InvalidOperationException("Navigation underline must clear glyphs and rounded-surface edges.");
                Equal(nav.X + nav.Width / 2, (icon.X + label.Right) / 2, "icon-label group horizontal center");
                Equal(nav.Y + nav.Height / 2, icon.Y + icon.Height / 2, "icon visual midline");
                Equal(nav.Y + nav.Height / 2, label.Y + label.Height / 2, "label visual midline");
            }
            F5Rect track = layout.ScrollTrack, visual = layout.ScrollThumbVisual(layout.MaxScroll / 2);
            if (visual.X < track.X || visual.Right > track.Right || visual.Width >= track.Width ||
                track.X <= layout.Viewport.Right || visual.Y < track.Y || visual.Bottom > track.Bottom)
                throw new InvalidOperationException("Thin scrollbar must stay inside the existing grab channel.");
            Equal(track.X + track.Width / 2, visual.X + visual.Width / 2, "visual and interactive scroll centers");
            foreach (F5Element element in layout.Elements)
            {
                if (element.Kind == F5ElementKind.Button)
                {
                    F5Rect surface = F5Layout.ButtonSurface(element), label = F5Layout.ButtonLabel(element);
                    F5Rect line = F5Layout.ButtonUnderline(element);
                    Equal(element.Rect.Y + element.Rect.Height / 2, surface.Y + surface.Height / 2, "flattening preserves hit center");
                    Equal(surface.Y + surface.Height / 2, label.Y + label.Height / 2, "label centered on the visible surface");
                    if (surface.Y < element.Rect.Y - 0.01f || surface.Bottom > element.Rect.Bottom + 0.01f ||
                        line.X < surface.X + 9.99f || line.Right > surface.Right - 9.99f || line.Y < label.Bottom + 0.49f || line.Bottom > surface.Bottom - 1.99f)
                        throw new InvalidOperationException("Function underline must fit its visible surface without crowding the label: " +
                            element.Text + " surface=" + surface.X + "," + surface.Y + "," + surface.Right + "," + surface.Bottom +
                            " line=" + line.X + "," + line.Y + "," + line.Right + "," + line.Bottom + " labelBottom=" + label.Bottom);
                    if (element.Command == F5Command.None && (F5Layout.IsSelected(element, true, false) || F5Layout.IsSelected(element, false, false)) ||
                        F5Layout.IsSelected(element, true, true) || F5Layout.IsSelected(element, false, true))
                        throw new InvalidOperationException("Unknown and failed controls have no state line.");
                    if (element.Command == F5Command.EnableBiome && (!F5Layout.IsSelected(element, true, false) || F5Layout.IsSelected(element, false, false)) ||
                        element.Command == F5Command.DisableBiome && (!F5Layout.IsSelected(element, false, false) || F5Layout.IsSelected(element, true, false)))
                        throw new InvalidOperationException("Only the button named for the actual state is selected.");
                }
                if (element.Rect.X < 0 || element.Rect.Right > layout.Viewport.Width + 0.01f ||
                    element.Rect.Y < 0 || element.Rect.Bottom > layout.ContentHeight + 0.01f)
                    throw new InvalidOperationException("A production page element exceeds its scrollable bounds.");
                if (element.Kind == F5ElementKind.Button &&
                    (element.TextSize.Width > element.Rect.Width - 8 || element.TextSize.Height > element.Rect.Height - 4))
                    throw new InvalidOperationException("A production button clips its measured label.");
            }
        }

        private static F5Size Measure(string text)
        { return new F5Size(text.Length * 18, 24); }

        internal static void Equal(float expected, float actual, string message)
        {
            if (Math.Abs(expected - actual) > 0.01f)
                throw new InvalidOperationException(message + ": expected " + expected + ", actual " + actual);
        }
    }
}
