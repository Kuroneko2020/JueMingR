using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using JueMingR.Features.Text;
using JueMingR.Infrastructure.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Controlled .8 outer-Update/Draw timing over the actual Host and GPU
    // consumer. Only the OS IME service is substituted; no Windows IME claim.
    internal static class NativeBlueprintInputContinuityChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static string composition = "";
        private static int enabled, disabled, clears, reads;
        internal sealed class ImeScope : IDisposable
        {
            private readonly Harmony patch = new Harmony("JueMingR.Tests.Blueprint.InputContinuity");
            internal ImeScope(object input)
            {
                var type = input.GetType().GetNestedType("NativeIme", BindingFlags.NonPublic);
                patch.Patch(type.GetMethod("Toggle", F), prefix: new HarmonyMethod(typeof(NativeBlueprintInputContinuityChecks), nameof(Toggle)));
                patch.Patch(type.GetProperty("Composition", F).GetGetMethod(true), prefix: new HarmonyMethod(typeof(NativeBlueprintInputContinuityChecks), nameof(Composition)));
                patch.Patch(type.GetProperty("Candidates", F).GetGetMethod(true), prefix: new HarmonyMethod(typeof(NativeBlueprintInputContinuityChecks), nameof(Candidates)));
                patch.Patch(typeof(Main).GetMethod("clrInput", F), prefix: new HarmonyMethod(typeof(NativeBlueprintInputContinuityChecks), nameof(Clear)));
                foreach (string method in new[] { "ReadBlock", "ReadRoot", "OpenIndex", "OpenMetadata", "EnumerateAssets" })
                    patch.Patch(typeof(FileBlueprintLibrary).GetMethod(method, F), prefix: new HarmonyMethod(typeof(NativeBlueprintInputContinuityChecks), nameof(Read)));
                composition = ""; enabled = disabled = clears = reads = 0;
            }
            public void Dispose() { patch.UnpatchAll(patch.Id); composition = ""; }
        }
        private static bool Toggle(bool enabled) { if (enabled) NativeBlueprintInputContinuityChecks.enabled++; else disabled++; PlayerInput.WritingText = enabled; return false; }
        private static bool Composition(ref string __result) { __result = composition; return false; }
        private static bool Candidates(ref bool __result) { __result = composition.Length != 0; return false; }
        private static void Clear() { clears++; }
        private static void Read() { System.Threading.Interlocked.Increment(ref reads); }

        // Also used by the old geometry specialist: acquire the real current
        // editor lease instead of installing leased/token fields by reflection.
        internal static IDisposable Lease(object ui)
        {
            var input = Get(ui, "TextInput"); string current = (string)Get(input, "Composition"); var scope = new ImeScope(input); composition = current;
            NativeQuickItemChecks.Sample(Get(Get(ui, "host"), "Input"), Array.Empty<Keys>());
            Call(ui, "BeforeInput", true); Call(input, "AfterSample", true, null, new KeyboardState(), true);
            return scope;
        }
        internal static void Run(object context, ProbeGraphics graphics)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var input = Get(context, "Input");
            var state = Get(shell, "State"); var ui = Get(shell, "BlueprintUi"); var layout = Get(state, "Layout");
            var renderer = Get(shell, "renderer"); var text = Get(ui, "TextInput");
            var flow = (BlueprintCaptureFlow)Get(host, "Flow");
            var libraryField = host.GetType().GetField("<Library>k__BackingField", F);
            var original = (LibraryState)libraryField.GetValue(host);
            Call(shell, "CloseAndSubmitPosition"); Call(host, "CancelCaptureOrDraft");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !original.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
            object oldOwner = Main.CurrentInputTextTakerOverride; bool oldBlock = Main.blockInput, oldWriting = PlayerInput.WritingText;
            using (var library = new LibraryState(new FileBlueprintLibrary(Path.Combine(Terraria.Program.SavePath, "input-continuity"))))
            using (NativeBlueprintLibraryWait.ObserveHost(library))
            using (var ime = new ImeScope(text))
            {
                libraryField.SetValue(host, library);
                try
                {
                    Main.screenWidth = 960; Main.screenHeight = 640; Main.UIScale = 1f; PlayerInput.CacheOriginalScreenDimensions();
                    Main.CurrentInputTextTakerOverride = null; Main.blockInput = false; PlayerInput.WritingText = false;
                    for (int i = 0; i < 6; i++) NativeBlueprintPlacementChecks.Seed(library, 2, 2, "连续输入" + i);
                    Action prepare = () => { Call(host, "Poll"); Call(layout, "SetBlueprintLibraryVisible", true); Call(renderer, "RefreshResources"); Call(renderer, "Prepare", state, 960f, 640f, 1f); Call(ui, "Prepare", true, Main.UIScaleMatrix, new Vector2(960, 640)); };
                    Action settle = () =>
                    {
                        Func<bool> ready = () => { prepare(); return !library.Snapshot.Busy && !(bool)Get(ui, "dirty") && (int)Get(ui, "PreviewCount") > 0; };
                        if (Get(ui, "mode").ToString() == "Library") NativeBlueprintLibraryWait.HostCondition(library, "input library settled", ready);
                        else NativeQuickItemChecks.Until(ready);
                    };
                    foreach (bool capture in new[] { false, true })
                    {
                        composition = "";
                        if (capture) { Draft(library, flow); Call(ui, "ShowCapture"); }
                        else Call(ui, "OpenLibrary");
                        settle();
                        // Settle the real outer shell's first size/origin update
                        // before beginning this text session, for both versions.
                        Frame(input, ui, state, Array.Empty<Keys>()); prepare();
                        Guid id = capture ? Guid.Empty : library.Snapshot.Page.Entries[0].AssetId;
                        Call(ui, "BeginEdit", id, "名字"); prepare();
                        Frame(input, ui, state, Array.Empty<Keys>()); composition = "pinyin";
                        Frame(input, ui, state, Array.Empty<Keys>()); prepare();
                        var editor = (TextEditBuffer)Get(ui, "Editor"); object bound = Get(text, "bound");
                        Require(ReferenceEquals(editor, bound) && (bool)Get(text, "Owned") && (string)Get(text, "Composition") == "pinyin", "real prefix/postfix establishes current editor and composition: capture=" + capture);
                        NativeBlueprintLibraryVisualChecks.DrawInput(ui, graphics);
                        Require(Anchor(ui) != null, "sampled real Draw initially submits anchor");
                        int on = enabled, off = disabled, queueClears = clears; long command = library.AcceptedCommandId, builds = (long)Get(ui, "LayoutBuilds"), uploads = (long)Get(Get(ui, "previews"), "Uploads");
                        int diskReads = reads; int queued = Main.keyCount; Main.keyInt[queued] = 90; Main.keyString[queued] = "Z"; Main.keyCount = queued + 1;
                        for (int i = 0; i < 3; i++)
                        {
                            // .8 DoUpdate clears this field before FrameSkip.Off
                            // returns without HandleInput. DoDraw clears anchor.
                            Main.CurrentInputTextTakerOverride = null; Call(input, "BeginUpdate"); ClearAnchor();
                            NativeBlueprintLibraryVisualChecks.DrawInput(ui, graphics);
                            Require(Anchor(ui) != null && NativeAnchor() != null, "empty outer Update actual Draw renews cleared anchor " + i + ": capture=" + capture);
                            Require(Main.CurrentInputTextTakerOverride == null && Main.keyCount == queued + 1 && editor.Text == "名字" && ReferenceEquals(bound, Get(text, "bound")) && (string)Get(text, "Composition") == "pinyin",
                                "Draw neither repairs token nor drains input nor replaces current composition/editor");
                        }
                        Require(enabled == on && disabled == off && clears == queueClears && reads == diskReads && library.AcceptedCommandId == command && (long)Get(ui, "LayoutBuilds") == builds && (long)Get(Get(ui, "previews"), "Uploads") == uploads,
                            "stable actual Draw does not toggle/finish/clear/read/build/upload/submit");
                        Console.WriteLine("BlueprintInput page=" + (capture ? "Capture" : "Library") + " empty-draws=3 anchors=3 token=null composition=pinyin delta-toggle/clear/read/command/build/upload=0");
                        Frame(input, ui, state, Array.Empty<Keys>()); prepare();
                        Require(editor.Text == "名字Z" && ReferenceEquals(editor, Get(ui, "Editor")), "next trusted sample consumes queued character once into same editor");
                        ScrollAndProjection(input, ui, state, prepare, graphics, library, editor);
                        ExitGates(input, ui, state, prepare, graphics);
                        Call(shell, "CloseAndSubmitPosition");
                        NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
                    }
                    Navigation(input, host, shell, ui, state, library, flow, prepare, settle);
                    NativeBlueprintLibraryUiChecks.InputContract(context);
                    Console.WriteLine("PASS BlueprintInput real leases; native clear/no HandleInput/cleared anchor/3 actual Draws in both pages; no Draw side effects; scroll/projection/offscreen/current pointer/focus/foreign/editor exits; Back/Escape/accepted Create/tails. Controlled native timing, Windows IME NOT_RUN.");
                }
                finally
                {
                    composition = ""; Main.CurrentInputTextTakerOverride = null;
                    Call(shell, "CloseAndSubmitPosition"); Call(host, "CancelCaptureOrDraft");
                    NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
                    libraryField.SetValue(host, original); Main.CurrentInputTextTakerOverride = oldOwner; Main.blockInput = oldBlock; PlayerInput.WritingText = oldWriting;
                }
            }
        }
        private static void Frame(object input, object ui, object state, Keys[] keys, bool left = false, int wheel = 0, bool active = true, bool focused = true, bool blocked = false, Vector2? point = null)
        {
            var p = point ?? new Vector2((float)Get(state, "X") + 130, (float)Get(state, "Y") + 180);
            Main.CurrentInputTextTakerOverride = null; PlayerInput.MouseInfo = new MouseState((int)p.X, (int)p.Y, 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            PlayerInput.Triggers.Reset(); Main.keyState = new KeyboardState(keys);
            Call(input, "BeginUpdate"); Call(ui, "BeforeInput", active && focused); Call(input, "AfterNativeMouse", PlayerInput.MouseKeys); Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh");
            // This is the actual Shell order: State.Update scrolls first;
            // Blueprint ProcessInput sees its old geometry until Prepare.
            var sample = Activator.CreateInstance(state.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Input", true));
            foreach (var pair in new[] { new { Name = "Width", Value = 960f }, new { Name = "Height", Value = 640f }, new { Name = "Scale", Value = 1f }, new { Name = "X", Value = p.X }, new { Name = "Y", Value = p.Y } }) Set(sample, pair.Name, pair.Value);
            Set(sample, "Active", active); Set(sample, "Focused", focused); Set(sample, "Left", left); Set(sample, "Wheel", wheel);
            Call(state, "Update", sample);
            Call(ui, "ProcessInput", active, new KeyboardState(keys), p, true, focused, blocked);
        }
        private static void ScrollAndProjection(object input, object ui, object state, Action prepare, ProbeGraphics graphics, LibraryState library, TextEditBuffer editor)
        {
            var text = Get(ui, "TextInput"); int off = disabled; object bound = Get(text, "bound"); long command = library.AcceptedCommandId;
            var before = Anchor(ui); Frame(input, ui, state, Array.Empty<Keys>(), wheel: -120); float scroll = (float)Get(state, "Scroll");
            Require(scroll > 0 && (bool)Get(text, "Owned") && ReferenceEquals(bound, Get(text, "bound")) && (string)Get(text, "Composition") == "pinyin" && disabled == off, "real State.Update scroll before ProcessInput preserves composition lease");
            prepare(); ClearAnchor(); NativeBlueprintLibraryVisualChecks.DrawInput(ui, graphics);
            if (Get(ui, "mode").ToString() == "Capture")
            {
                // The new Save/Back header stays fixed. A full forty-unit
                // wheel legitimately puts the name's caret above its body
                // scissor; a retained lease must not submit that old anchor.
                Require(Anchor(ui) == null && NativeAnchor() == null && (bool)Get(text, "Owned") && ReferenceEquals(bound, Get(text, "bound")),
                    "capture name hidden behind the fixed header retains composition but submits no IME anchor");
                float gap = (float)ui.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintLibraryLayout", true).GetField("HeaderGap", F).GetRawConstantValue();
                float visibleScroll = Math.Min(scroll, Math.Max(0, (float)Get(Get(ui, "editRect"), "Y") - (float)Get(ui, "captureHeaderHeight") - gap) / 2);
                Require(visibleScroll > 0 && visibleScroll < scroll, "actual capture geometry supplies a smaller nonzero visible-name scroll");
                Call(state, "ScrollTo", visibleScroll); Frame(input, ui, state, Array.Empty<Keys>()); prepare(); ClearAnchor(); NativeBlueprintLibraryVisualChecks.DrawInput(ui, graphics);
            }
            Require(Anchor(ui) != null && (Vector2)Anchor(ui) != (Vector2)before, "visible name anchor follows new scroll geometry");
            float x = (float)Get(state, "X"); Set(state, "X", x + 5);
            Frame(input, ui, state, Array.Empty<Keys>()); Require((bool)Get(text, "Owned") && disabled == off, "window projection mismatch retains current text lease");
            prepare(); ClearAnchor(); NativeBlueprintLibraryVisualChecks.DrawInput(ui, graphics); Require(Anchor(ui) != null, "new window projection submits current visible anchor");
            // Capture's ordinary one-cell summary is too short to scroll its
            // field fully offscreen. A legitimate wrapped error uses the same
            // real content-height path, rather than forging the field/clip.
            string validation = (string)GetOptional(ui, "validation");
            Set(ui, "validation", new string('错', 500)); Set(ui, "dirty", true); prepare();
            Call(state, "ScrollTo", 200f); Frame(input, ui, state, Array.Empty<Keys>()); prepare(); ClearAnchor(); NativeBlueprintLibraryVisualChecks.DrawInput(ui, graphics);
            Require(Anchor(ui) == null && NativeAnchor() == null && (bool)Get(text, "Owned") && ReferenceEquals(editor, Get(ui, "Editor")) && disabled == off && library.AcceptedCommandId == command, "offscreen name only suppresses anchor; no finish/save");
            Set(ui, "validation", validation); Set(ui, "dirty", true); Call(state, "ScrollTo", 0f); prepare(); NativeBlueprintLibraryVisualChecks.DrawInput(ui, graphics);
            var part = ((IEnumerable)Get(ui, "parts")).Cast<object>().First(p => Get(p, "Command").ToString() == "Back");
            var rect = Get(Get(part, "Element"), "Rect"); var view = Get(ui, "view");
            var point = new Vector2((float)Get(view, "X") + (float)Get(rect, "X") + 5, (float)Get(view, "Y") + (float)Get(rect, "Y") + 5);
            Frame(input, ui, state, Array.Empty<Keys>(), left: true, point: point);
            Require(GetOptional(ui, "armed") != null, "actual fresh press arms Back");
            Frame(input, ui, state, Array.Empty<Keys>(), left: false, wheel: -120, point: point);
            Require(GetOptional(ui, "armed") == null && (bool)Get(ui, "Submenu") && library.AcceptedCommandId == command, "scroll between press/release cancels stale action while text continues");
            Call(state, "ScrollTo", 0f); prepare(); Set(state, "X", x); prepare();
        }
        private static void ExitGates(object input, object ui, object state, Action prepare, ProbeGraphics graphics)
        {
            var text = Get(ui, "TextInput"); object foreign = new object();
            Main.UIScale = 1.25f; ClearAnchor(); NativeBlueprintLibraryVisualChecks.DrawInput(ui, graphics);
            Require(Anchor(ui) == null && NativeAnchor() == null, "stale frozen matrix cannot publish candidate anchor"); Main.UIScale = 1f;
            Frame(input, ui, state, Array.Empty<Keys>(), blocked: true);
            Require(!(bool)Get(text, "Owned"), "real blocked ProcessInput guard releases current lease");
            Frame(input, ui, state, Array.Empty<Keys>()); prepare();
            Main.CurrentInputTextTakerOverride = foreign; ClearAnchor(); NativeBlueprintLibraryVisualChecks.DrawInput(ui, graphics);
            Require(Anchor(ui) == null && ReferenceEquals(Main.CurrentInputTextTakerOverride, foreign), "foreign owner cannot borrow anchor or lose token");
            Call(ui, "BeforeInput", true); Require(!(bool)Get(text, "Owned") && ReferenceEquals(Main.CurrentInputTextTakerOverride, foreign) && !Main.blockInput, "foreign takeover releases own block and preserves foreign token");
            Main.CurrentInputTextTakerOverride = null; Frame(input, ui, state, Array.Empty<Keys>()); prepare();
            var editor = Get(ui, "Editor"); Set(ui, "editor", new TextEditBuffer("另一个", true, 48, 49152, "限制")); ClearAnchor(); NativeBlueprintLibraryVisualChecks.DrawInput(ui, graphics);
            Require(Anchor(ui) == null, "old editor lease cannot render for replacement before a sample"); Set(ui, "editor", editor);
            Call(ui, "BeforeInput", false); Require(!(bool)Get(text, "Owned"), "active=false guard releases immediately");
            Frame(input, ui, state, Array.Empty<Keys>()); prepare();
            Set(input, "foregroundWindow", (Func<IntPtr>)(() => IntPtr.Zero)); Call(input, "BeginUpdate"); ClearAnchor(); NativeBlueprintLibraryVisualChecks.DrawInput(ui, graphics);
            Require(Anchor(ui) == null && !(bool)Get(input, "CanPrepareText"), "actual focus quarantine rejects retained anchor");
            Call(ui, "BeforeInput", false); Require(!(bool)Get(text, "Owned"), "true focus exit releases lease");
            Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(1))); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
        }
        private static void Navigation(object input, object host, object shell, object ui, object state, LibraryState library, BlueprintCaptureFlow flow, Action prepare, Action settle)
        {
            foreach (bool capture in new[] { false, true })
            {
                composition = ""; if (capture) { Draft(library, flow); Call(ui, "ShowCapture"); } else Call(ui, "OpenLibrary"); settle();
                Call(ui, "BeginEdit", capture ? Guid.Empty : library.Snapshot.Page.Entries[0].AssetId, "编辑优先"); prepare();
                composition = "pin"; Frame(input, ui, state, Array.Empty<Keys>()); prepare();
                Main.keyInt[0] = 27; Main.keyString[0] = "\u001b"; Main.keyCount = 1;
                Frame(input, ui, state, new[] { Keys.Escape }); Require(GetOptional(ui, "Editor") != null && (bool)Get(ui, "Visible"), "first composing Escape stays in actual editor");
                composition = ""; Frame(input, ui, state, Array.Empty<Keys>()); Frame(input, ui, state, Array.Empty<Keys>());
                Main.keyInt[0] = 27; Main.keyString[0] = "\u001b"; Main.keyCount = 1;
                Frame(input, ui, state, new[] { Keys.Escape }); Require(GetOptional(ui, "Editor") == null && (bool)Get(ui, "Visible"), "independent noncomposition Escape cancels edit only");
                prepare(); Frame(input, ui, state, new[] { Keys.Escape }); Require((bool)Get(ui, "Visible"), "held edit Escape cannot navigate");
                Frame(input, ui, state, Array.Empty<Keys>()); Frame(input, ui, state, new[] { Keys.Escape });
                Require(!(bool)Get(ui, "Submenu") && (bool)Get(state, "Visible") == !capture, "later independent Escape agrees with Capture/Library Back");
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
                // A fresh pointer sequence goes through the actual consumer.
                if (capture) { Draft(library, flow); Call(ui, "ShowCapture"); } else Call(ui, "OpenLibrary"); settle();
                var back = ((IEnumerable)Get(ui, "parts")).Cast<object>().Single(p => Get(p, "Command").ToString() == "Back"); var rect = Get(Get(back, "Element"), "Rect"); var view = Get(ui, "view");
                var point = new Vector2((float)Get(view, "X") + (float)Get(rect, "X") + 8, (float)Get(view, "Y") + (float)Get(rect, "Y") + 8);
                Frame(input, ui, state, Array.Empty<Keys>(), point: point); Frame(input, ui, state, Array.Empty<Keys>(), left: true, point: point); Frame(input, ui, state, Array.Empty<Keys>(), point: point);
                Require(!(bool)Get(ui, "Submenu") && (bool)Get(state, "Visible") == !capture && (bool)Get(ui, "ConsumeLeft"), "fresh Back executes once and owns closing release sample");
                PlayerInput.Triggers.Current.MouseLeft = PlayerInput.Triggers.JustReleased.MouseLeft = true; Main.mouseLeft = true; Call(shell, "ConsumeSample");
                Require(!PlayerInput.Triggers.Current.MouseLeft && !PlayerInput.Triggers.JustReleased.MouseLeft && !Main.mouseLeft, "actual Shell consumer removes close click before world action");
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
            }
            Draft(library, flow); Call(ui, "ShowCapture"); settle(); long accepted = library.AcceptedCommandId;
            Call(ui, "Execute", ((IEnumerable)Get(ui, "parts")).Cast<object>().Single(p => Get(p, "Command").ToString() == "ConfirmDraft"));
            Require(flow.Status == BlueprintCaptureStatus.Saving && library.AcceptedCommandId > accepted, "real enabled Save accepts Create"); long create = library.AcceptedCommandId;
            foreach (var status in new[] { BlueprintCaptureStatus.Saving, BlueprintCaptureStatus.Saved, BlueprintCaptureStatus.CommitUnconfirmed })
            {
                flow.Status = status; Call(ui, "ShowCapture"); Call(ui, "Back");
                Require(!(bool)Get(state, "Visible") && flow.Status == status && library.AcceptedCommandId == create && !(bool)Get(host, "discardPending"), "Back preserves accepted Create state and queue: " + status);
            }
            flow.Status = BlueprintCaptureStatus.Saving;
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return flow.Status == BlueprintCaptureStatus.Saved; });
        }
        private static void Draft(LibraryState library, BlueprintCaptureFlow flow)
        {
            var token = new CaptureToken(Guid.NewGuid(), 1, 1, 1); var handle = new ContentHandle(token.OperationId);
            Require(library.TryBeginStage(token), "input draft begin"); Complete(library);
            Require(library.TryAppendChunk(handle, new CaptureChunk(new[] { new CaptureCell { X = 0, Y = 0, Layers = CaptureLayer.Wall, WallType = 2 } }, 1)), "input draft content"); Complete(library);
            var summary = new CaptureSummary { Width = 1, Height = 1, ContentCount = 1 };
            Require(library.TrySealDraft(handle, summary), "input draft sealed"); Complete(library);
            flow.Token = token; flow.ContentHandle = handle; flow.Summary = summary; flow.DraftName = "连续输入"; flow.Status = BlueprintCaptureStatus.DraftReady; flow.ExcludeUnreliable = false; flow.ExcludedObjectIds = Array.Empty<int>(); flow.Revision++;
        }
        private static void Complete(LibraryState library) { LibraryReceipt receipt = null; NativeQuickItemChecks.Until(() => { library.Pump(); return library.TryTakeReceipt(out receipt); }); Require(receipt.Status == LibraryStatus.Completed, "input fixture repository completion: " + receipt.Error); }
        private static object Anchor(object ui) { return GetOptional(Get(ui, "renderer"), "LastImeAnchor"); }
        private static object NativeAnchor() { return typeof(Main).GetField("imePanelAnchor", F).GetValue(null); }
        private static void ClearAnchor() { typeof(Main).GetField("imePanelAnchor", F).SetValue(null, null); }
    }
}
