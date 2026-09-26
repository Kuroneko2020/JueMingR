using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Tools
{
    internal enum ToolKind { Capture, Harvest, Seed, Mining, Recast }
    internal sealed class ToolIntent
    {
        internal ToolKind Kind;
        internal int Slot;
        internal Vector2 Target;
        internal Func<bool> Valid;
        internal Action Admitted;
        internal Action<bool,bool> Completed;
    }
    // One native animation, not a business scheduler. Selection belongs to
    // vanilla; this lease only contributes a pulse and an owned aim scope.
    internal sealed class ToolUse
    {
        private readonly HostTools host;
        private Player player;
        private Item item,originalItem;
        private int type,original,stack,originalType;
        private long token,session,pulseFrame,selection;
        private bool pulsed,checkedItem,started,cancelled,borrowed,notified;
        private int oldX,oldY,oldTileX,oldTileY,ownX,ownY,ownTileX,ownTileY;
        private bool oldMouse;
        private Projectile drill;
        private int drillKey;
        internal ToolIntent Intent {get;private set;}
        internal bool InNativeUse {get;private set;}
        internal bool Returning {get;private set;}
        internal bool Active {get{return player!=null;}}
        internal long Operation {get{return token;}}
        internal bool ActionValid {get{return Active && !cancelled && Identity() && host.Admit(player,Intent.Kind==ToolKind.Capture && host.Mode(0)==2) && (Intent.Valid?.Invoke()??false);}}
        internal ToolUse(HostTools host){this.host=host;}
        internal void Pick(Player p,ref int chosen,ref bool result)
        {
            if(!ReferenceEquals(p,host.Player))return;
            if(Active){Retire();return;} // Native Update has already returned it.
            if(result || host.Input.Frame<host.NextUseFrame || host.ManualSelectionFrame==host.Input.Frame)return;
            ToolIntent candidate=host.Choose(p);if(candidate==null)return;
            if(candidate.Slot<0 || candidate.Slot>=50 || !(candidate.Valid?.Invoke()??false))return;
            long next=host.Items.Ownership.NewUseToken();
            if(!host.Items.Ownership.TryBeginUse(host.Runtime.Generation,candidate.Slot,next))return;
            player=p;Intent=candidate;item=p.inventory[candidate.Slot];type=item.type;stack=item.stack;original=p.selectedItem;
            originalItem=original>=0 && original<50?p.inventory[original]:null;originalType=originalItem?.type??0;
            token=next;session=host.Runtime.Generation;selection=host.SelectionIntent;
            pulsed=checkedItem=started=cancelled=notified=false;
            candidate.Admitted?.Invoke();
            chosen=candidate.Slot;result=true;
        }
        private bool Identity(){return session==host.Runtime.Generation && host.SelectionIntent==selection && player.selectedItem==Intent.Slot && ReferenceEquals(player.inventory[Intent.Slot],item) && item.type==type && item.stack>0;}
        // Selecting a temporary tool must not make the original held source
        // available to automatic sell/store/discard. This observation-only
        // guard never blocks a real manual move or a newer selection intent.
        internal bool ProtectOriginal(Item source)
        {return Active && ReferenceEquals(player,host.Player) && session==host.Runtime.Generation && selection==host.SelectionIntent && originalItem!=null && ReferenceEquals(source,originalItem) && ReferenceEquals(player.inventory[original],source) && source.type==originalType && source.stack>0 && player.selectedItemState.HasActiveOverride;}
        private void HandToManual(){Cancel();host.Items.Ownership.EndUse(session,token);}
        internal void BeforeSync(Player p)
        {
            if(!ReferenceEquals(p,player))return;
            if(PlayerInput.Triggers.Current.MouseLeft || PlayerInput.Triggers.Current.MouseRight || PlayerInput.Triggers.Current.SmartSelect){HandToManual();return;}
            if(!ActionValid){Cancel();return;}
            if(pulsed || checkedItem)return;
            p.controlUseItem=true;p.releaseUseItem=true;pulseFrame=host.Input.Frame;pulsed=true;
        }
        internal void Begin(Player p)
        {
            if(!ReferenceEquals(p,player) || p.selectedItem!=Intent.Slot)return;
            // Cleanup responsibility may outlive automatic intent. A genuine
            // manual press owns this ItemCheck, including during a Boss pause.
            if(PlayerInput.Triggers.Current.MouseLeft || host.SelectionIntent!=selection){HandToManual();return;}
            InNativeUse=true;
            if(!ActionValid){Cancel();return;}
            // Establish cleanup ownership BEFORE the first temporary write.
            BorrowAim();
            if(pulsed && !checkedItem)Main.mouseLeft=true;
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
        {return Active && !cancelled && ReferenceEquals(drill,projectile) && projectile.active && (int)projectile.key==drillKey && Identity() && !PlayerInput.Triggers.Current.MouseLeft && host.Admit(player,false);}
        // The original projectile AI runs after ItemCheck. It only positions
        // the drill; retain this operation's submitted aim even if its tile was
        // just removed, without leasing another projectile or a later gesture.
        internal void BeginProjectile(){BorrowAim();}
        internal void EndProjectile(long operation,Exception error)
        {if(operation!=token)return;Restore();if(error!=null){host.HoldUnknown(Intent.Slot);Notify(true);Cancel();}}
        internal void Started(Player p){if(ReferenceEquals(p,player) && InNativeUse)started=true;}
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
        }
        internal bool Owns(Item[] array,int slot){return InNativeUse && ReferenceEquals(array,player?.inventory) && Intent!=null && slot==Intent.Slot && ReferenceEquals(array[slot],item);}
        internal bool Is(ToolKind kind){return InNativeUse && Intent!=null && Intent.Kind==kind;}
        internal void Update()
        {
            if(!Active)return;
            if(!Identity() || !host.Admit(player,Intent.Kind==ToolKind.Capture && host.Mode(0)==2))Cancel();
            if(checkedItem && player.selectedItemState.CanChangeSelectedItemImmediately)Retire();
        }
        private void Notify(bool unknown){if(notified)return;notified=true;Intent?.Completed?.Invoke(started,unknown);}
        internal void Cancel()
        {
            if(!Active)return;cancelled=true;
            if(player.selectedItemState.HasActiveOverride && !player.selectedItemState.HasBufferedChange && host.SelectionIntent==selection)
            {Returning=true;try{player.selectedItemState.Select(original);}finally{Returning=false;}}
            if(pulseFrame==host.Input.Frame && pulsed){player.controlUseItem=PlayerInput.Triggers.Current.MouseLeft;player.releaseUseItem=!player.controlUseItem;}
        }
        internal void Retire()
        {
            if(!Active)return;Restore();Cancel();Notify(false);
            host.Items.Ownership.EndUse(session,token);host.NextUseFrame=host.Input.Frame+2;
            player=null;item=originalItem=null;drill=null;Intent=null;token=0;InNativeUse=false;
        }
        private void Restore()
        {
            if(!borrowed)return;borrowed=false;
            if(Main.mouseX==ownX)Main.mouseX=oldX;if(Main.mouseY==ownY)Main.mouseY=oldY;
            if(Player.tileTargetX==ownTileX)Player.tileTargetX=oldTileX;if(Player.tileTargetY==ownTileY)Player.tileTargetY=oldTileY;
            if(!oldMouse && Main.mouseLeft && pulsed && !checkedItem)Main.mouseLeft=false;
        }
    }
}
