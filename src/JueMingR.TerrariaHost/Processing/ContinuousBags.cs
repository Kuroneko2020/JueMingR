using System;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Processing
{
    // One synchronous native container scope per Update. Holding a gesture is
    // only a candidate exclusion, never a lease over the downstream sale range.
    internal sealed class ContinuousBags
    {
        private readonly HostProcessing host;
        private Item bag,key;
        private Item[] keyArray;
        private int slot=-1,keySlot=-1,keyAccount;
        private long nextToken;
        private readonly ulong[] lease=new ulong[5];
        private readonly ulong[] unknownSlots=new ulong[5];
        private bool executing,unknown,claimed;
        private ulong claimedSlots;
        internal Exception Failure {get;private set;}
        internal bool Executing {get{return executing;}}
        internal ContinuousBags(HostProcessing host){this.host=host;}
        private bool PhysicalHold {get{return (host.Input.KeyboardSample.IsKeyDown(Keys.LeftShift) || host.Input.KeyboardSample.IsKeyDown(Keys.RightShift)) &&
            PlayerInput.MouseInfo.RightButton==ButtonState.Pressed && PlayerInput.MouseInfo.LeftButton==ButtonState.Released;}}
        internal bool Holding {get{return host.Value(0) && host.Admit(host.Player) && Main.playerInventory && PhysicalHold && Main.mouseItem!=null && Main.mouseItem.IsAir;}}
        internal bool Controls(Item[] array,int index)
        {return ReferenceEquals(array,host.Player?.inventory) && index>=0 && index<50 &&
            (executing && index==slot || claimed && PhysicalHold && (Eligible(array[index]) || (claimedSlots&(1UL<<index))!=0));}
        internal bool Owns(Item[] array,int index)
        {return executing && (ReferenceEquals(array,host.Player?.inventory) && index==slot && ReferenceEquals(array[index],bag) || ReferenceEquals(array,keyArray) && index==keySlot && ReferenceEquals(array[index],key));}
        internal bool ProtectIntent(Item item){return Holding && (ReferenceEquals(item,bag) || ReferenceEquals(item,key));}
        internal bool SuppressNative(Item[] array,int index){return !executing && Controls(array,index);}
        internal void StartSession(bool fresh){claimed=false;claimedSlots=0;ReleaseIntent();if(fresh){unknown=false;Failure=null;Array.Clear(unknownSlots,0,5);}}
        internal void RetainInterruptedSource(ulong slots){if(executing)unknownSlots[0]|=slots;}
        internal void RestoreUnknown(){if(unknown)host.Items.Ownership.HoldProcessing(host.Runtime.Generation,unknownSlots);}
        internal void EndSession(){claimed=false;claimedSlots=0;ReleaseIntent();}
        internal void ReleaseIntent(){if(!executing){bag=key=null;keyArray=null;slot=keySlot=-1;}if(!PhysicalHold){claimed=false;claimedSlots=0;}}
        private static bool Eligible(Item item){return item!=null && item.stack>0 && item.type>0 && item.type<ItemID.Sets.OpenableBag.Length && ItemID.Sets.OpenableBag[item.type];}
        private bool Protected(Player p,Item[] array,int account,int index)
        {var item=array[index];return host.Items.Ownership.IsProtected(account,index) || host.Items.World.ManualMaterials.Contains(item) || account==0 && (index==host.Items.World.ManualSlot || p.inventoryChestStack[index]) || host.Items.World.AdditionalProtection!=null && host.Items.World.AdditionalProtection(item);}
        internal void Update()
        {
            if(!Holding || unknown){ReleaseIntent();return;}
            var p=host.Player;
            if(p.itemAnimation>0 || p.itemTime>0 || p.chest!=-1 || p.sign>=0 || Main.mouseItem==null || !Main.mouseItem.IsAir || host.Items.World.HasManualOperation)return;
            // Hover chooses a type; native inventory order chooses its first
            // usable stack. No Item clone or retained old request grants rights.
            int preferred=Main.HoverItem==null?0:Main.HoverItem.type;
            int found=-1;
            for(int pass=0;pass<2 && found<0;pass++)for(int i=0;i<50;i++)
                if(Eligible(p.inventory[i]) && (pass!=0 || p.inventory[i].type==preferred) && !Protected(p,p.inventory,0,i)){found=i;break;}
            if(found<0){ReleaseIntent();return;}
            slot=found;bag=p.inventory[slot];key=null;keyArray=null;keySlot=-1;
            claimed=true;claimedSlots|=1UL<<slot;
            int type=bag.type,before=bag.stack;
            if(type==3085 || type==4879)
            {
                int keyType=type==3085?327:329;
                for(int account=0;account<=4;account+=4)
                {
                    if(account==4 && !p.useVoidBag())break;
                    Item[] array=account==0?p.inventory:p.bank4.item;
                    if(account==4 && type==3085)
                    {
                        // .8 ConsumeItem uses FindItem(type, bank4), whose
                        // stack predicate reads the same main-inventory index.
                        // Bind that exact original choice; do not fix or bypass
                        // its qualification by inventing a different key search.
                        int i=p.FindItem(keyType,array);
                        if(i>=0 && array[i].stack>0){key=array[i];keyArray=array;keySlot=i;keyAccount=account;}
                        break;
                    }
                    for(int i=0;i<(account==0?58:40);i++)if(array[i].type==keyType && array[i].stack>0)
                    {key=array[i];keyArray=array;keySlot=i;keyAccount=account;break;}
                    if(key!=null)break;
                }
                // Preserve native first-key choice. A blocked first key does
                // not silently replace the admitted source with another key.
                if(key==null || keyAccount==4 && !(host.BankGuardsReady?.Invoke()??false) || Protected(p,keyArray,keyAccount,keySlot))return;
            }
            Array.Clear(lease,0,5);lease[0]=1UL<<slot;if(key!=null)lease[keyAccount]|=1UL<<keySlot;
            long session=host.Runtime.Generation,token=++nextToken;
            if(!host.Items.Ownership.TryBeginProcessing(session,lease,token))return;
            int keyBefore=key?.stack??0;
            try
            {
                executing=true;
                if(!ReferenceEquals(p.inventory[slot],bag) || bag.type!=type || bag.stack!=before || key!=null && (!ReferenceEquals(keyArray[keySlot],key) || key.stack!=keyBefore) ||
                    type==3085 && keyAccount==4 && p.FindItem(327,keyArray)!=keySlot)return;
                ProcessingHooks.Open(p.inventory,0,slot,p);
                bool consumed=before==1?bag.IsAir:bag.type==type && bag.stack==before-1;
                bool unchanged=bag.type==type && bag.stack==before;
                bool keyOK=type!=3085 || (keyBefore==1?key.IsAir:key.stack==keyBefore-1);
                if(!consumed || !keyOK)
                {
                    if(unchanged && (key==null || key.stack==keyBefore))return;
                    unknown=true;host.Report("开袋结果未确认，已停止自动开袋。");
                }
            }
            catch(Exception error) {Failure=error;unknown=true;host.Report("开袋中断，已停止自动开袋；相关物品不会重复处理。");}
            finally{executing=false;if(unknown)for(int a=0;a<5;a++)unknownSlots[a]|=lease[a];host.Items.Ownership.EndProcessing(session,lease,token,unknown);}
        }
    }
}
