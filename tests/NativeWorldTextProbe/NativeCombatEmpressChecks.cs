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
    // This leaf observes original AI/UpdateNPC; it never substitutes a chosen
    // attack, stops projectile birth or turns a presentation failure into PASS.
    internal static class NativeCombatEmpressChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static Type Model {get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcEmpressMotion",true);}}
        private static Action<NPC> before,after;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static bool Network(int msgType){return msgType!=23 && msgType!=27 && msgType!=28;}
        private sealed class Outlets:IDisposable
        {
            private readonly Harmony patch=new Harmony("JueMingR.Probe.EmpressOutlets");
            internal Outlets(){var method=new HarmonyMethod(typeof(NativeCombatEmpressChecks),nameof(Network));method.priority=Priority.First+100;patch.Patch(typeof(NetMessage).GetMethod("SendData",Flags),prefix:method);}
            public void Dispose(){patch.UnpatchAll(patch.Id);}
        }
        private static bool Motor(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null)
        {object[] args={n,e,terrain,elapsed,PredictionStop.None,group,group==null?0:group.Length,false};bool valid=(bool)Model.GetMethod("Step",Flags).Invoke(null,args);n=(NpcMotionState)args[0];stop=(PredictionStop)args[4];return valid;}

        internal static void Boundaries(object context)
        {
            var io=new Inputs(context);PredictionStop stop;int cases=0;
            foreach(bool day in new[]{false,true})foreach(double time in new[]{(day?54000d:32400d)-1,day?54000d:32400d,(day?54000d:32400d)+1})
            {
                Setup(0);Main.dayTime=day;Main.time=time;Main.dayRate=1;var n=Actor(new Vector2(2400.125f,2210.25f));n.ai[0]=2;n.ai[1]=65;
                var state=io.Capture(n);io.Terrain.Reset();Require(Motor(ref state,EnvironmentFor(n),io.Terrain,1,out stop) && state.EmpressClock==time && state.EmpressDay==day,"First NPC action consumes captured time, including exact native strict-boundary value.");cases++;
                Require(Motor(ref state,EnvironmentFor(n),io.Terrain,2,out stop),"Second time action remains computable.");double expected=time+1;bool expectedDay=day;if(expected>(day?54000d:32400d)){expected=0;expectedDay=!day;}
                Require(state.EmpressClock==expected && state.EmpressDay==expectedDay,"Only the next NPC action consumes one intervening whole-world time tail.");cases++;
            }
            foreach(bool inactive in new[]{false,true})foreach(bool above in new[]{false,true})
            {
                Setup(3);var first=Actor(new Vector2(2000.25f,above?2180:2270),4);first.active=!inactive;var selected=Actor(new Vector2(2500.125f,2310),24);selected.ai[0]=2;selected.ai[1]=61;
                Require(first.whoAmI<selected.whoAmI,"Original NewNPC gives the lower raw636 role.");var state=io.Capture(selected);var firstState=io.Capture(first);io.Terrain.Reset();
                Require(state.EmpressFirstIdentity.Equals(firstState.Identity) && state.EmpressFirstActive==first.active,"Capture preserves the first raw full identity without filtering inactive slots.");
                Require(Motor(ref state,EnvironmentFor(selected),io.Terrain,1,out stop,new[]{firstState,state}) && state.EmpressRageLatched==above,"First raw actor's exact native CenterY controls Remix latch.");
                bool actual=NPC.ShouldEmpressBeEnraged();Require(actual==above && NPC.empressRageMode==above,"Original first-raw counter agrees with the scalar rule.");cases++;
                if(!inactive)
                {NPC.empressRageMode=false;state=io.Capture(selected);Require(state.EmpressFirstIdentity.Equals(firstState.Identity) && state.EmpressFirstActive && !state.EmpressRageLatched,"The missing-owner counterexample captures the native unlatched premise before removing its motor row.");io.Terrain.Reset();Require(!Motor(ref state,EnvironmentFor(selected),io.Terrain,1,out stop,new[]{state}) && stop==PredictionStop.MissingDependency,"An active different first role cannot become a frozen scalar anchor.");cases++;}
            }
            Setup(3);var own=Actor(new Vector2(2300.5f,2310),16);var higher=Actor(new Vector2(2300.5f,2100),24);higher.active=false;
            var input=io.Capture(own);Require(input.EmpressFirstIdentity.Equals(input.Identity) && !NPC.ShouldEmpressBeEnraged(),"A higher inactive636 above surface cannot replace the actual lower first role.");cases++;
            input.EmpressRelationsComplete=false;Require(!Motor(ref input,EnvironmentFor(own),io.Terrain,1,out stop) && stop==PredictionStop.MissingDependency,"Incomplete necessary raw-role closure is not an ordinary trend.");cases++;
            // Actual Source acquisition, with original AI first establishing a
            // legal receiver. Inspect its private candidate role closure, not
            // another hand-built source or a new publication API.
            foreach(bool inactive in new[]{false,true})
            {
                Setup(3);var low=Actor(new Vector2(2200,2300),4);low.active=!inactive;var chosen=Actor(new Vector2(2400,2300),24);chosen.ai[0]=2;chosen.ai[1]=65;
                using(var outlets=new Outlets())chosen.AI();Require(!chosen.dontTakeDamage,"Native observed action is a real selectable receiver.");NPC.empressRageMode=false;var state=io.Capture(chosen);io.TryCandidate(state.Identity);
                var candidate=Get(io.Source,"candidateSource");Require(candidate!=null,"Production candidate source actually acquired this identity.");var states=(NpcMotionState[])Get(candidate,"states");var roles=(bool[])Get(candidate,"motionRoles");int count=(int)Get(candidate,"observedCount");bool included=false,selectedFound=false;
                for(int i=0;i<count;i++){if(states[i].Identity.Equals(io.Capture(low).Identity) && roles[i])included=true;if(states[i].Identity.Equals(state.Identity)){selectedFound=true;Require(states[i].EmpressFirstIdentity.Equals(io.Capture(low).Identity),"Actual Source retains the first full raw identity.");}}
                Require(selectedFound && included==!inactive,"Only the active required first636 is queued as motor; inactive geometry remains a scalar fact.");cases++;
            }
            Console.WriteLine("PASS Empress clock/raw-role/source boundaries="+cases);
        }

        internal static void Lifecycle(object context)
        {
            var io=new Inputs(context);int actions=0;
            using(var outlets=new Outlets())for(int profile=0;profile<4;profile++)
            {
                bool second=profile!=0,expert=profile!=0;int faster=(second?15:0)+(expert?5:0);float wait=second?20:45;if(profile==2)wait/=2;
                var phases=new[]{new[]{0,0},new[]{0,10},new[]{0,149},new[]{0,179},new[]{1,0},new[]{1,10},new[]{1,11},new[]{1,(int)Math.Ceiling(wait)-1},
                    new[]{2,0},new[]{2,59},new[]{2,149-faster},new[]{3,0},new[]{3,119},new[]{4,5},new[]{4,53},new[]{4,119-faster},
                    new[]{5,0},new[]{5,41},new[]{5,71-faster},new[]{6,0},new[]{6,179},new[]{6,299-faster},new[]{7,0},new[]{7,expert?40:60},new[]{7,239+(expert?40:20)-faster},
                    new[]{8,5},new[]{8,6},new[]{8,40},new[]{8,41},new[]{8,90},new[]{8,91},new[]{8,109-faster},new[]{9,5},new[]{9,6},new[]{9,40},new[]{9,41},new[]{9,90},new[]{9,91},new[]{9,109-faster},
                    new[]{10,29},new[]{10,30},new[]{10,89},new[]{10,90},new[]{10,170},new[]{10,199-faster},new[]{11,5},new[]{11,53},new[]{11,119-faster},new[]{12,0},new[]{12,149-faster},new[]{13,0},new[]{13,19},new[]{13,50}};
                foreach(var phase in phases)
                {
                    Setup(profile==2?1:0);Main.GameMode=expert?1:0;Main.getGoodWorld=profile==2;Main.dayTime=profile==3;Main.time=Main.dayTime?53399:1800;
                    var n=Actor(new Vector2(2300.625f,2200.375f));n.ai[0]=phase[0];n.ai[1]=phase[1];n.ai[2]=actions%10;n.ai[3]=profile==3?3:second?1:0;n.localAI[0]=43;n.alpha=125;n.velocity=new Vector2(-3.125f,.375f);
                    if(second)n.life=n.lifeMax/2;var state=io.Capture(n);io.Terrain.Reset();PredictionStop stop;Require(Motor(ref state,EnvironmentFor(n),io.Terrain,1,out stop) || stop==PredictionStop.Despawn,"Observed Empress action computes profile="+profile+" phase="+string.Join("/",phase)+" stop="+stop);
                    if(phase[0]==10 && phase[1]==90)Require(state.NewSegment,"The known native relocation publishes a new segment boundary.");
                    n.AI();Equal(state,n,"profile="+profile+" phase="+string.Join("/",phase));actions++;
                }
            }
            using(var outlets=new Outlets())foreach(bool expert in new[]{false,true})for(int ordinal=0;ordinal<(expert?10:9);ordinal++)
            {
                Setup(expert?1:0);Main.getGoodWorld=false;var n=Actor(new Vector2(2300.625f,2200.375f));n.ai[0]=1;n.ai[1]=19;n.ai[2]=ordinal;n.ai[3]=1;n.life=n.lifeMax/2;n.velocity=new Vector2(-1.375f,2.25f);
                var state=io.Capture(n);io.Terrain.Reset();PredictionStop stop;Require(Motor(ref state,EnvironmentFor(n),io.Terrain,1,out stop),"Observed deterministic second-stage ordinal has its actual target.");n.AI();Equal(state,n,"second-stage ordinal="+ordinal+" expert="+expert);actions++;
            }
            // These are adjacent stored player centers, not ideal decimal
            // distances which collapse to the same float at X4096. The700
            // input reaches the native600 lerp clamp after its100 shortening.
            using(var outlets=new Outlets())foreach(float threshold in new[]{0,18,100,200,700})for(int side=threshold==0?0:-1;side<=(threshold==0?0:1);side++)
            {
                Setup(0);var n=Actor(new Vector2(4095.875f,2200.25f));n.ai[0]=1;n.ai[1]=2;float centerX=n.Center.X+threshold;int bits=BitConverter.ToInt32(BitConverter.GetBytes(centerX),0);centerX=BitConverter.ToSingle(BitConverter.GetBytes(bits+side),0);
                Main.LocalPlayer.position=new Vector2(centerX-Main.LocalPlayer.width*.5f,n.Center.Y+300-Main.LocalPlayer.height*.5f);
                Require(Main.LocalPlayer.Center.X==centerX && Main.LocalPlayer.Center.Y-300==n.Center.Y,"The strict DashTo input is actually represented in original Entity.Center.");
                var state=io.Capture(n);io.Terrain.Reset();PredictionStop stop;Require(Motor(ref state,EnvironmentFor(n),io.Terrain,1,out stop),"DashTo observed distance/zero fallback computes.");n.AI();Equal(state,n,"DashTo threshold="+threshold.ToString("R")+" side="+side+" storedCenterX="+centerX.ToString("R"));actions++;
            }
            using(var outlets=new Outlets())for(int variant=0;variant<3;variant++)
            {
                Setup(variant==2?0:2);var n=Actor(new Vector2(2300.625f,2200.375f));n.ai[3]=variant==2?0:2;n.ai[0]=variant==0?13:1;n.ai[1]=variant==0?19:variant==1?44:0;n.alpha=variant==0?250:125;n.velocity=new Vector2(3.125f,-2.25f);
                if(variant!=2)Main.time=53400;else Main.LocalPlayer.dead=true;
                var state=io.Capture(n);io.Terrain.Reset();PredictionStop stop;Require(Motor(ref state,EnvironmentFor(n),io.Terrain,1,out stop) || stop==PredictionStop.Despawn,"Actual time/target expiry is computed before native retirement.");n.AI();Equal(state,n,"expiry variant="+variant);Require(variant==0?!n.active:n.ai[0]==13,"The actual native expiry branch is exercised.");actions++;
            }
            Console.WriteLine("PASS Empress original finite phase/receive/DashTo actions="+actions);
        }
        private static void Equal(NpcMotionState p,NPC n,string note)
        {
            double position=Vector2.Distance(n.position,new Vector2(p.X,p.Y)),velocity=Vector2.Distance(n.velocity,new Vector2(p.Vx,p.Vy));
            Require(position<=.003 && velocity<=.00005 && p.A0==n.ai[0] && p.A1==n.ai[1] && p.A2==n.ai[2] && p.A3==n.ai[3] && p.Alpha==n.alpha && p.Active==n.active && p.Target==n.target && p.Health.Defense==n.defense && p.Health.DontTakeDamage==n.dontTakeDamage && p.CanReceive==(!n.dontTakeDamage && !n.immortal),
                "Original Empress "+note+" poseError="+position.ToString("R")+" velocityError="+velocity.ToString("R")+" P="+p.X.ToString("R")+","+p.Y.ToString("R")+"/"+n.position+" V="+p.Vx.ToString("R")+","+p.Vy.ToString("R")+"/"+n.velocity+" A="+p.A0+","+p.A1+","+p.A2+","+p.A3+"/"+string.Join(",",n.ai)+" L0="+p.L0+"/"+n.localAI[0]+" alpha="+p.Alpha+"/"+n.alpha+" defense="+p.Health.Defense+"/"+n.defense+" receive="+p.CanReceive+"/"+!n.dontTakeDamage+" active="+p.Active+"/"+n.active+" target="+p.Target+"/"+n.target);
        }

        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            int chosen=-1;string selector=System.Environment.GetEnvironmentVariable("JMR_EMPRESS_SCENE");Require(string.IsNullOrEmpty(selector) || int.TryParse(selector,out chosen) && chosen>=0 && chosen<4,"Empress selector must be0..3.");Console.WriteLine("Empress actual scene set="+(chosen<0?"0,1,2,3":chosen.ToString()));
            var io=new Inputs(context);var observer=new Harmony("JueMingR.Probe.EmpressOriginalSlot");observer.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatEmpressChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatEmpressChecks),nameof(After)));
            var rows=new List<string>{"scene,frame,pathType,pathSlot,count,stop,phase,clock,stage,alpha,receiving,day,time,rage,matches,long,hits,restored,retired"};
            try
            {
                using(var outlets=new Outlets())for(int scene=0;scene<4;scene++)
                {
                    if(chosen>=0 && chosen!=scene)continue;Setup(scene);var p=Main.LocalPlayer;p.isControlledByFilm=true;p.immune=true;p.immuneTime=1000000;p.statLife=p.statLifeMax=p.statLifeMax2=5000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
                    NativeCombatObservationChecks.Save(io.Host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));var actor=Actor(new Vector2(2400.25f,scene==3?2310:2200.375f));var frozen=new List<NpcTrajectory>();var phases=new HashSet<int>();var longPhases=new HashSet<int>();
                    int actions=0,matches=0,longMatches=0,published=0,receiving=0,hidden=0,hits=0,restore=0,second=0,relocations=0;double maximum=0,tangent=0;bool retired=false,retirePredicted=false,waiting=false,sawRage=false;long waitTick=-1;NpcMotionState predicted=default(NpcMotionState);PredictionStop actionStop=PredictionStop.None;bool valid=false;
                    before=n=>{valid=false;if(!ReferenceEquals(n,actor))return;io.Terrain.Reset();predicted=io.Capture(n);valid=NpcMotion.Step(ref predicted,new[]{predicted},1,EnvironmentFor(n),io.Terrain,1,true,out actionStop);Require(valid || actionStop==PredictionStop.Despawn,"Original Empress current full-action stop="+actionStop);};
                    after=n=>{if(!ReferenceEquals(n,actor))return;if(!valid){Require(actionStop==PredictionStop.Despawn && !n.active,"Pure retirement coincides with original slot retirement.");retirePredicted=true;return;}Equal(predicted,n,"natural scene="+scene+" nativeAction="+actions);actions++;};
                    try
                    {
                        for(int frame=0;frame<3000;frame++)
                        {
                            if(scene==1 && frame>=500 && hits==0 && !actor.dontTakeDamage)
                            {frozen.Clear();waiting=false;int old=actor.life;actor.StrikeNPCNoInteraction(actor.lifeMax/2+1000,0,0);Require(actor.active && actor.life<actor.lifeMax/2 && actor.life<old,"Actual original damage, not phase assignment, crosses half-life.");hits++;}
                            float previousX=actor.position.X,previousY=actor.position.Y;int previousPhase=(int)actor.ai[0];float previousClock=actor.ai[1];
                            NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();Require(p.active && !p.dead,"Actual fixed-input owner stays alive.");phases.Add((int)actor.ai[0]);sawRage|=NPC.empressRageMode;if(actor.ai[3]==1 || actor.ai[3]==3)second++;
                            if(previousPhase==10 && previousClock==90 && Vector2.Distance(actor.position,new Vector2(previousX,previousY))>40)relocations++;
                            if(actor.dontTakeDamage)hidden++;else receiving++;var path=cache.Read(0);
                            if(path!=null && ReferenceEquals(path.Identity.Token,actor))
                            {published++;if(path.Count<30 && (path.Stop==PredictionStop.MissingDependency || path.Stop==PredictionStop.PhaseBoundary) && !waiting){waiting=true;waitTick=path.CaptureTick;}if(waiting && path.CaptureTick>waitTick && path.Count>=30){restore++;waiting=false;}if(frame%5==0 && path.Count>1)frozen.Add(path);}
                            for(int i=frozen.Count-1;i>=0;i--)
                            {
                                var f=frozen[i];int offset=(int)((long)Main.GameUpdateCount-f.CaptureTick);if(offset>=f.Count){frozen.RemoveAt(i);continue;}if(offset<=0)continue;var point=f[offset];double error=Vector2.Distance(actor.position,new Vector2(point.Bounds.X,point.Bounds.Y));maximum=Math.Max(maximum,error);matches++;
                                if(offset>=30){longMatches++;longPhases.Add((int)actor.ai[0]);tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(f[0].Bounds.X+f[0].Vx*offset,f[0].Bounds.Y+f[0].Vy*offset)));}
                                Require(error<=.08 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal) && point.NoTileCollide==actor.noTileCollide,"Frozen Empress scene="+scene+" frame="+frame+" offset="+offset+" error="+error.ToString("R")+" P="+actor.position+" V="+actor.velocity+" phase="+string.Join("/",actor.ai)+" alpha="+actor.alpha+" day/time="+Main.dayTime+"/"+Main.time.ToString("R")+" rage="+NPC.empressRageMode+" stop="+f.Stop);
                            }
                            rows.Add(string.Join(",",scene,frame,path==null?"":path.Identity.Type.ToString(),path==null?"":path.Identity.Slot.ToString(),path?.Count??0,path?.Stop.ToString()??"none",actor.ai[0],actor.ai[1],actor.ai[3],actor.alpha,actor.dontTakeDamage?0:1,Main.dayTime?1:0,Main.time,NPC.empressRageMode?1:0,matches,longMatches,hits,restore,actor.active?0:1));
                            if(!actor.active){retired=true;Require((scene==2 || scene==3) && retirePredicted && path==null && !(bool)Get(Get(io.Host,"Selection"),"HasTarget"),"Original fade retirement revokes selection and publication.");break;}
                        }
                        Require(actions>=180 && published>0 && matches>=30 && longMatches>=30 && hidden>0 && receiving>0,"Natural birth/real receive/long curve denominator is exercised before any native retirement.");
                        if(scene==0)Require(!retired && tangent>2 && phases.Contains(2) && (phases.Contains(8) || phases.Contains(9)) && phases.Contains(5) && phases.Contains(6),"Normal original deterministic cycle contains curved attacks, not an initial tangent only.");
                        if(scene==1)Require(hits==1 && second>30 && phases.Contains(10) && relocations>0 && phases.Contains(7) && phases.Contains(11) && phases.Contains(12),"Expert GoodWorld actual half-hit reaches observed transition and second-stage controller attacks.");
                        if(scene==2)Require(retired && phases.Contains(13) && Main.time>=53400,"Original genuine daytime expiry at53400 reaches fade retirement.");
                        if(scene==3)Require(sawRage && actor.ai[3]>=2 && retired,"Original below-surface birth naturally crosses above the surface, latches rage and preserves native night expiry.");
                        Console.WriteLine("PASS Empress natural scene="+scene+" actions="+actions+" matched="+matches+" long="+longMatches+" phases="+string.Join(";",phases)+" longPhases="+string.Join(";",longPhases)+" hits="+hits+" second="+second+" relocation="+relocations+" restored="+restore+" recoveryDenominator="+(restore>0?"triggered":"not-triggered")+" receiving="+receiving+" hidden="+hidden+" retired="+retired+" rage="+sawRage+" maxError="+maximum.ToString("R"));
                    }
                    finally{before=after=null;}
                }
            }
            finally{before=after=null;observer.UnpatchAll(observer.Id);Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"empress-scenes.csv"),rows);Main.remixWorld=false;NPC.empressRageMode=false;Main.dayRate=1;}
        }
        private sealed class Inputs
        {
            internal readonly object Host,Source;internal readonly IPredictionTerrain Terrain;private readonly long session;private readonly MethodInfo read,tracking,capture,candidate;
            internal Inputs(object context){Host=Get(context,"CombatObservation");Source=Get(Host,"Prediction");Terrain=(IPredictionTerrain)Get(Source,"Terrain");session=(long)Get(Host,"Session");read=Source.GetType().GetMethod("Read",Flags);tracking=Source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);capture=Source.GetType().GetMethod("CaptureEmpressSample",Flags);candidate=Source.GetType().GetMethod("TryCandidate",Flags);}
            internal NpcMotionState Capture(NPC n){var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,state,Terrain};tracking.Invoke(null,args);state=(NpcMotionState)args[1];args=new object[]{n,state,session};capture.Invoke(null,args);return (NpcMotionState)args[1];}
            // This role-only acquisition follows the observed native AI in setup; it is not a BeforeNpc hook at tick zero.
            internal NpcTrajectory TryCandidate(NpcIdentity identity){return (NpcTrajectory)candidate.Invoke(Source,new object[]{identity,(long)Main.GameUpdateCount,false,120});}
        }
        private static NPC Actor(Vector2 position,int start=16){int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),(int)position.X,(int)position.Y,636,Start:start,Target:Main.myPlayer);var n=Main.npc[slot];n.position=position;return n;}
        private static void Setup(int scene)
        {
            NativeCombatLiveContextChecks.FlightWorld();Main.maxTilesY=480;Main.bottomWorld=7680;Main.tile=new Tile[Main.maxTilesX,Main.maxTilesY];for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<Main.maxTilesY;y++){Main.tile[x,y]=new Tile();if(y>=150){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}}
            Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=scene==1?1:0;Main.getGoodWorld=scene==1;Main.remixWorld=scene==3;NPC.empressRageMode=false;Main.dayTime=scene==2;Main.time=scene==2?53000:1800;Main.dayRate=1;Main.rand=new Terraria.Utilities.UnifiedRandom(12000+scene);
            Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true};Main.LocalPlayer.position=new Vector2(2600.125f,2400-Main.LocalPlayer.height);Main.LocalPlayer.ResetFallDamage();for(int i=0;i<Main.maxPlayers;i++)if(i!=Main.myPlayer && Main.player[i]!=null)Main.player[i].active=false;
        }
        private static PredictionEnvironment EnvironmentFor(NPC n)
        {
            var p=Main.player[n.target];var slots=new List<int>();var exact=new List<MotionRect>();var integer=new List<MotionRect>();var alive=new List<bool>();var dead=new List<bool>();for(int i=0;i<Main.maxPlayers;i++){var row=Main.player[i];if(row==null || !row.active)continue;slots.Add(i);exact.Add(new MotionRect(row.position.X,row.position.Y,row.width,row.height));integer.Add(new MotionRect((int)row.position.X,(int)row.position.Y,row.width,row.height));alive.Add(!row.dead && !row.ghost);dead.Add(row.dead);}
            return new PredictionEnvironment{Players=new PredictionPlayers(integer.ToArray(),integer.Count,slots.ToArray(),exact.ToArray(),alive.ToArray(),dead.ToArray()),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerVelocityCaptured=true,PlayerTimelineActive=p.active && !p.dead && !p.ghost,PlayerDead=p.dead || !p.active,Expert=Main.expertMode,GoodWorld=Main.getGoodWorld,Day=Main.dayTime,Remix=Main.remixWorld,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer};
        }
    }
}
