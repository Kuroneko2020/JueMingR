using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // Both hand flamethrowers emit type85. Its expanding damage rectangle,
    // short receive window and wall-settling body are independent facts.
    // AI193 also serves an autonomous minion; that role is not a hand shot.
    internal static class HostFlameAttack
    {
        internal static bool Handles(int type){return type==85;}
        internal sealed class State
        {
            internal Vector2 Position,Velocity;internal float Clock;internal int Left;
            internal bool Alive,Wet,Honey,Lava,Shimmer;
            internal State(Projectile q){Position=q.position;Velocity=q.velocity;Clock=q.localAI[0];Left=q.timeLeft;Alive=q.active;Wet=q.wet;Honey=q.honeyWet;Lava=q.lavaWet;Shimmer=q.shimmerWet;}
        }
#if DEBUG
        internal static int Candidates,Steps;
        internal static void ResetCounters(){Candidates=Steps=0;}
#endif
        internal static bool Step(Projectile sample,State s,PredictionTerrain terrain)
        {
#if DEBUG
            Steps++;
#endif
            if(!s.Alive)return true;
            if(s.Position.X<=Main.leftWorld || s.Position.X+sample.width>=Main.rightWorld || s.Position.Y<=Main.topWorld || s.Position.Y+sample.height>=Main.bottomWorld){s.Alive=false;return true;}
            Vector2 wetVelocity=s.Velocity;s.Clock++;
            if(s.Clock>=72){s.Alive=false;s.Left=0;return true;}
            if(s.Clock>=60)s.Velocity*=.95f;
            Vector2 half=new Vector2(sample.width,sample.height)*.5f,center=s.Position+half;
            float wind;if(!HostProjectileEnvironment.Impulse(sample,terrain,center.X,center.Y,s.Velocity.X,out wind))return false;s.Velocity.X+=wind;
            if(sample.owner<255 && !Main.player[sample.owner].active){s.Alive=false;s.Left=0;return true;}
            if(!sample.ignoreWater)
            {
                bool wet,lava;byte kind;
                if(!terrain.ProjectileWet(center.X,center.Y,sample.width,sample.height,out wet,out kind,true) || !terrain.ProjectileLava(center.X,center.Y,sample.width,sample.height,out lava,true))return false;
                if(lava)s.Lava=true;if(wet && kind==2)s.Honey=true;if(wet && kind==3)s.Shimmer=true;
                // Original Update tests the PRECEDING wet flag after setting
                // this action's lava flag. Entering water gets one movement;
                // birth already submerged can retire on its first action.
                if(s.Wet && !s.Lava){s.Alive=false;s.Left=0;return true;}
                s.Wet=wet;if(!wet)s.Honey=s.Lava=s.Shimmer=false;
            }
            Vector2 prior=s.Velocity;bool hit=false;
            if(sample.tileCollide && !terrain.ProjectileTileMotion(sample,ref s.Position,ref s.Velocity,s.Wet,s.Honey,out wetVelocity,out hit,shimmer:s.Shimmer,nativeClip:true))return false;
            if(hit){s.Velocity=prior*.95f;s.Position-=s.Velocity;}
            s.Position+=s.Wet?wetVelocity:s.Velocity;return true;
        }
        internal static void Tail(State s){if(s.Alive && --s.Left<=0){s.Alive=false;s.Left=0;}}
        internal static Rectangle DamageBox(Projectile sample,State s)
        {var box=new Rectangle((int)s.Position.X,(int)s.Position.Y,sample.width,sample.height);int margin=(int)Utils.Remap(s.Clock,0,72,10,40);box.Inflate(margin,margin);return box;}
        internal static bool Collision(Projectile sample,State s,PredictionTerrain terrain,MotionRect target,out bool hits)
        {
            hits=false;var box=DamageBox(sample,s);var other=new Rectangle((int)target.X,(int)target.Y,(int)target.Width,(int)target.Height);
            if(!box.Intersects(other))return true;
            Vector2 center=s.Position+new Vector2(sample.width,sample.height)*.5f;bool clear;PredictionStop stop;
            if(!terrain.CanHit(new MotionRect(center.X,center.Y,0,0),new MotionRect(other.Center.X,other.Center.Y,0,0),out clear,out stop))return false;
            hits=clear;return true;
        }
        private sealed class Shape
        {
            internal Projectile Shot;internal State State;internal PredictionTerrain Terrain;internal bool Known=true;
            internal bool Test(MotionRect target){bool hit;if(!Collision(Shot,State,Terrain,target,out hit)){Known=false;return false;}return hit;}
        }
        internal static Vector2 BirthOrigin(Player player,Vector2 origin,Vector2 unit,float speed,PredictionTerrain terrain)
        {
            int facing=Vector2.Dot(Vector2.UnitX.RotatedBy(player.fullRotation),unit)>0?1:-1;
            var birth=origin+new Vector2(0,-6*facing*player.gravDir).RotatedBy(unit.ToRotation());
            var far=birth+unit*speed*5;
            if(terrain.MeleeLine(birth.X,birth.Y,far.X,far.Y))birth+=unit*speed*4;
            return birth;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            int limit=Math.Min(120,timeline.Count-age-1);if(limit<=0)return null;var sample=ContentSamples.ProjectilesByType[85];
            // There are only eighteen damaging outer actions. One seed per
            // actual receive sample covers the short future; stationary
            // targets collapse to one direction without lowering refresh.
            var directions=new Vector2[18];int count=0;
            for(int seed=0;seed<Math.Min(18,limit);seed++)
            {
                var b=timeline[age+1+seed].ProjectileReceiveBounds;var unit=(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction);bool duplicate=false;
                for(int i=0;i<count;i++)if(directions[i]==unit){duplicate=true;break;}if(duplicate)continue;directions[count++]=unit;
#if DEBUG
                Candidates++;
#endif
                var birth=BirthOrigin(player,origin,unit,ammo.Speed,terrain);
                var s=new State(sample){Position=birth-new Vector2(sample.width,sample.height)*.5f,Velocity=unit*ammo.Speed,Clock=0,Alive=true};
                bool wet;byte kind;if(!terrain.ProjectileWet(birth.X,birth.Y,sample.width,sample.height,out wet,out kind,true))return null;
                s.Wet=wet;s.Honey=wet && kind==2;s.Shimmer=wet && kind==3;s.Lava=false;
                var shape=new Shape{Shot=sample,State=s,Terrain=terrain};Func<MotionRect,bool> collision=shape.Test;obstacles.BeginCandidate();var aim=origin+unit*10000;int actions=0;
                for(int outer=1;outer<=limit && s.Alive && s.Clock<53;outer++)for(int sub=0;sub<=sample.extraUpdates && s.Alive;sub++)
                {
                    if(!Step(sample,s,terrain) || !s.Alive){s.Alive=false;break;}
                    // Damage rejects at clock54 although the visual flame can
                    // live to72. Stop the entire replay at this finite bound.
                    if(s.Clock>=54){s.Alive=false;break;}
                    actions++;int tick=age+outer;Vector2 center=s.Position+new Vector2(sample.width,sample.height)*.5f;
                    if(!obstacles.PassCustomOuter(actions,tick,delay+outer,center.X,center.Y,sample.width,sample.height,collision) || !shape.Known){s.Alive=false;break;}
                    bool hit;if(!Collision(sample,s,terrain,timeline[tick].ProjectileReceiveBounds,out hit)){s.Alive=false;break;}
                    if(hit && obstacles.SelectedAllows(delay+outer))
                    {var damage=DamageBox(sample,s);var contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,damage.X+damage.Width*.5f,damage.Y+damage.Height*.5f,damage.Width,damage.Height,AttackConfidence.Conditional);if(contact!=null)return contact;}
                    Tail(s);
                }
            }
            return null;
        }
    }
}
