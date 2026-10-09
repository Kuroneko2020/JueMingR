using System;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatPlayerGeometryChecks
    {
        internal static void Run(object context)
        {
            var terrain=(IPredictionPlayerTerrain)Get(Get(Get(context,"CombatObservation"),"Prediction"),"Terrain");Main.tileSolid[1]=true;
            var tile=Main.tile[60,59];var saved=new Tile();saved.CopyFrom(tile);
            try
            {
                foreach(int gravity in new[]{1,-1})foreach(float speed in new[]{4f,24f,40f,60f})
                {
                    tile.active(true);tile.type=1;tile.slope(0);tile.halfBrick(gravity>0);
                    var p=Main.LocalPlayer;p.position=new Vector2(speed>=40?925:941,gravity>0?928:952);p.width=20;p.height=32;p.velocity=new Vector2(speed,.4f*gravity);p.gravity=.4f;p.gravDir=gravity;p.controlUp=p.controlDown=false;p.jump=9;p.merman=false;p.wet=false;p.waterWalk=p.waterWalk2=false;p.gfxOffY=p.stepSpeed=0;
                    var motion=new PredictionPlayerMotion{X=p.position.X,Y=p.position.Y,Vx=p.velocity.X,Vy=p.velocity.Y,Width=20,Height=32,Gravity=.4f,GravityDirection=gravity,Jump=9,IgnorePlatforms=gravity<0};var body=new NpcMotionState{X=motion.X,Y=motion.Y,Vx=motion.Vx,Vy=motion.Vy,Width=20,Height=32};
                    var onceEnd=p.position+p.TileCollision(p.position,p.velocity,false,gravity<0);
                    p.SlopeDownMovement();if(p.velocity.Y==p.gravity)Collision.StepDown(ref p.position,ref p.velocity,p.width,p.height,ref p.stepSpeed,ref p.gfxOffY,gravity,false);
                    Collision.StepUp(ref p.position,ref p.velocity,p.width,p.height,ref p.stepSpeed,ref p.gfxOffY,gravity,false);
                    p.DryCollision(false,gravity<0);p.SlopingCollision(false,gravity<0);Collision.StepConveyorBelt(p,gravity);
                    if(gravity>0?Collision.up:Collision.down){p.velocity.Y=.01f*gravity;p.jump=0;}
                    ((IPredictionTerrain)terrain).Reset();PredictionStop stop;Require(terrain.MovePlayer(ref body,default(PredictionEnvironment),ref motion,out stop),"Player geometry snapshot available: "+stop);
                    Console.WriteLine("PLAYER GEOMETRY grav="+gravity+" speed="+speed+" native="+p.position+" model="+new Vector2(body.X,body.Y)+" nativeV="+p.velocity+" modelV="+new Vector2(body.Vx,body.Vy));
                    Require(Vector2.Distance(p.position,new Vector2(body.X,body.Y))<.015f && Vector2.Distance(p.velocity,new Vector2(body.Vx,body.Vy))<.015f && p.jump==motion.Jump,"Native player StepUp/Down and dry segmented geometry.");
                    if(speed==60){Console.WriteLine("PLAYER SEGMENT DISCRIMINATOR grav="+gravity+" singleSweep="+onceEnd+" segmented="+p.position);Require(Vector2.Distance(onceEnd,p.position)>.1f,"A real intermediate contact distinguishes one full sweep from native dry segmentation.");}
                }
            }
            finally{tile.CopyFrom(saved);}
            var floor=Main.tile[60,60];var floorSaved=new Tile();floorSaved.CopyFrom(floor);var lower=Main.tile[60,61];var lowerSaved=new Tile();lowerSaved.CopyFrom(lower);float oldBottom=Main.bottomWorld;Main.bottomWorld=Main.maxTilesY*16;
            try
            {
                foreach(int drop in new[]{0,8,16})
                {
                    floor.active(drop!=16);floor.type=1;floor.slope(0);floor.halfBrick(drop==8);lower.active(drop==16);lower.type=1;lower.slope(0);lower.halfBrick(false);
                    var position=new Vector2(941,928);var velocity=new Vector2(4,.4f);float step=0,gfx=0;
                    var body=new NpcMotionState{X=position.X,Y=position.Y,Vx=velocity.X,Vy=velocity.Y,Width=20,Height=32};var motion=new PredictionPlayerMotion{GravityDirection=1};
                    Collision.StepDown(ref position,ref velocity,20,32,ref step,ref gfx,1,false);
                    var args=new object[]{body,motion,PredictionStop.None};((IPredictionTerrain)terrain).Reset();Require((bool)terrain.GetType().GetMethod("PlayerStepDown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(terrain,args),"Shared StepDown snapshot.");body=(NpcMotionState)args[0];motion=(PredictionPlayerMotion)args[1];
                    Console.WriteLine("PLAYER STEP DOWN drop="+drop+" nativeY="+position.Y+" modelY="+body.Y+" step="+step+" gfx="+gfx);
                    Require(Math.Abs(position.Y-body.Y)<.001f && step==motion.StepSpeed && gfx==motion.GfxOffset && position.Y==928+drop,"Original 8/16 step-down and ordinary solid control.");
                }
            }
            finally{floor.CopyFrom(floorSaved);lower.CopyFrom(lowerSaved);Main.bottomWorld=oldBottom;}
            Console.WriteLine("PASS PLAYER GEOMETRY native stair and dry segmentation.");
        }
    }
}
