using System;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        private sealed class ArcOwnerLease
        {
            private readonly Player owner;private readonly Item item;private readonly int slot,counter;
            private readonly float scale,rotation,baseHeight,mountCenter;private readonly Vector2 rotationOrigin;
            private readonly bool noMelee;
            internal readonly int[] Melee;
            internal ArcOwnerLease(Player player,Item weapon)
            {
                slot=player.whoAmI;owner=Main.player[slot];item=weapon;counter=player.chlorophyteBladeCounter;
                scale=player.GetAdjustedItemScale(weapon);noMelee=weapon.noMelee;rotation=player.fullRotation;rotationOrigin=player.fullRotationOrigin;
                baseHeight=player.BaseHeight;mountCenter=player.HeightOffsetHitboxCenter;Melee=(int[])player.meleeNPCHitCooldown.Clone();
            }
            private bool MembersMatch(bool afterHandStart)
            {return slot>=0 && slot<Main.player.Length && ReferenceEquals(Main.player[slot],owner) && owner!=null && ReferenceEquals(owner.HeldItem,item) && item.noMelee==noMelee && owner.GetAdjustedItemScale(item)==scale && owner.chlorophyteBladeCounter==(afterHandStart && counter>0?counter-1:counter) && owner.fullRotation==rotation && owner.fullRotationOrigin==rotationOrigin && owner.BaseHeight==baseHeight && owner.HeightOffsetHitboxCenter==mountCenter;}
            internal bool MembersCurrent {get{return MembersMatch(false);}}
            internal bool MembersAfterHandStart {get{return owner!=null && owner.ItemAnimationJustStarted && MembersMatch(true);}}
            internal bool Current
            {get{if(!MembersCurrent || owner.meleeNPCHitCooldown.Length!=Melee.Length)return false;for(int i=0;i<Melee.Length;i++)if(owner.meleeNPCHitCooldown[i]!=Melee[i])return false;return true;}}
            internal bool CurrentAfterHandStart
            {get{if(!MembersAfterHandStart || owner.meleeNPCHitCooldown.Length!=Melee.Length)return false;for(int i=0;i<Melee.Length;i++)if(owner.meleeNPCHitCooldown[i]!=0)return false;return true;}}
        }
        internal void CaptureArcOwner(Player value,Item item)
        {if(history.ArcOwner==null){history.ArcOwner=new ArcOwnerLease(value,item);history.ArcMelee=(int[])history.ArcOwner.Melee.Clone();}}
        // Player.ResetMeleeHitCooldowns runs once before the new animation,
        // not once per newborn projectile. Old local arcs share this reset.
        // These noMelee item roles do not count the array down each update.
        internal void ResetArcMeleeHistory()
        {if(history.ArcMelee!=null){Array.Clear(history.ArcMelee,0,history.ArcMelee.Length);history.ArcHandReset=true;}}
        private bool ArcAllows(HostAttackReceive receive,int slot,int outer)
        {return CapturedAllows(receive,slot,outer) && DynamicAllows(slot,outer) && (!shot.usesOwnerMeleeHitCD || !shot.OwnedBySomeone || history.ArcMelee!=null && history.ArcMelee[slot]<=0);}
        private void RecordArcMelee(int slot,int cooldown)
        {if(shot.usesOwnerMeleeHitCD && shot.OwnedBySomeone && history.ArcMelee!=null)history.ArcMelee[slot]=cooldown;}
        internal bool PassArcOuter(int k,int tick,int outer,float x,float y,int width,int height,Func<MotionRect,bool> collision,Func<MotionRect,bool,bool> ownerQualification,int meleeCooldown,bool consumeSelected=true,Func<int,int,bool> onHit=null)
        {return PassCore(k,tick,outer,x,y,width,height,false,0,onHit,collision,consumeSelected,null,ownerQualification,meleeCooldown,true);}
    }
}
