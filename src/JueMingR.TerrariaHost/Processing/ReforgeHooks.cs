using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq.Expressions;
using HarmonyLib;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Processing
{
    internal static class ReforgeHooks
    {
        private static HostProcessing host;
        private static Harmony harmony;
        internal static Action Roll;
        internal static Func<int> Cooldown;
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Install(HostProcessing value)
        {
            host=value;harmony=new Harmony("JueMingR.Reforge");
            var roll=typeof(Main).GetMethod("ReforgeItemInReforgeSlot",Flags);
            if(roll==null)throw new MissingMethodException("Main.ReforgeItemInReforgeSlot");
            var cooldown=typeof(Main).GetField("reforgeCooldown",Flags);
            if(cooldown==null || cooldown.FieldType!=typeof(int))throw new MissingFieldException("Main.reforgeCooldown");
            Cooldown=Expression.Lambda<Func<int>>(Expression.Field(null,cooldown)).Compile();
            harmony.Patch(roll,postfix:new HarmonyMethod(typeof(ReforgeHooks),nameof(AfterRoll)),finalizer:new HarmonyMethod(typeof(ReforgeHooks),nameof(FinalRoll)));
            Roll=(Action)Delegate.CreateDelegate(typeof(Action),roll);
            harmony.Patch(typeof(Main).GetMethod("DrawInventory",Flags),transpiler:new HarmonyMethod(typeof(ReforgeHooks),nameof(Button)),finalizer:new HarmonyMethod(typeof(ReforgeHooks),nameof(FinalDraw)));
        }
        internal static void Uninstall(){if(harmony!=null)foreach(var m in harmony.GetPatchedMethods().ToArray())harmony.Unpatch(m,HarmonyPatchType.All,harmony.Id);host=null;Roll=null;}
        private static void AfterRoll(){host?.Reforge.Rolled(null);}
        private static Exception FinalRoll(Exception __exception){if(__exception!=null)host?.Reforge.Rolled(__exception);return __exception;}
        private static Exception FinalDraw(Exception __exception){host?.Reforge.DrawEnded(__exception);return __exception;}
        private static bool Observe(bool hovered,int x,int y,int radius,long cost){return host==null?hovered:host.Reforge.Observe(hovered,x,y,radius,cost);}
        private static bool Payment(Player p,long cost,int currency){return host==null?p.BuyItem(cost,currency):host.Reforge.NativePayment(p,cost,currency);}
        private static IEnumerable<CodeInstruction> Button(IEnumerable<CodeInstruction> instructions)
        {
            var code=instructions.ToList();var buy=typeof(Player).GetMethod("BuyItem",new[]{typeof(long),typeof(int)});var roll=typeof(Main).GetMethod("ReforgeItemInReforgeSlot",Flags);
            int[] rolls=Enumerable.Range(0,code.Count).Where(i=>code[i].Calls(roll)).ToArray();
            if(rolls.Length!=1)throw new InvalidOperationException("reforge-roll-abi");
            int payment=rolls[0]-2;
            if(payment<2 || !code[payment].Calls(buy) || !code[payment-2].IsLdloc() || !code[payment-1].LoadsConstant(-1))throw new InvalidOperationException("reforge-payment-abi");
            var price=new CodeInstruction(code[payment-2].opcode,code[payment-2].operand);
            var texture=typeof(TextureAssets).GetField("Reforge",Flags);
            int[] textures=Enumerable.Range(0,payment).Where(i=>code[i].LoadsField(texture)).ToArray();
            if(textures.Length!=2)throw new InvalidOperationException("reforge-button-abi");
            int at=textures[0];CodeInstruction x=null,y=null;int xCount=0,yCount=0;
            var mx=typeof(Main).GetField("mouseX",Flags);var my=typeof(Main).GetField("mouseY",Flags);
            for(int i=Math.Max(0,at-45);i<at-2;i++)
            {
                if(!code[i+1].IsLdloc() || !code[i+2].LoadsConstant(15))continue;
                if(code[i].LoadsField(mx)){x=new CodeInstruction(code[i+1].opcode,code[i+1].operand);xCount++;}
                if(code[i].LoadsField(my)){y=new CodeInstruction(code[i+1].opcode,code[i+1].operand);yCount++;}
            }
            if(xCount!=2 || yCount!=2 || x==null || y==null)throw new InvalidOperationException("reforge-hit-abi");
            code[payment].opcode=OpCodes.Call;code[payment].operand=typeof(ReforgeHooks).GetMethod(nameof(Payment),Flags);
            // The native hit bool is already on the stack at this merge. Move
            // labels/blocks so both outcomes observe the same native geometry.
            x.labels.AddRange(code[at].labels);code[at].labels.Clear();x.blocks.AddRange(code[at].blocks);code[at].blocks.Clear();
            code.InsertRange(at,new[]{x,y,new CodeInstruction(OpCodes.Ldc_I4,15),price,new CodeInstruction(OpCodes.Call,typeof(ReforgeHooks).GetMethod(nameof(Observe),Flags))});
            return code;
        }
    }
}
