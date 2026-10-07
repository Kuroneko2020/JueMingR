using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatHandConstraintChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var read=source.GetType().GetMethod("Read",Flags);var capture=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPositionObservation").GetMethod("Capture",Flags);var step=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcPositionMotion").GetMethod("Step",Flags);var ai=typeof(NPC).GetMethod("AI_078_MoonLordHands",Flags);
            var owner=Main.npc[1]=new NPC();owner.SetDefaults(398);owner.whoAmI=1;owner.active=true;owner.position=new Vector2(1200,1200);Main.LocalPlayer.position=new Vector2(1400,900);
            int mode=Main.netMode;Main.netMode=1;int cases=0;
            try
            {
                foreach(int side in new[]{-1,1})foreach(int phase in new[]{-2,0,2,3})foreach(float x in new[]{-10f,0f,370f,380f})foreach(float y in new[]{-220f,-210f,-60f,-50f})
                {
                    var n=Main.npc[2]=new NPC();n.SetDefaults(397);n.whoAmI=2;n.active=true;n.target=Main.myPlayer;n.ai[3]=1;n.ai[2]=side<0?0:1;n.ai[0]=phase;n.ai[1]=0;n.frameCounter=0;n.Center=owner.Center+new Vector2((side<0?-700:330)+x,y);n.velocity=new Vector2(side*3,2);
                    if(phase!=-2)
                    {
                        int row=side<0?0:1;float timer=0;bool found=false;
                        for(int i=0;i<5;i++){if(NPC.MoonLordAttacksArray[row,0,i]==phase){n.ai[1]=timer+2;found=true;break;}timer+=NPC.MoonLordAttacksArray[row,1,i];}
                        Require(found,"Original hand attack schedule contains phase "+phase);
                    }
                    var args=new object[]{n,read.Invoke(null,new object[]{n,Get(host,"Session")}),Get(host,"Session")};capture.Invoke(null,args);var sample=(NpcMotionState)args[1];Vector2 initial=n.position;ai.Invoke(n,null);Require(n.ai[0]==phase,"Original schedule phase precondition.");
                    // This checks only the parent-relative positional kernel.
                    // Feed its actual pre-translation velocity; future attack
                    // velocity remains the explicitly qualified near trend.
                    sample.Vx=n.velocity.X;sample.Vy=n.velocity.Y;var parameters=new object[]{sample,new[]{(NpcMotionState)read.Invoke(null,new object[]{owner,Get(host,"Session")})},1,20,default(PredictionEnvironment),false,PredictionStop.None};Require((bool)step.Invoke(null,parameters),"Hand parent identity and scalar range are available.");sample=(NpcMotionState)parameters[0];
                    Require(Vector2.Distance(n.position,new Vector2(sample.X,sample.Y))<.003f && n.velocity.X==sample.Vx && n.velocity.Y==sample.Vy,"Original hand clamp subtracts velocity instead of hard attaching to parent: side="+side+" phase="+phase+" x="+x+" y="+y);cases++;
                    if(x==-10 && y==-220)Console.WriteLine("HAND CONSTRAINT side="+side+" phase="+phase+" initial="+initial+" native="+n.position+" model="+new Vector2(sample.X,sample.Y)+" v="+n.velocity);
                }
            }
            finally{Main.netMode=mode;owner.active=false;Main.npc[2].active=false;}
            Console.WriteLine("PASS HAND CONSTRAINT original AI positional kernel cases="+cases+"; qualified observed attack velocity, not complete future Moon Lord AI.");
        }
    }
}
