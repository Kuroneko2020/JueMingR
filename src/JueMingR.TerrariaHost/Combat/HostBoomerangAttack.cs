using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostBoomerangAttack
    {
        // This is the finite motor for the ordinary, explicitly admitted
        // AI_003 types. Return uses the current predicted owner, not mouse
        // input; a native catch happens before movement and damage.
        internal static bool Step(Projectile shadow,Player owner)
        {
            if(shadow.ai[0]==0)
            {
                shadow.ai[1]++;
                if(shadow.ai[1]>=(shadow.type==106?45:30))shadow.ai[0]=1;
            }
            else
            {
                shadow.tileCollide=false;
                float speed=9,acceleration=.4f;
                if(shadow.type==19){speed=20;acceleration=1.5f;}
                else if(shadow.type==33){speed=18;acceleration=1.2f;}
                else if(shadow.type==106){speed=16;acceleration=1.2f;}
                float factor=Math.Max(1,Math.Min(10,1+1.5f*(1/owner.meleeSpeed-1)));speed*=factor;acceleration*=factor;
                Vector2 delta=owner.position+new Vector2(owner.width/2,owner.height/2)-shadow.Center;
                float distance=delta.Length();if(distance>3000 || distance==0)return false;
                delta*=speed/distance;
                shadow.velocity.X=Approach(shadow.velocity.X,delta.X,acceleration);
                shadow.velocity.Y=Approach(shadow.velocity.Y,delta.Y,acceleration);
                if(new Rectangle((int)shadow.position.X,(int)shadow.position.Y,shadow.width,shadow.height).Intersects(new Rectangle((int)owner.position.X,(int)owner.position.Y,owner.width,owner.height)))return false;
            }
            shadow.Center+=shadow.velocity;return true;
        }
        private static float Approach(float value,float desired,float acceleration)
        {
            if(value<desired){value+=acceleration;if(value<0 && desired>0)value+=acceleration;}
            else if(value>desired){value-=acceleration;if(value>0 && desired<0)value-=acceleration;}
            return value;
        }
    }
}
