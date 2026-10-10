using System;
using System.Collections;
using System.Collections.Generic;
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
    internal static class NativeCombatCelebrationChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static NPC watched;private static int hits;private static readonly HashSet<int> bornStages=new HashSet<int>();
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(__instance,watched) && __result>0)hits++;}
        private static void Born(int __result)
        {if(__result>=0 && __result<Main.maxProjectiles){var q=Main.projectile[__result];if(q.owner==0 && q.aiStyle==147)bornStages.Add((int)q.ai[0]);}}
        internal static void Run(object context)
        {
            Motor(context);Cycle(context);
        }
        private static void Motor(object context)
        {
            var combat=Get(context,"Combat");var input=Get(context,"Input");var model=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostCelebrationAttack",true);
            var advance=model.GetMethod("Advance",Flags);var targets=Activator.CreateInstance(typeof(List<>).MakeGenericType(model.GetNestedType("Target",Flags)));int cases=0,steps=0;
            foreach(int type in new[]{715,717})foreach(int stage in Enumerable.Range(0,7))foreach(int direction in new[]{-1,1})foreach(int sibling in stage==4?new[]{0,1}:new[]{0})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3930,0,0);p.position=new Vector2(700,646);p.ResetEffects();
                for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();foreach(var n in Main.npc)n.active=false;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(900,500),new Vector2(direction*8,0),type,20,0,0,stage,sibling);var original=Main.projectile[slot];
                var shadow=new Projectile();shadow.SetDefaults(type);shadow.owner=0;shadow.Center=original.Center;shadow.velocity=original.velocity;shadow.ai=(float[])original.ai.Clone();shadow.localAI=(float[])original.localAI.Clone();
                for(int tick=0;tick<26;tick++)
                {
                    for(int sub=0;sub<shadow.MaxUpdates;sub++){advance.Invoke(null,new object[]{shadow,null,0,null,targets});shadow.position+=shadow.velocity;steps++;}
                    original.Update(slot);
                    Require(original.active && Vector2.DistanceSquared(original.Center,shadow.Center)<.001f && Vector2.DistanceSquared(original.velocity,shadow.velocity)<.001f && original.maxPenetrate==shadow.maxPenetrate && original.localAI[0]==shadow.localAI[0] && Math.Abs(original.scale-shadow.scale)<.0001f,"all seven native Celebration motors, gravity boundaries and twin helix agree: "+type+"/"+stage+"/"+direction+"/"+sibling+"/"+tick+" original="+original.Center+" shadow="+shadow.Center);
                }
                cases++;
            }
            Console.WriteLine("PASS Celebration native full projectile-update motors: cases="+cases+" subupdates="+steps+"; no autonomous targets in this scalar motor oracle");
        }
        private static void Cycle(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var attack=Get(combat,"Attack");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3930,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.releaseUseItem=true;p.inventory[54].SetDefaults(771);p.inventory[54].stack=999;Main.screenPosition=new Vector2(600,400);
            watched=NativeCombatControlPreviewChecks.Dummy(2,55,40);hits=0;bornStages.Clear();
            var audit=new Harmony("JueMingR.Tests.CelebrationCycle");var birth=typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float));
            audit.Patch(birth,postfix:new HarmonyMethod(typeof(NativeCombatCelebrationChecks).GetMethod(nameof(Born),Flags)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatCelebrationChecks).GetMethod(nameof(Strike),Flags)));
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));int visible=0;
                for(int tick=0;tick<80;tick++){NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true);if(GetOptional(attack,"ExpectedPreview")!=null)visible++;}
                Require(bornStages.SetEquals(Enumerable.Range(0,7)) && hits>0 && visible>0,"actual held ItemCheck/controller cycle creates every native stage, hits the real dummy and keeps useful preview");
                Console.WriteLine("PASS Celebration natural complete cycle: stages="+string.Join(",",bornStages.OrderBy(v=>v))+" actualDamage="+hits+" visible="+visible+"/80; native random spread remains Representative");
            }
            finally{watched=null;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
    }
}
