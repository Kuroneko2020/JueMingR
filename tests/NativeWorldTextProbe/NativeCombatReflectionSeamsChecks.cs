using System;
using System.Reflection;
using Terraria;
using JueMingR.Features.Combat;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // The two overloaded interfaces only. Each leaf executes its existing
    // Invoke and oracle; this selector does not run a broad combat workload.
    internal static class NativeCombatReflectionSeamsChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static void OldLookupRefuses(Type type,string name)
        {
            bool ambiguous=false;
            try{type.GetMethod(name,Flags);}
            catch(AmbiguousMatchException){ambiguous=true;}
            Require(ambiguous,"The original name-only lookup really is ambiguous: "+type.Name+"."+name);
            Console.WriteLine("REFLECTION NEGATIVE original-name-only="+type.Name+"."+name+" exception=AmbiguousMatchException");
        }
        internal static void Run(object context)
        {
            var assembly=Get(context,"Combat").GetType().Assembly;
            OldLookupRefuses(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true),"LaunchOrigin");
            OldLookupRefuses(assembly.GetType("JueMingR.TerrariaHost.Combat.HostThornAttack",true),"BirthVelocity");
            try
            {
                NativeCombatFixedSkyChecks.RunReflection(context);
                Console.WriteLine("PASS REFLECTION fixed-sky LaunchOrigin old signature/native receive oracle");
                NativeCombatDedupModelChecks.RunReflection(context);
                Console.WriteLine("PASS REFLECTION dedup-model both LaunchOrigin call sites/purity and finite-whip-full oracle");
                NativeCombatDedupReasons.RunReflection(context);
                NativeCombatAmmoChecks.RunReflection(context);
                Console.WriteLine("PASS REFLECTION ammo LaunchOrigin Pewmatic native modified Y/native receive oracle");
                NativeCombatPlacedPulseChecks.RunReflection(context);
                NativeCombatClingerWallChecks.RunReflection(context);
                NativeCombatDedupChecks.RunReflection(context);
            }
            finally
            {
                NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
                NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            }
            Console.WriteLine("PASS REFLECTION SEAMS original-seven plus direct-dedup-reader; exact legacy signatures, actual Invokes, original oracles; broad combat NOT_EVALUATED");
        }
    }
}
