using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintTrimAccumulationChecks
    {
        internal static void Run(object context)
        {
            WithScene(context, scene =>
            {
                var host = scene.Host; var target = scene.Target; var baseline = (BlueprintProjectionMask)Get(target, "Mask");
                long revision = (long)Get(host, "projectionSceneRevision"); object worker = Get(scene.Library, "worker");
                scene.Begin(); Require((string)GetOptional(host, "projectionFeedback") == "拖选修剪范围，选定后点确定应用修改", "target-selection feedback is the owner's exact first sentence");
                scene.Seal(0, 0); object prefix = GetOptional(host, "trimFlight");
                Require(prefix != null && !(bool)Get(host, "CanInvokeConfirmTrim") && (string)Get(host, "ConfirmTrimDisabledReason") == "正在核对修剪范围…", "accepted-but-unconsumed work is continuously checking in the actual Host state");
                scene.Seal(8, 0); scene.Pointer(true, 10, 0);
                Require(ReferenceEquals(GetOptional(host, "trimFlight"), prefix) && (long)Get(host, "trimSealedVersion") == 2 && (bool)Get(host, "TrimDragging"), "A in flight does not cancel or swallow sealed B or active C");
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return (long)Get(host, "trimVerifiedVersion") == 1; });
                Require((long)Get(host, "trimSealedVersion") == 2 && (bool)Get(host, "TrimDragging") && !(bool)Get(host, "CanInvokeConfirmTrim"), "new preview revisions and an active drag do not discard A's legal prefix or confirm only A");
                var manual = (BlueprintTrimGeometry)Get(host, "TrimManualGeometry");
                Require(manual.Contains(0, 0) && manual.Contains(8, 0) && !manual.Contains(5, 0) && !manual.Contains(10, 0), "only complete A/B are the exact union; gap and unfinished C are not selected");
                Main.playerInventory = true; NativeQuickItemChecks.Sample(scene.Input, Array.Empty<Keys>()); Call(host, "ProcessInput"); Main.playerInventory = false;
                Require(!(bool)Get(host, "TrimDragging") && manual.Contains(0, 0) && manual.Contains(8, 0), "actual native-UI suspension discards only unfinished C and retains the accepted prefix and pending suffix");
                scene.Ready(); Require(((BlueprintProjectionTrimPlan)Get(host, "TrimPlan")).RemovedLayerCount == 9, "A complete bed Tile plus B's distant Tile is nine, not their common bounding box");
                Require((bool)Get(host,"projectionNeedsNeutral") && !manual.Contains(3,1),"UI interruption retains the physical fresh-gesture gate; no later range has been selected yet");
                scene.Pointer(false,3,1);
                Require(!(bool)Get(host,"projectionNeedsNeutral"),"a trusted neutral sample ends UI suspension before a genuinely new drag");
                scene.Seal(3, 1); scene.Ready();
                Require(((BlueprintProjectionTrimPlan)Get(host, "TrimPlan")).RemovedLayerCount == 14, "later manual selection of the bed's expanded end still adds wall, two wire colors, liquid and actuator");
                long version = (long)Get(host, "trimSealedVersion"); long visits = (long)Get(worker, "projectionTrimRecordVisits");
                for (int n = 0; n < 4; n++) scene.Seal(3, 1);
                Require((long)Get(host, "trimSealedVersion") == version && (long)Get(worker, "projectionTrimRecordVisits") == visits && GetOptional(host, "trimFlight") == null, "duplicate full-layer selection creates no worker batch or historical source replay");
                scene.Seal(6, 1); scene.Ready(); scene.Seal(-3, -3);
                Require(((BlueprintProjectionTrimPlan)Get(host, "TrimPlan")).RemovedLayerCount == 14 && (bool)Get(host, "CanInvokeConfirmTrim"), "new empty content and wholly outside rectangles preserve a previous nonempty cumulative result");
                Require((string)GetOptional(host, "projectionFeedback") == "红色范围仅为预览；请点“确定修剪”，或使用已设置的确认键。", "ready feedback is the owner's exact second sentence");
                Require(ReferenceEquals(Get(target, "Mask"), baseline) && (long)Get(host, "projectionSceneRevision") == revision && (long)Get(worker, "projectionTrimMaskCopies") == 0, "all accumulation leaves permanent facts untouched and copies no historical mask");
                NativeQuickItemChecks.Sample(scene.Input, Array.Empty<Keys>()); Call(host, "ConfirmTrimProjection"); Call(host, "ProcessInput");
                Require((bool)Get(host, "trimFinalizing") && (string)Get(host, "ConfirmTrimDisabledReason") == "正在应用修剪…", "one confirmation has a separate finalizing responsibility and accurate disabled reason");
                scene.WaitFrozen(); Require(ReferenceEquals(Get(target, "Mask"), baseline) && (long)Get(host, "projectionSceneRevision") == revision, "Poll receives final immutable data without publishing ahead of the next input arbitration");
                Call(host, "CancelProjection"); scene.Step();
                Require(ReferenceEquals(Get(target, "Mask"), baseline) && (long)Get(host, "projectionSceneRevision") == revision && Scene(host).Contains(target), "cancel after Freeze receipt still wins before permanent publication");
                NativeQuickItemChecks.Until(() => GetOptional(worker, "projectionTrimOperation") == null && (long)Get(worker, "projectionTrimWorkingBytes") == 0);
                scene.Begin(); scene.Seal(0, 0); scene.Seal(8, 0); scene.Seal(3, 1); scene.Ready();
                long copies = (long)Get(worker, "projectionTrimMaskCopies");
                NativeQuickItemChecks.Sample(scene.Input, Array.Empty<Keys>()); Call(host, "ConfirmTrimProjection"); Call(host, "ProcessInput"); scene.WaitFrozen();
                Require((long)Get(worker, "projectionTrimMaskCopies") == copies + 1, "many completed selections compose baseline plus overlay exactly once at final Freeze");
                NativeQuickItemChecks.Sample(scene.Input, Array.Empty<Keys>()); Call(host, "ProcessInput");
                var final = (BlueprintProjectionMask)Get(target, "Mask");
                Require(final.RemovedLayerCount == 14 && !ReferenceEquals(final, baseline) && (long)Get(host, "projectionSceneRevision") == revision + 1 && !(bool)Get(host, "ProjectionEditing"), "one accepted confirmation publishes the entire verified union exactly once");
                var bedEnd = new CaptureCell { X = 3, Y = 1, ObjectId = 1, Layers = CaptureLayer.Tile | CaptureLayer.Wall | CaptureLayer.Liquid | CaptureLayer.Actuator | CaptureLayer.Wire, Wires = 5, Actuator = true };
                Require(!final.Apply(bedEnd).HasContent && final.Apply(new CaptureCell { X = 5, Y = 0, Layers = CaptureLayer.Tile }).HasContent && final.Apply(new CaptureCell { X = 10, Y = 0, Layers = CaptureLayer.Liquid }).HasContent,
                    "independently listed end layers vanish while the unselected gap Tile and interrupted C liquid survive");
                Require((int)Get(target, "OriginX") == 100 && (int)Get(target, "OriginY") == 40 && !(bool)Get(target, "MirroredHorizontally"), "trim retains origin, frame and pose");
                long order = (long)Get(target, "CommitOrder");
                NativeQuickItemChecks.Until(() => { scene.Step(); return scene.History.Saved; });
                string scope = (string)Get(scene.History, "requestedScope");
                var saved = BlueprintSceneCodec.Decode(File.ReadAllBytes(Path.Combine(scene.Root, "instances", scope, "scene.bin")), scope, 512, 160);
                Require(saved.Records.Single().Mask.RemovedLayerCount == 14 && saved.Records.Single().Order == order, "scene v2 saves only the final source-domain union and original order");
                scene.Begin(5, 0); scene.Seal(5, 0); scene.Ready();
                scene.Library.CloseProjection(); NativeQuickItemChecks.Until(() => GetOptional(worker, "projectionTrimOperation") == null && (long)Get(worker, "projectionTrimWorkingBytes") == 0);
                scene.Step(); Require(!(bool)Get(host,"ProjectionEditing"),"loss of the real worker reference ends an idle trim instead of retrying a permanently rejected busy request");
                Require(final.RemovedLayerCount == 14 && !final.Apply(bedEnd).HasContent && final.CellCount > 0 && final.ObjectCount == 1, "canceling a later baseline edit and closing the worker never clear a mask already handed to the scene");
                Console.WriteLine("PASS cumulative trim: A accepted/B sealed/C interrupted, prefix receipt despite new UI revision, exact disjoint union, empty/repeated/outside selection, Tile-only extension then manual other layers, exact text/wait reasons, zero per-step mask copies, one Freeze, Poll-before-input cancellation, final immutable ownership and scene save.");
            });
        }

        internal sealed class Scenario
        {
            internal object Context, Host, Input, Shell, Target;
            internal LibraryState Library;
            internal BlueprintSceneHistory History;
            internal string Root;
            internal Action Step;
            internal void Pointer(bool down, int x, int y) { Call(Host, "AcceptWorldPointer", true, down, false, 100 + x, 40 + y, Main.maxTilesX, Main.maxTilesY); }
            internal void Begin(int x = 0, int y = 0)
            {
                NativeQuickItemChecks.Until(() => { Call(Host, "UpdateProjectionWindow", new WorldTargetView(100, 40, 16, 8, 0)); Step(); return GetOptional(Target, "Window") != null; });
                NativeQuickItemChecks.Sample(Input, Array.Empty<Keys>()); Call(Host, "TrimProjection"); Pointer(false, x, y); Pointer(true, x, y);
                NativeQuickItemChecks.Until(() => { Step(); return Get(Host, "ProjectionMode").ToString() == "TrimReady"; }); Pointer(false, x, y);
            }
            internal void Seal(int x, int y) { Pointer(true, x, y); Pointer(false, x, y); }
            internal void Ready() { NativeQuickItemChecks.Until(() => { Step(); return (bool)Get(Host, "CanInvokeConfirmTrim"); }); }
            internal void WaitFrozen() { NativeQuickItemChecks.Until(() => { Call(Host, "Poll"); return GetOptional(Host, "trimFrozenPlan") != null; }); }
        }
        internal static void WithScene(object context, Action<Scenario> check)
        {
            var host = Get(context, "Blueprint"); var input = Get(context, "Input"); var shell = Get(context, "Shell");
            var oldLibrary = (LibraryState)Get(host, "Library"); var oldHistory = (BlueprintSceneHistory)Get(host, "projectionHistory");
            var oldWorld = Main.ActiveWorldFileData; bool oldMenu = Main.gameMenu, oldInventory = Main.playerInventory, oldAuto = (bool)Get(host, "AutoBuildEnabled");
            string root = Path.Combine(Terraria.Program.SavePath, "blueprint-trim-accumulation-" + Guid.NewGuid().ToString("N"));
            var library = new LibraryState(new FileBlueprintLibrary(Path.Combine(root, "templates")));
            var history = new BlueprintSceneHistory(scope => new AtomicFileDocument(Path.Combine(root, "instances", scope, "scene.bin"), int.MaxValue, true, ".schema1-original"));
            Action step = () => { NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime"); Call(host, "Poll"); };
            Main.gameMenu = true; Call(context, "UpdateRuntime"); var finite = NativeBlueprintWorldMatchChecks.EmptyWorld(host, 512, 160);
            try
            {
                Set(host, "<Library>k__BackingField", library); Set(host, "projectionHistory", history);
                Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(root, "world.wld"), false) { UniqueId = Guid.NewGuid() }; Main.gameMenu = false; Main.playerInventory = false;
                NativeQuickItemChecks.Until(() => { step(); return (bool)Get(host, "ProjectionPersistenceReady"); });
                NativeBlueprintWorldMatchChecks.InitializeMetadata(); Framing.Initialize(); Main.SetupTileMerge(); Call(host, "SetAutoBuild", false); Call(shell, "CloseAndSubmitPosition");
                Require(TileObject.Place(new TileObject { xCoord = 20, yCoord = 20, type = 79, style = 0, alternate = 0, random = 0 }), "native bed writer seeds the independent complete Tile fixture");
                var end = Main.tile[23, 21]; end.wall = 7; end.wire(true); end.wire3(true); end.actuator(true); end.liquid = 80;
                foreach (int x in new[] { 25, 28 }) { Main.tile[x, 20].active(true); Main.tile[x, 20].type = 1; }
                Main.tile[30, 20].liquid = 80;
                var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, library, step, 20, 20, 11, 2, "累计修剪有限代表");
                object target = NativeBlueprintWorldMatchChecks.Place(host, input, entry, 100, 40, step);
                check(new Scenario { Context = context, Host = host, Input = input, Shell = shell, Target = target, Library = library, History = history, Root = root, Step = step });
            }
            finally
            {
                Call(host, "CancelProjection"); Call(host, "SetAutoBuild", oldAuto); Main.gameMenu = true; Call(context, "UpdateRuntime"); history.Stop(3000); library.Stop(3000);
                Set(host, "<Library>k__BackingField", oldLibrary); Set(host, "projectionHistory", oldHistory); finite.Dispose(); Main.ActiveWorldFileData = oldWorld; Main.gameMenu = oldMenu; Main.playerInventory = oldInventory;
                if (!oldMenu) step();
            }
        }
        internal static object[] Scene(object host) { return ((IEnumerable)Get(host, "ProjectionScene")).Cast<object>().ToArray(); }
        internal static void Set(object owner, string name, object value)
        {
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            var field=owner.GetType().GetField(name,flags);
            if(field!=null){field.SetValue(owner,value);return;}
            var property=owner.GetType().GetProperty(name,flags);
            if(property?.GetSetMethod(true)!=null){property.SetValue(owner,value,null);return;}
            throw new MissingMemberException(owner.GetType().FullName,name);
        }
    }
}
