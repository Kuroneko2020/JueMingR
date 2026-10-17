using System;
using System.Runtime.CompilerServices;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // AI82 consumes an already completed AI85 anchor. These two controllers
    // share the rolling owner's private group; neither replays live AI/RNG.
    internal static class NpcMoonLeechMotion
    {
        private struct V {internal float X,Y;}
        [MethodImpl(MethodImplOptions.NoInlining)] private static V Store(float x,float y){return new V{X=x,Y=y};}
        private static V Center(MotionRect b){return Store(b.X+b.Width*.5f,b.Y+b.Height*.5f);}
        private static V Sub(V a,V b){return Store(a.X-b.X,a.Y-b.Y);}
        private static float Length(V v){return (float)Math.Sqrt((double)v.X*v.X+(double)v.Y*v.Y);}
        private static V Aim(V d)
        {if(d.X==0 && d.Y==0)return default(V);float length=Length(d);double inverse=1d/length;V unit=Store((float)(d.X*inverse),(float)(d.Y*inverse));float speed=Math.Min(16,length);return Store(unit.X*speed,unit.Y*speed);}
        private static V Lerp(V a,V b,float t){return Store(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t);}
        internal static bool Known(NpcMotionState n){return n.EffectiveType==401 && n.Style==82;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b);}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e){return false;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {
            if(!Known(n) || n.A2>=89 || n.MoonLeechAnchorCaptured && !n.MoonLeechAnchorValid || e.MoonLeeches==null)return false;
            for(int i=0;i<e.MoonLeeches.Count;i++)
            {var q=e.MoonLeeches[i];if(q.Key==n.MoonLeechKey && q.A0>0 && (e.Multiplayer || q.L0<329 && q.PlayerActive && !q.PlayerDead))return true;}
            return false;
        }
        internal static int PlayerPremise(NpcMotionState n,PredictionEnvironment e)
        {if(e.MoonLeeches!=null)for(int i=0;i<e.MoonLeeches.Count;i++)if(e.MoonLeeches[i].Key==n.MoonLeechKey)return e.MoonLeeches[i].PlayerIndex;return n.PlayerIndex;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!n.MoonRelationsCaptured || !n.MoonRelationsComplete || !n.PositionOwnerCaptured){stop=PredictionStop.MissingDependency;return false;}
            int head=Find(group,count,n.PositionOwner);
            if(n.PositionOwnerValid && head<0){stop=PredictionStop.MissingDependency;return false;}
            if(!n.PositionOwnerValid || !group[head].Active || group[head].EffectiveType!=396)
            {n.Life=0;n.Active=false;stop=PredictionStop.Despawn;return false;}
            if(!Finite(n.A2)){stop=PredictionStop.InvalidState;return false;}
            // Captured withdrawing/dead foreign cores have a finite global
            // write, not an unknown motor. Preserve native writer/reader slot
            // order; clearing the key does not retire the clot or its clock.
            if(e.MoonLeeches!=null)for(int i=0;i<e.MoonLeeches.Count;i++)
            {
                var fact=e.MoonLeeches[i];if(fact.Key!=n.MoonLeechKey)continue;
                if(elapsed>fact.ClearAt || elapsed==fact.ClearAt && fact.ClearWriterSlot<n.Identity.Slot)n.MoonLeechAnchorValid=false;
                break;
            }
            if(n.MoonLeechAnchorValid && n.A2<89 && elapsed>=n.MoonGlobalRetireAt){stop=PredictionStop.PhaseBoundary;return false;}
            n.A2++;
            if(n.A2>=90)
            {
                if(!e.Multiplayer)
                {
                    var owner=group[head];int core=Find(group,count,n.MoonPart0),left=-1,right=-1,coreSlot=(int)owner.A3;
                    for(int i=0;i<count;i++)if(group[i].Active && group[i].EffectiveType==397 && group[i].A3==coreSlot)
                    {if(group[i].A2==0 && (left<0 || group[i].Identity.Slot<group[left].Identity.Slot))left=i;if(group[i].A2==1 && (right<0 || group[i].Identity.Slot<group[right].Identity.Slot))right=i;}
                    // Native indexes both sidehands before doing ANY healing.
                    // A missing side is not an invented replacement actor.
                    if(core<0 || left<0 || right<0){stop=PredictionStop.MissingDependency;return false;}
                    int headDeficit=Deficit(n,group[head]),coreDeficit=Deficit(n,group[core]),leftDeficit=Deficit(n,group[left]),rightDeficit=Deficit(n,group[right]),budget=1000;
                    // Capture all deficits first. Distinct original array slots
                    // can alias a raw core/head; native does not re-read life.
                    Heal(ref n,group,head,headDeficit,ref budget);Heal(ref n,group,core,coreDeficit,ref budget);Heal(ref n,group,left,leftDeficit,ref budget);Heal(ref n,group,right,rightDeficit,ref budget);
                }
                n.Life=0;n.Active=false;stop=PredictionStop.Despawn;return false;
            }
            n.Vx=n.Vy=0;
            if(!n.MoonLeechAnchorCaptured){stop=PredictionStop.MissingDependency;return false;}
            // A completely observed absent/stale key only logs the vanilla
            // invariant. It leaves the zero-velocity body at its current pose.
            if(!n.MoonLeechAnchorValid)return true;
            V headCenter=Center(group[head].Bounds),goal=Store(headCenter.X,headCenter.Y+216),anchor=Store(n.MoonLeechAnchorX,n.MoonLeechAnchorY);
            V center=Lerp(anchor,goal,n.A2/90f);V position=Store(center.X-n.Width*.5f,center.Y-n.Height*.5f);n.X=position.X;n.Y=position.Y;return true;
        }
        private static int Deficit(NpcMotionState self,NpcMotionState row)
        {return row.Identity.Equals(self.Identity)?self.LifeMax-self.Life:row.LifeMax-row.Life;}
        private static void Heal(ref NpcMotionState self,NpcMotionState[] group,int index,int deficit,ref int budget)
        {
            if(deficit<=0 || budget<=0)return;int leftover=deficit-budget;if(leftover>0)leftover=0;int amount=budget+leftover;budget-=amount;
            if(group[index].Identity.Equals(self.Identity))self.Life+=amount;else group[index].Life+=amount;
        }
        private static int Find(NpcMotionState[] group,int count,NpcIdentity identity)
        {if(group!=null)for(int i=0;i<count;i++)if(group[i].Identity.Equals(identity))return i;return -1;}
        internal struct LeechWork
        {internal PredictionMoonLeech Fact;internal bool Active,Wet;internal float X,Y,Vx,Vy,A0,L0,L1;internal int TimeLeft;}
        internal static void ClearAnchors(NpcMotionState[] group,int count)
        {
            if(group==null)return;
            for(int i=0;i<count;i++)if(Known(group[i]) && group[i].MoonLeechAnchorCaptured)
            {var row=group[i];row.MoonLeechAnchorValid=false;group[i]=row;}
        }
        internal static LeechWork[] CreateLeeches(PredictionEnvironment e)
        {
            if(e.MoonLeeches==null)return null;var work=new LeechWork[e.MoonLeeches.Count];for(int i=0;i<work.Length;i++)
            {var f=e.MoonLeeches[i];work[i]=new LeechWork{Fact=f,Active=true,Wet=f.Wet,X=f.Area.X,Y=f.Area.Y,Vx=f.Vx,Vy=f.Vy,A0=f.A0,L0=f.L0,L1=f.L1,TimeLeft=f.TimeLeft};}return work;
        }
        internal static bool EndFrame(LeechWork[] work,NpcMotionState[] group,int count,PredictionEnvironment e,PredictionPlayerMotion player,bool firstPlayerStepCompleted,bool observedPlayer,int elapsed,IPredictionTerrain terrain,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(work==null)return true;
            for(int i=0;i<work.Length;i++)
            {
                var p=work[i];var f=p.Fact;bool consumed=false;for(int j=0;j<count;j++)if(Known(group[j]) && group[j].Active && group[j].MoonLeechKey==f.Key){consumed=true;break;}
                // The core NPC write precedes ALL projectile Update actions.
                // Mirror it in the worker even for an earlier-slot clot whose
                // last completed action legitimately read the old anchor.
                bool cleared=elapsed>=f.ClearAt;
                for(int j=0;!cleared && j<count;j++)if(Known(group[j]) && group[j].MoonLeechKey==f.Key && group[j].MoonLeechAnchorCaptured && !group[j].MoonLeechAnchorValid)cleared=true;
                if(cleared)p.Active=false;
                // No active forecast clot will read this anchor again. Newborn
                // slots remain an independently bounded controller effect.
                if(!consumed){work[i]=p;continue;}
                if(p.Active && (f.ExtraUpdates<0 || f.ExtraUpdates>15)){stop=PredictionStop.MissingDependency;return false;}
                for(int sub=0;sub<=f.ExtraUpdates && p.Active;sub++)
                {
                    var bounds=e.MoonLeeches;
                    if(p.X<=bounds.Left || p.X+f.Area.Width>=bounds.Right || p.Y<=bounds.Top || p.Y+f.Area.Height>=bounds.Bottom || !Finite(p.X) || !Finite(p.Y))
                    {p.Active=false;break;}
                    // Native world rejection precedes AI/profile consumers.
                    if(!f.SettingsKnown || !Finite(p.A0) || !Finite(p.L0) || !Finite(p.L1))
                    {stop=PredictionStop.MissingDependency;return false;}
                    if(!f.HeadCaptured){stop=PredictionStop.MissingDependency;return false;}int head=Find(group,count,f.HeadIdentity);
                    if(head<0){stop=PredictionStop.MissingDependency;return false;}var n=group[head];if(!n.Active || n.EffectiveType!=396){p.Active=false;break;}
                    float oldVx=p.Vx,oldVy=p.Vy;p.L0++;
                    if(!e.Multiplayer && p.A0>0 && (p.L0>=330 || !f.PlayerActive || f.PlayerDead))p.A0*=-1;
                    V target;
                    if(p.A0>0)
                    {
                        if(!f.PlayerCaptured){stop=PredictionStop.MissingDependency;return false;}
                        if(e.PlayerTimelineActive && !observedPlayer && player.PlayerIndex==f.PlayerIndex && ReferenceEquals(player.PlayerToken,f.PlayerToken))
                            target=Center(new MotionRect(player.X,player.Y,player.Width,player.Height));
                        else if(firstPlayerStepCompleted && elapsed==1 && player.PlayerIndex==f.PlayerIndex && ReferenceEquals(player.PlayerToken,f.PlayerToken))target=Center(f.PlayerArea);
                        else{stop=PredictionStop.MissingDependency;return false;}
                    }
                    else target=Center(n.Bounds);
                    V delta=Sub(target,Center(new MotionRect(p.X,p.Y,f.Area.Width,f.Area.Height)));
                    // Native return aim stores subtraction BEFORE adding216;
                    // adding to the head first changes float boundary branches.
                    if(p.A0<=0)delta=Store(delta.X,delta.Y+216);
                    V velocity=Aim(delta);p.Vx=velocity.X;p.Vy=velocity.Y;
                    if(Length(delta)<20)
                    {
                        if(p.A0<=0){p.Active=false;break;}
                        if(p.L1==0)p.L1=1; // Buff145 has no player motor consumer.
                    }
                    if(f.Owner<255 && !f.OwnerActive){p.Active=false;break;}
                    if(!f.IgnoreWater && !Wet(new MotionRect(p.X,p.Y,f.Area.Width,f.Area.Height),e,terrain,out p.Wet,out stop))return false;
                    V moved=Store(p.X+(p.Wet?oldVx:p.Vx),p.Y+(p.Wet?oldVy:p.Vy));p.X=moved.X;p.Y=moved.Y;
                    if(--p.TimeLeft<=0 || f.Penetrate==0)p.Active=false;
                }
                work[i]=p;V centerNow=Center(new MotionRect(p.X,p.Y,f.Area.Width,f.Area.Height));
                for(int j=0;j<count;j++)if(Known(group[j]) && group[j].MoonLeechKey==f.Key)
                {group[j].MoonLeechAnchorCaptured=true;group[j].MoonLeechAnchorValid=p.Active;group[j].MoonLeechAnchorX=centerNow.X;group[j].MoonLeechAnchorY=centerNow.Y;}
            }
            return true;
        }
        private static bool Wet(MotionRect b,PredictionEnvironment e,IPredictionTerrain terrain,out bool wet,out PredictionStop stop)
        {
            wet=false;stop=PredictionStop.None;if(terrain==null){stop=PredictionStop.TerrainUnavailable;return false;}
            int width=(int)b.Width,height=(int)b.Height;V center=Store(b.X+width/2,b.Y+height/2);int sampleWidth=Math.Min(10,width),sampleHeight=height/2;
            V probe=Store(center.X-sampleWidth/2,center.Y-sampleHeight/2);
            int left=Clamp((int)(b.X/16f)-1,0,e.WorldWidth-1),right=Clamp((int)((b.X+width)/16f)+2,0,e.WorldWidth-1),top=Clamp((int)(b.Y/16f)-1,0,e.WorldHeight-40),bottom=Clamp((int)((b.Y+height)/16f)+2,0,e.WorldHeight-40);
            for(int x=left;x<right;x++)for(int y=top;y<bottom;y++)
            {
                PredictionTile t;if(!terrain.Tile(x,y,out t,out stop))return false;float tileY=y*16;int liquidHeight=16;
                if(t.Liquid>0){float level=256-t.Liquid;level/=32f;tileY+=level*2f;liquidHeight-=(int)(level*2f);}
                else
                {
                    if(!t.RawActive || t.Slope==0 || y<=0)continue;PredictionTile upper;if(!terrain.Tile(x,y-1,out upper,out stop))return false;if(upper.Liquid==0)continue;
                }
                if(probe.X+sampleWidth>x*16 && probe.X<x*16+16 && probe.Y+sampleHeight>tileY && probe.Y<tileY+liquidHeight){wet=true;return true;}
            }
            return true;
        }
        private static int Clamp(int value,int min,int max){return Math.Max(min,Math.Min(max,value));}
        private static bool Finite(float f){return !float.IsNaN(f) && !float.IsInfinity(f);}
    }
}
