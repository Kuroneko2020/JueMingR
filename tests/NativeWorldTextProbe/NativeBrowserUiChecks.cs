using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
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
        private static bool Overlap(object a, object b)
        { return (float)Get(a, "X") < (float)Get(b, "Right") && (float)Get(b, "X") < (float)Get(a, "Right") && (float)Get(a, "Y") < (float)Get(b, "Bottom") && (float)Get(b, "Y") < (float)Get(a, "Bottom"); }
    }
}
