using System;
using JueMingR.Platform.Guidance;
using JueMingR.TerrariaHost.Npcs;
using Terraria;
using Terraria.GameContent.Events;

namespace JueMingR.TerrariaHost.Guidance
{
    internal sealed class GuidanceObservationReader : IGuidanceLocationSource
    {
        private readonly NativeNpcObservation npcs;
        internal readonly int[] Bosses = new int[Main.maxNPCs];
        internal readonly EffectiveEquipmentReader Equipment = new EffectiveEquipmentReader();
        internal int BossCount { get; private set; }
        internal GuidanceObservationReader(NativeNpcObservation npcs) { this.npcs = npcs; }
        internal static bool ValidPlayer
        { get { return !Main.gameMenu && !Main.dedServ && (Main.netMode == 0 || Main.netMode == 1) && Main.myPlayer >= 0 && Main.player != null && Main.myPlayer < Main.player.Length && Main.LocalPlayer != null && Main.LocalPlayer.active && !Main.LocalPlayer.dead; } }
        internal static bool RareQualified
        { get { var p = Main.LocalPlayer; return p.accCritterGuide && p.hideInfo != null && p.hideInfo.Length > 11 && !p.hideInfo[11]; } }
        internal bool ReadDanger(out int events)
        {
            BossCount = 0;
            // Blood moon alone is absent, not a veto of concurrent dangers.
            events = (Main.invasionType > 0 ? 1 | Main.invasionType << 16 : 0) | (Main.pumpkinMoon ? 2 : 0) | (Main.snowMoon ? 4 : 0) |
                (Main.eclipse ? 8 : 0) | (Main.slimeRain ? 16 : 0) | (DD2Event.Ongoing ? 32 : 0) |
                (NPC.LunarApocalypseIsUp ? 64 : 0) | (NPC.TowerActiveSolar ? 128 : 0) | (NPC.TowerActiveVortex ? 256 : 0) |
                (NPC.TowerActiveNebula ? 512 : 0) | (NPC.TowerActiveStardust ? 1024 : 0);
            if (!npcs.Readable) return false;
            for (int i = 0; i < npcs.Count; i++)
            { GuidanceNpc n; if (npcs.TryRead(i, NpcDemand.Danger, out n) && n.Active && n.Boss && n.Life > 0) Bosses[BossCount++] = n.Type; }
            return true;
        }
        public int PylonCount { get { return Main.PylonSystem == null || Main.PylonSystem.Pylons == null ? 0 : Main.PylonSystem.Pylons.Count; } }
        public bool TryPylon(int index, out GuidancePylon pylon)
        {
            pylon = default(GuidancePylon); if (index < 0 || index >= PylonCount) return false;
            var p = Main.PylonSystem.Pylons[index]; pylon = new GuidancePylon { Type = (int)p.TypeOfPylon, X = p.PositionInTiles.X, Y = p.PositionInTiles.Y }; return true;
        }
        public bool TryWorld(out int width, out int height, out double surface)
        { width = Main.maxTilesX; height = Main.maxTilesY; surface = Main.worldSurface; return width > 0 && height > 0 && surface > 0; }
    }
}
