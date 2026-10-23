using System;

namespace JueMingR.Features.Blueprint
{
    [Flags]
    public enum CaptureLayer { None = 0, Tile = 1, Wall = 2, Wire = 4, Actuator = 8, Liquid = 16 }

    public readonly struct ContentHandle
    {
        public ContentHandle(Guid id) { Id = id; }
        public Guid Id { get; }
        public bool IsEmpty { get { return Id == Guid.Empty; } }
    }

    public readonly struct CaptureToken
    {
        public CaptureToken(Guid operationId, long sessionGeneration, long modeGeneration, long selectionRevision)
        { OperationId = operationId; SessionGeneration = sessionGeneration; ModeGeneration = modeGeneration; SelectionRevision = selectionRevision; }
        public Guid OperationId { get; }
        public long SessionGeneration { get; }
        public long ModeGeneration { get; }
        public long SelectionRevision { get; }
    }

    // Cells contain semantic values only. World coordinates are retained in the
    // private stage; the fixed summary origin normalizes them without rewriting
    // the whole draft. Missing layers never express an erase operation.
    public struct CaptureCell
    {
        public int X, Y, ObjectId;
        public CaptureLayer Layers;
        public ushort TileType, WallType;
        public short FrameX, FrameY;
        public byte TilePaint, WallPaint, Slope, Wires, LiquidAmount, LiquidKind;
        public bool HalfBrick, Inactive, Actuator, InvisibleTile, FullbrightTile, InvisibleWall, FullbrightWall;
        public bool UnreliableObject;
        public bool HasContent { get { return Layers != CaptureLayer.None; } }
        public CaptureCell WithoutTile()
        {
            var result = this; result.Layers &= ~CaptureLayer.Tile;
            result.ObjectId = 0; result.UnreliableObject = false;
            result.TileType = 0; result.FrameX = result.FrameY = 0; result.TilePaint = result.Slope = 0;
            result.HalfBrick = result.Inactive = result.InvisibleTile = result.FullbrightTile = false;
            return result;
        }
    }

    public struct CaptureObject
    {
        public int Id, X, Y, Width, Height, Style, Alternate, Direction, OriginX, OriginY, PlacementStyle, RandomVariant;
        public ushort Type;
        public bool Reliable;
        public string Reason, MissingPayload;
    }

    // Ownership transfers on submission: callers must never mutate either
    // array after TryAppendChunk accepts it. One pending chunk provides bounded
    // backpressure rather than an unbounded queue of live-world snapshots.
    public sealed class CaptureChunk
    {
        public const int MaximumRecords = 2048;
        public CaptureChunk(CaptureCell[] cells, int count, CaptureObject[] objects = null, int objectCount = 0)
        {
            if (cells == null || count < 0 || count > cells.Length || objectCount < 0 ||
                objectCount > (objects == null ? 0 : objects.Length) || (long)count + objectCount > MaximumRecords)
                throw new ArgumentException("Invalid capture chunk.");
            Cells = cells; Count = count; Objects = objects ?? Array.Empty<CaptureObject>(); ObjectCount = objectCount;
        }
        public CaptureCell[] Cells { get; }
        public int Count { get; }
        public CaptureObject[] Objects { get; }
        public int ObjectCount { get; }
    }

    public sealed class CaptureSummary
    {
        public string TargetVersion = "1.4.5.8";
        public int Left, Top, Width, Height;
        public long ContentCount, UnreliableObjectCount;
        public ulong StartTick, EndTick;
        public string[] MissingPayloads = Array.Empty<string>();
        public string[] UnreliableReasons = Array.Empty<string>();
    }

    public interface IBlueprintCaptureSource
    {
        bool Read(int x, int y, out CaptureCell cell);
        // Null means no fixed rectangular object group: ordinary blocks and
        // selected natural snapshots still retain their raw tile/frame values.
        // Describe consumes the immediately preceding successful Read only.
        BlueprintObjectShape Describe(CaptureCell cell);
    }

    public sealed class BlueprintObjectShape
    {
        public CaptureObject Object;
        public int BaseFrameX, BaseFrameY, ColumnStep;
        // Some original world generators choose each member's decoration
        // independently. Describe proves membership; raw frames remain intact.
        public bool IndependentMemberFrames;
        public int[] RowOffsets = Array.Empty<int>();
        public string Key { get { return Object.Type + ":" + Object.X + ":" + Object.Y; } }
    }

    public enum BlueprintCaptureStatus { Idle, Starting, Capturing, Sealing, DraftReady, NoContent, Cancelled, Saving, Saved, Failed, CommitUnconfirmed }

    // This projection is owned only by HostBlueprint. UI owns editing buffers,
    // never a second capture job, save decision, or selection lifecycle.
    public sealed class BlueprintCaptureFlow
    {
        public long Revision { get; set; }
        public CaptureToken Token { get; set; }
        public BlueprintCaptureStatus Status { get; set; }
        public long Progress { get; set; }
        public long Total { get; set; }
        public string DraftName { get; set; } = "新蓝图";
        public ContentHandle ContentHandle { get; set; }
        public CaptureSummary Summary { get; set; }
        public bool ExcludeUnreliable { get; set; }
        public int[] ExcludedObjectIds { get; set; } = Array.Empty<int>();
        public string Message { get; set; } = "";
        public Guid? SelectedAssetId { get; set; }
    }
}
