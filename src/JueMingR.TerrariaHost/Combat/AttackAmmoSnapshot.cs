using System;
using System.Reflection;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed class AttackAmmoSnapshot
    {
        private static readonly Func<Player,Item,Item> select=(Func<Player,Item,Item>)Delegate.CreateDelegate(typeof(Func<Player,Item,Item>),typeof(Player).GetMethod("PickAmmo_PickAmmoItem",BindingFlags.Instance|BindingFlags.NonPublic));
        private static readonly HarmonyLib.AccessTools.FieldRef<Player,int> cardFire=HarmonyLib.AccessTools.FieldRefAccess<Player,int>("killingCardFireType");
        private delegate bool Specific(Player player,int launcher,int ammo,out int projectile);
        private static readonly Specific specific=(Specific)Delegate.CreateDelegate(typeof(Specific),typeof(Player).GetMethod("PickAmmo_TryFindingSpecificMatches",BindingFlags.Instance|BindingFlags.NonPublic));
        internal readonly Item Ammo;
        internal readonly int Type,Stack,Projectile,Mode,Offset,WeaponType,CardFireType;
        internal readonly float Speed,GemFeatures,NoteViewAxis;
        internal readonly int Damage,DamageLifetime;
        private readonly float weaponSpeed,meleeSpeed,ammoSpeed;
        private readonly int weaponShoot,ammoShoot,ammoPrefix;
        private readonly bool quiver,archery,molten,harpy,seraph,phoenix;
        internal bool ExtraArrowUpdates {get{return quiver;}}
        private readonly int ammoSlot=-1;
        private AttackAmmoSnapshot(Player player,Item weapon,Item ammo,int projectile,float speed)
        {Ammo=ammo;Type=ammo?.type??0;Stack=ammo?.stack??0;Projectile=projectile;Speed=speed;Mode=(int)player.ammoCyclingMode;Offset=player.ammoCyclingOffset;WeaponType=weapon.type;
            CardFireType=weapon.type==5461?cardFire(player):0;
            // Harp pitch uses the scaled viewport at the real Shoot entry.
            // Capture it once so zoom changes retire a prepared note preview.
            NoteViewAxis=weapon.type==494?Main.Camera.SmallerScaledAxis:0;
            weaponSpeed=weapon.shootSpeed;weaponShoot=weapon.shoot;meleeSpeed=player.meleeSpeed;ammoSpeed=ammo?.shootSpeed??0;ammoShoot=ammo?.shoot??0;ammoPrefix=ammo?.prefix??0;quiver=player.magicQuiver;archery=player.archery;molten=player.hasMoltenQuiver;harpy=player.accHarpyCharm;seraph=player.accSeraphNecklace;phoenix=player.hasPhoenixQuiver;
            if(weapon.type==494 || projectile==634 || projectile==660 || projectile==80 || projectile==521 || projectile==731 || projectile==337 || HostArcSwingAttack.Weapon(weapon.type) || HostAttachedShotAttack.Handles(projectile) || projectile==399 || HostStreamAttack.Handles(projectile) || HostThornAttack.Handles(projectile) || projectile==711 || projectile==183 || projectile==510 || weapon.type==1121 || weapon.type==1155 || weapon.type==1801 || weapon.type==514 || weapon.type==3350 || weapon.type==3094 || weapon.type==5460)Damage=player.GetWeaponDamage(weapon);
            if(projectile==304)
            {
                // VampireKnives have no ammo damage addition. Native AI002
                // truncates damage*.9 at substep30 and every later substep;
                // a live visual body with zero damage cannot be a Contact.
                Damage=player.GetWeaponDamage(weapon);DamageLifetime=Damage>0?54:0;int value=Damage;
                for(int k=30;k<=54 && value>0;k++){value=(int)(value*.9f);if(value==0)DamageLifetime=k-1;}
            }
            if(HostFlareAttack.Handles(projectile) || projectile==669 || weapon.type==3854 || projectile==442 || projectile==469 || projectile>=477 && projectile<=479)Damage=SelectedAmmoDamage(player,weapon,ammo);
            if(HostGemStaffAttack.Handles(projectile)){GemFeatures=HostGemStaffAttack.Features(player,projectile);Speed=HostGemStaffAttack.Speed(speed,GemFeatures);Damage=HostGemStaffAttack.Damage(player,weapon,GemFeatures);}
            if(ammo!=null)for(int i=0;i<player.inventory.Length;i++)if(ReferenceEquals(player.inventory[i],ammo)){ammoSlot=i;break;}}
        internal static AttackAmmoSnapshot Capture(Player player,Item weapon)
        {return CaptureCore(player,weapon,weapon.shootSpeed,weapon.shoot);}
        private static int SelectedAmmoDamage(Player player,Item weapon,Item ammo)
        {
            int value=player.GetWeaponDamage(weapon);
            // Locked PickAmmo54187..54198 adds SharpBarb before applying the
            // selected ammo's own bow multiplier. Never call RNG/consume ammo.
            if(ammo!=null){int addition=ammo.damage+(player.accSharpBarb && AmmoID.Sets.IsArrow[ammo.ammo]?1:0);if(addition>0)value+=(int)(addition*player.GetWeaponDamageMultiplier(ammo));}
            return value;
        }
        internal static AttackAmmoSnapshot CaptureController(Player player,Item weapon,float baseSpeed)
        {return CaptureCore(player,weapon,baseSpeed,14);}
        private static AttackAmmoSnapshot CaptureCore(Player player,Item weapon,float baseSpeed,int baseProjectile)
        {
            int projectile=baseProjectile;float speed=baseSpeed;Item ammo=null;
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
            // ItemCheck_Shoot emits a held gun controller with the weapon's
            // own speed after selecting its ammo. Its later AI selects the
            // damage shot using CaptureController's separate base speed.
            if(baseProjectile==weapon.shoot && ItemID.Sets.gunProj[weapon.type])speed=weapon.shootSpeed;
            if(weapon.type==1254 || weapon.type==1255 || weapon.type==1265){if(projectile==14)projectile=242;}
            // These ItemCheck_Shoot conversions occur AFTER PickAmmo, unlike
            // quiver/BeesKnees. Retain the selected ammunition's speed addition.
            if(weapon.type==120 && projectile==1)projectile=2;
            if(weapon.type==682)projectile=117;
            if(weapon.type==725)projectile=120;
            if(weapon.type==2796)projectile=442;
            if(weapon.type==2223)projectile=357;
            if(weapon.type==3859)projectile=710; // Native Shoot replaces every selected arrow with its six-member fan.
            if(weapon.type==5117)projectile=968;
            return speed>0 && projectile>0?new AttackAmmoSnapshot(player,weapon,ammo,projectile,speed):null;
        }
        private bool GemMatches(Player player,Item weapon)
        {return !HostGemStaffAttack.Handles(Projectile) || HostGemStaffAttack.Features(player,Projectile)==GemFeatures && HostGemStaffAttack.Damage(player,weapon,GemFeatures)==Damage;}
        internal bool IdentityMatches(Player player,Item weapon)
        {return ReferenceEquals(weapon.useAmmo>0?select(player,weapon):null,Ammo) && MembersMatch(player,weapon);}
        // Draw never reruns native ammunition selection. These constant reads
        // only retire the prepared presentation when its already selected slot
        // or known fields change. New higher-priority ammo is observed by the
        // next prepare/real consumer's full IdentityMatches, not inferred here.
        internal bool MembersMatch(Player player,Item weapon)
        {return (!HostFlareAttack.Handles(Projectile) || SelectedAmmoDamage(player,weapon,Ammo)==Damage) && (Ammo==null || ammoSlot>=0 && ammoSlot<player.inventory.Length && ReferenceEquals(player.inventory[ammoSlot],Ammo) && Ammo.type==Type && Ammo.stack==Stack && Ammo.prefix==ammoPrefix && Ammo.shoot==ammoShoot && Ammo.shootSpeed==ammoSpeed) &&
            weapon.shoot==weaponShoot && weapon.shootSpeed==weaponSpeed && player.meleeSpeed==meleeSpeed && player.magicQuiver==quiver && player.archery==archery && player.hasMoltenQuiver==molten && player.accHarpyCharm==harpy && player.accSeraphNecklace==seraph && player.hasPhoenixQuiver==phoenix && (WeaponType!=494 && Projectile!=634 && Projectile!=660 && Projectile!=80 && Projectile!=521 && Projectile!=731 && Projectile!=337 && !HostArcSwingAttack.Weapon(WeaponType) && !HostAttachedShotAttack.Handles(Projectile) && Projectile!=399 && !HostStreamAttack.Handles(Projectile) && !HostThornAttack.Handles(Projectile) && Projectile!=711 && Projectile!=304 && Projectile!=183 && Projectile!=510 && WeaponType!=1121 && WeaponType!=1155 && WeaponType!=1801 && WeaponType!=514 && WeaponType!=3350 && WeaponType!=3094 && WeaponType!=5460 || player.GetWeaponDamage(weapon)==Damage) && (Projectile!=669 && WeaponType!=3854 && Projectile!=442 && Projectile!=469 && (Projectile<477 || Projectile>479) || SelectedAmmoDamage(player,weapon,Ammo)==Damage) && (WeaponType!=5461 || cardFire(player)==CardFireType) && (WeaponType!=494 || Main.Camera.SmallerScaledAxis==NoteViewAxis) && GemMatches(player,weapon) && (int)player.ammoCyclingMode==Mode && player.ammoCyclingOffset==Offset;}
        internal bool Matches(Player player,Item weapon)
        {if(!MembersMatch(player,weapon))return false;var now=Capture(player,weapon);return now!=null && ReferenceEquals(now.Ammo,Ammo) && now.Type==Type && now.Stack==Stack && now.Projectile==Projectile && now.Speed==Speed && now.Damage==Damage && now.Mode==Mode && now.Offset==Offset;}
    }
}
