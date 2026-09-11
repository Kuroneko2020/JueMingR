namespace JueMingR.TerrariaHost.Hotkeys
{
    internal static class HotkeyActionIds
    {
        internal const string Biome = "biome-display.toggle";
        internal const string EnemyLabels = "entity-labels.enemy.toggle", CritterLabels = "entity-labels.critter.toggle", NpcLabels = "entity-labels.npc.toggle";
        internal static readonly string[] EntityLabels = { EnemyLabels, CritterLabels, NpcLabels };
        internal static readonly string[] Items = { "items.auto-stack.toggle", "items.auto-sell.toggle", "items.auto-discard.toggle" };
    }
}
