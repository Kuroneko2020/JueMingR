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
    internal static class NativeCombatFiniteShotChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static NPC target;private static int currentType,expectedType;private static long firstHit;
        private static void BeforeDamage(Projectile __instance){currentType=__instance.type;}
        private static void AfterDamage(){currentType=0;}
        private static void Strike(NPC __instance,int __result)
        {if(currentType==expectedType && ReferenceEquals(target,__instance) && __result>0 && firstHit<0)firstHit=Main.GameUpdateCount;}
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");bool wind=Main.windPhysics;var audit=new Harmony("JueMingR.Tests.FiniteShots");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatFiniteShotChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatFiniteShotChecks),nameof(AfterDamage)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatFiniteShotChecks),nameof(Strike)));
            try{Motor(context,combat.GetType().Assembly);Natural(context,host,combat);NativeCombatMixedSwingChecks.Run(context);}
            finally{target=null;currentType=0;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static void Motor(object context,Assembly assembly)
        {
            var model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostFiniteShotAttack",true);var stateType=model.GetNestedType("State",BindingFlags.NonPublic);var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);
            int cases=0,actions=0,retired=0,reflection=0,settled=0;
            foreach(int type in new[]{114,116,173,24,261,183,118,119,359})foreach(int clock in type==114?new[]{0,19,90}:type==24 || type==183?new[]{0,5}:type==261?new[]{0,15}:new[]{0})foreach(int scene in Enumerable.Range(-1,11))
            {
                var player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),683,0,0);Main.windPhysics=false;foreach(var npc in Main.npc)npc.active=false;
                for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                if(scene>=0 && scene<=3)for(int x=30;x<115;x++)for(int y=10;y<80;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene);}
                if(scene==4)for(int y=10;y<80;y++){NativeToolsChecks.Tile(55,y,1);NativeToolsChecks.Tile(65,y,1);}
                if(scene>=5 && scene<=8){NativeToolsChecks.Tile(60,25,1);Main.tile[60,25].slope((byte)(scene-4));}
                if(scene==9)for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,25,19);
                Vector2 center=scene>=5 && scene<=8?new Vector2(936,390):new Vector2(950,350),velocity=scene>=5 && scene<=8?new Vector2(24,8):scene==9?new Vector2(.025f,8):new Vector2(12,.2f);
                int slot=Projectile.NewProjectile(player.GetProjectileSource_Item(player.HeldItem),center,velocity,type,0,0,player.whoAmI,clock);var shot=Main.projectile[slot];
                var state=Activator.CreateInstance(stateType,Flags,null,new object[]{shot,shot.Center,shot.velocity},null);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                for(int outer=1;outer<=150 && shot.active;outer++)
                {
                    int updates=shot.extraUpdates+1;
                    for(int sub=0;sub<updates && (bool)Get(state,"Alive");sub++)
                    {var args=new object[]{shot,terrain,state};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"finite hand motor retains terrain "+type+"/"+clock+"/"+scene+"/"+outer);state=args[2];var tail=new object[]{state};model.GetMethod("Finish",Flags).Invoke(null,tail);state=tail[0];actions++;}
                    shot.Update(slot);bool alive=(bool)Get(state,"Alive");
                    Require(shot.active==alive && shot.ai[0]==(float)Get(state,"Counter") && shot.timeLeft==(int)Get(state,"Left") && shot.ai[2]==(float)Get(state,"Bounces") && (!alive || Vector2.DistanceSquared(shot.position,(Vector2)Get(state,"Position"))<.002f && Vector2.DistanceSquared(shot.velocity,(Vector2)Get(state,"Velocity"))<.0001f && shot.wet==(bool)Get(state,"Wet") && shot.shimmerWet==(bool)Get(state,"Shimmer")),"original finite hand motion/liquid/bounce/clock: "+type+"/"+clock+"/"+scene+"/"+outer+" native="+shot.position+"/"+shot.velocity+"/"+shot.ai[0]+"/"+shot.ai[2]+"/"+shot.timeLeft+" model="+Get(state,"Position")+"/"+Get(state,"Velocity")+"/"+Get(state,"Counter")+"/"+Get(state,"Bounces")+"/"+Get(state,"Left"));
                    if(type==114)Require(Math.Abs(shot.localAI[0]-(float)Get(state,"StoredSpeed"))<.0001f,"shadowbeam keeps its independent fading speed clock after tile settlement");
                    if(type==116 && shot.ai[2]>0)reflection++;if(type==114 && shot.active && shot.velocity.Length()<.001f)settled++;
                }
                if(!shot.active)retired++;cases++;
            }
            Require(cases==154 && retired>0 && reflection>0 && settled>0,"native finite hand family exercises all gravity/damping clocks, actual finite reflection, settlement and retirements");
            Console.WriteLine("PASS original finite hand motors="+cases+" actions="+actions+" retired="+retired+" reflections="+reflection+" settlement="+settled);
        }
        private static void Natural(object context,object host,object combat)
        {
            var attack=Get(combat,"Attack");var failures=new List<string>();int cases=0,hits=0,negative=0;
            foreach(var row in new[]{new[]{683,114},new[]{723,116},new[]{989,173},new[]{161,24},new[]{1296,261},new[]{1130,183}})foreach(int scene in Enumerable.Range(0,row[1]==24 || row[1]==183?6:5))
            {
                try
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                    var player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),row[0],0,0);player.position=new Vector2(650,646);player.ResetEffects();player.ResetFallDamage();player.statMana=player.statManaMax=player.statManaMax2=300;player.releaseUseItem=true;Main.windPhysics=false;Main.screenPosition=new Vector2(500,400);
                    target=NativeCombatControlPreviewChecks.Dummy(2,scene==5?45:52,40);firstHit=-1;expectedType=row[1];
                    // Native one-penetration bodies ignore owner immunity.
                    // BeamSword/SpikyBall/Boulder multi-hit roles do not.
                    // Honey's far slow throws must retain their actual
                    // 120-update negative, plus a closer reachable control.
                    // Original Damage below, not preview absence, decides it.
                    bool blocked=scene==1 || scene==4 && row[1]!=114 && row[1]!=173 && row[1]!=183 || scene==3 && (row[1]==24 || row[1]==183);
                    if(scene==1)for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(46,y,1);
                    if(scene==2 || scene==3 || scene==5)for(int x=35;x<75;x++)for(int y=20;y<43;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==2?0:2);}
                    if(scene==4)target.immune[player.whoAmI]=200;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,25,true,false));AttackContact preview=null;
                    for(int frame=0;frame<3;frame++){NativeCombatSelectionPreviewChecks.Step(context,target.Center,false,true);preview=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(!Main.projectile.Any(q=>q.active),"finite held observation never launches an attack");}
                    Require(blocked?preview==null:preview!=null,"finite hand preview respects movement/wall/receiver: "+row[0]+"/"+scene);
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,25,true,true));long start=Main.GameUpdateCount;bool born=false;
                    for(int frame=1;frame<=120 && firstHit<0;frame++){NativeCombatSelectionPreviewChecks.Step(context,target.Center,frame==1);born|=Main.projectile.Any(q=>q.active && q.type==row[1] && q.owner==player.whoAmI);}
                    Require(born,"finite hand real normal birth is retained even without a solution");
                    Require(blocked?firstHit<0:firstHit-start==preview.Tick,"original finite hand first Damage agrees with idle preview: "+row[0]+"/"+scene+" native="+(firstHit<0?-1:firstHit-start)+" model="+preview?.Tick);
                    if(blocked)negative++;else hits++;cases++;Console.WriteLine("FINITE HAND item="+row[0]+" scene="+scene+" native="+(firstHit<0?-1:firstHit-start)+" model="+preview?.Tick);
                }
                catch(Exception error){failures.Add(row[0]+"/"+scene+": "+error.Message);Console.WriteLine("FINITE HAND FAIL "+error);}
            }
            Require(failures.Count==0,"natural finite hand failures="+string.Join(" | ",failures));Require(cases==32 && hits==21 && negative==11,"all finite beam/rolling hand members retain actual first-use, honey window and blocked controls");
            Console.WriteLine("PASS finite hand natural cases="+cases+" hits="+hits+" negative="+negative);
        }
    }
}
