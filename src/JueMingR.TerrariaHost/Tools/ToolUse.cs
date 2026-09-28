using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Tools
{
    internal enum ToolKind { Capture, Harvest, Seed, Mining, Recast, FishingPull, FishingCast, FishingCut }
    internal sealed class ToolIntent
    {
        internal ToolKind Kind;
        internal int Slot;
        internal Vector2 Target;
        internal Func<bool> Valid;
        internal Func<bool> Refresh;
        internal Action Admitted;
        internal Action Used;
        internal Action Yielded;
        internal Action<bool,bool> Completed;
        internal bool SelectionOnly;
        internal bool HeldInventory;
    }
    // The target, sustained business intent and actual use lease have distinct
    // lifetimes. Only the feature refreshes targets; vanilla owns selection,
    // animation and tool timing, including consecutive legal uses.
    internal sealed class ToolUse
    {
        private readonly HostTools host;
        private Player player;
        private Item item,originalItem;
        private int type,original,stack,originalType;
        private long token,session,pulseFrame,selection,nextContention;
        private bool pulsed,checkedItem,started,cancelled,borrowed,notified,yieldExternal,returnRequested;
        private int oldX,oldY,oldTileX,oldTileY,ownX,ownY,ownTileX,ownTileY;
        private bool oldMouse;
        private Projectile drill;
        private int drillKey;
        internal ToolIntent Intent {get;private set;}
        internal bool InNativeUse {get;private set;}
        internal bool Returning {get;private set;}
        internal bool Active {get{return player!=null;}}
        internal long Operation {get{return token;}}
        internal bool CompletedAnimation {get{return Active && started && player.itemAnimation<=0;}}
        private bool HeldInventory {get{return Intent.HeldInventory || Intent.Kind==ToolKind.Capture && host.Mode(0)==2;}}
        // Only G10 automatic fishing pauses with F5. New tool kinds and the
        // G09 borrowed-net return must not inherit this feature-specific gate.
        private bool CanRunFeature {get{return (Intent.Kind!=ToolKind.FishingPull && Intent.Kind!=ToolKind.FishingCast && Intent.Kind!=ToolKind.FishingCut) || (host.CanFishingInterface?.Invoke()??true);}}
        private bool Admitted(Player p){return CanRunFeature && host.Admit(p,HeldInventory);}
        internal bool ActionValid {get{return Active && !cancelled && Identity() && Admitted(player) && (Intent.Valid?.Invoke()??false);}}
        internal ToolUse(HostTools host){this.host=host;}
        internal void Pick(Player p,ref int chosen,ref bool result)
        {
            if(!ReferenceEquals(p,host.Player))return;
            if(Active)
            {
                // Vanilla has restored its original selection before this
                // callback. Reapplying the same override avoids a spurious
                // selection change between animations; no timer is rewritten.
                if(!result && !cancelled && Intent.Refresh!=null && SourceIdentity() &&
                    host.ManualSelectionFrame!=host.Input.Frame && !host.ManualLeft &&
                    Admitted(p) && Intent.Refresh())
                {chosen=Intent.Slot;result=true;return;}
                bool handoff=yieldExternal;Retire();if(handoff)return;
            }
            if(result || host.Input.Frame<host.NextUseFrame || host.ManualSelectionFrame==host.Input.Frame)return;
            ToolIntent candidate=host.Choose(p);if(candidate==null)return;
            if(Acquire(p,candidate)){chosen=candidate.Slot;result=true;}
        }
        private bool Acquire(Player p,ToolIntent candidate)
        {
            if(candidate.Slot<0 || candidate.Slot>=50 || !(candidate.Valid?.Invoke()??false))return false;
            long next=host.Items.Ownership.NewUseToken();
            if(!host.Items.Ownership.TryBeginUse(host.Runtime.Generation,candidate.Slot,next))return false;
            player=p;Intent=candidate;item=p.inventory[candidate.Slot];type=item.type;stack=item.stack;original=p.selectedItem;
            originalItem=original>=0 && original<50?p.inventory[original]:null;originalType=originalItem?.type??0;
            token=next;session=host.Runtime.Generation;selection=host.SelectionIntent;
            nextContention=host.Input.Frame+Math.Max(1,item.useAnimation);
            pulsed=checkedItem=started=cancelled=notified=false;
            yieldExternal=returnRequested=false;
            candidate.Admitted?.Invoke();
            return true;
        }
        private bool SourceIdentity(){return session==host.Runtime.Generation && host.SelectionIntent==selection && ReferenceEquals(player.inventory[Intent.Slot],item) && item.type==type && (item.stack>0 || Intent.SelectionOnly && item.IsAir);}
        private bool Identity(){return SourceIdentity() && player.selectedItem==Intent.Slot;}
        private bool Refresh(){return Active && !cancelled && Identity() && (Intent.Refresh==null || Intent.Refresh()) && ActionValid;}
        // Selecting a temporary tool must not make the original held source
        // available to automatic sell/store/discard. This observation-only
        // guard never blocks a real manual move or a newer selection intent.
        internal bool ProtectOriginal(Item source)
        {return Active && ReferenceEquals(player,host.Player) && session==host.Runtime.Generation && selection==host.SelectionIntent && originalItem!=null && ReferenceEquals(source,originalItem) && ReferenceEquals(player.inventory[original],source) && source.type==originalType && source.stack>0 && player.selectedItemState.HasActiveOverride;}
        private void HandToManual(){Cancel();host.Items.Ownership.EndUse(session,token);}
        internal void BeforeSync(Player p)
        {
            // A genuine first hit established this automatic region. After
            // release the same held tool can keep its current animation; it
            // needs no selection change and must not wait for the next swing.
            // All consumers still use this one lease and the native timers.
            if(!Active && ReferenceEquals(p,host.Player) && host.Mode(2)==2 &&
                !p.selectedItemState.CanChangeSelectedItemImmediately && !p.selectedItemState.HasActiveOverride && !p.selectedItemState.HasBufferedChange &&
                host.Input.Frame>=host.NextUseFrame && host.ManualSelectionFrame!=host.Input.Frame &&
                !host.ManualLeft && !host.ManualRight && !PlayerInput.Triggers.Current.SmartSelect)
            {
                ToolIntent candidate=host.Mining.Choose(p);
                if(candidate!=null && candidate.Slot==p.selectedItem)Acquire(p,candidate);
            }
            if(!ReferenceEquals(p,player))return;
            if(host.ManualLeft || host.ManualRight || PlayerInput.Triggers.Current.SmartSelect){HandToManual();return;}
            if(!Refresh()){Cancel();return;}
            // A cut borrows selection only. No virtual use press is produced;
            // vanilla projectile AI observes the temporary non-rod itself.
            if(Intent.SelectionOnly)return;
            if(Intent.Refresh==null && (pulsed || checkedItem))return;
            p.controlUseItem=true;if(!pulsed)p.releaseUseItem=true;pulseFrame=host.Input.Frame;pulsed=true;
        }
        internal void Begin(Player p)
        {
            if(!ReferenceEquals(p,player) || p.selectedItem!=Intent.Slot)return;
            // Cleanup responsibility may outlive automatic intent. A genuine
            // manual press owns this ItemCheck, including during a Boss pause.
            if(host.ManualLeft || host.SelectionIntent!=selection){HandToManual();return;}
            InNativeUse=true;
            if(!Refresh()){Cancel();return;}
            if(Intent.SelectionOnly)return;
            // Establish cleanup ownership BEFORE the first temporary write.
            BorrowAim();
            if(pulsed && (Intent.Refresh!=null || !checkedItem))Main.mouseLeft=true;
        }
        private void BorrowAim()
        {
            oldX=Main.mouseX;oldY=Main.mouseY;oldMouse=Main.mouseLeft;oldTileX=Player.tileTargetX;oldTileY=Player.tileTargetY;
            Vector2 screen=Main.ReverseGravitySupport(Intent.Target-Main.screenPosition);
            ownX=(int)screen.X;ownY=(int)screen.Y;ownTileX=(int)(Intent.Target.X/16);ownTileY=(int)(Intent.Target.Y/16);borrowed=true;
            Main.mouseX=ownX;Main.mouseY=ownY;Player.tileTargetX=ownTileX;Player.tileTargetY=ownTileY;
        }
        internal void ObserveProjectile(Player p,Projectile projectile)
        {if(ReferenceEquals(p,player) && Is(ToolKind.Mining) && projectile.owner==p.whoAmI && projectile.type==item.shoot && (projectile.aiStyle==20 || projectile.type==445)){drill=projectile;drillKey=(int)projectile.key;}}
        internal bool OwnsProjectile(Projectile projectile)
        {return Active && !cancelled && ReferenceEquals(drill,projectile) && projectile.active && (int)projectile.key==drillKey && Identity() && !host.ManualLeft && host.Admit(player,false);}
        // The original projectile AI runs after ItemCheck. It only positions
        // the drill; retain this operation's submitted aim even if its tile was
        // just removed, without leasing another projectile or a later gesture.
        internal void BeginProjectile(){BorrowAim();}
        // Zero is the idle sentinel, never a receipt; stale callbacks cannot
        // borrow cancellation or unknown-source protection from another use.
        internal void EndProjectile(long operation,Exception error)
        {if(operation<=0 || operation!=token || !Active)return;Restore();if(error!=null){host.HoldUnknown(Intent.Slot);Notify(true);Cancel();}}
        internal void Started(Player p){if(ReferenceEquals(p,player) && InNativeUse){started=true;Intent.Used?.Invoke();}}
        internal void End(Player p,long operation,Exception error)
        {
            if(operation==0 || operation!=token || !ReferenceEquals(p,player))return;
            Restore();bool entered=InNativeUse;InNativeUse=false;if(!entered)return;checkedItem=true;
            if(error!=null)
            {
                // An interrupted consumer is not a rollback. Retain only the
                // real source until a genuinely new player/world connection.
                host.HoldUnknown(Intent.Slot);Notify(true);Cancel();host.Report("工具操作结果未确认，已停止；相关物品暂受保护。");
            }
            else if(item.stack>stack || item.stack<stack-1 || !ReferenceEquals(p.inventory[Intent.Slot],item) && !p.inventory[Intent.Slot].IsAir)
            {host.HoldUnknown(Intent.Slot);Notify(true);Cancel();}
            if(!cancelled && Intent.Refresh!=null && (host.Input.Frame>=nextContention || (host.Combat?.Handoff.ResumeWanted??false)))
            {
                nextContention=host.Input.Frame+Math.Max(1,p.itemAnimationMax);
                bool external;if(host.Contended(Intent.Kind,out external)){yieldExternal=external;Intent.Yielded?.Invoke();Cancel();}
            }
        }
        internal bool Owns(Item[] array,int slot){return InNativeUse && ReferenceEquals(array,player?.inventory) && Intent!=null && slot==Intent.Slot && ReferenceEquals(array[slot],item);}
        internal bool Is(ToolKind kind){return InNativeUse && Intent!=null && Intent.Kind==kind;}
        internal void Update()
        {
            if(!Active)return;
            if(!Identity() || !host.CanRetainUse(player,HeldInventory) || !CanRunFeature)Cancel();
            // Select only queues the original slot. Unsampled outer updates
            // must retain this exact lease until native selection consumes it;
            // otherwise fishing mistakes our own temporary slot for manual exit.
            // A newer manual intent or changed source immediately loses this right.
            if(returnRequested && SourceIdentity() && player.selectedItem!=original && player.selectedItemState.HasBufferedChange)return;
            if(checkedItem && (cancelled || Intent.Refresh==null) && player.selectedItemState.CanChangeSelectedItemImmediately)Retire();
        }
        private void Notify(bool unknown){if(notified)return;notified=true;Intent?.Completed?.Invoke(started,unknown);}
        internal void Cancel()
        {
            if(!Active)return;cancelled=true;
            if(player.selectedItemState.HasActiveOverride && !player.selectedItemState.HasBufferedChange && host.SelectionIntent==selection)
            {Returning=true;try{player.selectedItemState.Select(original);returnRequested=true;}finally{Returning=false;}}
            if(pulseFrame==host.Input.Frame && pulsed){player.controlUseItem=host.ManualLeft;player.releaseUseItem=!player.controlUseItem;}
        }
        internal void Retire()
        {
            if(!Active)return;Restore();Cancel();Notify(false);
            host.Items.Ownership.EndUse(session,token);
            player=null;item=originalItem=null;drill=null;Intent=null;token=0;InNativeUse=false;
        }
        private void Restore()
        {
            if(!borrowed)return;borrowed=false;
            if(Main.mouseX==ownX)Main.mouseX=oldX;if(Main.mouseY==ownY)Main.mouseY=oldY;
            if(Player.tileTargetX==ownTileX)Player.tileTargetX=oldTileX;if(Player.tileTargetY==ownTileY)Player.tileTargetY=oldTileY;
            if(!oldMouse && Main.mouseLeft && pulsed)Main.mouseLeft=false;
        }
    }
}
