using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using JueMingR.TerrariaHost.KeepFavorited;
using Terraria;
using Terraria.GameInput;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Fishing
{
    // One owner for temporary loadouts and real two-slot equipment exchanges.
    // No Item is cloned or removed into a private inventory. Outstanding return
    // responsibility outlives the fishing session, pauses and disabled switches.
    internal sealed class FishingEquipment
    {
        private delegate void Swap(ref Item a,ref Item b);
        private static readonly Swap NativeSwap=(Swap)Delegate.CreateDelegate(typeof(Swap),typeof(ItemSlot).GetMethod("SwapClearFavorite",BindingFlags.Static|BindingFlags.NonPublic));
        private static readonly object SaveLock=typeof(Player).GetField("IOLock",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        private sealed class Entry
        {
            internal Item[] Source;internal int Slot,Target,Loadout;
            internal Item Borrowed,Original;internal int BorrowType,BorrowPrefix,BorrowStack,OriginalType,OriginalPrefix,OriginalStack;
            internal bool BorrowFavorite,OriginalFavorite;internal long ShareEpoch,SourceShareEpoch;
            internal bool Applied,Unknown;
        }
        private readonly HostFishing host;
        private readonly HostKeepFavorited favorites;
        private readonly Func<Item,bool> priorProtection;
        private readonly List<Entry> entries=new List<Entry>();
        private readonly long[] shareEpochs=new long[20];
        private Player player;
        private object world,socket;
        private long generation,token,suppressed;
        private int networkMode,mode,originalLoadout,appliedLoadout,manualSlots,depth;
        private bool loadoutOwned,returning,failed,dirty=true,haveFingerprint;
        private ulong nextCheck;
        private long fingerprint;
        internal long SourceReads {get;private set;}
        internal long Plans {get;private set;}
        internal long Exchanges {get;private set;}
        internal bool Active {get{return loadoutOwned || entries.Count!=0;}}
        internal int Count {get{return entries.Count;}}
        internal int OperationDepth {get{return depth;}}
        internal string Status {get;private set;}
        internal FishingEquipment(HostFishing host,HostKeepFavorited favorites,Func<Item,bool> priorProtection)
        {this.host=host;this.favorites=favorites;this.priorProtection=priorProtection;}
        private bool Identity()
        {return player!=null && ReferenceEquals(player,Main.LocalPlayer) && ReferenceEquals(world,Main.ActiveWorldFileData) && networkMode==Main.netMode && ReferenceEquals(socket,Main.netMode==1?Netplay.Connection.Socket:null) && generation==host.Tools.Runtime.Generation;}
        internal bool Protect(Item item)
        {if(!Identity())return false;foreach(var e in entries)if(ReferenceEquals(item,e.Borrowed) || ReferenceEquals(item,e.Original))return true;return false;}
        private int Desired()
        {
            if(failed || !host.Ready || !host.Session.Active || suppressed==host.Session.Token)return 0;
            var value=host.Settings.Value;return value.Loadout?1:value.Equipment?2:0;
        }
        internal void Update()
        {
            int desired=Desired();
            if(!Active && desired==0)
            {
                // A prepared best-already-worn plan is not a loan. Retire it
                // with its fishing identity without inventing a return result.
                if(player!=null && (!host.Session.Active || token!=host.Session.Token)){ClearStatus();ClearOwner();}
                return; // OFF has no clock or container work.
            }
            if(player!=null && !Identity()){EndSession();host.Session.Stop();return;}
            if(player==null)
            {
                if(desired==0 || host.Player==null || !host.Session.InLiquid || Truffle())return;
                player=host.Player;world=Main.ActiveWorldFileData;socket=Main.netMode==1?Netplay.Connection.Socket:null;networkMode=Main.netMode;generation=host.Tools.Runtime.Generation;
                token=host.Session.Token;originalLoadout=player.CurrentLoadoutIndex;manualSlots=0;mode=desired;haveFingerprint=false;dirty=true;
            }
            // A pending unrelated save pauses new swaps, but does not tear down
            // a retained strategy. Explicit strategy changes start return first.
            var requested=host.Settings.Busy?host.Settings.Requested:host.Settings.Value;
            bool keep=host.KeepsSession && host.Session.Active && host.Session.Token==token && (mode==1?requested.Loadout:requested.Equipment) && !failed;
            if(!keep || player.dead)returning=true;
            bool gate=returning?ReturnGate(false):ApplyGate();
            if(!gate){dirty=true;return;}
            // Native autosave serializes these same objects on a worker. Keep
            // split/park/exchange and reverse pairs indivisible to that reader;
            // never block the game thread while a player save holds its lock.
            if(!Monitor.TryEnter(SaveLock)){dirty=true;return;}
            try{Advance(desired);}finally{Monitor.Exit(SaveLock);}
        }
        private void Advance(int desired)
        {
            ulong now=host.Tools.Tick;if(!dirty && now<nextCheck)return;nextCheck=now+20;dirty=false;
            long current=Fingerprint();if(haveFingerprint && fingerprint==current)return;fingerprint=current;haveFingerprint=true;
            if(returning)
            {
                Restore(false);if(!Active){ClearOwner();if(desired!=0)dirty=true;}return;
            }
            if(player.CurrentLoadoutIndex!=originalLoadout && mode==2)return;
            if(mode==1)
            {
                Plans++;int best=FishingEquipmentPlan.BestLoadout(player);
                if(best!=player.CurrentLoadoutIndex)Switch(best,false);
            }
            else
            {
                // At most one pass over the ten physical wear slots per changed
                // snapshot. Each swap revalidates its actual native endpoints.
                for(int i=0;i<10;i++)
                {
                    Plans++;var move=FishingEquipmentPlan.Next(player,host.Session.Liquid==3,Allowed,manualSlots);
                    if(move==null || !Apply(move))break;
                }
            }
            fingerprint=Fingerprint();
        }
        private bool Truffle()
        {var p=host.Player;if(p==null)return false;for(int i=54;i<58;i++)if(!p.inventory[i].IsAir && p.inventory[i].bait>0)return p.inventory[i].type==Terraria.ID.ItemID.TruffleWorm;for(int i=0;i<50;i++)if(!p.inventory[i].IsAir && p.inventory[i].bait>0)return p.inventory[i].type==Terraria.ID.ItemID.TruffleWorm;return false;}
        private bool ApplyGate()
        {return Identity() && host.Ready && !Truffle() && host.Tools.AdmitFishingEquipment(player,host.CanEquipmentInterface?.Invoke()??false) && !player.UsingOrReusingItem && !host.Tools.Use.Active && !host.Tools.Fishing.Active && !player.selectedItemState.HasBufferedChange && (!host.Tools.Input.IsFocused || !PlayerInput.Triggers.Current.MouseLeft && PlayerInput.MouseInfo.LeftButton==Microsoft.Xna.Framework.Input.ButtonState.Released && PlayerInput.MouseInfo.RightButton==Microsoft.Xna.Framework.Input.ButtonState.Released);}
        private bool ReturnGate(bool boundary)
        {
            return Identity() && !Main.gameMenu && !Main.ServerSideCharacter && !(Main.ActivePlayerFileData?.ServerSideCharacter??false) && !Main.gamePaused &&
                !player.UsingOrReusingItem && !host.Tools.Use.InNativeUse && !player.HasLockedInventory() && !host.Tools.Items.World.Busy && !host.Tools.Items.World.HasManualOperation &&
                // Foreground returns require physical release. Background mouse
                // samples may be stale; real manual ownership above still blocks.
                Main.mouseItem!=null && Main.mouseItem.IsAir && (boundary || host.Tools.Input.CanRetainAutomaticIntent && (!host.Tools.Input.IsFocused || !PlayerInput.Triggers.Current.MouseLeft && !PlayerInput.Triggers.Current.MouseRight && PlayerInput.MouseInfo.LeftButton==Microsoft.Xna.Framework.Input.ButtonState.Released && PlayerInput.MouseInfo.RightButton==Microsoft.Xna.Framework.Input.ButtonState.Released));
        }
        private bool Allowed(Item[] array,int slot)
        {
            Item item=array[slot];if(item==null || host.Tools.Items.World.ManualMaterials.Contains(item) || (priorProtection?.Invoke(item)??false) || Protect(item))return false;
            if(ReferenceEquals(array,player.inventory))return slot<50 && !player.inventoryChestStack[slot] && !host.Tools.Items.Ownership.IsProtected(slot);
            if(ReferenceEquals(array,player.bank4.item))return player.useVoidBag() && !host.Tools.Items.Ownership.IsProtected(4,slot);
            return ReferenceEquals(array,player.armor) && slot>=10 && player.IsItemSlotUnlockedAndUsable(slot);
        }
        private bool Apply(FishingEquipmentPlan.Move move)
        {
            if(move.Item.stack>1 && !Split(move))return false;
            if(!ApplyGate() || !Allowed(move.Source,move.Slot) || !ReferenceEquals(move.Source[move.Slot],move.Item) || !FishingEquipmentPlan.CanSwap(player,move.Source,move.Slot,move.Target))return false;
            var original=player.armor[move.Target];
            if((priorProtection?.Invoke(original)??false) || host.Tools.Items.World.ManualMaterials.Contains(original))return false;
            var e=new Entry{Source=move.Source,Slot=move.Slot,Target=move.Target,Loadout=player.CurrentLoadoutIndex,Borrowed=move.Item,Original=original,
                BorrowType=move.Item.type,BorrowPrefix=move.Item.prefix,BorrowStack=move.Item.stack,OriginalType=original.type,OriginalPrefix=original.prefix,OriginalStack=original.stack,
                BorrowFavorite=move.Item.favorited,OriginalFavorite=original.favorited,ShareEpoch=shareEpochs[move.Target],SourceShareEpoch=ReferenceEquals(move.Source,player.armor)?shareEpochs[move.Slot]:0};
            entries.Add(e);depth++;bool favoriteScope=false;
            try
            {
                favorites?.Begin();favoriteScope=true;
                NativeSwap(ref move.Source[move.Slot],ref player.armor[move.Target]);
                e.Applied=ReferenceEquals(move.Source[move.Slot],original) && ReferenceEquals(player.armor[move.Target],move.Item);
                if(!e.Applied)
                {
                    if(ReferenceEquals(move.Source[move.Slot],move.Item) && ReferenceEquals(player.armor[move.Target],original))entries.Remove(e);
                    else e.Unknown=true;
                    Fail("装备交换未确认，已停止配装并保留实际物品。");return false;
                }
                Exchanges++;host.Tools.Items.World.InvalidateObservation();return true;
            }
            catch{e.Unknown=!ReferenceEquals(move.Source[move.Slot],original) || !ReferenceEquals(player.armor[move.Target],move.Item);e.Applied=!e.Unknown;Fail("装备交换异常，已停止配装；未确认的格子不会重写。");return false;}
            finally{try{if(favoriteScope)favorites?.End();}catch{Fail("收藏状态检查异常，已停止配装并保留实际物品。");}finally{depth--;}}
        }
        private bool Split(FishingEquipmentPlan.Move move)
        {
            if(!ApplyGate() || !Allowed(move.Source,move.Slot) || !ReferenceEquals(move.Source[move.Slot],move.Item) || !FishingEquipmentPlan.CanSwap(player,move.Source,move.Slot,move.Target,true))return false;
            bool inventory=ReferenceEquals(move.Source,player.inventory),bank=ReferenceEquals(move.Source,player.bank4.item);
            if(!inventory && !bank)return false;
            int empty=-1;for(int i=0;i<(inventory?50:40);i++)if(move.Source[i].IsAir && Allowed(move.Source,i)){empty=i;break;}
            if(empty<0){Say("成堆钓鱼装备需要在原背包或虚空袋留一个安全空格，等待空位后配装。");return false;}
            Item source=move.Item;int count=source.stack,type=source.type,prefix=source.prefix;Item vacant=move.Source[empty];
            depth++;bool favoriteScope=false;
            try
            {
                favorites?.Begin();favoriteScope=true;
                // Use the original one-item split; never manufacture a clone or
                // put an entire stack into armor. The new native item is parked
                // in the SAME container before the normal exchange journal owns
                // it. It remains a separate singleton on return, preserving the
                // real source stack and each native death/storage domain.
                ItemSlot.PickupItemIntoMouse(move.Source,inventory?0:32,move.Slot,player);
                if(!ReferenceEquals(move.Source[move.Slot],source) || source.stack!=count-1 || source.type!=type || source.prefix!=prefix || Main.mouseItem.type!=type || Main.mouseItem.prefix!=prefix || Main.mouseItem.stack!=1 || !ReferenceEquals(move.Source[empty],vacant) || !vacant.IsAir)
                {Fail("装备拆分结果未确认，实际物品和鼠标物品已保留，请手动检查。");return false;}
                Item single=Main.mouseItem;NativeSwap(ref move.Source[empty],ref Main.mouseItem);
                if(!ReferenceEquals(move.Source[empty],single) || !Main.mouseItem.IsAir){Fail("装备拆分落位未确认，已停止配装并保留实际物品。");return false;}
                move.Slot=empty;move.Item=single;host.Tools.Items.World.InvalidateObservation();return true;
            }
            catch{Fail("装备拆分发生异常，已停止配装；请检查实际背包及鼠标物品。");return false;}
            finally{try{if(favoriteScope)favorites?.End();}catch{Fail("收藏状态检查异常，已停止配装并保留实际物品。");}finally{depth--;}}
        }
        private void Switch(int target,bool restore)
        {
            int before=player.CurrentLoadoutIndex;depth++;
            try{player.TrySwitchingLoadout(target);}
            catch{Fail("装备组切换发生异常，请检查当前装备组。");}
            finally
            {
                depth--;int after=player.CurrentLoadoutIndex;
                if(after!=before){appliedLoadout=after;loadoutOwned=!restore && after!=originalLoadout;host.Tools.Items.World.InvalidateObservation();}
                if(after==originalLoadout)loadoutOwned=false;
            }
        }
        private static bool Same(Item item,int type,int prefix,int stack)
        {return item!=null && item.type==type && item.prefix==prefix && item.stack==stack;}
        private bool ReturnAddress(Item[] array,int slot)
        {
            if(slot<0 || slot>=array.Length || (priorProtection?.Invoke(array[slot])??false) || host.Tools.Items.World.ManualMaterials.Contains(array[slot]))return false;
            if(ReferenceEquals(array,player.inventory))return slot<50 && !player.inventoryChestStack[slot] && !host.Tools.Items.Ownership.IsProtected(slot);
            if(ReferenceEquals(array,player.bank4.item))return player.useVoidBag() && !host.Tools.Items.Ownership.IsProtected(4,slot);
            return ReferenceEquals(array,player.armor) && slot>=10 && player.IsItemSlotUnlockedAndUsable(slot);
        }
        private bool FindReturn(Entry e,bool boundary,out Item[] array,out int slot)
        {
            array=null;slot=-1;
            if(!e.Applied || e.Unknown || !ReferenceEquals(player.armor[e.Target],e.Borrowed) || !Same(e.Borrowed,e.BorrowType,e.BorrowPrefix,e.BorrowStack))return false;
            // Loss of void access pauses this loan, including fallback. A readable
            // array does not authorize moving either endpoint through that bank.
            if(ReferenceEquals(e.Source,player.bank4.item) && !player.useVoidBag())return false;
            if(ReturnAddress(e.Source,e.Slot) && ReferenceEquals(e.Source[e.Slot],e.Original) && Same(e.Original,e.OriginalType,e.OriginalPrefix,e.OriginalStack) && FishingEquipmentPlan.CanSwap(player,e.Source,e.Slot,e.Target))
            {array=e.Source;slot=e.Slot;return true;}
            if(boundary)return false; // Never change native death-loss domains.
            Item[] found=null;int index=-1;
            bool Search(Item[] source,int start,int count)
            {
                for(int i=start;i<count;i++)
                {
                    var item=source[i];bool match=e.OriginalType==0?item.IsAir:ReferenceEquals(item,e.Original) && Same(item,e.OriginalType,e.OriginalPrefix,e.OriginalStack);
                    if(match && ReturnAddress(source,i) && FishingEquipmentPlan.CanSwap(player,source,i,e.Target)){found=source;index=i;return true;}
                }
                return false;
            }
            if(e.OriginalType==0 && ReferenceEquals(e.Source,player.bank4.item))Search(player.bank4.item,0,40);
            if(found==null)Search(player.inventory,0,50);
            if(found==null && e.OriginalType!=0 && player.useVoidBag())Search(player.bank4.item,0,40);
            if(found==null && e.OriginalType!=0)Search(player.armor,10,20);
            array=found;slot=index;return array!=null;
        }
        private void Restore(bool boundary)
        {
            if(!ReturnGate(boundary))return;
            if(loadoutOwned)
            {
                if(player.CurrentLoadoutIndex!=appliedLoadout)loadoutOwned=false;
                else if(!player.dead && !player.CCed)Switch(originalLoadout,true);
            }
            for(int i=entries.Count-1;i>=0;i--)
            {
                var e=entries[i];if(player.CurrentLoadoutIndex!=e.Loadout)continue;
                Item[] source;int slot;if(!FindReturn(e,boundary,out source,out slot))continue;
                Item returningOriginal=source[slot];
                // Restoration is an inverse of this exact exchange. Native
                // favorite flags are separate from item/reference identity.
                depth++;bool favoriteScope=false;
                try
                {
                    favorites?.Begin();favoriteScope=true;
                    NativeSwap(ref source[slot],ref player.armor[e.Target]);
                    if(!ReferenceEquals(source[slot],e.Borrowed) || !ReferenceEquals(player.armor[e.Target],returningOriginal)){e.Unknown=true;Fail("部分装备归还未能确认，实际物品已保留。");continue;}
                    if(e.BorrowFavorite && ReferenceEquals(source,e.Source) && (!ReferenceEquals(source,player.armor) || slot==e.Slot && shareEpochs[e.Slot]==e.SourceShareEpoch) && !e.Borrowed.favorited)e.Borrowed.favorited=true;
                    if(e.OriginalFavorite && shareEpochs[e.Target]==e.ShareEpoch && !e.Original.favorited)e.Original.favorited=true;
                    entries.RemoveAt(i);Exchanges++;host.Tools.Items.World.InvalidateObservation();
                }
                catch{e.Unknown=true;Fail("部分装备归还异常，未自动重复交换。");}
                finally{try{if(favoriteScope)favorites?.End();}catch{Fail("收藏状态检查异常，已停止配装并保留实际物品。");}finally{depth--;}}
            }
            if(Active)Say("部分钓鱼装备待归还；请放下鼠标物品并回到原装备组，保留原物位置。");
            else ClearStatus();
        }
        internal void BeforeBoundary(Player current,bool quitting)
        {
            if(!Active || !Identity() || !ReferenceEquals(current,player))return;returning=true;host.Session.Stop();
            if(Monitor.TryEnter(SaveLock)){try{Restore(true);}finally{Monitor.Exit(SaveLock);}}
            if(quitting && Active)Say("退出时部分钓鱼装备未归还，已保留在角色的实际物品格中。");
        }
        internal void AfterDrop(Player current)
        {
            if(!Identity() || !ReferenceEquals(current,player))return;
            // Medium/hardcore native DropItems removes real objects. Retire only
            // entries proved gone from every native owned container, never refill.
            for(int i=entries.Count-1;i>=0;i--)
            {
                var e=entries[i];
                if(!Same(e.Borrowed,e.BorrowType,e.BorrowPrefix,e.BorrowStack) || !Contains(e.Borrowed) || e.OriginalType>0 && (!Same(e.Original,e.OriginalType,e.OriginalPrefix,e.OriginalStack) || !Contains(e.Original)))
                {entries.RemoveAt(i);Say("原版掉落已接管部分装备；不会重建物品。");}
            }
            dirty=true;haveFingerprint=false;
        }
        private bool Contains(Item item)
        {
            if(ReferenceEquals(Main.mouseItem,item))return true;
            foreach(var array in new[]{player.inventory,player.armor,player.bank4.item})foreach(var value in array)if(ReferenceEquals(value,item))return true;
            foreach(var loadout in player.Loadouts)foreach(var value in loadout.Armor)if(ReferenceEquals(value,item))return true;return false;
        }
        internal void ManualLoadout(Player current,int before)
        {
            if(depth!=0 || !Identity() || !ReferenceEquals(current,player) || before==player.CurrentLoadoutIndex)return;
            if(mode==1){loadoutOwned=false;suppressed=host.Session.Token;ClearStatus();ClearOwner();}
            else{dirty=true;haveFingerprint=false;if(Active)Say("已切换装备组；原钓鱼配装记录等待回到原组后处理。");}
        }
        internal void ManualSlots()
        {
            if(depth!=0 || !Identity() || entries.Count==0)return;
            int visited=0,taken=0;
            for(int i=entries.Count-1;i>=0;i--)
            {
                var e=entries[i];if(player.CurrentLoadoutIndex!=e.Loadout || (visited&(1<<e.Target))!=0)continue;
                visited|=1<<e.Target;if(!ReferenceEquals(player.armor[e.Target],e.Borrowed))taken|=1<<e.Target;
            }
            for(int target=0;target<10;target++)if((taken&(1<<target))!=0)YieldTarget(target);
            dirty=true;haveFingerprint=false;
        }
        internal void Favorite(Item item)
        {if(depth!=0 || !Identity() || item==null)return;for(int i=entries.Count-1;i>=0;i--){var e=entries[i];if(ReferenceEquals(item,e.Borrowed) || ReferenceEquals(item,e.Original)){YieldTarget(e.Target);break;}}}
        internal void Shared(Item[] array,int slot)
        {
            if(depth!=0 || !Identity() || !ReferenceEquals(array,player.armor) || slot<0 || slot>=20)return;
            shareEpochs[slot]++;Favorite(array[slot]);dirty=true;haveFingerprint=false;
        }
        private void YieldTarget(int target)
        {
            // A later explicit slot/favorite/share intention takes over the
            // entire upgrade chain for this target, including older displaced
            // items. Keep every real item and flag where the player left it.
            manualSlots|=1<<target;entries.RemoveAll(e=>e.Target==target);dirty=true;haveFingerprint=false;
            Say("玩家已接管部分装备格及收藏/共享设置，保留当前物品位置，不再自动归还这些格子。");
        }
        private long Fingerprint()
        {
            long hash=17;
            void Value(long value){hash=unchecked(hash*31+value);}
            void Array(Item[] array,int count)
            {
                for(int i=0;i<count;i++)
                {
                    var item=array[i];SourceReads++;Value(item==null?0:RuntimeHelpers.GetHashCode(item));if(item==null)continue;
                    Value(item.type);Value(item.prefix);Value(item.stack);Value(item.favorited?1:0);Value(item.expertOnly?1:0);Value(item.accessory?1:0);Value(item.headSlot);Value(item.bodySlot);Value(item.legSlot);
                    Value((priorProtection?.Invoke(item)??false)?1:0);Value(host.Tools.Items.World.ManualMaterials.Contains(item)?1:0);
                    if(ReferenceEquals(array,player.inventory)){Value(host.Tools.Items.Ownership.IsProtected(i)?1:0);Value(player.inventoryChestStack[i]?1:0);}
                    else if(ReferenceEquals(array,player.bank4.item))Value(host.Tools.Items.Ownership.IsProtected(4,i)?1:0);
                }
            }
            Value(player.CurrentLoadoutIndex);Value(player.extraAccessory?1:0);Value(Main.expertMode?1:0);Value(Main.masterMode?1:0);Value(host.Session.Liquid);Value(returning?1:0);Value(player.dead?1:0);Value(player.CCed?1:0);
            Array(player.armor,20);Array(player.inventory,50);bool voidAvailable=player.useVoidBag();Value(voidAvailable?1:0);if(voidAvailable)Array(player.bank4.item,40);
            foreach(var loadout in player.Loadouts)Array(loadout.Armor,20);return hash;
        }
        internal void Fail(string message){failed=true;returning=true;dirty=true;haveFingerprint=false;Say(message);}
        private void Say(string message){Status=message;host.Report(message);}
        private void ClearStatus(){host.ClearReport(Status);Status=null;}
        internal void EndSession(){if(Active)Say("会话已结束，未归还装备保留在实际角色物品格；不会带入下一角色重试。");Detach();}
        private void Detach(){entries.Clear();loadoutOwned=false;ClearOwner();}
        private void ClearOwner(){player=null;world=socket=null;mode=0;returning=false;dirty=true;haveFingerprint=false;Array.Clear(shareEpochs,0,shareEpochs.Length);manualSlots=0;}
        internal void StartSession(){Detach();failed=false;suppressed=0;Status=null;}
    }
}
