using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    internal struct NpcInvasionTarget
    {
        internal MotionRect Area,Hitbox;
        internal float Vx,Vy;
        internal int Kind;
        internal NpcIdentity Identity;
        internal bool Invalid {get{return Kind==0;}}
    }
    // NPCUtils.TargetClosestOldOnesInvasion searches Euclidean geometry. Its
    // chosen facing hitbox can be a pet while GetTargetData(true) still reads
    // that pet's player. Neither operation runs a live targeting/AI action.
    internal static class NpcInvasionTargeting
    {
        internal static bool NeedsPlayer(NpcMotionState n)
        {return !n.DD2PlayerPremiseCaptured || n.DD2PlayerPremiseIndex>=0;}
        internal static int PlayerPremise(NpcMotionState n)
        {return n.DD2PlayerPremiseCaptured?n.DD2PlayerPremiseIndex:n.PlayerIndex;}
        internal static int ChoosePlayerPremise(NpcMotionState n,PredictionEnvironment e)
        {
            var players=e.InvasionPlayers;if(players==null)return -1;
            int winner=-1,firstAlive=-1;float score=float.MaxValue;
            for(int i=0;i<players.Count;i++)
            {
                var p=players[i];if(!p.Alive)continue;if(firstAlive<0)firstAlive=p.Slot;
                float distance=Distance(n.Bounds.CenterX,n.Bounds.CenterY,p.Area.CenterX,p.Area.CenterY);
                // The old direct target remains a GetTargetData prerequisite
                // when the new search finds no candidate. This does not widen
                // the actual 200-pixel candidate filter in TryTarget.
                if(!Betsy(n) && distance>200 && p.Slot!=n.Target)continue;
                float candidate=distance-p.Aggro;if(p.NoAggro(n.EffectiveType) && n.Direction==0)candidate+=1000;
                if(candidate<score){score=candidate;winner=p.Slot;}
            }
            // Even the sole far player can naturally enter 200 in the future.
            // Acquire that one real continuation instead of freezing its pose.
            return winner>=0?winner:firstAlive;
        }
        internal static bool TryTarget(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,NpcMotionState[] group,int count,bool faceTarget,bool confused,out NpcInvasionTarget target,out PredictionStop stop)
        {
            target=default(NpcInvasionTarget);stop=PredictionStop.None;
            if(!n.DD2TargetFactsCaptured || !n.DD2TargetFactsComplete || e.InvasionPlayers==null || group==null)
            {stop=PredictionStop.MissingDependency;return false;}
            if(elapsed>0)for(int i=0;i<e.InvasionPlayers.Count;i++)
            {var other=e.InvasionPlayers[i];if(other.Alive && other.Slot!=e.PlayerIndex){stop=PredictionStop.MissingDependency;return false;}}
            int npc=-1,player=-1;float npcSquared=float.MaxValue,playerScore=float.MaxValue,playerDistance=float.MaxValue;bool pet=false;MotionRect facing=default(MotionRect);
            for(int i=0;i<count;i++)
            {
                var other=group[i];if(!other.Active || other.Identity.Equals(n.Identity) || other.EffectiveType!=548 || other.HostileProtected)continue;
                float dx=other.Bounds.CenterX-n.Bounds.CenterX,dy=other.Bounds.CenterY-n.Bounds.CenterY,squared=dx*dx+dy*dy;
                if(squared<npcSquared){npcSquared=squared;npc=i;}
            }
            var players=e.InvasionPlayers;
            for(int i=0;i<players.Count;i++)
            {
                var p=players[i];if(!p.Alive)continue;
                MotionRect area=PlayerArea(p,e);float distance=Distance(n.Bounds.CenterX,n.Bounds.CenterY,area.CenterX,area.CenterY);
                // Betsy's native helper shares this exact search but has no
                // OnlyPlayersInCertainDistance filter. Its target remains the
                // nearest NPC/player/pet result even when facing is disabled.
                if(!Betsy(n) && distance>200)continue;
                bool noAggro=p.NoAggro(n.EffectiveType);float score=distance-p.Aggro;if(noAggro && n.Direction==0)score+=1000;
                if(score<playerScore){playerScore=score;playerDistance=distance;player=i;pet=false;facing=Hitbox(area);}
                if(!p.HasPet || noAggro)continue;
                float petDistance=Distance(n.Bounds.CenterX,n.Bounds.CenterY,p.PetArea.CenterX,p.PetArea.CenterY),petScore=petDistance-200;
                if(petScore>=playerScore || petScore>=200)continue;
                // A pet can affect facing independently of the player's future.
                // Its sampled pose is exact at capture; no pet AI continuation
                // is invented when it would become a future search winner.
                if(e.PlayerTimelineActive){stop=PredictionStop.MissingDependency;return false;}
                if(terrain==null){stop=PredictionStop.MissingDependency;return false;}
                bool clear;if(!terrain.CanHit(new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1),new MotionRect(p.PetArea.CenterX,p.PetArea.CenterY,1,1),out clear,out stop))return false;
                if(clear){playerScore=petScore;playerDistance=petDistance;player=i;pet=true;facing=Hitbox(p.PetArea);}
            }
            // A tie between NPC and adjusted player/pet score selects the
            // player/pet. No found candidate leaves target/targetRect intact.
            bool found=npc>=0 || player>=0;
            if(found && npc>=0 && (player<0 || (float)Math.Sqrt(npcSquared)<playerScore))
            {
                var other=group[npc];n.Target=other.Identity.Slot+300;n.DD2TargetIdentity=other.Identity;n.DD2TargetPlayerToken=null;n.DD2FaceArea=Hitbox(other.Bounds);
                if(faceTarget)Face(ref n,n.DD2FaceArea);
            }
            else if(found)
            {
                var p=players[player];
                if(e.PlayerTimelineActive && p.Slot!=e.PlayerIndex){stop=PredictionStop.MissingDependency;return false;}
                n.Target=p.Slot;n.DD2TargetPlayerToken=p.Token;n.DD2TargetIdentity=default(NpcIdentity);n.DD2FaceArea=facing;
                bool mayFace=pet || !(p.NoAggro(n.EffectiveType) && n.Direction!=0);
                float hiddenRange=(p.Area.Width+p.Area.Height+n.Width+n.Height)/4+800;
                if(!pet && p.Idle && p.Aggro<0 && playerDistance>hiddenRange && n.DD2OldTarget>=0 && n.DD2OldTarget<255)mayFace=false;
                if(faceTarget && mayFace)Face(ref n,facing);
            }
            // Unlike ordinary TargetClosest, this native helper does not
            // invert facing for confused. Callers still own attack confusion.
            return ReadTarget(n,e,elapsed,group,count,out target,out stop);
        }
        internal static bool ReadTarget(NpcMotionState n,PredictionEnvironment e,int elapsed,NpcMotionState[] group,int count,out NpcInvasionTarget target,out PredictionStop stop)
        {
            target=default(NpcInvasionTarget);stop=PredictionStop.None;
            if(!n.DD2TargetFactsCaptured){stop=PredictionStop.MissingDependency;return false;}
            if(n.Target>=300 && n.Target<500)
            {
                if(n.DD2TargetIdentity.Token==null)return true;
                if(group!=null)for(int i=0;i<count;i++)if(group[i].Identity.Equals(n.DD2TargetIdentity))
                {var other=group[i];if(other.Active)target=new NpcInvasionTarget{Kind=3,Identity=other.Identity,Area=other.Bounds,Hitbox=Hitbox(other.Bounds),Vx=other.Vx,Vy=other.Vy};return true;}
                stop=PredictionStop.MissingDependency;return false;
            }
            var players=e.InvasionPlayers;if(n.Target<0 || n.Target>=255)return true;
            if(players==null){stop=PredictionStop.MissingDependency;return false;}
            for(int i=0;i<players.Count;i++)
            {
                var p=players[i];if(p.Slot!=n.Target || !ReferenceEquals(p.Token,n.DD2TargetPlayerToken))continue;if(!p.Alive)return true;
                if(e.PlayerTimelineActive && p.Slot!=e.PlayerIndex){stop=PredictionStop.MissingDependency;return false;}
                var area=PlayerArea(p,e);target=new NpcInvasionTarget{Kind=1,Area=area,Hitbox=Hitbox(area),Vx=p.Slot==e.PlayerIndex?e.PlayerVx:p.Vx,Vy=p.Slot==e.PlayerIndex?e.PlayerVy:p.Vy};return true;
            }
            return true;
        }
        private static MotionRect PlayerArea(PredictionInvasionPlayer p,PredictionEnvironment e)
        {return p.Slot==e.PlayerIndex?new MotionRect(e.PlayerX-e.PlayerWidth*.5f,e.PlayerY-e.PlayerHeight*.5f,e.PlayerWidth,e.PlayerHeight):p.Area;}
        private static MotionRect Hitbox(MotionRect area){return new MotionRect((int)area.X,(int)area.Y,(int)area.Width,(int)area.Height);}
        private static void Face(ref NpcMotionState n,MotionRect area)
        {n.Direction=(int)area.X+(int)area.Width/2<n.Bounds.CenterX?-1:1;n.DirectionY=(int)area.Y+(int)area.Height/2<n.Bounds.CenterY?-1:1;}
        private static float Distance(float ax,float ay,float bx,float by)
        {float x=bx-ax,y=by-ay;return (float)Math.Sqrt(x*x+y*y);}
        private static bool Betsy(NpcMotionState n){return n.EffectiveType==551 && n.Style==110;}
    }
}
