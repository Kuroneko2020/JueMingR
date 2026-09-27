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
        private readonly CaptureOpportunity opportunities=new CaptureOpportunity();
        private int bossEpoch=-1;
        private bool boss;
        internal void ClearUnknown(){Array.Clear(unknownTargets,0,unknownTargets.Length);}
        internal void Reset(){opportunities.Clear();foreach(var shape in geometry)shape.Clear();nextProbe=0;bossEpoch=-1;}
        private long nextProbe;
#if DEBUG
        internal long Probes {get;private set;}
        internal long BossEvaluations {get;private set;}
        internal long BossSlotVisits {get;private set;}
        internal long CandidateNpcVisits {get;private set;}
        internal long ActiveTargetRefreshes {get;private set;}
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
            if(bossEpoch==host.Npcs.Epoch)return boss;
            bossEpoch=host.Npcs.Epoch;boss=false;
#if DEBUG
            BossEvaluations++;
#endif
            for(int i=0;i<host.Npcs.Count;i++)
            {
#if DEBUG
                BossSlotVisits++;
#endif
                GuidanceNpc n;if(!host.Npcs.TryRead(i,NpcDemand.Danger,out n) || !n.Active || n.Life<=0)continue;
                if(n.Boss){boss=true;return true;}
                switch(n.Type)
                {
                    case 13:case 14:case 15:case 35:case 36:case 114:case 125:case 126:case 127:case 128:case 129:case 130:case 131:
                    case 134:case 135:case 136:case 245:case 246:case 247:case 248:case 266:case 267:case 396:case 397:case 398:boss=true;return true;
                }
            }
            return false;
        }
        internal bool Ready(Player p){NPC target;int slot,index;return Find(p,out target,out slot,out index);}
        private bool Find(Player p,out NPC target,out int bestSlot,out int bestIndex)
        {
            target=null;bestSlot=bestIndex=-1;
            int mode=host.Mode(0);if(mode==0 || host.Fishing.Active || !host.Admit(p,mode==2))return false;
            slots[0]=slots[1]=slots[2]=-1;
            for(int i=0;i<50;i++)
            {
                if(mode==2 && i!=p.selectedItem || !host.Candidate(p,i))continue;
                var net=p.inventory[i];if(!NetGeometry.IsNet(net))continue;int index=Index(net.type),old=slots[index];
                if(old<0 || p.GetAdjustedItemScale(net)>p.GetAdjustedItemScale(p.inventory[old]))slots[index]=i;
            }
            if(slots[0]<0 && slots[1]<0 && slots[2]<0)return false;
            if(Boss())return false;
            float bestDistance=float.MaxValue;int bestFrames=int.MaxValue;
            for(int i=0;i<host.Npcs.Count;i++)
            {
#if DEBUG
                CandidateNpcVisits++;
#endif
                NPC n=host.Npcs.Active(i);if(n==null || ReferenceEquals(unknownTargets[i],n) && unknownGenerations[i]==n.generation || (host.Settings[0].Value.Categories&(1<<Category(n)))==0)continue;
                int selected=-1,fastest=int.MaxValue;
                for(int k=0;k<3;k++)
                {
                    int slot=slots[k];if(slot<0)continue;Item net=p.inventory[slot];
                    if(!Catchable(n,net))continue;
                    if(!geometry[k].Opportunity(p,net,n,0))continue;
                    int frames=NetGeometry.Frames(p,net);if(frames>=fastest)continue;fastest=frames;selected=k;
                }
                if(selected<0){opportunities.Outside(n);continue;}
                // Choose the best actual net first. Merely scanning another
                // carried net must not re-arm the same failed opportunity.
                int chosen=slots[selected];if(!opportunities.Allows(p,p.inventory[chosen],n,geometry[selected].ShapeVersion))continue;
                float distance=Vector2.DistanceSquared(p.Center,n.Center);
                if(fastest>bestFrames || fastest==bestFrames && distance>=bestDistance)continue;
                target=n;bestSlot=chosen;bestIndex=selected;bestFrames=fastest;bestDistance=distance;
            }
            return target!=null;
        }
        internal ToolIntent Choose(Player p)
        {
            if(host.Input.Frame<nextProbe)return null;nextProbe=host.Input.Frame+1;
            NPC target;int bestSlot,bestIndex;if(!Find(p,out target,out bestSlot,out bestIndex))return null;
            int mode=host.Mode(0);
#if DEBUG
            Probes++;
#endif
            int npcSlot=target.whoAmI,npcType=target.type,npcGeneration=target.generation;long session=host.Runtime.Generation;Item tool=p.inventory[bestSlot];
            long borrow=0;int swings=0;bool exhausted=false,leftOpportunity=false;
            Vector2 attemptedMotion=target.velocity-p.velocity;
            var intent=new ToolIntent{Kind=ToolKind.Capture,Slot=bestSlot,Target=target.Center};
            intent.Admitted=()=>{if(mode==1)borrow=host.Fishing.Prepare(p);};
            intent.Used=()=>{swings++;attemptedMotion=target.velocity-p.velocity;};
            // An automatic scheduler handoff ends an attempted opportunity;
            // it must not reset the budget merely by returning the fishing rod.
            // Manual/safety cancellation remains separate from a normal miss.
            intent.Yielded=()=>{if(swings>0){exhausted=true;leftOpportunity=!geometry[bestIndex].Opportunity(p,tool,target,0);}};
            intent.Valid=()=>host.Mode(0)==mode && host.Runtime.Generation==session && !Boss() && npcSlot>=0 && npcSlot<Main.maxNPCs && ReferenceEquals(Main.npc[npcSlot],target) &&
                target.generation==npcGeneration && target.type==npcType && Catchable(target,tool) && (host.Settings[0].Value.Categories&(1<<Category(target)))!=0 &&
                (mode!=2 || p.selectedItem==bestSlot);
            intent.Refresh=()=>
            {
#if DEBUG
                ActiveTargetRefreshes++;
#endif
                if(!intent.Valid())return false;
                intent.Target=target.Center;
                // A changed trajectory can use the remaining native phases.
                // Do not cancel/reborrow mid-swing because prediction changed;
                // the real Catch rectangle remains the final authority.
                if(p.itemAnimation>1)return true;
                bool possible=geometry[bestIndex].Opportunity(p,tool,target,0),next=swings<2 && possible;
                if(!next && swings>0){exhausted=true;leftOpportunity=!possible;}return next;
            };
            intent.Completed=(started,unknown)=>
            {
                if(unknown){unknownTargets[npcSlot]=target;unknownGenerations[npcSlot]=npcGeneration;}
                else if(exhausted && started && target.active)
                {
                    bool outside=leftOpportunity || !geometry[bestIndex].Opportunity(p,tool,target,0);
                    opportunities.Finish(p,tool,target,geometry[bestIndex].ShapeVersion,outside,attemptedMotion);
                }
                host.Fishing.NetFinished(borrow,started,unknown);
            };
            return intent;
        }
        private static int Index(int type){return type==1991?0:type==3183?1:2;}
    }
}
