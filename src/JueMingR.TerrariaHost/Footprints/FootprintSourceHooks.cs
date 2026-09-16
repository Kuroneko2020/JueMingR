using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Footprints
{
    internal sealed class FootprintSourceHooks : IDisposable
    {
        private const string Owner = "JueMingR.Footprints.Source";
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static FootprintSourceHooks current;
        private readonly Harmony harmony = new Harmony(Owner);
        private readonly List<MethodInfo> methods = new List<MethodInfo>();
        private readonly HostFootprints host;
        internal FootprintSourceHooks(HostFootprints host) { this.host = host; }
        internal bool Ready { get; private set; }
        internal string Error { get; private set; }
        internal void Install()
        {
            try
            {
                if (current != null || typeof(Main).Module.ModuleVersionId != new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083")) throw new InvalidOperationException("footprint-source-identity");
                var step = typeof(Main).GetMethod("DoUpdateInWorld_Inner", Flags, null, Type.EmptyTypes, null);
                var teleport = typeof(Player).GetMethod("Teleport", Flags, null, new[] { typeof(Vector2), typeof(int), typeof(int) }, null);
                var spawn = typeof(Player).GetMethods(Flags).SingleOrDefault(m => m.Name == "Spawn" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.Name == "PlayerSpawnContext");
                current = this; Patch(step, nameof(Before), nameof(Completed)); Patch(teleport, nameof(Discontinuity), null); Patch(spawn, nameof(Discontinuity), null); Ready = true;
            }
            catch (Exception e) { Error = e.GetType().Name + ": " + e.Message; Dispose(); }
        }
        private void Patch(MethodInfo method, string prefix, string finalizer)
        {
            if (method == null || method.IsStatic || method.ReturnType != typeof(void) || method.ContainsGenericParameters || method.GetMethodBody() == null) throw new MissingMethodException("footprint-source-shape");
            methods.Add(method); harmony.Patch(method, new HarmonyMethod(typeof(FootprintSourceHooks).GetMethod(prefix, Flags)), finalizer: finalizer == null ? null : new HarmonyMethod(typeof(FootprintSourceHooks).GetMethod(finalizer, Flags)));
            if (!Harmony.GetPatchInfo(method).Owners.Contains(Owner)) throw new InvalidOperationException("footprint-source-not-installed");
        }
        private static void Before(out bool __state)
        {
            __state = false; var owner = current; if (owner == null || !owner.Ready) return;
            try { __state = owner.host.BeforeSimulation(); } catch (Exception e) { owner.Fault(e); }
        }
        private static Exception Completed(Exception __exception, bool __state, bool __runOriginal)
        {
            var owner = current;
            if (owner != null && owner.Ready && __state)
                try { owner.host.AfterSimulation(__exception == null && __runOriginal); } catch (Exception e) { owner.Fault(e); }
            return __exception; // Observation never swallows or replaces a native failure.
        }
        private static void Discontinuity(Player __instance)
        {
            var owner = current; if (owner == null || !owner.Ready || !ReferenceEquals(__instance, Main.LocalPlayer)) return;
            // A native teleport/spawn attempt is a conservative break, even if
            // native code later catches its own failure. Distance is never used.
            owner.host.BreakContinuity();
        }
        private void Fault(Exception e) { Error = e.GetType().Name; Ready = false; host.BreakContinuity(); }
        public void Dispose()
        { Ready = false; if (ReferenceEquals(current, this)) current = null; foreach (var method in methods) harmony.Unpatch(method, HarmonyPatchType.All, Owner); methods.Clear(); }
    }
}
