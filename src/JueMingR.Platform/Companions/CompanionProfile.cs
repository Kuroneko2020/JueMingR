using System;
using System.Collections.Generic;

namespace JueMingR.Platform.Companions
{
    public enum CompanionInventoryArea { Inventory, Armor, Dye, Misc, MiscDye }

    public sealed class CompanionBehavior
    {
        public bool Follow { get; set; } = true;
        public bool AttackEnabled { get; set; } = true;
        public bool SelfDefense { get; set; }
        public bool PreferOwner { get; set; } = true;
        public CompanionBehavior Clone()
        { return new CompanionBehavior { Follow = Follow, AttackEnabled = AttackEnabled, SelfDefense = SelfDefense, PreferOwner = PreferOwner }; }
    }

    // NativeData is the complete native character stream, not a network-slot
    // snapshot. Each revision commits it together with identity and preferences.
    public sealed class CompanionProfile
    {
        private readonly byte[] nativeData;
        private readonly CompanionBehavior behavior;
        public CompanionProfile(Guid id, Guid ownerId, string name, long revision, long dataEpoch,
            CompanionBehavior behavior, byte[] nativeData, bool permanentlyDead = false)
        {
            if (id == Guid.Empty || ownerId == Guid.Empty) throw new ArgumentException("Character identity is required.");
            if (String.IsNullOrWhiteSpace(name) || name.Length > 20) throw new ArgumentException("Invalid character name.");
            if (revision <= 0 || dataEpoch <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (nativeData == null || nativeData.Length == 0) throw new ArgumentException("Native character data is required.");
            Id = id; OwnerId = ownerId; Name = name; Revision = revision; DataEpoch = dataEpoch; PermanentlyDead = permanentlyDead;
            this.behavior = (behavior ?? throw new ArgumentNullException(nameof(behavior))).Clone();
            this.nativeData = (byte[])nativeData.Clone();
        }
        public Guid Id { get; }
        public Guid OwnerId { get; }
        public string Name { get; }
        public long Revision { get; }
        public long DataEpoch { get; }
        public bool PermanentlyDead { get; }
        public CompanionBehavior Behavior { get { return behavior.Clone(); } }
        public byte[] NativeData { get { return (byte[])nativeData.Clone(); } }
        public CompanionProfile Revise(byte[] data, CompanionBehavior preferences = null, bool reset = false, bool? permanentlyDead = null)
        { return new CompanionProfile(Id, OwnerId, Name, checked(Revision + 1), reset ? checked(DataEpoch + 1) : DataEpoch, preferences ?? behavior, data, reset ? false : permanentlyDead ?? PermanentlyDead); }
    }

    public sealed class CompanionOwnerArchive
    {
        public CompanionOwnerArchive(Guid ownerId, IReadOnlyList<CompanionProfile> profiles, string error = null)
        { OwnerId = ownerId; Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles)); Error = error; }
        public Guid OwnerId { get; }
        public IReadOnlyList<CompanionProfile> Profiles { get; }
        public string Error { get; }
    }

    // Called only by the single document worker. The host captures native bytes
    // on the game thread; infrastructure never receives a live Player or Item.
    public interface ICompanionArchive : IDisposable
    {
        CompanionOwnerArchive LoadOwner(string characterKey);
        void LinkOwner(string characterKey, Guid ownerId);
        void Save(CompanionProfile profile);
    }
}
