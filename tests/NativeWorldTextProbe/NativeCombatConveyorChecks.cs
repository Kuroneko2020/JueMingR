using System;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatConveyorChecks
    {
        internal static void Run(object context)
        {
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            var source=Get(Get(context,"CombatObservation"),"Prediction");var read=source.GetType().GetMethod("Read",flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");var original=typeof(NPC).GetMethod("UpdateCollision",flags);
            Main.tileSolid[421]=Main.tileSolid[422]=true;var tile=Main.tile[60,60];tile.active(true);tile.type=421;tile.slope(0);tile.halfBrick(false);
            var n=new NPC();n.SetDefaults(1);n.position=new Vector2(961,928);n.width=20;n.height=32;n.velocity=Vector2.Zero;n.noGravity=true;n.noTileCollide=false;n.target=Main.myPlayer;
            var state=(NpcMotionState)read.Invoke(null,new object[]{n,1L});terrain.Reset();PredictionStop stop;Require(terrain.Move(ref state,default(PredictionEnvironment),out stop),"Conveyor shared terrain available: "+stop);original.Invoke(n,null);
            Console.WriteLine("CONVEYOR native="+n.position+" candidate="+new Vector2(state.X,state.Y));
            Require(Vector2.Distance(n.position,new Vector2(state.X,state.Y))<.015f && n.velocity==Vector2.Zero && state.Vx==0 && state.Vy==0,"Native body conveyor position contribution must survive zero velocity without changing velocity.");
            tile.active(false);int cases=1;
            var eligibility=typeof(NPC).GetMethod("ConveyorBeltCollision",flags);
            int[] applicable={624,85,629,195,1,147,184,537,204,16,59,71,535,225,676,303,335,336,333,334,667,141,81,121,183,138,244,304,105,123,685,686,687,106,354,376,579,589,37,695,696};
            foreach(int type in applicable)
            {
                n.SetDefaults(type);n.townNPC=false;n.lifeMax=100;n.damage=10;
                Require((bool)eligibility.Invoke(n,null) && NPCID.Sets.ConveyorBeltCollision[type],"Full original 41-member conveyor type set: "+type);cases++;
            }
            foreach(int type in new[]{3,488})foreach(bool town in new[]{false,true})foreach(int life in new[]{4,5,6})foreach(int damage in new[]{0,1})
            {
                n.SetDefaults(type);n.townNPC=town;n.lifeMax=life;n.damage=damage;n.friendly=true;
                var sampled=(NpcMotionState)read.Invoke(null,new object[]{n,1L});bool native=(bool)eligibility.Invoke(n,null);
                Require(native==(sampled.Town || sampled.LifeMax==5 && sampled.NoContactDamage),"Town/lifeMax==5/exact damage predicate independent of harmless/friendly: "+type);cases++;
            }
            for(int belt=421;belt<=422;belt++)for(int shape=0;shape<=5;shape++)foreach(int contact in new[]{0,1,2})
            {
                tile.active(true);tile.type=(ushort)belt;tile.slope((byte)(shape==5?0:shape));tile.halfBrick(shape==5);
                n.SetDefaults(1);n.position=new Vector2(961,contact==1?976:contact==2?918:shape==5?936:928);n.width=20;n.height=32;n.velocity=Vector2.Zero;n.oldVelocity=Vector2.Zero;n.noGravity=true;n.target=Main.myPlayer;
                var sampled=(NpcMotionState)read.Invoke(null,new object[]{n,1L});terrain.Reset();Require(terrain.Move(ref sampled,default(PredictionEnvironment),out stop),"Belt shape snapshot "+stop);original.Invoke(n,null);
                float error=Vector2.Distance(n.position,new Vector2(sampled.X,sampled.Y));
                Console.WriteLine("BELT shape="+shape+" contact="+contact+" type="+belt+" native="+n.position+" model="+new Vector2(sampled.X,sampled.Y));
                Require(error<.015f && Math.Abs(n.velocity.X-sampled.Vx)<.015f && Math.Abs(n.velocity.Y-sampled.Vy)<.015f,"Original left/right, top/bottom/off-surface, half and four slopes: "+shape+"/"+contact);cases++;
            }
            tile.slope(0);tile.halfBrick(false);tile.active(true);tile.type=421;
            var adjacent=Main.tile[61,60];adjacent.active(true);adjacent.type=422;adjacent.slope(0);adjacent.halfBrick(false);
            n.SetDefaults(1);n.position=new Vector2(961,928);n.width=30;n.height=32;n.velocity=Vector2.Zero;n.noGravity=true;n.target=Main.myPlayer;
            Check("opposite belt faces cancel");Require(n.position.X==961,"Native opposite faces contribute zero net carry.");adjacent.active(false);
            var wall=Main.tile[62,59];wall.active(true);wall.type=1;Main.tileSolid[1]=true;
            n.SetDefaults(1);n.position=new Vector2(971,928);n.width=20;n.height=32;n.velocity=Vector2.Zero;n.noGravity=true;n.target=Main.myPlayer;
            Check("body carry clipped by wall");Require(n.position.X<973.5f,"Belt carry cannot cross adjacent body wall.");wall.active(false);
            foreach(int type in new[]{72,247,248,542,543,544,545,359})
            {
                n.SetDefaults(type);n.position=new Vector2(961,928);n.width=20;n.height=32;n.velocity=Vector2.Zero;n.noGravity=true;n.target=Main.myPlayer;
                Check("native separate collision branch "+type);Require(n.position.X==961,"Skipped slope/conveyor route receives no carry: "+type);
            }
            tile.active(false);var low=Main.tile[60,64];low.active(true);low.type=421;low.slope(0);low.halfBrick(false);
            n.SetDefaults(686);n.position=new Vector2(961,928);n.width=20;n.height=32;n.velocity=Vector2.Zero;n.noGravity=true;n.target=Main.myPlayer;
            Check("686 extended movement contact while body misses belt");Require(n.position.X==961,"Movement rectangle cannot substitute body conveyor contact.");low.active(false);
            Console.WriteLine("PASS CONVEYOR independent original eligibility/body contact cases="+cases);
            void Check(string label)
            {
                var sampled=(NpcMotionState)read.Invoke(null,new object[]{n,1L});terrain.Reset();Require(terrain.Move(ref sampled,default(PredictionEnvironment),out stop),"Additional belt geometry available: "+label+" / "+stop);original.Invoke(n,null);
                Require(Vector2.Distance(n.position,new Vector2(sampled.X,sampled.Y))<.015f && Math.Abs(n.velocity.X-sampled.Vx)<.015f && Math.Abs(n.velocity.Y-sampled.Vy)<.015f,"Independent original belt consumer: "+label);Console.WriteLine("BELT CONTROL "+label+" native="+n.position);cases++;
            }
        }
        internal static void Player(object context)
        {
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            var source=Get(Get(context,"CombatObservation"),"Prediction");var terrain=(IPredictionPlayerTerrain)Get(source,"Terrain");
            Main.tileSolid[421]=true;Main.tileSolid[1]=true;
            var ceiling=Main.tile[60,57];ceiling.active(true);ceiling.type=1;ceiling.slope(0);ceiling.halfBrick(false);
            var belt=Main.tile[60,60];belt.active(true);belt.type=421;belt.slope(0);belt.halfBrick(false);
            var player=Main.LocalPlayer;player.position=new Vector2(961,930);player.velocity=new Vector2(0,-8);player.width=20;player.height=32;player.gravDir=1;player.jump=9;player.merman=true;player.wet=false;player.waterWalk=player.waterWalk2=false;
            var body=new NpcMotionState{X=player.position.X,Y=player.position.Y,Vx=player.velocity.X,Vy=player.velocity.Y,Width=20,Height=32};
            var state=new PredictionPlayerMotion{X=body.X,Y=body.Y,Vx=body.Vx,Vy=body.Vy,Width=20,Height=32,GravityDirection=1,Jump=9,Merman=true};
            typeof(Terraria.Player).GetMethod("DryCollision",flags).Invoke(player,new object[]{false,false});bool initialHead=Collision.up;int initialJump=player.jump;
            typeof(Terraria.Player).GetMethod("SlopingCollision",flags).Invoke(player,new object[]{false,false});Collision.StepConveyorBelt(player,1);bool finalHead=Collision.up;
            if(finalHead){player.velocity.Y=.01f;if(!player.merman)player.jump=0;}
            ((IPredictionTerrain)terrain).Reset();PredictionStop stop;Require(terrain.MovePlayer(ref body,default(PredictionEnvironment),ref state,out stop),"Player belt/head contact snapshot: "+stop);
            Console.WriteLine("PLAYER BELT initialHead="+initialHead+" finalHead="+finalHead+" initialJump="+initialJump+" nativeJump="+player.jump+" modelJump="+state.Jump+" native="+player.position+" model="+new Vector2(body.X,body.Y));
            Require(initialHead && initialJump==0 && !finalHead,"Native early DryCollision head response remains after later belt queries clear final head flag.");
            Require(state.Jump==player.jump && Vector2.Distance(player.position,new Vector2(body.X,body.Y))<.015f && Math.Abs(player.velocity.Y-body.Vy)<.015f,"Early native jump ownership and final velocity flag are separate.");
            ceiling.active(false);belt.active(false);Console.WriteLine("PASS PLAYER BELT phased dry Jump and final velocity collision ownership.");
        }
    }
}
