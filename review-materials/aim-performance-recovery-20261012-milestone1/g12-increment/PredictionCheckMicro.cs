using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
internal static class PredictionCheckMicro
{
    static void Noop(){}
    static long sink;
    static double Calls(Action call,int count){long start=Stopwatch.GetTimestamp();for(int i=0;i<count;i++)call();return (Stopwatch.GetTimestamp()-start)*1e9/Stopwatch.Frequency/count;}
    static void Clock(){sink=Stopwatch.GetTimestamp();}
    static int Main(string[] args)
    {
        var a=Assembly.LoadFrom(args[0]);var type=a.GetType("JueMingR.Features.Combat.CombatPredictionWork",true);var check=(Action)Delegate.CreateDelegate(typeof(Action),type.GetMethod("Check"));
        // This isolated component-only owner is NOT the actual Observation
        // owner. Its long allowance permits a micro loop; no world/candidate,
        // no production threshold/clock/active game is modified.
        object work=Activator.CreateInstance(type,new object[]{60000.0});Action noop=Noop,clock=Clock;
        Calls(noop,10000);Calls(clock,10000);Calls(check,10000);const int n=1000000;
        for(int pass=0;pass<3;pass++)
        {
            double empty=Calls(noop,n),qpc=Calls(clock,n),inactive=Calls(check,n);double active;
            using((IDisposable)type.GetMethod("Enter").Invoke(work,new object[]{1UL}))active=Calls(check,n);
            Console.WriteLine("MICRO n="+n+" pass="+pass+" delegate-noop-ns="+empty.ToString("R")+" qpc-ns="+qpc.ToString("R")+" inactive-Check-ns="+inactive.ToString("R")+" active-Check-ns="+active.ToString("R")+" active-minus-noop-ns="+(active-empty).ToString("R"));
        }
        Console.WriteLine("MICRO standalone delegate loop, not world cost or admission; no claim about inlined call cost. sink="+sink);return 0;
    }
}