using System;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatLocalFailureChecks
    {
        private static int prepares,selections;
        private static int readFaults;private static NPC faultNpc;private static string faultType;
        private static bool failPrepare,failSelection;
        private static Exception EndRead(NPC __0,Exception __exception)
        {if(__exception!=null){readFaults++;faultNpc=__0;faultType=__exception.GetType().FullName;}return __exception;}
        private static void BeforePrepare(){prepares++;if(failPrepare)throw new InvalidOperationException("Controlled shared Source fault.");}
        private static void BeforeSelection(){selections++;if(failSelection)throw new InvalidOperationException("Controlled shared selection fault.");}
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var cache=(NpcPredictionCache)Get(source,"Cache");foreach(var n in Main.npc)n.active=false;
            Main.LocalPlayer.position=new Vector2(600,700);Main.dayTime=false;
            var a=Main.npc[2];a.SetDefaults(2);a.whoAmI=2;a.active=true;a.target=Main.myPlayer;a.position=new Vector2(800,900);a.velocity=Vector2.Zero;
            var b=Main.npc[3];b.SetDefaults(2);b.whoAmI=3;b.target=Main.myPlayer;b.position=new Vector2(850,900);b.active=false;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,marker:true));NativeCombatObservationChecks.Fresh(context,host);var initial=cache.Read(0);var selected=(JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target");Require(initial!=null && selected.Slot==2 && selected.Type==2 && selected.Generation==a.generation && selected.NetId==a.netID && ReferenceEquals(selected.Token,a) && initial.Identity.Equals(selected),"Fault starts from complete selected A identity and actual default Source publication.");
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            var harmony=new Harmony("JueMingR.Probe.DefaultSourceFault");var read=source.GetType().GetMethod("Read",flags);readFaults=0;faultNpc=null;faultType=null;
            harmony.Patch(read,finalizer:new HarmonyMethod(typeof(NativeCombatLocalFailureChecks).GetMethod(nameof(EndRead),flags)));
            var times=a.buffTime;int oldBuff=a.buffType[0];a.buffType[0]=Terraria.ID.BuffID.Slow;a.buffTime=null;
            try{NativeCombatObservationChecks.Fresh(context,host);Console.WriteLine("LOCAL FAILURE A cached="+(cache.Read(0)!=null)+" marker="+Get(host,"Marker")+" faultType="+faultType+" faultHits="+readFaults+" fullIdentity="+((JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target")).Equals(selected));Require(cache.Read(0)==null && readFaults==1 && ReferenceEquals(faultNpc,a) && faultType.EndsWith("NpcObservationFailure",StringComparison.Ordinal) && ((JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target")).Equals(selected),"Actual selected A Source.Read fault hit and explicit local category precede isolation claims.");}
            finally{a.buffTime=times;a.buffType[0]=oldBuff;}
            a.active=false;b.active=true;NativeCombatObservationChecks.Fresh(context,host);
            Console.WriteLine("LOCAL FAILURE B selected="+Get(Get(host,"Selection"),"Target")+" path="+Get(host,"Path")+" cached="+(cache.Read(0)!=null));
            Require(cache.Read(0)!=null && ReferenceEquals(cache.Read(0).Identity.Token,b),"One malformed NPC observation cannot poison the next healthy selected object.");
            a.active=true;b.active=false;NativeCombatObservationChecks.Fresh(context,host);
            Require(cache.Read(0)==null && readFaults==1 && (bool)Get(host,"TargetPredictionFailed") && (bool)Get(host,"Marker"),"A→B→A retains bounded full-identity local fault without retrying Read, poisoning B or hiding selected A marker.");
            Call(host,"Set",1,true);NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(0)!=null && ReferenceEquals(cache.Read(0).Identity.Token,a),"Explicit path ON retries current object's restored observation.");
            var prepare=source.GetType().GetMethod("Prepare",flags);var selection=Get(host,"Selection").GetType().GetMethod("Update",flags);
            harmony.Patch(prepare,new HarmonyMethod(typeof(NativeCombatLocalFailureChecks).GetMethod(nameof(BeforePrepare),flags)));
            harmony.Patch(selection,new HarmonyMethod(typeof(NativeCombatLocalFailureChecks).GetMethod(nameof(BeforeSelection),flags)));
            try
            {
                // This fault check accepts any genuine future; strict horizon
                // refusal is a different contract, not evidence of failed retry.
                cache.Demand(1,1,120);prepares=0;failPrepare=true;NativeCombatObservationChecks.Fresh(context,host);
                for(int i=0;i<4;i++)NativeCombatObservationChecks.Fresh(context,host);
                Require(prepares==1 && cache.Required==120 && cache.Read(1)==null && (bool)Get(host,"Marker"),"Unknown shared Prepare fault blocks independent demand without repeated work, retaining separate selection marker.");
                failPrepare=false;Call(host,"Set",1,true);NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(1)!=null,"Explicit retry restores all valid shared prediction demand.");
                selections=0;failSelection=true;NativeCombatObservationChecks.Fresh(context,host);for(int i=0;i<4;i++)NativeCombatObservationChecks.Fresh(context,host);
                Require(selections==1 && !(bool)Get(host,"Enabled") && !(bool)Get(host,"Marker") && cache.Read(1)==null,"Untrusted shared selection stops marker and all prediction readers with one failure.");
                failSelection=false;Call(host,"Set",1,true);NativeCombatObservationChecks.Fresh(context,host);Require(cache.Read(1)!=null && (bool)Get(host,"Marker"),"Explicit retry restores unique selection after shared fault.");
                cache.Release(1);NativeCombatObservationChecks.Save(host,new ObservationOptions(marker:true));prepares=0;failPrepare=true;NativeCombatObservationChecks.Fresh(context,host);
                Require(prepares==0 && cache.Required==0 && (bool)Get(Get(host,"Selection"),"HasTarget"),"Marker-only has no Source failure surface or forecast demand.");
                NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatObservationChecks.Fresh(context,host);Require(prepares==0 && !(bool)Get(Get(host,"Selection"),"HasTarget"),"All consumers OFF retire without fault retries.");
            }
            finally{failPrepare=failSelection=false;harmony.Unpatch(prepare,HarmonyPatchType.Prefix,harmony.Id);harmony.Unpatch(selection,HarmonyPatchType.Prefix,harmony.Id);harmony.Unpatch(read,HarmonyPatchType.Finalizer,harmony.Id);}
            Call(host,"OnSessionEnded");Require(cache.Required==0 && !(bool)Get(host,"TargetPredictionFailed"),"Exit clears bounded local failure lifetime and all demand.");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());Console.WriteLine("PASS LOCAL FAILURE real default Source, malformed per-object owned array, healthy target isolation.");
        }
    }
}
