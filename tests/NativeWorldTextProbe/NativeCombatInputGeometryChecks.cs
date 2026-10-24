using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Bounded R2/R3 oracles. The old predicate/merge are isolated test patches;
    // preparation, native hand consumption, birth and Replay remain production.
    internal static class NativeCombatInputGeometryChecks
    {
        const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        static object attack;static Player watched;static bool legacyDirection,legacyOrder,displace;
        static int births,enhanced,basic,prepared,turned,replays,readyBirths;static uint readyTick;static Vector2 physical;
        static readonly List<string> facts=new List<string>();
        static void StrictDirection(object __instance,ref bool __result)
        {if(legacyDirection)__result&=(int)Get(__instance,"preparedDirection")==Main.LocalPlayer.direction;}
        static void BeforeFacing(Player __0)
        {
            if(!ReferenceEquals(__0,watched))return;
            var solution=Get(attack,"currentSolution");
            if(!(bool)Get(solution,"HasPoint") || Get(solution,"Reachability").ToString()!="Reachable")return;
            prepared++;readyTick=Main.GameUpdateCount;var point=(Vector2)Get(solution,"Point");
            facts.Add("prepared direction="+watched.direction+" point="+point+" target="+Main.npc[2].Center+" independent="+Get(solution,"DirectionIndependent"));
        }
        static void AfterFacing(Player __0)
        {
            if(!ReferenceEquals(__0,watched))return;
            if(watched.direction==-1)turned++;
            if(displace && prepared>0)watched.position.X+=16;
        }
        static void Born(int __result)
        {
            if(watched==null || __result<0 || __result>=Main.maxProjectiles)return;
            var p=Main.projectile[__result];if(p.owner!=watched.whoAmI)return;births++;
            string kind=(bool)Get(attack,"hasConsumedInput") && (uint)Get(attack,"consumedInputStep")==Main.GameUpdateCount?Get(Get(attack,"consumedInput"),"Kind").ToString():"Unbound";
            if(kind=="Enhanced")Require(Get(Get(attack,"consumedInput"),"PointSemantics").ToString()=="Direction","ordinary native birth consumes the adapter's direction meaning through final input");
            if(readyTick==Main.GameUpdateCount){readyBirths++;if(kind=="Enhanced")enhanced++;if(kind=="Basic")basic++;}
            facts.Add("birth type="+p.type+" kind="+kind+" direction="+watched.direction+" origin="+p.Center+" velocity="+p.velocity);
        }
        static void Replay(){replays++;}
        sealed class Row {internal Vector2 Direction;internal bool Center;internal float Speed;}
        // Exact pre-fix merge: finite original speed classes, real Launch gate,
        // dot threshold and stable first occurrence. New provenance is observed
        // only to identify which actual direction the old consumer selected.
        static List<Row> OldRows(Vector2 origin,AttackMotion motion,NpcTrajectory timeline,int age,bool deduplicate=true)
        {
            var rows=new List<Row>();float speed=motion.Speed;
            do
            {
                var branch=motion.WithSpeed(speed);
                if(AttackMotionCandidates.Supports(branch))foreach(var c in AttackMotionCandidates.Generate(timeline,age,origin.X,origin.Y,branch))
                {
                    var d=new Vector2(c.X,c.Y);float x=d.X*motion.Speed,y=d.Y*motion.Speed;motion.Launch(ref x,ref y);
                    if(Math.Abs((float)Math.Sqrt(x*x+y*y)-speed)>.002f)continue;
                    if(!deduplicate || !rows.Any(p=>Vector2.Dot(p.Direction,d)>.99999f))rows.Add(new Row{Direction=d,Center=c.IsCenter,Speed=speed});
                }
                if(!motion.ComponentBirthLimit || speed<16)break;speed*=.97f;
            }while(true);
            return rows;
        }
        static bool OldGenerate(Vector2 __0,AttackMotion __1,NpcTrajectory __2,int __3,ref Vector2[] __result)
        {if(!legacyOrder)return true;__result=OldRows(__0,__1,__2,__3).Select(r=>r.Direction).ToArray();return false;}
        internal static void Run(object context)
        {
            attack=Get(Get(context,"Combat"),"Attack");var assembly=attack.GetType().Assembly;var audit=new Harmony("JueMingR.Tests.InputGeometry20261024");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,F),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(attack.GetType().GetMethod("BalancedInputCurrent",F),postfix:new HarmonyMethod(typeof(NativeCombatInputGeometryChecks),nameof(StrictDirection)));
            audit.Patch(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatFacing",true).GetMethod("Apply",F),prefix:new HarmonyMethod(typeof(NativeCombatInputGeometryChecks),nameof(BeforeFacing)),postfix:new HarmonyMethod(typeof(NativeCombatInputGeometryChecks),nameof(AfterFacing)));
            audit.Patch(typeof(Projectile).GetMethods(F).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13),postfix:new HarmonyMethod(typeof(NativeCombatInputGeometryChecks),nameof(Born)));
            audit.Patch(assembly.GetType("JueMingR.TerrariaHost.Combat.HostMotionAttackInputs",true).GetMethod("Generate",F),prefix:new HarmonyMethod(typeof(NativeCombatInputGeometryChecks),nameof(OldGenerate)));
            audit.Patch(assembly.GetType("JueMingR.TerrariaHost.Combat.HostRepresentativeAttack",true).GetMethod("Replay",F),prefix:new HarmonyMethod(typeof(NativeCombatInputGeometryChecks),nameof(Replay)));
            try
            {
                var failures=new List<string>();
                void Check(string name,Action action)
                {try{action();}catch(Exception error){failures.Add(name+": "+error.GetBaseException().Message);Console.WriteLine("INPUT-GEOMETRY FAIL "+name+" "+error);}}
                Check("R3",()=>CrossSpeed(context));
                // The existing integration prefix had already unlocked this
                // independent display. Give the narrow fixture the same legal
                // warm path; a cold first-click Basic is not an R2 counterexample.
                if(!(bool)Get(Get(context,"CombatObservation"),"PathAccess"))NativeHiddenEntryChecks.RevealForComposition(context);
                Check("R2 old",()=>FacingScene(context,true,true,ItemID.SniperRifle,ItemID.MoonlordBullet));
                Check("R2 current",()=>FacingScene(context,false,true,ItemID.SniperRifle,ItemID.MoonlordBullet));
                Check("R2 facing-off",()=>FacingScene(context,false,false,ItemID.SniperRifle,ItemID.MoonlordBullet));
                Check("R2 ordinary",()=>FacingScene(context,false,true,ItemID.FlintlockPistol,ItemID.MusketBall));
                Check("R2 high-velocity",()=>FacingScene(context,false,true,ItemID.FlintlockPistol,ItemID.HighVelocityBullet));
                Check("R2 displacement",()=>FacingScene(context,false,true,ItemID.SniperRifle,ItemID.MoonlordBullet,true));
                Check("R2 sensitive",()=>FacingScene(context,false,true,3859,ItemID.WoodenArrow,false,true));
                Check("R2 off",()=>AimOff(context));
                Check("R2 quantization",()=>BirthBoundaries(context));
                Require(failures.Count==0,string.Join(" | ",failures));
                Console.WriteLine("PASS CombatInputGeometryCpu actual hand/facing and bounded multi-speed birth/Replay consumers");
            }
            finally
            {
                watched=null;legacyDirection=legacyOrder=displace=false;audit.UnpatchAll(audit.Id);
                NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            }
        }
        static void FacingScene(object context,bool old,bool facing,int weapon,int ammo,bool move=false,bool sensitive=false)
        {
            var combat=Get(context,"Combat");var observation=Get(context,"CombatObservation");
            NativeCombatObservationChecks.Save(observation,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            watched=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),weapon,0,0);
            foreach(var armor in watched.armor)armor.TurnToAir();watched.ResetEffects();watched.position=new Vector2(900,646);watched.velocity=Vector2.Zero;watched.direction=1;watched.releaseUseItem=true;
            watched.inventory[54].SetDefaults(ammo);watched.inventory[54].stack=999;
            foreach(var n in Main.npc)n.active=false;foreach(var p in Main.projectile)p.active=false;
            var target=Main.npc[2];target.SetDefaults(NPCID.Zombie);target.whoAmI=2;target.active=true;target.width=target.height=34;target.Center=watched.Center+new Vector2(-450,-70);target.velocity=new Vector2(-1,-.15f);target.aiStyle=-1;target.noGravity=target.noTileCollide=true;target.target=watched.whoAmI;target.life=target.lifeMax=100000;target.knockBackResist=0;
            Main.screenPosition=new Vector2(480.25f,400.75f);Main.GameViewMatrix.Zoom=Vector2.One;Main.windPhysics=false;Main.rand=new Terraria.Utilities.UnifiedRandom(7193);
            physical=watched.Center+new Vector2(240,-100);legacyDirection=old;displace=false;readyTick=uint.MaxValue;births=enhanced=basic=prepared=turned=readyBirths=0;facts.Clear();
            NativeCombatCadenceChecks.Save(combat,new CombatOptions(facing?32:0));
            NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,false,50,true,true));
            for(int i=0;i<32;i++)NativeCombatBalancedAimChecks.StepAt(context,physical,i>=20);
            // A cold first click may legally choose Basic while its finite
            // preparation warms. Finish/release that ORIGINAL hand, then test
            // the next natural press; no timer or Source result is supplied.
            int released=0;do{NativeCombatBalancedAimChecks.StepAt(context,physical,false);released++;}while((watched.itemAnimation>0 || watched.itemTime>0 || watched.reuseDelay>0) && released<120);
            Require(watched.itemAnimation==0 && watched.itemTime==0 && watched.reuseDelay==0,"native warm hand must really finish before the measured fresh press");
            watched.ChangeDir(1);displace=move;births=enhanced=basic=prepared=turned=readyBirths=0;readyTick=uint.MaxValue;facts.Clear();
            for(int i=0;i<4;i++)NativeCombatBalancedAimChecks.StepAt(context,physical,true);
            foreach(var fact in facts)Console.WriteLine("R2 "+fact);
            Console.WriteLine("R2 HAND old="+old+" facing="+facing+" moved="+move+" weapon="+weapon+" ammo="+ammo+" prepared="+prepared+" turned="+turned+" births="+births+" enhanced="+enhanced+" basic="+basic);
            Require(prepared>0 && readyBirths>0,"R2 requires an actually prepared reachable point and that same epoch's original natural hand birth");
            if(facing)Require(turned>0,"R2 must actually execute the installed Facing owner toward the left target");
            Require(old || move || sensitive?basic>0:enhanced>0,"pure direction change retains legal enhanced input; original predicate, sensitive birth and real displacement retain Basic fallback");
            watched=null;legacyDirection=displace=false;
        }
        static void AimOff(object context)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            watched=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),ItemID.SniperRifle,0,0);watched.inventory[54].SetDefaults(ItemID.MoonlordBullet);watched.inventory[54].stack=999;watched.releaseUseItem=true;
            births=enhanced=basic=prepared=0;physical=watched.Center+new Vector2(240,-100);
            for(int i=0;i<4;i++)NativeCombatBalancedAimChecks.StepAt(context,physical,i>=2);
            Require(births>0 && enhanced==0 && basic==0 && prepared==0,"Aim/Facing off keep original hand input and do no enhanced preparation");
            Console.WriteLine("R2 OFF births="+births+" enhanced="+enhanced+" basic="+basic+" prepared="+prepared);watched=null;
        }
        static NpcTrajectory Path(NpcIdentity identity,Vector2 center,int width,int height)
        {
            var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,new NpcMotionState{Identity=identity,X=center.X-width*.5f,Y=center.Y-height*.5f,Width=width,Height=height,Active=true,CanReceive=true})).ToArray();
            return new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);
        }
        static void BirthBoundaries(object context)
        {
            var host=attack.GetType().Assembly;var player=Main.LocalPlayer;
            var birth=host.GetType("JueMingR.TerrariaHost.Combat.HostThornAttack",true).GetMethods(F).Single(m=>m.Name=="BirthVelocity" && m.GetParameters().Length==5);
            // A floating nonzero aim can quantize to the exact muzzle. The
            // dependence is measured AFTER that same original quantization.
            Main.screenPosition=Vector2.Zero;player.gravDir=1;var origin=new Vector2(100,100);var args=new object[]{player,origin,origin+new Vector2(.3f,.4f),18f,null};birth.Invoke(null,args);
            Require((bool)args[4],"quantized-zero fallback remains direction-sensitive");args[2]=origin+new Vector2(1.3f,.4f);birth.Invoke(null,args);Require(!(bool)args[4],"nonzero native cursor delta is direction-independent");
            var solutionType=host.GetType("JueMingR.TerrariaHost.Combat.HostAttackSolution",true);Require(!(bool)Get(Activator.CreateInstance(solutionType),"DirectionIndependent"),"unknown/default/custom solution keeps strict direction qualification");
            Console.WriteLine("R2 BOUNDARY quantized-zero=strict nonzero=independent unknown-default=strict");
        }
        static void CrossSpeed(object context)
        {
            var player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),ItemID.SniperRifle,0,0);player.position=new Vector2(700,350);player.inventory[54].SetDefaults(ItemID.MoonlordBullet);player.inventory[54].stack=999;
            foreach(var n in Main.npc)n.active=false;foreach(var p in Main.projectile)p.active=false;
            for(int x=3;x<Main.maxTilesX-3;x++)for(int y=3;y<Main.maxTilesY-3;y++)Main.tile[x,y].ClearEverything();
            var host=attack.GetType().Assembly;var terrain=Get(attack,"terrain");Call(terrain,"Reset");
            var origin=(Vector2)host.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true).GetMethod("LaunchOrigin",F,null,new[]{typeof(Player),typeof(Item)},null).Invoke(null,new object[]{player,player.HeldItem});
            var motion=new AttackMotion(18,0,0,6,4,4,600,AttackConfidence.Representative,componentBirthLimit:true,maxFallSpeed:16);
            var identity=new NpcIdentity(1,new object(),2,1,1,1);bool found=false;int searches=0;
            foreach(float distance in new[]{220f,350f,500f,700f})foreach(float degrees in new[]{16.7f,20f,25f,30f})
            {
                searches++;var center=origin+MathHelper.ToRadians(degrees).ToRotationVector2()*distance;var path=Path(identity,center,150,100);var rows=OldRows(origin,motion,path,-1);
                if(rows.Count==0 || rows[0].Center || !rows.Any(r=>r.Center))continue;
                // Invoke the real representative consumer with a deterministic
                // ordinary birth. Its production Replay still decides contact.
                var clock=NativeCombatAimChecks.PhaseClock(attack,path,"BeforeNpc");
                Func<Vector2,Projectile> birth=aim=>
                {
                    var p=new Projectile();p.SetDefaults(638);p.active=true;p.owner=player.whoAmI;p.Center=origin;
                    p.velocity=(Vector2)host.GetType("JueMingR.TerrariaHost.Combat.HostThornAttack",true).GetMethod("BirthVelocity",F,null,new[]{typeof(Player),typeof(Vector2),typeof(Vector2),typeof(float)},null).Invoke(null,new object[]{player,origin,aim,18f});
                    while(p.velocity.X>=16 || p.velocity.X<=-16 || p.velocity.Y>=16 || p.velocity.Y< -16)p.velocity*=.97f;return p;
                };
                var prepare=host.GetType("JueMingR.TerrariaHost.Combat.HostRepresentativeAttack",true).GetMethod("PrepareMotion",F);
                legacyOrder=true;replays=0;var old=prepare.Invoke(null,new object[]{player,path,origin,clock,terrain,motion,birth,0});int oldReplays=replays;
                legacyOrder=false;replays=0;var current=prepare.Invoke(null,new object[]{player,path,origin,clock,terrain,motion,birth,0});int newReplays=replays;
                if(Get(old,"Reachability").ToString()!="Reachable" || Get(current,"Reachability").ToString()!="Reachable")continue;
                var oldDirection=((Vector2)Get(old,"Point")-origin).SafeNormalize(Vector2.UnitX);var newDirection=((Vector2)Get(current,"Point")-origin).SafeNormalize(Vector2.UnitX);
                var oldRow=rows.OrderBy(r=>Vector2.DistanceSquared(r.Direction,oldDirection)).First();var newRow=rows.OrderBy(r=>Vector2.DistanceSquared(r.Direction,newDirection)).First();
                if(oldRow.Center || !newRow.Center || oldRow.Speed<=newRow.Speed)continue;
                var born=birth((Vector2)Get(current,"Point"));
                Console.WriteLine("R3 FROZEN distance="+distance+" degrees="+degrees+" raw=18 box=150x100 candidates="+rows.Count+" oldCenter="+oldRow.Center+" oldSpeed="+oldRow.Speed+" newCenter="+newRow.Center+" newSpeed="+newRow.Speed+" oldReplays="+oldReplays+" newReplays="+newReplays+" oldPoint="+Get(old,"Point")+" newPoint="+Get(current,"Point")+" actualBirth="+born.velocity+" reach="+Get(current,"Reachability"));
                Require(Vector2.DistanceSquared(oldDirection,newDirection)>.000001f && Math.Abs(born.velocity.Length()-newRow.Speed)<.002f,"R3 final consumer selects a different real centre vector at its legal native birth speed");
                found=true;
                // Block the common centre ray with one actual tile. The same
                // producer/consumer must retain its legal rectangle-edge lane.
                int tx=(int)(origin.X+newDirection.X*distance*.5f)/16,ty=(int)(origin.Y+newDirection.Y*distance*.5f)/16;
                NativeToolsChecks.Tile(tx,ty,1);Call(terrain,"Reset");replays=0;
                var side=prepare.Invoke(null,new object[]{player,path,origin,clock,terrain,motion,birth,0});
                var sideDirection=((Vector2)Get(side,"Point")-origin).SafeNormalize(Vector2.UnitX);var sideRow=rows.OrderBy(r=>Vector2.DistanceSquared(r.Direction,sideDirection)).First();
                Console.WriteLine("R3 SIDE tile="+tx+","+ty+" centre="+sideRow.Center+" reach="+Get(side,"Reachability")+" replays="+replays+" point="+Get(side,"Point"));
                Require(Get(side,"Reachability").ToString()=="Reachable" && !sideRow.Center,"blocked centre preserves an actual reachable edge side lane");
                Main.tile[tx,ty].ClearEverything();Call(terrain,"Reset");
                var generate=host.GetType("JueMingR.TerrariaHost.Combat.HostMotionAttackInputs",true).GetMethod("Generate",F);
                var single=motion.WithSpeed(15);var expected=OldRows(origin,single,path,-1).Select(r=>r.Direction).ToArray();var actual=(Vector2[])generate.Invoke(null,new object[]{origin,single,path,-1});
                Require(expected.SequenceEqual(actual),"single speed preserves the actual prior candidate vectors/order");
                NearCoincidence(generate,origin,motion,identity);
                Console.WriteLine("R3 bounded-search-cases="+searches+" maximum=16 single-count="+actual.Length);
                return;
            }
            Require(found,"bounded 4x4 geometry search must witness old reachable edge/new reachable slower centre through actual PrepareMotion/Replay");
            Console.WriteLine("R3 bounded-search-cases="+searches+" maximum=16");
        }
        static void NearCoincidence(MethodInfo generate,Vector2 origin,AttackMotion motion,NpcIdentity identity)
        {
            motion=motion.WithUpdates(1,1);
            // Two finite receive windows straddle the real X=16 birth gate.
            // At substep12 the first rectangle's expanded top edge proposes
            // 27.3 degrees at speed18. At substep48 its 1px successor has a
            // 27.2-degree centre at speed18*.97. They share the old dot bucket,
            // but have distinct actual native launch classes and vectors.
            var edgeDirection=MathHelper.ToRadians(27.3f).ToRotationVector2();var centerDirection=MathHelper.ToRadians(27.2f).ToRotationVector2();
            var first=origin+edgeDirection*(18*12);var later=origin+centerDirection*(18*.97f*48);
            var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,new NpcMotionState{Identity=identity,X=0,Y=0,Width=1,Height=1,Active=true,CanReceive=false})).ToArray();
            points[11]=new NpcTrajectoryPoint(11,new NpcMotionState{Identity=identity,X=first.X-75,Y=first.Y+.95f,Width=150,Height=100,Active=true,CanReceive=true});
            points[47]=new NpcTrajectoryPoint(47,new NpcMotionState{Identity=identity,X=later.X-.5f,Y=later.Y-.5f,Width=1,Height=1,Active=true,CanReceive=true});
            var path=new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);
            var all=OldRows(origin,motion,path,-1,false);var old=OldRows(origin,motion,path,-1);
            foreach(var row in all)Console.WriteLine("R3 NEAR ROW center="+row.Center+" speed="+row.Speed+" direction="+row.Direction);
            var edge=old.FirstOrDefault(r=>!r.Center && Vector2.Dot(r.Direction,edgeDirection)>.9999999f);
            var center=all.FirstOrDefault(r=>r.Center && Vector2.Dot(r.Direction,centerDirection)>.9999999f && Math.Abs(r.Speed-17.46f)<.002f);
            Require(edge!=null && center!=null && Vector2.Dot(edge.Direction,center.Direction)>.99999f,"both actual inverse-region proposals cross the intended birth/dedup boundary");
            var actual=(Vector2[])generate.Invoke(null,new object[]{origin,motion,path,-1});
            Require(actual.Any(d=>Vector2.DistanceSquared(d,center.Direction)<.000000000001f) && !actual.Any(d=>d==edge.Direction),"promoted near-coincident row must retain the true centre vector and replace the old edge");
            Console.WriteLine("R3 NEAR windows=2 oldEdge="+edge.Direction+" trueCenter="+center.Direction+" dot="+Vector2.Dot(edge.Direction,center.Direction)+" oldCount="+old.Count+" newCount="+actual.Length);
        }
    }
}
