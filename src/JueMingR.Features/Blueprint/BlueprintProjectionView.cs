using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    // Saved templates/directories keep raw saved coordinates. Completed DTOs
    // subtract Summary.Left/Top once; sparse masks and source cores use that
    // normalized space. Physical offsets/ordinals never become display order.
    // Pose changes interpretation only: renderers borrow source pixels while
    // game consumers receive canonical semantic targets from this same view.
    public sealed class BlueprintProjectionView
    {
        public BlueprintProjectionView(CaptureSummary summary, bool mirroredHorizontally, BlueprintProjectionMask mask,
            IBlueprintHorizontalMirrorProfile profile = null)
        {
            if (summary == null || summary.Width <= 0 || summary.Height <= 0) throw new ArgumentException("blueprint-view-size");
            Width = summary.Width; Height = summary.Height; SourceLeft = summary.Left; SourceTop = summary.Top;
            MirroredHorizontally = mirroredHorizontally; Mask = mask ?? BlueprintProjectionMask.Empty; Profile = profile;
        }
        public int Width { get; }
        public int Height { get; }
        public int SourceLeft { get; }
        public int SourceTop { get; }
        public bool MirroredHorizontally { get; }
        public BlueprintProjectionMask Mask { get; }
        public IBlueprintHorizontalMirrorProfile Profile { get; }
        public long ProfileRevision { get { return Profile?.Revision ?? 0; } }
        public int DisplayX(int sourceX) { return MirroredHorizontally ? checked(Width - 1 - sourceX) : sourceX; }
        public int SourceX(int displayX) { return DisplayX(displayX); }
        public BlueprintProjectionBounds DisplayBounds(BlueprintProjectionBounds source)
        { return MirroredHorizontally ? new BlueprintProjectionBounds(checked(Width - source.Right), source.Top, source.Width, source.Height) : source; }
        public BlueprintProjectionBounds SourceBounds(BlueprintProjectionBounds display) { return DisplayBounds(display); }
        public bool SamePose(BlueprintProjectionView other)
        { return other != null && Width == other.Width && Height == other.Height && SourceLeft == other.SourceLeft && SourceTop == other.SourceTop &&
            MirroredHorizontally == other.MirroredHorizontally && ReferenceEquals(Mask, other.Mask) && ReferenceEquals(Profile, other.Profile) && ProfileRevision == other.ProfileRevision; }

        public static BlueprintProjectionBounds InclusiveRectangle(int x1, int y1, int x2, int y2, int width, int height)
        {
            long left = Math.Max(0L, Math.Min((long)x1, x2)), top = Math.Max(0L, Math.Min((long)y1, y2));
            long right = Math.Min((long)width, Math.Max((long)x1, x2) + 1), bottom = Math.Min((long)height, Math.Max((long)y1, y2) + 1);
            if (right <= left || bottom <= top) return new BlueprintProjectionBounds(0, 0, 0, 0);
            return new BlueprintProjectionBounds(checked((int)left), checked((int)top), checked((int)(right - left)), checked((int)(bottom - top)));
        }

        // Source transport remains in physical order. Complete object consumers
        // explicitly request display row-major, never reinterpret raw offsets.
        public bool TryApply(BlueprintProjectionWindow source, bool displayRowMajor, out BlueprintProjectionWindow target, out string reason, bool keepEmpty = false)
        {
            target = null; reason = null;
            if (source == null) { reason = "blueprint-view-source"; return false; }
            if (ReferenceEquals(source.View, this)) { target = source; return true; }
            source = source.SourceWindow ?? source;
            if (MirroredHorizontally && (Profile == null || !Profile.Ready)) { reason = "blueprint-mirror-profile-not-ready"; return false; }
            var visible = new List<CaptureCell>(source.Cells.Count);
            var members = new Dictionary<int, List<CaptureCell>>();
            foreach (var raw in source.Cells)
            {
                var cell = Mask.Apply(raw);
                if (!keepEmpty && !cell.HasContent) continue;
                visible.Add(cell);
                if ((cell.Layers & CaptureLayer.Tile) == 0 || cell.ObjectId <= 0) continue;
                List<CaptureCell> group;
                if (!members.TryGetValue(cell.ObjectId, out group)) members.Add(cell.ObjectId, group = new List<CaptureCell>());
                group.Add(cell);
            }
            var metadata = new List<CaptureObject>();
            var transformed = new Dictionary<long, CaptureCell>();
            foreach (var obj in source.Objects)
            {
                List<CaptureCell> group;
                if (!members.TryGetValue(obj.Id, out group)) continue;
                group.Sort(Compare);
                if (group.Count != (long)obj.Width * obj.Height) { reason = "blueprint-mirror-object-incomplete"; return false; }
                CaptureObject result = obj;
                if (MirroredHorizontally)
                {
                    CaptureCell[] cells;
                    if (!Profile.TryMirrorObject(obj, group, Width, out result, out cells, out reason)) return false;
                    if (cells == null || cells.Length != group.Count) { reason = "blueprint-mirror-object-members"; return false; }
                    foreach (var cell in cells) transformed.Add(Key(SourceX(cell.X), cell.Y), cell);
                }
                else if (Profile != null && Profile.Ready && !Profile.TryNormalizeObject(obj, group, out result, out reason)) return false;
                metadata.Add(result);
            }
            var output = new CaptureCell[visible.Count];
            for (int i = 0; i < output.Length; i++)
            {
                var cell = visible[i];
                if (MirroredHorizontally)
                {
                    if ((cell.Layers & CaptureLayer.Tile) != 0 && cell.ObjectId > 0)
                    {
                        CaptureCell member;
                        if (!transformed.TryGetValue(Key(cell.X, cell.Y), out member)) { reason = "blueprint-mirror-object-metadata"; return false; }
                        cell = member;
                    }
                    else if (!Profile.TryMirrorCell(cell, Width, out cell, out reason)) return false;
                }
                output[i] = cell;
            }
            if (displayRowMajor) Array.Sort(output, Compare);
            target = new BlueprintProjectionWindow(source.Identity, source.WindowGeneration, source.Summary, DisplayBounds(source.Bounds), output, metadata.ToArray(), true, Mask, this, source, !displayRowMajor);
            return true;
        }
        private static int Compare(CaptureCell a, CaptureCell b) { int y = a.Y.CompareTo(b.Y); return y == 0 ? a.X.CompareTo(b.X) : y; }
        private static long Key(int x, int y) { return ((long)y << 32) | (uint)x; }
    }
}
