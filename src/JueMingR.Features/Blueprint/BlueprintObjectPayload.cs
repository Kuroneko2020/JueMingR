using System;

namespace JueMingR.Features.Blueprint
{
    public static class BlueprintStructureSemantics
    {
        public static bool Special(int type) { return type == 314 || type == 137 || type == 424 || type == 442 || type == 470; }
        public static int State(CaptureCell cell) { return cell.TileType == 470 ? cell.Payload.PoseState == BlueprintPayloadState.Known ? cell.Payload.Pose : -1 : (ushort)cell.FrameX | cell.FrameY << 16; }
        public static bool Same(CaptureCell target, CaptureCell actual)
        { return target.TileType == actual.TileType && (target.TileType == 470 ? target.Payload.PoseState == BlueprintPayloadState.Absent || target.Payload.Compare(actual.Payload) == BlueprintWorldMatchState.Match : target.FrameX == actual.FrameX && target.FrameY == actual.FrameY); }
    }
    public enum BlueprintPayloadState : byte { Absent, Known, Unknown }

    // Only the canonical top-left member carries object payload. Absent is an
    // old asset's lack of a requirement; Unknown must never become known empty.
    // These immutable-by-ownership values follow the Tile, not walls or wires.
    public struct BlueprintObjectPayload
    {
        public int EntityItemType;
        public BlueprintPayloadState EntityState, NameState, PoseState;
        public string Name;
        public byte Pose;
        public bool HasValue { get { return EntityState != BlueprintPayloadState.Absent || EntityItemType != 0 || NameState != BlueprintPayloadState.Absent || PoseState != BlueprintPayloadState.Absent; } }
        public long EstimatedBytes { get { return HasValue ? 32L + (Name == null ? 0 : 24L + Name.Length * 2L) : 0; } }
        public static bool ContainerType(int type) { return type == 21 || type == 88 || type == 467; }
        public static bool EntityType(int type) { return type == 723 || type == 724; }
        public bool Valid(int type)
        {
            return EntityItemType >= 0 && EntityItemType <= Int16.MaxValue && EntityState <= BlueprintPayloadState.Unknown &&
                (EntityState == BlueprintPayloadState.Absent ? EntityItemType == 0 : EntityType(type)) &&
                (EntityState != BlueprintPayloadState.Unknown || EntityItemType == 0) && (EntityState != BlueprintPayloadState.Known || EntityItemType > 0) && (EntityItemType == 0 || EntityType(type)) &&
                NameState <= BlueprintPayloadState.Unknown && PoseState <= BlueprintPayloadState.Unknown &&
                (NameState == BlueprintPayloadState.Absent ? Name == null : ContainerType(type)) &&
                (NameState != BlueprintPayloadState.Known || Name != null && Name.Length <= 20) &&
                (NameState != BlueprintPayloadState.Unknown || Name == null) &&
                (PoseState == BlueprintPayloadState.Absent ? Pose == 0 : type == 470) && Pose <= 8;
        }
        public BlueprintWorldMatchState Compare(BlueprintObjectPayload actual)
        {
            if (EntityState == BlueprintPayloadState.Unknown || EntityItemType > 0 && (actual.EntityItemType == 0 || actual.EntityState != BlueprintPayloadState.Known) || NameState == BlueprintPayloadState.Unknown ||
                PoseState == BlueprintPayloadState.Unknown || NameState == BlueprintPayloadState.Known && actual.NameState != BlueprintPayloadState.Known ||
                PoseState == BlueprintPayloadState.Known && actual.PoseState != BlueprintPayloadState.Known) return BlueprintWorldMatchState.Unknown;
            if (EntityItemType > 0 && EntityItemType != actual.EntityItemType ||
                NameState == BlueprintPayloadState.Known && !String.Equals(Name, actual.Name, StringComparison.Ordinal) ||
                PoseState == BlueprintPayloadState.Known && Pose != actual.Pose) return BlueprintWorldMatchState.Conflict;
            return BlueprintWorldMatchState.Match;
        }
    }
}
