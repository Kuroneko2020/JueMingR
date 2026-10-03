using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Utilities;
using JueMingR.Features.Combat;

namespace NativeWorldTextProbe
{
    // Hold only mailbox consumption. Original AI, real worker bytes, every
    // intervening world update and the Session acceptance chain remain real.
    internal static class NativeCombatBirthProofChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static object worker;
        private static bool hold,inject;
        private static string mode;
        private static long birth;
        private static NPC target;
        private static int entryReads;
        private static volatile bool delayPreparation;
        private static int preparationDelay,delayedEncodes,completedDelays;
        private static void DelayEncode()
        {
            // Delay only the first warm-up request. It must finish before
            // capturing the tested arrow, so no injected sleep crosses hold.
            if(delayPreparation && preparationDelay>0 && System.Threading.Interlocked.CompareExchange(ref delayedEncodes,1,0)==0)
                try{System.Threading.Thread.Sleep(preparationDelay);}finally{System.Threading.Volatile.Write(ref completedDelays,1);}
        }
        private static void ReadPatches(MethodBase __0){if(__0.DeclaringType==typeof(NewProjectileModifiers) || __0.DeclaringType==typeof(Projectile) && (__0.Name=="NewProjectile" || __0.Name=="SetDefaults"))entryReads++;}
        private static bool Mailbox(object __instance)=>!hold || !ReferenceEquals(worker,__instance);
        private static void Born(int __result){if(__result==0)birth=Main.GameUpdateCount;}
        private static void Foreign(NPC __instance)
        {
            if(!inject || mode!="foreign-scope" || !ReferenceEquals(__instance,target))return;
            inject=false;Main.projectile[0].active=false;
            Projectile.NewProjectile(new EntitySource_Parent(target),target.Center,new Vector2(-11,0),82,35,0,255);
        }
        private static void Modify(ref NewProjectileModifier __12)
        {
            if(!inject || mode!="modifier-reset")return;
            inject=false;__12=p=>{var key=p.key;int slot=p.whoAmI;p.SetDefaults(p.type);p.key=key;p.whoAmI=slot;};
        }
        private static bool Borrow(IEntitySource __0,float __1,float __2,float __3,float __4,int __5,int __6,float __7,int __8,float __9,float __10,float __11,NewProjectileModifier __12,ref int __result)
        {
            if(!inject || mode!="prefix-borrow")return true;
            inject=false;__result=Projectile.NewProjectile(__0,__1,__2,__3,__4,__5,__6,__7,__8,__9,__10,__11,__12);return false;
        }
        private static void ThrowReset()
        {if(inject && mode=="suppressed-exception"){inject=false;throw new InvalidOperationException("birth factory original exception");}}
        private static Exception Suppress(Exception __exception,ref int __result)
        {if(__exception?.Message=="birth factory original exception"){__result=0;return null;}return __exception;}
        private static void BorrowReset(Projectile __instance,int __0)
        {
            if(!inject || mode!="reset-prefix-borrow")return;
            inject=false;__instance.SetDefaults(__0);throw new InvalidOperationException("birth reset prefix exception");
        }
        private static Exception SuppressReset(Exception __exception)
        {return __exception?.Message=="birth reset prefix exception"?null:__exception;}
        internal static void Run(object context,object native,NpcPredictionCache cache,Action step,string output)
        {
            worker=Get(native,"Worker");var hooks=new Harmony("JueMingR.Tests.BirthProof");
            var rows=new List<string>{"case,capture,birth,arrive,history,events,impact,retired,accepted,worker"};
            var scalar=typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters()[1].ParameterType==typeof(float));
            var trace=new NativeCombatAttackTrace(native,output);
            hooks.Patch(worker.GetType().GetMethod("TryTakeResult",Flags),prefix:Hook(nameof(Mailbox)));
            hooks.Patch(scalar,prefix:Hook(nameof(Modify)),postfix:Hook(nameof(Born)));
            hooks.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:Hook(nameof(Foreign)));
            try
            {
                preparationDelay=int.Parse(Environment.GetEnvironmentVariable("JUEMINGR_BIRTH_PREPARE_DELAY_MS")??"0");
                Require(preparationDelay>=0 && preparationDelay<=1000,"Bounded test-only preparation delay.");
                delayedEncodes=completedDelays=0;
                if(preparationDelay>0)hooks.Patch(native.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeCapturedValues",true).GetMethod("Encode",Flags),prefix:Hook(nameof(DelayEncode)));
                foreach(string scenario in (Environment.GetEnvironmentVariable("JUEMINGR_BIRTH_CASES")??"natural,generation-wrap,foreign-scope,inactive-foreign,replacement,modifier-reset,same-key-reset,prefix-borrow,suppressed-exception,reset-prefix-borrow").Split(','))
                {
                    mode=scenario;hold=inject=false;birth=0;Call(native,"ClearTarget");
                    trace.Phase="birth-prepare";trace.RequiredCapture=-1;
                    NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.hardMode=true;
                    var player=Main.LocalPlayer;player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;
                    player.controlLeft=player.controlRight=player.controlJump=false;player.dead=false;player.statLife=player.statLifeMax=player.statLifeMax2=100000;
                    player.immune=true;player.immuneTime=100000;player.wet=player.lavaWet=player.honeyWet=player.shimmerWet=false;player.fallStart=player.fallStart2=150;
                    Array.Clear(player.buffType,0,player.buffType.Length);Array.Clear(player.buffTime,0,player.buffTime.Length);Array.Clear(player.hurtCooldowns,0,player.hurtCooldowns.Length);
                    for(int i=0;i<10;i++)player.armor[i].TurnToAir();
                    typeof(Main).GetField("_rngs",Flags).SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdatePlayers",new UnifiedRandom(531)},{"UpdateNPCs",new UnifiedRandom(879)},{"UpdateProjectiles",new UnifiedRandom(171)}});
                    var generations=(int[])typeof(Projectile).GetField("slotGenerations",Flags).GetValue(null);if(scenario=="generation-wrap")generations[0]=16382;
                    int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1400,2400,110,Start:16,Target:0);target=Main.npc[slot];target.life=target.lifeMax=100000;
                    for(int i=0;i<33;i++)if(i!=slot){var n=new NPC();n.SetDefaults(678);n.whoAmI=i;n.active=true;n.position=new Vector2(3000+(i<slot?i:i-1)*30,2400-n.height);Main.npc[i]=n;}
                    NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:true,clearLine:false,mouseCenter:true,dummy:true,radius:25));
                    Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
                    Action advance=()=>
                    {
                        NativeCombatModeledImpactChecks.SampleMouse(context,target.Center);step();
                        if(trace.Fault!=null)throw new InvalidOperationException("Birth trace observed a Prepare/observer failure.",trace.Fault);
                        Require(!(bool)Get(Get(context,"CombatObservation"),"pathFailed"),"Host prediction path stays available during birth checks.");
                        Require(ReferenceEquals(worker,Get(native,"Worker")) && !(bool)Get(native,"Failed"),"Same healthy worker throughout birth checks.");
                        // Explicitly observe the same pages needed by the
                        // native town-query closure. This case tests lifetime
                        // acceptance; serial discovery has separate evidence.
                        for(int i=0;i<33;i++)((SortedSet<int>)Get(native,"npcs")).Add(i);
                        ((SortedSet<int>)Get(native,"projectiles")).Add(0);((SortedSet<int>)Get(native,"projectiles")).Add(1);
                    };
                    object pending=null;var wait=Stopwatch.StartNew();double preparationWait=0;
                    delayPreparation=scenario=="generation-wrap";
                    while(wait.Elapsed.TotalSeconds<8)
                    {
                        if(scenario=="generation-wrap" && (Get(native,"acceptedRequest")==null || birth!=0 && (long)Main.GameUpdateCount-birth>=18))
                        {
                            // The wrap is a lifecycle test, not a throughput test.
                            // Finish initial warm-up and get a fresh capture in
                            // the first arrow's window using real replies. Other
                            // preparation ticks keep the normal asynchronous pace.
                            long tick=(long)Main.GameUpdateCount;var busy=Stopwatch.StartNew();
                            while((int)Get(worker,"State")==2 && wait.Elapsed.TotalSeconds<8)System.Threading.Thread.Sleep(1);
                            preparationWait+=busy.Elapsed.TotalMilliseconds;
                            int state=(int)Get(worker,"State");
                            Require((long)Main.GameUpdateCount==tick && ReferenceEquals(worker,Get(native,"Worker")) && !(bool)Get(native,"Failed") && (state==1 || state==3),"Wrap preparation waits only for the real healthy worker; world tick is unchanged: state="+state+" elapsed-ms="+wait.ElapsedMilliseconds+" tick="+tick+"->"+Main.GameUpdateCount);
                        }
                        advance();pending=Get(native,"pending");long since=(long)Main.GameUpdateCount-birth;
                        if(birth!=0 && since>=18 && since<=40 && pending!=null && !(bool)Get(pending,"Retired") && Main.projectile[0].active
                            && (bool)Call(Get(pending,"Impacts"),"Owns",Main.projectile[0]) && (long)Main.GameUpdateCount-(long)Get(pending,"Tick")<=2 && Get(native,"acceptedRequest")!=null)break;
                        pending=null;
                    }
                    delayPreparation=false;
                    Require(pending!=null,"Capture an active original arrow before its next native reuse: "+scenario);
                    if(scenario=="generation-wrap")
                    {
                        Require(generations[0]==16383 && Main.projectile[0].key.Generation==16383,"Capture precedes the first original packed-key wrap: generation="+generations[0]+" birth="+birth+" tick="+Main.GameUpdateCount);
                        Require(preparationDelay==0 || delayedEncodes==1 && System.Threading.Volatile.Read(ref completedDelays)==1 && preparationWait>0,"One real background encoding delay completed entirely during preparation.");
                        File.WriteAllLines(Path.Combine(output,"generation-wrap-preparation.csv"),new[]{"capture,birth,generation,packedGeneration,waitMs,delayMs,delayedEncodes,completedDelays",string.Join(",",Get(pending,"Tick"),birth,generations[0],Main.projectile[0].key.Generation,preparationWait.ToString("F3",System.Globalization.CultureInfo.InvariantCulture),preparationDelay,delayedEncodes,completedDelays)});
                    }
                    hold=true;long capture=(long)Get(pending,"Tick"),priorBirth=birth;int priorGeneration=generations[0];
                    trace.Phase="birth-"+scenario;trace.RequiredCapture=capture;
                    var adversary=new Harmony("JueMingR.Tests.BirthAdversary");
                    if(scenario=="prefix-borrow"){var before=Hook(nameof(Borrow));before.priority=Priority.First+100;adversary.Patch(scalar,prefix:before);}
                    if(scenario=="suppressed-exception")
                    {adversary.Patch(typeof(Projectile).GetMethod("SetDefaults",Flags),postfix:Hook(nameof(ThrowReset)));var after=Hook(nameof(Suppress));after.priority=Priority.First+100;adversary.Patch(scalar,finalizer:after);}
                    if(scenario=="reset-prefix-borrow")
                    {var before=Hook(nameof(BorrowReset));before.priority=Priority.First+100;adversary.Patch(typeof(Projectile).GetMethod("SetDefaults",Flags),prefix:before,finalizer:Hook(nameof(SuppressReset)));}
                    if(scenario=="same-key-reset")
                    {var p=Main.projectile[0];var key=p.key;int owner=p.owner;p.SetDefaults(p.type);p.whoAmI=0;p.owner=owner;p.key=key;}
                    if(scenario=="inactive-foreign")
                    {Require(!Main.projectile[1].active,"Captured inactive page is available.");int born=Projectile.NewProjectile(new EntitySource_Parent(target),target.Center,new Vector2(-11,0),82,35,0,255);Require(born==1,"External vector factory uses the captured inactive page.");}
                    if(scenario=="replacement")Main.NoPooling=true;
                    if(scenario=="foreign-scope" || scenario=="modifier-reset" || scenario=="prefix-borrow" || scenario=="suppressed-exception" || scenario=="reset-prefix-borrow")inject=true;
                    for(int i=0;i<55 && birth==priorBirth && scenario!="same-key-reset";i++)advance();
                    if(scenario=="same-key-reset")advance();
                    Main.NoPooling=false;
                    adversary.UnpatchAll(adversary.Id);
                    bool positive=scenario=="natural" || scenario=="generation-wrap";
                    if(positive)
                    {
                        Require(birth>capture && generations[0]==unchecked(priorGeneration+1),"Actual original reuse advances exactly one full generation: capture="+capture+" birth="+birth+" priorBirth="+priorBirth+" generation="+priorGeneration+"->"+generations[0]);
                        if(scenario=="generation-wrap")Require(priorGeneration==16383 && generations[0]==16384 && Main.projectile[0].key.Generation==0,"Original packed key wraps while the full generation remains distinct.");
                        Require(!(bool)Get(pending,"Retired") && (int)Get(pending,"Impact")==0,"Predicted natural birth keeps pending proof alive: "+scenario);
                        Require(((IList)Get(pending,"History")).Count==(long)Main.GameUpdateCount-capture+1,"No intervening history frame is synthesized or missing.");
                        Wire(native,pending);
                    }
                    else Require(((int)Get(pending,"Impact")!=0 || (bool)Get(pending,"LifecycleInvalid")) && (bool)Get(pending,"Retired"),"Unknown/reset input is sticky despite the same source object/key: "+scenario);
                    hold=false;for(int i=0;i<10 && ReferenceEquals(pending,Get(native,"pending"));i++)advance();
                    bool accepted=ReferenceEquals(pending,Get(native,"acceptedRequest"));
                    rows.Add(string.Join(",",scenario,capture,birth,Main.GameUpdateCount,((IList)Get(pending,"History")).Count,((IList)Get(Get(pending,"Impacts"),"hits")).Count,Get(pending,"Impact"),Get(pending,"Retired"),accepted,Get(worker,"State")));
                    Require(accepted==positive,"Actual received proof accepts only the original modeled birth: "+scenario+" reason="+Get(native,"Reason"));
                    if(positive)Require(cache.Read(0)!=null && cache.Read(0).SampleTick==Main.GameUpdateCount && cache.Read(0).Count==121,"Birth-crossing reply publishes real current+120.");
                    Console.WriteLine("BIRTH-PROOF "+scenario+" capture="+capture+" birth="+birth+" history="+((IList)Get(pending,"History")).Count+" accepted="+accepted);
                }
                trace.Dispose();trace=null;
                Call(native,"ClearTarget");NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
                var observer=native.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeProjectileBirth",true);
                observer.GetField("hash",Flags).SetValue(null,null);
                hooks.Patch(typeof(Harmony).GetMethod("GetPatchInfo",Flags),prefix:Hook(nameof(ReadPatches)));entryReads=0;long prior=birth;
                for(int i=0;i<150;i++)step();
                Require(birth>prior,"OFF fixture still performs original projectile births.");
                Require(entryReads==0 && observer.GetField("hash",Flags).GetValue(null)==null && observer.GetField("factory",Flags).GetValue(null)==null,"OFF births neither inspect patch metadata nor allocate a proof writer/factory.");
                Console.WriteLine("BIRTH-PROOF OFF original-birth=true patch-reads=0 writer=false factory=false");
            }
            finally{delayPreparation=false;try{trace?.Dispose();}finally{hold=inject=false;Main.NoPooling=false;worker=null;target=null;hooks.UnpatchAll(hooks.Id);new Harmony("JueMingR.Tests.BirthAdversary").UnpatchAll("JueMingR.Tests.BirthAdversary");File.WriteAllLines(Path.Combine(output,"birth-proof.csv"),rows);}}
        }
        private static void Wire(object native,object request)
        {
            var proof=Get(request,"Impacts");var events=(IList)Get(proof,"hits");Require(events.Count>0,"Real original birth contributes an ordered event.");
            byte[] bytes;using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){proof.GetType().GetMethod("Write",Flags).Invoke(null,new object[]{w,events});bytes=stream.ToArray();}
            Array decoded;using(var r=new BinaryReader(new MemoryStream(bytes)))decoded=(Array)proof.GetType().GetMethod("Read",Flags).Invoke(null,new object[]{r,(long)Get(request,"Tick"),181});
            Require(Call(proof,"Difference",decoded,(long)Main.GameUpdateCount,Get(request,"Npcs"))==null,"Real birth wire round trip.");
            foreach(string field in new[]{"SourceGeneration","SourceEpoch","SourceSlot","SourceType","SourceKey","SourceOwner","ParentKind","ParentSlot","ParentType","ParentGeneration","ParentKey","ParentOwner","ParentEpoch","Origin","Signature"})
            {
                var changed=(Array)decoded.Clone();object e=changed.GetValue(0);var f=e.GetType().GetField(field,Flags);object v=f.GetValue(e);
                f.SetValue(e,v is ulong?(object)((ulong)v+1):v is uint?(object)((uint)v+1):(int)v+1);changed.SetValue(e,0);
                Require(Call(proof,"Difference",changed,(long)Main.GameUpdateCount,Get(request,"Npcs"))!=null,"Changed birth identity/sequence cannot reuse a proof: "+field);
            }
            var extra=Array.CreateInstance(decoded.GetType().GetElementType(),decoded.Length+1);Array.Copy(decoded,extra,decoded.Length);extra.SetValue(decoded.GetValue(0),decoded.Length);
            Require(Call(proof,"Difference",extra,(long)Main.GameUpdateCount,Get(request,"Npcs"))!=null,"An extra birth is not a source-slot set.");
            Require(Call(proof,"Difference",Array.CreateInstance(decoded.GetType().GetElementType(),0),(long)Main.GameUpdateCount,Get(request,"Npcs"))!=null,"A missing birth cannot be hidden by final-state equality.");
            FutureWire(proof,decoded,(long)Get(request,"Tick"));
        }
        private static void FutureWire(object proof,Array actual,long capture)
        {
            var first=actual.GetValue(0);var eventType=first.GetType();
            Func<object,object> copy=value=>RuntimeHelpers.GetObjectValue(value);
            Action<object,string,object> set=(value,name,data)=>eventType.GetField(name,Flags).SetValue(value,data);
            Func<object[],Array> array=values=>{var result=Array.CreateInstance(eventType,values.Length);for(int i=0;i<values.Length;i++)result.SetValue(values[i],i);return result;};
            Action<bool,object[]> decode=(valid,values)=>
            {
                var list=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(eventType));foreach(var value in values)list.Add(value);
                byte[] bytes;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){proof.GetType().GetMethod("Write",Flags).Invoke(null,new object[]{writer,list});bytes=stream.ToArray();}
                bool rejected=false;try{using(var reader=new BinaryReader(new MemoryStream(bytes)))proof.GetType().GetMethod("Read",Flags).Invoke(null,new object[]{reader,capture,181});}
                catch(TargetInvocationException error){if(!(error.InnerException is InvalidDataException))throw;rejected=true;}
                Require(rejected!=valid,"Wire decoder rejects malformed lifetime references, and accepts the valid control.");
            };
            var hit=copy(first);set(hit,"Kind",(byte)0);set(hit,"TargetSlot",16);set(hit,"SourceEpoch",0);
            decode(true,new[]{first,hit});
            foreach(int epoch in new[]{1,2}){var changed=copy(hit);set(changed,"SourceEpoch",epoch);decode(false,new[]{first,changed});}
            var child=copy(first);int generation=(int)Get(first,"SourceGeneration")+1;
            set(child,"SourceEpoch",1);set(child,"SourceGeneration",generation);set(child,"SourceKey",((uint)Get(first,"SourceKey")&0x3ffffu)|((uint)(generation&0x3fff)<<18));
            set(child,"ParentKind",2);set(child,"ParentEpoch",0);
            foreach(string field in new[]{"Slot","Type","Generation","Key","Owner"})set(child,"Parent"+field,Get(first,"Source"+field));
            decode(true,new[]{first,child});
            foreach(int epoch in new[]{1,2}){var changed=copy(child);set(changed,"ParentEpoch",epoch);decode(false,new[]{first,changed});}
            var wrongParent=copy(child);set(wrongParent,"ParentKey",(uint)Get(child,"ParentKey")+1);decode(false,new[]{first,wrongParent});
            // These are protocol-negative copies, never Session input. The
            // valid future control proves we check beyond the elapsed prefix.
            var future=copy(first);set(future,"Tick",capture+PredictionMaximumAge);
            Require((bool)Call(proof,"ValidTimeline",array(new[]{future})),"Valid captured future birth control.");
            foreach(string field in new[]{"SourceGeneration","SourceSlot","ParentGeneration","ParentSlot"})
            {var changed=copy(future);set(changed,field,(int)Get(changed,field)+1);Require(!(bool)Call(proof,"ValidTimeline",array(new[]{changed})),"Bad future captured binding is rejected: "+field);}
            Console.WriteLine("BIRTH-WIRE valid-controls=3 malformed-decoder=5 malformed-future=4");
        }
        private const int PredictionMaximumAge=60;
        private static HarmonyMethod Hook(string name)=>new HarmonyMethod(typeof(NativeCombatBirthProofChecks).GetMethod(name,Flags));
        private static object Get(object value,string name){if(value==null)return null;var f=value.GetType().GetField(name,Flags);return f!=null?f.GetValue(value):value.GetType().GetProperty(name,Flags).GetValue(value);}
        private static object Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Flags).Invoke(value,args);
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
