using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private sealed class SettledPaintAdmission
        {
            internal object Host, Tools;
            internal bool Written, DynamicLogged;
            internal int LiveReads;
            internal Item Short, Long;
            internal readonly List<int> Applied = new List<int>();
        }
        private static SettledPaintAdmission settledPaintAdmission;
        private static void SettledPaintDynamicWait(object __instance, object __0)
        {
            var state = settledPaintAdmission;
            if (state == null || !state.Written || state.DynamicLogged || (int)Get(__0, "WorldX") != 49) return;
            state.DynamicLogged = true;
            var preparation = Get(__instance, "buildMaterialPreparation");
            Console.WriteLine("SettledPaintAdmission first-dynamic-A world=" + Main.GameUpdateCount + " phase=" + Get(preparation, "phase") +
                " startAllowed=" + Call(preparation, "StartAllowed", Main.LocalPlayer) + " active/token=" +
                Get(Get(state.Tools, "Use"), "Active") + "/" + Get(Get(state.Tools, "Use"), "Operation") + " timers=" +
                Main.LocalPlayer.itemTime + "/" + Main.LocalPlayer.itemAnimation + " beforeReadiness=" + Get(__0, "Readiness") +
                " stack=" + Environment.StackTrace);
        }
        private static void SettledPaintAbsence(object __instance, bool __1, bool __result)
        {
            var state = settledPaintAdmission;
            if (state == null || !state.Written || !__1 || state.LiveReads++ >= 4) return;
            Console.WriteLine("SettledPaintAdmission liveAbsence result=" + __result + " world=" + Main.GameUpdateCount +
                " capabilityDependencies=" + ((System.Collections.IDictionary)Get(__instance, "nearProofCapabilities")).Count);
            var supply = GetOptional(__instance, "buildSupplyTarget");
            Console.WriteLine("SettledPaintAdmission supplyTarget=" + (supply == null ? "none" : Get(supply, "WorldX") + ":" + Get(supply, "Readiness")));
            foreach (var range in ((System.Collections.IDictionary)Get(__instance, "nearBuildRanges")).Values.Cast<object>())
                foreach (var fact in ((System.Collections.IDictionary)Get(range, "Facts")).Values.Cast<object>())
                    if ((int)Get(Get(Get(fact, "Seed"), "Cell"), "X") == 5)
                    {
                        var op = GetOptional(fact, "Operation");
                        Console.WriteLine("SettledPaintAdmission liveFar wait=" + Get(fact, "Wait") + " ready/pending=" + Get(fact, "Ready") + "/" + Get(fact, "Pending") +
                            " type/supply=" + Get(fact, "Type") + "/" + Get(fact, "Supply") + " operation=" +
                            (op == null ? "null" : Get(op, "Kind") + ":" + Get(op, "Mode")) + " continues=" +
                            __instance.GetType().GetMethod("NearFactContinues", F).Invoke(__instance, new[] { fact }));
                    }
        }
        private static void SettledPaintBeforeRefresh(object __instance)
        {
            var state = settledPaintAdmission;
            if (state == null || state.Written || !ReferenceEquals(__instance, state.Host)) return;
            var current = GetOptional(__instance, "activeBuild"); var next = GetOptional(__instance, "preparedBuild");
            if (current == null || next == null || !(bool)Get(current, "Checked") ||
                !(bool)Get(current, "EmptySupplySettled") || (int)Get(Get(next, "Operation"), "SupplyType") != ItemID.BluePaint) return;
            Require(state.Applied.SequenceEqual(new[] { 44 }) && (long)Get(current, "Token") > 0 && (long)Get(next, "Token") == 0 &&
                ReferenceEquals(Get(Get(current, "Source"), "Item"), Get(Get(next, "Source"), "Item")) &&
                !(bool)Get(Get(__instance, "buildMaterialPreparation"), "HasWork"),
                "real empty red receipt has settled and same-brush B is prepared but has not been admitted");
            var p = Main.LocalPlayer; long hostTick = checked((long)(ulong)Get(__instance, "projectionMatchTick"));
            long clock = (long)Get(__instance, "nearCapabilityClock");
            var supply = new Item(); supply.SetDefaults(ItemID.SpectrePaintbrush);
            Require(p.GetItem(supply, new GetItemSettings(NoText: true, NoSound: true)).IsAir,
                "original GetItem accepts the genuinely longer matching brush at the new same-token boundary");
            state.Long = p.inventory.Take(50).Single(item => item.type == ItemID.SpectrePaintbrush);
            state.Written = true;
            Console.WriteLine("SettledPaintAdmission proof currentGroup=" + __instance.GetType().GetMethod("BuildContinuationCurrent", F).Invoke(__instance, new object[] { p }) +
                " nextContinues=" + __instance.GetType().GetMethod("FlightContinuesBuild", F).Invoke(__instance, new[] { next }) +
                " capDependencies=" + ((System.Collections.IDictionary)Get(__instance, "nearProofCapabilities")).Count +
                " longEligible=" + Call(state.Tools, "BuildInventoryEligible", Array.IndexOf(p.inventory, state.Long), state.Long) +
                " absenceReady=" + Get(__instance, "nearAbsenceReady"));
            foreach (var rangeState in ((System.Collections.IDictionary)Get(__instance, "nearBuildRanges")).Values.Cast<object>())
                foreach (var fact in ((System.Collections.IDictionary)Get(rangeState, "Facts")).Values.Cast<object>())
                    if ((int)Get(Get(Get(fact, "Seed"), "Cell"), "X") == 5)
                        Console.WriteLine("SettledPaintAdmission farFact wait=" + Get(fact, "Wait") + " pending/ready=" + Get(fact, "Pending") + "/" + Get(fact, "Ready") +
                            " capability=" + (GetOptional(fact, "Capability") == null ? "null" : Get(Get(fact, "Capability"), "Identity")));
            Require(!p.IsInTileInteractionRange(49, 42, TileReachCheckSettings.Simple, state.Short.tileBoost + p.blockRange) &&
                p.IsInTileInteractionRange(49, 42, TileReachCheckSettings.Simple, state.Long.tileBoost + p.blockRange) &&
                checked((long)(ulong)Get(__instance, "projectionMatchTick")) == hostTick &&
                (long)Get(__instance, "nearCapabilityClock") == clock,
                "native acquisition restores A's actual reach without moving or publishing another Host capability snapshot");
            Console.WriteLine("SettledPaintAdmission GetItem world=" + Main.GameUpdateCount + " host=" + hostTick +
                " capabilityClock=" + clock + " liveA=true oldToken=" + Get(current, "Token") + " preparedBToken=0");
        }
        private static void SettledPaintBeforeConsumer(Player __instance, int __0, int __1, Item __4, out int[] __state)
        {
            __state = null;
            if (settledPaintAdmission == null || __1 != 42 || (__0 != 44 && __0 != 49 && __0 != 46)) return;
            __state = new[] { (int)Main.tile[__0, __1].color(), __4.stack };
        }
        private static void SettledPaintAfterConsumer(Player __instance, int __0, int __1, Item __4, int[] __state, bool __runOriginal)
        {
            var state = settledPaintAdmission;
            if (state == null || __state == null || !__runOriginal || Main.tile[__0, __1].color() == __state[0]) return;
            Require(__state[1] - (__4.IsAir ? 0 : __4.stack) == 1, "observed native paint changes the world and independently consumes exactly one real paint");
            state.Applied.Add(__0);
            Console.WriteLine("SettledPaintAdmission consumer world=" + Main.GameUpdateCount + " x=" + __0 +
                " held=" + __instance.HeldItem.type + " token=" + Get(Get(state.Tools, "Use"), "Operation"));
        }
        private static void ConstructionSettledPaintAdmission(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
            p.inventory[14].SetDefaults(ItemID.Paintbrush); var shortTool = p.inventory[14];
            // An unrelated long roller fixes the broad discovery envelope;
            // only the later matching long brush restores foreground A.
            p.inventory[15].SetDefaults(ItemID.SpectrePaintRoller);
            p.inventory[20].SetDefaults(ItemID.RedPaint); p.inventory[20].stack = 1;
            p.inventory[30].SetDefaults(ItemID.RedPaint); p.inventory[30].stack = 9999; var red = p.inventory[30];
            p.inventory[12].SetDefaults(ItemID.BluePaint); p.inventory[12].stack = 9999; var blue = p.inventory[12];
            foreach (int x in new[] { 44, 49, 46 }) NativeToolsChecks.Tile(x, 42, TileID.Stone);
            var entry = Asset(host,
                new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = red.paint },
                new CaptureCell { X = 5, Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = red.paint },
                new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = blue.paint });
            Place(context, host, input, entry, 44, 42);
            var patch = new HarmonyLib.Harmony("JueMingR.Tests.SettledPaintLiveAdmission");
            var self = typeof(NativeBlueprintBuildChecks);
            settledPaintAdmission = new SettledPaintAdmission { Host = host, Tools = tools, Short = shortTool };
            patch.Patch(host.GetType().GetMethod("RefreshAutomaticBuild", F), prefix: new HarmonyLib.HarmonyMethod(self, nameof(SettledPaintBeforeRefresh)));
            patch.Patch(host.GetType().GetMethod("NearBuildAbsenceCurrent", F), postfix: new HarmonyLib.HarmonyMethod(self, nameof(SettledPaintAbsence)));
            patch.Patch(host.GetType().GetMethod("WaitForDynamicBuild", F), prefix: new HarmonyLib.HarmonyMethod(self, nameof(SettledPaintDynamicWait)));
            patch.Patch(typeof(Player).GetMethod("ApplyPaint", F), prefix: new HarmonyLib.HarmonyMethod(self, nameof(SettledPaintBeforeConsumer)),
                postfix: new HarmonyLib.HarmonyMethod(self, nameof(SettledPaintAfterConsumer)));
            try
            {
                SetAutomatic(host, input, true);
                Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active") &&
                    !(bool)Get(Get(host, "buildMaterialPreparation"), "HasWork"));
                Console.WriteLine("SettledPaintAdmission order=" + string.Join(",", settledPaintAdmission.Applied));
                Require(settledPaintAdmission.Written && settledPaintAdmission.Applied.SequenceEqual(new[] { 44, 49, 46 }) &&
                    red.stack == 9998 && blue.stack == 9998 && p.inventory[20].IsAir && Main.mouseItem.IsAir,
                    "a matching long tool restored immediately before same-token color admission makes real A consume before B, with exact inventory conservation");
            }
            finally
            {
                settledPaintAdmission = null;
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyLib.HarmonyPatchType.All, patch.Id);
            }
        }
    }
}
