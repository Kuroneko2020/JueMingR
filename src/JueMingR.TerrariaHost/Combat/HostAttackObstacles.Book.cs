using System;
using Terraria;
using Terraria.Testing;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        private Player bookOwner;private Item bookWeapon;
        private int bookOwnerSlot,bookAlternate,bookUseTime,bookUseAnimation,bookDirection;
        private float bookRotation;private bool bookSlow,bookManaV2,bookLocked;
        internal void CaptureBookHand(Player value,bool lockedDirection)
        {
            // A future launch uses a private pose, but the premise belongs to
            // the actual numbered owner. Presentation checks only these fixed
            // scalars; it never replays the hand or scans the projectile pool.
            bookOwnerSlot=value.whoAmI;bookOwner=bookOwnerSlot>=0 && bookOwnerSlot<Main.player.Length?Main.player[bookOwnerSlot]??value:value;
            bookWeapon=bookOwner.HeldItem;bookAlternate=value.altFunctionUse;bookSlow=value.slowMagicUse;bookManaV2=DebugOptions.ManaV2;
            bookUseTime=value.HeldItem.useTime;bookUseAnimation=value.HeldItem.useAnimation;bookLocked=lockedDirection;
            if(lockedDirection){bookRotation=value.itemRotation;bookDirection=value.direction;}
        }
        private bool BookHandCurrent
        {get{return bookOwner==null || bookOwnerSlot>=0 && bookOwnerSlot<Main.player.Length && ReferenceEquals(Main.player[bookOwnerSlot],bookOwner) && ReferenceEquals(bookOwner.HeldItem,bookWeapon) && bookOwner.altFunctionUse==bookAlternate && bookOwner.slowMagicUse==bookSlow && DebugOptions.ManaV2==bookManaV2 && bookWeapon.useTime==bookUseTime && bookWeapon.useAnimation==bookUseAnimation && (!bookLocked || bookOwner.itemRotation==bookRotation && bookOwner.direction==bookDirection);}}
    }
}
