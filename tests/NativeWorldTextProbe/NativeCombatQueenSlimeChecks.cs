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
    internal static class NativeCombatQueenSlimeChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Type Model {get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcQueenSlimeMotion",true);}}
        private static Action<NPC> before,after;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static bool Network(int msgType){return msgType!=23 && msgType!=27;}
        private sealed class Outlets:IDisposable
        {
            private readonly Harmony patch=new Harmony("JueMingR.Probe.QueenSlimeOutlets");
            internal Outlets(){var method=new HarmonyMethod(typeof(NativeCombatQueenSlimeChecks),nameof(Network));method.priority=Priority.First+100;patch.Patch(typeof(NetMessage).GetMethod("SendData",Flags),prefix:method);}
            public void Dispose(){patch.UnpatchAll(patch.Id);}
        }
        private static bool ModelStep(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,out PredictionStop stop)
        {object[] args={n,e,terrain,1,PredictionStop.None,null,0,false};bool ok=(bool)Model.GetMethod("Step",Flags).Invoke(null,args);n=(NpcMotionState)args[0];stop=(PredictionStop)args[4];return ok;}
        internal static void Boundaries(object context)
        {
            var io=new Inputs(context);Setup(0);var n=Actor(new Vector2(2200.125f,2300));PredictionStop stop;int cases=0;
            n.localAI[0]=n.lifeMax;n.ai[0]=0;n.ai[1]=60;n.velocity=Vector2.Zero;
            var state=io.Capture(n);io.Terrain.Reset();Require(!ModelStep(ref state,EnvironmentFor(n),io.Terrain,out stop) && stop==PredictionStop.RandomDecision,"High-life idle stops before actual three-way attack choice.");cases++;
            n.life=n.lifeMax/2;n.ai[1]=120;Main.LocalPlayer.position=new Vector2(n.Center.X-10,n.Bottom.Y+10);
            state=io.Capture(n);io.Terrain.Reset();Require(!ModelStep(ref state,EnvironmentFor(n),io.Terrain,out stop) && stop==PredictionStop.RandomDecision,"Low-life close/below target has distinct slam/shot choices.");cases++;
            Main.LocalPlayer.position=new Vector2(n.Center.X+400,n.Bottom.Y+10);n.ai[2]=0;state=io.Capture(n);io.Terrain.Reset();
            Require(ModelStep(ref state,EnvironmentFor(n),io.Terrain,out stop) && state.A0==5 && state.A1==0 && state.A2==0,"Far target collapses both genuine flight RNG results to one known motor state.");cases++;
            n.ai[2]=1;state=io.Capture(n);io.Terrain.Reset();Require(!ModelStep(ref state,EnvironmentFor(n),io.Terrain,out stop) && stop==PredictionStop.RandomDecision,"The same target does not collapse different preserved A2 values.");cases++;
            state=io.Capture(n);state.Identity=new NpcIdentity(state.Identity.Session,state.Identity.Token,state.Identity.Slot,state.Identity.Generation,668,668);state.Style=123;
            Require(!(bool)Model.GetMethod("Known",Flags).Invoke(null,new object[]{state}),"Deerclops is not the AI121 family.");cases++;
            Console.WriteLine("PASS QueenSlime motor-choice boundaries="+cases);
        }
        internal static void Lifecycle(object context)
        {
            var io=new Inputs(context);int actions=0;
            // Native action representatives surround each consumed threshold,
            // not every cosmetic frame or every item/species in the directory.
            var phases=new[]{new[]{0,0,0},new[]{0,59,0},new[]{1,28,0},new[]{1,29,0},new[]{1,59,0},new[]{2,58,0},new[]{2,59,0},new[]{2,119,0},new[]{3,-5,0},new[]{3,-1,1},new[]{3,-1,2},new[]{3,-1,3},new[]{4,28,0},new[]{4,29,0},new[]{4,59,0},new[]{4,0,1},new[]{4,9,1},new[]{4,30,1},new[]{4,130,1},new[]{5,48,0},new[]{5,49,0},new[]{5,8,1},new[]{5,9,1}};
            using(var outlets=new Outlets())
            foreach(int mode in new[]{0,1})foreach(bool flight in new[]{false,true})foreach(var phase in phases)
            {
                Setup(0);Main.netMode=mode;Main.rand=new Terraria.Utilities.UnifiedRandom(12100+actions);
                var n=Actor(new Vector2(2100.375f,2250.625f));n.localAI[0]=n.lifeMax;n.life=flight?n.lifeMax/2:n.lifeMax;n.ai[0]=phase[0];n.ai[1]=phase[1];n.ai[2]=phase[2];n.localAI[1]=3300.875f;n.localAI[2]=2400;
                n.velocity=new Vector2(1.375f,phase[0]==3 || phase[0]==0?0:phase[0]==4 && phase[2]==1?7.75f:-2.25f);
                var predicted=io.Capture(n);var e=EnvironmentFor(n);io.Terrain.Reset();PredictionStop stop;
                Require(ModelStep(ref predicted,e,io.Terrain,out stop),"Known AI121 scalar continues phase="+string.Join("/",phase)+" flight="+flight+" mode="+mode+" stop="+stop);
                n.AI();Equal(predicted,n,"phase="+string.Join("/",phase)+" flight="+flight+" mode="+mode);actions++;
            }
            using(var outlets=new Outlets())
            for(int variant=0;variant<5;variant++)
            {
                Setup(variant==4?2:0);var n=Actor(new Vector2(2300.625f,2300));n.ai[0]=3;n.ai[1]=-1;n.ai[2]=2;n.velocity=new Vector2(-.101f,0);n.localAI[0]=variant==0?0:n.lifeMax;
                if(variant==1)n.life=n.lifeMax/2-1;if(variant==2)n.life=n.lifeMax/2;
                if(variant==3){Main.player[1]=new Player{whoAmI=1,active=true};Main.player[1].position=new Vector2(2450.25f,2358);n.target=1;Main.player[1].dead=true;}
                var predicted=io.Capture(n);io.Terrain.Reset();PredictionStop stop;Require(ModelStep(ref predicted,EnvironmentFor(n),io.Terrain,out stop),"Observed birth/strict half-life/retarget action has complete necessary input.");
                n.AI();Equal(predicted,n,"variant="+variant);actions++;
            }
            using(var outlets=new Outlets())
            foreach(int mode in new[]{0,1})
            {
                Setup(0);Main.netMode=mode;var n=Actor(new Vector2(2100.375f,2250.625f));n.localAI[0]=n.lifeMax;n.life=n.lifeMax/2;n.ai[0]=0;n.ai[1]=120;n.ai[2]=0;Main.LocalPlayer.position=new Vector2(2600.25f,2358);
                var predicted=io.Capture(n);io.Terrain.Reset();PredictionStop stop;Require(ModelStep(ref predicted,EnvironmentFor(n),io.Terrain,out stop),"Actual two-way RNG has one shared forced-flight motor result.");n.AI();Equal(predicted,n,"collapsed RNG mode="+mode);actions++;
            }
            Console.WriteLine("PASS QueenSlime original AI121 actions="+actions+"; native side-effect RNG is not replayed in the model.");
        }
        private static void Equal(NpcMotionState p,NPC n,string note)
        {
            Require(p.X==n.position.X && p.Y==n.position.Y && p.Vx==n.velocity.X && p.Vy==n.velocity.Y && p.Width==n.width && p.Height==n.height && p.Scale==n.scale && p.A0==n.ai[0] && p.A1==n.ai[1] && p.A2==n.ai[2] && p.A3==n.ai[3] && p.L0==n.localAI[0] && p.L1==n.localAI[1] && p.L2==n.localAI[2] && p.NoGravity==n.noGravity && p.NoTileCollide==n.noTileCollide && p.Health.DontTakeDamage==n.dontTakeDamage && p.CanReceive==(!n.dontTakeDamage && !n.immortal) && p.TimeLeft==n.timeLeft && p.Direction==n.direction && p.DirectionY==n.directionY && p.Target==n.target,
                "Original AI121 strict "+note+" pos="+p.X.ToString("R")+","+p.Y.ToString("R")+" vs "+n.position.X.ToString("R")+","+n.position.Y.ToString("R")+" V="+p.Vx.ToString("R")+","+p.Vy.ToString("R")+" vs "+n.velocity.X.ToString("R")+","+n.velocity.Y.ToString("R")+" size="+p.Width+","+p.Height+","+p.Scale+" vs "+n.width+","+n.height+","+n.scale+" phase="+p.A0+"/"+p.A1+"/"+p.A2+"/"+p.A3+" vs "+string.Join("/",n.ai)+" local="+p.L0+"/"+p.L1+"/"+p.L2+" vs "+string.Join("/",n.localAI)+" gravity="+p.NoGravity+"/"+n.noGravity+" tiles="+p.NoTileCollide+"/"+n.noTileCollide+" receive="+p.CanReceive+"/"+(!n.dontTakeDamage && !n.immortal)+" face="+p.Direction+","+p.DirectionY+"/"+n.direction+","+n.directionY+" target="+p.Target+"/"+n.target);
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            int chosen=-1;string selector=System.Environment.GetEnvironmentVariable("JMR_QUEEN_SLIME_SCENE");Require(string.IsNullOrEmpty(selector) || int.TryParse(selector,out chosen) && chosen>=0 && chosen<3,"QueenSlime scene selector is0..2.");
            Console.WriteLine("QueenSlime actual natural scenes="+(chosen<0?"0,1,2":chosen.ToString()));
            var io=new Inputs(context);var observer=new Harmony("JueMingR.Probe.QueenSlimeNativeSlot");observer.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatQueenSlimeChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatQueenSlimeChecks),nameof(After)));
            var rows=new List<string>{"scene,frame,flight,phase,clocks,pathType,pathSlot,count,stop,matches,long,random,recovery,teleports,receive"};var phaseLong=new int[6];var phases=new HashSet<int>();int totalTeleports=0,totalRecoveries=0;
            try
            {
                using(var outlets=new Outlets())for(int scene=0;scene<3;scene++)
                {
                    if(chosen>=0 && chosen!=scene)continue;Setup(scene);var p=Main.LocalPlayer;
                    p.position=new Vector2(scene==1?5400:2800,2400-p.height);p.ResetFallDamage();p.isControlledByFilm=true;p.immune=true;p.immuneTime=1000000;p.statLife=p.statLifeMax=p.statLifeMax2=5000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
                    if(scene==1)for(int x=240;x<=242;x++)for(int y=70;y<150;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                    NativeCombatObservationChecks.Save(io.Host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),scene==1?1800:2500,2400,657,Start:16,Target:Main.myPlayer);var actor=Main.npc[slot];var frozen=new List<Frozen>();
                    int actions=0,matches=0,longMatches=0,random=0,recovery=0,teleports=0,published=0,receiving=0,hidden=0,hits=0,waitingPhase=-1;long waiting=-1;double maxError=0,tangent=0;NpcMotionState predicted=default(NpcMotionState);PredictionStop actionStop=PredictionStop.None;bool valid=false;
                    before=n=>{valid=false;if(!ReferenceEquals(n,actor))return;io.Terrain.Reset();predicted=io.Capture(n);var e=EnvironmentFor(n);valid=NpcMotion.Step(ref predicted,new[]{predicted},1,e,io.Terrain,1,true,out actionStop);if(actionStop==PredictionStop.RandomDecision || actionStop==PredictionStop.RandomDestination)random++;Require(valid || actionStop==PredictionStop.RandomDecision || actionStop==PredictionStop.RandomDestination || actionStop==PredictionStop.Despawn,"Current original QueenSlime action stop="+actionStop+" phase="+string.Join("/",n.ai));};
                    after=n=>{if(!ReferenceEquals(n,actor) || !valid)return;double error=Vector2.Distance(n.position,new Vector2(predicted.X,predicted.Y));Require(error<=.002 && predicted.Vx==n.velocity.X && predicted.Vy==n.velocity.Y && predicted.A0==n.ai[0] && predicted.A1==n.ai[1] && predicted.A2==n.ai[2] && predicted.A3==n.ai[3] && predicted.Width==n.width && predicted.Height==n.height && predicted.CanReceive==(!n.dontTakeDamage && !n.immortal) && predicted.NoTileCollide==n.noTileCollide,"Current QueenSlime full action scene="+scene+" error="+error.ToString("R")+" predV="+predicted.Vx.ToString("R")+","+predicted.Vy.ToString("R")+" nativeV="+n.velocity.X.ToString("R")+","+n.velocity.Y.ToString("R")+" phases="+predicted.A0+"/"+predicted.A1+"/"+predicted.A2+"/"+predicted.A3+" vs "+string.Join("/",n.ai));if(predicted.NewSegment)teleports++;actions++;};
                    try
                    {
                        for(int frame=0;frame<2600;frame++)
                        {
                            if(scene!=1 && frame>0 && frame%320==0){frozen.Clear();waiting=-1;p.controlRight=frame%640==0;p.controlLeft=!p.controlRight;}
                            if(scene!=1 && frame>=800 && hits==0 && !actor.dontTakeDamage){frozen.Clear();waiting=-1;int old=actor.life;actor.StrikeNPCNoInteraction(actor.lifeMax/2+1000,0,0);Require(actor.active && actor.life<actor.lifeMax/2 && actor.life<old,"Actual native damage enters the flight health stage.");hits++;}
                            NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();Require(p.active && !p.dead && actor.active,"Native continuing QueenSlime actor/player remain alive.");int phase=(int)actor.ai[0];phases.Add(phase);if(actor.dontTakeDamage)hidden++;else receiving++;var path=cache.Read(0);
                            if(path!=null && ReferenceEquals(path.Identity.Token,actor))
                            {
                                published++;if(path.Count<30 && (path.Stop==PredictionStop.RandomDecision || path.Stop==PredictionStop.RandomDestination)){if(waiting<0){waiting=path.CaptureTick;waitingPhase=phase;}}
                                if(waiting>=0 && phase!=waitingPhase && path.CaptureTick>waiting && path.Count>=30){recovery++;waiting=-1;}
                                if(frame%5==0 && path.Count>1)frozen.Add(new Frozen(path,phase));
                            }
                            for(int i=frozen.Count-1;i>=0;i--)
                            {
                                var f=frozen[i];int offset=(int)((long)Main.GameUpdateCount-f.Path.CaptureTick);if(offset>=f.Path.Count){frozen.RemoveAt(i);continue;}if(offset<=0)continue;var point=f.Path[offset];double error=Vector2.Distance(actor.position,new Vector2(point.Bounds.X,point.Bounds.Y));matches++;maxError=Math.Max(maxError,error);
                                if(offset>=30){longMatches++;if(f.Phase>=0 && f.Phase<phaseLong.Length)phaseLong[f.Phase]++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(f.Path[0].Bounds.X+f.Path[0].Vx*offset,f.Path[0].Bounds.Y+f.Path[0].Vy*offset)));}
                                Require(error<=.08 && point.Bounds.Width==actor.width && point.Bounds.Height==actor.height && point.NoTileCollide==actor.noTileCollide && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal),"Frozen QueenSlime scene="+scene+" frame="+frame+" offset="+offset+" error="+error.ToString("R")+" size="+point.Bounds.Width+","+point.Bounds.Height+" vs "+actor.width+","+actor.height+" actual="+actor.position+" V="+actor.velocity+" phase="+string.Join("/",actor.ai)+" PC="+p.position+" PCV="+p.velocity+" stop="+f.Path.Stop);
                            }
                            rows.Add(string.Join(",",scene,frame,actor.life<=actor.lifeMax/2?1:0,phase,actor.ai[1],path==null?"":path.Identity.Type.ToString(),path==null?"":path.Identity.Slot.ToString(),path?.Count??0,path?.Stop.ToString()??"none",matches,longMatches,random,recovery,teleports,actor.dontTakeDamage?0:1));
                        }
                        Require(published>0 && actions>=300 && matches>=30 && longMatches>=30 && tangent>2 && recovery>0,"Natural body curve, exact current actions and fresh long-window recovery are actually exercised scene="+scene);
                        if(scene!=1)Require(hits==1,"Flight stage follows one actual native hit, not an assigned AI phase.");
                        totalTeleports+=teleports;totalRecoveries+=recovery;Console.WriteLine("PASS QueenSlime natural scene="+scene+" actions="+actions+" matches="+matches+" long="+longMatches+" random="+random+" recovery="+recovery+" teleport="+teleports+" receiving="+receiving+" hidden="+hidden+" maxError="+maxError.ToString("R"));
                    }
                    finally{before=after=null;}
                }
                // This is the actual exercised set. A narrow scene selector
                // never claims all observed phases or the omitted terrain case.
                if(chosen<0){foreach(int phase in new[]{0,3,4,5})Require(phases.Contains(phase) && phaseLong[phase]>=30,"Actual QueenSlime phase has a retained long segment phase="+phase);Require(totalTeleports>0,"Blocked natural approach really enters observed teleport, not only a synthetic phase.");}
                Console.WriteLine("QueenSlime aggregate recoveries="+totalRecoveries+" teleports="+totalTeleports);
            }
            finally{before=after=null;observer.UnpatchAll(observer.Id);Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"queen-slime-scenes.csv"),rows);}
        }
        private sealed class Frozen {internal readonly NpcTrajectory Path;internal readonly int Phase;internal Frozen(NpcTrajectory p,int phase){Path=p;Phase=phase;}}
        private sealed class Inputs
        {
            internal readonly object Host,Source;internal readonly IPredictionTerrain Terrain;private readonly long session;private readonly MethodInfo read,tracking;
            internal Inputs(object context){Host=Get(context,"CombatObservation");Source=Get(Host,"Prediction");Terrain=(IPredictionTerrain)Get(Source,"Terrain");session=(long)Get(Host,"Session");read=Source.GetType().GetMethod("Read",Flags);tracking=Source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);}
            internal NpcMotionState Capture(NPC n){var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,state,Terrain};tracking.Invoke(null,args);return (NpcMotionState)args[1];}
        }
        private static NPC Actor(Vector2 position){int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),(int)position.X,(int)position.Y,657,Start:16,Target:Main.myPlayer);var n=Main.npc[slot];n.position=position;return n;}
        private static void Setup(int scene)
        {
            NativeCombatLiveContextChecks.FlightWorld();Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=scene==0?0:1;Main.getGoodWorld=scene==2;Main.dayTime=false;Main.time=1800;Main.rand=new Terraria.Utilities.UnifiedRandom(12100+scene);
            Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true};Main.LocalPlayer.position=new Vector2(2600.125f,2400-Main.LocalPlayer.height);Main.LocalPlayer.ResetFallDamage();
            for(int i=0;i<Main.maxPlayers;i++)if(i!=Main.myPlayer && Main.player[i]!=null)Main.player[i].active=false;
        }
        private static PredictionEnvironment EnvironmentFor(NPC n)
        {
            var p=Main.player[n.target];var slots=new List<int>();var exact=new List<MotionRect>();var integer=new List<MotionRect>();var alive=new List<bool>();var dead=new List<bool>();
            for(int i=0;i<Main.maxPlayers;i++){var row=Main.player[i];if(row==null || !row.active)continue;slots.Add(i);exact.Add(new MotionRect(row.position.X,row.position.Y,row.width,row.height));integer.Add(new MotionRect((int)row.position.X,(int)row.position.Y,row.width,row.height));alive.Add(!row.dead && !row.ghost);dead.Add(row.dead);}
            return new PredictionEnvironment{Players=new PredictionPlayers(integer.ToArray(),integer.Count,slots.ToArray(),exact.ToArray(),alive.ToArray(),dead.ToArray()),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerVelocityCaptured=true,PlayerTimelineActive=p.active && !p.dead && !p.ghost,PlayerDead=p.dead || !p.active,Expert=Main.expertMode,GoodWorld=Main.getGoodWorld,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer};
        }
    }
}
