using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Companions
{
    internal static partial class CompanionHooks
    {
        [ThreadStatic] private static PredictionStep predictionStep;
        private sealed class PredictionStep
        {
            internal PredictionStep Previous;internal HostCompanions.Actor Actor;
            internal readonly Dictionary<int,NPC> Originals=new Dictionary<int,NPC>();
            internal void Restore()
            {
                foreach(var pair in Originals)
                {
                    Main.npc[pair.Key]=pair.Value;
                }
                predictionStep=Previous;
            }
        }
        private static void PredictionBefore(ref PredictionStep __state)
        {
            if(Main.netMode!=1 || current==null || current.Actor.Connection==null)return;
            __state=new PredictionStep{Previous=predictionStep,Actor=current.Actor};predictionStep=__state;
        }
        private static bool ProjectilePredictionBefore(Projectile __instance,ref NPC targetNPC,Rectangle projRectangle,ref PredictionStep __state)
        {
            if(Main.netMode!=1 || current==null || current.Actor.Connection==null)return true;
            if(host.PredictionDead(current.Actor,targetNPC))return false;
            var rectangle=targetNPC.getRect();if(targetNPC.type==414)rectangle.Inflate(8,8);
            if(!__instance.Colliding(projRectangle,rectangle))return true;
            PredictionBefore(ref __state);targetNPC=PredictionTargetNpc(targetNPC);return true;
        }
        private static bool DirectPredictionBefore(ref NPC npc,ref PredictionStep __state)
        {if(Main.netMode==1 && current!=null && host.PredictionDead(current.Actor,npc))return false;PredictionBefore(ref __state);if(__state!=null)npc=PredictionTargetNpc(npc);return true;}
        private static bool MeleePredictionBefore(int npcIndex,ref PredictionStep __state)
        {if(Main.netMode==1 && current!=null && host.PredictionDead(current.Actor,Main.npc[npcIndex]))return false;PredictionBefore(ref __state);return true;}
        private static Exception PredictionFinal(Exception __exception,PredictionStep __state)
        {__state?.Restore();return __exception;}
        private static NPC PredictionTargetNpc(NPC target)
        {
            var step=predictionStep;if(step==null)return target;
            int slot=target.whoAmI;if(!step.Originals.ContainsKey(slot))step.Originals.Add(slot,Main.npc[slot]);
            var view=host.PredictionNpc(step.Actor,target);Main.npc[slot]=view;
            if(target.realLife>=0 && target.realLife!=slot)
            {
                var root=Main.npc[target.realLife];if(!step.Originals.ContainsKey(root.whoAmI))step.Originals.Add(root.whoAmI,root);
                Main.npc[root.whoAmI]=host.PredictionNpc(step.Actor,root);
            }
            return view;
        }
        private static void PredictionPending(object __result)
        {if(predictionStep!=null && __result!=null)host.RegisterPendingStrike(predictionStep.Actor,__result);}
        private static IEnumerable<CodeInstruction> PredictionTarget(IEnumerable<CodeInstruction> input)
        {
            var code=input.ToList();int count=0;
            for(int i=code.Count-1;i>0;i--)
            {
                if((code[i].opcode!=OpCodes.Newobj && code[i].opcode!=OpCodes.Call) || !(code[i].operand is System.Reflection.ConstructorInfo ctor) || ctor.DeclaringType!=typeof(NPCKillAttempt))continue;
                var load=code[i-1];CodeInstruction store;
                if(load.opcode==OpCodes.Ldarg_2)store=new CodeInstruction(OpCodes.Starg_S,(byte)2);
                else if(load.opcode==OpCodes.Ldloc_0)store=new CodeInstruction(OpCodes.Stloc_0);
                else if(load.opcode==OpCodes.Ldloc_1)store=new CodeInstruction(OpCodes.Stloc_1);
                else if(load.opcode==OpCodes.Ldloc_2)store=new CodeInstruction(OpCodes.Stloc_2);
                else if(load.opcode==OpCodes.Ldloc_3)store=new CodeInstruction(OpCodes.Stloc_3);
                else if(load.opcode==OpCodes.Ldloc || load.opcode==OpCodes.Ldloc_S)store=new CodeInstruction(OpCodes.Stloc,load.operand);
                else throw new InvalidOperationException("Unsupported native hit target layout.");
                var first=new CodeInstruction(load.opcode,load.operand);
                // ProcessHitAgainstNPC has completed its native collision and
                // eligibility gates before ApplyRapidAttackBonus. Install the
                // same view before the first subsequent NPC OnHit mutation,
                // not merely before the later NPCKillAttempt constructor.
                int at=code.FindIndex(c=>c.Calls(AccessTools.Method(typeof(Player),"ApplyRapidAttackBonus")));
                if(at<0)throw new InvalidOperationException("Missing native melee hit boundary.");
                while(at>0 && code[at].opcode!=OpCodes.Ldarg_3)at--;
                if(at==0)throw new InvalidOperationException("Missing native melee damage argument.");
                first.labels.Clear();first.blocks.Clear();first.labels.AddRange(code[at].labels);first.blocks.AddRange(code[at].blocks);code[at].labels.Clear();code[at].blocks.Clear();
                code.InsertRange(at,new[]{first,new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(CompanionHooks),nameof(PredictionTargetNpc))),store});count++;
            }
            if(count!=1)throw new InvalidOperationException("Expected exactly one native hit attempt boundary.");return code;
        }
    }
}
