using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostAttackWindow
    {
        internal static bool Ordinary(Item item)
        {return item!=null && !HostSwingAttack.Handles(item) && !HostHeldAttack.Weapon(item.type) && !HostYoyoNavigation.Weapon(item) && !HostWhipAttack.Weapon(item) && !item.channel && !ItemID.Sets.ShootsOnUseRelease[item.type];}
        internal static bool NextAction(HostCombat combat,Player player,Item item)
        {
            if(!Ordinary(item))return true;
            return Opening(combat,player,item);
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
            return pressed && (player.releaseUseItem || repeat) && player.reuseDelay<=0 && player.itemTime<=1 && HostAttackResources.Initial(player,item);
        }
    }
}
