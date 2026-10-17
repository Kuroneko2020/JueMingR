using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace NativeWorldTextProbe
{
    // Hold only delivery time. Capture, original updates, decoded replies,
    // history acceptance and the next capture all belong to the real Session.
    internal static class NativeCombatPrefixSessionChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static object worker;
        private static bool hold;
        private static bool Mailbox(object __instance)=>!hold || !ReferenceEquals(worker,__instance);
        internal static void Run(object native,NpcPredictionCache cache,Action step,Action<Vector2> mouse)
        {
            worker=Get(native,"Worker");int pid=(int)Get(worker,"ChildId");
            var hooks=new Harmony("JueMingR.Tests.PrefixSession");
            hooks.Patch(worker.GetType().GetMethod("TryTakeResult",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPrefixSessionChecks).GetMethod(nameof(Mailbox),Flags)));
            try
            {
                for(int scenario=0;scenario<3;scenario++)
                {
                    int count=scenario==1?123:133;bool retired=scenario==2;
                    hold=false;Call(native,"ClearTarget");Drain(native);NPC.ClearAll();Projectile.ClearAll();
                    int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1400,2100,62,Start:16,Target:Main.myPlayer);
                    var npc=Main.npc[slot];npc.life=npc.lifeMax=100000;
                    mouse(npc.Center);step();var identity=(NpcIdentity)Get(native,"current");
                    Require(identity.Slot==slot,"Original Demon is the selected production target.");
                    Call(native,"ClearTarget");Drain(native);
                    // Demon shares AI_014's real firing boundary but has no
                    // Harpy-specific birth-page reservation in GatherDependencies.
                    npc.ai[0]=20-count;long tick=Main.GameUpdateCount;hold=true;
                    Call(native,"Prepare",identity,tick);var request=Get(native,"pending");
                    Require(request!=null && (int)Get(request,"Horizon")==180 && ((int[])Get(request,"Projectiles")).Length==0,"Actual Capture begins without a reserved projectile page.");
                    Ready();var result=Get(Get(worker,"decodedReply"),"Result");
                    Require(((Array)Get(result,"Frames"))?.Length==count && (int)Get(result,"ContinuationKind")==2,"Actual Demon birth produces the requested true prefix: "+Get(result,"Error"));
                    int hint=(int)Get(result,"ContinuationSlot"),age=count==133?8:3;
                    if(retired)Call(native,"ClearTarget");
                    for(int i=1;i<=age;i++){hold=i!=age;mouse(npc.Center);step();}
                    Require(((IList)Get(request,"History")).Count==(retired?1:age+1),"Only current requests retain every intervening original observation.");
                    var shown=cache.Read(NativeCombatObservationChecks.IndependentReader);
                    Require(count==133 && !retired?shown!=null && shown.CaptureTick==tick && shown.SampleTick==tick+age && shown.Count==121:shown==null,"Actual receipt keeps the exact current+120 age and retirement boundaries: "+Get(native,"Reason"));
                    var next=Get(native,"pending");
                    Require(next!=null && (int)Get(next,"Horizon")==180 && (long)Get(next,"Tick")>tick && (Array.IndexOf((int[])Get(next,"Projectiles"),hint)>=0)!=retired,"Only a current accepted history can seed the next fresh Capture.");
                    long nextTick=(long)Get(next,"Tick");Ready();mouse(npc.Center);step();shown=cache.Read(NativeCombatObservationChecks.IndependentReader);
                    Require(shown!=null && shown.CaptureTick==nextTick && shown.SampleTick==Main.GameUpdateCount && shown.Count==121,"The next real reply extends current+120 without reusing the old capture: "+Get(native,"Reason"));
                    Require(ReferenceEquals(worker,Get(native,"Worker")) && (int)Get(worker,"ChildId")==pid,"Prefix renewal retains the same worker.");
                    Console.WriteLine("PASS Session prefix="+count+" age="+age+" retired="+retired+" old="+tick+" fresh="+nextTick+" hint="+hint+" published="+shown.Count);
                }
            }
            finally{hold=false;worker=null;hooks.UnpatchAll(hooks.Id);}
        }
        private static void Drain(object native)
        {
            var watch=Stopwatch.StartNew();
            while(Get(native,"pending")!=null && watch.ElapsedMilliseconds<8000){Call(native,"DiscardRetiredResult");Thread.Sleep(2);}
            Require(Get(native,"pending")==null && (int)Get(worker,"State")==1,"Prior mailbox is retired and drained without replacing the worker.");
        }
        private static void Ready()
        {var watch=Stopwatch.StartNew();while((int)Get(worker,"State")!=3 && watch.ElapsedMilliseconds<8000)Thread.Sleep(2);Require((int)Get(worker,"State")==3,"The real worker completes before the selected delivery age.");}
        private static object Get(object value,string name){var f=value.GetType().GetField(name,Flags);return f!=null?f.GetValue(value):value.GetType().GetProperty(name,Flags)?.GetValue(value);}
        private static void Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Flags).Invoke(value,args);
        private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    }
}
