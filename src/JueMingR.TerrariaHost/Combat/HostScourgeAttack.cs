using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Native 306 Kill creates an independently owned AI036 role. Its unborn
    // random count/velocity are representatives; captured children use the
    // same deterministic motor and are only conditional on other NPC futures.
    internal static class HostScourgeAttack
    {
        internal static bool Handles(int type){return type==306;}
#if DEBUG
        internal static int Candidates,MotorSteps,TargetQueries;
        internal static int FailedSlot,FailedTick,FailedOuter;internal static Vector2 FailedPosition;internal static string FailedStage;
        internal static void ResetCounters(){Candidates=MotorSteps=TargetQueries=0;}
#endif
        internal struct State
        {
            internal Vector2 Position,Velocity,DeathPosition;private readonly Vector2 half;
            internal Vector2 Center {get{return Position+half;}set{Position=value-half;}}
            internal float Counter,Bounces;internal int Left,Alpha,Extra,TargetSlot;
            internal bool Alive,Friendly,Wet,Honey,Lava,Shimmer,StoppedAfterMovement,Killed;
            internal State(Projectile p)
            {half=new Vector2(p.width,p.height)*.5f;Position=p.position;Velocity=p.velocity;DeathPosition=Vector2.Zero;Counter=p.ai[0];Bounces=p.ai[1];Left=p.timeLeft;Alpha=p.alpha;Extra=p.extraUpdates;TargetSlot=-1;Alive=p.active;Friendly=p.friendly;Wet=p.wet;Honey=p.honeyWet;Lava=p.lavaWet;Shimmer=p.shimmerWet;StoppedAfterMovement=Killed=false;}
        }
        private sealed class Member
        {
            internal Projectile Shot;internal State State;internal HostAttackObstacles Receivers;
            internal int Slot;internal bool Representative;
        }
        private static void Kill(Projectile sample,ref State state)
        {
            if(!state.Alive)return;
            state.DeathPosition=state.Position;
            state.Alive=false;state.Left=0;state.Killed=true;
        }
        private static float Approach(float value,float desired)
        {
            if(value<desired){value+=.35f;if(value<0 && desired>0)value+=.35f*2;}
            else if(value>desired){value-=.35f;if(value>0 && desired<0)value-=.35f*2;}
            return value;
        }
        internal static bool Step(Projectile sample,HostProjectileTargets targets,NpcTrajectory timeline,int tick,PredictionTerrain terrain,ref State state)
        {
#if DEBUG
            MotorSteps++;
#endif
            state.StoppedAfterMovement=false;
            // Both the AI002 parent and AI036 children use Update's pre-AI
            // bounds retirement (AI003 is exempt). It clears active only:
            // no Kill/Left reset, children from Kill, or another tail.
            if(state.Position.X<=Main.leftWorld || state.Position.X+sample.width>=Main.rightWorld || state.Position.Y<=Main.topWorld || state.Position.Y+sample.height>=Main.bottomWorld || float.IsNaN(state.Position.X) || float.IsNaN(state.Position.Y))
            {state.Alive=false;state.StoppedAfterMovement=true;return true;}
            // Native wetVelocity is sampled before AI, not after steering.
            Vector2 wetVelocity=state.Velocity,half=new Vector2(sample.width,sample.height)*.5f;
            if(state.Alive)state.TargetSlot=-1;
            if(state.Alive && sample.type==307)
            {
                if(state.Alpha>0)state.Alpha-=50;else state.Extra=0;
                if(state.Alpha<0)state.Alpha=0;
                state.Counter++;Vector2 point=state.Center+state.Velocity*100f;
                if(state.Counter>30)
                {
                    state.Counter=30;float best=100000;
                    for(int i=0;i<targets.Count;i++)
                    {
#if DEBUG
                        TargetQueries++;
#endif
                        var box=targets.Bounds(i,timeline,tick);
                        // The native candidate center uses integer half-size.
                        var center=new Vector2(box.X+(int)box.Width/2,box.Y+(int)box.Height/2);
                        float distance=Math.Abs(state.Center.X-center.X)+Math.Abs(state.Center.Y-center.Y);
                        if(distance>=800 || distance>=best)continue;
                        bool clear;PredictionStop stop;
                        if(!terrain.CanHit(new MotionRect(state.Center.X-half.X,state.Center.Y-half.Y,sample.width,sample.height),box,out clear,out stop))return false;
                        if(!clear)continue;
                        best=distance;point=center;state.TargetSlot=targets.Slot(i);
                    }
                    if(state.TargetSlot>=0)state.Friendly=true;
                }
                // Preserve native zero-vector NaN comparisons: they leave a
                // stationary unacquired child stationary, never UnitX.
                Vector2 desired=point-state.Center;float length=(float)Math.Sqrt(desired.X*desired.X+desired.Y*desired.Y);
                float scale=13f/length;desired.X*=scale;desired.Y*=scale;
                state.Velocity.X=Approach(state.Velocity.X,desired.X);state.Velocity.Y=Approach(state.Velocity.Y,desired.Y);
            }
            else if(state.Alive)
            {
                if(Main.windPhysics)state.Velocity.X+=Main.windSpeedCurrent*Main.windPhysicsStrength;
                state.Alpha=Math.Max(0,state.Alpha-50);
                if(state.Velocity.Y>32)state.Velocity.Y=32;
            }
            float wind;if(!HostProjectileEnvironment.Impulse(sample,terrain,state.Center.X,state.Center.Y,state.Velocity.X,out wind))return false;
            state.Velocity.X+=wind;
            if(sample.owner<255 && !Main.player[sample.owner].active)Kill(sample,ref state);
            if(!sample.ignoreWater)
            {
                bool wet,lava;byte kind;
                if(!terrain.ProjectileWet(state.Center.X,state.Center.Y,sample.width,sample.height,out wet,out kind,true) || !terrain.ProjectileLava(state.Center.X,state.Center.Y,sample.width,sample.height,out lava,true))return false;
                if(lava)state.Lava=true;if(wet && kind==2)state.Honey=true;if(wet && kind==3)state.Shimmer=true;
                state.Wet=wet;if(!wet){state.Honey=false;state.Lava=false;state.Shimmer=false;}
            }
            if(sample.type==307 && state.Shimmer)
            {
                int x=(int)(state.Center.X/16),y=(int)((state.Center.Y-half.Y)/16);
                // This call uses WorldGen.InWorld's default fluff=0.
                bool fullColumn=false;
                if(x>=0 && y>=0 && x<Main.maxTilesX && y<Main.maxTilesY)
                {
                    PredictionTile tile,above;PredictionStop stop;
                    if(!terrain.Tile(x,y,out tile,out stop))return false;
                    if(tile.Liquid==255 && tile.ShimmerLiquid && y-1>=0)
                    {if(!terrain.Tile(x,y-1,out above,out stop))return false;if(above.Liquid>0 && above.ShimmerLiquid){fullColumn=true;Kill(sample,ref state);}}
                }
                if(!fullColumn && state.Velocity.Y>0){state.Velocity.Y*=-1;state.Shimmer=false;state.Wet=false;}
            }
            Vector2 position=state.Position,velocity=state.Velocity,original=velocity;bool hit=false;
            if(sample.tileCollide)
            {
                // R15 shared-helper seam: optional shimmer comes after the
                // existing fallThrough argument. Native gives shimmer priority
                // over sticky honey, before clipped-axis replacement/slopes.
                if(!terrain.ProjectileTileMotion(sample,ref position,ref velocity,state.Wet,state.Honey,out wetVelocity,out hit,true,state.Shimmer,true))return false;
            }
            state.Position=position;
            if(hit)
            {
                if(sample.type==307 && state.Bounces<5)
                {state.Bounces++;if(velocity.X!=original.X)velocity.X=-original.X;if(velocity.Y!=original.Y)velocity.Y=-original.Y;}
                else
                {state.Position+=velocity;Kill(sample,ref state);}
            }
            state.Velocity=velocity;
            // Even a tile/shimmer Kill can be followed by UpdatePosition.
            // Children nevertheless spawn at the saved Kill-entry position.
            state.Position+=state.Wet?wetVelocity:velocity;
            // Update returns immediately after HandleMovement if inactive.
            // A Damage/tail Kill instead reaches this gate on the next
            // remaining subupdate; neither path runs another Damage or tail.
            state.StoppedAfterMovement=!state.Alive;
            return true;
        }
        internal static void Tail(Projectile sample,ref State state)
        {if(state.StoppedAfterMovement)return;state.Left--;if(state.Left<=0)Kill(sample,ref state);}
        private static Projectile PrivateShot(int type,int owner,int damage,float knockBack)
        {
            var p=new Projectile();p.SetDefaults(type);p.owner=owner;p.damage=damage;p.knockBack=knockBack;p.active=true;
            return p;
        }
        private static bool BirthChildren(Member parent,List<Member> members,HostProjectileBirthSlots slots,PredictionTerrain terrain)
        {
            if(parent.Shot.owner!=Main.myPlayer)return true;
            // Four legal RNG outcomes form a bounded representative fan. This
            // does not assert the actual count (2..4, rare 15) or call RNG.
            foreach(var v in new[]{new Vector2(-7,0),new Vector2(7,0),new Vector2(0,-7),new Vector2(0,7)})
            {
                int slot;if(!slots.TryAllocate(out slot))return false;
                var child=PrivateShot(307,Main.myPlayer,(int)(parent.Shot.damage*.75),(int)(parent.Shot.knockBack*.35));
                child.Center=parent.State.DeathPosition;child.velocity=v;
                bool wet;byte kind;if(!terrain.ProjectileWet(child.Center.X,child.Center.Y,child.width,child.height,out wet,out kind))return false;
                child.wet=wet;child.honeyWet=wet && kind==2;child.shimmerWet=wet && kind==3;
                var state=new State(child);
                // Native 307 starts friendly=false. Receiver capture uses a
                // private future-friendly sample, while State alone controls
                // whether this substep actually executes Damage.
                child.friendly=true;
                members.Add(new Member{Shot=child,State=state,Slot=slot,Receivers=parent.Receivers.ForkForSpawn(child),Representative=true});
            }
            return true;
        }
        private static bool Outer(List<Member> members,HostProjectileTargets targets,HostProjectileBirthSlots slots,NpcTrajectory timeline,PredictionTerrain terrain,int tick,int outer,Vector2 aim,out AttackContact contact)
        {
            contact=null;int after=-1;
            // Native loops slots once. A higher-slot child runs this outer;
            // a lower-slot child waits until the next outer. Parent remains
            // occupied until all NewProjectile calls from its Kill finish.
            for(int visit=0;visit<1000;visit++)
            {
                Member current=null;foreach(var member in members)if(member.State.Alive && member.Slot>after && (current==null || member.Slot<current.Slot))current=member;
                if(current==null)return true;after=current.Slot;int updates=current.State.Extra+1;
                for(int sub=0;sub<updates;sub++)
                {
                    bool wasAlive=current.State.Alive;
                    if(!Step(current.Shot,targets,timeline,tick,terrain,ref current.State))
                    {
#if DEBUG
                        FailedSlot=current.Slot;FailedTick=tick;FailedOuter=outer;FailedPosition=current.State.Position;FailedStage="motor dependency";
#endif
                        return false;
                    }
                    if(current.State.Alive && current.State.Friendly && current.Shot.damage>0)
                    {
                        if(!current.Receivers.PassOuter(2,tick,outer,current.State.Center.X,current.State.Center.Y))
                        {if(current.Receivers.Remaining>0)
                            {
#if DEBUG
                                FailedSlot=current.Slot;FailedTick=tick;FailedOuter=outer;FailedPosition=current.State.Position;FailedStage="receiver dependency";
#endif
                                return false;
                            }
                            Kill(current.Shot,ref current.State);}
                        else if(current.Receivers.SelectedAllows(outer))
                        {
                            contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,current.State.Center.X,current.State.Center.Y,current.Shot.width,current.Shot.height,current.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);
                            if(contact!=null)return true;
                        }
                    }
                    Tail(current.Shot,ref current.State);
                    if(wasAlive && !current.State.Alive)
                    {if(current.Shot.type==306 && current.State.Killed && !BirthChildren(current,members,slots,terrain))return false;slots.Release(current.Slot);}
                    if(current.State.StoppedAfterMovement)break;
                }
            }
            return false;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            var sample=PrivateShot(306,player.whoAmI,player.GetWeaponDamage(player.HeldItem),player.HeldItem.knockBack);
            var childPolicy=PrivateShot(307,player.whoAmI,Math.Max(1,(int)(sample.damage*.75)),0);childPolicy.friendly=true;
            var targets=obstacles.CaptureChaseTargets(childPolicy);var capturedSlots=obstacles.CaptureBirthSlots();
            int limit=Math.Min(120,timeline.Count-age-1);if(limit<=0 || sample.damage<=0)return null;
            var directions=new Vector2[41];int count=0;
            for(int seed=0;seed<41;seed++)
            {
                Vector2 direction;
                if(seed<9){var b=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].ProjectileReceiveBounds;direction=(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction);}
                else direction=((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
#if DEBUG
                Candidates++;
#endif
                obstacles.BeginCandidate();var slots=capturedSlots.Copy();int slot;if(!slots.TryAllocate(out slot))return null;
                sample.Center=origin;sample.velocity=direction*ammo.Speed;var members=new List<Member>(16){new Member{Shot=sample,State=new State(sample),Slot=slot,Receivers=obstacles}};
                Vector2 aim=origin+direction*10000;
                for(int step=1;step<=limit;step++)
                {AttackContact contact;if(!Outer(members,targets,slots,timeline,terrain,age+step,delay+step,aim,out contact))break;if(contact!=null)return contact;bool alive=false;foreach(var m in members)if(m.State.Alive){alive=true;break;}if(!alive)break;}
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] children,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
#if DEBUG
            FailedSlot=-1;FailedTick=FailedOuter=0;FailedPosition=Vector2.Zero;FailedStage=null;
#endif
            if(children==null || children.Length==0 || children.Length>15 || clock.LaunchDelay!=0)return null;
            var policy=PrivateShot(307,player.whoAmI,1,0);policy.friendly=true;
            var targets=obstacles.CaptureChaseTargets(policy);var slots=obstacles.CaptureBirthSlots().Copy();obstacles.BeginCandidate();var members=new List<Member>(children.Length);
            foreach(var p in children)
            {
                if(p==null || !p.active || p.type!=307 || p.owner!=player.whoAmI)return null;
                var copy=PrivateShot(307,p.owner,p.damage,p.knockBack);copy.friendly=true;
                copy.width=p.width;copy.height=p.height;copy.tileCollide=p.tileCollide;copy.ignoreWater=p.ignoreWater;copy.correctSlopeCollision=p.correctSlopeCollision;copy.penetrate=p.penetrate;copy.maxPenetrate=p.maxPenetrate;
                members.Add(new Member{Shot=copy,State=new State(p),Slot=p.whoAmI,Receivers=obstacles.ForkForSpawn(copy)});
            }
            int limit=Math.Min(120,timeline.Count-clock.FirstTick);Vector2 aim=children[0].Center+children[0].velocity*1000;
            for(int step=0;step<limit;step++)
            {AttackContact contact;if(!Outer(members,targets,slots,timeline,terrain,clock.FirstTick+step,step+1,aim,out contact))return null;if(contact!=null)return contact;}
            return null;
        }
    }
}
