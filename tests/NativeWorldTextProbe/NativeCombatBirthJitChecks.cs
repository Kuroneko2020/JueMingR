using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatBirthJitChecks
    {
        // Vanilla's ForceJITOnAssembly prepares methods before Runtime installs
        // Tools. Reproduce just the actual factory/callee seam in this isolated
        // native process; do not run ForceLoad, a game, or a synthetic birth.
        internal static void PrepareBeforeHost()
        {
            Require(!Harmony.HasAnyPatches("JueMingR.Tools"),"factory warm-up precedes all product Tools installation");
            var factories=typeof(Projectile).GetMethods(BindingFlags.Public|BindingFlags.Static).Where(m=>m.Name=="NewProjectile").ToArray();
            var scalar=factories.Single(m=>m.GetParameters()[1].ParameterType==typeof(float));
            var vector=factories.Single(m=>m.GetParameters()[1].ParameterType==typeof(Vector2));
            var channel=typeof(Player).GetMethod("TryUpdateChannel",BindingFlags.Public|BindingFlags.Instance);
            Require(PatchProcessor.GetOriginalInstructions(scalar).Count(i=>i.Calls(channel))==1,"locked native scalar factory has exactly one actual channel call");
            RuntimeHelpers.PrepareMethod(channel.MethodHandle);RuntimeHelpers.PrepareMethod(scalar.MethodHandle);RuntimeHelpers.PrepareMethod(vector.MethodHandle);
            Console.WriteLine("PRISM JIT ORDER original channel/factories prepared before Host; scalarIL="+scalar.GetMethodBody().GetILAsByteArray().Length+" channelIL="+channel.GetMethodBody().GetILAsByteArray().Length+" channelFlags="+channel.GetMethodImplementationFlags()+"; no factory executed, no full ForceLoad");
        }
    }
}
