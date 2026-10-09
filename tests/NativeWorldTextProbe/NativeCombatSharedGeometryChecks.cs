using System;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // The original private predicate and collision pipeline are the oracle;
    // these inputs test shared geometry, not full AI coverage for every type.
    internal static class NativeCombatSharedGeometryChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context)
        {
            var source=Get(Get(context,"CombatObservation"),"Prediction");
            var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var read=source.GetType().GetMethod("Read",Flags);
            var decide=typeof(NPC).GetMethod("Collision_DecideFallThroughPlatforms",Flags);
            var move=typeof(NPC).GetMethod("UpdateCollision",Flags);
            var gravity=typeof(NPC).GetField("gravity",Flags);var savedGravity=gravity.GetValue(null);gravity.SetValue(null,.3f);
            Main.tileSolid[TileID.Stone]=true;Main.tileSolid[TileID.Platforms]=true;Main.tileSolidTop[TileID.Platforms]=true;Main.dayTime=true;Main.eclipse=false;Main.invasionType=0;
            var player=Main.LocalPlayer;player.position=new Vector2(960,1000);player.active=true;player.dead=false;
            var env=new PredictionEnvironment{PlayerIndex=Main.myPlayer,PlayerX=player.Center.X,PlayerY=player.Center.Y,PlayerWidth=player.width,PlayerHeight=player.height,Day=true};
            int cases=0;var tile=Main.tile[60,60];var saved=new Tile();saved.CopyFrom(tile);
            try
            {
                if(Environment.GetEnvironmentVariable("JUEMINGR_SHARED_GEOMETRY_PHASE")=="head")
                {
                    tile.active(true);tile.type=TileID.Stone;tile.slope(0);tile.halfBrick(false);
                    var mover=(IPredictionPlayerTerrain)terrain;
                    foreach(int direction in new[]{1,-1})foreach(bool merman in new[]{false,true})foreach(bool honey in new[]{false,true})
                    {
                        player.position=new Vector2(961,direction>0?980:924);player.width=20;player.height=32;player.gravDir=direction;
                        // Honey is the native wet branch even for a merman;
                        // ordinary water/air takes DryCollision for a merman.
                        for(int tx=59;tx<=62;tx++)for(int ty=57;ty<=63;ty++)if(ty!=60){Main.tile[tx,ty].liquid=(byte)(honey?255:0);Main.tile[tx,ty].liquidType(honey?2:0);}
                        var p=new PredictionPlayerMotion{X=player.position.X,Y=player.position.Y,Vy=-8*direction,Width=20,Height=32,GravityDirection=direction,Jump=9,Merman=merman};
                        player.velocity=new Vector2(0,p.Vy);player.jump=9;player.merman=merman;player.wet=honey;player.honeyWet=honey;player.waterWalk=player.waterWalk2=false;
                        if(honey)player.WetCollision(false,false,.25f);else player.DryCollision(false,false);
                        bool hit=direction>0?Collision.up:Collision.down;
                        Require(hit,"Independent original player collision reports head contact.");
                        int earlyJump=player.jump;
                        player.SlopingCollision(false,false);Collision.StepConveyorBelt(player,direction);
                        bool finalHead=direction>0?Collision.up:Collision.down;
                        if(finalHead){player.velocity.Y=.01f*direction;if(!merman)player.jump=0;}
                        var body=new NpcMotionState{X=p.X,Y=p.Y,Vy=p.Vy,Width=20,Height=32,Life=1,LifeMax=1};terrain.Reset();PredictionStop stop;
                        Require(mover.MovePlayer(ref body,env,ref p,out stop),"Player head response available: "+stop);
                        Console.WriteLine("HEAD PHASE grav="+direction+" merman="+merman+" honey="+honey+" earlyJump="+earlyJump+" finalHead="+finalHead+" nativeVy="+player.velocity.Y+" modelVy="+body.Vy);
                        Require(Math.Abs(body.Y-player.position.Y)<.015f,"Full original collision pipeline position.");
                        Require(Math.Abs(body.Vy-player.velocity.Y)<.015f,"Final slope/belt flag owns next-tick velocity independently of first contact.");
                        Require(p.Jump==player.jump,"Native phased dry/merman jump control: gravity="+direction+" honey="+honey+" jump="+p.Jump);cases++;
                    }
                    for(int tx=59;tx<=62;tx++)for(int ty=57;ty<=63;ty++)Main.tile[tx,ty].liquid=0;
                    Console.WriteLine("PASS SHARED-PLAYER-HEAD independent cases="+cases);return;
                }
                if(Environment.GetEnvironmentVariable("JUEMINGR_SHARED_GEOMETRY_PHASE")=="predicates")
                {
                    var rules=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcCollisionRules");var candidate=rules.GetMethod("FallThrough",Flags);
                    void Compare(NPC n,string label)
                    {
                        var model=(NpcMotionState)read.Invoke(null,new object[]{n,1L});env.Day=Main.dayTime;env.Eclipse=Main.eclipse;env.InvasionType=Main.invasionType;env.PlayerY=player.Center.Y;
                        bool original=(bool)decide.Invoke(n,null),actual=(bool)candidate.Invoke(null,new object[]{model,env});
                        Require(original==actual,"Independent original predicate: "+label+" native="+original+" candidate="+actual);cases++;
                    }
                    var branch=new NPC();branch.SetDefaults(469);branch.target=Main.myPlayer;branch.position=new Vector2(960,923);branch.directionY=-1;
                    foreach(float phase in new[]{0f,1f}){branch.ai[2]=phase;Compare(branch,"469 dir=-1 ai2="+phase);}
                    branch.SetDefaults(620);branch.target=Main.myPlayer;branch.position=new Vector2(960,923);
                    foreach(int direction in new[]{-1,1})foreach(float y in new[]{800f,1100f}){branch.directionY=direction;player.position.Y=y;Compare(branch,"620 direction="+direction+" playerY="+y);}
                    branch.SetDefaults(17);branch.aiStyle=7;branch.position=new Vector2(960,923);branch.height=32;branch.townNPC=true;
                    foreach(int mode in new[]{0,1,2,3})foreach(int home in new[]{58,59,60,75,76,77})
                    {Main.dayTime=mode!=1;Main.invasionType=mode==2?1:0;Main.eclipse=mode==3;branch.homeTileY=home;Compare(branch,"town mode="+mode+" homeY="+home);}
                    var shape=rules.GetMethod("MovementBounds",Flags);var parameters=typeof(NPC).GetMethod("GetTileCollisionParameters",Flags);
                    foreach(int type in new[]{3,594,686,243,290,351,482,343,348,349,391,415,576,577})foreach(float ai in new[]{0f,.5f,1f})foreach(bool attached in new[]{false,true})
                    {
                        branch.SetDefaults(type);branch.whoAmI=199;branch.position=new Vector2(961.25f,923.5f);branch.ai[1]=ai;
                        var part=Main.npc[198];part.SetDefaults(type==391?390:type==415?416:3);part.active=attached;part.ai[0]=199;
                        var model=(NpcMotionState)read.Invoke(null,new object[]{branch,1L});var args=new object[]{null,0,0};parameters.Invoke(branch,args);var position=(Vector2)args[0];var actual=(MotionRect)shape.Invoke(null,new object[]{model});
                        Require(actual.X==position.X && actual.Y==position.Y && actual.Width==(int)args[1] && actual.Height==(int)args[2],"Native full movement box: "+type+" ai1="+ai+" part="+attached);cases++;
                        // A private future form uses its new shape while the
                        // original selected identity remains immutable.
                        model.Identity=new NpcIdentity(1L,branch,199,0,3,3);model.MotionType=type;actual=(MotionRect)shape.Invoke(null,new object[]{model});
                        Require(actual.X==position.X && actual.Y==position.Y && actual.Width==(int)args[1] && actual.Height==(int)args[2],"Future form geometry uses MotionType.");
                        part.active=false;
                    }
                    Console.WriteLine("PASS SHARED-PREDICATE-BOUNDS independent cases="+cases);return;
                }
                if(Environment.GetEnvironmentVariable("JUEMINGR_SHARED_GEOMETRY_PHASE")=="player")
                {
                    tile.active(true);tile.type=TileID.Platforms;tile.frameY=0;
                    var estimator=new JueMingR.Features.Combat.RollingNpcPrediction();var advance=estimator.GetType().GetMethod("AdvancePlayer",Flags);
                    player.position=new Vector2(961,927.9f);player.width=20;player.height=32;player.gravDir=1;
                    var motion=new PredictionPlayerMotion{X=player.position.X,Y=player.position.Y,Vy=.3f,Width=20,Height=32,GravityDirection=1,Down=true,MaxFall=10,MaxSpeed=1,JumpSpeed=5};
                    var expected=player.TileCollision(player.position,new Vector2(0,.3f),true,false);var args=new object[]{motion,env,terrain,PredictionStop.None};terrain.Reset();
                    Require((bool)advance.Invoke(estimator,args),"Player premise geometry available.");motion=(PredictionPlayerMotion)args[0];
                    Console.WriteLine("SHARED-PLAYER down=true originalY="+(player.position.Y+expected.Y)+" modelY="+motion.Y);
                    Require(Math.Abs(player.position.Y+expected.Y-motion.Y)<.015f,"Player Down must use player fallThrough semantics without waterwalk.");return;
                }
                if(Environment.GetEnvironmentVariable("JUEMINGR_SHARED_GEOMETRY_PHASE")=="bounds")
                {
                    var wall=Main.tile[62,56];wall.active(true);wall.type=TileID.Stone;
                    var n=new NPC();n.SetDefaults(243);n.active=true;n.whoAmI=199;n.noTileCollide=false;n.position=new Vector2(950,900);n.velocity=new Vector2(6,0);n.width=40;n.height=120;
                    var model=(NpcMotionState)read.Invoke(null,new object[]{n,1L});model.Gravity=.3f;terrain.Reset();PredictionStop stop;
                    Require(terrain.Move(ref model,env,out stop),"Collision bounds test available.");move.Invoke(n,null);
                    Console.WriteLine("SHARED-BOUNDS type=243 nativeX="+n.position.X+" modelX="+model.X);
                    wall.active(false);Require(Math.Abs(n.position.X-model.X)<.015f,"Body is not native movement collision region.");return;
                }
                // Every direct style/type branch, both conditional directions,
                // the last override, and an ordinary negative control.
                int[] types={238,163,165,237,240,531,2,190,191,192,193,194,317,318,133,467,477,173,469,210,211,50,657,247,248,245,542,543,544,545,418,405,406,490,301,620,3};
                foreach(int type in types)for(int condition=0;condition<2;condition++)for(int solid=0;solid<2;solid++)
                {
                    var n=new NPC();n.SetDefaults(type);n.active=true;n.whoAmI=199;n.noTileCollide=false;n.position=new Vector2(961,923);n.velocity=new Vector2(0,8);n.width=20;n.height=32;n.target=Main.myPlayer;n.ai[2]=condition;n.directionY=condition==0?-1:1;
                    player.position.Y=condition==0?880:1000;env.PlayerY=player.Center.Y;
                    tile.active(true);tile.type=(ushort)(solid==0?TileID.Platforms:TileID.Stone);tile.frameX=tile.frameY=0;tile.slope(0);tile.halfBrick(false);
                    var model=(NpcMotionState)read.Invoke(null,new object[]{n,1L});model.Gravity=.3f;terrain.Reset();PredictionStop stop;
                    bool fall=(bool)decide.Invoke(n,null);
                    Require(terrain.Move(ref model,env,out stop),"Shared NPC geometry remains available: "+type+" / "+stop);
                    move.Invoke(n,null);
                    float error=Vector2.Distance(n.position,new Vector2(model.X,model.Y));
                    Console.WriteLine("SHARED-PLATFORM type="+type+" style="+n.aiStyle+" condition="+condition+" solid="+solid+" originalFall="+fall+" originalY="+n.position.Y+" modelY="+model.Y);
                    Require(error<.015f && Math.Abs(n.velocity.Y-model.Vy)<.015f,"Original platform/brick contact differs: type="+type+" condition="+condition+" solid="+solid+" fall="+fall+" error="+error);
                    cases++;
                }
                foreach(int style in new[]{10,5,40,44,22,49,14,3,26,107,87,7})for(int condition=0;condition<2;condition++)
                {
                    var n=new NPC();n.SetDefaults(3);n.aiStyle=style;n.active=true;n.whoAmI=199;n.position=new Vector2(961,923);n.velocity=new Vector2(0,8);n.width=20;n.height=32;n.target=Main.myPlayer;n.directionY=condition==0?-1:1;n.townNPC=style==7;n.homeTileY=condition==0?55:90;
                    player.position.Y=condition==0?880:1000;env.PlayerY=player.Center.Y;
                    tile.type=TileID.Platforms;var model=(NpcMotionState)read.Invoke(null,new object[]{n,1L});model.Gravity=.3f;terrain.Reset();PredictionStop stop;
                    bool fall=(bool)decide.Invoke(n,null);Require(terrain.Move(ref model,env,out stop),"Style strategy is available.");move.Invoke(n,null);
                    Require(Math.Abs(n.position.Y-model.Y)<.015f,"Complete original style predicate differs: style="+style+" condition="+condition+" fall="+fall+" nativeY="+n.position.Y+" modelY="+model.Y);cases++;
                }
                Console.WriteLine("PASS SHARED-PLATFORM cases="+cases);
            }
            finally{tile.CopyFrom(saved);gravity.SetValue(null,savedGravity);}
        }
    }
}
