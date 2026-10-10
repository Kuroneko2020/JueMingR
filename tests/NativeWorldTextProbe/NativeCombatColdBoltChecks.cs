using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatColdBoltChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static NPC target;private static long firstHit;
        private static void Strike(NPC __instance,int __result)
        {if(ReferenceEquals(target,__instance) && __result>0 && firstHit<0)firstHit=Main.GameUpdateCount;}
        internal static void Run(object context)
        {
            var audit=new Harmony("JueMingR.Tests.ColdBolts");
            // The isolated original Player.Update has no platform achievement
            // service. Suppress only its outbound notifications, as in the
            // other natural item-use checks; preserve biomes and combat.
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatColdBoltChecks).GetMethod(nameof(Strike),Flags)));
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            bool wind=Main.windPhysics;var failures=new List<string>();int cases=0,hits=0,negative=0;
            try
            {
                foreach(var row in new[]{new[]{676,119},new[]{724,118},new[]{726,359}})foreach(int scene in Enumerable.Range(0,6))
                {
                    try
                    {
                        NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                        var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,row[0],0,0);p.position=new Vector2(650,646);p.ResetFallDamage();p.ResetEffects();p.releaseUseItem=true;p.statMana=p.statManaMax=p.statManaMax2=200;Main.windPhysics=false;Main.screenPosition=new Vector2(500,400);
                        target=NativeCombatControlPreviewChecks.Dummy(2,60,40);firstHit=-1;
                        if(scene==1 || scene==2)for(int x=47;x<=70;x++)for(int y=30;y<43;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:2);}
                        if(scene==3)for(int y=30;y<43;y++)NativeToolsChecks.Tile(54,y,1);
                        if(scene==4)NativeCombatControlPreviewChecks.Dummy(1,52,40);
                        if(scene==5 && row[1]==119)Projectile.perIDStaticNPCImmunity[Terraria.ID.ContentSamples.ProjectilesByType[119].immunityIdentity,2]=Main.GameUpdateCount+100;
                        var mouse=target.Center;
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,25,true,false));AttackContact preview=null;
                        for(int i=0;i<3;i++){NativeCombatSelectionPreviewChecks.Step(context,mouse,false,true);preview=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(!Main.projectile.Any(q=>q.active),"cold bolt idle observation does not launch");}
                        bool blocked=scene==3 || scene==4 && row[1]==118 || scene==5 && row[1]==119;
                        Require(blocked?preview==null:preview!=null,"cold bolt actual held-stage feasibility: item="+row[0]+" scene="+scene+" preview="+preview?.Tick);
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,25,true,true));long start=Main.GameUpdateCount;bool born=false;
                        // Honey can move a valid contact beyond tick 90. Run
                        // the whole advertised 120-update prediction window.
                        for(int frame=1;frame<=120 && firstHit<0;frame++)
                        {
                            NativeCombatSelectionPreviewChecks.Step(context,mouse,frame==1);
                            born|=Main.projectile.Any(q=>q.active && q.owner==p.whoAmI && q.type==row[1]);
                        }
                        Require(born,"cold bolt keeps the original hand birth even when the selected target is blocked");
                        Require(blocked?firstHit<0:firstHit-start==preview.Tick,"cold bolt actual first Damage matches idle contact: item="+row[0]+" scene="+scene+" native="+(firstHit<0?-1:firstHit-start)+" predicted="+preview?.Tick);
                        if(blocked)negative++;else hits++;cases++;
                        Console.WriteLine("COLD BOLT item="+row[0]+" scene="+scene+" native="+(firstHit<0?-1:firstHit-start)+" preview="+preview?.Tick);
                    }
                    catch(Exception error){failures.Add(row[0]+"/"+scene+": "+error.Message);Console.WriteLine("COLD BOLT FAIL "+error);}
                }
                Require(failures.Count==0,"cold bolt natural failures="+string.Join(" | ",failures));
                Require(cases==18 && hits==13 && negative==5,"all explicit AI028 held weapons retain dry/liquid, obstruction, receiver and static-immunity cases");
                Console.WriteLine("PASS cold bolt original first-use cases="+cases+" damage="+hits+" negative="+negative);
            }
            finally{target=null;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
    }
}
