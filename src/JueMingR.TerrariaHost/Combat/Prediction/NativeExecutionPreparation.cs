using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Terraria;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Compile code in the serving private process. No NPC is selected, no AI
    // is executed, and no world values are captured here. This is deliberately
    // narrower than preparing the whole game or running a battle as warmup.
    internal static class NativeExecutionPreparation
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
        internal static void Run()
        {
            var watch=PredictionPipeProtocol.Measure?Stopwatch.StartNew():null;var pending=new Queue<MethodBase>();var visited=new HashSet<MethodBase>();
            foreach(var type in new[]{typeof(NPC),typeof(Projectile)})
                foreach(var method in type.GetMethods(Flags))
                    if(method.Name=="AI" || method.Name=="UpdateNPC" || method.Name=="Update")pending.Enqueue(method);
            foreach(var type in new[]{typeof(NativePlayerMotion),typeof(NativePredictionAlignment),typeof(NativeDependencyTimeline),typeof(NativeNpcEligibility)})
                foreach(var method in type.GetMethods(Flags))pending.Enqueue(method);
            var inventory=new NativeInstructionInventory();int count=0,bytes=0;
            while(pending.Count!=0)
            {
                var method=pending.Dequeue();if(!visited.Add(method) || method.ContainsGenericParameters || method.IsAbstract)continue;
                var body=method.GetMethodBody();if(body==null)continue;
                RuntimeHelpers.PrepareMethod(method.MethodHandle);count++;bytes+=body.GetILAsByteArray().Length;
                // Constructor preparation is useful, but does not run its
                // initializer or recursively discover unrelated object graphs.
                if(!(method is MethodInfo))continue;
                foreach(var instruction in inventory.Read(method))
                {
                    var called=instruction.operand as MethodBase;
                    if(called!=null && Relevant(called.DeclaringType))pending.Enqueue(called);
                }
            }
            if(watch!=null)Console.Error.WriteLine("PREPARED native-methods="+count+" il-bytes="+bytes+" ms="+watch.Elapsed.TotalMilliseconds.ToString("F3",CultureInfo.InvariantCulture));
        }
        private static bool Relevant(Type type)
        {
            if(type==null)return false;
            if(type.Assembly==typeof(NativeExecutionPreparation).Assembly)
                return type.Namespace==typeof(NativeExecutionPreparation).Namespace && type!=typeof(NativeExecutionPreparation);
            if(type.Assembly!=typeof(NPC).Assembly)return false;
            for(var owner=type;owner!=null;owner=owner.DeclaringType)
                if(owner==typeof(NPC) || owner==typeof(Projectile) || owner==typeof(Collision) || owner==typeof(Entity) || owner==typeof(Dust) || owner==typeof(Gore) || owner==typeof(CombatText) || owner==typeof(Framing) || owner==typeof(Utils))return true;
            return type.Namespace=="JueMingR.PrivatePrediction" || type==typeof(Terraria.Utilities.UnifiedRandom);
        }
    }
}
