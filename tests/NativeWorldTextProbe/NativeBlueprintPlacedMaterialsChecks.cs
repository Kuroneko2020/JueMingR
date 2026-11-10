using System;
using System.Linq;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintWorldMatchChecks
    {
        // This task slice uses the original world writers and production
        // material catalog/remaining pipeline. It does not execute the unrelated
        // entire declaration catalogue preflight or manufacture material totals.
        internal static void PlacedMaterialTargets(object context)
        {
            var host = Get(context, "Blueprint"); var input = Get(context, "Input");
            var original = (LibraryState)Get(host, "Library"); var field = host.GetType().GetField("<Library>k__BackingField", Flags);
            Call(Get(context, "Shell"), "CloseAndSubmitPosition"); Call(host, "CancelCaptureOrDraft"); Call(host, "ClearProjection");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !original.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
            ((BlueprintCaptureState)Get(host, "State")).Exit();
            var own = new LibraryState(new FileBlueprintLibrary(System.IO.Path.Combine(Terraria.Program.SavePath, "placed-material-targets")));
            field.SetValue(host, own); var inventory = Main.LocalPlayer.inventory; bool auto = (bool)Get(host, "AutoBuildEnabled");
            try
            {
                using (EmptyWorld(host, 1024, 160))
                {
                    InitializeMetadata(); Framing.Initialize(); Main.SetupTileMerge();
                    Main.LocalPlayer.inventory = new Item[inventory.Length]; for (int i = 0; i < inventory.Length; i++) Main.LocalPlayer.inventory[i] = new Item();
                    Main.LocalPlayer.inventory[0].SetDefaults(ItemID.StoneBlock); Main.LocalPlayer.inventory[0].stack = 80;
                    Call(host, "SetAutoBuild", false); Call(host, "SetMaterialViewDemand", true);
                    // The shared metadata producer is admitted by actual
                    // mapping opportunities, including this cold empty scene.
                    Action step = () => { NativeQuickItemChecks.BeginWorldStep(); NativeQuickItemChecks.Sample(input,Array.Empty<Microsoft.Xna.Framework.Input.Keys>()); Call(context, "UpdateRuntime"); };
                    NativeQuickItemChecks.Until(() => { step(); return GetOptional(host, "MaterialCatalog") != null; });
                    var entry = Small(own, Enumerable.Range(0, 50).Select(x => new CaptureCell { X = x, Layers = CaptureLayer.Tile, TileType = TileID.Stone }).ToArray());
                    var first = Place(host, input, entry, 40, 70, step); var second = Place(host, input, entry, 400, 70, step);
                    var firstIdentity = (BlueprintProjectionIdentity)Get(first, "Identity");
                    long firstId = firstIdentity.ProjectionGeneration, secondId = ((BlueprintProjectionIdentity)Get(second, "Identity")).ProjectionGeneration;
                    Require(MaterialAwait(host, "MaterialWorld", firstId, step).Total == 50 && MaterialAwait(host, "MaterialWorld", secondId, step).Total == 50, "same template two production remaining scopes start separately at fifty");
                    Require(WorldGen.PlaceTile(40, 70, TileID.Stone, true, true), "original world establishes first-copy-only progress");
                    NativeQuickItemChecks.Until(() => { step(); var one = (BlueprintMaterialSnapshot)Call(host, "MaterialWorld", firstId); return one.Status == BlueprintMaterialStatus.Exact && one.Total == 49; });
                    Require(MaterialAwait(host, "MaterialWorld", secondId, step).Total == 50, "other copy remains fifty after real first-copy progress");
                    NativeBlueprintPlacedListChecks.AssertPinned(context, firstId, secondId);
                    NativeQuickItemChecks.Sample(input, Array.Empty<Microsoft.Xna.Framework.Input.Keys>()); NativeQuickItemChecks.Sample(input, Array.Empty<Microsoft.Xna.Framework.Input.Keys>());
                    Require((bool)Call(host, "TryRemovePlacedInstance", firstIdentity.SessionGeneration, firstId), "precise first-copy Host removal admitted");
                    Require(Scene(host).Contains(second) && !Scene(host).Contains(first), "exact list removal preserves same-template sibling");
                    Require(MaterialAwait(host, "MaterialWorld", secondId, step).Total == 50, "surviving exact material owner remains current");
                    NativeBlueprintPlacedListChecks.RemoveAndRetire(context, secondId);
                    Console.WriteLine("PASS placed list exact production materials: two same-template copies 50/50, original placement 49/50, pinned UI targets and exact per-instance retirement without world demolition.");
                }
            }
            finally
            {
                Call(Get(context, "Shell"), "CloseAndSubmitPosition"); Call(host, "ClearProjection"); Call(host, "SetMaterialViewDemand", false);
                Main.LocalPlayer.inventory = inventory; Call(host, "SetAutoBuild", auto); field.SetValue(host, original); own.Dispose();
            }
        }
    }
}
