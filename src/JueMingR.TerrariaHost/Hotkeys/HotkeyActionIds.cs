namespace JueMingR.TerrariaHost.Hotkeys
{
    internal static class HotkeyActionIds
    {
        internal const string RareDirection = "rare-direction.toggle", MerchantDirection = "merchant-direction.toggle", EquipmentWarning = "equipment-warning.toggle";
        internal static readonly string[] Guidance = { RareDirection, MerchantDirection, EquipmentWarning };
        internal const string Biome = "biome-display.toggle";
        internal const string AdjustInformation = "information-window.adjust";
        internal static string Information(Platform.Information.InformationKind kind)
        {
            switch (kind)
            {
                case Platform.Information.InformationKind.Biome: return Biome;
                case Platform.Information.InformationKind.Infection: return "information.infection.toggle";
                case Platform.Information.InformationKind.Luck: return "information.luck.toggle";
                case Platform.Information.InformationKind.Angler: return "information.angler.toggle";
                default: throw new System.ArgumentOutOfRangeException(nameof(kind));
            }
        }
        internal const string EnemyLabels = "entity-labels.enemy.toggle", CritterLabels = "entity-labels.critter.toggle", NpcLabels = "entity-labels.npc.toggle";
        internal static readonly string[] EntityLabels = { EnemyLabels, CritterLabels, NpcLabels };
        internal static readonly string[] Items = { "items.auto-stack.toggle", "items.auto-sell.toggle", "items.auto-discard.toggle" };
        internal static string WorldObject(Platform.WorldObjectText.WorldObjectKind kind)
        {
            switch (kind)
            {
                case Platform.WorldObjectText.WorldObjectKind.Chest: return "world-object-text.chest.toggle";
                case Platform.WorldObjectText.WorldObjectKind.Sign: return "world-object-text.sign.toggle";
                case Platform.WorldObjectText.WorldObjectKind.Tombstone: return "world-object-text.tombstone.toggle";
                default: throw new System.ArgumentOutOfRangeException(nameof(kind));
            }
        }
        internal static string WorldTarget(Platform.WorldTargets.WorldTargetKind kind)
        {
            switch (kind)
            {
                case Platform.WorldTargets.WorldTargetKind.LifeCrystal: return "world-targets.life-crystal.toggle";
                case Platform.WorldTargets.WorldTargetKind.LifeFruit: return "world-targets.life-fruit.toggle";
                case Platform.WorldTargets.WorldTargetKind.ManaCrystal: return "world-targets.mana-crystal.toggle";
                case Platform.WorldTargets.WorldTargetKind.SleepingDigtoise: return "world-targets.sleeping-digtoise.toggle";
                case Platform.WorldTargets.WorldTargetKind.ChilletEgg: return "world-targets.chillet-egg.toggle";
                default: throw new System.ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
