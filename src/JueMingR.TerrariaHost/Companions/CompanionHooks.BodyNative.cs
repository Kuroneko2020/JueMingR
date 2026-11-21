using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Events;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal static partial class CompanionHooks
    {
        private static readonly Action<Player> purgeEnergy =
            AccessTools.MethodDelegate<Action<Player>>(AccessTools.Method(typeof(Player), "PurgeDD2EnergyCrystals"));

        private static void ExtendActiveBodyUpdate(List<CodeInstruction> code)
        {
            // Widen only the vanilla HasItem(VoidLens) -> voidVaultInfo[0]
            // assignment. The adjacent local-player HUD and equipment updates
            // remain native human work; no second scan is needed during pickup.
            var setter = AccessTools.PropertySetter(typeof(Player), nameof(Player.IsVoidVaultEnabled));
            var hasItem = AccessTools.Method(typeof(Player), nameof(Player.HasItem), new[] { typeof(int) });
            int write = code.FindIndex(instruction => instruction.Calls(setter));
            if (write < 7 || !code[write - 1].Calls(hasItem) || !code[write - 2].LoadsConstant(ItemID.VoidLens) ||
                code[write - 5].opcode != OpCodes.Bne_Un && code[write - 5].opcode != OpCodes.Bne_Un_S ||
                !code[write - 6].LoadsField(AccessTools.Field(typeof(Main), nameof(Main.myPlayer))) ||
                !code[write + 1].labels.Contains((Label)code[write - 5].operand))
                throw new InvalidOperationException("Companion void bag update gate changed.");
            ReplaceOwner(code, write - 6);

            var updateBuffs = AccessTools.Method(typeof(Player), nameof(Player.UpdateBuffs));
            int buffs = code.FindLastIndex(instruction => instruction.Calls(updateBuffs));
            if (buffs < 0) throw new InvalidOperationException("Companion active body buff call missing.");
            code[buffs].opcode = OpCodes.Call;
            code[buffs].operand = AccessTools.Method(typeof(CompanionHooks), nameof(UpdateBodyBuffsAndPets));
        }

        // The archive's native byte also contains unrelated bits. Recompute
        // only the derived pickup permission when a body or backpack becomes
        // usable between native steps. Quick use and bank UI have other gates.
        internal static void RefreshVoidPickup(Player body)
        {
            body.IsVoidVaultEnabled = body.HasItem(ItemID.VoidLens);
        }

        private static void PurgeCompanionEnergy(Player body)
        {
            // Use the original slot and open-bank rules. Keep the human index
            // here because the native mouse-item branch has process ownership.
            if (!DD2Event.Ongoing && body.whoAmI != Main.myPlayer)
                purgeEnergy(body);
        }

        private static IEnumerable<CodeInstruction> TrackEnergyPurge(IEnumerable<CodeInstruction> instructions)
        {
            var turnToAir = AccessTools.Method(typeof(Item), nameof(Item.TurnToAir));
            var clear = AccessTools.Method(typeof(CompanionHooks), nameof(ClearEnergyStack));
            foreach (var instruction in instructions)
            {
                if (!instruction.Calls(turnToAir)) { yield return instruction; continue; }
                var body = new CodeInstruction(OpCodes.Ldarg_0);
                body.labels.AddRange(instruction.labels);
                body.blocks.AddRange(instruction.blocks);
                yield return body;
                yield return new CodeInstruction(OpCodes.Call, clear);
            }
        }

        private static void ClearEnergyStack(Item stack, Player owner)
        {
            stack.TurnToAir();
            var actor = host?.Owned(owner);
            if (actor != null) actor.Dirty = true;
        }

        private static void UpdateBodyBuffsAndPets(Player body, int slot)
        {
            var actor = host?.Owned(body);
            if (actor == null || !actor.Ready || actor.Closing || slot == Main.myPlayer)
            {
                body.UpdateBuffs(slot);
                return;
            }
            using (Enter(actor, actor.Aim))
            {
                RefreshCompanionShieldAura(body);
                body.UpdateBuffs(slot);
                // This is the normal post-buff phase. The native owner-only
                // methods add equipment buffs; UpdateBuffs on the next step
                // creates the real pet projectile under this same actor.
                body.UpdatePet(slot);
                body.UpdatePetLight(slot);
            }
        }

        private static void RefreshCompanionShieldAura(Player body)
        {
            if (body.team <= 0 || body.miscCounter % 10 != 0) return;
            float rangeSquared = Player.PaladinsShieldRange * Player.PaladinsShieldRange;
            for (int slot = 0; slot < Main.maxPlayers; slot++)
            {
                var teammate = Main.player[slot];
                if (ReferenceEquals(teammate, body) || !teammate.CanDefendWithPaladinsShield(body.team) ||
                    Vector2.DistanceSquared(teammate.position, body.position) >= rangeSquared) continue;
                body.AddBuff(BuffID.PaladinsShield, 20);
                return;
            }
        }

        private static IEnumerable<CodeInstruction> PaladinDamageReceiver(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var defender = AccessTools.Method(typeof(Player), nameof(Player.CanDefendWithPaladinsShield));
            var local = AccessTools.PropertyGetter(typeof(Main), nameof(Main.LocalPlayer));
            var hurt = AccessTools.Method(typeof(Player), nameof(Player.Hurt));
            int selection = code.FindIndex(instruction => instruction.Calls(defender));
            int check = selection < 0 ? -1 : code.FindIndex(selection + 1, instruction => instruction.Calls(local));
            int receiver = check < 0 ? -1 : code.FindIndex(check + 1, instruction => instruction.Calls(local));
            int transfer = receiver < 0 ? -1 : code.FindIndex(receiver + 1, instruction => instruction.Calls(hurt));
            if (check < 1 || receiver < 0 || transfer < 0 || code[transfer - 1].opcode != OpCodes.Ldc_I4_0 ||
                code[check + 1].opcode != OpCodes.Bne_Un && code[check + 1].opcode != OpCodes.Bne_Un_S)
                throw new InvalidOperationException("Companion Paladin hurt target layout changed.");

            // The closest-player search and its 75/25 rounding run once in
            // native Hurt. Widen only the checked receiver authority.
            code[check].operand = AccessTools.Method(typeof(CompanionHooks), nameof(LocalShieldReceiver));
            code[check + 1].opcode = code[check + 1].opcode == OpCodes.Bne_Un_S ? OpCodes.Brfalse_S : OpCodes.Brfalse;
            code[receiver].opcode = code[check - 1].opcode;
            code[receiver].operand = code[check - 1].operand;
            code[transfer].opcode = OpCodes.Call;
            code[transfer].operand = AccessTools.Method(typeof(CompanionHooks), nameof(ChargeShieldReceiver));
            return code;
        }

        private static bool LocalShieldReceiver(Player receiver)
        {
            if (ReferenceEquals(receiver, Main.LocalPlayer)) return true;
            if (current != null && receiver.whoAmI == current.HumanIndex) return true;
            var actor = host?.Owned(receiver);
            return actor != null && actor.Ready && !actor.Closing && !actor.Faulted;
        }

        internal struct ShieldImpact
        {
            internal Player Receiver;
            internal PlayerDeathReason Source;
            internal int Damage, Direction, Cooldown;
            internal bool Pvp, Quiet, Critical, Dodgeable;

            internal void Apply()
            {
                Receiver.Hurt(Source, Damage, Direction, Pvp, Quiet, Critical, Cooldown, Dodgeable);
            }
        }

        private static void ApplyHumanShieldHits(List<ShieldImpact> hits)
        {
            if (hits == null) return;
            foreach (var hit in hits)
                if (ReferenceEquals(Main.LocalPlayer, hit.Receiver) && hit.Receiver.active && !hit.Receiver.dead)
                    hit.Apply();
        }

        private static double ChargeShieldReceiver(Player receiver, PlayerDeathReason source, int damage, int direction,
            bool pvp, bool quiet, bool critical, int cooldown, bool dodgeable)
        {
            if (current == null || host?.Owned(receiver) != null)
                return receiver.Hurt(source, damage, direction, pvp, quiet, critical, cooldown, dodgeable);

            // Native discards the secondary Hurt result. In this same step,
            // deliver the already-selected damage after all borrowed views
            // return: a lethal share can change the human mouse item, map,
            // world items and the active save through the human connection.
            var impact = new ShieldImpact
            {
                Receiver = receiver, Source = source, Damage = damage, Direction = direction, Cooldown = cooldown,
                Pvp = pvp, Quiet = quiet, Critical = critical, Dodgeable = dodgeable
            };
            var step = playerStep?.Root;
            if (step != null && step.EntryScope == null)
                (step.HumanShieldHits ?? (step.HumanShieldHits = new List<ShieldImpact>())).Add(impact);
            else
            {
                var root = current.Root;
                (root.HumanShieldHits ?? (root.HumanShieldHits = new List<ShieldImpact>())).Add(impact);
            }
            return 0;
        }
    }
}
