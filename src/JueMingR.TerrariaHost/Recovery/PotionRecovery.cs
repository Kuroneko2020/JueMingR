using System;
using System.Reflection;
using JueMingR.Features.Recovery;
using Terraria;

namespace JueMingR.TerrariaHost.Recovery
{
    internal sealed class PotionRecovery
    {
        private delegate void ManaDetails(Player player,Item item,out bool skip,out int raw,out bool free);
        private static readonly ManaDetails details=(ManaDetails)Delegate.CreateDelegate(typeof(ManaDetails),typeof(Player).GetMethod("GetItemManaUsageDetails",BindingFlags.Instance|BindingFlags.NonPublic));
        private readonly HostRecovery host;
        private readonly ulong[] lease=new ulong[5];
        private readonly Failure[] rejected=new Failure[98];
        private long nextToken,token;
        private RecoverySource current;
        internal int Kind {get;private set;}
        internal bool Effect {get;private set;}
        internal long ManaFrame {get;private set;}=-1;
        internal long LifeFrame {get;private set;}=-1;
#if DEBUG
        internal long CandidateReads,NativeCalls;
#endif
        private struct Failure {internal Item Item;internal int Type,Stack;internal long Revision;internal int Kind;}
        internal PotionRecovery(HostRecovery host){this.host=host;}
        internal void Reset(){Array.Clear(rejected,0,rejected.Length);ManaFrame=LifeFrame=-1;}
        internal static int Healing(Item item){return item.type==3001?95:item.healLife;}
        internal void Heal(Player p)
        {
            int d=p.statLifeMax2-p.statLife;
            if(d<=0 || p.potionDelay>0 || LifeFrame==host.Input.Frame)return;
            RecoverySource best=default(RecoverySource);int amount=0,mode=host.Value(0);
            Choose(p,1,ref best,ref amount,mode,d);if(best.Found)Use(best,1);
        }
        internal void Mana(Player p)
        {
            if(p.manaPotionDelay>0 || ManaFrame==host.Input.Frame || p.statMana>=p.statManaMax2)return;
            Item held=p.HeldItem;
            if(held==null || held.IsAir || held.mana<=0 || held.createTile>=0 || held.createWall>=0 || held.pick>0 || held.axe>0 || held.hammer>0 || held.fishingPole>0 || held.ammo>0 && held.useStyle==0 || held.shoot<=0 && held.damage<=0)return;
            bool skip,free;int raw;details(p,held,out skip,out raw,out free);
            // .8 ships ManaV2=true: PredictWithoutUse returns true even when
            // the payment is short (vanilla allows a slowed cast). It therefore
            // is NOT the replenishment predicate. Use the exact cost expression
            // from CheckMana, without executing its slowMagicUse/mana writes.
            int cost=(int)(raw*p.manaCost);
            if(skip || cost<=0 || p.statMana>=cost)return;
            // A potion cannot make an impossible next payment affordable. Do
            // not empty a bag repeatedly while holding that weapon.
            if(cost>p.statManaMax2)return;
            RecoverySource best=default(RecoverySource);int amount=0;Choose(p,2,ref best,ref amount,0,0);if(best.Found)Use(best,2);
        }
        private void Choose(Player p,int kind,ref RecoverySource best,ref int amount,int mode,int deficit)
        {
            bool voidOpen=p.useVoidBag();
            for(int a=0;a<2;a++)
            {
                if(a==1 && !voidOpen)break;int account=a==0?0:4;Item[] array=RecoverySource.AccountItems(p,account);int count=a==0?58:p.bank4.maxItems;
                for(int i=0;i<count;i++)
                {
#if DEBUG
                    CandidateReads++;
#endif
                    Item item=array[i];if(item==null || item.stack<=0 || item.type<=0)continue;
                    if(kind==1? !item.potion || item.healLife<=0 || !host.Potions.Value.LifeAllowed(item.type): item.healMana<=0 || p.potionDelay>0 && item.potion || !host.Potions.Value.ManaAllowed(item.type))continue;
                    if(host.Protected(p,array,account,i))continue;
                    Failure f=rejected[a*58+i];if(ReferenceEquals(f.Item,item) && f.Type==item.type && f.Stack==item.stack && f.Revision==host.Potions.Revision && f.Kind==kind)continue;
                    int h=kind==1?Healing(item):item.healMana;
                    if(kind==1? RecoveryRules.LifeScore(mode,deficit,p.statLifeMax2,h)>=0 && (!best.Found || RecoveryRules.BetterLife(mode,deficit,p.statLifeMax2,h,amount)):!best.Found || h>amount)
                    {best=host.Source(p,array,account,i,host.Potions.Revision);amount=h;}
                }
            }
        }
        internal bool Owns(Item[] array,int slot){return Kind!=0 && ReferenceEquals(array,current.Items) && slot==current.Slot;}
        internal Item Selection(Player p,int kind)
        {
            if(Kind!=kind || !ReferenceEquals(p,current.Player))return null;
            if(!current.Matches(host,true) || !host.Admit(p) || current.Revision!=host.Potions.Revision || !host.Potions.Ready ||
                kind==1 && (host.Value(0)==0 || !host.Potions.Value.LifeAllowed(current.Type)) || kind==2 && (host.Value(1)==0 || !host.Potions.Value.ManaAllowed(current.Type)))return null;
            return current.Item;
        }
        internal bool Use(RecoverySource source,int kind)
        {
            if(Kind!=0 || !source.Matches(host) || !host.Admit(source.Player))return false;
            Array.Clear(lease,0,5);lease[source.Account]=1UL<<source.Slot;
            if(!host.Items.Ownership.TryBeginRecovery(source.Session,lease,++nextToken))return false;
            token=nextToken;current=source;Kind=kind;Effect=false;bool unknown=false;
            try
            {
#if DEBUG
                NativeCalls++;
#endif
                if(kind==1)source.Player.QuickHeal();else source.Player.QuickMana();
                // No normal refusal can be retried against unchanged provider
                // facts. A changed stack/reference/config creates a new case.
                if(!Effect)rejected[(source.Account==0?0:58)+source.Slot]=new Failure{Item=source.Item,Type=source.Type,Stack=source.Item.stack,Revision=source.Revision,Kind=kind};
                if(!Effect && (source.Item.type!=source.Type || source.Item.stack!=source.Stack))unknown=true;
                return Effect;
            }
            catch{unknown=true;return false;}
            finally
            {
                if(unknown){host.UnknownSlots[source.Account]|=lease[source.Account];host.Report("药品使用结果未确认，已保护来源并停止重复使用。");}
                host.Items.Ownership.EndRecovery(source.Session,lease,token,unknown);Kind=0;current=default(RecoverySource);host.Items.World.InvalidateObservation();
            }
        }
        internal void Applied(Player player,Item item,int lifeBefore,int manaBefore)
        {
            if(!ReferenceEquals(player,host.Player))return;
            bool life=player.statLife>lifeBefore, mana=player.statMana>manaBefore;
            if(life)LifeFrame=host.Input.Frame;if(mana)ManaFrame=host.Input.Frame;
            if(Kind!=0 && ReferenceEquals(item,current.Item))Effect=life || mana;
        }
    }
}
