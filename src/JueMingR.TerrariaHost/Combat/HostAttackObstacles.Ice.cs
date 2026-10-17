using System.Collections.Generic;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        // Collision.EmptyTile includes friendly and invulnerable actors. This
        // placement-only census must not reuse the filtered damage receivers.
        // Capture it only at an actual placement query, once across candidates
        // and family forks. Other actors remain explicitly conditional inputs.
        private sealed class IcePlacementLease
        {
            private sealed class NpcRow {internal NPC Npc;internal NpcIdentity Identity;internal Rectangle Box;}
            private sealed class PlayerRow {internal Player Player;internal int Slot;internal Rectangle Box;}
            private readonly List<NpcRow> npcs=new List<NpcRow>();
            private readonly List<PlayerRow> players=new List<PlayerRow>();
            private readonly long session;
            private static bool Qualifies(Player p){return p!=null && p.active && !p.dead && !p.ghost;}
            private static Rectangle RawBox(Entity body){return new Rectangle((int)body.position.X,(int)body.position.Y,body.width,body.height);}
            private static Rectangle RawBox(MotionRect body){return new Rectangle((int)body.X,(int)body.Y,(int)body.Width,(int)body.Height);}
            internal IcePlacementLease(long session)
            {
                this.session=session;
                for(int slot=0;slot<255;slot++)
                {var p=Main.player[slot];if(Qualifies(p))players.Add(new PlayerRow{Player=p,Slot=slot,Box=RawBox(p)});}
                for(int slot=0;slot<Main.maxNPCs;slot++)
                {var n=Main.npc[slot];if(n!=null && n.active)npcs.Add(new NpcRow{Npc=n,Identity=CombatSelection.Identity(n,session),Box=RawBox(n)});}
            }
            internal bool Empty(int x,int y,NpcTrajectory future,int tick,int ownerSlot,MotionRect ownerBounds)
            {
                var tile=new Rectangle(x*16,y*16,16,16);
                foreach(var row in players)if(tile.Intersects(row.Slot==ownerSlot?RawBox(ownerBounds):row.Box))return false;
                foreach(var row in npcs)if(tile.Intersects(row.Identity.Equals(future.Identity)?RawBox(future[tick].Bounds):row.Box))return false;
                return true;
            }
            // A new occupant matters just as much as a changed old one. Full
            // membership checks belong to preparation/consumption, never Draw.
            internal bool Current
            {
                get
                {
                    int at=0;
                    for(int slot=0;slot<255;slot++)
                    {
                        var p=Main.player[slot];if(!Qualifies(p))continue;
                        if(at>=players.Count)return false;var row=players[at++];
                        if(row.Slot!=slot || !ReferenceEquals(row.Player,p) || row.Box!=RawBox(p))return false;
                    }
                    if(at!=players.Count)return false;at=0;
                    for(int slot=0;slot<Main.maxNPCs;slot++)
                    {
                        var n=Main.npc[slot];if(n==null || !n.active)continue;
                        if(at>=npcs.Count)return false;var row=npcs[at++];
                        if(row.Identity.Slot!=slot || !CombatSelection.Identity(n,session).Equals(row.Identity) || row.Box!=RawBox(n))return false;
                    }
                    return at==npcs.Count;
                }
            }
        }
        internal bool TryIcePlacement(PredictionTerrain terrain,int x,int y,int tick,int outer,MotionRect ownerRawBox,out bool placed)
        {
            placed=false;
            // PlaceTile returns a known failure outside the world or on a raw
            // active cell, including actuated tiles. No entity census is then
            // necessary. A missing in-world cell remains unknown to prediction.
            if(x<0 || y<0 || x>=Main.maxTilesX || y>=Main.maxTilesY)return true;
            PredictionTile tile;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;if(tile.RawActive)return true;
            if(tick<0 || tick>=timeline.Count || outer<0)return false;
            if(history.IcePlacement==null)history.IcePlacement=new IcePlacementLease(timeline.Identity.Session);
            placed=history.IcePlacement.Empty(x,y,timeline,tick,player.whoAmI,ownerRawBox);return true;
        }
    }
}
