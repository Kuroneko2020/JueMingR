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
            if(Environment.GetEnvironmentVariable("JUEMINGR_REVIEW_CASE")=="clock-end")
            {
                for(int i=0;i<Main.maxPlayers;i++){if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};Main.player[i].active=false;}
                var p=Main.LocalPlayer;p.position=new Vector2(1400,950);p.active=true;p.dead=false;var second=Main.player[1];second.active=true;second.dead=false;second.position=new Vector2(900,950);second.tankPet=-1;
                int mode=Main.netMode;Main.netMode=1;
                try
                {
                    foreach(int type in new[]{36,128,131})foreach(int delta in new[]{-1,0,1})
                    {
                        int period=type==36?300:type==128?1100:800;var owner=Main.npc[1]=new NPC();owner.SetDefaults(type==36?35:127);owner.whoAmI=1;owner.active=true;owner.position=new Vector2(800,800);owner.ai[1]=0;owner.velocity=Vector2.Zero;
                        var n=Main.npc[2]=new NPC();n.SetDefaults(type);n.whoAmI=2;n.active=true;n.target=Main.myPlayer;n.ai[0]=n.ai[1]=1;n.ai[2]=0;n.ai[3]=period-120+delta;n.position=new Vector2(700,1030);
                        var read=source.GetType().GetMethod("Read",Flags);var args=new object[]{n,read.Invoke(null,new object[]{n,Get(host,"Session")}),Get(host,"Session")};source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPositionObservation").GetMethod("Capture",Flags).Invoke(null,args);var state=(NpcMotionState)args[1];
                        var captured=new object[]{n,state,terrain};((IPredictionTerrain)terrain).Reset();source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation").GetMethod("Capture",Flags).Invoke(null,captured);state=(NpcMotionState)captured[1];
                        var probe=new PlayerQueryBoundary((IPredictionTerrain)terrain);var env=new PredictionEnvironment{PlayerIndex=Main.myPlayer,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,Multiplayer=true};var player=new PredictionPlayerMotion{X=p.position.X,Y=p.position.Y,Width=p.width,Height=p.height,GravityDirection=1,MaxFall=10};
                        var path=new RollingNpcPrediction().Prepare(new[]{(NpcMotionState)read.Invoke(null,new object[]{owner,Get(host,"Session")}),state},2,1,1,120,1,env,player,probe,new[]{true,true});
                        for(int future=1;future<=120;future++){n.AI();n.position+=n.velocity;}
                        Console.WriteLine("PARENT END type="+type+" delta="+delta+" nativePhase120="+n.ai[2]+" nativeClock="+n.ai[3]+" queries="+probe.PlayerQueries+" count="+(path==null?0:path.Count)+" stop="+(path==null?"null":path.Stop.ToString()));
                        bool needs=type!=36 && delta>0;
                        Require(path!=null && (needs?probe.PlayerQueries==1 && path.Count==1:probe.PlayerQueries==0 && path.Count==121),"A transition adds a premise only when its new action really pursues a player: "+type+" delta="+delta);
                        if(!needs)Require(n.ai[2]==(delta>=0?1:0) && Vector2.Distance(n.position,new Vector2(path[120].Bounds.X,path[120].Bounds.Y))<.025f,"Independent original home/raising actions match endpoint without an unrelated premise.");
                    }
                }
                finally{Main.netMode=mode;second.active=false;}
            }
            else if(Environment.GetEnvironmentVariable("JUEMINGR_REVIEW_CASE")=="clock")
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
                    Require(path!=null && probe.PlayerQueries==0 && path.Count==121,"A clock transition alone does not consume AI12 player geometry; its rising phase has not reached Aim height.");
                    int difficulty=Main.GameMode,mode=Main.netMode;
                    try{Main.GameMode=expert?1:0;Main.netMode=1;n.position=new Vector2(700,1030);n.velocity=Vector2.Zero;n.ai[2]=0;n.ai[3]=170;for(int future=1;future<=120;future++){n.AI();n.position+=n.velocity;}Require(n.ai[2]==(expert?1:0) && Vector2.Distance(n.position,new Vector2(path[120].Bounds.X,path[120].Bounds.Y))<.025f,"Original actual difficulty confirms clock-only home/raising window, not Aim.");}
                    finally{Main.GameMode=difficulty;Main.netMode=mode;}
                }
            }
            else if(Environment.GetEnvironmentVariable("JUEMINGR_REVIEW_CASE")=="owner-order")
            {
                for(int i=0;i<Main.maxPlayers;i++){if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};Main.player[i].active=false;}
                var p=Main.LocalPlayer;p.active=true;p.dead=false;p.tankPet=-1;p.position=new Vector2(900,950);
                int mode=Main.netMode,difficulty=Main.GameMode;Main.netMode=1;Main.GameMode=0;
                try
                {
                    foreach(int phase in new[]{1,4})foreach(bool entering in new[]{true,false})foreach(bool ownerFirst in new[]{true,false})foreach(int required in new[]{1,2})
                    {
                        if(entering && Environment.GetEnvironmentVariable("JUEMINGR_OWNER_BOUNDARY_ONLY")=="leaving")continue;
                        int ownerSlot=ownerFirst?1:2,childSlot=ownerFirst?2:1;
                        var owner=Main.npc[ownerSlot]=new NPC();owner.SetDefaults(35);owner.whoAmI=ownerSlot;owner.active=true;owner.position=new Vector2(800,800);owner.ai[1]=owner.ai[3]=0;owner.velocity=phase==1?new Vector2(0,entering?2:-2):new Vector2(entering?-2:2,0);
                        var n=Main.npc[childSlot]=new NPC();n.SetDefaults(36);n.whoAmI=childSlot;n.active=true;n.target=Main.myPlayer;n.ai[0]=1;n.ai[1]=ownerSlot;n.ai[2]=phase;n.ai[3]=0;n.position=phase==1?new Vector2(700,entering?601:599):new Vector2(owner.Center.X+(entering?499:501)-n.width*.5f,1030);n.velocity=Vector2.Zero;
                        var read=source.GetType().GetMethod("Read",Flags);var args=new object[]{n,read.Invoke(null,new object[]{n,Get(host,"Session")}),Get(host,"Session")};source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPositionObservation").GetMethod("Capture",Flags).Invoke(null,args);var state=(NpcMotionState)args[1];var parent=(NpcMotionState)read.Invoke(null,new object[]{owner,Get(host,"Session")});
                        var probe=new PlayerQueryBoundary((IPredictionTerrain)terrain);var env=new PredictionEnvironment{PlayerIndex=Main.myPlayer,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,Multiplayer=true};var player=new PredictionPlayerMotion{X=p.position.X,Y=p.position.Y,Width=p.width,Height=p.height,GravityDirection=1,MaxFall=10};
                        var path=new RollingNpcPrediction().Prepare(ownerFirst?new[]{parent,state}:new[]{state,parent},2,ownerFirst?1:0,1,required,1,env,player,probe,new[]{true,true});
                        // The owner has qualified observed constant motion. Apply
                        // that movement in real slot order around the original
                        // child's AI; this isolates propagation, not full head AI.
                        for(int future=1;future<=required;future++){if(ownerFirst)owner.position+=owner.velocity;n.AI();n.position+=n.velocity;if(!ownerFirst)owner.position+=owner.velocity;}
                        bool needs=entering?(ownerFirst || required==2):!ownerFirst;
                        Console.WriteLine("PARENT ORDER phase="+phase+" entering="+entering+" ownerFirst="+ownerFirst+" required="+required+" nativePhase="+n.ai[2]+" queries="+probe.PlayerQueries+" count="+(path==null?0:path.Count)+" assumptions="+(path==null?"null":path.Assumptions.ToString()));
                        Require(n.ai[2]==(needs?(phase==1?2:5):phase),"Original child's Aim reads the owner position at its actual slot update.");
                        Require(path!=null && (needs?probe.PlayerQueries==1 && path.Count==1 && !path.Assumptions.HasFlag(PredictionAssumption.NoPlayerMotionNeeded):probe.PlayerQueries==0 && path.Count==required+1),"Entering and departing owner boundaries are decided before actual consumption, including the last requested action.");
                        if(!needs)Require(Vector2.Distance(n.position,new Vector2(path[required].Bounds.X,path[required].Bounds.Y))<.025f,"Non-consuming actions match the original slot-order relative movement.");
                    }
                }
                finally{Main.netMode=mode;Main.GameMode=difficulty;}
            }
            else if(Environment.GetEnvironmentVariable("JUEMINGR_REVIEW_CASE")=="aim-boundary")
            {
                for(int i=0;i<Main.maxPlayers;i++){if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};Main.player[i].active=false;}
                var p=Main.LocalPlayer;p.active=true;p.dead=false;p.tankPet=-1;p.position=new Vector2(900,950);
                int mode=Main.netMode,difficulty=Main.GameMode;Main.netMode=1;Main.GameMode=0;
                try
                {
                    foreach(int type in new[]{36,129,130})foreach(int phase in new[]{1,4})foreach(int control in new[]{0,1,2})
                    {
                        int required=control==1?2:1;
                        var owner=Main.npc[1]=new NPC();owner.SetDefaults(type==36?35:127);owner.whoAmI=1;owner.active=true;owner.position=new Vector2(800,800);owner.ai[1]=owner.ai[3]=0;
                        var n=Main.npc[2]=new NPC();n.SetDefaults(type);n.whoAmI=2;n.active=true;n.target=Main.myPlayer;n.ai[0]=-1;n.ai[1]=1;n.ai[2]=phase;n.ai[3]=0;
                        float threshold=owner.position.Y-(type==130?280:200);
                        n.position=phase==1?new Vector2(700,threshold+(control==2?-1:1)):new Vector2(owner.Center.X+(control==2?501:499)-n.width*.5f,1030);
                        n.velocity=phase==1?new Vector2(0,-2):new Vector2(2,0);
                        var read=source.GetType().GetMethod("Read",Flags);var args=new object[]{n,read.Invoke(null,new object[]{n,Get(host,"Session")}),Get(host,"Session")};source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPositionObservation").GetMethod("Capture",Flags).Invoke(null,args);var state=(NpcMotionState)args[1];
                        var probe=new PlayerQueryBoundary((IPredictionTerrain)terrain);var env=new PredictionEnvironment{PlayerIndex=Main.myPlayer,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,Multiplayer=true};var player=new PredictionPlayerMotion{X=p.position.X,Y=p.position.Y,Width=p.width,Height=p.height,GravityDirection=1,MaxFall=10};
                        var path=new RollingNpcPrediction().Prepare(new[]{(NpcMotionState)read.Invoke(null,new object[]{owner,Get(host,"Session")}),state},2,1,1,required,1,env,player,probe,new[]{true,true});
                        for(int future=1;future<=required;future++){n.AI();n.position+=n.velocity;}
                        bool chase=type==129 && phase==4,needs=control!=0 || chase;
                        Console.WriteLine("PARENT AIM type="+type+" phase="+phase+" control="+control+" required="+required+" nativePhase="+n.ai[2]+" queries="+probe.PlayerQueries+" count="+(path==null?0:path.Count));
                        Require(n.ai[2]==(needs&&!chase?(phase==1?2:5):phase),"Original Aim occurs only after its action-before relative boundary; AI33 phase4 directly pursues.");
                        Require(path!=null && (needs?probe.PlayerQueries==1 && path.Count==1:probe.PlayerQueries==0 && path.Count==2),"Last non-Aim action publishes without an unrelated premise; first real Aim requires a consistent timeline.");
                        if(!needs)Require(Vector2.Distance(n.position,new Vector2(path[1].Bounds.X,path[1].Bounds.Y))<.025f,"Original rising/horizontal pre-Aim action matches Rolling.");
                    }
                }
                finally{Main.netMode=mode;Main.GameMode=difficulty;}
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
