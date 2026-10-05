using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatTrackingChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");
            foreach(var n in Main.npc)n.active=false;
            for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
            var player=Main.LocalPlayer;player.position=new Vector2(1200,950);player.aggro=0;player.itemAnimation=0;Main.dayTime=false;
            var pet=Main.projectile[0];pet.SetDefaults(625);pet.whoAmI=0;pet.owner=Main.myPlayer;pet.active=true;pet.width=pet.height=40;pet.position=new Vector2(900,950);pet.velocity=Vector2.Zero;player.tankPet=0;
            var target=Main.npc[2];target.SetDefaults(2);target.active=true;target.whoAmI=2;target.target=Main.myPlayer;target.position=new Vector2(1000,950);target.velocity=Vector2.Zero;target.timeLeft=750;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);
            Require(path!=null && path.Count>1,"Tracking default Source→Cache creates a frozen future before action.");
            var ai=typeof(NPC).GetMethod("AI_002_FloatingEye",Flags);ai.Invoke(target,null);
            Console.WriteLine("TRACKING nativeVx="+target.velocity.X+" frozenVx="+path[1].Vx+" nativeTarget="+target.target+" targetRect="+target.targetRect);
            Require(target.velocity.X<0 && path[1].Vx<0,"Player on right / guardian on left must face actual guardian before the original action.");
            Require(Math.Abs(target.velocity.X-path[1].Vx)<.001f,"Frozen first tracking velocity agrees with original private AI.");
            var move=typeof(NPC).GetMethod("UpdateCollision",Flags);move.Invoke(target,null);
            for(int future=2;future<=120;future++)
            {
                ai.Invoke(target,null);move.Invoke(target,null);
                if(future==15 || future==30 || future==60 || future==120)
                {Require(path.Count>future,"Frozen guardian path covers "+future);float error=Vector2.Distance(target.position,new Vector2(path[future].Bounds.X,path[future].Bounds.Y));Console.WriteLine("TRACKING FROZEN future="+future+" error="+error);Require(error<.1f,"Independent original guardian action window: "+future);}
            }
            player.tankPet=-1;pet.active=false;NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var source=Get(host,"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");var read=source.GetType().GetMethod("Read",Flags);var capture=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation").GetMethod("Capture",Flags);
            // AI_005 faces the guardian but GetTargetData() deliberately
            // ignores it for its speed vector. Direction is not a dependency
            // substitute for the numbered player's future position.
            var needs=typeof(NpcMotion).GetMethod("NeedsPlayerMotion",Flags);
            var flying=typeof(NPC).GetMethod("AI_005_EaterOfSouls",Flags);
            Require(flying!=null,"Locked original AI_005 entry.");
            for(int side=-1;side<=1;side+=2)foreach(bool moving in new[]{false,true})
            {
                player.position=new Vector2(1200,950);player.tankPet=0;pet.active=true;pet.position=new Vector2(1000+side*90,950);pet.velocity=Vector2.Zero;
                target.SetDefaults(6);target.whoAmI=2;target.active=true;target.target=Main.myPlayer;target.position=new Vector2(1000,950);target.velocity=Vector2.Zero;
                var sampled=(NpcMotionState)read.Invoke(null,new object[]{target,1L});var sampledArgs=new object[]{target,sampled,terrain};terrain.Reset();capture.Invoke(null,sampledArgs);sampled=(NpcMotionState)sampledArgs[1];
                Require(sampled.TrackingKind==2 && (bool)needs.Invoke(null,new object[]{sampled}),"Guardian flying still needs numbered-player future.");
                var flyingEnv=new PredictionEnvironment{PlayerIndex=Main.myPlayer,PlayerX=player.Center.X,PlayerY=player.Center.Y,PlayerWidth=player.width,PlayerHeight=player.height,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,RockLayer=(float)Main.rockLayer};
                for(int future=1;future<=15;future++)
                {
                    if(moving)player.position.X+=3;flyingEnv.PlayerX=player.Center.X;flyingEnv.PlayerY=player.Center.Y;
                    var group=new[]{sampled};PredictionStop stop;Require(NpcMotion.Step(ref sampled,group,1,flyingEnv,terrain,future,true,out stop),"Flying scalar step: "+stop);
                    flying.Invoke(target,null);
                    // UpdateNPC's shared post-AI phase at locked line 91461
                    // precedes UpdateCollision; the private AI alone omits it.
                    if(Math.Abs(target.velocity.X)<.005f)target.velocity.X=0;
                    move.Invoke(target,null);
                    if(future==1)Console.WriteLine("FLYING side="+side+" moving="+moving+" native="+target.velocity+" model="+new Vector2(sampled.Vx,sampled.Vy)+" nativeDirection="+target.direction+" modelDirection="+sampled.Direction+" expert="+Main.expertMode);
                    Require(sampled.Direction==target.direction && Math.Abs(sampled.Vx-target.velocity.X)<.001f && Math.Abs(sampled.Vy-target.velocity.Y)<.001f,"Guardian facing / numbered-player velocity native separation side="+side+" moving="+moving+" future="+future);
                }
            }
            player.tankPet=-1;pet.active=false;
            var second=Main.player[1];second.active=true;second.dead=false;second.position=new Vector2(1000,800);second.aggro=0;second.tankPet=-1;
            target.SetDefaults(620);target.whoAmI=2;target.active=true;target.position=new Vector2(1000,900);target.target=Main.myPlayer;
            player.position=new Vector2(1300,1100);var state=(NpcMotionState)read.Invoke(null,new object[]{target,1L});var args=new object[]{target,state,terrain};terrain.Reset();capture.Invoke(null,args);state=(NpcMotionState)args[1];
            Require(state.Target==Main.myPlayer && state.PlayerIndex==Main.myPlayer && state.ClosestPlayerIndex==1,"Capture retains numbered target until actual TargetClosest instead of overwriting every member.");
            var e=new PredictionEnvironment{PlayerIndex=state.PlayerIndex,PlayerX=state.PlayerArea.CenterX,PlayerY=state.PlayerArea.CenterY,PlayerWidth=state.PlayerArea.Width,PlayerHeight=state.PlayerArea.Height};
            bool original=(bool)typeof(NPC).GetMethod("Collision_DecideFallThroughPlatforms",Flags).Invoke(target,null),model=(bool)source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcCollisionRules").GetMethod("FallThrough",Flags).Invoke(null,new object[]{state,e});
            Require(original && model,"620 collision reads its current numbered lower player, not nearest upper player or guardian.");
            var other=Main.npc[1];other.SetDefaults(3);other.active=true;other.position=new Vector2(700,800);other.velocity=new Vector2(-1,2);target.SetDefaults(210);target.target=301;var native=target.GetTargetData();state=(NpcMotionState)read.Invoke(null,new object[]{target,1L});args=new object[]{target,state,terrain};capture.Invoke(null,args);state=(NpcMotionState)args[1];
            Require(state.TrackingKind==3 && state.PlayerIndex<0 && state.TrackingArea.X==native.Position.X && state.TrackingArea.Y==native.Position.Y && state.TrackingVx==native.Velocity.X,"Legal encoded NPC supplies its own actual target geometry, never a player substitution.");
            other.active=false;args=new object[]{target,(NpcMotionState)read.Invoke(null,new object[]{target,1L}),terrain};capture.Invoke(null,args);Require(((NpcMotionState)args[1]).TrackingKind==0,"Inactive encoded target is not fabricated from a nearby player.");
            player.position=new Vector2(1400,950);player.wet=false;second.position=new Vector2(900,950);second.wet=true;second.active=true;second.dead=false;
            foreach(int type in new[]{56,58})
            {
                target.SetDefaults(type);target.whoAmI=2;target.active=true;target.target=Main.myPlayer;target.position=new Vector2(1000,950);target.velocity=Vector2.Zero;target.wet=type==58;target.ai[0]=60;target.ai[1]=60;
                var root=Main.tile[60,60];root.active(true);root.type=1;
                state=(NpcMotionState)read.Invoke(null,new object[]{target,1L});args=new object[]{target,state,terrain};terrain.Reset();capture.Invoke(null,args);state=(NpcMotionState)args[1];Require(state.ClosestPlayerIndex==1 && state.PlayerIndex==Main.myPlayer,"Actual numbered-player retarget precondition.");
                e=new PredictionEnvironment{PlayerIndex=Main.myPlayer,PlayerX=player.Center.X,PlayerY=player.Center.Y,PlayerWidth=player.width,PlayerHeight=player.height,PlayerWet=player.wet,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,Multiplayer=true};
                int oldMode=Main.netMode;Main.netMode=1;try{target.AI();}finally{Main.netMode=oldMode;}
                var leaf=typeof(NpcMotion).Assembly.GetType(type==56?"JueMingR.Features.Combat.NpcAnchoredMotion":"JueMingR.Features.Combat.NpcAquaticMotion").GetMethod("Step",Flags);
                var parameters=type==56?new object[]{state,e,terrain,PredictionStop.None}:new object[]{state,e,terrain,state.Direction,state.DirectionY,PredictionStop.None};
                Require((bool)leaf.Invoke(null,parameters),"Native AI13/16 numbered-player consumer available.");var modeled=(NpcMotionState)parameters[0];
                Console.WriteLine("RETARGET type="+type+" nativeTarget="+target.target+" modelTarget="+modeled.Target+" nativeV="+target.velocity+" modelV="+new Vector2(modeled.Vx,modeled.Vy));
                Require(target.target==1 && modeled.Target==1 && Vector2.Distance(target.velocity,new Vector2(modeled.Vx,modeled.Vy))<.001f,"AI13 root vector / AI16 wet pursuit consumes new numbered player.");root.active(false);
            }
            second.active=false;target.SetDefaults(488);target.whoAmI=2;target.active=true;target.position=new Vector2(700,650);target.target=Main.myPlayer;player.position=new Vector2(640,640);
            var missing=Main.tile[40,40];Main.tile[40,40]=null;
            try{NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,dummy:true));NativeCombatObservationChecks.Fresh(context,host);var stationary=cache.Read(0);Require(stationary!=null && stationary.Count==121 && (stationary.Assumptions&PredictionAssumption.NoPlayerMotionNeeded)!=0,"Stationary selected target does not acquire irrelevant missing player geometry through actual Source→Cache.");}
            finally{Main.tile[40,40]=missing;NativeCombatObservationChecks.Save(host,new ObservationOptions());}
            target.SetDefaults(32);target.whoAmI=2;target.active=true;target.position=new Vector2(700,650);target.target=Main.myPlayer;target.ai[0]=1;target.velocity=Vector2.Zero;
            Main.tileSolid[1]=true;for(int tx=43;tx<=45;tx++){Main.tile[tx,44].active(true);Main.tile[tx,44].type=1;}
            missing=Main.tile[40,40];Main.tile[40,40]=null;
            try{NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));NativeCombatObservationChecks.Fresh(context,host);var teleport=cache.Read(0);Console.WriteLine("STYLE8 actual="+(teleport==null?"null":teleport.Identity.Type+" count="+teleport.Count+" stop="+teleport.Stop+" assumptions="+teleport.Assumptions));Require(teleport!=null && teleport.Identity.Type==32 && teleport.Count==121 && (teleport.Assumptions&PredictionAssumption.NoPlayerMotionNeeded)!=0,"Style8 own destination/clock model ignores unrelated missing player terrain through real Source→Cache.");}
            finally{Main.tile[40,40]=missing;for(int tx=43;tx<=45;tx++)Main.tile[tx,44].active(false);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
            Console.WriteLine("PASS TRACKING frozen guardian windows, numbered-player collision, legal encoded NPC identity, stationary independent player premise.");
        }
    }
}
