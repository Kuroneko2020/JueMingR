using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Exploration;
using Terraria;
using Terraria.Map;

namespace JueMingR.TerrariaHost.Map
{
    internal sealed class ExplorationMapHooks : IDisposable
    {
        private const string Owner = "JueMingR.Exploration.Map";
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private sealed class Binding
        {
            internal readonly WorldMap Map;
            internal readonly ExplorationCounter Counter;
            internal Binding(WorldMap map, ExplorationCounter counter) { Map = map; Counter = counter; }
        }
        private static Binding binding;
        private static ExplorationMapHooks installed;
        private static int batches, batchEpoch;
        private readonly Harmony harmony = new Harmony(Owner);
        private readonly List<MethodInfo> patched = new List<MethodInfo>();
        internal bool Ready { get; private set; }
        internal string Failure { get; private set; }
        internal static bool Loading { get { return Volatile.Read(ref batches) != 0; } }
        internal static int Epoch { get { return Volatile.Read(ref batchEpoch); } }
#if DEBUG
        internal static long ObservationCalls, EnabledCalls, DirtySignals;
#endif
        internal void Install()
        {
            try
            {
                if (installed != null || typeof(Main).Module.ModuleVersionId != new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083")) throw new InvalidOperationException("exploration-map-identity");
                installed = this;
                Patch("Update", new[] { typeof(int), typeof(int), typeof(byte) }, true);
                Patch("SetTile", new[] { typeof(int), typeof(int), typeof(MapTile).MakeByRefType() }, true);
                Patch("UpdateLighting", new[] { typeof(int), typeof(int), typeof(byte) }, true);
                Patch("UpdateType", new[] { typeof(int), typeof(int), typeof(MapTile).MakeByRefType() }, true);
                Patch("Load", Type.EmptyTypes, false); Patch("Clear", Type.EmptyTypes, false); Patch("ClearEdges", Type.EmptyTypes, false);
                Ready = true;
            }
            catch (Exception ex) { Failure = "exploration-map-install: " + ex.GetType().Name; Dispose(); }
        }
        private void Patch(string name, Type[] arguments, bool store)
        {
            var method = typeof(WorldMap).GetMethod(name, Flags, null, arguments, null);
            if (method == null || method.GetMethodBody() == null) throw new MissingMethodException(name);
            patched.Add(method);
            if (store) harmony.Patch(method, transpiler: Hook(nameof(AfterStore)));
            else harmony.Patch(method, prefix: Hook(nameof(BeginBatch)), finalizer: Hook(nameof(EndBatch)));
        }
        private static HarmonyMethod Hook(string name) { return new HarmonyMethod(typeof(ExplorationMapHooks).GetMethod(name, Flags)); }
        internal void Bind(WorldMap map, ExplorationCounter counter)
        { Volatile.Write(ref binding, Ready && map != null && counter != null ? new Binding(map, counter) : null); }
        private static bool Observe(WorldMap map)
        {
#if DEBUG
            Interlocked.Increment(ref ObservationCalls);
#endif
            var current = Volatile.Read(ref binding);
            if (current == null || !current.Counter.Dynamic || !ReferenceEquals(current.Map, map) || Loading) return false;
#if DEBUG
            Interlocked.Increment(ref EnabledCalls);
#endif
            return true;
        }
        private static void Stored(WorldMap map, int x, int y)
        {
            if (!Observe(map)) return; Notify(map, x, y);
        }
        private static void LightingStored(WorldMap map, int x, int y, byte oldLight, byte newLight)
        { if ((oldLight > 0) != (newLight > 0)) Notify(map, x, y); }
        private static void TypeStored(WorldMap map, int x, int y, byte newLight)
        { if (newLight == 0) Notify(map, x, y); }
        private static void Notify(WorldMap map, int x, int y)
        {
            var current = Volatile.Read(ref binding); if (current == null || !ReferenceEquals(current.Map, map)) return;
#if DEBUG
            Interlocked.Increment(ref DirtySignals);
#endif
            current.Counter.Changed(x, y);
        }
        private static void BeginBatch(WorldMap __instance, out ExplorationCounter __state)
        {
            Interlocked.Increment(ref batches); Interlocked.Increment(ref batchEpoch);
            var current = Volatile.Read(ref binding);
            __state = current != null && ReferenceEquals(current.Map, __instance) ? current.Counter : null;
            __state?.BeginBatch();
        }
        private static Exception EndBatch(Exception __exception, ExplorationCounter __state)
        {
            __state?.EndBatch(); Interlocked.Increment(ref batchEpoch); Interlocked.Decrement(ref batches); return __exception;
        }
        private static IEnumerable<CodeInstruction> AfterStore(IEnumerable<CodeInstruction> input, MethodBase __originalMethod, ILGenerator generator)
        {
            var result = new List<CodeInstruction>(); int matches = 0; string name = __originalMethod.Name;
            var light = typeof(MapTile).GetField("Light", Flags); if (light == null || light.FieldType != typeof(byte)) throw new MissingFieldException("MapTile.Light");
            foreach (var instruction in input)
            {
                result.Add(instruction);
                var target = instruction.operand as MethodInfo;
                bool store = name == "UpdateType" ? instruction.opcode == OpCodes.Stobj && Equals(instruction.operand, typeof(MapTile)) :
                    instruction.opcode == OpCodes.Call && target != null && target.Name == "Set" && target.DeclaringType == typeof(MapTile[,]);
                if (!store) continue; matches++;
                // Fixed-store insertion sees existing locals after the native
                // write. The disabled branch precedes every added Light read.
                if (name == "UpdateLighting" || name == "UpdateType")
                {
                    Label done = generator.DefineLabel();
                    result.Add(new CodeInstruction(OpCodes.Ldarg_0)); result.Add(new CodeInstruction(OpCodes.Call, typeof(ExplorationMapHooks).GetMethod(nameof(Observe), Flags)));
                    result.Add(new CodeInstruction(OpCodes.Brfalse, done));
                    result.Add(new CodeInstruction(OpCodes.Ldarg_0)); result.Add(new CodeInstruction(OpCodes.Ldarg_1)); result.Add(new CodeInstruction(OpCodes.Ldarg_2));
                    if (name == "UpdateLighting") { result.Add(new CodeInstruction(OpCodes.Ldloc_0)); result.Add(new CodeInstruction(OpCodes.Ldfld, light)); }
                    result.Add(new CodeInstruction(OpCodes.Ldloc_1)); result.Add(new CodeInstruction(OpCodes.Ldfld, light));
                    result.Add(new CodeInstruction(OpCodes.Call, typeof(ExplorationMapHooks).GetMethod(name == "UpdateLighting" ? nameof(LightingStored) : nameof(TypeStored), Flags)));
                    var end = new CodeInstruction(OpCodes.Nop); end.labels.Add(done); result.Add(end);
                }
                else
                {
                    result.Add(new CodeInstruction(OpCodes.Ldarg_0)); result.Add(new CodeInstruction(OpCodes.Ldarg_1)); result.Add(new CodeInstruction(OpCodes.Ldarg_2));
                    result.Add(new CodeInstruction(OpCodes.Call, typeof(ExplorationMapHooks).GetMethod(nameof(Stored), Flags)));
                }
            }
            if (matches != 1) throw new InvalidOperationException("exploration-store-shape-" + name); return result;
        }
        public void Dispose()
        {
            Ready = false; if (ReferenceEquals(installed, this)) { Volatile.Write(ref binding, null); installed = null; }
            foreach (var method in patched) try { harmony.Unpatch(method, HarmonyPatchType.All, Owner); } catch (Exception ex) { Failure = "exploration-unpatch: " + ex.GetType().Name; } patched.Clear();
        }
    }
}
