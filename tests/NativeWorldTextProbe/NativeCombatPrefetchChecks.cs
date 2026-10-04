using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Terraria;
using JueMingR.Platform.Combat;
using static NativeWorldTextProbe.NativeCombatAttackMechanismChecks;

namespace NativeWorldTextProbe
{
    // Test-only upper bound: one real missing-NPC reply authorizes one fresh
    // capture of this known scene cohort. It is not automatic discovery and
    // never retains pages across Hurt, world/target clearing or object reuse.
    internal sealed class NativeCombatPrefetchChecks:IDisposable
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private sealed class Actor
        {
            internal readonly NPC Value;internal readonly int Slot,Type,NetId;internal readonly byte Generation;
            internal Actor(NPC npc){Value=npc;Slot=npc.whoAmI;Type=npc.type;NetId=npc.netID;Generation=npc.generation;}
            internal bool Current=>Slot>=0 && Slot<Main.maxNPCs && ReferenceEquals(Main.npc[Slot],Value) && Value.whoAmI==Slot && Value.active && Value.generation==Generation && Value.type==Type && Value.netID==NetId;
            internal string Identity=>Slot+":"+Generation+":"+Type+":"+NetId;
        }
        private sealed class Stamp
        {
            internal NpcIdentity Identity;internal long Tick,Epoch;internal Actor[] Actors;internal double ObserverMs;
        }
        private sealed class Ticket
        {internal Stamp Stamp;internal long Arrive;internal int Missing;}
        private static NativeCombatPrefetchChecks active;
        private readonly object owner;private readonly NativeCombatAttackTrace trace;private readonly NPC primary;
        private readonly NPC[] cohort;private readonly string output;private readonly bool enabled;
        private readonly Harmony hooks=new Harmony("JueMingR.Tests.QueryPrefetch");
        private readonly Dictionary<object,Stamp> captures=new Dictionary<object,Stamp>();
        private readonly List<string> events=new List<string>{"phase,frame,tick,event,epoch,sourceCapture,sourceArrive,missing,cohort,added,observerMs"};
        private readonly List<string> replies=new List<string>{"phase,frame,capture,arrive,age,outcome,kind,frames,historyCount,checkedHistory,historyDifference,historyBytes,npcs,projectiles,captureMs,encodeMs,exchangeMs,decodeMs,acceptMs,workerTotalMs,workerResetMs,workerRestoreMs,workerAdvanceMs,requestBytes,replyBytes,observerMs"};
        private readonly List<KeyValuePair<string,byte[]>> histories=new List<KeyValuePair<string,byte[]>>();
        private Ticket ticket;private long epoch;private int historyBytes;
        internal NativeCombatPrefetchChecks(object native,NativeCombatAttackTrace observer,NPC target,IEnumerable<NPC> population,string destination,bool ideal)
        {
            owner=native;trace=observer;primary=target;cohort=population.OrderBy(n=>n.whoAmI).ToArray();output=destination;enabled=ideal;
            if(active!=null || cohort.Length!=34 || cohort.Distinct().Count()!=34 || !cohort.Contains(primary))throw new InvalidOperationException("Only the approved 34-actor scene is permitted.");
            if(!(bool)native.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionPipeProtocol",true).GetField("Measure",Flags).GetValue(null))throw new InvalidOperationException("This cost experiment requires explicit isolated measurements.");
            active=this;var type=native.GetType();
            Patch(type.GetMethod("Capture",Flags),nameof(Capturing),nameof(Captured));
            Patch(type.GetMethod("Receive",Flags),nameof(Receiving),nameof(Received));
            Patch(type.GetMethod("ClearTarget",Flags),nameof(Clearing),null);
        }
        private void Patch(MethodBase method,string before,string after)
        {hooks.Patch(method,before==null?null:new HarmonyMethod(GetType(),before),after==null?null:new HarmonyMethod(GetType(),after));}
        private static void Clearing(object __instance)
        {
            var a=active;if(a==null || !ReferenceEquals(a.owner,__instance))return;
            a.Record("clear",(long)Main.GameUpdateCount,a.ticket,0,0);a.ticket=null;a.epoch++;
        }
        private static void Capturing(object __instance,NpcIdentity identity,long tick,out Stamp __state)
        {
            __state=null;var a=active;if(a==null || !ReferenceEquals(a.owner,__instance))return;
            long start=Stopwatch.GetTimestamp();
            var value=new Stamp{Identity=identity,Tick=tick,Epoch=a.epoch,Actors=a.cohort.Select(n=>new Actor(n)).ToArray()};
            var permit=a.ticket;a.ticket=null;
            if(permit!=null)
            {
                bool current=permit.Stamp.Epoch==a.epoch && tick>permit.Stamp.Tick && permit.Stamp.Identity.Equals(identity) && ReferenceEquals(identity.Token,a.primary) && permit.Stamp.Actors.All(n=>n.Current);
                int added=0;
                if(a.enabled && current)
                {var pages=(SortedSet<int>)Get(__instance,"npcs");foreach(var n in permit.Stamp.Actors)if(pages.Add(n.Slot))added++;a.trace.RequiredCapture=tick;}
                a.Record(current?(a.enabled?"prefetch":"baseline-ticket"):"invalid-ticket",tick,permit,added,Ms(Stopwatch.GetTimestamp()-start));
            }
            value.ObserverMs=Ms(Stopwatch.GetTimestamp()-start);__state=value;
        }
        private static void Captured(object __instance,long tick,Stamp __state)
        {
            var a=active;if(a==null || !ReferenceEquals(a.owner,__instance) || __state==null)return;
            var request=Get(__instance,"pending");if(request==null || (long)Tick(request)!=tick)return;
            a.captures[request]=__state;
            a.Record("capture-observed",tick,null,0,__state.ObserverMs);
        }
        private static void Receiving(object __instance,object response,out object[] __state)
        {
            __state=null;var a=active;if(a==null || !ReferenceEquals(a.owner,__instance))return;
            var request=Get(__instance,"pending");Stamp stamp;
            if(request==null || !a.captures.TryGetValue(request,out stamp))throw new InvalidOperationException("Receive lacks its independent capture identity.");
            __state=new[]{request,Get(response,"Result"),stamp,Get(__instance,"Refused")};
        }
        private static void Received(object __instance,long tick,object[] __state)
        {
            var a=active;if(a==null || !ReferenceEquals(a.owner,__instance) || __state==null)return;
            long start=Stopwatch.GetTimestamp();var request=__state[0];var result=__state[1];var stamp=(Stamp)__state[2];
            object measure=((IEnumerable)Get(__instance,"Measurements")).Cast<object>().Last();
            if((long)Get(measure,"CaptureTick")!=stamp.Tick)throw new InvalidOperationException("Measurement is not this actual Receive.");
            string outcome=(string)Get(measure,"Outcome");int kind=(int)Get(result,"Kind"),missing=(int)Get(result,"Slot");
            // Match the branch the product really took, not just result.Kind:
            // a retired response may still contain the same missing-page data.
            bool refused=(long)Get(__instance,"Refused")==((long)__state[3])+1 && outcome==(string)Get(result,"Error");
            if(refused && kind==1 && ReferenceEquals(stamp.Identity.Token,a.primary) && stamp.Epoch==a.epoch && stamp.Identity.Equals((NpcIdentity)Get(__instance,"current")) && stamp.Actors.Any(n=>n.Slot==missing) && stamp.Actors.All(n=>n.Current))
            {
                a.ticket=new Ticket{Stamp=stamp,Arrive=tick,Missing=missing};a.Record("query-ticket",tick,a.ticket,0,0);
            }
            var history=((IEnumerable)Get(request,"History")).Cast<object>().ToArray();var frames=(Array)Get(result,"Frames");int compared=0,bytes=0;string difference=null;
            if(outcome=="accepted")
            {
                if(!ReferenceEquals(Get(__instance,"acceptedRequest"),request) || history.Length!=tick-stamp.Tick+1)throw new InvalidOperationException("Accepted request must own every observed history frame.");
                var alignment=__instance.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);var compare=alignment.GetMethod("Difference",Flags);var write=alignment.GetMethod("Write",Flags);
                using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                {
                    writer.Write(history.Length);
                    for(int i=0;i<history.Length;i++){difference=(string)compare.Invoke(null,new[]{frames.GetValue(i),history[i]});compared++;if(difference!=null)throw new InvalidOperationException("Accepted history differs: "+difference);write.Invoke(null,new[]{writer,history[i]});}
                    writer.Flush();byte[] data=stream.ToArray();bytes=data.Length;a.historyBytes+=bytes;
                    if(a.historyBytes>64*1024*1024)throw new InvalidOperationException("Bounded accepted evidence exhausted.");
                    a.histories.Add(new KeyValuePair<string,byte[]>("accepted-"+stamp.Tick+"-history.bin",data));
                }
            }
            a.replies.Add(Csv(a.trace.Phase,a.trace.Frame,stamp.Tick,tick,tick-stamp.Tick,outcome,kind,frames?.Length,history.Length,compared,difference,bytes,Join(Get(request,"Npcs")),Join(Get(request,"Projectiles")),Get(measure,"CaptureMs"),Get(measure,"EncodeMs"),Get(measure,"ExchangeMs"),Get(measure,"DecodeMs"),Get(measure,"AcceptMs"),Get(result,"TotalMs"),Get(result,"ResetMs"),Get(result,"RestoreMs"),Get(result,"AdvanceMs"),Get(measure,"Bytes"),Get(measure,"ReplyBytes"),Ms(Stopwatch.GetTimestamp()-start)));
            a.captures.Remove(request);
        }
        private void Record(string action,long tick,Ticket value,int added,double milliseconds)
        {events.Add(Csv(trace.Phase,trace.Frame,tick,action,epoch,value?.Stamp.Tick,value?.Arrive,value?.Missing,value==null?null:string.Join("|",value.Stamp.Actors.Select(n=>n.Identity)),added,milliseconds));}
        private static double Ms(long ticks)=>ticks*1000.0/Stopwatch.Frequency;
        private static string Join(object values)=>values==null?string.Empty:string.Join("|",((IEnumerable)values).Cast<object>());
        public void Dispose()
        {
            hooks.UnpatchAll(hooks.Id);active=null;ticket=null;
            File.WriteAllLines(Path.Combine(output,"prefetch-events.csv"),events);File.WriteAllLines(Path.Combine(output,"prefetch-replies.csv"),replies);
            foreach(var pair in histories)File.WriteAllBytes(Path.Combine(output,pair.Key),pair.Value);
        }
    }
}
