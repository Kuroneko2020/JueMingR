using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostBubbleAttack
    {
        // AI78 then original dry/ignoreWater movement, including wall/slope
        // reflection before UpdatePosition. A collision changes the sealed
        // angle; it does not kill this parent or spawn its eventual bullet.
        internal static bool Advance(Projectile bubble,PredictionTerrain terrain,ref Vector2 center,ref Vector2 velocity,ref float angle)
        {
            double delta=angle.ToRotationVector2().ToRotation()-velocity.ToRotation();
            if(delta>Math.PI)delta-=Math.PI*2;if(delta< -Math.PI)delta+=Math.PI*2;
            velocity=velocity.RotatedBy(delta*.05000000074505806)*.96f;
            if(!HostProjectileEnvironment.Dry(bubble,terrain,center.X,center.Y,velocity.X))return false;
            Vector2 previous=velocity;float x=center.X,y=center.Y,vx=velocity.X,vy=velocity.Y;
            if(bubble.tileCollide)
            {
                if(!terrain.ProjectileCollision(x,y,vx,vy,bubble.width,bubble.height,out vx,out vy) ||
                    !terrain.ProjectileSlope(ref x,ref y,ref vx,ref vy,bubble.width,bubble.height))return false;
                if(x!=center.X || y!=center.Y || vx!=previous.X || vy!=previous.Y)
                {if(vx!=previous.X)vx=-previous.X;if(vy!=previous.Y)vy=-previous.Y;angle=new Vector2(vx,vy).ToRotation();}
            }
            velocity=new Vector2(vx,vy);center=new Vector2(x,y)+velocity;return true;
        }
    }
}
