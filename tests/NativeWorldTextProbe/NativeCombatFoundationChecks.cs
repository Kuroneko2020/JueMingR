using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
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
            if(Environment.GetEnvironmentVariable("JUEMINGR_FOUNDATION_ONLY")!="source"){Gravity(source);Horizontal(readPlayer);}
            DefaultSource(context,host,source);
            SlimeSource(context,host,source);
            FloatingSource(context,host,source,readPlayer);
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
            other.active=false;other.dead=false;n.SetDefaults(2);n.whoAmI=2;n.active=true;n.target=p.whoAmI;n.position=new Vector2(500,600);
            Console.WriteLine("PASS B SLIME actual default Source first-action prerequisite cases="+cases);
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
                NativeCombatObservationChecks.Fresh(context,host);var entering=cache.Read(0);
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
