using System;
using System.Collections.Generic;
using JueMingR.Features.QuickItems;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Frozen physical samples enter the production shared input/dispatch seams.
    // Native profile mapping, selection, ItemCheck and return remain real.
    internal static class NativeQuickGestureChecks
    {
        internal static void Run(object context, QuickItemEntry entry)
        {
            object quick=Get(context,"QuickItems"), input=Get(context,"Input"), shell=Get(context,"Shell"), use=Get(quick,"Use");
            var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");
            Player p=Main.LocalPlayer;
            try
            {
                NativeQuickUseMatrix.Change(quick,entry.With(ItemID.MagicMirror,QuickItemMode.Use,false,true));
                for(int button=0;button<5;button++)
                {
                    Bind(bindings,entry.ActionId,"Mouse"+(button+1));Prepare(input,shell,p);
                    Mirror(input,shell,p,use,1<<button,new[]{Keys.W},"Mouse"+(button+1));
                    Require(p.controlUp,"gesture preserves independent held movement");
                    Frame(input,shell,p,0);int recalls=NativeQuickItemChecks.Recalls;
                    Mirror(input,shell,p,use,1<<button,new Keys[0],"fresh second Mouse"+(button+1));
                    Require(NativeQuickItemChecks.Recalls==recalls+1,"release allows a fresh edge");
                }
                Bind(bindings,entry.ActionId,"LeftShift+Mouse1");Prepare(input,shell,p);
                Frame(input,shell,p,0,Keys.LeftShift);Frame(input,shell,p,0,Keys.LeftShift);
                Require(p.controlTorch,"real native prior-frame SmartSelect exists");
                // This stale device label must not be treated as a current pad.
                PlayerInput.Triggers.Current.LatestInputMode["SmartSelect"]=InputMode.XBoxGamepad;
                Mirror(input,shell,p,use,1,new[]{Keys.LeftShift,Keys.W},"held Shift plus Mouse1");
                Frame(input,shell,p,0,Keys.LeftShift);Frame(input,shell,p,0,Keys.LeftShift);
                Require(!p.controlTorch,"modifier tail remains owned until its own release");
                Frame(input,shell,p,0);Frame(input,shell,p,0);Frame(input,shell,p,0,Keys.LeftShift);Frame(input,shell,p,0,Keys.LeftShift);
                Require(p.controlTorch,"released modifier resumes native SmartSelect on a new press");
                Frame(input,shell,p,0);Frame(input,shell,p,0);
                Frame(input,shell,p,1);Require(p.inventory[2].stack==1 && p.statLife>20,"plain independent click after chord release uses original potion");
                Aliases(context,entry,bindings);
                Prepare(input,shell,p);object gesture=Get(input,"UseGesture");long checks=(long)Get(gesture,"SourceChecks");
                for(int i=0;i<240;i++)Frame(input,shell,p,0);
                Require((long)Get(gesture,"SourceChecks")==checks,"idle has zero source scans after all physical tails release");
                var settings=(QuickItemSettings)Get(quick,"Settings");string reason;
                Require(settings.TryChange(settings.Current.Toggles(settings.KeepFavorited,false),null,out reason),"disable source observation");
                NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !settings.Busy;});
                MappedSample(input,0,new Keys[0],"RightTrigger");
                Require((long)Get(gesture,"SourceChecks")==checks && PlayerInput.Triggers.Current.MouseLeft,"disabled/no-tail does no source scan and preserves native attack");
                Require(settings.TryChange(settings.Current.Toggles(settings.KeepFavorited,true),null,out reason),"restore isolated preference");
                NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !settings.Busy;});
                Console.WriteLine("PASS: five public mouse bindings, held modifier, native UseItem aliases, independent attack/gamepad/focus protection, hold/release and actual item/selection return; idle source scans=0.");
            }
            finally {Frame(input,shell,p,0);Bind(bindings,entry.ActionId,"J");NativeQuickUseMatrix.Change(quick,entry);NativeQuickUseMatrix.Reset(p);}
        }
        private static void Prepare(object input,object shell,Player p)
        {
            Frame(input,shell,p,0);Frame(input,shell,p,0);NativeQuickUseMatrix.Reset(p);
            p.inventory[2].SetDefaults(ItemID.LesserHealingPotion);p.inventory[2].stack=2;
            p.statLifeMax2=100;p.statLife=20;p.potionDelay=0;p.inventory[17].SetDefaults(ItemID.MagicMirror);
        }
        private static void Mirror(object input,object shell,Player p,object use,int mouse,Keys[] keys,string scenario)
        {
            int recalls=NativeQuickItemChecks.Recalls;
            for(int i=0;i<160;i++)Frame(input,shell,p,mouse,keys);
            Require(NativeQuickItemChecks.Recalls==recalls+1 && p.inventory[2].stack==2 && p.statLife==20,
                scenario+" uses mirror exactly once without original potion; recalls="+(NativeQuickItemChecks.Recalls-recalls)+" original="+p.inventory[2].stack);
            Require(p.itemAnimation==0 && p.itemTime==0 && p.reuseDelay==0,"native use fully completes: "+scenario);
            NativeQuickUseMatrix.Returned(p,use,2,scenario);
        }
        private static void Aliases(object context,QuickItemEntry entry,HotkeyBindings bindings)
        {
            object input=Get(context,"Input"),shell=Get(context,"Shell"),use=Get(Get(context,"QuickItems"),"Use");Player p=Main.LocalPlayer;
            var aliases=PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].KeyStatus["MouseLeft"];
            var original=aliases.ToArray();
            try
            {
                foreach(string key in new[]{"J","Mouse3","Mouse4","Mouse5"})
                {
                    aliases.Clear();aliases.Add(key);Bind(bindings,entry.ActionId,key);Prepare(input,shell,p);
                    Mirror(input,shell,p,use,key=="J"?0:1<<(key[5]-'1'),key=="J"?new[]{Keys.J}:new Keys[0],"native UseItem="+key);
                }
                aliases.Clear();aliases.Add("Mouse1");aliases.Add("J");Bind(bindings,entry.ActionId,"Mouse1");Prepare(input,shell,p);
                p.inventory[2].TurnToAir();int recalls=NativeQuickItemChecks.Recalls;
                Frame(input,shell,p,0,Keys.J);Frame(input,shell,p,0,Keys.J);
                for(int i=0;i<4;i++)
                {Frame(input,shell,p,1,Keys.J);Require(PlayerInput.Triggers.Current.MouseLeft && p.selectedItem==2,"prior independent held alias cannot be swallowed");}
                Require(NativeQuickItemChecks.Recalls==recalls && !(bool)Get(use,"Active"),"existing attack refuses shortcut without retry");
                Prepare(input,shell,p);Frame(input,shell,p,1);Frame(input,shell,p,1,Keys.J);Frame(input,shell,p,1,Keys.J);
                Require(PlayerInput.Triggers.Current.MouseLeft && p.selectedItemState.HasBufferedChange,"later independent alias takes over through native buffer");
                for(int i=0;i<160;i++)Frame(input,shell,p,1);
                NativeQuickUseMatrix.Returned(p,use,2,"later independent attack with original shortcut still held");
                Require(p.inventory[2].stack==2,"owned hold cannot attack returned original item");
                Prepare(input,shell,p);p.inventory[2].TurnToAir();recalls=NativeQuickItemChecks.Recalls;
                MappedSample(input,1,new Keys[0],"RightTrigger");Call(shell,"ProcessInput");NativeQuickItemChecks.NativeFrame(p);
                Require(PlayerInput.Triggers.Current.MouseLeft && p.selectedItem==2 && NativeQuickItemChecks.Recalls==recalls,"actual native gamepad mapping remains foreign on first claim");
                Prepare(input,shell,p);Frame(input,shell,p,1);
                Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(2)));
                try
                {Frame(input,shell,p,1,Keys.J);Require(!PlayerInput.Triggers.Current.MouseLeft && !Main.mouseLeft,"tail cannot revive quarantined alias input");}
                finally {Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(1)));}
                Frame(input,shell,p,1,Keys.J);Require(!PlayerInput.Triggers.Current.MouseLeft,"held reactivation remains quarantined");
                Frame(input,shell,p,0);Frame(input,shell,p,0);
                for(int i=0;i<160;i++)Frame(input,shell,p,0);
                NativeQuickUseMatrix.Returned(p,use,2,"focus loss/rearm tail completion");
                Prepare(input,shell,p);Frame(input,shell,p,1);recalls=NativeQuickItemChecks.Recalls;
                object state=Get(shell,"State");Call(state,"RestoreVisible");
                try {for(int i=0;i<10;i++)Frame(input,shell,p,1);}
                finally {Call(state,"Close");}
                for(int i=0;i<160;i++)Frame(input,shell,p,1);
                Require(p.inventory[2].stack==2 && NativeQuickItemChecks.Recalls<=recalls+1,"F5 open/close cannot replay owned mouse hold into original item");
                NativeQuickUseMatrix.Returned(p,use,2,"F5 modal held tail");
            }
            finally {aliases.Clear();aliases.AddRange(original);Frame(input,shell,p,0);Frame(input,shell,p,0);}
        }
        internal static void Bind(HotkeyBindings bindings,string action,string text)
        {
            HotkeyChord chord;string reason;long command;
            Require(HotkeyChord.TryParse(text,out chord,out reason),"gesture chord: "+text);
            Require(bindings.TrySet(action,chord,null,out command,out reason),"public gesture binding: "+reason);
            NativeQuickUseMatrix.Until(()=>{bindings.Poll();return !bindings.Busy;});
            Require(bindings.CompletionSucceeded && bindings.Get(action).Text==text,"gesture binding persisted");
        }
        internal static void Frame(object input,object shell,Player player,int mouse,params Keys[] keys)
        {Sample(input,mouse,keys);Call(shell,"ProcessInput");NativeQuickItemChecks.NativeFrame(player);}
        internal static void Sample(object input,int mouse,params Keys[] keys)
        {MappedSample(input,mouse,keys,null);}
        private static void MappedSample(object input,int mouse,Keys[] keys,string gamepad)
        {
            Call(input,"BeginUpdate");
            PlayerInput.MouseInfo=new MouseState(200,200,0,Button(mouse,1),Button(mouse,4),Button(mouse,2),Button(mouse,8),Button(mouse,16));
            var tokens=new List<string>();for(int i=0;i<5;i++)if((mouse&(1<<i))!=0)tokens.Add("Mouse"+(i+1));
            PlayerInput.Triggers.Reset();
            Call(input,"AfterNativeMouse",tokens);
            var profile=PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard];
            foreach(Keys key in Main.keyState.GetPressedKeys())
                if(Main.oldKeyState.IsKeyDown(key))profile.CopyKeyState(PlayerInput.Triggers.Old,PlayerInput.Triggers.Current,key.ToString());
                else profile.Processkey(PlayerInput.Triggers.Current,key.ToString(),InputMode.Keyboard);
            foreach(string token in tokens)profile.Processkey(PlayerInput.Triggers.Current,token,InputMode.Keyboard);
            if(gamepad!=null)PlayerInput.CurrentProfile.InputModes[InputMode.XBoxGamepad].Processkey(PlayerInput.Triggers.Current,gamepad,InputMode.XBoxGamepad);
            PlayerInput.Triggers.Update();
            Main.mouseLeft=PlayerInput.Triggers.Current.MouseLeft;Main.mouseRight=PlayerInput.Triggers.Current.MouseRight;
            Call(input,"AfterMapping");Main.oldKeyState=Main.keyState;Main.keyState=new KeyboardState(keys);Call(input,"AfterKeyboardRefresh");
        }
        private static ButtonState Button(int mask,int button) {return (mask&button)!=0?ButtonState.Pressed:ButtonState.Released;}
    }
}
