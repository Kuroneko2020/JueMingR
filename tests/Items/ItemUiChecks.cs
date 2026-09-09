using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Items;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content.Readers;
using ReLogic.Graphics;
using Terraria.GameContent;
using Terraria.GameInput;

namespace Terraria
{
    internal static class ItemUiChecks
    {
        private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        internal static void RunLayout(HostItems host)
        {
            var shell = new F5Interaction { Ready = true }; bool notesMayLeave = false;
            shell.BeforeLeave = page => notesMayLeave;
            var p = new ItemsPresentation(host, shell); object font = new object();
            Func<string, F5Size> measure = t => new F5Size(t.Length * 18, 24);
            shell.Update(new F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1, F5 = true }); shell.Navigate(0);
            Action prepare = () => { shell.Layout.Ensure(1920, 1080, 1, shell.Page, font, measure); p.PrepareLayout(Matrix.Identity, new Vector2(1920, 1080)); };
            host.Change(ItemAutomationSettings.Default); host.PollPreferences(); prepare();
            Check(HasControl(p, "ToggleDiscardFeedback"), "discard row provides its real feedback preference command");
            var feedback = Control(p, "ToggleDiscardFeedback");
            var feedbackRect = Rect(feedback); var discardAdd = Rect(Control(p, "Add", (int)ItemListKind.Discard));
            Check(Math.Abs(discardAdd.X - feedbackRect.Right - 4) < .01f && feedbackRect.Y == discardAdd.Y && feedbackRect.Height == discardAdd.Height,
                "feedback precedes add in the same shared row group");
            Click(p, feedback); prepare();
            Check(!host.Preferences.Value.DiscardFeedbackEnabled && !host.Preferences.Value.StackEnabled && !host.Preferences.Value.SellEnabled && !host.Preferences.Value.DiscardEnabled &&
                host.Preferences.Value.SellTypes.SequenceEqual(new[] { 2337, 2338, 2339 }) && host.Preferences.Value.DiscardTypes.Count == 0,
                "feedback command toggles only its preference");
            Check(((F5Element)Control(p, "ToggleDiscardFeedback").GetType().GetField("Element", Fields).GetValue(Control(p, "ToggleDiscardFeedback"))).Text == "提示 关",
                "feedback label reflects the latest preference");
            Click(p, Control(p, "ToggleDiscardFeedback")); prepare();
            Check(host.Preferences.Value.DiscardFeedbackEnabled, "same button restores feedback on");
            foreach (int action in new[] { 0, 1, 2 })
            {
                var on = Rect(Control(p, "Enable", action)); var off = Rect(Control(p, "Disable", action));
                Check(on.Width < 80 && Math.Abs(off.X - on.Right - 4) < .01f && on.Height == off.Height, "glyph widths and equal button group gaps/heights");
                Check(Math.Abs(off.Right - (shell.X + shell.Layout.Viewport.Right - 8)) < .01f, "operation group right aligned inside panel");
                var e = (F5Element)Control(p, "Enable", action).GetType().GetField("Element", Fields).GetValue(Control(p, "Enable", action));
                var label = F5Layout.ButtonLabel(e); var line = F5Layout.ButtonUnderline(e);
                Check(Math.Abs(label.X + label.Width / 2 - (on.X + on.Width / 2)) < .01f && line.Width < on.Width && line.Bottom <= on.Bottom, "centered label and bounded short underline");
            }
            var panels = ((System.Collections.Generic.List<F5Element>)typeof(ItemsPresentation).GetField("elements", Fields).GetValue(p)).Where(e => e.Kind == F5ElementKind.Panel).ToArray();
            Check(panels.Length == 3 && panels.All(e => e.Rect.Width == shell.Layout.Viewport.Width), "three complete common row panels");
            var a = Rect(Control(p, "Replace", (int)ItemListKind.Sell, 2337)); var b = Rect(Control(p, "Replace", (int)ItemListKind.Sell, 2338));
            Check(a.Y == b.Y && b.X > a.Right && a.Width > a.Height && a.Y > panels[1].Rect.Bottom && a.Bottom < panels[2].Rect.Y, "compact sale icons belong between sale and discard rows");
            int builds = p.LayoutBuildCount; for (int i = 0; i < 100; i++) prepare(); Check(p.LayoutBuildCount == builds, "stable frames do not rebuild controls");
            Click(p, Control(p, "Enable", 1)); prepare(); long revision = host.Preferences.Revision;
            Click(p, Control(p, "Enable", 1)); prepare();
            Check(host.Preferences.Value.SellEnabled && revision == host.Preferences.Revision && Main.npcShop == 0, "explicit on is idempotent while no shop is open");
            Click(p, Control(p, "Disable", 1)); prepare(); Check(!host.Preferences.Value.SellEnabled, "explicit off");
            var resizeOn = Control(p, "Enable", 1); Pointer(p, resizeOn, true);
            Pointer(p, resizeOn, false, shell.Layout.Matches(1600, 900, 1, 0)); prepare();
            Check(!host.Preferences.Value.SellEnabled && p.ConsumeLeft, "resize before reflow cancels old release and consumes mouse tail");
            var legacy = new ItemAutomationCodec(ID.ItemID.Count).Decode(System.Text.Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.ItemAutomation\",\"version\":1,\"stackEnabled\":false,\"sellEnabled\":false,\"discardEnabled\":false,\"sellTypes\":[2337,2338,2339],\"discardTypes\":[],\"stackBinding\":112,\"sellBinding\":113,\"discardBinding\":114}"));
            host.Change(legacy); prepare();
            Main.keyState = new KeyboardState(Keys.F1, Keys.F2, Keys.F3); p.BeforeInput(true); p.ProcessInput(true, Main.keyState, Vector2.Zero);
            Check(host.Preferences.Value.Equals(legacy) && Main.keyState.IsKeyDown(Keys.F1), "legacy nonzero keys cannot change settings or consume normal game sample");
            Main.LocalPlayer.inventory[10] = new Item { type = 8, stack = 4, favorited = true };
            Main.LocalPlayer.inventory[11] = new Item { type = 101, stack = 2 };
            Main.LocalPlayer.inventory[12] = new Item { type = 102, stack = 1 };
            var add = Control(p, "Add", (int)ItemListKind.Sell); Pointer(p, add, true); prepare(); Check(!p.Selecting, "press alone cannot open picker"); Pointer(p, add, false); prepare();
            Check(p.Selecting && !shell.BeforeLeave(9), "Notes denied leave preserves picker");
            var inlineCandidate = Rect(Control(p, "Select", type: 8));
            Check(inlineCandidate.Width < inlineCandidate.Height && inlineCandidate.Height == 48 && HasControl(p, "Enable", 0), "inline candidate grid retains legal feature controls");
            Click(p, Control(p, "Select", type: 8)); prepare(); Click(p, Control(p, "Select", type: 101)); prepare();
            Check(!host.Preferences.Value.SellTypes.Contains(8), "batch selection remains draft"); Click(p, Control(p, "Confirm")); prepare();
            Check(host.Preferences.Value.SellTypes.Contains(8) && Main.LocalPlayer.inventory[10].stack == 4 && HasControl(p, "Add", argument: (int)ItemListKind.Sell), "commit only changes type list and restores add");
            Click(p, Control(p, "Replace", (int)ItemListKind.Sell, 8)); prepare();
            Click(p, Control(p, "Select", type: 102)); prepare(); Check(!host.Preferences.Value.SellTypes.Contains(8) && host.Preferences.Value.SellTypes.Contains(102), "direct body replacement");
            Click(p, Control(p, "Remove", (int)ItemListKind.Sell, 101)); prepare();
            Check(!host.Preferences.Value.SellTypes.Contains(101) && host.Preferences.Value.DiscardTypes.Count == 0, "remove only corresponding list entry");
            Click(p, Control(p, "Add", (int)ItemListKind.Discard)); prepare(); Click(p, Control(p, "Select", type: 8)); prepare(); Click(p, Control(p, "Cancel")); prepare();
            Check(host.Preferences.Value.DiscardTypes.Count == 0, "cancel does not submit");
            Click(p, Control(p, "Add", (int)ItemListKind.Discard)); prepare(); Main.CurrentInputTextTakerOverride = null; p.BeforeInput(true);
            Main.keyState = new KeyboardState(Keys.Escape); p.ProcessInput(true, Main.keyState, Vector2.Zero); prepare();
            Check(!p.Selecting && Main.keyState.GetPressedKeys().Length == 0 && HasControl(p, "Add", argument: (int)ItemListKind.Discard), "Esc closes picker, restores add and consumes sample");
            PlayerInput.WritingText = false; p.BeforeInput(true); Check(PlayerInput.WritingText, "Esc tail blocks next mapping");
            p.ProcessInput(true, new KeyboardState(), Vector2.Zero); PlayerInput.WritingText = false;
            host.Change(ItemAutomationSettings.Default.WithTypes(ItemListKind.Sell, Enumerable.Range(1000, 5000))); prepare();
            Check(((ICollection)typeof(ItemsPresentation).GetField("controls", Fields).GetValue(p)).Count < 360, "only visible cards are constructed");
            var oldEnable = Control(p, "Enable", 0); Pointer(p, oldEnable, true); shell.ScrollTo(shell.Layout.MaxScroll); Pointer(p, oldEnable, false); prepare();
            Check(!host.Preferences.Value.StackEnabled && !HasControl(p, "Replace", 1000) && HasControl(p, "Replace", 5999), "scroll cancels pressed command and retires hidden card hits");
            shell.ScrollTo(0); font = new object(); measure = t => new F5Size(t.Length * 20, 26, -3, 8); prepare();
            Check(Rect(Control(p, "Enable", 0)).Height > 30.1f, "high offset font drives common row height");
            host.Change(ItemAutomationSettings.Default); prepare(); Click(p, Control(p, "Add", (int)ItemListKind.Discard)); prepare(); notesMayLeave = true;
            Check(shell.BeforeLeave(9) && !p.Selecting, "accepted Notes leave cancels draft"); p.Suspend();
            Main.CurrentInputTextTakerOverride = null; PlayerInput.WritingText = false; Main.keyState = new KeyboardState();
            RunGridGeometry(host);
            RunInlineGeometry(host);
            ItemSelectionChecks.Run(host);
            Console.WriteLine("PASS: Items production geometry, explicit commands, legacy keys inert, inline selection/cancel, input tails, visible cards and stable-frame cache. No graphics device used.");
        }
        private static void RunGridGeometry(HostItems host)
        {
            var shell = new F5Interaction { Ready = true };
            var p = new ItemsPresentation(host, shell); object font = new object();
            var previous = host.Preferences.Value; var inventory = (Item[])Main.LocalPlayer.inventory.Clone();
            shell.Update(new F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1, F5 = true }); shell.Navigate(0);
            Action prepare = () => { shell.Layout.Ensure(1920, 1080, 1, 0, font, t => new F5Size(t.Length * 18, 24)); p.PrepareLayout(Matrix.Identity, new Vector2(1920, 1080)); };
            try
            {
                host.Change(ItemAutomationSettings.Default.WithTypes(ItemListKind.Sell, Enumerable.Range(1000, 20))); prepare();
                AssertGridRow(p, shell, ItemUiCommand.Replace);
                Click(p, Control(p, "Replace", (int)ItemListKind.Sell, 1009)); prepare();
                var selection = (ItemSelection)typeof(ItemsPresentation).GetField("selection", Fields).GetValue(p);
                Check(p.Selecting && selection.Target == 1009, "new last column opens its own replacement target");
                Click(p, Control(p, "Cancel")); prepare();
                for (int i = 0; i < 20; i++) Main.LocalPlayer.inventory[i] = new Item { type = 2000 + i, stack = 1 };
                Click(p, Control(p, "Add", (int)ItemListKind.Sell)); prepare();
                AssertGridRow(p, shell, ItemUiCommand.Select);
                Click(p, Control(p, "Select", type: 2009)); prepare();
                Check(selection.Count == 1 && selection.IsSelected(2009), "new last candidate column selects its own type");
            }
            finally { p.Suspend(); Array.Copy(inventory, Main.LocalPlayer.inventory, inventory.Length); host.Change(previous); }
        }
        private static void AssertGridRow(ItemsPresentation p, F5Interaction shell, ItemUiCommand command)
        {
            var cards = ((System.Collections.Generic.List<ItemUiControl>)typeof(ItemsPresentation).GetField("controls", Fields).GetValue(p))
                .Where(c => c.Command == command && c.Argument == (int)ItemListKind.Sell).OrderBy(c => c.Rect.Y).ThenBy(c => c.Rect.X).ToArray();
            var first = cards.Where(c => c.Rect.Y == cards[0].Rect.Y).ToArray();
            Check(first.Length == 10, "item grid fills ten columns in the current F5 viewport");
            var view = shell.Layout.Viewport.Offset(shell.X, shell.Y);
            Check(first[0].Rect.X == view.X + 8 && first[9].Rect.Right == view.Right - 8, "full row has equal eight-pixel side padding");
            Check(cards[10].Rect.Y == cards[0].Rect.Bottom + 4, "next row follows the current card height and gap");
            Check(first.All(c => c.Rect.Width < (command == ItemUiCommand.Select ? 48 : 50)), "only button width is reduced");
        }
        private static F5Rect Rect(object control) { return (F5Rect)control.GetType().GetField("Rect", Fields).GetValue(control); }
        private static void RunInlineGeometry(HostItems host)
        {
            var shell = new F5Interaction { Ready = true }; var p = new ItemsPresentation(host, shell);
            shell.Update(new F5Input { Active = true, Focused = true, Width = 1280, Height = 720, Scale = 1, F5 = true }); shell.Navigate(0);
            object font = new object(); Func<string, F5Size> measure = t => new F5Size(t.Length * 18, 24);
            Action prepare = () => { shell.Layout.Ensure(1280, 720, 1, 0, font, measure); p.PrepareLayout(Matrix.Identity, new Vector2(1280, 720)); };
            host.Change(ItemAutomationSettings.Default); prepare();
            var state = (ItemSelection)typeof(ItemsPresentation).GetField("selection", Fields).GetValue(p);
            var geometry = (ItemsLayout)typeof(ItemsPresentation).GetField("layout", Fields).GetValue(p);
            float originalDiscardY = Rect(Control(p, "Add", (int)ItemListKind.Discard)).Y;
            var originalAdd = Control(p, "Add", (int)ItemListKind.Sell);
            var originalOn = Rect(Control(p, "Enable", (int)ItemActionKind.Sell));
            Click(p, originalAdd); prepare();
            Check(!HasControl(p, "Add", argument: (int)ItemListKind.Sell) && HasControl(p, "Add", argument: (int)ItemListKind.Discard), "active row hides add while other row retains its entry");
            Check(Math.Abs(Rect(Control(p, "Enable", (int)ItemActionKind.Sell)).X - originalOn.X) < .01f, "hiding add keeps on/off positions stable");
            var confirm = Rect(Control(p, "Confirm"));
            Check(!(bool)Control(p, "Confirm").GetType().GetField("Enabled", Fields).GetValue(Control(p, "Confirm")), "zero selection retains weak inactive confirm");
            Check(Rect(Control(p, "Add", (int)ItemListKind.Discard)).Y > originalDiscardY && geometry.Header.Y > geometry.RowY[1], "inline content expands under owner and shifts following row");
            Click(p, Control(p, "Select", type: 8)); prepare();
            var selectedConfirm = Rect(Control(p, "Confirm"));
            Check(confirm.X == selectedConfirm.X && confirm.Width == selectedConfirm.Width && state.Count == 1, "count changes do not move confirm");
            Click(p, originalAdd); prepare(); Check(state.Count == 1 && state.List == ItemListKind.Sell, "hidden add has no stale hit target or draft effect");
            Click(p, Control(p, "Enable", (int)ItemActionKind.Discard)); prepare();
            Check(state.Count == 1 && host.Preferences.Value.DiscardEnabled && !host.Preferences.Value.SellTypes.Contains(8), "other row controls remain legal without committing draft");
            Click(p, Control(p, "Add", (int)ItemListKind.Discard)); prepare();
            Check(state.List == ItemListKind.Discard && state.Count == 0 && HasControl(p, "Replace", 2337), "one inline region; switching restores previous configured icons");
            Check(HasControl(p, "Add", argument: (int)ItemListKind.Sell) && !HasControl(p, "Add", argument: (int)ItemListKind.Discard), "switching restores previous add and hides current add");
            var stableCandidates = state.Candidates; int stableGeneration = state.Generation;
            var discardOn = Rect(Control(p, "Enable", (int)ItemActionKind.Discard));
            Click(p, Control(p, "Select", type: 8)); prepare();
            Click(p, Control(p, "ToggleDiscardFeedback")); prepare();
            Check(!host.Preferences.Value.DiscardFeedbackEnabled && state.List == ItemListKind.Discard && state.Count == 1 && state.IsSelected(8) &&
                ReferenceEquals(stableCandidates, state.Candidates) && state.Generation == stableGeneration,
                "feedback toggle retains the open picker, stable candidates and selected draft");
            Check(!HasControl(p, "Add", argument: (int)ItemListKind.Discard) && Math.Abs(Rect(Control(p, "Enable", (int)ItemActionKind.Discard)).X - discardOn.X) < .01f,
                "feedback toggle leaves hidden add and explicit action positions intact");
            Click(p, Control(p, "Confirm")); prepare();
            Check(host.Preferences.Value.DiscardTypes.Contains(8) && !host.Preferences.Value.DiscardFeedbackEnabled && HasControl(p, "Add", argument: (int)ItemListKind.Discard),
                "confirm merges its types into the latest feedback preference and restores add");
            Click(p, Control(p, "Replace", (int)ItemListKind.Sell, 2337)); prepare();
            Check(state.List == ItemListKind.Sell && state.Target == 2337 && !HasControl(p, "Confirm", 0), "configured body directly switches to replacement with no confirm");
            Check(!HasControl(p, "Add", argument: (int)ItemListKind.Sell), "replacement selector also hides redundant add");
            Click(p, Control(p, "Cancel")); prepare();
            Check(HasControl(p, "Add", argument: (int)ItemListKind.Sell) && HasControl(p, "Add", argument: (int)ItemListKind.Discard), "cancel restores both add entries");
            var remove = Control(p, "Remove", (int)ItemListKind.Sell, 2337); var body = Rect(Control(p, "Replace", (int)ItemListKind.Sell, 2337));
            Check(Rect(remove).Width == 18 && Rect(remove).Right == body.Right && Rect(remove).Y == body.Y, "visible cross has bounded top-right hit region");
            Click(p, remove); prepare(); Check(!p.Selecting && !host.Preferences.Value.SellTypes.Contains(2337) && p.ConsumeLeft, "cross wins over body and consumes release after card shift");
            Pointer(p, remove, false); prepare(); Check(host.Preferences.Value.SellTypes.Contains(2338) && !p.Selecting, "same gesture cannot activate shifted neighbor");
            var bounds = shell.Layout.Viewport.Offset(shell.X, shell.Y);
            var target = new F5Rect(bounds.X + 100, bounds.Y + 4, 50, 34);
            var hint = ItemsPresentation.Tooltip(target, bounds, 300, 60);
            Check(hint.Y > target.Bottom && hint.Right <= bounds.Right && hint.Bottom <= bounds.Bottom, "tooltip flips below and stays away from target within bounds");
            host.Change(ItemAutomationSettings.Default.WithTypes(ItemListKind.Sell, Enumerable.Range(1000, 5000))); prepare(); shell.ScrollTo(700); prepare();
            float oldScroll = shell.Scroll; font = new object(); measure = t => new F5Size(t.Length * 20, 26, -3, 8); prepare();
            Check(shell.Scroll == oldScroll, "font reflow does not reset valid dynamic page offset");
            host.Change(ItemAutomationSettings.Default); prepare(); Check(shell.Scroll == 0 && shell.Layout.MaxScroll == 0, "collapse clamps without invented empty scroll content");
            var fontFeedback = (ItemUiControl)Control(p, "ToggleDiscardFeedback");
            var fontLabel = F5Layout.ButtonLabel(fontFeedback.Element);
            Check(fontLabel.Width <= fontFeedback.Rect.Width && fontLabel.Height <= fontFeedback.Rect.Height && fontLabel.X >= fontFeedback.Rect.X && fontLabel.Right <= fontFeedback.Rect.Right,
                "replacement font keeps the complete feedback label within the shared button");
            shell.Update(new F5Input { Active = true, Focused = true, Width = 800, Height = 220, Scale = 1 });
            Action small = () => { shell.Layout.Ensure(800, 220, 1, 0, font, measure); p.PrepareLayout(Matrix.Identity, new Vector2(800, 220)); };
            small(); shell.ScrollTo(geometry.RowY[1]); small();
            Check(shell.Layout.Viewport.Height == 37, "small viewport uses real common geometry");
            Click(p, Control(p, "Add", (int)ItemListKind.Sell)); small();
            Check(p.Selecting && shell.Layout.MaxScroll > 0, "small view opens inline selector using main scroll");
            shell.ScrollTo(geometry.Header.Bottom + 4); small();
            Click(p, Control(p, "Select", type: 8)); small(); Check(state.Count == 1, "small view candidate click updates draft");
            var clippedCandidate = Control(p, "Select", type: 8); float kept = shell.Scroll;
            shell.ScrollTo(geometry.Header.Y); small();
            Pointer(p, clippedCandidate, true); Pointer(p, clippedCandidate, false); small();
            Check(state.Count == 1, "old geometry outside small viewport cannot toggle selection");
            shell.ScrollTo(kept); small(); Check(state.Count == 1 && p.Selecting, "ordinary small-view scroll preserves valid selection");
            p.BeforeInput(true); p.ProcessInput(true, new KeyboardState(Keys.Escape), Vector2.Zero); small();
            Check(!p.Selecting, "Esc cancels from actual small viewport");
            p.Suspend(); Main.CurrentInputTextTakerOverride = null; PlayerInput.WritingText = false;
        }
        internal static void Run(HostItems host, string content = null, string output = null)
        {
            using (var graphics = new F5FixtureGraphics())
            using (var shellRenderer = new F5Renderer())
            {
                ItemDecorationChecks.Run(graphics, output);
                var shell = new F5Interaction { Ready = true }; bool notesMayLeave = false;
                shell.BeforeLeave = page => notesMayLeave;
                var presentation = new ItemsPresentation(host, shell);
                host.Change(ItemAutomationSettings.Default.WithTypes(ItemListKind.Sell, new[] { 100 }).WithTypes(ItemListKind.Discard, new int[0])); host.PollPreferences();
                Main.LocalPlayer.inventory[10] = new Item { type = 8, stack = 4, favorited = true };
                Main.LocalPlayer.inventory[11] = new Item { type = 101, stack = 2 };
                Main.LocalPlayer.inventory[50] = new Item { type = 71, stack = 10 };
                Main.UIScaleMatrix = Matrix.Identity;
                shell.Update(new F5Input { Width = 1920, Height = 1080, Scale = 1, Active = true, Focused = true, F5 = true }); shell.Navigate(0);
                Action prepare = () => { shellRenderer.RefreshResources(); shellRenderer.Prepare(shell, 1920, 1080, 1); presentation.Prepare(true, Matrix.Identity, new Vector2(1920, 1080)); };
                prepare();
                Control(presentation, "Enable", (int)ItemActionKind.Stack);
                Control(presentation, "Disable", (int)ItemActionKind.Stack);
                object add = Control(presentation, "Add", (int)ItemListKind.Sell);
                Pointer(presentation, add, true); Check(!presentation.Selecting, "picker opens on release only");
                Pointer(presentation, add, false); prepare();
                Check(presentation.Selecting && !host.Preferences.Value.SellTypes.Contains(8), "picker draft does not change running list");
                Check(HasControl(presentation, "Select", 8) && !HasControl(presentation, "Select", 71) && !HasControl(presentation, "Select", 100), "favorite supplies type; coins and duplicates excluded");
                Click(presentation, Control(presentation, "Select", type: 8)); prepare();
                Click(presentation, Control(presentation, "Select", type: 101)); prepare();
                Check(!host.Preferences.Value.SellTypes.Contains(8), "multi-select stays draft");
                Check(!shell.BeforeLeave(9) && presentation.Selecting, "existing denied navigation callback preserves draft");
                Click(presentation, Control(presentation, "Confirm")); prepare();
                Check(host.Preferences.Value.SellTypes.SequenceEqual(new[] { 8, 100, 101 }) && Main.LocalPlayer.inventory[10].stack == 4, "confirm commits types without acting on representative items");
                Click(presentation, Control(presentation, "Add", (int)ItemListKind.Discard)); prepare();
                Click(presentation, Control(presentation, "Select", type: 8)); prepare();
                Click(presentation, Control(presentation, "Cancel")); prepare();
                Check(host.Preferences.Value.DiscardTypes.Count == 0, "cancel drops draft");
                Main.LocalPlayer.inventory[12] = new Item { type = 102, stack = 1 };
                Click(presentation, Control(presentation, "Replace", (int)ItemListKind.Sell, 100)); prepare();
                Click(presentation, Control(presentation, "Select", type: 102)); prepare();
                Check(host.Preferences.Value.SellTypes.SequenceEqual(new[] { 8, 101, 102 }), "replace commits exactly one type");
                Click(presentation, Control(presentation, "Remove", (int)ItemListKind.Sell, 101)); prepare();
                Check(host.Preferences.Value.SellTypes.SequenceEqual(new[] { 8, 102 }), "remove changes only selected list member");
                Click(presentation, Control(presentation, "Add", (int)ItemListKind.Discard)); prepare();
                Main.CurrentInputTextTakerOverride = null; presentation.BeforeInput(true);
                bool inventoryTriggered = !PlayerInput.WritingText; PlayerInput.WritingText = false;
                Main.keyState = new KeyboardState(Keys.Escape); presentation.ProcessInput(true, Main.keyState, Vector2.Zero); prepare();
                Check(!inventoryTriggered && !presentation.Selecting && Main.keyState.GetPressedKeys().Length == 0, "Esc cancels picker without native inventory action");
                Main.CurrentInputTextTakerOverride = null; presentation.BeforeInput(true);
                Check(PlayerInput.WritingText, "held Esc tail blocks next original input mapping");
                PlayerInput.WritingText = false; Main.keyState = new KeyboardState(); presentation.ProcessInput(true, Main.keyState, Vector2.Zero); prepare();
                Main.CurrentInputTextTakerOverride = null; presentation.BeforeInput(true);
                notesMayLeave = true;
                Click(presentation, Control(presentation, "Add", (int)ItemListKind.Discard)); prepare();
                Check(shell.BeforeLeave(9) && !presentation.Selecting, "accepted leave cancels picker without replacing Notes gate");
                prepare();
                Draw(graphics, shellRenderer, shell, presentation, null);
                // A taller offset font and skin replacement use the same real
                // draw pass, clipping, texture ownership and input rectangles.
                FontAssets.MouseText = graphics.Asset("item-offset-font", graphics.CreateFont(12, 26, -3, 8, 39)); prepare();
                Draw(graphics, shellRenderer, shell, presentation, null);
                ItemAutomationSettings beforeLongList = host.Preferences.Value;
                host.Change(beforeLongList.WithTypes(ItemListKind.Sell, Enumerable.Range(1000, 5000))); prepare();
                Check(((ICollection)typeof(ItemsPresentation).GetField("controls", Fields).GetValue(presentation)).Count < 360, "large valid list builds only visible controls");
                shell.ScrollTo(shell.Layout.MaxScroll); prepare(); Draw(graphics, shellRenderer, shell, presentation, null);
                host.Change(beforeLongList); shell.ScrollTo(0); prepare();
                if (content != null)
                {
                    var services = new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService), new GraphicsService(graphics.Device));
                    using (var reader = new XnbReader(services))
                    using (Texture2D skin = Read<Texture2D>(reader, Path.Combine(content, "Images/Inventory_Back.xnb")))
                    using (Texture2D pixel = Read<Texture2D>(reader, Path.Combine(content, "Images/MagicPixel.xnb")))
                    {
                        var previousPixel = TextureAssets.MagicPixel;
                        try
                        {
                            TextureAssets.MagicPixel = graphics.Asset("actual-item-pixel", pixel);
                            using (var markers = new ItemsRenderer())
                            {
                                markers.Refresh();
                                ItemDecorationChecks.Draw(graphics, markers, 1, Path.Combine(output, "markers-original-scale1.png"));
                                ItemDecorationChecks.Draw(graphics, markers, 1.5f, Path.Combine(output, "markers-original-scale1_5.png"));
                            }
                            Console.WriteLine("PASS: original MagicPixel " + pixel.Width + "x" + pixel.Height + " marker bounds at 1/1.5 UI scale; page previews use this asset.");
                            FontAssets.MouseText = graphics.Asset("actual-item-font", Read<DynamicSpriteFont>(reader, Path.Combine(content, "Fonts/Mouse_Text.xnb")));
                            TextureAssets.InventoryBack = graphics.Asset("actual-item-skin", skin);
                            var icons = new System.Collections.Generic.List<Texture2D>();
                            foreach (int type in new[] { 8, 100, 101, 102, 2337, 2338, 2339 }.Concat(Enumerable.Range(1000, 180)))
                            {
                                var texture = Read<Texture2D>(reader, Path.Combine(content, "Images/Item_" + type + ".xnb"));
                                icons.Add(texture); TextureAssets.Item[type] = graphics.Asset("actual-item-" + type, texture);
                            }
                            host.Change(ItemAutomationSettings.Default); shell.Navigate(9); prepare();
                            Draw(graphics, shellRenderer, shell, presentation, Path.Combine(output, "information-original.png"));
                            shell.Navigate(0); prepare(); presentation.ProcessInput(true, new KeyboardState(), Vector2.Zero);
                            Draw(graphics, shellRenderer, shell, presentation, Path.Combine(output, "items-original-short-scrollbar.png"));
                            Pointer(presentation, Control(presentation, "Replace", (int)ItemListKind.Sell, 2337), false); prepare();
                            Draw(graphics, shellRenderer, shell, presentation, Path.Combine(output, "items-body-hover.png"));
                            Pointer(presentation, Control(presentation, "Remove", (int)ItemListKind.Sell, 2337), false); prepare();
                            Draw(graphics, shellRenderer, shell, presentation, Path.Combine(output, "items-remove-hover.png"));
                            Click(presentation, Control(presentation, "Replace", (int)ItemListKind.Sell, 2337)); prepare();
                            Draw(graphics, shellRenderer, shell, presentation, Path.Combine(output, "items-inline-replace.png"));
                            Click(presentation, Control(presentation, "Cancel")); prepare();
                            host.Change(ItemAutomationSettings.Default.WithTypes(ItemListKind.Sell, Enumerable.Range(1000, 180).Concat(new[] { 2337, 2338, 2339 }))); prepare();
                            shell.ScrollTo(180); prepare();
                            Draw(graphics, shellRenderer, shell, presentation, Path.Combine(output, "items-long-list.png"));
                            host.Change(ItemAutomationSettings.Default); shell.ScrollTo(0); prepare();
                            Click(presentation, Control(presentation, "Add", (int)ItemListKind.Discard)); prepare();
                            Click(presentation, Control(presentation, "Select", type: 8)); prepare();
                            Click(presentation, Control(presentation, "Select", type: 101)); prepare();
                            Draw(graphics, shellRenderer, shell, presentation, Path.Combine(output, "items-inline-multiselect.png"));
                            Click(presentation, Control(presentation, "Cancel")); prepare();
                            host.Change(ItemAutomationSettings.Default); shell.ScrollTo(0); prepare();
                            using (var replacement = new Texture2D(graphics.Device, 32, 32))
                            {
                                replacement.SetData(Enumerable.Repeat(new Color(40, 88, 64), 1024).ToArray());
                                TextureAssets.InventoryBack = graphics.Asset("synthetic-item-reskin", replacement); prepare();
                                Draw(graphics, shellRenderer, shell, presentation, Path.Combine(output, "items-reskin.png"));
                                presentation.Suspend(); Check(!replacement.IsDisposed && !skin.IsDisposed, "borrowed current and old skins survive suspension");
                            }
                            foreach (Texture2D icon in icons) icon.Dispose();
                        }
                        finally { TextureAssets.MagicPixel = previousPixel; }
                    }
                }
                presentation.Suspend();
                object renderer = typeof(ItemsPresentation).GetField("renderer", Fields).GetValue(presentation);
                Check(renderer.GetType().GetField("clipped", Fields).GetValue(renderer) == null, "owned rasterizer released on suspend");
                Console.WriteLine("PASS: Items production picker, release commit, draft/cancel/navigation, real XNA clipping/state and font replacement. Preview uses fixture item labels.");
            }
        }
        private static void Draw(F5FixtureGraphics graphics, F5Renderer renderer, F5Interaction shell, ItemsPresentation items, string path)
        {
            using (var target = new RenderTarget2D(graphics.Device, 1920, 1080))
            {
                graphics.Device.SetRenderTarget(target); graphics.Device.Clear(Color.Transparent); Main.spriteBatch.Begin();
                Rectangle clip = graphics.Device.ScissorRectangle;
                renderer.Draw(shell, Matrix.Identity, false, false); items.Draw();
                Check(graphics.Device.ScissorRectangle == clip, "item pass restores caller scissor");
                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, 8, 8), Color.Red);
                Main.spriteBatch.End(); graphics.Device.SetRenderTarget(null);
                var pixels = new Color[1920 * 1080]; target.GetData(pixels);
                Check(pixels[3 * 1920 + 3].R == 255 && pixels[100 * 1920 + 100].A == 0, "restored batch still draws; distant pixels stay clipped");
                F5Rect view = shell.Layout.Viewport.Offset(shell.X, shell.Y); bool ink = false;
                for (int y = (int)view.Y; y < view.Bottom; y++) for (int x = (int)view.X; x < view.Right; x++) ink |= pixels[y * 1920 + x].A != 0;
                Check(ink, "item content produces real pixels");
                if (path != null) { Directory.CreateDirectory(Path.GetDirectoryName(path)); using (var stream = File.Create(path)) target.SaveAsPng(stream, 1920, 1080); }
            }
        }
        private static object Control(ItemsPresentation presentation, string command, int argument = -1, int type = -1)
        {
            foreach (object control in (IEnumerable)typeof(ItemsPresentation).GetField("controls", Fields).GetValue(presentation))
            {
                Type t = control.GetType();
                if (t.GetField("Command", Fields).GetValue(control).ToString() == command &&
                    (argument < 0 || (int)t.GetField("Argument", Fields).GetValue(control) == argument) &&
                    (type < 0 || (int)t.GetField("Type", Fields).GetValue(control) == type)) return control;
            }
            throw new InvalidOperationException("Expected visible control missing: " + command + "/" + argument + "/" + type);
        }
        private static bool HasControl(ItemsPresentation p, string command, int type = -1, int argument = -1) { try { Control(p, command, argument, type); return true; } catch (InvalidOperationException) { return false; } }
        private static void Pointer(ItemsPresentation presentation, object control, bool pressed, bool geometryCurrent = true)
        {
            F5Rect rect = (F5Rect)control.GetType().GetField("Rect", Fields).GetValue(control);
            var point = new Vector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            PlayerInput.MouseInfo = new MouseState((int)point.X, (int)point.Y, 0, pressed ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            presentation.ProcessInput(true, new KeyboardState(), point, geometryCurrent);
        }
        private static void Click(ItemsPresentation presentation, object control) { Pointer(presentation, control, true); Pointer(presentation, control, false); }
        private static T Read<T>(XnbReader reader, string path) where T : class { using (var file = File.OpenRead(path)) return reader.FromStream<T>(file); }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("ITEM UI CHECK FAILED: " + message); }
        private sealed class GraphicsService : IGraphicsDeviceService
        {
            public GraphicsDevice GraphicsDevice { get; }
            internal GraphicsService(GraphicsDevice device) { GraphicsDevice = device; }
            public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
            public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
            public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
            public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
        }
    }
}
