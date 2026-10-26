using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal static class BlueprintHooks
    {
        private static HostBlueprint host;
        private static readonly Harmony harmony = new Harmony("JueMingR.Blueprint.WorldActions");
        private static int heldDepth;
        internal static void Install(HostBlueprint owner)
        {
            host = owner;
            Patch(typeof(TriggersSet), "CopyInto", new[] { typeof(Player) }, null, nameof(Copied));
            Patch(typeof(TriggersSet), "CopyIntoDuringChat", new[] { typeof(Player) }, null, nameof(Copied));
            Patch(typeof(Player), "LookForTileInteractions", Type.EmptyTypes, nameof(PlayerGate));
            Patch(typeof(Player), "ItemCheck_ManageRightClickFeatures", Type.EmptyTypes, nameof(PlayerGate));
            Patch(typeof(Player), "TileInteractionsUse", new[] { typeof(int), typeof(int) }, nameof(TileUse));
            Patch(typeof(Player), "ItemCheck_TryStartUse", new[] { typeof(Item), typeof(bool) }, nameof(TryUse));
            Patch(typeof(Player), "dropItemCheck", Type.EmptyTypes, nameof(DropBefore), nameof(ScopeAfter), nameof(ScopeFinal));
            Patch(typeof(Main), "HoverOverNPCs", new[] { typeof(Rectangle) }, nameof(HoverBefore), nameof(ScopeAfter), nameof(ScopeFinal));
            Patch(typeof(Main), "TryInteractingWithVoidLens", new[] { typeof(Projectile) }, nameof(HoverBefore), nameof(ScopeAfter), nameof(ScopeFinal));
            Patch(typeof(Main), "TryInteractingWithMoneyTrough", new[] { typeof(Projectile) }, nameof(HoverBefore), nameof(ScopeAfter), nameof(ScopeFinal));
            Patch(typeof(Main), "TryDespawningProjectile", new[] { typeof(Projectile), typeof(int) }, nameof(HoverBefore), nameof(ScopeAfter), nameof(ScopeFinal));
            Patch(typeof(Chest), "AfterPlacement_Hook", new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int) }, nameof(ContainerBefore), nameof(ContainerAfter), nameof(ContainerFinal));
            Patch(typeof(MessageBuffer), "GetData", new[] { typeof(int), typeof(int), typeof(int).MakeByRefType() }, nameof(ReceiveBefore), nameof(ReceiveAfter), nameof(ReceiveFinal));
        }
        private static void Patch(Type type, string name, Type[] args, string before = null, string after = null, string final = null)
        {
            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, args, null);
            if (method == null || method.GetMethodBody() == null) throw new MissingMethodException("blueprint-world-abi:" + name);
            harmony.Patch(method, Hook(before), Hook(after), null, Hook(final));
        }
        private static HarmonyMethod Hook(string name) { return name == null ? null : new HarmonyMethod(typeof(BlueprintHooks).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)); }
        internal static bool Blocks(Player player) { return host != null && host.Local(player) && host.WorldActionsBlocked; }
        internal static void Gate(Player player)
        {
            if (host == null || !host.Local(player)) return;
            int mask = host.BlockedActions;
            // Editor/tail protection remains local to this world consumer.
            // An exact admitted build supplies its scoped press after CopyInto;
            // pure material-view pointer ownership stays in F5Shell instead.
            if ((mask & 1) != 0 && !host.OwnsAutomaticBuildInput(player)) player.controlUseItem = false;
            if ((mask & 2) != 0) player.controlUseTile = false;
        }
        private static void Copied(Player __0) { Gate(__0); }
        private static void PlayerGate(Player __instance) { Gate(__instance); }
        private static bool TileUse(Player __instance) { return !Blocks(__instance) || (host.BlockedActions & 2) == 0; }
        private static bool TryUse(Player __instance, ref bool __result)
        {
            // QuickHeal/QuickMana share TryStartUse. Only the held ItemCheck
            // scope is denied; independent medicine shortcuts retain vanilla.
            if (heldDepth == 0 || !Blocks(__instance) || (host.BlockedActions & 1) == 0) return true;
            __result = false; return false;
        }
        internal sealed class MouseLease
        {
            private bool ended;
            private readonly bool left, right, held;
            private readonly int mask;
            internal MouseLease(int mask, bool held)
            {
                this.mask = mask; this.held = held; left = Main.mouseLeft; right = Main.mouseRight;
                if (held) heldDepth++;
                if ((mask & 1) != 0) Main.mouseLeft = false;
                if ((mask & 2) != 0) Main.mouseRight = false;
            }
            internal void End()
            {
                if (ended) return; ended = true;
                if (held) heldDepth--;
                // Return only the temporary values still owned by this lease.
                // Native animation, channel and release flags are never restored.
                if ((mask & 1) != 0 && !Main.mouseLeft) Main.mouseLeft = left;
                if ((mask & 2) != 0 && !Main.mouseRight) Main.mouseRight = right;
            }
        }
        internal static MouseLease BeginHeld(Player player)
        { if (!Blocks(player)) return null; Gate(player); return new MouseLease(host.BlockedActions, true); }
        private static void DropBefore(Player __instance, out MouseLease __state)
        {
            // dropItemCheck also returns carried slots and maintains noThrow.
            // Mask its direct right-click world branch; always run that upkeep.
            __state = Blocks(__instance) ? new MouseLease(host.BlockedActions & 2, false) : null;
        }
        private static void HoverBefore(out MouseLease __state)
        {
            // These are exact world hover consumers, never DrawMouseOver/Draw:
            // inventory, resource and menu clicks keep their native raw state.
            Gate(Main.LocalPlayer);
            __state = Blocks(Main.LocalPlayer) ? new MouseLease(host.BlockedActions & 2, false) : null;
        }
        private static void ScopeAfter(MouseLease __state) { __state?.End(); }
        private static Exception ScopeFinal(MouseLease __state, Exception __exception) { __state?.End(); return __exception; }
        private static void ContainerBefore(int __0, int __1, int __2, int __3, out HostBlueprint.BuildContainerReceipt __state)
        { __state = host?.BeginBuildContainer(__0, __1, __2, __3); }
        private static void ContainerAfter(int __result, HostBlueprint.BuildContainerReceipt __state)
        { host?.EndBuildContainer(__state, __result, null); }
        private static Exception ContainerFinal(HostBlueprint.BuildContainerReceipt __state, Exception __exception)
        { if (__exception != null) host?.EndBuildContainer(__state, -1, __exception); return __exception; }
        private struct ReceivedContainer
        {
            internal HostBlueprint Owner;
            internal HostBlueprint.BuildContainerReceipt Receipt;
            internal bool Accepted;
        }
        private static void ReceiveBefore(MessageBuffer __instance, int __0, int __1, out ReceivedContainer __state)
        {
            __state = default(ReceivedContainer);
            if (Main.netMode != 1 || __instance.whoAmI != 256 || host == null) return;
            var receipt = host.MatchBuildContainer(__instance.readBuffer, __0, __1);
            if (receipt != null) __state = new ReceivedContainer { Owner = host, Receipt = receipt,
                Accepted = unchecked((short)(__instance.readBuffer[__0 + 8] | __instance.readBuffer[__0 + 9] << 8)) >= 0 };
        }
        private static void ReceiveAfter(ReceivedContainer __state)
        { __state.Owner?.ReceiveBuildContainer(__state.Receipt, __state.Accepted, null); }
        private static Exception ReceiveFinal(ReceivedContainer __state, Exception __exception)
        { if (__exception != null) __state.Owner?.ReceiveBuildContainer(__state.Receipt, false, __exception); return __exception; }
        internal static void Uninstall() { harmony.UnpatchAll(harmony.Id); host = null; heldDepth = 0; }
    }
}
