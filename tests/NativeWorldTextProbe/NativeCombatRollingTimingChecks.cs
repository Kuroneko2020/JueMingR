using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;

namespace NativeWorldTextProbe
{
    // Developer localization only. Nested observer patches alter execution,
    // so this ledger is never one of the three accepted cost rounds.
    internal sealed class NativeCombatRollingTimingChecks : IDisposable
    {
        private static readonly Dictionary<MethodBase,int> indices=new Dictionary<MethodBase,int>();
        private static readonly double[] times=new double[3];
        private static readonly int[] calls=new int[3];
        private readonly Harmony harmony;
        private readonly string output;
        private readonly List<string> rows=new List<string>{"phase,frame,readMs,readCalls,playerMs,playerCalls,kernelMs,kernelCalls"};
        internal NativeCombatRollingTimingChecks(object source,string output)
        {
            this.output=output;
            if(Environment.GetEnvironmentVariable("JUEMINGR_ROLLING_PROFILE")!="1")return;
            harmony=new Harmony("JueMingR.Tests.RollingTiming");indices.Clear();
            Patch(source.GetType().GetMethod("Read",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public),0);
            Patch(source.GetType().GetMethod("ReadPlayer",BindingFlags.Static|BindingFlags.NonPublic),1);
            var kernel=source.GetType().GetField("rolling",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(source);
            Patch(kernel.GetType().GetMethod("Prepare"),2);
        }
        private void Patch(MethodInfo method,int index)
        {
            indices[method]=index;
            harmony.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatRollingTimingChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatRollingTimingChecks),nameof(After)));
        }
        private static void Before(MethodBase __originalMethod,out long __state)
        {__state=Stopwatch.GetTimestamp();calls[indices[__originalMethod]]++;}
        private static void After(MethodBase __originalMethod,long __state)
        {times[indices[__originalMethod]]+=(Stopwatch.GetTimestamp()-__state)*1000.0/Stopwatch.Frequency;}
        internal void Begin(){if(harmony==null)return;Array.Clear(times,0,times.Length);Array.Clear(calls,0,calls.Length);}
        internal void End(string phase,int frame)
        {if(harmony==null)return;rows.Add(phase+","+frame+","+times[0].ToString("R",CultureInfo.InvariantCulture)+","+calls[0]+","+times[1].ToString("R",CultureInfo.InvariantCulture)+","+calls[1]+","+times[2].ToString("R",CultureInfo.InvariantCulture)+","+calls[2]);}
        public void Dispose(){if(harmony==null)return;harmony.UnpatchAll(harmony.Id);File.WriteAllLines(Path.Combine(output,"rolling-kernel-timing.csv"),rows);}
    }
}
