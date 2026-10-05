using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatPhaseReviewChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var terrain=(IPredictionPlayerTerrain)Get(source,"Terrain");
            if(Environment.GetEnvironmentVariable("JUEMINGR_REVIEW_CASE")=="clock")
            {
                var p=Main.LocalPlayer;p.position=new Vector2(900,950);p.active=true;p.dead=false;
                var owner=Main.npc[1]=new NPC();owner.SetDefaults(35);owner.whoAmI=1;owner.active=true;owner.position=new Vector2(800,800);owner.ai[1]=0;owner.velocity=Vector2.Zero;
                var n=Main.npc[2]=new NPC();n.SetDefaults(36);n.whoAmI=2;n.active=true;n.target=Main.myPlayer;n.ai[0]=n.ai[1]=1;n.ai[2]=0;n.ai[3]=170;n.position=new Vector2(700,1030);
                var read=source.GetType().GetMethod("Read",Flags);var state=(NpcMotionState)read.Invoke(null,new object[]{n,Get(host,"Session")});var args=new object[]{n,state,Get(host,"Session")};source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPositionObservation").GetMethod("Capture",Flags).Invoke(null,args);state=(NpcMotionState)args[1];
                foreach(bool expert in new[]{false,true})
                {
                    var probe=new PlayerQueryBoundary((IPredictionTerrain)terrain);var env=new PredictionEnvironment{PlayerIndex=Main.myPlayer,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,Expert=expert};
                    var player=new PredictionPlayerMotion{X=p.position.X,Y=p.position.Y,Width=p.width,Height=p.height,GravityDirection=1,MaxFall=10};
                    var path=new RollingNpcPrediction().Prepare(new[]{(NpcMotionState)read.Invoke(null,new object[]{owner,Get(host,"Session")}),state},2,1,1,120,1,env,player,probe,new[]{true,true});
                    Console.WriteLine("PARENT CLOCK expert="+expert+" playerQueries="+probe.PlayerQueries+" count="+(path==null?0:path.Count)+" stop="+(path==null?"null":path.Stop.ToString()));
                    Require(path!=null && (expert?probe.PlayerQueries==1 && path.Count==1:probe.PlayerQueries==0 && path.Count==121),"Normal170 ends at290 without unrelated future; Expert crosses actual300 clock and requires its player premise.");
                }
            }
            else if(Environment.GetEnvironmentVariable("JUEMINGR_REVIEW_CASE")=="confused")
            {
                foreach(var actor in Main.npc)actor.active=false;
                for(int i=0;i<Main.maxPlayers;i++){if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};Main.player[i].active=false;}
                var p=Main.LocalPlayer;p.active=true;p.dead=false;p.tankPet=-1;p.position=new Vector2(900,950);
                foreach(int immune in new[]{58,177,61}){var control=new NPC();control.SetDefaults(immune);Require(control.buffImmune[31],"Original immunity control: "+immune);control.AddBuff(31,120);Require(control.FindBuffIndex(31)<0,"Immune actor does not acquire fabricated confusion.");}
                foreach(bool confused in new[]{false,true})
                {
                    var n=Main.npc[2]=new NPC();n.SetDefaults(153);n.whoAmI=2;n.active=true;n.target=Main.myPlayer;n.position=new Vector2(1000,950);n.velocity=n.oldVelocity=new Vector2(0,.2f);n.direction=0;n.directionY=1;n.ai[0]=n.ai[1]=0;
                    Require(!n.buffImmune[31],"Original type153 really accepts Confused.");if(confused)n.AddBuff(31,120);n.UpdateNPC_BuffSetFlags(false);Require(n.confused==confused && (!confused || n.FindBuffIndex(31)>=0),"Original buff application before motion.");
                    var state=(NpcMotionState)source.GetType().GetMethod("Read",Flags).Invoke(null,new object[]{n,Get(host,"Session")});var captured=new object[]{n,state,terrain};((IPredictionTerrain)terrain).Reset();source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation").GetMethod("Capture",Flags).Invoke(null,captured);state=(NpcMotionState)captured[1];
                    var env=new PredictionEnvironment{PlayerIndex=Main.myPlayer,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface};
                    int before=state.ConfusedTicks;n.AI();PredictionStop stop;Require(NpcMotion.Step(ref state,new[]{state},1,env,(IPredictionTerrain)terrain,1,true,out stop),"Actual finite tortoise motion: "+stop);
                    Console.WriteLine("CONFUSED PHASE applied="+confused+" nativeDirection="+n.direction+" modelDirection="+state.Direction+" nativeVx="+n.velocity.X+" modelVx="+state.Vx+" ticks="+before+"->"+state.ConfusedTicks);
                    Require(n.direction==state.Direction && Math.Abs(n.velocity.X-state.Vx)<.001f && state.ConfusedTicks==Math.Max(0,before-1),"Action-before confusion is used once by actual TargetClosest and walking.");
                }
            }
            else
            {
                Main.tileSolid[421]=Main.tileSolid[422]=true;var originals=new Tile[16];for(int x=55;x<71;x++){originals[x-55]=new Tile();originals[x-55].CopyFrom(Main.tile[x,60]);}
                try
                {
                    foreach(int type in new[]{421,422})foreach(float speed in new[]{24f,60f})
                    {
                        for(int x=55;x<71;x++){var t=Main.tile[x,60];t.active(true);t.type=(ushort)type;t.slope(0);t.halfBrick(false);t.liquid=0;}
                        var p=new Player{whoAmI=1,active=true,width=20,height=32,position=new Vector2(961,928),velocity=new Vector2(speed,0),gravity=.4f,gravDir=1,carpetFrame=-1};
                        var motion=(PredictionPlayerMotion)source.GetType().GetMethod("ReadPlayer",Flags).Invoke(null,new object[]{p});var body=new NpcMotionState{X=p.position.X,Y=p.position.Y,Vx=p.velocity.X,Vy=p.velocity.Y,Width=20,Height=32};
                        p.SlopeDownMovement();p.DryCollision(false,false);float dryX=p.position.X;p.SlopingCollision(false,false);Collision.StepConveyorBelt(p,1);if(Collision.up)p.velocity.Y=.01f;
                        ((IPredictionTerrain)terrain).Reset();PredictionStop stop;Require(terrain.MovePlayer(ref body,default(PredictionEnvironment),ref motion,out stop),"High-speed belt snapshot: "+stop);
                        Console.WriteLine("FINAL BELT type="+type+" speed="+speed+" dryX="+dryX+" finalX="+p.position.X+" modelX="+body.X+" nativeV="+p.velocity+" modelV="+new Vector2(body.Vx,body.Vy));
                        Require(Math.Abs(p.position.X-dryX)>2 && Vector2.Distance(p.position,new Vector2(body.X,body.Y))<.02f && Vector2.Distance(p.velocity,new Vector2(body.Vx,body.Vy))<.02f,"Dry segments retain the additional outer SlopingCollision/Conveyor phase.");
                    }
                }
                finally{for(int x=55;x<71;x++)Main.tile[x,60].CopyFrom(originals[x-55]);}
            }
            Console.WriteLine("PASS PHASE REVIEW "+Environment.GetEnvironmentVariable("JUEMINGR_REVIEW_CASE"));
        }
        private sealed class PlayerQueryBoundary:IPredictionTerrain,IPredictionPlayerTerrain
        {
            private readonly IPredictionTerrain source;internal int PlayerQueries;
            internal PlayerQueryBoundary(IPredictionTerrain source){this.source=source;}
            public void Reset(){source.Reset();}
            public bool Unchanged {get{return source.Unchanged;}}
            public bool MovePlayer(ref NpcMotionState n,PredictionEnvironment e,ref PredictionPlayerMotion p,out PredictionStop stop){PlayerQueries++;stop=PredictionStop.TerrainUnavailable;return false;}
            public bool Move(ref NpcMotionState n,PredictionEnvironment e,out PredictionStop stop){return source.Move(ref n,e,out stop);}
            public bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop){return source.Tile(x,y,out tile,out stop);}
            public bool Solid(MotionRect bounds,out bool solid,out PredictionStop stop){return source.Solid(bounds,out solid,out stop);}
            public bool CanHit(MotionRect a,MotionRect b,out bool clear,out PredictionStop stop){return source.CanHit(a,b,out clear,out stop);}
        }
    }
}
