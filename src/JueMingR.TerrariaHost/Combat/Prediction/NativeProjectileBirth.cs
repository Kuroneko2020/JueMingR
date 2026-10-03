using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // A native callsite, not an ambient AI scope or a forgeable EntitySource,
    // authorizes one factory invocation. Its sole original SetDefaults call
    // gets a separate one-use ticket. Nested/modifier resets cannot borrow it.
    internal static class NativeProjectileBirth
    {
        private struct Origin {internal Entity Parent;internal ulong Site;internal NewProjectileModifier Modifier;}
        private sealed class Factory
        {
            internal Factory Prior;
            internal NativePredictionSession Session;
            internal bool Worker,Reset,Invalid,Completed;
            internal int Result;
            internal NativeImpactProof.Hit Event;
            internal NewProjectileModifier Modifier;
            internal bool ModifierApplied;
        }
        [ThreadStatic]private static Entity actor;
        [ThreadStatic]private static Origin origin,vectorOrigin;
        [ThreadStatic]private static Factory factory;
        [ThreadStatic]private static Projectile reset;
        [ThreadStatic]private static NativePredictionAlignment.ValueHashWriter hash;
        private static readonly MethodInfo[] Factories=typeof(Projectile).GetMethods(BindingFlags.Public|BindingFlags.Static).Where(m=>m.Name=="NewProjectile").ToArray();
        private static readonly MethodInfo Defaults=AccessTools.DeclaredMethod(typeof(Projectile),"SetDefaults");
        private static readonly MethodInfo DungeonModifier=AccessTools.DeclaredMethod(typeof(NewProjectileModifiers),"HardmodeDungeonSkeletonShot");
        private static readonly MethodInfo IchorModifier=AccessTools.DeclaredMethod(typeof(NewProjectileModifiers),"IchorDartUpdatePenetrate");
        private static string patchOwner;
        private static readonly MethodInfo Scalar=Factories.Single(m=>m.GetParameters()[1].ParameterType==typeof(float));
        private static readonly MethodInfo Vector=Factories.Single(m=>m.GetParameters()[1].ParameterType==typeof(Vector2));
        private static bool Demand=>NativeNpcImpact.Collecting || CombatGeometryHooks.PredictionSession?.HasImpactDemand==true;

        internal static void Install(Harmony patches,List<MethodBase> owned,Action<MethodInfo,HarmonyMethod> installCaller=null)
        {
            patchOwner=patches.Id;
            var cost=PredictionPipeProtocol.Measure?System.Diagnostics.Stopwatch.StartNew():null;
            var slow=cost==null?null:new List<KeyValuePair<MethodBase,double>>();
            double discovery=0,installation=0;int methods=0;
            var inventory=new NativeInstructionInventory();
            foreach(Type type in new[]{typeof(NPC),typeof(Projectile)})
            foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
            {
                var calls=inventory.Read(method);bool selected=calls.Any(c=>Factories.Any(c.Calls));
                if(cost!=null){discovery+=cost.Elapsed.TotalMilliseconds;cost.Restart();}
                if(!selected)continue;
                owned?.Add(method);
                if(installCaller==null)patches.Patch(method,transpiler:Hook(nameof(Calls)));
                else installCaller(method,Hook(nameof(Calls)));
                methods++;if(cost!=null){double elapsed=cost.Elapsed.TotalMilliseconds;installation+=elapsed;slow.Add(new KeyValuePair<MethodBase,double>(method,elapsed));cost.Restart();}
            }
            var npc=AccessTools.DeclaredMethod(typeof(NPC),"UpdateNPC");
            var projectile=AccessTools.DeclaredMethod(typeof(Projectile),"Update",new[]{typeof(int)});
            foreach(var method in new[]{npc,projectile})
            {owned?.Add(method);patches.Patch(method,prefix:Hook(nameof(BeginActor)),finalizer:Hook(nameof(EndActor)));}
            owned?.Add(Vector);owned?.Add(Scalar);
            var vector=Hook(nameof(BeginVector));vector.priority=Priority.First;
            patches.Patch(Vector,prefix:vector,finalizer:Hook(nameof(EndVector)),transpiler:Hook(nameof(VectorCall)));
            var scalar=Hook(nameof(BeginFactory));scalar.priority=Priority.First;
            patches.Patch(Scalar,prefix:scalar,finalizer:Hook(nameof(EndFactory)),transpiler:Hook(nameof(ResetCall)));
            if(cost!=null)Console.Error.WriteLine("STARTUP birth-callers="+methods+" discovery-ms="+discovery.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" caller-install-ms="+installation.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" scopes-factories-ms="+cost.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
            if(slow!=null)foreach(var pair in slow.OrderByDescending(p=>p.Value).Take(5))Console.Error.WriteLine("STARTUP birth-caller="+pair.Key.DeclaringType.Name+"."+pair.Key.Name+" install-ms="+pair.Value.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
        }
        private static HarmonyMethod Hook(string name)=>new HarmonyMethod(typeof(NativeProjectileBirth),name);
        private static void BeginActor(Entity __instance,out Entity __state){__state=actor;actor=Demand?__instance:null;}
        private static Exception EndActor(Exception __exception,Entity __state){actor=__state;return __exception;}
        private static void BeginVector(out Origin __state)
        {__state=vectorOrigin;vectorOrigin=origin;origin=default(Origin);if(vectorOrigin.Parent!=null && !EntryIsSafe(Vector,nameof(BeginVector)))vectorOrigin=default(Origin);}
        private static Exception EndVector(Exception __exception,Origin __state){vectorOrigin=__state;return __exception;}
        private static IEnumerable<CodeInstruction> Calls(IEnumerable<CodeInstruction> input,MethodBase __originalMethod)
        {
            var code=input.ToList();int ordinal=0;
            for(int i=0;i<code.Count;i++)
            {
                var called=Factories.FirstOrDefault(code[i].Calls);if(called==null)continue;
                ulong site=14695981039346656037UL;
                foreach(char c in __originalMethod.DeclaringType.FullName+"."+__originalMethod+"#"+ordinal++)unchecked{site=(site^c)*1099511628211UL;}
                var parent=new CodeInstruction(OpCodes.Ldarg_0);parent.MoveLabelsFrom(code[i]);parent.MoveBlocksFrom(code[i]);
                code.Insert(i++,parent);code.Insert(i++,new CodeInstruction(OpCodes.Ldc_I8,unchecked((long)site)));
                code[i].opcode=OpCodes.Call;code[i].operand=AccessTools.Method(typeof(NativeProjectileBirth),called==Scalar?nameof(FromActor):nameof(FromActorVector));
            }
            return code;
        }
        private static IEnumerable<CodeInstruction> VectorCall(IEnumerable<CodeInstruction> input)
        {return Replace(input,Scalar,nameof(FromVector));}
        private static IEnumerable<CodeInstruction> ResetCall(IEnumerable<CodeInstruction> input)
        {
            var code=Replace(Replace(input,Defaults,nameof(ResetFromFactory)),AccessTools.DeclaredMethod(typeof(NewProjectileModifier),"Invoke"),nameof(ApplyModifier)).ToList();
            for(int i=0;i<code.Count;i++)if(code[i].opcode==OpCodes.Ret)
            {
                var duplicate=new CodeInstruction(OpCodes.Dup);duplicate.MoveLabelsFrom(code[i]);duplicate.MoveBlocksFrom(code[i]);
                code.Insert(i++,duplicate);code.Insert(i++,new CodeInstruction(OpCodes.Ldarg_S,(byte)12));
                code.Insert(i++,new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(NativeProjectileBirth),nameof(Complete))));
            }
            return code;
        }
        private static void Complete(int result,NewProjectileModifier modifier)
        {
            var current=factory;if(current==null)return;
            if(current.Event.Known && (result!=1000 || current.Reset) && (!ReferenceEquals(modifier,current.Modifier) || current.ModifierApplied!=(modifier!=null)))Failed(current,null);
            current.Completed=true;current.Result=result;
        }
        private static int ModifierKind(NewProjectileModifier modifier)
        {
            if(modifier==null)return 0;
            // Delegate.Method alone exposes only the last multicast element.
            // Trust exact original leaf methods, never matching output values
            // or a name. WorldGen has no actor ticket and remains outside this
            // proof even though it also uses a native modifier.
            if(modifier.Target!=null || modifier.GetInvocationList().Length!=1)return -1;
            var method=modifier.Method;int kind=method==DungeonModifier?1:method==IchorModifier?2:-1;
            if(kind<0)return -1;
            var patches=Harmony.GetPatchInfo(method);
            return patches==null || patches.Prefixes.Count==0 && patches.Postfixes.Count==0 && patches.Transpilers.Count==0 && patches.Finalizers.Count==0?kind:-1;
        }
        private static void ApplyModifier(NewProjectileModifier modifier,Projectile projectile)
        {
            var current=factory;
            // Lower-priority factory prefixes run after BeginFactory and can
            // replace its argument. Authenticate the delegate actually called
            // by the original body, while preserving its effects/exceptions.
            if(current!=null && current.Event.Known)
            {
                if(current.ModifierApplied || !ReferenceEquals(current.Modifier,modifier) || !ReferenceEquals(current.Event.Source,projectile) || ModifierKind(modifier)<=0)Failed(current,null);
                else current.ModifierApplied=true;
            }
            modifier(projectile);
        }
        private static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> input,MethodInfo method,string replacement)
        {
            var code=input.ToList();var calls=code.Where(c=>c.Calls(method)).ToArray();
            if(calls.Length!=1)throw new InvalidOperationException("Native projectile birth callsite is not unique: "+method.Name);
            calls[0].opcode=OpCodes.Call;calls[0].operand=AccessTools.Method(typeof(NativeProjectileBirth),replacement);return code;
        }
        private static int FromActor(IEntitySource source,float x,float y,float vx,float vy,int type,int damage,float knockBack,int owner,float ai0,float ai1,float ai2,NewProjectileModifier modifier,Entity parent,ulong site)
        {
            var prior=origin;origin=Demand && ReferenceEquals(actor,parent) && EntryIsSafe(Scalar,nameof(BeginFactory))?new Origin{Parent=parent,Site=site,Modifier=modifier}:default(Origin);
            try{return Projectile.NewProjectile(source,x,y,vx,vy,type,damage,knockBack,owner,ai0,ai1,ai2,modifier);}
            finally{origin=prior;}
        }
        private static int FromActorVector(IEntitySource source,Vector2 position,Vector2 velocity,int type,int damage,float knockBack,int owner,float ai0,float ai1,float ai2,NewProjectileModifier modifier,Entity parent,ulong site)
        {
            var prior=origin;origin=Demand && ReferenceEquals(actor,parent) && EntryIsSafe(Vector,nameof(BeginVector)) && EntryIsSafe(Scalar,nameof(BeginFactory))?new Origin{Parent=parent,Site=site,Modifier=modifier}:default(Origin);
            try{return Projectile.NewProjectile(source,position,velocity,type,damage,knockBack,owner,ai0,ai1,ai2,modifier);}
            finally{origin=prior;}
        }
        private static int FromVector(IEntitySource source,float x,float y,float vx,float vy,int type,int damage,float knockBack,int owner,float ai0,float ai1,float ai2,NewProjectileModifier modifier)
        {
            var prior=origin;origin=vectorOrigin;vectorOrigin=default(Origin);
            try{return Projectile.NewProjectile(source,x,y,vx,vy,type,damage,knockBack,owner,ai0,ai1,ai2,modifier);}
            finally{origin=prior;}
        }
        private static void BeginFactory(IEntitySource __0,float __1,float __2,float __3,float __4,int __5,int __6,float __7,int __8,float __9,float __10,float __11,NewProjectileModifier __12,out Factory __state)
        {
            var ticket=origin;origin=default(Origin);__state=null;
            if(!Demand)return;
            if(ticket.Parent!=null && !EntryIsSafe(Scalar,nameof(BeginFactory)))ticket=default(Origin);
            var value=new Factory{Prior=factory,Session=CombatGeometryHooks.PredictionSession,Worker=NativeNpcImpact.Collecting};
            __state=value;factory=value;
            try
            {
                var parent=__0 as EntitySource_Parent;
                if(ticket.Parent==null || !ReferenceEquals(ticket.Parent,actor) || parent==null || !ReferenceEquals(parent.Entity,ticket.Parent) || !ReferenceEquals(ticket.Modifier,__12))return;
                int modifierKind=ModifierKind(__12);if(modifierKind<0)return;
                value.Modifier=__12;
                var e=new NativeImpactProof.Hit{Kind=1,Tick=Main.GameUpdateCount,Known=true,Parent=ticket.Parent,Origin= ticket.Site,TargetSlot=-1,SharedSlot=-1};
                var npc=ticket.Parent as NPC;var shot=ticket.Parent as Projectile;
                if(npc!=null){e.ParentKind=1;e.ParentSlot=npc.whoAmI;e.ParentType=npc.type;e.ParentGeneration=npc.generation;}
                else if(shot!=null){e.ParentKind=2;e.ParentSlot=shot.whoAmI;e.ParentType=shot.type;e.ParentGeneration=NativeImpactProof.Generation(shot.whoAmI);e.ParentKey=(uint)shot.key;e.ParentOwner=shot.owner;}
                else return;
                var w=Writer();w.Reset();w.Write(__1);w.Write(__2);w.Write(__3);w.Write(__4);w.Write(__5);w.Write(__6);w.Write(__7);w.Write(__8);w.Write(__9);w.Write(__10);w.Write(__11);
                // Final BirthState includes the actual timeLeft/penetration;
                // this tag also binds which original modifier produced them.
                if(modifierKind!=0)w.Write(modifierKind);
                e.Signature=w.Hash;value.Event=e;
            }
            catch(Exception error){Failed(value,error);}
        }
        private static void ResetFromFactory(Projectile value,int type)
        {
            var current=factory;
            bool trusted=current!=null && current.Event.Known && !current.Reset;
            // SetDefaults also has a prefix boundary: an earlier recursive
            // reset followed by a suppressed exception must not borrow this
            // ticket or bypass the reset observer entirely.
            if(trusted && !EntryIsSafe(Defaults,"ResetSource",typeof(NativeNpcImpact)))
            {Failed(current,null);trusted=false;}
            if(trusted)
            {
                current.Event.Source=value;current.Event.SourceSlot=value.whoAmI;current.Event.SourceKey=(uint)value.key;
                current.Event.SourceGeneration=NativeImpactProof.Generation(value.whoAmI);
            }
            var prior=reset;reset=trusted?value:null;
            try{value.SetDefaults(type);}
            finally{reset=prior;}
        }
        internal static bool ConsumeReset(Projectile value)
        {
            var ticket=reset;reset=null;
            if(ticket==null || !ReferenceEquals(ticket,value) || factory==null || factory.Reset)return false;
            if(!EntryIsSafe(Defaults,"ResetSource",typeof(NativeNpcImpact))){Failed(factory,null);return false;}
            factory.Reset=true;return true;
        }
        private static Exception EndFactory(int __result,Exception __exception,Factory __state)
        {
            if(__state==null)return __exception;
            factory=__state.Prior;
            try
            {
                var e=__state.Event;
                if(!e.Known)return __exception;
                if(__state.Completed && __state.Result==1000 && !__state.Reset)return __exception;
                // A plugin finalizer may suppress an exception. Only reaching
                // the original body's ret proves initialization completed.
                if(__exception!=null || __state.Invalid || !__state.Reset || !__state.Completed || __state.Result!=__result || __result!=e.SourceSlot || __result<0 || __result>=Main.maxProjectiles || !ReferenceEquals(Main.projectile[__result],e.Source)
                    || (uint)e.Source.key!=e.SourceKey || NativeImpactProof.Generation(__result)!=e.SourceGeneration
                    || e.Source.key.Spawner!=Main.myPlayer || e.Source.key.Index!=__result || e.Source.key.Generation!=(e.SourceGeneration&0x3fff))
                {Failed(__state,__exception);return __exception;}
                e.SourceType=e.Source.type;e.SourceOwner=e.Source.owner;
                bool prior=__state.Worker && NativePredictionPurpose.Pause();
                try{var w=Writer();w.Hash=e.Signature;NativePredictionAlignment.BirthState(w,e.Source);e.Signature=w.Hash;}
                finally{if(__state.Worker)NativePredictionPurpose.Resume(prior);}
                if(__state.Worker)NativeNpcImpact.RecordBirth(e);
                __state.Session?.ObserveProjectileBirth(e);
            }
            catch(Exception error){Failed(__state,error);}
            return __exception;
        }
        private static void Failed(Factory value,Exception error)
        {
            if(error is OutOfMemoryException)throw error;
            value.Invalid=true;if(value.Worker)NativeNpcImpact.BirthFailure();value.Session?.ObserveImpactFailure();
        }
        private static NativePredictionAlignment.ValueHashWriter Writer()=>hash??(hash=new NativePredictionAlignment.ValueHashWriter());
        private static bool EntryIsSafe(MethodInfo method,string consumer,Type declaring=null)
        {
            // Priority.First is not exclusive: an earlier foreign prefix could
            // recursively consume a caller's ticket and then skip that caller.
            // Recheck the live patch set before issuing and consuming a ticket;
            // ambiguous ordering executes normally, without reuse authority.
            var patches=Harmony.GetPatchInfo(method);if(patches==null)return false;int own=0;
            foreach(var p in patches.Prefixes)
            {
                if(p.owner==patchOwner && p.PatchMethod.DeclaringType==(declaring??typeof(NativeProjectileBirth)) && p.PatchMethod.Name==consumer)
                {if(p.priority!=Priority.First || p.after.Length!=0)return false;own++;}
                else if(p.priority>=Priority.First || p.before.Length!=0)return false;
            }
            return own==1;
        }
    }
}
