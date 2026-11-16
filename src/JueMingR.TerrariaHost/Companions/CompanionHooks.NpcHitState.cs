using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal static partial class CompanionHooks
    {
        private sealed class NpcBirth
        {internal int Slot;internal byte Old,Next;internal NpcBirth Previous;}
        [ThreadStatic] private static NpcBirth primaryBirth;
        private static void NpcBirthBefore(int slot,byte generation,ref NpcBirth __state)
        {
            if(host==null || Main.netMode!=1 || !host.HasActors || host.ReadingObservation)return;
            __state=new NpcBirth{Slot=slot,Old=Main.npc[slot].generation,Next=generation,Previous=primaryBirth};primaryBirth=__state;
        }
        private static Exception NpcBirthFinal(Exception __exception,NpcBirth __state)
        {if(__state!=null)primaryBirth=__state.Previous;return __exception;}
        private static bool PrimaryProjectileSlotReset(int npcIndex)
        {
            var birth=primaryBirth;bool replacement=birth!=null && birth.Slot==npcIndex;
            byte generation=Main.npc[npcIndex].generation;
            return !(host?.ResetPrimaryProjectileNpcHitSlot(npcIndex,replacement?birth.Old:generation,replacement?birth.Next:generation,replacement)??false);
        }
        private static bool PrimaryPlayerSlotReset(int npcIndex)
        {
            var birth=primaryBirth;bool replacement=birth!=null && birth.Slot==npcIndex;
            byte generation=Main.npc[npcIndex].generation;
            return !(host?.ResetPrimaryPlayerNpcHitSlot(npcIndex,replacement?birth.Old:generation,replacement?birth.Next:generation,replacement)??false);
        }
        private static void NpcStaticImmunitySet(uint[,] matrix,int type,int slot,uint value)
        {if(host==null)matrix[type,slot]=value;else host.SetNpcStaticHitImmunity(matrix,type,slot,value);}
        private static IEnumerable<CodeInstruction> NpcStaticImmunityWrites(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        {
            var code=instructions.ToList();int count=0;
            foreach(var instruction in code)
            {
                var method=instruction.operand as MethodInfo;
                if(method==null || method.DeclaringType!=typeof(uint[,]) || method.Name!="Set")continue;
                instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(CompanionHooks),nameof(NpcStaticImmunitySet));count++;
            }
            int expected=__originalMethod.Name=="Damage_PVE_Inner"?2:1;
            if(count!=expected)throw new InvalidOperationException("companion NPC static immunity ABI: "+__originalMethod.Name+" expected "+expected+" writes, found "+count);
            // Only the four actual native writes are mirrored. Reads remain
            // vanilla and every actor borrows the one observed-incarnation
            // matrix, so same-generation human/AI hits still share cooldown.
            return code;
        }
    }
}
