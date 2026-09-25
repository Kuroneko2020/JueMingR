using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using JueMingR.TerrariaHost.Npcs;

namespace JueMingR.TerrariaHost.Recovery
{
    internal sealed class NurseRecovery
    {
        private readonly HostRecovery host;
        internal readonly NearbyServiceTarget Target;
        private readonly ShopHelper pricing=new ShopHelper();
        private readonly NursePayment payment;
        private ulong next;
        private long token,settingsRevision;
        private bool unknown,refused,zeroPrice,paid;
        private int expectedCost,payCalls;
        internal bool Executing {get;private set;}
#if DEBUG
        internal long Quotes,NativeCalls;
        internal Exception LastFailure;
#endif
        internal NurseRecovery(HostRecovery host,NativeNpcObservation npcs){this.host=host;Target=new NearbyServiceTarget(npcs,18);payment=new NursePayment(host);}
        internal void Reset(){unknown=refused=zeroPrice=false;next=0;Target.Reset();}
        internal void Rearm(){refused=zeroPrice=false;next=0;}
        internal static bool Need(Player p)
        {if(p.statLife<p.statLifeMax2)return true;for(int i=0;i<Player.maxBuffs;i++){int b=p.buffType[i];if(b>0 && b<BuffID.Count && p.buffTime[i]>60 && Main.debuff[b] && !BuffID.Sets.NurseCannotRemoveDebuff[b])return true;}return false;}
        internal void Update(Player p,ulong tick)
        {
            if(unknown || !Need(p))return;
            if(refused){if(!Target.Valid(p))refused=false;else return;}
            if(tick<next)return;next=tick+60;
            NPC target=Target.Find(p,tick);if(target==null)return;
            int cost;long funds;
            try
            {
                var quote=pricing.GetShoppingSettings(p,target);var previous=p.currentShoppingSettings;long dialogVersion=host.Dialog.Changes;
                try{p.currentShoppingSettings=quote;cost=Main.GetNurseHealCost();}
                finally{if(dialogVersion==host.Dialog.Changes && p.currentShoppingSettings.Equals(quote))p.currentShoppingSettings=previous;}
#if DEBUG
                Quotes++;
#endif
                if(cost<0 || !payment.Balance(p,out funds)){refused=true;host.Report("护士费用或余额无法确认，未执行治疗。");return;}
            }
            catch{refused=true;host.Report("护士费用无法确认，未执行治疗。");return;}
            if(cost>funds || zeroPrice && cost==0)return;
            if(!host.Admit(p) || !Target.Valid(p) || !Need(p))return;
            bool lease=cost>0;long generation=host.Runtime.Generation,current=++token;
            if(lease && (!payment.Capture(p) || !host.Items.Ownership.TryBeginRecovery(generation,payment.Slots,current)))return;
            bool callStarted=false,completed=false;paid=false;payCalls=0;settingsRevision=host.Services.Revision;
            try
            {
                if(!host.Dialog.Open(p,Target) || !host.Dialog.Owns || !Target.Valid(p) || !Need(p) || lease && !payment.Unchanged(p))return;
                expectedCost=Main.GetNurseHealCost();
                if(expectedCost<0 || !payment.Balance(p,out funds) || funds<expectedCost || expectedCost>0 && !lease)return;
                Executing=true;callStarted=true;
#if DEBUG
                NativeCalls++;
#endif
                Main.NPCChatText_DoNurseHeal(expectedCost);
                long after;
                if(expectedCost==0){zeroPrice=true;return;}
                if(!payment.Balance(p,out after)){unknown=true;return;}
                if(paid && payCalls==1 && funds-after==expectedCost && !Need(p)){completed=true;host.Dialog.Success();}
                else if(!paid && funds==after)refused=true;
                else unknown=true;
            }
#if DEBUG
            catch(Exception error)
#else
            catch(Exception)
#endif
            {
#if DEBUG
                LastFailure=error;
#endif
                if(callStarted && !completed)unknown=true;else if(!completed)refused=true;host.Report(completed?"护士治疗已完成，对话关闭未完成。":"护士服务未完成，保留原版对话；不会自动重复本次治疗。");}
            finally
            {
                Executing=false;
                if(lease){if(unknown)for(int a=0;a<5;a++)host.UnknownSlots[a]|=payment.Slots[a];host.Items.Ownership.EndRecovery(generation,payment.Slots,current,unknown);}
                if(unknown)host.Report("护士扣款或治疗结果未确认，已保护涉及的账户；不会重新扣款。");
                host.Items.World.InvalidateObservation();
            }
        }
        internal bool BeforePayment(Player p,long cost,int currency)
        {
            // Opening/quoting may re-enter other hooks. Only the still-owned
            // target and unchanged physical accounts may reach the debit.
            return ReferenceEquals(p,host.Player) && host.Dialog.Owns && Target.Valid(p) && Need(p) &&
                host.Services.Ready && host.Services.Revision==settingsRevision && host.Services.Value.Nurse &&
                host.Input.CanStartActions && !Main.gamePaused && !Main.mapFullscreen && !Main.ServerSideCharacter &&
                !p.dead && !p.CCed && !p.noItems && !p.HasLockedInventory() && !Main.LocalPlayerHasPendingInventoryActions() &&
                payment.Unchanged(p) && currency==-1 && cost==expectedCost && ++payCalls==1;
        }
        internal void Paid(bool success){paid=success;}
    }
}
