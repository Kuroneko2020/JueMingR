using System;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.F5
{
    // The only pointer-ownership source is State. Leases below remember values
    // changed by this shell, not independent decisions about who owns the mouse.
    internal sealed class F5Shell
    {
        internal readonly F5Interaction State = new F5Interaction();
        private readonly F5Renderer renderer = new F5Renderer();
        private readonly Phase0TBiomeRuntime biome;
        private readonly HostPreferences preferences;
        private static readonly Action<string> displayPreferenceFeedback = message => Main.NewText(message, 255, 180, 90);
        private Player leasedPlayer;
        private bool priorMouseInterface, hoverLease, priorMouseText;
        private bool failed, failureNotified, positionRestored;
        private Matrix matrix;
        internal bool LayersReady { get; set; }
        internal bool Failed { get { return failed; } }

        internal F5Shell(Phase0TBiomeRuntime biome, HostPreferences preferences)
        { this.biome = biome; this.preferences = preferences; }
        internal bool OwnsPointer { get { return !failed && CanPresentNow && State.OwnsPointer; } }

        private static bool CanPresentNow
        {
            get
            {
                Player player = Main.LocalPlayer;
                var capture = Terraria.Graphics.Capture.CaptureManager.Instance;
                return !Main.gameMenu && !Main.dedServ && Main.netMode == 0 && player != null && player.active &&
                    FocusHelper.AllowInputProcessing && !PlayerInput.UsingGamepad && !PlayerInput.ShouldFastUseItem &&
                    !Main.mapFullscreen && !Main.hideUI && !Main.onlyDrawFancyUI && !Main.ingameOptionsWindow &&
                    !Main.inFancyUI && capture != null && !capture.Active;
            }
        }

        internal void ProcessInput()
        {
            try
            {
                RestoreLeases();
                RestorePositionWhenLoaded();
                matrix = Main.UIScaleMatrix;
                Vector2 screen = PlayerInput.OriginalScreenSize;
                Vector2 raw = new Vector2(PlayerInput.MouseInfo.X * PlayerInput.RawMouseScale.X,
                    PlayerInput.MouseInfo.Y * PlayerInput.RawMouseScale.Y);
                bool f5 = Main.keyState.IsKeyDown(Keys.F5);
                Vector2 pointer = State.Visible || f5 ? Vector2.Transform(raw, Matrix.Invert(matrix)) : raw;
                State.Update(new F5Input
                {
                    Width = screen.X, Height = screen.Y, Scale = matrix.M11, X = pointer.X, Y = pointer.Y,
                    Active = CanPresentNow && !PlayerInput.Triggers.Current.MapFull && !PlayerInput.Triggers.Current.ToggleCameraMode,
                    Focused = Terraria.FocusHelper.AllowInputProcessing,
                    F5 = f5,
                    Left = PlayerInput.MouseInfo.LeftButton == ButtonState.Pressed,
                    Right = PlayerInput.MouseInfo.RightButton == ButtonState.Pressed,
                    Wheel = PlayerInput.ScrollWheelDeltaForUI
                });
                if (OwnsPointer) LeaseMouseInterface();
                ConsumeSample();
                SubmitPosition();
                // Settings owns intent; the Feature still owns actual state and
                // its failure latch. Loading or a failure cannot become a click.
                if (State.Command != F5Command.None && preferences.BiomeLoaded && !biome.FeatureFailed)
                    preferences.SetBiomeEnabled(State.Command == F5Command.EnableBiome);
            }
            catch { FailClosed(); ConsumeSample(); }
        }

        private void ConsumeSample()
        {
            if (State.ConsumeLeft)
            {
                PlayerInput.Triggers.Current.MouseLeft = false;
                PlayerInput.Triggers.JustPressed.MouseLeft = false;
                PlayerInput.Triggers.JustReleased.MouseLeft = false;
                Main.mouseLeft = false;
            }
            if (State.ConsumeRight)
            {
                PlayerInput.Triggers.Current.MouseRight = false;
                PlayerInput.Triggers.JustPressed.MouseRight = false;
                PlayerInput.Triggers.JustReleased.MouseRight = false;
                Main.mouseRight = false;
            }
            if (State.ConsumeWheel)
            { PlayerInput.ScrollWheelDelta = 0; PlayerInput.ScrollWheelDeltaForUI = 0; }
            // Absolute wheel and physical MouseInfo are never changed. Consumed
            // button transitions/deltas are never restored or replayed later.
        }

        internal void AfterUpdate()
        {
            try
            {
                RestoreLeases();
                RestorePositionWhenLoaded();
                SubmitPosition();
                // Do not consume a required alert while normal game text is hidden.
                if (!Main.gameMenu && !Main.hideUI) preferences.TakeFeedback(displayPreferenceFeedback);
                if (failed)
                {
                    if (!failureNotified && !Main.gameMenu)
                    {
                        failureNotified = true;
                        Main.NewText("F5 界面已安全关闭：资源、布局或绘制不可用。群系设置保留。", 255, 180, 90);
                    }
                    return;
                }
                if (!CanPresentNow)
                { CloseAndSubmitPosition(); renderer.Dispose(); return; }
                // Documents finish independently; a protected/failed read still
                // finishes loading and permits in-memory use of that document.
                if (!preferences.UiLoaded) { State.Ready = false; return; }
                if (!State.Visible && State.Ready) return;
                State.Ready = LayersReady && renderer.RefreshResources();
                if (!State.Ready) { CloseAndSubmitPosition(); return; }
                if (State.Visible)
                {
                    Vector2 screen = PlayerInput.OriginalScreenSize;
                    renderer.Prepare(State, screen.X, screen.Y, matrix.M11);
                    // Emote Bubbles runs before the modal early-return layers.
                    // Clear only the old pointer-triggered NPC bubble at update end.
                    if (OwnsPointer) Main.instance.currentNPCShowingChatBubble = -1;
                }
            }
            catch { FailClosed(); }
        }

        internal bool BeginPointerLayer()
        {
            try
            {
                RestoreLeases();
                if (!CanPresentNow) { CloseAndSubmitPosition(); return true; }
                if (OwnsPointer)
                {
                    // AfterUpdate already cleared the old bubble before Emote.
                    // Keep this pointer's later NPC hover state clear as well.
                    Main.instance.currentNPCShowingChatBubble = -1;
                    // Claim this pointer's pending text before lower mouse UI can
                    // generate it. null is Terraria's supported no-text value;
                    // DrawPendingMouseText still draws its normal cursor.
                    Main.ClearHoverItem();
                    Main.instance.MouseTextNoOverride(null);
                }
            }
            catch { FailClosed(); }
            return true;
        }

        internal bool HoverGateLayer()
        {
            if (!OwnsPointer) return true;
            try
            {
                // Immediately after Mouse Item / NPC Head: its reset must have
                // already run. Preserve until Interact Item Icon has consumed it.
                LeaseMouseInterface();
                priorMouseText = Main.mouseText;
                hoverLease = true;
                Main.mouseText = true;
            }
            catch { FailClosed(); }
            return true;
        }

        internal bool AllowNpcHover()
        {
            if (!OwnsPointer) return true;
            // DrawMouseOver already resets HoveringOverAnNPC immediately before
            // this call. The skipped body never acquires noThrow or frees the
            // elder slime; do not undo unrelated existing noThrow/interaction.
            Main.instance.currentNPCShowingChatBubble = -1;
            return false;
        }

        internal bool DrawLayer()
        {
            try
            {
                if (!CanPresentNow) { CloseAndSubmitPosition(); RestoreLeases(); }
                else if (State.Visible && State.Ready && !failed)
                    renderer.Draw(State, matrix, biome.FeatureEnabled, biome.FeatureFailed || !preferences.BiomeLoaded);
            }
            catch { FailClosed(); }
            return true;
        }

        internal bool EndPointerLayer() { RestoreLeases(); return true; }

        private void LeaseMouseInterface()
        {
            Player player = Main.LocalPlayer;
            if (player == null) return;
            if (leasedPlayer == null) { leasedPlayer = player; priorMouseInterface = player.mouseInterface; }
            player.mouseInterface = true;
        }

        private void RestoreLeases()
        {
            // Restore the captured player, not a possibly different current LocalPlayer.
            if (leasedPlayer != null)
            {
                if (leasedPlayer.mouseInterface) leasedPlayer.mouseInterface = priorMouseInterface;
                leasedPlayer = null;
            }
            if (hoverLease)
            {
                if (Main.mouseText) Main.mouseText = priorMouseText;
                hoverLease = false;
            }
        }

        private void RestorePositionWhenLoaded()
        {
            if (positionRestored || !preferences.UiLoaded) return;
            State.RestorePosition(preferences.Position);
            positionRestored = true;
        }

        private void SubmitPosition()
        {
            WindowPosition position = State.TakePositionToSave();
            if (position != null) preferences.SetPosition(position);
        }

        internal void CloseAndSubmitPosition() { State.Close(); SubmitPosition(); }

        internal void FailClosed()
        { failed = true; State.Ready = false; CloseAndSubmitPosition(); RestoreLeases(); renderer.Dispose(); }
    }
}
