using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using JueMingR.TerrariaHost.Npcs;

namespace JueMingR.TerrariaHost.Recovery
{
    // One bounded target per service. Cached discovery is never a write permit.
    internal sealed class NearbyServiceTarget
    {
        private readonly NativeNpcObservation npcs;
        private readonly int type;
        private NPC npc;
        private byte generation;
        private int slot=-1;
        private ulong next;
        private Rectangle region;
        internal NearbyServiceTarget(NativeNpcObservation npcs,int type){this.npcs=npcs;this.type=type;}
#if DEBUG
        internal long Reads;
#endif
        internal void Reset(){npc=null;slot=-1;next=0;}
        internal NPC Find(Player p,ulong tick)
        {
            Rectangle current=TileReachCheckSettings.Simple.GetWorldRegion(p);
            if(Valid(p) && tick<next)return npc;
            if(current==region && tick<next)return null;
            region=current;next=tick+60;npc=null;slot=-1;float distance=Single.MaxValue;
            if(!npcs.Readable)return null;
            for(int i=0;i<npcs.Count;i++)
            {
#if DEBUG
                Reads++;
#endif
                var candidate=npcs.Active(i);if(!Eligible(p,candidate,type))continue;
                float d=Vector2.DistanceSquared(p.Center,candidate.Center+candidate.netOffset);if(d>=distance)continue;
                distance=d;npc=candidate;slot=i;generation=npc.generation;
            }
            return npc;
        }
        internal int Slot {get{return slot;}}
        internal byte Generation {get{return generation;}}
        internal bool Valid(Player p){return npc!=null && slot>=0 && slot<Main.maxNPCs && ReferenceEquals(Main.npc[slot],npc) && npc.generation==generation && Eligible(p,npc,type);}
        internal static bool Eligible(Player p,NPC npc,int type)
        {
            if(p==null || npc==null || !npc.active || npc.type!=type || !npc.townNPC || npc.CurrentlyShimmerTransparent() || p.stinky || p.mouseInterface || p.dead || p.ownedProjectileCounts[651]>0 || p.tileInteractionHappened)return false;
            return TileReachCheckSettings.Simple.GetWorldRegion(p).Intersects(new Rectangle((int)(npc.position.X+npc.netOffset.X),(int)(npc.position.Y+npc.netOffset.Y),npc.width,npc.height));
        }
    }

    internal sealed class ServiceDialog
    {
        private readonly HostRecovery host;
        private long changes,ownedChange,session;
        private Player player;
        private NPC npc;
        private int slot;
        private byte generation;
        internal ServiceDialog(HostRecovery host){this.host=host;}
        internal long Changes {get{return changes;}}
        internal void Changed(){changes++;}
        internal bool Open(Player p,NearbyServiceTarget target)
        {
            if(!host.Admit(p) || !target.Valid(p))return false;
            player=p;slot=target.Slot;npc=Main.npc[slot];generation=npc.generation;session=host.Runtime.Generation;
            ownedChange=changes+1;p.SetTalkNPC(slot);
            if(!Owns)return false;
            string text=npc.GetChat();if(!Owns)return false;Main.npcChatText=text;return true;
        }
        internal bool Owns {get{return ReferenceEquals(player,host.Player) && session==host.Runtime.Generation && changes==ownedChange && player.talkNPC==slot && slot>=0 && ReferenceEquals(Main.npc[slot],npc) && npc.generation==generation && npc.active && player.sign<0 && Main.npcShop==0;}}
        internal void Success()
        {
            if(!Owns)return;
            // Native closure is safe only for this still-owned context. Failed
            // calls deliberately leave the original dialog/text to the player.
            Main.CloseNPCChatOrSign(true);
        }
    }
}
