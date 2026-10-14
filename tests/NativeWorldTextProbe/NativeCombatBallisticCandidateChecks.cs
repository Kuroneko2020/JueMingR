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
    // Real ItemCheck/Shoot/Update/Damage consumers in an isolated probe world.
    // Dry seed checks and private replay evidence are reported separately.
    internal static class NativeCombatBallisticCandidateChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static NPC target;private static Projectile receiving;
        private static int hits,births,replays,motors;private static Vector2 birthVelocity,cursor,raw;
        private static bool legacy;
        private static int generated,pathCount,pathAge;private static long generationStart;private static double generationMs;
        private static object currentContext;
        private static int enhancedHands,basicHands;private static bool aiming;
        private static int firstHitWidth,firstHitMember;private static long firstBirth,firstHit;
        private static readonly List<Projectile> sources=new List<Projectile>();
        private static object Static(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static void Replay(){replays++;}private static void Motor(){motors++;}
        private static bool LegacyCandidates(NpcTrajectory path,int age,float originX,float originY,ref AttackBallisticCandidates.Direction[] __result)
        {
            generationStart=System.Diagnostics.Stopwatch.GetTimestamp();pathCount=path.Count;pathAge=age;
            if(!legacy)return true;var rows=new List<AttackBallisticCandidates.Direction>();var constructor=typeof(AttackBallisticCandidates.Direction).GetConstructors(Flags).Single();
            // Reproduce only TASK_BASE's 9 direct + 32 sparse directions. The
            // exact host motor/receivers below are unchanged by this test hook.
            for(int i=0;i<41;i++){var box=path[Math.Min(path.Count-1,age+1+Math.Min(i,8)*4)].ProjectileReceiveBounds;var d=i<9?new Vector2(box.CenterX-originX,box.CenterY-originY).SafeNormalize(Vector2.UnitX):((i-9)*MathHelper.TwoPi/32).ToRotationVector2();rows.Add((AttackBallisticCandidates.Direction)constructor.Invoke(new object[]{(double)d.X,(double)d.Y,age+1,0d}));}
            __result=rows.ToArray();return false;
        }
        private static void Generated(AttackBallisticCandidates.Direction[] __result){generated=__result.Length;generationMs=(System.Diagnostics.Stopwatch.GetTimestamp()-generationStart)*1000d/System.Diagnostics.Stopwatch.Frequency;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(target,__instance) && receiving!=null && sources.Contains(receiving) && __result>0){hits++;if(firstHit<0){firstHit=Main.GameUpdateCount;firstHitWidth=receiving.width;firstHitMember=sources.IndexOf(receiving);}}}
        private static void Born(IEntitySource __0,int __result)
        {
            if(__result<0 || __0 is EntitySource_Parent parent && parent.Entity is Projectile)return;
            var q=Main.projectile[__result];if(q.type!=711 && q.type!=399 && q.type!=669)return;
            sources.Add(q);births++;if(aiming && (births-1)%(q.type==711?3:1)==0){if(Vector2.DistanceSquared(Main.MouseWorld,target.Center)>100){enhancedHands++;cursor=Main.MouseWorld;}else basicHands++;}
            if(births==1){firstBirth=Main.GameUpdateCount;birthVelocity=q.velocity;cursor=Main.MouseWorld;var attack=Get(Get(currentContext,"Combat"),"Attack");var host=Get(currentContext,"CombatObservation");var selection=Get(host,"Selection");var key=(NpcIdentity)Get(selection,"Target");int hint=(int)attack.GetType().GetMethods(Flags).Single(m=>m.Name=="ActionPreferred" && m.GetParameters().Length==1).Invoke(attack,new object[]{key});var p=Main.LocalPlayer;var a=Ammo(p);var model=assembly.GetType("JueMingR.TerrariaHost.Combat."+(q.type==711?"HostBetsyFlameAttack":"HostBottleAttack"));int direct=(int)Static(model,"PreferredRange",p,a,target);Console.WriteLine("BALLISTIC first-consumer path="+pathCount+" age="+pathAge+" seeds="+generated+" generation-ms="+generationMs+" replays="+replays+" motors="+motors+" currentHint="+hint+" directHint="+direct+" actionRequired="+Get(attack,"ActionRequired")+" key="+key.Slot+"/"+key.Type+"/"+key.Session+" session="+Get(host,"Session")+" origin="+p.RotatedRelativePoint(p.MountedCenter)+" target="+target.Center+" speed="+Get(a,"Speed"));}
        }
        private static Player Scene(object context,int item,int tileX,bool mirror=false,bool moving=false,bool wall=false,bool liquid=false,int tileY=40)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),item,0,0);p.position=new Vector2(mirror?1250:650,646);p.releaseUseItem=true;p.statMana=p.statManaMax2=200;Main.screenPosition=new Vector2(500,400);Main.windPhysics=false;
            if(item==3821){p.inventory[54].SetDefaults(353);p.inventory[54].stack=999;}else if(item==2590)p.HeldItem.stack=999;
            for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);
            target=NativeCombatControlPreviewChecks.Dummy(2,tileX,tileY);
            // A placed elevated dummy still receives vanilla gravity. Give
            // its real 2x3 tile object a two-tile support, rather than freezing
            // AI or mistaking a falling dummy for the stationary oracle.
            if(tileY!=40)for(int x=tileX;x<tileX+2;x++)NativeToolsChecks.Tile(x,tileY+3,1);
            if(moving){typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{2,(byte)0});target=Main.npc[2];target.SetDefaults(3);target.active=true;target.whoAmI=2;target.Center=new Vector2(tileX*16+16,660);target.target=0;target.life=target.lifeMax=10000;}
            if(wall)for(int y=37;y<43;y++)NativeToolsChecks.Tile(55,y,1);
            if(liquid)for(int x=48;x<51;x++)for(int y=38;y<43;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
            hits=births=replays=motors=enhancedHands=basicHands=firstHitWidth=0;firstHitMember=-1;firstHit=firstBirth=-1;aiming=false;sources.Clear();receiving=null;birthVelocity=cursor=Vector2.Zero;raw=target.Center-new Vector2(mirror?-500:500,0);return p;
        }
        private static NpcTrajectory Timeline(NPC n)
        {
            var key=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);
            var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=n.noTileCollide};
            var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Ammo(Player p){return Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);}
        private static AttackContact Solve(Player p,NpcTrajectory path)
        {
            var a=Ammo(p);int type=(int)Get(a,"Projectile");var shot=new Projectile();shot.SetDefaults(type);
            var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");
            var obstacles=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,shot,path,0,true,false},null);
            var model=assembly.GetType("JueMingR.TerrariaHost.Combat."+(type==711?"HostBetsyFlameAttack":"HostBottleAttack"),true);
            return (AttackContact)Static(model,"Solve",p,a,path,p.RotatedRelativePoint(p.MountedCenter),0,true,terrain,obstacles,0);
        }
        private static void Fire(object context,Vector2 point,bool aim)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),aim?new ObservationOptions(false,true,true,true,true,50,true,true):new ObservationOptions());
            aiming=aim;
            if(aim)for(int warm=0;warm<3;warm++)NativeCombatSelectionPreviewChecks.Step(context,point,false);
            NativeCombatSelectionPreviewChecks.Step(context,point,true);
            // Retain real native opportunities through a cold preparation.
            // Betsy holds its genuine autoReuse; bottles get ordinary repeat
            // clicks. Nothing delays an input or forces channel/timeLeft.
            for(int t=1;t<=120 && hits==0;t++)NativeCombatSelectionPreviewChecks.Step(context,point,aim && (Main.LocalPlayer.HeldItem.type==3870 || t%40==0));
            if(aim)NativeCombatSelectionPreviewChecks.Step(context,point,false);
            Console.WriteLine("BALLISTIC consumer-summary aim="+aim+" hands="+(enhancedHands+basicHands)+" enhanced="+enhancedHands+" basic="+basicHands+" sourceBirths="+births+" strikes="+hits+" firstReceive="+(firstHit<0?0:firstHit-firstBirth+1)+" firstWidth="+firstHitWidth+" firstMember="+firstHitMember);
        }
        internal static void Run(object context)
        {
            currentContext=context;assembly=Get(context,"Combat").GetType().Assembly;var audit=new Harmony("JueMingR.Tests.BallisticCandidates");bool wind=Main.windPhysics;
            audit.Patch(typeof(AttackBallisticCandidates).GetMethod("Generate"),prefix:new HarmonyMethod(typeof(NativeCombatBallisticCandidateChecks),nameof(LegacyCandidates)),postfix:new HarmonyMethod(typeof(NativeCombatBallisticCandidateChecks),nameof(Generated)));
            foreach(string model in new[]{"HostBetsyFlameAttack","HostBottleAttack"}){var type=assembly.GetType("JueMingR.TerrariaHost.Combat."+model,true);audit.Patch(type.GetMethod("Prepare",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBallisticCandidateChecks),nameof(Replay)));audit.Patch(type.GetMethod("Motor",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBallisticCandidateChecks),nameof(Motor)));}
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatBallisticCandidateChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBallisticCandidateChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatBallisticCandidateChecks),nameof(AfterDamage)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatBallisticCandidateChecks),nameof(Strike)));
            try
            {
                bool rejectedOld=false;
                foreach(int x in new[]{80,81,71,72,73})
                {
                    int y=x>=80?25:14;var p=Scene(context,3870,x,tileY:y);var path=Timeline(target);legacy=true;var old=Solve(p,path);legacy=false;int oldReplays=replays,oldMotors=motors;var next=Solve(p,path);
                    Console.WriteLine("BALLISTIC old-candidates x="+x+" y="+y+" old="+old?.Tick+" new="+next?.Tick+" oldReplays="+oldReplays+" oldMotors="+oldMotors+" newReplays="+(replays-oldReplays)+" newMotors="+(motors-oldMotors));
                    if(old!=null || next==null)continue;
                    Fire(context,new Vector2(next.AimX,next.AimY),false);Require(births==3 && hits>0,"between-grid elevated hand is actually reachable by original manual Shoot/Damage");p=Scene(context,3870,x,tileY:y);Fire(context,raw,true);Console.WriteLine("BALLISTIC repaired x="+x+" y="+y+" births="+births+" strikes="+hits+" v="+birthVelocity+" cursor="+cursor+" raw="+raw+" replays="+replays+" motors="+motors);Require(births>=3 && enhancedHands>0 && hits>0 && Vector2.DistanceSquared(cursor,raw)>100,"normal enhanced consumer repairs the old direction miss");rejectedOld=true;break;
                }
                Require(rejectedOld,"at least one reachable between-grid original hand rejects the old 9+32 candidate policy");
                foreach(int[] row in new[]{new[]{3870,45,0},new[]{3870,76,0},new[]{3870,39,1},new[]{2590,57,0},new[]{3821,76,0}})
                {
                    var p=Scene(context,row[0],row[1],row[2]!=0);var path=Timeline(target);
                    if(row[0]!=3870){legacy=true;var old=Solve(p,path);legacy=false;Console.WriteLine("BALLISTIC bottle-old item="+row[0]+" contact="+old?.Tick+" replays="+replays+" motors="+motors);replays=motors=0;}
                    var contact=Solve(p,path);Require(contact!=null,"dry finite replay finds a legal ballistic hand item="+row[0]+" x="+row[1]);
                    var origin=p.RotatedRelativePoint(p.MountedCenter);var a=Ammo(p);var dirs=AttackBallisticCandidates.Generate(path,0,origin.X,origin.Y,(float)Get(a,"Speed"),row[0]==3870?2:1,row[0]==3870,row[0]==3870?40*p.HeldItem.scale:0,10);
                    Require(dirs.Length>0 && dirs.Length<=8,"discrete inverse emits a finite set before family replay");int localReplays=replays,localMotors=motors;
                    // Aim disabled: only the real manual cursor supplies the
                    // proven reachable hand; no velocity/Hit/Contact injection.
                    Fire(context,new Vector2(contact.AimX,contact.AimY),false);Require(births==(row[0]==3870?3:1) && hits>0,"original manual Shoot/positive Damage proves the selected ballistic hand reachable");
                    Console.WriteLine("BALLISTIC manual item="+row[0]+" x="+row[1]+" mirror="+row[2]+" candidates="+dirs.Length+" exactReplays="+localReplays+" motors="+localMotors+" receiveTick="+contact.Tick+" births="+births+" strikes="+hits+" v="+birthVelocity+" cursor="+cursor);
                    p=Scene(context,row[0],row[1],row[2]!=0);Fire(context,raw,true);Console.WriteLine("BALLISTIC enhanced item="+row[0]+" x="+row[1]+" births="+births+" strikes="+hits+" v="+birthVelocity+" raw="+raw+" cursor="+cursor+" replays="+replays+" motors="+motors);
                    Require(births>0 && enhancedHands>0 && hits>0 && Vector2.DistanceSquared(cursor,raw)>100,"normal original input consumes the enhanced hand and actually receives");
                }
                foreach(string kind in new[]{"moving","wall","liquid"})
                {
                    var p=Scene(context,3870,76,false,kind=="moving",kind=="wall",kind=="liquid");
                    if(kind=="moving"){var manual=Solve(p,Timeline(target));Require(manual!=null,"moving manual preparation has a finite candidate");var startManual=target.Center;Fire(context,new Vector2(manual.AimX,manual.AimY),false);Require(hits>0 && target.Center!=startManual,"Aim-off original hand reaches the genuinely moving native AI receiver");Console.WriteLine("BALLISTIC moving-manual sourceBirths="+births+" strikes="+hits+" motion="+(target.Center-startManual));p=Scene(context,3870,76,moving:true);}
                    var start=target.Center;Fire(context,raw,true);
                    Console.WriteLine("BALLISTIC boundary kind="+kind+" births="+births+" strikes="+hits+" targetMotion="+(target.Center-start)+" v="+birthVelocity+" replays="+replays+" motors="+motors);
                    Require(births>=3 && enhancedHands>0 && hits>0,"natural movement / obstructed low arc / liquid representative retains a genuine receiver: "+kind);
                    if(kind=="moving")Require(Vector2.DistanceSquared(start,target.Center)>1,"natural NPC AI actually moves; no stationary future answer injected");
                }
                {var p=Scene(context,3870,111);var path=Timeline(target);Require(Solve(p,path)==null,"full dry family window supplies no fake contact beyond horizontal reach");Fire(context,raw,true);Require(births>=3 && enhancedHands==0 && basicHands>0 && hits==0,"unresolved far hand remains immediate original Basic and invents no damage");Console.WriteLine("BALLISTIC unreachable births="+births+" strikes="+hits+" cursor="+cursor);}
            }
            finally{legacy=false;audit.UnpatchAll(audit.Id);Main.windPhysics=wind;NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
    }
}
