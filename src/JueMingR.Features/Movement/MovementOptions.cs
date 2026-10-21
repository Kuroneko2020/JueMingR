using System;

namespace JueMingR.Features.Movement
{
    public enum ContinuousDashMode { Hold, DoubleTap }

    // Durable choices only; a gesture, player or update identity never enters
    // the preference document. Disabled dash still retains its chosen mode.
    public sealed class MovementOptions : IEquatable<MovementOptions>
    {
        public bool SimulatedJumpEnabled { get; }
        public bool ContinuousDashEnabled { get; }
        public bool TeleportCorrectionEnabled { get; }
        public ContinuousDashMode DashMode { get; }
        public SmartFallProtectionOptions SmartFallProtection { get; }
        public MovementOptions(bool jump = false, bool dash = false, bool teleport = false, ContinuousDashMode mode = ContinuousDashMode.DoubleTap, SmartFallProtectionOptions smartFallProtection = null)
        {
            if (mode != ContinuousDashMode.Hold && mode != ContinuousDashMode.DoubleTap) throw new ArgumentOutOfRangeException(nameof(mode));
            SimulatedJumpEnabled = jump; ContinuousDashEnabled = dash; TeleportCorrectionEnabled = teleport; DashMode = mode;
            SmartFallProtection = smartFallProtection ?? new SmartFallProtectionOptions();
        }
        public bool Enabled(int feature) { return feature == 0 ? SimulatedJumpEnabled : feature == 1 ? ContinuousDashEnabled : feature == 2 ? TeleportCorrectionEnabled : feature == 3 ? SmartFallProtection.Enabled : throw new ArgumentOutOfRangeException(nameof(feature)); }
        public MovementOptions WithEnabled(int feature, bool value)
        {
            if (feature < 0 || feature > 3) throw new ArgumentOutOfRangeException(nameof(feature));
            return new MovementOptions(feature == 0 ? value : SimulatedJumpEnabled, feature == 1 ? value : ContinuousDashEnabled, feature == 2 ? value : TeleportCorrectionEnabled, DashMode, feature == 3 ? SmartFallProtection.WithEnabled(value) : SmartFallProtection);
        }
        public MovementOptions WithMode(ContinuousDashMode mode) { return new MovementOptions(SimulatedJumpEnabled, ContinuousDashEnabled, TeleportCorrectionEnabled, mode, SmartFallProtection); }
        public MovementOptions WithSmartFallProtection(SmartFallProtectionOptions value) { return new MovementOptions(SimulatedJumpEnabled, ContinuousDashEnabled, TeleportCorrectionEnabled, DashMode, value ?? throw new ArgumentNullException(nameof(value))); }
        public bool Equals(MovementOptions other) { return other != null && SimulatedJumpEnabled == other.SimulatedJumpEnabled && ContinuousDashEnabled == other.ContinuousDashEnabled && TeleportCorrectionEnabled == other.TeleportCorrectionEnabled && DashMode == other.DashMode && SmartFallProtection.Equals(other.SmartFallProtection); }
        public override bool Equals(object other) { return Equals(other as MovementOptions); }
        public override int GetHashCode() { return ((SimulatedJumpEnabled ? 1 : 0) | (ContinuousDashEnabled ? 2 : 0) | (TeleportCorrectionEnabled ? 4 : 0) | (int)DashMode << 3) ^ SmartFallProtection.GetHashCode() << 4; }
    }
}
