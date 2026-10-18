using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Light;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeViewFailureChecks
    {
        private const BindingFlags F=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static LegacyLighting current;
        private static bool armed;
        private static int attempts;
        private static readonly FieldInfo states=typeof(LegacyLighting).GetField("_states",F);
        private static IEnumerable<CodeInstruction> FailAfterPublication(IEnumerable<CodeInstruction> source)
        {
            int count=0;foreach(var instruction in source)
            {yield return instruction;if(instruction.opcode==OpCodes.Stfld && Equals(instruction.operand,states)){count++;yield return new CodeInstruction(OpCodes.Ldarg_0);yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(NativeViewFailureChecks),nameof(AfterPublication)));}}
            Require(count==1,"controlled allocation failure hooks exact original partial-array publication");
        }
        private static void AfterPublication(LegacyLighting value)
        {
            if(!armed || ReferenceEquals(value,current))return;
            attempts++;Console.WriteLine("VIEW FAILURE actual candidate Rebuild partially published states; attempt="+attempts);
            throw new OutOfMemoryException("Controlled VIEW extra-capacity allocation failure after native states publication");
        }
        internal static void Run(object view,Action draw)
        {
            var legacy=typeof(Lighting).GetField("LegacyEngine",F);var oldEngine=legacy.GetValue(null);var oldMode=Lighting.Mode;
            var audit=new Harmony("JueMingR.Tests.View.AllocationFailure");
            try
            {
                Main.GameZoomTarget=1;draw();
                current=new LegacyLighting(Main.Camera);current.Rebuild();legacy.SetValue(null,current);Lighting.Mode=LightMode.Retro;
                var oldStates=states.GetValue(current);var oldMap=Get(current,"_lightMap");var oldAxes=Get(current,"_axisFlipStates");
                audit.Patch(typeof(LegacyLighting).GetMethod("Rebuild"),transpiler:new HarmonyMethod(typeof(NativeViewFailureChecks),nameof(FailAfterPublication)));
                Require((bool)Get(view,"ControlsEnabled"),"owned failure test starts with available view");Call(view,"SetUnlockEnabled",true);
                Main.GameZoomTarget=.5f;attempts=0;armed=true;draw();
                Require(attempts==1 && ReferenceEquals(states.GetValue(current),oldStates) && ReferenceEquals(Get(current,"_lightMap"),oldMap) && ReferenceEquals(Get(current,"_axisFlipStates"),oldAxes),"actual failed growth never replaces live arrays or LightMap");
                var messages=new List<string>();Call(view,"TakeFeedback",(Action<string>)messages.Add);Call(view,"TakeFeedback",(Action<string>)messages.Add);
                Require(messages.Count==1 && !(bool)Get(view,"ControlsEnabled") && Main.GameZoomTarget>=1,"owned failure disables further expansion, restores safe target and reports once");
                for(int i=0;i<20;i++)draw();Call(view,"TakeFeedback",(Action<string>)messages.Add);
                Require(attempts==1 && messages.Count==1 && Main.GameViewMatrix.RenderZoom.X>=1 && ReferenceEquals(states.GetValue(current),oldStates),"subsequent native lighting/Draw uses intact normal capacity without retry or feedback loop");
                var color=Lighting.GetColor((int)(Main.LocalPlayer.Center.X/16),(int)(Main.LocalPlayer.Center.Y/16));
                Console.WriteLine("PASS VIEW owned allocation failure: candidate native partial publication isolated, live arrays/map retained, next20 native Draw+GetColor="+color+", attempts=1, feedback=1, safe target="+Main.GameZoomTarget+" render="+Main.GameViewMatrix.RenderZoom);
            }
            finally{armed=false;audit.UnpatchAll(audit.Id);legacy.SetValue(null,oldEngine);Lighting.Mode=oldMode;current=null;}
        }
    }
}
