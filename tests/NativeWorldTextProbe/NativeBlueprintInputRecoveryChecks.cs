using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Independent literal stages and actual production consumers. These tests
    // operate isolated files/samples; they do not establish OS input or IME.
    internal static class NativeBlueprintInputRecoveryChecks
    {
        internal static void Cpu(object context)
        {
            var host = Get(context, "Blueprint"); var input = Get(context, "Input"); var shell = Get(context, "Shell");
            var state = (BlueprintCaptureState)Get(host, "State"); var library = (LibraryState)Get(host, "Library");
            var flow = (BlueprintCaptureFlow)Get(host, "Flow"); var ui = Get(shell, "BlueprintUi");
            Action sample = () => NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            Action drain = () => NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
            Action clear = () => { Call(shell, "CloseAndSubmitPosition"); Call(host, "CancelCaptureOrDraft"); Call(host, "ClearProjection"); state.Exit(); drain(); sample(); };
            clear(); var entry = NativeBlueprintPlacementChecks.Seed(library, 3, 2, "输入阶段反例");
            try
            {
                Call(input, "BeginUpdate");
                Require(!(bool)Get(input, "CanStartActions") && (bool)Get(host, "CanOpenLibrary"), "unsampled phase revokes action permission but preserves library business eligibility");
                Call(host, "OpenLibrary"); Require(!(bool)Get(ui, "Submenu"), "actual OpenLibrary rejects before presentation mutation");
                Require(!(bool)Call(host, "Place", entry) && GetOptional(host, "pendingProjection") == null, "actual new Place rejects unsampled input");
                for (int i = 0; i < 4; i++) Call(host, "Poll");
                Require(GetOptional(host, "pendingProjection") == null && !(bool)Get(ui, "Submenu"), "rejected actions are not queued for a later Poll");
                sample(); Call(host, "OpenLibrary"); Require((bool)Get(ui, "Submenu"), "fresh completed input admits one explicit library action");
                clear(); state.Enter(); state.Selection.ToggleRectangle(1, 1, 1, 1, 120, 120);
                Call(input, "BeginUpdate"); long accepted = library.AcceptedCommandId;
                Require((bool)Get(host, "CanSave") && !(bool)Call(host, "Save") && library.AcceptedCommandId == accepted, "Save display remains available while actual entry rejects unsampled input");
                sample(); Require((bool)Call(host, "Save"), "fresh Save admitted once"); long save = library.AcceptedCommandId;
                Require(!(bool)Call(host, "Save") && library.AcceptedCommandId == save, "same accepted capture cannot duplicate stage command");
                clear(); sample(); Require((bool)Call(host, "Place", entry), "sampled Place starts Open");
                long transaction = (long)Get(host, "projectionTransaction"); Call(input, "BeginUpdate");
                Require((bool)Get(host, "CanInvokePlace") && !(bool)Call(host, "Place", (object)null) && (long)Get(host, "projectionTransaction") == transaction && GetOptional(host, "pendingProjection") != null,
                    "Place same-action cancellation also rejects unsampled input without touching pending Open");
                sample(); Require((bool)Call(host, "Place", (object)null), "fresh same-action cancellation admitted"); drain();
                sample(); Require((bool)Call(host, "Place", entry), "new real projection admitted");
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return Get(host, "ProjectionMode").ToString() == "Following"; });
                Call(host, "AcceptWorldPointer", true, false, false, 20, 20, 120, 120);
                Call(host, "AcceptWorldPointer", true, true, false, 20, 20, 120, 120);
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return Get(host, "ProjectionMode").ToString() == "Fixed"; });
                foreach (string action in new[] { "MoveProjection", "RemoveProjection" })
                {
                    sample(); Call(input, "BeginUpdate"); Call(host, action);
                    Require(Get(host, "ProjectionMode").ToString() == "Fixed", "actual " + action + " start rejects unsampled input");
                    sample(); Call(host, action); string mode = Get(host, "ProjectionMode").ToString();
                    Call(input, "BeginUpdate"); Call(host, action);
                    Require(Get(host, "ProjectionMode").ToString() == mode, "actual " + action + " cancellation rejects unsampled input");
                    sample(); Call(host, action); Require(Get(host, "ProjectionMode").ToString() == "Fixed", "fresh same-action cancellation completes once");
                }
                sample(); Call(host, "RemoveProjection"); Call(input, "BeginUpdate"); Call(host, "RemoveAllProjections");
                Require(((IEnumerable)Get(host, "ProjectionScene")).Cast<object>().Count() == 1, "RemoveAll retains scene when current Update lacks input permission");
                sample(); Call(host, "RemoveAllProjections");
                Require(!((IEnumerable)Get(host, "ProjectionScene")).Cast<object>().Any(), "fresh RemoveAll consumes the existing operation once");
                Console.WriteLine("PASS Blueprint input entries Open/Save/Move/Remove/Place including cancellations/RemoveAll reject skipped mapping+keyboard; no delayed action; fresh commands and duplicate-save guard.");
            }
            finally { clear(); }
        }

        internal static void Visual(object context, ProbeGraphics graphics)
        {
            Cpu(context);
            var host = Get(context, "Blueprint"); var input = Get(context, "Input"); var shell = Get(context, "Shell");
            var state = Get(shell, "State"); var renderer = Get(shell, "renderer"); var ui = Get(shell, "BlueprintUi");
            var capture = (BlueprintCaptureState)Get(host, "State"); var flow = (BlueprintCaptureFlow)Get(host, "Flow");
            var libraryField = host.GetType().GetField("<Library>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            var originalLibrary = (LibraryState)libraryField.GetValue(host);
            Call(shell, "CloseAndSubmitPosition"); Call(host, "CancelCaptureOrDraft"); Call(host, "ClearProjection"); capture.Exit();
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !originalLibrary.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
            using (var library = new LibraryState(new FileBlueprintLibrary(Path.Combine(Terraria.Program.SavePath, "input-recovery-seven"))))
            {
                libraryField.SetValue(host, library);
                try
                {
                    Main.screenWidth = 1280; Main.screenHeight = 900; Main.UIScale = 1f; PlayerInput.CacheOriginalScreenDimensions();
                    var entry = NativeBlueprintPlacementChecks.Seed(library, 2, 2, "阶段缓存");
                    Call(ui, "OpenLibrary"); Call(Get(state, "Layout"), "SetBlueprintLibraryVisible", true);
                    Call(renderer, "RefreshResources"); Call(renderer, "Prepare", state, 1280f, 900f, 1f);
                    Action prepare = () => { Call(host, "Poll"); Call(ui, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 900)); };
                    NativeQuickItemChecks.Until(() => { prepare(); return !library.Snapshot.Busy && (int)Get(ui, "PreviewCount") == 1; });
                    var controls = Get(renderer, "BlueprintControls"); var toolbar = Get(host, "Toolbar");
                    var commandType = controls.GetType().GetMethod("Available", BindingFlags.Instance | BindingFlags.NonPublic).GetParameters()[0].ParameterType;
                    var commands = new[] { "BlueprintSave", "BlueprintLibrary", "BlueprintMove", "BlueprintRemove" }.Select(n => Enum.Parse(commandType, n)).ToArray();
                    Func<string[]> f5 = () => commands.Select(c => Call(controls, "Available", c) + ":" + (string)Call(controls, "Hint", c)).ToArray();
                    Func<object> place = () => ((IEnumerable)Get(ui, "parts")).Cast<object>().Single(p => Get(p, "Command").ToString() == "Place");
                    // Freeze complete page resources before the phase sequence.
                    NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); prepare();
                    var baseline = f5(); var card = place(); long builds = (long)Get(ui, "LayoutBuilds");
                    Require((bool)Get(card, "Enabled"), "actual library part starts enabled with a usable asset");
                    Call(input, "BeginUpdate"); prepare();
                    Require(f5().SequenceEqual(baseline) && ReferenceEquals(card, place()) && (bool)Get(place(), "Enabled") && (long)Get(ui, "LayoutBuilds") == builds,
                        "F5 hints/enabled and real cached Place part remain unchanged through skipped native input without Build");
                    NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); prepare();
                    Require(f5().SequenceEqual(baseline) && (long)Get(ui, "LayoutBuilds") == builds, "complete input again does not rebuild phase-independent cards");
                    Set(host, "deletePending", true); prepare();
                    Require(!(bool)Get(place(), "Enabled") && (long)Get(ui, "LayoutBuilds") > builds, "actual business placement gate invalidates enabled cache");
                    Set(host, "deletePending", false); prepare(); Require((bool)Get(place(), "Enabled"), "business recovery rebuilds the real enabled card");
                    ToolbarPhases(host, input, shell, toolbar, controls, commands, graphics, library, entry);
                    Call(shell, "CloseAndSubmitPosition"); NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy; });
                    SevenObjects(host, ui, shell, state, renderer, library, flow, prepare);
                    Console.WriteLine("PASS Blueprint actual F5/Toolbar display and cached-card phase stability, business invalidation and seven-object two-page focus recovery; OS physical input/IME NOT_RUN.");
                }
                finally
                {
                    Call(ui, "Suspend"); Call(host, "CancelCaptureOrDraft");
                    NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
                    capture.Exit(); Call(shell, "CloseAndSubmitPosition"); libraryField.SetValue(host, originalLibrary);
                }
            }
        }

        private static void ToolbarPhases(object host, object input, object shell, object toolbar, object controls, object[] commands, ProbeGraphics graphics, LibraryState library, LibraryEntry entry)
        {
            Call(shell, "CloseAndSubmitPosition"); var player = Main.LocalPlayer; Item old = player.inventory[0]; int selected = player.selectedItem;
            var capture = (BlueprintCaptureState)Get(host, "State");
            try
            {
                player.inventory[0] = new Item(); player.inventory[0].SetDefaults(Terraria.ID.ItemID.Gel); player.selectedItemState.Select(0); player.selectedItemState.Update(); Call(host, "SetToolbar", true);
                // Reach actual operation states so enabled Move/Remove and each
                // cancellation consumer participate, not only disabled buttons.
                Action sample = () => NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
                Action following = () => { sample(); Require((bool)Call(host, "Place", entry), "toolbar fixture uses real Place"); NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return Get(host, "ProjectionMode").ToString() == "Following"; }); };
                Action fixedScene = () => { following(); Call(host, "AcceptWorldPointer", true, false, false, 20, 20, 120, 120); Call(host, "AcceptWorldPointer", true, true, false, 20, 20, 120, 120); NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return Get(host, "ProjectionMode").ToString() == "Fixed"; }); };
                foreach (string mode in new[] { "idle", "capture", "fixed", "move", "remove", "place" })
                {
                    capture.Exit(); Call(host, "ClearProjection"); NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy; }); sample();
                    if (mode == "capture") { capture.Enter(); capture.Selection.ToggleRectangle(1, 1, 1, 1, 120, 120); }
                    if (mode == "fixed" || mode == "move" || mode == "remove") { fixedScene(); sample(); if (mode == "move") Call(host, "MoveProjection"); if (mode == "remove") Call(host, "RemoveProjection"); }
                    if (mode == "place") following();
                    Func<string[]> snapshot = () =>
                    {
                        graphics.Pixels(() => Call(toolbar, "Draw"), Main.UIScaleMatrix, 1280, 900);
                        Require((bool)Get(toolbar, "presented"), "actual toolbar remains presented");
                        var panel = Get(Get(toolbar, "Layout"), "Panel"); var buttons = ((IEnumerable)Get(Get(toolbar, "Layout"), "Buttons")).Cast<object>().ToArray();
                        var result = commands.Select(c => Call(controls, "Available", c) + ":" + (string)Call(controls, "Hint", c)).ToList();
                        for (int i = 0; i < buttons.Length; i++)
                        {
                            var rect = Get(buttons[i], "Rect"); int x = (int)((float)Get(panel, "X") + (float)Get(rect, "X") + (float)Get(rect, "Width") / 2);
                            int y = (int)((float)Get(panel, "Y") + (float)Get(rect, "Y") + (float)Get(rect, "Height") / 2);
                            PlayerInput.MouseInfo = new MouseState(x, y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                            graphics.Pixels(() => Call(toolbar, "Draw"), Main.UIScaleMatrix, 1280, 900);
                            var hint = Get(toolbar, "hint"); string text = String.Join("|", ((IEnumerable)Get(hint, "Lines")).Cast<object>().Select(l => (string)Get(l, "Text")));
                            Require(!text.Contains("请先结束当前输入"), "toolbar never derives a false input-phase reason");
                            result.Add(Call(toolbar, "Available", i) + ":" + Get(hint, "Visible") + ":" + text);
                        }
                        return result.ToArray();
                    };
                    NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); var before = snapshot();
                    Call(input, "BeginUpdate"); Require(snapshot().SequenceEqual(before), "actual toolbar availability and rendered hints remain stable during skipped sampling");
                    NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Require(snapshot().SequenceEqual(before), "toolbar hints remain identical after complete input resumes");
                }
            }
            finally { capture.Exit(); Call(host, "ClearProjection"); NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy; }); player.inventory[0] = old; player.selectedItemState.Select(selected); player.selectedItemState.Update(); }
        }

        private static void SevenObjects(object host, object ui, object shell, object state, object renderer, LibraryState library, BlueprintCaptureFlow flow, Action prepare)
        {
            var token = new CaptureToken(Guid.NewGuid(), 1, 1, 1); var handle = new ContentHandle(token.OperationId);
            var objects = Enumerable.Range(1, 7).Select(id => new CaptureObject { Id = id, X = id, Y = 0, Width = 1, Height = 1, Type = 10, Reliable = false, Reason = "构造的残缺组" }).ToArray();
            var cells = objects.Select(o => new CaptureCell { X = o.X, Y = 0, ObjectId = o.Id, UnreliableObject = true, Layers = CaptureLayer.Tile, TileType = 10 }).Concat(new[] { new CaptureCell { X = 0, Y = 0, Layers = CaptureLayer.Wall, WallType = 2 } }).ToArray();
            Require(library.TryBeginStage(token), "seven-object independent stage accepted"); Complete(library);
            Require(library.TryAppendChunk(handle, new CaptureChunk(cells, cells.Length, objects, objects.Length)), "literal seven objects accepted in existing chunk loop"); Complete(library);
            var original = new CaptureSummary { Width = 8, Height = 1, ContentCount = 8, UnreliableObjectCount = 7 };
            Require(library.TrySealDraft(handle, original), "literal raw summary sealed"); Complete(library);
            flow.Token = token; flow.ContentHandle = handle; flow.Summary = original; flow.Status = BlueprintCaptureStatus.DraftReady; flow.DraftName = "七对象恢复"; flow.ExcludeUnreliable = false; flow.ExcludedObjectIds = Array.Empty<int>(); flow.Revision++;
            Call(ui, "ShowCapture"); Call(Get(state, "Layout"), "SetBlueprintLibraryVisible", true); Call(renderer, "Prepare", state, 1280f, 900f, 1f);
            var cache = Get(ui, "previews"); Func<CaptureSummary> summary = () => (CaptureSummary)Call(cache, "Summary", Call(ui, "DraftKey"));
            Action ready = () => NativeQuickItemChecks.Until(() => { prepare(); return !library.Snapshot.Busy && summary() != null; });
            ready(); Require(summary().UnreliableObjectCount == 7, "raw preview reports independently constructed seven unresolved objects");
            var firstPage = Get(ui, "objects"); Require(((IEnumerable)Get(firstPage, "Items")).Cast<object>().Count() == 6, "actual first object page holds six");
            foreach (int id in Enumerable.Range(1, 6)) Call(host, "ExcludeObject", id, true);
            var next = ((IEnumerable)Get(ui, "parts")).Cast<object>().Single(p => Get(p, "Command").ToString() == "NextObjects"); Call(ui, "Execute", next);
            ready(); Require(summary().UnreliableObjectCount == 1 && ((string)Call(ui, "GetDraftSaveEligibility")).Contains("待排除"), "six individual exclusions leave exactly one and correctly refuse Save");
            Call(host, "ExcludeObject", 7, true); ready(); string allKey = (string)Call(ui, "DraftKey");
            Require(summary().UnreliableObjectCount == 0 && summary().ContentCount == 1 && Call(ui, "GetDraftSaveEligibility") == null && original.UnreliableObjectCount == 7,
                "all seven exclusions retain the wall and enable Save from paired filtered summary without changing raw Flow");
            Call(ui, "LoseFocus"); Require((int)Get(ui, "PreviewCount") == 0 && GetOptional(ui, "objects") == null, "focus loss actually releases GPU cache and page resources");
            // Focus restoration resumes the existing submenu. Explicit
            // ShowCapture is a different navigation operation that resets page.
            Call(state, "RestoreVisible"); ready();
            Require(((IEnumerable)Get(Get(ui, "objects"), "Items")).Cast<object>().Count() == 1 && flow.ExcludedObjectIds.SequenceEqual(Enumerable.Range(1, 7)) && Call(ui, "GetDraftSaveEligibility") == null,
                "same-key focus recovery loads only current one-item second page yet preserves Save and seven individual exclusions");
            Call(host, "ExcludeObject", 7, false);
            Require((string)Call(ui, "DraftKey") != allKey && Call(ui, "GetDraftSaveEligibility") != null, "old all-excluded key cannot unlock changed draft");
            ready(); Require(summary().UnreliableObjectCount == 1 && Call(ui, "GetDraftSaveEligibility") != null, "current worker summary rejects a real remaining object");
            Require(library.TryCreate(handle, "必须拒绝", token, false, flow.ExcludedObjectIds), "partial-exclusion Create reaches final authority");
            var rejected = Complete(library, false); Require(rejected.Status != LibraryStatus.Saved && rejected.Error.Contains("排除"), "final Create still refuses unresolved objects independently of UI");
            Call(host, "SetExcludeUnreliable", true); ready(); Require(summary().UnreliableObjectCount == 0 && summary().ContentCount == 1 && Call(ui, "GetDraftSaveEligibility") == null, "exclude-all retains existing layer semantics");
            // Text confirmation remains valid even between physical action samples.
            Call(ui, "BeginEdit", Guid.Empty, flow.DraftName); Call(Get(host, "Input"), "BeginUpdate");
            long accepted = library.AcceptedCommandId; Call(ui, "RequestFinish");
            Require(flow.Status == BlueprintCaptureStatus.Saving && library.AcceptedCommandId > accepted, "name Enter uses existing TextInput completion rather than a new action-phase gate");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return flow.Status == BlueprintCaptureStatus.Saved; });
            // Existing visual empty-result case also exercises the no-content gate.
            Console.WriteLine("PASS seven literal unresolved objects / two real pages / current filtered summary / focus GPU teardown and one-page rebuild / stale-key and final Create guards / phase-independent name confirmation.");
        }

        private static LibraryReceipt Complete(LibraryState library, bool success = true)
        {
            LibraryReceipt receipt = null; NativeQuickItemChecks.Until(() => { library.Pump(); return library.TryTakeReceipt(out receipt); });
            if (success) Require(receipt.Status == LibraryStatus.Completed || receipt.Status == LibraryStatus.Saved, "isolated repository command completed: " + receipt.Error);
            return receipt;
        }
    }
}
