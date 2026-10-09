using System;
using System.Reflection;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed class AttackAmmoSnapshot
    {
        private static readonly Func<Player,Item,Item> select=(Func<Player,Item,Item>)Delegate.CreateDelegate(typeof(Func<Player,Item,Item>),typeof(Player).GetMethod("PickAmmo_PickAmmoItem",BindingFlags.Instance|BindingFlags.NonPublic));
        private delegate bool Specific(Player player,int launcher,int ammo,out int projectile);
        private static readonly Specific specific=(Specific)Delegate.CreateDelegate(typeof(Specific),typeof(Player).GetMethod("PickAmmo_TryFindingSpecificMatches",BindingFlags.Instance|BindingFlags.NonPublic));
        internal readonly Item Ammo;
        internal readonly int Type,Stack,Projectile,Mode,Offset,WeaponType;
        internal readonly float Speed;
        private readonly float weaponSpeed,meleeSpeed,ammoSpeed;
        private readonly int weaponShoot,ammoShoot,ammoPrefix;
        private readonly bool quiver,archery,molten;
        private AttackAmmoSnapshot(Player player,Item weapon,Item ammo,int projectile,float speed)
        {Ammo=ammo;Type=ammo?.type??0;Stack=ammo?.stack??0;Projectile=projectile;Speed=speed;Mode=(int)player.ammoCyclingMode;Offset=player.ammoCyclingOffset;WeaponType=weapon.type;
            weaponSpeed=weapon.shootSpeed;weaponShoot=weapon.shoot;meleeSpeed=player.meleeSpeed;ammoSpeed=ammo?.shootSpeed??0;ammoShoot=ammo?.shoot??0;ammoPrefix=ammo?.prefix??0;quiver=player.magicQuiver;archery=player.archery;molten=player.hasMoltenQuiver;}
        internal static AttackAmmoSnapshot Capture(Player player,Item weapon)
        {
            int projectile=weapon.shoot;float speed=weapon.shootSpeed;Item ammo=null;
            if(weapon.melee && !ProjectileID.Sets.NoMeleeSpeedVelocityScaling[projectile])speed/=player.meleeSpeed;
            if(weapon.useAmmo>0)
            {
                // This exact helper reads native slot order/cycling policy. In
                // contrast PickAmmo(dontConsume:true) STILL advances RNG and
                // has a forced-consumption exception; never call it here.
                ammo=select(player,weapon);if(ammo==null)return null;
                int chosen;if(specific(player,weapon.type,ammo.type,out chosen))projectile=chosen;
                else if(weapon.type==1946)projectile=338+ammo.type-771;
                else if(weapon.type==3930)projectile=715+ammo.type-AmmoID.Rocket;
                else if(weapon.useAmmo==AmmoID.Rocket || weapon.useAmmo==AmmoID.Solution)projectile+=ammo.shoot;
                else if(ammo.shoot>0)projectile=ammo.shoot;
                if(weapon.type==3019 && projectile==1)projectile=485;
                if(weapon.type==3052)projectile=495;
                if(weapon.type==4953 && projectile==1)projectile=932;
                if(weapon.type==4381)projectile=819;
                if(weapon.type==4058 && projectile==474)projectile=117;
                if(projectile==42){if(ammo.type==370)projectile=65;else if(ammo.type==408)projectile=68;else if(ammo.type==1246)projectile=354;}
                if(player.inventory[player.selectedItem].type==2888 && projectile==1)projectile=469;
                if(player.hasMoltenQuiver && projectile==1)projectile=2;
                speed+=ammo.shootSpeed;
                if(player.magicQuiver && (weapon.useAmmo==AmmoID.Arrow || weapon.useAmmo==AmmoID.Stake))speed*=1.1f;
                if(AmmoID.Sets.IsArrow[ammo.ammo] && player.archery && speed<20)speed=Math.Min(20,speed*1.2f);
            }
            if(weapon.type==1254 || weapon.type==1255 || weapon.type==1265){if(projectile==14)projectile=242;}
            // These ItemCheck_Shoot conversions occur AFTER PickAmmo, unlike
            // quiver/BeesKnees. Retain the selected ammunition's speed addition.
            if(weapon.type==120 && projectile==1)projectile=2;
            if(weapon.type==682)projectile=117;
            if(weapon.type==725)projectile=120;
            if(weapon.type==2796)projectile=442;
            if(weapon.type==2223)projectile=357;
            if(weapon.type==5117)projectile=968;
            return speed>0 && projectile>0?new AttackAmmoSnapshot(player,weapon,ammo,projectile,speed):null;
        }
        internal bool IdentityMatches(Player player,Item weapon)
        {return ReferenceEquals(weapon.useAmmo>0?select(player,weapon):null,Ammo) && (Ammo==null || Ammo.type==Type && Ammo.stack==Stack && Ammo.prefix==ammoPrefix && Ammo.shoot==ammoShoot && Ammo.shootSpeed==ammoSpeed) &&
            weapon.shoot==weaponShoot && weapon.shootSpeed==weaponSpeed && player.meleeSpeed==meleeSpeed && player.magicQuiver==quiver && player.archery==archery && player.hasMoltenQuiver==molten && (int)player.ammoCyclingMode==Mode && player.ammoCyclingOffset==Offset;}
        internal bool Matches(Player player,Item weapon)
        {var now=Capture(player,weapon);return now!=null && ReferenceEquals(now.Ammo,Ammo) && now.Type==Type && now.Stack==Stack && now.Projectile==Projectile && now.Speed==Speed && now.Mode==Mode && now.Offset==Offset;}
    }
}
