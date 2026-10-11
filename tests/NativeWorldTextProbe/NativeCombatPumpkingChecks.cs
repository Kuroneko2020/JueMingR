using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatPumpkingChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static Type Model {get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcPumpkingMotion",true);}}
        internal static void Boundaries()
        {
            var known=Model.GetMethod("Known",Flags);var action=Model.GetMethod("Step",Flags);
            var body=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,327,327),Style=58,Active=true,A0=1,L2=300,A1=2};
            Require((bool)known.Invoke(null,new object[]{body}),"Actual AI58 member has its motor.");
            object[] args={body,new PredictionEnvironment(),null,1,PredictionStop.None,null,0,false};
            Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.RandomDecision,"Server mode choice stops before RNG even in the retreat phase.");
            args[0]=body;args[1]=new PredictionEnvironment{Day=true};Require((bool)action.Invoke(null,args) && ((NpcMotionState)args[0]).PumpkingModeUnknown,"Day motor preserves unknown chosen mode without inventing night movement.");
            args[0]=(NpcMotionState)args[0];args[1]=new PredictionEnvironment();Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.RandomDecision,"A later night consumer rejects that unknown mode.");
            var blade=new NpcMotionState{Identity=new NpcIdentity(1,new object(),20,1,328,328),Style=59,Active=true,A0=-1,Vx=2,Vy=-1,PositionOwnerCaptured=true,PositionOwnerValid=false};
            args=new object[]{blade,new PredictionEnvironment{Multiplayer=true},null,1,PredictionStop.None,null,0,false};Require((bool)action.Invoke(null,args) && ((NpcMotionState)args[0]).SpriteDirection==1 && ((NpcMotionState)args[0]).Vx==1.8f,"Client orphan executes facing and damping without fabricated retirement.");
            args[0]=blade;args[1]=new PredictionEnvironment();Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.Despawn && !((NpcMotionState)args[0]).Active,"Nonclient known orphan retires.");
            blade.PositionOwnerValid=true;blade.PositionOwner=body.Identity;args[0]=blade;Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.MissingDependency,"Valid owner requires its complete observed identity, never a static anchor.");
            body.Life=0;body.L2=0;body.Width=body.Height=80;body.X=1300;body.Y=1700;blade.A2=1;blade.Y=1600;args[0]=blade;args[5]=new[]{body};args[6]=1;Require((bool)action.Invoke(null,args),"Active zero-life AI58 remains a valid blade parent.");
            args[5]=new[]{new NpcMotionState{Identity=new NpcIdentity(1,body.Identity.Token,16,2,327,327),Style=58,Active=true}};Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.MissingDependency,"Reused owner slot cannot impersonate its old generation.");
            Require(!(bool)known.Invoke(null,new object[]{new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,345,345),Style=60}}),"Independent IceQueen is not borrowed as Pumpking coverage.");
            Console.WriteLine("PASS AI58/59 native mode consumer, day unknown, orphan and owner-generation boundaries");
        }
        internal static void Lifecycle(object context)
        {
            var io=new Inputs(context);int cases=0;
            // Each real observed phase, and the actual same-action transition,
            // is compared without advancing another NPC or calling native RNG.
            for(int variant=0;variant<12;variant++)
            {
                World();var p=Main.LocalPlayer;var n=Actor(16,327,new Vector2(1300.125f,1800.25f));n.ai[0]=1;n.ai[3]=variant%3;n.ai[1]=variant/4;n.velocity=new Vector2(-2.375f,1.125f);n.localAI[0]=6;n.localAI[1]=4;
                if(variant==3){n.ai[2]=299;n.ai[3]=1;}if(variant==7)n.ai[2]=599;
                if(variant==8 || variant==9){Main.dayTime=true;n.localAI[2]=variant==9?300:0;Main.netMode=1;}
                if(variant==10){p.position.X=n.position.X+2000.125f;n.ai[1]=0;}
                if(variant==11){p.position.X=n.Center.X-p.width*.5f;p.position.Y=n.Center.Y-200-p.height*.5f;n.ai[1]=0;}
                Compare(io,n);cases++;
            }
            foreach(int side in new[]{-1,1})for(int variant=0;variant<10;variant++)
            {
                World();var body=Actor(16,327,new Vector2(1600.375f,1800.625f));body.ai[0]=1;body.ai[3]=2;
                var n=Actor(20,328,new Vector2(1600.125f,1900.25f));n.ai[0]=side;n.ai[1]=16;n.ai[2]=variant%6;n.velocity=new Vector2(2.375f,-1.125f);
                if(variant==6 || variant==7){n.ai[2]=variant==6?0:3;n.ai[3]=179;}
                if(variant==8){n.ai[2]=1;n.position.Y=body.Top.Y-200.125f;}
                if(variant==9){n.ai[2]=4;n.position.X=body.Center.X+500.125f-n.width*.5f;}
                Compare(io,n);cases++;
            }
            // Capture itself verifies the exact native clamping/validity rule.
            foreach(int parentSlot in new[]{-20,16,500})
            {
                World();var n=Actor(20,328,new Vector2(1700,1800));n.ai[1]=parentSlot;int clamped=Math.Max(0,Math.Min(parentSlot,Main.maxNPCs-1));var parent=Actor(clamped,327,new Vector2(1600,1800));parent.life=0;
                var s=io.Capture(n);Require(s.PositionOwnerCaptured && s.PositionOwnerValid && ReferenceEquals(s.PositionOwner.Token,parent),"Native AI59 clamped active zero-life owner is captured.");parent.active=false;s=io.Capture(n);Require(s.PositionOwnerCaptured && !s.PositionOwnerValid,"Known inactive parent is captured separately from missing input.");
            }
            Console.WriteLine("PASS original AI58/59 scalar actions="+cases+" plus clamped owner capture");
        }
        private static void Compare(Inputs io,NPC n)
        {
            io.Terrain.Reset();var state=io.Capture(n);var group=new List<NpcMotionState>();foreach(var actor in Main.npc)if(actor!=null && actor.active && (actor.type==327 || actor.type==328))group.Add(io.Capture(actor));
            object[] args={state,Environment(),io.Terrain,0,PredictionStop.None,group.ToArray(),group.Count,false};Require((bool)Model.GetMethod("Step",Flags).Invoke(null,args),"Known AI58/59 scalar action continues stop="+args[4]);state=(NpcMotionState)args[0];n.AI();
            Require(state.Vx==n.velocity.X && state.Vy==n.velocity.Y && state.A0==n.ai[0] && state.A1==n.ai[1] && state.A2==n.ai[2] && state.A3==n.ai[3] && state.L0==n.localAI[0] && state.L1==n.localAI[1] && state.L2==n.localAI[2] && state.Direction==n.direction && state.SpriteDirection==n.spriteDirection && state.TimeLeft==n.timeLeft,
                "Original Pumpking scalar type="+n.type+" predictedV="+state.Vx.ToString("R")+","+state.Vy.ToString("R")+" actualV="+n.velocity.X.ToString("R")+","+n.velocity.Y.ToString("R")+" phases="+state.A0+","+state.A1+","+state.A2+","+state.A3+"/"+string.Join("/",n.ai)+" local="+state.L0+","+state.L1+","+state.L2+"/"+string.Join("/",n.localAI)+" dir="+state.Direction+","+state.SpriteDirection+"/"+n.direction+","+n.spriteDirection+" TL="+state.TimeLeft+"/"+n.timeLeft);
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            var io=new Inputs(context);var candidate=io.Source.GetType().GetMethod("TryCandidate",Flags);var oracle=new Harmony("JueMingR.Tests.PumpkingOriginalSlot");oracle.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPumpkingChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatPumpkingChecks),nameof(After)));
            try
            {
                var rows=new List<string>{"scene,frame,type,phase,defaultSelected,hasForecast,count,stop,matches,long,cross"};int total=0,totalLong=0,totalCross=0;var failures=new List<string>();
                for(int scene=0;scene<3;scene++)try
                {
                    World();Main.pumpkinMoon=true;Main.rand=new Terraria.Utilities.UnifiedRandom(5800+scene);var p=Main.LocalPlayer;p.position=new Vector2(2000,2400-p.height);p.ResetFallDamage();p.isControlledByFilm=true;p.releaseJump=true;p.immune=true;p.immuneTime=1000000;p.statLife=p.statLifeMax=p.statLifeMax2=5000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
                    NativeCombatObservationChecks.Save(io.Host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    int bodySlot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,1800,327,Start:16,Target:Main.myPlayer);NPC body=Main.npc[bodySlot],selectedActor=scene==0?body:null;
                    int actions=0,choices=0,published=0,selected=0,matches=0,longMatches=0,cross=0,recovered=0;bool waiting=false;double maxError=0,tangent=0;var phases=new HashSet<int>();var frozen=new List<Frozen>();var predicted=default(NpcMotionState);bool valid=false;
                    before=n=>
                    {
                        valid=false;if(!ReferenceEquals(n,selectedActor))return;io.Terrain.Reset();predicted=io.Capture(n);var group=new[]{io.Capture(body),predicted};var e=Environment();e.PlayerTimelineActive=true;PredictionStop stop;
                        valid=NpcMotion.Step(ref predicted,group,group.Length,e,io.Terrain,1,true,out stop);if(stop==PredictionStop.RandomDecision)choices++;
                        Require(valid || stop==PredictionStop.RandomDecision || stop==PredictionStop.Despawn,"Current original Pumpking action dependency stop="+stop);
                    };
                    after=n=>
                    {
                        if(!ReferenceEquals(n,selectedActor) || !valid)return;double error=Vector2.Distance(n.position,new Vector2(predicted.X,predicted.Y));
                        Require(error<=.002 && Math.Abs(n.velocity.X-predicted.Vx)<=.0001 && Math.Abs(n.velocity.Y-predicted.Vy)<=.0001 && predicted.A1==n.ai[1] && predicted.A2==n.ai[2],"Current original Pumpking scene="+scene+" error="+error+" predictedV="+predicted.Vx.ToString("R")+","+predicted.Vy.ToString("R")+" actual="+n.position+" V="+n.velocity+" phase="+string.Join("/",n.ai));actions++;
                    };
                    for(int frame=0;frame<3000;frame++)
                    {
                        if(frame%240==0){p.controlRight=frame%480==0;p.controlLeft=!p.controlRight;frozen.Clear();}
                        if(selectedActor==null)for(int slot=0;slot<Main.maxNPCs;slot++){var child=Main.npc[slot];if(child.active && child.type==328 && (int)child.ai[1]==bodySlot && child.ai[0]==(scene==1?-1:1)){selectedActor=child;break;}}
                        NativeCombatModeledImpactChecks.SampleMouse(context,(selectedActor??body).Center);step();Require(body.active && p.active && !p.dead,"Original Pumpking continuous prerequisites remain alive.");if(selectedActor==null)continue;Require(selectedActor.active,"Selected native blade remains attached to its actual parent.");
                        int phase=selectedActor.type==327?(int)selectedActor.ai[1]:(int)selectedActor.ai[2];phases.Add(phase);bool has=(bool)Get(Get(io.Host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(io.Host,"Selection"),"Target")).Token,selectedActor);if(has)selected++;
                        // Native blades stay invulnerable. Their motion/dependency route is
                        // tested by pure slot-ordered dependency continuation, without claiming
                        // default selection or mutating native receiving flags.
                        NpcTrajectory path=scene==0?cache.Read(0):null;
                        if(scene!=0)
                        {
                            Require(selectedActor.dontTakeDamage && !has,"Native blade remains outside the actual receiving selection.");
                            if(frame%5==0)
                            {
                                Require(candidate.Invoke(io.Source,new object[]{io.Capture(selectedActor).Identity,(long)Main.GameUpdateCount,false,cache.Required})==null,"Source correctly refuses the native nonreceiver, rather than pretending it is selectable.");
                                path=DependencyFuture(io,selectedActor,cache.Required);
                            }
                        }
                        if(path!=null && ReferenceEquals(path.Identity.Token,selectedActor))
                        {published++;Require((scene!=0 || has) && path.CaptureTick==Main.GameUpdateCount,"Pumpking actual selection or pure dependency forecast uses this sample.");if(path.Stop==PredictionStop.RandomDecision)waiting=true;else if(waiting && path.Count>=30){recovered++;waiting=false;}if(frame%5==0 && path.Count>1)frozen.Add(new Frozen(path,phase));}
                        for(int i=frozen.Count-1;i>=0;i--)
                        {
                            var item=frozen[i];int offset=(int)((long)Main.GameUpdateCount-item.Path.CaptureTick);if(offset>=item.Path.Count){frozen.RemoveAt(i);continue;}if(offset<=0)continue;var point=item.Path[offset];double error=Vector2.Distance(selectedActor.position,new Vector2(point.Bounds.X,point.Bounds.Y));maxError=Math.Max(maxError,error);matches++;
                            if(offset>=30){longMatches++;tangent=Math.Max(tangent,Vector2.Distance(selectedActor.position,new Vector2(item.Path[0].Bounds.X+item.Path[0].Vx*offset,item.Path[0].Bounds.Y+item.Path[0].Vy*offset)));}if(item.Phase!=phase)cross++;
                            Require(error<=.08 && point.NoTileCollide==selectedActor.noTileCollide && point.CanReceive==(!selectedActor.dontTakeDamage && !selectedActor.immortal),"Frozen Pumpking scene="+scene+" frame="+frame+" offset="+offset+" error="+error.ToString("R")+" actual="+selectedActor.position+" V="+selectedActor.velocity+" phase="+string.Join("/",selectedActor.ai)+" owner="+body.position+" ownerPhase="+string.Join("/",body.ai)+" player="+p.position+" pv="+p.velocity+" stop="+item.Path.Stop);
                        }
                        rows.Add(string.Join(",",scene,frame,selectedActor.type,phase,has,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matches,longMatches,cross));
                    }
                    Require((scene!=0 || selected>0) && published>0 && matches>=30 && longMatches>=30 && cross>=30 && tangent>2 && actions>=300 && recovered>=3,"Pumpking natural curved stages, true random-boundary resampling and long recovery scene="+scene+" matched="+matches+" long="+longMatches+" cross="+cross+" recovery="+recovered+" phases="+string.Join("/",phases));
                    if(scene==0)Require(phases.Contains(0) && phases.Contains(1) && choices>=3,"Body actually observes both hover/chase and native choices.");else for(int phase=0;phase<6;phase++)Require(phases.Contains(phase),"Actual blade traverses its complete six observed phases scene="+scene+" phase="+phase);
                    total+=matches;totalLong+=longMatches;totalCross+=cross;Console.WriteLine("PASS AI58/59 natural consumer="+(scene==0?"default-selected-body":"invulnerable-blade-kernel-dependency")+" scene="+scene+" matched="+matches+" long="+longMatches+" cross="+cross+" actions="+actions+" recovery="+recovered+" maxError="+maxError.ToString("R"));
                }
                catch(Exception ex){failures.Add("scene"+scene+":"+ex.Message);Console.WriteLine("FAIL AI58/59 scene="+scene+" "+ex);}
                finally{before=after=null;}
                Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"pumpking-scenes.csv"),rows);Require(failures.Count==0,"Pumpking actual scenes: "+string.Join(" | ",failures));Console.WriteLine("PASS AI58/59 scenes=3 matched="+total+" long="+totalLong+" cross="+totalCross);
            }
            finally{before=after=null;oracle.UnpatchAll(oracle.Id);}
        }
        // A nonreceiver cannot be submitted to Rolling.Prepare. This fixture
        // exercises its necessary motor from a real completed world sample,
        // with the existing pure player continuation and original NPC slot
        // order. It is not a Source publication or a selectable-target proof.
        private static NpcTrajectory DependencyFuture(Inputs io,NPC selected,int maximum)
        {
            io.Terrain.Reset();var rows=new List<NpcMotionState>();int selectedIndex=-1;
            for(int i=0;i<Main.maxNPCs;i++)
            {var n=Main.npc[i];if(n.active && (n.type==327 || n.type==328)){if(ReferenceEquals(n,selected))selectedIndex=rows.Count;rows.Add(io.Capture(n));}}
            Require(selectedIndex>=0,"Native dependent blade has its real captured group.");var group=rows.ToArray();var points=new NpcTrajectoryPoint[maximum+1];points[0]=new NpcTrajectoryPoint(0,group[selectedIndex]);
            var player=(PredictionPlayerMotion)io.Source.GetType().GetMethod("ReadPlayer",Flags).Invoke(null,new object[]{Main.LocalPlayer});var rolling=new RollingNpcPrediction();var advance=typeof(RollingNpcPrediction).GetMethod("AdvancePlayer",Flags);var e=Environment();e.PlayerTimelineActive=true;int count=1;PredictionStop stop=PredictionStop.None;
            for(int future=1;future<=maximum;future++)
            {
                object[] stepPlayer={player,e,io.Terrain,PredictionStop.None};if(!(bool)advance.Invoke(rolling,stepPlayer)){stop=(PredictionStop)stepPlayer[3];break;}player=(PredictionPlayerMotion)stepPlayer[0];
                e.PlayerX=player.X+player.Width*.5f;e.PlayerY=player.Y+player.Height*.5f;e.PlayerVx=player.Vx;e.PlayerVy=player.Vy;e.PlayerWet=player.Wet;
                for(int i=0;i<group.Length;i++)
                {
                    if(group[i].HasPlayer && group[i].PlayerIndex==player.PlayerIndex){group[i].PlayerCenterX=e.PlayerX;group[i].PlayerCenterY=e.PlayerY;group[i].PlayerVx=player.Vx;group[i].PlayerVy=player.Vy;if(group[i].PlayerPreciseAreaCaptured)group[i].PlayerPreciseArea=new MotionRect(player.X,player.Y,player.Width,player.Height);}
                    if(group[i].HasClosestPlayer && group[i].ClosestPlayerIndex==player.PlayerIndex){group[i].ClosestPlayerCenterX=e.PlayerX;group[i].ClosestPlayerCenterY=e.PlayerY;group[i].ClosestPlayerVx=player.Vx;group[i].ClosestPlayerVy=player.Vy;group[i].ClosestPlayerArea=new MotionRect(player.X,player.Y,player.Width,player.Height);if(group[i].ClosestPlayerPreciseAreaCaptured)group[i].ClosestPlayerPreciseArea=new MotionRect(player.X,player.Y,player.Width,player.Height);}
                }
                bool valid=true;for(int i=0;i<group.Length;i++){var n=group[i];if(!NpcMotion.Step(ref n,group,group.Length,e,io.Terrain,future,true,out stop)){valid=false;break;}group[i]=n;}
                if(!valid)break;points[count++]=new NpcTrajectoryPoint(future,group[selectedIndex]);
            }
            Require(stop==PredictionStop.None || stop==PredictionStop.RandomDecision,"Pure dependency forecast retains explicit native mode boundary stop="+stop);
            return new NpcTrajectory(group[selectedIndex].Identity,(long)Main.GameUpdateCount,0,PredictionAssumption.NoNewHits,stop,points,count);
        }
        private sealed class Frozen
        {internal readonly NpcTrajectory Path;internal readonly int Phase;internal Frozen(NpcTrajectory p,int phase){Path=p;Phase=phase;}}
        private sealed class Inputs
        {
            internal readonly object Host,Source;internal readonly IPredictionTerrain Terrain;private readonly long session;private readonly MethodInfo read,tracking,position;
            internal Inputs(object context){Host=Get(context,"CombatObservation");Source=Get(Host,"Prediction");Terrain=(IPredictionTerrain)Get(Source,"Terrain");session=(long)Get(Host,"Session");read=Source.GetType().GetMethod("Read",Flags);var hostAssembly=Source.GetType().Assembly;tracking=hostAssembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);position=hostAssembly.GetType("JueMingR.TerrariaHost.Combat.NpcPositionObservation",true).GetMethod("Capture",Flags);}
            internal NpcMotionState Capture(NPC n){var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] a={n,state,Terrain};tracking.Invoke(null,a);state=(NpcMotionState)a[1];a=new object[]{n,state,session};position.Invoke(null,a);return (NpcMotionState)a[1];}
        }
        private static NPC Actor(int slot,int type,Vector2 pos){var n=Main.npc[slot]=new NPC{whoAmI=slot};n.SetDefaults(type);n.active=true;n.target=Main.myPlayer;n.position=pos;return n;}
        private static void World(){NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.dayTime=false;Main.remixWorld=false;Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true};Main.LocalPlayer.position=new Vector2(2000.125f,2400-Main.LocalPlayer.height);Main.LocalPlayer.ResetFallDamage();}
        private static PredictionEnvironment Environment(){var p=Main.LocalPlayer;return new PredictionEnvironment{Players=new PredictionPlayers(new[]{new MotionRect((int)p.position.X,(int)p.position.Y,p.width,p.height)},1,new[]{p.whoAmI}),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerVelocityCaptured=true,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,Day=Main.dayTime,Multiplayer=Main.netMode==1,Expert=Main.expertMode};}
    }
}
