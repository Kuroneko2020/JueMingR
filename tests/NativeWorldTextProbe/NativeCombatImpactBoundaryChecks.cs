using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using JueMingR.Features.Combat;

namespace NativeWorldTextProbe
{
    // Negative inputs use original Damage/Strike against real sealed Session
    // requests. Holding the real mailbox changes consumption time only; no
    // result bytes, history frame or acceptance decision is replaced.
    internal static class NativeCombatImpactBoundaryChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static object heldWorker;
        private static bool hold,nested;
        private static string nestedMode;
        private static Projectile second;
        private static NPC background;
        private static bool Mailbox(object __instance)=>!hold || !ReferenceEquals(heldWorker,__instance);
        private static void NestedStrike(NPC __instance)
        {
            if(nested || nestedMode==null || ReferenceEquals(__instance,background))return;
            nested=true;
            try{if(nestedMode=="nested-direct")background.StrikeNPC(20,0,1);else Hit(second,background);}
            finally{nested=false;}
        }
        private static void ThrowAfterStrike(NPC __instance)
        {if(nestedMode=="exception"){__instance.realLife=-1;throw new InvalidOperationException("impact fixture original exception");}}
        internal static void Run(object context,object native,NpcPredictionCache cache,Action step,string output)
        {
            var assembly=native.GetType().Assembly;var observer=assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeNpcImpact",true);
            var hooks=new Harmony("JueMingR.Tests.ImpactBoundaries");heldWorker=Get(native,"Worker");
            var rows=new List<string>{"case,capture,events,impact,retired,rejected,reason"};
            hooks.Patch(heldWorker.GetType().GetMethod("TryTakeResult",Flags),prefix:Hook(nameof(Mailbox)));
            hooks.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags),prefix:Hook(nameof(NestedStrike)),postfix:Hook(nameof(ThrowAfterStrike)));
            try
            {
                NativeCombatWorkerImmunityChecks.CombatFont(output);
                for(int i=0;i<Main.combatText.Length;i++)Main.combatText[i]=new CombatText();
                Repatch(context,native);
                foreach(string scenario in new[]{"captured-extra","other-type-background","same-owner-two","shared-root","hit-heal","owner-changed","key-changed","uncaptured","direct","same-key-reset","inactive-birth","rejected-hit","nested-direct","nested-projectile","exception"})
                {
                    hold=false;nestedMode=null;Call(native,"ClearTarget");NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();
                    var player=Main.LocalPlayer;for(int i=0;i<10;i++)player.armor[i].TurnToAir();Array.Clear(player.buffType,0,player.buffType.Length);Array.Clear(player.buffTime,0,player.buffTime.Length);
                    player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;player.controlLeft=player.controlRight=player.controlJump=false;player.dead=false;player.immune=true;player.immuneTime=100000;
                    player.wet=player.honeyWet=player.lavaWet=player.shimmerWet=false;player.fallStart=player.fallStart2=150;player.statLife=player.statLifeMax=player.statLifeMax2=500;
                    Array.Clear(player.hurtCooldowns,0,player.hurtCooldowns.Length);
                    var target=Npc(16,1500);background=Npc(0,700);
                    if(scenario=="shared-root")target.realLife=background.whoAmI;
                    Projectile first=Shot(1,500);second=Shot(14,550);
                    var shots=scenario=="inactive-birth"?new[]{first.whoAmI,second.whoAmI,2}:new[]{first.whoAmI,second.whoAmI};
                    NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:true,clearLine:false,mouseCenter:true,dummy:true,radius:25));
                    Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
                    Action advance=()=>{NativeCombatModeledImpactChecks.SampleMouse(context,target.Center);step();};
                    object accepted=null,pending=null;var timer=Stopwatch.StartNew();
                    do
                    {
                        advance();((SortedSet<int>)Get(native,"npcs")).Add(0);foreach(int slot in shots)((SortedSet<int>)Get(native,"projectiles")).Add(slot);
                        accepted=Get(native,"acceptedRequest");
                    }while(!Owns(accepted,shots) && timer.Elapsed.TotalSeconds<8);
                    Require(Owns(accepted,shots),"Real accepted request contains both original sources and captured background: "+scenario+" reason="+Get(native,"Reason")+" selected="+Get(Get(Get(context,"CombatObservation"),"Selection"),"Target")+" dead="+player.dead);
                    hold=true;for(int i=0;i<15;i++){advance();pending=Get(native,"pending");if(Owns(pending,shots) && Main.GameUpdateCount>(long)Get(pending,"Tick"))break;}
                    Require(Owns(pending,shots) && !(bool)Get(pending,"Retired"),"Real pending request remains sealed before extra input: "+scenario);
                    long capture=(long)Get(pending,"Tick"),rejections=(long)Get(native,"Rejected");
                    if(scenario=="owner-changed")first.owner=1;
                    if(scenario=="key-changed")first.key=second.key;
                    if(scenario=="uncaptured")first=Shot(1,600);
                    if(scenario=="same-key-reset")
                    {var key=first.key;int slot=first.whoAmI;first.SetDefaults(first.type);first.whoAmI=slot;first.owner=0;first.key=key;}
                    else if(scenario=="inactive-birth")
                    {Require(!Main.projectile[2].active,"A sealed request owns an inactive page.");var birth=Shot(1,600);Require(birth.whoAmI==2,"Original NewProjectile reuses that inactive object.");}
                    else if(scenario=="direct")Require(target.StrikeNPC(20,0,1)>0,"Unknown direct native hit succeeds.");
                    else if(scenario=="rejected-hit")
                    {target.dontTakeDamage=true;int life=target.life;first.Center=target.Center;first.Damage();Require(target.life==life,"Native rejected collision does not count as a hit.");target.dontTakeDamage=false;}
                    else if(scenario=="exception")
                    {
                        target.realLife=background.whoAmI;nestedMode=scenario;bool escaped=false;
                        try{Hit(first,target);}catch(InvalidOperationException error){escaped=error.Message=="impact fixture original exception";}finally{nestedMode=null;}
                        Require(escaped,"Original exception propagates without being swallowed by observation.");
                    }
                    else
                    {
                        int life=target.life;if(scenario.StartsWith("nested-",StringComparison.Ordinal))nestedMode=scenario;
                        int local=Main.myPlayer;
                        // Original Damage only processes its local owner's
                        // PVE. Exercise changed ownership under that native
                        // authority, then return to the captured player.
                        try{if(scenario=="owner-changed")Main.myPlayer=1;Hit(scenario=="other-type-background"?second:first,scenario=="other-type-background"?background:target);}
                        finally{Main.myPlayer=local;nestedMode=null;}
                        if(scenario=="same-owner-two")Hit(second,background);
                        if(scenario=="hit-heal")target.life=life;
                    }
                    Require(observer.GetField("ticket",Flags).GetValue(null)==null,"Source ticket never escapes original call or exception.");
                    var events=(IList)Get(Get(pending,"Impacts"),"hits");bool invalid=(int)Get(pending,"Impact")!=0;
                    bool conservative=scenario=="owner-changed" || scenario=="key-changed" || scenario=="uncaptured" || scenario=="direct" || scenario=="same-key-reset" || scenario=="nested-direct" || scenario=="exception";
                    Require(invalid==conservative,"Captured identity versus external input boundary: "+scenario);
                    bool noHit=scenario=="rejected-hit" || scenario=="inactive-birth";
                    if(noHit)Require(events.Count==0 && (int)Get(accepted,"Impact")==0,"Rejected damage or inactive-page birth is not an old-source impact.");
                    else Require((int)Get(accepted,"Impact")!=0,"Every actual impact still withdraws the old accepted result: "+scenario);
                    if(!conservative && !noHit)
                    {
                        int expected=scenario=="same-owner-two" || scenario=="nested-projectile"?2:1;
                        Require(events.Count==expected,"Every successful native source event is retained: "+scenario);
                        var proof=Get(pending,"Impacts");var empty=Array.CreateInstance(events[0].GetType(),0);
                        Require((string)Call(proof,"Difference",empty,Main.GameUpdateCount,Get(pending,"Npcs"))=="extra NPC impact","An extra captured-source hit cannot disappear after healing.");
                        if(scenario=="shared-root")Require((int)Get(events[0],"SharedSlot")==0 && background.life<10000,"Successful realLife damage retains the pre-hit shared owner.");
                        Wire(assembly,proof,events,(int[])Get(pending,"Npcs"));
                    }
                    if(!noHit)
                    {
                        advance();Require(((bool)Get(pending,"Retired"))==conservative,"Pending retirement keeps full modeled history while invalid inputs stop: "+scenario);
                        hold=false;for(int i=0;i<45 && ReferenceEquals(pending,Get(native,"pending"));i++)advance();
                        Require(!ReferenceEquals(pending,Get(native,"pending")) && !ReferenceEquals(pending,Get(native,"acceptedRequest")) && (long)Get(native,"Rejected")>rejections,"Actual delayed reply rejects extra input: "+scenario);
                    }
                    rows.Add(string.Join(",",scenario,capture,events.Count,invalid,Get(pending,"Retired"),(long)Get(native,"Rejected")-rejections,Get(native,"Reason")));
                    Require(!(bool)Get(native,"Failed") && ReferenceEquals(heldWorker,Get(native,"Worker")),"Ordinary boundary refusal keeps the same worker healthy.");
                    Console.WriteLine("IMPACT-BOUNDARY "+scenario+" events="+events.Count+" conservative="+invalid+" same-worker=true");
                }
                hold=false;Call(native,"ClearTarget");NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
                observer.GetField("hash",Flags).SetValue(null,null);
                background.life=10000;Require(background.StrikeNPC(20,0,1)>0,"OFF direct native hit.");Hit(Shot(1,600),background);
                Require(observer.GetField("hash",Flags).GetValue(null)==null && Get(native,"pending")==null,"OFF direct/projectile hit creates no fingerprint writer or request.");
                Console.WriteLine("IMPACT-BOUNDARY OFF actual-direct-and-projectile=true fingerprint-created=false");
            }
            finally{hold=false;nestedMode=null;heldWorker=null;second=null;background=null;hooks.UnpatchAll(hooks.Id);File.WriteAllLines(Path.Combine(output,"impact-boundaries.csv"),rows);}
        }
        private static void Repatch(object context,object native)
        {
            Call(native,"ClearTarget");NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
            var method=typeof(Projectile).GetMethod("Damage_PVE_Inner",Flags);var patches=Harmony.GetPatchInfo(method).Transpilers;
            var receipt=patches.Single(p=>p.owner=="JueMingR.Combat.ProjectileReceipts");var impact=patches.Single(p=>p.owner=="JueMingR.CombatObservation");
            Action<Patch> add=p=>new Harmony(p.owner).Patch(method,transpiler:new HarmonyMethod(p.PatchMethod){priority=p.priority,before=p.before,after=p.after});
            Action clear=()=>{new Harmony(receipt.owner).Unpatch(method,receipt.PatchMethod);new Harmony(impact.owner).Unpatch(method,impact.PatchMethod);};
            try
            {
                foreach(bool receiptFirst in new[]{false,true})
                {
                    clear();add(receiptFirst?receipt:impact);add(receiptFirst?impact:receipt);
                    NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Hit(Shot(1,500),Npc(16,1500));
                    Require(Harmony.GetPatchInfo(method).Transpilers.Count(p=>p.owner==receipt.owner || p.owner==impact.owner)==2,"Both independent native consumers survive either install order.");
                }
            }
            finally{clear();add(receipt);add(impact);}
            Console.WriteLine("IMPACT-BOUNDARY receipt/impact install-order=both native-hit=true restored=true");
        }
        private static void Wire(Assembly assembly,object actual,IList events,int[] npcs)
        {
            var proof=assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeImpactProof",true);byte[] bytes;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){proof.GetMethod("Write",Flags).Invoke(null,new object[]{writer,events});bytes=stream.ToArray();}
            long tick=(long)Get(events[0],"Tick");
            Array decoded;
            using(var reader=new BinaryReader(new MemoryStream(bytes)))decoded=(Array)proof.GetMethod("Read",Flags).Invoke(null,new object[]{reader,tick-1,181});
            Require(decoded.Length==events.Count && Call(actual,"Difference",decoded,Main.GameUpdateCount,npcs)==null,"Actual native event wire round-trip retains the complete sequence.");
            foreach(string name in new[]{"Tick","SourceSlot","SourceType","SourceOwner","SourceKey","TargetSlot","SharedSlot","Signature"})
            {
                var changed=(Array)decoded.Clone();object hit=changed.GetValue(0);var field=hit.GetType().GetField(name,Flags);object value=field.GetValue(hit);
                field.SetValue(hit,value is long?(object)((long)value+1):value is uint?(object)((uint)value+1):value is ulong?(object)((ulong)value+1):(int)value+1);changed.SetValue(hit,0);
                Require(Call(actual,"Difference",changed,Main.GameUpdateCount+1,npcs)!=null,"Mismatched event field is never accepted: "+name);
            }
            if(decoded.Length>1)
            {var reversed=(Array)decoded.Clone();object first=reversed.GetValue(0);reversed.SetValue(reversed.GetValue(1),0);reversed.SetValue(first,1);Require(Call(actual,"Difference",reversed,Main.GameUpdateCount,npcs)!=null,"Source/victim order remains part of the evidence.");}
            foreach(long capture in new[]{tick,tick-61})
            {
                bool rejected=false;try{using(var reader=new BinaryReader(new MemoryStream(bytes)))proof.GetMethod("Read",Flags).Invoke(null,new object[]{reader,capture,181});}
                catch(TargetInvocationException error){rejected=error.InnerException is InvalidDataException;}Require(rejected,"Events outside completed acceptance history are rejected.");
            }
            foreach(int count in new[]{-1,4097})
            {
                bool rejected=false;try{using(var reader=new BinaryReader(new MemoryStream(BitConverter.GetBytes(count))))proof.GetMethod("Read",Flags).Invoke(null,new object[]{reader,tick-1,181});}
                catch(TargetInvocationException error){rejected=error.InnerException is InvalidDataException;}Require(rejected,"Malformed event capacity is rejected before allocation.");
            }
        }
        private static bool Owns(object request,int[] shots)
        {if(request==null || Array.IndexOf((int[])Get(request,"Npcs"),0)<0)return false;foreach(int slot in shots)if(Array.IndexOf((int[])Get(request,"Projectiles"),slot)<0)return false;return true;}
        private static NPC Npc(int slot,int x)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.life=n.lifeMax=10000;n.position=new Vector2(x,2400-n.height);n.target=0;return n;}
        private static Projectile Shot(int type,int x)
        {int slot=Projectile.NewProjectile(new EntitySource_DebugCommand(),new Vector2(x,2200),Vector2.Zero,type,20,0,0);var p=Main.projectile[slot];p.aiStyle=0;p.tileCollide=false;p.timeLeft=10000;p.penetrate=-1;return p;}
        private static void Hit(Projectile p,NPC n)
        {int life=n.life;p.Center=n.Center;p.velocity=new Vector2(1,0);Array.Clear(n.immune,0,n.immune.Length);p.Damage();Require(n.life<life,"Original projectile damages its actual victim: type="+p.type+" owner="+p.owner+" victim="+n.whoAmI);}
        private static HarmonyMethod Hook(string name)=>new HarmonyMethod(typeof(NativeCombatImpactBoundaryChecks).GetMethod(name,Flags));
        private static object Get(object value,string name){if(value==null)return null;var f=value.GetType().GetField(name,Flags);return f!=null?f.GetValue(value):value.GetType().GetProperty(name,Flags).GetValue(value);}
        private static object Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Flags).Invoke(value,args);
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
