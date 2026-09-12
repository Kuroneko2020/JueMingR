using System;
using System.Collections.Generic;
using JueMingR.Platform.WorldObjectText;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.Features.WorldObjectText
{
    public struct WorldObjectTextCandidate { public WorldObject Value; public string Text; }

    // One frozen tile pass and one spatial-history pass. Small camera motion
    // changes ranking immediately, but cannot repeatedly restart discovery.
    public sealed class WorldObjectDiscovery
    {
        private readonly IWorldObjectSource source;
        private readonly Func<int, int, WorldTargetTile> read;
        private readonly WorldObjectSelection[] selections = { new WorldObjectSelection(272), new WorldObjectSelection(56), new WorldObjectSelection(56) };
        private readonly WorldObject[] scratch = new WorldObject[384];
        private readonly List<WorldObjectTextCandidate> candidates = new List<WorldObjectTextCandidate>(392);
        private readonly List<WorldObjectTextCandidate> pending = new List<WorldObjectTextCandidate>(8);
        private readonly Rejected[] rejected = new Rejected[512];
        private readonly Dictionary<long, string> rejectedByPosition = new Dictionary<long, string>();
        private int rejectionCursor, cursor;
        private WorldTargetView pass, historyPass;
        private OpenedPositionQuery historyCursor;
        private WorldObjectSettings settings = WorldObjectSettings.Default;
        private OpenedPositionHistory history;
        private bool chests, signs, tombstones, opened;
        private bool historyFirst;
        private Func<WorldObject, string, WorldObjectStyle, bool> presentation;
        private Func<WorldObject, string, WorldObjectStyle, bool> prepared;
        public WorldObjectDiscovery(IWorldObjectSource source) { this.source = source ?? throw new ArgumentNullException(nameof(source)); read = source.Read; }
        public IReadOnlyList<WorldObjectTextCandidate> Candidates { get { return candidates; } }
        public WorldObjectView View { get; private set; }
        public int SelectedCount { get; private set; }
        public void SetPresentationGate(Func<WorldObject, string, WorldObjectStyle, bool> gate, Func<WorldObject, string, WorldObjectStyle, bool> isPrepared = null) { presentation = gate; prepared = isPrepared; }
        public void Clear()
        { foreach (var selection in selections) selection.Clear(); candidates.Clear(); pending.Clear(); SelectedCount = 0; InvalidateTextLayout(); pass = default(WorldTargetView); cursor = 0; historyCursor = null; }
        public void InvalidateTextLayout() { Array.Clear(rejected, 0, rejected.Length); rejectedByPosition.Clear(); rejectionCursor = 0; }
        public void Reject(WorldObjectTextCandidate candidate)
        {
            selections[(int)candidate.Value.Kind].Remove(candidate.Value.Key);
            var old = rejected[rejectionCursor]; string current;
            if (old.Text != null && rejectedByPosition.TryGetValue(old.Key, out current) && ReferenceEquals(current, old.Text)) rejectedByPosition.Remove(old.Key);
            rejected[rejectionCursor] = new Rejected { Key = candidate.Value.Key, Text = candidate.Text }; rejectedByPosition[candidate.Value.Key] = candidate.Text;
            rejectionCursor = (rejectionCursor + 1) % rejected.Length;
        }
        public void Update(WorldObjectSettings next, OpenedPositionHistory openedHistory)
        {
            // This is label admission only. The independent successful-open
            // observer runs before this method, including when all three are off.
            if (settings.Style(WorldObjectKind.Chest).Mode != next.Style(WorldObjectKind.Chest).Mode) { selections[0].Clear(); historyCursor = null; }
            for (int i = 0; i < 3; i++)
            { var before = settings.Style((WorldObjectKind)i); var after = next.Style((WorldObjectKind)i);
                if (before.Mode != after.Mode || before.Size != after.Size || before.Lines != after.Lines || before.Characters != after.Characters) { InvalidateTextLayout(); break; } }
            settings = next; history = openedHistory;
            var mode = next.Style(WorldObjectKind.Chest).Mode;
            opened = mode == WorldObjectMode.Opened;
            chests = mode == WorldObjectMode.Always ? source.HasDetector : opened && history != null && history.HasAny;
            signs = next.Style(WorldObjectKind.Sign).Mode != WorldObjectMode.Off;
            tombstones = next.Style(WorldObjectKind.Tombstone).Mode != WorldObjectMode.Off;
            if (!chests) selections[0].Clear(); if (!signs) selections[1].Clear(); if (!tombstones) selections[2].Clear();
            if (!chests && !signs && !tombstones) { Clear(); return; }
            WorldObjectView view;
            if (!source.TryBegin(chests, signs || tombstones, out view) || view.Discovery.Width > 1024 || view.Discovery.Height > 1024) { Clear(); return; }
            View = view; var area = view.Discovery;
            bool restart = pass.Width == 0 || pass.ReadRevision != area.ReadRevision || pass.GeometryRevision != area.GeometryRevision || !pass.Intersects(area);
            if (restart) { Clear(); pass = area; }
            foreach (var selection in selections) selection.SetView(view.Visible, view.PlayerX, view.PlayerY);
            // Cold qualification has its own eight slots. A full queue pauses
            // the resumable scan, never restarts it or promotes unverified text
            // into TopK. The 512 rejection cache is only an optimization: even
            // an arbitrarily long blank prefix eventually advances past itself.
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var old = pending[i].Value; WorldObject current; string text;
                if (!Near(old, area) || !WorldObjectResolver.TryResolve(old.TileX, old.TileY, read(old.TileX, old.TileY), read, out current) ||
                    current.Key != old.Key || current.Kind != old.Kind || !Wanted(current) || !source.TryText(current, out text) || text == null || text.Length == 0 || IsRejected(current.Key, text) || !MayPresent(current, text)) pending.RemoveAt(i);
                else if (IsPrepared(current, text)) { selections[(int)current.Kind].Offer(current); pending.RemoveAt(i); }
                else pending[i] = new WorldObjectTextCandidate { Value = current, Text = text };
            }
            int existing = CopyObjects();
            for (int i = 0; i < existing; i++)
            {
                WorldObject old = scratch[i], current; string text;
                if (!Near(old, area) || !WorldObjectResolver.TryResolve(old.TileX, old.TileY, read(old.TileX, old.TileY), read, out current) ||
                    current.Key != old.Key || current.Kind != old.Kind || !Wanted(current) || !source.TryText(current, out text) || text == null || text.Length == 0 || IsRejected(current.Key, text) || !MayPresent(current, text))
                    selections[(int)old.Kind].Remove(old.Key);
                else if (IsPrepared(current, text)) selections[(int)old.Kind].Offer(current);
                else { selections[(int)old.Kind].Remove(old.Key); Queue(current, text); }
            }
            bool tileDemand = signs || tombstones || chests && !opened;
            bool historyDemand = chests && opened;
            if (pending.Count < 8)
            {
                if (historyDemand && historyFirst) ScanHistory(area);
                if (tileDemand) ScanTiles(area);
                if (historyDemand && !historyFirst) ScanHistory(area);
                if (tileDemand && historyDemand) historyFirst = !historyFirst;
            }
            candidates.Clear(); int count = CopyObjects();
            for (int i = 0; i < count; i++)
            { string text; if (source.TryText(scratch[i], out text) && text != null && text.Length != 0) candidates.Add(new WorldObjectTextCandidate { Value = scratch[i], Text = text }); }
            SelectedCount = candidates.Count; candidates.AddRange(pending);
        }
        private int CopyObjects() { int count = 0; foreach (var selection in selections) count += selection.CopyTo(scratch, count); return count; }
        private void ScanTiles(WorldTargetView latest)
        {
            if (cursor >= pass.Width * pass.Height) { pass = latest; cursor = 0; }
            int tiles = 0, objects = 0, total = pass.Width * pass.Height;
            while (cursor < total && tiles < 4096 && objects < 256 && pending.Count < 8)
            {
                int x = pass.X + cursor % pass.Width, y = pass.Y + cursor / pass.Width; cursor++; tiles++;
                if (x < latest.X - 2 || x >= latest.Right || y < latest.Y - 1 || y >= latest.Bottom) continue;
                var tile = read(x, y); if (!Relevant(tile)) continue;
                objects++; WorldObject value;
                if (WorldObjectResolver.TryResolve(x, y, tile, read, out value) && Wanted(value) && Near(value, latest)) Admit(value);
            }
        }
        private void ScanHistory(WorldTargetView latest)
        {
            if (historyCursor == null || !historyPass.Intersects(latest) || historyPass.ReadRevision != latest.ReadRevision)
            { historyPass = latest; historyCursor = history.Query(latest); }
            int remaining = 256;
            while (remaining > 0 && pending.Count < 8)
            {
                var step = historyCursor.MoveNext(remaining); remaining -= historyCursor.WorkUsed;
                if (step == OpenedQueryStep.End) { historyCursor = null; break; }
                if (step == OpenedQueryStep.Pending) break;
                long key = historyCursor.Current; int x = (int)(key >> 32), y = (int)key; WorldObject value;
                if (WorldObjectResolver.TryResolve(x, y, read(x, y), read, out value) && value.Key == key && value.Kind == WorldObjectKind.Chest && Near(value, latest)) Admit(value);
            }
        }
        private void Admit(WorldObject value)
        {
            var selection = selections[(int)value.Kind]; if (!selection.WouldAdmit(value)) return;
            string text;
            if (source.TryText(value, out text) && text != null && text.Length != 0 && !IsRejected(value.Key, text) && MayPresent(value, text))
            { if (IsPrepared(value, text)) selection.Offer(value); else Queue(value, text); }
        }
        private bool IsPrepared(WorldObject value, string text) { return prepared == null || prepared(value, text, settings.Style(value.Kind)); }
        private void Queue(WorldObject value, string text)
        { foreach (var item in pending) if (item.Value.Key == value.Key) return; if (pending.Count < 8) pending.Add(new WorldObjectTextCandidate { Value = value, Text = text }); }
        private bool MayPresent(WorldObject value, string text) { return presentation == null || presentation(value, text, settings.Style(value.Kind)); }
        private bool Wanted(WorldObject value)
        { return value.Kind == WorldObjectKind.Chest ? chests && (!opened || history.Contains(value.Key)) : value.Kind == WorldObjectKind.Sign ? signs : tombstones; }
        private bool Relevant(WorldTargetTile tile)
        {
            if (!tile.Readable || !tile.Active || tile.Inactive) return false;
            return tile.Type == 85 ? tombstones : tile.Type == 55 || tile.Type == 425 || tile.Type == 573 ? signs :
                chests && !opened && (tile.Type == 21 || tile.Type == 467 || tile.Type == 88 || tile.Type == 441 || tile.Type == 468);
        }
        private bool IsRejected(long key, string text)
        { string current; return rejectedByPosition.TryGetValue(key, out current) && ReferenceEquals(current, text); }
        private static bool Near(WorldObject value, WorldTargetView view)
        { return value.TileX < view.Right && value.TileX + value.Width > view.X && value.TileY < view.Bottom && value.TileY + 2 > view.Y; }
        private struct Rejected { internal long Key; internal string Text; }
    }
}
