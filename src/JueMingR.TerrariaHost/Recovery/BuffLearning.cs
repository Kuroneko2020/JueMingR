using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Recovery
{
    // Only receipts inside a real manual-use/icon call can change the list.
    // Item identity is captured before the last stack turns to air. Hooks never
    // write files; the game-thread poll publishes one revision-checked change.
    internal sealed class BuffLearning
    {
        private readonly HostRecovery host;
        private readonly Dictionary<int,bool> pending=new Dictionary<int,bool>();
        private long revision=-1,session;
        private int scope,type,buff,icon=-1;
        private Item item;
        internal BuffLearning(HostRecovery host){this.host=host;}
        private bool Manual(Player p)
        {return host.Available && ReferenceEquals(p,host.Player) && host.Input.CanStartActions && host.Buffs.Ready && !host.BuffUse.Executing && host.PotionsUse.Kind==0 && !(host.IsQuickUse?.Invoke()??false);}
        internal int Begin(Player p,int kind)
        {
            int previous=scope;scope=0;
            if(!Manual(p) || !host.Buffs.Value.FollowAdd)return previous;
            if(kind==1)
            {
                if(p.controlUseItem && PlayerInput.Triggers.Current.MouseLeft && p.selectedItem>=0 && p.selectedItem<p.inventory.Length)Capture(p.inventory[p.selectedItem]);
                else if(p.itemAnimation==0 || item==null || !ReferenceEquals(item,p.inventory[p.selectedItem]))ClearSource();
                if(item!=null)scope=1;
            }
            else if(kind==2 && PlayerInput.Triggers.Current.QuickBuff || kind==3 && p.controlQuickHeal){scope=kind;ClearSource();}
            return previous;
        }
        internal void End(int previous){scope=previous;}
        internal void CaptureQuick(Item source){if(scope==2 || scope==3)Capture(source);}
        private void Capture(Item source)
        {ClearSource();if(!RecoveryCatalog.BuffCandidate(source))return;item=source;type=source.type;buff=source.buffType;session=host.Runtime.Generation;}
        private void ClearSource(){item=null;type=buff=0;}
        internal int BeforeAdd(Player p,int effect)
        {return scope!=0 && type>0 && session==host.Runtime.Generation && effect==buff && Manual(p)?Time(p,effect):-1;}
        internal void AfterAdd(Player p,int effect,int before)
        {
            if(before<0 || Time(p,effect)<=before || !Manual(p) || !host.Buffs.Value.FollowAdd)return;
            Queue(type,true);ClearSource();host.ObserveManual();
        }
        private static int Time(Player p,int effect){for(int i=0;i<Player.maxBuffs;i++)if(p.buffType[i]==effect && p.buffTime[i]>0)return p.buffTime[i];return 0;}
        internal int BeginIcon(int slot){int previous=icon;icon=host.Value(7)!=0 && host.Input.CanStartActions?slot:-1;return previous;}
        internal void EndIcon(int previous){icon=previous;}
        internal bool BeforeRemove(int slot,int effect)
        {var p=host.Player;return icon==slot && p!=null && Manual(p) && host.Buffs.Value.FollowRemove && slot>=0 && slot<Player.maxBuffs && p.buffType[slot]==effect && p.buffTime[slot]>0;}
        internal void AfterRemove(int effect,bool observed)
        {
            var p=host.Player;if(!observed || p==null || Time(p,effect)>0 || !host.Buffs.Ready)return;
            foreach(int allowed in host.Buffs.Value.AllowedBuffs)
            {Item definition;if(ContentSamples.ItemsByType.TryGetValue(allowed,out definition) && definition.buffType==effect)Queue(allowed,false);}
            host.ObserveManual();
        }
        private void Queue(int value,bool add)
        {
            if(value<=0 || value>=ItemID.Count)return;
            if(revision!=host.Buffs.Revision){pending.Clear();revision=host.Buffs.Revision;}
            pending[value]=add;
        }
        internal void Poll()
        {
            if(pending.Count==0)return;
            if(!host.Buffs.Ready || revision!=host.Buffs.Revision){pending.Clear();return;}
            var value=host.Buffs.Value;bool changed=false;
            foreach(var change in pending)if(value.BuffAllowed(change.Key)!=change.Value){value=value.ChangeType(2,change.Key,change.Value);changed=true;}
            pending.Clear();if(changed)host.Buffs.Set(value);
        }
        internal void Reset(){scope=0;icon=-1;ClearSource();pending.Clear();}
    }
}
