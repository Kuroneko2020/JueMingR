using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.WorldBuilding;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal sealed class ActorEnvironment
        {
            internal readonly SceneMetrics Scene = new SceneMetrics();
            internal Tile[,] Tiles;
            internal Point Center, CheckedCenter;
            internal ushort Wall;
            internal int HeldCandle;
            internal bool Known, HasProjection, Rain, Sandstorm;
            internal uint CheckedAt = UInt32.MaxValue, RetryAt, ScanAt;
            internal CompanionConnection PublishedConnection;
            internal long PublishedGeneration;
            internal int PublishedSlot;
            internal ulong PublishedZones;
        }

        internal bool RefreshActorEnvironment(Actor actor)
        {
            var body = actor.Body;
            if (actor.Closing || body.dead || body.ghost) return false;
            var environment = actor.Environment;
            var tiles = Main.tile;
            Point center = body.Center.ToTileCoordinates();
            uint now = Main.GameUpdateCount;
            bool sameWorld = ReferenceEquals(tiles, environment.Tiles);
            if (sameWorld && environment.CheckedAt == now && environment.CheckedCenter == center) return environment.Known;
            if (!environment.Known && sameWorld && environment.Center == center && now < environment.RetryAt) return false;
            environment.CheckedAt = now;
            environment.CheckedCenter = center;

            // The native scan treats null tiles as empty. A multiplayer scene
            // needs the entire native footprint, not just the body tile.
            bool valid = tiles != null && WorldGen.InWorld(center.X, center.Y, 1) && tiles[center.X, center.Y] != null;
            if (!valid || Main.netMode == 1 && (Main.sectionManager == null || MissingEnvironmentSection(center, false, body.team)))
            {
                if (environment.HasProjection && (!sameWorld || now - environment.ScanAt >= 120))
                {
                    // Vanilla Zone bits have no unknown representation. An
                    // old projection can bridge delivery briefly but cannot
                    // assert Crimson etc. forever at an unloaded destination.
                    // Unknown still blocks fishing, quotes and publication.
                    body.zone1 = body.zone2 = body.zone3 = body.zone4 = body.zone5 = 0;
                    body.townNPCs = 0;
                    environment.HasProjection = false;
                    environment.Scene.Reset();
                }
                environment.Tiles = tiles;
                environment.Center = center;
                environment.Known = false;
                environment.RetryAt = now + 12;
                return false;
            }

            ushort wall = tiles[center.X, center.Y].wall;
            int held = body.inventory[body.selectedItem].type;
            int candle = held == ItemID.WaterCandle || held == ItemID.PeaceCandle || held == ItemID.ShadowCandle ? held : 0;
            bool sandstorm = Sandstorm.Happening;
            bool moved = Math.Abs(center.X - environment.Center.X) >= 8 || Math.Abs(center.Y - environment.Center.Y) >= 8;
            bool sectionChanged = Netplay.GetSectionX(center.X) != Netplay.GetSectionX(environment.Center.X) ||
                Netplay.GetSectionY(center.Y) != Netplay.GetSectionY(environment.Center.Y);
            if (environment.Known && sameWorld && !moved && !sectionChanged && wall == environment.Wall &&
                candle == environment.HeldCandle && Main.raining == environment.Rain && sandstorm == environment.Sandstorm &&
                now - environment.ScanAt < 60 && SameEnvironmentDepth(environment.Scene, center.Y)) return true;

            // A body owns one scan independently of its activity. The resting
            // scan also catches in-place tile conversion and town-NPC changes.
            environment.Scene.Scan(new SceneMetricsScanSettings
            {
                BiomeScanCenterPositionInWorld = body.Center,
                PerspectivePlayer = body,
                ScanNPCPositions = true
            });
            environment.Tiles = tiles;
            environment.Center = center;
            environment.ScanAt = now;
            environment.Wall = wall;
            environment.HeldCandle = candle;
            environment.Rain = Main.raining;
            environment.Sandstorm = sandstorm;
            environment.Known = environment.HasProjection = true;
            CompanionHooks.ApplyActorEnvironment(actor);
            return true;
        }

        private static bool SameEnvironmentDepth(SceneMetrics scene, int y) =>
            scene.ZoneSkyHeight == (y <= Main.worldSurface * 0.35f) &&
            scene.BelowSurface == (y > Main.worldSurface) &&
            scene.ZoneDirtLayerHeight == (y <= Main.rockLayer && y > Main.worldSurface) &&
            scene.ZoneRockLayerHeight == (y <= Main.UnderworldLayer && y > Main.rockLayer) &&
            scene.ZoneUnderworldHeight == (y > Main.UnderworldLayer);

        private bool MissingEnvironmentSection(Point center, bool request, int team)
        {
            var scan = WorldUtils.ClampToWorld(Utils.CenteredRectangle(center, SceneMetrics.ZoneScanSize));
            int bottom = scan.Bottom - 1;
            if (Main.dualDungeonsSeed && center.Y > Main.worldSurface && center.Y <= Main.UnderworldLayer)
                bottom = Math.Max(bottom, Math.Min(center.Y + 299, Main.maxTilesY - 1));
            for (int x = Netplay.GetSectionX(scan.Left); x <= Netplay.GetSectionX(scan.Right - 1); x++)
                for (int y = Netplay.GetSectionY(scan.Top); y <= Netplay.GetSectionY(bottom); y++)
                {
                    if (Main.sectionManager.SectionLoaded(x, y)) continue;
                    if (!request || TryRequestTerrain(Math.Min(x * Main.sectionWidth + Main.sectionWidth / 2, Main.maxTilesX - 11),
                        Math.Min(y * Main.sectionHeight + Main.sectionHeight / 2, Main.maxTilesY - 11), team)) return true;
                }
            return false;
        }

        private bool RequestEnvironmentTerrain()
        {
            foreach (var actor in actors.Values)
            {
                if (actor.Closing || actor.Connection == null || !actor.Ready && !actor.ReservedBodySpawned || actor.Body.dead) continue;
                Point center = actor.Body.Center.ToTileCoordinates();
                if (WorldGen.InWorld(center.X, center.Y, 1) && MissingEnvironmentSection(center, true, actor.Body.team)) return true;
            }
            return false;
        }

        internal static void InvalidateActorEnvironment(Actor actor)
        {
            var environment = actor.Environment;
            environment.Known = false;
            environment.CheckedAt = UInt32.MaxValue;
            environment.RetryAt = 0;
            environment.PublishedConnection = null;
            environment.Scene.Reset();
        }

        internal void RefreshTeleportedEnvironment(Actor actor)
        {
            // A very long native teleport may already have refreshed at its
            // destination inside UpdateBiomesIfMovedEnoughForBlackFade.
            var state = actor.Environment;
            if (!state.Known || state.ScanAt != Main.GameUpdateCount || state.Center != actor.Body.Center.ToTileCoordinates())
            {
                InvalidateActorEnvironment(actor);
                RefreshActorEnvironment(actor);
            }
            // Even an unchanged set of flags is a new position baseline.
            state.PublishedConnection = null;
        }

        private static void PublishActorEnvironment(Actor actor)
        {
            var environment = actor.Environment;
            var body = actor.Body;
            var connection = actor.Connection;
            if (connection == null || !environment.Known || body.dead || actor.Closing) return;
            // The comparison uses the vanilla payload's byte values. Leaving
            // the writer to NetMessage keeps the real protocol and FIFO order.
            ulong zones = (byte)body.zone1 | (ulong)(byte)body.zone2 << 8 | (ulong)(byte)body.zone3 << 16 |
                (ulong)(byte)body.zone4 << 24 | (ulong)(byte)body.zone5 << 32 | (ulong)(byte)body.townNPCs << 40;
            if (ReferenceEquals(environment.PublishedConnection, connection) && environment.PublishedGeneration == actor.Generation &&
                environment.PublishedSlot == body.whoAmI && environment.PublishedZones == zones) return;
            using (var scope = CompanionHooks.Enter(actor, actor.Aim))
            {
                try { NetMessage.SendData(MessageID.SyncPlayerZone, number: body.whoAmI); }
                catch (CompanionConnection.SendQueueFullException) when (!connection.IsRelay) { return; }
                if (!scope.PacketAccepted) return;
            }
            environment.PublishedConnection = connection;
            environment.PublishedGeneration = actor.Generation;
            environment.PublishedSlot = body.whoAmI;
            environment.PublishedZones = zones;
        }
    }

    internal static partial class CompanionHooks
    {
        private static readonly AccessTools.FieldRef<SceneMetrics> PlayerEnvironmentScene =
            AccessTools.StaticFieldRefAccess<SceneMetrics>(AccessTools.Field(typeof(Main), "_playerSceneMetrics"));

        private static bool ActorSceneMetricsBefore(Player __instance)
        {
            var actor = host?.Owned(__instance);
            if (actor == null) return true;
            host.RefreshActorEnvironment(actor);
            return false;
        }

        private static bool ActorBiomesBefore(Player __instance, out BiomeScope __state)
        {
            __state = default;
            var actor = host?.Owned(__instance);
            if (actor == null) return true;
            if (!actor.Environment.Known) return false;
            __state = new BiomeScope(actor.Environment.Scene);
            return true;
        }

        private static Exception ActorBiomesAfter(BiomeScope __state, Exception __exception)
        {
            __state.Dispose();
            return __exception;
        }

        private readonly struct BiomeScope : IDisposable
        {
            private readonly SceneMetrics previous;
            private readonly bool entered;

            internal BiomeScope(SceneMetrics scene)
            {
                previous = PlayerEnvironmentScene();
                entered = true;
                PlayerEnvironmentScene() = scene;
            }

            public void Dispose()
            {
                if (entered) PlayerEnvironmentScene() = previous;
            }
        }

        internal static void ApplyActorEnvironment(HostCompanions.Actor actor)
        {
            // A reserved multiplayer body has no Owned lookup until the
            // server confirms its join. Native projection still uses its scan.
            using (new BiomeScope(actor.Environment.Scene)) actor.Body.UpdateBiomes();
        }
    }
}
