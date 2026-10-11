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
    internal static class NativeCombatStreamChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving;
        private static bool natural;private static long first,birthTick;private static int births,solves,steps;private static AttackContact known;private static Vector2 raw;
        private static readonly List<int> hits=new List<int>();
        private static object Static(Type type,string method,params object[] args){return type.GetMethod(method,Flags).Invoke(null,args);}
        private static void SolveWork(){solves++;}private static void StepWork(){steps++;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {if(receiving==null || (receiving.type!=22 && receiving.type!=280) || __result<=0)return;hits.Add(__instance.whoAmI);if(ReferenceEquals(__instance,target) && first<0)first=Main.GameUpdateCount;}
        private static Player Reset(object context,int item=157)
        {
            natural=false;target=null;receiving=null;first=-1;births=0;known=null;hits.Clear();
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),item,0,0);Main.windPhysics=false;
            for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=18;n.height=40;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;
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
            if(!natural || __result<0 || __result>=Main.maxProjectiles)return;var q=Main.projectile[__result];if(q.type!=22 && q.type!=280)return;
            births++;if(births!=1)return;birthTick=Main.GameUpdateCount;var p=Main.LocalPlayer;var origin=p.RotatedRelativePoint(p.MountedCenter);var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;
            var expected=(Vector2)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostThornAttack",true),"BirthVelocity",p,origin,cursor,p.HeldItem.shootSpeed);
            Require(Vector2.DistanceSquared(q.Center,origin)<.0001f && Vector2.DistanceSquared(q.velocity,expected)<.0001f && Vector2.Dot(q.velocity,raw-origin)<0,"actual stream hand birth reads the borrowed cursor rather than the opposite physical mouse");
            if(q.type==22)Require(q.ai[2]>=0 && q.ai[2]<20000,"original water stream carries its real randomly selected lava seed");
            var path=Timeline(target);known=(AttackContact)Static(model,"Known",p,q,path,Clock(path),Terrain(),Obstacles(p,q,path));
            Console.WriteLine("STREAM birth type="+q.type+" center="+q.Center+" velocity="+q.velocity+" seed="+q.ai[2]+" cursor="+cursor+" raw="+raw+" known="+known?.Tick);
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostStreamAttack",true);var audit=new Harmony("JueMingR.Tests.Stream");bool wind=Main.windPhysics,remix=Main.remixWorld;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatStreamChecks),nameof(SolveWork)));audit.Patch(model.GetMethod("Step",Flags),prefix:new HarmonyMethod(typeof(NativeCombatStreamChecks),nameof(StepWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatStreamChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatStreamChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatStreamChecks),nameof(AfterDamage)));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatStreamChecks),nameof(Strike)));
            try{string leaf=Environment.GetEnvironmentVariable("JMR_STREAM_ORACLE_ONLY");Require(string.IsNullOrEmpty(leaf) || leaf=="motor" || leaf=="receive" || leaf=="natural","known stream leaf");if(string.IsNullOrEmpty(leaf) || leaf=="motor")Motors(context);if(string.IsNullOrEmpty(leaf) || leaf=="receive")Receives(context);if(string.IsNullOrEmpty(leaf) || leaf=="natural"){Natural(context,157);Natural(context,1336);}}
            finally{natural=false;audit.UnpatchAll(audit.Id);Main.windPhysics=wind;Main.remixWorld=remix;}
        }
        private static void Motors(object context)
        {
            int actions=0;
            for(int scene=0;scene<12;scene++)
            {
                var p=Reset(context);Main.remixWorld=scene==9;int type=scene==1 || scene==3 || scene==5 || scene==11?280:22;
                if(scene>=4 && scene<=9)for(int x=40;x<100;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==4 || scene==5?0:scene==6?2:scene==7?3:1);}
                if(scene==2 || scene==3)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);
                var center=scene==10?new Vector2(8,350):new Vector2(920,350);var velocity=new Vector2(scene==10?-6:6,scene>=8 && scene<=9?1:-.3f);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,type,0,0,p.whoAmI,0,0,scene==9?19999:12345);var q=Main.projectile[slot];if(scene==11)q.timeLeft=1;
                var copy=(Projectile)Static(model,"Copy",q);var terrain=Terrain();Vector2 anchor;int cw,ch;q.GetCollisionParams(out anchor,out cw,out ch);Require(q.width==(type==22?18:32) && cw==(type==22?18:10),"stream full Damage body differs from ichor's ten-pixel tile body");
                for(int outer=1;outer<=40 && q.active;outer++)
                {
                    int updates=copy.extraUpdates+1;for(int sub=0;sub<updates && copy.active;sub++){Require((bool)Static(model,"Step",copy,terrain),"stream terrain lease remains complete");Static(model,"Finish",copy);}q.Update(slot);actions++;
                    Console.WriteLine("STREAM motor scene="+scene+" action="+outer+" actual="+q.position+"/"+q.velocity+" scale="+q.scale+" left="+q.timeLeft+" a="+q.ai[0]+","+q.ai[1]+" extra="+q.extraUpdates+" model="+copy.position+"/"+copy.velocity+" scale="+copy.scale+" left="+copy.timeLeft+" a="+copy.ai[0]+","+copy.ai[1]+" extra="+copy.extraUpdates);
                    Require(q.active==copy.active && q.timeLeft==copy.timeLeft && q.extraUpdates==copy.extraUpdates && q.penetrate==copy.penetrate && q.ai.SequenceEqual(copy.ai) && Math.Abs(q.scale-copy.scale)<.000001f && (!q.active || Vector2.DistanceSquared(q.position,copy.position)<.002f && Vector2.DistanceSquared(q.velocity,copy.velocity)<.00001f),"stream live movement, native gravity/seed phase and damage lifetime agree");
                }
                if(scene==8 || scene==9)Require(q.ai[1]==1 && q.extraUpdates==0 && q.penetrate==-1 && q.usesLocalNPCImmunity==Main.remixWorld,"lava seed transition retains remaining subactions then adopts the real world immunity variant");
                if(scene==10)Require(q.timeLeft>0,"world boundary retires without Kill");
            }
            Main.remixWorld=false;Console.WriteLine("PASS STREAM motor=12 actions="+actions);
        }
        private static void Receives(object context)
        {
            for(int mode=0;mode<10;mode++)
            {
                var p=Reset(context);Main.remixWorld=mode==2;int type=mode==8 || mode==9?280:22;target=Target(6,new Vector2(940,350));
                if(mode==1)target.immune[p.whoAmI]=200;if(mode==3)target.reflectsProjectiles=true;if(mode==4 || mode==5)for(int i=0;i<(mode==4?4:5);i++)Target(i,new Vector2(940,350));
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(940,350),Vector2.Zero,type,50,0,p.whoAmI);var q=Main.projectile[slot];if(mode==2)q.localNPCImmunity[6]=-1;if(mode==6 || mode==9)q.scale=type==22?.005f:.001f;if(mode==7)q.timeLeft=1;
                NativeQuickItemChecks.BeginWorldStep();var path=Timeline(target);var contact=(AttackContact)Static(model,"Known",p,q,path,Clock(path),Terrain(),Obstacles(p,q,path));long sample=path.SampleTick;
                for(int outer=1;outer<=40 && q.active && first<0;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active)Main.npc[i].UpdateNPC(i);q.Update(slot);}
                int actual=first<0?0:(int)(first-sample);bool negative=mode==1 || mode==2 || mode==5 || mode==6 || mode==9;
                Console.WriteLine("STREAM receive mode="+mode+" actual="+actual+" known="+contact?.Tick+" slots="+string.Join(",",hits));
                Require((actual==0)==negative && (contact==null)==negative && (contact==null || contact.Tick==actual),"native stream alpha permission, ordered penetration, local/owner immunity and scale retirement match");
                if(mode==4)Require(hits.SequenceEqual(new[]{0,1,2,3,6}),"the fifth qualified receiver remains a positive hit");
            }
            Main.remixWorld=false;Console.WriteLine("PASS STREAM receive=10");
        }
        private static void Natural(object context,int item)
        {
            var p=Reset(context,item);Main.remixWorld=false;p.position=new Vector2(650,646);p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,48,40);raw=target.Center-new Vector2(490,0);var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");solves=steps=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++){NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);Require(GetOptional(attack,"ExpectedPreview")!=null && !Main.projectile.Any(q=>q.active),"idle stream preview works independently of attack permission");}
            natural=true;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));int visible=0;
            for(int i=0;i<24;i++)
            {
                NativeCombatSelectionPreviewChecks.Step(context,raw,true);var plan=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(plan!=null,"feasible continuous stream use retains the next native attack preview");visible++;
                int a=solves,b=steps;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;for(int j=0;j<20;j++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PreviewCurrent",plan),"same-update CPU stream presentation stays current");}Require(a==solves && b==steps,"CPU presentation does not solve or replay streams");
            }
            Console.WriteLine("STREAM natural item="+item+" births="+births+" first="+(first<0?0:first-birthTick+1)+" known="+known?.Tick+" preview="+visible+"/24 solves="+solves+" steps="+steps);
            Require(births>1 && known!=null && first>=birthTick && first-birthTick+1==known.Tick,"actual stream first positive Strike matches its born flight and continuous real cadence");
            NativeCombatSelectionPreviewChecks.Step(context,raw,false);natural=false;
        }
    }
}
