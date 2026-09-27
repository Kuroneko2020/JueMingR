using System;
using System.Reflection;
using JueMingR.Features.Tools;
using JueMingR.TerrariaHost.World;
using Terraria;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.Tools
{
    internal static class MiningEligibility
    {
        private static readonly Func<Player,int,int,int,int,Tile,int> damage=(Func<Player,int,int,int,int,Tile,int>)Delegate.CreateDelegate(
            typeof(Func<Player,int,int,int,int,Tile,int>),typeof(Player).GetMethod("GetPickaxeDamage",BindingFlags.Instance|BindingFlags.NonPublic));
        internal static bool CanProgress(Player player,Item tool,int x,int y,int type)
        {
            if(player==null || tool==null || tool.pick<=0 || player.noBuilding || !MiningRegion.Supported(type))return false;
            var current=WorldTileObservation.ReadCurrent(x,y);
            if(!current.Readable || !current.Active || current.Inactive || current.Type!=type || !player.IsInTileInteractionRange(x,y,TileReachCheckSettings.Simple,tool.tileBoost))return false;
            // Strict support filter excludes GetPickaxeDamage's 128/269/334
            // mutation branches. Never replace this with DetermineDamage or
            // HasEnoughPickPower: both allocate native hit-buffer entries.
            for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)if(!WorldTileObservation.ReadCurrent(x+dx,y+dy).Readable)return false;
            // CheckTileBreakability also protects furniture supports that
            // CanKillTile and positive pick damage alone do not reject. The
            // readable neighbourhood above keeps this query allocation-free.
            // Clinger/Man Eater AI also protects its actual anchor, including
            // supported demonite. Read that native set; never test by digging.
            return WorldGen.CanKillTile(x,y) && WorldGen.CheckTileBreakability(x,y)==0 &&
                !Terraria.GameContent.FixExploitManEaters.SpotProtected(x,y) && damage(player,x,y,tool.pick,0,Main.tile[x,y])>0;
        }
    }
}
