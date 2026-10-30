using System;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static int placementIndependentReady, placementNativeContinuityRefusals;
        private static void PlacementRefreshBefore()
        {
            if (!smartPlatformWatch) return;
            var current = GetOptional(smartPlatformHost, "activeBuild"); var next = GetOptional(smartPlatformHost, "preparedBuild");
            if (current == null || next == null || !(bool)Get(current, "Checked") ||
                Get(current, "CursorMode").ToString() != "SmartPlatform" || Get(next, "CursorMode").ToString() != "SmartPlatform" ||
                (bool)Call(smartPlatformHost, "SameSmartPlatformSegment", current, next)) return;
            var a = Get(current, "Source"); var b = Get(next, "Source");
            if (!ReferenceEquals(Get(a, "Item"), Get(b, "Item"))) return;
            placementIndependentReady++;
            var target = Get(next, "Target");
            // A read-only original predicate observes the live old lock. The
            // production path must clear it through neutral input, not a test
            // write to either private continuity field.
            if (!(bool)typeof(Terraria.GameContent.SmartCursorHelper).GetMethod("AllowedForContinuity", F)
                .Invoke(null, new object[] { Get(target, "WorldX"), Get(target, "WorldY"), 2 })) placementNativeContinuityRefusals++;
        }
        private static void ConstructionSmartPlacementRepair(object context, object host, object tools, object input)
        {
            foreach (int slope in new[] { 1, 2 })
            {
                int side = slope == 1 ? 1 : -1;
                foreach (string layout in new[] { "uphill", "two-end-gap", "stone-above" })
                {
                    var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                    p.inventory[12].SetDefaults(ItemID.WoodPlatform); p.inventory[12].stack = 4; var material = p.inventory[12];
                    SeedSmartPlatform(material, 44 + side, 40, slope);
                    if (layout == "two-end-gap") SeedSmartPlatform(material, 44 - side, 38, slope);
                    if (layout == "stone-above") Require(WorldGen.PlaceTile(44, 38, TileID.Stone, true, true), "ordinary Stone above is real and legal");
                    Place(context, host, input, Asset(host, SmartPlatformTarget(material, 0, 0, slope)), 44, 39);
                    ResetSmartPlatformCounts(); smartPlatformWatch = true; SetAutomatic(host, input, true);
                    Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active")); smartPlatformWatch = false;
                    Require(Main.tile[44, 39].active() && Main.tile[44, 39].slope() == slope && material.stack == 3 &&
                        smartPlatformChoices == 1 && smartPlatformPreciseChoices == 0 && smartPlatformHammers == 0 && smartPlatformProofValid &&
                        Main.tile[44 + side, 40].slope() == slope && (long)Get(host, "BuildUnknownResults") == smartPlatformUnknownBaseline,
                        "two-dimensional natural Lookup/PlaceIt closes " + layout + " slope=" + slope + " without hammer or endpoint shape changes");
                    string expectedDirection = layout == "two-end-gap" ? slope == 1 ? "1,1" : "1,-1" : (-side) + ",-1";
                    Require(smartPlatformDirections["44,39"] == expectedDirection,
                        "the independent endpoint layout fixes native motion DX/DY, rather than asking the production proof for its own answer");
                    Console.WriteLine("SMART_PLACEMENT layout=" + layout + " slope=" + slope + " nativeChoices=1 consumed=1 hammer=0 completeMatch=true");
                }
            }
            {
                var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                p.inventory[12].SetDefaults(ItemID.WoodPlatform); p.inventory[12].stack = 5; var material = p.inventory[12];
                SeedSmartPlatform(material, 43, 38, 0);
                var cells = new[] { SmartPlatformTarget(material, 0, 0), SmartPlatformTarget(material, 1, 0, 1), SmartPlatformTarget(material, 2, 1, 1) };
                Place(context, host, input, Asset(host, cells), 43, 38);
                ResetSmartPlatformCounts(); smartPlatformWatch = true; SetAutomatic(host, input, true);
                Until(context, input, () => Main.tile[44, 38].active());
                Require(Main.tile[44, 38].slope() == 0 && !Main.tile[45, 39].active() && Main.tile[43, 38].slope() == 0 && material.stack == 4 &&
                    smartPlatformDirections["44,38"] == "1,0" && (long)Get(host, "BuildUnknownResults") == smartPlatformUnknownBaseline,
                    "the first real horizontal Smart action legitimately leaves the requested future slope temporarily flat");
                Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active")); smartPlatformWatch = false;
                Require(Main.tile[44, 38].slope() == 1 && Main.tile[45, 39].slope() == 1 && Main.tile[43, 38].slope() == 0 && material.stack == 3 &&
                    smartPlatformDirections["45,39"] == "1,1" && smartPlatformChoices == 2 && smartPlatformPreciseChoices == 0 &&
                    smartPlatformHammers == 0 && smartPlatformProofValid && (long)Get(host, "BuildUnknownResults") == smartPlatformUnknownBaseline,
                    "the next natural diagonal action changes its center and authorized former flat target, preserving the outer flat seed and complete Match");
                Console.WriteLine("SMART_PLACEMENT stagedFlat first=(44,38):0 direction=1,0 writeSet=(44,38); secondDirection=1,1 writeSet=(45,39):1+(44,38):1 outer(43,38):0 consumed=2 hammer=0 completeMatch=true");
            }
            foreach (string permission in new[] { "selected", "selected-pair", "outside", "masked", "correct-flat" })
            {
                var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                p.inventory[12].SetDefaults(ItemID.WoodPlatform); p.inventory[12].stack = 4; var material = p.inventory[12];
                SeedSmartPlatform(material, 43, 38, 0);
                if (permission == "selected-pair") SeedSmartPlatform(material, 45, 40, 0);
                bool selected = permission == "selected" || permission == "selected-pair";
                var cells = permission == "outside" ? new[] { SmartPlatformTarget(material, 0, 0, 1) } :
                    new[] { SmartPlatformTarget(material, 0, 0, selected || permission == "masked" ? 1 : 0), SmartPlatformTarget(material, 1, 1, 1) };
                if (permission == "selected-pair") cells = cells.Concat(new[] { SmartPlatformTarget(material, 2, 2, 1) }).ToArray();
                // The one-cell negative uses its own exact local origin;
                // extra empty leading rows/columns are not part of its target.
                var placed = Place(context, host, input, Asset(host, cells), permission == "outside" ? 44 : 43, permission == "outside" ? 39 : 38);
                if (!selected) WorldGen.PlaceWall(44, 39, WallID.Stone, true);
                if (permission == "masked")
                {
                    Place(context, host, input, Asset(host, SmartPlatformTarget(material, 0, 0)), 43, 38);
                    Require((((BlueprintProjectionMask)Get(placed, "Mask")).Apply(cells[0]).Layers & CaptureLayer.Tile) == 0,
                        "a real overlapping projection commit permanently masks the former neighbor shape authorization");
                }
                ResetSmartPlatformCounts(); smartPlatformWatch = true; SetAutomatic(host, input, true);
                Until(context, input, () => Main.tile[44, 39].active());
                if (selected) Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
                else for (int n = 0; n < 32; n++) Step(context, input);
                smartPlatformWatch = false;
                Require(material.stack == 3 && (long)Get(host, "BuildUnknownResults") == smartPlatformUnknownBaseline && smartPlatformHammers == 0,
                    "native neighbor authorization never duplicates material or hides a result");
                Require(selected ? Main.tile[43, 38].slope() == 1 && Main.tile[44, 39].slope() == 1 && smartPlatformChoices == 1 && Scene(host).Length == 0 &&
                    (permission != "selected-pair" || Main.tile[45, 40].slope() == 1) :
                    Main.tile[43, 38].slope() == 0 && smartPlatformChoices == 0 && smartPlatformPreciseChoices == 1 && Scene(host).Length == 1,
                    "actual SpinSmartPlatform write set respects selected target versus " + permission);
                Console.WriteLine("SMART_PLACEMENT neighbor=" + permission + " nativeSmart=" + smartPlatformChoices + " consumed=1 unknown=0");
            }
            {
                var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                p.inventory[12].SetDefaults(ItemID.WoodPlatform); p.inventory[12].stack = 4; var material = p.inventory[12];
                SeedSmartPlatform(material, 43, 38, 1);
                Place(context, host, input, Asset(host, SmartPlatformTarget(material, 0, 0, 1)), 44, 39);
                ResetSmartPlatformCounts(); smartPlatformMissingLookups.Add("44,39", 0); smartPlatformWatch = true;
                SetAutomatic(host, input, true);
                Until(context, input, () => smartPlatformMissingLookups["44,39"] == 1);
                var failed = smartPlatformRejectedFlight; var target = Get(failed, "Target");
                Require((bool)Get(failed, "Rejected") && material.stack == 4 && !Main.tile[44, 39].active() &&
                    (bool)Call(host, "SmartPlatformFallbackCurrent", target, material),
                    "one real rejected held lookup freezes this exact recipe before any neutral recovery");
                material.stack++;
                WorldGen.PlaceWall(70, 40, WallID.Stone, true);
                Require((bool)Call(host, "SmartPlatformFallbackCurrent", target, material),
                    "stock change and unrelated native dirty write do not reopen a stable failure");
                long before = (long)Get(host, "smartPlatformNeutralEpoch");
                SetAutomatic(host, input, false);
                Until(context, input, () => !(bool)Get(Get(tools, "Use"), "Active") && (long)Get(host, "smartPlatformNeutralEpoch") > before);
                Require(!(bool)Call(host, "SmartPlatformFallbackCurrent", target, material),
                    "the only failed target reopens after actual neutral Lookup even with no other Smart flight");
                smartPlatformMissingLookups.Clear();
                SetAutomatic(host, input, true);
                Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active")); smartPlatformWatch = false;
                Require(Main.tile[44, 39].slope() == 1 && material.stack == 4 && smartPlatformChoices == 1 &&
                    smartPlatformPreciseChoices == 0 && smartPlatformHammers == 0 && smartPlatformProofValid && (long)Get(host, "BuildUnknownResults") == smartPlatformUnknownBaseline,
                    "one bounded restored attempt uses real Lookup/PlaceIt, not another target, token reset or hammer");
                Console.WriteLine("SMART_PLACEMENT singleTarget initialRejected=1 stockAndUnrelatedWriteFrozen=true actualNeutralRecovered=true nativeChoices=1 consumed=1 hammer=0");
            }
            var patch = new Harmony("JueMingR.Tests.SmartPlacementSegments");
            patch.Patch(host.GetType().GetMethod("RefreshAutomaticBuild", F), prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(PlacementRefreshBefore)));
            try
            {
                var p = Reset(context, host, tools, input); p.position.X = 44 * 16;
                p.inventory[12].SetDefaults(ItemID.WoodPlatform); p.inventory[12].stack = 7; var material = p.inventory[12];
                SeedSmartPlatform(material, 43, 37, 1); SeedSmartPlatform(material, 47, 37, 1);
                var cells = new[] { SmartPlatformTarget(material, 0, 0, 1), SmartPlatformTarget(material, 4, 0, 1),
                    SmartPlatformTarget(material, 1, 1, 1), SmartPlatformTarget(material, 5, 1, 1) };
                Place(context, host, input, Asset(host, cells), 44, 38);
                placementIndependentReady = placementNativeContinuityRefusals = 0;
                long neutral = (long)Get(host, "smartPlatformNeutralEpoch");
                ResetSmartPlatformCounts(); smartPlatformWatch = true; SetAutomatic(host, input, true);
                Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active")); smartPlatformWatch = false;
                Require(cells.All(c => Main.tile[44 + c.X, 38 + c.Y].slope() == 1) && material.stack == 3 &&
                    smartPlatformChoices == 4 && smartPlatformPreciseChoices == 0 && smartPlatformHammers == 0 && smartPlatformProofValid &&
                    placementIndependentReady > 0 && placementNativeContinuityRefusals > 0 && (long)Get(host, "smartPlatformNeutralEpoch") > neutral + 1,
                    "accurate Ready same-source independent stairs see the real old-lock refusal, then original neutral Lookup and four exact direct slopes");
                Console.WriteLine("SMART_PLACEMENT independentReady=" + placementIndependentReady + " originalOldLockRefusals=" + placementNativeContinuityRefusals +
                    " observedNeutral=" + ((long)Get(host, "smartPlatformNeutralEpoch") - neutral) + " nativeChoices=4 consumed=4 hammer=0 completeMatch=true");
            }
            finally
            {
                smartPlatformWatch = false;
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
            }
        }
    }
}
