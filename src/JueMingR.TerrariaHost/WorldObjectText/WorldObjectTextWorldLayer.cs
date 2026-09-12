using System;
using System.Collections.Generic;
using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.WorldObjectText;
using Microsoft.Xna.Framework;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.WorldObjectText
{
    internal sealed class WorldObjectTextWorldLayer
    {
        private readonly WorldObjectDiscovery discovery;
        private readonly Func<bool> available;
        private readonly Dictionary<long, Entry> cache = new Dictionary<long, Entry>();
        private readonly LinkedList<long> age = new LinkedList<long>();
        private readonly Entry[] jobs = new Entry[8];
        private readonly Packet[] packets = new Packet[384];
        private int packetCount, epoch;
        private DynamicSpriteFont font;
        private object language;
        internal WorldObjectTextWorldLayer(WorldObjectDiscovery discovery, Func<bool> available) { this.discovery = discovery; this.available = available; }
        internal string Failure { get; private set; }
        internal bool FontUnavailable { get; private set; }
#if DEBUG
        internal int LastDrawn { get; private set; }
        internal int PreparationWork { get; private set; }
#endif
        internal void Clear() { cache.Clear(); age.Clear(); Array.Clear(jobs, 0, jobs.Length); Array.Clear(packets, 0, packets.Length); packetCount = 0; font = null; language = null; }
        internal void Prepare(WorldObjectSettings settings)
        {
            Array.Clear(packets, 0, packetCount); packetCount = 0;
#if DEBUG
            PreparationWork = 0;
#endif
            if (Failure != null || discovery.Candidates.Count == 0) { if (!settings.AnyEnabled && cache.Count != 0) Clear(); return; }
            try
            {
                var currentFont = FontAssets.MouseText == null ? null : FontAssets.MouseText.Value;
                FontUnavailable = currentFont == null;
                if (FontUnavailable) return;
                var currentLanguage = Terraria.Localization.LanguageManager.Instance.ActiveCulture;
                if (!ReferenceEquals(font, currentFont) || !ReferenceEquals(language, currentLanguage)) { Clear(); font = currentFont; language = currentLanguage; discovery.InvalidateTextLayout(); }
                epoch++;
                // Tile floor/ceil widths fluctuate during ordinary motion. Use
                // actual screen/zoom width so moving never invalidates layout.
                float viewportWidth = Main.screenWidth / Main.GameViewMatrix.ZoomMatrix.M11;
                foreach (var candidate in discovery.Candidates)
                {
                    Entry entry; if (!cache.TryGetValue(candidate.Value.Key, out entry)) continue;
                    var style = settings.Style(candidate.Value.Kind); float width = Math.Min(460 * style.Size / 100f, Math.Max(32, viewportWidth - 24));
                    if (!entry.Matches(candidate, style, width)) { Remove(entry); continue; }
                    entry.Seen = epoch; age.Remove(entry.Node); age.AddLast(entry.Node);
                }
                for (int i = 0; i < jobs.Length; i++) if (jobs[i] != null && jobs[i].Seen != epoch) Remove(jobs[i]);
                // Admit at most eight cold/dirty preparations. The retained cache
                // evicts individual oldest entries; reaching 512 never clears it.
                foreach (var candidate in discovery.Candidates)
                {
                    Entry entry;
                    if (cache.TryGetValue(candidate.Value.Key, out entry))
                    {
                        if (!entry.Layout.Ready && Array.IndexOf(jobs, entry) < 0)
                        { int vacant = Array.IndexOf(jobs, null); if (vacant >= 0) jobs[vacant] = entry; }
                        continue;
                    }
                    int slot = Array.IndexOf(jobs, null); if (slot < 0) break;
                    var style = settings.Style(candidate.Value.Kind); float width = Math.Min(460 * style.Size / 100f, Math.Max(32, viewportWidth - 24));
                    if (cache.Count == 512) Remove(cache[age.First.Value]);
                    entry = new Entry { Candidate = candidate, Style = style, Width = width, Seen = epoch,
                        Layout = new NativeWorldTextLayout(font, candidate.Text, style, width), Node = age.AddLast(candidate.Value.Key) };
                    cache.Add(candidate.Value.Key, entry); jobs[slot] = entry;
                }
                for (int i = 0; i < jobs.Length; i++)
                {
                    var entry = jobs[i]; if (entry == null) continue;
                    int work = entry.Layout.Step(512);
#if DEBUG
                    PreparationWork += work;
#endif
                    if (!entry.Layout.Ready) continue;
                    jobs[i] = null;
                    if (!entry.Layout.HasInk) { discovery.Reject(entry.Candidate); Remove(entry); }
                }
                for (int i = 0; i < discovery.SelectedCount; i++)
                {
                    var candidate = discovery.Candidates[i];
                    Entry entry; if (!cache.TryGetValue(candidate.Value.Key, out entry) || !entry.Layout.Ready || !entry.Layout.HasInk) continue;
                    entry.Layout.ApplyColor(settings.Style(candidate.Value.Kind).Rgb);
                    packets[packetCount++] = new Packet { Value = candidate.Value, Layout = entry.Layout };
                }
                // Retire a finite tail of no-longer-near entries per update.
                for (int i = 0; i < 8 && age.First != null && epoch - cache[age.First.Value].Seen > 120; i++) Remove(cache[age.First.Value]);
            }
            catch (Exception e) { Failure = "world-text-prepare-" + e.GetType().Name; Clear(); }
        }
        internal bool Draw()
        {
#if DEBUG
            LastDrawn = 0;
#endif
            if (Failure != null || packetCount == 0 || !available() || !Rendering.WorldPresentation.CanDraw || Main.spriteBatch == null) return true;
            try
            {
                int chests = 0, signs = 0, tombstones = 0;
                Matrix zoom = Main.GameViewMatrix.ZoomMatrix;
                for (int i = 0; i < packetCount; i++)
                {
                    var packet = packets[i]; var value = packet.Value;
                    if (value.Kind == WorldObjectKind.Chest ? chests == 240 : value.Kind == WorldObjectKind.Sign ? signs == 40 : tombstones == 40) continue;
                    float anchor = value.TileY * 16 - Main.screenPosition.Y;
                    if (Main.LocalPlayer != null && Main.LocalPlayer.gravDir == -1) anchor = Main.screenHeight - anchor - 32;
                    var position = new Vector2(value.CenterX - Main.screenPosition.X - packet.Layout.Width / 2, anchor - 6 - packet.Layout.Height);
                    if (!packet.Layout.HasVisibleInk(position, zoom, Main.screenWidth, Main.screenHeight)) continue;
                    packet.Layout.Draw(Main.spriteBatch, position);
                    if (value.Kind == WorldObjectKind.Chest) chests++; else if (value.Kind == WorldObjectKind.Sign) signs++; else tombstones++;
#if DEBUG
                    LastDrawn++;
#endif
                }
            }
            catch (Exception e) { Failure = "world-text-draw-" + e.GetType().Name; Clear(); }
            return true;
        }
        private void Remove(Entry entry)
        { cache.Remove(entry.Candidate.Value.Key); age.Remove(entry.Node); for (int i = 0; i < jobs.Length; i++) if (ReferenceEquals(jobs[i], entry)) jobs[i] = null; }
        internal bool MayPresent(WorldObject value, string text, WorldObjectStyle style)
        {
            Matrix zoom = Main.GameViewMatrix.ZoomMatrix;
            float anchor = value.TileY * 16 - Main.screenPosition.Y;
            if (Main.LocalPlayer != null && Main.LocalPlayer.gravDir == -1) anchor = Main.screenHeight - anchor - 32;
            // Every label lies above this bottom edge. This cheap exact rejection
            // prevents any number of top-edge objects from consuming the reserve.
            if (Vector2.Transform(new Vector2(0, anchor - 4), zoom).Y < 0) return false;
            Entry entry; float width = Math.Min(460 * style.Size / 100f, Math.Max(32, Main.screenWidth / zoom.M11 - 24));
            if (!cache.TryGetValue(value.Key, out entry) || !entry.Layout.Ready || !entry.Matches(new WorldObjectTextCandidate { Value = value, Text = text }, style, width)) return true;
            entry.Seen = epoch + 1; age.Remove(entry.Node); age.AddLast(entry.Node);
            var position = new Vector2(value.CenterX - Main.screenPosition.X - entry.Layout.Width / 2, anchor - 6 - entry.Layout.Height);
            return entry.Layout.HasVisibleInk(position, zoom, Main.screenWidth, Main.screenHeight);
        }
        internal bool IsPrepared(WorldObject value, string text, WorldObjectStyle style)
        {
            Entry entry; float width = Math.Min(460 * style.Size / 100f, Math.Max(32, Main.screenWidth / Main.GameViewMatrix.ZoomMatrix.M11 - 24));
            return cache.TryGetValue(value.Key, out entry) && entry.Layout.Ready && entry.Layout.HasInk && entry.Matches(new WorldObjectTextCandidate { Value = value, Text = text }, style, width);
        }
        private sealed class Entry
        {
            internal WorldObjectTextCandidate Candidate; internal WorldObjectStyle Style; internal float Width; internal int Seen;
            internal NativeWorldTextLayout Layout; internal LinkedListNode<long> Node;
            internal bool Matches(WorldObjectTextCandidate candidate, WorldObjectStyle style, float width)
            { return ReferenceEquals(Candidate.Text, candidate.Text) && Candidate.Value.Kind == candidate.Value.Kind && Style.Size == style.Size && Style.Mode == style.Mode &&
                Style.Lines == style.Lines && Style.Characters == style.Characters && Width == width; }
        }
        private struct Packet { internal WorldObject Value; internal NativeWorldTextLayout Layout; }
    }
}
