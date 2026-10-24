using JueMingR.Features.Combat;
using Terraria;
using Terraria.ID;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Combat
{
    // Reads the resolved shot, never ItemCheck/PickAmmo/AI. Explicit members
    // protect homing/controller/derived stages from an AI-style blanket promise.
    internal static class HostAttackModels
    {
        internal static bool TryRead(AttackAmmoSnapshot ammo,out AttackMotion motion)
        {
            if(!TryResolved(ammo.Projectile,ammo.Speed,ammo.WeaponType,out motion))return false;
            var sample=ContentSamples.ProjectilesByType[ammo.Projectile];if(ammo.ExtraArrowUpdates && sample.arrow && sample.extraUpdates<1)motion=motion.WithUpdates(2,1);
            if(ammo.Projectile==304){if(ammo.DamageLifetime<=0)return false;motion=motion.WithLifetime(System.Math.Min(motion.Lifetime,ammo.DamageLifetime));}return true;
        }
        internal static Vector2 LaunchOrigin(Player player,Item item)
        {bool directionDependent;return LaunchOrigin(player,item,out directionDependent);}
        internal static Vector2 LaunchOrigin(Player player,Item item,out bool directionDependent)
        {
            directionDependent=false;var origin=player.RotatedRelativePoint(player.MountedCenter);
            if(item.type==5117 || item.type==3094 || item.type==3378 || item.type==3543)origin.Y=player.position.Y+player.height/3;
            if(item.type==281 || item.type==986){origin.X+=6*player.direction;origin.Y-=6*player.gravDir;directionDependent=true;}
            if(item.type==3007){origin.X-=4*player.direction;origin.Y-=2*player.gravDir;directionDependent=true;}
            return origin;
        }
        internal static bool TryResolved(int projectile,float speed,int weaponType,out AttackMotion motion)
        {
            motion=default(AttackMotion);Projectile sample;
            if(!ContentSamples.ProjectilesByType.TryGetValue(projectile,out sample))return false;
            float gravity=0,acceleration=1,max=0;int start=0,lifetime=sample.timeLeft;bool componentAcceleration=false,drag=false,snap=false;
            switch(projectile)
            {
                case 1:case 2:case 4:case 41:case 51:case 82:case 103:case 225:case 278:case 282:case 474:case 819:gravity=.1f;start=15;break;
                case 336:case 345:gravity=.5f;start=50;break;
                case 246:gravity=.15f;start=60;break;
                case 311:gravity=.5f;start=30;break;
                case 312:gravity=.5f;start=20;break;
                case 172:gravity=.085f;start=17;break;
                case 117:gravity=.06f;start=35;break;
                case 120:gravity=.05f;start=30;break;
                case 495:gravity=.04f;start=30;break;
                case 639:gravity=.1f;start=15;break;
                case 81:case 91:gravity=.07f;start=20;break;
                case 1006:gravity=-.1f;start=15;break;
                case 267:gravity=.075f;start=20;break;
                case 323:break; // AI001 excludes Stake from its advancing gravity clock.
                case 968:gravity=.4f;start=20;acceleration=.97f;break;
                // Locked AI002 ordinary throw family: the same scalar motor,
                // separately audited birth/death and native collision bodies.
                // MagicDagger's random threshold and compound flask/dart roles
                // are intentionally outside this deterministic family.
                case 3:case 21:case 48:case 54:case 318:case 330:case 520:case 599:case 1012:case 1085:gravity=.4f;start=20;acceleration=.97f;break;
                // Conversion flasks end at their first collision; the native
                // world conversion on Kill is never replayed by prediction.
                case 69:case 70:case 621:gravity=.25f;start=10;acceleration=.99f;break;
                case 166:gravity=.3f;start=20;acceleration=.98f;break;
                // AI010's released seeds count ai1, not AI001's ai0.
                // Mud's first AI stores ai0=1 before incrementing it, so its
                // twentieth counter value occurs at subupdate nineteen.
                case 42:case 65:case 68:case 354:gravity=.2f;start=60;break;
                case 1081:gravity=.3f;start=19;acceleration=.98f;break;
                case 1082:case 1083:gravity=.4f;start=17;acceleration=.95f;break;
                case 497:gravity=.5f;start=30;acceleration=.99f;break;
                // These are seed motors for the finite throw dispatcher.
                // 93's six random gravity starts are validated there; 306's
                // straight hand stage does not cover its random 307 children.
                case 93:gravity=.4f;start=28;acceleration=.97f;break;
                case 306:break;
                case 954:case 979:gravity=.1f;start=20;acceleration=.99f;lifetime=System.Math.Min(lifetime,59);break;
                case 134:case 137:case 140:case 143:acceleration=1.1f;max=15;componentAcceleration=true;break;
                case 133:case 136:case 139:case 142:gravity=.2f;start=16;break;
                case 135:case 138:case 141:case 144:gravity=.2f;start=1;acceleration=.97f;drag=true;snap=true;break;
                // CrystalStorm and its shimmer variant use AI_024's .985
                // damping, not the .96 fallback of the other crystal roles.
                case 94:case 1114:acceleration=.985f;lifetime=System.Math.Min(lifetime,151);break;
                // AI_002 fades VampireKnives after substep 30, killing before
                // damage on substep 55. Its first 54 positions remain straight.
                case 304:lifetime=System.Math.Min(lifetime,54);break;
                case 12:case 955:break;
                // The three actual AI028 hand roles have a straight motor;
                // their native penetration/static immunity stays in Receive.
                case 118:case 119:case 359:break;
                // Inversion only seeds these finite replays; settling against
                // walls, three reflections and rolling drag stay in their
                // actual native motor/collision/receive dispatcher.
                case 114:acceleration=.98f;start=20;break;
                case 116:case 173:break;
                case 24:case 183:gravity=.2f;start=6;break;
                case 261:gravity=.2f;start=16;break;
                case 507:gravity=.3f;start=61;acceleration=.98f;break;
                case 20:case 104:case 265:case 287:case 355:
                case 14:case 5:case 89:case 100:case 110:case 242:case 257:case 279:case 283:case 284:case 285:case 286:
                case 981:case 158:case 159:case 160:case 161:case 357:case 638:break;
                default:return false;
            }
            bool spread=projectile==1114 || weaponType==534 || weaponType==964 || weaponType==4703 || weaponType==3788 || weaponType==2624 || weaponType==1229 || weaponType==1569 || weaponType==3019 || weaponType==1930 || weaponType==5117 || weaponType==518;
            motion=new AttackMotion(speed,gravity,start,sample.extraUpdates+1,sample.width,sample.height,lifetime,
                spread?AttackConfidence.Representative:AttackConfidence.Conditional,acceleration,max,sample.aiStyle==1,componentAcceleration,drag,snap,maxFallSpeed:sample.aiStyle==10?10:sample.aiStyle==1?16:sample.aiStyle==2?32:0,
                gravityClockUsesLaunchX:weaponType==3019 && sample.aiStyle==1 && gravity!=0,maxRiseSpeed:projectile==1006?16:0,accelerationStart:sample.aiStyle==2 && acceleration!=1 || projectile==114 || projectile==507 || projectile==1081 || projectile==1082 || projectile==1083?start:0,horizontalAccelerationOnly:sample.aiStyle==2 && acceleration!=1 || projectile==507 || projectile==1081 || projectile==1082 || projectile==1083,
                preMotorWindX:sample.aiStyle==2 && Main.windPhysics?Main.windSpeedCurrent*Main.windPhysicsStrength:0);return true;
        }
    }
}
