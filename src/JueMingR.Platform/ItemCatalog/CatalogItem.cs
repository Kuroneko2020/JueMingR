using System;

namespace JueMingR.Platform.ItemCatalog
{
    [Flags]
    public enum ItemCategory { All = 0, Weapon = 1, Tool = 2, Equipment = 4, Consumable = 8, Material = 16, Placeable = 32 }

    // Frozen display data only. A catalog type never grants ownership or an operation.
    public sealed class CatalogItem
    {
        public int Type { get; }
        public string Name { get; }
        public string InternalName { get; }
        public int Categories { get; }
        public int BaseValue { get; }
        public string Description { get; }
        public CatalogItem(int type, string name, string internalName, int categories)
            : this(type, name, internalName, categories, 0, "") { }
        public CatalogItem(int type, string name, string internalName, int categories, int baseValue, string description)
        {
            if (type <= 0) throw new ArgumentOutOfRangeException(nameof(type));
            Type = type; Name = name ?? ""; InternalName = internalName ?? ""; Categories = categories;
            BaseValue = baseValue; Description = description ?? "";
        }
    }
}
