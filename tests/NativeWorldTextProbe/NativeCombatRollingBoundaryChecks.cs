using System;
using System.IO;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeCombatAttackMechanismChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatRollingBoundaryChecks
    {
        internal static void Run(object context,object host,NpcPredictionCache cache,Action step,string output)
        {
            var source=Get(host,"Prediction");
            Require(Get(source,"Native")==null,"Default production composition has no native owner.");
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1600,2400,NPCID.Derpling,Start:16,Target:Main.myPlayer);
            var n=Main.npc[slot];step();
            // The first runtime update enters the isolated world and clears old input; sample after that real lifecycle boundary.
            NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);Call(context,"UpdateRuntime");Call(context,"UpdateShell");
            var selected=(NpcIdentity)Get(Get(host,"Selection"),"Target");
            Console.WriteLine("SHORT-SELECTION has="+Get(Get(host,"Selection"),"HasTarget")+" mouse="+Get(Get(host,"Selection"),"RealMouse")+" hasMouse="+Get(Get(host,"Selection"),"HasMouse")+" npc="+n.position+" size="+n.Size+" active="+n.active+" life="+n.life+" expected="+slot+" actual="+selected.Slot+" options="+((ObservationOptions)Get(host,"Options")).Radius);
            Require((bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(selected.Token,n) && selected.Slot==slot,"Short-prefix fixture enters through the actual unique target consumer.");
            // Slots 0/1 belong to the real display/attack owners. The strict
            // fixture reader uses its own slot so normal Host updates cannot
            // legitimately release or replace its demand.
            cache.Demand(0,1,120);cache.Demand(2,120);
            var scratchDust=Main.dust;var scratchRandom=Main.rand;
            bool scratchUp=Collision.up,scratchDown=Collision.down,scratchHoney=Collision.honey,scratchShimmer=Collision.shimmer;
            try
            {
                Main.dust=new Dust[scratchDust.Length];for(int i=0;i<Main.dust.Length;i++)Main.dust[i]=new Dust();
                Main.rand=new Terraria.Utilities.UnifiedRandom(917);
                TerrainBounds(source,n);
            }
            finally{Main.dust=scratchDust;Main.rand=scratchRandom;Collision.up=scratchUp;Collision.down=scratchDown;Collision.honey=scratchHoney;Collision.shimmer=scratchShimmer;}
            Require(ReferenceEquals(Main.dust,scratchDust) && ReferenceEquals(Main.rand,scratchRandom) && Collision.up==scratchUp && Collision.down==scratchDown && Collision.honey==scratchHoney && Collision.shimmer==scratchShimmer,"Native oracle returns owned dust/random and collision scratch");
            var identity=new NpcIdentity((long)Get(host,"Session"),n,n.whoAmI,n.generation,n.type,n.netID);
            foreach(int effect in new[]{120,151,169,183,186,189,337,344,362,30,375,395,397})
            {
                n.AddBuff(effect,300);Require(Array.IndexOf(n.buffType,effect)>=0,"Original AddBuff reaches the attachment/non-motion effect "+effect);
                Call(source,"Prepare",identity,(long)Main.GameUpdateCount);
                Require(cache.Read(0)!=null,"Existing non-motion/attached effect must not withdraw the whole rolling path: "+effect);
                Require(effect==120 || (cache.Read(0).Assumptions&PredictionAssumption.UnmodeledDamageEffects)!=0,"Unmodeled damage effect remains explicit: "+effect);
                for(int i=0;i<n.buffType.Length;i++)if(n.buffType[i]==effect)n.DelBuff(i);
            }
            ShortPrefix(context,host,source,cache,n,identity,step,output);
            Call(source,"Prepare",identity,(long)Main.GameUpdateCount);var before=cache.Read(0);
            Require(before!=null && before.Strategy==PredictionStrategy.RollingConditional,"Boundary fixture consumes default output.");
            Vector2 position=n.position;n.SetDefaults(n.type);n.position=position;n.target=Main.myPlayer;
            Require(cache.Read(0)==null,"Same-type SetDefaults immediately retires the published result.");
            identity=new NpcIdentity((long)Get(host,"Session"),n,n.whoAmI,n.generation,n.type,n.netID);
            Call(source,"Prepare",identity,(long)Main.GameUpdateCount+1);var reset=cache.Read(0);
            Require(reset!=null && reset.RelationVersion>before.RelationVersion,"A reconstruction epoch survives identical type/slot.");
            n.life=0;Call(source,"Prepare",identity,(long)Main.GameUpdateCount+2);Require(cache.Read(0)==null,"Actual death clears rather than relabels a living forecast.");
            cache.Release(0);Require(cache.Required==120,"Path OFF only releases its own consumer.");cache.Release(2);Call(source,"Clear");NPC.ClearAll();Projectile.ClearAll();
            NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true,path:false));
            step();clears=0;
            var hooks=new HarmonyLib.Harmony("JueMingR.Tests.RollingOffBoundary");
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            try
            {
                foreach(var pair in new[]{new object[]{Get(source,"Terrain"),"Reset"},new object[]{Get(source,"rolling"),"Clear"}})
                    hooks.Patch(pair[0].GetType().GetMethod((string)pair[1],flags),prefix:new HarmonyLib.HarmonyMethod(typeof(NativeCombatRollingBoundaryChecks),nameof(Clearing)));
                for(int i=0;i<30;i++)step();
                Require(clears==0 && cache.Required==0,"Collision-only updates do no repeated prediction buffer/terrain/history cleanup.");
            }
            finally{hooks.UnpatchAll(hooks.Id);}
            File.WriteAllText(Path.Combine(output,"rolling-boundaries.txt"),"PASS default owner, original AddBuff effects, same-type reset epoch, death, independent consumers");
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static int clears;
        private static void ShortPrefix(object context,object host,object source,NpcPredictionCache cache,NPC n,NpcIdentity identity,Action step,string output)
        {
            int life=n.life,regen=n.lifeRegenCount,lifeRegen=n.lifeRegen;bool active=n.active,fire=n.onFire,fire3=n.onFire3;Vector2 position=n.position,velocity=n.velocity,camera=Main.screenPosition;
            var rows=new System.Collections.Generic.List<string>{"condition,liveLife,active,rawCount,stop,displayMinimum,displayShown,strict120Shown"};
            try
            {
                n.life=1;n.lifeRegenCount=0;n.AddBuff(BuffID.OnFire,300);
                Require(Array.IndexOf(n.buffType,BuffID.OnFire)>=0,"Original OnFire must reach the terminal prefix fixture.");
                Call(source,"Prepare",identity,(long)Main.GameUpdateCount);
                var path=(NpcTrajectory)Get(cache,"result");
                Require(n.active && path!=null && path.Count>1 && path.Count<=30 && path.Stop==PredictionStop.Despawn,"A still-living target can have a known short terminal future.");
                rows.Add("known-DOT-death,"+n.life+","+n.active+","+path.Count+","+path.Stop+","+cache.MinimumRequired+","+(cache.Read(0)!=null)+","+(cache.Read(2)!=null));
                Require(cache.Read(0)==path && cache.Read(2)==null,"Display consumes the true short endpoint; strict120 still refuses it.");
                Require(!ReferenceEquals(Get(host,"DisplayPath"),path),"Source publication alone cannot grant/relabel a current Host display lease");
                // The real owner grants display only after native frame facts
                // and its normal runtime/shell consumers, never Cache alone.
                for(int attempt=0;attempt<4;attempt++)
                {
                    OwnerFrame(context,n,step,true);
                    path=(NpcTrajectory)Get(host,"DisplayPath");
                    if(path!=null && path.Count>1 && path.Count<=30 && path.Stop==PredictionStop.Despawn)break;
                }
                Require(n.active && path!=null && path.Count>1 && path.Count<=30 && path.Stop==PredictionStop.Despawn,"Actual owner grants the naturally terminal short future before native death");
                Require(cache.Read(2)==null,"Strict120 remains independent from the actual short display lease");
                Main.screenPosition=n.Center-new Vector2(Main.screenWidth/2,Main.screenHeight/2);Call(Get(host,"World"),"Prepare");NativeCombatPresentationChecks.Project(Get(host,"World"));
                Console.WriteLine("SHORT-PRESENT owner-entry=true tick="+Main.GameUpdateCount+" sample="+path.SampleTick+" lease="+Get(host,"displayLeaseTick")+" age="+Get(host,"DisplayAge")+" optional="+Get(host,"optionalTick")+" short="+Get(host,"displayShortTick")+" failed="+Get(Get(host,"World"),"Failed")+" canDraw="+Get(host,"CanDraw")+" identity="+identity.Equals((NpcIdentity)Get(Get(host,"Selection"),"Target"))+" strokes="+Get(Get(host,"World"),"StrokeCount")+" future="+(path.Count-1));
                Require((int)Get(Get(host,"World"),"StrokeCount")>0 && ReferenceEquals(Get(Get(host,"World"),"preparedPath"),path),"WorldLayer actually projects the true short endpoint from the published path.");
                n.life=life;n.lifeRegenCount=regen;for(int i=0;i<n.buffType.Length;i++)if(n.buffType[i]==BuffID.OnFire)n.DelBuff(i);
                int tx=(int)n.Center.X/16,ty=(int)n.Center.Y/16;var cells=new Tile[7,7];
                try
                {
                    for(int x=0;x<7;x++)for(int y=0;y<7;y++){cells[x,y]=Main.tile[tx+x-3,ty+y-3];Main.tile[tx+x-3,ty+y-3]=null;}
                    // No original NPC motor is run across absent world data.
                    // This arm exercises actual Host observation/acquisition.
                    for(int attempt=0;attempt<4;attempt++)
                    {
                        OwnerFrame(context,n,step,false);path=(NpcTrajectory)Get(cache,"result");
                        if(path!=null && path.Count==1 && path.Stop==PredictionStop.TerrainUnavailable)break;
                    }
                    Console.WriteLine("SHORT-UNKNOWN tick="+Main.GameUpdateCount+" sample="+(path==null?-1:path.SampleTick)+" count="+(path==null?0:path.Count)+" stop="+(path==null?"null":path.Stop.ToString())+" display="+(Get(host,"DisplayPath")!=null)+" target="+Get(Get(host,"Selection"),"HasTarget"));
                    Require(path!=null && path.Count==1 && path.Stop==PredictionStop.TerrainUnavailable,"Actual missing in-world acquisition has no computed future");
                    rows.Add("unknown-in-world-cell,"+n.life+","+n.active+","+path.Count+","+path.Stop+","+cache.MinimumRequired+","+(cache.Read(0)!=null)+","+(cache.Read(2)!=null));
                    Require(cache.Read(0)==null && Get(host,"DisplayPath")==null && Get(Get(host,"World"),"preparedPath")==null,"Actual owner retires unknown one-point output rather than relabeling its old lease");
                }
                finally{for(int x=0;x<7;x++)for(int y=0;y<7;y++)Main.tile[tx+x-3,ty+y-3]=cells[x,y];}
                n.life=0;OwnerFrame(context,n,step,false);
                Require(cache.Read(0)==null && Get(host,"DisplayPath")==null && Get(Get(host,"World"),"preparedPath")==null,"Actual observed death retires output through the Host owner");
            }
            finally
            {
                n.life=life;n.lifeRegenCount=regen;n.lifeRegen=lifeRegen;n.active=active;n.onFire=fire;n.onFire3=fire3;n.position=position;n.velocity=velocity;Main.screenPosition=camera;
                for(int i=0;i<n.buffType.Length;i++)if(n.buffType[i]==BuffID.OnFire)n.DelBuff(i);
                File.WriteAllLines(Path.Combine(output,"rolling-short-prefix.csv"),rows);
            }
        }
        private static void OwnerFrame(object context,NPC n,Action step,bool native)
        {
            if(native)step();else NativeQuickItemChecks.BeginWorldStep();
            NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);Call(context,"UpdateRuntime");Call(context,"UpdateShell");
        }
        private static void Clearing(){clears++;}
        private static void TerrainBounds(object source,NPC n)
        {
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
            var seed=(NpcMotionState)source.GetType().GetMethod("Read",flags).Invoke(null,new object[]{n,1L});
            var terrain=(IPredictionTerrain)Activator.CreateInstance(Get(source,"Terrain").GetType(),true);
            var env=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY};
            var cases=new[]{new Vector2(-1,128),new Vector2(128,-1),new Vector2(Main.maxTilesX*16-10,128),new Vector2(128,Main.maxTilesY*16-10)};
            foreach(bool free in new[]{false,true})foreach(var position in cases)
            {
                BoundsOracle(source,n,terrain,env,position,Vector2.Zero,free,"stationary-edge");
            }
            foreach(bool free in new[]{false,true})
            {
                BoundsOracle(source,n,terrain,env,new Vector2(20,128),new Vector2(-40,0),free,"outgoing-edge");
            }
            // f50646b deliberately gave NPC motors the original clipped scans.
            // This is not a player/projectile lease relaxation. Keep their
            // actual strict paths, invalid bodies and unknown cells negative.
            var playerTerrain=(IPredictionPlayerTerrain)terrain;
            foreach(var position in cases)
            {
                var body=seed;body.X=position.X;body.Y=position.Y;body.Vx=body.Vy=0;body.NoTileCollide=false;body.Width=20;body.Height=42;
                var player=new PredictionPlayerMotion{GravityDirection=1,StepSpeed=1};terrain.Reset();PredictionStop stop;
                Require(!playerTerrain.MovePlayer(ref body,env,ref player,out stop) && stop==PredictionStop.TerrainUnavailable,"Player body still rejects an unknown edge before movement: "+position);
            }
            {
                var body=seed;body.X=20;body.Y=128;body.Vx=-40;body.Vy=0;body.NoTileCollide=false;body.Width=20;body.Height=42;
                var player=new PredictionPlayerMotion{GravityDirection=1,StepSpeed=1};terrain.Reset();PredictionStop stop;
                Require(!playerTerrain.MovePlayer(ref body,env,ref player,out stop) && stop==PredictionStop.TerrainUnavailable,"Player outgoing edge remains protected");
            }
            var projectileWet=terrain.GetType().GetMethod("ProjectileWet",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            object[] wetArgs={8f,128f,10,10,false,(byte)0,false};
            Require(!(bool)projectileWet.Invoke(terrain,wetArgs),"Projectile wet acquisition retains its separate strict edge lease");
            foreach(bool free in new[]{false,true})for(int invalid=0;invalid<7;invalid++)
            {
                var state=seed;state.X=state.Y=1280;state.Vx=state.Vy=0;state.NoTileCollide=free;
                if(invalid==0)state.X=float.NaN;else if(invalid==1)state.Y=float.PositiveInfinity;else if(invalid==2)state.Width=0;else if(invalid==3)state.Height=0;else if(invalid==4)state.Width=1025;else if(invalid==5)state.Vx=513;else state.Vy=float.NegativeInfinity;
                terrain.Reset();PredictionStop stop;Require(!terrain.Move(ref state,env,out stop) && stop==PredictionStop.InvalidState,"NPC clipped scans do not accept invalid/oversized/unbounded state free="+free+" kind="+invalid);
            }
            var missing=Main.tile[82,82];
            try
            {
                Main.tile[82,82]=null;
                foreach(bool free in new[]{false,true})
                {
                    var state=seed;state.X=state.Y=1280;state.Vx=state.Vy=0;state.NoTileCollide=free;terrain.Reset();PredictionStop stop;
                    Require(!terrain.Move(ref state,env,out stop) && stop==PredictionStop.TerrainUnavailable,"An actually queried missing in-world cell is not clipped air free="+free);
                }
            }
            finally{Main.tile[82,82]=missing;terrain.Reset();}
            BoundsContactCases(source,n,terrain,env);
            terrain.Reset();PredictionTile tile;PredictionStop reason;int x=80,y=80;var original=Main.tile[x,y];bool active=original.active();ushort type=original.type;
            try
            {
                Require(terrain.Tile(x,y,out tile,out reason),"Known real tile copied.");original.active(true);original.type=1;terrain.Reset();
                Require(terrain.Tile(x,y,out tile,out reason) && tile.Active && tile.Solid,"New sample invalidates hot geometry cells after a real tile edit.");
                terrain.Reset();int read=0;for(int j=10;j<90;j++)for(int i=10;i<90;i++)
                {
                    if(terrain.Tile(i,j,out tile,out reason)){read++;continue;}
                    Require(reason==PredictionStop.TerrainLimit && read==4096,"Hot cache cannot bypass the 4096 distinct-cell limit.");return;
                }
                throw new InvalidOperationException("Geometry limit fixture did not reach its real bound.");
            }
            finally{original.active(active);original.type=type;terrain.Reset();}
        }
        private static void BoundsOracle(object source,NPC prototype,IPredictionTerrain terrain,PredictionEnvironment env,Vector2 position,Vector2 velocity,bool free,string label)
        {
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
            var native=(NPC)prototype.Clone();native.ai=(float[])prototype.ai.Clone();native.localAI=(float[])prototype.localAI.Clone();
            native.position=position;native.velocity=velocity;native.noTileCollide=free;
            var state=(NpcMotionState)source.GetType().GetMethod("Read",flags).Invoke(null,new object[]{native,1L});
            Collision.up=Collision.down=false;terrain.Reset();PredictionStop stop;
            Require(terrain.Move(ref state,env,out stop),"NPC original clipped motor remains available "+label+" free="+free+" pos="+position+" stop="+stop);
            if(free)
            {
                // Exact noTileCollide UpdateNPC stage: no collision/liquid drag.
                native.oldPosition=native.position;native.oldDirection=native.direction;native.position+=native.velocity;
                Collision.WetCollision(native.position,native.width,native.height);
            }
            else typeof(NPC).GetMethod("UpdateCollision",flags).Invoke(native,null);
            Require(Vector2.Distance(native.position,new Vector2(state.X,state.Y))<.015f && Vector2.Distance(native.velocity,new Vector2(state.Vx,state.Vy))<.015f &&
                native.collideX==state.CollideX && native.collideY==state.CollideY && native.wet==state.Wet && native.honeyWet==state.Honey && native.lavaWet==state.Lava && native.shimmerWet==state.Shimmer && native.wetCount==state.WetCount,
                "NPC original complete collision/wet motor agrees "+label+" free="+free+" pos="+position+" native="+native.position+" model="+new Vector2(state.X,state.Y));
            if(label=="solid" && !free)Require(native.collideY,"Original edge solid contact is actually exercised");
            if((label=="water" || label=="honey") && !free)Require(native.wet,"Original clipped wet/drag consumer is actually exercised "+label);
            Console.WriteLine("ROLLING BOUNDS ORACLE label="+label+" type="+native.type+" box="+native.width+"x"+native.height+" free="+free+" input="+position+"/"+velocity+" output="+native.position+"/"+native.velocity+" wet="+native.wet+" collide="+native.collideX+"/"+native.collideY);
        }
        private static void BoundsContactCases(object source,NPC prototype,IPredictionTerrain terrain,PredictionEnvironment env)
        {
            var saved=new Tile[9,11];for(int x=0;x<9;x++)for(int y=0;y<11;y++){saved[x,y]=new Tile();saved[x,y].CopyFrom(Main.tile[x,y+35]);}
            try
            {
                foreach(int kind in new[]{-1,0,2})
                {
                    for(int x=0;x<9;x++)for(int y=35;y<46;y++){Main.tile[x,y].ClearEverything();if(kind>=0){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(kind);}}
                    if(kind<0){Main.tile[2,40].active(true);Main.tile[2,40].type=1;}
                    foreach(bool free in new[]{false,true})BoundsOracle(source,prototype,terrain,env,new Vector2(-8,kind<0?580:590),new Vector2(12,kind<0?20:4),free,kind<0?"solid":kind==0?"water":"honey");
                }
            }
            finally{for(int x=0;x<9;x++)for(int y=0;y<11;y++)Main.tile[x,y+35].CopyFrom(saved[x,y]);terrain.Reset();}
        }
        private static void Call(object value,string name,params object[] args)
        {value.GetType().GetMethod(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).Invoke(value,args);}
    }
}


