using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // The unique native PVE callsite supplies a one-use source ticket. A wide
    // Damage/AI scope would misattribute nested environmental/direct damage.
    // Both the original host and isolated worker use this same observation;
    // it changes neither native arguments, return values nor exceptions.
    internal static class NativeNpcImpact
    {
        [ThreadStatic] private static Projectile ticket;
        [ThreadStatic] private static NativePredictionAlignment.ValueHashWriter hash;
        private static bool worker;
        private static long lastProofTick;
        private static int[] workerNpcs;
        private static NativeImpactProof workerProof;
        private static long captureTick;
        internal static bool Collecting=>worker && Main.GameUpdateCount<=lastProofTick;
        internal static string Failure {get;private set;}
        private struct StrikeState
        {
            internal NativePredictionSession Session;
            internal bool Worker,Observed;
            internal NPC Target,Shared;
            internal NativeImpactProof.Hit Hit;
        }
        internal static void Install(Harmony patches,List<MethodBase> owned=null,Action<MethodInfo,HarmonyMethod> installBirthCaller=null)
        {
            var pve=AccessTools.DeclaredMethod(typeof(Projectile),"Damage_PVE_Inner");
            var strike=AccessTools.DeclaredMethod(typeof(NPC),"StrikeNPC");
            var reset=AccessTools.DeclaredMethod(typeof(Projectile),"SetDefaults");
            if(pve==null || strike==null || reset==null)throw new MissingMethodException("NPC impact observation ABI.");
            owned?.Add(pve);owned?.Add(strike);owned?.Add(reset);
            var rewrite=Hook(nameof(StrikeCall));
            // Preserve G11A's existing return-value receipt. Harmony applies
            // this ordering again when either independent owner is repatched.
            rewrite.after=new[]{"JueMingR.Combat.ProjectileReceipts"};
            patches.Patch(pve,transpiler:rewrite);
            var before=Hook(nameof(BeforeStrike));before.priority=Priority.First;
            patches.Patch(strike,prefix:before,finalizer:Hook(nameof(EndStrike)));
            var resetHook=Hook(nameof(ResetSource));resetHook.priority=Priority.First;
            patches.Patch(reset,prefix:resetHook);
            NativeProjectileBirth.Install(patches,owned,installBirthCaller);
        }
        private static HarmonyMethod Hook(string name)=>new HarmonyMethod(typeof(NativeNpcImpact),name);
        private static IEnumerable<CodeInstruction> StrikeCall(IEnumerable<CodeInstruction> input)
        {
            var code=input.ToList();var original=AccessTools.DeclaredMethod(typeof(NPC),"StrikeNPC");
            var calls=Enumerable.Range(0,code.Count).Where(i=>code[i].Calls(original)).ToArray();
            if(calls.Length!=1)throw new InvalidOperationException("NPC impact StrikeNPC call is not unique.");
            int index=calls[0];var source=new CodeInstruction(OpCodes.Ldarg_0);
            source.MoveLabelsFrom(code[index]);source.MoveBlocksFrom(code[index]);
            code.Insert(index,source);code[index+1].opcode=OpCodes.Call;code[index+1].operand=AccessTools.Method(typeof(NativeNpcImpact),nameof(FromProjectile));return code;
        }
        private static int FromProjectile(NPC target,int damage,float knockBack,int direction,bool crit,bool fromNet,int owner,Projectile source)
        {
            var prior=ticket;ticket=source;
            try{return target.StrikeNPC(damage,knockBack,direction,crit,fromNet,owner);}
            finally{ticket=prior;}
        }
        private static void BeforeStrike(NPC __instance,int __0,float __1,int __2,bool __3,bool __4,int __5,out StrikeState __state)
        {
            var source=ticket;ticket=null;__state=default(StrikeState);
            var session=CombatGeometryHooks.PredictionSession;
            // Hooks are installed for the Host lifetime. No-demand calls must
            // return before entity fingerprints, allocations or worker work.
            if(session!=null && !session.HasImpactDemand)session=null;
            bool collect=worker && Main.GameUpdateCount<=lastProofTick;
            if(session==null && !collect)return;
            __state.Session=session;__state.Worker=collect;__state.Target=__instance;
            try
            {
                if(!Live(__instance))return;
                int shared=__instance.realLife;
                if(session!=null && !session.TracksNpcImpact(__instance,shared))session=null;
                if(collect && Array.IndexOf(workerNpcs,__instance.whoAmI)<0 && (shared<0 || Array.IndexOf(workerNpcs,shared)<0))collect=false;
                __state.Session=session;__state.Worker=collect;
                if(session==null && !collect)return;
                NPC root=shared>=0 && shared<Main.maxNPCs?Main.npc[shared]:null;
                bool known=source!=null && source.active && source.whoAmI>=0 && source.whoAmI<Main.maxProjectiles && ReferenceEquals(Main.projectile[source.whoAmI],source)
                    && (!collect || NativeEntityDirectory.CanAdvance(source) && NativeEntityDirectory.CanAdvance(__instance) && (root==null || NativeEntityDirectory.CanAdvance(root)));
                var hit=new NativeImpactProof.Hit{Tick=Main.GameUpdateCount,Known=known,Source=source,SourceSlot=known?source.whoAmI:-1,SourceType=known?source.type:0,SourceOwner=known?source.owner:-1,SourceKey=known?(uint)source.key:0,SourceGeneration=known?NativeImpactProof.Generation(source.whoAmI):0,SourceEpoch=-1,TargetSlot=__instance.whoAmI,SharedSlot=shared};
                __state.Shared=root;__state.Hit=hit;__state.Observed=true;
                // Unknown sources are always conservative in the live owner.
                // Do not read opaque worker pages just to build a certificate.
                if(known)
                {
                    bool prior=collect && NativePredictionPurpose.Pause();
                    try
                    {
                        var w=Writer();w.Reset();NativePredictionAlignment.ImpactState(w,__instance,source);
                        w.Write(__0);w.Write(__1);w.Write(__2);w.Write(__3);w.Write(__4);w.Write(__5);
                        if(root!=null){w.Write(root.type);w.Write(root.netID);w.Write(root.generation);w.Write(root.life);w.Write(root.active);}
                        __state.Hit.Signature=w.Hash;
                    }
                    finally{if(collect)NativePredictionPurpose.Resume(prior);}
                }
            }
            catch(Exception error){ObserveFailure(__state,error);__state.Observed=false;}
        }
        private static Exception EndStrike(int __result,Exception __exception,StrikeState __state)
        {
            if(__exception!=null){if(__state.Session!=null || __state.Worker)ObserveFailure(__state,__exception);return __exception;}
            if(!__state.Observed || __result<=0)return null;
            try
            {
                var hit=__state.Hit;var w=Writer();w.Hash=hit.Signature;
                w.Write(__result);w.Write(__state.Target.life);w.Write(__state.Target.realLife);
                if(__state.Shared!=null)w.Write(__state.Shared.life);hit.Signature=w.Hash;
                if(__state.Worker && NativeImpactProof.Touches(workerNpcs,hit))
                {
                    if(!workerProof.Record(hit,captureTick))Failure="NPC impact source proof.";
                }
                __state.Session?.ObserveNpcImpact(__state.Target,hit);
            }
            catch(Exception error){ObserveFailure(__state,error);}
            return null;
        }
        private static void ObserveFailure(StrikeState state,Exception error)
        {
            if(error is OutOfMemoryException)throw error;
            if(state.Worker)Failure="NPC impact observation failed.";
            state.Session?.ObserveImpactFailure();
        }
        private static void ResetSource(Projectile __instance)
        {
            if(NativeProjectileBirth.ConsumeReset(__instance))return;
            // The native pool reuses objects and SetDefaults can retain key.
            // Remember reconstruction even if all identity values return to
            // their captured values before the next completed update.
            var session=CombatGeometryHooks.PredictionSession;
            if(session?.HasImpactDemand==true)session.ObserveProjectileReset(__instance);
        }
        private static bool Live(NPC n)=>n!=null && n.whoAmI>=0 && n.whoAmI<Main.maxNPCs && ReferenceEquals(Main.npc[n.whoAmI],n);
        private static NativePredictionAlignment.ValueHashWriter Writer()=>hash??(hash=new NativePredictionAlignment.ValueHashWriter());
        internal static void BeginWorker(long tick,int[] npcs,int[] projectiles)
        {workerProof=new NativeImpactProof(projectiles,npcs);captureTick=tick;Failure=null;workerNpcs=npcs;lastProofTick=tick+PredictionWire.MaximumAlignmentAge;worker=true;}
        internal static void EndWorker(){worker=false;ticket=null;workerNpcs=null;}
        internal static void RecordBirth(NativeImpactProof.Hit value)
        {if(!workerProof.RecordBirth(value,captureTick))BirthFailure();}
        internal static void BirthFailure(){Failure="Projectile birth proof.";}
        internal static void WriteWorker(BinaryWriter writer){workerProof.WriteEvents(writer);workerProof=null;}
    }
}
