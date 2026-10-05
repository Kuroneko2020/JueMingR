using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatRetargetPhaseChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var read=source.GetType().GetMethod("Read",Flags);var capture=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation").GetMethod("Capture",Flags);
            foreach(var npc in Main.npc)npc.active=false;
            for(int i=0;i<Main.maxPlayers;i++){if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};Main.player[i].active=false;}
            var a=Main.LocalPlayer;var b=Main.player[1];a.active=b.active=true;a.dead=b.dead=false;a.tankPet=b.tankPet=-1;a.aggro=b.aggro=0;
            a.position=new Vector2(1400,950);b.position=new Vector2(900,950);Main.dayTime=false;int oldMode=Main.netMode;Main.netMode=1;
            int cases=0;
            try
            {
                foreach(int type in new[]{61,1,177,153,237})foreach(int phase in new[]{0,1,2})
                {
                    var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.target=Main.myPlayer;n.position=new Vector2(1000,950);n.velocity=new Vector2(0,.2f);n.direction=1;n.directionY=1;n.timeLeft=750;
                    if(type==61){n.ai[0]=phase==0?0:1;n.wet=phase==2;}
                    if(type==1){n.ai[2]=phase==0?0:1;n.wet=phase==1;if(phase==2){n.velocity=Vector2.Zero;n.ai[0]=0;}}
                    if(type==177){n.ai[2]=phase==0?0:1;if(phase==1){n.velocity=Vector2.Zero;n.ai[0]=0;}if(phase==2)n.ai[2]=2;}
                    if(type==153){n.ai[0]=phase==0?3:phase==1?5:1;n.ai[1]=phase==1?29:0;if(phase==2)n.direction=0;}
                    if(type==237){n.target=phase==0?255:Main.myPlayer;if(phase==1)a.dead=true;}
                    var state=(NpcMotionState)read.Invoke(null,new object[]{n,Get(host,"Session")});var captured=new object[]{n,state,terrain};terrain.Reset();capture.Invoke(null,captured);state=(NpcMotionState)captured[1];
                    Require(state.HasClosestPlayer && state.ClosestPlayerIndex==1,"Native closest B precondition.");
                    var env=new PredictionEnvironment{PlayerIndex=Main.myPlayer,PlayerX=a.Center.X,PlayerY=a.Center.Y,PlayerWidth=a.width,PlayerHeight=a.height,PlayerDead=a.dead,PlayerWet=a.wet,Multiplayer=true,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface};
                    n.AI();string leaf=type==61||type==1?"JueMingR.Features.Combat.NpcMotion":type==237?"JueMingR.Features.Combat.NpcWallMotion":"JueMingR.Features.Combat.NpcRollingMotion";
                    string method=type==61?"Vulture":type==1?"Slime":type==177?"Derpling":type==153?"Tortoise":"Step";
                    object[] args=type==61?new object[]{state,env,state.Direction,state.DirectionY,false}:type==1?new object[]{state,env,terrain,state.Direction,state.DirectionY,false,PredictionStop.None}:type==177?new object[]{state,env,state.Direction}:type==153?new object[]{state,env,terrain,state.Direction,state.DirectionY,PredictionStop.None}:new object[]{state,env,terrain,false,PredictionStop.None};
                    var result=typeof(NpcMotion).Assembly.GetType(leaf).GetMethod(method,Flags).Invoke(null,args);if(result is bool)Require((bool)result,"Actual finite retarget phase geometry.");var model=(NpcMotionState)args[0];
                    Console.WriteLine("RETARGET PHASE type="+type+" phase="+phase+" nativeTarget="+n.target+" modelTarget="+model.Target+" nativeDirection="+n.direction+" modelDirection="+model.Direction+" nativeV="+n.velocity+" modelV="+new Vector2(model.Vx,model.Vy));
                    Require(n.target==model.Target && n.direction==model.Direction && Vector2.Distance(n.velocity,new Vector2(model.Vx,model.Vy))<.001f,"Native phase retarget timing / old-target control: "+type+"/"+phase);a.dead=false;cases++;
                }
                foreach(int side in new[]{-1,1})
                {
                    var pet=Main.projectile[0];pet.SetDefaults(625);pet.whoAmI=0;pet.owner=1;pet.active=true;pet.width=pet.height=40;pet.position=new Vector2(1000+side*90,950);b.tankPet=0;b.wet=false;
                    var fish=Main.npc[2];fish.SetDefaults(58);fish.active=true;fish.whoAmI=2;fish.position=new Vector2(1000,950);fish.target=Main.myPlayer;fish.wet=true;fish.direction=fish.directionY=1;fish.velocity=new Vector2(0,.2f);
                    var sampled=(NpcMotionState)read.Invoke(null,new object[]{fish,Get(host,"Session")});var captured=new object[]{fish,sampled,terrain};terrain.Reset();capture.Invoke(null,captured);sampled=(NpcMotionState)captured[1];Require(sampled.TrackingKind==2,"Known guardian fish precondition.");
                    var env=new PredictionEnvironment{PlayerIndex=Main.myPlayer,PlayerX=a.Center.X,PlayerY=a.Center.Y,PlayerWidth=a.width,PlayerHeight=a.height,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,Multiplayer=true};
                    fish.AI();var args=new object[]{sampled,env,terrain,sampled.Direction,sampled.DirectionY,PredictionStop.None};Require((bool)typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcAquaticMotion").GetMethod("Step",Flags).Invoke(null,args),"Known guardian wander geometry.");var modeled=(NpcMotionState)args[0];
                    Console.WriteLine("FISH GUARDIAN side="+side+" nativeDirection="+fish.direction+" modelDirection="+modeled.Direction+" nativeV="+fish.velocity+" modelV="+new Vector2(modeled.Vx,modeled.Vy));
                    Require(modeled.Target==fish.target && modeled.Direction==fish.direction && Vector2.Distance(fish.velocity,new Vector2(modeled.Vx,modeled.Vy))<.001f,"faceTarget:false still writes actual guardian direction for dry-player wander.");pet.active=false;b.tankPet=-1;cases++;
                }
                foreach(var npc in Main.npc)npc.active=false;
                // Direct native AI cases above own their temporary actors and
                // tracking side effects. Start the Source timeline from fresh
                // original entities, not those already consumed by native AI.
                Main.player[1]=b=new Player{whoAmI=1,active=true};b.tankPet=-1;
                Main.npc[2]=new NPC();var plant=Main.npc[2];plant.SetDefaults(56);plant.whoAmI=2;plant.active=true;plant.target=Main.myPlayer;plant.position=new Vector2(1000,950);plant.velocity=Vector2.Zero;plant.ai[0]=plant.ai[1]=60;
                var root=Main.tile[60,60];var rootBefore=new Tile();rootBefore.CopyFrom(root);root.active(true);root.type=1;
                var deadOld=Main.player[2];deadOld.active=deadOld.dead=true;deadOld.position=new Vector2(1400,950);plant.target=2;
                b.position=new Vector2(900,950);b.velocity=new Vector2(.5f,0);b.gravity=0;b.gravDir=1;b.runAcceleration=b.runSlowdown=0;b.maxRunSpeed=b.accRunSpeed=.5f;b.maxFallSpeed=10;
                var cache=(NpcPredictionCache)Get(source,"Cache");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);
                Console.WriteLine("RETARGET B SOURCE selected="+Get(Get(host,"Selection"),"HasTarget")+" path="+(path==null?"null":path.Identity.Type+" count="+path.Count+" stop="+path.Stop)+" supplied="+Get(source,"targetPlayer"));
                Require(path!=null && path.Identity.Slot==2 && path.Identity.Type==56 && path.Count==121 && (int)Get(source,"targetPlayer")==1,"Real Source replaces dead A with necessary live B before its player-validity gate.");
                var collision=typeof(NPC).GetMethod("UpdateCollision",Flags);
                for(int future=1;future<=120;future++)
                {
                    b.position.X+=.5f;plant.AI();if(Math.Abs(plant.velocity.X)<.005f)plant.velocity.X=0;collision.Invoke(plant,null);
                    if(future==1)Console.WriteLine("RETARGET B FIRST native="+plant.position+" model="+path[future].Bounds.X+","+path[future].Bounds.Y+" nativeV="+plant.velocity+" modelV="+path[future].Vx+","+path[future].Vy+" B="+b.position+" model0="+path[0].Bounds.X+","+path[0].Bounds.Y);
                    if(future==15 || future==30 || future==60 || future==120)
                    {float error=Vector2.Distance(plant.position,new Vector2(path[future].Bounds.X,path[future].Bounds.Y));Console.WriteLine("RETARGET B FROZEN future="+future+" error="+error+" target="+plant.target);Require(plant.target==1 && error<.12f,"Real supplied B timeline before original action: "+future);}
                }
                root.CopyFrom(rootBefore);a.dead=false;plant.active=false;
                var eye=Main.npc[2];eye.SetDefaults(2);eye.whoAmI=2;eye.active=true;eye.target=Main.myPlayer;eye.position=new Vector2(700,650);eye.velocity=new Vector2(1,-.1f);eye.timeLeft=750;
                Main.dayTime=true;double oldSurface=Main.worldSurface;Main.worldSurface=100;a.position=new Vector2(640,640);b.active=false;
                var missing=Main.tile[40,40];Main.tile[40,40]=null;
                try
                {NativeCombatObservationChecks.Fresh(context,host);var escape=cache.Read(0);Require(escape!=null && escape.Identity.Type==2 && escape.Count>1 && (escape.Assumptions&PredictionAssumption.NoPlayerMotionNeeded)!=0,"Real dry daytime Eye rolling ignores unrelated unknown A terrain.");Console.WriteLine("DAY EYE SOURCE count="+escape.Count+" stop="+escape.Stop+" assumptions="+escape.Assumptions);}
                finally{Main.tile[40,40]=missing;Main.worldSurface=oldSurface;Main.dayTime=false;NativeCombatObservationChecks.Save(host,new ObservationOptions());}
            }
            finally{Main.netMode=oldMode;b.active=false;a.dead=false;}
            Console.WriteLine("PASS RETARGET PHASE native actual consumers cases="+cases);
        }
    }
}
