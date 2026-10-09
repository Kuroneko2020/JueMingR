using System;
using System.Reflection;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostAttackResources
    {
        private delegate void ManaDetails(Player player,Item item,out bool skip,out int amount,out bool free);
        private static readonly ManaDetails details=(ManaDetails)Delegate.CreateDelegate(typeof(ManaDetails),typeof(Player).GetMethod("GetItemManaUsageDetails",BindingFlags.Instance|BindingFlags.NonPublic));
        internal static bool Initial(Player player,Item item)
        {
            if(item.mana<=0)return true;if(player.silence)return false;
            bool skip,free;int amount;details(player,item,out skip,out amount,out free);if(skip)return true;
            // Prediction is the original pure query. CheckMana itself resets
            // slowMagicUse and can drink/consume even when pay is false.
            bool quick=player.manaFlower && !Main.LocalPlayerHasPendingInventoryActions() && !player.cursed && !player.CCed && !player.dead && player.manaPotionDelay<=0;
            return player.CheckManaPredictWithoutUse(amount,quick);
        }
        // The parent charges before its fee query changes slowMagicUse. Keep
        // this order in a private resource prefix; regeneration, potion use
        // and other attacks remain future conditions, never extra live queries.
        internal sealed class FeeClock
        {
            private readonly int type,cost;private readonly bool manaV2;
            private float phase,counter;private int balance;private bool slow;
            internal FeeClock(Player player,Projectile parent)
            {type=parent.type;cost=(int)(player.HeldItem.mana*player.manaCost);manaV2=Terraria.Testing.DebugOptions.ManaV2;phase=parent.ai[0];counter=parent.ai[1];balance=player.statMana;slow=player.slowMagicUse;}
            internal bool Advance(out float charge,out bool readsDirection)
            {
                bool fee;
                if(type==633)
                {float interval=phase>120?5:phase>90?15:30;phase++;counter++;readsDirection=counter>=1;if(readsDirection)counter=0;fee=phase%interval==0;}
                else
                {
                    phase+=slow?.4f:1f;bool beam=phase>=180;if(!beam)counter++;
                    readsDirection=beam?(int)phase%5==0:counter>=5;
                    bool check=phase==1 || beam && phase%20==0 || readsDirection;
                    if(readsDirection && !beam)counter=0;
                    fee=check && ((int)phase==80 || (int)phase==180 || phase>180 && (int)phase%20==0);
                }
                charge=phase;if(!fee)return true;
                if(balance>=cost){balance-=cost;slow=false;return true;}
                if(!manaV2)return false;balance=0;slow=true;return true;
            }
        }
    }
}
