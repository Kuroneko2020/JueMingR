using System;
using System.Collections.Generic;

namespace JueMingR.Platform.Companions
{
    public enum CompanionInventoryArea { Inventory, Armor, Dye, Misc, MiscDye, PiggyBank, Safe, DefenderForge, VoidVault }
    public enum CompanionHealthRecoveryMode { Fast = 1, Smart = 2 }
    public enum CompanionSetting { Follow, Attack, Life, Mana, Buff, Growth, Respawn, Nurse, Furniture }

    public readonly struct CompanionWeaponPreference
    {
        public CompanionWeaponPreference(int type, int prefix, int slotHint)
        { Type = type; Prefix = prefix; SlotHint = slotHint; }
        public int Type { get; }
        public int Prefix { get; }
        public int SlotHint { get; }
    }

    public sealed class CompanionBehavior
    {
        public const int MaxMainWeapons = 2;
        public const int MainInventorySlots = 50;
        public const int MaxSupplyTypes = 4096;

        public bool Follow { get; set; } = true;
        public bool AttackEnabled { get; set; } = true;
        public bool AutoRespawn { get; set; } = true;
        public CompanionWeaponPreference[] MainWeapons { get; set; } = Array.Empty<CompanionWeaponPreference>();
        public bool HealthRecoveryEnabled { get; set; } = true;
        public CompanionHealthRecoveryMode HealthRecoveryMode { get; set; } = CompanionHealthRecoveryMode.Smart;
        public bool ManaRecoveryEnabled { get; set; } = true;
        public bool AllowNurse { get; set; } = true;
        public bool FurnitureEnabled { get; set; }
        public bool BuffEnabled { get; set; } = true;
        public bool GrowthEnabled { get; set; } = true;
        // Empty override sets preserve potion defaults. Empty permission sets grant nothing.
        public int[] ExcludedHealingTypes { get; set; } = Array.Empty<int>();
        public int[] ExcludedManaTypes { get; set; } = Array.Empty<int>();
        public int[] AllowedGrowthTypes { get; set; } = Array.Empty<int>();
        public int[] AllowedBuffTypes { get; set; } = Array.Empty<int>();

        public int SettingValue(CompanionSetting setting)
        {
            switch (setting)
            {
                case CompanionSetting.Follow: return Follow ? 1 : 0;
                case CompanionSetting.Attack: return AttackEnabled ? 1 : 0;
                case CompanionSetting.Life: return HealthRecoveryEnabled ? (int)HealthRecoveryMode : 0;
                case CompanionSetting.Mana: return ManaRecoveryEnabled ? 1 : 0;
                case CompanionSetting.Buff: return BuffEnabled ? 1 : 0;
                case CompanionSetting.Growth: return GrowthEnabled ? 1 : 0;
                case CompanionSetting.Nurse: return AllowNurse ? 1 : 0;
                case CompanionSetting.Furniture: return FurnitureEnabled ? 1 : 0;
                case CompanionSetting.Respawn: return AutoRespawn ? 1 : 0;
                default: throw new ArgumentOutOfRangeException(nameof(setting));
            }
        }

        public CompanionBehavior Clone()
        {
            var copy = (CompanionBehavior)MemberwiseClone();
            if (MainWeapons.Length != 0) copy.MainWeapons = (CompanionWeaponPreference[])MainWeapons.Clone();
            if (ExcludedHealingTypes.Length != 0) copy.ExcludedHealingTypes = (int[])ExcludedHealingTypes.Clone();
            if (ExcludedManaTypes.Length != 0) copy.ExcludedManaTypes = (int[])ExcludedManaTypes.Clone();
            if (AllowedGrowthTypes.Length != 0) copy.AllowedGrowthTypes = (int[])AllowedGrowthTypes.Clone();
            if (AllowedBuffTypes.Length != 0) copy.AllowedBuffTypes = (int[])AllowedBuffTypes.Clone();
            return copy;
        }

        internal bool Valid()
        {
            if (MainWeapons == null || MainWeapons.Length > MaxMainWeapons ||
                (HealthRecoveryMode != CompanionHealthRecoveryMode.Fast && HealthRecoveryMode != CompanionHealthRecoveryMode.Smart))
                return false;
            foreach (var weapon in MainWeapons)
                if (weapon.SlotHint < 0 || weapon.SlotHint >= MainInventorySlots || weapon.Type <= 0 || weapon.Prefix < 0 || weapon.Prefix > Byte.MaxValue)
                    return false;
            if (MainWeapons.Length == 2 && MainWeapons[0].SlotHint == MainWeapons[1].SlotHint) return false;
            return ValidTypes(ExcludedHealingTypes) && ValidTypes(ExcludedManaTypes) && ValidTypes(AllowedGrowthTypes) && ValidTypes(AllowedBuffTypes);
        }

        private static bool ValidTypes(int[] types)
        {
            if (types == null || types.Length > MaxSupplyTypes) return false;
            if (types.Length == 0) return true;
            var seen = new HashSet<int>();
            foreach (int type in types)
                if (type <= 0 || !seen.Add(type)) return false;
            return true;
        }
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
            if (behavior == null) throw new ArgumentNullException(nameof(behavior));
            if (!behavior.Valid())
                throw new ArgumentException("Invalid companion preferences.", nameof(behavior));
            Id = id; OwnerId = ownerId; Name = name; Revision = revision; DataEpoch = dataEpoch; PermanentlyDead = permanentlyDead;
            this.behavior = behavior.Clone();
            this.nativeData = (byte[])nativeData.Clone();
        }
        public Guid Id { get; }
        public Guid OwnerId { get; }
        public string Name { get; }
        public long Revision { get; }
        public long DataEpoch { get; }
        public bool PermanentlyDead { get; }
        public CompanionBehavior Behavior { get { return behavior.Clone(); } }
        // A displayed hotkey receipt rechecks just its setting. Keep the
        // immutable snapshot private without copying its item lists each frame.
        public int SettingValue(CompanionSetting setting) => behavior.SettingValue(setting);
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
