using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatPhoenixChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving,motor,controller,phoenix;
        private static bool natural;private static int births,arrows,solves,steps,kills;private static long first,birthTick;private static AttackContact known;private static Vector2 raw,killCenter;
        private static readonly List<int> hits=new List<int>();
        private static object Static(Type type,string method,params object[] args){return type.GetMethod(method,Flags).Invoke(null,args);}
        private static void SolveWork(){solves++;}private static void StepWork(){steps++;}
        private static void BeforeAI(Projectile __instance){if(natural && __instance.type==705)controller=__instance;}
        private static void AfterAI(Projectile __instance){if(ReferenceEquals(controller,__instance))controller=null;}
        private static void BeforeKill(Projectile __instance){if(ReferenceEquals(__instance,motor) && __instance.active){kills++;killCenter=__instance.Center;}}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}
        private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {if(receiving==null || receiving.type!=706 || receiving.owner!=Main.myPlayer || __result<=0)return;hits.Add(__instance.whoAmI);if(ReferenceEquals(__instance,target) && first<0)first=Main.GameUpdateCount;}
        private static Player Reset(object context)
        {
            natural=false;target=null;receiving=motor=controller=phoenix=null;first=-1;births=arrows=kills=0;known=null;hits.Clear();
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3854,0,0);p.inventory[54].SetDefaults(1334);p.inventory[54].stack=999;p.phantomPhoneixCounter=0;p.setApprenticeT3=false;Main.windPhysics=false;
            for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center,int width=18,int height=40)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=width;n.height=height;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;
            Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;
        }
        private static NpcTrajectory Timeline(NPC n)
        {
            var key=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide,ReflectsProjectiles=n.reflectsProjectiles};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Obstacles(Player p,Projectile q,NpcTrajectory path,bool before=true,bool next=false){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,before,next},null);}
        private static object Clock(NpcTrajectory path,string phase="BeforeNpc"){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),phase),0},null);}
        private static void Born(IEntitySource __0,int __result)
        {
            if(!natural || controller==null || __result<0 || __result>=Main.maxProjectiles)return;var q=Main.projectile[__result];if(q.type!=706){arrows++;return;}
            phoenix=q;births++;birthTick=Main.GameUpdateCount;var p=Main.LocalPlayer;var origin=p.RotatedRelativePoint(p.MountedCenter);var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;
            var expected=(Vector2)Static(model,"BirthVelocity",p,controller,origin,cursor);var ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"CaptureController",p,p.HeldItem,12f);
            Require(arrows==4 && p.phantomPhoneixCounter==0 && q.damage==2*(int)Get(ammo,"Damage") && q.timeLeft==120 && q.owner==p.whoAmI && Vector2.DistanceSquared(q.Center,origin)<.0001f && Vector2.DistanceSquared(q.velocity,expected)<.0001f && Vector2.Dot(q.velocity,raw-origin)<0,"actual third controller use emits doubled phoenix from the borrowed cursor after four original ammo arrows");
            var path=Timeline(target);bool later=q.whoAmI>controller.whoAmI;known=(AttackContact)Static(model,"Known",p,q,path,Clock(path,later?"Projectiles":"CompletedWorld"),Terrain(),Obstacles(p,q,path,false,!later));
            Console.WriteLine("PHOENIX birth parent="+controller.whoAmI+" slot="+q.whoAmI+" later="+later+" center="+q.Center+" V="+q.velocity+" damage="+q.damage+" actualAmmo="+arrows+" cursor="+cursor+" raw="+raw+" known="+known?.Tick);
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostPhoenixAttack",true);var audit=new Harmony("JueMingR.Tests.Phoenix");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("SolvePrepared",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPhoenixChecks),nameof(SolveWork)));audit.Patch(model.GetMethod("Step",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPhoenixChecks),nameof(StepWork)));
            audit.Patch(typeof(Projectile).GetMethod("AI",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPhoenixChecks),nameof(BeforeAI)),postfix:new HarmonyMethod(typeof(NativeCombatPhoenixChecks),nameof(AfterAI)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatPhoenixChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPhoenixChecks),nameof(BeforeKill)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPhoenixChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatPhoenixChecks),nameof(AfterDamage)));audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatPhoenixChecks),nameof(Strike)));
            try{string only=Environment.GetEnvironmentVariable("JMR_PHOENIX_ORACLE_ONLY");if(string.IsNullOrEmpty(only) || only=="motor")Motors(context);if(string.IsNullOrEmpty(only) || only=="receive")Receives(context);if(string.IsNullOrEmpty(only) || only=="natural")Natural(context);Require(string.IsNullOrEmpty(only) || only=="motor" || only=="receive" || only=="natural","native phoenix narrow leaf");}
            finally{natural=false;controller=null;audit.UnpatchAll(audit.Id);Main.windPhysics=wind;Main.LocalPlayer.setApprenticeT3=false;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Motors(object context)
        {
            int actions=0;for(int scene=0;scene<8;scene++)
            {
                var p=Reset(context);p.setApprenticeT3=scene==6;
                if(scene==1 || scene==2 || scene==3)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:3);}
                if(scene==4)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);
                var center=scene==7?new Vector2(10,350):new Vector2(920,350);var velocity=new Vector2(scene==7?-6:6,-.3f);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,706,0,0,p.whoAmI);var q=Main.projectile[slot];if(scene==5 || scene==6)q.timeLeft=1;
                var state=Activator.CreateInstance(model.GetNestedType("State",BindingFlags.NonPublic),Flags,null,new object[]{q,p.setApprenticeT3},null);var terrain=Terrain();motor=q;Vector2 anchor;int cw,ch;q.GetCollisionParams(out anchor,out cw,out ch);Require(q.width==66 && cw==10 && ch==10,"native phoenix damage and tile bodies are independent");
                for(int outer=1;outer<=32 && q.active;outer++)
                {
                    Require((bool)Static(model,"Step",state,terrain),"phoenix finite native terrain remains readable");Static(model,"Finish",state);q.Update(slot);actions++;var copy=(Projectile)Get(state,"Shot");
                    Console.WriteLine("PHOENIX motor scene="+scene+" action="+outer+" actual="+q.position+"/"+q.velocity+"/"+q.width+"/"+q.timeLeft+"/"+q.alpha+" model="+copy.position+"/"+copy.velocity+"/"+copy.width+"/"+copy.timeLeft+"/"+copy.alpha);
                    Require(q.active==copy.active && q.width==copy.width && q.height==copy.height && q.timeLeft==copy.timeLeft && q.alpha==copy.alpha && q.frame==copy.frame && q.frameCounter==copy.frameCounter && q.ai.SequenceEqual(copy.ai) && q.wet==copy.wet && q.honeyWet==copy.honeyWet && q.shimmerWet==copy.shimmerWet && Vector2.DistanceSquared(q.position,copy.position)<.002f && Vector2.DistanceSquared(q.velocity,copy.velocity)<.0001f,"native phoenix motor/lifetime/raw body agrees");
                    if((bool)Get(state,"Burst"))Require(kills==1 && Vector2.DistanceSquared(killCenter,(Vector2)Get(state,"BurstCenter"))<.0001f && q.width==(p.setApprenticeT3?208:96),"native Kill uses the captured burst center and actual equipment-dependent size");
                }
                if(scene==7)Require(kills==0 && q.timeLeft>0,"world retirement never invents a Kill burst");
            }
            Console.WriteLine("PASS PHOENIX motor=8 actions="+actions);
        }
        private static void Receives(object context)
        {
            for(int mode=0;mode<10;mode++)
            {
                var p=Reset(context);p.setApprenticeT3=mode==5;target=Target(4,new Vector2(mode==4 || mode==5?1020:mode==6?972:mode==9?990:950,350));NPC front=mode==7 || mode==9?Target(1,new Vector2(920,350)):null;
                if(mode==1)target.immune[p.whoAmI]=200;if(mode==8)target.immune[p.whoAmI]=5;if(mode==3)target.reflectsProjectiles=true;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(920,350),mode==9?new Vector2(6,0):Vector2.Zero,706,mode==7 || mode==9?1:50,0,p.whoAmI);var q=Main.projectile[slot];if(mode==2)q.localNPCImmunity[4]=-1;if(mode>=4 && mode<=6)q.timeLeft=1;
                NativeQuickItemChecks.BeginWorldStep();var path=Timeline(target);var lease=Obstacles(p,q,path);var contact=(AttackContact)Static(model,"Known",p,q,path,Clock(path),Terrain(),lease);long sample=path.SampleTick;
                for(int outer=1;outer<=12 && q.active && first<0;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();if(front!=null)front.UpdateNPC(1);target.UpdateNPC(4);q.Update(slot);}
                int actual=first<0?0:(int)(first-sample);bool negative=mode==1 || mode==2 || mode==3 || mode==4 || mode==9;
                Console.WriteLine("PHOENIX receive mode="+mode+" actual="+actual+" known="+contact?.Tick+" slots="+string.Join(",",hits)+" damage="+q.damage+" body="+q.width);
                Require((actual==0)==negative && (contact==null)==negative && (contact==null || contact.Tick==actual),"phoenix actual first positive receive agrees through immunity/reflection/burst/earlier-slot damage decay");
                if(mode==7)Require(hits.SequenceEqual(new[]{1,4}) && q.damage==0,"OnHit reaching zero does not interrupt the already-admitted Damage NPC loop");
                if(mode==9)Require(hits.SequenceEqual(new[]{1}) && q.damage==0,"zero damage blocks a subsequent Damage call when the moving body reaches the next receiver");
            }
            Console.WriteLine("PASS PHOENIX receive=10");
        }
        private static void Natural(object context)
        {
            var p=Reset(context);p.position=new Vector2(650,646);p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,48,40);raw=target.Center-new Vector2(490,0);var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");solves=steps=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++){NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);Require(GetOptional(attack,"ExpectedPreview")!=null && !Main.projectile.Any(q=>q.active),"idle Phoenix bow previews its original first ammo stage without firing");}
            natural=true;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));int thirdWindows=0,thirdVisible=0;
            for(int i=0;i<100 && first<0;i++)
            {
                NativeCombatSelectionPreviewChecks.Step(context,raw,true);
                if(p.phantomPhoneixCounter==2)
                {
                    thirdWindows++;var plan=(AttackContact)GetOptional(attack,"ExpectedPreview");if(plan!=null)thirdVisible++;Require(plan!=null,"third-shot cooldown retains a feasible current phoenix contact on the same target path");
                    int priorSolves=solves,priorSteps=steps;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;for(int d=0;d<20;d++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PreviewCurrent",plan),"same-update third-shot presentation stays valid");}Require(priorSolves==solves && priorSteps==steps,"twenty presentation calls perform zero Phoenix solve or motor work");
                }
            }
            Console.WriteLine("PHOENIX natural births="+births+" arrows="+arrows+" first="+(first<0?0:first-birthTick+1)+" known="+known?.Tick+" thirdPreview="+thirdVisible+"/"+thirdWindows+" solves="+solves+" steps="+steps);
            Require(births==1 && phoenix!=null && known!=null && first>=birthTick && first-birthTick+1==known.Tick && thirdWindows>0 && thirdVisible==thirdWindows,"actual third use has positive native Strike on the genuine488 and matches its actual-born contact with continuous cooldown preview");
            NativeCombatSelectionPreviewChecks.Step(context,raw,false);natural=false;
        }
    }
}
