using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using JueMingR.Features.ItemBrowser;
using JueMingR.Platform.ItemCatalog;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Production page layout, real ReLogic glyph measurements, no device/draw.
    // This is separate from the visual probe and is not an in-game acceptance.
    internal static class NativeBrowserUiChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(Assembly assembly, object catalog)
        {
            object host = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.HostItemKnowledge"), true);
            ((IDisposable)Get(host, "Native")).Dispose(); Set(host, "Native", catalog);
            object shell = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.F5Interaction"), true);
            object input = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Input.HostInputState"), true);
            object renderer = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.F5Renderer"), true);
            object page = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.BrowserPresentation"), Flags, null, new[] { host, shell, input, null }, null);
            try
            {
                FiniteCostChecks.SetCpuFont(10); Call(renderer, "RefreshResources"); Set(shell, "Ready", true); Call(shell, "Navigate", 3); Call(shell, "RestoreVisible");
                var workspace = Get(host, "Workspace"); Call(workspace, "Navigate", 172, false);
                int[] matches = (int[])Call(Get(catalog, "Catalog"), "Search", "", 0, false); Set(page, "matches", matches);
                Set(page, "shownStatus", "6100 件"); Set(page, "shownLocatorStatus", "只读定位");
                foreach (int height in new[] { 210, 297, 320, 350, 390, 400, 480, 580 })
                {
                    var layout = Get(shell, "Layout"); Call(renderer, "Prepare", shell, 960f, (float)(height + 183), 1f);
                    object rect = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect"), Flags, null, new object[] { 0f, 0f, 522f, (float)height }, null);
                    Set(page, "View", rect); Set(page, "row", 30f); Call(page, "Build");
                    var parts = ((IEnumerable)Get(page, "Parts")).Cast<object>().ToArray();
                    foreach (object part in parts)
                    {
                        var e = Get(part, "Element"); var r = Get(e, "Rect");
                        float x = (float)Get(r, "X"), y = (float)Get(r, "Y"), w = (float)Get(r, "Width"), h = (float)Get(r, "Height");
                        Require(w > 0 && h > 0 && x >= 0 && y >= 0 && x + w <= 522.01f && y + h <= height + .01f, "browser control stays within readable viewport h=" + height);
                        Require((float)Get(Get(e, "TextSize"), "Width") <= w, "full text cannot spill into adjacent browser controls");
                    }
                    var buttons = parts.Where(p => (int)Get(p, "Command") != 0).ToArray();
                    for (int i = 0; i < buttons.Length; i++) for (int j = i + 1; j < buttons.Length; j++)
                        Require(!Overlap(Get(Get(buttons[i], "Element"), "Rect"), Get(Get(buttons[j], "Element"), "Rect")), "browser controls cannot overlap at height=" + height + " commands=" + Get(buttons[i], "Command") + "," + Get(buttons[j], "Command"));
                }
                CheckCompleteRelations(assembly, catalog, host, page);
                CheckDynamicTextLifetime(host, shell, page);
                var draft = Get(page, "query"); Call(draft, "SelectAll"); Call(draft, "Insert", "最后一个字"); Set(page, "editing", 1);
                Require((bool)Call(page, "RequestFinish") && (string)Get(workspace, "Query") == "最后一个字", "finish commits latest query before releasing editor");
                Set(page, "leftTail", true); Set(page, "rightTail", true);
                Set(input, "mapped", true); Set(input, "nativePermission", true); Set(input, "IsFocused", true);
                PlayerInput.MouseInfo = new MouseState();
                Call(page, "Process", false, Vector2.Zero, false, false);
                Require((bool)Get(page, "ConsumeLeft") && (bool)Get(page, "ConsumeRight"), "closed page owns final physical release");
                Call(page, "Process", false, Vector2.Zero, false, false);
                Require(!(bool)Get(page, "ConsumeLeft") && !(bool)Get(page, "ConsumeRight"), "closed page retires both mouse tails without reopening");
                Console.WriteLine("PASS: production browser bounded layout, measured text, latest edit commit and closed-page release retirement.");
            }
            finally { ((IDisposable)page).Dispose(); }
        }
        internal static ItemRelation LongRelation(object catalog)
        {
            var directory = (BrowserCatalog)Get(catalog, "Catalog");
            return ((IEnumerable<ItemRelation>)Get(catalog, "RecipeValues")).Where(r => r.Ingredients.Count > 0).OrderByDescending(r => r.Ingredients.Max(i => (i.Types.Count > 1 ? i.Label : directory.Find(i.Types[0])?.Name ?? "").Length)).First();
        }
        private static void CheckDynamicTextLifetime(object host, object shell, object page)
        {
            var state = (BrowserWorkspace)Get(host, "Workspace");
            var fixedText = (IDictionary)Get(Get(shell, "Layout"), "textSizes");
            int before = fixedText.Count;
            // One unchanged viewport/session: growing item descriptions and all
            // their wrapping prefixes must not enter the fixed F5 label cache.
            for (int type = 1; type <= 160; type++)
            {
                state.Navigate(type, false); state.Kind = 0; state.Group = -1; state.DetailScroll = 0;
                Call(page, "Build");
            }
            Call(page, "Suspend"); Call(shell, "Close"); Call(shell, "RestoreVisible");
            Set(page, "shownLocatorStatus", "已清除定位结果；输入保留"); Call(page, "Build");
            Require(fixedText.Count == before, "browser dynamic names/prefixes and clear status cannot grow the fixed F5 text cache across reopen");
            Console.WriteLine("PASS: 160 real item details and reopen/clear retain the fixed F5 text-cache size.");
        }
        private static void CheckCompleteRelations(Assembly assembly, object catalog, object host, object page)
        {
            var directory = (BrowserCatalog)Get(catalog, "Catalog");
            var recipes = ((IEnumerable<ItemRelation>)Get(catalog, "RecipeValues")).ToArray();
            var samples = new[] { LongRelation(catalog), recipes.OrderByDescending(r => r.StationName.Length).First(), recipes.First(r => r.Ingredients.Any(i => i.Types.Count > 1)) };
            foreach (int height in new[] { 297, 580 }) foreach (var recipe in samples)
            {
                var state = (BrowserWorkspace)Get(host, "Workspace"); state.Navigate(recipe.Output, false); state.Kind = 0;
                var choices = ((IEnumerable<ItemRelation>)Call(Get(host, "Sources"), "Find", recipe.Output, false, 0, catalog, Get(host, "Shops"))).ToArray();
                state.RelationOffset = Array.FindIndex(choices, r => r.Id == recipe.Id);
                Set(page, "View", Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect"), Flags, null, new object[] { 0f, 0f, 522f, (float)height }, null));
                var recovered = new System.Text.StringBuilder();
                for (int scroll = 0; scroll < 300; scroll++)
                {
                    state.DetailScroll = scroll; Call(page, "Build");
                    var parts = ((IEnumerable)Get(page, "Parts")).Cast<object>().ToArray();
                    var body = parts.Where(p => (int)Get(p, "Command") == 0 && (float)Get(Get(Get(p, "Element"), "Rect"), "X") >= (height >= 400 ? 202 : 0) &&
                        (float)Get(Get(Get(p, "Element"), "Rect"), "Y") >= 160 && (float)Get(Get(Get(p, "Element"), "Rect"), "Y") < height - 102).ToArray();
                    Require(body.Length > 0, "relation has scrollable body rows");
                    Require(body.All(p => !((string)Get(Get(p, "Element"), "Text")).EndsWith("…")), "relation text must wrap instead of losing a name or quantity");
                    bool more = parts.Any(p => (int)Get(p, "Command") == 23 && (int)Get(p, "Argument") == 1 && (bool)Get(p, "Enabled"));
                    foreach (var part in more ? body.Take(1) : body) recovered.Append((string)Get(Get(part, "Element"), "Text"));
                    if (!more) break;
                }
                string text = recovered.ToString();
                Require(text.Contains(directory.Find(recipe.Output).Name + " ×" + recipe.Minimum), "full output name and quantity recover through detail scrolling");
                foreach (var ingredient in recipe.Ingredients)
                {
                    string name = ingredient.Types.Count > 1 ? "任选 " + ingredient.Label + "（" + ingredient.Types.Count + " 种）" : directory.Find(ingredient.Types[0]).Name;
                    Require(text.Contains(name + " ×" + ingredient.Count), "full material/group name and required quantity recover through detail scrolling");
                }
                Require(recipe.StationName.Length == 0 || text.Contains("工作台：" + recipe.StationName), "full station requirement recovers through detail scrolling");
            }
        }
        private static bool Overlap(object a, object b)
        { return (float)Get(a, "X") < (float)Get(b, "Right") && (float)Get(b, "X") < (float)Get(a, "Right") && (float)Get(a, "Y") < (float)Get(b, "Bottom") && (float)Get(b, "Y") < (float)Get(a, "Bottom"); }
    }
}
