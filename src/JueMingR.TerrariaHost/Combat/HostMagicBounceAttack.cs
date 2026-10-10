using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // The complete friendly AI_008 set has different platform, water-death,
    // lava-power, bounce-limit and Meowmere burst stages. Only finite scalar
    // facts are copied; no AI, Kill, Damage, particle or RNG call is a getter.
    internal static class HostMagicBounceAttack
    {
        internal static bool Handles(int type){return type==15 || type==27 || type==95 || type==253 || type==502;}
        internal struct State
        {
            internal float X,Y,Vx,Vy,Ai0,Ai1,BurstX,BurstY;internal int ExtraUpdates,Left,BurstSide;
            internal bool Alive,Wet,Lava,Honey;
            internal State(Projectile shot,Vector2 center,Vector2 velocity)
            {X=center.X;Y=center.Y;Vx=velocity.X;Vy=velocity.Y;Ai0=shot.ai[0];Ai1=shot.ai[1];ExtraUpdates=shot.extraUpdates;Left=shot.timeLeft;Alive=true;Wet=shot.wet;Lava=shot.lavaWet;Honey=shot.honeyWet;BurstX=BurstY=0;BurstSide=0;}
        }
        internal static bool Step(Projectile sample,PredictionTerrain terrain,ref State s)
        {
            s.BurstSide=0;if(!s.Alive || s.Left<=0){s.Alive=false;return true;}
            Vector2 wetVelocity=new Vector2(s.Vx,s.Vy);
            if(sample.type==27)
            {
                // Update snapshots extraUpdates before this AI. The power
                // change therefore applies only to the next outer update.
                if(s.Ai1==1)s.ExtraUpdates=5;
                if(s.Vy>0){bool lava;if(!terrain.ProjectileCenterLava(s.X,s.Y,out lava))return false;if(lava){s.Ai1=1;s.Vy=-s.Vy;}}
            }
            else s.Ai1++;
            if(s.Ai1>=20)s.Vy+=.2f;if(s.Vy>16)s.Vy=16;
            float wind;if(!HostProjectileEnvironment.Impulse(sample,terrain,s.X,s.Y,s.Vx,out wind))return false;s.Vx+=wind;
            if(!sample.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileLava(s.X,s.Y,sample.width,sample.height,out lava) || !terrain.ProjectileWet(s.X,s.Y,sample.width,sample.height,out wet,out kind) || wet && kind==3)return false;
                if(lava)s.Lava=true;if(wet && kind==2)s.Honey=true;
                // Native tests the preceding wet flag after accumulating the
                // present full-body lava collision. Fire survives lava but
                // dies on the second water/honey substep, before body Damage.
                if(s.Wet && !s.Lava && ProjectileID.Sets.IsDestroyedByWater[sample.type]){s.Alive=false;return true;}
                s.Wet=wet;if(!wet)s.Lava=s.Honey=false;
            }
            float lastX=s.Vx,lastY=s.Vy;Vector2 half=new Vector2(sample.width,sample.height)*.5f,position=new Vector2(s.X,s.Y)-half,velocity=new Vector2(s.Vx,s.Vy);bool hit=false;
            if(sample.tileCollide && !terrain.ProjectileTileMotion(sample,ref position,ref velocity,s.Wet,s.Honey,out wetVelocity,out hit,sample.type!=15 && sample.type!=253))return false;
            s.X=position.X+half.X;s.Y=position.Y+half.Y;s.Vx=velocity.X;s.Vy=velocity.Y;float moveX=wetVelocity.X,moveY=wetVelocity.Y;
            if(hit)
            {
                s.Ai0++;if(sample.type==502)s.Ai0=MathHelper.Clamp(s.Ai0,1,5);
                int maximum=sample.type==15?6:sample.type==253?8:sample.type==27 && s.Ai1==1?7:5;
                if(s.Ai0>=maximum){s.X+=s.Vx;s.Y+=s.Vy;s.Alive=false;}
                else
                {
                    if(s.Vy!=lastY)s.Vy=-lastY*(sample.type==15 && s.Vy>4?.8f:1);
                    if(s.Vx!=lastX)s.Vx=-lastX;
                }
                // Meowmere's collision Damage runs before UpdatePosition,
                // including its final Kill collision. It temporarily makes
                // penetration unlimited and shares local immunity with body.
                if(sample.type==502){s.BurstX=s.X;s.BurstY=s.Y;s.BurstSide=40+8*(int)s.Ai0;}
            }
            s.X+=s.Wet?moveX:s.Vx;s.Y+=s.Wet?moveY:s.Vy;s.Left--;return true;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay=0)
        {
            var sample=ContentSamples.ProjectilesByType[ammo.Projectile];var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);
            int limit=Math.Min(120,timeline.Count-age-1);if(limit<=0)return null;
            bool bornWet;byte bornKind;if(!terrain.ProjectileWet(origin.X,origin.Y,sample.width,sample.height,out bornWet,out bornKind) || bornWet && bornKind==3)return null;
            var motion=new AttackMotion(ammo.Speed,sample.type==27?0:.2f,20,sample.extraUpdates+1,sample.width,sample.height,sample.timeLeft,AttackConfidence.Conditional,maxFallSpeed:16);
            var ordinary=AttackIntercept.Solve(origin.X,origin.Y,motion,timeline,age,0);var directions=new Vector2[42];int count=0;
            for(int seed=-1;seed<41;seed++)
            {
                Vector2 direction;
                if(seed<0){if(ordinary==null)continue;direction=new Vector2(ordinary.AimX-origin.X,ordinary.AimY-origin.Y).SafeNormalize(Vector2.UnitX*player.direction);}
                else if(seed<9){var b=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].ProjectileReceiveBounds;direction=(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction);}
                else direction=((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
                // NewProjectile seals wet/honey from the birth sensor but
                // leaves lavaWet false until the first full-body lava query.
                var state=new State(sample,origin,direction*ammo.Speed);state.Wet=!sample.ignoreWater && bornWet;state.Honey=bornWet && bornKind==2;var aim=origin+direction*10000;obstacles.BeginCandidate();int k=0;
                for(int step=0;step<limit && state.Alive && state.Left>0;step++)
                {
                    int updates=state.ExtraUpdates+1,tick=age+step+1;
                    if(!obstacles.BeforeNpc(tick,delay+step+1,state.X,state.Y)){state.Alive=false;break;}
                    for(int sub=0;sub<updates && state.Alive && state.Left>0;sub++)
                    {
                        k++;if(!Step(sample,terrain,ref state)){state.Alive=false;break;}
                        if(state.BurstSide>0)
                        {
                            if(!obstacles.PassShapeOuter(k+1,tick,delay+step+1,state.BurstX,state.BurstY,state.BurstSide,state.BurstSide,true)){state.Alive=false;break;}
                            if(receive.Allows(delay+step+1)){var burst=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,state.BurstX,state.BurstY,state.BurstSide,state.BurstSide,AttackConfidence.Conditional);if(burst!=null)return burst;}
                        }
                        if(!state.Alive)break;
                        if(!obstacles.PassOuter(k+1,tick,delay+step+1,state.X,state.Y)){state.Alive=false;break;}
                        if(receive.Allows(delay+step+1)){var body=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,state.X,state.Y,sample.width,sample.height,AttackConfidence.Conditional);if(body!=null)return body;}
                    }
                }
            }
            return null;
        }
    }
}
