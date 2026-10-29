using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Blueprint
{
    // Observe the locked original's content stores, rather than guesses about
    // a PlaceTile/KillTile radius. This is private to active Blueprint watches:
    // it supplies invalidation, never another world mutation/event platform.
    internal static class BlueprintWorldChangeHooks
    {
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        // A section receiver can visit 200*150 cells in one native call. Its
        // loop does not justify retaining that many Tile identities. Keep a
        // small exact working set plus the latest native read; overflow keeps
        // only current-watch/layer summaries and never becomes a scan limit.
        internal const int MaximumCoordinateAssociations = 256;
        internal const int MaximumRetainedScopes = 16;
        private static readonly Harmony harmony = new Harmony("JueMingR.Blueprint.WorldChanges");
        private static BlueprintWorldChangeObserver owner;
        private static readonly Dictionary<FieldInfo, int> fields = FieldKinds();
        private static readonly MethodInfo arrayGet = typeof(Tile[,]).GetMethod("Get");
        private static readonly MethodInfo arraySet = typeof(Tile[,]).GetMethod("Set");
        private static readonly FieldInfo worldTiles = typeof(Main).GetField("tile", Flags);
        private static readonly FieldInfo worldSections = typeof(Main).GetField("sectionManager", Flags);
        private static readonly MethodInfo safeGet = typeof(Framing).GetMethod("GetTileSafely", Flags, null, new[] { typeof(int), typeof(int) }, null);
        private static readonly Dictionary<short, OpCode> opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)).ToDictionary(c => c.Value);
        private static readonly object poolGate = new object();
        private static readonly List<WeakReference> pools = new List<WeakReference>();
        private static int ownerGeneration, resourceGeneration;
        [ThreadStatic] private static ScopePool scopePool;
        [ThreadStatic] private static WriteScope current;
        [ThreadStatic] private static int depth;
        internal static bool NativeTransformPending { get { return owner != null && WorldGen.TransformingWorld; } }
#if DEBUG
        internal static int DebugHookCount;
        internal static long DebugMappedReads, DebugRecordedWrites;
        internal static readonly List<string> DebugHookTargets = new List<string>();
        internal static long DebugDiscoveryMilliseconds, DebugSelectionMilliseconds, DebugPatchMilliseconds;
        internal static int DebugScopeBuffers { get { return scopePool == null ? 0 : scopePool.Count; } }
        internal static int DebugGlobalScopeBuffers { get { lock (poolGate) { return pools.Sum(p => (p.Target as ScopePool)?.Count ?? 0); } } }
        internal static int DebugMaxCoordinates, DebugMaxDepth;
        internal static long DebugOverflowScopes, DebugFallbackWrites;
#endif

        // Fixed retained slots, rather than a call-depth high-water list. A
        // deep original recursion still runs using ephemeral scopes. Slots
        // can be atomically released from the game thread after the last watch
        // even when a previously used Task thread is already idle.
        private sealed class ScopePool
        {
            internal readonly int Generation, Resources;
            private readonly WriteScope[] slots = new WriteScope[MaximumRetainedScopes];
            internal ScopePool(int generation, int resources) { Generation = generation; Resources = resources; }
            internal WriteScope Take(int at)
            {
                if (at >= slots.Length) return new WriteScope();
                var value = System.Threading.Volatile.Read(ref slots[at]);
                if (value == null) { value = new WriteScope(); System.Threading.Volatile.Write(ref slots[at], value); }
                return value;
            }
            internal void Forget(int at, WriteScope value)
            { if (at < slots.Length) System.Threading.Interlocked.CompareExchange(ref slots[at], null, value); }
            internal void Clear()
            { for (int i = 0; i < slots.Length; i++) System.Threading.Interlocked.Exchange(ref slots[i], null); }
#if DEBUG
            internal int Count { get { int count = 0; for (int i = 0; i < slots.Length; i++) if (System.Threading.Volatile.Read(ref slots[i]) != null) count++; return count; } }
#endif
        }

        // Coordinate maps last only for a native mutation call stack. We wrap
        // the original array read once, retaining only cells intersecting a
        // live dependency. No bbox scan or persistent Tile snapshot is built.
        private sealed class WriteScope
        {
            internal BlueprintWorldChangeObserver Owner;
            internal WriteScope Parent, Root;
            internal readonly Dictionary<Tile, Point> Coordinates = new Dictionary<Tile, Point>();
            internal Tile LastTile;
            internal Point LastPoint;
            internal BlueprintWorldChangeObserver.ReadDependencies OverflowReads;
            internal BlueprintWorldChangeObserver.MutationBatch Batch;
            internal bool Ended, Failed;
            internal bool Touch(int x, int y, ushort changedLayers = 0xff)
            {
                // A native writer can change an extra, non-target layer at a
                // watched coordinate. Filter before allocating a batch or
                // incrementing Pending; the batch rechecks under its lock.
                if (!Owner.HasInterest(x, y, changedLayers)) return false;
                var root = Root;
                if (root.Batch == null) root.Batch = Owner.BeginBatch();
                root.Batch.Touch(new BlueprintProjectionBounds(x, y, 1, 1), changedLayers);
                return true;
            }
            internal bool TouchStore(int x, int y, int kind, int oldValue, int newValue, ushort changedLayers, bool seedFrameOnly = false)
            {
                if (seedFrameOnly ? !Owner.HasRawSeedInterest(x, y) :
                    !Owner.HasInterest(x, y, changedLayers) && ((changedLayers & 1) == 0 || !Owner.HasRawSeedInterest(x, y))) return false;
                var root = Root;
                if (root.Batch == null) root.Batch = Owner.BeginBatch();
                root.Batch.TouchStore(x, y, kind, oldValue, newValue, changedLayers, seedFrameOnly); return true;
            }
            internal bool TouchReads(BlueprintWorldChangeObserver.ReadDependencies reads, ushort changedLayers, bool seedFrameOnly = false)
            {
                if (!reads.HasChanges(changedLayers)) return false;
                var root = Root;
                if (root.Batch == null) root.Batch = Owner.BeginBatch();
                return root.Batch.TouchReads(reads, changedLayers, seedFrameOnly);
            }
        }
        private sealed class TransformLease : IDisposable
        {
            internal Action Original;
            internal BlueprintWorldChangeObserver.MutationBatch Batch;
            internal void Run()
            { try { Original(); } finally { Dispose(); } }
            public void Dispose() { var batch = InterlockedExchangeBatch(); batch?.Dispose(); }
            private BlueprintWorldChangeObserver.MutationBatch InterlockedExchangeBatch()
            { return System.Threading.Interlocked.Exchange(ref Batch, null); }
        }

        private static Dictionary<FieldInfo, int> FieldKinds()
        {
            var result = new Dictionary<FieldInfo, int>();
            string[] names = { "type", "wall", "liquid", "sTileHeader", "bTileHeader", "bTileHeader3", "frameX", "frameY" };
            for (int i = 0; i < names.Length; i++) result.Add(typeof(Tile).GetField(names[i], Flags), i);
            return result;
        }

        internal static void Install(BlueprintWorldChangeObserver observer)
        {
            if (ReferenceEquals(owner, observer)) return;
            if (owner != null) throw new InvalidOperationException("blueprint-world-change-owner");
            owner = observer; System.Threading.Interlocked.Increment(ref ownerGeneration);
            try
            {
#if DEBUG
                var installTimer = System.Diagnostics.Stopwatch.StartNew();
#endif
                // These are actual .8 mutation mechanisms: local tools,
                // natural tile changes, object/rail state, wiring and liquid,
                // plus the receiving network paths. Drawing/lighting, all
                // animation arrays, file IO and unrelated modules are absent.
                var types = new List<Type> { typeof(WorldGen), typeof(Wiring), typeof(Liquid), typeof(Player), typeof(NPC), typeof(Projectile),
                    typeof(TileObject), typeof(Tile), typeof(TileColorCache), typeof(Chest), typeof(Minecart), typeof(Framing), typeof(MessageBuffer), typeof(NetMessage),
                    OriginalType("Terraria.GameContent.NetModules.NetLiquidModule"), OriginalType("Terraria.GameContent.Tile_Entities.TELogicSensor"),
                    OriginalType("Terraria.WorldBuilding.WorldUtils") };
                AddNested(typeof(WorldGen), types);
                var originals = new Dictionary<MethodInfo, List<CodeInstruction>>();
                foreach (Type type in types.Distinct())
                    foreach (MethodInfo method in type.GetMethods(Flags | BindingFlags.DeclaredOnly))
                        if (!method.ContainsGenericParameters && method.GetMethodBody() != null)
                            originals[method] = PatchProcessor.GetOriginalInstructions(method);
#if DEBUG
                DebugDiscoveryMilliseconds = installTimer.ElapsedMilliseconds;
#endif

                // Propagate only Tile-taking mutators (e.g. paint cache and
                // Minecart.FrontTrack), not the entire Main.Update call graph.
                var mutators = new HashSet<MethodInfo>();
                bool changed;
                do
                {
                    changed = false;
                    foreach (var pair in originals)
                    {
                        if (mutators.Contains(pair.Key) || pair.Key.DeclaringType != typeof(Tile) && !TakesTile(pair.Key)) continue;
                        if (pair.Value.Any(c => ContentStore(c) || CallsMutator(c, mutators))) changed |= mutators.Add(pair.Key);
                    }
                } while (changed);

                HashSet<MethodInfo> activeWorld = ActiveWorldMethods(originals);
                AddRuntimeActions(originals, activeWorld);
                var candidates = new HashSet<MethodInfo>();
                foreach (var pair in originals)
                {
                    MethodInfo method = pair.Key;
                    if (method == safeGet) continue;
                    if (WorldGenMethod(method) && !activeWorld.Contains(method) && !pair.Value.Any(IdentityStore)) continue;
                    // Array-Set-only native readers just fill null slots with
                    // empty Tiles. They cannot invalidate a current content
                    // match, and must not turn NPC.AI/Player.Update/Draw into
                    // Blueprint mutation carriers. Real replacement/receive
                    // writers are selected by their actual content stores.
                    if (!pair.Value.Any(c => ContentStore(c) || IdentityStore(c) || CallsMutator(c, mutators))) continue;
                    candidates.Add(method);
                }
#if DEBUG
                DebugSelectionMilliseconds = installTimer.ElapsedMilliseconds - DebugDiscoveryMilliseconds;
                long patchStart = installTimer.ElapsedMilliseconds;
#endif
                // Patch a native writer's callees before recompiling its
                // caller. Otherwise the original tiny Tile/rail setters can
                // already have been inlined into the newly generated caller.
                foreach (MethodInfo method in PatchOrder(candidates, originals))
                {
                    // Tile and paint-cache methods inherit the caller's world
                    // coordinates; standalone copies never become observations.
                    bool tileValue = method.DeclaringType == typeof(Tile) || method.DeclaringType == typeof(TileColorCache);
                    // Only the finalizer can distinguish normal/exceptional
                    // native completion. A postfix must not certify a frame
                    // transaction before the exception boundary has closed.
                    harmony.Patch(method, tileValue ? null : Hook(nameof(Enter)), null, Hook(nameof(Rewrite)), tileValue ? null : Hook(nameof(FinalizeScope)));
#if DEBUG
                    DebugHookCount++;
                    DebugHookTargets.Add(method.DeclaringType.FullName + "." + method.Name);
#endif
                }
                harmony.Patch(safeGet, Hook(nameof(EnterSafe)), Hook(nameof(SafeReturned)), Hook(nameof(Rewrite)), Hook(nameof(FinalizeScope)));
                PatchSection("SetSectionLoaded", new[] { typeof(int), typeof(int) }, nameof(SectionBefore));
                PatchSection("SetAllSectionsLoaded", Type.EmptyTypes, nameof(AllSectionsBefore));
                harmony.Patch(typeof(WorldGen).GetMethod("TransformWorldOnBackgroundThread", Flags), Hook(nameof(TransformBefore)), null, null, Hook(nameof(TransformFinal)));
#if DEBUG
                DebugPatchMilliseconds = installTimer.ElapsedMilliseconds - patchStart;
#endif
            }
            catch
            {
                harmony.UnpatchAll(harmony.Id); owner = null; ReleasePools(); ResetThreadState(); throw;
            }
        }

        private static Type OriginalType(string name) { return typeof(Main).Assembly.GetType(name, true); }
        private static void AddNested(Type type, List<Type> result)
        { foreach (Type child in type.GetNestedTypes(Flags)) { result.Add(child); AddNested(child, result); } }
        private static bool TakesTile(MethodInfo method)
        { return method.GetParameters().Any(p => p.ParameterType == typeof(Tile) || p.ParameterType == typeof(Tile).MakeByRefType()); }
        private static bool ContentStore(CodeInstruction instruction)
        { return instruction.opcode == OpCodes.Stfld && instruction.operand is FieldInfo && fields.ContainsKey((FieldInfo)instruction.operand); }
        private static bool IdentityStore(CodeInstruction instruction)
        { return instruction.opcode == OpCodes.Stsfld && (Equals(instruction.operand, worldTiles) || Equals(instruction.operand, worldSections)); }
        private static bool CallsMutator(CodeInstruction instruction, HashSet<MethodInfo> mutators)
        { return instruction.operand is MethodInfo && (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && mutators.Contains((MethodInfo)instruction.operand); }

        private static bool WorldGenMethod(MethodInfo method)
        {
            for (Type type = method.DeclaringType; type != null; type = type.DeclaringType) if (type == typeof(WorldGen)) return true;
            return false;
        }
        private static HashSet<MethodInfo> ActiveWorldMethods(Dictionary<MethodInfo, List<CodeInstruction>> originals)
        {
            var reachable = new HashSet<MethodInfo>(); var pending = new Queue<MethodInfo>();
            Action<MethodInfo> add = method => { if (method != null && WorldGenMethod(method) && originals.ContainsKey(method) && reachable.Add(method)) pending.Enqueue(method); };
            // World update is entered directly by Main. Other roots are the
            // actual tool/entity/network/object consumers above. Follow native
            // calls AND delegate targets, retaining background hardmode work.
            add(typeof(WorldGen).GetMethod("UpdateWorld", Flags));
            add(typeof(WorldGen).GetMethod("StartHardmode", Flags));
            // Main has direct time/map roots outside WorldGen.UpdateWorld
            // (notably the nightly meteor). Read only its method operands,
            // without patching Main or enumerating the entire game graph.
            foreach (MethodInfo method in MainWorldCalls()) add(method);
            foreach (var pair in originals)
                if (!WorldGenMethod(pair.Key))
                    foreach (CodeInstruction instruction in pair.Value) add(instruction.operand as MethodInfo);
            while (pending.Count != 0)
            {
                MethodInfo method = pending.Dequeue();
                // These entry points replace/create the world before a valid
                // in-world observer exists. Keep their identity stores, not
                // hundreds of new-world generation passes and seed closures.
                if (method.Name == "GenerateWorld" || method.Name == "CreateNewWorld" || method.Name == "worldGenCallback" || method.Name == "clearWorld") continue;
                foreach (CodeInstruction instruction in originals[method]) add(instruction.operand as MethodInfo);
            }
            return reachable;
        }
        private static IEnumerable<MethodInfo> MainWorldCalls()
        {
            foreach (MethodInfo method in typeof(Main).GetMethods(Flags | BindingFlags.DeclaredOnly))
            {
                MethodBody body = method.GetMethodBody(); if (body == null) continue;
                byte[] code = body.GetILAsByteArray();
                for (int at = 0; at < code.Length;)
                {
                    short value = code[at++]; if (value == 0xfe) value = unchecked((short)(0xfe00 | code[at++]));
                    OpCode opcode = opcodes[value]; int size;
                    switch (opcode.OperandType)
                    {
                        case OperandType.InlineNone: size = 0; break;
                        case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: size = 1; break;
                        case OperandType.InlineVar: size = 2; break;
                        case OperandType.InlineI8: case OperandType.InlineR: size = 8; break;
                        case OperandType.InlineSwitch: size = 4 + 4 * BitConverter.ToInt32(code, at); break;
                        default: size = 4; break;
                    }
                    if (opcode.OperandType == OperandType.InlineMethod)
                    {
                        MethodInfo target = null;
                        try { target = method.Module.ResolveMethod(BitConverter.ToInt32(code, at), Type.EmptyTypes, method.IsGenericMethod ? method.GetGenericArguments() : Type.EmptyTypes) as MethodInfo; }
                        catch (ArgumentException) { }
                        catch (FileNotFoundException) { }
                        if (target != null && WorldGenMethod(target)) yield return target;
                    }
                    at += size;
                }
            }
        }
        private static void AddRuntimeActions(Dictionary<MethodInfo, List<CodeInstruction>> originals, HashSet<MethodInfo> activeWorld)
        {
            var actions = new HashSet<Type>(); Type actionBase = OriginalType("Terraria.WorldBuilding.GenAction");
            foreach (var pair in originals)
            {
                if (WorldGenMethod(pair.Key) && !activeWorld.Contains(pair.Key)) continue;
                foreach (CodeInstruction instruction in pair.Value)
                {
                    var constructor = instruction.operand as ConstructorInfo;
                    if (instruction.opcode == OpCodes.Newobj && constructor != null && actionBase.IsAssignableFrom(constructor.DeclaringType)) actions.Add(constructor.DeclaringType);
                }
            }
            // A GenAction.Apply virtual call hides its concrete writer from
            // the ordinary call graph. Only action types actually constructed
            // by these runtime roots join the same content-store selection.
            foreach (Type type in actions)
                foreach (MethodInfo method in type.GetMethods(Flags | BindingFlags.DeclaredOnly))
                    if (!method.ContainsGenericParameters && method.GetMethodBody() != null && !originals.ContainsKey(method)) originals[method] = PatchProcessor.GetOriginalInstructions(method);
        }
        private static List<MethodInfo> PatchOrder(HashSet<MethodInfo> candidates, Dictionary<MethodInfo, List<CodeInstruction>> originals)
        {
            var ordered = new List<MethodInfo>(); var visited = new HashSet<MethodInfo>();
            Action<MethodInfo> visit = null;
            visit = method =>
            {
                if (!visited.Add(method)) return;
                foreach (CodeInstruction instruction in originals[method])
                { var target = instruction.operand as MethodInfo; if (target != null && candidates.Contains(target)) visit(target); }
                ordered.Add(method);
            };
            foreach (MethodInfo method in candidates.OrderBy(m => m.DeclaringType == typeof(Tile) ? 0 : 1)) visit(method);
            return ordered;
        }
        private static HarmonyMethod Hook(string name) { return new HarmonyMethod(typeof(BlueprintWorldChangeHooks).GetMethod(name, Flags)); }
        private static MethodInfo Method(string name) { return typeof(BlueprintWorldChangeHooks).GetMethod(name, Flags); }

        private static void Enter(out WriteScope __state)
        {
            var observer = owner;
            if (observer == null || observer.WatchCount == 0) { __state = null; return; }
            int resources = System.Threading.Volatile.Read(ref resourceGeneration);
            if (scopePool == null || scopePool.Generation != System.Threading.Volatile.Read(ref ownerGeneration) || scopePool.Resources != resources)
            {
                scopePool = new ScopePool(System.Threading.Volatile.Read(ref ownerGeneration), resources);
                lock (poolGate)
                {
                    pools.RemoveAll(p => !(p.Target is ScopePool pool) || pool.Resources != System.Threading.Volatile.Read(ref resourceGeneration));
                    pools.Add(new WeakReference(scopePool));
                }
            }
            __state = scopePool.Take(depth);
            // A last-watch clear can race a native prefix on a Task thread.
            // The active local remains safe until finally, but it must not
            // repopulate a retired pool after the cross-thread slot clear.
            if (observer.WatchCount == 0 || resources != System.Threading.Volatile.Read(ref resourceGeneration)) scopePool.Forget(depth, __state);
            depth++; __state.Parent = current; __state.Owner = observer; __state.Ended = false; __state.Failed = false;
            __state.Root = current != null && ReferenceEquals(current.Owner, observer) ? current.Root : __state;
            current = __state;
#if DEBUG
            DebugMaxDepth = Math.Max(DebugMaxDepth, depth);
#endif
        }
        private static void EnterSafe(out WriteScope __state)
        {
            // Ordinary Draw/reader calls do not create a mutation scope just
            // because GetTileSafely can initialize a null Tile.
            if (current == null) { __state = null; return; }
            Enter(out __state);
        }
        private static void Leave(WriteScope __state, bool normalExit)
        {
            if (__state == null || __state.Ended) return;
            __state.Ended = true;
            WriteScope root = __state.Root;
            if (!normalExit) root.Failed = true;
            try { if (ReferenceEquals(root, __state)) __state.Batch?.Complete(normalExit && !root.Failed); }
            finally
            {
                current = __state.Parent; __state.Parent = __state.Root = null; __state.Owner = null; __state.Batch = null;
                __state.LastTile = null; __state.OverflowReads = null; __state.Coordinates.Clear(); depth--;
                if (depth == 0 && (owner == null || owner.WatchCount == 0)) ResetThreadState();
            }
        }
        private static Exception FinalizeScope(WriteScope __state, Exception __exception)
        { Leave(__state, __exception == null); return __exception; }
        private static void SafeReturned(int __0, int __1, Tile __result, WriteScope __state)
        { Leave(__state, true); Track(__result, __0, __1); }

        private static Tile Read(Tile[,] tiles, int x, int y)
        {
            Tile value = tiles[x, y];
            if (current != null && ReferenceEquals(tiles, Main.tile)) Track(value, x, y);
            return value;
        }
        private static void Track(Tile value, int x, int y)
        {
            var scope = current;
            if (scope == null || value == null) return;
            // Remember even a non-interesting latest read: a following store
            // to that actual Tile must not fall back to unrelated read ranges.
            scope.LastTile = value; scope.LastPoint = new Point(x, y);
            // A first Door seed lease owns its exact nine coordinates even
            // before a published profile exists in the source route index.
            if (!scope.Owner.HasInterest(x, y) && !scope.Owner.HasRawSeedInterest(x, y)) return;
            if (scope.Coordinates.ContainsKey(value) || scope.Coordinates.Count < MaximumCoordinateAssociations)
                scope.Coordinates[value] = scope.LastPoint;
            else
            {
                if (scope.OverflowReads == null)
                {
                    scope.OverflowReads = new BlueprintWorldChangeObserver.ReadDependencies(scope.Owner);
#if DEBUG
                    DebugOverflowScopes++;
#endif
                }
                scope.OverflowReads.Record(new BlueprintProjectionBounds(x, y, 1, 1));
            }
#if DEBUG
            DebugMappedReads++;
            DebugMaxCoordinates = Math.Max(DebugMaxCoordinates, scope.Coordinates.Count);
#endif
        }
        private static void BeforeStore(Tile tile, int kind, int oldValue, int newValue)
        {
            var scope = current;
            if (scope == null) return;
            // The source deliberately ignores ordinary terrain drawing frames.
            // Preserve that early exit unless a Door's finite 3x3 raw seed is
            // published or currently being sampled. Even an inactive/ordinary
            // wing must then keep its exact raw old-value chain: a later type
            // change can expose a frame assignment that is itself a no-op.
            // This is the EXISTING write-object type/static metadata read;
            // the new witness path adds only pure coordinate/field deltas.
            bool seedFrameOnly = (kind == 6 || kind == 7) && tile.type < Main.tileFrameImportant.Length && !Main.tileFrameImportant[tile.type];
            if (seedFrameOnly && !scope.Owner.HasRawSeedInterests) return;
            ushort layers = ChangedLayers(kind, oldValue ^ newValue);
            if (layers == 0) return;
            Point point = default(Point);
            for (WriteScope source = scope; source != null; source = source.Parent)
                if (ReferenceEquals(source.LastTile, tile) || source.Coordinates.TryGetValue(tile, out point))
                {
                    if (ReferenceEquals(source.LastTile, tile)) point = source.LastPoint;
                    bool relevant = scope.TouchStore(point.X, point.Y, kind, oldValue, newValue, layers, seedFrameOnly);
#if DEBUG
                    if (relevant) DebugRecordedWrites++;
#else
                    _ = relevant;
#endif
                    return;
                }
            // Only an actual changed field reaches this pressure fallback.
            // It keeps the exact changed layers, so merely overflowing on a
            // stable read or changing extra wire/liquid cannot revoke walls.
            for (WriteScope source = scope; source != null; source = source.Parent)
                if (source.OverflowReads != null && scope.TouchReads(source.OverflowReads, layers, seedFrameOnly))
                {
#if DEBUG
                    DebugFallbackWrites++;
#endif
                }
        }
        private static void BeforeSet(Tile[,] tiles, int x, int y, Tile value)
        {
            var observer = owner;
            if (observer == null || observer.WatchCount == 0 || !ReferenceEquals(tiles, Main.tile) || !observer.HasInterest(x, y)) return;
            // A later return to the same Tile reference cannot erase an
            // observed replacement in this finite installation's provenance.
            if (!ReferenceEquals(tiles[x, y], value)) HostBlueprint.ObserveConstructionTileReplacement(x, y);
            if (current != null) { current.Touch(x, y); Track(value, x, y); }
            else
            {
                // The original's null initialization in an ordinary safe read
                // changes Unknown to readable. This call cannot reenter Host;
                // native reads/stores and Host updates run on the game thread.
                using (observer.BeginMutation(new BlueprintProjectionBounds(x, y, 1, 1))) { }
            }
        }

        private static ushort ChangedLayers(int kind, int delta)
        {
            if (delta == 0) return 0;
            // Locked .8 Tile.cs getters/setters define these packed bits.
            // This is the match/mask counting unit, NOT CaptureLayer's enum.
            // Presence, inactive, shape, paint, frame/type and block coatings
            // all belong to Tile; the installed actuator is independent.
            switch (kind)
            {
                case 0: case 6: case 7: return 1; // type / frameX / frameY
                case 1: return 2; // wall type
                case 2: return 4; // liquid amount
                case 3: // sTileHeader: Tile.cs color..fullbrightWall
                    return (ushort)(((delta & 0x747f) != 0 ? 1 : 0) |
                        ((delta & 0x8000) != 0 ? 2 : 0) | ((delta & 0x800) != 0 ? 8 : 0) |
                        ((delta & 0x80) != 0 ? 16 : 0) | ((delta & 0x100) != 0 ? 32 : 0) | ((delta & 0x200) != 0 ? 64 : 0));
                case 4: // wallColor low5 / liquidType bits5-6 / wire4 bit7
                    return (ushort)(((delta & 0x1f) != 0 ? 2 : 0) | ((delta & 0x60) != 0 ? 4 : 0) | ((delta & 0x80) != 0 ? 128 : 0));
                case 5: // invisibleBlock/fullbrightBlock vs invisibleWall
                    // Low5 wall-frame/liquid scheduling bits are not content.
                    return (ushort)(((delta & 0xa0) != 0 ? 1 : 0) | ((delta & 0x40) != 0 ? 2 : 0));
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        private static void BeforeIdentityStore()
        {
            var scope = current;
            if (scope == null) return;
            var root = scope.Root;
            if (root.Batch == null) root.Batch = scope.Owner.BeginBatch();
            root.Batch.TouchAll();
        }

        private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> source, MethodBase __originalMethod, ILGenerator generator)
        {
            var output = new List<CodeInstruction>();
            AddTileParameterMapping(output, __originalMethod);
            foreach (CodeInstruction instruction in source)
            {
                if (instruction.Calls(arrayGet))
                { instruction.opcode = OpCodes.Call; instruction.operand = Method(nameof(Read)); }
                else if (ContentStore(instruction))
                {
                    var field = (FieldInfo)instruction.operand;
                    LocalBuilder value = generator.DeclareLocal(field.FieldType), tile = generator.DeclareLocal(typeof(Tile));
                    First(output, new CodeInstruction(OpCodes.Stloc, value), instruction);
                    output.Add(new CodeInstruction(OpCodes.Stloc, tile));
                    output.Add(new CodeInstruction(OpCodes.Ldloc, tile)); output.Add(new CodeInstruction(OpCodes.Ldc_I4, fields[field]));
                    output.Add(new CodeInstruction(OpCodes.Ldloc, tile)); output.Add(new CodeInstruction(OpCodes.Ldfld, field));
                    output.Add(new CodeInstruction(OpCodes.Ldloc, value)); output.Add(new CodeInstruction(OpCodes.Call, Method(nameof(BeforeStore))));
                    output.Add(new CodeInstruction(OpCodes.Ldloc, tile)); output.Add(new CodeInstruction(OpCodes.Ldloc, value));
                }
                else if (instruction.Calls(arraySet))
                {
                    LocalBuilder value = generator.DeclareLocal(typeof(Tile)), y = generator.DeclareLocal(typeof(int)), x = generator.DeclareLocal(typeof(int)), tiles = generator.DeclareLocal(typeof(Tile[,]));
                    First(output, new CodeInstruction(OpCodes.Stloc, value), instruction);
                    output.Add(new CodeInstruction(OpCodes.Stloc, y)); output.Add(new CodeInstruction(OpCodes.Stloc, x)); output.Add(new CodeInstruction(OpCodes.Stloc, tiles));
                    LoadSet(output, tiles, x, y, value); output.Add(new CodeInstruction(OpCodes.Call, Method(nameof(BeforeSet))));
                    LoadSet(output, tiles, x, y, value);
                }
                else if (IdentityStore(instruction)) First(output, new CodeInstruction(OpCodes.Call, Method(nameof(BeforeIdentityStore))), instruction);
                output.Add(instruction);
            }
            return output;
        }
        private static void First(List<CodeInstruction> output, CodeInstruction first, CodeInstruction original)
        { first.labels.AddRange(original.labels); original.labels.Clear(); first.blocks.AddRange(original.blocks); original.blocks.Clear(); output.Add(first); }
        private static void LoadSet(List<CodeInstruction> output, LocalBuilder tiles, LocalBuilder x, LocalBuilder y, LocalBuilder value)
        { output.Add(new CodeInstruction(OpCodes.Ldloc, tiles)); output.Add(new CodeInstruction(OpCodes.Ldloc, x)); output.Add(new CodeInstruction(OpCodes.Ldloc, y)); output.Add(new CodeInstruction(OpCodes.Ldloc, value)); }

        private static void AddTileParameterMapping(List<CodeInstruction> output, MethodBase method)
        {
            // The .8 cached-Tile helpers carry their real world coordinate
            // pair (Convert/Replace, wiring lights, SelfFrame8Way). Other
            // cached values inherit the caller's actual array-read mapping.
            ParameterInfo[] args = method.GetParameters(); int x = -1, y = -1;
            string[,] pairs = { { "x", "y" }, { "i", "j" }, { "topLeftX", "topLeftY" }, { "tileX", "tileY" } };
            for (int pair = 0; pair < pairs.GetLength(0) && x < 0; pair++)
            {
                int a = Array.FindIndex(args, p => p.ParameterType == typeof(int) && String.Equals(p.Name, pairs[pair, 0], StringComparison.OrdinalIgnoreCase));
                int b = Array.FindIndex(args, p => p.ParameterType == typeof(int) && String.Equals(p.Name, pairs[pair, 1], StringComparison.OrdinalIgnoreCase));
                if (a >= 0 && b >= 0) { x = a; y = b; }
            }
            if (x < 0) return;
            int offset = method.IsStatic ? 0 : 1;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ParameterType != typeof(Tile) && args[i].ParameterType != typeof(Tile).MakeByRefType()) continue;
                output.Add(new CodeInstruction(OpCodes.Ldarg, i + offset));
                if (args[i].ParameterType.IsByRef) output.Add(new CodeInstruction(OpCodes.Ldind_Ref));
                output.Add(new CodeInstruction(OpCodes.Ldarg, x + offset)); output.Add(new CodeInstruction(OpCodes.Ldarg, y + offset));
                output.Add(new CodeInstruction(OpCodes.Call, Method(nameof(Track))));
            }
        }

        private static void PatchSection(string name, Type[] args, string before)
        {
            MethodInfo method = typeof(WorldSections).GetMethod(name, Flags, null, args, null);
            if (method == null) throw new MissingMethodException("blueprint-world-section:" + name);
            harmony.Patch(method, Hook(before), Hook(nameof(SectionAfter)), null, Hook(nameof(SectionFinal)));
        }
        private static void SectionBefore(WorldSections __instance, int __0, int __1, out BlueprintWorldChangeObserver.Mutation __state)
        {
            var observer = owner;
            __state = observer == null || !ReferenceEquals(__instance, Main.sectionManager) || __instance.SectionLoaded(__0, __1) ? null :
                observer.BeginMutation(new BlueprintProjectionBounds(checked(__0 * 200), checked(__1 * 150), 200, 150));
        }
        private static void AllSectionsBefore(WorldSections __instance, out BlueprintWorldChangeObserver.Mutation __state)
        {
            var observer = owner;
            __state = observer == null || !ReferenceEquals(__instance, Main.sectionManager) ? null :
                observer.BeginMutation(new BlueprintProjectionBounds(0, 0, Main.maxTilesX, Main.maxTilesY));
        }
        private static void SectionAfter(BlueprintWorldChangeObserver.Mutation __state) { __state?.Dispose(); }
        private static Exception SectionFinal(BlueprintWorldChangeObserver.Mutation __state, Exception __exception)
        { __state?.Dispose(); return __exception; }

        private static void TransformBefore(ref Action __0, out TransformLease __state)
        {
            var observer = owner; __state = null;
            if (observer == null || observer.WatchCount == 0) return;
            __state = new TransformLease { Original = __0, Batch = observer.BeginBatch() };
            // These two native transformations may touch arbitrary world
            // cells on a Task. Revoke existing proof before queueing, then
            // retain pending until that actual delegate completes/throws.
            __state.Batch.TouchAll(); __0 = __state.Run;
        }
        private static Exception TransformFinal(TransformLease __state, Exception __exception)
        {
            // Queue creation can itself fail before the delegate owns cleanup.
            if (__exception != null) __state?.Dispose();
            return __exception;
        }

        internal static void Uninstall(BlueprintWorldChangeObserver observer)
        {
            if (!ReferenceEquals(owner, observer)) return;
            harmony.UnpatchAll(harmony.Id); owner = null; ReleasePools(); ResetThreadState();
        }
        internal static void ReleaseIdle(BlueprintWorldChangeObserver observer)
        { if (ReferenceEquals(owner, observer) && observer.WatchCount == 0) { ReleasePools(); ResetThreadState(); } }
        private static void ReleasePools()
        {
            // Resource generation is ONLY a pool-retirement guard, never a
            // world-content epoch or a reason to invalidate a match.
            System.Threading.Interlocked.Increment(ref resourceGeneration);
            lock (poolGate)
            {
                foreach (WeakReference weak in pools) (weak.Target as ScopePool)?.Clear();
                pools.Clear();
            }
        }
        private static void ResetThreadState()
        {
            if (depth != 0) return;
            scopePool?.Clear(); scopePool = null; current = null;
        }
    }
}
