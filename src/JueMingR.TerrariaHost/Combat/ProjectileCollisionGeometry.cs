using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.Combat
{
    // Shape extraction reads the state at the natural damage call. It never
    // invokes Damage, AI, Colliding or the stateful Damage_GetHitbox getter.
    internal static class ProjectileCollisionGeometry
    {
        internal static bool TryCapture(Projectile shot,Rectangle hitbox,int category,CombatShapeSample sample)
        {
            int type=shot.type;Vector2 center=shot.Center;float scale=shot.scale;
            if(shot.aiStyle==15)
            {
                if(shot.ai[0]==0 && shot.owner>=0 && shot.owner<Main.maxPlayers)
                {var player=Main.player[shot.owner];sample.Curve(player.MountedCenter,55,new Vector2(.8f,player.gravDir>0?.4f:.8f),2,category);}
                else sample.Rectangle(hitbox,category);
                return true;
            }
            if(type==121 || type==122 || type==123 || type==124 || type==125 || type==126 || type==597)
            {
                var features=new Projectile.GemStaffFeatures(shot.ai[0]);
                if(features.FastThenSlow)for(int i=1;i<=5 && i<shot.oldPos.Length && shot.oldPos[i]!=Vector2.Zero;i++)
                {var box=hitbox;box.X=(int)shot.oldPos[i].X;box.Y=(int)shot.oldPos[i].Y;if(features.BiggerHitbox)box.Inflate(30,30);sample.Rectangle(box,category);sample.Condition(2,Vector2.Zero);}
                if(features.BiggerHitbox)hitbox.Inflate(30,30);sample.Rectangle(hitbox,category);sample.Condition(1,center);return true;
            }
            if(type==85 || type==1106){sample.Rectangle(hitbox,category);sample.Condition(1,center);return true;}
            if(type==965){hitbox.Offset((shot.rotation.ToRotationVector2()*(-30*shot.spriteDirection)).ToPoint());sample.Rectangle(hitbox,category);return true;}
            if(type==698)
            {
                hitbox.Inflate(70,0);hitbox.X+=(int)shot.velocity.X*30;sample.Rectangle(hitbox,category);
                hitbox.Y-=70;hitbox.X+=(int)shot.velocity.X*20;hitbox.Inflate(-30,0);sample.Rectangle(hitbox,category);return true;
            }
            if(type==973 || type==985)
            {
                sample.Rectangle(hitbox,category);
                sample.Curve(center,(type==973?100:90)*scale,new Vector2(shot.rotation,(float)Math.PI/4),type==973?1:3,category,1);return true;
            }
            if(shot.aiStyle==190)
            {
                float angle=shot.rotation+(float)Math.PI*2/25*shot.ai[0],q=Utils.Remap(shot.localAI[0],shot.ai[1]*.3f,shot.ai[1]*.5f,1,0);
                sample.Curve(center,94*scale,new Vector2(angle,(float)Math.PI/4),4,category);
                if(q>0)sample.Curve(center,94*scale,new Vector2(angle-(float)Math.PI/4*shot.ai[0]*q,(float)Math.PI/4),4,category);
                return true;
            }
            if(type==623 || type==758 || type==1093 || type==1112 || type==1118)
            {
                sample.Rectangle(hitbox,category);
                if(shot.ai[0]==2){float offset=type==623?40:type==758?30:8;var size=type==623?new Vector2(80,40):type==758?new Vector2(50,20):new Vector2(20);sample.Rectangle(Utils.CenteredRectangle(center+new Vector2(offset*(type==623?shot.direction:shot.spriteDirection),0),size),category);}
                return true;
            }
            if(type==933 || type==1100)
            {
                for(int k=14;k<shot.oldPos.Length;k+=15)
                    if(shot.localAI[0]-k>=0 && shot.localAI[0]-k<=60)
                    {var c=shot.oldPos[k]+shot.Size/2;CrossSection(sample,c,shot.oldRot[k]+(float)Math.PI/2,40,20,category);sample.Limit(new Rectangle((int)c.X-150,(int)c.Y-150,300,300));}
                CrossSection(sample,center,shot.rotation+(float)Math.PI/2,40,20,category);
                sample.Limit(new Rectangle((int)shot.position.X-150,(int)shot.position.Y-150,300,300));return true;
            }
            if(type==927)
            {
                for(float t=0;t<=1;t+=.05f){var box=hitbox;var offset=shot.velocity.SafeNormalize(Vector2.Zero)*shot.width*Utils.Remap(t,0,1,0,5)*scale;box.Offset((int)offset.X,(int)offset.Y);sample.Rectangle(box,category);}return true;
            }
            if(type==974 || type==919 || type==932)
            {
                CrossSection(sample,center,shot.rotation,type==974?46*scale:40,type==974?8*scale:8,category);
                var bound=type==974?shot.Hitbox:new Rectangle((int)shot.position.X-150,(int)shot.position.Y-150,300,300);
                if(type==974)bound.Inflate((int)(46*scale),(int)(46*scale));sample.Limit(bound);return true;
            }
            if(type==877 || type==878 || type==879)
            {
                // These lances also have aiStyle 19. Their type dispatch must
                // precede the generic spear extension rule, as vanilla does.
                float angle=shot.rotation-(float)Math.PI/4-(float)Math.PI/2-(shot.spriteDirection==1?(float)Math.PI:(float)Math.PI/2);
                sample.Line(center,center+angle.ToRotationVector2()*95,23*scale,category);
                sample.Limit(new Rectangle((int)shot.position.X-150,(int)shot.position.Y-150,300,300));return true;
            }
            if(shot.type==923)
            {
                // Sun Dance is a union of three tapered beam sections. Native
                // Colliding returns before its generic rectangle branch.
                Vector2 direction=shot.rotation.ToRotationVector2();
                sample.Line(shot.Center,shot.Center+direction*(510*shot.scale),70*shot.scale,category);
                sample.Line(shot.Center,shot.Center+direction*(660*shot.scale),42*shot.scale,category);
                sample.Line(shot.Center,shot.Center+direction*(800*shot.scale),7*shot.scale,category);
                return true;
            }
            if(type==598 || type==614 || type==636){sample.Rectangle(hitbox,category);sample.Condition(4,center);return true;}
            if(type==871)
            {
                for(int j=0;j<shot.AI_172_GetPelletStormsCount();j++)
                {var storm=shot.AI_172_GetPelletStormInfo(j);for(int k=0;k<storm.BulletsInStorm;k++)if(storm.IsValid(k))sample.Rectangle(storm.GetBulletHitbox(k,center),category);}
                return true;
            }
            if(type==963){if(shot.ai[0]>=2)hitbox.Inflate(30,30);sample.Rectangle(hitbox,category);return true;}
            if(type==607){hitbox.Offset((int)shot.velocity.X,(int)shot.velocity.Y);sample.Rectangle(hitbox,category);return true;}
            if(type==661){sample.Rectangle(hitbox,category);sample.Condition(3,Vector2.Zero);return true;}
            if(shot.aiStyle==137)
            {sample.Curve(center,shot.height/2-20,Vector2.Zero,1,category,5);sample.Condition(5,shot.Top+new Vector2(0,20));sample.Limit(hitbox);return true;}
            if(type==756 || type==961 || type==1041 || type==1125)
            {if(shot.ai[0]>=0)sample.Line(center,center+shot.velocity.SafeNormalize(-Vector2.UnitY)*200*scale,22*scale,category);return true;}
            if(shot.aiStyle==203)
            {
                if(shot.localAI[0]==1){sample.Rectangle(Utils.CenteredRectangle(center,new Vector2(64)),category);if(type==1117){hitbox.Inflate(60,60);sample.Rectangle(hitbox,category);}}
                if(shot.localAI[0]==3)sample.Curve(center,500,Vector2.Zero,1,category,6);
                var points=shot.customHitbox as MultiPointHitbox;if(points!=null)sample.PointSet(points,category);
                return true;
            }
            if(type==697 || type==707)
            {sample.Rectangle(hitbox,category);if(shot.owner>=0 && shot.owner<Main.maxPlayers)sample.Curve(Main.player[shot.owner].MountedCenter,type==697?73:110,Vector2.Zero,6,category);return true;}
            if(type==802 || type==842 || type>=938 && type<=945 || type==1127)
            {sample.Rectangle(hitbox,category);sample.Line(center,center+shot.velocity*(type==1127?28:6),10*scale,category);return true;}
            if(type==611)
            {sample.Rectangle(hitbox,category);sample.Line(center,center+shot.velocity+shot.velocity.SafeNormalize(Vector2.Zero)*48,16*scale,category);return true;}
            if(type==684)
            {sample.Rectangle(hitbox,category);var offset=shot.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(-Math.PI/2)*40*scale;sample.Line(center-offset,center+offset,16*scale,category);return true;}
            if(type==687)
            {sample.Rectangle(hitbox,category);var axis=shot.rotation.ToRotationVector2()*400;sample.Line(center+axis*Math.Max((shot.ai[0]-38)/40,0),center+axis*Math.Min(shot.ai[0]/25,1),40*scale,category);return true;}
            return false;
        }
        private static void CrossSection(CombatShapeSample sample,Vector2 center,float angle,float halfLength,float width,int category)
        {var offset=angle.ToRotationVector2()*halfLength;sample.Line(center-offset,center+offset,width,category);}
    }
}
