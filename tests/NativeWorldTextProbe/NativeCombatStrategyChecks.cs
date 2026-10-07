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
    internal static class NativeCombatStrategyChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");
            for(int i=0;i<Main.maxPlayers;i++){if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};Main.player[i].active=false;}
            foreach(var npc in Main.npc)npc.active=false;
            Main.netMode=1;Main.dayTime=false;Main.worldSurface=20;Main.tileSolid[TileID.Stone]=true;
            var p=Main.LocalPlayer;p.active=true;p.dead=p.ghost=false;p.position=new Vector2(900,960-p.height);p.velocity=Vector2.Zero;p.controlLeft=p.controlRight=p.controlJump=false;p.carpetFrame=-1;p.gravity=.4f;p.maxFallSpeed=10;p.maxRunSpeed=3;p.accRunSpeed=6;p.runAcceleration=.08f;p.runSlowdown=.2f;p.aggro=0;p.tankPet=-1;
            for(int x=1;x<Main.maxTilesX-1;x++){Main.tile[x,60].active(true);Main.tile[x,60].type=TileID.Stone;}
            Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,marker:true));Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));
            string only=Environment.GetEnvironmentVariable("JUEMINGR_STRATEGY_ONLY");
            if(only=="facing"){Facing(source,p);return;}
            if(only=="moving"){cache.Demand(0,120);MovingCompetition(source,cache,p);return;}
            if(only=="retarget"){NativeCombatRetargetChecks.Run(context);return;}
            if(only=="switchface"){SwitchFacing(source,p);return;}
            if(only=="eyes"){NativeCombatFamilyControlChecks.Eyes(context);return;}
            if(only=="bats"){NativeCombatFamilyControlChecks.Bats(context);return;}
            if(only=="batform"){NativeCombatFamilyControlChecks.BatForms(context);return;}
            if(only=="vultures"){NativeCombatFamilyControlChecks.Vultures(context);return;}
            if(only=="hoppers" || only=="tortoises"){NativeCombatRollingControlChecks.Run(context,only=="hoppers");return;}
            if(only=="swords"){NativeCombatFiniteControlChecks.Swords(context);return;}
            if(only=="mimics"){NativeCombatFiniteControlChecks.Mimics(context);return;}
            if(only=="swordpremise"){NativeCombatFiniteControlChecks.SwordPremise(context);return;}
            if(only=="mimicpremise"){NativeCombatFiniteControlChecks.MimicPremise(context);return;}
            if(only=="jellyfish"){NativeCombatFiniteControlChecks.Jellyfish(context);return;}
            if(only=="mimicbirth"){NativeCombatFiniteControlChecks.MimicBirth(context);return;}
            if(only=="mimicair"){NativeCombatFiniteControlChecks.MimicAir(context);return;}
            if(only=="rollchoice" || only=="rollchoice39"){NativeCombatRollingControlChecks.Choices(context);return;}
            if(only=="roll417"){NativeCombatRollingControlChecks.UnknownBounce(context);return;}
            if(only!="bee")
            {
                var n=Main.npc[2];n.SetDefaults(25);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=new Vector2(4,0);n.target=p.whoAmI;n.wet=true;n.wetCount=1;n.noTileCollide=false;n.timeLeft=750;
                NativeCombatObservationChecks.Fresh(context,host);var frozen=cache.Read(0);
                Require(frozen!=null && frozen.Count>13,"TR01 actual Source publishes the dry-exit future");
                Require(Math.Abs(frozen[1].Vx-2)<.00001 && Math.Abs(frozen[2].Vx-2)<.00001 && Math.Abs(frozen[13].Vx-2)<.00001,"TR01 no-contact Wet→Dry must retain physical final velocity, first="+frozen[1].Vx+" second="+frozen[2].Vx+" late="+frozen[13].Vx);
                Call(source,"Prepare",frozen.Identity,frozen.CaptureTick);Require(ReferenceEquals(frozen,cache.Read(0)),"TR01 repeated identical Source sample retains immutable result");
                n.wet=false;n.velocity=new Vector2(4,0);NativeCombatObservationChecks.Fresh(context,host);var dry=cache.Read(0);
                Require(dry!=null && Math.Abs(dry[120].Vx-4)<.00001,"TR01 unchanged environment keeps base velocity");
                n.position+=n.velocity;n.velocity=new Vector2(3.8f,0);NativeCombatObservationChecks.Fresh(context,host);var transient=cache.Read(0);
                Require(transient!=null && Math.Abs(transient[13].Vx-3.8f)<.00001 && transient[3].Vx<3.8f,"TR01 observed acceleration stays transient and never feeds back into base");
                for(int i=0;i<3;i++)Call(host,"Update",Main.GameUpdateCount);Require(ReferenceEquals(transient,cache.Read(0)),"TR01 identical observed trend sample never advances itself");
                n.velocity.X=5;Call(host,"Update",Main.GameUpdateCount);Require(cache.Read(0)[0].Vx==5 && cache.Read(0).CaptureTick==transient.CaptureTick,"TR01 same tick real velocity correction recomputes without re-marking capture time");
                Console.WriteLine("PASS TR01 Source liquid exit, dry positive, transient/no feedback, repeated sample and same tick correction");n.active=false;
            }
            if(only!="trend")foreach(int type in new[]{210,211})foreach(float birth in new[]{0f,59f,120f})foreach(bool npcTarget in new[]{false,true})
            {
                foreach(var npc in Main.npc)npc.active=false;
                var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.dontTakeDamage=n.immortal=n.friendly=false;n.position=new Vector2(650,700);n.velocity=new Vector2(6,-1);n.target=p.whoAmI;n.ai[0]=99;n.ai[1]=birth;n.timeLeft=750;
                if(npcTarget){var other=Main.npc[3];other.SetDefaults(1);other.whoAmI=3;other.active=true;other.position=new Vector2(630,650);other.velocity=Vector2.Zero;other.life=100;other.dontTakeDamage=other.immortal=other.friendly=false;}
                NativeCombatObservationChecks.Fresh(context,host);var frozen=cache.Read(0);Require(frozen!=null && frozen.Count>30,"TR02 Source bee actual necessary target has usable future type="+type+" birth="+birth+" npc="+npcTarget+" count="+frozen?.Count+" stop="+frozen?.Stop);
                Require(ReferenceEquals(frozen.Identity.Token,n),"TR02 selected receiver is the bee, distinct from its AI target");
                float originX=n.position.X,originY=n.position.Y;
                n.AI();Call(n,"UpdateCollision");
                Require(Math.Abs(frozen[1].Vx-n.velocity.X)<.0001 && Math.Abs(frozen[1].Vy-n.velocity.Y)<.0001 && Math.Abs(frozen[1].Bounds.X-n.position.X)<.0001 && Math.Abs(frozen[1].Bounds.Y-n.position.Y)<.0001,"TR02 frozen before-action bee native rule type="+type+" birth="+birth+" npc="+npcTarget+" native="+n.velocity+" model="+frozen[1].Vx+","+frozen[1].Vy+" target="+n.target+" origin="+originX+","+originY);
                float nearMax=0,farMax=0;bool crossed=false,turned=false;int originalSide=Math.Sign(p.Center.X-n.Center.X);float previousVx=n.velocity.X;
                for(int step=2;step<=120;step++)
                {
                    n.AI();Call(n,"UpdateCollision");var point=frozen[step];float error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),n.position);
                    if(step<=15)nearMax=Math.Max(nearMax,error);farMax=Math.Max(farMax,error);crossed|=Math.Sign(p.Center.X-n.Center.X)!=originalSide;turned|=n.velocity.X*previousVx<0;previousVx=n.velocity.X;
                    if(!npcTarget)Require(error<.3f,"TR02 player frozen120 original motor before-action type="+type+" birth="+birth+" step="+step+" error="+error);
                }
                Require(nearMax<n.width,"TR02 NPC target conditional near future stays within target size type="+type+" birth="+birth+" error="+nearMax);
                Require(farMax<.3f,"TR02 observed static NPC/player competitors switch before the known turn type="+type+" birth="+birth+" error="+farMax);
                Console.WriteLine("PASS TR02 bee frozen120 type="+type+" birth="+birth+" npc="+npcTarget+" target="+n.target+" near15Max="+nearMax+" far120Max="+farMax+" crossed="+crossed+" turned="+turned);
            }
            if(only!="trend")Competition(source,cache,p);
        }
        private static void Facing(object source,Player p)
        {
            var terrain=(IPredictionTerrain)Get(source,"Terrain");var read=source.GetType().GetMethod("Read",Flags);var capture=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation").GetMethod("Capture",Flags);
            var kernel=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcFlyingMotion").GetMethod("Step",Flags);
            foreach(int type in new[]{6,42,210,211})
            {
                foreach(var npc in Main.npc)npc.active=false;
                var n=Main.npc[2];n.SetDefaults(type);n.whoAmI=2;n.active=true;n.position=new Vector2(650,700);n.target=p.whoAmI;n.oldTarget=p.whoAmI;n.direction=-1;n.directionY=-1;n.velocity=Vector2.Zero;n.collideX=true;n.oldVelocity=Vector2.UnitX;n.ai[1]=120;
                p.aggro=type==210 || type==211?-1:0;p.itemAnimation=0;
                if(type==211){n.position=new Vector2(50,10);n.oldTarget=303;}
                var state=(NpcMotionState)read.Invoke(null,new object[]{n,1L});terrain.Reset();object[] observed={n,state,terrain};capture.Invoke(null,observed);state=(NpcMotionState)observed[1];
                var env=new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,WorldSurface=20,Multiplayer=true,PlayerIdleWithNegativeAggro=p.aggro<0};
                object[] args={state,env,terrain,1,PredictionStop.None,false};Require((bool)kernel.Invoke(null,args),"AI5 facing action available");state=(NpcMotionState)args[0];n.oldTarget=n.target;n.AI();
                Require(state.Direction==n.direction && state.DirectionY==n.directionY && Math.Abs(state.Vx-n.velocity.X)<.0001f,"AI5 native retarget before bounce type="+type+" nativeDirection="+n.direction+","+n.directionY+" modelDirection="+state.Direction+","+state.DirectionY+" nativeV="+n.velocity+" modelV="+state.Vx+","+state.Vy);
                Console.WriteLine("PASS AI5 target flip/collision/near idle facing type="+type);
            }
            foreach(var npc in Main.npc)npc.active=false;p.active=false;
            var bee=Main.npc[2];bee.SetDefaults(210);bee.whoAmI=2;bee.active=true;bee.position=new Vector2(650,700);bee.target=303;bee.oldTarget=0;bee.direction=1;bee.directionY=1;bee.targetRect=new Rectangle(600,650,20,20);bee.collideX=true;bee.oldVelocity=-Vector2.UnitX;bee.velocity=Vector2.Zero;bee.ai[1]=120;
            var ignored=Main.npc[3];ignored.SetDefaults(1);ignored.whoAmI=3;ignored.active=true;ignored.friendly=true;ignored.position=new Vector2(600,650);
            var retained=(NpcMotionState)read.Invoke(null,new object[]{bee,1L});terrain.Reset();object[] noCandidate={bee,retained,terrain};capture.Invoke(null,noCandidate);retained=(NpcMotionState)noCandidate[1];Require(!retained.BeeFoundTarget && retained.BeeTargetValid,"Bee no search winner retains native valid old target");
            object[] held={retained,new PredictionEnvironment{Multiplayer=true},terrain,1,PredictionStop.None,false};Require((bool)kernel.Invoke(null,held),"No new winner is not invalid old target");retained=(NpcMotionState)held[0];bee.AI();Require(bee.direction==retained.Direction && Math.Abs(bee.velocity.X-retained.Vx)<.0001f,"No candidate leaves original target/facing unchanged");
            Console.WriteLine("PASS bee no search winner retains old target without facing");p.active=true;
            p.aggro=0;
        }
        private static void Competition(object source,NpcPredictionCache cache,Player p)
        {
            foreach(var npc in Main.npc)npc.active=false;
            var bee=Main.npc[2];bee.SetDefaults(210);bee.whoAmI=2;bee.active=true;bee.dontTakeDamage=bee.immortal=bee.friendly=false;bee.position=new Vector2(p.Center.X-90-bee.width*.5f,p.Center.Y-90-bee.height*.5f);bee.velocity=new Vector2(-2,1);bee.target=303;bee.ai[1]=120;bee.timeLeft=750;
            var other=Main.npc[3];other.SetDefaults(1);other.whoAmI=3;other.active=true;other.dontTakeDamage=other.immortal=other.friendly=false;other.life=100;other.position=new Vector2(bee.Center.X+140-other.width*.5f,bee.Center.Y-other.height*.5f);other.velocity=Vector2.Zero;
            var read=source.GetType().GetMethod("Read",Flags);var before=(NpcMotionState)read.Invoke(null,new object[]{bee,1L});Call(source,"Prepare",before.Identity,(long)Main.GameUpdateCount);var frozen=cache.Read(0);Require(frozen!=null && ReferenceEquals(frozen.Identity.Token,bee) && frozen.Count>1,"Source direct requested bee remains distinct from its AI target");
            bee.AI();Call(bee,"UpdateCollision");Require(bee.target==p.whoAmI && Math.Abs(bee.velocity.X-frozen[1].Vx)<.0001f && Math.Abs(bee.velocity.Y-frozen[1].Vy)<.0001f,"Bee Euclidean competition overturns old NPC/Manhattan target");
            other.position=bee.position+new Vector2(20,0);Call(source,"Prepare",before.Identity,(long)Main.GameUpdateCount);var revised=cache.Read(0);Require(revised!=null && !ReferenceEquals(revised,frozen) && revised.CaptureTick==frozen.CaptureTick,"Same tick target geometry/identity revision recomputes source");
            Console.WriteLine("PASS bee Source Euclidean player/NPC competition and same tick target revision");
            p.position.X+=.6f;bee.SetDefaults(210);bee.whoAmI=2;bee.active=true;bee.position=new Vector2(p.Center.X-20.2f-bee.width*.5f,p.Center.Y-bee.height*.5f);bee.target=303;bee.ai[1]=120;bee.timeLeft=750;other.position=new Vector2(p.Center.X+.3f-other.width*.5f,p.Center.Y-other.height*.5f);
            before=(NpcMotionState)read.Invoke(null,new object[]{bee,1L});Call(source,"Prepare",before.Identity,(long)Main.GameUpdateCount);var sampled=((NpcMotionState[])Get(source,"states"))[0];var kernel=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcFlyingMotion").GetMethod("Step",Flags);var env=new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,Multiplayer=true};object[] action={sampled,env,(IPredictionTerrain)Get(source,"Terrain"),1,PredictionStop.None,false};Require((bool)kernel.Invoke(null,action),"Fractional competitor action available");sampled=(NpcMotionState)action[0];bee.AI();Require(bee.target==p.whoAmI && sampled.Target==bee.target,"Fractional native competition preserves real winner native="+bee.target+" model="+sampled.Target);p.position.X-=.6f;
            Console.WriteLine("PASS bee fractional candidate competition preserves native winner");
        }
        private static void MovingCompetition(object source,NpcPredictionCache cache,Player p)
        {
            foreach(var npc in Main.npc)npc.active=false;
            p.controlLeft=true;p.velocity=new Vector2(-3,0);p.gravDir=1;
            var bee=Main.npc[2];bee.SetDefaults(210);bee.whoAmI=2;bee.active=true;bee.dontTakeDamage=bee.immortal=bee.friendly=false;bee.position=new Vector2(650,p.Center.Y-bee.height*.5f);bee.velocity=Vector2.Zero;bee.target=303;bee.ai[1]=120;bee.timeLeft=750;
            var other=Main.npc[3];other.SetDefaults(1);other.whoAmI=3;other.active=true;other.dontTakeDamage=other.immortal=other.friendly=false;other.life=100;other.position=new Vector2(700-other.width*.5f,p.Center.Y-other.height*.5f);other.velocity=Vector2.Zero;
            var read=source.GetType().GetMethod("Read",Flags);var before=(NpcMotionState)read.Invoke(null,new object[]{bee,1L});Call(source,"Prepare",before.Identity,(long)Main.GameUpdateCount);var frozen=cache.Read(0);Require(frozen!=null && frozen.Count==121,"Moving real competitor Source future available");
            Require((frozen.Assumptions&PredictionAssumption.NoPlayerMotionNeeded)==0,"Captured moving player competes even when current NPC wins");
            bool switched=false;float max=0;
            for(int i=1;i<=120;i++){p.HorizontalMovement();p.position.X+=p.velocity.X;bee.oldTarget=bee.target;bee.AI();Call(bee,"UpdateCollision");switched|=bee.target==p.whoAmI;max=Math.Max(max,Vector2.Distance(new Vector2(frozen[i].Bounds.X,frozen[i].Bounds.Y),bee.position));}
            Require(switched && max<.3f,"Moving held player overtakes initial NPC in frozen before-action future error="+max+" switched="+switched);p.controlLeft=false;p.velocity=Vector2.Zero;
            Console.WriteLine("PASS bee initial NPC winner + real held moving player switch frozen120 max="+max);
        }
        private static void SwitchFacing(object source,Player p)
        {
            foreach(var npc in Main.npc)npc.active=false;p.position.X=1010;p.aggro=-1;p.itemAnimation=0;p.controlLeft=true;p.velocity=new Vector2(-3,0);
            var bee=Main.npc[2];bee.SetDefaults(210);bee.whoAmI=2;bee.active=true;bee.position=new Vector2(120-bee.width*.5f,p.Center.Y-bee.height*.5f);bee.target=p.whoAmI;bee.oldTarget=p.whoAmI;bee.direction=-1;bee.collideX=true;bee.oldVelocity=Vector2.UnitX;bee.velocity=Vector2.Zero;bee.ai[1]=120;
            var other=Main.npc[3];other.SetDefaults(1);other.whoAmI=3;other.active=true;other.dontTakeDamage=other.immortal=other.friendly=false;other.life=100;other.position=new Vector2(bee.Center.X+900.5f-other.width*.5f,p.Center.Y-other.height*.5f);
            var terrain=(IPredictionTerrain)Get(source,"Terrain");var read=source.GetType().GetMethod("Read",Flags);var capture=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation").GetMethod("Capture",Flags);var state=(NpcMotionState)read.Invoke(null,new object[]{bee,1L});terrain.Reset();object[] sampled={bee,state,terrain};capture.Invoke(null,sampled);state=(NpcMotionState)sampled[1];Require(state.BeeTarget==303,"Switch facing starts with actual NPC winner");
            p.HorizontalMovement();p.position.X+=p.velocity.X;var env=new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerIdleWithNegativeAggro=true,Multiplayer=true};var kernel=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcFlyingMotion").GetMethod("Step",Flags);object[] action={state,env,terrain,1,PredictionStop.None,false};Require((bool)kernel.Invoke(null,action),"Switch facing action available");state=(NpcMotionState)action[0];bee.oldTarget=bee.target;bee.AI();Require(bee.target==p.whoAmI && state.Target==bee.target && state.Direction==bee.direction && Math.Abs(state.Vx-bee.velocity.X)<.0001f,"First future winner owns facing permission, native="+bee.target+"/"+bee.direction+"/"+bee.velocity.X+" model="+state.Target+"/"+state.Direction+"/"+state.Vx);
            Console.WriteLine("PASS bee first future NPC→idle far player changes winner and suppresses Face");p.controlLeft=false;p.aggro=0;p.velocity=Vector2.Zero;
        }
    }
}
