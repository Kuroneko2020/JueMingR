using System;
using System.Collections.Generic;
using JueMingR.Features.Fishing;

namespace JueMingR.Platform.Companions
{
    public enum CompanionInventoryArea { Inventory, Armor, Dye, Misc, MiscDye, PiggyBank, Safe, DefenderForge, VoidVault }
    public enum CompanionHealthRecoveryMode { Fast = 1, Smart = 2 }
    public enum CompanionSetting
    {
        Follow, Attack, Life, Mana, Buff, Growth, Respawn, Nurse, Furniture,
        FishingEquipment, FishingStorage, FishingCut, FishingTeleport, FishingLoadout, FishingMode,
        AutoTeleportEnabled, AutoTeleportBlockedEnabled, AutoTeleportBlockedSeconds,
        AutoTeleportDistanceEnabled, AutoTeleportDistanceTiles
    }

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
        public static FishingOptions DefaultFishing { get; } = new FishingOptions();

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
        public FishingOptions Fishing { get; set; } = DefaultFishing;
        public bool TeleportStopsFishing { get; set; } = true;
        public bool AutoTeleportEnabled { get; set; }
        public bool AutoTeleportBlockedEnabled { get; set; } = true;
        public int AutoTeleportBlockedSeconds { get; set; } = 5;
        public bool AutoTeleportDistanceEnabled { get; set; } = true;
        public int AutoTeleportDistanceTiles { get; set; } = 150;
        // If a multiplayer transfer lost its last receipt, preserve the raw
        // source bytes but record that these exact slots are not spendable.
        public ulong FishingDepositUnconfirmedSlots { get; set; }
        // Empty override sets preserve potion defaults. Empty permission sets grant nothing.
        public int[] ExcludedHealingTypes { get; set; } = Array.Empty<int>();
        public int[] ExcludedManaTypes { get; set; } = Array.Empty<int>();
        public int[] AllowedGrowthTypes { get; set; } = Array.Empty<int>();
        public int[] AllowedBuffTypes { get; set; } = Array.Empty<int>();

        public static bool IsFishingSetting(CompanionSetting setting)
        {
            switch (setting)
            {
                case CompanionSetting.FishingEquipment:
                case CompanionSetting.FishingStorage:
                case CompanionSetting.FishingCut:
                case CompanionSetting.FishingTeleport:
                case CompanionSetting.FishingLoadout:
                case CompanionSetting.FishingMode:
                    return true;
                default:
                    return false;
            }
        }

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
                case CompanionSetting.FishingEquipment: return Fishing.Equipment ? 1 : 0;
                case CompanionSetting.FishingStorage: return Fishing.StoreMode;
                case CompanionSetting.FishingCut: return Fishing.Cut ? 1 : 0;
                case CompanionSetting.FishingTeleport: return TeleportStopsFishing ? 1 : 0;
                case CompanionSetting.FishingLoadout: return Fishing.Loadout ? 1 : 0;
                // An activity exists only on a live actor for this world.
                case CompanionSetting.FishingMode: return 0;
                case CompanionSetting.AutoTeleportEnabled: return AutoTeleportEnabled ? 1 : 0;
                case CompanionSetting.AutoTeleportBlockedEnabled: return AutoTeleportBlockedEnabled ? 1 : 0;
                case CompanionSetting.AutoTeleportBlockedSeconds: return AutoTeleportBlockedSeconds;
                case CompanionSetting.AutoTeleportDistanceEnabled: return AutoTeleportDistanceEnabled ? 1 : 0;
                case CompanionSetting.AutoTeleportDistanceTiles: return AutoTeleportDistanceTiles;
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
            if (Fishing == null || Fishing.Auto || Fishing.AnglerContinuous ||
                FishingDepositUnconfirmedSlots >= (1UL << MainInventorySlots) ||
                AutoTeleportBlockedSeconds <= 0 || AutoTeleportDistanceTiles <= 0 ||
                MainWeapons == null || MainWeapons.Length > MaxMainWeapons ||
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
        public FishingOptions Fishing => behavior.Fishing;
        public ulong FishingDepositUnconfirmedSlots => behavior.FishingDepositUnconfirmedSlots;
        public byte[] NativeData { get { return (byte[])nativeData.Clone(); } }
        public CompanionProfile Revise(byte[] data, CompanionBehavior preferences = null, bool reset = false, bool? permanentlyDead = null,
            ulong resolvedStorageSlots = 0)
        {
            var next = preferences ?? behavior;
            // Keep the archive format. Routine captures retain the quarantine.
            // Only reset or the native storage attempt's exact resolved slots can clear it.
            ulong pending = reset ? 0 : (next.FishingDepositUnconfirmedSlots | FishingDepositUnconfirmedSlots) & ~resolvedStorageSlots;
            if (next.FishingDepositUnconfirmedSlots != pending)
            {
                next = next.Clone();
                next.FishingDepositUnconfirmedSlots = pending;
            }
            return new CompanionProfile(Id, OwnerId, Name, checked(Revision + 1), reset ? checked(DataEpoch + 1) : DataEpoch, next, data, reset ? false : permanentlyDead ?? PermanentlyDead);
        }
    }

    public sealed class CompanionOwnerArchive
    {
        public CompanionOwnerArchive(Guid ownerId, IReadOnlyList<CompanionProfile> profiles, string error = null,
            IReadOnlyList<CompanionDeletion> deletions = null)
        { OwnerId = ownerId; Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles)); Error = error; Deletions = deletions ?? Array.Empty<CompanionDeletion>(); }
        public Guid OwnerId { get; }
        public IReadOnlyList<CompanionProfile> Profiles { get; }
        public string Error { get; }
        public IReadOnlyList<CompanionDeletion> Deletions { get; }
    }

    public sealed class CompanionDeletion
    {
        public CompanionDeletion(Guid ownerId, Guid profileId, string name, bool filesRemoved = false, string error = null, bool finishing = false)
        { OwnerId = ownerId; ProfileId = profileId; Name = name; FilesRemoved = filesRemoved; Error = error; Finishing = finishing; }
        public Guid OwnerId { get; }
        public Guid ProfileId { get; }
        public string Name { get; }
        public bool FilesRemoved { get; }
        public bool Finishing { get; }
        public string Error { get; }
    }

    // Called only by the single document worker. The host captures native bytes
    // on the game thread; infrastructure never receives a live Player or Item.
    public interface ICompanionArchive : IDisposable
    {
        CompanionOwnerArchive LoadOwner(string characterKey);
        void LinkOwner(string characterKey, Guid ownerId);
        void Save(CompanionProfile profile);
        void DeleteFiles(CompanionDeletion deletion, long maximumRevision);
        void FinishDeletion(CompanionDeletion deletion);
    }
}
