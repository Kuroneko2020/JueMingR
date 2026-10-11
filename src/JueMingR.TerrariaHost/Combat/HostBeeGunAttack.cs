using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Item1121's hand selects direction; its born AI036 bees own all later
    // steering. Counts, jitter and beeType/beeDamage are native random draws.
    // Never call those methods: they also mutate Player.makeStrongBee.
    internal static class HostBeeGunAttack
    {
        internal static bool Handles(int type){return type==181 || type==566;}
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(ammo.WeaponType!=1121 || !Handles(ammo.Projectile) || ammo.Damage<=0 || player.whoAmI!=Main.myPlayer || age+1>=timeline.Count)return null;
            bool strong=obstacles.CaptureBeeOwner();var policy=new Projectile();policy.SetDefaults(181);policy.owner=player.whoAmI;policy.damage=ammo.Damage;policy.magic=true;
            var targets=HostBeeAttack.CaptureTargets(policy,obstacles);var pool=obstacles.CaptureBirthSlots();var directions=new Vector2[41];int count=0;float knockBack=player.GetWeaponKnockback(player.HeldItem,player.HeldItem.knockBack);
            for(int seed=0;seed<41;seed++)
            {
                Vector2 direction;if(seed<9){var b=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].ProjectileReceiveBounds;direction=(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction);}else direction=((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
                for(int branch=0;branch<(strong?2:1);branch++)
                {
                    // One member, zero independent +/-0.7 jitter, ordinary
                    // +0 damage or strong +1 are all legal native outcomes.
                    // They remain representatives of this future random use;
                    // actual captured bees retain their real count/damage.
                    obstacles.BeginCandidate();var slots=pool.Copy();int slot;if(!slots.TryAllocate(out slot))return null;
                    var bee=new Projectile();bee.SetDefaults(branch==0?181:566);bee.active=true;bee.whoAmI=slot;bee.owner=player.whoAmI;bee.magic=true;bee.damage=ammo.Damage+branch;bee.knockBack=branch==0?knockBack:.5f+knockBack*1.1f;bee.Center=origin;bee.velocity=direction*ammo.Speed;
                    bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,bee.width,bee.height,out wet,out kind,true))return null;bee.wet=wet;bee.honeyWet=wet && kind==2;bee.shimmerWet=wet && kind==3;
                    var contact=HostBeeAttack.NewbornRepresentative(new[]{bee},timeline,age+1,delay+1,terrain,obstacles,targets);
                    if(contact!=null){var aim=origin+direction*10000;return AttackIntercept.WithAimInput(contact,aim.X,aim.Y);}
                }
            }
            return null;
        }
    }
}
