using System;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Explicit AI003 variants keep their separate clocks and damage phases.
    // Phaseblades' input-dependent and dynamic-subupdate stages remain separate.
    internal static class HostBoomerangVariants
    {
        internal static bool Handles(int type){return type==182 || type==383 || type==301 || type==866;}
        private static float Approach(float value,float desired,float acceleration,float crossing)
        {
            if(value<desired){value+=acceleration;if(value<0 && desired>0)value+=crossing;}
            else if(value>desired){value-=acceleration;if(value>0 && desired<0)value-=crossing;}
            return value;
        }
        internal static bool Advance(Projectile shot,Player owner,PredictionTerrain terrain,HostProjectileTargets targets,NpcTrajectory timeline,int tick,out bool alive,HostAttackObstacles receivers=null,int outer=0)
        {
            alive=true;Vector2 wetVelocity=shot.velocity;
            if(shot.ai[0]==0)
            {
                if(shot.type!=866)shot.ai[1]++;
                if(shot.type==866)
                {
                    if(shot.owner==Main.myPlayer && shot.damage>0)
                    {
                        int slot=-1;
                        if(shot.localAI[0]>=10 && shot.localAI[0]<=360 && !FindTarget(shot,terrain,targets,timeline,tick,receivers,outer,out slot))return false;
                        shot.ai[1]=slot;
                    }
                    shot.localAI[0]++;
                    Vector2 point;bool found=TargetCenter(targets,timeline,tick,(int)shot.ai[1],out point);
                    if(!found)
                    {
                        point=shot.Center+shot.velocity*100;
                        if(shot.localAI[0]>=(shot.owner==Main.myPlayer?30:60)){shot.ai[0]=1;shot.ai[1]=0;}
                    }
                    var desired=point-shot.Center;float length=(float)Math.Sqrt(desired.X*desired.X+desired.Y*desired.Y);float scale=12f/length;desired.X*=scale;desired.Y*=scale;
                    shot.velocity.X=Approach(shot.velocity.X,desired.X,.25f,.5f);shot.velocity.Y=Approach(shot.velocity.Y,desired.Y,.25f,.5f);
                }
                else if(shot.type==301)
                {
                    shot.scale=1.75f;
                    if(owner.active && !owner.dead)
                    {
                        // Native reanchors on every subupdate, then the
                        // shared movement adds velocity again. It is not a
                        // fixed launch position or an animation-only pose.
                        if(shot.ai[0]!=-1000 && shot.ai[1]<30)shot.Center=owner.MountedCenter-shot.velocity;
                        if(shot.ai[1]>=60){shot.ai[0]=1;shot.ai[1]=0;shot.velocity=Vector2.Zero;}
                    }
                }
                else if(shot.type==383)
                {
                    if(shot.ai[1]>=10)
                    {
                        shot.velocity.Y+=.5f;if(shot.velocity.Y<0)shot.velocity.Y+=.35f;shot.velocity.X*=.95f;if(shot.velocity.Y>16)shot.velocity.Y=16;
                        if(Vector2.Distance(shot.Center,owner.Center)>800)shot.ai[0]=1;
                    }
                }
                else
                {
                    Vector2 point=shot.Center+shot.velocity*100;bool found=false;float best=800;
                    if(shot.ai[1]>10 && shot.ai[1]<360)
                    {
                        if(targets==null)return false;
                        for(int i=0;i<targets.Count;i++)
                        {
                            var box=targets.Bounds(i,timeline,tick);var candidate=new Vector2(box.CenterX,box.CenterY);float distance=Vector2.Distance(shot.Center,candidate);
                            if(distance>=best)continue;bool clear;PredictionStop stop;
                            if(!terrain.CanHit(new MotionRect(shot.position.X+shot.width/2,shot.position.Y+shot.height/2,1,1),box,out clear,out stop))return false;
                            if(!clear)continue;best=distance;point=new Vector2(box.X+(int)box.Width/2,box.Y+(int)box.Height/2);found=true;
                        }
                    }
                    if(!found && shot.ai[1]>=30){shot.ai[0]=1;shot.ai[1]=0;}
                    var desired=point-shot.Center;float length=(float)Math.Sqrt(desired.X*desired.X+desired.Y*desired.Y);float scale=12f/length;desired.X*=scale;desired.Y*=scale;
                    shot.velocity.X=Approach(shot.velocity.X,desired.X,.25f,.5f);shot.velocity.Y=Approach(shot.velocity.Y,desired.Y,.25f,.5f);
                }
            }
            else
            {
                shot.tileCollide=false;float speed=shot.type==301?15:16,acceleration=shot.type==301?3:shot.type==383?4:1.2f;
                float factor=Math.Max(1,Math.Min(10,1+1.5f*(1/owner.meleeSpeed-1)));speed*=factor;acceleration*=factor;
                Vector2 delta=owner.position+new Vector2(owner.width/2,owner.height/2)-shot.Center;float distance=delta.Length();
                if(distance>(shot.type==301?6000:3000) || distance==0){alive=false;return true;}delta*=speed/distance;
                if(shot.type==383)
                {var difference=delta-shot.velocity;if(difference!=Vector2.Zero){var normal=difference;normal.Normalize();shot.velocity+=normal*Math.Min(acceleration,difference.Length());}}
                else
                {shot.velocity.X=Approach(shot.velocity.X,delta.X,acceleration,acceleration);shot.velocity.Y=Approach(shot.velocity.Y,delta.Y,acceleration,acceleration);}
                if(shot.owner==Main.myPlayer && shot.Hitbox.Intersects(owner.Hitbox)){alive=false;return true;}
            }
            float wind;if(!HostProjectileEnvironment.Impulse(shot,terrain,shot.Center.X,shot.Center.Y,shot.velocity.X,out wind))return false;shot.velocity.X+=wind;
            if(!shot.ignoreWater)
            {
                bool wet,lava;byte kind;
                if(!terrain.ProjectileWet(shot.Center.X,shot.Center.Y,shot.width,shot.height,out wet,out kind) || !terrain.ProjectileLava(shot.Center.X,shot.Center.Y,shot.width,shot.height,out lava))return false;
                if(lava)shot.lavaWet=true;if(wet && kind==2)shot.honeyWet=true;if(wet && kind==3)shot.shimmerWet=true;shot.wet=wet;
                if(!wet)shot.honeyWet=shot.lavaWet=shot.shimmerWet=false;
            }
            Vector2 position=shot.position,velocity=shot.velocity,last=velocity;bool hit=false;
            if(shot.tileCollide && !terrain.ProjectileTileMotion(shot,ref position,ref velocity,shot.wet,shot.honeyWet,out wetVelocity,out hit,true,shot.shimmerWet))return false;
            if(hit)
            {
                if(shot.type==866)
                {if(velocity.X!=last.X)velocity.X=-last.X;if(velocity.Y!=last.Y)velocity.Y=-last.Y;}
                else if(shot.type==383){shot.ai[0]=1;if(shot.ai[1]>=10)shot.localAI[0]=300;}
                else if(shot.type==301){shot.ai[0]=1;shot.localAI[0]=200;velocity=-last;}
                else {shot.ai[0]=1;velocity=-last;}
            }
            shot.velocity=velocity;shot.position=position+(shot.wet?wetVelocity:velocity);return true;
        }
        internal static Rectangle DamageBox(Projectile shot)
        {
            var box=shot.Hitbox;
            if((shot.type==383 || shot.type==301) && shot.localAI[0]>0)
            {
                // Native Damage_GetHitbox consumes this impulse even if no
                // NPC overlaps. It is a one-call damage stage, not body growth.
                box.Inflate(-shot.width/2,-shot.height/2);box.Inflate((int)(shot.localAI[0]/2),(int)shot.localAI[0]/2);shot.localAI[0]=-1;
            }
            // 301's Colliding applies this after Damage_GetHitbox, including
            // its one-call 200 impulse. Other receivers and selected must
            // see the same effective native shape (82 normally, 244 burst).
            if(shot.type==301)box.Inflate(22,22);
            return box;
        }
        private static bool TargetCenter(HostProjectileTargets targets,NpcTrajectory timeline,int tick,int slot,out Vector2 center)
        {
            if(targets!=null)for(int i=0;i<targets.Count;i++)if(targets.Slot(i)==slot)
            {var box=targets.Bounds(i,timeline,tick);center=new Vector2(box.CenterX,box.CenterY);return true;}
            center=Vector2.Zero;return false;
        }
        private static bool FindTarget(Projectile shot,PredictionTerrain terrain,HostProjectileTargets targets,NpcTrajectory timeline,int tick,HostAttackObstacles receivers,int outer,out int slot)
        {
            slot=-1;if(targets==null || receivers==null)return false;float best=800;
            for(int i=0;i<targets.Count;i++)
            {
                int candidate=targets.Slot(i);if(!receivers.LocalTargetAllowed(candidate,outer))continue;
                var box=targets.Bounds(i,timeline,tick);float distance=Vector2.Distance(shot.Center,new Vector2(box.CenterX,box.CenterY));if(distance>=best)continue;
                bool clear;PredictionStop stop;if(!terrain.CanHit(new MotionRect(shot.position.X,shot.position.Y,shot.width,shot.height),box,out clear,out stop))return false;
                if(clear){best=distance;slot=candidate;}
            }
            return true;
        }
        internal static bool OnRetargetHit(Projectile shot,int slot,int penetration,PredictionTerrain terrain,HostProjectileTargets targets,NpcTrajectory timeline,int tick,HostAttackObstacles receivers,int outer)
        {
            if(shot.type!=866)return true;
            // Native Damage reduces stored damage before its specialized
            // OnHit retarget/penetration branch. Keep double multiplication
            // and integer truncation at every qualified receive (50->40->32
            // ->25->20); an immune body never runs this mutation.
            shot.damage=(int)((double)shot.damage*.8);
            shot.penetrate=penetration-1;
            if(shot.penetrate==0){shot.penetrate=1;shot.damage=0;shot.ai[1]=-1;return true;}
            if(shot.owner==Main.myPlayer)
            {
                int next;if(!FindTarget(shot,terrain,targets,timeline,tick,receivers,outer,out next))return false;shot.ai[1]=next;
                Vector2 center;if(next>=0 && TargetCenter(targets,timeline,tick,next,out center))shot.velocity=shot.velocity.Length()*Vector2.Normalize(center-shot.Center);
            }
            return true;
        }
        internal static void OnOtherHits(Projectile shot,int hits)
        {
            // 383's earlier own OnHit branch skips the common AI003 return.
            // 182 reverses only on the first qualified outgoing receive.
            if(shot.type!=182 || hits==0)return;if(shot.ai[0]==0)shot.velocity=-shot.velocity;shot.ai[0]=1;
        }
    }
}
