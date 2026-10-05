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
                foreach(int type in new[]{36,128,129,130,131})foreach(int parentPhase in new[]{0,1})foreach(int side in new[]{-1,1})foreach(int phase in new[]{0,1,2,3,4,5,99})
                {
                    var owner=Main.npc[1];owner.SetDefaults(type==36?35:127);owner.whoAmI=1;owner.active=true;owner.position=new Vector2(800,800);owner.velocity=new Vector2(side*2,1);owner.ai[1]=parentPhase;
                    var child=Main.npc[2];child.SetDefaults(type);child.whoAmI=2;child.active=true;child.ai[0]=1;child.ai[1]=1;child.ai[2]=phase;child.ai[3]=20;child.target=Main.myPlayer;child.position=owner.Center+new Vector2(side*350,side*350);child.velocity=new Vector2(side*2,side);
                    var args=new object[]{child,read.Invoke(null,new object[]{child,Get(host,"Session")}),Get(host,"Session")};capture.Invoke(null,args);var before=(NpcMotionState)args[1];
                    child.AI();var group=new[]{(NpcMotionState)read.Invoke(null,new object[]{owner,Get(host,"Session")})};var environment=new PredictionEnvironment{PlayerIndex=Main.myPlayer,PlayerX=Main.LocalPlayer.Center.X,PlayerY=Main.LocalPlayer.Center.Y,PlayerWidth=Main.LocalPlayer.width,PlayerHeight=Main.LocalPlayer.height,Expert=Main.expertMode,Multiplayer=true};var parameters=new object[]{before,group,1,1,environment,PredictionStop.None};Require((bool)step.Invoke(null,parameters),"Observed current parent permits phase velocity: "+type);var after=(NpcMotionState)parameters[0];
                    Console.WriteLine("PARENT phase type="+type+" child="+phase+" parent="+parentPhase+" side="+side+" nativeV="+child.velocity+" modelV="+new Vector2(after.Vx,after.Vy));
                    Require(Vector2.Distance(child.velocity,new Vector2(after.Vx,after.Vy))<.001f && child.ai[2]==after.A2 && child.ai[3]==after.A3,"Native current parent position/phase controls child speed independently of generic trend: "+type);cases++;
                }
            }
            finally{Main.netMode=savedMode;}
            Console.WriteLine("PASS PARENT MOTION original AI current relative position/phase cases="+cases);
        }
    }
}
