using System;
using System.Diagnostics;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatEventRetirementChecks
    {
        internal static void Cpu(object context)
        {
            var host=Get(context,"CombatObservation");var geometry=Get(host,"Geometry");NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true));Call(geometry,"Clear");
            var pending=Call(geometry,"Event");Call(pending,"Rectangle",new Rectangle(200,200,30,30),0,false);for(int i=0;i<6;i++){NativeQuickItemChecks.BeginWorldStep();Call(geometry,"PrepareEvents");}
            Require((int)Get(geometry,"EventCount")==1 && !(bool)Get(pending,"Presented"),"CPU short six updates keep a real opportunity");
            Set(pending,"CreatedAt",Stopwatch.GetTimestamp()-3*Stopwatch.Frequency);uint tick=Main.GameUpdateCount;Call(geometry,"PrepareEventsForDraw");Require(Main.GameUpdateCount==tick && (int)Get(geometry,"EventCount")==0 && !(bool)Get(pending,"Presented"),"CPU same-tick bounded wall retirement is not Presented");
            Call(geometry,"Clear");for(int i=0;i<4001;i++)Call(geometry,"Event");Require((int)Get(geometry,"EventCount")==4000 && (bool)Get(geometry,"EventOverflow"),"CPU capacity remains bounded under dense callbacks");
            Call(host,"OnSessionEnded");Require((int)Get(geometry,"EventCount")==0,"CPU session clears events");Console.WriteLine("PASS R02 CPU owner time/capacity/session contract; actual XNA Draw is the separate NpcEventRetirement scope");
        }
        // This is the existing hidden XNA device / actual World.Draw outlet.
        // Projection probes never count as a completed presentation receipt.
        internal static void Run(object context,ProbeGraphics graphics)
        {
            var host=Get(context,"CombatObservation");var world=Get(host,"World");var geometry=Get(host,"Geometry");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true));Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));
            Main.screenWidth=960;Main.screenHeight=640;Main.GameViewMatrix.Zoom=Vector2.One;Main.screenPosition=Vector2.Zero;
            object Emit(Rectangle bounds){var value=Call(geometry,"Event");Call(value,"Rectangle",bounds,0,false);return value;}
            void Draw(){graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);}
            int EventStrokes(){return (int)Get(world,"eventEnd")-(int)Get(world,"eventStart");}
            Call(geometry,"Clear");var outside=Emit(new Rectangle(10000,10000,30,30));Call(world,"Prepare");Draw();Require(EventStrokes()==0 && !(bool)Get(outside,"Presented"),"First completed Draw truly has no offscreen event pixels");
            Main.screenPosition=new Vector2(9900,9900);Draw();Require(EventStrokes()==0,"Old fully offscreen event cannot replay after final-camera move in same world tick");
            Call(geometry,"Clear");Main.screenPosition=Vector2.Zero;var shortWait=Emit(new Rectangle(200,200,30,30));for(int i=0;i<6;i++){NativeQuickItemChecks.BeginWorldStep();Call(geometry,"PrepareEvents");}
            Call(world,"Prepare");NativeCombatPresentationChecks.Project(world);Require(!(bool)Get(shortWait,"Presented") && !(bool)Get(shortWait,"Retired"),"Projection alone cannot commit an opportunity");Draw();Require((bool)Get(shortWait,"Presented") && EventStrokes()>0,"Six-update short wait gets an actual visible Draw");Draw();Require(EventStrokes()==0,"Visible event draws only once");
            Call(geometry,"Clear");var aged=Emit(new Rectangle(200,200,30,30));uint tick=Main.GameUpdateCount;Set(aged,"CreatedAt",Stopwatch.GetTimestamp()-3*Stopwatch.Frequency);Draw();Require(Main.GameUpdateCount==tick && EventStrokes()==0 && !(bool)Get(aged,"Presented"),"Wall-age retirement works on Draw recovery without Prepare/world updates, never claims shown");
            Call(geometry,"Clear");var noBatch=Emit(new Rectangle(200,200,30,30));var batch=Main.spriteBatch;Main.spriteBatch=null;Call(world,"Draw");Main.spriteBatch=batch;Require(!(bool)Get(noBatch,"Presented") && !(bool)Get(noBatch,"Retired"),"Unavailable batch is not a completed opportunity");Draw();Require((bool)Get(noBatch,"Presented"),"Short device absence still receives one later real Draw");
            Call(geometry,"Clear");var failed=Emit(new Rectangle(200,200,30,30));bool threw=false;
            using(var disposed=new Microsoft.Xna.Framework.Graphics.SpriteBatch(graphics.GraphicsDevice))
            {disposed.Dispose();graphics.Pixels(()=>{var good=Main.spriteBatch;try{Main.spriteBatch=disposed;try{Call(world,"Draw");}catch(System.Reflection.TargetInvocationException){threw=true;}}finally{Main.spriteBatch=good;}},Main.GameViewMatrix.ZoomMatrix);}
            Require(threw && !(bool)Get(failed,"Presented") && !(bool)Get(failed,"Retired"),"Projected geometry followed by actual failed Draw commits neither shown nor invisible opportunity");Draw();Require((bool)Get(failed,"Presented"),"Successful later Draw consumes failed attempt once");
            Call(geometry,"Clear");var crossing=Call(geometry,"Event");Call(crossing,"Line",new Vector2(-1000,300),new Vector2(2000,300),4f,0,false);Draw();Require(EventStrokes()>0 && (bool)Get(crossing,"Presented"),"Offscreen origin long line still crosses and draws final viewport");
            Call(geometry,"Clear");for(int i=0;i<26;i++){var dense=Call(geometry,"Event");for(int j=0;j<160;j++)Call(dense,"Rectangle",new Rectangle(100,100,30,30),0,false);}var unvisited=Emit(new Rectangle(10000,10000,30,30));Draw();Require((bool)Get(world,"limited") && !(bool)Get(unvisited,"Presented") && !(bool)Get(unvisited,"Retired"),"Budget-unvisited event cannot be declared fully offscreen");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());Require((int)Get(geometry,"EventCount")==0,"OFF clears bounded events");
            Console.WriteLine("PASS R02 actual XNA Draw: same-tick offscreen retirement, short6, project-only, wall age without update, unavailable/failed batch, crossing line, budget-unvisited, OFF");
        }
    }
}
