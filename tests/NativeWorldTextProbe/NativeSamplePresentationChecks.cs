using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using JueMingR.Platform.Guidance;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;
namespace NativeWorldTextProbe
{
    internal static class NativeSamplePresentationChecks
    {
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var world=Get(host,"World");var npcs=Get(context,"nativeNpcs");
            foreach(var npc in Main.npc)npc.active=false;
            Main.LocalPlayer.position=new Vector2(640,640);Main.screenPosition=new Vector2(300,300);Main.screenWidth=960;Main.screenHeight=640;Main.GameViewMatrix.Zoom=Vector2.One;
            var n=Main.npc[2];n.SetDefaults(34);n.whoAmI=2;n.active=true;n.dontTakeDamage=false;n.immortal=false;n.friendly=false;n.aiStyle=0;n.noGravity=true;n.noTileCollide=false;n.position=new Vector2(720,650);n.velocity=new Vector2(4,0);n.target=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,collision:true,marker:true));NativeCombatObservationChecks.Fresh(context,host);
            var first=cache.Read(0);Require(first!=null && first.Count>1,"Default Host/Source publishes initial route.");
            n.position+=n.velocity;n.velocity=new Vector2(3.9992f,.079995f);NativeCombatObservationChecks.Fresh(context,host);var curved=cache.Read(0);Require(curved!=null,"Second real sample.");
            for(int i=0;i<4;i++)Call(host,"Update",Main.GameUpdateCount);
            Require(ReferenceEquals(cache.Read(0),curved),"Outer reentry retains the identical immutable forecast and turn history through Source.");
            var endpoint=curved[curved.Count-1].Bounds;
            var wall=Main.tile[48,41];var saved=new Tile();saved.CopyFrom(wall);Main.tileSolid[TileID.Stone]=true;wall.active(true);wall.type=TileID.Stone;wall.slope(0);wall.halfBrick(false);
            Call(host,"Update",Main.GameUpdateCount);var changed=cache.Read(0);
            Require(changed!=null && !ReferenceEquals(changed,curved),"Same tick cell edit invalidates Source before Reset erases its evidence.");wall.CopyFrom(saved);
            n.position.X+=20;Call(host,"Update",Main.GameUpdateCount);Require(cache.Read(0)[0].Bounds.X==n.position.X,"Same tick network position correction is immediately captured.");
            Call(npcs,"BeginCompleted",(long)Main.GameUpdateCount);object[] readArgs={2,NpcDemand.Direction,null};var read=npcs.GetType().GetMethod("TryRead");read.Invoke(npcs,readArgs);var f=(GuidanceNpc)readArgs[2];
            int reads=(int)Get(npcs,"DirectionReads");Call(npcs,"BeginCompleted",(long)Main.GameUpdateCount);read.Invoke(npcs,readArgs);Require((int)Get(npcs,"DirectionReads")==reads,"Completed phase repeats do not re-expand shared direction facts.");n.position.X+=3;read.Invoke(npcs,readArgs);Require(((GuidanceNpc)readArgs[2]).X==n.Center.X,"Same tick shared observation correction updates demanded fact.");
            Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));Call(world,"Prepare");Main.screenPosition=new Vector2(500,400);Main.GameViewMatrix.Zoom=new Vector2(1.4f);NativeCombatPresentationChecks.Project(world);
            var marker=Get(world,"Marker");Require((bool)Get(marker,"Visible") && (int)Get(world,"StrokeCount")>0,"Current final camera projects marker and retained forecast.");
            var zoom=Main.GameViewMatrix.ZoomMatrix;var pieces=(Array)Get(marker,"Pieces");var mean=Vector2.Zero;foreach(var piece in pieces)mean+=Vector2.Transform((Vector2)Get(piece,"Position"),zoom);mean/=6;
            var center=new Vector2(n.Hitbox.Center.X,n.Hitbox.Center.Y);
            var project=world.GetType().Assembly.GetType("JueMingR.TerrariaHost.Guidance.GuidanceWorldLayer").GetMethod("Project",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var expected=(Vector2)project.Invoke(null,new object[]{center,zoom});Require(Vector2.Distance(mean,expected)<2,"Marker consumes final camera after Update.");
            Call(source,"ObserveNpcQueryUpdate",2,false);NativeCombatPresentationChecks.Project(world);Require((int)Get(world,"StrokeCount")==(int)Get(world,"eventEnd") && GetOptional(world,"pathText")==null,"Cache retirement without another Prepare suppresses retained path immediately.");
            n.dontTakeDamage=true;n.position.X+=40;Call(marker,"Capture");n.dontTakeDamage=false;NativeCombatPresentationChecks.Project(world);Require(!(bool)Get(marker,"Visible"),"An unreceivable capture cannot reuse the old receive box when permission returns before Draw.");Call(marker,"Capture");NativeCombatPresentationChecks.Project(world);Require((bool)Get(marker,"Visible"),"A new valid capture restores the current receive box.");
            int hook=Main.LocalPlayer.grappling[0],hookCount=Main.LocalPlayer.grapCount;Main.LocalPlayer.grappling[0]=Main.maxProjectiles;Main.LocalPlayer.grapCount=1;n.aiStyle=10;n.noTileCollide=true;Call(host,"Update",Main.GameUpdateCount);Call(world,"Prepare");var failureText=GetOptional(world,"pathText");Require(failureText!=null && cache.Read(0)==null,"A required invalid player premise prepares its legitimate unavailable message.");NativeCombatPresentationChecks.Project(world);Require(Equals(failureText,GetOptional(world,"pathText")),"A missing path retains its current failure message, unlike a revoked former path.");Main.LocalPlayer.grappling[0]=hook;Main.LocalPlayer.grapCount=hookCount;n.aiStyle=0;
            var geometry=Get(host,"Geometry");Call(geometry,"Clear");var sample=Call(geometry,"Event");Call(sample,"Rectangle",new Rectangle(750,650,30,30),0,false);
            for(int i=0;i<6;i++){NativeQuickItemChecks.BeginWorldStep();Call(geometry,"PrepareEvents");}
            Require((int)Get(geometry,"EventCount")==1 && !(bool)Get(sample,"Presented"),"Six genuine updates without Draw preserve unpresented event.");
            Call(world,"Prepare");NativeCombatPresentationChecks.Project(world);int eventStrokes=(int)Get(world,"eventEnd")-(int)Get(world,"eventStart");Require(eventStrokes>0,"Deferred event has visible current-camera strokes.");Call(geometry,"PresentedEvents",(long)Get(world,"presentation"));NativeCombatPresentationChecks.Project(world);Require((int)Get(world,"eventEnd")== (int)Get(world,"eventStart"),"A second presentation cannot replay a consumed event.");
            Call(host,"OnSessionEnded");Require((int)Get(geometry,"EventCount")==0,"World exit retires bounded pending events.");
            Console.WriteLine("PASS SAMPLE-PRESENTATION default Source reentry, same-tick terrain/correction, shared phase, final camera and six-update event retention/one consume. CPU command evidence; no GPU claim.");
        }
    }
}
