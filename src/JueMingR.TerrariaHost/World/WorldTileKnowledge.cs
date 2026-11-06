using Terraria;

namespace JueMingR.TerrariaHost.World
{
    // Tile allocation is not network knowledge: vanilla backgrounds can make
    // default Tiles before section data arrives. These game-thread predicates
    // read the original ACK owner; they neither request nor allocate terrain.
    internal static class WorldTileKnowledge
    {
        internal static bool CellKnown(int x,int y)
        {return Main.tile!=null && x>=0 && y>=0 && x<Main.maxTilesX && y<Main.maxTilesY && (Main.netMode!=1 || Main.sectionManager!=null && Main.sectionManager.TileLoaded(x,y));}

        // Exclusive scan bounds, including vanilla's defined empty clipped
        // scans. A cached geometric proof needs section checks, not a second
        // walk over its cells; at most the intersected sections are visited.
        internal static bool RegionKnown(int left,int top,int right,int bottom)
        {
            if(Main.tile==null)return false;
            if(right<=left || bottom<=top)return true;
            if(left<0 || top<0 || right>Main.maxTilesX || bottom>Main.maxTilesY)return false;
            if(Main.netMode!=1)return true;
            var sections=Main.sectionManager;if(sections==null)return false;
            int lastX=Netplay.GetSectionX(right-1),lastY=Netplay.GetSectionY(bottom-1);
            for(int x=Netplay.GetSectionX(left);x<=lastX;x++)for(int y=Netplay.GetSectionY(top);y<=lastY;y++)if(!sections.SectionLoaded(x,y))return false;
            return true;
        }
    }

    // A bounded read lease belongs to this physical world/network source.
    // Session/target eligibility remains with its existing consumer. This is
    // a value identity, not a new revision owner or a serialized worker page.
    internal readonly struct WorldTileSource
    {
        private readonly object tiles,sections,connection,socket,world;
        private readonly int width,height,mode,worldId;
        private WorldTileSource(object tiles,object sections,object connection,object socket,object world,int width,int height,int mode,int worldId)
        {this.tiles=tiles;this.sections=sections;this.connection=connection;this.socket=socket;this.world=world;this.width=width;this.height=height;this.mode=mode;this.worldId=worldId;}
        internal static WorldTileSource Capture()
        {
            var client=Main.netMode==1?Netplay.Connection:null;
            return new WorldTileSource(Main.tile,Main.netMode==1?Main.sectionManager:null,client,client?.Socket,Main.ActiveWorldFileData,Main.maxTilesX,Main.maxTilesY,Main.netMode,Main.worldID);
        }
        internal bool Current
        {get{return tiles!=null && ReferenceEquals(tiles,Main.tile) && width==Main.maxTilesX && height==Main.maxTilesY && mode==Main.netMode && worldId==Main.worldID && ReferenceEquals(world,Main.ActiveWorldFileData) && (mode!=1 || ReferenceEquals(sections,Main.sectionManager) && ReferenceEquals(connection,Netplay.Connection) && ReferenceEquals(socket,Netplay.Connection?.Socket));}}
    }
}
