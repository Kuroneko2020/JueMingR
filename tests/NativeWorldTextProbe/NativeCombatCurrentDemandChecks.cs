using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Real multi-candidate native use, with the same NPC/item/settings across
    // long -> short -> long geometry. Observers never supply a demand/contact.
    internal static class NativeCombatCurrentDemandChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static object attack,host;private static int frame,handBorrows,prepareRange,npcSteps;private static bool pathOn;
        private static readonly List<string> rows=new List<string>();
        private static void Preparing(object __instance,int __3)
        {if(ReferenceEquals(__instance,host)){prepareRange=__3;npcSteps=NativeCombatUsageChecks.ObservedNpcSteps;}}
        private static void Borrowed(object __instance,bool __result)
        {if(ReferenceEquals(__instance,attack) && __result)handBorrows++;}
        private static void Recorded(object __instance)
        {
            if(!ReferenceEquals(__instance,attack))return;
            var input=Get(attack,"consumedInput");var id=(NpcIdentity)Get(input,"Target");
            var plan=((Array)Get(attack,"candidatePlans")).GetValue(id.Slot*2+1);
            var timeline=plan==null?null:(NpcTrajectory)GetOptional(plan,"Timeline");
            var contact=plan==null?null:(AttackContact)GetOptional(plan,"Contact");
            rows.Add(string.Join(",",frame,Main.GameUpdateCount,id.Slot,Get(input,"Kind"),((int[])Get(attack,"candidateDepths"))[id.Slot*2+1],((int[])Get(attack,"candidateRequests"))[id.Slot*2+1],timeline?.Count??0,contact?.ReceiveTick??-1,Get(attack,"ActionRequired"),prepareRange,NativeCombatUsageChecks.ObservedNpcSteps-npcSteps,handBorrows,pathOn));
        }
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");host=Get(context,"CombatObservation");attack=Get(combat,"Attack");var input=Get(context,"Input");
            rows.Clear();rows.Add("frame,tick,target,kind,historyDepth,requested,planCount,receiveTick,actionRequired,selectedPrepareRange,selectedNpcSteps,handBorrows,path");
            var audit=new Harmony("JueMingR.Tests.CurrentCandidateDemand");
            try
            {
                audit.Patch(host.GetType().GetMethod("PrepareSelectedPrediction",Flags),prefix:new HarmonyMethod(typeof(NativeCombatCurrentDemandChecks),nameof(Preparing)));
                audit.Patch(attack.GetType().GetMethod("TryCandidateHand",Flags),postfix:new HarmonyMethod(typeof(NativeCombatCurrentDemandChecks),nameof(Borrowed)));
                audit.Patch(attack.GetType().GetMethod("RecordInput",Flags),postfix:new HarmonyMethod(typeof(NativeCombatCurrentDemandChecks),nameof(Recorded)));
                foreach(bool path in new[]{false,true})
                {
                    pathOn=path;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;
                    var n=Scene(2,new Vector2(1160,667));Scene(3,new Vector2(1350,667));
                    var originalItem=p.HeldItem;var originalNpc=n;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,path,true,false,false,50,true,true));
                    int start=rows.Count;handBorrows=0;
                    using(NativeCombatUsageChecks.ObserveDrawSteps())
                    for(frame=0;frame<240;frame++)
                    {
                        if(frame==80)n.Center=new Vector2(870,667);
                        if(frame==160)n.Center=new Vector2(1160,667);
                        NativeCombatSelectionPreviewChecks.Step(context,new Vector2(650,550),frame%40<30);
                    }
                    Require(ReferenceEquals(n,originalNpc) && ReferenceEquals(p.HeldItem,originalItem),"Geometry changes retain both actual NPC and held item identities.");
                    var values=rows.Skip(start).Select(s=>s.Split(',')).ToArray();
                    Require(values.Any(v=>int.Parse(v[0])<80 && v[2]=="2" && int.Parse(v[6])>1 && int.Parse(v[7])>=0),"The unchanged target first completes a real longer-distance candidate plan before moving closer; a cold native Basic does not erase that completed exploration.");
                    var shortUses=values.Where(v=>int.Parse(v[0])>=80 && int.Parse(v[0])<160 && v[2]=="2" && v[3]=="Enhanced" && int.Parse(v[4])>int.Parse(v[6])-1).ToArray();
                    Console.WriteLine("CURRENT-DEMAND path="+path+" opportunities="+values.Length+" shorterFreshPlans="+shortUses.Length+" handBorrows="+handBorrows);
                    Require(shortUses.Length>0,"Same multi-candidate identity naturally retains a longer history while completing a shorter current Enhanced plan.");
                    foreach(var use in shortUses)
                        Require(int.Parse(use[8])==int.Parse(use[6])-1 && int.Parse(use[9])==int.Parse(use[8]),"Current native action prepares its complete solved prefix, rather than the historical maximum.");
                    Require(values.Any(v=>int.Parse(v[0])>=160 && v[3]=="Enhanced" && int.Parse(v[9])>int.Parse(shortUses.Last()[9])),"Moving the same target back out grows the real selected request again.");
                    Require(handBorrows>0,"Complete current prefixes preserve candidate hand donation and real Enhanced input.");
                }
            }
            finally
            {
                foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);
                string root=Path.Combine(Terraria.Program.SavePath,"current-demand");Directory.CreateDirectory(root);File.WriteAllLines(Path.Combine(root,"native-uses.csv"),rows);Console.WriteLine("CURRENT-DEMAND raw="+root);
                NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());attack=host=null;
            }
        }
        private static NPC Scene(int slot,Vector2 center)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.Center=center;n.aiStyle=-1;n.noGravity=true;n.velocity=Vector2.Zero;n.life=n.lifeMax=100000;Array.Clear(n.immune,0,n.immune.Length);return n;}
    }
}
