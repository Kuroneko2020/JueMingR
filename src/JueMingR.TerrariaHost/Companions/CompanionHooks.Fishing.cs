using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingR.TerrariaHost.Fishing;
using JueMingR.TerrariaHost.Items;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal static partial class CompanionHooks
    {
        private static readonly Harmony fishingHarmony = new Harmony("JueMingR.Companions.Fishing");
        internal static Exception FishingUnavailable { get; private set; }
        internal static ItemNearbyStorage.NativeChestStack FishingChestStack { get; private set; }

        private static void InstallFishing()
        {
            try
            {
                FishingChestStack = new ItemNearbyStorage.NativeChestStack();
                PatchFishing(typeof(Player), "Fishing_GetBait", transpiler: nameof(FishingBaitReads));
                PatchFishing(typeof(Player), "ItemCheck_CheckFishingBobber_ConsumeBait", nameof(FishingBaitDebit));
                PatchFishing(typeof(Projectile), nameof(Projectile.FishingCheck), nameof(FishingCheckBefore));
                PatchFishing(typeof(Projectile), "FishingCheck_ProbeForQuestFish", transpiler: nameof(FishingQuestRead));
                PatchFishing(typeof(PopupText), nameof(PopupText.AssignAsSonarText), nameof(HumanSonarCursor));
                PatchFishing(typeof(PopupText), nameof(PopupText.ClearSonarText), nameof(HumanSonarCursor));
                PatchFishing(typeof(Player), nameof(Player.GetItem), nameof(FishingProductBefore),
                    postfix: nameof(FishingProductAfter), arguments: new[] { typeof(Item), typeof(GetItemSettings) });
                PatchFishing(typeof(Player), nameof(Player.ItemCheck), finalizer: nameof(FishingItemCheckFinal));
                FishingHooks.InstallForCompanions();
                ItemPendingGuards.FishingStorageHold = CompanionFishingStorageHold;
                FishingUnavailable = null;
            }
            catch (Exception error)
            {
                UninstallFishing();
                FishingUnavailable = error;
            }
        }

        private static void UninstallFishing()
        {
            fishingHarmony.UnpatchAll(fishingHarmony.Id);
            FishingHooks.UninstallForCompanions();
            ItemPendingGuards.FishingStorageHold = null;
            FishingChestStack = null;
        }

        private static void PatchFishing(Type type, string name, string prefix = null, string postfix = null, string finalizer = null, string transpiler = null,
            Type[] arguments = null)
        {
            var method = AccessTools.DeclaredMethod(type, name, arguments);
            if (method == null) throw new MissingMethodException(type.FullName, name);
            fishingHarmony.Patch(method, prefix: Hook(prefix), postfix: Hook(postfix), finalizer: Hook(finalizer), transpiler: Hook(transpiler));
        }

        private static void FishingProductBefore(Player __instance, Item __0, out HostCompanions.FishingGrant __state) =>
            __state = FishingHooks.BeforeNativeProduct(__instance, __0);

        private static void FishingProductAfter(HostCompanions.FishingGrant __state, bool __runOriginal)
        { if (__runOriginal && __state != null) __state.Produced = true; }

        private static Exception FishingItemCheckFinal(Player __instance, Exception __exception)
        {
            if (__exception == null && host?.Owned(__instance) is HostCompanions.Actor a) host.FinishFishingItemCheck(a);
            return __exception;
        }

        // Native SonarAlert popups still show at the AI bobber and expire.
        // Vanilla has just one immediate-clear cursor shared by local casts.
        private static bool HumanSonarCursor() => CurrentActor == null;

        private static bool CompanionFishingStorageHold(Player p, int slot)
        {
            if (slot < 0 || slot >= 50 || !p.inventoryChestStack[slot]) return false;
            var a = host?.Owned(p);
            return a != null && HostCompanions.FishingSourceHeld(a, slot);
        }

        internal static HostCompanions FishingHost => host;

        private static IEnumerable<CodeInstruction> FishingBaitReads(IEnumerable<CodeInstruction> input)
        {
            var bait = AccessTools.Field(typeof(Item), nameof(Item.bait));
            int reads = 0;
            foreach (var instruction in input)
            {
                if (instruction.opcode != OpCodes.Ldfld || !Equals(instruction.operand, bait))
                {
                    yield return instruction;
                    continue;
                }
                // Preserve the two native passes, including their ordering
                // and chosen stack. Only this actor's Truffle Worm is invisible.
                var player = new CodeInstruction(OpCodes.Ldarg_0);
                player.labels.AddRange(instruction.labels);
                player.blocks.AddRange(instruction.blocks);
                yield return player;
                yield return CodeInstruction.Call(typeof(CompanionHooks), nameof(FishingBaitPower));
                reads++;
            }
            if (reads != 4) throw new InvalidOperationException("Unexpected native fishing bait reads.");
        }

        private static int FishingBaitPower(Item item, Player p)
        {
            if (item.type != ItemID.TruffleWorm) return item.bait;
            var a = host?.Owned(p);
            return a != null && (host.FishingActorActive(a) || a.FishingLine.Ending) ? 0 : item.bait;
        }

        private static bool FishingBaitDebit(Player __instance, Projectile __0, ref int __1, ref bool __result)
        {
            var a = host?.Owned(__instance);
            if (a == null || !host.FishingActorActive(a) && !a.FishingLine.Ending) return true;
            // Protect against a stale native bite marker as well as current
            // bait admission, and do not collect a neighbouring pool's catch.
            if (__0.localAI[2] != ItemID.TruffleWorm && host.FishingDebitAllowed(a, __0)) return true;
            __1 = 0;
            __result = false;
            return false;
        }

        private static bool FishingCheckBefore(Projectile __instance)
        {
            var a = host?.ProjectileActor(__instance);
            if (a == null || !host.FishingActorActive(a)) return true;
            return host.FishingCheckAllowed(a, __instance);
        }

        private static IEnumerable<CodeInstruction> FishingQuestRead(IEnumerable<CodeInstruction> input)
        {
            var field = AccessTools.Field(typeof(Main), nameof(Main.anglerQuestFinished));
            int reads = 0;
            foreach (var instruction in input)
            {
                if (instruction.opcode != OpCodes.Ldsfld || !Equals(instruction.operand, field))
                {
                    yield return instruction;
                    continue;
                }
                var bobber = new CodeInstruction(OpCodes.Ldarg_0);
                bobber.labels.AddRange(instruction.labels);
                bobber.blocks.AddRange(instruction.blocks);
                yield return bobber;
                yield return CodeInstruction.Call(typeof(CompanionHooks), nameof(FishingQuestFinished));
                reads++;
            }
            if (reads != 1) throw new InvalidOperationException("Unexpected native quest qualification read.");
        }

        private static bool FishingQuestFinished(Projectile b)
        {
            var a = host?.ProjectileActor(b);
            return a == null ? Main.anglerQuestFinished : !HostCompanions.ActorQuestAvailable(a);
        }
    }

    internal sealed partial class HostCompanions
    {
        internal bool FishingActorActive(Actor a) => FishingActive(a);

        internal static void FishingNativeBait(Player body, out int power, out int type)
        {
            // Policy observation of the same two native inventory passes.
            // The original item check and fishing roll still select/consume.
            for (int pass = 0; pass < 2; pass++)
                for (int i = pass == 0 ? 54 : 0; i < (pass == 0 ? 58 : 50); i++)
                {
                    var item = body.inventory[i];
                    if (item.stack <= 0 || item.bait <= 0 || item.type == ItemID.TruffleWorm) continue;
                    power = item.bait;
                    type = item.type;
                    return;
                }
            power = type = 0;
        }

        internal bool FishingCheckAllowed(Actor a, Projectile b)
        {
            if (FishingActivityOwns(a, b, out var tracked))
            {
                // A qualifying bobber uses the same body Zones as NPC AI.
                return !a.FishingLine.Ending && tracked.Qualified && RefreshActorEnvironment(a) && FishingEnteredLiquid(a, b);
            }
            return !a.FishingLine.Ending;
        }

        internal bool FishingDebitAllowed(Actor a, Projectile b)
        {
            if (a.FishingLine.Ending) return false;
            return a.FishingLine.Issued != FishingHand.Pull ||
                FishingActivityOwns(a, b, out var tracked) && tracked.Pullable;
        }

        private static bool FishingActivityOwns(Actor a, Projectile b, out CompanionBobber tracked)
        {
            foreach (var candidate in a.FishingLine.Bobbers)
                if (ReferenceEquals(candidate.Body.Projectile, b) && candidate.Body.Key == (int)b.key)
                { tracked = candidate; return true; }
            tracked = null;
            return false;
        }

        internal static bool ActorQuestAvailable(Actor a)
        {
            if (Main.anglerQuest < 0 || Main.anglerQuest >= Main.anglerQuestItemNetIDs.Length) return false;
            return Main.netMode == 1 ? a.FishingLine.Quest == Main.anglerQuest && !a.FishingLine.QuestFinished :
                !Main.anglerWhoFinishedToday.Contains(a.Body.name);
        }

        private static int ActorQuestType(Actor a) => ActorQuestAvailable(a) ? Main.anglerQuestItemNetIDs[Main.anglerQuest] : -1;
    }
}
