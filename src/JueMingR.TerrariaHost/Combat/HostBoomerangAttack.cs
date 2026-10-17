using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostBoomerangAttack
    {
        internal static bool Handles(int type)
        {return type==6 || type==19 || type==33 || type==52 || type==106 || type==113 || type==272 || type==320 || type==333 || type==902 || type==867 || type==1000 || type==1052 || HostBoomerangVariants.Handles(type);}
        internal static void OnOtherHits(Projectile shot,int hits)
        {
            if(hits<=0)return;
            if(HostBoomerangVariants.Handles(shot.type)){HostBoomerangVariants.OnOtherHits(shot,hits);return;}
            if(!Handles(shot.type))return;
            // Damage_PVE's common AI003 branch reverses only the first
            // outgoing receive, then starts return. Multiple qualified NPCs
            // in that same Damage rectangle do not reverse it again. 902's
            // separate earlier OnHit sets return without this reversal.
            if(shot.type!=902 && shot.ai[0]==0)shot.velocity=-shot.velocity;
            shot.ai[0]=1;
        }
        // This is the finite motor for the ordinary, explicitly admitted
        // AI_003 types. Return uses the current predicted owner, not mouse
        // input; a native catch happens before movement and damage.
        internal static bool Step(Projectile shadow,Player owner)
        {
            // Special variants require their terrain/target phase context;
            // this old motor-only oracle must not silently run common AI003.
            if(HostBoomerangVariants.Handles(shadow.type))return false;
            if(!Motor(shadow,owner))return false;
            shadow.Center+=shadow.velocity;return true;
        }
        internal static bool Advance(Projectile shadow,Player owner,PredictionTerrain terrain,out bool alive)
        {
            if(HostBoomerangVariants.Handles(shadow.type)){alive=false;return false;}
            // Projectile.Update captures this before AI. A returning boomerang
            // disables tileCollide in AI, so wet movement keeps that pre-AI
            // vector while its public velocity has already accelerated home.
            Vector2 wetVelocity=shadow.velocity;
            alive=Motor(shadow,owner);if(!alive)return true;
            float wind;if(!HostProjectileEnvironment.Impulse(shadow,terrain,shadow.Center.X,shadow.Center.Y,shadow.velocity.X,out wind))return false;shadow.velocity.X+=wind;
            if(shadow.shimmerWet)return false;
            if(shadow.ignoreWater)shadow.wet=false;
            else
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(shadow.Center.X,shadow.Center.Y,shadow.width,shadow.height,out wet,out kind,true) || wet && kind==3 || !terrain.ProjectileLava(shadow.Center.X,shadow.Center.Y,shadow.width,shadow.height,out lava,true))return false;
                if(lava)shadow.lavaWet=true;if(wet && kind==2)shadow.honeyWet=true;shadow.wet=wet;if(!wet)shadow.lavaWet=shadow.honeyWet=false;
            }
            // The collision branch replaces the saved vector with its wet
            // displacement; skipping collision must preserve the saved value.
            Vector2 position=shadow.position,velocity=shadow.velocity,last=velocity;bool hit=false;
            if(shadow.tileCollide)
            {
                if(!terrain.ProjectileTileMotion(shadow,ref position,ref velocity,shadow.wet,shadow.honeyWet,out wetVelocity,out hit,nativeClip:true))return false;
                // AI003 is exempt from Update's pre-AI world retirement. Its
                // tile movement raises the same collision flag at a body edge
                // even when TileCollision clipped neither velocity axis. The
                // native role response below still decides return/reflection.
                if(position.X<=Main.leftWorld || position.X+shadow.width>=Main.rightWorld || position.Y<=Main.topWorld || position.Y+shadow.height>=Main.bottomWorld)hit=true;
            }
            if(hit)
            {
                // LightDisc/ThornChakram keep their outbound AI and reflect
                // only clipped axes. Ordinary boomerangs mark return here,
                // reflect both axes, and disable tiles on their NEXT AI.
                if(shadow.type==33 || shadow.type==106)
                {if(velocity.X!=last.X)velocity.X=-last.X;if(velocity.Y!=last.Y)velocity.Y=-last.Y;}
                else {shadow.ai[0]=1;velocity=-last;}
            }
            shadow.velocity=velocity;shadow.position=position+(shadow.wet?wetVelocity:velocity);return true;
        }
        private static bool Motor(Projectile shadow,Player owner)
        {
            if(shadow.ai[0]==0)
            {
                shadow.ai[1]++;
                // Native type106 has an earlier >=45 check, but also reaches the
                // common >=30 branch below it; that first return wins.
                if(shadow.type==320)
                {
                    // BloodyMachete is not the common 30-step return. It
                    // falls after ten AI steps until native terrain starts
                    // its return; a clear airborne continuation stays out.
                    if(shadow.ai[1]>=10){shadow.velocity.Y+=.5f;shadow.velocity.X*=.95f;if(shadow.velocity.Y>16)shadow.velocity.Y=16;}
                }
                else if(shadow.ai[1]>=30){shadow.ai[0]=1;shadow.ai[1]=0;}
            }
            else
            {
                shadow.tileCollide=false;
                float speed=9,acceleration=.4f;
                if(shadow.type==1000)speed=9.5f;
                if(shadow.type==19 || shadow.type==272){speed=20;acceleration=1.5f;}
                else if(shadow.type==333){speed=12;acceleration=.6f;}
                else if(shadow.type==33){speed=18;acceleration=1.2f;}
                else if(shadow.type==106){speed=16;acceleration=1.2f;}
                else if(shadow.type==320){speed=15;acceleration=3;}
                float factor=Math.Max(1,Math.Min(10,1+1.5f*(1/owner.meleeSpeed-1)));speed*=factor;acceleration*=factor;
                Vector2 delta=owner.position+new Vector2(owner.width/2,owner.height/2)-shadow.Center;
                float distance=delta.Length();if(distance>3000 || distance==0)return false;
                delta*=speed/distance;
                shadow.velocity.X=Approach(shadow.velocity.X,delta.X,acceleration);
                shadow.velocity.Y=Approach(shadow.velocity.Y,delta.Y,acceleration);
                if(shadow.owner==Main.myPlayer && new Rectangle((int)shadow.position.X,(int)shadow.position.Y,shadow.width,shadow.height).Intersects(new Rectangle((int)owner.position.X,(int)owner.position.Y,owner.width,owner.height)))return false;
            }
            return true;
        }
        private static float Approach(float value,float desired,float acceleration)
        {
            if(value<desired){value+=acceleration;if(value<0 && desired>0)value+=acceleration;}
            else if(value>desired){value-=acceleration;if(value>0 && desired<0)value-=acceleration;}
            return value;
        }
    }
}
