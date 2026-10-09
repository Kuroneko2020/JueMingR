using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatImpactVisualChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static bool readFault,drawFault;
        private static int faults;
        private static void ReadFault(){if(readFault){faults++;throw new InvalidOperationException("controlled impact read failure");}}
        private static void DrawFault(){if(drawFault){faults++;throw new InvalidOperationException("controlled impact segment failure");}}
        private static bool Red(Color p){return p.A>100 && p.R>190 && p.G<110 && p.B<110;}
        private static bool PathInk(Color p){return p.A>100 && p.R>180 && p.G>150 && p.B>150;}
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            Directory.CreateDirectory(output);var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var world=Get(host,"World");var impact=Get(world,"Impact");var input=Get(context,"Input");
            var harmony=new Harmony("JueMingR.Tests.ImpactVisual");
            harmony.Patch(attack.GetType().GetProperty("ExpectedImpact",Flags).GetGetMethod(true),prefix:new HarmonyMethod(typeof(NativeCombatImpactVisualChecks).GetMethod("ReadFault",Flags)));
            harmony.Patch(impact.GetType().GetMethod("Segment",Flags),prefix:new HarmonyMethod(typeof(NativeCombatImpactVisualChecks).GetMethod("DrawFault",Flags)));
            try
            {
                Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));Main.screenWidth=960;Main.screenHeight=640;Main.UIScale=1;
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(810,646);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;Array.Clear(n.immune,0,n.immune.Length);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));
                var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");
                foreach(float gravity in new[]{1f,-1f})foreach(float scale in new[]{.8f,1.4f})
                {
                    p.gravDir=gravity;Main.screenPosition=new Vector2(600,400);Main.GameViewMatrix.Zoom=Vector2.One;
                    NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatAimChecks.Prepare(host,attack,n,0);Call(world,"Prepare");
                    var contact=(AttackContact)Get(attack,"ExpectedImpact");Require(contact!=null,"render fixture has a legal prepared contact");
                    // Change camera AFTER preparation. The oracle uses the final
                    // camera independently of the production marker projection.
                    Main.screenPosition=new Vector2(640,420);Main.GameViewMatrix.Zoom=new Vector2(scale);var zoom=Main.GameViewMatrix.ZoomMatrix;var local=new Vector2(contact.ImpactX,contact.ImpactY)-Main.screenPosition;if(gravity<0)local.Y=Main.screenHeight-local.Y;var center=Vector2.Transform(local,zoom);
                    int steps=cache.Steps;var pixels=graphics.Pixels(()=>Call(world,"Draw"),zoom);var red=pixels.Select((c,i)=>new{c,i}).Where(v=>Red(v.c)).Select(v=>new Vector2(v.i%960,v.i/960)).ToArray();
                    Require(red.Length>=16 && red.Length<=100 && red.All(v=>Math.Abs(v.X-center.X)<=6 && Math.Abs(v.Y-center.Y)<=6),"actual red pixels stay around final ExpectedImpact camera projection");
                    Require(red.Max(v=>v.X)-red.Min(v=>v.X)>=6 && red.Max(v=>v.X)-red.Min(v=>v.X)<=10 && red.Max(v=>v.Y)-red.Min(v=>v.Y)>=6 && red.Max(v=>v.Y)-red.Min(v=>v.Y)<=10,"red screen outline remains eight pixels at both zooms");
                    Console.WriteLine("IMPACT oracle hollow="+!Red(pixels[(int)center.Y*960+(int)center.X])+" pathInk="+pixels.Count(PathInk)+" strokes="+Get(world,"StrokeCount")+" steps="+steps+"/"+cache.Steps+" center="+center);
                    Require(!Red(pixels[(int)center.Y*960+(int)center.X]) && pixels.Any(PathInk) && cache.Steps==steps,"red is hollow, path survives and actual Draw does not advance prediction");
                    graphics.Image(Path.Combine(output,"impact-"+gravity+"-"+scale+".png"),()=>Call(world,"Draw"),zoom);
                    Console.WriteLine("PASS actual impact Draw gravity="+gravity+" zoom="+scale+" redPixels="+red.Length+" center="+center);
                }
                p.gravDir=1;Main.screenPosition=new Vector2(600,400);Main.GameViewMatrix.Zoom=Vector2.One;
                foreach(bool path in new[]{false,true})foreach(bool aim in new[]{false,true})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,path,false,false,false,25,false,aim));NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatAimChecks.Prepare(host,attack,n,0);Call(world,"Prepare");var pixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                    Require(pixels.Any(Red)==(path&&aim),"actual red Draw requires independent path and aim toggles");if(path)Require(pixels.Any(PathInk),"path remains independently visible with aim OFF");
                }
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");
                foreach(string stage in new[]{"capture","project","draw"})
                {
                    Call(host,"Set",6,true);p.releaseUseItem=true;NativeCombatAimChecks.Prepare(host,attack,n,0);Call(world,"Prepare");var healthyContact=Get(attack,"ExpectedImpact");faults=0;
                    readFault=stage!="draw";drawFault=stage=="draw";bool leaked=false;
                    try{if(stage=="capture")Call(world,"Prepare");else graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);}catch(TargetInvocationException){leaked=true;}
                    Require(!leaked && (bool)Get(impact,"Failed") && !(bool)Get(world,"Failed") && !(bool)Get(attack,"Failed"),"impact "+stage+" failure is local to red presentation");
                    for(int i=0;i<3;i++){Call(world,"PollResources");Call(world,"Prepare");var pixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Require(!pixels.Any(Red) && pixels.Any(PathInk),"healthy path survives latched impact failure");}
                    Require(faults==1,"impact failure does not retry on each prepare/draw with injection still armed");readFault=drawFault=false;Call(host,"Set",6,true);Require(ReferenceEquals(healthyContact,Get(attack,"ExpectedImpact")),"display-only retry preserves the healthy prepared attack capability");Call(world,"Prepare");Require(!(bool)Get(impact,"Failed") && graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix).Any(Red),"explicit Aim retry restores only failed red display");
                }
                p.inventory[0].SetDefaults(39);p.inventory[54].SetDefaults(40);p.inventory[54].stack=999;p.releaseUseItem=true;n.position=new Vector2(1060,646);NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatAimChecks.Prepare(host,attack,n,0);var arc=(AttackContact)Get(attack,"ExpectedImpact");Require(arc!=null && Math.Abs(arc.AimY-arc.ImpactY)>12,"bow render fixture separates elevated AimInput from ExpectedImpact");Call(world,"Prepare");var arcPixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);var arcCenter=Vector2.Transform(new Vector2(arc.ImpactX,arc.ImpactY)-Main.screenPosition,Main.GameViewMatrix.ZoomMatrix);Require(arcPixels.Select((c,i)=>new{c,i}).Where(v=>Red(v.c)).All(v=>Math.Abs(v.i%960-arcCenter.X)<=6 && Math.Abs(v.i/960-arcCenter.Y)<=6) && arcPixels.Any(Red),"actual bow red pixels mark contact rather than elevated input");graphics.Image(Path.Combine(output,"impact-bow.png"),()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                NativeCombatAimChecks.Prepare(host,attack,n,0);Call(world,"Prepare");typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var consumed=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Require(!consumed.Any(Red) && consumed.Any(PathInk),"real Shot consumption retires red pixels in the same frame while path survives");
                Console.WriteLine("PASS impact actual World.Draw pixels, final camera, toggles, consumption and Capture/Project/Draw local latch/recovery. Render fixture, not Main/game execution.");
            }
            finally{readFault=drawFault=false;harmony.UnpatchAll(harmony.Id);Main.LocalPlayer.gravDir=1;Main.GameViewMatrix.Zoom=Vector2.One;NativeCombatObservationChecks.Save(host,new ObservationOptions());Call(world,"Recover");}
        }
    }
}
