using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintPlacementChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static LibraryEntry Seed(LibraryState library, int width, int height, string name)
        {
            Guid id = Guid.NewGuid(); var token = new CaptureToken(id, 1, 1, 1); var handle = new ContentHandle(id);
            Action<bool> complete = accepted => { Require(accepted, "isolated projection asset command accepted"); LibraryReceipt receipt = null; NativeQuickItemChecks.Until(() => { library.Pump(); return library.TryTakeReceipt(out receipt); }); Require(receipt.Status == LibraryStatus.Completed || receipt.Status == LibraryStatus.Saved, "isolated projection asset reliable result: " + receipt.Error); };
            complete(library.TryBeginStage(token));
            var cells = new[] { new CaptureCell { X = 30, Y = 40, Layers = CaptureLayer.Tile, TileType = 1 }, new CaptureCell { X = 30 + width - 1, Y = 40 + height - 1, Layers = CaptureLayer.Wire, Wires = 1 } };
            complete(library.TryAppendChunk(handle, new CaptureChunk(cells, 2)));
            complete(library.TrySealDraft(handle, new CaptureSummary { Left = 30, Top = 40, Width = width, Height = height, ContentCount = 2, StartTick = 1, EndTick = 2 }));
            complete(library.TryCreate(handle, name, token)); complete(library.TryList(7700, 0, id));
            return library.Snapshot.Page.Entries.Single(e => e.AssetId == id);
        }
        internal static void Run(object context)
        {
            var host = Get(context, "Blueprint"); var input = Get(context, "Input"); var shell = Get(context, "Shell");
            var state = (BlueprintCaptureState)Get(host, "State"); var library = (LibraryState)Get(host, "Library");
            Call(shell, "CloseAndSubmitPosition"); Call(host, "CancelCaptureOrDraft");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
            state.Exit(); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            var entry = Seed(library, 89, 32, "中心锚点稀疏图"); var second = Seed(library, 4, 2, "替换图"); var unrelated = Seed(library, 2, 2, "非关联删除图");
            Func<object> current = () => GetOptional(host, "CurrentProjection");
            Func<string> mode = () => Get(host, "ProjectionMode").ToString();
            Func<int> x = () => (int)Get(current(), "OriginX"), y = () => (int)Get(current(), "OriginY");
            Action<bool, bool, bool, int, int> pointer = (trusted, left, right, mx, my) => Call(host, "AcceptWorldPointer", trusted, left, right, mx, my, Main.maxTilesX, Main.maxTilesY);
            bool transient = true;
            Action<LibraryEntry> place = e =>
            {
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Require((bool)Call(host, "Place", e), "actual Host Place accepts valid idle capture");
                if (transient)
                {
                    transient = false; object oldWorld = Get(host, "world"); var worker = Get(library, "worker");
                    try
                    {
                        Set(host, "world", new object());
                        NativeQuickItemChecks.Until(() => { lock (Get(worker, "gate")) return GetOptional(worker, "projectionReceipt") != null; });
                        Call(host, "Poll");
                        Require(current() == null && GetOptional(host, "pendingProjection") != null && GetOptional(worker, "projectionReceipt") != null,
                            "temporary unavailable session observation preserves accepted bounded Opened receipt instead of losing it");
                    }
                    finally { Set(host, "world", oldWorld); }
                }
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return mode() == "Following"; });
            };
            try
            {
                place(entry);
                pointer(true, true, false, 100, 100);
                Require(mode() == "Following" && x() == 56 && y() == 85, "89x32 sparse center-air anchor is (44,15); startup hold cannot confirm");
                pointer(false, false, false, 0, 0); pointer(true, true, false, 100, 100);
                Require(mode() == "Following", "UI/unfocused pseudo release cannot rearm confirmation");
                pointer(true, false, false, 60, 60); pointer(true, true, false, 60, 60);
                Require(mode() == "Fixed" && x() == 16 && y() == 45, "new world click fixes complete effective bounds inside the isolated 120x120 world");
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
                PlayerInput.Triggers.Current.MouseLeft = PlayerInput.Triggers.Current.MouseRight = true; PlayerInput.Triggers.Current.CopyInto(Main.LocalPlayer);
                Require(Main.LocalPlayer.controlUseItem && Main.LocalPlayer.controlUseTile && !(bool)Get(host, "ProjectionEditing"), "Fixed releases ordinary native world actions");
                Call(host, "MoveProjection");
                pointer(true, true, false, 5, 5); Require(x() == 16 && y() == 45, "held UI/start action cannot recenter or move fixed origin");
                pointer(true, false, false, 90, 90); pointer(true, false, false, 93, 92);
                Require(x() == 19 && y() == 47, "move uses delta from first trusted neutral world baseline");
                pointer(false, false, false, 0, 0); pointer(true, true, false, 10, 10);
                Require(x() == 19 && y() == 47 && mode() == "Moving", "UI/focus takeover freezes movement and confirmation");
                pointer(true, false, false, 70, 70); Require(x() == 19 && y() == 47, "recovery neutral rebases without catch-up displacement");
                pointer(true, false, true, 71, 71);
                Require(mode() == "Fixed" && x() == 16 && y() == 45, "new right cancels move and restores exact original origin");
                place(second); pointer(true, false, false, 40, 40); pointer(true, false, true, 40, 40);
                Require(mode() == "Fixed" && ((BlueprintProjectionIdentity)Get(current(), "Identity")).AssetId == entry.AssetId && x() == 16 && y() == 45, "replacement cancel restores old immutable identity and origin");
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return GetOptional(host, "pendingProjection") == null; });
                state.Enter(); state.Selection.ToggleRectangle(1, 1, 2, 2, Main.maxTilesX, Main.maxTilesY);
                long revision = state.Selection.Revision;
                Require(!(bool)Get(host, "CanMoveProjection") && !(bool)Call(host, "Place", second), "all projection entry points share capture selection admission");
                Call(host, "MoveProjection"); Require(state.Selection.Revision == revision && state.Selection.HasSelection && mode() == "Fixed", "blocked Move/Place preserve capture data");
                state.Exit(); place(second); pointer(true, false, false, -10, 1); pointer(true, true, false, -10, 1);
                Require(mode() == "Following" && x() == -11 && y() == 1, "boundary confirmation checks full even-sized bounds without clamping anchor");
                pointer(true, false, false, 40, 40); pointer(true, true, false, 40, 40);
                Require(mode() == "Fixed" && x() == 39 && y() == 40, "even effective bounds choose upper-left central tile");
                var registry = (HotkeyRegistry)Get(Get(shell, "hotkeys"), "Registry"); var bindings = (HotkeyBindings)Get(Get(shell, "hotkeys"), "Bindings");
                Require(registry.Find("blueprint.move")?.CanConfigure == true && registry.Find("blueprint.remove")?.CanConfigure == true && bindings.Get("blueprint.move") == null && bindings.Get("blueprint.remove") == null, "move/remove use the real configurable unbound registry");
                var projectionWorker = Get(library, "worker");
                object fixedBeforeDelete = current();
                Require((bool)Call(host, "DeleteAsset", unrelated), "unrelated library deletion is accepted");
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy; });
                Require(ReferenceEquals(current(), fixedBeforeDelete) && mode() == "Fixed" && x() == 39 && y() == 40,
                    "unrelated deletion preserves current fixed identity and exact origin");
                Call(host, "RemoveProjection"); Require(current() == null && library.Snapshot.Page.Entries.Any(e => e.AssetId == second.AssetId), "Remove clears overlay without deleting immutable library asset");
                place(entry); pointer(true, false, false, 60, 60); pointer(true, true, false, 60, 60);
                place(second);
                object retiredOwner;
                lock (Get(projectionWorker, "gate")) retiredOwner = GetOptional(projectionWorker, "projectionOwner");
                Require(retiredOwner != null && GetOptional(retiredOwner, "Index") != null, "replacement really holds a pinned reader before rollback deletion");
                Require((bool)Call(host, "DeleteAsset", entry), "delete of rollback asset is accepted while another replacement is current");
                lock (Get(projectionWorker, "gate")) Require(GetOptional(projectionWorker, "projectionDesired") == null,
                    "deleting rollback asset retires the sibling candidate's read qualification as well");
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy && !(bool)Get(host, "deletePending"); });
                NativeQuickItemChecks.Until(() => GetOptional(retiredOwner, "Index") == null);
                for (int poll = 0; poll < 16; poll++) Call(host, "Poll");
                lock (Get(projectionWorker, "gate")) Require(GetOptional(projectionWorker, "projectionOwner") == null && GetOptional(projectionWorker, "projectionReading") == null && GetOptional(projectionWorker, "projectionOpening") == null,
                    "worker boundary disposes sibling index/cache and opening/window iterators");
                Require(current() == null && mode() == "None", "successful rollback-asset deletion cannot revive retired replacement or old fixed content");
                place(second); Call(host, "OnSessionEnded"); Call(host, "Poll");
                Require(current() == null && mode() == "None", "session exit retires overlay and late read qualification"); Call(host, "OnSessionStarted");
                Console.WriteLine("PASS Blueprint actual Host placement: center/even/sparse anchor, startup/new actions, takeover/move delta/rebase, cancel/replace restore, capture admission, complete boundary, Fixed native actions, registered move/remove and session retirement. Certified-native pointer/OS input remain separate.");
            }
            finally { NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "RemoveProjection"); state.Exit(); }
        }
    }
}
