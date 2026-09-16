using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Map;

namespace JueMingR.TerrariaHost.Map
{
    internal sealed class MapView
    {
        internal readonly Vector2 Position, Offset;
        internal readonly float Zoom, IconScale;
        internal readonly int Width, Height, Alpha;
        internal readonly Matrix UiMatrix;
        internal readonly object Device;
        private readonly object map = Main.Map;
        private readonly int worldWidth = Main.maxTilesX, worldHeight = Main.maxTilesY, epoch = ExplorationMapHooks.Epoch;
        internal object HoverOwner;
        internal MapView(Vector2 position, Vector2 offset, float zoom, float iconScale, int alpha)
        { Position = position; Offset = offset; Zoom = zoom; IconScale = iconScale; Alpha = alpha; Width = Main.screenWidth; Height = Main.screenHeight; UiMatrix = Main.UIScaleMatrix; Device = Main.spriteBatch?.GraphicsDevice; }
        internal bool Valid
        { get { return Finite(Zoom) && Zoom > 0 && Finite(IconScale) && IconScale > 0 && Finite(Position.X) && Finite(Position.Y) && Finite(Offset.X) && Finite(Offset.Y) && Width > 0 && Height > 0; } }
        internal bool MatchesScreen()
        { return Valid && Width == Main.screenWidth && Height == Main.screenHeight && UiMatrix == Main.UIScaleMatrix && ReferenceEquals(Device, Main.spriteBatch?.GraphicsDevice); }
        internal bool MatchesMap()
        { return ReferenceEquals(map, Main.Map) && worldWidth == Main.maxTilesX && worldHeight == Main.maxTilesY && epoch == ExplorationMapHooks.Epoch && !ExplorationMapHooks.Loading; }
        internal Vector2 Project(double x, double y)
        { return new Vector2((float)((x - Position.X) * Zoom + Offset.X), (float)((y - Position.Y) * Zoom + Offset.Y)); }
        internal bool TryPoint(float x, float y, int worldWidth, int worldHeight, out double tileX, out double tileY)
        {
            tileX = tileY = 0;
            if (!Valid || !Finite(x) || !Finite(y) || x < 0 || y < 0 || x >= Width || y >= Height) return false;
            tileX = (x - (double)Offset.X) / Zoom + Position.X; tileY = (y - (double)Offset.Y) / Zoom + Position.Y;
            return tileX >= 0 && tileY >= 0 && tileX < worldWidth && tileY < worldHeight;
        }
        private static bool Finite(float value) { return !Single.IsNaN(value) && !Single.IsInfinity(value); }
    }
    // One native geometry publication and one final hover decision. Participants
    // retain their domain state; later drawn visible icons replace hover ownership.
    internal sealed class FullscreenMapDrawing : IDisposable
    {
        private const string Owner = "JueMingR.Map.Drawing";
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static FullscreenMapDrawing current;
        private readonly Harmony harmony = new Harmony(Owner);
        private readonly List<MethodInfo> patched = new List<MethodInfo>();
        private int references, depth;
        internal event Action Begin;
        internal event Action<MapView> Icons;
        internal event Action<MapView> Overlay;
        internal event Action<MapView> Completed;
        internal MapView Frame { get; private set; }
        internal bool Ready { get; private set; }
        internal static FullscreenMapDrawing Acquire()
        {
            if (current == null) { var next = new FullscreenMapDrawing(); next.Install(); current = next; }
            current.references++; return current;
        }
        private void Install()
        {
            if (typeof(Main).Module.ModuleVersionId != new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083")) throw new InvalidOperationException("map-drawing-identity");
            try
            {
                Patch(typeof(Main).GetMethod("DrawMap", Flags, null, new[] { typeof(GameTime) }, null), nameof(BeginMap), null, nameof(EndMap));
                Patch(typeof(MapIconOverlay).GetMethod("Draw", Flags, null, new[] { typeof(Vector2), typeof(Vector2), typeof(Rectangle?), typeof(float), typeof(float), typeof(int), typeof(string).MakeByRefType() }, null), null, nameof(IconsDrawn), null);
                Main.OnPostFullscreenMapDraw += Post; Ready = true;
            }
            catch { Dispose(); throw; }
        }
        private void Patch(MethodInfo method, string prefix, string postfix, string finalizer)
        { if (method == null) throw new MissingMethodException("map-drawing-method"); patched.Add(method); harmony.Patch(method, Hook(prefix), Hook(postfix), null, Hook(finalizer)); }
        private static HarmonyMethod Hook(string name) { return name == null ? null : new HarmonyMethod(typeof(FullscreenMapDrawing).GetMethod(name, Flags)); }
        private static void BeginMap()
        { var owner = current; if (owner == null || ++owner.depth != 1) return; owner.Frame = null; owner.Begin?.Invoke(); }
        private static Exception EndMap(Exception __exception)
        {
            var owner = current; if (owner == null) return __exception;
            if (--owner.depth == 0) { if (__exception != null) owner.Frame = null; owner.Completed?.Invoke(owner.Frame); }
            return __exception;
        }
        private static void IconsDrawn(MapIconOverlay __instance, Vector2 mapPosition, Vector2 mapOffset, Rectangle? clippingRect, float mapScale, float drawScale, int alpha, ref string text, bool __runOriginal)
        {
            var owner = current;
            if (owner == null || !owner.Ready || owner.depth != 1 || !__runOriginal || !Main.mapFullscreen || Main.gameMenu || Main.hideUI || Main.dedServ || !ReferenceEquals(__instance, Main.MapIcons)) return;
            var frame = new MapView(mapPosition, mapOffset, mapScale, drawScale, alpha); if (!frame.Valid) return;
            owner.Frame = frame; owner.Icons?.Invoke(frame); if (frame.HoverOwner != null) text = "";
        }
        private void Post(Vector2 ignoredPosition, float ignoredScale) { if (depth == 1 && Frame != null) Overlay?.Invoke(Frame); }
        public void Dispose()
        {
            if (references > 1) { references--; return; }
            Ready = false; Main.OnPostFullscreenMapDraw -= Post; if (ReferenceEquals(current, this)) current = null;
            foreach (var method in patched) harmony.Unpatch(method, HarmonyPatchType.All, Owner); patched.Clear(); Frame = null;
        }
    }
}
