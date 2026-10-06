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
    // A: locked original local rules. B: actual default Source publication.
    // These isolated fixtures never initialize/run Main or read owner saves.
    internal static class NativeCombatFoundationChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");
            var readPlayer=source.GetType().GetMethod("ReadPlayer",Flags);
            if(Environment.GetEnvironmentVariable("JUEMINGR_FOUNDATION_ONLY")!="source"){Vertical(readPlayer);VerticalSteps(readPlayer);Gravity(source);Horizontal(readPlayer);FighterRules(source);}
            DefaultSource(context,host,source);
            if(Environment.GetEnvironmentVariable("JUEMINGR_FOUNDATION_ONLY")!="source")
            {
                if(Environment.GetEnvironmentVariable("JUEMINGR_FOUNDATION_ONLY")!="ground")NativeCombatFoundationLiquidChecks.Run(source);
                if(Environment.GetEnvironmentVariable("JUEMINGR_FOUNDATION_ONLY")!="liquid")NativeCombatFoundationGroundChecks.Run(source);
            }
            SlimeSource(context,host,source);
            FighterEntrySource(context,host,source);
            FloatingSource(context,host,source,readPlayer);
        }
        private static void Vertical(MethodInfo readPlayer)
        {
            // Set the original base from its documented liquid branch, then
            // execute ORIGINAL UpdateJumpHeight. ReadPlayer must consume this
            // player's completed aggregate, never the static last-player scratch.
            var height=typeof(Player).GetField("jumpHeight",Flags);var speed=typeof(Player).GetField("jumpSpeed",Flags);int cases=0;
            NativeCombatLiveContextChecks.InitializeMount();
            foreach(int fluid in new[]{0,1,2,3,4,5,6})foreach(int effect in new[]{0,1,2,3,4,5})foreach(bool sticky in new[]{false,true})foreach(bool dazed in new[]{false,true})
            {
                var p=new Player{whoAmI=1,active=true,wet=fluid!=0,honeyWet=fluid==2,shimmerWet=fluid==3,merman=fluid==4,trident=fluid==5 || fluid==6,lavaWet=fluid==6,jumpBoost=effect==1,frogLegJumpBoost=effect==2,empressBrooch=effect==3,moonLordLegs=effect==4,wereWolf=effect==5,sticky=sticky,dazed=dazed};
                height.SetValue(null,fluid==3?23:fluid==5?25:fluid==1 || fluid==6?30:15);speed.SetValue(null,fluid==3 || fluid==5?5.51f:fluid==1 || fluid==6?6.01f:5.01f);
                p.UpdateJumpHeight();float expectedSpeed=(float)speed.GetValue(null);int expectedHeight=(int)height.GetValue(null);
                // Poison shared scratch AFTER the completed own effects.
                height.SetValue(null,999);speed.SetValue(null,999f);
                var model=(PredictionPlayerMotion)readPlayer.Invoke(null,new object[]{p});
                Require(model.JumpHeight==expectedHeight && Math.Abs(model.JumpSpeed-expectedSpeed)<.00001f,"A actual ReadPlayer owned vertical liquid/effects fluid="+fluid+" effect="+effect+" native="+expectedHeight+"/"+expectedSpeed+" sampled="+model.JumpHeight+"/"+model.JumpSpeed);cases++;
            }
            Console.WriteLine("PASS A actual vertical Source / original effect ordering cases="+cases);
        }
        private static void VerticalSteps(MethodInfo readPlayer)
        {
            var height=typeof(Player).GetField("jumpHeight",Flags);var speed=typeof(Player).GetField("jumpSpeed",Flags);
            int cases=0;bool oldServer=Main.dedServ;double oldSurface=Main.worldSurface;int oldWidth=Main.maxTilesX;Main.dedServ=true;Main.worldSurface=140;Main.maxTilesX=120;
            try
            {
                foreach(bool merfolk in new[]{false,true})foreach(int jump in new[]{0,1,4})foreach(float vy in new[]{0f,-2f,1f})
                foreach(bool held in new[]{false,true})foreach(bool release in new[]{false,true})foreach(float dir in new[]{-1f,1f})
                foreach(bool slow in new[]{false,true})foreach(bool up in new[]{false,true})foreach(bool down in new[]{false,true})
                {
                    var p=new Player{whoAmI=1,active=true,position=new Vector2(400,880),wet=merfolk,merman=merfolk,accFlipper=merfolk,jump=jump,velocity=new Vector2(0,vy),controlJump=held,releaseJump=release,gravDir=dir,slowFall=slow};
                    typeof(Player).GetField("tryKeepingHoveringUp",Flags).SetValue(p,up);typeof(Player).GetField("tryKeepingHoveringDown",Flags).SetValue(p,down);
                    if(merfolk)p.armor[3].SetDefaults(497);
                    var model=(PredictionPlayerMotion)readPlayer.Invoke(null,new object[]{p});
                    // Locked Player24554/24856 bases and altitude formula,
                    // independent of the production Parameters result.
                    float world=(float)Main.maxTilesX/4200;world*=world;
                    float altitude=(float)((double)(p.position.Y/16-(60+10*world))/(Main.worldSurface/(Main.remixWorld?1.0:6.0)));
                    float gravity=(merfolk?.3f:Player.defaultGravity)*Math.Max(Main.remixWorld?.1f:.25f,Math.Min(1,altitude));
                    Require(Math.Abs(model.Gravity-gravity)<.000001,"A owned gravity follows locked base/altitude, not sampled scratch.");
                    height.SetValue(null,15);speed.SetValue(null,5.01f);
                    if(merfolk)p.releaseJump=true; // native equip refresh25772
                    p.JumpMovement();
                    float acceleration=gravity;if(slow && !down)acceleration/=up?10:3;
                    float expected=p.velocity.Y+acceleration*dir,fall=(merfolk?7:10)+.01f;
                    if(expected*dir>fall)expected=fall*dir;
                    if(slow && !down && expected*dir>fall/3)expected=fall/3*dir;
                    if(slow && up && expected*dir>fall/5)expected=fall/10*dir;
                    PlayerVerticalMotion.Step(ref model,new PredictionEnvironment{WorldWidth=Main.maxTilesX,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld});
                    Require(Math.Abs(model.Vy-expected)<.00001 && model.Jump==p.jump && model.ReleaseJump==p.releaseJump && model.SwimTime==p.swimTime,"A original JumpMovement plus ordinary gravity/hover direction/hold phase: "+merfolk+"/"+jump+"/"+vy+"/"+held+"/"+release+"/"+dir+"/"+slow+"/"+up+"/"+down+" expected="+expected+" actual="+model.Vy);cases++;
                }
                foreach(bool independent in new[]{false,true})foreach(bool lava in new[]{false,true})foreach(int slime in new[]{0,1})
                {
                    var m=new PredictionPlayerMotion{VerticalProfile=true,DefaultGravity=.4f,GravityDirection=1,Wet=true,Merman=true,MerfolkEquipment=true,BaseFlipper=independent,Flipper=true,Jump=15,JumpHeight=15,SwimTime=10,WetSlime=slime};
                    PlayerVerticalMotion.AfterFluid(ref m,lava,false,lava,false);
                    Require(m.Jump==(lava || slime>0?15:3) && m.SwimTime==(lava?9:0) && m.WetSlime==0,"A liquid exit consumes old slime before decrement and native frame clears dry swim.");
                    m.HoldJump=true;m.ReleaseJump=true;m.Vy=1;m.Jump=0;PlayerVerticalMotion.Step(ref m,default(PredictionEnvironment));
                    Require(m.Merman==false && m.Flipper==independent && (m.Vy<0)==(lava && independent),"A merfolk exits dry/lava without retaining derived flipper; independent flippers remain legal. independent="+independent+" lava="+lava+" slime="+slime+" merman="+m.Merman+" flipper="+m.Flipper+" vy="+m.Vy+" jump="+m.Jump);cases++;
                }
                Console.WriteLine("PASS A native jump / hover acceleration / fluid timers and equipment provenance cases="+cases);
            }
            finally{Main.dedServ=oldServer;Main.worldSurface=oldSurface;Main.maxTilesX=oldWidth;}
        }
        private static void Gravity(object source)
        {
            var native=typeof(NPC).GetMethod("UpdateNPC_UpdateGravity",Flags);
            var gravity=typeof(NPC).GetField("gravity",Flags);
            var kernel=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcGravityMotion").GetMethod("BeforeAi",Flags);
            int cases=0;int oldWidth=Main.maxTilesX;double oldSurface=Main.worldSurface;object oldGravity=gravity.GetValue(null);
            try
            {
                foreach(int width in new[]{120,4200,6400,8400})foreach(float y in new[]{200f,1440.125f,5000f})
                foreach(int type in new[]{258,425,427,426,576,577,541,17,999})foreach(bool phase in new[]{false,true})foreach(int liquid in new[]{0,1,2,3})
                {
                    Main.maxTilesX=width;Main.worldSurface=479.123456789;
                    var n=new NPC{type=type,aiStyle=type==17?7:99,position=new Vector2(400,y),velocity=new Vector2(2,40),wet=liquid!=0,honeyWet=liquid==2,shimmerWet=liquid==3};
                    n.ai[0]=type==17?(phase?25:0):(phase?1:0);n.ai[1]=phase?2:0;n.ai[2]=phase?1:0;
                    var state=new NpcMotionState{MotionType=type,Style=n.aiStyle,Y=y,Vx=2,Vy=40,A0=n.ai[0],A1=n.ai[1],A2=n.ai[2],Wet=n.wet,Honey=n.honeyWet,Shimmer=n.shimmerWet};
                    var e=new PredictionEnvironment{WorldWidth=width,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface};
                    var args=new object[]{state,e};kernel.Invoke(null,args);state=(NpcMotionState)args[0];
                    var fall=new object[]{0f};native.Invoke(n,fall);
                    Require(state.Gravity==(float)gravity.GetValue(null) && state.MaxFall==(float)fall[0] && state.Vy==n.velocity.Y,"A original public gravity type/phase/height/old-fluid operands: "+type+" phase="+phase+" width="+width+" y="+y+" fluid="+liquid);cases++;
                }
                Console.WriteLine("PASS A GRAVITY original local method exact cases="+cases);
            }
            finally{Main.maxTilesX=oldWidth;Main.worldSurface=oldSurface;gravity.SetValue(null,oldGravity);}
        }
        private static void Horizontal(MethodInfo readPlayer)
        {
            NativeCombatLiveContextChecks.InitializeMount();
            var kernel=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.PlayerHorizontalMotion").GetMethod("Step",Flags);
            int cases=0;bool oldServer=Main.dedServ;float oldWind=Main.windSpeedCurrent;
            try
            {
                Main.dedServ=true;
                foreach(int control in new[]{-1,0,1})foreach(float vx in new[]{-6f,-3f,-.1f,0f,.1f,2.99f,3f,6f})foreach(bool air in new[]{false,true})
                foreach(bool wings in new[]{false,true})foreach(int delay in new[]{-1,0})foreach(bool wrong in new[]{false,true})foreach(int mount in new[]{-1,6,5})
                {
                    var p=new Player{whoAmI=1,active=true,position=new Vector2(400,400),velocity=new Vector2(vx,air?1:0),gravDir=1,maxRunSpeed=3,accRunSpeed=6,runAcceleration=.08f,runSlowdown=.2f,controlLeft=control<0,controlRight=control>0,wingsLogic=wings?1:0,dashDelay=delay,onWrongGround=wrong};
                    if(mount>=0){int mode=Main.netMode;Main.netMode=2;try{p.mount.SetMount(mount,p);}finally{Main.netMode=mode;}p.velocity=new Vector2(vx,air?1:0);}
                    // Source hover parameters are mount-owned; establish the
                    // matching original HorizontalMovement entry parameters.
                    if(mount==5){p.runAcceleration=p.mount.Acceleration;p.runSlowdown=.2f;p.maxRunSpeed=p.mount.RunSpeed;p.accRunSpeed=p.mount.DashSpeed;}
                    var motion=(PredictionPlayerMotion)readPlayer.Invoke(null,new object[]{p});var args=new object[]{motion};kernel.Invoke(null,args);motion=(PredictionPlayerMotion)args[0];p.HorizontalMovement();
                    Require(Math.Abs(motion.Vx-p.velocity.X)<.000001f,"A sampled ordinary/fast horizontal original branch: control="+control+" vx="+vx+" air="+air+" wings="+wings+" dash="+delay+" wrong="+wrong+" mount="+mount+" native="+p.velocity.X+" model="+motion.Vx);cases++;
                }
                foreach(float wind in new[]{-.7f,.3f,.7f})foreach(float boost in new[]{-4f,0f,4f})
                {
                    Main.windSpeedCurrent=wind;var p=new Player{whoAmI=1,active=true,velocity=new Vector2(0,1),gravDir=1,maxRunSpeed=3,accRunSpeed=6,runAcceleration=.08f,runSlowdown=.2f,windPushed=true,trackBoost=boost};
                    var args=new object[]{(PredictionPlayerMotion)readPlayer.Invoke(null,new object[]{p})};kernel.Invoke(null,args);p.HorizontalMovement();Require(((PredictionPlayerMotion)args[0]).Vx==p.velocity.X,"A wind after friction / consumed track boost order.");cases++;
                }
                Console.WriteLine("PASS A HORIZONTAL actual ReadPlayer + original HorizontalMovement cases="+cases);
            }
            finally{Main.dedServ=oldServer;Main.windSpeedCurrent=oldWind;}
        }
        private static void FighterRules(object source)
        {
            var kernel=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.FighterHorizontalMotion").GetMethod("Step",Flags);var read=source.GetType().GetMethod("Read",Flags);var ai=typeof(NPC).GetMethod("AI_003_Fighters",Flags);var nativeGravity=typeof(NPC).GetMethod("UpdateNPC_UpdateGravity",Flags);int cases=0;
            var p=Main.LocalPlayer;p.active=true;p.dead=false;p.position=new Vector2(1100,500);Main.dayTime=false;Main.netMode=1;
            for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};typeof(NPC).GetField("gravity",Flags).SetValue(null,.3f);
            var env=new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,WorldSurface=20,WorldWidth=120};
            foreach(int type in new[]{3,21,27,104,109,67,78,79,287,243,251,270,310,508,580,582,159,199,120,386,460,419,518,532,430,425,489,466,586,480,462,463})foreach(float vx in new[]{-2f,0f,.8f,2f,7f})foreach(float vy in new[]{0f,1f})foreach(int life in new[]{49,50,100})foreach(float scale in new[]{.8f,1f,1.2f})
            {
                var n=new NPC();n.SetDefaults(type);n.whoAmI=199;n.position=new Vector2(500,400);n.target=p.whoAmI;n.direction=n.spriteDirection=1;n.velocity=new Vector2(vx,vy);n.lifeMax=100;n.life=life;n.scale=scale;n.ai[3]=type==466?0:1;n.ai[2]=type==466?1:0;n.noTileCollide=true;
                if(type==586)n.alpha=0;
                var state=(NpcMotionState)read.Invoke(null,new object[]{n,1L});state.Gravity=.3f;var args=new object[]{state,env};Require((bool)kernel.Invoke(null,args),"A direct common fighter family modeled.");state=(NpcMotionState)args[0];ai.Invoke(n,null);
                Require(Math.Abs(n.velocity.X-state.Vx)<.00001f,"A original common fighter horizontal type="+type+" life="+life+" vx="+vx+" vy="+vy+" native="+n.velocity.X+" model="+state.Vx);cases++;
            }
            foreach(float phase in new[]{0f,-16f,-1f})
            {
                var n=new NPC();n.SetDefaults(466);n.whoAmI=199;n.position=new Vector2(500,400);n.target=p.whoAmI;n.direction=1;n.velocity=new Vector2(-2,0);n.ai[2]=phase;
                var state=(NpcMotionState)read.Invoke(null,new object[]{n,1L});var args=new object[]{state,env};Require(!(bool)kernel.Invoke(null,args),"A dormant/revealing466 cannot enter common motor.");ai.Invoke(n,null);
                Require(n.velocity.X==(phase==-1?2:-2),"A original466 reveal returns before horizontal motor.");cases++;
            }
            foreach(int entry in new[]{0,1,2})
            {
                var n=new NPC();n.SetDefaults(586);n.whoAmI=199;n.position=new Vector2(500,400);n.target=p.whoAmI;n.direction=1;n.velocity=new Vector2(-2,0);n.alpha=entry==0?255:0;n.wet=entry==1;n.ai[3]=entry==2?-.10101f:0;
                var state=(NpcMotionState)read.Invoke(null,new object[]{n,1L});var args=new object[]{state,env};Require(!(bool)kernel.Invoke(null,args),"A586 spawn/fluid vector entry is independent of common motor.");cases++;
            }
            foreach(int entry in new[]{0,1,2})
            {
                var n=new NPC();n.SetDefaults(461);n.whoAmI=199;n.position=new Vector2(500,400);n.target=p.whoAmI;n.direction=1;n.velocity=new Vector2(2,0);n.wet=entry==1;n.ai[3]=entry==2?-.10101f:0;
                var state=(NpcMotionState)read.Invoke(null,new object[]{n,1L});state.Gravity=.3f;var args=new object[]{state,env};bool common=(bool)kernel.Invoke(null,args);
                Require(common==(entry==0),"A461 only ordinary dry action enters common speed2.");ai.Invoke(n,null);
                if(entry==0)Require(n.velocity.X==((NpcMotionState)args[0]).Vx && n.velocity.X==2,"A original461 dry common speed2 remains.");
                else if(entry==1)Require(n.noGravity && n.width==34 && n.height==24 && n.ai[3]==-.10101f,"A original461 wet pre-motor vector branch owns shape/gravity phase.");
                else Require(!n.noGravity && n.ai[3]!=-.10101f,"A original461 exits its fluid vector before common dry movement.");cases++;
            }
            // Public clipping is observable BEFORE the independent aerial AI
            // branch: with a lower target, vy<6 permits +.15 only after clip.
            float clipped=0,unclipped=0;
            foreach(bool apply in new[]{false,true})
            {
                var n=new NPC();n.SetDefaults(427);n.whoAmI=199;n.position=new Vector2(500,300);n.target=p.whoAmI;n.direction=1;n.velocity=new Vector2(1,40);n.ai[2]=1;n.noTileCollide=true;
                if(apply)nativeGravity.Invoke(n,new object[]{0f});ai.Invoke(n,null);if(apply)clipped=n.velocity.Y;else unclipped=n.velocity.Y;
            }
            Require(Math.Abs(clipped-4.15f)<.00001f && unclipped==40,"A original AI branch entrance changes when public gravity clips first.");
            Main.netMode=0;Console.WriteLine("PASS A original common fighter local AI cases="+cases+"; public pre-AI clip discriminator clipped="+clipped+" unclipped="+unclipped);
        }
        private static void DefaultSource(object context,object host,object source)
        {
            foreach(var n in Main.npc)n.active=false;
            Main.dayTime=false;Main.worldSurface=20;Main.bottomWorld=Main.maxTilesY*16;Main.tileSolid[1]=true;
            for(int x=1;x<Main.maxTilesX-1;x++){Main.tile[x,60].active(true);Main.tile[x,60].type=1;}
            var p=Main.LocalPlayer;p.active=true;p.dead=p.ghost=false;p.position=new Vector2(800,960-p.height);p.velocity=Vector2.Zero;p.gravDir=1;p.gravity=.4f;p.maxFallSpeed=10;p.maxRunSpeed=3;p.accRunSpeed=6;p.runAcceleration=.08f;p.runSlowdown=.2f;p.carpetFrame=-1;p.controlRight=true;p.controlLeft=false;
            var n0=Main.npc[2];n0.SetDefaults(2);n0.whoAmI=2;n0.active=true;n0.target=Main.myPlayer;n0.position=new Vector2(500,600);n0.timeLeft=750;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,marker:true));
            var cache=(NpcPredictionCache)Get(source,"Cache");int cases=0;
            foreach(int mechanism in new[]{0,1,2,3,0})
            {
                p.pulley=mechanism==2;p.canFloatInWater=mechanism==3;p.grappling[0]=mechanism==1?4:-1;p.grapCount=mechanism==1?1:0;
                var hook=Main.projectile[4];hook.active=mechanism==1;hook.owner=p.whoAmI;hook.aiStyle=7;hook.ai[0]=2;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);
                Console.WriteLine("B SOURCE mechanism="+mechanism+" selected="+Get(Get(host,"Selection"),"Target")+" steps="+(path==null?0:path.Count-1)+" stop="+path?.Stop+" assumptions="+path?.Assumptions);
                Require(path!=null && path.Count==121 && path.Strategy==PredictionStrategy.RollingConditional,"B default real selection/Source/Cache supplies ordinary and legal complex current+120.");
                Require(((path.Assumptions&PredictionAssumption.CurrentPlayerObservation)!=0)==(mechanism==1 || mechanism==2),"B dry float capability stays ordinary; entering/exiting actual complex player premise recovers on the next real sample.");cases++;
            }
            p.canFloatInWater=false;p.grappling[0]=4;p.grapCount=1;Main.projectile[4].active=true;Main.projectile[4].owner=p.whoAmI+1;
            NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)==null && (bool)Get(Get(host,"Selection"),"HasTarget") && (bool)Get(host,"Marker"),"B bad grapple owner rejects future while independent selected marker remains.");cases++;
            p.grappling[0]=-1;p.grapCount=0;p.velocity=new Vector2(float.NaN,0);NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)==null,"B real source invalid player numeric remains rejected.");cases++;
            p.velocity=Vector2.Zero;NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)!=null,"B restoring valid ordinary numeric state recovers automatically.");cases++;
            p.grappling[0]=4;p.grapCount=1;Main.projectile[4].owner=p.whoAmI;n0.SetDefaults(43);n0.whoAmI=2;n0.active=true;n0.target=p.whoAmI;n0.position=new Vector2(500,600);n0.ai[0]=n0.ai[1]=30;
            var root=Main.tile[30,30];Main.tile[30,30]=null;NativeCombatObservationChecks.Fresh(context,host);
            Require(cache.Read(0)==null && (bool)Get(Get(host,"Selection"),"HasTarget") && Main.tile[30,30]==null,"B legal grapple player premise never waives missing necessary NPC root geometry or creates a Tile.");Main.tile[30,30]=root;cases++;
            n0.SetDefaults(2);n0.whoAmI=2;n0.active=true;n0.target=p.whoAmI;n0.position=new Vector2(float.NaN,600);NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)==null,"B bounded player never waives nonfinite selected NPC physical state.");cases++;
            n0.position=new Vector2(500,600);p.grapCount=0;NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)==null,"B inconsistent active grapple count remains an invalid premise.");cases++;
            p.grappling[0]=-1;Main.projectile[4].active=false;n0.SetDefaults(34);n0.whoAmI=2;n0.active=true;n0.target=255;n0.position=new Vector2(500,600);p.velocity=new Vector2(float.NaN,0);NativeCombatObservationChecks.Fresh(context,host);
            Require(cache.Read(0)!=null && cache.Read(0).Count==121 && (cache.Read(0).Assumptions&PredictionAssumption.NoPlayerMotionNeeded)!=0,"B independent default Source trend does not require missing numbered player or unrelated local velocity.");cases++;
            p.velocity=Vector2.Zero;n0.SetDefaults(2);n0.whoAmI=2;n0.active=true;n0.target=p.whoAmI;n0.position=new Vector2(500,600);
            n0.dontTakeDamage=true;NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)==null && !(bool)Get(Get(host,"Selection"),"HasTarget"),"B invalid receiver identity never becomes a conditional target.");cases++;
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatObservationChecks.Fresh(context,host);Require(cache.Required==0 && cache.Read(0)==null,"B OFF retires the shared demand.");cases++;
            Console.WriteLine("PASS B DEFAULT SOURCE original Host selection + ReadPlayer + rolling + actual Cache cases="+cases);
        }
        private static void SlimeSource(object context,object host,object source)
        {
            var p=Main.LocalPlayer;int otherIndex=p.whoAmI==0?1:0;var other=Main.player[otherIndex]=new Player{whoAmI=otherIndex,active=true};other.position=new Vector2(1700,900);other.velocity=Vector2.Zero;
            var cache=(NpcPredictionCache)Get(source,"Cache");var n=Main.npc[2];int cases=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,marker:true));
            foreach(bool oldDead in new[]{false,true})foreach(int firstAction in new[]{0,1,2})
            {
                Main.dayTime=false;other.dead=oldDead;n.SetDefaults(1);n.whoAmI=2;n.active=true;n.target=other.whoAmI;n.position=new Vector2(500,960-n.height);n.velocity=Vector2.Zero;n.ai[0]=-1;n.ai[2]=firstAction==0?0:1;n.ai[3]=-1;n.wet=firstAction==2;
                if(n.wet)n.velocity.Y=-1;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);
                Console.WriteLine("B SLIME oldDead="+oldDead+" firstAction="+firstAction+" future="+(path==null?0:path.Count-1)+" stop="+path?.Stop);
                Require(path!=null && path.Count>1 && path.Stop!=PredictionStop.MissingDependency,"B actual default Source accepts captured living closest at initial/jump/wet native retarget, including dead old numbered player.");cases++;
            }
            // Before any target choice this special lava slime consumes the
            // old numbered player's vertical relation. A dead prerequisite
            // must not be replaced by an unrelated local-player observation.
            n.SetDefaults(59);n.whoAmI=2;n.active=true;n.target=other.whoAmI;n.position=new Vector2(500,900);n.wet=true;n.velocity.Y=-1;n.ai[2]=1;other.dead=true;
            NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)==null,"B lava vertical old-target prerequisite remains a hard Source refusal.");cases++;
            foreach(bool oldDead in new[]{false,true})
            {
                Main.dayTime=true;Main.worldSurface=140;other.dead=oldDead;n.SetDefaults(1);n.whoAmI=2;n.active=true;n.target=other.whoAmI;n.position=new Vector2(500,900);n.velocity=Vector2.Zero;n.ai[0]=-1;n.ai[2]=1;n.ai[3]=-1;n.lifeRegenCount=-112;n.AddBuff(BuffID.OnFire,60);
                Require(n.life==n.lifeMax && n.buffTime[0]>0,"B legal full-health burning slime observation precedes the next DOT action.");
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Console.WriteLine("B SLIME health-before-AI oldDead="+oldDead+" future="+(path==null?0:path.Count-1)+" stop="+path?.Stop);
                Require(path!=null && path.Count>1 && path.Stop!=PredictionStop.MissingDependency,"B real Source must use AI-entry life after DOT when selecting a first jumping player's prerequisite.");cases++;
            }
            Main.dayTime=false;Main.worldSurface=20;
            other.active=false;other.dead=false;n.SetDefaults(2);n.whoAmI=2;n.active=true;n.target=p.whoAmI;n.position=new Vector2(500,600);
            Console.WriteLine("PASS B SLIME actual default Source first-action prerequisite cases="+cases);
        }
        private static void FighterEntrySource(object context,object host,object source)
        {
            var p=Main.LocalPlayer;var other=Main.player[(p.whoAmI+1)%Main.maxPlayers];var n=Main.npc[2];var cache=(NpcPredictionCache)Get(source,"Cache");int cases=0;
            p.active=true;p.dead=false;p.position=new Vector2(800,900);p.velocity=Vector2.Zero;other.active=true;other.position=new Vector2(1600,900);
            foreach(bool dead in new[]{false,true})foreach(int type in new[]{466,586,461})
            {
                other.dead=dead;n.SetDefaults(type);n.whoAmI=2;n.active=true;n.target=other.whoAmI;n.position=new Vector2(500,900);n.velocity=new Vector2(1,0);n.ai[3]=0;n.wet=type==461;
                NativeCombatObservationChecks.Fresh(context,host);var path=cache.Read(0);Console.WriteLine("B FIGHTER entry type="+type+" oldDead="+dead+" future="+(path==null?0:path.Count-1)+" stop="+path?.Stop);
                Require(path!=null && path.Count>1 && path.Stop!=PredictionStop.MissingDependency,"B fighter action-entry TargetClosest shares the real Source prerequisite.");cases++;
            }
            other.dead=true;n.SetDefaults(466);n.whoAmI=2;n.active=true;n.target=other.whoAmI;n.position=new Vector2(500,900);n.ai[2]=-8;n.ai[3]=0;
            NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)!=null && cache.Read(0).Count==121 && (cache.Read(0).Assumptions&PredictionAssumption.NoPlayerMotionNeeded)!=0,"B revealing466 trend consumes no old numbered player and must not acquire a dead prerequisite.");cases++;
            foreach(int type in new[]{461,586})
            {
                n.SetDefaults(type);n.whoAmI=2;n.active=true;n.target=other.whoAmI;n.position=new Vector2(500,900);n.alpha=0;n.ai[3]=-.10101f;n.wet=false;
                NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)!=null && cache.Read(0).Count==121 && (cache.Read(0).Assumptions&PredictionAssumption.NoPlayerMotionNeeded)!=0,"B fluid-exit trend does not read the dead old player or invent a local replacement.");cases++;
            }
            other.active=false;other.dead=false;n.SetDefaults(2);n.whoAmI=2;n.active=true;n.target=p.whoAmI;n.position=new Vector2(500,600);
            Console.WriteLine("PASS B FIGHTER bounded action-entry prerequisites cases="+cases);
        }
        private static void FloatingSource(object context,object host,object source,MethodInfo read)
        {
            var p=Main.LocalPlayer;var cache=(NpcPredictionCache)Get(source,"Cache");var terrain=(IPredictionTerrain)Get(source,"Terrain");int cases=0;
            NativeCombatLiveContextChecks.InitializeMount();bool server=Main.dedServ;int network=Main.netMode;
            try
            {
                Main.dedServ=true;Main.netMode=0;
                foreach(int mount in new[]{-1,37,5})foreach(bool down in new[]{false,true})
                {
                    if(p.mount.Active)p.mount.Dismount(p);if(mount>=0)p.mount.SetMount(mount,p);
                    p.position=new Vector2(801,928);p.velocity=Vector2.Zero;p.wet=false;p.canFloatInWater=true;p.controlDown=down;
                    var sampled=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});
                    Require(sampled.FloatInWater==p.ShouldFloatInWater && !sampled.FloatingNow,"B dry floating ability and native mount/Down qualification never imply current floating.");cases++;
                }
                int lineCases=0;var immune=typeof(Player).GetField("shimmerImmune",Flags);
                foreach(int mount in new[]{-1,37,5})foreach(bool down in new[]{false,true})foreach(bool shimmer in new[]{false,true})foreach(bool immunity in new[]{false,true})foreach(int pattern in new[]{-1,0,1,2,3})foreach(float vy in new[]{-2f,0f,8f})
                {
                    if(p.mount.Active)p.mount.Dismount(p);if(mount>=0)p.mount.SetMount(mount,p);p.width=20;p.height=42;p.position=new Vector2(801,916);p.velocity=new Vector2(0,vy);p.wet=true;p.shimmerWet=shimmer;p.honeyWet=false;p.ignoreWater=p.merman=p.trident=false;p.canFloatInWater=true;p.controlDown=down;immune.SetValue(p,immunity);
                    int xx=(int)(p.Center.X/16),yy=(int)(p.Center.Y/16);for(int row=-2;row<=1;row++){Main.tile[xx,yy+row].active(false);Main.tile[xx,yy+row].liquid=(byte)(pattern==row+2?255:0);}
                    float line;bool exists=Collision.GetWaterLine(xx,yy,out line);
                    bool expected=p.ShouldFloatInWater && (!shimmer || immunity) && (!exists || p.Center.Y-(mount==37?6:0)+8+vy>=line);
                    Require(((PredictionPlayerMotion)read.Invoke(null,new object[]{p})).FloatingNow==expected,"B original readonly line branches / mount37 offset / Down / shimmer immunity: mount="+mount+" pattern="+pattern+" vy="+vy+" shimmer="+shimmer+" immunity="+immunity);lineCases++;
                }
                Console.WriteLine("PASS B FLOAT original water-line/action qualification matrix="+lineCases);immune.SetValue(p,false);p.shimmerWet=false;
                if(p.mount.Active)p.mount.Dismount(p);p.width=20;p.height=42;p.position=new Vector2(801,916);p.velocity=Vector2.Zero;p.controlDown=false;p.wet=true;p.ignoreWater=p.merman=p.trident=false;
                int x=(int)(p.Center.X/16),y=(int)(p.Center.Y/16);
                for(int yy=y-2;yy<=y+1;yy++){Main.tile[x,yy].active(false);Main.tile[x,yy].liquid=0;}
                Main.tile[x,y+1].liquid=255;p.velocity.Y=-1;
                var ascent=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});Require(!ascent.FloatingNow,"B wet ascending player above actual water line retains ordinary entry.");cases++;
                p.velocity.Y=8;var surface=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});Require(surface.FloatingNow,"B real water line action enables explicit finite player premise.");
                NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)!=null && (cache.Read(0).Assumptions&PredictionAssumption.CurrentPlayerObservation)!=0,"B current surface floating reaches actual Source/Cache.");cases++;
                p.controlDown=true;Require(!((PredictionPlayerMotion)read.Invoke(null,new object[]{p})).FloatingNow,"B Down exits real floating qualification.");cases++;
                p.controlDown=false;p.velocity=Vector2.Zero;p.position=new Vector2(801,880);p.wet=false;
                // Ordinary source initially, then the modeled player enters
                // the surface. The published whole trajectory must carry the
                // bounded premise flag, without retaining an ordinary prefix.
                // This is a vertical first-contact scenario. The native low
                // altitude gravity is .1 here; inherited Right input would
                // leave the one-column pool before contact under that gravity.
                p.controlLeft=p.controlRight=false;
                NativeCombatObservationChecks.Fresh(context,host);var entering=cache.Read(0);
                Console.WriteLine("B FLOAT future-entry count="+(entering?.Count??0)+" stop="+entering?.Stop+" assumptions="+entering?.Assumptions+" player="+p.position+" gravity="+p.gravity+" wet="+p.wet+" sampled="+((PredictionPlayerMotion)read.Invoke(null,new object[]{p})).Gravity);
                Require(entering!=null && entering.Count==121 && (entering.Assumptions&PredictionAssumption.CurrentPlayerObservation)!=0,"B future first water contact restarts the whole player premise once.");cases++;
                p.position=new Vector2(801,916);var saved=Main.tile[x,y-2];Main.tile[x,y-2]=null;
                Require(((PredictionPlayerMotion)read.Invoke(null,new object[]{p})).FloatingNow==false && Main.tile[x,y-2]==null,"B dry capability does not read or allocate missing water-line cell.");
                p.wet=true;Require(((PredictionPlayerMotion)read.Invoke(null,new object[]{p})).FloatingNow && Main.tile[x,y-2]==null,"B unknown current player-only water line selects finite premise without creating real Tile.");Main.tile[x,y-2]=saved;cases++;
                terrain.Reset();PredictionTile cell;PredictionStop stop;Require(terrain.Tile(x,y+1,out cell,out stop) && terrain.Unchanged,"B terrain snapshot remains immutable after observation.");Main.tile[x,y+1].liquid=0;Require(!terrain.Unchanged,"B later water edit invalidates captured future geometry.");terrain.Reset();Require(terrain.Unchanged,"B next Prepare reset retires prior local geometry.");cases++;
            }
            finally{Main.dedServ=server;Main.netMode=network;if(p.mount.Active)p.mount.Dismount(p);p.wet=false;p.canFloatInWater=false;p.controlDown=false;p.velocity=Vector2.Zero;}
            Console.WriteLine("PASS B FLOAT qualification / actual line / first future entry / readonly geometry cases="+cases);
        }
    }
}
