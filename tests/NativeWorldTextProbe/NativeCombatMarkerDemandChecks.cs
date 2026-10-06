using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatMarkerDemandChecks
    {
        private static int selections,forecasts;
        private static void Selection(){selections++;}
        private static void Forecast(){forecasts++;}
        internal static void Run(object context)
        {
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            var host=Get(context,"CombatObservation");var selection=Get(host,"Selection");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");var world=Get(host,"World");var marker=Get(world,"Marker");
            foreach(var n in Main.npc)n.active=false;var body=Main.npc[2];body.SetDefaults(2);body.whoAmI=2;body.active=true;body.target=Main.myPlayer;body.position=new Vector2(720,450);Main.LocalPlayer.position=new Vector2(640,500);Main.dayTime=false;
            var select=selection.GetType().GetMethod("Update",flags);var prepare=source.GetType().GetMethod("Prepare",flags);var harmony=new Harmony("JueMingR.Probe.MarkerDemand");
            harmony.Patch(select,new HarmonyMethod(typeof(NativeCombatMarkerDemandChecks).GetMethod(nameof(Selection),flags)));harmony.Patch(prepare,new HarmonyMethod(typeof(NativeCombatMarkerDemandChecks).GetMethod(nameof(Forecast),flags)));
            try
            {
                foreach(bool path in new[]{false,true})foreach(bool mark in new[]{false,true})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:path,marker:mark));selections=forecasts=0;NativeCombatObservationChecks.Fresh(context,host);Call(world,"Prepare");NativeCombatPresentationChecks.Project(world);
                    Require(selections==(path || mark?1:0) && forecasts==(path?1:0),"Four demand combinations share one selection and only path predicts: "+path+"/"+mark);
                    Require(cache.Required==(path?120:0) && (bool)Get(marker,"Visible")==mark && GetOptional(source,"Native")==null,"Demand and actual marker command visibility match switches.");
                }
                body.SetDefaults(401);body.whoAmI=2;body.active=true;body.dontTakeDamage=body.immortal=false;body.position=new Vector2(720,450);body.ai[0]=0;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,marker:true));NativeCombatObservationChecks.Fresh(context,host);Call(world,"Prepare");NativeCombatPresentationChecks.Project(world);Require(((NpcIdentity)Get(selection,"Target")).Type==401 && cache.Read(0)==null && (bool)Get(marker,"Visible"),"Legal selected unsupported relation still has current visible marker without a path.");
                var head=Main.npc[0];head.SetDefaults(13);head.whoAmI=0;head.active=true;head.position=new Vector2(670,450);
                body.SetDefaults(14);body.whoAmI=2;body.active=true;body.dontTakeDamage=body.immortal=false;body.position=new Vector2(720,450);body.ai[1]=0;body.realLife=-1;
                var input=Get(context,"Input");PlayerInput.MouseInfo=new MouseState((int)(body.Center.X-Main.screenPosition.X),(int)(body.Center.Y-Main.screenPosition.Y),0,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);Call(input,"BeginUpdate");Call(input,"AfterNativeMouse",new List<string>());Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,marker:true,mouseCenter:true,radius:0));Call(host,"SampleMouse");NativeCombatObservationChecks.Fresh(context,host);Call(world,"Prepare");NativeCombatPresentationChecks.Project(world);var identity=(NpcIdentity)Get(marker,"identity");
                Require(identity.Slot==2 && identity.Type==14 && ReferenceEquals(identity.Token,body) && cache.Read(0)?.Strategy==PredictionStrategy.SegmentedTrend,"Marker and light segmented path retain actually selected body, never the head.");
                var pieces=(Array)Get(marker,"Pieces");var center=Vector2.Zero;foreach(var piece in pieces)center+=(Vector2)Get(piece,"Position");center/=pieces.Length;Require(Vector2.Distance(center,body.Center-Main.screenPosition)<.01f,"Selected segment marker uses current visible body center.");
                body.active=false;NativeCombatObservationChecks.Fresh(context,host);Call(world,"Prepare");NativeCombatPresentationChecks.Project(world);Require(!(bool)Get(marker,"Visible"),"Selected segment death immediately clears current marker.");
                for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i]==null)Main.player[i]=new Player{whoAmI=i};
                body=(NPC)typeof(NPC).GetMethod("NewNPCInstanceInSlot",flags).Invoke(null,new object[]{2,(byte)0});body.SetDefaults(14);body.active=true;body.position=new Vector2(720,450);body.dontTakeDamage=body.immortal=false;
                NativeCombatObservationChecks.Fresh(context,host);Call(world,"Prepare");NativeCombatPresentationChecks.Project(world);var replacement=(NpcIdentity)Get(marker,"identity");Require((bool)Get(marker,"Visible") && replacement.Slot==2 && !replacement.Equals(identity) && ReferenceEquals(replacement.Token,body),"Native same-slot generation/replacement never inherits old body marker.");
                NativeCombatObservationChecks.Save(host,new ObservationOptions());Call(world,"Prepare");NativeCombatPresentationChecks.Project(world);Require(!(bool)Get(marker,"Visible") && cache.Required==0,"Final consumer removal clears part display and forecast demand.");
            }
            finally{harmony.Unpatch(select,HarmonyPatchType.Prefix,harmony.Id);harmony.Unpatch(prepare,HarmonyPatchType.Prefix,harmony.Id);}
            Console.WriteLine("PASS MARKER DEMAND four switches, one real selection, unsupported selected target, actual segment, death/native slot replacement and final OFF.");
        }
    }
}
