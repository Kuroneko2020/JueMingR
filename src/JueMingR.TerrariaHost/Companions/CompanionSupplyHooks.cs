using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingR.TerrariaHost.Recovery;
using Terraria;
using Terraria.GameContent.Achievements;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal static class CompanionSupplyHooks
    {
        private static readonly Harmony Harmony = new Harmony("JueMingR.CompanionSupplies");
        private static HostCompanions host;
        [ThreadStatic] private static HostCompanions.NursePaymentAttempt nurse;

        internal static void Install(HostCompanions value)
        {
            host = value;
            try
            {
                CompanionNurseWallet.Bind();
                Patch(typeof(Player), "QuickHeal_GetItemToUse", Type.EmptyTypes, prefix: nameof(HealChoice));
                Patch(typeof(Player), "QuickMana_GetItemToUse", Type.EmptyTypes, prefix: nameof(ManaChoice));
                Patch(typeof(Player), "QuickMana", Type.EmptyTypes, prefix: nameof(ManaStart), finalizer: nameof(ManaEnd));
                Patch(typeof(Player), "QuickBuff_PickBestFoodItem", Type.EmptyTypes, prefix: nameof(BuffFood));
                Patch(typeof(Player), "QuickBuff_ShouldUseItem", new[] { typeof(Item), typeof(int).MakeByRefType() }, prefix: nameof(BuffChoice));
                Patch(typeof(Player), "ApplyLifeAndOrMana", new[] { typeof(Item) }, prefix: nameof(BeforeEffect), postfix: nameof(AfterEffect));
                Patch(typeof(Player), "ItemCheck", Type.EmptyTypes, prefix: nameof(BeforeItemCheck), finalizer: nameof(AfterItemCheck));
                Patch(typeof(Player), "Update", new[] { typeof(int) }, finalizer: nameof(AfterPlayerUpdate));
                Patch(typeof(Main), nameof(Main.NPCChatText_DoNurseHeal), new[] { typeof(int) }, transpiler: nameof(NursePaymentSite));
            }
            catch
            {
                Uninstall();
                throw;
            }
        }

        internal static void Uninstall()
        {
            Harmony.UnpatchAll(Harmony.Id);
            host = null;
        }

        private static void Patch(Type type, string name, Type[] parameters, string prefix = null,
            string postfix = null, string finalizer = null, string transpiler = null)
        {
            var method = AccessTools.DeclaredMethod(type, name, parameters);
            if (method == null) throw new MissingMethodException(type.FullName, name);
            Harmony.Patch(method, Method(prefix), Method(postfix), Method(transpiler), Method(finalizer));
        }

        private static HarmonyMethod Method(string name)
        {
            return name == null ? null : new HarmonyMethod(typeof(CompanionSupplyHooks), name);
        }

        private static HostCompanions.Actor ScopedActor(Player body)
        {
            var actor = CompanionHooks.CurrentActor;
            return actor != null && ReferenceEquals(actor.Body, body) ? actor : null;
        }

        private static bool HealChoice(Player __instance, ref Item __result)
        {
            var actor = ScopedActor(__instance);
            if (actor == null || !actor.Supplies.Quick.Found || actor.Supplies.QuickKind != CompanionSupplyKind.Life) return true;
            __result = HostCompanions.QuickSupply(actor, CompanionSupplyKind.Life);
            return false;
        }

        private static bool ManaChoice(Player __instance, ref Item __result)
        {
            var actor = ScopedActor(__instance);
            if (actor == null || !actor.Supplies.NativeMana &&
                (!actor.Supplies.Quick.Found || actor.Supplies.QuickKind != CompanionSupplyKind.Mana)) return true;
            __result = HostCompanions.QuickSupply(actor, CompanionSupplyKind.Mana);
            return false;
        }

        private static bool ManaStart(Player __instance, out HostCompanions.Actor __state)
        {
            __state = null;
            var actor = ScopedActor(__instance);
            if (actor == null) return true;
            var state = actor.Supplies;
            if (state.ManaFrame == Main.GameUpdateCount) return false;
            if (state.Quick.Found) return state.QuickKind == CompanionSupplyKind.Mana;
            if (state.NativeMana) return false;

            // Native mana flower keeps its original source order and works
            // with proactive recovery disabled. Track the actual actor's
            // chosen source so an unchanged draft/lease cannot be overwritten.
            state.Quick = host.ChooseQuick(actor, CompanionSupplyKind.Mana, nativeMana: true);
            state.QuickKind = CompanionSupplyKind.Mana;
            state.QuickObserved = false;
            state.NativeMana = true;
            __state = actor;
            return true;
        }

        private static Exception ManaEnd(HostCompanions.Actor __state, Exception __exception)
        {
            if (__state == null) return __exception;
            __state.Supplies.NativeMana = false;
            host.FinishQuick(__state, __exception == null);
            return __exception;
        }

        private static bool BuffFood(Player __instance, ref Item __result)
        {
            var actor = ScopedActor(__instance);
            if (actor == null || !actor.Supplies.Quick.Found || actor.Supplies.QuickKind != CompanionSupplyKind.Buff) return true;
            Item item = HostCompanions.QuickSupply(actor, CompanionSupplyKind.Buff);
            bool selected = !actor.Supplies.QuickBuffTried && item != null && BuffRecoveryFood(item);
            if (selected) actor.Supplies.QuickBuffTried = true;
            __result = selected ? item : null;
            return false;
        }

        private static bool BuffRecoveryFood(Item item)
        {
            return BuffID.Sets.IsWellFed[item.buffType];
        }

        private static bool BuffChoice(Player __instance, Item __0, ref int __1, ref bool __result)
        {
            var actor = ScopedActor(__instance);
            if (actor == null || !actor.Supplies.Quick.Found || actor.Supplies.QuickKind != CompanionSupplyKind.Buff) return true;
            if (!actor.Supplies.QuickBuffTried && ReferenceEquals(__0, HostCompanions.QuickSupply(actor, CompanionSupplyKind.Buff)) && !BuffRecoveryFood(__0))
            {
                actor.Supplies.QuickBuffTried = true;
                return true;
            }
            __1 = 0;
            __result = false;
            return false;
        }

        private struct Effect
        {
            internal HostCompanions.Actor Actor;
            internal int Life, Mana;
        }

        private static void BeforeEffect(Player __instance, out Effect __state)
        {
            __state = new Effect { Actor = ScopedActor(__instance), Life = __instance.statLife, Mana = __instance.statMana };
        }

        private static void AfterEffect(Player __instance, Item __0, Effect __state)
        {
            var actor = __state.Actor;
            if (actor == null) return;
            bool life = __instance.statLife > __state.Life, mana = __instance.statMana > __state.Mana;
            if (life) actor.Supplies.LifeFrame = Main.GameUpdateCount;
            if (mana) actor.Supplies.ManaFrame = Main.GameUpdateCount;
            if (life || mana)
            {
                actor.Dirty = true;
                if (ReferenceEquals(actor.Supplies.Quick.Item, __0)) actor.Supplies.QuickObserved = true;
            }
        }

        private static void BeforeItemCheck(Player __instance)
        {
            var actor = CompanionHooks.SupplyActor(__instance);
            if (actor != null) host.ValidateCompanionHeldItem(actor);
        }

        private static Exception AfterItemCheck(Player __instance, Exception __exception)
        {
            var actor = CompanionHooks.SupplyActor(__instance);
            if (actor != null) host.EndCompanionHeldItem(actor, __exception == null);
            return __exception;
        }

        private static Exception AfterPlayerUpdate(Player __instance, Exception __exception)
        {
            var actor = CompanionHooks.SupplyActor(__instance);
            if (actor != null && actor.Supplies.HeldSource.Found)
                host.AbandonCompanionHeldItem(actor, __exception);
            return __exception;
        }

        internal static void RunNurse(HostCompanions.NursePaymentAttempt attempt)
        {
            if (nurse != null || !ReferenceEquals(CompanionHooks.CurrentActor, attempt.Actor)) return;
            nurse = attempt;
            try { Main.NPCChatText_DoNurseHeal(attempt.Quote); }
            finally { nurse = null; }
        }

        private static IEnumerable<CodeInstruction> NursePaymentSite(IEnumerable<CodeInstruction> instructions)
        {
            var buy = AccessTools.DeclaredMethod(typeof(Player), nameof(Player.BuyItem), new[] { typeof(long), typeof(int) });
            var achievement = AccessTools.DeclaredMethod(typeof(AchievementsHelper), nameof(AchievementsHelper.HandleNurseService));
            var portrait = AccessTools.DeclaredMethod(typeof(Main), nameof(Main.DoNPCPortraitHop));
            var dialogue = AccessTools.DeclaredField(typeof(Main), nameof(Main.npcChatText));
            int payments = 0, achievements = 0, portraits = 0, dialogues = 0;
            foreach (var instruction in instructions)
            {
                string replacement = null;
                if (instruction.Calls(buy)) { payments++; replacement = nameof(NurseBuy); }
                else if (instruction.Calls(achievement)) { achievements++; replacement = nameof(NurseAchievement); }
                else if (instruction.Calls(portrait)) { portraits++; replacement = nameof(NursePortrait); }
                else if (instruction.opcode == OpCodes.Stsfld && Equals(instruction.operand, dialogue))
                { dialogues++; replacement = nameof(NurseDialogue); }
                if (replacement != null)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.DeclaredMethod(typeof(CompanionSupplyHooks), replacement);
                }
                yield return instruction;
            }
            if (payments != 1 || achievements != 1 || portraits != 1 || dialogues == 0)
                throw new InvalidOperationException("Pinned native nurse call sites changed.");
        }

        private static bool NurseBuy(Player player, long price, int currency)
        {
            // Only this BuyItem call site, inside the native nurse method,
            // can commit the split. Ordinary BuyItem and human nurse hooks
            // retain their own admission and transaction semantics.
            return nurse == null ? player.BuyItem(price, currency) : nurse.Commit(player, price, currency);
        }

        private static void NurseAchievement(int price)
        {
            if (nurse == null) AchievementsHelper.HandleNurseService(price);
        }

        private static void NursePortrait()
        {
            if (nurse == null) Main.DoNPCPortraitHop();
        }

        private static void NurseDialogue(string text)
        {
            if (nurse == null) Main.npcChatText = text;
        }
    }
}
