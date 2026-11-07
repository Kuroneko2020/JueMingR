using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Terraria
{
    // Independent .8 field/method ABI. No native world generation or networking.
    public class Tile
    {
        public ushort type;
        public short frameX, frameY;
        public bool Active, Inactive;
        public bool active() { return Active; }
        public bool inActive() { return Inactive; }
    }
    public class WorldSections
    {
        public readonly HashSet<long> Unknown = new HashSet<long>();
        public readonly HashSet<long> UnknownSections = new HashSet<long>();
        public int Reads;
        public bool TileLoaded(int x, int y)
        { Reads++; return !Unknown.Contains(((long)x << 32) | (uint)y) && !UnknownSections.Contains(((long)Netplay.GetSectionX(x) << 32) | (uint)Netplay.GetSectionY(y)); }
        public bool SectionLoaded(int x, int y)
        {
            Reads++;
            if(UnknownSections.Contains(((long)x << 32) | (uint)y))return false;
            // Existing fixtures mark individual cells unknown. A section
            // proof must not upgrade any such cell into synchronized terrain.
            foreach(long cell in Unknown)
                if(Netplay.GetSectionX((int)(cell>>32))==x && Netplay.GetSectionY((int)cell)==y)return false;
            return true;
        }
    }
    public sealed partial class Player { public bool accOreFinder; }
    public partial class Main
    {
        public static Tile[,] tile;
        public static WorldSections sectionManager;
        public static int maxTilesX, maxTilesY;
        public static GameTime gameTimeCache = new GameTime();
    }
}
