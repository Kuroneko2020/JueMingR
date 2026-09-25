using System;
using JueMingR.Features.QuickItems;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.QuickItems
{
    // No Item snapshots and no physical moves. Native SelectedItemState owns
    // the override and buffered manual selection; this owner borrows one pulse.
    internal sealed class QuickItemUse
    {
        private readonly HostQuickItems host;
        private readonly QuickItemCandidate[] candidates=new QuickItemCandidate[50];
        private Player player;
        private Item provider;
        private QuickItemEntry entry;
        private QuickItemChoice choice;
        private long nextToken, token, session, admittedFrame;
        private int original;
        private bool selected, pulsed, checkedItem, cancelled, started, ownsMouse;
        internal bool InNativeUse { get; private set; }
        internal bool Active { get { return player!=null; } }
        internal int Slot { get { return Active ? choice.Slot : -1; } }
        internal long Operation { get { return token; } }
        internal QuickItemUse(HostQuickItems host) { this.host=host; }
        internal void TryStart(QuickItemEntry requested)
        {
            Player current=host.Player;
            // Dispatch precedes Player.ResetControls/CopyInto. Read this frame's
            // residual actions, not last frame's controlTorch/controlUseTile.
            if (Active || current==null || !host.Input.CanStartActions || host.CanGameplay==null || !host.CanGameplay() ||
                !current.selectedItemState.CanChangeSelectedItemImmediately || current.CCed || current.noItems || current.isOperatingAnotherEntity ||
                current.HasLockedInventory() || Main.LocalPlayerHasPendingInventoryActions() || host.Items.World.Busy ||
                PlayerInput.Triggers.Current.MouseRight || PlayerInput.Triggers.Current.SmartSelect || PlayerInput.Triggers.Current.MouseLeft || Main.mouseLeft || !Main.mouseItem.IsAir)
            { host.Feedback(HostQuickItems.FeedbackKind.NotExecuted,notify:false); return; }
            for(int i=0;i<50;i++) { Item item=current.inventory[i]; candidates[i]=item==null ? default(QuickItemCandidate) :
                new QuickItemCandidate(i,item.type,item.stack,item.useStyle!=0,host.Items.Ownership.IsProtected(i)); }
            choice=QuickItemRules.Choose(requested,candidates,current.selectedItem);
            if(!choice.Found) {host.Feedback(HostQuickItems.FeedbackKind.NotExecuted,"背包中没有可用的目标物品。");return;}
            long next=++nextToken;
            if(!host.Items.Ownership.TryBeginUse(host.Runtime.Generation,choice.Slot,next)) {host.Feedback(HostQuickItems.FeedbackKind.NotExecuted,"目标物品正在被其他操作使用。");return;}
            token=next;session=host.Runtime.Generation;admittedFrame=host.Input.Frame;player=current;provider=current.inventory[choice.Slot];entry=requested;
            selected=pulsed=checkedItem=cancelled=started=false;original=current.selectedItem;
        }
        private void Transform()
        {
            int steps=0;
            while(provider.type!=choice.TargetType && steps++<4)
            {
                int next=QuickItemRules.NextState(provider.type);
                if(next==0 || provider.stack!=1)throw new InvalidOperationException("Native state edge unavailable.");
                provider.ChangeItemType(next);
            }
            if(provider.type!=choice.TargetType)throw new InvalidOperationException("Native state edge did not reach target.");
        }
        private bool StillAdmitted {get{return admittedFrame==host.Input.Frame && !Main.gamePaused && host.Input.CanStartActions &&
            host.CanGameplay!=null && host.CanGameplay() && host.Settings.CanExecute(entry.Id);}}
        internal void Pick(Player current,ref int slot,ref bool result)
        {
            if(!ReferenceEquals(current,player))return;
            if(selected)
            {
                // Called only after native Update has applied the newest buffer
                // or its own override return. Never replace that decision.
                Retire();
                return;
            }
            if(cancelled || result || !StillAdmitted || !ReferenceEquals(current.inventory[choice.Slot],provider) ||
                provider.type!=choice.OriginalType || provider.stack<=0 || !Main.mouseItem.IsAir)
            {host.Feedback(HostQuickItems.FeedbackKind.NotExecuted,notify:false);Retire();return;}
            try { Transform(); }
            catch {host.Feedback(HostQuickItems.FeedbackKind.Error,"快捷物品形态转换未确认，已停止本次操作。",entry.Id);Retire();return;}
            if(!choice.Use)
            {host.Feedback(HostQuickItems.FeedbackKind.Success,id:entry.Id);Retire();return;}
            original=current.selectedItem;selected=true;slot=choice.Slot;result=true;
        }
        internal void BeforeSync(Player current)
        {
            // A later physical use press takes over. The native buffered return
            // stops repeating our provider without clearing the player's input.
            if(ReferenceEquals(current,player) && selected && pulsed && PlayerInput.Triggers.Current.MouseLeft)Cancel();
            if(!ReferenceEquals(current,player) || !selected || pulsed || checkedItem)return;
            if(cancelled || current.selectedItem!=choice.Slot || current.CCed || !StillAdmitted ||
                !ReferenceEquals(current.inventory[choice.Slot],provider)) {Cancel();return;}
            // This stage precedes vanilla packet 13 and normal ItemCheck. The
            // following native ResetControls/CopyInto releases the synthetic bit.
            current.controlUseItem=true;current.releaseUseItem=true;pulsed=true;
        }
        internal bool BeginItemCheck(Player current,out bool borrowedMouse)
        {
            borrowedMouse=false;
            if(!ReferenceEquals(current,player) || !selected || current.selectedItem!=choice.Slot)return false;
            InNativeUse=true;
            if(pulsed && !checkedItem && !Main.mouseLeft) {borrowedMouse=ownsMouse=true;Main.mouseLeft=true;}
            return true;
        }
        internal void Started(Player current) {if(ReferenceEquals(current,player) && InNativeUse)started=true;}
        internal void EndItemCheck(Player current,long operation,bool entered,bool borrowedMouse,Exception error)
        {
            if(operation!=token || !ReferenceEquals(current,player))return;
            if(borrowedMouse)ReleaseMouse();
            if(!entered || !ReferenceEquals(current,player))return;
            InNativeUse=false;checkedItem=true;
            // Do not buffer a return during ordinary use: native burst weapons
            // consult HasBufferedChange and would lose their final projectiles.
            // The one-frame pulse releases through normal mapping; native
            // SelectedItemState returns its override when animation/channel/
            // reuse completes. Explicit cancellation still uses its buffer.
            if(error!=null) {cancelled=true;host.Feedback(HostQuickItems.FeedbackKind.Error,"快捷物品使用中断，已停止本次操作。",entry.Id);}
            else if(!started)host.Feedback(HostQuickItems.FeedbackKind.NotExecuted,"原版未开始本次使用。请确认物品的使用条件。",entry.Id);
            else host.Feedback(HostQuickItems.FeedbackKind.Success,id:entry.Id);
        }
        private void ReturnSelection()
        {
            if(player!=null && player.selectedItemState.HasActiveOverride && !player.selectedItemState.HasBufferedChange)
                player.selectedItemState.Select(original);
        }
        internal void CancelEntry(string id) {if(entry!=null && entry.Id==id)Cancel();}
        // Shared dispatch precedes the next native selection update. Do not
        // lose a fresh B edge after A has actually finished on the prior frame.
        internal void RetireCompleted()
        {if(Active && selected && checkedItem && player.selectedItemState.CanChangeSelectedItemImmediately)Retire();}
        internal void Cancel() {cancelled=true;ReturnSelection();if(!selected)Retire();}
        internal void Retire()
        {
            if(player==null)return;
            ReleaseMouse();
            ReturnSelection();
            if(pulsed && admittedFrame==host.Input.Frame)
            {
                // A boundary can follow ItemCheck in the very same input
                // frame. Completion of ItemCheck does not release controlUseItem;
                // only the next native mapping does. Never edit a later frame.
                player.controlUseItem=PlayerInput.Triggers.Current.MouseLeft;
                player.releaseUseItem=!player.controlUseItem;
            }
            host.Items.Ownership.EndUse(session,token);
            player=null;provider=null;entry=null;InNativeUse=false;token=0;
        }
        private void ReleaseMouse() {if(ownsMouse){if(Main.mouseLeft)Main.mouseLeft=false;ownsMouse=false;}}
    }
}
