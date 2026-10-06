using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;
namespace NativeWorldTextProbe
{
    internal static class NativeFiniteFlightChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");var read=source.GetType().GetMethod("Read",Flags);var capture=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation").GetMethod("Capture",Flags);
            var kernel=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcFiniteFlightMotion").GetMethod("Step",Flags);
            for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
            foreach(var npc in Main.npc)npc.active=false;Main.netMode=1;Main.dayTime=false;Main.eclipse=Main.pumpkinMoon=true;Main.worldSurface=20;Main.tileSolid[TileID.Stone]=true;
            var p=Main.LocalPlayer;p.active=true;p.dead=p.ghost=false;p.position=new Vector2(900,960-p.height);p.velocity=Vector2.Zero;p.controlLeft=p.controlRight=p.controlJump=false;p.carpetFrame=-1;p.gravity=.4f;p.maxFallSpeed=10;p.maxRunSpeed=3;p.accRunSpeed=6;p.runAcceleration=.08f;p.runSlowdown=.2f;
            for(int x=1;x<Main.maxTilesX-1;x++){Main.tile[x,60].active(true);Main.tile[x,60].type=TileID.Stone;}
            Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
            int cases=0;
            foreach(int type in new[]{261,265,34,289,694,250,75,82,122,169,182,268,316,330,490,253})foreach(int sign in new[]{-1,1})foreach(float vx in new[]{-4f,0f,4f})foreach(int branch in new[]{0,1,2})
            {
                var n=new NPC();n.SetDefaults(type);n.whoAmI=199;n.active=true;n.position=new Vector2(sign>0?600:1100,800);n.velocity=new Vector2(vx,branch==2?-2:1);n.target=p.whoAmI;n.direction=sign;n.directionY=1;n.timeLeft=750;n.ai[0]=n.position.X;n.ai[1]=n.position.Y;
                if(n.aiStyle==10){n.ai[0]=branch==1?199:-99;n.ai[1]=branch==2?601:5;n.ai[2]=10;n.ai[3]=type==694?branch==0?0:branch==1?3:2:0;}
                if(n.aiStyle==22){n.ai[2]=branch==1?-199:branch==2?59:0;n.collideX=branch==2;n.oldVelocity=new Vector2(3,2);n.wet=type==75 && branch==1;}
                var state=(NpcMotionState)read.Invoke(null,new object[]{n,1L});terrain.Reset();object[] args={n,state,terrain};capture.Invoke(null,args);state=(NpcMotionState)args[1];
                var env=new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,WorldWidth=120,WorldHeight=120,WorldSurface=20,Multiplayer=true,Eclipse=true,PumpkinMoon=true};
                object[] action={state,env,terrain,false,PredictionStop.None};Require((bool)kernel.Invoke(null,action),"Declared stable native movement action available type="+type);state=(NpcMotionState)action[0];n.AI();
                Require(Math.Abs(n.velocity.X-state.Vx)<.0001f && Math.Abs(n.velocity.Y-state.Vy)<.0001f,"Original whole AI versus finite action type="+type+" branch="+branch+" vx="+vx+" native="+n.velocity+" model="+state.Vx+","+state.Vy);cases++;
            }
            Console.WriteLine("PASS FINITE-FLIGHT independent whole-original movement actions="+cases);
            Main.netMode=0;var cache=(NpcPredictionCache)Get(source,"Cache");NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,marker:true));Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));
            foreach(int type in new[]{261,265,34,289,694,250,75,82,122,169,182,268,316,330,490,253})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=false;n.immortal=false;n.friendly=false;n.position=new Vector2(650,700);n.velocity=new Vector2(1,-.2f);n.target=p.whoAmI;n.timeLeft=750;
                if(type==694){n.ai[2]=-300;n.ai[3]=0;}n.ai[0]=n.aiStyle==22?n.position.X:0;n.ai[1]=n.aiStyle==22?n.position.Y:0;
                NativeCombatObservationChecks.Fresh(context,host);var frozen=cache.Read(0);Require(frozen!=null && frozen.Count==121 && frozen.Quality==PredictionQuality.StructuredApproximation,"Default Host Source first sample owns full finite route type="+type+" count="+frozen?.Count+" stop="+frozen?.Stop);
                var world=Get(host,"World");Call(world,"Prepare");Main.screenPosition=new Vector2(300,400);NativeCombatPresentationChecks.Project(world);Require((int)Get(world,"StrokeCount")>(int)Get(world,"eventEnd"),"Production cache route reaches final camera consumer type="+type);
                if(type!=694)
                {
                    int selected=0,published=0;float maxAligned=0;bool crossed=false,turned=false;int initialSign=Math.Sign(p.Center.X-n.Center.X);float previousVx=n.velocity.X;
                    for(int step=1;step<=120;step++){NativeQuickItemChecks.BeginWorldStep();n.AI();if(n.noTileCollide)n.position+=n.velocity;else Call(n,"UpdateCollision");var point=frozen[step];Require(Math.Abs(point.Bounds.X-n.position.X)<.3f && Math.Abs(point.Bounds.Y-n.position.Y)<.3f,"Frozen actual future type="+type+" step="+step+" predicted="+point.Bounds.X+","+point.Bounds.Y+" native="+n.position);
                        crossed|=Math.Sign(p.Center.X-n.Center.X)!=initialSign;turned|=n.velocity.X*previousVx<0;previousVx=n.velocity.X;
                        Call(Get(context,"nativeNpcs"),"BeginCompleted",(long)Main.GameUpdateCount);Call(host,"Update",Main.GameUpdateCount);var refreshed=cache.Read(0);if((bool)Get(Get(host,"Selection"),"HasTarget"))selected++;if(refreshed!=null)published++;
                        if(step<=90){Require(refreshed!=null && refreshed.Count>30,"Continuous legal route has a real future type="+type);var a=refreshed[30].Bounds;var b=frozen[step+30].Bounds;float delta=(float)Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));maxAligned=Math.Max(maxAligned,delta);Require(delta<.5f,"Adjacent forecasts compare the same absolute future world action type="+type+" step="+step+" delta="+delta);}
                    }
                    Require(selected==120 && published==120,"Continuous legal/selected/publication denominator is120 for type="+type);
                    Console.WriteLine("FINITE WINDOW type="+type+" legal=120 selected="+selected+" published="+published+" frozen120=.3px alignedFutureMax="+maxAligned+" crossed="+crossed+" turned="+turned+" finalV="+n.velocity);
                }
            }
            // Two live numbered players distinguish native pursuit/escape
            // phases from the nearest-player assumption. This is isolated
            // targeting evidence, not a network topology or multiplayer test.
            var remote=Main.player[1];remote.active=true;remote.dead=false;remote.position=new Vector2(1200,960-remote.height);remote.velocity=Vector2.Zero;remote.gravity=.4f;remote.maxFallSpeed=10;remote.maxRunSpeed=3;remote.accRunSpeed=6;remote.runAcceleration=.08f;remote.runSlowdown=.2f;remote.carpetFrame=-1;
            foreach(int type in new[]{82,75,490,169,253,330})
            {
                foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=new Vector2(1,0);n.target=1;n.ai[2]=-20;n.timeLeft=750;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Console.WriteLine("FINITE TWO-PLAYER type="+type+" count="+path?.Count+" stop="+path?.Stop+" premise="+Get(source,"targetPlayer"));
                Require(path!=null && path.Count>1 && (type==253 || type==330?path.Count==121:path.Stop==PredictionStop.MissingDependency && path.Count>=20),"Actual Source retains escape prefix then stops before a new numbered player future type="+type+" count="+path?.Count+" stop="+path?.Stop);
                var states=(NpcMotionState[])Get(source,"states");int expected=type==253 || type==330?Main.myPlayer:1;Require((int)Get(source,"targetPlayer")==expected && states[0].PlayerIndex==1,"Player premise identity matches native phase type="+type);
                if(type==169)Require(path[1].Vx>1,"169 rewrites horizontal direction after fixing its scan column.");
            }
            foreach(var npc in Main.npc)npc.active=false;var faded=Main.npc[2];faded.SetDefaults(316);faded.whoAmI=2;faded.active=true;faded.dontTakeDamage=faded.immortal=faded.friendly=false;faded.position=new Vector2(650,700);faded.velocity=Vector2.UnitX;faded.target=1;faded.ai[3]=1;faded.alpha=0;remote.dead=true;
            NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)!=null && cache.Read(0).Count>1 && cache.Read(0).Stop==PredictionStop.Despawn,"Source retains316 independently fading departure when old player is dead.");
            faded.SetDefaults(82);faded.whoAmI=2;faded.active=true;faded.dontTakeDamage=faded.immortal=faded.friendly=false;faded.position=new Vector2(650,700);faded.target=1;faded.ai[2]=-20;NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)==null,"A necessary dead numbered player in82 escape is not replaced by the local player.");remote.active=false;
            Console.WriteLine("PASS FINITE-FLIGHT default first-sample Host Source Cache WorldLayer16 direct members; frozen120 actual AI actions15 deterministic members; no future RNG/world replay.");
        }
    }
}
