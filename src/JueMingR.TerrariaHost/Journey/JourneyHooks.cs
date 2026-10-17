using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.IO;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Journey
{
    internal static class JourneyHooks
    {
        private const string OwnerId = "JueMingR.Journey.Native1458";
        private static JourneySession owner;
        private static readonly List<MethodBase> targets = new List<MethodBase>();
        [ThreadStatic] private static SaveView save;
        [ThreadStatic] private static int creativeInputDepth;

        internal static void Install(JourneySession value)
        {
            if (owner != null) throw new InvalidOperationException("Journey already has a native owner.");
            var harmony = new Harmony(OwnerId);
            try
            {
                // Each version-bound method contains precisely one relevant
                // read. Metadata/IL are resolved at installation only; the
                // replacement retains labels and exception blocks in place.
                Qualify(harmony, typeof(Player), "ToggleCreativeMenu", Type.EmptyTypes, false);
                Qualify(harmony, typeof(CreativeUI), "Draw", new[] { typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch) }, false);
                Qualify(harmony, typeof(Player), "GrabItems", new[] { typeof(int) }, false);
                Qualify(harmony, typeof(Player), "GetItemGrabRange", new[] { typeof(Item) }, false);
                Qualify(harmony, typeof(Player), "ResetEffects", Type.EmptyTypes, true);
                Qualify(harmony, typeof(Main), "UpdateCreativeGameModeOverride", Type.EmptyTypes, true);
                Type spawner = AccessTools.Inner(typeof(NPC), "Spawner") ?? throw new MissingMemberException("NPC.Spawner");
                Qualify(harmony, spawner, "CanSpawnEnemiesNear", new[] { typeof(Player) }, true);
                Qualify(harmony, spawner, "GetSpawnRate", new[] { typeof(Player), typeof(int).MakeByRefType(), typeof(int).MakeByRefType() }, true);
                Qualify(harmony, spawner, "SlimeRainSpawns", new[] { typeof(Player) }, true);
                Qualify(harmony, typeof(WorldGen), "CheckSign", new[] { typeof(int), typeof(int), typeof(ushort) }, true);
                Qualify(harmony, typeof(WorldGen), "SmashAltar", new[] { typeof(int), typeof(int) }, true);
                Patch(harmony, Method(typeof(CreativeUI), "Update", typeof(Microsoft.Xna.Framework.GameTime)), "BeginCreativeUpdate", "EndCreativeInput");
                Patch(harmony, Method(typeof(CreativeUI), "Draw", typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch)), "BeginCreativeInput", "EndCreativeInput");
                foreach (MethodInfo method in new[] {
                    Method(typeof(UserInterface), "Update", typeof(Microsoft.Xna.Framework.GameTime)),
                    Method(typeof(CreativeUI), "DrawToggleButton", typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch), typeof(Microsoft.Xna.Framework.Vector2)) })
                { Apply(harmony, method, transpiler: new HarmonyMethod(typeof(JourneyHooks), "MouseRead")); }
                Patch(harmony, Method(typeof(UICreativeItemGrid), "DrawSlot", typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch), typeof(Item), typeof(Microsoft.Xna.Framework.Vector2), typeof(bool)), "CreativeSlotHover");
                Patch(harmony, Method(typeof(ItemsSacrificedUnlocksTracker), "ForEachItemWithResearchProgress", typeof(Action<int>)), "Enumerate");
                Patch(harmony, Method(typeof(ItemsSacrificedUnlocksTracker), "TryGetSacrificeNumbers", typeof(int), typeof(int).MakeByRefType(), typeof(int).MakeByRefType()), "Numbers");
                Patch(harmony, Method(typeof(ItemsSacrificedUnlocksTracker), "GetSacrificeCount", typeof(int)), "Count");
                Patch(harmony, Method(typeof(Player), "InternalSavePlayerFile", typeof(PlayerFileData)), "BeginPlayerSave", "EndSave");
                Patch(harmony, Method(typeof(WorldFile), "SaveWorld_Version2", typeof(BinaryWriter)), "BeginWorldSave", "EndSave");
                Patch(harmony, Method(typeof(CreativePowerManager), "SaveToPlayer", typeof(Player), typeof(BinaryWriter)), "SavePlayerPowers");
                Patch(harmony, Method(typeof(CreativePowerManager), "SaveToWorld", typeof(BinaryWriter)), "SaveWorldPowers");
                Patch(harmony, Method(typeof(CreativePowerManager), "Reset"), "BeforeReset");
                Patch(harmony, Method(typeof(CreativePowerManager), "ResetDataForNewPlayer", typeof(Player)), "BeforeReset");
                // Publish only after all required patches and IL read counts
                // have succeeded. A partial install cannot grant eligibility.
                owner = value;
            }
            catch (Exception original)
            {
                var errors = new List<Exception> { original };
                foreach (MethodBase target in targets)
                {
                    try { harmony.Unpatch(target, HarmonyPatchType.All, OwnerId); }
                    catch (Exception cleanup) { errors.Add(new InvalidOperationException("Journey cleanup target: " + target.DeclaringType.FullName + "." + target.Name, cleanup)); }
                }
                targets.Clear();
                if (errors.Count > 1) throw new AggregateException("Journey installation failed; cleanup also failed.", errors);
                throw;
            }
        }

        private static MethodInfo Method(Type type, string name, params Type[] args)
        { return AccessTools.Method(type, name, args) ?? throw new MissingMethodException(type.FullName, name); }
        private static void Patch(Harmony harmony, MethodInfo method, string prefix, string finalizer = null)
        {
            Apply(harmony, method, prefix: new HarmonyMethod(typeof(JourneyHooks), prefix),
                finalizer: finalizer == null ? null : new HarmonyMethod(typeof(JourneyHooks), finalizer));
        }
        private static void Qualify(Harmony harmony, Type type, string name, Type[] args, bool world)
        {
            var method = Method(type, name, args);
            string patch = type == typeof(Player) && name == "ResetEffects" ? "LocalWorldRead" : world ? "WorldRead" : "PlayerRead";
            Apply(harmony, method, transpiler: new HarmonyMethod(typeof(JourneyHooks), patch));
        }
        private static void Apply(Harmony harmony, MethodInfo method, HarmonyMethod prefix = null, HarmonyMethod transpiler = null, HarmonyMethod finalizer = null)
        {
            try
            {
                harmony.Patch(method, prefix: prefix, transpiler: transpiler, finalizer: finalizer);
                targets.Add(method);
            }
            catch (Exception error)
            { throw new InvalidOperationException("Journey patch target: " + method.DeclaringType.FullName + "." + method.Name, error); }
        }

        private static IEnumerable<CodeInstruction> PlayerRead(IEnumerable<CodeInstruction> instructions)
        {
            int found = 0;
            FieldInfo field = AccessTools.Field(typeof(Player), "difficulty");
            MethodInfo replacement = Method(typeof(JourneyHooks), "Difficulty", typeof(Player));
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, field))
                { instruction.opcode = OpCodes.Call; instruction.operand = replacement; found++; }
                yield return instruction;
            }
            if (found != 1) throw new InvalidOperationException("Journey player eligibility read count: " + found);
        }
        private static IEnumerable<CodeInstruction> WorldRead(IEnumerable<CodeInstruction> instructions)
        {
            int found = 0;
            MethodInfo original = AccessTools.PropertyGetter(typeof(Main), "IsJourneyMode");
            MethodInfo replacement = Method(typeof(JourneyHooks), "WorldEligible");
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(original)) { instruction.operand = replacement; found++; }
                yield return instruction;
            }
            if (found != 1) throw new InvalidOperationException("Journey world eligibility read count: " + found);
        }
        private static byte Difficulty(Player player)
        { return owner != null && owner.OwnsPlayer(player) ? (byte)3 : player.difficulty; }
        private static bool WorldEligible() { return Main.IsJourneyMode || owner != null && owner.IsActive; }
        private static bool LocalWorldEligible(Player player)
        { return Main.IsJourneyMode || owner != null && owner.OwnsPlayer(player); }
        private static IEnumerable<CodeInstruction> LocalWorldRead(IEnumerable<CodeInstruction> instructions)
        {
            int found = 0;
            var original = AccessTools.PropertyGetter(typeof(Main), "IsJourneyMode");
            var replacement = Method(typeof(JourneyHooks), "LocalWorldEligible", typeof(Player));
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(original))
                {
                    var player = new CodeInstruction(OpCodes.Ldarg_0);
                    player.labels.AddRange(instruction.labels); instruction.labels.Clear();
                    player.blocks.AddRange(instruction.blocks); instruction.blocks.Clear();
                    yield return player;
                    instruction.opcode = OpCodes.Call; instruction.operand = replacement; found++;
                }
                yield return instruction;
            }
            if (found != 1) throw new InvalidOperationException("Journey local range eligibility read count: " + found);
        }
        private static IEnumerable<CodeInstruction> MouseRead(IEnumerable<CodeInstruction> instructions)
        {
            int found = 0;
            MethodInfo original = AccessTools.PropertyGetter(typeof(PlayerInput), "IgnoreMouseInterface");
            MethodInfo replacement = Method(typeof(JourneyHooks), "IgnoreCreativeMouse");
            foreach (var instruction in instructions)
            { if (instruction.Calls(original)) { instruction.operand = replacement; found++; } yield return instruction; }
            if (found != 1) throw new InvalidOperationException("Journey native mouse read count: " + found);
        }
        private static bool IgnoreCreativeMouse()
        {
            // Only the actual native CreativeUI call stack can borrow this
            // exception for a held tool. The host's focus/release gate remains
            // required; every other native UI keeps its original getter.
            return !(creativeInputDepth > 0 && owner != null && owner.CanUseNativeInput) && PlayerInput.IgnoreMouseInterface;
        }
        private static void CreativeSlotHover(UICreativeItemGrid __instance, Microsoft.Xna.Framework.Vector2 pos, ref bool hovering)
        {
            if (hovering || creativeInputDepth == 0 || owner == null || !owner.CanUseNativeInput) return;
            // Harmony/.NET Framework cannot wrap the constructed generic
            // collection DrawSelf. This non-generic native override receives
            // its exact .8 44x44 slot top-left; recompute only that suppressed
            // hover using the same element and ActiveInstance mouse position.
            hovering = __instance.IsMouseHovering && new Microsoft.Xna.Framework.Rectangle((int)pos.X, (int)pos.Y, 44, 44)
                .Contains(UserInterface.ActiveInstance.MousePosition.ToPoint());
        }
        private static void BeginCreativeInput(out int __state)
        { __state = creativeInputDepth; creativeInputDepth++; }
        private static bool BeginCreativeUpdate(out int __state)
        {
            __state = creativeInputDepth;
            // Native CreativeUI.Update precedes this frame's input mapping.
            // On focus/release quarantine, even a leftover previous mapping
            // must not become a fresh UI command. Other native UI is untouched.
            if (owner != null && owner.IsActive && !owner.CanUseNativeInput) return false;
            creativeInputDepth++; return true;
        }
        private static Exception EndCreativeInput(Exception __exception, int __state)
        { creativeInputDepth = __state; return __exception; }

        private static bool Enumerate(ItemsSacrificedUnlocksTracker __instance, Action<int> action)
        {
            if (owner == null || !owner.OwnsTracker(__instance)) return true;
            owner.Research.Enumerate(action); return false;
        }
        private static bool Numbers(ItemsSacrificedUnlocksTracker __instance, int itemId, ref int amountWeHave, ref int amountNeededTotal, ref bool __result)
        {
            if (owner == null || !owner.OwnsTracker(__instance)) return true;
            __result = owner.Research.TryCap(itemId, out int cap); amountWeHave = amountNeededTotal = __result ? cap : 0; return false;
        }
        private static bool Count(ItemsSacrificedUnlocksTracker __instance, int itemId, ref int __result)
        {
            if (owner == null || !owner.OwnsTracker(__instance) || !owner.Research.TryCap(itemId, out int cap)) return true;
            __result = cap; return false;
        }

        // SaveView is a bounded stack along actual synchronous serializers,
        // entered on whichever thread owns that native save. It never conveys
        // runtime qualification to a worker or temporarily flips a singleton.
        private sealed class SaveView
        {
            internal readonly SaveView Parent;
            internal readonly JourneyPowers Powers;
            internal readonly Player Player;
            internal readonly bool World;
            internal SaveView(JourneyPowers powers, Player player, bool world)
            { Parent = save; Powers = powers; Player = player; World = world; }
        }
        private static void BeginPlayerSave(PlayerFileData playerFile, out SaveView __state)
        {
            JourneyPowers captured = owner?.Persistence;
            if (captured != null && (!ReferenceEquals(playerFile, captured.PlayerFile) || !ReferenceEquals(playerFile.Player, captured.Player))) captured = null;
            __state = new SaveView(captured, playerFile.Player, false); save = __state;
        }
        private static void BeginWorldSave(out SaveView __state)
        {
            JourneyPowers captured = owner?.Persistence;
            if (captured != null && !ReferenceEquals(Main.ActiveWorldFileData, captured.WorldFile)) captured = null;
            __state = new SaveView(captured, null, true); save = __state;
        }
        private static Exception EndSave(Exception __exception, SaveView __state)
        { if (__state != null) save = __state.Parent; return __exception; }
        private static bool SavePlayerPowers(Player player, BinaryWriter writer)
        {
            var context = save;
            if (context?.Powers == null || context.World || !ReferenceEquals(player, context.Player)) return true;
            writer.Write(context.Powers.PlayerBytes); return false;
        }
        private static bool SaveWorldPowers(BinaryWriter writer)
        {
            var context = save;
            if (context?.Powers == null || !context.World) return true;
            writer.Write(context.Powers.WorldBytes); return false;
        }
        private static void BeforeReset() { owner?.BeforeNativeReset(); }
    }
}
