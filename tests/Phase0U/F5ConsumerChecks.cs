using System;
using System.Reflection;
using System.Collections;
using Microsoft.Xna.Framework;

namespace Terraria
{
    internal static class F5ConsumerChecks
    {
        internal static void Run(Main main, bool biomeFailure = false)
        {
            using (var graphics = new F5FixtureGraphics())
            {
                CheckGlyphMetrics(graphics);
                Utils.ThrowF5Text = false;
                Main.SampleLeft = Main.SampleRight = Main.SampleF5 = Main.SpecialNpc = false;
                Main.SampleWheel = 0;
                main.RunUpdateLoop(1);
                main.DrawAllFixtureLayers();
                int npc = Main.NpcHits, drop = Main.DropHits;
                Check(npc > 0 && drop > 0 && Main.DrawnText == "NPC", "closed window consumers must work");
                Main.SampleF5 = true;
                main.RunUpdateLoop(1); main.DrawAllFixtureLayers();
                Check(Main.NpcHits == npc + 1 && Main.DropHits == drop + 1, "open window outside must preserve hover");
                Check(Utils.F5TextDraws > 0, "actual Host renderer must draw the open window");
                Main.SampleF5 = false;
                Main.SampleX = 700; Main.SampleY = 350;
                npc = Main.NpcHits; drop = Main.DropHits;
                int bubbles = Main.BubbleDraws, use = Main.UseCount, slot = Main.SelectedSlot;
                Main.SampleLeft = true; Main.SampleWheel = 120;
                main.RunUpdateLoop(1); main.DrawAllFixtureLayers();
                Check(Main.UseCount == use && Main.SelectedSlot == slot, "first click and wheel must not reach world consumers");
                Check(Main.NpcHits == npc && Main.DropHits == drop && Main.BubbleDraws == bubbles && Main.DrawnText != "NPC" && Main.DrawnText != "DROP",
                    "owned pointer must stop actual hover hits and prior bubble drawing");
                Main.SampleLeft = false; Main.SampleWheel = 0;
                main.RunUpdateLoop(1); main.DrawAllFixtureLayers();
                Check(Main.NpcHits == npc && Main.DropHits == drop, "pure hover must remain isolated");
                Main.SpecialNpc = true; Main.SampleRight = true;
                int special = Main.SpecialInteractions;
                main.RunUpdateLoop(1); main.DrawAllFixtureLayers();
                Check(Main.SpecialInteractions == special, "special NPC branch must not bypass ownership");
                Main.SampleRight = false;
                Main.SampleX = 1850; Main.SampleY = 900;
                main.RunUpdateLoop(1); main.DrawAllFixtureLayers();
                Check(Main.NpcHits == npc + 1 && Main.DropHits == drop + 1, "moving outside restores hover without restart");
                Main.SampleRight = true;
                main.RunUpdateLoop(1); main.DrawAllFixtureLayers();
                Check(Main.SpecialInteractions == special + 1, "outside special NPC interaction must remain live");
                Main.SampleRight = false; Main.SpecialNpc = false;
                object state = State();
                object layout = Get(state, "Layout");
                CheckNativeModes(main, state);
                int generation = (int)Get(layout, "Generation"), measurements = (int)Get(layout, "MeasurementCount");
                for (int i = 0; i < 12; i++) Frame(main, 1850, 900);
                Check((int)Get(layout, "Generation") == generation && (int)Get(layout, "MeasurementCount") == measurements,
                    "ordinary real Host updates must not rebuild or remeasure layout");

                // Disabled controls and empty short pages still own their entire window.
                ClickLocal(main, state, 50, 155);
                ClickLocal(main, state, 50, 60); // first top navigation page
                Check((int)Get(state, "Page") == 0, "production navigation must switch the actual page");
                use = Main.UseCount; slot = Main.SelectedSlot;
                FrameLocal(main, state, 200, 400, true, 120);
                FrameLocal(main, state, 200, 400, false, -120);
                Check(Main.UseCount == use && Main.SelectedSlot == slot && (float)Get(state, "Scroll") == 0,
                    "placeholder blank area and short-page scroll boundaries must not penetrate");
                ClickLocal(main, state, 340, 100); // information page (second row, fourth)
                Check((int)Get(state, "Page") == 9, "information navigation must restore actual page");
                ClickBiome(main, state, "DisableBiome");
                int biomeDraws = Main.FixtureDrawCount;
                int observations = Main.FixtureZoneReadCount;
                for (int i = 0; i < 35; i++) FrameLocal(main, state, 40, 150);
                Check(Main.FixtureDrawCount == biomeDraws && Main.FixtureZoneReadCount == observations,
                    "real biome feature must stop both observation and drawing after the off button");
                ClickBiome(main, state, "EnableBiome");
                Check(Main.FixtureDrawCount > biomeDraws, "real biome feature must resume after the on button");
                measurements = Main.PendingMeasurements;
                int hints = Utils.F5HintDraws;
                int layoutMeasurements = (int)Get(layout, "MeasurementCount");
                for (int i = 0; i < 12; i++) Frame(main, Main.SampleX, Main.SampleY);
                Check(Main.PendingMeasurements == measurements && Utils.F5HintDraws > hints &&
                    (int)Get(layout, "MeasurementCount") == layoutMeasurements,
                    "steady F5 hover must draw its own hint without layout or native pending-text measurement");

                // Dragging uses the same geometry and must not rebuild layout.
                generation = (int)Get(layout, "Generation");
                float oldX = (float)Get(state, "X");
                FrameLocal(main, state, 30, 15, true);
                Frame(main, 1500, 800, true);
                npc = Main.NpcHits; drop = Main.DropHits;
                Frame(main, 1900, 1040, true);
                Check(Main.NpcHits == npc && Main.DropHits == drop && (float)Get(state, "X") != oldX,
                    "drag capture outside the clamped window must prevent actual world hover");
                Frame(main, 1900, 1040);
                Check((int)Get(layout, "Generation") == generation, "window movement must not rebuild layout");
                Frame(main, 20, 20);
                Check(Main.NpcHits > npc && Main.DropHits > drop, "drag release must restore outside hover");

                // Ownership of a held click survives closing and focus loss until
                // a real focused release, while the next independent click works.
                FrameLocal(main, state, 40, 145, true);
                use = Main.UseCount;
                Main.SampleF5 = true; Frame(main, 20, 20, true);
                Main.SampleF5 = false;
                FocusHelper.IsSelectedApplication = false; Frame(main, 20, 20, true);
                FocusHelper.IsSelectedApplication = true; Frame(main, 20, 20, true);
                Frame(main, 20, 20);
                Check(Main.UseCount == use, "close/focus/release must not replay an owned click");
                Frame(main, 20, 20, true);
                Check(Main.UseCount == use + 1, "new outside click must work after release");
                Frame(main, 20, 20);

                // Physical inputs at 150% and 1280x720 use the inverse of exactly
                // the matrix used by rendering; the width remains 580 logical.
                GameInput.PlayerInput.FixtureScreen = new Vector2(1280, 720);
                Main.UIScaleMatrix = Matrix.CreateScale(1.5f, 1.5f, 1);
                Main.SampleF5 = true; Frame(main, 20, 20);
                Main.SampleF5 = false; FrameLocal(main, state, 100, 200);
                npc = Main.NpcHits; drop = Main.DropHits;
                FrameLocal(main, state, 100, 200, false, -1200);
                Check(Main.NpcHits == npc && Main.DropHits == drop && (float)Get(state, "Scroll") > 0,
                    "scaled scrolling must share draw/hit coordinates and stop consumers");
                Main.OtherUiHover = true;
                FrameLocal(main, state, 100, 200);
                Check(Main.LocalPlayer.mouseInterface, "F5 release must preserve a prior legal UI state");
                Main.OtherUiHover = false;
                Main.gameMenu = true; FrameLocal(main, state, 30, 15, true);
                Check(!(bool)Get(state, "Visible"), "world exit closes the F5 window");
                Main.gameMenu = false; Frame(main, 20, 20);
                Main.UIScaleMatrix = Matrix.Identity;
                GameInput.PlayerInput.FixtureScreen = new Vector2(1920, 1080);

                // Resource replacement with equal dimensions is not a reflow;
                // metric changes are. The resource wrapper itself may stay live.
                Main.SampleF5 = true; Frame(main, 20, 20); Main.SampleF5 = false;
                generation = (int)Get(layout, "Generation");
                typeof(ReLogic.Content.Asset<ReLogic.Graphics.DynamicSpriteFont>)
                    .GetMethod("SubmitLoadedContent", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(GameContent.FontAssets.MouseText, new object[] { graphics.CreateFont(10, 20), graphics });
                Frame(main, 20, 20);
                Check((int)Get(layout, "Generation") == generation, "same-metric resource replacement preserves layout");
                GameContent.FontAssets.MouseText = graphics.Asset("wider-font", graphics.CreateFont(12, 22));
                Frame(main, 20, 20);
                Check((int)Get(layout, "Generation") == generation + 1, "actual font replacement invalidates layout once");
                // Reset scroll to top so the controlled failure is inside content.
                FrameLocal(main, state, 100, 200, false, 12000);
                if (biomeFailure)
                {
                    Main.FixtureThrowOnDraw = true; main.DrawBiomeLayer(); Main.FixtureThrowOnDraw = false;
                    observations = Main.FixtureZoneReadCount; biomeDraws = Main.FixtureDrawCount;
                    int unavailable = Utils.UnavailableDraws;
                    ClickBiome(main, state, "EnableBiome");
                    for (int i = 0; i < 35; i++) Frame(main, Main.SampleX, Main.SampleY);
                    Check(Main.FixtureZoneReadCount == observations && Main.FixtureDrawCount == biomeDraws &&
                        Utils.UnavailableDraws > unavailable && (bool)Get(state, "Visible"),
                        "failed biome cannot be falsely enabled; F5 must remain usable and draw the existing unavailable hover feedback");
                    Console.WriteLine("PASS: failed biome remains unavailable after the real F5 enable action.");
                    return;
                }
                int cursor = Main.CursorDraws, damage = Main.DamageDraws;
                biomeDraws = Main.FixtureDrawCount;
                Utils.ThrowF5Text = true; FrameLocal(main, state, 100, 200);
                Utils.ThrowF5Text = false;
                Check(!(bool)Get(state, "Visible"), "F5 draw failure must safely close its window");
                Frame(main, 20, 20);
                Check(Main.CursorDraws > cursor && Main.DamageDraws > damage && Main.FixtureDrawCount > biomeDraws,
                    "F5 failure must preserve cursor, unrelated overlays, world updates and biome feature");
                Check(Main.NpcHits > npc && Main.DropHits > drop, "F5 failure must not permanently intercept hover");
                Console.WriteLine("PASS: Phase 0-U real Host consumers, biome controls, lifecycle, cache and XNA state restoration.");
            }
        }
        private static void CheckGlyphMetrics(F5FixtureGraphics graphics)
        {
            Assembly host = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == "JueMingR.TerrariaHost") host = assembly;
            Type type = host.GetType("JueMingR.TerrariaHost.F5.F5Renderer", true);
            var renderer = (IDisposable)Activator.CreateInstance(type, true);
            var original = GameContent.FontAssets.MouseText;
            try
            {
                GameContent.FontAssets.MouseText = graphics.Asset("offset-font", graphics.CreateFont(10, 16, 3, 7, 36));
                type.GetMethod("RefreshResources", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(renderer, null);
                object size = type.GetMethod("Measure", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(renderer, new object[] { "AA" });
                Check((float)Get(size, "Width") == 20 && (float)Get(size, "Height") == 16 &&
                    (float)Get(size, "OffsetX") == 3 && (float)Get(size, "OffsetY") == 7,
                    "Host text metrics must use actual glyph quads and offsets instead of font line spacing");
                using (var icons = host.GetManifestResourceStream("JueMingR.F5.TabIcons.alpha"))
                {
                    Check(icons != null && icons.Length == 13 * 72 * 72,
                        "the actual Host assembly must carry twelve tabs and one rounded keyboard mask");
                    using (var reader = new System.IO.BinaryReader(icons))
                    using (var sha = System.Security.Cryptography.SHA256.Create())
                        Check(BitConverter.ToString(sha.ComputeHash(reader.ReadBytes(12 * 72 * 72))).Replace("-", "") ==
                            "C367D0E155A22010CEDAC61CA62033AF9C33C6753D090D3043FF09A8774BBC4D",
                            "the original twelve tab masks remain byte-identical in the actual Host");
                }
            }
            finally { GameContent.FontAssets.MouseText = original; renderer.Dispose(); }
        }
        private static void CheckNativeModes(Main main, object state)
        {
            Action<bool>[] modes = { value => Main.mapFullscreen = value, value => Main.hideUI = value,
                value => Main.onlyDrawFancyUI = value, value => Main.ingameOptionsWindow = value,
                value => Main.inFancyUI = value, value => Graphics.Capture.CaptureManager.Instance.Active = value };
            foreach (Action<bool> setMode in modes)
            {
                // Arm a real biome command, then hide the window before release.
                ClickBiome(main, state, "EnableBiome");
                ClickBiome(main, state, "DisableBiome", false);
                int draws = Utils.F5TextDraws;
                setMode(true);
                Frame(main, Main.SampleX, Main.SampleY); // the owned release is drained
                int clicks = Main.NativeClicks, wheel = Main.NativeWheel;
                Frame(main, Main.SampleX, Main.SampleY, true, 120);
                Check(!(bool)Get(state, "Visible") && Utils.F5TextDraws == draws &&
                    Main.NativeClicks == clicks + 1 && Main.NativeWheel == wheel + 1,
                    "a native mode that skips F5 drawing must receive new input and close the hidden window");
                setMode(false); Frame(main, 1850, 900);
                Main.SampleF5 = true; Frame(main, 1850, 900); Main.SampleF5 = false;
                Check((bool)Get(state, "Visible"), "F5 must reopen normally after the native mode ends");
                int biomeDraws = Main.FixtureDrawCount; Frame(main, 1850, 900);
                Check(Main.FixtureDrawCount > biomeDraws, "a hidden armed disable button must not execute on release");
            }
            FrameLocal(main, state, 100, 200);
            Main.SampleCapture = true;
            int nativeClicks = Main.NativeClicks;
            FrameLocal(main, state, 100, 200, true, 120);
            Check(!(bool)Get(state, "Visible") && Main.DrawnText == "CAPTURE" && Main.NativeClicks == nativeClicks + 1,
                "same-frame capture activation must retain native input and its actual pending tooltip");
            Main.SampleCapture = false; Graphics.Capture.CaptureManager.Instance.Active = false;
            Frame(main, 1850, 900);
            Main.SampleF5 = true; Frame(main, 1850, 900); Main.SampleF5 = false;
        }
        private static object State()
        {
            Assembly host = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == "JueMingR.TerrariaHost") host = assembly;
            object context = host.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker")
                .GetField("postfixContext", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            return Get(Get(context, "Shell"), "State");
        }
        private static object Get(object value, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            FieldInfo field = value.GetType().GetField(name, flags);
            return field != null ? field.GetValue(value) : value.GetType().GetProperty(name, flags).GetValue(value);
        }
        private static void Frame(Main main, int x, int y, bool left = false, int wheel = 0)
        {
            Main.SampleX = x; Main.SampleY = y; Main.SampleLeft = left; Main.SampleWheel = wheel;
            main.RunUpdateLoop(1); main.DrawAllFixtureLayers(); Main.SampleWheel = 0;
        }
        private static void FrameLocal(Main main, object state, float x, float y, bool left = false, int wheel = 0)
        { Frame(main, (int)(((float)Get(state, "X") + x) * Main.UIScaleMatrix.M11),
            (int)(((float)Get(state, "Y") + y) * Main.UIScaleMatrix.M22), left, wheel); }
        private static void ClickLocal(Main main, object state, float x, float y)
        { FrameLocal(main, state, x, y, true); FrameLocal(main, state, x, y); }
        private static void ClickBiome(Main main, object state, string command, bool release = true)
        {
            object layout = Get(state, "Layout"), element = null;
            foreach (object candidate in (IEnumerable)Get(layout, "Elements"))
                if (Get(candidate, "Command").ToString() == command) element = candidate;
            Check(element != null, "real biome command must exist on the production page");
            object rect = Get(element, "Rect"), view = Get(layout, "Viewport");
            float y = (float)Get(rect, "Y") + (float)Get(rect, "Height") / 2;
            for (int i = 0; i < 25 && y - (float)Get(state, "Scroll") > (float)Get(view, "Height") - 10; i++)
                FrameLocal(main, state, 100, 200, false, -120);
            float x = (float)Get(view, "X") + (float)Get(rect, "X") + (float)Get(rect, "Width") / 2;
            float localY = (float)Get(view, "Y") + y - (float)Get(state, "Scroll");
            FrameLocal(main, state, x, localY, true);
            if (release) FrameLocal(main, state, x, localY);
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
