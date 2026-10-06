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
            Gravity(source);Horizontal(readPlayer);DefaultSource(context,host,source);
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
                Require(((path.Assumptions&PredictionAssumption.CurrentPlayerObservation)!=0)==(mechanism!=0),"B entering/exiting explicit complex player premise recovers on the next real sample.");cases++;
            }
            p.canFloatInWater=false;p.grappling[0]=4;p.grapCount=1;Main.projectile[4].active=true;Main.projectile[4].owner=p.whoAmI+1;
            NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)==null && (bool)Get(Get(host,"Selection"),"HasTarget") && (bool)Get(host,"Marker"),"B bad grapple owner rejects future while independent selected marker remains.");cases++;
            p.grappling[0]=-1;p.grapCount=0;p.velocity=new Vector2(float.NaN,0);NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)==null,"B real source invalid player numeric remains rejected.");cases++;
            p.velocity=Vector2.Zero;NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)!=null,"B restoring valid ordinary numeric state recovers automatically.");cases++;
            n0.dontTakeDamage=true;NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)==null && !(bool)Get(Get(host,"Selection"),"HasTarget"),"B invalid receiver identity never becomes a conditional target.");cases++;
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatObservationChecks.Fresh(context,host);Require(cache.Required==0 && cache.Read(0)==null,"B OFF retires the shared demand.");cases++;
            Console.WriteLine("PASS B DEFAULT SOURCE original Host selection + ReadPlayer + rolling + actual Cache cases="+cases);
        }
    }
}
