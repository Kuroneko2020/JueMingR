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
            NativeAnnouncementIconChecks.Draw(graphics, output);
            var assembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll"));
            object host = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.HostItemKnowledge"), true);
            object shell = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.F5Interaction"), true);
            object input = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Input.HostInputState"), true);
            object renderer = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.F5Renderer"), true);
            object page = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.BrowserPresentation"), Flags, null, new[] { host, shell, input, null }, null);
            try
            {
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
                    object directory = GetOptional(Get(host, "Native"), "Catalog");
                    Set(page, "matches", directory == null ? new int[0] : Call(directory, "Search", (string)Get(state, "Query"), 0, false));
                    Set(page, "shownStatus", Get(Get(host, "Native"), "Status")); Set(page, "shownLocatorStatus", "附近箱内定位"); Call(page, "Build");
                    graphics.LoadItemTextures(((IEnumerable)Get(page, "Parts")).Cast<object>().Select(p => (int)Get(p, "Type")).Where(i => i > 0).Distinct());
                    Set(page, "dirty", true); Call(page, "Prepare", true, transform);
                    graphics.Image(Path.Combine(output, name), () => { Call(renderer, "Draw", shell, transform, false, false); Call(page, "Draw"); }, transform, width, height);
                    long layouts = (long)Get(page, "LayoutBuilds"), searches = (long)Get(page, "Searches"); object icons = Get(page, "renderer");
                    long loads = (long)Get(icons, "IconLoads"), sets = (long)Get(icons, "VisibleSetBuilds");
                    if ((bool)Get(Get(host, "Native"), "Ready"))
                    {
                        for (int i = 0; i < 120; i++) Call(page, "Prepare", true, transform);
                        Require((long)Get(page, "LayoutBuilds") == layouts && (long)Get(page, "Searches") == searches && (long)Get(icons, "IconLoads") == loads && (long)Get(icons, "VisibleSetBuilds") == sets, "steady visible browser does no layout/search/icon-set/texture rebuild");
                    }
                };
                draw(960, 760, 1, "browser-loading.png");
                for (int i = 0; i < 1800; i++) Call(host, "Step", true);
                Require((bool)Get(Get(host, "Native"), "Ready"), "visual native catalog ready");
                draw(960, 760, 1, "browser-two-panes.png"); draw(960, 580, 1, "browser-compact-detail.png");
                draw(1280, 720, 1.5f, "browser-720p-150-detail.png");
                Set(state, "Group", 0); draw(1280, 720, 1.5f, "browser-720p-150-alternatives.png"); Set(state, "Group", -1);
                Set(state, "Detail", false); draw(1280, 720, 1.5f, "browser-720p-150-catalog.png");
                draw(960, 400, 1, "browser-too-short.png");
                Set(state, "Query", "no-such-item-987654321"); Call(Get(page, "query"), "Insert", "no-such-item-987654321");
                draw(960, 760, 1, "browser-no-match.png");
                Set(state, "Query", ""); Call(Get(page, "query"), "SelectAll"); Call(Get(page, "query"), "DeleteSelection"); Set(state, "CatalogOffset", 6150);
                draw(960, 760, 1, "browser-last-page.png");
                var recipe = NativeBrowserUiChecks.LongRelation(Get(host, "Native")); Call(state, "Navigate", recipe.Output, false); Set(state, "Kind", 0);
                var choices = ((IEnumerable)Call(Get(host, "Sources"), "Find", recipe.Output, false, 0, Get(host, "Native"), Get(host, "Shops"))).Cast<object>().ToArray();
                Set(state, "RelationOffset", Array.FindIndex(choices, r => (string)Get(r, "Id") == recipe.Id)); Set(state, "DetailScroll", 2);
                draw(960, 860, 1, "browser-long-relation.png");
                Set(state, "DetailScroll", 3); draw(1280, 720, 1.5f, "browser-long-relation-150.png");
                Call(state, "Navigate", 9, true); Call(state, "Back"); Call(page, "CancelEdit");
                Require((int)Get(state, "Selected") == recipe.Output, "return and editor cancel preserve long relation history");
                draw(960, 760, 1, "browser-return-cancel.png");
                NativeBrowserLocatorVisualChecks.Run(graphics, output, assembly, host, page, draw);
                NativeBrowserLifecycleChecks.Run(graphics, output, assembly);
                Console.WriteLine("PASS: original XNB browser drawings produced; inspect each PNG before delivery.");
            }
            finally { ((IDisposable)page).Dispose(); ((IDisposable)host).Dispose(); }
        }
    }
}
