using JueMingR.Features.Combat;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // Reads the resolved shot, never ItemCheck/PickAmmo/AI. Explicit members
    // protect homing/controller/derived stages from an AI-style blanket promise.
    internal static class HostAttackModels
    {
        internal static bool TryRead(AttackAmmoSnapshot ammo,out AttackMotion motion)
        {return TryResolved(ammo.Projectile,ammo.Speed,ammo.WeaponType,out motion);}
        internal static bool TryResolved(int projectile,float speed,int weaponType,out AttackMotion motion)
        {
            motion=default(AttackMotion);Projectile sample;
            if(!ContentSamples.ProjectilesByType.TryGetValue(projectile,out sample))return false;
            float gravity=0,acceleration=1,max=0;int start=0;bool componentAcceleration=false,drag=false,snap=false;
            switch(projectile)
            {
                case 1:case 2:case 4:case 41:gravity=.1f;start=15;break;
                case 117:gravity=.06f;start=35;break;
                case 120:gravity=.05f;start=30;break;
                case 495:gravity=.04f;start=30;break;
                case 639:gravity=.1f;start=15;break;
                case 134:case 137:case 140:case 143:acceleration=1.1f;max=15;componentAcceleration=true;break;
                case 133:case 136:case 139:case 142:gravity=.2f;start=16;break;
                case 135:case 138:case 141:case 144:gravity=.2f;start=1;acceleration=.97f;drag=true;snap=true;break;
                case 14:case 5:case 89:case 100:case 110:case 242:case 257:case 279:case 283:case 284:case 285:case 286:
                case 981:case 158:case 159:case 160:case 161:case 357:case 638:break;
                default:return false;
            }
            bool spread=weaponType==534 || weaponType==964 || weaponType==4703 || weaponType==3788 || weaponType==2624 || weaponType==1229;
            motion=new AttackMotion(speed,gravity,start,sample.extraUpdates+1,sample.width,sample.height,sample.timeLeft,
                spread?AttackConfidence.Representative:AttackConfidence.Conditional,acceleration,max,sample.aiStyle==1,componentAcceleration,drag,snap);return true;
        }
    }
}
