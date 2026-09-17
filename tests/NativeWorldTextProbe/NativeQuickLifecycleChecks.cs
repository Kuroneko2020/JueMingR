using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingR.Features.QuickItems;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Items;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeQuickUseMatrix;

namespace NativeWorldTextProbe
{
    internal static class NativeQuickLifecycleChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static readonly Exception expected=new InvalidOperationException("isolated current ItemCheck failure");
        private static bool throwAfterUse,throwDuringUse;
        private static Action saveBoundary;
        internal static void Run(object context,QuickItemEntry entry)
        {
            object quick=Get(context,"QuickItems"),use=Get(quick,"Use"),input=Get(context,"Input"),shell=Get(context,"Shell");
            var settings=(QuickItemSettings)Get(quick,"Settings");var bindings=(HotkeyBindings)Get(Get(shell,"hotkeys"),"Bindings");
            var ownership=(ItemOperationOwnership)Get(Get(quick,"Items"),"Ownership");Player p=Main.LocalPlayer;
            var audit=new Harmony("JueMingR.Tests.QuickLifecycle");
            MethodInfo check=typeof(Player).GetMethod("ItemCheck",Flags,null,Type.EmptyTypes,null),save=typeof(WorldGen).GetMethod("SaveAndQuit",Flags,null,new[]{typeof(Action)},null);
            audit.Patch(check,postfix:new HarmonyMethod(typeof(NativeQuickLifecycleChecks).GetMethod(nameof(FailAfterUse),Flags)){before=new[]{"JueMingR.QuickItems"}});
            audit.Patch(typeof(Player).GetMethod("ItemCheck_StartActualUse",Flags),postfix:new HarmonyMethod(typeof(NativeQuickLifecycleChecks).GetMethod(nameof(FailDuringUse),Flags)));
            // Keep the real entry and every production prefix; replace only
            // the save body so this fixture cannot create a save or a worker.
            audit.Patch(save,transpiler:new HarmonyMethod(typeof(NativeQuickLifecycleChecks).GetMethod(nameof(IsolateSave),Flags)));
            Action prepare=()=>
            {
                Call(quick,"Poll");bindings.Poll();string reason;
                Require(settings.TryChange(new QuickItemDocument(false,true,new[]{entry}),entry.Id,out reason),"lifecycle preparation");Until(()=>{Call(quick,"Poll");bindings.Poll();return !settings.Busy && !bindings.Busy;});
                if(bindings.Get(entry.ActionId)==null){HotkeyChord key;HotkeyChord.TryParse("J",out key,out reason);long command;Require(bindings.TrySet(entry.ActionId,key,null,out command,out reason),"restore isolated deleted binding");Until(()=>{bindings.Poll();return !bindings.Busy;});}
                Reset(p);p.inventory[17].SetDefaults(50);p.inventory[18]=new Item();p.inventory[2].SetDefaults(ItemID.CopperShortsword);
            };
            Action<string> finish=scenario=>
            {
                Frames(input,shell,p,150);Returned(p,use,2,scenario);Require(!ownership.IsUseSlot(17),scenario+" releases shared source ownership");
                // Ordinary native use after cleanup, without resetting timers.
                NativeQuickItemChecks.Sample(input,new Keys[0]);PlayerInput.Triggers.Current.MouseLeft=true;NativeQuickItemChecks.NativeFrame(p);
                Require(p.selectedItem==2 && p.itemAnimation>0,scenario+" permits subsequent ordinary use");Frames(input,shell,p,80);
            };
            try
            {
                prepare();Press(input,shell,Keys.J);NativeQuickItemChecks.NativeFrame(p);
                int animation=p.itemAnimation;Item provider=p.inventory[17];Main.gamePaused=true;
                for(int i=0;i<20;i++){NativeQuickItemChecks.Sample(input,new[]{Keys.W,Keys.Space});Call(shell,"ProcessInput");Call(quick,"Update",0UL);}
                Require(p.itemAnimation==animation && ReferenceEquals(provider,p.inventory[17]) && provider.stack==1,"pause leaves native timers and physical item unchanged");
                Main.gamePaused=false;finish("pause/resume");
                prepare();Press(input,shell,Keys.J);Main.gamePaused=true;
                NativeQuickItemChecks.Sample(input,new[]{Keys.J});Call(shell,"ProcessInput");Main.gamePaused=false;
                NativeQuickItemChecks.NativeFrame(p);Require(!(bool)Get(use,"Active") && p.itemAnimation==0 && p.selectedItem==2,"pause before native admission cannot replay the old press");
                foreach(string operation in new[]{"disable","master-off","delete"})
                {
                    prepare();Press(input,shell,Keys.J);
                    if(operation=="disable")Call(quick,"Save",entry.With(50,QuickItemMode.Use,true,false));
                    else if(operation=="delete")Call(quick,"Delete",entry.Id);else Call(quick,"ToggleQuick");
                    NativeQuickItemChecks.NativeFrame(p);Require(!(bool)Get(use,"Active") && p.selectedItem==2 && p.itemAnimation==0,"pre-use "+operation+" stops pending action");
                    Until(()=>{Call(quick,"Poll");bindings.Poll();return !settings.Busy && !bindings.Busy;});
                }
                foreach(string operation in new[]{"disable","master-off","delete"})
                {
                    prepare();Press(input,shell,Keys.J);NativeQuickItemChecks.NativeFrame(p);var slots=p.inventory.ToArray();
                    if(operation=="disable")Require((bool)Call(quick,"Save",entry.With(50,QuickItemMode.Use,true,false)),"in-flight disable accepted");
                    else if(operation=="delete")Require((bool)Call(quick,"Delete",entry.Id),"in-flight deletion accepted");else Call(quick,"ToggleQuick");
                    Until(()=>{Call(quick,"Poll");bindings.Poll();return !settings.Busy && !bindings.Busy;});
                    finish(operation);Require(slots.Select((item,i)=>ReferenceEquals(item,p.inventory[i])).All(v=>v) && p.inventory[17].stack==1,"in-flight "+operation+" keeps physical inventory");
                }
                prepare();Press(input,shell,Keys.J);NativeQuickItemChecks.NativeFrame(p);
                NativeQuickItemChecks.Sample(input,new Keys[0]);PlayerInput.Triggers.Current.MouseLeft=true;Main.mouseLeft=true;NativeQuickItemChecks.NativeFrame(p);
                Require(Main.mouseLeft && p.controlUseItem,"physical attack is not cleared by quick cancellation");finish("physical attack takeover");
                prepare();Press(input,shell,Keys.J);NativeQuickItemChecks.NativeFrame(p);
                for(int i=0;i<150;i++){NativeQuickItemChecks.Sample(input,new[]{Keys.J});Call(shell,"ProcessInput");NativeQuickItemChecks.NativeFrame(p);}
                Returned(p,use,2,"held key completes once");
                for(int toggle=0;toggle<2;toggle++){Call(quick,"ToggleQuick");Until(()=>{Call(quick,"Poll");return !settings.Busy;});NativeQuickItemChecks.Sample(input,new[]{Keys.J});Call(shell,"ProcessInput");NativeQuickItemChecks.NativeFrame(p);}
                Require(!(bool)Get(use,"Active") && p.selectedItem==2 && p.itemAnimation==0,"re-enable cannot replay an already held key");
                prepare();Press(input,shell,Keys.J);throwDuringUse=true;int recalls=NativeQuickItemChecks.Recalls;
                try{NativeQuickItemChecks.NativeFrame(p);throw new Exception("mid-use exception not raised");}
                catch(Exception error){while(error is TargetInvocationException && error.InnerException!=null)error=error.InnerException;Require(ReferenceEquals(error,expected),"actual ItemCheck mid-use exception preserved");}
                Require(!Main.mouseLeft && !(bool)Get(use,"InNativeUse") && p.inventory[17].stack==1,"mid-use finalizer releases borrowed input without rollback");finish("mid-use exception");
                Require(NativeQuickItemChecks.Recalls<=recalls+1,"mid-use exception never repeats native effect");
                prepare();Change(quick,entry.With(ItemID.LesserHealingPotion,QuickItemMode.Use,false,true));p.inventory[17].SetDefaults(ItemID.LesserHealingPotion);p.statLife=20;p.statLifeMax2=100;p.potionDelay=0;
                Press(input,shell,Keys.J);throwAfterUse=true;
                try{NativeQuickItemChecks.NativeFrame(p);throw new Exception("actual ItemCheck failure was not raised");}
                catch(Exception error){while(error is TargetInvocationException && error.InnerException!=null)error=error.InnerException;Require(ReferenceEquals(error,expected),"same current native exception preserved");}
                Require(p.inventory[17].stack==0 && p.statLife>20 && !Main.mouseLeft && !(bool)Get(use,"InNativeUse"),"current native finalizer preserves consumed item/effect and returns borrowed mouse");finish("current exception");
                prepare();Press(input,shell,Keys.J);NativeQuickItemChecks.NativeFrame(p);
                // Session loss immediately after a real use frame must release
                // this frame's pulse as well as its lease, before another tick.
                Main.gameMenu=true;Call(context,"UpdateRuntime");
                Require(!(bool)Get(use,"Active") && !ownership.IsUseSlot(17) && !p.controlUseItem && !Main.mouseLeft,"immediate session exit returns current synthetic pulse");Main.gameMenu=false;Call(context,"UpdateRuntime");finish("session exit");
                prepare();Press(input,shell,Keys.J);NativeQuickItemChecks.NativeFrame(p);
                saveBoundary=()=>Require(!(bool)Get(use,"Active") && !ownership.IsUseSlot(17) && !p.controlUseItem && !Main.mouseLeft,"real SaveAndQuit prefix releases owner before isolated save body");
                save.Invoke(null,new object[]{null});Call(context,"UpdateRuntime");finish("save exit");
                prepare();Press(input,shell,Keys.J);NativeQuickItemChecks.NativeFrame(p);
                p.DropItems(false);
                Require(!(bool)Get(use,"Active") && !ownership.IsUseSlot(17) && !p.controlUseItem && p.inventory[17].IsAir,"real death-drop entry retires before inventory is emptied");
                p.dead=true;Call(quick,"Update",0UL);p.dead=false;Frames(input,shell,p,150);
                Require(p.inventory[17].IsAir && Main.mouseItem.IsAir,"post-death frames cannot restore consumed/lost provider");
                Console.WriteLine("PASS: real in-flight disable/delete/master-off, pause/resume, physical attack, current ItemCheck exception, session/save/death boundaries and subsequent ordinary use.");
            }
            finally
            {throwAfterUse=throwDuringUse=false;saveBoundary=null;Main.gamePaused=false;p.dead=false;foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);Call(context,"UpdateRuntime");prepare();}
        }
        private static void FailAfterUse(){if(throwAfterUse){throwAfterUse=false;throw expected;}}
        private static void FailDuringUse(){if(throwDuringUse){throwDuringUse=false;throw expected;}}
        private static IEnumerable<CodeInstruction> IsolateSave(IEnumerable<CodeInstruction> ignored)
        {yield return new CodeInstruction(OpCodes.Call,typeof(NativeQuickLifecycleChecks).GetMethod(nameof(Saved),Flags));yield return new CodeInstruction(OpCodes.Ret);}
        private static void Saved(){saveBoundary?.Invoke();}
    }
}
