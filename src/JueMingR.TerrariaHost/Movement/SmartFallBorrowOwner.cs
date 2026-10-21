using System;
using System.Reflection;
using System.Threading;
using JueMingR.Features.Movement;
using JueMingR.TerrariaHost.Fishing;
using JueMingR.TerrariaHost.Items;
using Terraria;
using Terraria.ID;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Movement
{
    internal enum SmartFallAvailability { Unavailable, InPlace, Borrowable, Held, ActiveLoan, IdentityBlocked, ItemUseBlocked, Busy, SelectionBlocked, Protected, NoSource, NoTarget, OwnershipBlocked, SaveBusy }
    internal struct SmartFallPreparation
    {
        internal SmartFallAvailability Availability;
        internal bool AvailableNow,AlreadyInPlace,HeldSelectionReady,Displaced,FishingEndpoint,Misc,ReturnPending;
        internal int BorrowWrites,SelectionWrites,SourceSlot,TargetSlot;
        internal Item[] Source,Target;
    }
    // One short rescue owns at most one real exchange or held-item selection.
    // Every item remains in native containers; no clone/private inventory exists.
    internal sealed class SmartFallBorrowOwner
    {
        private static readonly object SaveLock=typeof(Player).GetField("IOLock",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        private readonly HostItems items;
        private readonly Func<long> currentGeneration;
        private readonly Func<Item,SmartFallFamily,bool> matches;
        private readonly FishingEquipment fishing;
        private Player player;
        private long generation,token;
        private Item[] source,target;
        private int sourceSlot,targetSlot,loadout,originalSelection,heldSlot;
        private Item borrowed,original,selectionAnchor;
        private SmartFallFamily borrowedFamily;
        private int originalResources;
        private long nextNativeUse,nativeUse,nativeUseToken;
        private SmartFallFamily nativeUseFamily;
        private bool nativeUseConsumed;
        private bool selection,selected,restoreRequested,pulsed,ownsMouse,ownedUseBit,originalUseBit,originalReleaseBit;
        private bool misc,unknown;
        private bool borrowedFavorite,originalFavorite;
        private int borrowedType,borrowedPrefix,borrowedStack,originalType,originalPrefix,originalStack;
        private long returnRevision=-1;
        private Item[] returnSource;
        private int returnSlot;
        private Player blockerPlayer;
        private Item blockerCursor;
        private long blockerRevision,blockerGeneration,blockerToken,blockerSession;
        private ulong blockerFlags,blockerProtected;
        private int blockerSelection,blockerLoadout;
        internal bool HasPending {get{return player!=null;}}
        internal bool SelectionReady {get{return selection && selected && Identity(player,generation) && player.selectedItem==heldSlot && ReferenceEquals(player.inventory[heldSlot],borrowed);}}
        internal bool AllowsUse(int slot){return slot==heldSlot && SelectionReady && !restoreRequested;}
        // This is a synchronous QuickMount/QuickGrapple call lease, not a tick
        // bypass. The native consumer asks StartUse once; every other protected
        // item/endpoint remains unavailable, including the parked original.
        internal long BeginNativeUse(Player p,long epoch,Item item,SmartFallFamily family)
        {
            if(nativeUse!=0 || !NativeUseIdentity(p,epoch,item,family))return 0;
            if(nextNativeUse==long.MaxValue)throw new InvalidOperationException("Fall native-use lease exhausted.");
            nativeUse=++nextNativeUse;nativeUseToken=token;nativeUseFamily=family;nativeUseConsumed=false;return nativeUse;
        }
        internal void EndNativeUse(long invocation)
        {if(invocation>0 && invocation==nativeUse)ClearNativeUse();}
        internal bool AllowsNativeUse(Player p,Item item)
        {
            if(nativeUse==0 || nativeUseConsumed || nativeUseToken!=token || !NativeUseIdentity(p,generation,item,nativeUseFamily))return false;
            nativeUseConsumed=true;return true;
        }
        private bool NativeUseIdentity(Player p,long epoch,Item item,SmartFallFamily family)
        {
            int slot=MiscSlot(family);
            return items!=null && slot>=0 && misc && !selection && !restoreRequested && !unknown && family==borrowedFamily &&
                Identity(p,epoch) && Permitted(p,epoch) && ReferenceEquals(p,items.World.SessionPlayer) && items.World.SessionGeneration==epoch &&
                items.Ownership.OwnsFallEquipment(epoch,token,true,slot) && ReferenceEquals(item,borrowed) &&
                ReferenceEquals(target,p.miscEquips) && targetSlot==slot && ReferenceEquals(p.miscEquips[slot],item) &&
                ReferenceEquals(source[sourceSlot],original) && item.type==borrowedType && item.prefix==borrowedPrefix && item.stack==borrowedStack && !item.favorited;
        }
        private void ClearNativeUse(){nativeUse=nativeUseToken=0;nativeUseConsumed=false;}
        internal long Operation {get{return token;}}
        internal int OriginalResources {get{return originalResources;}}
        // Activation is a privilege of this live exchange, not of an item ID.
        // Recheck both physical endpoints, token, epoch and native legal slot
        // after UpdateEquips and immediately before its resource consumer.
        internal bool AllowsActivation(Player p,long epoch,Item item,SmartFallFamily family)
        {
            return items!=null && !misc && !selection && !restoreRequested && !unknown && family==borrowedFamily &&
                Identity(p,epoch) && Permitted(p,epoch) && ReferenceEquals(p,items.World.SessionPlayer) && items.World.SessionGeneration==epoch &&
                items.Ownership.OwnsFallEquipment(epoch,token,false,targetSlot) && ReferenceEquals(target,p.armor) &&
                p.CurrentLoadoutIndex==loadout && p.IsItemSlotUnlockedAndUsable(targetSlot) && ReferenceEquals(p.GetEffectiveArmor(targetSlot),item) &&
                ReferenceEquals(item,borrowed) && ReferenceEquals(source[sourceSlot],original) &&
                item.type==borrowedType && item.prefix==borrowedPrefix && item.stack==borrowedStack && !item.favorited &&
                original.type==originalType && original.prefix==originalPrefix && original.stack==originalStack && !original.favorited &&
                FishingEquipmentPlan.CanSwap(p,source,sourceSlot,targetSlot);
        }
        internal bool OwnsEndpoint(Item[] array,int slot)
        { return HasPending && !selection && (ReferenceEquals(array,source) && slot==sourceSlot || ReferenceEquals(array,target) && slot==targetSlot); }
        internal long SourceReads {get;private set;}
        internal long Exchanges {get;private set;}
        internal long ReturnSearches {get;private set;}
        internal bool Unknown {get{return unknown;}}
        internal bool SyncUnconfirmed {get;private set;}
        internal SmartFallResult ReturnResult {get;private set;}
        // Test-only fault seam: production never installs a callback. The journal
        // already owns both endpoints before either write can invoke this seam.
#if DEBUG
        internal Action<int> WriteBoundary {get;set;}
#endif
        [System.Diagnostics.Conditional("DEBUG")]
        private void Written(int stage)
        {
#if DEBUG
            WriteBoundary?.Invoke(stage);
#endif
        }
        internal SmartFallBorrowOwner(HostItems items,Func<long> generation,Func<Item,SmartFallFamily,bool> matcher,FishingEquipment fishing=null)
        {this.items=items;currentGeneration=generation;matches=matcher;this.fishing=fishing;}
        private bool Identity(Player value,long epoch)
        {return (items==null || Thread.CurrentThread.ManagedThreadId==items.ThreadId) && value!=null && ReferenceEquals(value,player) && ReferenceEquals(value,Main.LocalPlayer) && epoch>0 && epoch==generation && epoch==(currentGeneration?.Invoke()??0);}
        private bool Permitted(Player p,long epoch)
        {return (items==null || Thread.CurrentThread.ManagedThreadId==items.ThreadId) && p!=null && ReferenceEquals(p,Main.LocalPlayer) && epoch>0 && epoch==(currentGeneration?.Invoke()??0) && !p.dead && !p.ghost && !p.CCed && !Main.gameMenu && !Main.gamePaused && !Main.ServerSideCharacter && !(Main.ActivePlayerFileData?.ServerSideCharacter??false);}
        private bool ManualUse(Player p)
        {return p.controlUseItem && !ownedUseBit || p.controlUseTile || p.controlTorch || PlayerInput.Triggers.Current.MouseLeft || PlayerInput.Triggers.Current.MouseRight || PlayerInput.Triggers.Current.SmartSelect;}
        private bool Match(Item item,SmartFallFamily family)
        {SourceReads++;return item!=null && !item.IsAir && item.stack==1 && (matches?.Invoke(item,family)??false);}
        private static bool Held(SmartFallFamily family){return family==SmartFallFamily.Umbrella || family==SmartFallFamily.TeleportRod;}
        private static int MiscSlot(SmartFallFamily family)
        {return family==SmartFallFamily.Grapple?4:family==SmartFallFamily.FlyingMount || family==SmartFallFamily.SafeMount?3:-1;}
        private bool Protected(Item item)
        {return item==null || ReferenceEquals(item,Main.mouseItem) || items!=null && (items.World.ManualMaterials.Contains(item) || (items.World.AdditionalProtection?.Invoke(item)??false));}
        private bool SourceAllowed(Player p,Item[] array,int slot)
        {
            if(array==null || slot<0 || slot>=array.Length || Protected(array[slot]))return false;
            if(ReferenceEquals(array,p.inventory))return slot<50 && !p.inventoryChestStack[slot] && !(items?.Ownership.IsProtected(slot)??true);
            return ReferenceEquals(array,p.armor) && slot>=10 && slot<20 && p.IsItemSlotUnlockedAndUsable(slot) && !(items?.Ownership.IsFallEquipmentSlot(false,slot)??true);
        }
        internal Item GetCandidate(Player p,SmartFallFamily family)
        {return GetCandidate(p,family,null);}
        internal Item GetCandidate(Player p,SmartFallFamily family,Func<Item,bool> eligible)
        {
            if(p==null || items!=null && Thread.CurrentThread.ManagedThreadId!=items.ThreadId)return null;
            if(Held(family))
            {if(p.selectedItem>=0 && p.selectedItem<50 && Match(p.inventory[p.selectedItem],family) && (eligible?.Invoke(p.inventory[p.selectedItem])??true))return p.inventory[p.selectedItem];}
            else
            {
                int ms=MiscSlot(family);
                if(ms>=0){if(Match(p.miscEquips[ms],family) && (eligible?.Invoke(p.miscEquips[ms])??true))return p.miscEquips[ms];}
                else for(int i=0;i<10;i++)if(p.IsItemSlotUnlockedAndUsable(i) && Match(p.GetEffectiveArmor(i),family) && (eligible?.Invoke(p.GetEffectiveArmor(i))??true))return p.GetEffectiveArmor(i);
            }
            if(items==null || HasPending)return null;
            for(int i=0;i<50;i++)if(SourceAllowed(p,p.inventory,i) && Match(p.inventory[i],family) && (eligible?.Invoke(p.inventory[i])??true) && CanPlace(p,p.inventory,i,family,out _,out _))return p.inventory[i];
            if(!Held(family) && MiscSlot(family)<0)for(int i=10;i<20;i++)if(SourceAllowed(p,p.armor,i) && Match(p.armor[i],family) && (eligible?.Invoke(p.armor[i])??true) && CanPlace(p,p.armor,i,family,out _,out _))return p.armor[i];
            return null;
        }
        private bool CanPlace(Player p,Item[] array,int slot,SmartFallFamily family,out Item[] destination,out int index)
        {
            destination=null;index=-1;
            if(Held(family)){destination=p.inventory;index=slot;return ReferenceEquals(array,p.inventory);}
            int ms=MiscSlot(family);
            if(ms>=0){if(Protected(p.miscEquips[ms]) || items.Ownership.IsFallEquipmentSlot(true,ms))return false;destination=p.miscEquips;index=ms;return true;}
            // Prefer disjoint, then empty functional endpoints. Only if none is
            // legal may this exact fishing F endpoint be lent for A->F->S.
            for(int pass=0;pass<3;pass++)for(int i=0;i<10;i++)
            {
                var old=p.armor[i];bool protectedItem=Protected(old),lent=protectedItem && (fishing?.CanLendFallSlot(p,i,old)??false);
                if(items.Ownership.IsFallEquipmentSlot(false,i) || (pass==0?!old.IsAir || protectedItem:pass==1?protectedItem || old.IsAir:!lent))continue;
                if(FishingEquipmentPlan.CanSwap(p,array,slot,i)){destination=p.armor;index=i;return true;}
            }
            return false;
        }
        private static bool SaveAvailable()
        {if(!Monitor.TryEnter(SaveLock))return false;Monitor.Exit(SaveLock);return true;}
        // Only constant-size native operation gates are sampled here. Timers
        // contribute their busy state, not a new stamp on every countdown tick.
        // Source/terrain changes keep their existing separate revision owners.
        internal long BlockerStamp(Player p)
        {
            if(p==null || items!=null && Thread.CurrentThread.ManagedThreadId!=items.ThreadId)return 0;
            ulong flags=0;
            void Flag(bool value,int bit){if(value)flags|=1UL<<bit;}
            Flag(p.UsingOrReusingItem || p.itemTime!=0,0);Flag(p.selectedItemState.HasBufferedChange,1);Flag(p.selectedItemState.CanChangeSelectedItemImmediately,2);
            Flag(p.selectedItemState.HasActiveOverride,3);Flag(p.noItems,4);Flag(p.dead,5);Flag(p.ghost,6);Flag(p.CCed,7);Flag(p.HasLockedInventory(),8);
            Flag(ManualUse(p),9);Flag(Main.gameMenu,10);Flag(Main.gamePaused,11);Flag(Main.ServerSideCharacter || (Main.ActivePlayerFileData?.ServerSideCharacter??false),12);
            Flag(HasPending,13);Flag(restoreRequested,14);Flag(items?.Ownership.HasUse??false,15);Flag(items?.Ownership.HasFallEquipment??false,16);
            Flag(Main.LocalPlayerHasPendingInventoryActions(),17);Flag(SaveAvailable(),18);Flag(ReferenceEquals(p,Main.LocalPlayer),19);Flag(Main.mouseItem.IsAir,20);
            long epoch=currentGeneration?.Invoke()??0,session=items?.Ownership.Session??0;ulong protection=items?.Ownership.ProtectedSlots??0;
            Item cursor=Main.mouseItem.IsAir?null:Main.mouseItem;
            if(!ReferenceEquals(blockerPlayer,p) || !ReferenceEquals(blockerCursor,cursor) || blockerFlags!=flags || blockerProtected!=protection ||
                blockerGeneration!=epoch || blockerSession!=session || blockerToken!=token || blockerSelection!=p.selectedItem || blockerLoadout!=p.CurrentLoadoutIndex)
            {blockerPlayer=p;blockerCursor=cursor;blockerFlags=flags;blockerProtected=protection;blockerGeneration=epoch;blockerSession=session;blockerToken=token;blockerSelection=p.selectedItem;blockerLoadout=p.CurrentLoadoutIndex;blockerRevision++;}
            return blockerRevision;
        }
        // Planning and execution share the actual entry gates. This performs no
        // token acquisition, selection or Item writes. The Host caches it until
        // a source revision or the O(1) blocker stamp changes.
        internal SmartFallPreparation GetPreparation(Player p,long epoch,Item item,SmartFallFamily family)
        {
            SmartFallPreparation Block(SmartFallAvailability why){return new SmartFallPreparation{Availability=why,SourceSlot=-1,TargetSlot=-1,ReturnPending=HasPending};}
            if(!Permitted(p,epoch))return Block(SmartFallAvailability.IdentityBlocked);
            if(item==null || !Match(item,family))return Block(SmartFallAvailability.NoSource);
            if(family==SmartFallFamily.TeleportRod && p.noItems)return Block(SmartFallAvailability.ItemUseBlocked);
            if(HasPending)
            {
                if(!Identity(p,epoch) || !ReferenceEquals(borrowed,item) || restoreRequested)return Block(SmartFallAvailability.OwnershipBlocked);
                return new SmartFallPreparation{Availability=SmartFallAvailability.ActiveLoan,AvailableNow=true,HeldSelectionReady=SelectionReady,ReturnPending=true,Source=selection?p.inventory:source,SourceSlot=selection?heldSlot:sourceSlot,Target=selection?p.inventory:target,TargetSlot=selection?heldSlot:targetSlot,Misc=misc,Displaced=original!=null && !original.IsAir};
            }
            // Already worn capabilities need no inventory operation. In
            // particular native cursed/noItems does not forbid JumpMovement.
            int ms=MiscSlot(family);
            if(ms>=0 && ReferenceEquals(p.miscEquips[ms],item))return new SmartFallPreparation{Availability=SmartFallAvailability.InPlace,AvailableNow=true,AlreadyInPlace=true,Source=p.miscEquips,Target=p.miscEquips,SourceSlot=ms,TargetSlot=ms,Misc=true};
            if(!Held(family) && ms<0)for(int i=0;i<10;i++)if(p.IsItemSlotUnlockedAndUsable(i) && ReferenceEquals(p.GetEffectiveArmor(i,out var shared),item))
                return new SmartFallPreparation{Availability=SmartFallAvailability.InPlace,AvailableNow=true,AlreadyInPlace=true,Source=shared.HasValue?p.Loadouts[shared.Value].Armor:p.armor,Target=p.armor,SourceSlot=i,TargetSlot=i};
            if(items==null)return Block(SmartFallAvailability.NoSource);
            // noItems is an item-use limit, not a native armor/selection limit.
            // Umbrella is held passively; the rod must actually start an item.
            if(p.HasLockedInventory() || Main.LocalPlayerHasPendingInventoryActions())return Block(SmartFallAvailability.OwnershipBlocked);
            if(p.UsingOrReusingItem || p.itemTime!=0)return Block(SmartFallAvailability.Busy);
            if(p.selectedItemState.HasBufferedChange)return Block(SmartFallAvailability.SelectionBlocked);
            Item[] array=null;int slot=-1;
            for(int i=0;i<50;i++)if(ReferenceEquals(p.inventory[i],item)){array=p.inventory;slot=i;break;}
            if(array==null && !Held(family) && ms<0)for(int i=10;i<20;i++)if(ReferenceEquals(p.armor[i],item)){array=p.armor;slot=i;break;}
            if(array==null)return Block(SmartFallAvailability.NoSource);
            if(!SourceAllowed(p,array,slot))return Block(SmartFallAvailability.Protected);
            if(!CanPlace(p,array,slot,family,out var dest,out var at))return Block(SmartFallAvailability.NoTarget);
            if(items.Ownership.Session!=epoch)return Block(SmartFallAvailability.IdentityBlocked);
            if(Held(family))
            {
                if(!Main.mouseItem.IsAir || ManualUse(p) || !p.selectedItemState.CanChangeSelectedItemImmediately || p.selectedItemState.HasActiveOverride)return Block(SmartFallAvailability.SelectionBlocked);
                if(items.Ownership.HasUse)return Block(SmartFallAvailability.OwnershipBlocked);
            }
            else
            {
                if(items.Ownership.HasFallEquipment)return Block(SmartFallAvailability.OwnershipBlocked);
                if(!SaveAvailable())return Block(SmartFallAvailability.SaveBusy);
            }
            return new SmartFallPreparation{Availability=Held(family)?SmartFallAvailability.Held:SmartFallAvailability.Borrowable,AvailableNow=true,Source=array,SourceSlot=slot,Target=dest,TargetSlot=at,Misc=ms>=0,
                BorrowWrites=Held(family)?0:2,SelectionWrites=Held(family) && p.selectedItem!=slot?1:0,HeldSelectionReady=Held(family) && p.selectedItem==slot,Displaced=!Held(family) && !dest[at].IsAir,FishingEndpoint=ReferenceEquals(dest,p.armor) && (fishing?.CanLendFallSlot(p,at,dest[at])??false)};
        }
        internal bool TryBorrow(Player p,long epoch,Item item,SmartFallFamily family)
        {
            var preparation=GetPreparation(p,epoch,item,family);
            if(!preparation.AvailableNow)return false;
            if(HasPending)return true;
            ReturnResult=SmartFallResult.None;
            if(preparation.AlreadyInPlace)return true;
            Item[] array=preparation.Source,dest=preparation.Target;int slot=preparation.SourceSlot,at=preparation.TargetSlot;
            long next=items.Ownership.NewUseToken();
            if(Held(family))
            {
                if(!Main.mouseItem.IsAir || ManualUse(p) || !p.selectedItemState.CanChangeSelectedItemImmediately || p.selectedItemState.HasActiveOverride || !items.Ownership.TryBeginUse(epoch,slot,next))return false;
                Bind(p,epoch,next,item,family);selection=true;selected=p.selectedItem==slot;heldSlot=slot;originalSelection=p.selectedItem;selectionAnchor=p.inventory[originalSelection];return true;
            }
            if(!Monitor.TryEnter(SaveLock))
            {
                // Autosave can acquire the lock after the read-only plan was
                // sampled. Remember that actual unavailable observation so a
                // release before the next poll is still a qualification change.
                blockerFlags&=~(1UL<<18);blockerRevision++;return false;
            }
            try
            {
                ulong inv=ReferenceEquals(array,p.inventory)?1UL<<slot:0;
                uint armor=ReferenceEquals(dest,p.armor)?1U<<at:0;if(ReferenceEquals(array,p.armor))armor|=1U<<slot;
                if(!items.Ownership.TryBeginFallEquipment(epoch,inv,armor,ReferenceEquals(dest,p.miscEquips)?1U<<at:0,next))return false;
                Bind(p,epoch,next,item,family);originalResources=SmartFallCapabilities.EffectiveResources(p);source=array;sourceSlot=slot;target=dest;targetSlot=at;original=dest[at];misc=ReferenceEquals(dest,p.miscEquips);loadout=p.CurrentLoadoutIndex;
                borrowedFavorite=borrowed.favorited;originalFavorite=original.favorited;
                originalType=original.type;originalPrefix=original.prefix;originalStack=original.stack;
                try
                {
                    Exchange(false);borrowed.favorited=false;original.favorited=false;Exchanges++;items.World.InvalidateObservation();SyncEndpoints();return true;
                }
                catch
                {
                    // A failed second write is still this journal's operation.
                    // Repair only the identifiable half-write, never guess by ID.
                    RepairHalfWrite();
                    if(ReferenceEquals(source[sourceSlot],original) && ReferenceEquals(target[targetSlot],borrowed))
                    {source[sourceSlot]=borrowed;target[targetSlot]=original;}
                    unknown=!(ReferenceEquals(source[sourceSlot],borrowed) && ReferenceEquals(target[targetSlot],original));
                    ReturnResult=unknown?SmartFallResult.Unconfirmed:SmartFallResult.Failed;
                    if(!unknown)Retire();return false;
                }
            }
            finally{Monitor.Exit(SaveLock);}
        }
        private void Bind(Player p,long epoch,long operation,Item item,SmartFallFamily family)
        {ClearNativeUse();player=p;generation=epoch;token=operation;borrowed=item;borrowedFamily=family;borrowedType=item.type;borrowedPrefix=item.prefix;borrowedStack=item.stack;selection=selected=restoreRequested=pulsed=ownsMouse=ownedUseBit=unknown=false;returnRevision=-1;returnSource=null;ReturnResult=SmartFallResult.None;SyncUnconfirmed=false;}
        private void Exchange(bool reverse)
        {
            Item a=reverse?borrowed:original,b=reverse?original:borrowed;
            source[sourceSlot]=a;Written(1);target[targetSlot]=b;Written(2);
        }
        private void RepairHalfWrite()
        {
            if(ReferenceEquals(source[sourceSlot],original) && ReferenceEquals(target[targetSlot],original))source[sourceSlot]=borrowed;
            else if(ReferenceEquals(source[sourceSlot],borrowed) && ReferenceEquals(target[targetSlot],borrowed))target[targetSlot]=original;
        }
        internal void Restore(Player p,long epoch)
        {
            if(!Identity(p,epoch))return;
            ClearNativeUse();
            restoreRequested=true;
            ReturnResult=SmartFallResult.Returning;
            if(selection){ReturnSelection();return;}
            if(unknown){ReturnResult=SmartFallResult.Unconfirmed;return;}
            // Later loadout/slot edits hand the real containers to the player.
            // Both original objects already reside there; no old snapshot wins.
            if((!misc && p.CurrentLoadoutIndex!=loadout) || !ReferenceEquals(target[targetSlot],borrowed) || borrowed.favorited || original.favorited ||
                borrowed.type!=borrowedType || borrowed.prefix!=borrowedPrefix || borrowed.stack!=borrowedStack ||
                original.type!=originalType || original.prefix!=originalPrefix || original.stack!=originalStack){TakeOver();return;}
            if(!ReferenceEquals(source[sourceSlot],original) && returnSource==null && returnRevision==items.World.Revision)return;
            if(p.dead || p.ghost || p.HasLockedInventory() || p.UsingOrReusingItem || !Monitor.TryEnter(SaveLock))return;
            try
            {
                if(!ReferenceEquals(source[sourceSlot],original))
                {
                    // A moved original is found by reference, never by its type
                    // signature. Retry the bounded search only on a real change.
                    if(returnRevision!=items.World.Revision)
                    {returnRevision=items.World.Revision;returnSource=null;FindReturn(p);}
                    if(!HasPending)return;
                    if(returnSource==null)return;
                    ulong inv=ReferenceEquals(returnSource,p.inventory)?1UL<<returnSlot:0;
                    uint armor=misc?0:1U<<targetSlot;if(ReferenceEquals(returnSource,p.armor))armor|=1U<<returnSlot;
                    if(!items.Ownership.TryMoveFallEquipment(epoch,inv,armor,misc?1U<<targetSlot:0,token))return;
                    source=returnSource;sourceSlot=returnSlot;
                }
                // A legal edit to another functional slot can make the parked
                // original incompatible. Both objects already live in native
                // containers: hand over this layout rather than force a swap.
                if(!misc && !FishingEquipmentPlan.CanSwap(p,source,sourceSlot,targetSlot))
                {borrowed.favorited=borrowedFavorite;original.favorited=originalFavorite;TakeOver();return;}
                try{Exchange(true);borrowed.favorited=borrowedFavorite;original.favorited=originalFavorite;Exchanges++;items.World.InvalidateObservation();SyncEndpoints();ReturnResult=SmartFallResult.Returned;Retire();}
                catch
                {
                    RepairHalfWrite();
                    if(ReferenceEquals(source[sourceSlot],borrowed) && ReferenceEquals(target[targetSlot],original)){borrowed.favorited=borrowedFavorite;original.favorited=originalFavorite;ReturnResult=SmartFallResult.Returned;Retire();}
                    else {unknown=true;ReturnResult=SmartFallResult.Unconfirmed;}
                }
            }
            finally{Monitor.Exit(SaveLock);}
        }
        private void FindReturn(Player p)
        {
            ReturnSearches++;
            bool foundOriginal=false;
            bool Try(Item[] array,int slot)
            {
                Item at=array[slot];
                if(ReferenceEquals(at,original))foundOriginal=true;
                if(at==null || (original.IsAir?!at.IsAir:!ReferenceEquals(at,original)) ||
                    items.World.ManualMaterials.Contains(at) || items.Ownership.IsProtected(ReferenceEquals(array,p.inventory)?0:5,slot))return false;
                if(ReferenceEquals(array,p.inventory) && p.inventoryChestStack[slot])return false;
                if(!misc && !FishingEquipmentPlan.CanSwap(p,array,slot,targetSlot))return false;
                returnSource=array;returnSlot=slot;
                // Air has no persistent item identity; use the actual legal
                // empty endpoint without replacing somebody else's live item.
                if(original.IsAir)original=at;
                return true;
            }
            for(int i=0;i<50;i++)if(Try(p.inventory,i))return;
            if(!misc && !original.IsAir)for(int i=10;i<20;i++)if(p.IsItemSlotUnlockedAndUsable(i) && Try(p.armor,i))return;
            // The player moved this physical item out of our legal domain.
            // It remains where vanilla put it; there is no private object to
            // recover and no reason to retain an unfinishable return job.
            if(originalType>0 && !foundOriginal)TakeOver();
        }
        private void SyncEndpoints()
        {
            if(Main.netMode!=1)return;
            // A loan can begin and end between Main's inventory-delta passes.
            // Publish only these two physical endpoints through vanilla's slot
            // encoding; a send is never interpreted as server acknowledgement.
            try
            {
                int sourceId=(ReferenceEquals(source,player.inventory)?PlayerItemSlotID.Inventory0:PlayerItemSlotID.Armor0)+sourceSlot;
                int targetId=(misc?PlayerItemSlotID.Misc0:PlayerItemSlotID.Armor0)+targetSlot;
                NetMessage.SendData(5,-1,-1,null,player.whoAmI,sourceId);
                NetMessage.SendData(5,-1,-1,null,player.whoAmI,targetId);
            }
            catch{SyncUnconfirmed=true;}
        }
        internal void Pick(Player p,ref int slot,ref bool result)
        {
            if(!selection || !Identity(p,generation))return;
            if(restoreRequested){if(!p.selectedItemState.HasActiveOverride){ReturnResult=SmartFallResult.Returned;Retire();}return;}
            if(result || !ReferenceEquals(p.inventory[heldSlot],borrowed) || !ReferenceEquals(p.inventory[originalSelection],selectionAnchor) || p.selectedItem!=originalSelection){ManualSelection();return;}
            slot=heldSlot;result=true;selected=true;
        }
        private void TakeOver(){ReturnResult=SmartFallResult.TakenOver;Retire();}
        internal void ManualSelection(){if(selection && Identity(player,generation))TakeOver();}
        internal void ManualEquipment(Item[] array,int slot)
        {
            if(!HasPending || selection || !Identity(player,generation))return;
            if(ReferenceEquals(array,source) && slot==sourceSlot || ReferenceEquals(array,target) && slot==targetSlot)TakeOver();
        }
        internal void ManualLoadout(Player p)
        {if(!selection && !misc && Identity(p,generation) && p.CurrentLoadoutIndex!=loadout)TakeOver();}
        private void ReturnSelection()
        {
            ReleaseMouse();ReleaseInput();
            if(!player.selectedItemState.HasActiveOverride){ReturnResult=SmartFallResult.Returned;Retire();return;}
            if(!player.selectedItemState.HasBufferedChange)
            {
                int restore=ReferenceEquals(player.inventory[originalSelection],selectionAnchor)?originalSelection:player.selectedItem;
                items.ReturnSelection(()=>player.selectedItemState.Select(restore));
            }
            // Native selection update, rather than a raw index assignment,
            // settles the override and any later manual buffered selection.
        }
        internal void BeforeSync(Player p,bool useItem)
        {
            if(selection && Identity(p,generation) && ManualUse(p)){ManualSelection();return;}
            if(!useItem || p.noItems || !SelectionReady || !ReferenceEquals(p,player) || pulsed || restoreRequested)return;
            originalUseBit=p.controlUseItem;originalReleaseBit=p.releaseUseItem;ownedUseBit=true;p.controlUseItem=true;p.releaseUseItem=true;pulsed=true;
        }
        internal bool BeginItemCheck(Player p)
        {if(!selection || !Identity(p,generation) || !pulsed || !SelectionReady)return false;if(!Main.mouseLeft){ownsMouse=true;Main.mouseLeft=true;}return true;}
        internal void EndItemCheck(Player p,long operation,bool entered,Exception error)
        {if(operation!=token || !Identity(p,generation) || !entered)return;ReleaseMouse();if(error!=null)Restore(p,generation);}
        private void ReleaseMouse(){if(ownsMouse){if(Main.mouseLeft)Main.mouseLeft=false;ownsMouse=false;}}
        private void ReleaseInput()
        {
            if(!ownedUseBit || player==null)return;
            if(player.controlUseItem)player.controlUseItem=originalUseBit;
            // A native consumed release=false is an effect, not our contribution.
            if(player.releaseUseItem)player.releaseUseItem=originalReleaseBit;
            ownedUseBit=false;
        }
        internal void EndUpdate(Player p,long epoch)
        {if(!Identity(p,epoch))return;ReleaseMouse();ReleaseInput();if(restoreRequested)Restore(p,epoch);}
        internal void EndSession()
        {
            // A lifecycle boundary revokes writes. Items stay in the exact real
            // containers, even if their former player is no longer writable.
            if(player!=null){ReturnResult=SmartFallResult.Unconfirmed;if(Identity(player,generation)){ReleaseMouse();ReleaseInput();}}
            Retire(false);
        }
        internal bool Protect(Item item)
        {return HasPending && Identity(player,generation) && (ReferenceEquals(item,borrowed) || ReferenceEquals(item,original));}
        private void Retire(bool release=true)
        {
            ClearNativeUse();
            if(player==null)return;
            if(release){ReleaseMouse();ReleaseInput();}else{ownsMouse=ownedUseBit=false;}
            if(selection)items?.Ownership.EndUse(generation,token);else items?.Ownership.EndFallEquipment(generation,token);
            fishing?.FallReturned();player=null;source=target=returnSource=null;borrowed=original=selectionAnchor=null;token=0;selection=selected=restoreRequested=false;
        }
    }
}
