using System;
using System.IO;
using System.Linq;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCombatImpactVisualChecks
    {
        // Oracle starts at the receive sample and native outlet, independently
        // of the marker's stored corners and projection implementation.
        internal static void AssertReceiveRectanglePixels(AttackContact contact,Color[] pixels,Matrix zoom,string name)
        {
            Require(contact!=null,"rectangle oracle requires a genuine contact: "+name);
            var sample=contact.Timeline[contact.ReceiveTick];var bounds=contact.ReceiveGeometry==AttackReceiveGeometry.Melee?sample.ReceiveBounds:sample.ProjectileReceiveBounds;
            Require(Same(bounds,contact.ReceiveBounds),"contact preserves its solved receive rectangle: "+name);
            Require(ReceiveRectanglePixelsCurrent(contact,pixels,zoom),"actual red pixels form a hollow receive-time rectangle with final zoom and clipped edges: "+name);
        }
        internal static bool ReceiveRectanglePixelsCurrent(AttackContact contact,Color[] pixels,Matrix zoom)
        {
            if(contact==null || pixels==null || contact.ReceiveTick<0 || contact.ReceiveTick>=contact.Timeline.Count)return false;
            var sample=contact.Timeline[contact.ReceiveTick];var bounds=contact.ReceiveGeometry==AttackReceiveGeometry.Melee?sample.ReceiveBounds:sample.ProjectileReceiveBounds;
            if(!Same(bounds,contact.ReceiveBounds))return false;
            var corners=new[]{new Vector2(bounds.X,bounds.Y),new Vector2(bounds.X+bounds.Width,bounds.Y),new Vector2(bounds.X+bounds.Width,bounds.Y+bounds.Height),new Vector2(bounds.X,bounds.Y+bounds.Height)};
            for(int i=0;i<corners.Length;i++){var local=corners[i]-Main.screenPosition;if(Main.LocalPlayer.gravDir==-1)local.Y=Main.screenHeight-local.Y;corners[i]=Vector2.Transform(local,zoom);}
            float left=corners.Min(v=>v.X),right=corners.Max(v=>v.X),top=corners.Min(v=>v.Y),bottom=corners.Max(v=>v.Y);
            var ink=pixels.Select((color,index)=>new{color,index}).Where(v=>Red(v.color)).Select(v=>new Vector2(v.index%Main.screenWidth,v.index/Main.screenWidth)).ToArray();
            if(ink.Length<6 || !ink.All(v=>v.X>=left-3 && v.X<=right+3 && v.Y>=top-3 && v.Y<=bottom+3 && Math.Min(Math.Min(Math.Abs(v.X-left),Math.Abs(v.X-right)),Math.Min(Math.Abs(v.Y-top),Math.Abs(v.Y-bottom)))<=3))return false;
            if(Math.Abs(ink.Min(v=>v.X)-Math.Max(0,left))>3 || Math.Abs(ink.Max(v=>v.X)-Math.Min(Main.screenWidth-1,right))>3 || Math.Abs(ink.Min(v=>v.Y)-Math.Max(0,top))>3 || Math.Abs(ink.Max(v=>v.Y)-Math.Min(Main.screenHeight-1,bottom))>3)return false;
            int cx=(int)((left+right)/2),cy=(int)((top+bottom)/2);
            return cx<0 || cx>=Main.screenWidth || cy<0 || cy>=Main.screenHeight || right-left<=12 || bottom-top<=12 || !Red(pixels[cy*Main.screenWidth+cx]);
        }
        private static bool Same(MotionRect a,MotionRect b){return a.X==b.X && a.Y==b.Y && a.Width==b.Width && a.Height==b.Height;}
        // This leaf deliberately uses only A2's existing API. The old payload
        // must reach actual World.Draw and fail on dimensions, not a missing
        // ReceiveGeometry field or a fixture/bootstrap incompatibility.
        private static void CheckA2Rectangle(object context,ProbeGraphics graphics)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var world=Get(host,"World");var input=Get(context,"Input");
            Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));Main.screenWidth=960;Main.screenHeight=640;Main.UIScale=1;
            var player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);player.position=new Vector2(700,646);player.gravDir=1;player.inventory[54].SetDefaults(97);player.inventory[54].stack=999;
            var npc=Main.npc[2];npc.SetDefaults(3);npc.whoAmI=2;npc.active=true;npc.position=new Vector2(810,646);npc.velocity=npc.netOffset=Vector2.Zero;npc.aiStyle=-1;npc.noGravity=true;npc.life=npc.lifeMax=10000;Array.Clear(npc.immune,0,npc.immune.Length);
            Main.screenPosition=new Vector2(600,400);Main.GameViewMatrix.Zoom=Vector2.One;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));PrepareDisplayFixture(context,host,attack,combat,input,npc);
            var contact=(AttackContact)Get(Get(world,"Impact"),"contact");Require(contact!=null,"A2 rectangle negative control has a genuine prepared contact before Draw");
            var bounds=contact.Timeline[contact.ReceiveTick].ProjectileReceiveBounds;var pixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
            var ink=pixels.Select((color,index)=>new{color,index}).Where(v=>Red(v.color)).Select(v=>new Vector2(v.index%960,v.index/960)).ToArray();
            Require(ink.Length>0,"A2 rectangle negative control reaches actual red Draw");
            float width=ink.Max(v=>v.X)-ink.Min(v=>v.X),height=ink.Max(v=>v.Y)-ink.Min(v=>v.Y);
            Console.WriteLine("RECTANGLE negative-control actualRed="+width+"x"+height+" expectedReceive="+bounds.Width+"x"+bounds.Height+" receiveTick="+contact.ReceiveTick);
            Require(Math.Abs(width-bounds.Width)<=3 && Math.Abs(height-bounds.Height)<=3,"actual Draw must outline the solved receiver dimensions; old 8-screen-pixel display is rejected");
        }
        private static void CheckReceiveGeometry()
        {
            var identity=new NpcIdentity(1,new object(),2,7,414,414);
            var state=new NpcMotionState{Identity=identity,X=100.8f,Y=110.6f,Width=32,Height=46,NetOffsetX=3.5f,NetOffsetY=-2.5f,CanReceive=true,Active=true};
            var points=new NpcTrajectoryPoint[3];points[0]=new NpcTrajectoryPoint(0,state);state.X=220.8f;state.Y=230.6f;state.Width=52;state.Height=64;points[1]=new NpcTrajectoryPoint(1,state);state.X=500;state.Y=550;points[2]=new NpcTrajectoryPoint(2,state);
            var path=new NpcTrajectory(identity,10,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);
            var melee=AttackIntercept.MeleeContact(path,1,220,220,80,90,1234,4321);
            var projectile=AttackIntercept.ProjectileShapeContact(path,1,2,true,1234,4321,220,230,AttackConfidence.Conditional);
            Require(melee!=null && melee.Tick==2 && melee.ReceiveTick==1 && Same(melee.ReceiveBounds,points[1].ReceiveBounds),"melee freezes the earlier receive sample, not the next attack tick/current/aim point");
            Require(projectile!=null && projectile.ReceiveBounds.Width==melee.ReceiveBounds.Width+16 && projectile.ReceiveBounds.X==melee.ReceiveBounds.X-8 && Same(projectile.ReceiveBounds,points[1].ProjectileReceiveBounds),"414 projectile-only eight-world-pixel extension is preserved by its contact outlet");
            foreach(var contact in new[]{melee,projectile})foreach(var copy in new[]{AttackIntercept.WithAimInput(contact,-100,-200),AttackIntercept.WithLaunchDelay(contact,3),AttackIntercept.Rebind(contact,path.Republish(11,2))})
                Require(copy!=null && copy.ReceiveGeometry==contact.ReceiveGeometry && copy.ReceiveTick==contact.ReceiveTick && Same(copy.ReceiveBounds,contact.ReceiveBounds),"aim/delay/rebind copies preserve receiver kind and rectangle");
            Console.WriteLine("PASS impact Contact receive geometry, earlier melee phase,414 extension and copy propagation");
        }
        private static void CheckRectangleRepresentatives(object context,object host,object attack,object combat,object input,NPC npc,Player player,object world,ProbeGraphics graphics,string output)
        {
            player.gravDir=1;Main.GameViewMatrix.Zoom=Vector2.One;Main.screenPosition=new Vector2(600,400);
            foreach(int type in new[]{1,3,414})
            {
                npc.SetDefaults(type);npc.whoAmI=2;npc.active=true;npc.position=new Vector2(810,646);npc.velocity=Vector2.Zero;npc.netOffset=new Vector2(5.75f,-3.25f);npc.aiStyle=-1;npc.noGravity=true;npc.life=npc.lifeMax=10000;Array.Clear(npc.immune,0,npc.immune.Length);
                if(type==3){npc.width=72;npc.height=48;}
                PrepareDisplayFixture(context,host,attack,combat,input,npc);var contact=(AttackContact)(GetOptional(attack,"ExpectedImpact")??GetOptional(attack,"ExpectedPreview"));Require(contact!=null,"representative rectangle obtains a prepared contact");
                int steps=NativeCombatUsageChecks.ObservedNpcSteps;var pixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);AssertReceiveRectanglePixels(contact,pixels,Main.GameViewMatrix.ZoomMatrix,"representative NPC "+type);
                Require(contact.ReceiveBounds.Width==npc.width+(type==414?16:0) && contact.ReceiveBounds.Height==npc.height+(type==414?16:0),"ordinary/slime/different-size/special contact uses native receiver dimensions");
                graphics.Image(Path.Combine(output,"impact-rectangle-"+type+".png"),()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                // Move only the final camera so the centre lies outside, while
                // the right edge and portions of top/bottom remain visible.
                Main.screenPosition=new Vector2(contact.ReceiveBounds.X+contact.ReceiveBounds.Width*.75f,contact.ReceiveBounds.Y-200);
                var edge=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);AssertReceiveRectanglePixels(contact,edge,Main.GameViewMatrix.ZoomMatrix,"offscreen centre NPC "+type);Require((bool)Get(Get(world,"Impact"),"Visible"),"intersecting rectangle survives offscreen centre");
                Require(NativeCombatUsageChecks.ObservedNpcSteps==steps,"camera resize/projection/Draw never advances the prediction");
                Main.screenPosition=new Vector2(600,400);
            }
            // Restore the fixture used by the adjacent toggle/recovery checks.
            npc.SetDefaults(3);npc.whoAmI=2;npc.active=true;npc.position=new Vector2(810,646);npc.velocity=npc.netOffset=Vector2.Zero;npc.aiStyle=-1;npc.noGravity=true;npc.life=npc.lifeMax=10000;Array.Clear(npc.immune,0,npc.immune.Length);
        }
    }
}
