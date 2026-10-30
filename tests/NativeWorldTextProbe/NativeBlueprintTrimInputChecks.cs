using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintTrimInputChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static void Set(object owner, string field, object value) { NativeBlueprintTrimAccumulationChecks.Set(owner, field, value); }
        internal static void Run(object context)
        {
            NativeBlueprintTrimAccumulationChecks.WithScene(context, scene =>
            {
                var host = scene.Host; var input = scene.Input; var shell = scene.Shell; var state = Get(shell, "State"); var renderer = Get(shell, "renderer");
                var hotkeys = Get(shell, "hotkeys"); var registry = (HotkeyRegistry)Get(hotkeys, "Registry"); var oldBindings = (HotkeyBindings)Get(hotkeys, "Bindings");
                var oldFont = FontAssets.MouseText; var oldMouse = PlayerInput.MouseInfo; var oldKeys = Main.keyState;
                int oldWidth = Main.screenWidth, oldHeight = Main.screenHeight; var oldScale = Main.UIScaleMatrix;
                object oldWindow = Get(input, "gameWindow"), oldForeground = Get(input, "foregroundWindow");
                bool oldLayers = (bool)Get(shell, "LayersReady"), oldReady = (bool)Get(state, "Ready");
                var baseline = (BlueprintProjectionMask)Get(scene.Target, "Mask"); long revision = (long)Get(host, "projectionSceneRevision");
                var pointer = Get(host, "Pointer"); var current = pointer.GetType().GetField("current", F); object oldPointer = current.GetValue(null);
                using (var bindings = new HotkeyBindings(registry, new AtomicFileDocument(System.IO.Path.Combine(scene.Root, "input-bindings.json"), 65536, true)))
                try
                {
                    NativeQuickItemChecks.Until(() => { bindings.Poll(); return bindings.Loaded; }); Set(hotkeys, "Bindings", bindings);
                    Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(1))); Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(1)));
                    current.SetValue(null, pointer); FiniteCostChecks.SetCpuFont(10); Call(renderer, "RefreshResources");
                    Main.screenWidth = 1280; Main.screenHeight = 900; NativeBlueprintUiChecks.SetScale(1f); PlayerInput.CacheOriginalScreenDimensions(); PlayerInput.RawMouseScale = Vector2.One;
                    Set(shell, "LayersReady", true); Set(state, "Ready", true); Call(shell, "CloseAndSubmitPosition");
                    Action<Keys[], bool, bool, bool, int, int> frame = (keys, left, right, mouse4, x, y) =>
                    {
                        Main.LocalPlayer.mouseInterface = Main.mouseText = false; FocusHelper.IsSelectedApplication = true;
                        PlayerInput.MouseInfo = new MouseState(x, y, 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released,
                            right ? ButtonState.Pressed : ButtonState.Released, mouse4 ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released);
                        Main.mouseX = PlayerInput.MouseX = x; Main.mouseY = PlayerInput.MouseY = y; Main.mouseLeft = left; Main.mouseRight = right;
                        Call(input, "BeginUpdate"); Call(shell, "BeforeInput");
                        var mouse = new List<string>(); if (left) mouse.Add("Mouse1"); if (right) mouse.Add("Mouse2"); if (mouse4) mouse.Add("Mouse4");
                        Call(input, "AfterNativeMouse", mouse); PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = left; PlayerInput.Triggers.Current.MouseRight = right; PlayerInput.Triggers.Update();
                        PlayerInput.ScrollWheelDelta = PlayerInput.ScrollWheelDeltaForUI = 0; Call(input, "AfterMapping"); Main.keyState = new KeyboardState(keys); Call(input, "AfterKeyboardRefresh");
                        Call(shell, "ProcessInput"); Require(!(bool)Get(shell, "Failed"), "actual Shell dispatch remains healthy"); Call(host, "ProcessInput");
                    };
                    Action<Keys[]> keyFrame = keys => frame(keys, false, false, false, 1256, 450);
                    Action ready = () => { scene.Begin(); scene.Seal(0, 0); scene.Ready(); keyFrame(Array.Empty<Keys>()); };
                    Action cancelled = () => Require(!(bool)Get(host, "ProjectionEditing") && ReferenceEquals(Get(scene.Target, "Mask"), baseline) &&
                        (long)Get(host, "projectionSceneRevision") == revision && NativeBlueprintTrimAccumulationChecks.Scene(host).Contains(scene.Target), "accepted cancellation wins without mask/save/retirement or same-map reentry");
                    Save(bindings, "blueprint.trim.confirm", "G"); Save(bindings, "blueprint.trim", "H");
                    ready(); keyFrame(new[] { Keys.G, Keys.Escape }); cancelled();
                    ready(); keyFrame(new[] { Keys.G, Keys.H }); cancelled();
                    Save(bindings, "blueprint.trim.confirm", null); Save(bindings, "blueprint.trim", "G"); Save(bindings, "blueprint.trim.confirm", "H");
                    ready(); keyFrame(new[] { Keys.G, Keys.H }); cancelled();
                    Save(bindings, "blueprint.trim.confirm", "Mouse4");
                    ready(); frame(new[] { Keys.Escape }, false, false, true, 1256, 450); cancelled();
                    Save(bindings, "blueprint.trim", "H"); Save(bindings, "blueprint.trim.confirm", "G");
                    ready(); frame(Array.Empty<Keys>(), false, true, false, 1256, 450);
                    // Production observer methods certify the captured native UI
                    // boundary, with no raw right-click shortcut in the Host.
                    pointer.GetType().GetMethod("Begin", F).Invoke(null, null); pointer.GetType().GetMethod("Boundary", F).Invoke(null, null);
                    pointer.GetType().GetMethod("Resources", F).Invoke(null, new object[] { true }); pointer.GetType().GetMethod("End", F).Invoke(null, new object[] { true });
                    Require((bool)Get(pointer, "completed"), "previous mapping has an actual controlled native-UI right certificate");
                    keyFrame(new[] { Keys.G }); cancelled();
                    ready(); keyFrame(new[] { Keys.G }); Require((bool)Get(host, "trimFinalizing") && ReferenceEquals(Get(scene.Target, "Mask"), baseline), "ordinary keyboard confirmation becomes one accepted Freeze responsibility after Host arbitration");
                    scene.WaitFrozen(); keyFrame(new[] { Keys.Escape }); cancelled();
                    // A real F5 release is a distinct source, allowed through its
                    // own control proof while the broad world drag gate is shut.
                    ready(); Call(state, "Navigate", 6); Call(state, "RestoreVisible"); keyFrame(Array.Empty<Keys>()); Call(renderer, "Prepare", state, 1280f, 900f, 1f);
                    var layout = Get(state, "Layout"); object confirm = ((IEnumerable)Get(layout, "Elements")).Cast<object>().First(element =>
                        Get(element, "Kind").ToString() == "Button" && Get(element, "Command").ToString() == "BlueprintConfirmTrim");
                    var area = Get(confirm, "Rect"); var viewport = Get(layout, "Viewport");
                    int cx = (int)((float)Get(state, "X") + (float)Get(viewport, "X") + (float)Get(area, "X") + 3);
                    int cy = (int)((float)Get(state, "Y") + (float)Get(viewport, "Y") + (float)Get(area, "Y") - (float)Get(state, "Scroll") + 3);
                    frame(Array.Empty<Keys>(), true, false, false, cx, cy); frame(Array.Empty<Keys>(), false, false, false, cx, cy);
                    Require((bool)Get(host, "trimFinalizing") && ReferenceEquals(Get(scene.Target, "Mask"), baseline), "actual F5 control press/release reaches the unified accepted command, never a world drag");
                    scene.WaitFrozen(); Call(host, "CancelProjection"); Call(shell, "CloseAndSubmitPosition"); keyFrame(Array.Empty<Keys>()); cancelled();
                    ready(); ToolbarRelease(scene, frame);
                    Require((bool)Get(host, "trimFinalizing"), "actual presented gel-toolbar release reaches the same finalizing responsibility");
                    scene.WaitFrozen(); keyFrame(Array.Empty<Keys>());
                    var final = (BlueprintProjectionMask)Get(scene.Target, "Mask");
                    Require(final.RemovedLayerCount == 8 && (long)Get(host, "projectionSceneRevision") == revision + 1 && !(bool)Get(host, "ProjectionEditing"), "one normal controlled UI confirmation publishes once after Freeze, without another click");
                    keyFrame(Array.Empty<Keys>()); Require(ReferenceEquals(Get(scene.Target, "Mask"), final), "later input cannot replay the accepted confirmation");
                    Console.WriteLine("PASS trim input actual Shell -> Host: G+Esc, Mouse4+Esc, both key-code orders of confirm/cancel, prior certified world right, Freeze receipt+Esc, real F5 release and presented gel-toolbar release, one eventual publication. Controlled CPU observer calls do not prove GPU/OS input.");
                }
                finally
                {
                    Call(shell, "RestoreLeases"); Call(shell, "CloseAndSubmitPosition"); Call(host, "CancelProjection"); Set(hotkeys, "Bindings", oldBindings);
                    current.SetValue(null, oldPointer); Set(input, "gameWindow", oldWindow); Set(input, "foregroundWindow", oldForeground);
                    FontAssets.MouseText = oldFont; Call(renderer, "RefreshResources"); Main.screenWidth = oldWidth; Main.screenHeight = oldHeight;
                    typeof(Main).GetField("_uiScaleMatrix", F).SetValue(null, oldScale); PlayerInput.CacheOriginalScreenDimensions(); PlayerInput.MouseInfo = oldMouse; Main.keyState = oldKeys;
                    Set(shell, "LayersReady", oldLayers); Set(state, "Ready", oldReady);
                }
            });
        }
        private static void ToolbarRelease(NativeBlueprintTrimAccumulationChecks.Scenario scene, Action<Keys[], bool, bool, bool, int, int> frame)
        {
            var host = scene.Host; var toolbar = Get(host, "Toolbar"); var layout = Get(toolbar, "Layout"); var player = Main.LocalPlayer;
            var oldItem = player.inventory[player.selectedItem]; bool oldEnabled = (bool)Get(host, "ToolbarEnabled");
            object oldFont = GetOptional(toolbar, "font");
            try
            {
                player.inventory[player.selectedItem] = new Item(Terraria.ID.ItemID.Gel) { stack = 1 }; Call(host, "SetToolbar", true); Set(toolbar, "font", FontAssets.MouseText.Value);
                Call(layout, "SetPreviewRevision", (long)Get(host, "TrimPreviewRevision"));
                var measure = Delegate.CreateDelegate(layout.GetType().GetMethod("EnsureProjection", F).GetParameters()[4].ParameterType, toolbar, toolbar.GetType().GetMethod("Measure", F));
                Call(layout, "EnsureProjection", 1280f, 900f, 1f, FontAssets.MouseText.Value, measure, false, 7);
                Set(toolbar, "presented", true); Set(toolbar, "drawnMatrix", Main.UIScaleMatrix); Set(toolbar, "visibleClip", Get(layout, "Panel"));
                Set(toolbar, "skin", TextureAssets.InventoryBack?.Value); Set(toolbar, "pixel", TextureAssets.MagicPixel?.Value);
                var buttons = ((IEnumerable)Get(layout, "Buttons")).Cast<object>().ToArray(); int index = Enumerable.Range(0, buttons.Length).Single(i => Call(layout, "Action", i).ToString() == "ConfirmTrim");
                var rect = Get(buttons[index], "Rect"); var panel = Get(layout, "Panel");
                int x = (int)((float)Get(panel, "X") + (float)Get(rect, "X") + 3), y = (int)((float)Get(panel, "Y") + (float)Get(rect, "Y") + 3);
                frame(Array.Empty<Keys>(), false, false, false, x, y); frame(Array.Empty<Keys>(), true, false, false, x, y); frame(Array.Empty<Keys>(), false, false, false, x, y);
            }
            finally { player.inventory[player.selectedItem] = oldItem; Call(host, "SetToolbar", oldEnabled); Set(toolbar, "font", oldFont); Call(toolbar, "Cancel"); }
        }
        private static void Save(HotkeyBindings owner, string id, string text)
        {
            HotkeyChord chord = null; string reason = null; if (text != null) Require(HotkeyChord.TryParse(text, out chord, out reason), "fixture binding parses");
            long command; Require(owner.TrySet(id, chord, null, out command, out reason), "actual common binding admitted: " + reason);
            NativeQuickItemChecks.Until(() => { owner.Poll(); return !owner.Busy; }); Require(owner.CompletionSucceeded, "common binding write acknowledged");
        }
    }
}
