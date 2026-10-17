using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // OnyxBlaster adds a real fifth body to the same ItemCheck volley. The
    // four selected bullets and the black bolt keep one ordered birth pool
    // and owner history; a bolt-only ray cannot prove the whole volley.
    internal static class HostOnyxBlastAttack
    {
        internal static bool Weapon(int type){return type==3788;}
        internal static bool Family(int type){return type==661;}
        internal static int Ai(Projectile p)
        {
            if(p.type!=661)return -1;
            if(p.alpha>0){p.alpha-=55;p.scale=1.3f;if(p.alpha<0)p.alpha=0;}
            // 661 is excluded from AI001's ordinary clock increment, but an
            // already observed clock>=15 still takes its common gravity arm.
            if(p.ai[0]>=15){p.ai[0]=15;p.velocity.Y+=.1f;}
            p.rotation=(float)Math.Atan2(p.velocity.Y,p.velocity.X)+1.57f;
            if(p.velocity.Y>16)p.velocity.Y=16;return 0;
        }
        internal static bool PrepareKillBurst(Projectile p)
        {
            if(p.type!=661)return false;
            var center=p.Center;p.width=p.height=160;p.Center=center;p.maxPenetrate=p.penetrate=-1;
            return true;
        }
        internal static bool Collides(Projectile p,MotionRect target,PredictionTerrain terrain,out bool known)
        {
            var body=p.Hitbox;var box=new Rectangle((int)target.X,(int)target.Y,(int)target.Width,(int)target.Height);known=true;
            if(!body.Intersects(box))return false;
            var from=body.Center.ToVector2();var to=box.Center.ToVector2();
            if(Vector2.Distance(from,to)>500)return false;
            bool clear;PredictionStop stop;known=terrain.CanHitLine(new MotionRect(from.X,from.Y,0,0),new MotionRect(to.X,to.Y,0,0),out clear,out stop);return known && clear;
        }
        internal static bool Step(Projectile p,PredictionTerrain terrain,out bool killed,out bool skipped)
        {return HostArrowFamilyAttack.StepMotor(p,terrain,out killed,out skipped,Ai);}
        internal static Projectile[] Birth(Player player,AttackAmmoSnapshot ammo,Vector2 origin,Vector2 velocity)
        {
            var born=new Projectile[5];
            for(int i=0;i<born.Length;i++)
            {
                var p=HostNoteAttack.Copy(Terraria.ID.ContentSamples.ProjectilesByType[i==4?661:ammo.Projectile]);
                p.active=true;p.owner=player.whoAmI;p.whoAmI=-1;p.Center=origin;p.velocity=i==4?velocity.SafeNormalize(Vector2.UnitX*player.direction)*(ammo.Speed*1.3f):velocity;
                p.damage=ammo.Damage*(i==4?2:1);p.knockBack=player.HeldItem.knockBack;
                // Four zero jitter draws are legal independent representatives
                // of native NextFloatDirection. The fifth bolt is deterministic
                // after this actual shoot direction/speed have been fixed.
                if(p.aiStyle==1)while(p.velocity.X>=16 || p.velocity.X<=-16 || p.velocity.Y>=16 || p.velocity.Y< -16)p.velocity*=.97f;
                born[i]=p;
            }
            return born;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(!Weapon(ammo.WeaponType) || ammo.Damage<=0)return null;
            var clock=new HostAttackClock(path,beforeNpc?HostAttackPhase.BeforeNpc:HostAttackPhase.CompletedWorld,delay);
            root.CaptureShotFacing(player);var captured=HostHeldAmmoFamilyAttack.CaptureForSolve(player,root,new[]{ammo.Projectile,661});var directions=new List<Vector2>();
            for(int seed=0;seed<41;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;
                var unit=seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                if(directions.Contains(unit))continue;directions.Add(unit);var aim=origin+unit*10000;
                var velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);var born=Birth(player,ammo,origin,velocity);
                var contact=HostHeldAmmoFamilyAttack.ExplicitBirthBatch(player,born,path,clock,terrain,root,aim,0,null,captured,true);
                if(contact!=null)return contact;
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            // An already born661 uses the same batch motor/receive engine;
            // this entry cannot mint a hand or sustained cursor lease.
            return HostHeldAmmoFamilyAttack.KnownFamily(player,sources,path,clock,terrain,root);
        }
    }
}
