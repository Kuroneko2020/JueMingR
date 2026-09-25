using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.WorldObjectText;
using JueMingR.TerrariaHost.WorldObjectText;
using Microsoft.Xna.Framework;
using Terraria.UI.Chat;

namespace NativeWorldTextProbe
{
    internal static class NativeChecks
    {
        internal static int Run(string content, string output, string scope)
        {
            if(scope=="RecoveryCpu" || scope=="RecoveryVisual")
            {
                Terraria.Program.SavePath=Path.Combine(Path.GetTempPath(),"JueMingR-native-recovery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Terraria.Program.SavePath);
                NativeQuickItemChecks.Run(scope=="RecoveryVisual"?(Action<object>)(context=>{using(var graphics=new ProbeGraphics(content))NativeRecoveryVisualChecks.Run(context,graphics,output);}):null,recovery:true);return 0;
            }
            if (scope == "AboutCpu" || scope == "AboutVisual")
            {
                Terraria.Program.SavePath = Path.Combine(Path.GetTempPath(), "JueMingR-native-about-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Terraria.Program.SavePath);
                NativeQuickItemChecks.Run(context => {
                    NativeAboutChecks.Run(context);
                    if (scope == "AboutVisual") using (var graphics = new ProbeGraphics(content)) NativeAboutVisualChecks.Run(context, graphics, output);
                }, true, true); return 0;
            }
            if (scope == "CoinDepositCpu" || scope == "CoinDepositVisual")
            {
                Terraria.Program.SavePath = Path.Combine(Path.GetTempPath(), "JueMingR-native-coins-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Terraria.Program.SavePath);
                NativeQuickItemChecks.Run(context => NativeCoinChecks.Run(context, scope == "CoinDepositVisual" ? (Action<object>)(current =>
                { using (var graphics = new ProbeGraphics(content)) NativeCoinUiChecks.Run(current, graphics, output); }) : null), true); return 0;
            }
            if (scope == "QuickItemsCpu" || scope == "QuickItemsVisual")
            {
                Terraria.Program.SavePath = Path.Combine(Path.GetTempPath(), "JueMingR-native-quick-items-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Terraria.Program.SavePath);
                NativeQuickItemChecks.Run(scope=="QuickItemsVisual"?(Action<object>)(context=>{using(var graphics=new ProbeGraphics(content))NativeQuickUiChecks.Run(context,graphics,output);}):null);return 0;
            }
            if (scope == "BrowserCpu" || scope == "BrowserVisual")
            {
                Terraria.Program.SavePath = Path.Combine(Path.GetTempPath(), "JueMingR-native-browser-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Terraria.Program.SavePath); NativeBrowserChecks.Run();
                if (scope == "BrowserVisual") using (var graphics = new ProbeGraphics(content)) NativeBrowserVisualChecks.Run(graphics, output);
                return 0;
            }
            if (scope == "ExplorationRelease") scope = "ExplorationCpu";
            if (scope != "Full" && scope != "SelectionCpuCosts" && scope != "SelectionCpuChecks" && scope != "WorkloadCpu" && scope != "InformationCpu" && scope != "GuidanceCpu" && scope != "GuidanceVisual" && scope != "DeathCpu" && scope != "DeathVisual" && scope != "ExplorationCpu" && scope != "MapVisual" && scope != "MapAlignment" && scope != "FootprintsCpu" && scope != "FootprintsVisual") throw new ArgumentException("Unknown probe scope");
            if (IntPtr.Size != 4 || typeof(object).Assembly.GetName().Name != "mscorlib") throw new InvalidOperationException("Native workload requires .NET Framework x86.");
            string isolated = Path.Combine(Path.GetTempPath(), "JueMingR-native-text-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(isolated);
            // Main's beforefieldinit constructor reads Program.SavePath. This is
            // set before a separate no-inline method touches any Main field.
            Terraria.Program.SavePath = isolated;
            return Check(content, output, scope);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Check(string content, string output, string scope)
        {
            var actual = typeof(ChatManager).Assembly;
            string hash; using (var stream = File.OpenRead(actual.Location)) using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            Require(hash == "960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3" && actual.ManifestModule.ModuleVersionId == new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083"), "actual fixed EXE identity");
            ChatManager.Register<Terraria.GameContent.UI.Chat.ColorTagHandler>("c", "color");
            ChatManager.Register<Terraria.GameContent.UI.Chat.ItemTagHandler>("i", "item");
            Terraria.Localization.LanguageManager.Instance.SetLanguage("en-US"); Terraria.Lang.InitializeLegacyLocalization();
            Require(Terraria.Lang.chestType[10].Value == "Ivy Chest" && Terraria.Lang.dresserType[0].Value == "Dresser", "actual native language tables loaded");
            var parsed = ChatManager.ParseMessage("[c/ff0000:literal]", Color.White);
            Require(parsed.Count == 1 && parsed[0].Text == "literal" && parsed[0].Color == Color.Red, "actual native color parser");
            Console.WriteLine("PASS: real ChatManager assembly, MVID, hash and parser; no fixture Terraria assembly.");
            if (scope == "ExplorationCpu") { NativeExplorationChecks.Run(); NativeMapMarkerChecks.Run(output); return 0; }
            if (scope == "FootprintsCpu") { NativeFootprintChecks.Run(output); return 0; }
            if (scope == "DeathCpu")
            {
                Terraria.Main.dedServ = true;
                try { RuntimeHelpers.RunClassConstructor(typeof(Terraria.Graphics.Capture.CaptureManager).TypeHandle); }
                finally { Terraria.Main.dedServ = false; }
                NativeDeathSourceChecks.Run(); return 0;
            }
            NativeTextAnchorChecks.Metrics();
            NativeLayerReadinessChecks.Run();
            if (content == "--metrics") return 0;
            if (scope == "SelectionCpuCosts") { FiniteCostChecks.RunSelection(output); return 0; }
            if (scope == "SelectionCpuChecks" || scope == "WorkloadCpu" || scope == "InformationCpu" || scope == "GuidanceCpu")
            {
                if (scope == "SelectionCpuChecks") FiniteCostChecks.RunSelection(output);
                else
                {
                    // Explicit CPU fixture setup; no timed sampling is a hidden prerequisite.
                    Terraria.Main.gameMenu = Terraria.Main.dedServ = Terraria.Main.hideUI = Terraria.Main.mapFullscreen = Terraria.Main.inFancyUI = Terraria.Main.onlyDrawFancyUI = Terraria.Main.ingameOptionsWindow = false;
                    Terraria.Main.netMode = Terraria.Main.myPlayer = 0; Terraria.Main.screenWidth = 960; Terraria.Main.screenHeight = 640;
                    Terraria.Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null);
                    Terraria.Main.GameViewMatrix.SetViewportOverride(new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 960, 640));
                    FiniteCostChecks.SetCpuFont(10);
                    Terraria.Main.player[0] = new Terraria.Player { active = true, accOreFinder = true, position = new Vector2(450, 450), gravDir = 1 };
                }
                // The native capture controller supports headless construction.
                // Initialize its real inactive interface without a GPU camera;
                // restore ordinary client flags before exercising the shell.
                Terraria.Main.dedServ = true;
                try { RuntimeHelpers.RunClassConstructor(typeof(Terraria.Graphics.Capture.CaptureManager).TypeHandle); }
                finally { Terraria.Main.dedServ = false; }
                if (scope == "InformationCpu") { NativeInformationChecks.RunCpu(); return 0; }
                if (scope == "GuidanceCpu") { NativeGuidanceChecks.RunCpu(); return 0; }
                NativeWorldChecks.RunSelectionCpu();
                NativeCompositionChecks.RunSelectionCpu();
                return 0;
            }
            using (var graphics = new ProbeGraphics(content, scope == "GuidanceVisual"))
            {
                if (scope == "FootprintsVisual") { NativeFootprintVisualChecks.Run(graphics, output); return 0; }
                if (scope == "MapVisual") { NativeMapVisualChecks.Run(graphics, output); return 0; }
                if (scope == "MapAlignment") { NativeMapVisualChecks.Run(graphics, output, true); return 0; }
                if (scope == "DeathVisual") { NativeDeathVisualChecks.Run(graphics, output); return 0; }
                if (scope == "GuidanceVisual") { NativeGuidanceVisualChecks.Run(graphics, output); return 0; }
                var style = WorldObjectSettings.Default.Style(WorldObjectKind.Sign).WithMode(WorldObjectMode.Characters).WithLimits(3, 3);
                foreach (string blank in new[] { new string('\n', 11), "  " })
                {
                    var empty = new NativeWorldTextLayout(graphics.Font, blank, style.WithLimits(3, 1), 460);
                    while (!empty.Ready) empty.Step(512);
                    Require(!empty.HasInk, "truncating an all-blank display must not manufacture an ellipsis label");
                }
                var slash = new NativeWorldTextLayout(graphics.Font, "[c/ff0000:A\\B]", style.WithMode(WorldObjectMode.All), 460);
                while (!slash.Ready) slash.Step(512);
                Require(!slash.Truncated && slash.VisibleUnits == 3 && slash.Snippets.Count == 3 && slash.Snippets[1].Snippet.Text == "\\", "a valid colored backslash remains literal and cannot disable labels");
                var layout = new NativeWorldTextLayout(graphics.Font, "[c/ff0000:Á]😀BZ", style, 460);
                while (!layout.Ready) layout.Step(512);
                Require(layout.Truncated && layout.VisibleUnits == 3 && layout.Lines == 1, "native layout truncates complete visible units");
                var lines = new NativeWorldTextLayout(graphics.Font, "a\r\n\r\nb\r\nc", style.WithMode(WorldObjectMode.Lines).WithLimits(3, 80), 460);
                while (!lines.Ready) lines.Step(512);
                Require(lines.Lines == 3 && lines.Truncated, "blank lines count toward final line cap");
                var wrapped = new NativeWorldTextLayout(graphics.Font, new string('W', 1000000), style.WithMode(WorldObjectMode.All), 120);
                while (!wrapped.Ready) wrapped.Step(512);
                Require(wrapped.Lines == 10 && wrapped.Truncated && wrapped.SourceWork < 4096, "long cold input stops at the final wrapped-line guard");
                var icon = new NativeWorldTextLayout(graphics.Font, "A[i/s20:8]B", style.WithLimits(3, 2), 460);
                while (!icon.Ready) icon.Step(512);
                int icons = 0; foreach (var snippet in icon.Snippets) if (snippet.Snippet.DeleteWhole) icons++;
                Require(icon.Truncated && icon.VisibleUnits == 2 && icons == 1, "real item tag counts as one indivisible visible unit");
                float inventoryScale = Terraria.Main.inventoryScale;
                graphics.Preview(output, layout, lines, wrapped, slash, icon);
                Require(Terraria.Main.inventoryScale == inventoryScale, "native item drawing restores inventory scale");
                NativeWorldChecks.Run(graphics, output);
                FiniteCostChecks.Run(graphics, output);
                NativeCompositionChecks.Run(graphics, output);
                Console.WriteLine("PASS: real-font bounded wrapping, Unicode, native colors and production positioned-snippet drawing.");
            }
            return 0;
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
