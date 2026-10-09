using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatQueryRetirementChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static object worker;
        private static bool hold;
        private static bool Mailbox(object __instance)=>!hold || !ReferenceEquals(__instance,worker);
        internal static void Run(object native,NpcPredictionCache cache,Action step)
        {
            worker=Get(native,"Worker");var hooks=new Harmony("JueMingR.Tests.QueryRetirement");
            hooks.Patch(worker.GetType().GetMethod("TryTakeResult",Flags),prefix:new HarmonyMethod(typeof(NativeCombatQueryRetirementChecks).GetMethod(nameof(Mailbox),Flags)));
            try
            {
                foreach(string change in new[]{"same-tick-reset","new-object","friendly-roundtrip","active-roundtrip"})
                {
                    var town=new NPC();town.SetDefaults(678);town.whoAmI=0;town.active=true;town.position=new Vector2(3000,2300);Main.npc[0]=town;
                    var watch=Stopwatch.StartNew();object accepted;
                    do{step();accepted=Get(native,"acceptedRequest");}while(!Owns(accepted,town) && watch.Elapsed.TotalSeconds<8);
                    Require(Owns(accepted,town),"A real accepted request owns the query-only town.");
                    Require(Array.IndexOf((int[])Get(accepted,"Npcs"),0)<0,"The retirement scenario must not hide behind a full NPC page.");
                    hold=true;object pending;
                    do{step();pending=Get(native,"pending");}while(!Owns(pending,town) && watch.Elapsed.TotalSeconds<9);
                    Require(Owns(pending,town),"The real mailbox contains another request owning the same query premise.");
                    long old=(long)Get(accepted,"Tick"),late=(long)Get(pending,"Tick"),rejected=(long)Get(native,"Rejected");
                    if(change=="same-tick-reset")
                    {
                        var position=town.position;byte generation=town.generation;
                        town.SetDefaults(1);town.SetDefaults(678);town.position=position;town.active=true;
                        Require(town.generation==generation && ReferenceEquals(Main.npc[0],town),"Native same-tick reconstruction retains object and generation.");
                    }
                    else if(change=="new-object")
                    {var replacement=new NPC();replacement.SetDefaults(678);replacement.whoAmI=0;replacement.position=town.position;replacement.active=true;Main.npc[0]=replacement;}
                    else if(change=="friendly-roundtrip"){town.friendly=false;step();town.friendly=true;}
                    else{town.active=false;step();town.active=true;}
                    step();Require((bool)Get(pending,"Retired") && !ReferenceEquals(accepted,Get(native,"acceptedRequest")),"Changed query retires both accepted and in-flight ownership.");
                    Require(cache.Read(0)==null || cache.Read(0).CaptureTick!=old,"The old displayed window is not revived by restored eligibility.");
                    hold=false;watch.Restart();int published=0;
                    while(published<30 && watch.Elapsed.TotalSeconds<8)
                    {
                        step();var path=cache.Read(0);
                        Require(path==null || path.CaptureTick!=late,"Late retired reply must never publish.");
                        if(path!=null && path.CaptureTick>late)published++;
                    }
                    Require(published==30 && (long)Get(native,"Rejected")>rejected && ReferenceEquals(worker,Get(native,"Worker")),"Same worker rejects the old mailbox and recovers from current observations.");
                    Console.WriteLine("PASS query retirement "+change+" old="+old+" late="+late+" recovery="+published);
                }
            }
            finally{hold=false;worker=null;hooks.UnpatchAll(hooks.Id);}
        }
        private static bool Owns(object request,NPC npc)
        {
            if(request==null || (bool)Get(request,"Retired"))return false;
            foreach(object premise in (Array)Get(request,"Queries"))if((bool)premise.GetType().GetMethod("Owns",Flags).Invoke(premise,new object[]{npc}))return true;
            return false;
        }
        private static object Get(object value,string name){var f=value.GetType().GetField(name,Flags);return f!=null?f.GetValue(value):value.GetType().GetProperty(name,Flags)?.GetValue(value);}
        private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    }
}
