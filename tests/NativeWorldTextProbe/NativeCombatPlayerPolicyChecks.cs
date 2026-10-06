using System;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatPlayerPolicyChecks
    {
        internal static void Run(object context)
        {
            const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance;
            var source=Get(Get(context,"CombatObservation"),"Prediction");var read=source.GetType().GetMethod("ReadPlayer",flags);var terrain=(IPredictionPlayerTerrain)Get(source,"Terrain");
            var cell=Main.tile[60,60];var saved=new Tile();saved.CopyFrom(cell);Main.tileSolid[1]=true;Main.tileSolid[19]=Main.tileSolidTop[19]=true;float bottom=Main.bottomWorld;Main.bottomWorld=Main.maxTilesY*16;int cases=0;
            try
            {
                var runner=new Player{whoAmI=1,active=true,maxRunSpeed=3,accRunSpeed=6,runAcceleration=.08f,runSlowdown=.2f};
                var sampledRunner=(PredictionPlayerMotion)read.Invoke(null,new object[]{runner});
                Require(sampledRunner.MaxSpeed==3,"Source retains the ordinary run threshold separately from fast-running cap.");
                foreach(bool down in new[]{false,true})foreach(float vy in new[]{.3f,8f})foreach(bool brick in new[]{false,true})
                {cell.active(true);cell.type=(ushort)(brick?1:19);cell.frameY=0;cell.liquid=0;Compare(new Player{whoAmI=1,active=true,width=20,height=32,position=new Vector2(961,923),velocity=new Vector2(0,vy),gravDir=1,controlDown=down},"platform down="+down+" vy="+vy+" brick="+brick,read,terrain,ref cases);}
                NativeCombatLiveContextChecks.InitializeMount();
                foreach(int mount in new[]{-1,6,7,8,12,23,44,48,49,55})foreach(int slide in mount==55?new[]{0,1}:new[]{0})
                {
                    var p=new Player{whoAmI=1,active=true};if(mount>=0){bool server=Main.dedServ;int mode=Main.netMode;try{Main.dedServ=true;Main.netMode=2;p.mount.SetMount(mount,p);}finally{Main.dedServ=server;Main.netMode=mode;}}p.width=20;p.height=32;p.position=new Vector2(961,923);p.velocity=new Vector2(0,8);p.gravDir=1;p.slideDir=slide;
                    cell.active(true);cell.type=19;cell.frameY=0;cell.liquid=0;Compare(p,"mount="+mount+" slide="+slide,read,terrain,ref cases);
                }
                foreach(int liquid in new[]{0,1})foreach(int boots in new[]{0,1,2})foreach(int scenario in new[]{0,1,2})
                {
                    cell.active(false);cell.liquid=255;cell.liquidType(liquid);
                    var p=new Player{whoAmI=1,active=true,width=20,height=32,position=new Vector2(961,scenario==2?950:928),velocity=new Vector2(0,scenario==1?-8:8),gravDir=1,waterWalk=boots==2,waterWalk2=boots==1};
                    Compare(p,"surface liquid="+liquid+" boots="+boots+" scenario="+scenario,read,terrain,ref cases);
                }
                foreach(int liquid in new[]{0,2})foreach(int equipment in new[]{0,1,2,3})foreach(bool blocked in new[]{false,true})
                {
                    var wall=Main.tile[62,60];var wallSaved=new Tile();wallSaved.CopyFrom(wall);
                    try
                    {
                        cell.active(false);cell.liquid=255;cell.liquidType(liquid);wall.active(blocked);wall.type=1;
                        var p=new Player{whoAmI=1,active=true,width=20,height=32,position=new Vector2(961,950),velocity=new Vector2(12,8),gravDir=1,merman=equipment==1,ignoreWater=equipment==2,trident=equipment==3};
                        Compare(p,"wet liquid="+liquid+" equipment="+equipment+" blocked="+blocked,read,terrain,ref cases);
                    }
                    finally{wall.CopyFrom(wallSaved);}
                }
                foreach(int special in new[]{0,1,2,3})
                {
                    var p=new Player{whoAmI=1,active=true,width=20,height=32,position=new Vector2(961,923),gravDir=1};p.shimmering=special==0;p.tongued=special==1;p.pulley=special==2;if(special==3)p.grappling[0]=0;
                    var motion=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});var body=new NpcMotionState{X=motion.X,Y=motion.Y,Width=20,Height=32};PredictionStop stop;
                    Require(motion.UnsupportedGeometry && !terrain.MovePlayer(ref body,default(PredictionEnvironment),ref motion,out stop) && stop==PredictionStop.UnsupportedMechanism,"Necessary bypass/grapple geometry is an explicit boundary, never ordinary tile motion.");cases++;
                }
                foreach(bool down in new[]{false,true})
                {
                    cell.active(false);cell.liquid=255;cell.liquidType(0);
                    var p=new Player{whoAmI=1,active=true,width=20,height=32,position=new Vector2(961,950),gravDir=1,canFloatInWater=true,controlDown=down};
                    var motion=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});var body=new NpcMotionState{X=p.position.X,Y=p.position.Y,Width=20,Height=32};PredictionStop stop;((IPredictionTerrain)terrain).Reset();bool moved=terrain.MovePlayer(ref body,default(PredictionEnvironment),ref motion,out stop);
                    Require(p.ShouldFloatInWater==motion.FloatInWater && (down?moved:!moved && stop==PredictionStop.UnsupportedMechanism),"Original floating qualification respects held Down; unknown floating geometry is not ordinary wet motion.");cases++;
                }
                // NPC health consumers remain active on their own route.
                cell.active(false);cell.liquid=255;cell.liquidType(0);var npc=new NpcMotionState{X=961,Y=950,Width=20,Height=32,Life=100,Health=new NpcHealthState{RealLife=-1,Fire=60},WaterSpeed=.5f};PredictionStop npcStop;
                ((IPredictionTerrain)terrain).Reset();Require(((IPredictionTerrain)terrain).Move(ref npc,default(PredictionEnvironment),out npcStop) && npc.Health.Fire==0,"NPC wet extinguish remains its own health consumer.");cases++;
            }
            finally{cell.CopyFrom(saved);Main.bottomWorld=bottom;}
            Console.WriteLine("PASS PLAYER POLICY independent native phase cases="+cases);
        }
        private static void Compare(Player p,string label,MethodInfo read,IPredictionPlayerTerrain terrain,ref int cases)
        {
            p.gravity=.4f;p.jump=9;p.carpetFrame=-1;p.gfxOffY=p.stepSpeed=0;
            p.wet=Collision.WetCollision(p.position,p.width,p.height);p.honeyWet=Collision.honey;p.shimmerWet=Collision.shimmer;
            var motion=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});var health=new NpcHealthState{RealLife=0,Fire=60,ShimmerTicks=5};var body=new NpcMotionState{X=p.position.X,Y=p.position.Y,Vx=p.velocity.X,Vy=p.velocity.Y,Width=p.width,Height=p.height,Life=100,Health=health};
            bool ignore=p.gravDir<0 || p.mount.Active && (p.mount.Cart || p.mount.Type==12 || p.mount.Type==7 || p.mount.Type==8 || p.mount.Type==23 || p.mount.Type==44 || p.mount.Type==48 || p.mount.Type==55 && p.slideDir!=0) || p.GoingDownWithGrapple || p.pulley;
            bool fall=p.controlDown || ignore;bool wet=p.wet && (p.shimmerWet || p.honeyWet && !p.ignoreWater || !p.merman && !p.ignoreWater && !p.trident);
            if(p.mount.Type!=48)p.SlopeDownMovement();
            bool stepMount=p.mount.Active && (p.mount.Type==7 || p.mount.Type==8 || p.mount.Type==12 || p.mount.Type==44 || p.mount.Type==49);
            if(p.velocity.Y==p.gravity && !p.IsRidingTracks && p.mount.Type!=48 && !stepMount)Collision.StepDown(ref p.position,ref p.velocity,p.width,p.height,ref p.stepSpeed,ref p.gfxOffY,(int)p.gravDir,p.waterWalk||p.waterWalk2);
            if(p.gravDir<0?(p.carpetFrame!=-1 || p.velocity.Y<=p.gravity)&&!p.controlUp:(p.carpetFrame!=-1 || p.velocity.Y>=p.gravity)&&!p.controlDown&&!p.IsRidingTracks&&!stepMount&&p.grappling[0]<0)Collision.StepUp(ref p.position,ref p.velocity,p.width,p.height,ref p.stepSpeed,ref p.gfxOffY,(int)p.gravDir,p.controlUp);
            if(wet)p.WetCollision(fall,ignore,p.shimmerWet?.375f:p.honeyWet?.25f:.5f);else p.DryCollision(fall,ignore);
            p.SlopingCollision(fall,ignore);if(Math.Abs(p.gfxOffY)<=2)Collision.StepConveyorBelt(p,(int)p.gravDir);
            if(p.gravDir>0?Collision.up:Collision.down){p.velocity.Y=.01f*p.gravDir;if(!p.merman)p.jump=0;}
            ((IPredictionTerrain)terrain).Reset();PredictionStop stop;Require(terrain.MovePlayer(ref body,default(PredictionEnvironment),ref motion,out stop),"Player scalar policy available: "+label+" "+stop);
            Console.WriteLine("PLAYER POLICY "+label+" native="+p.position+" model="+new Vector2(body.X,body.Y)+" nativeV="+p.velocity+" modelV="+new Vector2(body.Vx,body.Vy));
            Require(ignore==motion.IgnorePlatforms && Vector2.Distance(p.position,new Vector2(body.X,body.Y))<.02f && Vector2.Distance(p.velocity,new Vector2(body.Vx,body.Vy))<.02f && p.jump==motion.Jump,"Independent player-owned collision phases: "+label);cases++;
            Require(body.Health.Equals(health) && body.Life==100,"Player geometry does not consume NPC health sentinels, effects or retirement.");
        }
    }
}
