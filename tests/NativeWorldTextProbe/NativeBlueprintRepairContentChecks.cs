using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintRepairContentChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(Type sourceType)
        {
            var previous = Main.tile;
            try
            {
                Main.tile = new Tile[120, 120];
                for (int x = 0; x < 120; x++) for (int y = 0; y < 120; y++) Main.tile[x, y] = new Tile();
                Func<IBlueprintCaptureSource> source = () => (IBlueprintCaptureSource)Activator.CreateInstance(sourceType, F, null, new object[] { (Func<bool>)(() => true) }, null);
                // Literal frame samples are derived from the locked .8 original
                // WorldGen.Place4x2 / Place1x2 and metadata Calculate. They are
                // deliberately not generated from the resolver's metadata math.
                foreach (int facing in new[] { 0, 72 }) Check(source(), Pattern(79, 4, new[] { 108, 126 }, facing), 3, 1, "bed negative padding, style 3/facing " + facing);
                foreach (ushort type in new ushort[] { 15, 497 })
                    foreach (int facing in new[] { 0, 18 }) Check(source(), Pattern(type, 1, new[] { 40, 58 }, facing), 0, 1, "positive padding style 1/facing " + facing);
                // PlaceDoor and CloseDoor randomize X independently per row;
                // style 37 crosses the original 36-style vertical page boundary.
                var door = Pattern(10, 1, new[] { 54, 72, 90 }, 54);
                door[1].FrameX = 72; door[2].FrameX = 90;
                for (int row = 0; row < 3; row++) Check(source(), door, 0, row, "mixed closed-door members from row " + row);
                // OpenDoor uses its own width/direction grid, not closed-door
                // random slots. WorldGen.OpenDoor/CloseDoor use X modulo 72.
                foreach (int facing in new[] { 72, 108 }) Check(source(), Pattern(11, 2, new[] { 54, 72, 90 }, facing), 1, 2, "open-door direction " + facing);
                // GetStyle_* and original WorldGen.PlaceSmallPile,
                // PlaceUncheckedStalactite: shape regions are raw Y offsets.
                Check(source(), Pattern(185, 1, new[] { 0 }, 90), 0, 0, "small-pile 1x1");
                Check(source(), Pattern(185, 2, new[] { 18 }, 108), 1, 0, "small-pile 2x1");
                Check(source(), Pattern(233, 3, new[] { 0, 18 }, 108), 2, 1, "detritus 3x2");
                Check(source(), Pattern(233, 2, new[] { 36, 54 }, 108), 1, 1, "detritus 2x2");
                foreach (int attachment in new[] { 0, 36, 72, 90 })
                    Check(source(), Pattern(165, 1, attachment < 72 ? new[] { attachment, attachment + 18 } : new[] { attachment }, 54), 0, 0, "stalactite attachment " + attachment);
                Check(source(), Pattern(3, 1, new[] { 0 }, 54), 0, 0, "ordinary grass uses registered metadata");

                Clear(); Set(Pattern(10, 1, new[] { 0, 18, 36 }, 0)); Main.tile[20, 20].active(false);
                var selected = Selection(20, 22); var broken = Drain(new CaptureJob(source(), selected.Freeze(), 0));
                Require(broken.Item2.UnreliableObjectCount == 1 && broken.Item1.Single().UnreliableObject, "real fixed object missing a member stays explicitly unreliable");
                Set(Pattern(10, 1, new[] { 0, 18, 36 }, 0));
                var changing = new Changing(source()); var changed = Drain(new CaptureJob(changing, selected.Freeze(), 0));
                Require(changed.Item2.UnreliableObjectCount == 1, "legal independent frames still reject a member changing during the verification pass");

                Clear(); var natural = Pattern(5, 1, new[] { 198, 216, 234 }, 22); Set(natural);
                selected = Selection(20, 20); selected.ToggleRectangle(20, 22, 20, 22, 120, 120);
                var job = new CaptureJob(source(), selected.Freeze(), 10); var captured = Drain(job);
                Require(job.TotalReads == 2 && captured.Item1.Count == 2 && captured.Item1.All(c => c.ObjectId == 0 && c.FrameX == 22) && captured.Item2.Height == 3,
                    "selected tree fragment retains raw frames/holes without traversing the unselected tree");
                RoundTrip(captured, "natural-tree-fragment");
                Console.WriteLine("PASS Blueprint repair original-frame mechanisms: +/-PaddingFix, mixed/style-wrapped closed door, independent open-door directions, all native selector shape branches, natural snapshot and real missing/change rejection; capture/draft/preview/Saved/reopen chain.");
            }
            finally { Main.tile = previous; }
        }
        private static CaptureCell[] Pattern(ushort type, int width, int[] rows, int startX)
        {
            var values = new CaptureCell[width * rows.Length];
            for (int row = 0; row < rows.Length; row++) for (int col = 0; col < width; col++)
                values[row * width + col] = new CaptureCell { X = 20 + col, Y = 20 + row, Layers = CaptureLayer.Tile, TileType = type, FrameX = (short)(startX + col * 18), FrameY = (short)rows[row] };
            return values;
        }
        private static void Set(CaptureCell[] values)
        {
            foreach (var cell in values) { var tile = Main.tile[cell.X, cell.Y]; tile.active(true); tile.type = cell.TileType; tile.frameX = cell.FrameX; tile.frameY = cell.FrameY; }
        }
        private static void Clear()
        { for (int x = 19; x < 26; x++) for (int y = 19; y < 25; y++) Main.tile[x, y] = new Tile(); }
        private static BlueprintSelection Selection(int x, int y)
        { var result = new BlueprintSelection(); result.ToggleRectangle(x, y, x, y, 120, 120); return result; }
        private static void Check(IBlueprintCaptureSource source, CaptureCell[] values, int col, int row, string label)
        {
            Clear(); Set(values); var selected = Selection(20 + col, 20 + row);
            // Only the selected member may contribute non-tile layers. The
            // completed footprint cannot import its unselected surroundings.
            foreach (var cell in values) { var tile = Main.tile[cell.X, cell.Y]; tile.wall = 7; tile.wire(true); tile.actuator(true); tile.liquid = 123; tile.color(9); }
            var job = new CaptureJob(source, selected.Freeze(), 1); var captured = Drain(job);
            Console.WriteLine("REPAIR FRAME " + label + " cells=" + captured.Item1.Count + " objects=" + captured.Item3.Count + " unreliable=" + captured.Item2.UnreliableObjectCount + " reasons=" + string.Join(";", captured.Item2.UnreliableReasons));
            Require(captured.Item2.UnreliableObjectCount == 0 && captured.Item1.Count == values.Length && captured.Item1.All(c => c.ObjectId == 1), "original literal sample forms one full fixed object: " + label);
            Require(captured.Item1.Single(c => c.X == 20 + col && c.Y == 20 + row).Layers == (CaptureLayer)31 && captured.Item1.Count(c => c.Layers != CaptureLayer.Tile) == 1,
                "selected layers stay, completed outside footprint carries tile only: " + label);
            Require(captured.Item1.All(c => values.Any(v => v.X == c.X && v.Y == c.Y && v.FrameX == c.FrameX && v.FrameY == c.FrameY)), "raw member frames are unchanged: " + label);
            if (values[0].TileType == 10 && row == 1) RoundTrip(captured, "mixed-door-fixed-draft");
        }
        private static Tuple<List<CaptureCell>, CaptureSummary, List<CaptureObject>> Drain(CaptureJob job)
        {
            var cells = new List<CaptureCell>(); var objects = new List<CaptureObject>();
            for (int n = 0; n < 1000 && !job.Complete && job.Error == null; n++)
            {
                job.Advance(1, 100); Require(job.LastReads <= 1, "actual source metadata projection fits one counted Tile read");
                if (job.PendingChunk == null) continue;
                cells.AddRange(job.PendingChunk.Cells.Take(job.PendingChunk.Count)); objects.AddRange(job.PendingChunk.Objects.Take(job.PendingChunk.ObjectCount)); job.AcceptChunk();
            }
            Require(job.Complete && job.Error == null, "literal sample capture completes: " + job.Error);
            return Tuple.Create(cells, job.Summary(20), objects);
        }
        private static LibraryReceipt Complete(LibraryState state, bool accepted)
        {
            Require(accepted, "real library accepts fixed sample command"); LibraryReceipt receipt = null;
            NativeQuickItemChecks.Until(() => { state.Pump(); return state.TryTakeReceipt(out receipt); }); return receipt;
        }
        private static void RoundTrip(Tuple<List<CaptureCell>, CaptureSummary, List<CaptureObject>> captured, string label)
        {
            string directory = Path.Combine(Terraria.Program.SavePath, "repair-content-" + label); Guid asset;
            using (var state = new LibraryState(new FileBlueprintLibrary(directory)))
            {
                var token = new CaptureToken(Guid.NewGuid(), 1, 2, 3); var handle = new ContentHandle(token.OperationId);
                Require(Complete(state, state.TryBeginStage(token)).Status == LibraryStatus.Completed, "fixed sample stage begins");
                Require(Complete(state, state.TryAppendChunk(handle, new CaptureChunk(captured.Item1.ToArray(), captured.Item1.Count, captured.Item3.ToArray(), captured.Item3.Count))).Status == LibraryStatus.Completed, "raw frames enter bounded stage");
                Require(Complete(state, state.TrySealDraft(handle, captured.Item2)).Status == LibraryStatus.Completed, "fixed sample seals");
                var preview = Complete(state, state.TryPreview(501, handle, 231, 58));
                Require(preview.Preview != null && preview.Summary.ContentCount == captured.Item1.Count, "fixed sample preview consumes all content before saving");
                // Changing the original live tiles cannot change the sealed
                // draft or the subsequently confirmed persistent content.
                Main.tile[20, 20].type = 1;
                var saved = Complete(state, state.TryCreate(handle, label, token));
                Require(saved.Status == LibraryStatus.Saved, "normal content reaches reliable Saved"); asset = saved.AssetId;
            }
            using (var reopened = new LibraryState(new FileBlueprintLibrary(directory)))
            {
                var page = Complete(reopened, reopened.TryList(502, 0)).Page;
                Require(page.Entries.Count == 1 && page.Entries[0].AssetId == asset && page.Entries[0].ContentCount == captured.Item1.Count, "new library instance rereads saved asset");
                var preview = Complete(reopened, reopened.TryPreview(503, asset, page.Entries[0].ContentHash, 231, 58));
                Require(preview.Preview != null && preview.Summary.ContentCount == captured.Item1.Count, "saved content preview survives a worker restart");
            }
            var decode = typeof(CaptureCell).Assembly.GetType("JueMingR.Features.Blueprint.LibraryCodec", true).GetMethod("DecodeChunk", F);
            var reread = Directory.EnumerateFiles(Path.Combine(directory, asset.ToString("N"), "content", "chunks")).SelectMany(p => {
                var chunk = (CaptureChunk)decode.Invoke(null, new object[] { File.ReadAllBytes(p) }); return chunk.Cells.Take(chunk.Count); }).ToArray();
            Require(reread.Length == captured.Item1.Count && reread.All(c => captured.Item1.Any(v => c.FrameX == v.FrameX && c.FrameY == v.FrameY && c.TileType == v.TileType && c.Layers == v.Layers && c.ObjectId == v.ObjectId)), "reopened raw body retains original frames/layers without rereading the changed world");
        }
        private sealed class Changing : IBlueprintCaptureSource
        {
            private readonly IBlueprintCaptureSource inner; private int reads;
            internal Changing(IBlueprintCaptureSource inner) { this.inner = inner; }
            public bool Read(int x, int y, out CaptureCell cell)
            { if (++reads == 5) Main.tile[20, 20].frameX = 18; return inner.Read(x, y, out cell); }
            public BlueprintObjectShape Describe(CaptureCell cell) { return inner.Describe(cell); }
        }
    }
}
