using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatBigMimicChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Boundaries()
        {
            var model=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcBigMimicMotion",true);var known=model.GetMethod("Known",Flags);var action=model.GetMethod("Step",Flags);
            foreach(int member in new[]{473,474,475,476})
            {
                var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,member,member),Style=87,Life=500,LifeMax=500,A0=2,A2=210,Width=80,Height=80,Direction=1};
                Require((bool)known.Invoke(null,new object[]{n}),"All four locked large mimic members are dispatched.");
                var other=n;other.Identity=new NpcIdentity(1,new object(),16,1,85,85);Require(!(bool)known.Invoke(null,new object[]{other}),"Ordinary mimic is a distinct motor.");
                object[] args={n,new PredictionEnvironment{PlayerX=100,PlayerY=0},new Air(),false,PredictionStop.None};
                Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.RandomDecision,"Stop before the real landed third-action random choice, member="+member);
                n.A0=4;n.A2=1;n.X=n.Y=0;args[0]=n;args[1]=new PredictionEnvironment{PlayerX=40,PlayerY=40};
                Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.InvalidState,"Original zero Normalize cannot invent a finite dive.");
            }
            Console.WriteLine("PASS large mimic exact members / random and zero-vector boundaries");
        }
        private static void NormalizeOracle()
        {
            var normalize=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcBigMimicMotion",true).GetMethod("Normalize",Flags);int cases=0;
            foreach(float speed in new[]{8f,10f,12f})for(int i=1;i<=8192;i++)
            {
                float x=((i*7919)%100007-50003)/31f,y=((i*3571)%90007-45003)/29f;var native=new Vector2(x,y);native.Normalize();native*=speed;
                object[] args={x,y,speed,PredictionStop.None};Require((bool)normalize.Invoke(null,args),"Finite native normalize input.");
                Require((float)args[0]==native.X && (float)args[1]==native.Y,"Exact locked XNA normalize component/order speed="+speed+" x="+x+" y="+y+" predicted="+args[0]+","+args[1]+" actual="+native);cases++;
            }
            Console.WriteLine("PASS original XNA mimic Normalize exact noninteger vector matrix cases="+cases);
        }
        internal static void Lifecycle(object context)
        {
            NormalizeOracle();
            var source=Get(Get(context,"CombatObservation"),"Prediction");long session=(long)Get(Get(context,"CombatObservation"),"Session");
            var read=source.GetType().GetMethod("Read",Flags);var capture=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);
            var action=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcBigMimicMotion",true).GetMethod("Step",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");
            int oldMode=Main.netMode,oldGameMode=Main.GameMode,cases=0;bool oldAnniversary=Main.tenthAnniversaryWorld;
            try
            {
                NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;
                Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true};var p=Main.LocalPlayer;p.position=new Vector2(1100,2358);
                foreach(int member in new[]{473,474,475,476})foreach(float phase in new[]{0f,1f,2f,3f,4f,4.1f,5f,6f,7f,8f})foreach(int variant in new[]{0,1})
                {
                    Main.tenthAnniversaryWorld=variant==1;var actor=Main.npc[16]=new NPC{whoAmI=16};actor.SetDefaults(member);actor.active=true;actor.target=Main.myPlayer;actor.direction=-1;actor.directionY=1;
                    actor.position=new Vector2(1450,2240);actor.velocity=new Vector2(-2,variant==0?0:1);actor.ai[0]=phase;actor.ai[1]=phase==1?36:phase==3?179:phase==6?5:phase==4?6:12;
                    actor.ai[2]=phase==4?variant:0;actor.ai[3]=phase==2?2:0;if(variant==1)actor.life=actor.lifeMax/4;
                    terrain.Reset();var state=(NpcMotionState)read.Invoke(null,new object[]{actor,session});object[] facts={actor,state,terrain};capture.Invoke(null,facts);state=(NpcMotionState)facts[1];
                    var e=new PredictionEnvironment{PlayerIndex=Main.myPlayer,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,Anniversary=Main.tenthAnniversaryWorld};
                    actor.AI();object[] args={state,e,terrain,false,PredictionStop.None};Require((bool)action.Invoke(null,args),"Original known mimic action continues phase="+phase+" stop="+args[4]);state=(NpcMotionState)args[0];
                    Require(state.Vx==actor.velocity.X && state.Vy==actor.velocity.Y && state.A0==actor.ai[0] && state.A1==actor.ai[1] && state.A2==actor.ai[2] && state.A3==actor.ai[3] && state.NoTileCollide==actor.noTileCollide && state.NoGravity==actor.noGravity && state.ReflectsProjectiles==actor.reflectsProjectiles && state.Health.DontTakeDamage==actor.dontTakeDamage && state.Alpha==actor.alpha && state.Direction==actor.direction && state.SpriteDirection==actor.spriteDirection,
                        "Original mimic motor/phase/collision action member="+member+" phase="+phase+" variant="+variant+" predictedV="+state.Vx+","+state.Vy+" actualV="+actor.velocity+" predictedPhase="+state.A0+","+state.A1+","+state.A2+","+state.A3+" actualPhase="+actor.ai[0]+","+actor.ai[1]+","+actor.ai[2]+","+actor.ai[3]+" exactV="+state.Vx.ToString("R",CultureInfo.InvariantCulture)+","+state.Vy.ToString("R",CultureInfo.InvariantCulture)+"/"+actor.velocity.X.ToString("R",CultureInfo.InvariantCulture)+","+actor.velocity.Y.ToString("R",CultureInfo.InvariantCulture)+" noTile="+state.NoTileCollide+"/"+actor.noTileCollide+" noGravity="+state.NoGravity+"/"+actor.noGravity+" immune="+state.Health.DontTakeDamage+"/"+actor.dontTakeDamage+" alpha="+state.Alpha+"/"+actor.alpha+" direction="+state.Direction+"/"+actor.direction+" sprite="+state.SpriteDirection+"/"+actor.spriteDirection);cases++;
                }
                int reflections=0;
                foreach(int mode in new[]{0,1})foreach(int member in new[]{473,474,475,476})foreach(int clock in new[]{178,179,180})
                {
                    Main.GameMode=mode;var n=Main.npc[16]=new NPC{whoAmI=16};n.SetDefaults(member);n.active=true;n.target=Main.myPlayer;n.ai[0]=3;n.ai[1]=clock;n.position=new Vector2(1450,2240);n.velocity=new Vector2(-2,0);
                    terrain.Reset();var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] facts={n,state,terrain};capture.Invoke(null,facts);state=(NpcMotionState)facts[1];object[] args={state,new PredictionEnvironment{Expert=Main.expertMode,PlayerIndex=Main.myPlayer,PlayerX=p.Center.X,PlayerY=p.Center.Y},terrain,false,PredictionStop.None};Require((bool)action.Invoke(null,args),"Observed mimic shield action continues.");state=(NpcMotionState)args[0];n.AI();
                    Require(state.ReflectsProjectiles==n.reflectsProjectiles && state.CanReceive==(!n.dontTakeDamage && !n.immortal) && state.A0==n.ai[0] && state.A1==n.ai[1],"Original Expert shield reflection is independent of receiving and remains true on the phase3->2 action member="+member+" mode="+mode+" clock="+clock);reflections++;
                }
                Console.WriteLine("PASS original large mimic scalar actions cases="+cases+" actual reflection shield edges="+reflections);
            }
            finally{Main.netMode=oldMode;Main.GameMode=oldGameMode;Main.tenthAnniversaryWorld=oldAnniversary;}
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            var host=Get(context,"CombatObservation");Require(GetOptional(Get(host,"Prediction"),"Native")==null,"Large mimic uses the default synchronous producer.");
            var rows=new List<string>{"scene,frame,tick,phase,x,y,vx,vy,noTile,receiving,selected,published,count,stop,matched,longMatches,crossMatches,restored,actorPublished,directionalDisplays,adjacentDisplays,retainedDisplays,maxMissingRun,minFuture,maxFuture,maxDisplaySpan,curvedError"};var failures=new List<string>();
            int scenes=0,total=0,totalLong=0,totalCross=0;bool oldAnniversary=Main.tenthAnniversaryWorld;
            try
            {
                foreach(int member in new[]{473,474,475,476})foreach(int variant in new[]{0,1,2})
                {
                    string scene=member+"-natural-"+variant;scenes++;
                    try
                    {
                        NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=variant==1?1:0;Main.dayTime=false;Main.tenthAnniversaryWorld=member==476 && variant==2;
                        Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};var p=Main.LocalPlayer;p.position=new Vector2(1100,2400-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.creativeGodMode=true;p.immune=true;p.immuneTime=100000;
                        for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=100000;for(int i=0;i<3;i++)p.armor[i].SetDefaults(696+i);
                        if(variant==2)for(int x=80;x<88;x++)for(int y=143;y<150;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                        int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1160,2400,member,Start:16,Target:Main.myPlayer);var actor=Main.npc[slot];actor.timeLeft=750;
                        // A real native hit wakes the same default-born actor.
                        // Low health changes all subsequent jump thresholds;
                        // no producer scene overwrites ai/localAI or its phase.
                        actor.StrikeNPCNoInteraction(variant==2?actor.lifeMax/2+actor.defense+100:1,0,0);Require(actor.active,"Mimic wake hit preserves the natural actor.");
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        var freezes=new List<NpcTrajectory>();var phases=new HashSet<float>();int matches=0,longMatches=0,cross=0,selectedCount=0,published=0,restored=0;bool unavailable=false;double maximum=0,tangent=0;
                        // Display accepts useful short futures; the existing phase/action
                        // requirements below remain independent of its optional long tail.
                        int directionalDisplays=0,adjacentDisplays=0,retainedDisplays=0,missingRun=0,maxMissingRun=0,minFuture=int.MaxValue,maxFuture=0;long lastDisplayTick=-2,lastCapture=-1;double maxDisplaySpan=0,curvedError=0;
                        for(int frame=0;frame<1200 && actor.active;frame++)
                        {
                            NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(!actor.active)break;phases.Add(actor.ai[0]);
                            bool has=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(has)selectedCount++;
                            var path=NativeCombatPresentationChecks.CurrentPath(host);if(path!=null && ReferenceEquals(path.Identity.Token,actor))
                            {
                                Require(has && path.SampleTick==Main.GameUpdateCount,"Mimic fresh selected publication.");published++;
                                Require(ReferenceEquals(GetOptional(Get(host,"World"),"preparedPath"),GetOptional(host,"DisplayPath")),"World.Prepare consumes the actual selected Display owner and current lease.");
                                double span=DisplaySpan(path);maxDisplaySpan=Math.Max(maxDisplaySpan,span);
                                double markerSpan=Math.Max(Vector2.TransformNormal(new Vector2(6,0),Main.GameViewMatrix.ZoomMatrix).Length(),Vector2.TransformNormal(new Vector2(0,6),Main.GameViewMatrix.ZoomMatrix).Length());
                                if(span>markerSpan)directionalDisplays++;
                                if(lastDisplayTick==(long)Main.GameUpdateCount-1){adjacentDisplays++;if(lastCapture==path.CaptureTick)retainedDisplays++;}
                                lastDisplayTick=Main.GameUpdateCount;lastCapture=path.CaptureTick;minFuture=Math.Min(minFuture,path.Count-1);maxFuture=Math.Max(maxFuture,path.Count-1);

                                if(unavailable && path.Count>30){restored++;unavailable=false;}
                                if(published==1 || frame%15==0)freezes.Add(path);
                            }
                            else unavailable=true;
                            if(has && (path==null || !ReferenceEquals(path.Identity.Token,actor))){missingRun++;maxMissingRun=Math.Max(maxMissingRun,missingRun);}else missingRun=0;
                            for(int i=freezes.Count-1;i>=0;i--)
                            {
                                var frozen=freezes[i];int offset=(int)((long)Main.GameUpdateCount-frozen.SampleTick);if(offset>=frozen.Count){freezes.RemoveAt(i);continue;}if(offset<=0)continue;
                                var point=frozen[offset];double error=Vector2.Distance(actor.position,new Vector2(point.Bounds.X,point.Bounds.Y));maximum=Math.Max(maximum,error);matches++;
                                curvedError=Math.Max(curvedError,Vector2.Distance(actor.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));
                                if(offset>=30){longMatches++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));}
                                if(point.Phase!=frozen[0].Phase)cross++;
                                Require(error<=.08 && point.ReflectsProjectiles==actor.reflectsProjectiles && point.NoTileCollide==actor.noTileCollide && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal),"Frozen native mimic "+scene+" offset="+offset+" error="+error.ToString("R",CultureInfo.InvariantCulture)+" predicted="+point.Bounds.X+","+point.Bounds.Y+" pv="+point.Vx+","+point.Vy+" actual="+actor.position+" av="+actor.velocity+" predictedPhase="+point.Phase+" actualPhase="+actor.ai[0]+","+actor.ai[1]+","+actor.ai[2]+" collision="+point.NoTileCollide+"/"+actor.noTileCollide+" stop="+frozen.Stop);
                            }
                            rows.Add(string.Join(",",scene,frame,Main.GameUpdateCount,actor.ai[0],actor.position.X,actor.position.Y,actor.velocity.X,actor.velocity.Y,actor.noTileCollide,!actor.dontTakeDamage,has,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matches,longMatches,cross,restored,path!=null && ReferenceEquals(path.Identity.Token,actor),directionalDisplays,adjacentDisplays,retainedDisplays,maxMissingRun,published==0?0:minFuture,maxFuture,maxDisplaySpan.ToString("R",CultureInfo.InvariantCulture),curvedError.ToString("R",CultureInfo.InvariantCulture)));
                        }
                        Require(selectedCount>0 && published>0 && directionalDisplays>0 && adjacentDisplays>0 && cross>0,"Same natural mimic must consume directional connected Display futures across actual phase changes "+scene+" matches="+matches+" long="+longMatches+" cross="+cross);
                        Require(phases.Contains(2) && (phases.Contains(3) || phases.Contains(4) || phases.Contains(6)),"Actual random choice must lead into observed receiving/recovery phases "+scene);
                        Require(curvedError>8,"Mimic genuine turns must reject a velocity tangent "+scene);
                        total+=matches;totalLong+=longMatches;totalCross+=cross;
                        Console.WriteLine("BIG_MIMIC scene="+scene+" selected="+selectedCount+" published="+published+" matched="+matches+" long="+longMatches+" cross="+cross+" restored="+restored+" phases="+string.Join("|",phases)+" maxError="+maximum+" tangentError="+tangent+" directionalDisplays="+directionalDisplays+" adjacentDisplays="+adjacentDisplays+" retainedDisplays="+retainedDisplays+" maxMissingRun="+maxMissingRun+" minFuture="+(published==0?0:minFuture)+" maxFuture="+maxFuture+" maxDisplaySpan="+maxDisplaySpan.ToString("R",CultureInfo.InvariantCulture)+" curvedError="+curvedError.ToString("R",CultureInfo.InvariantCulture));
                    }
                    catch(InvalidOperationException failure){failures.Add(scene+": "+failure.Message);Console.WriteLine("BIG_MIMIC FAIL "+scene+": "+failure.Message);}
                }
                Require(failures.Count==0,"Large mimic natural failures="+failures.Count+": "+string.Join(" | ",failures));
                Console.WriteLine("PASS original large mimic continuous scenes="+scenes+" matched="+total+" long="+totalLong+" crossed="+totalCross);
            }
            finally{Main.tenthAnniversaryWorld=oldAnniversary;Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"big-mimic-continuous.csv"),rows);}
        }
        private sealed class Air:IPredictionTerrain
        {
            public bool Unchanged {get{return true;}}public void Reset(){}
            public bool Solid(MotionRect area,out bool solid,out PredictionStop stop){solid=false;stop=PredictionStop.None;return true;}
            public bool CanHit(MotionRect a,MotionRect b,out bool clear,out PredictionStop stop){clear=true;stop=PredictionStop.None;return true;}
            public bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop){tile=default(PredictionTile);stop=PredictionStop.None;return true;}
            public bool Move(ref NpcMotionState state,PredictionEnvironment e,out PredictionStop stop){stop=PredictionStop.None;return true;}
        }
        // Same body-centre segments and zoom as the real Display consumer.
        // A direction must extend beyond its six-world-unit endpoint cross;
        // NewSegment is a break, never a fabricated connecting line.
        private static double DisplaySpan(NpcTrajectory path)
        {
            var start=new Vector2(path[0].Bounds.CenterX,path[0].Bounds.CenterY);double maximum=0;
            for(int i=1;i<path.Count;i++)
            {
                var point=path[i];var next=new Vector2(point.Bounds.CenterX,point.Bounds.CenterY);
                if(point.NewSegment){start=next;continue;}
                float span=Vector2.TransformNormal(next-start,Main.GameViewMatrix.ZoomMatrix).Length();
                Require(!float.IsNaN(span) && !float.IsInfinity(span),"Actual Display geometry is finite.");maximum=Math.Max(maximum,span);
            }
            return maximum;
        }
    }
}
