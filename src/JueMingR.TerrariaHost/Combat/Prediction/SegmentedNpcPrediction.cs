using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Game-thread only. This owns small motion observations, not copied NPC
    // pages or a second simulation. Only the selected part is published.
    internal sealed class SegmentedNpcPrediction
    {
        private struct History
        {
            internal NpcIdentity Identity;
            internal long Tick;
            internal Vector2 Center,Velocity;
            internal bool Observed;
        }
        private readonly History[] history=new History[NpcPredictionCache.Capacity];
        private readonly NpcTrajectoryPoint[] points=new NpcTrajectoryPoint[NpcPredictionCache.Horizon+1];
        private NpcIdentity current,parent,child;
        private float parentLink,childLink;
        private int lifeOwner;
        private long version,relationVersion;

        // Locked .8: AI_006_Worms / AI_037_Destroyer create these explicit
        // multi-hit families. aiStyle and realLife alone are insufficient:
        // EoW clears realLife, while Crawltipede has only a vulnerable tail.
        internal static int Family(int type)
        {
            switch(type)
            {
                case NPCID.TheDestroyer:case NPCID.TheDestroyerBody:case NPCID.TheDestroyerTail:return 1;
                case NPCID.EaterofWorldsHead:case NPCID.EaterofWorldsBody:case NPCID.EaterofWorldsTail:return 2;
                case NPCID.WyvernHead:case NPCID.WyvernLegs:case NPCID.WyvernBody:case NPCID.WyvernBody2:case NPCID.WyvernBody3:case NPCID.WyvernTail:return 3;
                case NPCID.SeekerHead:case NPCID.SeekerBody:case NPCID.SeekerTail:return 4;
                case NPCID.DevourerHead:case NPCID.DevourerBody:case NPCID.DevourerTail:return 5;
                case NPCID.GiantWormHead:case NPCID.GiantWormBody:case NPCID.GiantWormTail:return 6;
                case NPCID.BoneSerpentHead:case NPCID.BoneSerpentBody:case NPCID.BoneSerpentTail:return 7;
                case NPCID.DiggerHead:case NPCID.DiggerBody:case NPCID.DiggerTail:return 8;
                case NPCID.LeechHead:case NPCID.LeechBody:case NPCID.LeechTail:return 9;
                case NPCID.DuneSplicerHead:case NPCID.DuneSplicerBody:case NPCID.DuneSplicerTail:return 10;
                case NPCID.TombCrawlerHead:case NPCID.TombCrawlerBody:case NPCID.TombCrawlerTail:return 11;
                case NPCID.CultistDragonHead:case NPCID.CultistDragonBody1:case NPCID.CultistDragonBody2:case NPCID.CultistDragonBody3:case NPCID.CultistDragonBody4:case NPCID.CultistDragonTail:return 12;
                case NPCID.BloodEelHead:case NPCID.BloodEelBody:case NPCID.BloodEelTail:return 13;
                default:return 0;
            }
        }
        internal void Clear()
        {Array.Clear(history,0,history.Length);current=parent=child=default(NpcIdentity);relationVersion++;}
        internal NpcTrajectory Prepare(NpcIdentity identity,long tick,int required)
        {
            NPC n=Main.npc[identity.Slot];int family=Family(n.type);
            if(required==0 || family==0 || !CombatSelection.Receives(n,true) || !Finite(n.Center))return null;
            NPC before=Neighbor(n.ai[1],family),after=Neighbor(n.ai[0],family);
            NpcIdentity p=before==null?default(NpcIdentity):CombatSelection.Identity(before,identity.Session);
            NpcIdentity c=after==null?default(NpcIdentity):CombatSelection.Identity(after,identity.Session);
            bool changed=!current.Equals(identity) || !parent.Equals(p) || !child.Equals(c) || parentLink!=n.ai[1] || childLink!=n.ai[0] || lifeOwner!=n.realLife;
            if(changed){relationVersion++;current=identity;parent=p;child=c;parentLink=n.ai[1];childLink=n.ai[0];lifeOwner=n.realLife;}
            // Adjacent observations also give a just-selected part a recent
            // sample. There is no per-step/full-chain read or hidden AI work.
            if(before!=null)Observe(before,identity.Session,tick);
            if(after!=null && !ReferenceEquals(after,before))Observe(after,identity.Session,tick);
            History h=Observe(n,identity.Session,tick);
            Vector2 velocity=h.Velocity;
            var quality=h.Observed?PredictionQuality.ObservedTrend:PredictionQuality.LimitedObservation;
            if(!h.Observed && velocity.LengthSquared()==0 && before!=null)
            {
                History b=history[before.whoAmI];
                // No arbitrary forward direction for a fresh zero-velocity
                // body: only a measured adjacent motion may seed the first tick.
                if(b.Observed && b.Tick==tick)velocity=b.Velocity;
            }
            var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,
                Vx=velocity.X,Vy=velocity.Y,A0=n.ai[0],Active=true,CanReceive=true,CanHarm=!n.friendly && n.damage>0,
                NetOffsetX=n.netOffset.X,NetOffsetY=n.netOffset.Y};
            points[0]=new NpcTrajectoryPoint(0,state);
            // Measured position-difference baseline. No unvalidated curvature
            // is accumulated into repeated circles or accelerating motion.
            // These worms can cross tiles; applying ground collision here
            // would invent a stop. Distant positions remain conditional trends.
            for(int i=1;i<=required;i++)
            {
                state.X=n.position.X+velocity.X*i;state.Y=n.position.Y+velocity.Y*i;
                if(!Finite(new Vector2(state.X,state.Y)))return null;
                points[i]=new NpcTrajectoryPoint(i,state);
            }
            var assumptions=PredictionAssumption.ApproximateMechanism|PredictionAssumption.NoNewHits|PredictionAssumption.CurrentConnection;
            if(Main.netMode==1)assumptions|=PredictionAssumption.NetworkObservation;
            return new NpcTrajectory(identity,tick,++version,assumptions,PredictionStop.None,points,required+1,PredictionStrategy.SegmentedTrend,relationVersion,quality);
        }
        private History Observe(NPC n,long session,long tick)
        {
            var identity=CombatSelection.Identity(n,session);History prior=history[n.whoAmI];
            if(prior.Identity.Equals(identity) && prior.Tick==tick)return prior;
            long elapsed=tick-prior.Tick;
            // Type changes retire results, but native Transform keeps the
            // instance. Center history survives a size change without turning
            // its top-left repositioning into false movement. A skipped long
            // interval, reuse or a network jump never lends old velocity.
            bool same=SameInstance(prior.Identity,identity) && Family(prior.Identity.Type)==Family(identity.Type);
            Vector2 delta=n.Center-prior.Center;
            bool observed=same && elapsed>0 && elapsed<=4 && Finite(delta) && delta.LengthSquared()<=256f*256f*elapsed*elapsed;
            Vector2 velocity=observed?delta/(float)elapsed:n.velocity;
            if(!Finite(velocity) || velocity.LengthSquared()>256f*256f)velocity=Vector2.Zero;
            var value=new History{Identity=identity,Tick=tick,Center=n.Center,Velocity=velocity,Observed=observed};
            history[n.whoAmI]=value;return value;
        }
        private static NPC Neighbor(float link,int family)
        {
            if(float.IsNaN(link) || float.IsInfinity(link) || link<=0 || link>=Main.maxNPCs || link!=(int)link)return null;
            NPC value=Main.npc[(int)link];return value!=null && value.active && value.life>0 && Family(value.type)==family?value:null;
        }
        private static bool SameInstance(NpcIdentity a,NpcIdentity b)
        {return a.Session==b.Session && a.Slot==b.Slot && a.Generation==b.Generation && ReferenceEquals(a.Token,b.Token);}
        private static bool Finite(Vector2 value){return !float.IsNaN(value.X) && !float.IsNaN(value.Y) && !float.IsInfinity(value.X) && !float.IsInfinity(value.Y);}
    }
}
