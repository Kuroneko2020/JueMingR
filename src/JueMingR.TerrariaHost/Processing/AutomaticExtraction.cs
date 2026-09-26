using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Processing
{
    // Native SelectedItemState owns selection and animation. This owner holds
    // one exact material through that animation; each consume is revalidated.
    internal sealed class AutomaticExtraction
    {
        private readonly HostProcessing host;
        private Player player;
        private Item material;
        private int slot,original,type,expected,products;
        private long token,nextToken,session,frame,nextProbe;
        private bool cancelled,unknown,inCheck,borrowed,attempted;
        private ExtractionMachine machine;
        private int mouseX,mouseY,targetX,targetY,ownMouseX,ownMouseY;
        private bool mouseLeft;
        private readonly ulong[] unknownSlots=new ulong[5];
        internal Exception Failure {get;private set;}
        internal bool Active {get{return player!=null;}}
        internal long Operation {get{return token;}}
        internal AutomaticExtraction(HostProcessing host){this.host=host;}
        private bool Admitted(Player p)
        {
            return host.Value(1) && host.Admit(p) && p.chest==-1 && p.talkNPC<0 && p.sign<0 && Main.npcShop==0 &&
                Main.mouseItem!=null && Main.mouseItem.IsAir && !p.mouseInterface && !p.selectedItemState.HasBufferedChange &&
                !PlayerInput.Triggers.Current.MouseLeft && !PlayerInput.Triggers.Current.MouseRight && !PlayerInput.Triggers.Current.SmartSelect &&
                PlayerInput.MouseInfo.LeftButton==ButtonState.Released && PlayerInput.MouseInfo.RightButton==ButtonState.Released && !host.Items.World.HasManualOperation;
        }
        private static bool Eligible(Item item){return item!=null && item.stack>0 && item.type>0 && item.type<ItemID.Sets.ExtractinatorMode.Length && ItemID.Sets.ExtractinatorMode[item.type]>=0;}
        private bool Candidate(Player p,int i){return i>=0 && i<50 && Eligible(p.inventory[i]) && !host.Items.Ownership.IsProtected(i) &&
            !p.inventoryChestStack[i] && !host.Items.World.ManualMaterials.Contains(p.inventory[i]) && !(host.Items.World.AdditionalProtection?.Invoke(p.inventory[i])??false);}
        internal void Pick(Player p,ref int chosen,ref bool result)
        {
            if(!ReferenceEquals(p,host.Player))return;
            if(Active)
            {
                bool newer=p.selectedItem!=original;
                Retire(false);
                if(newer)return;
            }
            if(unknown || result || !Admitted(p) || host.Input.Frame<nextProbe)return;
            host.Items.World.RefreshManualRelease();
            int candidate=Candidate(p,p.selectedItem)?p.selectedItem:-1;
            for(int i=0;i<50 && candidate<0;i++)if(Candidate(p,i))candidate=i;
            if(candidate<0){nextProbe=host.Input.Frame+6;return;}
            ExtractionMachine target;
            if(!ExtractionMachine.Find(p,p.inventory[candidate],out target)){nextProbe=host.Input.Frame+6;return;}
            long next=++nextToken;if(!host.Items.Ownership.TryBeginUse(host.Runtime.Generation,candidate,next))return;
            token=next;session=host.Runtime.Generation;player=p;slot=candidate;original=p.selectedItem;material=p.inventory[slot];type=material.type;expected=material.stack;
            cancelled=false;machine=target;chosen=slot;result=true;
        }
        private bool SameSource(){return player!=null && session==host.Runtime.Generation && ReferenceEquals(player.inventory[slot],material) && material.type==type && material.stack==expected && expected>0;}
        internal void BeforeSync(Player p)
        {
            if(!ReferenceEquals(p,player))return;
            if(cancelled || !Admitted(p) || p.selectedItem!=slot || !SameSource()){Cancel();return;}
            // This precedes native packet 13. No private network protocol or
            // synthetic physical mouse sample is introduced.
            p.controlUseItem=true;p.releaseUseItem=true;frame=host.Input.Frame;
        }
        internal bool Begin(Player p)
        {
            if(!ReferenceEquals(p,player) || p.selectedItem!=slot)return false;
            inCheck=true;products=0;attempted=false;
            if(cancelled || !Admitted(p) || !SameSource()){Cancel();return true;}
            mouseX=Main.mouseX;mouseY=Main.mouseY;mouseLeft=Main.mouseLeft;targetX=Player.tileTargetX;targetY=Player.tileTargetY;
            Vector2 screen=Main.ReverseGravitySupport(new Vector2(machine.X*16+8,machine.Y*16+8)-Main.screenPosition);
            Main.mouseX=ownMouseX=(int)screen.X;Main.mouseY=ownMouseY=(int)screen.Y;Main.mouseLeft=true;
            Player.tileTargetX=machine.X;Player.tileTargetY=machine.Y;borrowed=true;return true;
        }
        internal bool Owns(Item[] array,int index){return inCheck && ReferenceEquals(array,player?.inventory) && index==slot && ReferenceEquals(array[index],material);}
        internal bool Place(Player p,ref Player.ItemCheckContext context)
        {
            if(!inCheck || !ReferenceEquals(p,player))return true;
            // Always intercept the whole placement branch in our own scope.
            // An invalid machine must not turn silt into a placed block.
            if(cancelled || !SameSource() || !Admitted(p) || !ExtractionMachine.Valid(p,material,machine.X,machine.Y))
            {context.SkipItemConsumption=true;Cancel();return false;}
            if(p.ItemTimeIsZero && p.itemAnimation>0 && p.controlUseItem)
            {
                ExtractionMachine nearest;
                if(!ExtractionMachine.Find(p,material,out nearest)){context.SkipItemConsumption=true;Cancel();return false;}
                machine=nearest;Player.tileTargetX=nearest.X;Player.tileTargetY=nearest.Y;
                attempted=true;ProcessingHooks.Extract(p,ref context);
            }
            else context.SkipItemConsumption=true;
            return false;
        }
        internal void Product(Player p){if(inCheck && ReferenceEquals(p,player))products++;}
        internal void End(Player p,long operation,bool entered,Exception error)
        {
            if(!entered || operation!=token || !ReferenceEquals(p,player))return;
            RestoreMouse();inCheck=false;
            if(error!=null){Failure=error;Fail();return;}
            int after=material.stack;
            if(products>0)
            {
                if(!ReferenceEquals(p.inventory[slot],material) && !p.inventory[slot].IsAir || after!=expected-1){Fail();return;}
                expected=after;if(after<=0 || material.IsAir)Cancel();
            }
            // Native cleanup may turn a proved last consumption into Air on a
            // later animation frame. That frame did not start another extract.
            else if(attempted && (material.type!=type || after!=expected)){Fail();return;}
        }
        private void Fail(){unknown=true;unknownSlots[0]|=1UL<<slot;host.Items.Ownership.HoldProcessing(session,unknownSlots);host.Report("提炼结果未确认，已停止自动提炼；材料不会重试。");Cancel();}
        internal void Cancel()
        {
            cancelled=true;
            if(player!=null && player.selectedItemState.HasActiveOverride && !player.selectedItemState.HasBufferedChange)player.selectedItemState.Select(original);
            if(player!=null && frame==host.Input.Frame && !PlayerInput.Triggers.Current.MouseLeft){player.controlUseItem=false;player.releaseUseItem=true;}
        }
        internal void Update()
        {
            if(unknown)host.Items.Ownership.HoldProcessing(host.Runtime.Generation,unknownSlots);
            if(!Active)return;
            if(!Admitted(player))Cancel();
            if(player.selectedItem!=slot || !player.selectedItemState.HasActiveOverride && player.selectedItemState.CanChangeSelectedItemImmediately)Retire(false);
        }
        internal void Reset(bool fresh){Retire(true);nextProbe=0;if(fresh){unknown=false;Failure=null;Array.Clear(unknownSlots,0,5);}}
        private void Retire(bool returnSelection)
        {
            if(player==null)return;if(returnSelection)Cancel();RestoreMouse();
            host.Items.Ownership.EndUse(session,token);player=null;material=null;token=0;inCheck=false;
        }
        private void RestoreMouse()
        {
            if(!borrowed)return;borrowed=false;
            if(Main.mouseX==ownMouseX)Main.mouseX=mouseX;if(Main.mouseY==ownMouseY)Main.mouseY=mouseY;
            if(Main.mouseLeft && PlayerInput.MouseInfo.LeftButton==ButtonState.Released)Main.mouseLeft=mouseLeft;
            if(Player.tileTargetX==machine.X)Player.tileTargetX=targetX;if(Player.tileTargetY==machine.Y)Player.tileTargetY=targetY;
        }
    }
}
