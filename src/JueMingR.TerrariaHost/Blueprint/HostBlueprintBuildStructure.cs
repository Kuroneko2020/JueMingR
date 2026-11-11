using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private static readonly MethodInfo specialPoundable = typeof(Player).GetMethod("IsTilePoundable", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Tile) }, null);
        private static bool SpecialShapePoundable(Player p, Tile tile)
        { return specialPoundable != null && (bool)specialPoundable.Invoke(p,new object[] { tile }); }
        private sealed class StructureCycle
        {
            internal long Environment;
            internal BlueprintProjectionIdentity Identity;
            internal BlueprintProjectionMask Mask;
            internal int Type, Wanted;
            internal readonly HashSet<int> Seen = new HashSet<int>();
        }
        private readonly Dictionary<long, StructureCycle> structureCycles = new Dictionary<long, StructureCycle>();
        private bool TryPlanStructure(BlueprintBuildTarget target, BlueprintWorldMatchSample actual, out BlueprintBuildOperation operation)
        {
            operation = null;
            if (!BlueprintStructureSemantics.Special(target.Cell.TileType) || !BuildOperationBodySame(target, actual) ||
                BlueprintStructureSemantics.Same(target.Cell, actual.Cell)) return false;
            if (target.Cell.TileType == TileID.DisplayDoll && (target.WorldX != target.Bounds.Left || target.WorldY != target.Bounds.Top ||
                target.Cell.Payload.PoseState != BlueprintPayloadState.Known || actual.Cell.Payload.PoseState != BlueprintPayloadState.Known)) return false;
            if (target.Cell.TileType == TileID.MinecartTrack && !TrackLocalBodiesReady(target)) return false;
            long environment;
            if (!StructureEnvironment(target, out environment)) return false;
            long key = (long)target.WorldY << 32 | (uint)target.WorldX;
            StructureCycle cycle;
            if (!structureCycles.TryGetValue(key, out cycle)) structureCycles[key] = cycle = new StructureCycle { Environment = environment };
            int wanted = BlueprintStructureSemantics.State(target.Cell);
            if (cycle.Environment != environment || cycle.Identity?.Equals(target.Identity) != true || !ReferenceEquals(cycle.Mask,target.Mask) ||
                cycle.Type != target.Cell.TileType || cycle.Wanted != wanted)
            {
                cycle.Environment = environment; cycle.Identity = target.Identity; cycle.Mask = target.Mask;
                cycle.Type = target.Cell.TileType; cycle.Wanted = wanted; cycle.Seen.Clear();
            }
            int state = BlueprintStructureSemantics.State(actual.Cell);
            // An observed full cycle under identical local conditions is a
            // local dependency wait. Neighbor changes wake it; no magic count
            // or synthetic frame write can turn that wait into completion.
            if (cycle.Seen.Contains(state))
            {
                projectionFeedback = target.Cell.TileType == TileID.DisplayDoll && target.Cell.Payload.Pose > 3 ?
                    "人体模型的目标姿态需要对应的手持装备，当前保留姿态待办。" : "此处结构暂不能达到图纸状态，相邻结构变化后会重新尝试。";
                return false;
            }
            operation = new BlueprintBuildOperation { Kind = BlueprintBuildOperationKind.Hammer, Consumer = ToolBuildConsumer.Hammer,
                Structure = true, Mode = state, PrimaryMaximum = 0, HeldMaximum = 0 };
            return true;
        }
        private bool TrackLocalBodiesReady(BlueprintBuildTarget target)
        {
            // A target slot's actual native connections identify at most four
            // prerequisite cells. Missing neighbors are local body work, not
            // a reason to cycle the hammer or wait for every rail in the scene.
            for (int slot=0;slot<2;slot++)
            {
                int frame=slot==0?target.Cell.FrameX:target.Cell.FrameY;
                if (frame == -1) continue;
                for (int side=0;side<2;side++)
                {
                    int connection;
                    if (!BlueprintTrackMirror.Connection(frame,side==0,out connection)) return false;
                    if (connection < 0) continue;
                    BlueprintWorldMatchSample neighbor;
                    if (!ReadBuildCell(target.WorldX+(side==0?-1:1),target.WorldY+connection-1,out neighbor) ||
                        (neighbor.Cell.Layers&CaptureLayer.Tile)==0 || neighbor.Cell.TileType!=TileID.MinecartTrack) return false;
                }
            }
            return true;
        }
        private bool StructureEnvironment(BlueprintBuildTarget target, out long value)
        {
            value = 1469598103934665603L ^ (target.Cell.TileType==TileID.DisplayDoll?BlueprintNativePayload.StructureDependency(target.WorldX,target.WorldY):0);
            Tile center = Main.tile[target.WorldX,target.WorldY];
            if (center != null) value ^= System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(center);
            // Track and pad native consumers read adjacent cells. Include the
            // exact finite neighborhood, not an unrelated whole-scene epoch.
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                BlueprintWorldMatchSample sample;
                if (!ReadBuildCell(target.WorldX + dx, target.WorldY + dy, out sample)) return false;
                unchecked { value = (value ^ sample.Cell.Payload.Pose ^ sample.Cell.TileType ^ (long)(ushort)sample.Cell.FrameX << 16 ^ (long)(ushort)sample.Cell.FrameY << 32 ^
                    (long)sample.Cell.Layers << 48 ^ sample.Cell.Slope ^ (sample.Cell.HalfBrick ? 64 : 0)) * 1099511628211L; }
            }
            return true;
        }
        private void ClearStructureCycles(BlueprintProjectionIdentity identity)
        {
            if (identity == null) { structureCycles.Clear(); return; }
            var retired = new List<long>();
            foreach (var pair in structureCycles) if (pair.Value.Identity?.Equals(identity) == true) retired.Add(pair.Key);
            foreach (long key in retired) structureCycles.Remove(key);
        }
        private bool StructureApplied(BuildFlight flight, BlueprintWorldMatchSample actual)
        {
            long key = (long)flight.Target.WorldY << 32 | (uint)flight.Target.WorldX; StructureCycle cycle;
            if (structureCycles.TryGetValue(key, out cycle)) cycle.Seen.Add(flight.Operation.Mode);
            return (actual.Cell.Layers & CaptureLayer.Tile) != 0 && actual.Cell.TileType == flight.Target.Cell.TileType &&
                BlueprintStructureSemantics.State(actual.Cell) != flight.Operation.Mode;
        }
    }

    internal static class BlueprintTrackMirror
    {
        private static int[] connectionLeft, connectionRight;
        internal static bool Connection(int frame,bool left,out int connection)
        {
            connection=-1; var values=left?connectionLeft:connectionRight;
            if(values==null || frame<0 || frame>=values.Length)return false;
            connection=values[frame];return true;
        }
        // Main.Initialize owns these arrays. Capture only after initialization;
        // the worker receives a 36-entry pure-value map and never touches ABI.
        internal static int[] Capture()
        {
            const BindingFlags f = BindingFlags.Static | BindingFlags.NonPublic;
            var type = typeof(Minecart);
            var left = type.GetField("_leftSideConnection", f)?.GetValue(null) as int[];
            var right = type.GetField("_rightSideConnection", f)?.GetValue(null) as int[];
            var kind = type.GetField("_trackType", f)?.GetValue(null) as int[];
            var boost = type.GetField("_boostLeft", f)?.GetValue(null) as bool[];
            var heights = type.GetField("_tileHeight", f)?.GetValue(null) as int[][];
            if (left?.Length != 36 || right?.Length != 36 || kind?.Length != 36 || boost?.Length != 36 || heights?.Length != 36) return null;
            var result = new int[36];
            for (int a = 0; a < 36; a++)
            {
                int found = -1;
                for (int b = 0; b < 36; b++)
                {
                    if (left[a] != right[b] || right[a] != left[b] || kind[a] != kind[b] || kind[a] == 2 && boost[a] == boost[b] || heights[a]?.Length != 8 || heights[b]?.Length != 8) continue;
                    bool equal = true; for (int x = 0; x < 8; x++) equal &= heights[a][x] == heights[b][7 - x];
                    if (!equal) continue;
                    if (found >= 0) return null; found = b;
                }
                if (found < 0) return null; result[a] = found;
            }
            for (int a = 0; a < 36; a++) if (result[result[a]] != a) return null;
            connectionLeft=(int[])left.Clone(); connectionRight=(int[])right.Clone();
            return result;
        }
    }
}
