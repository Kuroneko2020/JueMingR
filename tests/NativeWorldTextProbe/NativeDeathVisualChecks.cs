using System;
using System.IO;
using System.Reflection;
using System.Threading;
using JueMingR.Platform.DeathHistory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeDeathVisualChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void Run(ProbeGraphics graphics, string output)
        {
            Directory.CreateDirectory(output); string root = Path.Combine(Terraria.Program.SavePath, "death-visual"); Directory.CreateDirectory(root);
            Main.gameMenu = Main.dedServ = Main.hideUI = Main.mapFullscreen = Main.inFancyUI = Main.onlyDrawFancyUI = Main.ingameOptionsWindow = false;
            Main.netMode = Main.myPlayer = 0; Main.screenWidth = 960; Main.screenHeight = 640;
            typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.Identity);
            Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(graphics.GraphicsDevice); Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, 960, 640));
            Main.player[0] = new Player { active = true, name = "隔离预览", position = new Vector2(320, 320), gravDir = 1 };
            Main.ActivePlayerFileData = new Terraria.IO.PlayerFileData(Path.Combine(root, "isolated.plr"), false) { Player = Main.player[0] };
            Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(root, "isolated.wld"), false) { UniqueId = Guid.NewGuid() };
            Main.maxTilesX = 4200; Main.maxTilesY = 1200; Main.npc = new NPC[Main.maxNPCs];
            Main.dedServ = true; try { System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Terraria.Graphics.Capture.CaptureManager).TypeHandle); } finally { Main.dedServ = false; }
            var assembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll"));
            object context = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker").GetNestedType("PostfixContext", Flags), Flags, null,
                new object[] { "death-history-" + new string('8', 40), Path.Combine(root, "evidence.txt"), root }, null);
            object host = null; var originalMap = Main.MapIcons; Main.MapIcons = new Terraria.Map.MapIconOverlay();
            try
            {
                Call(context, "InstallInformationSources"); Call(context, "InitializeRuntime", true); host = Get(context, "DeathRecords");
                Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(host, "ControlsEnabled") && ((JueMingR.Features.DeathHistory.DeathHistory)Get(host, "History")).Snapshot.Known; });
                object shell = Get(context, "Shell"), popup = Get(shell, "DeathPopup"), renderer = Get(shell, "renderer"); Call(renderer, "RefreshResources");
                var prepare = popup.GetType().GetMethod("Prepare", Flags);
                var measure = Delegate.CreateDelegate(prepare.GetParameters()[3].ParameterType, renderer, renderer.GetType().GetMethod("PopupMeasure", Flags));
                Action<float, float> layout = (w, h) => prepare.Invoke(popup, new object[] { w, h, Get(renderer, "FontIdentity"), measure, Get(renderer, "SkinGeneration") });
                Action<string, int, int> image = (name, w, h) => { layout(w, h); graphics.Image(Path.Combine(output, name + ".png"), () => Call(renderer, "DrawDeathPopup", popup), Matrix.Identity, w, h); };
                Call(popup, "Open", true, 2); image("quantity-256", 960, 640);
                Call(popup, "Open", false, 2); Until(() => (bool)Get(host, "QueryReady")); image("details-empty", 960, 640);
                var history = (JueMingR.Features.DeathHistory.DeathHistory)Get(host, "History");
                string longReason = "原句 [i:1] 不触发物品图标；\r\n" + String.Concat(System.Linq.Enumerable.Repeat("这一段很长的死亡原因保留全文，换行后仍可滚动阅读。", 100));
                var moment = new DateTimeOffset(2026, 9, 15, 16, 0, 0, TimeSpan.FromHours(8));
                for (int i = 0; i < 1040; i++)
                {
                    var fact = new DeathFact(DeathEventId.Create(moment.AddSeconds(i), new Guid(i + 1, 0, 0, new byte[8])), moment.Offset, true,
                        (40 + i % 30 * 25) * 16, (50 + i / 30 * 15) * 16, i == 0 ? longReason : "隔离角色被击败了。记录 " + (i + 1), i % 2 == 0 ? "死于飞鱼" : "死于摔落");
                    Until(() => { if (history.Snapshot.Error != null) throw new InvalidOperationException(history.Snapshot.Error); Call(context, "UpdateRuntime"); return history.Accept(fact); });
                    if (i == 0) { Until(() => history.Snapshot.Count == 1 && history.Snapshot.Rows.Count == 1); image("details-one", 960, 640); }
                    if (i % 32 == 31) { int expected = i + 1; Until(() => history.Snapshot.Count >= expected); }
                }
                Until(() => history.Snapshot.Count == 1040 && history.Snapshot.Rows.Count == 6);
                image("details-six", 960, 640);
                Call(popup, "Execute", 10); Until(() => history.Snapshot.Selected != null);
                for (int i = 0; i < 100; i++) layout(960, 640);
                image("reason-full", 960, 640); Call(popup, "Back"); Until(() => (bool)Get(host, "QueryReady")); image("details-return", 960, 640);
                image("details-small", 640, 220); Call(popup, "Open", true, 2); image("quantity-small", 640, 220); Call(popup, "Close");
                graphics.LoadDeathTexture(); Main.mapFullscreen = true; Call(host, "SetEnabled", true);
                var map = Get(host, "Map"); var mapType = map.GetType();
                foreach (int k in new[] { 128, 256, 512, 1024 })
                {
                    Call(host, "SetCount", k); Call(context, "UpdateRuntime"); Until(() => history.Snapshot.Markers.Count == k);
                    var begin = mapType.GetMethod("BeginMap", Flags); var end = mapType.GetMethod("EndMap", Flags);
                    graphics.Image(Path.Combine(output, "map-" + k + ".png"), () =>
                    {
                        begin.Invoke(null, null);
                        try { string text = ""; Main.MapIcons.Draw(Vector2.Zero, Vector2.Zero, null, 1, 1, 255, ref text); }
                        finally { end.Invoke(null, new object[] { null }); }
                    }, Matrix.Identity);
                    Require((bool)Get(map, "Ready"), "actual map drawing remains ready");
                }
                Console.WriteLine("PASS: actual-resource death quantity/list/full-text/map previews generated; inspect images separately from gameplay acceptance.");
            }
            finally { if (host != null) Call(host, "OnExit", null, EventArgs.Empty); StopContext(context); Main.MapIcons = originalMap; }
        }
        private static void Until(Func<bool> done)
        { var timer = System.Diagnostics.Stopwatch.StartNew(); while (!done()) { if (timer.ElapsedMilliseconds > 5000) throw new TimeoutException("death preview data unavailable"); Thread.Sleep(5); } }
    }
}
