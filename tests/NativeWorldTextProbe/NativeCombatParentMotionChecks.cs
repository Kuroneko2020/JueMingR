using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatParentMotionChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var read=source.GetType().GetMethod("Read",Flags);var capture=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPositionObservation").GetMethod("Capture",Flags);
            var step=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcPositionMotion").GetMethod("Step",Flags);
            for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};Main.LocalPlayer.position=new Vector2(900,900);
            int savedMode=Main.netMode;Main.netMode=1;int cases=0;
            try
            {
                foreach(int type in new[]{36,128,129,130,131})foreach(int parentPhase in new[]{0,1,3})foreach(int side in new[]{-1,1})foreach(int phase in new[]{0,1,2,3,4,5,99})
                {
                    var owner=Main.npc[1];owner.SetDefaults(type==36?35:127);owner.whoAmI=1;owner.active=true;owner.position=new Vector2(800,800);owner.velocity=new Vector2(side*2,1);owner.ai[1]=parentPhase;
                    var child=Main.npc[2];child.SetDefaults(type);child.whoAmI=2;child.active=true;child.ai[0]=1;child.ai[1]=1;child.ai[2]=phase;child.ai[3]=20;child.target=Main.myPlayer;child.position=owner.Center+new Vector2(side*350,side*350);child.velocity=new Vector2(side*2,side);
                    var args=new object[]{child,read.Invoke(null,new object[]{child,Get(host,"Session")}),Get(host,"Session")};capture.Invoke(null,args);var before=(NpcMotionState)args[1];
                    child.AI();var group=new[]{(NpcMotionState)read.Invoke(null,new object[]{owner,Get(host,"Session")})};var environment=new PredictionEnvironment{PlayerIndex=Main.myPlayer,PlayerX=Main.LocalPlayer.Center.X,PlayerY=Main.LocalPlayer.Center.Y,PlayerWidth=Main.LocalPlayer.width,PlayerHeight=Main.LocalPlayer.height,Expert=Main.expertMode,Multiplayer=true};var parameters=new object[]{before,group,1,1,environment,PredictionStop.None};Require((bool)step.Invoke(null,parameters),"Observed current parent permits phase velocity: "+type);var after=(NpcMotionState)parameters[0];
                    Console.WriteLine("PARENT phase type="+type+" child="+phase+" parent="+parentPhase+" side="+side+" nativeV="+child.velocity+" modelV="+new Vector2(after.Vx,after.Vy));
                    Require(Vector2.Distance(child.velocity,new Vector2(after.Vx,after.Vy))<.001f && child.ai[2]==after.A2 && child.ai[3]==after.A3 && child.timeLeft==after.TimeLeft,"Native current parent position/phase controls child speed and phase lifetime independently of generic trend: "+type);cases++;
                }
            }
            finally{Main.netMode=savedMode;}
            int difficulty=Main.GameMode;Main.netMode=1;
            try
            {
                foreach(int mode in new[]{0,1})foreach(int red in new[]{0,1})foreach(int phase in new[]{0,1,2,4,5})foreach(int side in new[]{-1,1})
                {
                    Main.GameMode=mode;var owner=Main.npc[1];owner.SetDefaults(35);owner.whoAmI=1;owner.active=true;owner.position=new Vector2(800,800);owner.ai[1]=0;owner.ai[3]=red;
                    var child=Main.npc[2];child.SetDefaults(36);child.active=true;child.whoAmI=2;child.ai[0]=1;child.ai[1]=1;child.ai[2]=phase;child.ai[3]=20;child.target=Main.myPlayer;child.position=owner.Center+new Vector2(side*350,side*350);child.velocity=new Vector2(side*2,side);
                    var args=new object[]{child,read.Invoke(null,new object[]{child,Get(host,"Session")}),Get(host,"Session")};capture.Invoke(null,args);var before=(NpcMotionState)args[1];child.AI();
                    var environment=new PredictionEnvironment{PlayerIndex=Main.myPlayer,PlayerX=Main.LocalPlayer.Center.X,PlayerY=Main.LocalPlayer.Center.Y,PlayerWidth=Main.LocalPlayer.width,PlayerHeight=Main.LocalPlayer.height,Expert=Main.expertMode,Multiplayer=true};var parameters=new object[]{before,new[]{(NpcMotionState)read.Invoke(null,new object[]{owner,Get(host,"Session")})},1,1,environment,PredictionStop.None};Require((bool)step.Invoke(null,parameters),"Red/expert parent scalar phase.");var after=(NpcMotionState)parameters[0];
                    Require(Vector2.Distance(child.velocity,new Vector2(after.Vx,after.Vy))<.001f && child.ai[2]==after.A2 && child.ai[3]==after.A3,"Original normal/expert and red-head current clock/relative constraints: mode="+mode+" red="+red+" phase="+phase);cases++;
                }
            }
            finally{Main.GameMode=difficulty;Main.netMode=savedMode;}
            var needs=typeof(NpcMotion).GetMethod("NeedsPlayerMotion",Flags);
            var home=new NpcMotionState{Style=12,PositionRelation=6,A2=0,A3=170,PositionParameter=0,L3=0};
            var ownerState=new NpcMotionState{Identity=home.PositionOwner,Y=800};
            Require(!(bool)needs.Invoke(null,new object[]{home,new PredictionEnvironment{Expert=false},120,new[]{ownerState},1}) && !(bool)needs.Invoke(null,new object[]{home,new PredictionEnvironment{Expert=true},120,new[]{ownerState},1}),"A clock crossing alone is not the actual Aim consumer; full Rolling boundary controls cover the subsequent rising phase.");
            var second=Main.player[1];second.active=true;second.dead=false;second.position=new Vector2(900,950);second.tankPet=-1;Main.LocalPlayer.position=new Vector2(1400,950);
            var boss=Main.npc[1];boss.SetDefaults(127);boss.whoAmI=1;boss.active=true;boss.ai[1]=0;boss.position=new Vector2(800,800);
            var member=Main.npc[2];member.SetDefaults(131);member.whoAmI=2;member.active=true;member.target=Main.myPlayer;member.position=new Vector2(1000,950);member.ai[0]=1;member.ai[1]=1;member.ai[2]=0;member.ai[3]=799;
            var observation=new object[]{member,read.Invoke(null,new object[]{member,Get(host,"Session")}),Get(host,"Session")};capture.Invoke(null,observation);var sampled=(NpcMotionState)observation[1];
            var targetCapture=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation").GetMethod("Capture",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");var targetArgs=new object[]{member,sampled,terrain};terrain.Reset();targetCapture.Invoke(null,targetArgs);sampled=(NpcMotionState)targetArgs[1];
            savedMode=Main.netMode;Main.netMode=1;
            try
            {
                for(int future=1;future<=2;future++)
                {
                    member.AI();var owner=(NpcMotionState)read.Invoke(null,new object[]{boss,Get(host,"Session")});var env=new PredictionEnvironment{PlayerIndex=future==1?Main.myPlayer:1,PlayerX=future==1?Main.LocalPlayer.Center.X:second.Center.X,PlayerY=future==1?Main.LocalPlayer.Center.Y:second.Center.Y,PlayerWidth=second.width,PlayerHeight=second.height,Multiplayer=true};
                    var args=new object[]{sampled,new[]{owner},1,future,env,PredictionStop.None};Require((bool)step.Invoke(null,args),"Parent Style36 current home boundary.");sampled=(NpcMotionState)args[0];
                    Require(member.target==1 && sampled.Target==1 && Vector2.Distance(member.velocity,new Vector2(sampled.Vx,sampled.Vy))<.001f,"Style36 home selects B on clock799 before next phase consumes B.");
                    Console.WriteLine("PARENT STYLE36 boundary future="+future+" nativeTarget="+member.target+" modelTarget="+sampled.Target+" nativeV="+member.velocity+" modelV="+new Vector2(sampled.Vx,sampled.Vy));
                }
            }
            finally{Main.netMode=savedMode;second.active=false;}
            Console.WriteLine("PASS PARENT MOTION original AI current relative position/phase cases="+cases);
        }
    }
}
