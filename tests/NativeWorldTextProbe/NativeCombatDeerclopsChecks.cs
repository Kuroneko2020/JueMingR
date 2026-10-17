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
    internal static class NativeCombatDeerclopsChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Type Model {get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcDeerclopsMotion",true);}}
        private static Action<NPC> before,after;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static bool Network(int msgType){return msgType!=23 && msgType!=27 && msgType!=28;}
        private sealed class Outlets:IDisposable
        {
            private readonly Harmony patch=new Harmony("JueMingR.Probe.DeerclopsOutlets");
            internal Outlets(){var method=new HarmonyMethod(typeof(NativeCombatDeerclopsChecks),nameof(Network));method.priority=Priority.First+100;patch.Patch(typeof(NetMessage).GetMethod("SendData",Flags),prefix:method);}
            public void Dispose(){patch.UnpatchAll(patch.Id);}
        }
        private static bool Motor(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop)
        {object[] args={n,e,terrain,elapsed,PredictionStop.None,null,0,false};bool ok=(bool)Model.GetMethod("Step",Flags).Invoke(null,args);n=(NpcMotionState)args[0];stop=(PredictionStop)args[4];return ok;}
        internal static void Boundaries(object context)
        {
            Setup(0);var io=new Inputs(context);var native=Actor(2400);native.homeTileX=150;native.homeTileY=150;native.ai[2]=150;native.ai[3]=150;native.ai[0]=3;native.ai[1]=27;
            Main.LocalPlayer.position=new Vector2(2700,2400-Main.LocalPlayer.height);
            var e=EnvironmentFor(native);var state=io.Capture(native);io.Terrain.Reset();PredictionStop stop;int cases=0;
            Require(Motor(ref state,e,io.Terrain,1,out stop) && state.DeerSlowConsumerAt==2,"Actual Slow insertion keeps the current known NPC action and ends before the next player consumer.");cases++;
            Require(!Motor(ref state,e,io.Terrain,2,out stop) && stop==PredictionStop.BuffTransition,"New Slow is not silently applied after the ordinary player continuation.");cases++;
            foreach(bool god in new[]{false,true})
            {Main.LocalPlayer.creativeGodMode=god;Main.LocalPlayer.buffImmune[32]=!god;state=io.Capture(native);io.Terrain.Reset();Require(Motor(ref state,e,io.Terrain,1,out stop) && state.DeerSlowConsumerAt==int.MaxValue,"Native immunity/God rejects Slow without truncating the known controller.");cases++;}
            Main.LocalPlayer.creativeGodMode=false;Main.LocalPlayer.buffImmune[32]=false;
            foreach(bool complete in new[]{false,true})
            {
                var p=Main.LocalPlayer;p.AddBuff(32,1);p.slow=true;state=io.Capture(native,complete);int actual=(int)Model.GetMethod("SlowChangeAction",Flags).Invoke(null,new object[]{state.DeerPlayers.Local,complete,Main.myPlayer});
                Require(actual==(complete?3:2),"Time1->0 retains Slow for that player action before next expiry consumer.");cases++;
                for(int i=0;i<p.buffType.Length;i++)p.buffType[i]=p.buffTime[i]=0;p.slow=false;
            }
            state=io.Capture(native);state.DeerPlayers=null;Require(!Motor(ref state,e,io.Terrain,1,out stop) && stop==PredictionStop.MissingDependency,"Missing native home/player inputs remain a dependency boundary.");cases++;
            native.position=new Vector2(-31.125f,-155.125f);native.homeTileX=native.homeTileY=-1;native.ai[0]=-1;state=io.Capture(native);io.Terrain.Reset();var home=native.Bottom.ToTileCoordinates();
            Require(Motor(ref state,e,io.Terrain,1,out stop) && state.A2==home.X && state.A3==home.Y && !state.DeerHomeInitialized,"Native signed tile shift preserves the both-minus-one home sentinel for the next action.");cases++;
            Console.WriteLine("PASS Deerclops mechanism boundaries="+cases);
        }
        internal static void Lifecycle(object context)
        {
            var io=new Inputs(context);int actions=0,solidCases=0;
            var phases=new[]{new[]{-1,0},new[]{0,89},new[]{0,119},new[]{0,239},new[]{1,35},new[]{1,79},new[]{2,31},new[]{2,59},new[]{3,27},new[]{3,29},new[]{3,59},new[]{4,55},new[]{4,89},new[]{5,29},new[]{5,59},new[]{6,419},new[]{6,599},new[]{6,1499},new[]{7,39},new[]{7,59},new[]{8,39}};
            using(var outlets=new Outlets())foreach(int mode in new[]{0,1,2})foreach(var phase in phases)
            {
                Setup(0);var n=Actor(2300);Main.netMode=mode;n.homeTileX=144;n.homeTileY=150;n.ai[2]=144;n.ai[3]=150;n.ai[0]=phase[0];n.ai[1]=phase[1];n.timeLeft=321;n.localAI[0]=1;n.localAI[1]=2;n.localAI[2]=79;n.localAI[3]=29;n.velocity=new Vector2(-.111f,phase[0]==0?0:-1.25f);
                Main.LocalPlayer.position=new Vector2(2700.25f,2358);Main.dayRate=3;var predicted=io.Capture(n);io.Terrain.Reset();PredictionStop stop;
                bool valid=Motor(ref predicted,EnvironmentFor(n),io.Terrain,1,out stop);Require(valid || stop==PredictionStop.Despawn,"Native observed AI123 scalar is computable mode="+mode+" phase="+string.Join("/",phase)+" stop="+stop);
                n.AI();Equal(predicted,n,"mode="+mode+" phase="+string.Join("/",phase));actions++;
            }
            using(var outlets=new Outlets())for(int variant=0;variant<10;variant++)
            {
                Setup(variant%2);var n=Actor(2400);n.velocity=new Vector2(1.25f,0);n.localAI[3]=variant%2==0?0:30;
                if(variant>0){n.homeTileX=150;n.homeTileY=150;n.ai[2]=150;n.ai[3]=150;}
                float center=n.Center.X+(variant>=2 && variant<=4?450:variant>=5 && variant<=7?2400:120);
                if(variant==2 || variant==5)center=Adjacent(center,-1);
                if(variant==4 || variant==7)center=Adjacent(center,1);
                Main.LocalPlayer.position=new Vector2(center-Main.LocalPlayer.width*.5f,n.Center.Y-Main.LocalPlayer.height*.5f);
                if(variant==8)Main.LocalPlayer.ghost=true;if(variant==9){Main.LocalPlayer.dead=true;n.ai[0]=6;n.timeLeft=0;}
                var predicted=io.Capture(n);io.Terrain.Reset();PredictionStop stop;Require(Motor(ref predicted,EnvironmentFor(n),io.Terrain,1,out stop),"Native birth/distance/validity representative computes before actual side effects.");n.AI();Equal(predicted,n,"variant="+variant);actions++;
            }
            Setup(0);var terrain=io.Terrain;var e=EnvironmentFor(Actor(2400));
            // Isolate the queried tile: untouched neighbouring floor would
            // otherwise turn every platform/actuation negative into stone.
            for(int x=159;x<=164;x++)for(int y=149;y<=156;y++)Main.tile[x,y].active(false);
            foreach(int kind in new[]{0,1,2,3,4})foreach(bool accept in new[]{false,true})foreach(float edge in new[]{2399.9998f,2400f,2400.0002f})
            {
                var tile=Main.tile[160,150];tile.active(kind!=0);tile.type=(ushort)(kind>=2?19:1);tile.inActive(kind==4);tile.frameX=(short)(kind==3?8*18:0);tile.frameY=0;tile.halfBrick(kind==1);
                var pos=new Vector2(2560.375f,edge-20);bool original=Collision.SolidCollision(pos,40,20,accept);object[] args={terrain,e,new MotionRect(pos.X,pos.Y,40,20),accept,false,PredictionStop.None};terrain.Reset();Require((bool)Model.GetMethod("Solid",Flags).Invoke(null,args) && (bool)args[4]==original,"Original four-argument foot query retains proper platform/half/actuation/ULP branches.");solidCases++;
            }
            Console.WriteLine("PASS Deerclops original AI123 actions="+actions+" foot queries="+solidCases);
        }
        private static void Equal(NpcMotionState p,NPC n,string note)
        {
            Require(p.X==n.position.X && p.Y==n.position.Y && p.Vx==n.velocity.X && p.Vy==n.velocity.Y && p.A0==n.ai[0] && p.A1==n.ai[1] && p.A2==n.ai[2] && p.A3==n.ai[3] && p.L0==n.localAI[0] && p.L1==n.localAI[1] && p.L2==n.localAI[2] && p.L3==n.localAI[3] && p.TimeLeft==n.timeLeft && p.Active==n.active && p.Life==n.life && p.Target==n.target && p.Direction==n.direction && p.SpriteDirection==n.spriteDirection && p.Health.DontTakeDamage==n.dontTakeDamage && p.CanReceive==(!n.dontTakeDamage && !n.immortal),
                "Original Deerclops strict "+note+" P="+p.X.ToString("R")+","+p.Y.ToString("R")+"/"+n.position+" V="+p.Vx.ToString("R")+","+p.Vy.ToString("R")+"/"+n.velocity+" A="+p.A0+","+p.A1+","+p.A2+","+p.A3+"/"+string.Join(",",n.ai)+" L="+p.L0+","+p.L1+","+p.L2+","+p.L3+"/"+string.Join(",",n.localAI)+" lifetime="+p.TimeLeft+"/"+n.timeLeft+" active="+p.Active+"/"+n.active+" life="+p.Life+"/"+n.life+" face="+p.Direction+","+p.SpriteDirection+"/"+n.direction+","+n.spriteDirection);
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            string selector=System.Environment.GetEnvironmentVariable("JMR_DEERCLOPS_SCENE");int chosen=-1;Require(string.IsNullOrEmpty(selector) || int.TryParse(selector,out chosen) && chosen>=0 && chosen<4,"Deerclops scene selector is0..3.");Console.WriteLine("Deerclops actual scenes="+(chosen<0?"0,1,2,3":chosen.ToString()));
            var io=new Inputs(context);var observer=new Harmony("JueMingR.Probe.DeerclopsNativeSlot");observer.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatDeerclopsChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatDeerclopsChecks),nameof(After)));
            var rows=new List<string>{"scene,frame,pathType,pathSlot,count,stop,phase,clock,shield,slow,matches,long,teleports,retired"};
            try
            {
                using(var outlets=new Outlets())for(int scene=0;scene<4;scene++)
                {
                    if(chosen>=0 && chosen!=scene)continue;Setup(scene);var p=Main.LocalPlayer;p.position=new Vector2(scene==1?3000:scene>=2?5800:2490,2400-p.height);p.ResetFallDamage();p.isControlledByFilm=true;p.immune=true;p.immuneTime=1000000;p.statLife=p.statLifeMax=p.statLifeMax2=5000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
                    if(scene==2)for(int x=130;x<=190;x++)for(int y=150;y<Main.maxTilesY;y++)Main.tile[x,y].active(false);
                    NativeCombatObservationChecks.Save(io.Host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));var actor=Actor(scene==1?2400:scene==2?2500:2400);var frozen=new List<NpcTrajectory>();
                    int actions=0,matches=0,longMatches=0,teleports=0,published=0,slow=0,restore=0,receiving=0,hidden=0;double maximum=0,tangent=0;bool previousSlow=false,waiting=false,retired=false,retirePredicted=false;long waitTick=-1;var phases=new HashSet<int>();NpcMotionState predicted=default(NpcMotionState);PredictionStop actionStop=PredictionStop.None;bool valid=false;
                    before=n=>{valid=false;if(!ReferenceEquals(n,actor))return;io.Terrain.Reset();predicted=io.Capture(n);valid=NpcMotion.Step(ref predicted,new[]{predicted},1,EnvironmentFor(n),io.Terrain,1,true,out actionStop);Require(valid || actionStop==PredictionStop.Despawn,"Current Deerclops pure full action stop="+actionStop);};
                    after=n=>{if(!ReferenceEquals(n,actor))return;if(!valid){if(actionStop==PredictionStop.Despawn){Require(!n.active,"Known retire occurs at the same original slot action.");retirePredicted=true;}return;}double error=Vector2.Distance(n.position,new Vector2(predicted.X,predicted.Y));Require(error<=.002 && predicted.Vx==n.velocity.X && predicted.Vy==n.velocity.Y && predicted.A0==n.ai[0] && predicted.A1==n.ai[1] && predicted.A2==n.ai[2] && predicted.A3==n.ai[3] && predicted.L3==n.localAI[3] && predicted.CanReceive==(!n.dontTakeDamage && !n.immortal),"Current Deerclops action error="+error.ToString("R")+" prior/currentphase="+string.Join("/",n.ai)+" P="+predicted.X.ToString("R")+","+predicted.Y.ToString("R")+"/"+n.position+" V="+predicted.Vx.ToString("R")+","+predicted.Vy.ToString("R")+"/"+n.velocity);if(predicted.NewSegment)teleports++;actions++;};
                    try
                    {
                        for(int frame=0;frame<1800;frame++)
                        {
                            if(scene==0 && frame==600){frozen.Clear();waiting=false;p.controlRight=true;}
                            if(scene==0 && frame==900){frozen.Clear();waiting=false;p.controlRight=false;p.controlLeft=true;}
                            if(scene==3 && frame==0)Main.dayRate=120;
                            NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();Require(p.active && !p.dead,"Native Deerclops owner remains alive under the declared no-new-hit premise.");phases.Add((int)actor.ai[0]);bool currentSlow=p.FindBuffIndex(32)>=0;if(currentSlow && !previousSlow)slow++;previousSlow=currentSlow;
                            if(actor.dontTakeDamage)hidden++;else receiving++;var path=cache.Read(0);
                            if(path!=null && ReferenceEquals(path.Identity.Token,actor))
                            {
                                published++;if(path.Count<30 && path.Stop==PredictionStop.BuffTransition && !waiting){waiting=true;waitTick=path.CaptureTick;}
                                if(waiting && path.CaptureTick>waitTick && path.Count>=30){restore++;waiting=false;}
                                if(frame%5==0 && path.Count>1)frozen.Add(path);
                            }
                            for(int i=frozen.Count-1;i>=0;i--)
                            {
                                var f=frozen[i];int offset=(int)((long)Main.GameUpdateCount-f.CaptureTick);if(offset>=f.Count){frozen.RemoveAt(i);continue;}if(offset<=0)continue;var point=f[offset];double error=Vector2.Distance(actor.position,new Vector2(point.Bounds.X,point.Bounds.Y));matches++;maximum=Math.Max(maximum,error);
                                if(offset>=30){longMatches++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(f[0].Bounds.X+f[0].Vx*offset,f[0].Bounds.Y+f[0].Vy*offset)));}
                                Require(error<=.08 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal) && point.NoTileCollide==actor.noTileCollide,"Frozen Deerclops scene="+scene+" frame="+frame+" offset="+offset+" error="+error.ToString("R")+" P="+actor.position+" V="+actor.velocity+" phase="+string.Join("/",actor.ai)+" slow="+currentSlow+" stop="+f.Stop);
                            }
                            rows.Add(string.Join(",",scene,frame,path==null?"":path.Identity.Type.ToString(),path==null?"":path.Identity.Slot.ToString(),path?.Count??0,path?.Stop.ToString()??"none",actor.ai[0],actor.ai[1],actor.dontTakeDamage?1:0,currentSlow?1:0,matches,longMatches,teleports,actor.active?0:1));
                            if(!actor.active){retired=true;Require(scene==3 && retirePredicted && path==null && !(bool)Get(Get(io.Host,"Selection"),"HasTarget"),"Actual scheduled retirement revokes target and cache, without extending the original lifetime.");break;}
                        }
                        Require(actions>=300,"Native continuing/retirement action denominator is actually exercised.");
                        if(scene==0){Require(published>0 && matches>=30 && longMatches>=30 && tangent>2 && phases.Contains(1) && phases.Contains(4),"Close approach has two observed forward attacks, then both-side attack and real curved future.");}
                        if(scene==1){Require(slow>0 && restore>0 && longMatches>=30 && phases.Contains(3),"Real original Slow application withdraws old future and fresh slowed input restores long prediction.");}
                        if(scene==2){Require(teleports>0 && phases.Contains(6) && phases.Contains(7),"Natural falling go-home condition really reaches observed forty-action relocation.");}
                        if(scene==3)Require(retired && phases.Contains(8),"Actual dayRate lifetime and return-home flow reaches natural retirement.");
                        Console.WriteLine("PASS Deerclops natural scene="+scene+" actions="+actions+" matched="+matches+" long="+longMatches+" slow="+slow+" restore="+restore+" teleports="+teleports+" hidden="+hidden+" receiving="+receiving+" retired="+retired+" maxError="+maximum.ToString("R"));
                    }
                    finally{before=after=null;}
                }
            }
            finally{before=after=null;observer.UnpatchAll(observer.Id);Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"deerclops-scenes.csv"),rows);Main.dayRate=1;}
        }
        private sealed class Inputs
        {
            internal readonly object Host,Source;internal readonly IPredictionTerrain Terrain;private readonly long session;private readonly MethodInfo read,tracking,capture;
            internal Inputs(object context){Host=Get(context,"CombatObservation");Source=Get(Host,"Prediction");Terrain=(IPredictionTerrain)Get(Source,"Terrain");session=(long)Get(Host,"Session");read=Source.GetType().GetMethod("Read",Flags);tracking=Source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);capture=Source.GetType().GetMethod("CaptureDeerclopsInputs",Flags);}
            internal NpcMotionState Capture(NPC n,bool firstCompleted=true){var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,state,Terrain};tracking.Invoke(null,args);state=(NpcMotionState)args[1];args=new object[]{n,state,firstCompleted};capture.Invoke(null,args);return (NpcMotionState)args[1];}
        }
        private static NPC Actor(int x){int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),x,2400,668,Start:16,Target:Main.myPlayer);return Main.npc[slot];}
        private static float Adjacent(float value,int direction){return BitConverter.ToSingle(BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(value),0)+direction),0);}
        private static void Setup(int scene)
        {
            NativeCombatLiveContextChecks.FlightWorld();Main.maxTilesY=480;Main.bottomWorld=7680;Main.tile=new Tile[Main.maxTilesX,Main.maxTilesY];for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<Main.maxTilesY;y++){Main.tile[x,y]=new Tile();if(y>=150){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}}
            Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=scene==0?0:1;Main.getGoodWorld=false;Main.dayTime=false;Main.time=1800;Main.dayRate=1;Main.rand=new Terraria.Utilities.UnifiedRandom(12300+scene);
            if(Main.instance==null){Main.instance=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(Main.instance);}Main.instance.CameraModifiers=new Terraria.Graphics.CameraModifiers.CameraModifierStack();
            Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true};Main.LocalPlayer.position=new Vector2(2600.125f,2400-Main.LocalPlayer.height);Main.LocalPlayer.ResetFallDamage();for(int i=0;i<Main.maxPlayers;i++)if(i!=Main.myPlayer && Main.player[i]!=null)Main.player[i].active=false;
        }
        private static PredictionEnvironment EnvironmentFor(NPC n)
        {
            var p=Main.player[n.target];var slots=new List<int>();var exact=new List<MotionRect>();var integer=new List<MotionRect>();var alive=new List<bool>();var dead=new List<bool>();for(int i=0;i<Main.maxPlayers;i++){var row=Main.player[i];if(row==null || !row.active)continue;slots.Add(i);exact.Add(new MotionRect(row.position.X,row.position.Y,row.width,row.height));integer.Add(new MotionRect((int)row.position.X,(int)row.position.Y,row.width,row.height));alive.Add(!row.dead && !row.ghost);dead.Add(row.dead);}
            return new PredictionEnvironment{Players=new PredictionPlayers(integer.ToArray(),integer.Count,slots.ToArray(),exact.ToArray(),alive.ToArray(),dead.ToArray()),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerVelocityCaptured=true,PlayerTimelineActive=p.active && !p.dead && !p.ghost,PlayerDead=p.dead || !p.active,Expert=Main.expertMode,GoodWorld=Main.getGoodWorld,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer};
        }
    }
}
