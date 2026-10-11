using System;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostAttackWindow
    {
        internal static bool Ordinary(Item item)
        {return item!=null && (!HostSwingAttack.Handles(item) || HostSwingAttack.Mixed(item)) && !HostHeldAttack.Weapon(item.type) && !HostYoyoNavigation.Weapon(item) && !HostWhipAttack.Weapon(item) && (!item.channel || item.type==3473) && !ItemID.Sets.ShootsOnUseRelease[item.type];}
        internal static bool NextAction(HostCombat combat,Player player,Item item)
        {
            if(!Ordinary(item))return true;
            return Opening(combat,player,item);
        }
        // This is a hypothetical use time, never permission to press Use or
        // alter native clocks. Ordinary cooldown survives between real shots;
        // controllers resolve their current charge/release stage separately.
        internal static int PreviewDelay(Player player,Item item)
        {
            if(player==null || item==null || item.IsAir || player.noItems || player.CCed || player.delayUseItem || player.selectedItemState.HasBufferedChange)return -1;
            // Solar's channel flag sustains native dummy timers, but its
            // sealed arc never consumes later aim input. Only a new legal
            // hand use needs an execution plan; preview adds the live arc's
            // remaining timer ownership before hypothesizing that new use.
            if(HostHeldAttack.Weapon(item.type) || HostYoyoNavigation.Weapon(item) || item.channel && item.type!=3473)return 0;
            if(HostSwingAttack.Handles(item) && !HostSwingAttack.Mixed(item) && player.itemAnimation>0)return 0;
            if(!HostAttackResources.Initial(player,item))return -1;
            if(player.itemAnimation>1 && !item.shootsEveryUse)return Math.Max(0,player.itemTime-1);
            return Math.Max(item.shootsEveryUse?0:Math.Max(0,player.itemTime-1),Math.Max(player.reuseDelay,Math.Max(0,player.itemAnimation-1)));
        }
        // Controllers still need continuous preparation between item uses.
        // This narrower read asks only whether the next native ItemCheck can
        // emit from the hand; it never replaces ongoing projectile demand.
        internal static bool Opening(HostCombat combat,Player player,Item item)
        {
            if(player.noItems || player.CCed || player.delayUseItem || player.selectedItemState.HasBufferedChange)return false;
            // The native action decrements timers before OwnerOnlyCode. Thus
            // entry time=1/animation>=2 is this action's firing window, while
            // 1/1 ends the animation without another ordinary Shoot.
            if(player.itemAnimation>1)return player.itemTime<=1 && !item.shootsEveryUse;
            bool pressed=combat.Left || player.controlUseItem || combat.Use.OrdinaryPress;
            // Managed magic-string reuse fills release after native autoreuse
            // at this same ready action. Its real source intention is readable
            // here; waiting for the later flag would miss that legal emission.
            bool repeat=item.autoReuse || (player.autoReuseAllWeapons || player.stressBall) && (!item.channel || !player.channel) || player.autoReuseGlove && item.melee && item.type!=3030 || combat.Use.OrdinaryPress || combat.Use.ManagedAttackIntent(player);
            // Only the item's AutoReuseLogic clears animation=1 before the
            // new-use gate. Attachments and G11A merely grant a release edge.
            if(player.itemAnimation==1 && !item.autoReuse)return false;
            // Starting a use is tested BEFORE the decrement. A held ordinary
            // non-autoreuse item does not become fresh just because timers hit
            // zero. This is a read-only planning gate, never a native Shoot veto.
            // shootsEveryUse replaces the native ItemTimeIsZero shoot gate
            // with ItemAnimationJustStarted. On automatic reuse the previous
            // item's time may still be 2 while this action starts a real shot.
            return pressed && (player.releaseUseItem || repeat) && player.reuseDelay<=0 && (item.shootsEveryUse || player.itemTime<=1) && HostAttackResources.Initial(player,item);
        }
    }
}
