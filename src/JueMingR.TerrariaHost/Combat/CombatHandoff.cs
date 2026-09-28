using System;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Combat
{
    // A combat hold may lend one legal use opportunity to an existing owner.
    // The shared use token still arbitrates acquisition. This object only
    // remembers which residual buttons belong to that hold and the real weapon
    // to await on return. Temporary residual input is scoped; selection and
    // restoration remain the actual borrower's responsibility.
    internal sealed class CombatHandoff
    {
        private enum Borrower { None, Tools, Extraction, QuickItem }
        private readonly HostCombat host;
        private Processing.HostProcessing processing;
        private QuickItems.HostQuickItems quick;
        private Borrower offered,last;
        private Player player;
        private Item weapon;
        private int slot,type;
        private long session,selection,borrowToken;
        private bool left,right,probing,borrowed,revoked,releasing;
        private CombatInputScope tileScope;
        internal bool Waiting {get{return offered!=Borrower.None;}}
        internal bool RightSync {get{return Waiting && right && Valid();}}
        internal bool ResumeWanted {get{return Waiting && borrowed && Valid() && (offered!=Borrower.Tools || host.Tools.Use.CompletedAnimation);}}
        internal CombatHandoff(HostCombat host){this.host=host;}
        internal void Attach(Processing.HostProcessing processing,QuickItems.HostQuickItems quick)
        {
            this.processing=processing;this.quick=quick;
            var prior=host.Tools.Items.World.AdditionalProtection;
            host.Tools.Items.World.AdditionalProtection=item=>(prior?.Invoke(item)??false) || Protect(item);
            if(processing!=null)
            {
                processing.HeldUsePermit=()=>Allows(Borrower.Extraction);
                processing.YieldTools=()=>ResumeWanted || host.Tools.Ready();
                processing.ToolsYieldProtection=item=>host.Tools.ProtectedAfterYield(item) || Protect(item);
            }
            host.Tools.OtherUseReady=()=>ResumeWanted || (processing?.Extraction.Ready()??false);
            if(quick!=null)
            {
                quick.HeldUsePermit=()=>Allows(Borrower.QuickItem);
                quick.YieldTools=()=>{QuickRequest();host.Tools.Yield();};
            }
        }
        private bool Identity()
        {
            return player!=null && ReferenceEquals(player,host.Player) && session==host.Runtime.Generation && selection==host.Tools.SelectionIntent &&
                ReferenceEquals(player.inventory[slot],weapon) && weapon.type==type && weapon.stack>0;
        }
        private bool Valid()
        {
            return !revoked && Identity() && host.Tools.Input.CanRetainIntent && host.Settings.CanRun && (host.Settings.Value.EnabledMask & 31)!=0 &&
                !PlayerInput.Triggers.Current.SmartSelect &&
                (!PlayerInput.Triggers.Current.MouseLeft || left) && (!PlayerInput.Triggers.Current.MouseRight || right) &&
                (PlayerInput.Triggers.Current.MouseLeft && left || PlayerInput.Triggers.Current.MouseRight && right) &&
                (!right || WeaponCatalog.RightAvailable(player,weapon));
        }
        private bool Allows(Borrower who)
        {
            if(offered!=who || releasing && !probing || !Valid())return false;
            if(probing || !borrowed)return true;
            return who==Borrower.Tools?host.Tools.Use.Active && host.Tools.Use.Operation==borrowToken:
                who==Borrower.Extraction?processing.Extraction.Active && processing.Extraction.Operation==borrowToken:
                quick.Use.Active && quick.Use.Operation==borrowToken;
        }
        internal bool AllowsTools {get{return Allows(Borrower.Tools);}}
        internal bool Protect(Item item){return Waiting && Identity() && ReferenceEquals(item,weapon);}
        private void Capture(Borrower who)
        {
            player=host.Player;slot=player.selectedItem;weapon=player.inventory[slot];type=weapon.type;session=host.Runtime.Generation;selection=host.Tools.SelectionIntent;
            left=host.Left;right=host.Right;offered=who;borrowed=revoked=releasing=false;borrowToken=0;
        }
        internal void BeforeSelection(Player p)
        {
            if(!ReferenceEquals(p,host.Player))return;
            Observe(false);
            if(Waiting)
            {
                if(releasing && host.Use.CanYield){host.Use.Stop();releasing=false;}
                return;
            }
            if(!host.Use.CanOffer || !host.FreshGesture)return;
            Borrower first=last==Borrower.Tools?Borrower.Extraction:Borrower.Tools;
            if(TryOffer(first) || TryOffer(first==Borrower.Tools?Borrower.Extraction:Borrower.Tools))
            {
                if(host.Use.CanYield)host.Use.Stop();
                else{releasing=true;host.Use.RequestYield();}
            }
        }
        private bool TryOffer(Borrower who)
        {
            if(who==Borrower.Extraction && (processing==null || !processing.Value(1)) || who==Borrower.Tools && !host.Tools.Enabled)return false;
            Capture(who);bool ready=false;
            try{probing=true;ready=Valid() && (who==Borrower.Tools?host.Tools.Ready():processing.Extraction.Ready());}
            finally{probing=false;if(!ready)Reset();}
            return ready;
        }
        private void QuickRequest()
        {
            // A shortcut is a fresh manual request, not an automatic queue.
            // Busy requests keep the existing QuickItems non-queue outcome.
            if(!host.Use.CanYield)return;
            Capture(Borrower.QuickItem);host.Use.Stop();
        }
        internal void AfterSelection(Player p){if(ReferenceEquals(p,host.Player))Observe(true);}
        internal void BeforeSync(Player p)
        {
            if(!ReferenceEquals(p,host.Player))return;FinishFrame();Observe(true);
            // This seam runs before every borrower's Sync prefix. Remove only
            // our old hold; the actual borrower then supplies its own pulse.
            if(Waiting && Valid() && (AllowsTools || Allows(Borrower.Extraction) || Allows(Borrower.QuickItem)))
            {if(left)p.controlUseItem=false;}
        }
        internal void AfterInteractions(Player p)
        {
            if(!Waiting || !ReferenceEquals(p,player))return;
            Observe(false);
            // Native tile/NPC interaction wins even over a pulse a borrower
            // submitted at TrySyncingInput. Cancel only our exact borrowed
            // token; QuickItems must retire its not-yet-consumed pulse too.
            if(right && (p.tileInteractionHappened || p.chest>=0 || p.talkNPC>=0 || p.sign>=0))
            {Interrupted();return;}
            if(right && Valid() && !releasing && (AllowsTools || Allows(Borrower.Extraction) || Allows(Borrower.QuickItem)))
            {FinishFrame();tileScope=new CombatInputScope(p,p.controlUseItem,true,false);tileScope.Apply();}
        }
        internal void Interrupted()
        {
            FinishFrame();revoked=true;
            if(!borrowed)return;
            if(offered==Borrower.Tools && host.Tools.Use.Operation==borrowToken)host.Tools.Yield();
            else if(offered==Borrower.Extraction && processing.Extraction.Operation==borrowToken)processing.Extraction.Cancel();
            else if(offered==Borrower.QuickItem && quick.Use.Operation==borrowToken)quick.Use.Retire();
        }
        internal void FinishFrame(){tileScope?.End();tileScope=null;}
        private void Observe(bool afterSelection)
        {
            if(!Waiting)return;
            if(!Identity()){Reset();return;}
            if(!Valid())revoked=true;
            if(releasing)
            {if(revoked){host.Use.CancelYield();Reset();}return;}
            long current=offered==Borrower.Tools?host.Tools.Use.Operation:offered==Borrower.Extraction?processing.Extraction.Operation:quick.Use.Operation;
            bool active=offered==Borrower.Tools?host.Tools.Use.Active:offered==Borrower.Extraction?processing.Extraction.Active:quick.Use.Active;
            if(!borrowed && active){borrowed=true;borrowToken=current;last=offered;}
            if(borrowed && active && current!=borrowToken){Reset();return;}
            if(afterSelection && !active && !PickingReturn())Reset();
        }
        private bool PickingReturn(){return player.selectedItem!=slot || player.selectedItemState.HasBufferedChange || player.selectedItemState.HasActiveOverride;}
        internal bool Releasing {get{return releasing;}}
        internal void Sample()
        {
            if(!Waiting)return;
            if(host.Left!=left || host.Right!=right || !host.Tools.Input.CanRetainIntent)Revoke();
        }
        internal void Revoke(){revoked=true;}
        internal void Reset(){FinishFrame();offered=Borrower.None;player=null;weapon=null;borrowed=probing=revoked=releasing=false;borrowToken=0;}
    }
}
