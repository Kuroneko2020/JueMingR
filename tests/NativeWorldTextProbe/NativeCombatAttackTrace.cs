using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using Terraria;
using static NativeWorldTextProbe.NativeCombatAttackMechanismChecks;

namespace NativeWorldTextProbe
{
    // Bounded test-side observation of real inputs and results. Hooks never
    // assign a result, change arguments, suppress a call, or consume a mailbox.
    internal sealed class NativeCombatAttackTrace:IDisposable
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static NativeCombatAttackTrace active;
        private readonly object owner;private readonly string output;private readonly Harmony patches;
        private readonly object gate=new object();
        private sealed class Packet{internal byte[] Core,Alignment;internal int Field;}
        private readonly Dictionary<object,Packet> packets=new Dictionary<object,Packet>();
        private readonly SortedDictionary<long,byte[]> encoded=new SortedDictionary<long,byte[]>();
        private readonly Dictionary<long,string> capturePhases=new Dictionary<long,string>();
        private readonly SortedDictionary<long,List<object>> observedHistory=new SortedDictionary<long,List<object>>();
        private readonly List<KeyValuePair<string,byte[]>> samples=new List<KeyValuePair<string,byte[]>>();
        private readonly HashSet<string> sampled=new HashSet<string>();private int sampleBytes;
        private readonly List<string> replies=new List<string>{"phase,frame,capture,arrive,age,requestId,retired,impact,queryChanged,outcome,kind,slot,field,error,frames,ns,ps,newNpcs,newProjectiles,chunks,continuationKind,continuationSlot,advanceMs,firstDifference,sample,capturePhase,sampleComplete"};
        private readonly List<string> captures=new List<string>{"phase,frame,tick,requestId,npcs,projectiles,chunks,queryCount"};
        private readonly List<string> events=new List<string>{"phase,frame,tick,event,npcSource,projectileSource,slot,type,key,owner,value,detail"};
        private readonly List<string> differences=new List<string>{"phase,frame,where,expectedTick,actualTick,difference,expected,actual"};
        private readonly List<string> encodings=new List<string>{"capture,bytes,sha256"};
        private readonly List<string> impactProof=new List<string>{"phase,capture,arrive,age,frames,difference,observedFrames,historyDifference,expected,current"};
        [ThreadStatic]private static object receiving;
        [ThreadStatic]private static string firstDifference;
        [ThreadStatic]private static bool impactObservation;
        [ThreadStatic]private static int currentNpc;
        [ThreadStatic]private static int currentProjectile;
        internal string Phase="setup",SelectedRngBefore,SelectedRngAfter;internal int Frame,Selected,CanHitTrue,CanHitFalse;internal long Received;internal Exception Fault;
        internal NativeCombatAttackTrace(object native,string destination)
        {
            owner=native;output=destination;active=this;currentNpc=currentProjectile=-1;
            patches=new Harmony("JueMingR.Tests.AttackMechanism");var host=native.GetType().Assembly;
            Patch(native.GetType().GetMethod("Capture",Flags),null,nameof(Captured));
            Patch(native.GetType().GetMethod("Receive",Flags),nameof(Receiving),nameof(ReceivedReply));
            Patch(native.GetType().GetMethod("ObserveNpcImpact",Flags),nameof(Impact));
            patches.Patch(native.GetType().GetMethod("Prepare",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAttackTrace).GetMethod(nameof(PrepareObservation),Flags)),finalizer:new HarmonyMethod(typeof(NativeCombatAttackTrace).GetMethod(nameof(PrepareFault),Flags)));
            Patch(host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionResult",true).GetMethod("Read",Flags),null,nameof(Decoded));
            Patch(host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeCapturedValues",true).GetMethod("Encode",Flags),null,nameof(Encoded));
            Patch(host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true).GetMethod("Difference",Flags),null,nameof(Difference));
            Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),nameof(NpcStart),nameof(NpcEnd));
            Patch(typeof(Projectile).GetMethod("Update",Flags,null,new[]{typeof(int)},null),nameof(ProjectileStart),nameof(ProjectileEnd));
            Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters()[1].ParameterType==typeof(float)),null,nameof(Birth));
            Patch(typeof(NPC).GetMethods(Flags).Single(m=>m.Name=="NewNPC"),null,nameof(NpcBirth));
            foreach(var method in typeof(Collision).GetMethods(Flags).Where(m=>m.Name=="CanHit"))Patch(method,null,nameof(CanHit));
        }
        private void Patch(MethodBase method,string before,string after=null)
        {if(method==null)throw new InvalidOperationException("Missing observation method.");patches.Patch(method,before==null?null:new HarmonyMethod(typeof(NativeCombatAttackTrace).GetMethod(before,Flags)),after==null?null:new HarmonyMethod(typeof(NativeCombatAttackTrace).GetMethod(after,Flags)));}
        private static void Captured(object __instance)
        {
            var a=active;if(a==null || !ReferenceEquals(a.owner,__instance))return;var r=Get(__instance,"pending");if(r==null)return;
            a.capturePhases[(long)Tick(r)]=a.Phase;
            // Own a separate evidence history. Never append to the product's
            // request.History, whose retirement behavior is under investigation.
            var initial=((IEnumerable)Get(r,"History")).Cast<object>().ToList();a.observedHistory[(long)Tick(r)]=initial;
            while(a.observedHistory.Count>8)a.observedHistory.Remove(a.observedHistory.Keys.First());
            a.captures.Add(Csv(a.Phase,a.Frame,Tick(r),Get(__instance,"Requests"),Join(Get(r,"Npcs")),Join(Get(r,"Projectiles")),((Array)Get(Get(r,"Terrain"),"Chunks")).Length,((Array)Get(r,"Queries"))?.Length));
        }
        private static void PrepareFault(Exception __exception)
        {if(__exception!=null && active!=null){active.Fault=__exception;Console.Error.WriteLine("ATTACK ORIGINAL EXCEPTION "+__exception);}}
        private static void PrepareObservation(object __instance,long tick)
        {
            var a=active;if(a==null || !ReferenceEquals(a.owner,__instance))return;var r=Get(__instance,"pending");if(r==null)return;
            List<object> history;if(!a.observedHistory.TryGetValue((long)Tick(r),out history))return;
            // Only the guardian experiment needs a full independent history.
            // The ordinary emitter windows retain their original observation cost.
            if(!a.Phase.Contains("guardian"))return;
            if(history.Count>=64)throw new InvalidOperationException("Bounded independent history exhausted.");
            var alignment=__instance.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);
            history.Add(alignment.GetMethod("Observe",Flags).Invoke(null,new object[]{tick,Get(r,"Npcs"),Get(r,"Projectiles"),Get(Get(r,"Identity"),"Slot")}));
        }
        private static void Encoded(byte[] __result)
        {
            var a=active;if(a==null || __result.Length<12)return;long tick=BitConverter.ToInt64(__result,4);string hash;
            using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(__result)).Replace("-","");
            lock(a.gate){a.encoded[tick]=__result;while(a.encoded.Count>8)a.encoded.Remove(a.encoded.Keys.First());a.encodings.Add(Csv(tick,__result.Length,hash));}
        }
        private static void Decoded(object[] __args,object __result)
        {
            var a=active;if(a==null)return;var p=new Packet{Core=(byte[])__args[0],Alignment=(byte[])__args[1]};
            if(BitConverter.ToInt32(p.Core,0)<0)using(var r=new BinaryReader(new MemoryStream(p.Core))){r.ReadInt32();r.ReadString();r.ReadString();for(int i=0;i<4;i++)r.ReadInt32();p.Field=r.ReadInt32();}
            lock(a.gate)a.packets[__result]=p;
        }
        private static void Receiving(object __instance,object response,ref object[] __state)
        {
            var a=active;if(a==null || !ReferenceEquals(a.owner,__instance))return;
            var request=Get(__instance,"pending");var result=Get(response,"Result");
            __state=new[]{request,result,Get(__instance,"Requests"),((IEnumerable)Get(__instance,"npcs")).Cast<int>().ToArray(),((IEnumerable)Get(__instance,"projectiles")).Cast<int>().ToArray()};
            receiving=request;firstDifference=null;
        }
        private static void ReceivedReply(object __instance,long tick,object[] __state)
        {
            var a=active;if(a==null || __state==null || __state[0]==null)return;
            try
            {
                a.Received++;var r=__state[0];var result=__state[1];var m=((IEnumerable)Get(__instance,"Measurements")).Cast<object>().Last();string outcome=(string)Get(m,"Outcome");
                if((long)Get(m,"CaptureTick")!=(long)Tick(r))throw new InvalidOperationException("Trace did not observe this Receive's Measurement.");
                Packet packet;lock(a.gate){a.packets.TryGetValue(result,out packet);a.packets.Remove(result);}
                string sample="";bool complete=false;string key=a.Phase+"|"+outcome;
                if(a.sampled.Add(key) && a.sampleBytes<48*1024*1024 && a.sampled.Count<=48)
                {
                    sample="sample-"+(long)Tick(r);byte[] request;lock(a.gate)a.encoded.TryGetValue((long)Tick(r),out request);
                    complete=request!=null && packet!=null && (BitConverter.ToInt32(packet.Core,0)<0 || packet.Alignment!=null);
                    a.Save(sample+"-request.bin",request);if(packet!=null){a.Save(sample+"-core.bin",packet.Core);a.Save(sample+"-alignment.bin",packet.Alignment);}
                    var write=__instance.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment").GetMethod("Write",Flags);
                    using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes)){var history=((IEnumerable)Get(r,"History")).Cast<object>().ToArray();writer.Write(history.Length);foreach(var frame in history)write.Invoke(null,new[]{writer,frame});writer.Flush();a.Save(sample+"-history.bin",bytes.ToArray());}
                    List<object> independent;if(a.observedHistory.TryGetValue((long)Tick(r),out independent))using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes)){writer.Write(independent.Count);foreach(var frame in independent)write.Invoke(null,new[]{writer,frame});writer.Flush();a.Save(sample+"-observed-history.bin",bytes.ToArray());}
                }
                string field=packet==null?"":packet.Field.ToString("X8",CultureInfo.InvariantCulture);
                if(packet!=null && packet.Field!=0)try{field+=" "+typeof(NPC).Module.ResolveField(packet.Field);}catch(ArgumentException){}
                string capturePhase;if(!a.capturePhases.TryGetValue((long)Tick(r),out capturePhase))capturePhase="before-trace";
                a.replies.Add(Csv(a.Phase,a.Frame,Tick(r),tick,tick-(long)Tick(r),__state[2],Get(r,"Retired"),Get(r,"Impact"),Get(r,"QueryChanged"),outcome,Get(result,"Kind"),Get(result,"Slot"),field,Get(result,"Error"),((Array)Get(result,"Frames"))?.Length,Join(Get(r,"Npcs")),Join(Get(r,"Projectiles")),Missing(Get(result,"Npcs"),(int[])__state[3]),Missing(Get(result,"Projectiles"),(int[])__state[4]),((Array)Get(Get(r,"Terrain"),"Chunks")).Length,Get(result,"ContinuationKind"),Get(result,"ContinuationSlot"),(double)Get(result,"TotalMs")>0?Get(result,"AdvanceMs"):null,firstDifference,sample,capturePhase,sample.Length==0?null:(object)complete));
                var frames=(Array)Get(result,"Frames");long age=tick-(long)Tick(r);
                if((int)Get(r,"Impact")!=0 && frames!=null && age>=0 && age<frames.Length)
                {
                    // A read-only counterfactual, AFTER the genuine rejection:
                    // does its returned current frame match actual live state?
                    // This never restores acceptance or claims missing history.
                    var alignment=__instance.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment",true);
                    var current=alignment.GetMethod("Observe",Flags).Invoke(null,new object[]{tick,Get(r,"Npcs"),Get(r,"Projectiles"),Get(Get(r,"Identity"),"Slot")});
                    var expected=frames.GetValue((int)age);var prior=receiving;receiving=null;
                    string difference,historyDifference=null;int compared=0;
                    try
                    {
                        impactObservation=true;var compare=alignment.GetMethod("Difference",Flags);difference=(string)compare.Invoke(null,new[]{expected,current});
                        List<object> independent;if(a.observedHistory.TryGetValue((long)Tick(r),out independent))foreach(var frame in independent)
                        {long index=(long)Get(frame,"Tick")-(long)Tick(r);if(index<0 || index>=frames.Length){historyDifference="outside returned frames";break;}string d=(string)compare.Invoke(null,new[]{frames.GetValue((int)index),frame});compared++;if(d!=null){historyDifference="tick="+Get(frame,"Tick")+" "+d;break;}}
                    }
                    finally{impactObservation=false;receiving=prior;}
                    a.impactProof.Add(Csv(a.Phase,Tick(r),tick,age,frames.Length,difference,compared,historyDifference,Dump(expected),Dump(current)));
                }
            }
            finally{receiving=null;firstDifference=null;}
        }
        private static void Difference(object expected,object actual,string __result)
        {
            var a=active;if(a==null || __result==null)return;if(receiving!=null && firstDifference!=null)return;
            if(receiving!=null)firstDifference=__result;
            a.differences.Add(Csv(a.Phase,a.Frame,impactObservation?"impact-current-observation":receiving==null?"reuse":"receive",Get(expected,"Tick"),Get(actual,"Tick"),__result,Dump(expected),Dump(actual)));
        }
        private static string Dump(object value)
        {return string.Join(";",value.GetType().GetFields(Flags).Where(f=>!f.IsStatic).Select(f=>f.Name+"="+Join(f.GetValue(value))));}
        private static string Join(object value)
        {if(value==null)return "null";if(value is string)return (string)value;if(value is IEnumerable list)return "["+string.Join("|",list.Cast<object>().Select(Join))+"]";return Convert.ToString(value,CultureInfo.InvariantCulture);}
        private static string Missing(object value,int[] owned)=>value==null?"":string.Join("|",((IEnumerable)value).Cast<int>().Except(owned));
        private void Save(string name,byte[] bytes){if(bytes==null)return;if(sampleBytes+bytes.Length>64*1024*1024)throw new InvalidOperationException("Bounded trace evidence capacity.");samples.Add(new KeyValuePair<string,byte[]>(name,bytes));sampleBytes+=bytes.Length;}
        private static string Rng()
        {var random=Main.rand;if(random==null)return "null";uint hash=2166136261;foreach(int value in (int[])Get(random,"SeedArray"))unchecked{hash=(hash^(uint)value)*16777619;}return Get(random,"inext")+":"+hash.ToString("X8",CultureInfo.InvariantCulture);}
        private static void NpcStart(NPC __instance){currentNpc=__instance.whoAmI;if(active!=null && currentNpc==active.Selected)active.SelectedRngBefore=Rng();}
        private static void NpcEnd(){if(active!=null && currentNpc==active.Selected)active.SelectedRngAfter=Rng();currentNpc=-1;}
        private static void ProjectileStart(Projectile __instance){currentProjectile=__instance.whoAmI;}
        private static void ProjectileEnd(){currentProjectile=-1;}
        private static void Birth(int __result)
        {var a=active;if(a==null || __result<0 || __result>=Main.maxProjectiles)return;var p=Main.projectile[__result];a.events.Add(Csv(a.Phase,a.Frame,Main.GameUpdateCount,"projectile-birth",currentNpc,currentProjectile,__result,p.type,(uint)p.key,p.owner,p.penetrate,""));}
        private static void NpcBirth(int __result)
        {var a=active;if(a==null || __result<0 || __result>=Main.maxNPCs)return;var n=Main.npc[__result];a.events.Add(Csv(a.Phase,a.Frame,Main.GameUpdateCount,"npc-birth",currentNpc,currentProjectile,__result,n.type,n.generation,n.target,n.life,""));}
        private static void Impact(NPC npc)
        {var a=active;if(a==null)return;var pending=Get(a.owner,"pending");var accepted=Get(a.owner,"acceptedRequest");var source=currentProjectile>=0?Main.projectile[currentProjectile]:null;a.events.Add(Csv(a.Phase,a.Frame,Main.GameUpdateCount,"npc-impact",currentNpc,currentProjectile,npc.whoAmI,npc.type,source==null?null:(object)(uint)source.key,source?.owner,npc.life,"sourceType="+source?.type+" sourceAI="+(source==null?"":Join(source.ai))+" pending="+Tick(pending)+" accepted="+Tick(accepted)+" pendingPs="+Join(Get(pending,"Projectiles"))+" acceptedPs="+Join(Get(accepted,"Projectiles"))));}
        private static void CanHit(bool __result)
        {var a=active;if(a==null || currentNpc!=a.Selected)return;if(__result)a.CanHitTrue++;else a.CanHitFalse++;}
        public void Dispose()
        {
            patches.UnpatchAll(patches.Id);active=null;
            File.WriteAllLines(Path.Combine(output,"attack-captures.csv"),captures);File.WriteAllLines(Path.Combine(output,"attack-replies.csv"),replies);
            File.WriteAllLines(Path.Combine(output,"attack-events.csv"),events);File.WriteAllLines(Path.Combine(output,"attack-differences.csv"),differences);
            lock(gate)File.WriteAllLines(Path.Combine(output,"attack-encodings.csv"),encodings);
            File.WriteAllLines(Path.Combine(output,"attack-impact-current-proof.csv"),impactProof);
            foreach(var pair in samples)File.WriteAllBytes(Path.Combine(output,pair.Key),pair.Value);
        }
    }
}
