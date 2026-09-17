using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeQuickManualInputChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context)
        {
            object input=Get(context,"Input"),shell=Get(context,"Shell"),use=Get(Get(context,"QuickItems"),"Use");Player p=Main.LocalPlayer;
            var isolated=new Harmony("JueMingR.Tests.QuickNativeSelectionSegment");
            var method=typeof(Player).GetMethod("Update",new[]{typeof(int)});
            isolated.Patch(method,transpiler:new HarmonyMethod(typeof(NativeQuickManualInputChecks).GetMethod(nameof(Isolate),Flags)));
            try
            {
                NativeQuickUseMatrix.Reset(p);p.inventory[17].SetDefaults(50);p.inventory[7]=new Item();p.changeItem=-1;
                NativeQuickUseMatrix.Press(input,shell,Keys.J);NativeQuickItemChecks.NativeFrame(p);
                Require(p.itemAnimation>0 && p.selectedItem==17,"manual matrix starts actual mirror animation");
                p.inventory[7].SetDefaults(50);
                NativeQuickItemChecks.Sample(input,new[]{Keys.D7,Keys.W});
                // Use the real profile mapping, not a D7-to-slot copy in tests.
                PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].Processkey(PlayerInput.Triggers.Current,"D7",InputMode.Keyboard);
                Require(PlayerInput.Triggers.Current.Hotbar7,"native default key profile maps D7");
                PlayerInput.ScrollWheelDelta=-120;Frame(p);PlayerInput.ScrollWheelDelta=0;
                Require(p.selectedItem==17 && p.selectedItemState.Hotbar==7,"real number then wheel order is buffered while provider is busy");
                for(int i=0;i<130;i++){NativeQuickItemChecks.Sample(input,new[]{Keys.W,Keys.Space,Keys.LeftShift});Call(shell,"ProcessInput");Frame(p);}
                NativeQuickUseMatrix.Returned(p,use,7,"native numeric/wheel latest choice");
                PlayerInput.Triggers.Current.MouseLeft=true;Frame(p);Require(p.selectedItem==7 && p.itemAnimation>0,"ordinary click after quick return starts actual newly selected item");
                for(int i=0;i<130;i++){NativeQuickItemChecks.Sample(input,new Keys[0]);Frame(p);}
                Console.WriteLine("PASS: retained original Player.Update number/radial/wheel/selection IL, real profile mapping, latest choice and subsequent ordinary use.");
            }
            finally{isolated.Unpatch(method,HarmonyPatchType.All,isolated.Id);PlayerInput.ScrollWheelDelta=0;NativeQuickUseMatrix.Reset(p);}
        }
        private static void Frame(Player p)
        {typeof(Player).GetMethod("ResetControls",Flags).Invoke(p,null);PlayerInput.Triggers.Current.CopyInto(p);p.Update(Main.myPlayer);typeof(Player).GetMethod("TrySyncingInput",Flags).Invoke(p,null);p.ItemCheck();}
        private static IEnumerable<CodeInstruction> Isolate(IEnumerable<CodeInstruction> source,ILGenerator generator)
        {
            var code=source.ToList();
            int end=Unique(code,c=>(c.operand as MethodInfo)?.DeclaringType==typeof(Player.SelectedItemState) && (c.operand as MethodInfo)?.Name=="Update");
            int store=Unique(code,c=>c.opcode==OpCodes.Stfld && Equals(c.operand,typeof(Player).GetField("changeItem")));
            int start=-1;for(int i=Math.Max(0,store-20);i<store;i++)if(code[i].opcode==OpCodes.Ldfld && Equals(code[i].operand,typeof(Player).GetField("changeItem"))){start=i-1;break;}
            Require(start>=0 && code[start].opcode==OpCodes.Ldarg_0 && start<end,"fixed native input expression boundaries");
            for(int i=1;i<=10;i++) {int key=i;int at=Unique(code,c=>(c.operand as MethodInfo)?.DeclaringType==typeof(TriggersSet) && (c.operand as MethodInfo)?.Name=="get_Hotbar"+key);Require(at>start && at<end,"all actual number-key branches retained");}
            int wheel=Unique(code,c=>(c.operand as MethodInfo)?.Name=="HandleHotbarControls");Require(wheel>start && wheel<end,"actual wheel routing retained");
            Require(code[start].blocks.Count==0 && code[end+1].blocks.Count==0,"native extraction has no crossed exception boundary");
            Label target=generator.DefineLabel();code[start].labels.Add(target);
            var stop=new CodeInstruction(OpCodes.Ret);stop.labels.AddRange(code[end+1].labels);code[end+1].labels.Clear();code.Insert(end+1,stop);
            code.Insert(0,new CodeInstruction(OpCodes.Br,target));return code;
        }
        private static int Unique(List<CodeInstruction> code,Func<CodeInstruction,bool> predicate)
        {var found=Enumerable.Range(0,code.Count).Where(i=>predicate(code[i])).ToArray();Require(found.Length==1,"unique native input IL anchor (found "+found.Length+")");return found[0];}
    }
}
