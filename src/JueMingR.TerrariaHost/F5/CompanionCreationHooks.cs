using System;
using HarmonyLib;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.GameContent.UI.States;
using Terraria.Initializers;
using Terraria.UI;

namespace JueMingR.TerrariaHost.F5
{
    internal static class CompanionCreationHooks
    {
        internal static void Install(Harmony harmony)
        {
            Patch(harmony,typeof(UserInterface),"Update",nameof(Update),nameof(DispatchFinally));
            Patch(harmony,typeof(UserInterface),"Draw",nameof(Draw),nameof(DispatchFinally));
            Patch(harmony,typeof(HairstyleUnlocksHelper),"UpdateUnlocks",nameof(Hair));
            Patch(harmony,typeof(UICharacterCreation),"Click_Naming",nameof(Name));
            Patch(harmony,typeof(UICharacterCreation),"Click_NamingAndCreating",nameof(NameAndCreate));
            Patch(harmony,typeof(UICharacterCreation),"FinishCreatingCharacter",nameof(Submit));
            Patch(harmony,typeof(UICharacterCreation),"Click_GoBack",nameof(Cancel));
            Patch(harmony,typeof(UICharacterCreation),"HandleBackButtonUsage",nameof(InstanceBack));
            foreach(string callback in new[]{"OnFinishedNaming","OnFinishedNamingAndCreating","OnCanceledNaming"})Patch(harmony,typeof(UICharacterCreation),callback,nameof(MenuNameCallback));
            Patch(harmony,typeof(UIVirtualKeyboard),"InternalSubmit",nameof(KeyboardSubmit));
            Patch(harmony,typeof(UIVirtualKeyboard),"Cancel",nameof(KeyboardCancel));
            Patch(harmony,typeof(UICharacterCreation),"GoBack",nameof(GoBack));
            Patch(harmony,typeof(IngameFancyUI),"Close",nameof(Close));
            Patch(harmony,typeof(UILinksInitializer),"FancyExit",nameof(Back));
            Patch(harmony,typeof(UILinksInitializer),"OnFancyUIPageMoveAttempt",nameof(Category));
            Patch(harmony,typeof(Player),"PlayerFrame",nameof(Frame),nameof(FrameFinally));
            Patch(harmony,typeof(GameTipsDisplay),"Draw",nameof(Tips),nameof(TipsFinally));
        }
        private static void Patch(Harmony h,Type type,string method,string prefix,string finalizer=null)
        {h.Patch(AccessTools.Method(type,method),new HarmonyMethod(typeof(CompanionCreationHooks),prefix),finalizer:finalizer==null?null:new HarmonyMethod(typeof(CompanionCreationHooks),finalizer));}
        private static CompanionCreationSession Current {get {var s=CompanionCreationSession.Active;return s!=null&&s.Valid&&s.Owns(Main.InGameUI.CurrentState)?s:null;}}
        private static CompanionCreationSession OwnedActive {get {var s=CompanionCreationSession.Active;return s!=null&&s.Owns(Main.InGameUI.CurrentState)?s:null;}}
        private static bool Update(UserInterface __instance,Microsoft.Xna.Framework.GameTime __0,out IDisposable __state)
        {
            __state=null;var s=OwnedActive;
            if(ReferenceEquals(__instance,Main.InGameUI)&&s!=null&&!s.Dispatching){s.QueueNativeUpdate(__0);return false;}
            return Begin(__instance,false,out __state);
        }
        private static bool Draw(UserInterface __instance,out IDisposable __state) {return Begin(__instance,true,out __state);}
        private static bool Begin(UserInterface ui,bool draw,out IDisposable state)
        {
            state=null;if(!ReferenceEquals(ui,Main.InGameUI))return true;
            var s=OwnedActive;if(s==null)return true;
            if(!s.Valid){CompanionCreationSession.DisposeFor(s.Host);return false;}
            if(!draw&&!s.InputAllowed) {
                // A focus-loss neutral sample is not a physical MouseUp. Drop
                // native LastDown/LastClicked before it can submit a keyboard;
                // keep the native page and its draft for trusted reactivation.
                ui.ResetLasts();return false;
            }
            state=s.Enter(draw);return true;
        }
        private static Exception DispatchFinally(Exception __exception,IDisposable __state) {__state?.Dispose();return __exception;}
        private static bool Hair(HairstyleUnlocksHelper __instance) {return !ReferenceEquals(__instance,CompanionCreationSession.Active?.Hairstyles);}
        // Weak exact-instance ownership outlives the active session. A delayed
        // callback from our retired creator must never become vanilla file I/O.
        // Ordinary native creators were never marked and retain their own exits.
        private static bool IsCreator(UICharacterCreation c) {return Current?.InputAllowed==true&&ReferenceEquals(c,Current.Creator);}
        private static bool Name(UICharacterCreation __instance) {if(!CompanionCreationSession.WasManaged(__instance))return true;if(IsCreator(__instance))Current.Name(false);return false;}
        private static bool NameAndCreate(UICharacterCreation __instance) {if(!CompanionCreationSession.WasManaged(__instance))return true;if(IsCreator(__instance))Current.Name(true);return false;}
        private static bool Submit(UICharacterCreation __instance) {if(!CompanionCreationSession.WasManaged(__instance))return true;if(IsCreator(__instance))Current.Submit();return false;}
        private static bool Cancel(UICharacterCreation __instance) {if(!CompanionCreationSession.WasManaged(__instance))return true;if(IsCreator(__instance))Current.Cancel();return false;}
        private static bool InstanceBack(UICharacterCreation __instance) {if(!CompanionCreationSession.WasManaged(__instance))return true;if(IsCreator(__instance))Current.Back();return false;}
        private static bool MenuNameCallback(UICharacterCreation __instance) {return !CompanionCreationSession.WasManaged(__instance);}
        private static bool KeyboardSubmit(UIVirtualKeyboard __instance) {if(!CompanionCreationSession.WasManaged(__instance))return true;var s=Current;if(s==null||!ReferenceEquals(__instance,s.Keyboard))return false;if(s.InputAllowed)return true;s.RequestKeyboardSubmit();return false;}
        private static bool KeyboardCancel() {var s=Current;if(s==null||!ReferenceEquals(Main.InGameUI.CurrentState,s.Keyboard)||s.InputAllowed)return true;s.RequestBack();return false;}
        private static bool GoBack() {var s=Current;if(s==null)return true;s.RequestBack();return false;}
        private static bool Close() {var s=OwnedActive;if(s==null)return true;if(!s.Valid)s.Back();else s.RequestBack();return false;}
        private static bool Back() {var s=OwnedActive;if(s==null)return true;if(!s.Valid)s.Back();else s.RequestBack();return false;}
        private static bool Category(int direction) {var s=Current;if(s==null)return true;s.RequestCategory(direction);return false;}
        private struct TipsState {internal GameTipsDisplay Display;internal int Width,Height;internal float Offset;}
        private static bool Tips(GameTipsDisplay __instance,out TipsState __state)
        {
            __state=default(TipsState);var s=Current;if(s==null||s.DrawDepth==0||!ReferenceEquals(s.Tips,__instance))return true;
            if(!s.TipsFit)return false;
            __state=new TipsState{Display=__instance,Width=Main.screenWidth,Height=Main.screenHeight,Offset=__instance.TipOffsetY};
            // This original menu-only text consumer uses raw screen dimensions.
            // Give only its synchronous draw the logical UI viewport; unlike the
            // whole creation page it has no child hit targets or world updates.
            var dimensions=Main.InGameUI.GetDimensions();Main.screenWidth=(int)dimensions.Width;Main.screenHeight=(int)dimensions.Height;__instance.TipOffsetY=-60;
            return true;
        }
        private static Exception TipsFinally(Exception __exception,TipsState __state)
        {if(__state.Display!=null){Main.screenWidth=__state.Width;Main.screenHeight=__state.Height;__state.Display.TipOffsetY=__state.Offset;}return __exception;}
        private struct FrameState
        {
            internal Player Player;internal bool Frozen,Inanimate;
        }
        private static void Frame(Player __instance,out FrameState __state)
        {
            var s=Current;__state=default(FrameState);if(s==null||s.DrawDepth==0||!ReferenceEquals(__instance,s.Draft))return;
            __state=new FrameState{Player=__instance,Frozen=__instance.frozen,Inanimate=__instance.isDisplayDollOrInanimate};
            __instance.frozen=true;__instance.isDisplayDollOrInanimate=true;
        }
        private static Exception FrameFinally(Exception __exception,FrameState __state)
        {
            if(__state.Player!=null){var p=__state.Player;p.frozen=__state.Frozen;p.isDisplayDollOrInanimate=__state.Inanimate;p.socialIgnoreLight=true;p.leinforsHair=false;}
            return __exception;
        }
    }
}
