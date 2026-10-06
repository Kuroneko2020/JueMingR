using System;
using System.IO;
using System.Reflection;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // These checks read pixels from the actual world layer. Geometry oracles
    // elsewhere cannot detect lost dash gaps, an opaque fill, or Draw replay.
    internal static class NativeCombatShapeVisualChecks
    {
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            var host=Get(context,"CombatObservation");var layer=Get(host,"World");var geometry=Get(host,"Geometry");
            Main.screenWidth=960;Main.screenHeight=640;Main.screenPosition=Vector2.Zero;Main.UIScale=1;Main.GameViewMatrix.Zoom=Vector2.One;Main.LocalPlayer.gravDir=1;
            Main.hideUI=false;Main.mapFullscreen=false;Main.LocalPlayer.itemAnimation=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true));
            void Reset(){foreach(var n in Main.npc)n.active=false;foreach(var p in Main.projectile)p.active=false;Call(geometry,"Clear");NativeCombatObservationChecks.Fresh(context,host);}
            Projectile Shot(int type,Vector2 center){var p=Main.projectile[10];p.SetDefaults(type);p.whoAmI=10;p.active=true;p.owner=Main.myPlayer;p.friendly=true;p.damage=25;p.Center=center;return p;}
            Color[] Draw(){return graphics.Pixels(()=>Call(layer,"Draw"),Main.GameViewMatrix.ZoomMatrix);}
            void Image(string name){graphics.Image(Path.Combine(output,"shape-"+name+".png"),()=>Call(layer,"Draw"),Main.GameViewMatrix.ZoomMatrix);}

            Reset();var beam=Shot(632,new Vector2(-300,250));beam.velocity=Vector2.UnitX;beam.localAI[1]=1500;beam.scale=1;beam.Damage();Call(layer,"Prepare");var pixels=Draw();
            Require(Count(pixels,100,220,700,60)>700 && Count(pixels,100,245,700,8)==0,"offscreen beam crosses viewport as two outlines without filled interior");Image("offscreen-beam");
            Main.screenPosition=new Vector2(60,35);Main.LocalPlayer.gravDir=-1;Call(layer,"Prepare");pixels=Draw();Require(Count(pixels,100,395,650,60)>600,"screen pan and inverted gravity preserve offscreen-origin beam");Image("offscreen-inverted");Main.screenPosition=Vector2.Zero;Main.LocalPlayer.gravDir=1;

            Reset();var circle=Shot(973,new Vector2(300,220));circle.scale=.4f;circle.alpha=0;circle.Damage();Call(layer,"Prepare");pixels=Draw();int marks=0,gaps=0;
            for(int i=0;i<72;i++){double a=i*Math.PI*2/72;int x=(int)Math.Round(300+40*Math.Cos(a)),y=(int)Math.Round(220+40*Math.Sin(a));if(Count(pixels,x-1,y-1,3,3)>0)marks++;else gaps++;}
            Require(marks>12 && gaps>8,"conditional circle has visible dashes and actual gaps: marks="+marks+" gaps="+gaps);Require(Count(pixels,267,187,3,3)==0,"circle does not paint bounding-square corners");Image("conditional-circle");

            Reset();var player=Main.LocalPlayer;var oldPosition=player.position;var oldVelocity=player.velocity;player.position=new Vector2(300,220);player.velocity=new Vector2(12,0);player.dashType=player.dash=2;player.dashDelay=-1;player.eocDash=10;player.eocHit=-1;player.itemAnimation=0;
            player.DashMovement();Call(layer,"Prepare");pixels=Draw();Require(Count(pixels,298,214,40,60)>50,"body attack draws without held-item animation");Image("shield-body");NativeQuickItemChecks.BeginWorldStep();player.eocHit=1;player.DashMovement();Call(layer,"Prepare");pixels=Draw();Require(Count(pixels,298,214,40,60)==0,"body window retires on next update without replay");player.position=oldPosition;player.velocity=oldVelocity;player.dash=player.dashType=player.eocDash=player.dashDelay=0;player.eocHit=-1;

            Reset();var points=Shot(1117,new Vector2(240,220));points.localAI[0]=1;points.customHitbox=new Terraria.DataStructures.MultiPointHitbox(new Point(16,16),new[]{new Vector2(240,220),new Vector2(500,320)});points.Damage();Call(layer,"Prepare");pixels=Draw();
            Require(Count(pixels,230,210,21,21)>30 && Count(pixels,490,310,21,21)>30 && Count(pixels,340,240,60,50)==0,"discrete lightning point boxes remain separated by a non-damaging gap");Image("discrete-points");

            Reset();var pulse=Shot(644,new Vector2(400,250));pulse.localAI[0]=1;pulse.localAI[1]=29;pulse.AI();Require((int)Get(geometry,"EventCount")==1,"natural crystal pulse creates event before drawing");Call(layer,"Prepare");Call(layer,"Prepare");
            pixels=Draw();Require(Count(pixels,368,218,65,65)>150,"two prepares preserve event until actual first draw");pixels=Draw();Require(Count(pixels,368,218,65,65)==0,"second draw never replays event");Call(layer,"Prepare");pixels=Draw();Require(Count(pixels,368,218,65,65)==0,"reprepare after presentation does not replay event");

            Reset();Call(layer,"Prepare");int before=(int)Get(layer,"StrokeCount");var line=layer.GetType().GetMethod("Line",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(Vector2),typeof(Vector2),typeof(Color),typeof(bool),typeof(float)},null);
            line.Invoke(layer,new object[]{new Vector2(-10,2),new Vector2(2,-10),Color.White,true,1.5f});line.Invoke(layer,new object[]{new Vector2(300,200),new Vector2(300,200),Color.White,true,1.5f});Require((int)Get(layer,"StrokeCount")==before,"zero-length and clip-corner segments produce no invalid stroke");

            Reset();for(int i=0;i<Main.maxProjectiles;i++){var p=Main.projectile[i];p.SetDefaults(1);p.whoAmI=i;p.active=true;p.owner=Main.myPlayer;p.friendly=true;p.damage=1;p.penetrate=p.maxPenetrate=-1;p.position=new Vector2(20+i%50*18,30+i/50*20);p.Damage();}
            for(int i=0;i<Main.maxNPCs;i++){var n=Main.npc[i];n.SetDefaults(1);n.whoAmI=i;n.active=true;n.damage=0;n.life=n.lifeMax=1000000;n.position=new Vector2(20+i%40*22,30+i/40*60);}
            NativeCombatObservationChecks.Fresh(context,host);
            // Fresh advances the sample tick; resample each natural attack in
            // that same update, as the real Damage hooks do before world Draw.
            foreach(var p in Main.projectile)if(p.active)p.Damage();Call(layer,"Prepare");NativeCombatPresentationChecks.Project(layer);Require((int)Get(layer,"StrokeCount")==4800 && !(bool)Get(layer,"limited"),"1000 ordinary projectiles plus 200 receivers fit actual world stroke capacity: "+Get(layer,"StrokeCount"));
            int samples=(int)Get(geometry,"ProjectileSamples");for(int i=0;i<3;i++){Call(layer,"Prepare");Draw();}Require((int)Get(geometry,"ProjectileSamples")==samples,"repeated real prepare/draw never resamples native attacks");Image("dense-world");
            Reset();NativeCombatObservationChecks.Save(host,new ObservationOptions());Call(layer,"Prepare");
            Console.WriteLine("PASS actual world pixels: offscreen/inverted beam, circle dash gaps, discrete points, event presentation, zero-length clipping and dense 1200-object capacity.");
        }
        private static int Count(Color[] pixels,int x,int y,int width,int height)
        {int result=0;for(int j=Math.Max(0,y);j<Math.Min(640,y+height);j++)for(int i=Math.Max(0,x);i<Math.Min(960,x+width);i++)if(pixels[j*960+i].A!=0)result++;return result;}
    }
}
