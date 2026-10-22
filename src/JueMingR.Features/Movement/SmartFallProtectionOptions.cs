using System;

namespace JueMingR.Features.Movement
{
    // Durable permissions, never readiness or borrowed game state. The main
    // switch can be OFF without discarding any of the twelve choices.
    public sealed class SmartFallProtectionOptions : IEquatable<SmartFallProtectionOptions>
    {
        public const int CapabilityCount = 12;
        public bool Enabled { get; }
        private readonly ushort permissions;
        public bool DoubleJump => Allows(0);
        public bool RocketBoots => Allows(1);
        public bool Carpet => Allows(2);
        public bool Wings => Allows(3);
        public bool Horseshoe => Allows(4);
        public bool Umbrella => Allows(5);
        public bool Grapple => Allows(6);
        public bool FlyingMount => Allows(7);
        public bool DjinnLegs => Allows(8);
        public bool SafeMount => Allows(9);
        public bool TeleportRod => Allows(10);
        public bool GravityGlobe => Allows(11);
        public SmartFallProtectionOptions(bool enabled = false, ushort permissions = 4095)
        { if ((permissions & ~4095) != 0) throw new ArgumentOutOfRangeException(nameof(permissions)); Enabled = enabled; this.permissions = permissions; }
        public bool Allows(int capability)
        { Validate(capability); return (permissions & (1 << capability)) != 0; }
        public SmartFallProtectionOptions WithEnabled(bool value) => new SmartFallProtectionOptions(value, permissions);
        public SmartFallProtectionOptions WithPermission(int capability, bool value)
        { Validate(capability); return new SmartFallProtectionOptions(Enabled, (ushort)(value ? permissions | (1 << capability) : permissions & ~(1 << capability))); }
        private static void Validate(int capability)
        { if (capability < 0 || capability >= CapabilityCount) throw new ArgumentOutOfRangeException(nameof(capability)); }
        public bool Equals(SmartFallProtectionOptions other) => other != null && Enabled == other.Enabled && permissions == other.permissions;
        public override bool Equals(object other) => Equals(other as SmartFallProtectionOptions);
        public override int GetHashCode() => permissions | (Enabled ? 4096 : 0);
    }
}
