using System;

namespace JueMingR.Platform.Biomes
{
    [Flags]
    public enum BiomeFlags : uint
    {
        None = 0,
        Desert = 1u << 0,
        UndergroundDesert = 1u << 1,
        Snow = 1u << 2,
        Jungle = 1u << 3,
        Dungeon = 1u << 4,
        Beach = 1u << 5,
        Corrupt = 1u << 6,
        Crimson = 1u << 7,
        Hallow = 1u << 8,
        Holy = 1u << 9,
        Glowshroom = 1u << 10,
        Meteor = 1u << 11,
        Granite = 1u << 12,
        Marble = 1u << 13,
        Hive = 1u << 14,
        LihzhardTemple = 1u << 15,
        Graveyard = 1u << 16,
        Sky = 1u << 17,
        Underworld = 1u << 18,
        RockLayer = 1u << 19,
        DirtLayer = 1u << 20,
        BelowSurface = 1u << 21,
        Overworld = 1u << 22
    }
}
