using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.Items
{
    internal static class ItemGuardTranspilers
    {
        // These methods only select from Item arrays before using the selected
        // object. The air value is a read result, never written into inventory.
        // Do not apply this rewrite to an empty-slot receiver such as FillAmmo.
        internal static IEnumerable<CodeInstruction> SelectionReads(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            int count = 0;
            MethodInfo read = typeof(ItemPendingGuards).GetMethod(nameof(ItemPendingGuards.ReadSelectionSlot), BindingFlags.Static | BindingFlags.NonPublic);
            foreach (CodeInstruction instruction in code)
            {
                if (instruction.opcode != OpCodes.Ldelem_Ref) continue;
                instruction.opcode = OpCodes.Call; instruction.operand = read; count++;
            }
            if (count == 0) throw new InvalidOperationException("item-selection-read-shape");
            return code;
        }

        internal static IEnumerable<CodeInstruction> AmmoReceiver(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            FieldInfo type = typeof(Item).GetField("type");
            MethodInfo read = typeof(ItemPendingGuards).GetMethod(nameof(ItemPendingGuards.ReadAmmoSlotType), BindingFlags.Static | BindingFlags.NonPublic);
            int count = 0;
            for (int i = 0; i + 2 < code.Count; i++)
            {
                if (code[i].opcode != OpCodes.Ldelem_Ref || code[i + 1].opcode != OpCodes.Ldfld || !Equals(code[i + 1].operand, type)) continue;
                // In 1.4.5.8 the two type predicates are occupied (> 0) and
                // empty (== 0). -1 fails both; an air substitution would admit
                // the empty-slot write and corrupt an unconfirmed transfer.
                bool occupied = i + 3 < code.Count && code[i + 2].opcode == OpCodes.Ldc_I4_0 &&
                    (code[i + 3].opcode == OpCodes.Ble || code[i + 3].opcode == OpCodes.Ble_S);
                bool empty = code[i + 2].opcode == OpCodes.Brtrue || code[i + 2].opcode == OpCodes.Brtrue_S;
                if (!occupied && !empty) throw new InvalidOperationException("ammo-receiver-predicate-shape");
                code[i].opcode = OpCodes.Call; code[i].operand = read;
                code[i + 1].opcode = OpCodes.Nop; code[i + 1].operand = null; count++;
            }
            if (count != 2) throw new InvalidOperationException("ammo-receiver-loop-count");
            return code;
        }
        internal static IEnumerable<CodeInstruction> ActuatorSelection(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList(); int count = 0;
            MethodInfo original = typeof(Player).GetMethod("FindItem", new[] { typeof(int) });
            MethodInfo guarded = typeof(ItemPendingGuards).GetMethod(nameof(ItemPendingGuards.FindAvailableItem), BindingFlags.Static | BindingFlags.NonPublic);
            foreach (CodeInstruction instruction in code)
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && Equals(instruction.operand, original))
                { instruction.opcode = OpCodes.Call; instruction.operand = guarded; count++; }
            if (count != 1) throw new InvalidOperationException("auto-actuator-selection-shape");
            return code;
        }
    }
}
