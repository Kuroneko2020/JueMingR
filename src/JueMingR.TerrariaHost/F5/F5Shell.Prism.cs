#if JMR_AIM_LIGHT
using Terraria;
using Terraria.GameInput;
using Trace=JueMingR.TerrariaHost.Combat.Prediction.AimLightTrace;
using Gate=JueMingR.TerrariaHost.Combat.Prediction.AimLightTrace.PrismGate;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class F5Shell
    {
        // Same order and native getters as the composed Combat.CanInterface.
        // These are the consumed values, not a later explanation re-read.
        internal bool PrismCombatInterface()
        {
            if(!Trace.PrismConsuming)return CanTargetInput && !OwnsPointer && CanAutomaticMouseInterface;
            return Trace.Gate(Gate.ShellTarget,PrismTarget()) && Trace.Gate(Gate.ShellPointer,!PrismPointer()) && Trace.Gate(Gate.ShellMouse,PrismMouse());
        }
        private bool PrismTarget()
        {
            return Trace.Gate(Gate.ShellFailed,!failed) && Trace.Gate(Gate.Layers,LayersReady) && Trace.Gate(Gate.ShellSession,biome.SharedRuntime.IsSessionActive) &&
                Trace.Gate(Gate.PresentationRuntime,!biome.SharedRuntime.HasFailed) && Trace.Gate(Gate.Presentation,PrismNativePresentation()) && Trace.Gate(Gate.Journey,!PrismJourney()) &&
                Trace.Gate(Gate.NativeUiFree,Trace.Gate(Gate.NotBlocked,!Main.blockInput) && Trace.Gate(Gate.NotChat,!Main.drawingPlayerChat) && Trace.Gate(Gate.NotSign,!Main.editSign) && Trace.Gate(Gate.NotChest,!Main.editChest) && Trace.Gate(Gate.NoTextTaker,Main.CurrentInputTextTakerOverride==null) && Trace.Gate(Gate.NotWriting,!PlayerInput.WritingText)) &&
                Trace.Gate(Gate.Gesture,!inputState.HotkeyCapture) && Trace.Gate(Gate.NativePlayer,Main.LocalPlayer!=null) &&
                Trace.Gate(Gate.ContainersFree,Trace.Gate(Gate.NoNpcTalk,Main.LocalPlayer.talkNPC<0) && Trace.Gate(Gate.NoSignEdit,Main.LocalPlayer.sign<0) && Trace.Gate(Gate.NoShop,Main.npcShop==0) && Trace.Gate(Gate.NoNpcChat,string.IsNullOrEmpty(Main.npcChatText))) &&
                Trace.Gate(Gate.NativeWindows,Trace.Gate(Gate.NoClothes,!Main.clothesWindow) && Trace.Gate(Gate.NoHair,!Main.hairWindow)) && Trace.Gate(Gate.Adjustment,Trace.Gate(Gate.NoInfoAdjustment,!(information!=null && information.Adjustment.Active)) && Trace.Gate(Gate.NoPendingAdjustment,!adjustmentPending));
        }
        private bool PrismNativePresentation()
        {
            Player player=Main.LocalPlayer;var capture=Terraria.Graphics.Capture.CaptureManager.Instance;
            return Trace.Gate(Gate.NativeWorld,Trace.Gate(Gate.NotMenu,!Main.gameMenu) && Trace.Gate(Gate.Client,!Main.dedServ) && Trace.Gate(Gate.NetworkMode,Main.netMode==0 || Main.netMode==1)) &&
                Trace.Gate(Gate.NativePlayer,Trace.Gate(Gate.Player,player!=null) && Trace.Gate(Gate.PlayerActive,player.active)) && Trace.Gate(Gate.Focus,inputState.IsFocused) &&
                Trace.Gate(Gate.InputMode,Trace.Gate(Gate.NotGamepad,!PlayerInput.UsingGamepad) && Trace.Gate(Gate.NotFastUse,!PlayerInput.ShouldFastUseItem)) && Trace.Gate(Gate.Map,!Main.mapFullscreen) &&
                Trace.Gate(Gate.HideUi,!Main.hideUI) && Trace.Gate(Gate.NotFancyDraw,!Main.onlyDrawFancyUI) && Trace.Gate(Gate.Options,!Main.ingameOptionsWindow) &&
                Trace.Gate(Gate.NotFancyUi,!Main.inFancyUI) && Trace.Gate(Gate.Capture,Trace.Gate(Gate.CaptureExists,capture!=null) && Trace.Gate(Gate.CaptureInactive,!capture.Active)) &&
                Trace.Gate(Gate.TextOwner,PrismText());
        }
        private bool PrismMouse()
        {return Trace.Gate(Gate.Journey,!PrismJourney()) && Trace.Gate(Gate.MouseOwner,!inputState.IsFocused || Main.LocalPlayer!=null && (!Main.LocalPlayer.mouseInterface || ReferenceEquals(leasedPlayer,Main.LocalPlayer) && !priorMouseInterface));}
        private bool PrismJourney()
        {
            var player=Main.LocalPlayer;bool present=journey!=null,menu=present && journey.IsMenuOpen;
            bool busy=menu && (Main.CurrentInputTextTakerOverride!=null || PlayerInput.WritingText || Main.mouseItem!=null && Main.mouseItem.stack>0 || player!=null &&
                (player.mouseInterface && !(ReferenceEquals(leasedPlayer,player) && !priorMouseInterface) || player.lastMouseInterface && !State.Visible));
            // Snapshot cached primitive facts without extra owner evaluation.
            // The Journey gate still means its original short-circuit result.
            Trace.PrismJourneyFacts((present?1:0)|(menu?2:0)|(Main.CurrentInputTextTakerOverride!=null?4:0)|(PlayerInput.WritingText?8:0)|
                (Main.mouseItem!=null && Main.mouseItem.stack>0?16:0)|(player!=null?32:0)|(player?.mouseInterface==true?64:0)|(player?.lastMouseInterface==true?128:0)|
                (ReferenceEquals(leasedPlayer,player)?256:0)|(priorMouseInterface?512:0)|(State.Visible?1024:0)|(busy?2048:0));
            return busy;
        }
        private bool PrismPointer()
        {
            if(failed || !CanPresentNow){Trace.PrismPointerFacts(0);return false;}
            bool state=State.OwnsPointer,other=false,otherEvaluated=false;
            if(!state){other=OtherPointerOwner;otherEvaluated=true;}
            bool owns=state || other;
            Trace.PrismPointerFacts(1|(state?2:0)|(otherEvaluated?4:0)|(other?8:0)|(owns?16:0));return owns;
        }
        private bool PrismText()
        {
            bool other=notes.OtherTextOwner;
            int facts=(other?1:0)|(Main.CurrentInputTextTakerOverride!=null?8:0)|(PlayerInput.WritingText?16:0);
            if(!other){Trace.PrismTextFacts(facts);return true;}
            bool known=BlueprintUi!=null && BlueprintUi.OwnsTextToken || FishingUi!=null && FishingUi.OwnsTextToken || ReforgeUi!=null && ReforgeUi.OwnsTextToken || Browser!=null && Browser.OwnsTextToken || StylePopup!=null && StylePopup.TextInput.OwnsTextToken || MapPopup!=null && MapPopup.TextInput.OwnsTextToken;
            Trace.PrismTextFacts(facts|2|(known?4:0));
            return Trace.Gate(Gate.KnownTextOwner,known) && Trace.Gate(Gate.NotChat,!Main.drawingPlayerChat) && Trace.Gate(Gate.NotSign,!Main.editSign) && Trace.Gate(Gate.NotChest,!Main.editChest);
        }
    }
}
#endif
