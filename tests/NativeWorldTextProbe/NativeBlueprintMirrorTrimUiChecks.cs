using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Production CPU geometry, shared physical edges, common binding storage
    // coordinator and real Host commands. This is not OS/IME or GPU evidence.
    internal static class NativeBlueprintMirrorTrimUiChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly string[] EditIds = { "blueprint.mirror", "blueprint.trim", "blueprint.trim.confirm" };

        internal static void Run(object context)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell");
            var renderer = Get(shell, "renderer"); var toolbar = Get(host, "Toolbar");
            var oldFont = FontAssets.MouseText; object oldToolbarFont = GetOptional(toolbar, "font");
            float oldScale = Main.UIScale; int oldWidth = Main.screenWidth, oldHeight = Main.screenHeight;
            var oldMouse = PlayerInput.MouseInfo; var oldRawScale = PlayerInput.RawMouseScale;
            try
            {
                FiniteCostChecks.SetCpuFont(10); Call(renderer, "RefreshResources");
                object font = Get(renderer, "FontIdentity"); Set(toolbar, "font", font);
                Main.screenWidth = 1280; Main.screenHeight = 900; NativeBlueprintUiChecks.SetScale(1f);
                PlayerInput.CacheOriginalScreenDimensions(); PlayerInput.RawMouseScale = Vector2.One;
                LayoutAndTails(host.GetType().Assembly, renderer, toolbar, font);
                AdmissionAndCommands(context, renderer, font);
                Console.WriteLine("PASS Blueprint mirror/trim UI CPU: distinct real rows/binders and action hints, toolbar own-cancel/explicit confirm, submenu/page/preview invalidation with physical release tails, ordinary F5 all-tab narrow gate, real saved bindings/new primary -> Host start/cancel, world Mouse1/2 reservation. GPU/OS/IME and certified Mouse1 preview commit are separate evidence.");
            }
            finally
            {
                Call(shell, "CloseAndSubmitPosition"); Call(host, "CancelProjection");
                FontAssets.MouseText = oldFont; Set(toolbar, "font", oldToolbarFont); Call(renderer, "RefreshResources");
                NativeBlueprintUiChecks.SetScale(oldScale); Main.screenWidth = oldWidth; Main.screenHeight = oldHeight;
                PlayerInput.CacheOriginalScreenDimensions(); PlayerInput.MouseInfo = oldMouse; PlayerInput.RawMouseScale = oldRawScale;
            }
        }

        // Starts from the caller's real, stable preview. Unlike the finite
        // certificate checks, this traverses the actual Shell's sole public
        // Dispatch before Host.ProcessInput captures any native UI proof.
        internal static void ConfirmVisibleF5(object context, object target)
        {
            var host = Get(context, "Blueprint"); var input = Get(context, "Input"); var shell = Get(context, "Shell");
            var state = Get(shell, "State"); var layout = Get(state, "Layout"); var renderer = Get(shell, "renderer");
            var toolbar = Get(host, "Toolbar"); var pointer = Get(host, "Pointer"); var hotkeys = Get(shell, "hotkeys");
            var registry = (HotkeyRegistry)Get(hotkeys, "Registry"); var oldBindings = (HotkeyBindings)Get(hotkeys, "Bindings");
            var current = pointer.GetType().GetField("current", F); object oldPointer = current.GetValue(null);
            var oldFont = FontAssets.MouseText; object oldToolbarFont = GetOptional(toolbar, "font");
            var oldGameMatrix = Main.GameViewMatrix; var oldScreenPosition = Main.screenPosition;
            var oldMouse = PlayerInput.MouseInfo; var oldRawScale = PlayerInput.RawMouseScale; var oldKeyboard = Main.keyState;
            int oldWidth = Main.screenWidth, oldHeight = Main.screenHeight, oldMouseX = Main.mouseX, oldMouseY = Main.mouseY;
            var oldInputX = PlayerInput.MouseX; var oldInputY = PlayerInput.MouseY;
            int oldWheel = PlayerInput.ScrollWheelDelta, oldUiWheel = PlayerInput.ScrollWheelDeltaForUI;
            float oldScale = Main.UIScale; var oldUiMatrix = Main.UIScaleMatrix;
            bool oldInterface = Main.LocalPlayer.mouseInterface, oldMouseText = Main.mouseText;
            bool oldLeft = Main.mouseLeft, oldRight = Main.mouseRight, oldSelected = FocusHelper.IsSelectedApplication;
            bool oldLayers = (bool)Get(shell, "LayersReady"), oldReady = (bool)Get(state, "Ready"), oldVisible = (bool)Get(state, "Visible");
            int oldPage = (int)Get(state, "Page"); float oldScroll = (float)Get(state, "Scroll"), oldX = (float)Get(state, "X"), oldY = (float)Get(state, "Y");
            object oldWindow = Get(input, "gameWindow"), oldForeground = Get(input, "foregroundWindow");
            var triggerSets = new[] { PlayerInput.Triggers.Current, PlayerInput.Triggers.Old, PlayerInput.Triggers.JustPressed, PlayerInput.Triggers.JustReleased };
            var oldTriggers = triggerSets.Select(set => set.KeyStatus.ToDictionary(pair => pair.Key, pair => pair.Value)).ToArray();
            var mask = (BlueprintProjectionMask)Get(target, "Mask"); long scene = (long)Get(host, "projectionSceneRevision");
            long preview = (long)Get(host, "TrimPreviewRevision"); bool focused = true;
            HotkeyBindings bindings = null; Exception primary = null;
            // The native stages consume one physical sample per mapping. No
            // dispatch flags, confirm intents or certificate bits are assigned.
            Action<int, int, bool> shellFrame = (x, y, left) =>
            {
                Main.LocalPlayer.mouseInterface = Main.mouseText = false; FocusHelper.IsSelectedApplication = focused;
                PlayerInput.MouseInfo = new MouseState(x, y, 0, left ? ButtonState.Pressed : ButtonState.Released,
                    ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                Main.mouseX = PlayerInput.MouseX = x; Main.mouseY = PlayerInput.MouseY = y;
                Main.mouseLeft = left; Main.mouseRight = false;
                Call(input, "BeginUpdate"); Call(shell, "BeforeInput");
                Call(input, "AfterNativeMouse", new List<string>(left ? new[] { "Mouse1" } : Array.Empty<string>()));
                PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = left; PlayerInput.Triggers.Update();
                PlayerInput.ScrollWheelDelta = PlayerInput.ScrollWheelDeltaForUI = 0;
                Call(input, "AfterMapping"); Main.keyState = new KeyboardState(); Call(input, "AfterKeyboardRefresh");
                Call(shell, "ProcessInput"); Require(!(bool)Get(shell, "Failed"), "actual confirm Shell.ProcessInput remains healthy");
            };
            Action<int, int, bool> frame = (x, y, left) => { shellFrame(x, y, left); Call(host, "ProcessInput"); };
            Action unchanged = () => Require(ReferenceEquals(Get(target, "Mask"), mask) && (long)Get(host, "projectionSceneRevision") == scene &&
                (long)Get(host, "TrimPreviewRevision") == preview && ReferenceEquals(GetOptional(host, "editorTarget"), target) &&
                (bool)Get(host, "CanInvokeConfirmTrim"), "rejected public F5 confirmation retains the exact target, mask and stable preview");
            Action<bool> draw = nativeUi =>
            {
                // These are controlled calls to the production observer hooks,
                // after its own Capture. They do not execute Main.DrawInterface,
                // an OS event, a resource renderer or any GPU work.
                pointer.GetType().GetMethod("Begin", F).Invoke(null, null);
                Main.mouseText = nativeUi;
                try
                {
                    pointer.GetType().GetMethod("Boundary", F).Invoke(null, null);
                    pointer.GetType().GetMethod("Resources", F).Invoke(null, new object[] { true });
                    pointer.GetType().GetMethod("End", F).Invoke(null, new object[] { true });
                    Require((bool)Get(pointer, "completed") && (bool)Get(pointer, "ui") == nativeUi,
                        "production native UI hooks complete the current controlled certificate and record its real owner signal");
                }
                finally { Main.mouseText = false; }
            };
            Action accepted = () =>
            {
                shellFrame(1256, 450, true);
                long mapping = (long)Get(input, "Mapping");
                Require((long)Get(host, "trimConfirmMapping") == mapping && ReferenceEquals(Get(target, "Mask"), mask),
                    "actual saved Mouse1 -> Shell public Dispatch admits an intent without committing in the Shell");
                Call(host, "ProcessInput");
                Require((long)Get(host, "trimConfirmMapping") == mapping && (bool)Get(pointer, "pending") && (long)Get(pointer, "mapping") == mapping,
                    "public F5 confirm intent must survive actual Host.ProcessInput and create its current-mapping Pointer.Capture; the old F5 suspend path clears it");
                unchanged();
            };
            try
            {
                Require((bool)Get(host, "CanInvokeConfirmTrim") && ReferenceEquals(GetOptional(host, "editorTarget"), target),
                    "public F5 confirmation starts from the caller's actual nonempty stable preview");
                var storage = new BindingStorage();
                using (var first = new HotkeyBindings(registry, storage))
                {
                    Wait(first, () => first.Loaded); Save(first, EditIds[2], "Mouse1");
                }
                bindings = new HotkeyBindings(registry, new BindingStorage(storage.Snapshot));
                Wait(bindings, () => bindings.Loaded);
                Require(bindings.Get(EditIds[2])?.Text == "Mouse1", "a new isolated common-document owner reopens the acknowledged Mouse1 confirmation binding");
                Set(hotkeys, "Bindings", bindings); current.SetValue(null, pointer);
                Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(1)));
                Set(input, "foregroundWindow", (Func<IntPtr>)(() => focused ? new IntPtr(1) : new IntPtr(2)));
                FiniteCostChecks.SetCpuFont(10); Call(renderer, "RefreshResources"); Set(toolbar, "font", Get(renderer, "FontIdentity"));
                Main.screenWidth = 1280; Main.screenHeight = 900; NativeBlueprintUiChecks.SetScale(1f); Main.screenPosition = Vector2.Zero;
                Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null);
                Main.GameViewMatrix.SetViewportOverride(new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 1280, 900));
                PlayerInput.CacheOriginalScreenDimensions(); PlayerInput.RawMouseScale = Vector2.One;
                Call(shell, "CloseAndSubmitPosition"); Set(shell, "LayersReady", true); Set(state, "Ready", true);
                Call(state, "Navigate", 9); Call(state, "RestoreVisible");
                frame(1256, 450, false); frame(1256, 450, false); Call(renderer, "Prepare", state, 1280f, 900f, 1f);
                Require((bool)Get(state, "Visible") && (int)Get(state, "Page") == 9 && !(bool)Get(state, "OwnsPointer"),
                    "ordinary non-blueprint F5 page is visible while the real pointer is outside its window in world space");

                accepted(); // no Draw: a valid public intent is still insufficient
                frame(1256, 450, false); unchanged();
                Require((long)Get(host, "trimConfirmMapping") < 0 && !(bool)Get(pointer, "pending"), "missing Draw retires the public intent without replay");

                // A real control press owns its physical release even when the
                // pointer leaves the window; releasing outside executes nothing.
                object control = Objects(Get(layout, "Elements")).First(element => Get(element, "Kind").ToString() == "Button" &&
                    Get(element, "Command").ToString() != "None" && (float)Get(Get(element, "Rect"), "Y") < (float)Get(Get(layout, "Viewport"), "Height"));
                var area = Get(control, "Rect"); var view = Get(layout, "Viewport");
                int controlX = (int)((float)Get(state, "X") + (float)Get(view, "X") + (float)Get(area, "X") + 3);
                int controlY = (int)((float)Get(state, "Y") + (float)Get(view, "Y") + (float)Get(area, "Y") - (float)Get(state, "Scroll") + 3);
                frame(controlX, controlY, true);
                Require(GetOptional(state, "armed") != null && (bool)Get(state, "ConsumeLeft") && (long)Get(host, "trimConfirmMapping") < 0,
                    "an actual armed F5 control consumes Mouse1 before the public confirm action"); unchanged();
                frame(1256, 450, false);
                Require((bool)Get(state, "ConsumeLeft") && GetOptional(state, "ClickedControl") == null && (long)Get(host, "trimConfirmMapping") < 0,
                    "the actual control release tail remains owned outside the window and cannot confirm"); unchanged();
                frame(1256, 450, false);

                accepted(); draw(true); frame(1256, 450, false); unchanged();
                Require((long)Get(host, "trimConfirmMapping") < 0, "current native UI certificate rejects the admitted confirm rather than turning it into a world drag");

                accepted(); draw(false); focused = false; frame(1256, 450, true); unchanged();
                Require(!(bool)Get(input, "CanStartActions") && !(bool)Get(pointer, "pending") && (long)Get(host, "trimConfirmMapping") < 0,
                    "real HostInput focus loss invalidates the completed certificate before the next mapping can commit");
                focused = true; frame(1256, 450, false); frame(1256, 450, false);
                Set(state, "Ready", true); Call(state, "Navigate", 9); Call(state, "RestoreVisible");
                Call(renderer, "Prepare", state, 1280f, 900f, 1f); frame(1256, 450, false); unchanged();

                accepted(); draw(false); long certifiedMapping = (long)Get(input, "Mapping");
                frame(1256, 450, true);
                Require((long)Get(input, "Mapping") == certifiedMapping + 1 && (bool)Get(host, "trimFinalizing") && ReferenceEquals(Get(target,"Mask"),mask),
                    "adjacent certified mapping accepts one complete trim responsibility without synchronous publication");
                NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return GetOptional(host,"trimFrozenPlan")!=null;});
                frame(1256,450,true);
                var committed = (BlueprintProjectionMask)Get(target, "Mask");
                Require(!ReferenceEquals(committed, mask) &&
                    committed.RemovedLayerCount > mask.RemovedLayerCount && (long)Get(host, "projectionSceneRevision") == scene + 1 && !(bool)Get(host, "ProjectionEditing"),
                    "the actual Shell-admitted F5 Mouse1 publishes the same accepted complete version once after Freeze, without a second physical click");
                // A held primary and repeated old observer callbacks cannot
                // restore the retired intent or earn another scene publication.
                pointer.GetType().GetMethod("Begin", F).Invoke(null, null);
                pointer.GetType().GetMethod("Boundary", F).Invoke(null, null);
                pointer.GetType().GetMethod("Resources", F).Invoke(null, new object[] { true });
                pointer.GetType().GetMethod("End", F).Invoke(null, new object[] { true });
                frame(1256, 450, true); frame(1256, 450, true); frame(1256, 450, false);
                Require(ReferenceEquals(Get(target, "Mask"), committed) && (long)Get(host, "projectionSceneRevision") == scene + 1 &&
                    (long)Get(host, "trimConfirmMapping") < 0 && !(bool)Get(pointer, "pending"), "held Mouse1 and old proof cannot repeat the completed F5 confirmation");
                Console.WriteLine("PASS Blueprint trim public F5 Mouse1: isolated save/reopen -> actual HostInput/Shell Dispatch/Host capture, missing Draw/control tail/native UI/focus rejection, adjacent controlled certificate commits once; held/old proof cannot replay. No normal Main Draw, GPU or OS input evidence.");
            }
            catch (Exception error) { primary = error; throw; }
            finally
            {
                Exception cleanupFault = null;
                Action<Action> restore = action => { try { action(); } catch (Exception error) { if (cleanupFault == null) cleanupFault = error; } };
                restore(() => Call(shell, "RestoreLeases")); restore(() => Call(shell, "CloseAndSubmitPosition"));
                restore(() => Call(pointer, "Cancel")); restore(() => current.SetValue(null, oldPointer));
                restore(() => Set(hotkeys, "Bindings", oldBindings)); restore(() => bindings?.Dispose());
                restore(() => Set(input, "gameWindow", oldWindow)); restore(() => Set(input, "foregroundWindow", oldForeground));
                restore(() => { FontAssets.MouseText = oldFont; Set(toolbar, "font", oldToolbarFont); Call(renderer, "RefreshResources"); });
                restore(() =>
                {
                    Main.GameViewMatrix = oldGameMatrix; Main.screenPosition = oldScreenPosition;
                    Main.screenWidth = oldWidth; Main.screenHeight = oldHeight; NativeBlueprintUiChecks.SetScale(oldScale);
                    typeof(Main).GetField("_uiScaleMatrix", F).SetValue(null, oldUiMatrix); PlayerInput.CacheOriginalScreenDimensions();
                    PlayerInput.RawMouseScale = oldRawScale; PlayerInput.MouseInfo = oldMouse; Main.keyState = oldKeyboard;
                    Main.mouseX = oldMouseX; Main.mouseY = oldMouseY; PlayerInput.MouseX = oldInputX; PlayerInput.MouseY = oldInputY;
                    PlayerInput.ScrollWheelDelta = oldWheel; PlayerInput.ScrollWheelDeltaForUI = oldUiWheel;
                    Main.LocalPlayer.mouseInterface = oldInterface; Main.mouseText = oldMouseText;
                    Main.mouseLeft = oldLeft; Main.mouseRight = oldRight; FocusHelper.IsSelectedApplication = oldSelected;
                    for (int i = 0; i < triggerSets.Length; i++) foreach (var pair in oldTriggers[i]) triggerSets[i].KeyStatus[pair.Key] = pair.Value;
                });
                restore(() =>
                {
                    Set(shell, "LayersReady", oldLayers); Set(state, "Ready", oldReady); Call(state, "Navigate", oldPage);
                    Set(state, "Scroll", oldScroll); Set(state, "X", oldX); Set(state, "Y", oldY);
                    if (oldVisible) Call(state, "RestoreVisible");
                });
                if (primary == null && cleanupFault != null) throw new InvalidOperationException("public F5 confirmation fixture restoration failed", cleanupFault);
            }
        }

        private static void LayoutAndTails(Assembly assembly, object renderer, object toolbar, object font)
        {
            var state = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.F5Interaction", true), true);
            var layout = Get(state, "Layout"); Set(state, "Ready", true); Set(layout, "BlueprintAttached", true);
            Call(state, "Navigate", 6); Call(state, "RestoreVisible");
            Type measureType = layout.GetType().GetMethod("Ensure", F).GetParameters()[5].ParameterType;
            var measure = Delegate.CreateDelegate(measureType, renderer, renderer.GetType().GetMethod("Measure", F));
            Action ensure = () => Call(layout, "Ensure", 1280f, 900f, 1f, (int)Get(state, "Page"), font, measure);
            foreach (int operation in new[] { 0, 4, 5, 6 })
            {
                Call(layout, "SetBlueprintOperation", operation); ensure();
                var elements = Objects(Get(layout, "Elements"));
                var ids = elements.Where(e => GetOptional(e, "HotkeyTarget") != null).Select(e => (string)Get(e, "HotkeyTarget")).ToArray();
                Require(ids.Count(id => id == EditIds[0]) == 1 && ids.Count(id => id == EditIds[1]) == 1 &&
                    ids.Count(id => id == EditIds[2]) == (operation == 5 || operation == 6 ? 1 : 0), "three edit rows use distinct stable action identities; confirm is contextual");
                Require(elements.Any(e => (string)GetOptional(e, "Text") == (operation == 4 ? "取消镜像" : "水平镜像")) &&
                    elements.Any(e => (string)GetOptional(e, "Text") == (operation == 5 || operation == 6 ? "取消修剪" : "局部修剪")), "only the active editor owns its cancel wording");
            }
            var confirm = Objects(Get(layout, "Elements")).Single(e => (string)GetOptional(e, "HotkeyTarget") == EditIds[2]);
            foreach (var pair in new[] { new[] { EditIds[0], "双击设置水平镜像的快捷键" }, new[] { EditIds[1], "双击设置局部修剪的快捷键" }, new[] { EditIds[2], "双击设置确定修剪的快捷键" } })
            {
                var key = Objects(Get(layout, "Elements")).Single(e => (string)GetOptional(e, "HotkeyTarget") == pair[0]);
                Hover(state, layout, key);
                Require((string)renderer.GetType().GetMethod("ResolveHint", F).Invoke(renderer, new object[] { state, null, false, false, null, null }) == pair[1],
                    "actual binder hint describes an action rather than a feature switch: " + pair[0]);
            }
            Click(state, layout, confirm, true);
            Call(layout, "SetBlueprintPreviewRevision", 91L); ensure();
            Click(state, layout, confirm, false);
            Require(GetOptional(state, "ClickedHotkey") == null && (bool)Get(state, "ConsumeLeft"), "a new preview retires the old confirm press but consumes its real release");

            foreach (bool submenu in new[] { true, false })
            {
                Call(state, "Navigate", 6); Call(layout, "SetBlueprintLibraryVisible", false); ensure();
                var mirror = Objects(Get(layout, "Elements")).Single(e => (string)GetOptional(e, "HotkeyTarget") == EditIds[0]);
                Click(state, layout, mirror, true);
                if (submenu) Call(layout, "SetBlueprintLibraryVisible", true); else Call(state, "Navigate", 9);
                ensure(); Click(state, layout, mirror, false);
                Require(GetOptional(state, "ClickedHotkey") == null && (bool)Get(state, "ConsumeLeft"), "submenu/page departure invalidates old binder press and retains release tail");
                Require(!Objects(Get(layout, "Elements")).Any(e => EditIds.Contains((string)GetOptional(e, "HotkeyTarget"))), "secondary or other page has no transparent blueprint binder targets");
            }

            var bar = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintToolbarLayout", true), true);
            var barMeasure = Delegate.CreateDelegate(bar.GetType().GetMethod("Ensure", F).GetParameters()[4].ParameterType,
                toolbar, toolbar.GetType().GetMethod("Measure", F));
            foreach (int mode in new[] { 1, 2, 3, 4, 5, 6, 7 })
            {
                Require((bool)Call(bar, "EnsureProjection", 960f, 540f, 1.5f, font, barMeasure, false, mode), "expanded toolbar wraps inside supported narrow geometry");
                var buttons = Objects(Get(bar, "Buttons"));
                var actions = Enumerable.Range(0, buttons.Length).ToDictionary(i => Call(bar, "Action", i).ToString(), i => (string)Get(buttons[i], "Text"));
                Require(actions["Mirror"] == (mode == 5 ? "取消镜像" : "水平镜像") && actions["Trim"] == (mode == 6 || mode == 7 ? "取消修剪" : "局部修剪") &&
                    actions.ContainsKey("ConfirmTrim") == (mode == 6 || mode == 7), "toolbar uses exact edit action identity and explicit contextual confirmation");
                int generation = (int)Get(bar, "Generation");
                Call(bar, "SetPreviewRevision", 1L);
                Require(!(bool)Call(bar, "MatchesProjection", 960f, 540f, 1.5f, font, false, mode), "changed preview rejects old toolbar geometry identity");
                Call(bar, "EnsureProjection", 960f, 540f, 1.5f, font, barMeasure, false, mode);
                Require((int)Get(bar, "Generation") > generation, "preview change invalidates an armed toolbar release");
                Call(bar, "SetPreviewRevision", 0L);
            }
            Call(state, "Close");
        }

        private static void AdmissionAndCommands(object context, object renderer, object font)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var state = Get(shell, "State");
            var input = Get(context, "Input"); var layout = Get(state, "Layout"); var library = (LibraryState)Get(host, "Library");
            var capture = (BlueprintCaptureState)Get(host, "State");
            string[] flagNames = { "dispatchGameplay", "dispatchBlueprint", "dispatchJourneyClose", "dispatchTarget", "dispatchMap" };
            var flags = flagNames.ToDictionary(name => name, name => Get(shell, name));
            bool oldInterface = Main.LocalPlayer.mouseInterface, oldChat = Main.drawingPlayerChat, oldWriting = PlayerInput.WritingText;
            var oldText = Main.CurrentInputTextTakerOverride;
            Call(shell, "CloseAndSubmitPosition"); Call(host, "ClearProjection"); Call(host, "CancelCaptureOrDraft"); capture.Exit();
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
            int ordinaryMouse = 0;
            var registry = new HotkeyRegistry(); Call(host, "Register", registry, null);
            registry.Register(new HotkeyAction("test.ui-world-right", "独立鼠标动作", HotkeyContext.Gameplay, () => true, () => ordinaryMouse++));
            var bindLayout = Activator.CreateInstance(layout.GetType(), true); Set(bindLayout, "BlueprintAttached", true);
            Call(bindLayout, "SetBlueprintOperation", 6);
            var rowMeasure = Delegate.CreateDelegate(bindLayout.GetType().GetMethod("Ensure", F).GetParameters()[5].ParameterType,
                renderer, renderer.GetType().GetMethod("Measure", F));
            Call(bindLayout, "Ensure", 1280f, 900f, 1f, 6, font, rowMeasure);
            string[] rowTargets = Objects(Get(bindLayout, "Elements")).Where(e => EditIds.Contains((string)GetOptional(e, "HotkeyTarget")))
                .Select(e => (string)Get(e, "HotkeyTarget")).ToArray();
            Require(rowTargets.SequenceEqual(EditIds), "actual F5 rows supply the three persisted dispatch identities");
            using (var bindings = SavedAndReopened(registry, rowTargets))
            try
            {
                var entry = NativeBlueprintPlacementChecks.Seed(library, 5, 2, "编辑入口图");
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Require((bool)Call(host, "Place", entry), "real isolated asset opens as an instance");
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return Get(host, "ProjectionMode").ToString() == "Following"; });
                Call(host, "AcceptWorldPointer", true, false, false, 30, 30, Main.maxTilesX, Main.maxTilesY);
                Call(host, "AcceptWorldPointer", true, true, false, 30, 30, Main.maxTilesX, Main.maxTilesY);
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return Get(host, "ProjectionMode").ToString() == "Fixed"; });

                Main.LocalPlayer.mouseInterface = false;
                var admit = (Func<HotkeyAction, HotkeyChord, bool>)Get(shell, "admitHotkey");
                foreach (int page in Enumerable.Range(0, 12))
                {
                    Call(state, "Navigate", page); Set(state, "Ready", true); Call(state, "RestoreVisible");
                    NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
                    NeutralUi(state);
                    HoverWindow(state); Call(shell, "LeaseMouseInterface");
                    Require((bool)Get(shell, "OwnsPointer") && Main.LocalPlayer.mouseInterface &&
                        (bool)Call(shell, "CanDispatchBlueprintEdit", true, false, false), "ordinary F5 hover admits only the edit group on tab " + page);
                    Call(shell, "RestoreLeases");
                }
                Require(!(bool)Call(shell, "CanDispatchBlueprintEdit", true, false, true), "a higher UI owner blocks the edit group");
                Main.drawingPlayerChat = true;
                Require(!(bool)Call(shell, "CanDispatchBlueprintEdit", true, false, false), "native text owner blocks the edit group");
                Main.drawingPlayerChat = false;
                Require(!(bool)Call(shell, "CanDispatchBlueprintEdit", false, false, false), "missing current input permission never admits editing");

                Call(state, "Navigate", 6); Call(renderer, "Prepare", state, 1280f, 900f, 1f);
                var keys = Objects(Get(layout, "Elements")).Where(e => GetOptional(e, "HotkeyTarget") != null).ToArray();
                foreach (string id in EditIds.Take(2))
                {
                    var key = keys.Single(e => (string)Get(e, "HotkeyTarget") == id);
                    Call(shell, "OpenHotkey", (string)Get(key, "HotkeyTarget"), Get(key, "Rect"));
                    Call(shell, "OpenHotkey", (string)Get(key, "HotkeyTarget"), Get(key, "Rect"));
                    Require((string)Get(Get(shell, "HotkeyPopup"), "Target") == id && !(bool)Call(shell, "CanDispatchBlueprintEdit", true, false, false), "actual row opens only its own common binder and modal blocks dispatch: " + id);
                    Call(Get(shell, "HotkeyPopup"), "Close");
                }
                foreach (var pair in new[] { new[] { EditIds[0], "OwnMirrorCancellation", "F16" }, new[] { EditIds[1], "OwnTrimCancellation", "F17" } })
                {
                    Call(state, "Navigate", 9); Call(state, "RestoreVisible");
                    foreach (string name in flagNames) Set(shell, name, name == "dispatchBlueprint");
                    NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
                    NativeQuickItemChecks.Sample(input, new[] { (Keys)Enum.Parse(typeof(Keys), pair[2]) });
                    bindings.Dispatch((HotkeyInput)Get(input, "Hotkeys"), HotkeyContext.SinglePlayer, false, admit: admit);
                    Require((bool)Get(host, pair[1]) && !(bool)Get(state, "Visible"), "saved real action starts the matching Host editor and safely leaves ordinary F5");
                    long transaction = (long)Get(host, "projectionTransaction");
                    NativeQuickItemChecks.Sample(input, new[] { (Keys)Enum.Parse(typeof(Keys), pair[2]) });
                    bindings.Dispatch((HotkeyInput)Get(input, "Hotkeys"), HotkeyContext.SinglePlayer, false, admit: admit);
                    Require((long)Get(host, "projectionTransaction") == transaction && (bool)Get(host, pair[1]), "held action does not repeat or cancel its editor");
                    NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, new[] { (Keys)Enum.Parse(typeof(Keys), pair[2]) });
                    bindings.Dispatch((HotkeyInput)Get(input, "Hotkeys"), HotkeyContext.SinglePlayer, false, admit: admit);
                    Require(!(bool)Get(host, "ProjectionEditing"), "a new same-function primary cancels only its own editor");
                }
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "TrimProjection");
                Require((bool)Get(host, "OwnTrimCancellation"), "world mouse ownership fixture starts actual trim selection");
                long unconfirmedScene = (long)Get(host, "projectionSceneRevision");
                NativeQuickItemChecks.Sample(input, new[] { Keys.F18 });
                bindings.Dispatch((HotkeyInput)Get(input, "Hotkeys"), HotkeyContext.SinglePlayer, false, admit: admit);
                Require((bool)Get(host, "OwnTrimCancellation") && (long)Get(host, "projectionSceneRevision") == unconfirmedScene,
                    "saved independent confirm binding cannot commit without a complete stable preview");
                Save(bindings, EditIds[1], "Mouse1"); Set(shell, "dispatchGameplay", true); Set(shell, "dispatchBlueprint", true);
                SampleMouse(input, 0); SampleMouse(input, 1);
                Require((bool)Get(host, "CanRetainEdit"), "world mouse ownership has no higher UI or text owner");
                bindings.Dispatch((HotkeyInput)Get(input, "Hotkeys"), HotkeyContext.SinglePlayer, true, admit: admit);
                Require((bool)Get(host, "OwnTrimCancellation"), "active world Mouse1 selects or drags and never cancels its bound trim action");
                SampleMouse(input, 0); SampleMouse(input, 2);
                bindings.Dispatch((HotkeyInput)Get(input, "Hotkeys"), HotkeyContext.SinglePlayer, true, admit: admit);
                Require(ordinaryMouse == 0 && (bool)Get(host, "OwnTrimCancellation"), "active world Mouse2 remains the world cancellation primary, never another public action");
                Save(bindings, EditIds[1], "Mouse3"); SampleMouse(input, 0); SampleMouse(input, 3);
                bindings.Dispatch((HotkeyInput)Get(input, "Hotkeys"), HotkeyContext.SinglePlayer, true, admit: admit);
                Require(!(bool)Get(host, "ProjectionEditing"), "Mouse3 same-function binding remains an explicit cancellation");
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
                Call(state, "Navigate", 4); Call(state, "RestoreVisible");
                int notesLeave = 0;
                var notes = Get(shell, "notes"); var workspace = Get(notes, "workspace");
                NativeQuickItemChecks.Until(() => { Call(workspace, "Poll"); return (bool)Get(Get(workspace, "Feature"), "Readable") && !(bool)Get(Get(workspace, "Feature"), "Busy"); });
                Call(shell, "RequestBlueprintEditLeave", EditIds[0], (Action)(() => notesLeave++));
                Call(input, "BeginUpdate"); // no new sample is authorized by this outer Update
                NativeQuickItemChecks.Until(() => { Call(workspace, "Poll"); Call(notes, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 900)); return notesLeave == 1; });
                Require(notesLeave == 1 && !(bool)Get(state, "Visible"), "Notes safe navigation finishes one accepted command without demanding another physical input sample");
                Call(notes, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 900));
                Require(notesLeave == 1, "completed Notes navigation cannot repeat the same accepted edit");
            }
            finally
            {
                foreach (var flag in flags) Set(shell, flag.Key, flag.Value);
                Main.LocalPlayer.mouseInterface = oldInterface; Main.drawingPlayerChat = oldChat; PlayerInput.WritingText = oldWriting;
                Main.CurrentInputTextTakerOverride = oldText;
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(shell, "CloseAndSubmitPosition"); Call(host, "CancelProjection"); Call(host, "ClearProjection"); capture.Exit();
            }
        }

        private static void SampleMouse(object input, int button)
        {
            Main.keyState = new KeyboardState();
            PlayerInput.MouseInfo = new MouseState(200, 200, 0, button == 1 ? ButtonState.Pressed : ButtonState.Released,
                button == 3 ? ButtonState.Pressed : ButtonState.Released, button == 2 ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);
            PlayerInput.Triggers.Reset(); Call(input, "BeginUpdate"); Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh");
        }
        private static void Click(object state, object layout, object element, bool left)
        {
            var area = Get(element, "Rect"); var view = Get(layout, "Viewport");
            object sample = Activator.CreateInstance(state.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Input", true));
            Set(sample, "Width", 1280f); Set(sample, "Height", 900f); Set(sample, "Scale", 1f); Set(sample, "Active", true); Set(sample, "Focused", true);
            if (left) Call(state, "Update", sample);
            Call(state, "ScrollTo", Math.Max(0f, (float)Get(area, "Y") - 20));
            Set(sample, "X", (float)Get(state, "X") + (float)Get(view, "X") + (float)Get(area, "X") + 3);
            Set(sample, "Y", (float)Get(state, "Y") + (float)Get(view, "Y") + (float)Get(area, "Y") - (float)Get(state, "Scroll") + 3);
            Set(sample, "Left", left); Call(state, "Update", sample);
        }
        private static void Hover(object state, object layout, object element)
        {
            var area = Get(element, "Rect"); var view = Get(layout, "Viewport"); NeutralUi(state);
            Call(state, "ScrollTo", Math.Max(0f, (float)Get(area, "Y") - 20));
            object sample = Activator.CreateInstance(state.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Input", true));
            Set(sample, "Width", 1280f); Set(sample, "Height", 900f); Set(sample, "Scale", 1f); Set(sample, "Active", true); Set(sample, "Focused", true);
            Set(sample, "X", (float)Get(state, "X") + (float)Get(view, "X") + (float)Get(area, "X") + 3);
            Set(sample, "Y", (float)Get(state, "Y") + (float)Get(view, "Y") + (float)Get(area, "Y") - (float)Get(state, "Scroll") + 3);
            Call(state, "Update", sample);
        }
        private static void NeutralUi(object state)
        {
            object sample = Activator.CreateInstance(state.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Input", true));
            Set(sample, "Width", 1280f); Set(sample, "Height", 900f); Set(sample, "Scale", 1f); Set(sample, "Active", true); Set(sample, "Focused", true);
            Call(state, "Update", sample); Call(state, "Update", sample);
        }
        private static void HoverWindow(object state)
        {
            object sample = Activator.CreateInstance(state.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Input", true));
            Set(sample, "Width", 1280f); Set(sample, "Height", 900f); Set(sample, "Scale", 1f); Set(sample, "Active", true); Set(sample, "Focused", true);
            Set(sample, "X", (float)Get(state, "X") + 40); Set(sample, "Y", (float)Get(state, "Y") + 16);
            Call(state, "Update", sample);
        }
        private static object[] Objects(object value) { return ((IEnumerable)value).Cast<object>().ToArray(); }
        private static void Wait(HotkeyBindings owner, Func<bool> done) { NativeQuickItemChecks.Until(() => { owner.Poll(); return done(); }); }
        private static HotkeyBindings SavedAndReopened(HotkeyRegistry registry, string[] rowTargets)
        {
            var storage = new BindingStorage();
            using (var first = new HotkeyBindings(registry, storage))
            {
                Wait(first, () => first.Loaded);
                Require(rowTargets.All(id => registry.Find(id) != null && first.Get(id) == null), "all three production edit registrations are initially unbound");
                for (int i = 0; i < rowTargets.Length; i++) Save(first, rowTargets[i], new[] { "F16", "F17", "F18" }[i]);
                Save(first, "test.ui-world-right", "Mouse2");
            }
            // Reopen the acknowledged exact document in a new coordinator over
            // its own storage copy, with no concurrent writers or private data.
            var reopened = new HotkeyBindings(registry, new BindingStorage(storage.Snapshot));
            try
            {
                Wait(reopened, () => reopened.Loaded);
                Require(rowTargets.Select(id => reopened.Get(id).Text).SequenceEqual(new[] { "F16", "F17", "F18" }), "reopened common document retains exact row targets before physical dispatch");
                return reopened;
            }
            catch { reopened.Dispose(); throw; }
        }
        private static void Save(HotkeyBindings owner, string id, string value)
        {
            HotkeyChord chord; string reason; Require(HotkeyChord.TryParse(value, out chord, out reason), "fixture chord parses: " + reason);
            long command; Require(owner.TrySet(id, chord, null, out command, out reason), "common binder accepts real action: " + reason);
            Wait(owner, () => !owner.Busy); Require(owner.CompletionSucceeded && owner.Get(id).Equals(chord), "reliable binding completion activates the exact action/chord");
        }
        private sealed class BindingStorage : IPreferenceStorage
        {
            private byte[] bytes; private int revision;
            internal BindingStorage(byte[] initial = null) { bytes = initial == null ? null : (byte[])initial.Clone(); }
            internal byte[] Snapshot { get { return bytes == null ? null : (byte[])bytes.Clone(); } }
            public PreferenceReadResult Read() { return new PreferenceReadResult(bytes == null ? PreferenceReadStatus.Missing : PreferenceReadStatus.Loaded, bytes, revision.ToString(), null); }
            public PreferenceWriteResult Write(string expected, byte[] value)
            {
                if (expected != revision.ToString()) return new PreferenceWriteResult(PreferenceWriteStatus.Conflict, null, "fixture identity changed", false, true);
                bytes = (byte[])value.Clone(); return new PreferenceWriteResult(PreferenceWriteStatus.Saved, (++revision).ToString(), null);
            }
            public void Dispose() { }
        }
    }
}
