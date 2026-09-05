using JueMingR.Features.Biomes;
using JueMingR.Platform.Biomes;
using JueMingR.Platform.Runtime;
using Terraria;

namespace JueMingR.TerrariaHost
{
    internal sealed class Phase0TBiomeRuntime
    {
        private readonly BiomeDisplayFeature feature;
        private readonly SingleFeatureRuntime runtime;

        private Phase0TBiomeRuntime(bool enabled)
        {
            var worldReader = new TerrariaBiomeWorldReader();
            feature = new BiomeDisplayFeature(worldReader, enabled);
            runtime = new SingleFeatureRuntime(worldReader, feature);
        }

        internal BiomeDisplayViewModel CurrentViewModel
        {
            get { return feature.CurrentViewModel; }
        }

        internal bool FeatureEnabled
        {
            get { return feature.Enabled; }
        }

        internal static Phase0TBiomeRuntime Create(bool enabled)
        {
            return new Phase0TBiomeRuntime(enabled);
        }

        internal void Update(ulong updateTick)
        {
            runtime.Update(updateTick);
        }

        internal void FailClosed()
        {
            feature.FailClosed();
        }

        internal void SetFeatureEnabled(bool enabled) { feature.SetEnabled(enabled); }

        private sealed class TerrariaBiomeWorldReader : IGameSessionProbe, IBiomeObservationSource
        {
            public bool IsSessionActive
            {
                get
                {
                    Player player;
                    return TryGetActivePlayer(out player);
                }
            }

            public bool TryObserve(out BiomeObservation observation)
            {
                Player player;
                if (!TryGetActivePlayer(out player))
                {
                    observation = default(BiomeObservation);
                    return false;
                }

                BiomeFlags flags = BiomeFlags.None;
                Add(ref flags, player.ZoneDesert, BiomeFlags.Desert);
                Add(ref flags, player.ZoneUndergroundDesert, BiomeFlags.UndergroundDesert);
                Add(ref flags, player.ZoneSnow, BiomeFlags.Snow);
                Add(ref flags, player.ZoneJungle, BiomeFlags.Jungle);
                Add(ref flags, player.ZoneDungeon, BiomeFlags.Dungeon);
                Add(ref flags, player.ZoneBeach, BiomeFlags.Beach);
                Add(ref flags, player.ZoneCorrupt, BiomeFlags.Corrupt);
                Add(ref flags, player.ZoneCrimson, BiomeFlags.Crimson);
                Add(ref flags, player.ZoneHallow, BiomeFlags.Hallow);
                // Terraria 1.4.5.8 has no Player.ZoneHoly member. The pure
                // observation contract retains that compatibility bit, while
                // this fixed typed host deliberately leaves it false.
                Add(ref flags, player.ZoneGlowshroom, BiomeFlags.Glowshroom);
                Add(ref flags, player.ZoneMeteor, BiomeFlags.Meteor);
                Add(ref flags, player.ZoneGranite, BiomeFlags.Granite);
                Add(ref flags, player.ZoneMarble, BiomeFlags.Marble);
                Add(ref flags, player.ZoneHive, BiomeFlags.Hive);
                Add(ref flags, player.ZoneLihzhardTemple, BiomeFlags.LihzhardTemple);
                Add(ref flags, player.ZoneGraveyard, BiomeFlags.Graveyard);
                Add(ref flags, player.ZoneSkyHeight, BiomeFlags.Sky);
                Add(ref flags, player.ZoneUnderworldHeight, BiomeFlags.Underworld);
                Add(ref flags, player.ZoneRockLayerHeight, BiomeFlags.RockLayer);
                Add(ref flags, player.ZoneDirtLayerHeight, BiomeFlags.DirtLayer);
                Add(ref flags, player.ShoppingZone_BelowSurface, BiomeFlags.BelowSurface);
                Add(ref flags, player.ZoneOverworldHeight, BiomeFlags.Overworld);

                observation = new BiomeObservation(flags);
                return true;
            }

            private static bool TryGetActivePlayer(out Player player)
            {
                player = null;
                if (Main.gameMenu)
                {
                    return false;
                }

                player = Main.LocalPlayer;
                return player != null && player.active;
            }

            private static void Add(ref BiomeFlags flags, bool active, BiomeFlags value)
            {
                if (active)
                {
                    flags |= value;
                }
            }
        }
    }
}
