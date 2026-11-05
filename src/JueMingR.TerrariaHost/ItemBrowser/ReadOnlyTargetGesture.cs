using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingR.Platform.Hotkeys;
using JueMingR.TerrariaHost.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using Terraria.UI;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    // Shared observation/gesture lease only. Browser and announcement commands
    // retain distinct state and operation outlets. No delayed pointer resampling.
    internal sealed class ReadOnlyTargetGesture : IDisposable
    {
        private const string Owner = "JueMingR.ItemTargets.ReadOnlyGesture";
        private static ReadOnlyTargetGesture current;
        private readonly Harmony harmony = new Harmony(Owner);
        private readonly List<MethodInfo> patches = new List<MethodInfo>();
        private readonly HostInputState input;
        private readonly NativeItemCatalog catalog;
        private readonly TargetInputSnapshot snapshot = new TargetInputSnapshot();
        private bool recordingPass, sourceRejected;
        private int notificationDepth;
        private Func<bool> uiLease;
        private long tick, expires;
        private long placementDeadline, placementGeneration;
        private int placementProgress;
        private const int PlacementStallSeconds = 10;
        private int pending;
        private bool picking, released, rawPick, inPass, completed, seenSlot, uiBlocked;
        private bool uiBoundary, resourceBoundary, preparingPlacement;
        private object player, world, tiles, connection;
        private bool inventory;
        private int chest;
        private Vector2 raw, screen, mouseScale, camera;
        private Matrix matrix, zoom;
        private float gravity;
        private TargetValue frozen, hovered;
        private int claimedKind;
        private HotkeyChord claimedChord, requestChord;
        private long claimedSample, claimedFrame, requestFocus;
        private bool duplicateClaim;
        private bool NeedsSnapshot { get { return Busy || QueryBinding?.Invoke() != null && CanPick?.Invoke() == true || AnnouncementBinding?.Invoke() != null && CanAnnounce?.Invoke() == true; } }
        internal Func<bool> CanPick { get; set; }
        internal Func<bool> CanAnnounce { get; set; }
        internal Func<bool> CanAbsorbAnnouncement { get; set; }
        internal Func<bool> PointerOwned { get; set; }
        internal Func<Func<bool>> FreezeUiOwner { get; set; }
        internal Func<HotkeyChord> AnnouncementBinding { get; set; }
        internal Func<HotkeyChord> QueryBinding { get; set; }
        internal Action<TargetValue> Picked { get; set; }
        internal Action PickCancelled { get; set; }
        internal Action<TargetValue> Announced { get; set; }
        internal Action<string> Feedback { get; set; }
        internal bool Ready { get; private set; }
        internal bool Picking { get { return picking || pending == 1; } }
        internal bool Busy { get { return picking || pending != 0; } }
        internal ReadOnlyTargetGesture(HostInputState input, NativeItemCatalog catalog)
        {
            this.input = input; this.catalog = catalog;
#if JMR_TARGET_GESTURE_DIAGNOSTIC
            TargetGestureTrace.Install(0);
#endif
            try
            {
                if (current != null || typeof(Main).Module.ModuleVersionId != new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083")) return;
                current = this;
                Patch(typeof(Main), "DrawInterface", new[] { typeof(GameTime) }, nameof(BeginPass), nameof(EndPass), nameof(PassFinally));
                Patch(typeof(Main), "DrawInterface_39_MouseOver", Type.EmptyTypes, nameof(BeforeWorldHover), null);
                // The tiny manager wrapper may already be inlined in a caller
                // prepared before our late installation. Observe the active
                // resource implementation's real normal return, at the same UI
                // boundary before world tooltips can change mouseText.
                Patch(typeof(Terraria.GameContent.UI.ResourceSets.ClassicPlayerResourcesDisplaySet), "TryToHover", Type.EmptyTypes, null, nameof(AfterResources));
                Patch(typeof(Terraria.GameContent.UI.ResourceSets.FancyClassicPlayerResourcesDisplaySet), "TryToHover", Type.EmptyTypes, null, nameof(AfterResources));
                Patch(typeof(Terraria.GameContent.UI.ResourceSets.HorizontalBarsPlayerResourcesDisplaySet), "TryToHover", Type.EmptyTypes, null, nameof(AfterResources));
                Patch(typeof(ItemSlot), "MouseHover", new[] { typeof(Item[]), typeof(int), typeof(int) }, nameof(Hover), null);
                Patch(typeof(ItemSlot), "LeftClick", new[] { typeof(Item[]), typeof(int), typeof(int) }, nameof(Left), null);
                Patch(typeof(ItemSlot), "RightClick", new[] { typeof(Item[]), typeof(int), typeof(int) }, nameof(Right), null);
                Patch(typeof(ItemSlot), "Draw", new[] { typeof(SpriteBatch), typeof(Item[]), typeof(int), typeof(int), typeof(Vector2), typeof(Color) }, nameof(Hotbar), null);
                Patch(typeof(InGamePopups.AchievementUnlockedPopup), "DrawInGame", new[] { typeof(SpriteBatch), typeof(Vector2) }, nameof(BeginNotification), null, nameof(NotificationFinally));
                Patch(typeof(InGamePopups.PlayerWantsToJoinGamePopup), "DrawInGame", new[] { typeof(SpriteBatch), typeof(Vector2) }, nameof(BeginNotification), null, nameof(NotificationFinally));
                // The rectangle overload forwards through a tiny wrapper. Use
                // the real drawing primitive's normal return, not installation
                // of a potentially caller-inlined forwarding method.
                Patch(typeof(Utils), "DrawInvBG", new[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(int), typeof(int), typeof(Color) }, null, nameof(NotificationBounds));
                var infoAccs = typeof(Main).GetMethod("DrawInfoAccs", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (infoAccs == null) throw new MissingMethodException("Main.DrawInfoAccs");
                patches.Add(infoAccs); harmony.Patch(infoAccs, transpiler: Hook(nameof(InfoAccsTranspiler)));
                var inventoryDraw = typeof(Main).GetMethod("DrawInventory", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (inventoryDraw == null) throw new MissingMethodException("Main.DrawInventory");
                patches.Add(inventoryDraw); harmony.Patch(inventoryDraw, transpiler: Hook(nameof(InventoryButtonsTranspiler)));
                Ready = true; input.ClaimsReadOnlyGesture = Claim; input.ReadOnlyGestureDemand = () => NeedsSnapshot;
#if JMR_TARGET_GESTURE_DIAGNOSTIC
                TargetGestureTrace.Install(1);
#endif
            }
            catch { Dispose(); }
        }
        private void Patch(Type type, string name, Type[] signature, string before, string after, string finalizer = null)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, signature, null);
            if (method == null) throw new MissingMethodException(type.FullName, name);
            patches.Add(method); harmony.Patch(method, Hook(before), Hook(after), null, Hook(finalizer));
        }
        private static HarmonyMethod Hook(string name) { return name == null ? null : new HarmonyMethod(typeof(ReadOnlyTargetGesture).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)); }
        internal void BeginPick()
        {
            if (!Ready || Busy || CanPick != null && !CanPick()) return;
            picking = true; released = false; expires = tick + 600; Feedback?.Invoke("松开按钮后点击物品；右键或 Esc 取消");
            player = Main.LocalPlayer; world = Main.ActiveWorldFileData; tiles = Main.tile; connection = Netplay.Connection;
        }
        private int Claim(int down, int pressed)
        {
            if (!Ready) return 0;
            // HostInputState calls Claim only for a trustworthy physical sample.
            // Neutral is a release fact even over a higher pointer owner; owner
            // arbitration below still decides whether a new press may dispatch.
            if (picking && down == 0) released = true;
            if (pressed != 0 && input.ReadOnlySystemModifier) return 0;
            if (NativeControlOwnsPointer()) return 0;
            if (picking)
            {
                if (CanPick != null && !CanPick() || PointerOwned?.Invoke() == true) return 0;
                if (released && (pressed & 3) != 0) { rawPick = true; return pressed & 3; }
                return 0;
            }
            if (PointerOwned?.Invoke() == true) return 0;
            bool announce = pending != 0 && CanAbsorbAnnouncement != null ? CanAbsorbAnnouncement() : CanAnnounce?.Invoke() == true;
            int kind = CanPick != null && CanPick() && ClaimBinding(QueryBinding?.Invoke(), pressed) != 0 ? 1 :
                announce && ClaimBinding(AnnouncementBinding?.Invoke(), pressed) != 0 ? 2 : 0;
            if (kind == 0) return 0;
            claimedKind = kind; claimedChord = kind == 1 ? QueryBinding() : AnnouncementBinding();
            claimedSample = input.MouseSample; claimedFrame = input.Frame;
            // A repeat owns its new physical tail, but never replaces or renews
            // the first target/proof/preparation deadline and is never replayed.
            duplicateClaim = pending != 0;
            return 1 << (claimedChord.MainKey - 256);
        }
        private int ClaimBinding(HotkeyChord binding, int pressed)
        {
            if (binding == null || binding.MainKey < 256) return 0;
            return input.ReadOnlyModifiers == binding.Modifiers ? pressed & 1 << (binding.MainKey - 256) : 0;
        }
        internal void ProcessInput()
        {
            if (claimedKind != 0 && claimedFrame == input.Frame && claimedSample == input.MouseSample &&
                (duplicateClaim || input.Hotkeys.SystemModifier || input.Hotkeys.Modifiers != claimedChord.Modifiers ||
                !ReferenceEquals(claimedChord, claimedKind == 1 ? QueryBinding?.Invoke() : AnnouncementBinding?.Invoke())))
            {
                // The mouse was already removed before native mapping. A late
                // modifier/table change cancels dispatch, never returns its tail.
                input.Hotkeys.ConsumePress(claimedChord.MainKey);
                claimedKind = 0; claimedChord = null; duplicateClaim = false;
            }
#if JMR_TARGET_GESTURE_DIAGNOSTIC
            if (picking || pending != 0) Trace(12, (input.CanStartActions ? 1 : 0) | (input.Hotkeys.IsNew(27) ? 2 : 0) | (input.Hotkeys.IsNew(257) ? 4 : 0) | (rawPick ? 8 : 0));
#endif
            if (picking && (!SessionMatches() || !input.CanStartActions || CanPick != null && !CanPick())) { Cancel("点选已取消"); return; }
            // Only an accepted physical cancellation owns this input frame.
            // System retirement stays in Cancel without consuming unrelated
            // keys. Retain the existing read-only mouse tail until real release.
            if (Busy && input.CanStartActions && SessionMatches() && input.Hotkeys.IsNew(27))
            { input.ConsumeCancellation(27); Cancel(picking ? "点选已取消" : pending == 2 ? "宣告已取消" : "目标查询已取消"); return; }
            if (picking && rawPick)
            {
                rawPick = false;
                if (input.Hotkeys.IsNew(257)) { input.ConsumeCancellation(257); Cancel("点选已取消"); return; }
                if (input.Hotkeys.IsNew(256) && (input.ReadOnlyMouseMask & 1) != 0) { picking = false; Request(1); }
            }
        }
        internal void RequestQuery(HotkeyChord gesture)
        {
            if (!Ready || Busy || !input.CanStartActions || CanPick == null || !CanPick()) return;
            if (NativeControlOwnsPointer()) return;
            if (!AcceptGesture(1, gesture)) { Feedback?.Invoke("这次手势未能安全接管，未查询"); return; }
            requestChord = gesture;
            Request(1);
        }
        internal void RequestAnnouncement(HotkeyChord gesture)
        {
            if (!Ready || Busy || !input.CanStartActions || CanAnnounce == null || !CanAnnounce()) return;
            if (NativeControlOwnsPointer()) return;
            if (!AcceptGesture(2, gesture))
            { Feedback?.Invoke("这次手势未能安全接管，未发送"); return; }
            requestChord = gesture;
            Request(2);
        }
        private bool AcceptGesture(int kind, HotkeyChord gesture)
        {
            if (gesture == null || input.Hotkeys.SystemModifier || PointerOwned?.Invoke() == true || !ReferenceEquals(gesture, kind == 1 ? QueryBinding?.Invoke() : AnnouncementBinding?.Invoke())) return false;
            if (gesture.MainKey < 256) return true;
            return claimedKind == kind && !duplicateClaim && claimedFrame == input.Frame && claimedSample == input.MouseSample &&
                ReferenceEquals(gesture, claimedChord) && input.Hotkeys.Modifiers == gesture.Modifiers &&
                (input.ReadOnlyMouseMask & 1 << (gesture.MainKey - 256)) != 0;
        }
        internal bool NativeControlOwnsPointer()
        {
            if (!Ready || !NeedsSnapshot || !input.CanPrepareText || Main.gameMenu || PointerOwned?.Invoke() == true) return false;
            Vector2 rawPoint = new Vector2(PlayerInput.MouseInfo.X * PlayerInput.RawMouseScale.X, PlayerInput.MouseInfo.Y * PlayerInput.RawMouseScale.Y);
            return snapshot.NativeControlOwns(Vector2.Transform(rawPoint, Matrix.Invert(Main.UIScaleMatrix)));
        }
        private void Request(int kind)
        {
            pending = kind; preparingPlacement = completed = inPass = seenSlot = uiBlocked = false; hovered = null; expires = tick + 4;
#if JMR_TARGET_GESTURE_DIAGNOSTIC
            TargetGestureTrace.Request(kind, tick, input.Mapping, expires);
#endif
            player = Main.LocalPlayer; world = Main.ActiveWorldFileData; tiles = Main.tile; connection = Netplay.Connection;
            requestFocus = input.FocusGeneration;
            uiLease = FreezeUiOwner?.Invoke();
            raw = new Vector2(PlayerInput.MouseInfo.X, PlayerInput.MouseInfo.Y); screen = PlayerInput.OriginalScreenSize;
            matrix = Main.UIScaleMatrix; zoom = Main.GameViewMatrix.ZoomMatrix; mouseScale = PlayerInput.RawMouseScale; camera = Main.screenPosition;
            gravity = Main.LocalPlayer.gravDir; inventory = Main.playerInventory; chest = Main.LocalPlayer.chest;
            Vector2 uiPoint = Vector2.Transform(raw * mouseScale, Matrix.Invert(matrix));
            sourceRejected = !snapshot.Freeze(uiPoint) || !TargetInputSnapshot.WorldInputMatches();
            seenSlot = snapshot.HasSlot; hovered = snapshot.Value;
            // Main.MouseWorld is read only after authenticating this native
            // mapping. UI is already frozen from a complete visible receipt.
            try { frozen = sourceRejected || seenSlot ? null : NativeTargetObservation.World(Main.MouseWorld, catalog, kind == 2); }
            catch { frozen = null; }
        }
#if JMR_TARGET_GESTURE_DIAGNOSTIC
        // This diagnostic comparison never replaces Matches or changes its
        // short-circuit evaluation. Missing objects are a rejected observation,
        // not an excuse to mutate the frozen request or call admission twice.
        private void Trace(int point, int detail = 0)
        {
            if (!TargetGestureTrace.RequestActive) return;
            string phase = "target.local-player";
            try
            {
                int reasons = 0;
                var local = Main.LocalPlayer;
                phase = "target.focus"; if (!input.IsFocused) reasons |= 1;
                phase = "target.player"; if (!ReferenceEquals(player, local)) reasons |= 2;
                phase = "target.world"; if (!ReferenceEquals(world, Main.ActiveWorldFileData)) reasons |= 4;
                phase = "target.tiles"; if (!ReferenceEquals(tiles, Main.tile)) reasons |= 8;
                phase = "target.connection"; if (!ReferenceEquals(connection, Netplay.Connection)) reasons |= 16;
                phase = "target.raw"; if (raw.X != PlayerInput.MouseInfo.X || raw.Y != PlayerInput.MouseInfo.Y) reasons |= 32;
                phase = "target.screen"; if (screen != PlayerInput.OriginalScreenSize) reasons |= 64;
                phase = "target.matrix"; if (matrix != Main.UIScaleMatrix) reasons |= 128;
                phase = "target.zoom"; if (zoom != Main.GameViewMatrix.ZoomMatrix) reasons |= 256;
                phase = "target.mouse-scale"; if (mouseScale != PlayerInput.RawMouseScale) reasons |= 512;
                phase = "target.camera"; if (camera != Main.screenPosition) reasons |= 1024;
                phase = "target.gravity"; if (local == null || gravity != local.gravDir) reasons |= 2048;
                phase = "target.inventory"; if (inventory != Main.playerInventory) reasons |= 4096;
                phase = "target.chest"; if (local == null || chest != local.chest) reasons |= 8192;
                if (pending == 0) reasons |= 16384;
                int state = pending | (picking ? 4 : 0) | (completed ? 8 : 0) | (inPass ? 16 : 0) | (seenSlot ? 32 : 0) |
                    (uiBlocked ? 64 : 0) | (uiBoundary ? 128 : 0) | (resourceBoundary ? 256 : 0) | (preparingPlacement ? 512 : 0);
                phase = "target.record"; TargetGestureTrace.Record(point, tick, input.Mapping, state, reasons, detail);
            }
            catch (Exception error) { TargetGestureTrace.Fail(error, point, phase); }
        }
#endif
        private bool SessionMatches() { return ReferenceEquals(player, Main.LocalPlayer) && ReferenceEquals(world, Main.ActiveWorldFileData) && ReferenceEquals(tiles, Main.tile) && ReferenceEquals(connection, Netplay.Connection); }
        private bool RequestMatches()
        {
            return pending != 0 && input.CanRetainIntent && requestFocus == input.FocusGeneration && SessionMatches() &&
                (requestChord == null || ReferenceEquals(requestChord, pending == 1 ? QueryBinding?.Invoke() : AnnouncementBinding?.Invoke()));
        }
        private bool InputSpaceMatches()
        {
            // Camera travel does not change the frozen world point. Pointer,
            // viewport, gravity and UI/world transforms remain distinct gates.
            return raw.X == PlayerInput.MouseInfo.X && raw.Y == PlayerInput.MouseInfo.Y &&
                screen == PlayerInput.OriginalScreenSize && matrix == Main.UIScaleMatrix && zoom == Main.GameViewMatrix.ZoomMatrix && mouseScale == PlayerInput.RawMouseScale &&
                gravity == Main.LocalPlayer.gravDir;
        }
        private bool UiSpaceMatches() { return !sourceRejected && snapshot.StillMatches() && (uiLease == null || uiLease()) && inventory == Main.playerInventory && chest == Main.LocalPlayer.chest && PointerOwned?.Invoke() != true; }
        private bool Matches() { return RequestMatches() && InputSpaceMatches() && UiSpaceMatches(); }
        private static void BeginPass()
        {
            var owner = current;
#if JMR_TARGET_GESTURE_DIAGNOSTIC
            owner?.Trace(3);
#endif
            if (owner == null) return;
            if (!owner.NeedsSnapshot) { owner.recordingPass = owner.inPass = false; owner.snapshot.Invalidate(); return; }
            owner.snapshot.BeginDraw(); owner.recordingPass = true;
            owner.inPass = owner.pending != 0 && !owner.completed && owner.Matches();
            owner.uiBoundary = owner.resourceBoundary = owner.uiBlocked = false;
        }
        private static void BeforeWorldHover()
        {
            var owner = current;
#if JMR_TARGET_GESTURE_DIAGNOSTIC
            owner?.Trace(4);
#endif
            if (owner == null || !owner.recordingPass) return; owner.uiBoundary = true; owner.uiBlocked = Main.LocalPlayer.mouseInterface || Main.mouseText;
        }
        private static void AfterResources(bool __runOriginal)
        {
            var owner = current;
#if JMR_TARGET_GESTURE_DIAGNOSTIC
            owner?.Trace(5, __runOriginal ? 1 : 0);
#endif
            if (owner == null || !owner.recordingPass || !owner.uiBoundary) return; owner.resourceBoundary = __runOriginal; owner.uiBlocked |= Main.LocalPlayer.mouseInterface || Main.mouseText;
        }
        private static void Hover(Item[] __0, int __1, int __2)
        {
            var owner = current;
            if (owner == null || !owner.inPass || __0 == null || __2 < 0 || __2 >= __0.Length) return;
            bool sameSource = owner.snapshot.Confirm(__0, __1, __2);
            // An unrecorded/native overlay slot cannot reveal the world behind
            // it or turn a late item into the trigger's target.
            if (!sameSource) owner.sourceRejected = true;
        }
        private static void Hotbar(Item[] __1, int __2, int __3, Vector2 __4)
        {
            var owner = current;
            if (owner == null || !owner.recordingPass) return;
            try
            {
                var back = Terraria.GameContent.TextureAssets.InventoryBack?.Value;
                if (back == null) return;
                owner.snapshot.Record(__1, __2, __3, __4, back.Width * Main.inventoryScale, back.Height * Main.inventoryScale);
                if (owner.inPass) owner.snapshot.Confirm(__1, __2, __3);
            }
            catch { owner.sourceRejected = owner.pending != 0; }
        }
        private static bool Left() { return current == null || (current.input.ReadOnlyMouseMask & 1) == 0; }
        private static bool Right() { return current == null || (current.input.ReadOnlyMouseMask & 2) == 0; }
        private static void BeginNotification(out bool __state)
        {
            __state = current != null && current.recordingPass;
            if (__state) current.notificationDepth++;
        }
        private static void NotificationBounds(int __1, int __2, int __3, int __4, bool __runOriginal)
        {
            var owner = current;
            if (owner == null || !owner.recordingPass || owner.notificationDepth == 0 || !__runOriginal) return;
            owner.snapshot.RecordBlocker(new Rectangle(__1, __2, __3, __4));
        }
        private static Exception NotificationFinally(Exception __exception, bool __state)
        {
            var owner = current;
            if (__state && owner != null) owner.notificationDepth--;
            if (__exception != null && owner != null) { owner.snapshot.Invalidate(); owner.sourceRejected = owner.pending != 0; }
            return __exception;
        }
        private static IEnumerable<CodeInstruction> InfoAccsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            MethodInfo draw = typeof(SpriteBatch).GetMethod("Draw", new[] { typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(float), typeof(SpriteEffects), typeof(float) });
            int matches = 0;
            foreach (var instruction in code)
                if (instruction.opcode == OpCodes.Callvirt && Equals(instruction.operand, draw))
                {
                    // Locked .8 IL_1491 is the actual icon; IL_14eb is its
                    // decorative hover border. Observe the icon's real call
                    // site, avoiding a global/inlinable SpriteBatch hook.
                    if (++matches == 1)
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = typeof(ReadOnlyTargetGesture).GetMethod(nameof(DrawInfoIcon), BindingFlags.NonPublic | BindingFlags.Static);
                    }
                }
            if (draw == null || matches != 2) throw new InvalidOperationException("target-info-icon-callsite-shape");
            return code;
        }
        private static IEnumerable<CodeInstruction> InventoryButtonsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            MethodInfo draw = typeof(SpriteBatch).GetMethod("Draw", new[] { typeof(Texture2D), typeof(Vector2), typeof(Color) });
            int matches = 0;
            foreach (var instruction in code)
                if (instruction.opcode == OpCodes.Callvirt && Equals(instruction.operand, draw))
                {
                    // Locked .8: IL_0bac pet/light, IL_0bf3 super cart, and
                    // IL_1259 accessory visibility. The remaining Draw overloads
                    // are unrelated painting and retain their original calls.
                    matches++;
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = typeof(ReadOnlyTargetGesture).GetMethod(matches <= 2 ? nameof(DrawMiscButton) : nameof(DrawAccessoryButton), BindingFlags.NonPublic | BindingFlags.Static);
                }
            if (draw == null || matches != 3) throw new InvalidOperationException("target-inventory-button-callsite-shape");
            return code;
        }
        private static void DrawMiscButton(SpriteBatch batch, Texture2D texture, Vector2 position, Color color)
        {
            batch.Draw(texture, position, color);
            var owner = current;
            if (owner == null || !owner.recordingPass) return;
            // Native r2 is sized from TickOn before choosing on/off/cart art.
            var hitTexture = Terraria.GameContent.TextureAssets.InventoryTickOn.Value;
            owner.snapshot.RecordNativeControl(new Rectangle((int)position.X, (int)position.Y, hitTexture.Width, hitTexture.Height));
        }
        private static void DrawAccessoryButton(SpriteBatch batch, Texture2D texture, Vector2 position, Color color)
        {
            batch.Draw(texture, position, color);
            var owner = current;
            if (owner == null || !owner.recordingPass) return;
            owner.snapshot.RecordNativeControl(new Rectangle((int)position.X, (int)position.Y, texture.Width, texture.Height));
        }
        private static void DrawInfoIcon(SpriteBatch batch, Texture2D texture, Vector2 position, Rectangle? source, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float depth)
        {
            batch.Draw(texture, position, source, color, rotation, origin, scale, effects, depth);
            var owner = current;
            if (owner == null || !owner.recordingPass) return;
            if (rotation != 0 || origin != Vector2.Zero || scale != 1 || texture == null) { owner.snapshot.Invalidate(); return; }
            owner.snapshot.RecordBlocker(new Rectangle((int)position.X, (int)position.Y, texture.Width, texture.Height));
        }
        private static void EndPass(bool __runOriginal)
        {
            var owner = current;
#if JMR_TARGET_GESTURE_DIAGNOSTIC
            owner?.Trace(7, __runOriginal ? 1 : 0);
#endif
            if (owner == null || !owner.recordingPass) return;
            // World item/NPC tooltips also set mouseText later. Only the actual
            // UI boundary, including life/mana hover, can veto a world target.
            bool complete = __runOriginal && owner.uiBoundary && owner.resourceBoundary;
            if (owner.inPass) owner.completed = complete && owner.Matches() && (!owner.snapshot.HasSlot || owner.snapshot.Confirmed);
            owner.inPass = owner.recordingPass = false;
            owner.snapshot.EndDraw(complete);
        }
        private static Exception PassFinally(Exception __exception)
        {
            if (__exception != null && current != null) { current.inPass = current.completed = current.recordingPass = false; current.snapshot.EndDraw(false); }
#if JMR_TARGET_GESTURE_DIAGNOSTIC
            current?.Trace(8, __exception != null ? 1 : 0);
#endif
            return __exception;
        }
        internal void Update(long currentTick)
        {
            tick = currentTick;
            bool timedOut = preparingPlacement ? Stopwatch.GetTimestamp() >= placementDeadline : tick > expires;
#if JMR_TARGET_GESTURE_DIAGNOSTIC
            if (Busy) Trace(9, (timedOut ? 1 : 0) | (Main.gameMenu ? 2 : 0) | (!input.IsFocused ? 4 : 0) | (!SessionMatches() ? 8 : 0));
#endif
            // Proof/pick leases use the outer clock. Once proof exists, only
            // actual catalog cursor progress renews a separate monotonic stall
            // lease: skipped outer Updates spend no preparation work allowance.
            if (Busy && (timedOut || Main.gameMenu || !input.IsFocused || !SessionMatches() || picking && CanPick != null && !CanPick())) { Cancel("未取得有效的新目标，点选或宣告已取消"); return; }
            if (pending != 0 && !Matches()) { Cancel("目标或界面已变化，请重新操作"); return; }
            if (pending == 0 || !completed) return;
            int kind = pending; bool valid = Matches() && (kind == 1 ? CanPick == null || CanPick() : CanAnnounce != null && CanAnnounce()); TargetValue target = seenSlot ? hovered : uiBlocked ? null : frozen;
            valid &= target != null && target.StillMatches();
#if JMR_TARGET_GESTURE_DIAGNOSTIC
            Trace(13, (valid ? 1 : 0) | (target != null ? 2 : 0) | (target?.Placement != null ? 4 : 0));
#endif
            if (valid && target != null && target.Placement != null)
            {
                bool firstPlacementStep = !preparingPlacement;
                if (firstPlacementStep)
                {
                    preparingPlacement = true; placementDeadline = Stopwatch.GetTimestamp() + PlacementStallSeconds * Stopwatch.Frequency;
                    Feedback?.Invoke("正在准备物品对应资料；移动指针或 Esc 可取消");
                }
                try
                {
                    catalog.StepPlacement();
                    if (!firstPlacementStep && placementGeneration != catalog.PlacementGeneration) valid = false;
                    if (firstPlacementStep || catalog.PlacementProgress > placementProgress)
                        placementDeadline = Stopwatch.GetTimestamp() + PlacementStallSeconds * Stopwatch.Frequency;
                    placementProgress = catalog.PlacementProgress; placementGeneration = catalog.PlacementGeneration;
                    if (valid && !catalog.PlacementReady)
                    {
#if JMR_TARGET_GESTURE_DIAGNOSTIC
                        Trace(14, 0);
#endif
                        return;
                    }
                    if (valid) valid = NativeTargetObservation.ResolvePlacement(target, catalog);
#if JMR_TARGET_GESTURE_DIAGNOSTIC
                    Trace(14, valid ? 2 : 1);
#endif
                }
                catch
                {
                    valid = false;
#if JMR_TARGET_GESTURE_DIAGNOSTIC
                    Trace(14, 3);
#endif
                }
            }
#if JMR_TARGET_GESTURE_DIAGNOSTIC
            Trace(10, (valid ? 1 : 0) | (target != null ? 2 : 0) | (target?.Placement != null ? 4 : 0));
#endif
            pending = 0; preparingPlacement = completed = false; frozen = hovered = null; requestChord = null; uiLease = null; snapshot.Retire();
            if (!valid || target == null) { Feedback?.Invoke("目标已变化或被界面遮挡，请重新操作"); if (kind == 1) PickCancelled?.Invoke(); return; }
            if (kind == 1) Picked?.Invoke(target); else Announced?.Invoke(target);
        }
        internal void Cancel(string message = null)
        {
#if JMR_TARGET_GESTURE_DIAGNOSTIC
            Trace(11);
#endif
            bool returnPick = Picking; picking = rawPick = inPass = completed = preparingPlacement = false; pending = 0; frozen = hovered = null; requestChord = null; uiLease = null; snapshot.Retire(); if (message != null) Feedback?.Invoke(message); if (returnPick) PickCancelled?.Invoke();
        }
        // Each observed native method can have Blueprint's independent owner.
        // Retiring this lease removes only our own patches and request state.
        public void Dispose()
        { Ready = false; Cancel(); input.ClaimsReadOnlyGesture = null; input.ReadOnlyGestureDemand = null; foreach (var patch in patches) harmony.Unpatch(patch, HarmonyPatchType.All, Owner); patches.Clear(); if (ReferenceEquals(current, this)) current = null; }
    }
}
