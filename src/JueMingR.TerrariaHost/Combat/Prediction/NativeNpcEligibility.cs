using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Two negative leaf queries, not a second actor simulator. The observed
    // population remains conditional: Session latches changes before consuming
    // either a pending reply or a published window. All other accesses still
    // require the ordinary full page, including native slot allocation.
    internal static class NativeNpcEligibility
    {
        internal sealed class Premise
        {
            private readonly NPC actor;
            private readonly int slot,type,netId;
            private readonly byte generation;
            internal Premise(NPC npc){actor=npc;slot=npc.whoAmI;type=npc.type;netId=npc.netID;generation=npc.generation;}
            internal bool Current=>ReferenceEquals(Main.npc[slot],actor) && actor.generation==generation && actor.type==type && actor.netID==netId && Stable(actor);
            internal bool Owns(NPC npc)=>ReferenceEquals(actor,npc);
            internal int Slot=>slot;
        }
        private static readonly NPC[] candidates=new NPC[Main.maxNPCs+1];
        private static int allocationDepth;
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static bool Stable(NPC npc)
        {
            int type=npc.type;
            // Catchable protection, hostile conversions, shimmer transforms,
            // active attack states and reflected damage cannot be inferred from
            // friendly alone. AttackType is native capability data, no species
            // whitelist. No AI array is granted directory access by this check.
            return npc.active && type>0 && type<NPCID.Count && npc.friendly && npc.townNPC && npc.aiStyle==7 &&
                !Main.npcCatchable[type] && !NPCID.Sets.CritterThatCanTurnOnPlayers[type] && NPCID.Sets.ShimmerTransformToNPC[type]<0 &&
                NPCID.Sets.AttackType[type]<0 && SafeState(npc.ai[0]) && !npc.dryadWard;
        }
        private static bool SafeState(float state)=>!float.IsNaN(state) && !float.IsInfinity(state) && state!=10 && state!=12 && state!=13 && state!=14 && state!=15;
        internal static Premise[] Capture(int[] full)
        {
            var result=new List<Premise>();
            for(int slot=0;slot<Main.maxNPCs;slot++)if(Array.IndexOf(full,slot)<0 && Stable(Main.npc[slot]))result.Add(new Premise(Main.npc[slot]));
            return result.ToArray();
        }
        internal static bool Current(Premise[] values){foreach(var p in values)if(!p.Current)return false;return true;}
        internal static bool Owns(Premise[] values,NPC npc){foreach(var p in values)if(p.Owns(npc))return true;return false;}
        internal static bool Changed(Premise[] values,int slot){foreach(var p in values)if(p.Slot==slot && !p.Current)return true;return false;}
        internal static void Write(BinaryWriter writer)
        {for(int slot=0;slot<=Main.maxNPCs;slot++)writer.Write(slot<Main.maxNPCs && Stable(Main.npc[slot]));}
        internal static void Read(BinaryReader reader)
        {
            allocationDepth=0;
            for(int slot=0;slot<=Main.maxNPCs;slot++)
            {
                bool candidate=reader.ReadBoolean();NPC npc=Main.npc[slot];
                if(candidate && (slot==Main.maxNPCs || !npc.active || !npc.friendly || npc.aiStyle!=7))throw new InvalidDataException("NPC query premise conflicts with directory.");
                candidates[slot]=candidate?npc:null;
            }
        }
        internal static void Reset(){Array.Clear(candidates,0,candidates.Length);allocationDepth=0;}
        private static bool Candidate(NPC npc)=>npc!=null && npc.whoAmI>=0 && npc.whoAmI<Main.maxNPCs && ReferenceEquals(candidates[npc.whoAmI],npc) && NativeEntityDirectory.IsOpaque(npc);
        internal static bool AllocationNeedsPage(NPC npc)=>allocationDepth!=0 && Candidate(npc);
        internal static void Install(Harmony patches)
        {
            patches.Patch(typeof(NPC).GetMethod("CanBeChasedBy",Flags),prefix:Hook(nameof(ChaseBefore)));
            patches.Patch(typeof(Projectile).GetMethod("Damage_PVE",Flags),transpiler:Hook(nameof(DamageCandidates)));
            patches.Patch(typeof(NPC).GetMethod("GetAvailableNPCSlot",Flags),prefix:Hook(nameof(AllocationBefore)),finalizer:Hook(nameof(AllocationAfter)));
        }
        private static HarmonyMethod Hook(string name)=>new HarmonyMethod(typeof(NativeNpcEligibility).GetMethod(name,Flags));
        private static bool ChaseBefore(NPC __instance,ref bool __result)
        {if(!Candidate(__instance))return true;__result=false;return false;}
        private static void AllocationBefore(){allocationDepth++;}
        private static void AllocationAfter(){allocationDepth--;}
        private static bool NoHit(Projectile projectile,NPC npc)
        {
            // Damage_PVE_Inner's eligibility must return before collision and
            // damage for this exact conjunction. Hostile shots and native
            // friendly-fire exceptions keep full-page permission and replay.
            return Candidate(npc) && !projectile.hostile && projectile.type!=318 && npc.type!=22 && npc.type!=54 && !NPCID.Sets.ZappingJellyfish[npc.type];
        }
        private static IEnumerable<CodeInstruction> DamageCandidates(IEnumerable<CodeInstruction> input,MethodBase __originalMethod)
        {
            var code=input.ToList();
            if(__originalMethod.GetMethodBody().ExceptionHandlingClauses.Count!=0)throw new InvalidDataException("Native damage query exception layout changed.");
            var guard=typeof(NPC).Assembly.GetType(NativeEntityImage.GuardName,true);
            string token=((string)guard.GetField("OriginalFields").GetRawConstantValue()).Split('\n').Single(s=>s.EndsWith("|Terraria.NPC.active",StringComparison.Ordinal)).Split('|')[0];
            var accessor=typeof(NPC).GetMethod("__JmrField_"+token+"_0",Flags);int found=0;
            for(int i=1;i<code.Count-2;i++)if(code[i].Calls(accessor))
            {
                var load=code[i-1];var branch=code[i+1];
                if(!load.IsLdloc() || branch.opcode!=OpCodes.Brfalse && branch.opcode!=OpCodes.Brfalse_S || !(branch.operand is Label))throw new InvalidDataException("Native damage candidate loop changed.");
                var first=new CodeInstruction(OpCodes.Ldarg_0);first.labels.AddRange(code[i+2].labels);code[i+2].labels.Clear();
                code.InsertRange(i+2,new[]{first,new CodeInstruction(load.opcode,load.operand),new CodeInstruction(OpCodes.Call,typeof(NativeNpcEligibility).GetMethod(nameof(NoHit),Flags)),new CodeInstruction(OpCodes.Brtrue,branch.operand)});
                found++;i+=4;
            }
            if(found!=1)throw new InvalidDataException("Native damage candidate inventory changed.");
            return code;
        }
    }
}
