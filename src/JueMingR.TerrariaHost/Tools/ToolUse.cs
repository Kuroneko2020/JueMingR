using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
#if JMR_BLUEPRINT_DIAGNOSTIC
using JueMingR.TerrariaHost.Blueprint;
#endif

namespace JueMingR.TerrariaHost.Tools
{
    internal enum ToolKind { Capture, Harvest, Seed, Mining, Recast, FishingPull, FishingCast, FishingCut, Build }
    internal enum ToolReadiness { Ready, Pending, Yield, Invalid }
    internal enum ToolTargetRejection { Resolution, Consumer }
    internal sealed class ToolIntent
    {
        internal ToolKind Kind;
        internal int Slot;
        internal Vector2 Target;
        internal Func<bool> Valid;
        internal Func<bool> Refresh;
        internal Func<ToolReadiness> Readiness;
        internal Func<int,int,bool> ResolvedTarget;
        internal Action<ToolTargetRejection> TargetRejected;
        internal bool PreciseTarget;
        internal Action Admitted;
        internal Action Used;
        internal Action Yielded;
        internal Action<bool,bool> Completed;
        internal bool SelectionOnly;
        internal bool HeldInventory;
        internal int PlayerDirection;
        internal int MaximumConsumption=1;
        internal ulong MaterialSlots;
        internal Func<Item[],int,bool> OwnsMaterial;
        internal Func<Item,bool> OwnsMaterialIdentity;
        internal Func<bool> ConsumptionValid;
        internal Func<bool,bool> NativePlacement;
        internal Func<bool,TileObject,int,bool> NativeTileChoice;
        internal Action NativeChecked;
        internal Action NativeBeginning;
        internal Func<Action<bool>> NativePreparing;
#if JMR_BLUEPRINT_DIAGNOSTIC
        internal long BlueprintDiagnosticFlight;
#endif
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
        private long token,session,pulseFrame,selection,nextContention,nativeGeneration,suspendedFrame=-1;
        private bool pulsed,checkedItem,started,cancelled,borrowed,notified,yieldExternal,returnRequested,nativeReady,nativeBaseline,targetRejected;
        private int oldX,oldY,oldTileX,oldTileY,ownX,ownY,ownTileX,ownTileY;
        private bool oldMouse;
        private bool cursorBorrowed,oldSmartMouse,oldSmartPad;
        private bool directionBorrowed;
        private int oldDirection,ownDirection;
        private Projectile drill;
        private int drillKey;
        internal ToolIntent Intent {get;private set;}
        internal bool InNativeUse {get;private set;}
        internal bool Returning {get;private set;}
        internal bool Active {get{return player!=null;}}
        internal long Operation {get{return token;}}
        internal long NativeGeneration {get{return nativeGeneration;}}
        internal bool CompletedAnimation {get{return Active && started && player.itemAnimation<=0;}}
        internal bool Building {get{return Active && Intent?.Kind==ToolKind.Build;}}
        private bool HeldInventory {get{return Intent.HeldInventory || Intent.Kind==ToolKind.Capture && host.Mode(0)==2;}}
        // Only G10 automatic fishing pauses with F5. New tool kinds and the
        // G09 borrowed-net return must not inherit this feature-specific gate.
        private bool CanRunFeature {get{return (Intent.Kind!=ToolKind.FishingPull && Intent.Kind!=ToolKind.FishingCast && Intent.Kind!=ToolKind.FishingCut) || (host.CanFishingInterface?.Invoke()??true);}}
        private bool Admitted(Player p){return CanRunFeature && host.Admit(p,HeldInventory);}
        internal bool ActionValid {get{return Active && !cancelled && Identity() && Admitted(player) && (Intent.Valid?.Invoke()??false);}}
        internal ToolUse(HostTools host){this.host=host;}
#if JMR_BLUEPRINT_DIAGNOSTIC
        private void DiagnosticPhase(BuildTraceStage stage,BuildTraceReason reason)
        {
            if(!BlueprintBuildTrace.Active || Intent?.Kind!=ToolKind.Build || player==null)return;
            BlueprintBuildTrace.Hit(stage);
            long flags=(directionBorrowed?1:0)|(player.controlLeft?2:0)|(player.controlRight?4:0)|(nativeReady?8:0)|
                (player.controlUseItem?16:0)|(player.releaseUseItem?32:0)|(player.selectedItemState.HasBufferedChange?64:0)|(player.selectedItemState.HasActiveOverride?128:0);
            BlueprintBuildTrace.Semantic(stage,reason,player.direction,flags,Player.tileTargetX,Player.tileTargetY,Intent.BlueprintDiagnosticFlight,token,nativeGeneration);
        }
#endif
        internal void Pick(Player p,ref int chosen,ref bool result)
        {
            if(!ReferenceEquals(p,host.Player))return;
#if JMR_BLUEPRINT_DIAGNOSTIC
            BlueprintBuildTrace.Hit(BuildTraceStage.Selection);
#endif
            if(Active)
            {
                // Vanilla has restored its original selection before this
                // callback. Reapplying the same override avoids a spurious
                // selection change between animations; no timer is rewritten.
                if(!result && !cancelled && (Intent.Refresh!=null || Intent.Readiness!=null) && SourceIdentity() &&
                    host.ManualSelectionFrame!=host.Input.Frame && !host.ManualLeft &&
                    Admitted(p) && RetainAtSelection())
                {chosen=Intent.Slot;result=true;return;}
                bool handoff=yieldExternal;Retire();if(handoff)return;
            }
            if(result || host.Input.Frame<host.NextUseFrame || host.ManualSelectionFrame==host.Input.Frame)
            {
#if JMR_BLUEPRINT_DIAGNOSTIC
                BlueprintBuildTrace.Count(BuildTraceReason.SelectionWait);
#endif
                return;
            }
            ToolIntent candidate=host.Choose(p);if(candidate==null)return;
            if(Acquire(p,candidate)){chosen=candidate.Slot;result=true;}
        }
        private bool Acquire(Player p,ToolIntent candidate)
        {
#if JMR_BLUEPRINT_DIAGNOSTIC
            if(candidate.Kind==ToolKind.Build)BlueprintBuildTrace.Hit(BuildTraceStage.Acquire);
#endif
            if(candidate.Slot<0 || candidate.Slot>=50 || !(candidate.Valid?.Invoke()??false))
            {
#if JMR_BLUEPRINT_DIAGNOSTIC
                if(candidate.Kind==ToolKind.Build)BlueprintBuildTrace.Count(BuildTraceReason.AcquireRejected);
#endif
                return false;
            }
            long next=host.Items.Ownership.NewUseToken();
            if(!host.Items.Ownership.TryBeginUse(host.Runtime.Generation,candidate.Slot,next,candidate.MaterialSlots))
            {
#if JMR_BLUEPRINT_DIAGNOSTIC
                if(candidate.Kind==ToolKind.Build)BlueprintBuildTrace.Count(BuildTraceReason.OwnershipDenied);
#endif
                return false;
            }
            player=p;Intent=candidate;item=p.inventory[candidate.Slot];type=item.type;stack=item.stack;original=p.selectedItem;
            originalItem=original>=0 && original<50?p.inventory[original]:null;originalType=originalItem?.type??0;
            token=next;session=host.Runtime.Generation;selection=host.SelectionIntent;
            nextContention=host.Input.Frame+Math.Max(1,item.useAnimation);
            pulsed=checkedItem=started=cancelled=notified=nativeReady=nativeBaseline=targetRejected=false;
            yieldExternal=returnRequested=false;
            candidate.Admitted?.Invoke();
            return true;
        }
        private bool SourceIdentity(){return session==host.Runtime.Generation && host.SelectionIntent==selection && ReferenceEquals(player.inventory[Intent.Slot],item) && item.type==type && (item.stack>0 || Intent.SelectionOnly && item.IsAir);}
        private bool Identity(){return SourceIdentity() && player.selectedItem==Intent.Slot;}
        private ToolReadiness Refresh()
        {
            if(!Active || cancelled || !Identity() || !Admitted(player))
            {
#if JMR_BLUEPRINT_DIAGNOSTIC
                DiagnosticPhase(BuildTraceStage.Refresh,BuildTraceReason.Invalid);
#endif
                return ToolReadiness.Invalid;
            }
            var state=Intent.Readiness!=null?Intent.Readiness():(Intent.Refresh==null || Intent.Refresh()?ToolReadiness.Ready:ToolReadiness.Invalid);
            if(state==ToolReadiness.Ready && !ActionValid)
            {
#if JMR_BLUEPRINT_DIAGNOSTIC
                DiagnosticPhase(BuildTraceStage.Refresh,BuildTraceReason.Invalid);
#endif
                return ToolReadiness.Invalid;
            }
#if JMR_BLUEPRINT_DIAGNOSTIC
            DiagnosticPhase(BuildTraceStage.Refresh,state==ToolReadiness.Ready?BuildTraceReason.Ready:state==ToolReadiness.Pending?BuildTraceReason.Pending:state==ToolReadiness.Yield?BuildTraceReason.Yield:BuildTraceReason.Invalid);
#endif
            return state;
        }
        private bool RetainAtSelection()
        {
            // SelectedItemState has already restored the original slot here;
            // source identity remains ours, selectedItem identity deliberately does not.
            var state=Intent.Readiness!=null?Intent.Readiness():(Intent.Refresh?.Invoke()==true?ToolReadiness.Ready:ToolReadiness.Invalid);
            if(state==ToolReadiness.Ready)return true;
            if(state!=ToolReadiness.Pending)return false;
            bool external;
            // Pending retains only a finite discovery responsibility. Another
            // ready tool gets the normal vanilla selection boundary immediately.
            if(host.Contended(Intent.Kind,out external)){yieldExternal=external;Intent.Yielded?.Invoke();return false;}
            return true;
        }
        // Selecting a temporary tool must not make the original held source
        // available to automatic sell/store/discard. This observation-only
        // guard never blocks a real manual move or a newer selection intent.
        internal bool ProtectOriginal(Item source)
        {return Active && ReferenceEquals(player,host.Player) && session==host.Runtime.Generation && selection==host.SelectionIntent && originalItem!=null && ReferenceEquals(source,originalItem) && ReferenceEquals(player.inventory[original],source) && source.type==originalType && source.stack>0 && player.selectedItemState.HasActiveOverride;}
        private void HandToManual(){Restore();Cancel();host.Items.Ownership.EndUse(session,token);}
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
            if(host.ManualLeft || host.ManualRight || PlayerInput.Triggers.Current.SmartSelect){
#if JMR_BLUEPRINT_DIAGNOSTIC
                if(Building)BlueprintBuildTrace.Count(BuildTraceReason.ManualInput);
#endif
                HandToManual();return;}
            var readiness=suspendedFrame==host.Input.Frame?ToolReadiness.Pending:Refresh();nativeReady=readiness==ToolReadiness.Ready;
#if JMR_BLUEPRINT_DIAGNOSTIC
            DiagnosticPhase(BuildTraceStage.BeforeSync,readiness==ToolReadiness.Ready?BuildTraceReason.Ready:readiness==ToolReadiness.Pending?BuildTraceReason.Pending:readiness==ToolReadiness.Yield?BuildTraceReason.Yield:BuildTraceReason.Invalid);
#endif
            if(nativeReady)targetRejected=false;
            if(readiness==ToolReadiness.Invalid || readiness==ToolReadiness.Yield){Cancel();return;}
            if(!nativeReady){SuspendInput();return;}
            // A cut borrows selection only. No virtual use press is produced;
            // vanilla projectile AI observes the temporary non-rod itself.
            if(Intent.SelectionOnly)return;
            if(Intent.Refresh==null && Intent.Readiness==null && (pulsed || checkedItem))return;
            // Held construction can use another original item-time window
            // inside an animation. A non-reusing item still gets a real neutral
            // input step before its next animation; never manufacture release.
            if(Building && p.itemAnimation<=0 && !p.releaseUseItem && !item.autoReuse)
            {
#if JMR_BLUEPRINT_DIAGNOSTIC
                BlueprintBuildTrace.Count(BuildTraceReason.NativeTimerWait);
#endif
                return;
            }
            p.controlUseItem=true;if(!pulsed && !Building)p.releaseUseItem=true;pulseFrame=host.Input.Frame;pulsed=true;
        }
        internal void Begin(Player p)
        {
            if(!ReferenceEquals(p,player) || p.selectedItem!=Intent.Slot)return;
            nativeGeneration++;
            nativeBaseline=false;
#if JMR_BLUEPRINT_DIAGNOSTIC
            DiagnosticPhase(BuildTraceStage.ItemBegin,BuildTraceReason.DirectionPhase);
#endif
            // Cleanup responsibility may outlive automatic intent. A genuine
            // manual press owns this ItemCheck, including during a Boss pause.
            if(host.ManualLeft || host.SelectionIntent!=selection){HandToManual();return;}
            InNativeUse=true;
            var readiness=suspendedFrame==host.Input.Frame?ToolReadiness.Pending:Refresh();nativeReady=readiness==ToolReadiness.Ready;
            if(readiness==ToolReadiness.Invalid || readiness==ToolReadiness.Yield){Cancel();return;}
            // A neutral ItemCheck owns cleanup, but no old target, consumption
            // baseline or rejection callback. Vanilla alone winds its timers down.
            if(!nativeReady){SuspendInput();return;}
            if(Intent.SelectionOnly)return;
            nativeBaseline=true;targetRejected=false;
            if(Building)stack=item.stack;
            Intent.NativeBeginning?.Invoke();
            // Native movement and StartActualUse must choose their own facing.
            // Only target lookup and an admitted placement borrow direction;
            // aim/cursor still span this native use until the normal cleanup.
            BorrowCursorMode();
            BorrowAim();
            if(pulsed && (Intent.Refresh!=null || Intent.Readiness!=null || !checkedItem) && (!Building || pulseFrame==host.Input.Frame))Main.mouseLeft=true;
        }
        internal void BeforeTargetResolution(Player p)
        {
            if(!ReferenceEquals(p,player) || !Building || !nativeReady || pulseFrame!=host.Input.Frame)return;
#if JMR_BLUEPRINT_DIAGNOSTIC
            DiagnosticPhase(BuildTraceStage.ResolveBefore,BuildTraceReason.DirectionPhase);
#endif
            BorrowDirection();BorrowCursorMode();BorrowAim();
        }
        internal void AfterTargetResolution(Player p)
        {
            if(!ReferenceEquals(p,player))return;
            try
            {
                if(!Building || !nativeReady || !borrowed)return;
#if JMR_BLUEPRINT_DIAGNOSTIC
                DiagnosticPhase(BuildTraceStage.ResolveAfter,BuildTraceReason.DirectionPhase);
#endif
                if(Intent.ResolvedTarget!=null && !Intent.ResolvedTarget(Player.tileTargetX,Player.tileTargetY))
                    RejectTarget(ToolTargetRejection.Resolution);
            }
            finally{RestoreDirection();}
        }
        internal void TargetResolutionFailed(Player p)
        {
            // Vanilla catches lookup faults and continues into movement before
            // Player.Update cleanup. End only this player's direction borrow;
            // its aim/cursor still belong to the later native-use cleanup.
            if(ReferenceEquals(p,player))RestoreDirection();
        }
        private void RejectTarget(ToolTargetRejection reason)
        {
            if(targetRejected)return;
            // An explicit final rejection ends this target, unlike discovery
            // Pending. The feature advances its candidate; the lease may only
            // continue with a newly prepared target after this native call.
            targetRejected=true;nativeReady=false;suspendedFrame=host.Input.Frame;
            Intent.TargetRejected?.Invoke(reason);SuspendInput();
        }
        internal void FinishPlayerUpdate(Player p,Exception error)
        {
            if(!ReferenceEquals(p,player))return;
            // Aim is borrowed before vanilla SmartCursorLookup. ItemCheck may
            // be skipped or throw earlier, so its finally is not the whole scope.
            Restore();
            if(error!=null){if(InNativeUse)End(p,token,error,nativeGeneration);else Cancel();}
        }
        private void SuspendInput()
        {
            if(player!=null && pulsed && pulseFrame==host.Input.Frame && player.controlUseItem)player.controlUseItem=host.ManualLeft;
            // Never manufacture releaseUseItem. The next real neutral native
            // step establishes release and cannot start an empty build animation.
        }
        private void BorrowAim()
        {
            if(!borrowed){oldX=Main.mouseX;oldY=Main.mouseY;oldMouse=Main.mouseLeft;oldTileX=Player.tileTargetX;oldTileY=Player.tileTargetY;}
            Vector2 screen=Main.ReverseGravitySupport(Intent.Target-Main.screenPosition);
            ownX=(int)screen.X;ownY=(int)screen.Y;ownTileX=(int)(Intent.Target.X/16);ownTileY=(int)(Intent.Target.Y/16);borrowed=true;
            Main.mouseX=ownX;Main.mouseY=ownY;Player.tileTargetX=ownTileX;Player.tileTargetY=ownTileY;
        }
        private void BorrowCursorMode()
        {
            if(!Building || !Intent.PreciseTarget || cursorBorrowed)return;
            oldSmartMouse=Main.SmartCursorWanted_Mouse;oldSmartPad=Main.SmartCursorWanted_GamePad;cursorBorrowed=true;
            // Ordinary precision input reaches solid-backed walls that the
            // smart lookup intentionally excludes. Let vanilla clear its own
            // continuity state; keep this same mode through the real consumer.
            Main.SmartCursorWanted_Mouse=Main.SmartCursorWanted_GamePad=false;
        }
        private void BorrowDirection()
        {
            int direction=Intent.PlayerDirection;
            if(direction!=1 && direction!=-1)return;
            // Lookup releases before native movement; actual placement starts
            // a fresh scope after StartActualUse. Repeated consumer checks must
            // not mistake our own temporary value for a new return baseline.
            if(!directionBorrowed || player.direction!=ownDirection)oldDirection=player.direction;
            ownDirection=direction;directionBorrowed=true;
            // Ordinary facing input can reach either direction without moving.
            // Direct ownership of this temporary field avoids ChangeDir's
            // pulley relocation; cleanup still respects a later native writer.
            player.direction=direction;
        }
        internal bool AllowBuildPlacement(bool wall)
        {
            bool allowed=!Building;
            if(Building && CurrentBuildConsumerTarget())
            {
                // StartActualUse can face along a grapple's movement after
                // Begin. Reapply only after admission and actual coordinates
                // pass, before native geometry chooses its directional object.
                BorrowDirection();allowed=Intent.NativePlacement?.Invoke(wall)??false;
            }
#if JMR_BLUEPRINT_DIAGNOSTIC
            if(Building)BlueprintBuildTrace.Count(allowed?BuildTraceReason.Passed:BuildTraceReason.NativeRejected);
#endif
            return allowed;
        }
        internal bool AllowBuildTileChoice(bool objectType,TileObject placement,int tileType)
        {
            // The original consumer has already chosen this placement. Keep
            // validating the actual choice; facing cannot repair it this late.
            bool allowed=!Building || CurrentBuildConsumerTarget() && (Intent.NativeTileChoice?.Invoke(objectType,placement,tileType)??false);
#if JMR_BLUEPRINT_DIAGNOSTIC
            DiagnosticPhase(BuildTraceStage.NativeChoiceGate,allowed?BuildTraceReason.NativeChosen:BuildTraceReason.NativeRejected);
#endif
            return allowed;
        }
        private bool CurrentBuildConsumerTarget()
        {
#if JMR_BLUEPRINT_DIAGNOSTIC
            DiagnosticPhase(BuildTraceStage.ConsumerGate,BuildTraceReason.DirectionPhase);
#endif
            if(!InNativeUse || !nativeReady || !ActionValid)return false;
            // Other vanilla stages/hooks may adjust tileTarget after lookup.
            // Recheck the coordinates the consumer is about to read, including
            // the later object-choice boundary; an earlier aim is no receipt.
            if(Intent.ResolvedTarget==null || Intent.ResolvedTarget(Player.tileTargetX,Player.tileTargetY))return true;
            RejectTarget(ToolTargetRejection.Consumer);return false;
        }
        internal Action<bool> BeginBuildPreparation()
        {return Building && InNativeUse && nativeReady && ActionValid ? Intent.NativePreparing?.Invoke() : null;}
        internal bool OwnsBuildSource(Item[] array,int slot)
        {return Building && session==host.Runtime.Generation && host.SelectionIntent==selection && ReferenceEquals(array,player.inventory) &&
            (slot==Intent.Slot && ReferenceEquals(array[slot],item) || (Intent.OwnsMaterial?.Invoke(array,slot)??false));}
        internal bool OwnsBuildMaterial(Item source)
        {return Building && !cancelled && SourceIdentity() && host.Items.Ownership.IsUseSlot(Intent.Slot) &&
            (ReferenceEquals(source,item) || (Intent.OwnsMaterialIdentity?.Invoke(source)??false));}
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
        internal void End(Player p,long operation,Exception error,long generation=0)
        {
            if(operation==0 || operation!=token || generation!=0 && generation!=nativeGeneration || !ReferenceEquals(p,player))return;
            Restore();bool entered=InNativeUse;InNativeUse=false;if(!entered)return;checkedItem=true;
            bool neutral=Building && !nativeBaseline;
            if(neutral){if(error!=null)Cancel();}
            else if(error!=null)
            {
                // An interrupted consumer is not a rollback. Retain only the
                // real source until a genuinely new player/world connection.
                host.HoldUnknown((1UL<<Intent.Slot)|Intent.MaterialSlots);Notify(true);Cancel();host.Report("工具操作结果未确认，已停止；相关物品暂受保护。");
            }
            else if(item.stack>stack || item.stack<stack-Intent.MaximumConsumption || !ReferenceEquals(p.inventory[Intent.Slot],item) && !p.inventory[Intent.Slot].IsAir ||
                Intent.ConsumptionValid!=null && !Intent.ConsumptionValid())
            {host.HoldUnknown((1UL<<Intent.Slot)|Intent.MaterialSlots);Notify(true);Cancel();}
            else if(nativeReady || !Building)Intent.NativeChecked?.Invoke();
            if(!cancelled && (Intent.Refresh!=null || Intent.Readiness!=null) && (targetRejected || host.Input.Frame>=nextContention || (host.Combat?.Handoff.ResumeWanted??false)))
            {
                nextContention=host.Input.Frame+Math.Max(1,p.itemAnimationMax);
                bool external;if(host.Contended(Intent.Kind,out external)){yieldExternal=external;Intent.Yielded?.Invoke();Cancel();}
            }
        }
        internal bool Owns(Item[] array,int slot){return InNativeUse && (!Building || nativeReady) && ReferenceEquals(array,player?.inventory) && Intent!=null &&
            (slot==Intent.Slot && ReferenceEquals(array[slot],item) || Building && (Intent.OwnsMaterial?.Invoke(array,slot)??false));}
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
            if(checkedItem && (cancelled || Intent.Refresh==null && Intent.Readiness==null) && player.selectedItemState.CanChangeSelectedItemImmediately)Retire();
        }
        private void Notify(bool unknown){if(notified)return;notified=true;Intent?.Completed?.Invoke(started,unknown);}
        internal void Cancel()
        {
            if(!Active)return;cancelled=true;
            if(player.selectedItemState.HasActiveOverride && !player.selectedItemState.HasBufferedChange && host.SelectionIntent==selection)
            {Returning=true;try{player.selectedItemState.Select(original);returnRequested=true;}finally{Returning=false;}}
            if(pulseFrame==host.Input.Frame && pulsed){player.controlUseItem=host.ManualLeft;if(!Building)player.releaseUseItem=!player.controlUseItem;}
        }
        internal void Retire()
        {
            if(!Active)return;Restore();Cancel();Notify(false);
            host.Items.Ownership.EndUse(session,token);
            player=null;item=originalItem=null;drill=null;Intent=null;token=0;InNativeUse=false;
        }
        private void Restore()
        {
            if(cursorBorrowed)
            {
                cursorBorrowed=false;
                if(!Main.SmartCursorWanted_Mouse)Main.SmartCursorWanted_Mouse=oldSmartMouse;
                if(!Main.SmartCursorWanted_GamePad)Main.SmartCursorWanted_GamePad=oldSmartPad;
            }
            RestoreDirection();
            if(!borrowed)return;borrowed=false;
            if(Main.mouseX==ownX)Main.mouseX=oldX;if(Main.mouseY==ownY)Main.mouseY=oldY;
            if(Player.tileTargetX==ownTileX)Player.tileTargetX=oldTileX;if(Player.tileTargetY==ownTileY)Player.tileTargetY=oldTileY;
            if(!oldMouse && Main.mouseLeft && pulsed)Main.mouseLeft=false;
        }
        private void RestoreDirection()
        {
            if(!directionBorrowed)return;
            directionBorrowed=false;
            if(player!=null && player.direction==ownDirection)player.direction=oldDirection;
        }
    }
}
