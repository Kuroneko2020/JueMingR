using System;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.Localization;

namespace JueMingR.TerrariaHost.Processing
{
    // Draw supplies the original quote/geometry. Only Update advances automatic
    // payment and roll; the native manual button is gated before its debit.
    internal sealed class AutomaticReforge
    {
        private readonly HostProcessing host;
        private readonly ReforgePayment payment;
        internal readonly ReforgeTargets Targets;
        private bool down,owned,tail,manual,executing,unknown,paid;
        private Item item;
        private NPC npc;
        private GameCulture culture;
        private long revision,pressFrame=-1,spentFrame=-1,token,nextToken,session;
        private int type,stack;
        private bool quoteReady,quoteDiscount;
        private long quote,quoteFrame;
        private Item quotedItem;
        private int quotedType,quotedPrefix,quotedValue,quotedStack,cx,cy,radius,width,height;
        private float quoteAdjustment,scale;
        private readonly ulong[] unknownSlots=new ulong[5];
        internal Exception Failure {get;private set;}
        internal bool Executing {get{return executing;}}
        internal AutomaticReforge(HostProcessing host){this.host=host;payment=new ReforgePayment(host);Targets=new ReforgeTargets(host);}
        private bool Valid()
        {
            Player p=host.Player;
            return host.Value(2) && host.Admit(p) && Main.playerInventory && Main.InReforgeMenu && !Main.InGuideCraftMenu && Main.npcShop==0 &&
                p.chest==-1 && p.talkNPC>=0 && p.talkNPC<Main.npc.Length && Main.npc[p.talkNPC]!=null && Main.npc[p.talkNPC].active && Main.npc[p.talkNPC].type==107 &&
                Main.reforgeItem!=null && !Main.reforgeItem.IsAir && Main.mouseItem!=null && Main.mouseItem.IsAir && !PlayerInput.IgnoreMouseInterface && (host.BankGuardsReady?.Invoke()??false);
        }
        private bool Same(){return ReferenceEquals(item,Main.reforgeItem) && item.type==type && item.stack==stack && host.Settings[2].Revision==revision &&
            ReferenceEquals(culture,Language.ActiveCulture) && host.Player!=null && host.Player.talkNPC>=0 && ReferenceEquals(npc,Main.npc[host.Player.talkNPC]);}
        internal void Update()
        {
            if(unknown)host.Items.Ownership.HoldProcessing(host.Runtime.Generation,unknownSlots);
            bool now=PlayerInput.MouseInfo.LeftButton==ButtonState.Pressed;
            // Focus-loss pseudo releases cannot create another manual allowance.
            if(!now && host.Input.CanStartActions){down=owned=tail=manual=false;item=null;npc=null;pressFrame=-1;return;}
            if(!down){down=true;pressFrame=host.Input.Frame;}
            if(owned && (!Valid() || !Same())){owned=false;tail=true;}
            if(!now || tail || unknown || !Valid() || !FreshQuote())return;
            if(!owned)
            {
                if(pressFrame!=host.Input.Frame || !Targets.HasTargets(Main.reforgeItem))return;
                item=Main.reforgeItem;type=item.type;stack=item.stack;npc=Main.npc[host.Player.talkNPC];culture=Language.ActiveCulture;revision=host.Settings[2].Revision;
                // Fresh already-matched presses retain exactly the ordinary
                // manual roll. It cannot later become a new automatic tail.
                if(Targets.Matches(item)){manual=true;tail=true;return;}
                owned=true;
            }
            if(ReforgeHooks.Cooldown()>0 || spentFrame==host.Input.Frame)return;
            long cost=quote;quoteReady=false;
            if(Pay(host.Player,cost,-1))
            {try{ReforgeHooks.Roll();}catch(Exception error){if(executing)Fail(error);}}
        }
        internal bool Observe(bool hovered,int x,int y,int halfSize,long nativeCost)
        {
            if(!host.Value(2) || !Valid()){quoteReady=false;return hovered;}
            var p=host.Player;quotedItem=Main.reforgeItem;quotedType=quotedItem.type;quotedPrefix=quotedItem.prefix;quotedValue=quotedItem.value;quotedStack=quotedItem.stack;
            quote=nativeCost;quoteFrame=host.Input.Frame;quoteDiscount=p.discountAvailable;quoteAdjustment=p.currentShoppingSettings.PriceAdjustment;
            // Main.screenWidth/Height change domains during native UI Draw.
            // Compare the physical viewport in both Draw and Update instead.
            cx=x;cy=y;radius=halfSize;scale=Main.UIScale;width=(int)PlayerInput.OriginalScreenSize.X;height=(int)PlayerInput.OriginalScreenSize.Y;quoteReady=true;return hovered;
        }
        private bool FreshQuote()
        {
            var p=host.Player;var current=Main.reforgeItem;
            if(!quoteReady || host.Input.Frame-quoteFrame>1 || host.Input.Frame<quoteFrame || !ReferenceEquals(quotedItem,current) ||
                current.type!=quotedType || current.prefix!=quotedPrefix || current.value!=quotedValue || current.stack!=quotedStack ||
                quoteDiscount!=p.discountAvailable || quoteAdjustment!=p.currentShoppingSettings.PriceAdjustment || scale!=Main.UIScale || width!=(int)PlayerInput.OriginalScreenSize.X || height!=(int)PlayerInput.OriginalScreenSize.Y)return false;
            int x=(int)(host.Input.PhysicalMapX*(1f/scale)),y=(int)(host.Input.PhysicalMapY*(1f/scale));
            return x>cx-radius && x<cx+radius && y>cy-radius && y<cy+radius;
        }
        internal bool NativePayment(Player p,long cost,int currency)
        {
            if(manual)
            {
                manual=false;
                if(!Valid() || !Same() || !FreshQuote() || cost!=quote || pressFrame!=host.Input.Frame || PlayerInput.MouseInfo.LeftButton!=ButtonState.Pressed)return false;
                return p.BuyItem(cost,currency);
            }
            return !owned && !tail && p.BuyItem(cost,currency);
        }
        internal bool Pay(Player p,long quote,int currency)
        {
            if(!owned)return false;
            if(executing || spentFrame==host.Input.Frame || !Valid() || !Same() || Targets.Matches(item) || quote<0 || currency!=-1 || PlayerInput.MouseInfo.LeftButton!=ButtonState.Pressed)return false;
            long before;if(!payment.Balance(p,out before) || before<quote){Stop();return false;}
            if(!payment.Capture(p))return false;
            session=host.Runtime.Generation;token=++nextToken;
            if(!host.Items.Ownership.TryBeginProcessing(session,payment.Slots,token))return false;
            executing=true;paid=false;spentFrame=host.Input.Frame;
            try
            {
                if(!Valid() || !Same() || !payment.Unchanged(p)){Finish(false);return false;}
                bool success=p.BuyItem(quote,currency);long after;
                if(!payment.Balance(p,out after) || success && before-after!=quote || !success && before!=after){Fail(null);return false;}
                if(!success){Stop();Finish(false);return false;}
                paid=true;return true;
            }
            catch(Exception error){Fail(error);return false;}
        }
        internal void Rolled(Exception error)
        {
            if(!executing)return;
            if(error!=null || !paid || !Same()){Fail(error);return;}
            // A same-prefix roll is a real paid result, not a failed attempt.
            if(Targets.Matches(item))Stop();Finish(false);
        }
        internal void DrawEnded(Exception error){if(error!=null)quoteReady=false;}
        private void Stop(){owned=false;tail=true;}
        private void Fail(Exception error){Failure=error;unknown=true;Stop();for(int a=0;a<5;a++)unknownSlots[a]|=payment.Slots[a];host.Report("重铸扣款或结果未确认，已停止自动重铸；不会重复扣款。");Finish(true);}
        private void Finish(bool unknownResult){host.Items.Ownership.EndProcessing(session,payment.Slots,token,unknownResult);executing=false;paid=false;host.Items.World.InvalidateObservation();}
        internal void Reset(bool fresh){if(executing)Fail(null);quoteReady=false;owned=false;tail=PlayerInput.MouseInfo.LeftButton==ButtonState.Pressed;down=tail;pressFrame=-1;if(fresh){unknown=false;Failure=null;Array.Clear(unknownSlots,0,5);}}
    }
}
