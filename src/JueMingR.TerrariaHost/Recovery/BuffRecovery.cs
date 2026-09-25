using System;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Recovery
{
    internal sealed class BuffRecovery
    {
        private readonly HostRecovery host;
        private readonly ulong[] lease=new ulong[5];
        private readonly Failure[] failures=new Failure[98];
        private RecoverySource source;
        private long token,revision=-1;
        private ulong nextScan;
        private bool attempted;
        internal bool Executing {get;private set;}
#if DEBUG
        internal long CandidateReads,NativeCalls,DefinitionReads;
#endif
        private struct Failure{internal Item Item;internal int Type,Stack,Mana;internal long Revision;}
        internal BuffRecovery(HostRecovery host){this.host=host;}
        internal void Reset(){Array.Clear(failures,0,failures.Length);nextScan=0;revision=-1;}
        // A Fairy Bell really produces any of these three effects. Other
        // same-group pets/foods are competitors, not the same learning source.
        internal static bool ProviderEffect(int a,int b){return a>0 && b>0 && (a==b || (a==27 || a==101 || a==102) && (b==27 || b==101 || b==102));}
        internal static bool SameEffect(int a,int b)
        {
            if(a<=0 || b<=0)return false;
            return ProviderEffect(a,b) ||
                BuffID.Sets.IsWellFed[a] && BuffID.Sets.IsWellFed[b] || Main.meleeBuff[a] && Main.meleeBuff[b] ||
                Main.lightPet[a] && Main.lightPet[b] || Main.vanityPet[a] && Main.vanityPet[b];
        }
        internal static bool Missing(Player p,int buff)
        {
            if(buff<=0 || buff>=p.buffImmune.Length || p.buffImmune[buff] || p.CountBuffs()>=Player.maxBuffs)return false;
            for(int i=0;i<Player.maxBuffs;i++)if(p.buffTime[i]>0 && SameEffect(buff,p.buffType[i]))return false;
            return true;
        }
        internal void Update(Player p,ulong tick)
        {
            var settings=host.Buffs;
            if(!settings.Ready || !settings.Value.Buffs || settings.Value.AllowedBuffs.Count==0)return;
            if(revision!=settings.Revision){revision=settings.Revision;nextScan=0;}
            if(tick<nextScan || p.spectating>=0 || p.CountBuffs()>=Player.maxBuffs)return;
            bool need=false;
            foreach(int type in settings.Value.AllowedBuffs)
            {
#if DEBUG
                DefinitionReads++;
#endif
                Item definition;if(ContentSamples.ItemsByType.TryGetValue(type,out definition) && RecoveryCatalog.BuffCandidate(definition) && Missing(p,definition.buffType)){need=true;break;}
            }
            // Stable satisfied/unknown definitions share the same bounded
            // observation cadence as missing stock; a setting edit invalidates it.
            if(!need){nextScan=tick+30;return;}
            for(int a=0;a<2;a++)
            {
                if(a==1 && !p.useVoidBag())break;int account=a==0?0:4;var items=RecoverySource.AccountItems(p,account);int count=a==0?58:p.bank4.maxItems;
                for(int i=0;i<count;i++)
                {
#if DEBUG
                    CandidateReads++;
#endif
                    Item item=items[i];if(!RecoveryCatalog.BuffCandidate(item) || !settings.Value.BuffAllowed(item.type) || !Missing(p,item.buffType) || TemporarilyBlocked(p,item) || host.Protected(p,items,account,i))continue;
                    var f=failures[a*58+i];if(ReferenceEquals(item,f.Item) && item.type==f.Type && item.stack==f.Stack && p.statMana==f.Mana && f.Revision==settings.Revision)continue;
                    Use(host.Source(p,items,account,i,settings.Revision));return;
                }
            }
            nextScan=tick+30;
        }
        internal bool Owns(Item[] items,int slot){return Executing && ReferenceEquals(items,source.Items) && slot==source.Slot;}
        // Cooldown and silence are known temporary native gates, not a failed
        // attempt. Do not poison a stable provider when those gates later clear.
        private static bool TemporarilyBlocked(Player p,Item item){return item.potion && p.potionDelay>0 || item.mana>0 && p.silence;}
        private bool Valid(Player p)
        {return Executing && ReferenceEquals(p,source.Player) && source.Matches(host,true) && host.Buffs.Ready && host.Buffs.Revision==source.Revision && host.Value(4)!=0 && host.Buffs.Value.BuffAllowed(source.Type) && !TemporarilyBlocked(p,source.Item) && Missing(p,source.Item.buffType);}
        internal Item Food(Player p)
        {if(attempted || !Valid(p) || !BuffID.Sets.IsWellFed[source.Item.buffType])return null;attempted=true;return source.Item;}
        internal bool Permit(Player p,Item item)
        {if(attempted || !ReferenceEquals(item,source.Item) || !Valid(p) || BuffID.Sets.IsWellFed[item.buffType])return false;attempted=true;return true;}
        internal bool AllowsPet(Player p,Item item){return attempted && ReferenceEquals(item,source.Item) && Valid(p);}
        private void Use(RecoverySource candidate)
        {
            if(!candidate.Matches(host) || !host.AdmitBuff(candidate.Player))return;
            Array.Clear(lease,0,5);lease[candidate.Account]=1UL<<candidate.Slot;
            if(!host.Items.Ownership.TryBeginRecovery(candidate.Session,lease,++token))return;
            source=candidate;Executing=true;attempted=false;bool unknown=false;int buff=source.Item.buffType;
            try
            {
#if DEBUG
                NativeCalls++;
#endif
                source.Player.QuickBuff();
                bool exists=false;for(int i=0;i<Player.maxBuffs;i++)if(source.Player.buffTime[i]>0 && SameEffect(buff,source.Player.buffType[i])){exists=true;break;}
                if(!exists)
                {failures[(source.Account==0?0:58)+source.Slot]=new Failure{Item=source.Item,Type=source.Type,Stack=source.Item.stack,Mana=source.Player.statMana,Revision=source.Revision};unknown=source.Item.type!=source.Type || source.Item.stack!=source.Stack;}
            }
            catch{unknown=true;}
            finally
            {
                if(unknown){host.UnknownSlots[source.Account]|=lease[source.Account];host.Report("增益使用结果未确认，已保护来源；其它独立增益仍可继续。");}
                host.Items.Ownership.EndRecovery(source.Session,lease,token,unknown);Executing=false;source=default(RecoverySource);host.Items.World.InvalidateObservation();
            }
        }
    }
}
