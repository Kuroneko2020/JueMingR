namespace JueMingR.TerrariaHost.Hotkeys
{
    internal static class HotkeyActionIds
    {
        internal const string Biome = "biome-display.toggle";
        internal const string EnemyLabels = "entity-labels.enemy.toggle", CritterLabels = "entity-labels.critter.toggle", NpcLabels = "entity-labels.npc.toggle";
        internal static readonly string[] EntityLabels = { EnemyLabels, CritterLabels, NpcLabels };
        internal static readonly string[] Items = { "items.auto-stack.toggle", "items.auto-sell.toggle", "items.auto-discard.toggle" };
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
