using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Blueprint
{
    // Game-thread owner of one instance's completed cores and derived commands.
    // Native textures are borrowed each draw, including pending paint requests;
    // retaining a fallback would otherwise permanently hide a ready paint.
    internal sealed class BlueprintProjectionRenderer : IDisposable
    {
        internal const int VisualHaloTiles = 5;
        private BlueprintProjectionWindow window;
        private BlueprintProjectionMask mask;
        private readonly Dictionary<long, Segment> segments = new Dictionary<long, Segment>();
        internal sealed class Segment
        {
            internal CaptureCell[] Cells;
            internal CaptureObject[] Objects;
            internal BlueprintProjectionBounds Bounds;
            internal List<BlueprintProjectionCommand> Commands;
        }
        private List<BlueprintProjectionCommand> commands;
        private readonly Dictionary<long, int> slots = new Dictionary<long, int>();
        private readonly List<BlueprintProjectionCommand> textureKeys = new List<BlueprintProjectionCommand>();
        private int[] commandSlots = Array.Empty<int>();
        private Texture2D[] textures = Array.Empty<Texture2D>();
        private string pendingError;
        internal Exception Failure { get; private set; }
        internal long BuildCount { get; private set; }
        internal long SegmentBuildCount { get; private set; }
        internal int CommandCount { get { return commands == null ? 0 : commands.Count; } }
        internal sealed class WindowStage
        {
            internal Dictionary<long, Segment> Segments;
            internal BlueprintProjectionWindow Window;
            internal BlueprintProjectionMask Mask;
            internal int CommandCount;
        }
        // Build locally, then let Host admit DTO and commands together. Rejected
        // stages own no borrowed textures and never replace a resident core.
        internal WindowStage StageWindow(BlueprintProjectionWindow next, BlueprintProjectionMask nextMask, BlueprintProjectionBounds core, BlueprintProjectionBounds coverage)
        {
            try { return BuildWindow(next, nextMask, core, coverage); }
            catch (Exception error) when (LocalFailure(error)) { Fail(error, "蓝图外观数据与当前游戏不匹配，已停止投影并保留原文件。"); return null; }
        }
        internal void Publish(WindowStage stage)
        {
            segments.Clear(); foreach (var pair in stage.Segments) segments.Add(pair.Key, pair.Value);
            Compose(); window = stage.Window; mask = stage.Mask; BuildCount++;
        }
        internal int RequiredHalo
        {
            get
            {
                // Locked metadata slices fit four tiles beyond their member
                // cell (including FrameOffsets); one more is adjacency-only.
                // A native/resource-pack pylon body has texture-defined size.
                // Requery when that size becomes ready or changes, including
                // when no pylon member was in the previous narrower window.
                Texture2D texture;
                try { texture = Extra(181); }
                catch (Exception error) when (LocalFailure(error))
                { Fail(error, "蓝图绘制资源暂不可用，已停止投影并保留原文件。"); return VisualHaloTiles; }
                if (texture == null || texture.IsDisposed) return VisualHaloTiles;
                float overhang = Math.Max(texture.Width / 28f - 24f, texture.Height / 16f - 24f);
                return Math.Max(VisualHaloTiles, 1 + (int)Math.Ceiling(Math.Max(0, overhang) / 16f));
            }
        }

        internal void SetWindow(BlueprintProjectionWindow next) { SetMaskedWindow(next, BlueprintProjectionMask.Empty); }
        internal void SetMaskedWindow(BlueprintProjectionWindow next, BlueprintProjectionMask nextMask)
        {
            try { SetWindowCore(next, nextMask ?? BlueprintProjectionMask.Empty, null, null); }
            catch (Exception error) when (LocalFailure(error))
            { Fail(error, "蓝图外观数据与当前游戏不匹配，已停止投影并保留原文件。"); }
        }
        internal void UpdateWindow(BlueprintProjectionWindow next, BlueprintProjectionMask nextMask, BlueprintProjectionBounds core, BlueprintProjectionBounds coverage)
        {
            try { SetWindowCore(next, nextMask, core, coverage); }
            catch (Exception error) when (LocalFailure(error)) { Fail(error, "蓝图外观数据与当前游戏不匹配，已停止投影并保留原文件。"); }
        }
        private void SetWindowCore(BlueprintProjectionWindow next, BlueprintProjectionMask nextMask, BlueprintProjectionBounds? core, BlueprintProjectionBounds? coverage)
        {
            // Once this local projection fails, later queued windows cannot
            // overwrite the unconsumed failure or revive it. Host explicitly
            // retires/reopens the identity and calls Clear before retrying.
            if (Failure != null && next != null) return;
            if (!core.HasValue && ReferenceEquals(window, next) && ReferenceEquals(mask, nextMask)) return;
            if (next == null) { Clear(); return; }
            Publish(BuildWindow(next, nextMask, core, coverage));
        }
        private WindowStage BuildWindow(BlueprintProjectionWindow next, BlueprintProjectionMask nextMask, BlueprintProjectionBounds? core, BlueprintProjectionBounds? coverage)
        {
            if (Failure != null) return null;
            var bounds = next.Bounds;
            // Reader coordinates are already local. Summary.Left/Top are used
            // only for source-anchored natural variation, never subtracted twice.
            // Segment ownership is in source coordinates. A shifted window
            // only rebuilds newly arrived/changed segments plus their one-cell
            // adjacency; moving the world origin does not enter this key.
            var visible = nextMask.Apply(next);
            var buckets = new Dictionary<long, List<CaptureCell>>();
            foreach (var cell in visible.Cells)
            {
                long key = SegmentKey(cell.X, cell.Y); List<CaptureCell> values;
                if (!buckets.TryGetValue(key, out values)) { values = new List<CaptureCell>(); buckets.Add(key, values); }
                values.Add(cell);
            }
            var nextSegments = new Dictionary<long, Segment>();
            if (coverage.HasValue) foreach (var pair in segments)
            {
                int sx = (int)(pair.Key & uint.MaxValue), sy = (int)(pair.Key >> 32);
                if (coverage.Value.Contains(sx, sy) && (!core.HasValue || !core.Value.Contains(sx, sy))) nextSegments.Add(pair.Key, pair.Value);
            }
            foreach (var bucket in buckets)
            {
                int sx = (int)(bucket.Key & uint.MaxValue), sy = (int)(bucket.Key >> 32);
                if (core.HasValue && !core.Value.Contains(sx, sy)) continue;
                int l = Math.Max(sx, Math.Max(0, bounds.Left <= 0 ? 0 : bounds.Left + 1));
                int t = Math.Max(sy, Math.Max(0, bounds.Top <= 0 ? 0 : bounds.Top + 1));
                int r = Math.Min(sx + 16, Math.Min(next.Summary.Width, bounds.Right >= next.Summary.Width ? next.Summary.Width : bounds.Right - 1));
                int b = Math.Min(sy + 16, Math.Min(next.Summary.Height, bounds.Bottom >= next.Summary.Height ? next.Summary.Height : bounds.Bottom - 1));
                if (l >= r || t >= b) continue;
                var local = new List<CaptureCell>(); var objectIds = new HashSet<int>();
                for (int by = sy - 16; by <= sy + 16; by += 16) for (int bx = sx - 16; bx <= sx + 16; bx += 16)
                {
                    List<CaptureCell> neighbors; if (!buckets.TryGetValue(Key(bx, by), out neighbors)) continue;
                    foreach (var cell in neighbors) if (cell.X >= l - 1 && cell.X <= r && cell.Y >= t - 1 && cell.Y <= b)
                    { local.Add(cell); if (cell.ObjectId > 0) objectIds.Add(cell.ObjectId); }
                }
                local.Sort((a, c) => a.Y == c.Y ? a.X.CompareTo(c.X) : a.Y.CompareTo(c.Y));
                var objects = new List<CaptureObject>(); foreach (var obj in visible.Objects) if (objectIds.Contains(obj.Id)) objects.Add(obj);
                var region = new BlueprintProjectionBounds(l - 1, t - 1, r - l + 2, b - t + 2);
                Segment old; var cells = local.ToArray(); var metadata = objects.ToArray();
                if (!segments.TryGetValue(bucket.Key, out old) || !region.Equals(old.Bounds) || !Equal(cells, old.Cells) || !Equal(metadata, old.Objects))
                { old = new Segment { Cells = cells, Objects = metadata, Bounds = region, Commands = BlueprintProjectionRendererCommands.Build(next.Summary, cells, metadata, region.Left, region.Top, region.Right, region.Bottom) }; SegmentBuildCount++; }
                nextSegments[bucket.Key] = old;
            }
            var extras = new HashSet<Vector2>(); int count = 0;
            foreach (var segment in nextSegments.Values) foreach (var command in segment.Commands)
                if (command.Texture != BlueprintProjectionTexture.Extra || extras.Add(command.Position)) count++;
            return new WindowStage { Segments = nextSegments, Window = next, Mask = nextMask, CommandCount = count };
        }
        internal void RemoveCore(BlueprintProjectionBounds core)
        {
            window = null; mask = null;
            var remove = new List<long>();
            foreach (long key in segments.Keys) if (core.Contains((int)(key & uint.MaxValue), (int)(key >> 32))) remove.Add(key);
            if (remove.Count == 0) return; foreach (long key in remove) segments.Remove(key); Compose();
        }
        internal void Trim(BlueprintProjectionBounds coverage)
        {
            var remove = new List<long>();
            foreach (var pair in segments) if (!coverage.Contains((int)(pair.Key & uint.MaxValue), (int)(pair.Key >> 32))) remove.Add(pair.Key);
            if (remove.Count == 0) return; foreach (long key in remove) segments.Remove(key); window = null; mask = null; Compose();
        }
        private void Compose()
        {
            commands = new List<BlueprintProjectionCommand>(); var extras = new HashSet<Vector2>();
            foreach (var segment in segments.Values) foreach (var command in segment.Commands)
                if (command.Texture != BlueprintProjectionTexture.Extra || extras.Add(command.Position)) commands.Add(command);
            // Preserve the original global wall/liquid/tile/circuit passes even
            // when a furniture sprite extends into a neighboring segment.
            var ordered = new List<BlueprintProjectionCommand>(commands.Count);
            for (int layer = 0; layer < 5; layer++) foreach (var command in commands) if (command.Layer == layer) ordered.Add(command);
            commands = ordered;
            slots.Clear(); textureKeys.Clear();
            commandSlots = new int[commands.Count];
            for (int i = 0; i < commands.Count; i++)
            {
                var command = commands[i]; long key = ((long)command.Texture << 48) | ((long)command.Type << 16) | (uint)command.Paint;
                int slot;
                if (!slots.TryGetValue(key, out slot)) { slot = textureKeys.Count; slots.Add(key, slot); textureKeys.Add(command); }
                commandSlots[i] = slot;
            }
            textures = new Texture2D[textureKeys.Count];
        }
        private static long Key(int x, int y) { return ((long)y << 32) | (uint)x; }
        private static long SegmentKey(int x, int y) { return Key((int)Math.Floor(x / 16d) * 16, (int)Math.Floor(y / 16d) * 16); }
        private static bool Equal<T>(T[] a, T[] b)
        { if (a.Length != b.Length) return false; var comparer = EqualityComparer<T>.Default; for (int i = 0; i < a.Length; i++) if (!comparer.Equals(a[i], b[i])) return false; return true; }

        internal void Draw(int originX, int originY)
        {
            try { DrawCore(originX, originY); }
            catch (Exception error) when (LocalFailure(error))
            { Fail(error, "蓝图绘制资源暂不可用，已停止投影并保留原文件。"); }
        }
        private void DrawCore(int originX, int originY)
        {
            if (commands == null || Main.spriteBatch == null) return;
            // Resolve once per distinct visible texture key, not once per tile.
            // No owned GPU resources survive a device generation: all slots are
            // refreshed from original resource owners and are never disposed.
            for (int i = 0; i < textureKeys.Count; i++) textures[i] = Resolve(textureKeys[i]);
            Vector2 offset = new Vector2(originX * 16f, originY * 16f);
            for (int i = 0; i < commands.Count; i++)
            {
                var command = commands[i]; var texture = textures[commandSlots[i]];
                if (texture == null || texture.IsDisposed || !ReferenceEquals(texture.GraphicsDevice, Main.spriteBatch.GraphicsDevice)) continue;
                Rectangle source = command.Source; Vector2 position = command.Position + offset;
                Vector2 scale = Vector2.One;
                if (command.Texture == BlueprintProjectionTexture.Pixel)
                { scale = new Vector2(source.Width, source.Height); source = new Rectangle(0, 0, 1, 1); }
                else if (command.Columns > 0)
                {
                    int width = texture.Width / command.Columns, height = texture.Height / command.Rows;
                    source = new Rectangle(source.X * width, source.Y * height, width, height);
                    if (command.Centered) position -= new Vector2(width / 2f, height / 2f);
                }
                if (source.X < 0 || source.Y < 0 || source.Width <= 0 || source.Height <= 0 || source.Right > texture.Width || source.Bottom > texture.Height) continue;
                var effects = command.Effects;
                if (!BlueprintProjectionRendererCommands.Clip(ref position, ref source, ref scale, effects,
                    command.Texture == BlueprintProjectionTexture.Pixel, Main.maxTilesX * 16f, Main.maxTilesY * 16f)) continue;
                bool reverse = Main.LocalPlayer != null && Main.LocalPlayer.gravDir == -1f;
                position -= Main.screenPosition;
                position = Main.ReverseGravitySupport(position, source.Height * scale.Y);
                if (reverse) effects ^= SpriteEffects.FlipVertically;
                // Caller already established the reliable world zoom matrix.
                // We only use its existing reverse-gravity screen conversion.
                Main.spriteBatch.Draw(texture, position, source, command.Color, 0, Vector2.Zero, scale, effects, 0);
            }
        }

        private static Texture2D Resolve(BlueprintProjectionCommand command)
        {
            if (command.Texture == BlueprintProjectionTexture.Pixel) return TextureAssets.MagicPixel != null && TextureAssets.MagicPixel.IsLoaded ? TextureAssets.MagicPixel.Value : null;
            if (command.Texture == BlueprintProjectionTexture.Extra) return Extra(command.Type);
            if (Main.instance == null) return null;
            if (command.Texture == BlueprintProjectionTexture.Tile)
            {
                if (command.Type < 0 || command.Type >= TextureAssets.Tile.Length) return null;
                Main.instance.LoadTiles(command.Type);
                // These resource-only native requests read key/cache/texture
                // owners, unlike GetTileDrawTexture(Tile,x,y)'s ecology path.
                if (command.Paint != 0 && Main.instance.TilePaintSystem != null)
                {
                    var painted = Main.instance.TilePaintSystem.TryGetTileAndRequestIfNotReady(command.Type, 0, command.Paint);
                    if (painted != null) return painted;
                }
                return TextureAssets.Tile[command.Type].IsLoaded ? TextureAssets.Tile[command.Type].Value : null;
            }
            if (command.Type < 0 || command.Type >= TextureAssets.Wall.Length) return null;
            Main.instance.LoadWall(command.Type);
            if (command.Paint != 0 && Main.instance.TilePaintSystem != null)
            {
                var painted = Main.instance.TilePaintSystem.TryGetWallAndRequestIfNotReady(command.Type, command.Paint);
                if (painted != null) return painted;
            }
            return TextureAssets.Wall[command.Type].IsLoaded ? TextureAssets.Wall[command.Type].Value : null;
        }
        private static Texture2D Extra(int type)
        {
            var asset = TextureAssets.Extra != null && type >= 0 && type < TextureAssets.Extra.Length ? TextureAssets.Extra[type] : null;
            if (asset == null) return null;
            if (asset.State == AssetState.NotLoaded && Main.Assets != null) Main.Assets.Request<Texture2D>(asset.Name);
            return asset.IsLoaded ? asset.Value : null;
        }
        internal void Clear()
        {
            window = null; mask = null; commands = null; slots.Clear(); textureKeys.Clear(); segments.Clear();
            commandSlots = Array.Empty<int>(); textures = Array.Empty<Texture2D>();
            pendingError = null; Failure = null;
        }
        internal bool TryTakeError(out string error) { error = pendingError; pendingError = null; return error != null; }
        private void Fail(Exception failure, string error) { Clear(); Failure = failure; pendingError = error; }
        private static bool LocalFailure(Exception error)
        { return error is ArgumentException || error is IndexOutOfRangeException || error is InvalidOperationException || error is OverflowException || error is System.IO.InvalidDataException; }
        public void Dispose() { Clear(); }
    }
}
