using System;
using System.IO;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Fixed immutable motion is deliberate: this checks the real display
    // consumer and GPU pixels independently of weapon/NPC solving quality.
    internal static class NativeCombatPathContinuityChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            Directory.CreateDirectory(output);
            var host=Get(context,"CombatObservation");var world=Get(host,"World");var source=Get(host,"Prediction");
            var cache=(NpcPredictionCache)Get(source,"Cache");var input=Get(context,"Input");
            NativeCombatAimChecks.Initialize();
            var player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);
            player.position=new Vector2(140,300);player.gravDir=1;player.itemAnimation=player.itemTime=0;
            foreach(var row in Main.npc)row.active=false;
            var npc=Main.npc[2];npc.SetDefaults(3);npc.whoAmI=2;npc.active=true;npc.dontTakeDamage=npc.immortal=npc.friendly=false;
            npc.aiStyle=-1;npc.noGravity=true;npc.noTileCollide=false;npc.position=new Vector2(180,200);npc.velocity=new Vector2(4,0);npc.life=npc.lifeMax=100000;
            Main.screenWidth=960;Main.screenHeight=640;Main.screenPosition=Vector2.Zero;Main.UIScale=1;Main.GameViewMatrix.Zoom=Vector2.One;Main.hideUI=Main.mapFullscreen=false;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),false);Call(host,"SampleMouse");
            NativeCombatObservationChecks.Fresh(context,host);
            Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));
            var identity=(NpcIdentity)Get(Get(host,"Selection"),"Target");Require(ReferenceEquals(identity.Token,npc),"real selection supplies the displayed identity");
            // The exact starting phase is fixed before constructing either
            // publication, so the marker oracle is not inferred from output.
            while(Main.GameUpdateCount%30!=3)NativeQuickItemChecks.BeginWorldStep();
            long firstTick=Main.GameUpdateCount;var firstPosition=npc.position;
            NpcTrajectory PathAt(long tick,int count,PredictionStop stop=PredictionStop.None,int changed=-1,bool segment=false)
            {
                var points=new NpcTrajectoryPoint[count];
                for(int i=0;i<count;i++)
                {
                    var position=firstPosition+new Vector2((tick-firstTick+i)*4,0);
                    if(i==changed)position.Y+=20;
                    if(segment && i>=10)position.X+=200;
                    points[i]=new NpcTrajectoryPoint(i,new NpcMotionState{Identity=identity,X=position.X,Y=position.Y,Width=npc.width,Height=npc.height,Vx=4,CanReceive=true,NewSegment=segment && i==10});
                }
                return new NpcTrajectory(identity,tick,tick,PredictionAssumption.NoNewHits,stop,points,count,budgetPaused:count<100);
            }
            void Capture(NpcTrajectory path)
            {
                cache.Publish(path);
                host.GetType().GetMethod("CaptureDisplay",Flags,null,new[]{typeof(long)},null).Invoke(host,new object[]{path.SampleTick});
                Require((bool)Call(host,"ValidateDisplay",(long)Main.GameUpdateCount),"test publication passes actual display life, input, terrain, pose and age gates");
                Call(world,"Prepare");
            }
            void Advance(int updates)
            {for(int i=0;i<updates;i++){NativeQuickItemChecks.BeginWorldStep();npc.position+=npc.velocity;}}
            Color[] Draw(){return graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);}
            void Image(string name){graphics.Image(System.IO.Path.Combine(output,name+".png"),()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);}
            Set(host,"displayPath",null);Call(Get(source,"Terrain"),"Reset");
            var longPath=PathAt(firstTick,121);Capture(longPath);
            using(var work=NativeCombatUsageChecks.ObserveDrawSteps())
            {
                int steps=NativeCombatUsageChecks.ObservedNpcSteps;
                var pixels=Draw();int markerX=(int)(firstPosition.X+npc.width*.5f+27*4),markerY=(int)(firstPosition.Y+npc.height*.5f);
                bool absoluteMarker=Count(pixels,markerX-1,markerY-4,3,2)>0;
                Image("path-fixed-initial");
                for(int draw=0;draw<4;draw++)
                {
                    var repeated=Draw();absoluteMarker&=Count(repeated,markerX-1,markerY-4,3,2)>0;
                }
                // A real lease can age at most four updates; refresh the same
                // future at T+4 and compare the identical absolute milestone.
                Advance(4);var refreshed=PathAt(Main.GameUpdateCount,121);Capture(refreshed);pixels=Draw();
                absoluteMarker&=Count(pixels,markerX-1,markerY-4,3,2)>0;
                Image("path-fixed-refreshed");
                Console.WriteLine("PATH WINDOW firstGuc="+firstTick+" refreshedGuc="+Main.GameUpdateCount+" drawCount=8 absoluteMarkerTick="+(firstTick+27)+" marker="+markerX+","+markerY+" count="+refreshed.Count);

                var shortPath=PathAt(Main.GameUpdateCount,33);Capture(shortPath);
                bool retainedLong=ReferenceEquals(Get(host,"DisplayPath"),refreshed);
                Console.WriteLine("PATH REGRESSIONS absoluteMarker="+absoluteMarker+" equalPrefixRetainsLong="+retainedLong);
                var changed=PathAt(Main.GameUpdateCount,33,changed:10);Capture(changed);
                Require(ReferenceEquals(Get(host,"DisplayPath"),changed),"changed overlapping future replaces the old long display");
                Capture(refreshed);var terminal=PathAt(Main.GameUpdateCount,25,PredictionStop.PhaseBoundary);Capture(terminal);
                Require(ReferenceEquals(Get(host,"DisplayPath"),terminal),"real new terminal replaces old long tail rather than retaining invented future");
                Capture(refreshed);npc.life--;Capture(shortPath);
                Require(ReferenceEquals(Get(host,"DisplayPath"),shortPath),"real life change prevents old-tail retention and accepts the newly captured prefix");
                Capture(refreshed);Advance(5);var expiredReplacement=PathAt(Main.GameUpdateCount,33);Capture(expiredReplacement);
                Require(ReferenceEquals(Get(host,"DisplayPath"),expiredReplacement),"expired long display never earns a lease by its length");

                var split=PathAt(Main.GameUpdateCount,70,segment:true);Capture(split);pixels=Draw();
                int gapX=(int)(npc.position.X+npc.width*.5f+10*4+35);
                Require(Count(pixels,gapX,markerY-2,100,5)==0,"real NewSegment leaves a pixel gap instead of connecting the teleport");Image("path-real-segment");
                npc.active=false;pixels=Draw();Require(Count(pixels,150,180,700,80)==0,"live target retirement immediately withdraws the retained path at Draw");
                Require(NativeCombatUsageChecks.ObservedNpcSteps==steps,"all real Draws and publication-only checks performed zero future simulation steps");
                Require(absoluteMarker && retainedLong,"absolute future markers must keep their phase across repeated Draw and rolling publications; an equal fresh short prefix must retain an independently valid longer display");
            }
            NativeCombatObservationChecks.Save(host,new ObservationOptions());Call(world,"Prepare");Require((int)Get(world,"StrokeCount")==0,"OFF clears prepared strokes");
            Console.WriteLine("PASS PATH CONTINUITY actual GPU fixed-path/repeated-Draw, absolute rolling marker, retained valid tail; changed future/terminal/life/age/segment/retirement controls. No along-path arrow implementation found; no animation claim.");
        }
        private static int Count(Color[] pixels,int x,int y,int width,int height)
        {int result=0;for(int j=Math.Max(0,y);j<Math.Min(640,y+height);j++)for(int i=Math.Max(0,x);i<Math.Min(960,x+width);i++)if(pixels[j*960+i].A!=0)result++;return result;}
    }
}
