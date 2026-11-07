using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Intercept the real six Run boundaries. This proves dispatch/completion
    // and failure propagation, not the assertions inside the intercepted bodies.
    internal static class NativeObservationBoundaryChecks
    {
        private static readonly List<string> reached=new List<string>();
        private static bool failGeometry;
        private sealed class BoundaryFailure : Exception { }
        private static bool Before(MethodBase __originalMethod)
        {
            reached.Add(__originalMethod.DeclaringType.Name);
            if(failGeometry && __originalMethod.DeclaringType==typeof(NativeCombatGeometryChecks))throw new BoundaryFailure();
            return false;
        }
        internal static void Run(object context)
        {
            Type[] bodies={typeof(NativeCombatObservationChecks),typeof(NativeCombatBodyChecks),typeof(NativeCombatTerrainChecks),typeof(NativeCombatPredictionChecks),typeof(NativeCombatEventChecks),typeof(NativeCombatGeometryChecks)};
            var patch=new Harmony("JueMingR.Tests.ObservationFormalBoundary");
            try
            {
                foreach(Type body in bodies)patch.Patch(body.GetMethod("Run",BindingFlags.NonPublic|BindingFlags.Static),prefix:new HarmonyMethod(typeof(NativeObservationBoundaryChecks),nameof(Before)));
                foreach(bool contaminated in new[]{false,true})
                {
                    string previous=Environment.GetEnvironmentVariable("JMR_OBSERVATION_ORACLE_ONLY");
                    try
                    {
                        Environment.SetEnvironmentVariable("JMR_OBSERVATION_ORACLE_ONLY",contaminated?"ownership":null);
                        reached.Clear();failGeometry=false;NativeCombatObservationSuite.Run(context);
                        Require(reached.Count==6,"complete suite reaches six actual business boundaries despite inherited ownership shortcut");
                        for(int i=0;i<bodies.Length;i++)Require(reached[i]==bodies[i].Name,"formal observation ordering remains complete");
                        reached.Clear();failGeometry=true;bool failed=false;
                        try{NativeCombatObservationSuite.Run(context);}catch(BoundaryFailure){failed=true;}
                        Require(failed && reached.Count==6,"failure at formerly skipped real geometry boundary cannot return completion");
                        Console.WriteLine("OBSERVATION boundary contaminated="+contaminated+" reached=6 failure=propagated; intercepted real Run boundaries, body assertions not evaluated");
                    }
                    finally{Environment.SetEnvironmentVariable("JMR_OBSERVATION_ORACLE_ONLY",previous);}
                }
            }
            finally{failGeometry=false;reached.Clear();patch.UnpatchAll(patch.Id);}
        }
    }
}
