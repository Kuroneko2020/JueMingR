using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using JueMingR.Features.Combat;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatExplosiveChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static Projectile watched;private static NPC targetReceiver;private static bool killed;private static Vector2 killedAt;private static int blastWidth,blastHeight,targetHits;
        private static void BeforeKill(Projectile __instance){if(ReferenceEquals(watched,__instance) && __instance.active){killed=true;killedAt=__instance.Center;}}
        private static void BeforeDamage(Projectile __instance){if(ReferenceEquals(watched,__instance) && __instance.timeLeft==0){blastWidth=__instance.width;blastHeight=__instance.height;}}
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(targetReceiver,__instance) && __result>0)targetHits++;}
        internal static void Run(object context)
        {
            var audit=new Harmony("JueMingR.Tests.ExplosiveStages");
            audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatExplosiveChecks).GetMethod(nameof(BeforeKill),Flags)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatExplosiveChecks).GetMethod(nameof(BeforeDamage),Flags)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatExplosiveChecks).GetMethod(nameof(Strike),Flags)));
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks).GetMethod("SkipAchievement",Flags)));
            bool wind=Main.windPhysics,good=Main.getGoodWorld,traps=Main.noTrapsWorld,tenth=Main.tenthAnniversaryWorld;
            try
            {
                Main.windPhysics=Main.getGoodWorld=Main.noTrapsWorld=Main.tenthAnniversaryWorld=false;
                var failures=new List<string>();
                foreach(var group in new Action[]{()=>AcornGeometry(context),()=>Motor(context),()=>Natural(context)})
                {try{group();}catch(Exception error){failures.Add(error.Message);Console.WriteLine("EXPLOSIVE GROUP FAIL "+error);}}
                Require(failures.Count==0,"explosive independent groups: "+string.Join(" | ",failures));
            }
            finally{watched=null;targetReceiver=null;Main.windPhysics=wind;Main.getGoodWorld=good;Main.noTrapsWorld=traps;Main.tenthAnniversaryWorld=tenth;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());audit.UnpatchAll(audit.Id);}
        }
        private static void AcornGeometry(object context)
        {
            var combat=Get(context,"Combat");var input=Get(context,"Input");var terrain=Get(Get(combat,"Attack"),"terrain");
            NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,5629,0,0);
            var q=new Projectile();q.SetDefaults(1088);q.Center=new Vector2(968,630);
            var native=typeof(Projectile).GetMethod("AI_016_CanAcornPlant",Flags);int cases=0,plants=0;
            foreach(int ground in new[]{2,23,53,60,109,112,116,147,199,234,477,492,633,661,662,1})
            foreach(int variant in Enumerable.Range(0,8))
            {
                for(int y=37;y<=41;y++)Main.tile[60,y].ClearEverything();NativeToolsChecks.Tile(60,40,ground);
                if(variant==1)Main.tile[60,40].halfBrick(true);
                if(variant>=2 && variant<=5)Main.tile[60,40].slope((byte)(variant-1));
                if(variant==6)Main.tile[60,39].wall=350;
                if(variant==7)NativeToolsChecks.Tile(60,38,1);
                object[] original={Point.Zero};bool expected=(bool)native.Invoke(q,original);Call(terrain,"Reset");object[] args={q.Center,false};
                Require((bool)terrain.GetType().GetMethod("AcornPlant",Flags).Invoke(terrain,args) && (bool)args[1]==expected,"pure acorn retirement geometry equals actual CanAcornPlant ground="+ground+" variant="+variant);
                cases++;if(expected)plants++;
            }
            Require(plants==45,"all fifteen actual grounds plant on flat or lower slopes; upper slopes/half/protected/blocked cells do not");
            Console.WriteLine("PASS acorn native plant retirement geometry cases="+cases+" plantable="+plants);
        }
        private static void Motor(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");var assembly=attack.GetType().Assembly;
            var model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostExplosiveAttack",true);var stateType=model.GetNestedType("State",Flags);var advance=model.GetMethod("Step",Flags);var terrain=Get(attack,"terrain");int actions=0,bursts=0,reflections=0,cases=0;
            var failures=new List<string>();var types=Enumerable.Range(133,12).Concat(Enumerable.Range(338,4)).Concat(Enumerable.Range(776,26)).Concat(Enumerable.Range(803,8)).Concat(new[]{30,397,517,588,681,862,863,930,1088});
            foreach(int type in types)foreach(int scene in Enumerable.Range(0,6))
            {
                try
                {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,758,0,0);
                for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                for(int y=15;y<=45;y++)foreach(int x in new[]{40,80})NativeToolsChecks.Tile(x,y,1);
                for(int x=40;x<=80;x++)foreach(int y in new[]{15,45})NativeToolsChecks.Tile(x,y,1);
                if(scene==1 || scene==2)for(int x=41;x<80;x++)for(int y=16;y<45;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:2);}
                if(scene>=3 && scene<=4){NativeToolsChecks.Tile(60,25,1);Main.tile[60,25].slope((byte)(scene==3?1:4));}
                Main.getGoodWorld=Main.noTrapsWorld=scene==5;
                int slot=Projectile.NewProjectile(new EntitySource_DebugCommand(),scene>=3 && scene<=4?new Vector2(936,390):new Vector2(950,350),scene>=3 && scene<=4?new Vector2(24,12):new Vector2(6,3),type,0,0,0);var q=Main.projectile[slot];
                // This scalar matrix also enters the genuine final fuse. It
                // changes no AI/physics expectation and is separate from the
                // natural ItemCheck/outer-update user-flow checks below.
                if(scene==5)q.timeLeft=7;
                watched=q;killed=false;blastWidth=blastHeight=0;object state=Activator.CreateInstance(stateType,Flags,null,new object[]{q},null);Call(terrain,"Reset");
                for(int frame=0;frame<85 && q.active;frame++)
                {
                    Vector2 old=q.velocity;
                    for(int sub=0;sub<=q.extraUpdates && (bool)Get(state,"Alive");sub++){object[] args={q,terrain,state,null};Require((bool)advance.Invoke(null,args),"AI016 all finite terrain facts type="+type+" scene="+scene+" frame="+frame);state=args[2];}
                    q.Update(slot);actions++;
                    string detail=" type="+type+" scene="+scene+" frame="+frame+" predicted="+Get(state,"Center")+"/"+Get(state,"Velocity")+" native="+q.Center+"/"+q.velocity+" left="+Get(state,"Left")+"/"+q.timeLeft+" clock="+Get(state,"Clock")+"/"+q.ai[0];
                    Require((bool)Get(state,"Alive")==q.active,"AI016 original lifetime"+detail);
                    if(!q.active)
                    {
                        Require(killed && (bool)Get(state,"Burst")== (type!=1088) && Vector2.DistanceSquared(killedAt,(Vector2)Get(state,"Center"))<.001f,"AI016 original pre-visual Kill center"+detail);
                        if(type==1088){Require(blastWidth==0 && blastHeight==0,"acorn retires without explosive Damage");break;}
                        int expected=(int)model.GetMethod("BurstSide",Flags).Invoke(null,new object[]{type,Terraria.ID.ContentSamples.ProjectilesByType[type].width});Require(blastWidth==expected && blastHeight==expected,"AI016 actual Damage uses pre-visual explosion body "+type+" original="+blastWidth+"x"+blastHeight+" modeled="+expected);bursts++;break;
                    }
                    Require(Vector2.DistanceSquared(q.Center,(Vector2)Get(state,"Center"))<.001f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(state,"Velocity"))<.00001f && q.ai[0]==(float)Get(state,"Clock") && q.timeLeft==(int)Get(state,"Left"),"AI016 actual AI/movement/bounce/fuse"+detail);
                    if(old.X*q.velocity.X<0 || old.Y*q.velocity.Y<0)reflections++;
                }
                cases++;
                }
                catch(Exception error){failures.Add(type+"/"+scene+": "+error.Message);Console.WriteLine("EXPLOSIVE MOTOR FAIL "+type+"/"+scene+": "+error);}
            }
            Main.getGoodWorld=Main.noTrapsWorld=false;watched=null;
            Require(failures.Count==0,"AI016 native motor failures="+failures.Count+": "+string.Join(" | ",failures));
            Require(bursts>=58 && reflections>20,"explosive matrix reaches original explosions and reflections");Console.WriteLine("PASS AI016 native motors cases="+cases+" actions="+actions+" bursts="+bursts+" reflections="+reflections);
        }
        private static void Natural(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var input=Get(context,"Input");int completed=0;var failures=new List<string>();
            foreach(var row in new[]{new[]{168,0,30},new[]{2586,0,397},new[]{3116,0,517},new[]{3548,0,588},new[]{758,771,133},new[]{760,771,135},new[]{759,4445,776},new[]{758,4457,794},new[]{1946,771,338},new[]{1946,4458,809},new[]{5629,27,1088}})
            {
                try
                {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,row[0],0,0);p.position=new Vector2(700,646);p.ResetEffects();p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;p.releaseUseItem=true;Main.screenPosition=new Vector2(600,400);Main.rand=new Terraria.Utilities.UnifiedRandom(88143);
                targetReceiver=NativeCombatControlPreviewChecks.Dummy(2,52,40);targetHits=0;AttackContact preview=null;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,false));
                for(int i=0;i<3;i++){NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),false,true);preview=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(preview!=null && !Main.projectile.Any(q=>q.active),"explosive held idle preview before birth type="+row[2]);}
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));
                var births=new HashSet<int>();int first=0;
                for(int frame=1;frame<=120;frame++)
                {
                    NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,650),frame==1);
                    foreach(var q in Main.projectile.Where(q=>q.active && q.owner==0))births.Add(q.type);
                    if(targetHits>0){first=frame;break;}
                }
                Require(first>0,"explosive real first click reaches actual target Damage type="+row[2]+" visibleTick="+(preview==null?-1:preview.Tick)+" births="+string.Join(",",births));
                Console.WriteLine("EXPLOSIVE natural item="+row[0]+" ammo="+row[1]+" projectile="+row[2]+" idleTick="+preview.Tick+" firstDamage="+first);completed++;
                }
                catch(Exception error){failures.Add(row[0]+"/"+row[1]+": "+error.Message);Console.WriteLine("EXPLOSIVE NATURAL FAIL "+row[0]+"/"+row[1]+": "+error);}
            }
            Require(failures.Count==0,"explosive natural failures="+failures.Count+": "+string.Join(" | ",failures));
            Console.WriteLine("PASS explosive natural idle/first click/native Damage members="+completed);
        }
    }
}
