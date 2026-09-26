using System;
using JueMingR.Platform.Guidance;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Tools
{
    internal sealed class AutoCapture
    {
        private readonly HostTools host;
        private readonly NetGeometry[] geometry={new NetGeometry(),new NetGeometry(),new NetGeometry()};
        private readonly int[] slots=new int[3];
        private readonly NPC[] unknownTargets=new NPC[Main.maxNPCs];
        private readonly int[] unknownGenerations=new int[Main.maxNPCs];
        internal void ClearUnknown(){Array.Clear(unknownTargets,0,unknownTargets.Length);}
        private long nextProbe;
#if DEBUG
        internal long Probes {get;private set;}
#endif
        internal AutoCapture(HostTools host){this.host=host;}
        internal static int Category(NPC n)
        {
            int item=n.catchItem;
            if(n.type==374 || n.type==375 || item==2673)return 5;
            if(n.type==661 || item==4961)return 6;
            if(n.type>=583 && n.type<=585 || item>=4068 && item<=4070)return 1;
            if(n.type>=0 && n.type<NPCID.Sets.IsGoldCritter.Length && NPCID.Sets.IsGoldCritter[n.type])return 2;
            if(n.type>=639 && n.type<=652 || item>=4831 && item<=4844)return 3;
            Item sample;if(ContentSamples.ItemsByType.TryGetValue(item,out sample) && sample.bait>0)return 0;
            return 4; // Legacy catchItem>0 observations all classify as critters.
        }
        internal static bool Catchable(NPC n,Item net)
        {
            if(n==null || !n.active || n.life<=0 || n.catchItem<=0 || n.SpawnedFromStatue || n.type==687 || !NetGeometry.IsNet(net))return false;
            if(n.type>=583 && n.type<=585 && n.ai[2]>1)return false;
            return net.type!=1991 || n.catchItem>=ItemID.Sets.IsLavaBait.Length || !ItemID.Sets.IsLavaBait[n.catchItem];
        }
        internal bool Boss()
        {
            for(int i=0;i<host.Npcs.Count;i++)
            {
                GuidanceNpc n;if(!host.Npcs.TryRead(i,NpcDemand.Danger,out n) || !n.Active || n.Life<=0)continue;
                if(n.Boss)return true;
                switch(n.Type)
                {
                    case 13:case 14:case 15:case 35:case 36:case 114:case 125:case 126:case 127:case 128:case 129:case 130:case 131:
                    case 134:case 135:case 136:case 245:case 246:case 247:case 248:case 266:case 267:case 396:case 397:case 398:return true;
                }
            }
            return false;
        }
        internal ToolIntent Choose(Player p)
        {
            int mode=host.Mode(0);if(mode==0 || host.Fishing.Active || host.Input.Frame<nextProbe || !host.Admit(p,mode==2))return null;
            nextProbe=host.Input.Frame+4;if(Boss())return null;
#if DEBUG
            Probes++;
#endif
            slots[0]=slots[1]=slots[2]=-1;
            for(int i=0;i<50;i++)
            {
                if(mode==2 && i!=p.selectedItem || !host.Candidate(p,i))continue;
                var net=p.inventory[i];if(!NetGeometry.IsNet(net))continue;int index=Index(net.type),old=slots[index];
                if(old<0 || p.GetAdjustedItemScale(net)>p.GetAdjustedItemScale(p.inventory[old]))slots[index]=i;
            }
            NPC target=null;int bestSlot=-1,bestIndex=-1;float bestDistance=float.MaxValue;int bestFrames=int.MaxValue;
            for(int i=0;i<host.Npcs.Count;i++)
            {
                NPC n=host.Npcs.Active(i);if(n==null || ReferenceEquals(unknownTargets[i],n) && unknownGenerations[i]==n.generation || (host.Settings[0].Value.Categories&(1<<Category(n)))==0)continue;
                for(int k=0;k<3;k++)
                {
                    int slot=slots[k];if(slot<0)continue;Item net=p.inventory[slot];
                    if(!Catchable(n,net) || !geometry[k].Hits(p,net,n.Hitbox,host.Input.Frame))continue;
                    int frames=NetGeometry.Frames(p,net);float distance=Vector2.DistanceSquared(p.Center,n.Center);
                    if(frames>bestFrames || frames==bestFrames && distance>=bestDistance)continue;
                    target=n;bestSlot=slot;bestIndex=k;bestFrames=frames;bestDistance=distance;
                }
            }
            if(target==null)return null;
            int npcSlot=target.whoAmI,npcType=target.type,npcGeneration=target.generation;long session=host.Runtime.Generation;Item tool=p.inventory[bestSlot];
            long borrow=0;
            var intent=new ToolIntent{Kind=ToolKind.Capture,Slot=bestSlot,Target=target.Center};
            intent.Admitted=()=>{if(mode==1)borrow=host.Fishing.Prepare(p);};
            intent.Valid=()=>host.Mode(0)==mode && host.Runtime.Generation==session && !Boss() && npcSlot>=0 && npcSlot<Main.maxNPCs && ReferenceEquals(Main.npc[npcSlot],target) &&
                target.generation==npcGeneration && target.type==npcType && Catchable(target,tool) && (host.Settings[0].Value.Categories&(1<<Category(target)))!=0 &&
                (mode!=2 || p.selectedItem==bestSlot) && geometry[bestIndex].Hits(p,tool,target.Hitbox,host.Input.Frame);
            intent.Completed=(started,unknown)=>{if(unknown){unknownTargets[npcSlot]=target;unknownGenerations[npcSlot]=npcGeneration;}host.Fishing.NetFinished(borrow,started,unknown);};
            return intent;
        }
        private static int Index(int type){return type==1991?0:type==3183?1:2;}
    }
}
