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
    internal static class NativeCombatAttachedWeaponChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static int isolatedTransformMessages;
        private static bool inspectingTerrain;
        private static string terrainFailure;
        private static void TerrainCellAfter(int x,int y,PredictionStop stop,bool __result)
        {if(inspectingTerrain && !__result && terrainFailure==null)terrainFailure="CellAt="+x+","+y+" stop="+stop+" consumer="+System.Environment.StackTrace;}
        private static bool IsolateTransformMessage(int msgType)
        {if(msgType!=23 && msgType!=54)return true;isolatedTransformMessages++;return false;}
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static Type Model {get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcAttachedWeaponMotion",true);}}
        internal static void Boundaries()
        {
            var known=Model.GetMethod("Known",Flags);var action=Model.GetMethod("Step",Flags);var unit=Model.GetMethod("Unit",Flags);
            foreach(int type in new[]{390,392,393,394,416,492,395,491})
            {
                var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,type,type),Style=type==395?76:type==491?93:75,Active=true};
                Require((bool)known.Invoke(null,new object[]{n}),"All AI75 parts and actual native motor owners share a captured component type="+type);
                object[] args={n,new PredictionEnvironment(),null,1,PredictionStop.None,null,0,false};Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.MissingDependency,"Uncaptured weapon components never become frozen anchors.");
            }
            Require(!(bool)known.Invoke(null,new object[]{new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,389,389),Style=75}}),"Adjacent unmodeled type does not borrow attached weapons.");
            for(int i=0;i<8192;i++)
            {float x=(i%257-128)*.17321f,y=(i%113-56)*.337f;if(x==0 && y==0)y=1;object[] args={x,y,0f,0f};unit.Invoke(null,args);var native=Vector2.Normalize(new Vector2(x,y));Require((float)args[2]==native.X && (float)args[3]==native.Y,"AI75/76 original XNA normalization index="+i);}
            Console.WriteLine("PASS AI75 exact families / required component / XNA matrix8192");
        }
        internal static void Lifecycle(object context)
        {
            var io=new Inputs(context);int matched=0,boundaries=0;
            foreach(int type in new[]{390,392,393,394,416,492,395,491})foreach(int variant in new[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15})
            {
                World();Main.netMode=1;Main.LocalPlayer.position=new Vector2(2100.25f,2340.5f);
                var n=Actor(16,type,new Vector2(1800.125f,1900.25f));n.velocity=new Vector2(2.137f,-1.321f);n.direction=-1;n.spriteDirection=1;n.localAI[0]=n.localAI[3]=1;
                int ownerType=type==390?391:type==416?415:type==492?491:395;
                if(type!=395 && type!=491)
                {
                    var owner=Actor(12,ownerType,new Vector2(1900.375f,2040.625f));owner.velocity=new Vector2(3.127f,-1.173f);owner.direction=-1;owner.spriteDirection=variant%2==0?1:-1;owner.rotation=.317f;owner.localAI[0]=owner.localAI[3]=1;owner.ai[0]=2;owner.ai[3]=variant%2==0?310:200;n.ai[0]=12;n.ai[1]=variant%2;n.ai[3]=variant==5?239:variant==6?1:58;
                    if(type==390)n.ai[1]=variant==4?59:variant==5?-1:variant==6?-60:2;
                    if(variant==8)owner.active=false;if(variant==9)owner.life=0;if(variant==10){owner.width=61;owner.height=41;n.width=19;n.height=41;}
                }
                if(type==395)
                {n.ai[0]=variant<8?0:variant<12?1:variant<15?2:-2;n.ai[1]=variant==9?39:variant==10?79:variant==11?149:2;n.ai[2]=-1;n.ai[3]=new float[]{0,19,259,279,419,439,579,599,0,0,0,0,59,119,3599,0}[variant];}
                if(type==491 || type==392)
                {
                    for(int i=0;i<4;i++){var part=Actor(20+i,type==491?492:i<2?393:394,new Vector2(1850+i*40,1900));part.ai[0]=type==491?16:12;if(type==491)n.ai[i]=20+i;else n.localAI[i]=20+i;if(variant==7 || variant==15)part.active=false;}
                    if(type==392 && variant==15)Main.npc[12].ai[0]=0;
                }
                io.Terrain.Reset();var group=io.Group();int index=Index(group,16);var state=group[index];object[] args={state,Environment(),io.Terrain,0,PredictionStop.None,group,group.Length,false};bool valid=(bool)Model.GetMethod("Step",Flags).Invoke(null,args);state=(NpcMotionState)args[0];n.AI();
                if(!valid)
                {Require((PredictionStop)args[4]==PredictionStop.Despawn && !n.active || (PredictionStop)args[4]==PredictionStop.MissingDependency && (type==390 || type==416) && variant==8,"Scalar stops at actual retirement or missing observed form facts type="+type+" variant="+variant+" stop="+args[4]);boundaries++;continue;}
                Require(state.X==n.position.X && state.Y==n.position.Y && state.Vx==n.velocity.X && state.Vy==n.velocity.Y && state.A0==n.ai[0] && state.A1==n.ai[1] && state.A3==n.ai[3] && state.Direction==n.direction && state.SpriteDirection==n.spriteDirection && (!state.MotorRotationCaptured || state.MotorRotation==n.rotation),"Original AI75/76/93 scalar type="+type+" variant="+variant+" predicted="+state.X.ToString("R")+","+state.Y.ToString("R")+" V="+state.Vx.ToString("R")+","+state.Vy.ToString("R")+" phase="+state.A0+","+state.A1+","+state.A3+" dir="+state.Direction+" sprite="+state.SpriteDirection+" rotation="+state.MotorRotation+" actual="+n.position+" V="+n.velocity+" phases="+string.Join("/",n.ai)+" dir="+n.direction+" sprite="+n.spriteDirection+" rotation="+n.rotation);matched++;
            }
            var line=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcFighterClockMotion",true).GetMethod("Line",Flags);int lines=0;
            World();foreach(float x in new[]{-80,0,16,Main.maxTilesX*16-16,Main.maxTilesX*16,Main.maxTilesX*16+80})foreach(float y in new[]{800,1200,2400})
            {io.Terrain.Reset();var a=new MotionRect(x,y,18,40);var b=new MotionRect(x+4,y+128,20,42);object[] args={io.Terrain,Environment(),a,b,false,PredictionStop.None};Require((bool)line.Invoke(null,args),"Known world-edge line completes without turning native false into missing geometry.");bool native=Collision.CanHitLine(new Vector2(a.X,a.Y),(int)a.Width,(int)a.Height,new Vector2(b.X,b.Y),(int)b.Width,(int)b.Height);Require((bool)args[4]==native,"Original CanHitLine edge x="+x+" y="+y);lines++;}
            Console.WriteLine("PASS AI75/76/93 native scalar="+matched+" retirement/form boundaries="+boundaries+" original line edges="+lines);
            World();Main.netMode=1;var straight=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcStraightMotion",true).GetMethod("Step",Flags);
            for(int i=0;i<256;i++)
            {
                var n=Actor(16,516,new Vector2(1800.125f,1900.25f));n.ai[0]=1;n.velocity=new Vector2((i%31-15)*.33713f,(i%17-8)*.21971f);n.alpha=i%2==0?0:255;
                io.Terrain.Reset();var state=io.Group()[0];object[] args={state,Environment(),io.Terrain,false,PredictionStop.None};Require((bool)straight.Invoke(null,args),"Initialized 516 retains its observed target and motor.");state=(NpcMotionState)args[0];n.AI();
                Require(state.Vx==n.velocity.X && state.Vy==n.velocity.Y && state.Alpha==n.alpha && state.TimeLeft==n.timeLeft,"Original initialized 516 field arithmetic index="+i+" predicted="+state.Vx.ToString("R")+","+state.Vy.ToString("R")+" actual="+n.velocity.X.ToString("R")+","+n.velocity.Y.ToString("R"));
            }
            Console.WriteLine("PASS initialized 516 native scalar256");
            int transforms=0;
            var network=new Harmony("jmr.attached.scalar.transform.outlet");var send=typeof(NetMessage).GetMethod("SendData",Flags);isolatedTransformMessages=0;
            var isolate=new HarmonyMethod(typeof(NativeCombatAttachedWeaponChecks).GetMethod(nameof(IsolateTransformMessage),Flags));isolate.priority=Priority.First+100;
            // Keep the actual server Transform and all of its state changes.
            // Only its original NPC-state/buff network outlet is isolated;
            // unrelated sends still reach the production scene's refusal.
            network.Patch(send,prefix:isolate);
            try
            {
            foreach(int type in new[]{390,416})foreach(int mode in new[]{0,1,2})
            {
                World();Main.netMode=mode;var n=Actor(16,type,new Vector2(1800.125f,1900.25f));n.localAI[0]=1;n.ai[0]=12;n.ai[1]=27;n.ai[2]=3;n.velocity=new Vector2(2.137f,-1.321f);n.direction=-1;n.life-=33;
                io.Terrain.Reset();var group=io.Group();var state=group[Index(group,16)];object[] args={state,Environment(),io.Terrain,0,PredictionStop.None,group,group.Length,false};Require((bool)Model.GetMethod("Step",Flags).Invoke(null,args),"Known orphan transition applies native server/client contract.");state=(NpcMotionState)args[0];n.AI();
                Require(state.EffectiveType==n.type && state.Style==n.aiStyle && state.X==n.position.X && state.Y==n.position.Y && state.Width==n.width && state.Height==n.height && state.Vx==n.velocity.X && state.Vy==n.velocity.Y && state.A0==n.ai[0] && state.A1==n.ai[1] && state.A2==n.ai[2] && state.A3==n.ai[3] && state.Life==n.life && state.LifeMax==n.lifeMax && state.Direction==n.direction && state.Target==n.target,"Native missing-owner transform/noop type="+type+" mode="+mode+" predictedType="+state.EffectiveType+" actualType="+n.type+" life="+state.Life+"/"+n.life);transforms++;
            }
            }
            finally{network.Unpatch(send,HarmonyPatchType.Prefix,network.Id);}
            Require(isolatedTransformMessages==4,"Two actual server transforms issued both original NPC state and buff sends, isolated without networking.");
            Console.WriteLine("PASS original attached orphan server/client transformations="+transforms+" isolatedOriginalNetworkOutlets="+isolatedTransformMessages);
            UnsafeSaucerEdge(io);
        }
        private static void UnsafeSaucerEdge(Inputs io)
        {
            int cases=0;var candidate=io.Source.GetType().GetMethod("TryCandidate",Flags);
            foreach(float x in new[]{-77.16656f,-76.14996f,Main.maxTilesX*16f-59f})
            {
                World();Main.netMode=1;var n=Actor(16,395,new Vector2(x,2005.369f));n.localAI[3]=1;n.ai[0]=0;n.ai[2]=-1;n.ai[3]=244;n.velocity=new Vector2(-3.5f,-.000062759f);
                var child=Actor(24,393,new Vector2(2000,2040));child.ai[0]=16;
                io.Terrain.Reset();var group=io.Group();var state=group[Index(group,16)];object[] args={state,Environment(),io.Terrain,0,PredictionStop.None,group,group.Length,false};
                Require(!(bool)Model.GetMethod("Step",Flags).Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.TerrainUnavailable,"Original unguarded AI76 outside-world tile read is refused, never clamped to invented air.");
                var selected=group[Index(group,24)].Identity;var rejected=(NpcTrajectory)candidate.Invoke(io.Source,new object[]{selected,(long)Main.GameUpdateCount,false,60});
                Require(rejected==null || rejected.Count<=1 && rejected.Stop==PredictionStop.TerrainUnavailable,"Actual component candidate cannot publish an unsafe owner future.");
                bool threw=false;try{n.AI();}catch(IndexOutOfRangeException){threw=true;}
                Require(threw,"Original AI76 same raw edge state throws its own array bound exception x="+x);cases++;
            }
            // The unsafe sample must not latch the private candidate owner.
            // Resume through a distinct real birth in the legal native corridor.
            World();Main.LocalPlayer.position.X=Main.rightWorld*.5f;Main.LocalPlayer.ResetFallDamage();
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),(int)(Main.rightWorld*.5f),1900,395,Start:16,Target:Main.myPlayer);Main.npc[slot].UpdateNPC(slot);
            NPC part=null;for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==393 && (int)Main.npc[i].ai[0]==slot){part=Main.npc[i];break;}
            Require(part!=null,"Legal original birth supplies its real receiving component.");var fresh=io.Group();var identity=fresh[Index(fresh,part.whoAmI)].Identity;
            var recovered=(NpcTrajectory)candidate.Invoke(io.Source,new object[]{identity,(long)Main.GameUpdateCount,false,60});
            Require(recovered!=null && recovered.Count>30,"Unsafe owner refusal does not lock a fresh healthy native component candidate.");
            Console.WriteLine("PASS original AI76 unsafe raw tile edge counterexamples="+cases+" and fresh native candidate recovery; no forecast crosses the original exception");
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            var io=new Inputs(context);Require(GetOptional(io.Source,"Native")==null,"Attached weapons use the production rolling route.");var failures=new List<string>();var rows=new List<string>{"family,mode,frame,slot,type,phase,count,stop,matched,long,fullActions"};long total=0,totalLong=0;
            var observer=new Harmony("jmr.attached.original.actions");var update=typeof(NPC).GetMethod("UpdateNPC",Flags,null,new[]{typeof(int)},null);observer.Patch(io.Terrain.GetType().GetMethod("CellAt",Flags),postfix:new HarmonyMethod(typeof(NativeCombatAttachedWeaponChecks).GetMethod(nameof(TerrainCellAfter),Flags)));observer.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatAttachedWeaponChecks).GetMethod("Before",Flags)),postfix:new HarmonyMethod(typeof(NativeCombatAttachedWeaponChecks).GetMethod("After",Flags)));
            try
            {
                foreach(int family in new[]{390,416,395,491})foreach(int mode in new[]{0,1})
                {
                    try
                    {
                        World();Main.invasionType=family==390?4:family==491?3:0;Main.GameMode=family==395 && mode==1?1:0;
                        // The original AI76 patrol reads raw tile[x,y] with no
                        // clamp. Keep the long natural course away from that
                        // proven unsafe edge; UnsafeSaucerEdge tests it separately.
                        if(family==395){Main.LocalPlayer.position.X=Main.rightWorld*.5f;Main.LocalPlayer.ResetFallDamage();}
                        Main.LocalPlayer.isControlledByFilm=true;Main.LocalPlayer.releaseJump=true;Main.LocalPlayer.statLife=Main.LocalPlayer.statLifeMax=Main.LocalPlayer.statLifeMax2=5000;Main.LocalPlayer.immune=true;Main.LocalPlayer.immuneTime=100000;
                        NativeCombatObservationChecks.Save(io.Host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        Item bow=new Item();bow.SetDefaults(39);Item arrow=new Item();arrow.SetDefaults(40);arrow.stack=9999;Main.LocalPlayer.inventory[0]=bow;Main.LocalPlayer.inventory[54]=arrow;
                        if(mode==1)for(int x=124;x<=126;x++)for(int y=130;y<150;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                        int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),family==395?(int)(Main.rightWorld*.5f):2200,1900,family);Require(slot>=0 && slot<Main.maxNPCs,"Real native component birth.");
                        var frozen=new List<NpcTrajectory>();NpcMotionState predicted=default(NpcMotionState);bool hasPrediction=false;int actions=0,selected=0,published=0,curved=0,velocityCurves=0,positionCurves=0;long matched=0,longMatches=0;float max=0;var phases=new HashSet<float>();var members=new HashSet<int>();
                        before=actor=>
                        {
                            hasPrediction=false;if(!Member(actor.type))return;io.Terrain.Reset();var group=io.Group();int i=Index(group,actor.whoAmI);if(i<0)return;predicted=group[i];PredictionStop stop;var e=Environment();e.PlayerTimelineActive=true;terrainFailure=null;inspectingTerrain=true;
                            try{hasPrediction=NpcMotion.Step(ref predicted,group,group.Length,e,io.Terrain,0,true,out stop);}finally{inspectingTerrain=false;}
                            if(!hasPrediction && stop==PredictionStop.TerrainUnavailable)Console.WriteLine("ATTACHED TERRAIN currentTick="+Main.GameUpdateCount+" actor="+actor.type+" slot="+actor.whoAmI+" position="+actor.position+" size="+actor.width+","+actor.height+" V="+actor.velocity+" phase="+string.Join("/",actor.ai)+" player="+Main.LocalPlayer.position+" PCV="+Main.LocalPlayer.velocity+" consumer="+(terrainFailure??"no failed CellAt"));
                            if(!hasPrediction)Require(stop==PredictionStop.PhaseBoundary || stop==PredictionStop.RandomDecision || stop==PredictionStop.Despawn,"Real actor prefix permits only birth/random/retirement boundary actor="+actor.type+" stop="+stop);
                        };
                        after=actor=>
                        {
                            if(!Member(actor.type))return;members.Add(actor.type);phases.Add(actor.ai[0]);if(hasPrediction && actor.active)
                            {float error=Math.Max(Math.Abs(predicted.X-actor.position.X),Math.Abs(predicted.Y-actor.position.Y));Require(error<=.002f && Math.Abs(predicted.Vx-actor.velocity.X)<=.0001f && Math.Abs(predicted.Vy-actor.velocity.Y)<=.0001f && predicted.EffectiveType==actor.type && predicted.NoTileCollide==actor.noTileCollide,"Native attached fullStep type="+actor.type+" slot="+actor.whoAmI+" predicted="+predicted.X+","+predicted.Y+" V="+predicted.Vx+","+predicted.Vy+" actual="+actor.position+" V="+actor.velocity+" phase="+actor.ai[0]);actions++;}
                        };
                        for(int frame=0;frame<1800;frame++)
                        {
                            var player=Main.LocalPlayer;if(frame%240==0){player.controlLeft=!player.controlLeft;player.controlRight=!player.controlLeft;player.controlJump=frame%480==0;frozen.Clear();}player.immune=true;player.immuneTime=100000;
                            // Owners such as 395/491 initially refuse damage.
                            // Aim at an actually receiving native component so
                            // the production selectable route is exercised.
                            NPC aim=null;float nearest=float.MaxValue;for(int a=0;a<Main.maxNPCs;a++){var candidate=Main.npc[a];if(candidate==null || !candidate.active || !Member(candidate.type) || candidate.dontTakeDamage || candidate.immortal || candidate.life<=0)continue;float distance=Vector2.DistanceSquared(player.Center,candidate.Center);if(distance<nearest){nearest=distance;aim=candidate;}}
                            if(aim!=null)NativeCombatModeledImpactChecks.SampleMouse(context,aim.Center);step();
                            bool has=(bool)Get(Get(io.Host,"Selection"),"HasTarget");if(has)selected++;var path=cache.Read(0);
                            for(int i=frozen.Count-1;i>=0;i--)
                            {
                                var old=frozen[i];long offset=(long)Main.GameUpdateCount-old.CaptureTick;if(offset<0 || offset>=old.Count){frozen.RemoveAt(i);continue;}var actor=Main.npc[old.Identity.Slot];if(actor==null || !actor.active || !ReferenceEquals(old.Identity.Token,actor) || old.Identity.Generation!=actor.generation){frozen.RemoveAt(i);continue;}
                                var point=old[(int)offset];float error=Math.Max(Math.Abs(point.Bounds.X-actor.position.X),Math.Abs(point.Bounds.Y-actor.position.Y));Require(error<=.08f && point.NoTileCollide==actor.noTileCollide,"Frozen attached curve family="+family+" mode="+mode+" actor="+actor.type+" capture="+old.CaptureTick+" offset="+offset+" error="+error+" predicted="+point.Bounds.X+","+point.Bounds.Y+" V="+point.Vx+","+point.Vy+" actual="+actor.position+" V="+actor.velocity+" phase="+actor.ai[0]+","+actor.ai[1]+","+actor.ai[3]+" stop="+old.Stop);matched++;if(offset>=30)longMatches++;max=Math.Max(max,error);
                            }
                            if(path!=null && has && Member(path.Identity.Type)){published++;if(frame%5==0 && path.Count>1)frozen.Add(path);if(path.Count>30)
                                {
                                    // Native attached components can have V=0:
                                    // their position is rewritten from the moving
                                    // owner. Measure actual path curvature too.
                                    bool velocityCurve=Math.Abs(path[30].Vx-path[0].Vx)>.1 || Math.Abs(path[30].Vy-path[0].Vy)>.1;
                                    bool positionCurve=Math.Abs((path[30].Bounds.X-path[15].Bounds.X)-(path[15].Bounds.X-path[0].Bounds.X))>.1 || Math.Abs((path[30].Bounds.Y-path[15].Bounds.Y)-(path[15].Bounds.Y-path[0].Bounds.Y))>.1;
                                    if(velocityCurve)velocityCurves++;if(positionCurve)positionCurves++;if(velocityCurve || positionCurve)curved++;
                                }}
                            rows.Add(string.Join(",",family,mode,frame,path?.Identity.Slot??-1,path?.Identity.Type??0,phases.Count,path?.Count??0,path?.Stop.ToString()??"none",matched,longMatches,actions));
                        }
                        Require(actions>300 && selected>100 && published>100 && longMatches>300 && curved>8,"Natural complete component publishes curved long forecasts family="+family+" mode="+mode+" members="+string.Join("/",members)+" long="+longMatches+" published="+published+" actions="+actions+" selected="+selected+" velocityCurves="+velocityCurves+" positionCurves="+positionCurves);
                        Require(family==390?members.Contains(391):family==416?members.Contains(415):family==395?members.Contains(392) && members.Contains(393) && members.Contains(394):members.Contains(492),"Original birth actually established the required owner/parts family="+family);
                        total+=matched;totalLong+=longMatches;Console.WriteLine("PASS natural AI75 component="+family+" mode="+mode+" matched="+matched+" long="+longMatches+" fullActions="+actions+" members="+string.Join("/",members)+" velocityCurves="+velocityCurves+" positionCurves="+positionCurves+" maxError="+max.ToString("R",CultureInfo.InvariantCulture));
                    }
                    catch(Exception error){failures.Add(family+"-"+mode+":"+error.Message);Console.WriteLine("FAIL natural attached component="+family+" mode="+mode+" "+error);}
                    finally{before=after=null;}
                }
            }
            finally{before=after=null;inspectingTerrain=false;terrainFailure=null;observer.UnpatchAll(observer.Id);Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"attached-weapons.csv"),rows);}
            Require(failures.Count==0,"Complete natural attached components: "+string.Join(" | ",failures));Console.WriteLine("PASS AI75 eight complete natural components matched="+total+" long="+totalLong);
        }
        private static NPC Actor(int slot,int type,Vector2 position){var n=Main.npc[slot]=new NPC{whoAmI=slot};n.SetDefaults(type);n.active=true;n.target=Main.myPlayer;n.position=position;n.direction=n.spriteDirection=1;return n;}
        private static bool Member(int type){return type==390 || type==391 || type==392 || type==393 || type==394 || type==395 || type==415 || type==416 || type==491 || type==492 || type==516;}
        private static int Index(NpcMotionState[] group,int slot){for(int i=0;i<group.Length;i++)if(group[i].Identity.Slot==slot)return i;return -1;}
        private sealed class Inputs
        {
            internal readonly object Host,Source;internal readonly IPredictionTerrain Terrain;private readonly long session;private readonly MethodInfo read,tracking,position;
            internal Inputs(object context){Host=Get(context,"CombatObservation");Source=Get(Host,"Prediction");Terrain=(IPredictionTerrain)Get(Source,"Terrain");session=(long)Get(Host,"Session");read=Source.GetType().GetMethod("Read",Flags);tracking=Source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);position=Source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPositionObservation",true).GetMethod("Capture",Flags);}
            internal NpcMotionState[] Group()
            {
                var result=new List<NpcMotionState>();for(int i=0;i<Main.maxNPCs;i++)
                {var n=Main.npc[i];if(n==null || !n.active || !Member(n.type))continue;var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,state,Terrain};tracking.Invoke(null,args);state=(NpcMotionState)args[1];args=new object[]{n,state,session};position.Invoke(null,args);state=(NpcMotionState)args[1];state.WeaponRelationsCaptured=state.WeaponRelationsComplete=true;result.Add(state);}return result.ToArray();
            }
        }
        private static PredictionEnvironment Environment(){var p=Main.LocalPlayer;return new PredictionEnvironment{Players=new PredictionPlayers(new[]{new MotionRect((int)p.position.X,(int)p.position.Y,p.width,p.height)},1,new[]{p.whoAmI}),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVelocityCaptured=true,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerDead=p.dead,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface,Multiplayer=Main.netMode==1,Expert=Main.expertMode,Day=Main.dayTime,InvasionType=Main.invasionType};}
        private static void World()
        {
            typeof(NativeCombatDiveFlightChecks).GetMethod("World",Flags).Invoke(null,null);
            // Original Strike/checkDead needs Main.Initialize's genuine loot
            // database. Preserve the native receiving/death path, including
            // actual WorldItem slot identities; no loot method is bypassed.
            for(int i=0;i<Main.item.Length;i++)Main.item[i].whoAmI=i;
            if(Main.ItemDropSolver==null){Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}
        }
    }
}
