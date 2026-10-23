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
    // Game-thread owner of one completed window's derived drawing commands.
    // Native textures are borrowed each draw, including pending paint requests;
    // retaining a fallback would otherwise permanently hide a ready paint.
    internal sealed class BlueprintProjectionRenderer : IDisposable
    {
        internal const int VisualHaloTiles = 5;
        private BlueprintProjectionWindow window;
        private List<BlueprintProjectionCommand> commands;
        private readonly Dictionary<long, int> slots = new Dictionary<long, int>();
        private readonly List<BlueprintProjectionCommand> textureKeys = new List<BlueprintProjectionCommand>();
        private int[] commandSlots = Array.Empty<int>();
        private Texture2D[] textures = Array.Empty<Texture2D>();
        private string pendingError;
        internal Exception Failure { get; private set; }
        internal long BuildCount { get; private set; }
        internal int CommandCount { get { return commands == null ? 0 : commands.Count; } }
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

        internal void SetWindow(BlueprintProjectionWindow next)
        {
            try { SetWindowCore(next); }
            catch (Exception error) when (LocalFailure(error))
            { Fail(error, "蓝图外观数据与当前游戏不匹配，已停止投影并保留原文件。"); }
        }
        private void SetWindowCore(BlueprintProjectionWindow next)
        {
            // Once this local projection fails, later queued windows cannot
            // overwrite the unconsumed failure or revive it. Host explicitly
            // retires/reopens the identity and calls Clear before retrying.
            if (Failure != null && next != null) return;
            if (ReferenceEquals(window, next)) return;
            Clear(); if (next == null) return;
            var bounds = next.Bounds;
            // Reader coordinates are already local. Summary.Left/Top are used
            // only for source-anchored natural variation, never subtracted twice.
            commands = BlueprintProjectionRendererCommands.Build(next.Summary, next.Cells, next.Objects,
                bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
            window = next; BuildCount++;
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

        internal void Draw(int originX, int originY)
        {
            try { DrawCore(originX, originY); }
            catch (Exception error) when (LocalFailure(error))
            { Fail(error, "蓝图绘制资源暂不可用，已停止投影并保留原文件。"); }
        }
        private void DrawCore(int originX, int originY)
        {
            if (window == null || commands == null || Main.spriteBatch == null) return;
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
            window = null; commands = null; slots.Clear(); textureKeys.Clear();
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
