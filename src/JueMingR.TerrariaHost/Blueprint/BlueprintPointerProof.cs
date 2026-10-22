using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Blueprint
{
    // A bounded native UI certificate, independent of Browser's read-only
    // gestures. It observes slots/resources; it never consumes UI raw input.
    internal sealed class BlueprintPointerProof : IDisposable
    {
        private readonly HostBlueprint host;
        private static BlueprintPointerProof current;
        private readonly Harmony harmony = new Harmony("JueMingR.Blueprint.Pointer");
        private bool pending, completed, inPass, boundary, resources, ui, left, right, drawn, freshLeft, lastLeft = true;
        private long mapping, lastMapping = -1;
        private int x, y, width, height;
        private Vector2 raw, screen, scale;
        private Matrix uiMatrix, zoom;
        private float gravity;
        internal bool HasPoint { get; private set; }
        internal int CellX { get { return x; } }
        internal int CellY { get { return y; } }
        internal BlueprintPointerProof(HostBlueprint host) { this.host = host; }
        internal void Install()
        {
            current = this;
            Patch(typeof(Main), "DrawInterface", new[] { typeof(GameTime) }, nameof(Begin), nameof(End), nameof(Final));
            Patch(typeof(Main), "DrawInterface_39_MouseOver", Type.EmptyTypes, nameof(Boundary)
                );
            // DrawMouseOver can be prepared before this late installation.
            // Its tiny manager wrapper can already be inlined in that caller;
            // patching the wrapper then observes no calls. The interface dispatch
            // reaches these three actual resource implementations. Observe their
            // normal returns, before later world hover can set mouseText. A skip
            // or exception still cannot certify the pass; never invent proof.
            Patch(typeof(Terraria.GameContent.UI.ResourceSets.ClassicPlayerResourcesDisplaySet), "TryToHover", Type.EmptyTypes, null, nameof(Resources));
            Patch(typeof(Terraria.GameContent.UI.ResourceSets.FancyClassicPlayerResourcesDisplaySet), "TryToHover", Type.EmptyTypes, null, nameof(Resources));
            Patch(typeof(Terraria.GameContent.UI.ResourceSets.HorizontalBarsPlayerResourcesDisplaySet), "TryToHover", Type.EmptyTypes, null, nameof(Resources));
            Patch(typeof(ItemSlot), "MouseHover", new[] { typeof(Item[]), typeof(int), typeof(int) }, nameof(Slot));
            Patch(typeof(ItemSlot), "Draw", new[] { typeof(SpriteBatch), typeof(Item[]), typeof(int), typeof(int), typeof(Vector2), typeof(Color) }, nameof(Hotbar));
        }
        private void Patch(Type type, string name, Type[] args, string before = null, string after = null, string final = null)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance, null, args, null);
            if (method == null) throw new MissingMethodException(type.FullName, name);
            harmony.Patch(method, Hook(before), Hook(after), null, Hook(final));
        }
        private static HarmonyMethod Hook(string name) { return name == null ? null : new HarmonyMethod(typeof(BlueprintPointerProof).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)); }
        internal void Capture()
        {
            if (!host.State.Active) return;
            if (pending && mapping == host.Input.Mapping) return;
            pending = true; completed = inPass = drawn = false; mapping = host.Input.Mapping;
            raw = new Vector2(PlayerInput.MouseInfo.X, PlayerInput.MouseInfo.Y); screen = PlayerInput.OriginalScreenSize; scale = PlayerInput.RawMouseScale;
            uiMatrix = Main.UIScaleMatrix; zoom = Main.GameViewMatrix.ZoomMatrix; gravity = Main.LocalPlayer.gravDir;
            HasPoint = false;
            if (!Finite(zoom.M11) || !Finite(zoom.M22) || zoom.M11 <= 0 || zoom.M22 <= 0 || Main.screenWidth <= 0 || Main.screenHeight <= 0 ||
                !Finite(Main.screenPosition.X) || !Finite(Main.screenPosition.Y) || Main.maxTilesX <= 0 || Main.maxTilesY <= 0) {
                Cancel(); host.State.Interrupt(); return; }
            left = host.Input.RawMouseLeft; right = host.Input.RawMouseRight;
            freshLeft = left && !lastLeft && mapping == lastMapping + 1;
            lastLeft = left; lastMapping = mapping;
            Vector2 point = Vector2.Transform(raw * scale, Matrix.Invert(zoom));
            if (gravity == -1) point.Y = Main.screenHeight - point.Y;
            point += Main.screenPosition;
            if (float.IsNaN(point.X) || float.IsInfinity(point.X) || float.IsNaN(point.Y) || float.IsInfinity(point.Y)) {
                Cancel(); host.State.Interrupt(); return; }
            x = (int)Math.Floor(point.X / 16); y = (int)Math.Floor(point.Y / 16); width = Main.maxTilesX; height = Main.maxTilesY;
            HasPoint = true;
        }
        internal void Consume(bool canEdit)
        {
            if (!pending || host.Input.Mapping == mapping) return;
            bool valid = canEdit && host.Input.Mapping == mapping + 1;
            bool certified = completed;
            pending = completed = inPass = false;
            // No Draw is not UI ownership. Retain a held draft across skipped
            // draws, but an unobserved release cannot be replayed later. A press
            // lost in that gap cannot become a new press from a later hold.
            if (valid && !drawn && (!host.State.Dragging || left)) return;
            if (valid && certified && !ui && left && !host.State.Dragging && !freshLeft) return;
            host.State.Pointer(valid && certified && !ui, left, right, x, y, width, height);
        }
        internal void Cancel() {
            pending = completed = inPass = HasPoint = false; lastMapping = -1; lastLeft = true; }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private bool Matches()
        {
            // Camera travel is not a UI-geometry change: keep the frozen world
            // point and certify this mapping's raw pointer at the native UI pass.
            return pending && host.CanRetainEdit && host.Input.Mapping == mapping &&
                raw == new Vector2(PlayerInput.MouseInfo.X, PlayerInput.MouseInfo.Y) && screen == PlayerInput.OriginalScreenSize && scale == PlayerInput.RawMouseScale &&
                uiMatrix == Main.UIScaleMatrix && zoom == Main.GameViewMatrix.ZoomMatrix && gravity == Main.LocalPlayer.gravDir;
        }
        private static void Begin()
        { var p = current;
            if (p == null || !p.pending || p.completed) return; p.drawn = true; p.inPass = p.Matches(); p.boundary = p.resources = p.ui = false;
        }
        private static void Boundary()
        {
            var p = current;
            if (p == null || !p.inPass) return;
            p.boundary = true; p.ui |= Main.LocalPlayer.mouseInterface || Main.mouseText || p.host.NativeUiBusy ||
                p.host.Shell?.State.Visible == true || p.host.Toolbar.HitCurrent();
        }
        private static void Resources(bool __runOriginal)
        { var p = current;
            if (p == null || !p.inPass || !p.boundary) return; p.resources = __runOriginal; p.ui |= Main.LocalPlayer.mouseInterface || Main.mouseText;
        }
        private static void Slot()
        { var p = current; if (p != null && p.inPass) p.ui = true; }
        private static void Hotbar(Item[] __1, int __2, int __3, Vector2 __4)
        {
            var p = current; if (p == null || !p.inPass || __2 != 13 || Main.playerInventory || !ReferenceEquals(__1, Main.LocalPlayer.inventory) || __3 < 0 || __3 >= 10) return;
            var back = Terraria.GameContent.TextureAssets.InventoryBack?.Value;
            if (back != null && Main.mouseX >= __4.X && Main.mouseY >= __4.Y && Main.mouseX < __4.X + back.Width * Main.inventoryScale && Main.mouseY < __4.Y + back.Height * Main.inventoryScale) p.ui = true;
        }
        private static void End(bool __runOriginal)
        { var p = current;
            if (p == null || !p.inPass) return; p.completed = __runOriginal && p.boundary && p.resources && p.Matches(); p.inPass = false;
        }
        private static Exception Final(Exception __exception)
        { if (__exception != null) {
                current?.Cancel(); } return __exception; }
        public void Dispose() {
            Cancel(); harmony.UnpatchAll(harmony.Id); if (ReferenceEquals(current, this)) current = null; }
    }
}
