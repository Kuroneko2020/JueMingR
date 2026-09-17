using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBrowserVisualChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(ProbeGraphics graphics, string output)
        {
            Directory.CreateDirectory(output);
            var assembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll"));
            object host = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.HostItemKnowledge"), true);
            object shell = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.F5Interaction"), true);
            object input = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Input.HostInputState"), true);
            object renderer = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.F5Renderer"), true);
            object page = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.BrowserPresentation"), Flags, null, new[] { host, shell, input, null }, null);
            try
            {
                for (int i = 0; i < 1800; i++) Call(host, "Step", true);
                Require((bool)Get(Get(host, "Native"), "Ready"), "visual native catalog ready");
                Main.gameMenu = Main.hideUI = false; Set(shell, "Ready", true); Call(shell, "Navigate", 3); Call(shell, "RestoreVisible");
                Call(renderer, "RefreshResources");
                var state = Get(host, "Workspace"); Call(state, "Navigate", 172, false);
                Action<int, int, float, string> draw = (width, height, scale, name) =>
                {
                    Matrix transform = Matrix.CreateScale(scale);
                    Call(renderer, "Prepare", shell, (float)width, (float)height, scale);
                    // First build computes visible IDs without calling LoadItem;
                    // original XNBs are then loaded into the native Asset slots.
                    Set(page, "View", Get(Get(shell, "Layout"), "Viewport")); Set(page, "row", 30f);
                    Set(page, "matches", Call(Get(Get(host, "Native"), "Catalog"), "Search", "", 0, false));
                    Set(page, "shownStatus", "全目录与真实配方"); Set(page, "shownLocatorStatus", "附近箱内定位"); Call(page, "Build");
                    graphics.LoadItemTextures(((IEnumerable)Get(page, "Parts")).Cast<object>().Select(p => (int)Get(p, "Type")).Where(i => i > 0).Distinct());
                    Set(page, "dirty", true); Call(page, "Prepare", true, transform);
                    graphics.Image(Path.Combine(output, name), () => { Call(renderer, "Draw", shell, transform, false, false); Call(page, "Draw"); }, transform, width, height);
                    long layouts = (long)Get(page, "LayoutBuilds"), searches = (long)Get(page, "Searches"); object icons = Get(page, "renderer");
                    long loads = (long)Get(icons, "IconLoads"), sets = (long)Get(icons, "VisibleSetBuilds");
                    for (int i = 0; i < 120; i++) Call(page, "Prepare", true, transform);
                    Require((long)Get(page, "LayoutBuilds") == layouts && (long)Get(page, "Searches") == searches && (long)Get(icons, "IconLoads") == loads && (long)Get(icons, "VisibleSetBuilds") == sets, "steady visible browser does no layout/search/icon-set/texture rebuild");
                };
                draw(960, 760, 1, "browser-two-panes.png"); draw(960, 580, 1, "browser-compact-detail.png");
                draw(1280, 720, 1.5f, "browser-720p-150-detail.png");
                Set(state, "Group", 0); draw(1280, 720, 1.5f, "browser-720p-150-alternatives.png"); Set(state, "Group", -1);
                Set(state, "Detail", false); draw(1280, 720, 1.5f, "browser-720p-150-catalog.png");
                draw(960, 400, 1, "browser-too-short.png");
                Console.WriteLine("PASS: original XNB browser drawings produced; inspect each PNG before delivery.");
            }
            finally { ((IDisposable)page).Dispose(); ((IDisposable)host).Dispose(); }
        }
    }
}
