using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBrowserLocatorVisualChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(ProbeGraphics graphics, string output, Assembly assembly, object host, object page, Action<int, int, float, string> draw)
        {
            object world = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.World.WorldTileObservation"), Flags, null, new object[] { (Func<bool>)(() => true) }, null);
            object locator = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.ChestLocator.HostChestLocator"), Flags, null, new[] { world, host }, null);
            long tick = 0;
            Action update = () => { Call(world, "BeginTick"); Call(locator, "Update", ++tick); };
            try
            {
                // In-memory fixture only: neither a server nor a real save is
                // opened. Unknown illustrates lack of received slot coverage.
                typeof(NativeChestScanChecks).GetMethod("Reset", Flags).Invoke(null, null);
                typeof(NativeChestScanChecks).GetMethod("Place", Flags).Invoke(null, new object[] { 0, 20, 20, 21, 99 });
                typeof(NativeChestScanChecks).GetMethod("Place", Flags).Invoke(null, new object[] { 1, 40, 20, 88, 11 });
                Main.gameMenu = Main.hideUI = Main.mapFullscreen = Main.inFancyUI = Main.onlyDrawFancyUI = Main.ingameOptionsWindow = false;
                Main.LocalPlayer.active = true; Main.LocalPlayer.chest = -1; Main.LocalPlayer.position = new Vector2(300, 300); Main.LocalPlayer.gravDir = 1;
                Main.screenPosition = Vector2.Zero; Main.screenWidth = 960; Main.screenHeight = 640;
                Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null); Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, 960, 640));
                Main.netMode = 1; Main.sectionManager = new WorldSections(2, 2); Main.sectionManager.SetAllSectionsLoaded(); Netplay.Connection = new RemoteServer(); update();
                Set(page, "LocatorStatus", (Func<string>)(() => (string)Get(locator, "Status")));
                Set(page, "LocatorDetails", (Func<System.Collections.Generic.IReadOnlyList<string>>)(() => (System.Collections.Generic.IReadOnlyList<string>)Get(locator, "Details")));
                Set(page, "LocatorRevision", (Func<long>)(() => (long)Get(locator, "Revision")));
                Set(page, "locatorView", 1);
                Call(locator, "Submit", "#9"); for (int i = 0; i < 100 && (bool)Get(locator, "scanning"); i++) update();
                Require(((IList)Get(locator, "Results")).Count == 0 && ((string)Get(locator, "Status")).Contains("未知"), "visual unknown state originates in the real evidence gate");
                draw(960, 760, 1, "locator-unknown.png");
                Main.netMode = 0; update(); Call(locator, "Submit", "#9"); for (int i = 0; i < 100 && (bool)Get(locator, "scanning"); i++) update();
                Require(((IList)Get(locator, "Results")).Count == 2, "visual success state originates in the real spatial/content scan");
                draw(960, 760, 1, "locator-success-details.png");
                Require(graphics.Pixels(() => Call(locator, "Draw"), Matrix.Identity).Count(c => c.G > c.R && c.G > c.B && c.A > 0) > 100, "production locator emits visible green fill, border and text pixels");
                graphics.Scene(() => Call(locator, "Draw"), Path.Combine(output, "locator-world-highlight.png"));
                Main.LocalPlayer.gravDir = -1;
                graphics.Scene(() => Call(locator, "Draw"), Path.Combine(output, "locator-inverted-highlight.png"));
            }
            finally { ((IDisposable)locator).Dispose(); Main.netMode = 0; Main.LocalPlayer.gravDir = 1; }
        }
    }
}
