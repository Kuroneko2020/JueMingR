using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked .8 AI84 only. Ritual490 affects presentation, not this
    // motor/receiving contract. No native AI, RNG, projectile or actor birth is
    // executed here. New sibling identities invalidate only their next consumer.
    internal static class NpcCultistBossMotion
    {
        internal static bool Known(NpcMotionState n){return n.Style==84 && (n.EffectiveType==439 || n.EffectiveType==440);}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b) && a.EffectiveType==b.EffectiveType;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {return !(n.EffectiveType==440 && n.PositionOwnerCaptured && !n.PositionOwnerValid);}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {return n.Target<0 || n.Target==255 || !n.PlayerActive || n.PlayerNativeDead || !n.PlayerVelocityCaptured || Length(n.PlayerCenterX-n.Bounds.CenterX,n.PlayerCenterY-n.Bounds.CenterY)>5600 || n.A0==0 && n.A1==0;}
        internal static int PlayerPremise(NpcMotionState n,PredictionEnvironment e,NpcMotionState[] group,int count)
        {
            var phase=n;
            if(n.EffectiveType==440){int owner=Find(group,count,n.PositionOwner);if(owner>=0){phase.A0=group[owner].A0;phase.A1=group[owner].A1;}}
            if(phase.L0==0 && phase.CultistNetMode!=1)phase.A0=-1;
            return Retargets(phase,e) && n.HasClosestPlayer?n.ClosestPlayerIndex:n.PlayerIndex;
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!n.CultistRelationsCaptured){stop=PredictionStop.MissingDependency;return false;}
            bool body=n.EffectiveType==439;float ownA3=n.A3;
            if(!body)
            {
                if(!n.PositionOwnerCaptured){stop=PredictionStop.MissingDependency;return false;}
                if(!n.PositionOwnerValid)return Retire(ref n,out stop);
                int parent=Find(group,count,n.PositionOwner);
                if(parent<0){stop=PredictionStop.MissingDependency;return false;}
                var owner=group[parent];
                if(!owner.Active || owner.EffectiveType!=439)return Retire(ref n,out stop);
                n.A0=owner.A0;n.A1=owner.A1;
                if(n.A0==5 && n.JustHit)
                {
                    // The parent write is unconditional; only SendData23 is
                    // server-only. The retired clone's presentation is omitted.
                    owner.A0=6;owner.A1=0;group[parent]=owner;
                    return Retire(ref n,out stop);
                }
            }
            else if(n.A0==5 && n.A1>=120 && n.A1<420 && n.JustHit)
            {
                if(!n.CultistRelationsComplete){stop=PredictionStop.MissingDependency;return false;}
                n.A0=0;n.A1=0;n.A3++;n.Vx=n.Vy=0;
                int remaining=e.Expert?3:10;var siblings=Siblings(n,group,count);
                foreach(int index in siblings)
                {
                    if(remaining<=0)break;var clone=group[index];clone.Life=0;clone.Active=false;group[index]=clone;remaining--;
                }
                // Cancelling490 is a one-way effect and has no motor readback.
            }
            var center=Center(n.X,n.Y,n.Width,n.Height);
            if(!n.TargetCaptured){stop=PredictionStop.MissingDependency;return false;}
            if(n.Target<0 || n.Target==255 || !n.HasPlayer || !n.PlayerActive || n.PlayerNativeDead || !n.PlayerVelocityCaptured || Length(n.PlayerCenterX-center.X,n.PlayerCenterY-center.Y)>5600)
                NpcTargeting.Face(ref n,ref e,false,confused);
            if(!Ready(n,elapsed,out stop))return false;
            if(!n.PlayerActive || n.PlayerNativeDead || Length(e.PlayerX-center.X,e.PlayerY-center.Y)>5600)
            {
                if(body)
                {
                    if(!n.CultistRelationsComplete){stop=PredictionStop.MissingDependency;return false;}
                    foreach(int index in Siblings(n,group,count)){var clone=group[index];clone.Active=false;clone.Life=0;group[index]=clone;}
                }
                return Retire(ref n,out stop);
            }
            // Native creates a local Player before its second TargetClosest.
            // Preserve this action's earlier geometry even if the target changes.
            float playerX=e.PlayerX,playerY=e.PlayerY;
            bool half=n.Life<=n.LifeMax/2,protectedPhase=!body && n.A0!=5;
            if(half)n.Health.Defense=(int)(n.Health.DefaultDefense*.65f);
            if(n.L0==0)
            {
                n.L0=1;n.Alpha=255;
                if(n.CultistNetMode!=1)n.A0=-1;
            }
            if(n.A0==-1)
            {
                n.Alpha=Math.Max(0,n.Alpha-5);n.A1++;
                if(n.A1>=420){n.A0=0;n.A1=0;}
                else if(n.A1>360){n.Vx*=.95f;n.Vy*=.95f;n.L2=13;}
                else if(n.A1>300){n.Vx=0;n.Vy=-1;n.L2=10;}
                else n.L2=n.A1>120?1:0;
                protectedPhase=true;
            }
            if(n.A0==0)
            {
                if(n.A1==0)NpcTargeting.Face(ref n,ref e,false,confused);
                if(!Ready(n,elapsed,out stop))return false;
                n.L2=10;Face(ref n,playerX-center.X);n.A1++;
                if(n.A1>=40 && body)
                {
                    int choice=Choice((int)n.A3,half);
                    if((int)n.A3==(half?13:11) || (int)n.A3<0 || (int)n.A3>(half?13:11))n.A3=-1;
                    if(e.Expert && half && choice!=0 && choice!=4 && choice!=5)
                    {
                        int dooms=0,certifiedSurvivors=0;
                        if(group!=null)for(int i=0;i<count;i++)if(group[i].Active && group[i].EffectiveType==523)
                        {
                            dooms++;int owner=Find(group,count,group[i].PositionOwner);
                            // A health-only foreign parent can retire in AI84
                            // without a new hit. Only a motor-certified active
                            // owner makes its surviving523 a reliable lower bound.
                            if(owner>=0 && group[owner].Active && group[owner].EffectiveType==439 && group[owner].CultistRelationsCaptured)certifiedSurvivors++;
                        }
                        bool complete=n.CultistDoomsCaptured && n.CultistDoomsComplete && elapsed<n.CultistForeignDoomBirthAt;
                        if(certifiedSurvivors<10 && (!complete || dooms<10))
                        {stop=complete?PredictionStop.RandomDecision:PredictionStop.MissingDependency;return false;}
                    }
                    if(choice==0)
                    {
                        if(!n.CultistRelationsComplete){stop=PredictionStop.MissingDependency;return false;}
                        Move(ref n,group,count,playerX,playerY);
                    }
                    else{n.A0=choice==1?3:choice==2?2:choice==3?4:choice==4?5:choice==5?7:8;n.A1=0;}
                }
            }
            else if(n.A0==1)
            {
                protectedPhase=true;n.L2=10;
                if((float)(int)n.A1%2!=0 && n.A1!=1){n.X-=n.Vx;n.Y-=n.Vy;}
                if(--n.A1<=0)Reset(ref n);
            }
            else if(n.A0==2)
            {
                n.L2=11;int period=e.Expert?90:120;if(e.GoodWorld)period-=30;
                if(body && n.A1>=4 && (int)(n.A1-4)%period==0 && n.CultistNetMode!=1)FaceSiblings(n,group,count,playerX);
                if(++n.A1>=4+period)Reset(ref n);
            }
            else if(n.A0==3)
            {
                n.L2=11;int period=e.GoodWorld?10:e.Expert?12:18,shots=e.GoodWorld?5:e.Expert?4:3;
                if(body && n.A1>=4 && (int)(n.A1-4)%period==0)
                {
                    if((int)(n.A1-4)/period==2 && n.CultistNetMode!=1)FaceSiblings(n,group,count,playerX);
                    Face(ref n,playerX-center.X);
                }
                if(++n.A1>=4+period*shots)Reset(ref n);
            }
            else if(n.A0==4)
            {
                n.L2=body?12:11;int period=e.Expert?40:80;if(e.GoodWorld)period-=20;
                if(body && n.A1==20 && n.CultistNetMode!=1)FaceSiblings(n,group,count,playerX);
                if(++n.A1>=20+period)Reset(ref n);
            }
            else if(n.A0==5)
            {
                // Ritual availability only selects L2 and sprite orientation.
                // These presentation fields are deliberately not certified.
                n.L2=10;
                if(n.A1>=0 && n.A1<30){protectedPhase=true;n.Alpha=(int)(n.A1/30f*255);}
                else if(n.A1>=30 && n.A1<90)
                {
                    if(n.A1==30 && body && n.CultistNetMode!=1)
                    {
                        if(!n.CultistRelationsComplete){stop=PredictionStop.MissingDependency;return false;}
                        Ritual(ref n,group,count,playerX,playerY);
                    }
                    protectedPhase=true;n.Alpha=255;
                }
                else if(n.A1>=90 && n.A1<120){protectedPhase=true;n.Alpha=255-(int)((n.A1-90)/30f*255);}
                else if(n.A1>=120 && n.A1<420)n.Alpha=0;
                if(++n.A1>=420)Reset(ref n);
            }
            else if(n.A0==6){n.L2=13;if(++n.A1>=120)Reset(ref n);}
            else if(n.A0==7)
            {
                n.L2=11;int period=e.Expert?30:20;
                if(body && n.A1>=4 && (int)(n.A1-4)%period==0)
                {
                    if((int)(n.A1-4)/period==2)FaceSiblings(n,group,count,playerX);
                    Face(ref n,playerX-center.X);
                }
                // Random522 birth does not enter this family's motor/count.
                if(++n.A1>=4+period*2)Reset(ref n);
            }
            else if(n.A0==8)
            {
                n.L2=13;
                if(body && n.A1>=4 && (int)(n.A1-4)%20==0)
                {
                    Face(ref n,playerX-center.X);
                    if(n.CultistNetMode!=1 && Length(e.PlayerX-center.X,e.PlayerY-center.Y)<=2000)InvalidateDooms(ref n,group,count);
                }
                // Random placement changes only subsequent global523 count.
                if(++n.A1>=64)Reset(ref n);
            }
            if(!body)n.A3=ownA3;
            n.Health.DontTakeDamage=protectedPhase;n.CanReceive=n.Active && n.Life>0 && !protectedPhase && !n.Health.Immortal;
            return true;
        }
        // Source-only finite clock projection. It never advances a foreign
        // motor, samples RNG or declares a birth successful. The result is the
        // earliest POSSIBLE attempt in this horizon. Unknown foreign geometry
        // can only make a later move/2000px test fail, not permit an earlier one.
        // Only an actual Count523 consumer uses this deadline; casts continue.
        internal static int FirstPotentialDoomBirthAction(NpcMotionState n,PredictionEnvironment e,int maximum,NpcMotionState[] group,int count)
        {return FirstClockConsumer(n,e,maximum,group,count,true);}
        internal static int FirstNecessaryDoomCountAction(NpcMotionState n,PredictionEnvironment e,int maximum,NpcMotionState[] group,int count)
        {return FirstClockConsumer(n,e,maximum,group,count,false);}
        private static int FirstClockConsumer(NpcMotionState n,PredictionEnvironment e,int maximum,NpcMotionState[] group,int count,bool birth)
        {
            if(!n.Active || n.EffectiveType!=439 || n.Style!=84 || birth && n.CultistNetMode==1 || maximum<1)return int.MaxValue;
            int halfAt=e.Expert?FirstPossibleHalfAction(n,e,maximum,group,count):int.MaxValue;
            var paths=new HashSet<ClockPath>{new ClockPath(n.L0==0 && n.CultistNetMode!=1?-1:n.A0,n.A1,n.A3)};
            for(int action=1;action<=maximum;action++)
            {
                var next=new HashSet<ClockPath>();
                foreach(var original in paths)
                {
                    var path=original;
                    if(action==1 && n.JustHit && path.Phase==5 && path.Clock>=120 && path.Clock<420){path.Phase=path.Clock=0;path.Attack++;}
                    if(path.Phase==-1)
                    {path.Clock++;if(path.Clock<420){next.Add(path);continue;}path.Phase=path.Clock=0;}
                    if(birth && path.Phase==8 && path.Clock>=4 && (int)(path.Clock-4)%20==0)return action;
                    if(path.Phase==0)
                    {
                        path.Clock++;if(path.Clock<40){next.Add(path);continue;}
                        // Upper-loss half eligibility is a lower bound on the
                        // crossing time. Retain both schedules because actual
                        // immunity/healing may postpone that crossing.
                        int alternatives=action>=halfAt?2:1;
                        for(int half=0;half<alternatives;half++)
                        {
                            var branch=path;int choice=Choice((int)branch.Attack,half!=0);
                            if((int)branch.Attack==(half!=0?13:11) || (int)branch.Attack<0 || (int)branch.Attack>(half!=0?13:11))branch.Attack=-1;
                            if(half!=0 && choice!=0 && choice!=4 && choice!=5)
                            {if(!birth)return action;next.Add(new ClockPath(8,0,branch.Attack));}
                            branch.Phase=choice==0?1:choice==1?3:choice==2?2:choice==3?4:choice==4?5:7;
                            // Future move duration ceil(distance/50) has native
                            // minimum1; AI1 is twice that duration. No static
                            // foreign center is used as a future anchor.
                            branch.Clock=choice==0?2:0;next.Add(branch);
                        }
                    }
                    else if(path.Phase==1)
                    {if(--path.Clock<=0){path.Phase=path.Clock=0;path.Attack++;}next.Add(path);}
                    else
                    {
                        int limit=path.Phase==2?4+(e.Expert?90:120)-(e.GoodWorld?30:0):path.Phase==3?4+(e.GoodWorld?10:e.Expert?12:18)*(e.GoodWorld?5:e.Expert?4:3):path.Phase==4?20+(e.Expert?40:80)-(e.GoodWorld?20:0):path.Phase==5?420:path.Phase==6?120:path.Phase==7?4+(e.Expert?30:20)*2:path.Phase==8?64:int.MaxValue;
                        if(limit==int.MaxValue)return action; // Noncanonical unknown clock producer.
                        if(++path.Clock>=limit){path.Phase=path.Clock=0;path.Attack++;}next.Add(path);
                    }
                }
                paths=next;
            }
            return int.MaxValue;
        }
        private static int FirstPossibleHalfAction(NpcMotionState n,PredictionEnvironment e,int maximum,NpcMotionState[] group,int count)
        {
            if(n.Life<=n.LifeMax/2)return 1;
            if(n.Health.Immortal)return int.MaxValue;
            var values=new List<NpcMotionState>{n};
            if(group!=null)for(int i=0;i<count;i++)if(group[i].Active && !group[i].Identity.Equals(n.Identity) && group[i].Health.RealLife==n.Identity.Slot)values.Add(group[i]);
            bool damage=false;
            foreach(var row in values)if(row.UnmodeledDamageTicks>0 || row.Health.Regen<0 && !HasDot(row.Health))return 1;
            foreach(var row in values)if(HasDot(row.Health) || row.Health.RegenCount<0)damage=true;
            if(!damage)return int.MaxValue;
            var projected=values.ToArray();Array.Sort(projected,(a,b)=>a.Identity.Slot.CompareTo(b.Identity.Slot));int owner=Find(projected,projected.Length,n.Identity);
            for(int i=0;i<projected.Length;i++)
            {
                // Future invulnerability/water can delay damage. Removing
                // those gates yields an earliest half-health possibility,
                // not a certified foreign health or death timeline. Preserve
                // actual Immortal, which AI84 never changes.
                projected[i].Health.DontTakeDamage=false;projected[i].Health.WetBuff=0;projected[i].Health.Buffs=default(NpcBuffLayout);
                if(i!=owner){projected[i].Life=projected[i].LifeMax=int.MaxValue;}
                else if(projected[i].Health.RealLife>=0 && projected[i].Health.RealLife!=n.Identity.Slot)
                    projected[i].Health.RealLife=-1; // Upper possible own loss; external shared life cannot make it earlier.
            }
            for(int action=1;action<=maximum;action++)
            {
                for(int i=0;i<projected.Length;i++)
                {
                    var row=projected[i];PredictionStop stop;bool valid=NpcHealth.Step(ref row,projected,projected.Length,e,out stop);projected[i]=row;
                    if(projected[owner].Life<=n.LifeMax/2)return action+(i>owner?1:0);
                    if(!valid)return action;
                    // The native AI tests half-health at its own slot, before
                    // higher-slot shared-life contributors can settle DOT.
                    if(i==owner && row.Life<=n.LifeMax/2)return action;
                }
            }
            return int.MaxValue;
        }
        private static bool HasDot(NpcHealthState h)
        {return h.Poison>0 || h.Fire>0 || h.Cursed>0 || h.Venom>0 || h.Frost>0 || h.Fire3>0 || h.Frost2>0 || h.Shadow>0;}
        private struct ClockPath:IEquatable<ClockPath>
        {
            internal float Phase,Clock,Attack;
            internal ClockPath(float phase,float clock,float attack){Phase=phase;Clock=clock;Attack=attack;}
            public bool Equals(ClockPath b){return Phase==b.Phase && Clock==b.Clock && Attack==b.Attack;}
            public override bool Equals(object b){return b is ClockPath && Equals((ClockPath)b);}
            public override int GetHashCode(){return Phase.GetHashCode()^Clock.GetHashCode()*397^Attack.GetHashCode()*7919;}
        }
        private static void Move(ref NpcMotionState n,NpcMotionState[] group,int count,float px,float py)
        {
            var center=Center(n.X,n.Y,n.Width,n.Height);
            float duration=(float)Math.Ceiling(Length(px-center.X,py-100-center.Y)/50f);if(duration==0)duration=1;
            var siblings=Siblings(n,group,count);int total=siblings.Length+1;
            for(int ordinal=0;ordinal<total;ordinal++)
            {
                int index=ordinal==0?-1:siblings[ordinal-1];var row=index<0?n:group[index];var old=Center(row.X,row.Y,row.Width,row.Height);
                float angle=((ordinal+(total%2==0?1:0)+1)/2)*((float)Math.PI*2)*.4f/total;if(ordinal%2==1)angle*=-1;if(total==1)angle=0;
                // RotatedBy returns float Vector2 fields before the component
                // multiply and vector add/subtract/divide field stores.
                float ox=(float)Math.Sin(angle),oy=(float)(-Math.Cos(angle));ox*=300;oy*=200;
                float dx=px+ox,dy=py+oy;dx-=old.X;dy-=old.Y;
                double inverse=1d/duration;row.Vx=(float)(dx*inverse);row.Vy=(float)(dy*inverse);row.A0=1;row.A1=duration*2;
                if(n.Identity.Slot>=row.Identity.Slot){row.X-=row.Vx;row.Y-=row.Vy;}
                if(index<0)n=row;else group[index]=row;
            }
        }
        private static void Ritual(ref NpcMotionState n,NpcMotionState[] group,int count,float px,float py)
        {
            n.L1++;var center=Center(n.X,n.Y,n.Width,n.Height);var siblings=Siblings(n,group,count);
            int births=Math.Min(2,6-siblings.Length);int total=siblings.Length+births+1;
            // Noncanonical >6-clone input keeps the original negative birth
            // arithmetic, rather than silently clamping it into a valid circle.
            int nearest=0;float nearestDistance=0;
            for(int i=0;i<total;i++)
            {var offset=Ring(i,total);float x=center.X+offset.X,y=center.Y+offset.Y;float distance=Length(x-px,y-py);if(i==0 || nearestDistance>distance){nearest=i;nearestDistance=distance;}}
            int opposite=nearest>=total/2?nearest-total/2:nearest+total/2,remaining=births;
            for(int i=0;i<total;i++)if(i!=opposite)
            {
                var offset=Ring(i,total);if(remaining--<=0)
                {int index=siblings[-remaining-1];var clone=group[index];SetCenter(ref clone,center.X+offset.X,center.Y+offset.Y);group[index]=clone;}
            }
            var own=Ring(opposite,total);SetCenter(ref n,center.X+own.X,center.Y+own.Y);
            if(births>0)
            {
                n.CultistRelationsComplete=false;
                foreach(int index in siblings){var clone=group[index];clone.CultistRelationsComplete=false;group[index]=clone;}
            }
        }
        private static void InvalidateDooms(ref NpcMotionState n,NpcMotionState[] group,int count)
        {n.CultistDoomsComplete=false;if(group!=null)for(int i=0;i<count;i++)if(group[i].CultistDoomsCaptured){var row=group[i];row.CultistDoomsComplete=false;group[i]=row;}}
        private static int[] Siblings(NpcMotionState n,NpcMotionState[] group,int count)
        {
            int length=0;if(group!=null)for(int i=0;i<count;i++)if(Sibling(n,group[i]))length++;
            var result=new int[length];int next=0;
            if(group!=null)for(int i=0;i<count;i++)if(Sibling(n,group[i]))result[next++]=i;
            for(int i=1;i<length;i++){int value=result[i],j=i;while(j>0 && group[result[j-1]].Identity.Slot>group[value].Identity.Slot){result[j]=result[j-1];j--;}result[j]=value;}
            return result;
        }
        private static bool Sibling(NpcMotionState parent,NpcMotionState row)
        {return row.Active && row.EffectiveType==440 && row.A3==parent.Identity.Slot;}
        private static void FaceSiblings(NpcMotionState n,NpcMotionState[] group,int count,float px)
        {if(group!=null)for(int i=0;i<count;i++)if(Sibling(n,group[i])){var row=group[i];Face(ref row,px-row.Bounds.CenterX);group[i]=row;}}
        private static void Face(ref NpcMotionState n,float x){int sign=Math.Sign(x);if(sign!=0)n.Direction=n.SpriteDirection=sign;}
        private static void Reset(ref NpcMotionState n){n.A0=n.A1=0;n.A3++;n.Vx=n.Vy=0;}
        private static bool Retire(ref NpcMotionState n,out PredictionStop stop){n.Active=false;n.Life=0;stop=PredictionStop.Despawn;return false;}
        private static bool Ready(NpcMotionState n,int elapsed,out PredictionStop stop)
        {stop=PredictionStop.None;if(!n.HasPlayer || !n.PlayerVelocityCaptured || n.TargetChoiceUnavailable || n.PlayerGhost && elapsed>1){stop=PredictionStop.MissingDependency;return false;}return true;}
        private static int Find(NpcMotionState[] group,int count,NpcIdentity identity)
        {if(group!=null)for(int i=0;i<count;i++)if(group[i].Identity.Equals(identity))return i;return -1;}
        private static int Choice(int attack,bool half)
        {if(attack==1 || !half && attack==7)return 1;if(half && (attack==3 || attack==7))return 5;if(attack==5 || half && attack==11)return 3;if(half?attack==9:attack==3 || attack==9)return 2;if(half?attack==13:attack==11)return 4;return 0;}
        private struct StoredVector{internal float X,Y;}
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static StoredVector Center(float x,float y,int width,int height){return new StoredVector{X=x+width/2f,Y=y+height/2f};}
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SetCenter(ref NpcMotionState n,float x,float y){n.X=x-n.Width/2f;n.Y=y-n.Height/2f;}
        private static StoredVector Ring(int i,int count)
        {double angle=(double)i*((float)Math.PI*2f)/count-(float)Math.PI/2f;return new StoredVector{X=(float)(180*Math.Cos(angle)),Y=(float)(180*Math.Sin(angle))};}
        private static float Length(float x,float y){return (float)Math.Sqrt((double)x*x+(double)y*y);}
    }
}
