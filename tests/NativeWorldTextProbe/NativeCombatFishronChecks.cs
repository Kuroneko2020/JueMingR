using System;
using System.Collections.Generic;
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
    // Default Source and original Player -> NPC -> Projectile order. The old
    // scalar phase checks remain useful, but cannot certify this consumer path.
    internal static class NativeCombatFishronChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static readonly List<string> networkOutlets=new List<string>();
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static bool IsolateNetwork(int msgType,int number,float number2,float number3,float number4)
        {
            if((msgType!=23 && msgType!=28) || number<0 || number>=Main.maxNPCs || Main.npc[number].type<370 || Main.npc[number].type>373)return true;
            networkOutlets.Add(string.Join("/",msgType,number,number2,number3,number4));return false;
        }
        internal static void Lifecycle(object context)
        {
            var io=new Inputs(context);int cases=0;
            // The old target remains an observed inactive player. Actual AI69
            // must choose player0 before computing its hover and enrage clocks.
            World();Main.player[1]=new Player{whoAmI=1,active=false,dead=false};Main.player[1].position=new Vector2(5000,100);
            var n=Actor(370,new Vector2(1800.125f,1800.375f));n.target=1;n.localAI[0]=1;n.ai[0]=0;n.ai[2]=29;n.velocity=new Vector2(3,-2);Compare(io,n);cases++;
            foreach(int type in new[]{372,373})
            {
                World();n=Actor(type,new Vector2(1800.125f,1800.375f));n.target=255;n.ai[0]=0;n.ai[1]=89;n.ai[2]=30;n.ai[3]=-2;n.localAI[1]=17;n.direction=-1;Compare(io,n);cases++;
            }
            Console.WriteLine("PASS Fishron actual retarget/preparation actions="+cases);
        }
        private static void Compare(Inputs io,NPC n)
        {
            io.Terrain.Reset();var state=io.Capture(n);var input=state;PredictionStop stop;
            bool valid=NpcMotion.Step(ref state,new[]{state},1,Environment(),io.Terrain,1,true,out stop);n.UpdateNPC(n.whoAmI);
            double error=Vector2.Distance(n.position,new Vector2(state.X,state.Y));
            Require(valid && error<=.002 && state.A0==n.ai[0] && state.A1==n.ai[1] && state.A2==n.ai[2] && state.Target==n.target && state.CanReceive==(!n.dontTakeDamage && !n.immortal),
                "Fishron actual action type="+n.type+" oldTarget="+input.Target+" target="+state.Target+"/"+n.target+" stop="+stop+" error="+error.ToString("R")+" predictedAI="+state.A0+"/"+state.A1+"/"+state.A2+" actualAI="+string.Join("/",n.ai)+" predictedV="+state.Vx.ToString("R")+","+state.Vy.ToString("R")+" actualV="+n.velocity);
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            int selectedScene=-1;string selector=System.Environment.GetEnvironmentVariable("JMR_FISHRON_SCENE");
            Require(string.IsNullOrEmpty(selector) || int.TryParse(selector,out selectedScene) && selectedScene>=0 && selectedScene<=3,"JMR_FISHRON_SCENE must be 0, 1, 2 or 3.");
            var io=new Inputs(context);var oracle=new Harmony("JueMingR.Tests.FishronOriginalSlot");
            oracle.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatFishronChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatFishronChecks),nameof(After)));
            oracle.Patch(typeof(NetMessage).GetMethod("SendData",Flags),prefix:new HarmonyMethod(typeof(NativeCombatFishronChecks),nameof(IsolateNetwork)){priority=Priority.First+100});
            var rows=new List<string>{"scene,frame,phase,count,stop,matches,long,hits"};networkOutlets.Clear();
            try
            {
                for(int scene=0;scene<=3;scene++)
                {
                    if(selectedScene>=0 && selectedScene!=scene)continue;
                    if(scene==3){before=after=null;NativeCombatFishronChildChecks.Run(context,cache,step);continue;}
                    if(scene==2){EnrageCrossing(context,cache,step,io,rows);continue;}
                    World();Main.GameMode=scene;Main.rand=new Terraria.Utilities.UnifiedRandom(6900+scene);var p=Main.LocalPlayer;
                    p.isControlledByFilm=true;p.releaseJump=true;p.immune=true;p.immuneTime=1000000;p.statLife=p.statLifeMax=p.statLifeMax2=5000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
                    int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2200,1850,370,Start:16,Target:Main.myPlayer);var actor=Main.npc[slot];
                    NativeCombatObservationChecks.Save(io.Host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    var frozen=new List<NpcTrajectory>();var phases=new HashSet<int>();var phaseLong=new int[14];int matches=0,longMatches=0,actions=0,receiveCross=0,hits=0,bubbles=0;double maxError=0;bool valid=false;var predicted=default(NpcMotionState);
                    before=n=>{valid=false;if(!ReferenceEquals(n,actor))return;io.Terrain.Reset();predicted=io.Capture(n);var e=Environment();e.PlayerTimelineActive=true;PredictionStop stop;valid=NpcMotion.Step(ref predicted,new[]{predicted},1,e,io.Terrain,1,true,out stop);Require(valid || stop==PredictionStop.Despawn,"Actual Fishron current action stop="+stop);};
                    after=n=>{if(!ReferenceEquals(n,actor) || !valid)return;double error=Vector2.Distance(n.position,new Vector2(predicted.X,predicted.Y));Require(error<=.002 && predicted.A0==n.ai[0] && predicted.CanReceive==(!n.dontTakeDamage && !n.immortal),"Actual current Fishron motion/receive error="+error.ToString("R")+" predictedPhase="+predicted.A0+" actual="+string.Join("/",n.ai));actions++;};
                    for(int frame=0;frame<3500;frame++)
                    {
                        if(frame%240==0){p.controlRight=frame%480==0;p.controlLeft=!p.controlRight;frozen.Clear();}
                        // A real received hit, after the relevant full cycle,
                        // supplies each new health premise. No AI phase write.
                        bool next=hits==0 && phases.Contains(3) && actor.ai[0]==0 || scene==1 && hits==1 && phases.Contains(8) && actor.ai[0]==5;
                        if(next && !actor.dontTakeDamage)
                        {
                            frozen.Clear();int old=actor.life;actor.StrikeNPCNoInteraction(hits==0?(int)(actor.lifeMax*.62): (int)(actor.lifeMax*.30),0,0);
                            Require(actor.life<old && actor.life>0,"Actual Fishron received damage supplies health transition.");hits++;
                        }
                        NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();Require(actor.active && p.active && !p.dead,"Natural Fishron and player remain active.");phases.Add((int)actor.ai[0]);
                        foreach(var child in Main.npc)if(child.active && child.type==371){bubbles++;break;}
                        var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,actor) && frame%5==0 && path.Count>1)frozen.Add(path);
                        for(int i=frozen.Count-1;i>=0;i--)
                        {
                            var f=frozen[i];int offset=(int)((long)Main.GameUpdateCount-f.CaptureTick);if(offset>=f.Count){frozen.RemoveAt(i);continue;}if(offset<=0)continue;
                            var point=f[offset];double error=Vector2.Distance(actor.position,new Vector2(point.Bounds.X,point.Bounds.Y));maxError=Math.Max(maxError,error);matches++;
                            if(offset>=30){longMatches++;int phase=(int)actor.ai[0];if(phase>=0 && phase<phaseLong.Length)phaseLong[phase]++;}
                            if(point.CanReceive!=f[0].CanReceive)receiveCross++;
                            Require(error<=.08 && point.Phase==actor.ai[0] && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal),"Frozen Fishron scene="+scene+" frame="+frame+" offset="+offset+" error="+error.ToString("R")+" phase="+point.Phase+"/"+actor.ai[0]+" actor="+actor.position+" velocity="+actor.velocity+" ai="+string.Join("/",actor.ai));
                        }
                        rows.Add(string.Join(",",scene,frame,actor.ai[0],path?.Count??0,path?.Stop.ToString()??"none",matches,longMatches,hits));
                        bool covered=true;foreach(int phase in scene==0?new[]{0,1,2,3,4,5,6,7,8}:new[]{0,1,2,3,4,5,6,7,8,9,10,11,12})if(!phases.Contains(phase) || phaseLong[phase]<10)covered=false;
                        if(covered && hits==(scene==0?1:2) && bubbles>0 && receiveCross>30)break;
                    }
                    foreach(int phase in scene==0?new[]{0,1,2,3,4,5,6,7,8}:new[]{0,1,2,3,4,5,6,7,8,9,10,11,12})Require(phases.Contains(phase) && phaseLong[phase]>=10,"Natural Fishron has long default-source phase coverage scene="+scene+" phase="+phase+" long="+phaseLong[phase]);
                    Require(matches>30 && longMatches>30 && actions>300 && receiveCross>30 && bubbles>0 && hits==(scene==0?1:2),"Fishron actual birth/cycle/health/receive denominator.");
                    Console.WriteLine("PASS Fishron scene="+scene+" matched="+matches+" long="+longMatches+" actions="+actions+" receiveCross="+receiveCross+" actualHits="+hits+" actualBubbleFrames="+bubbles+" phaseLong="+string.Join("/",phaseLong)+" maxError="+maxError.ToString("R"));
                }
            }
            finally{Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"fishron-scenes.csv"),rows);foreach(var row in networkOutlets)Console.WriteLine("Fishron actual isolated SendData="+row);before=after=null;oracle.UnpatchAll(oracle.Id);}
        }
        private static void EnrageCrossing(object context,NpcPredictionCache cache,Action step,Inputs io,List<string> rows)
        {
            World();Main.rand=new Terraria.Utilities.UnifiedRandom(6902);var p=Main.LocalPlayer;
            // A real jump crosses the native top-left Y800 enrage boundary
            // after capture. Keep its held controls fixed across each window.
            for(int x=0;x<Main.maxTilesX;x++){Main.tile[x,54].active(true);Main.tile[x,54].type=1;}
            p.position=new Vector2(2400,864-p.height);p.isControlledByFilm=true;p.releaseJump=true;p.immune=true;p.immuneTime=1000000;
            p.statLife=p.statLifeMax=p.statLifeMax2=5000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2200,600,370,Start:16,Target:Main.myPlayer);var actor=Main.npc[slot];
            NativeCombatObservationChecks.Save(io.Host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
            var frozen=new List<Tuple<NpcTrajectory,bool>>();int matches=0,longMatches=0,crossings=0,actions=0;double maxError=0;bool valid=false;var predicted=default(NpcMotionState);
            before=n=>{valid=false;if(!ReferenceEquals(n,actor))return;io.Terrain.Reset();predicted=io.Capture(n);var e=Environment();e.PlayerTimelineActive=true;PredictionStop stop;valid=NpcMotion.Step(ref predicted,new[]{predicted},1,e,io.Terrain,1,true,out stop);Require(valid,"Fishron enrage current action stop="+stop);};
            after=n=>{if(!ReferenceEquals(n,actor) || !valid)return;Require(Vector2.Distance(n.position,new Vector2(predicted.X,predicted.Y))<=.002 && predicted.A0==n.ai[0],"Fishron actual enrage current action.");actions++;};
            bool below=false,above=false;
            for(int frame=0;frame<240;frame++)
            {
                if(frame==90 || frame==160){p.controlJump=frame==90;frozen.Clear();}
                NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();Require(actor.active && !p.dead,"Enrage crossing actors remain active.");
                bool enraged=p.position.Y<800;below|=!enraged;above|=enraged;
                var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,actor) && path.Count>1)frozen.Add(Tuple.Create(path,enraged));
                for(int i=frozen.Count-1;i>=0;i--)
                {
                    var f=frozen[i].Item1;int offset=(int)((long)Main.GameUpdateCount-f.CaptureTick);if(offset>=f.Count){frozen.RemoveAt(i);continue;}if(offset<=0)continue;
                    var point=f[offset];double error=Vector2.Distance(actor.position,new Vector2(point.Bounds.X,point.Bounds.Y));maxError=Math.Max(maxError,error);matches++;if(offset>=30)longMatches++;if(frozen[i].Item2!=enraged)crossings++;
                    Require(error<=.08 && point.Phase==actor.ai[0] && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal),"Frozen Fishron enrage frame="+frame+" offset="+offset+" playerY="+p.position.Y.ToString("R")+" error="+error.ToString("R")+" phase="+point.Phase+"/"+actor.ai[0]);
                }
                rows.Add(string.Join(",",2,frame,actor.ai[0],path?.Count??0,path?.Stop.ToString()??"none",matches,longMatches,crossings));
            }
            Require(below && above && crossings>=10 && longMatches>=30 && actions==240,"Actual jump crosses enrage in retained default-source windows.");
            Console.WriteLine("PASS Fishron enrage jump actions="+actions+" matched="+matches+" long="+longMatches+" retainedCrossings="+crossings+" maxError="+maxError.ToString("R"));
        }
        internal sealed class Inputs
        {
            internal readonly object Host,Source;internal readonly IPredictionTerrain Terrain;private readonly long session;private readonly MethodInfo read,tracking;
            internal Inputs(object context){Host=Get(context,"CombatObservation");Source=Get(Host,"Prediction");Terrain=(IPredictionTerrain)Get(Source,"Terrain");session=(long)Get(Host,"Session");read=Source.GetType().GetMethod("Read",Flags);tracking=Source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);}
            internal NpcMotionState Capture(NPC n){var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,state,Terrain};tracking.Invoke(null,args);return (NpcMotionState)args[1];}
        }
        private static NPC Actor(int type,Vector2 position){var n=Main.npc[16]=new NPC{whoAmI=16};n.SetDefaults(type);n.active=true;n.target=Main.myPlayer;n.position=position;return n;}
        internal static void World(){NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=0;Main.dayTime=false;Main.remixWorld=false;Main.worldSurface=200;Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true};Main.LocalPlayer.position=new Vector2(2400,2400-Main.LocalPlayer.height);Main.LocalPlayer.ResetFallDamage();}
        internal static PredictionEnvironment Environment()
        {
            var p=Main.LocalPlayer;return new PredictionEnvironment{Players=new PredictionPlayers(new[]{new MotionRect((int)p.position.X,(int)p.position.Y,p.width,p.height)},1,new[]{p.whoAmI}),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerVelocityCaptured=true,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,Day=Main.dayTime,Multiplayer=Main.netMode==1,Expert=Main.expertMode,Enraged=p.position.Y<800 || p.position.Y>Main.worldSurface*16 || p.position.X>6400 && p.position.X<Main.maxTilesX*16-6400};
        }
    }
}
