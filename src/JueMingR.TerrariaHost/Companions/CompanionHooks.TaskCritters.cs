using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal static partial class CompanionHooks
    {
        internal static bool TaskCritterAvailable { get; private set; }

        private static void InstallTaskCritters()
        {
            var harmony=new Harmony("JueMingR.Companions.TaskCritters");
            try
            {
                var filter=AccessTools.DeclaredMethod(typeof(Player),"ItemCheck_CatchCritters",new[] { typeof(Item),typeof(Rectangle) });
                var catchNpc=AccessTools.DeclaredMethod(typeof(NPC),nameof(NPC.CatchNPC),new[] { typeof(int),typeof(int) });
                harmony.Patch(filter,transpiler:Hook(nameof(CatchOneNativeTarget)));
                harmony.Patch(catchNpc,postfix:Hook(nameof(CatchObserved)));
                TaskCritterAvailable=true;
            }
            catch (Exception)
            {
                harmony.UnpatchAll(harmony.Id);
                TaskCritterAvailable=false;
            }
        }

        private static IEnumerable<CodeInstruction> CatchOneNativeTarget(IEnumerable<CodeInstruction> instructions)
        {
            var active=AccessTools.Field(typeof(NPC),nameof(NPC.active));
            int matches=0;
            foreach(var instruction in instructions)
            {
                if (instruction.opcode!=OpCodes.Ldfld || !Equals(instruction.operand,active)) { yield return instruction; continue; }
                // Suppress other creatures before vanilla's geometry and
                // lava-net checks. The accepted creature still uses that loop.
                var actor=new CodeInstruction(OpCodes.Ldarg_0);
                actor.labels.AddRange(instruction.labels); actor.blocks.AddRange(instruction.blocks);
                yield return actor;
                yield return CodeInstruction.Call(typeof(CompanionHooks),nameof(CatchCandidate));
                matches++;
            }
            if (matches!=1) throw new InvalidOperationException("Unexpected native critter candidate loop.");
        }

        private static bool CatchCandidate(NPC npc,Player player)
        {
            var actor=CurrentActor;
            return !ReferenceEquals(actor?.Body,player) || actor.TaskWorldHand?.Critter==null ? npc.active :
                host.TaskCritterAllowsNativeTarget(actor,npc);
        }

        private static void CatchObserved(int __0)
        {
            var actor=CurrentActor;
            if (actor?.TaskWorldHand?.Critter!=null) host.ObserveTaskCritterCaught(actor,__0);
        }
    }
}
