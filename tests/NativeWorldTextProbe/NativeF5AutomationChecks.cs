using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeF5AutomationChecks
    {
        internal static void Open(object context)
        {
            object shell=Get(context,"Shell"),state=Get(shell,"State"),renderer=Get(shell,"renderer"),input=Get(context,"Input");
            if(Main.instance==null){Main.instance=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(Main.instance);}
            // Keep the fixture's native camera and original screen dimensions
            // paired. Changing only the UI size would skew later world aims.
            PlayerInput.SetZoom_Unscaled();Main.UIScale=1;FiniteCostChecks.SetCpuFont(10);Call(renderer,"RefreshResources");
            Call(state,"Navigate",10);Call(state,"RestoreVisible");Call(renderer,"Prepare",state,(float)Main.screenWidth,(float)Main.screenHeight,1f);
            for(int i=0;i<3;i++)
            {
                var point=new Vector2((float)Get(state,"X")+100,(float)Get(state,"Y")+160);
                PlayerInput.WritingText=false;Call(input,"BeginUpdate");Call(shell,"BeforeInput");
                PlayerInput.MouseInfo=new MouseState((int)point.X,(int)point.Y,0,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
                PlayerInput.Triggers.Reset();PlayerInput.Triggers.Update();Main.mouseLeft=false;Main.mouseX=(int)point.X;Main.mouseY=(int)point.Y;
                Call(input,"AfterMapping");Main.keyState=new KeyboardState();Call(input,"AfterKeyboardRefresh");Call(shell,"ProcessInput");Call(renderer,"Prepare",state,(float)Main.screenWidth,(float)Main.screenHeight,1f);
            }
            Require((bool)Get(state,"Visible") && (bool)Get(state,"OwnsPointer") && Main.LocalPlayer.mouseInterface && ReferenceEquals(GetOptional(shell,"leasedPlayer"),Main.LocalPlayer),"actual foreground F5 owns hover lease");
            Require((bool)Get(shell,"CanAutomaticTargetInput") && (bool)Get(shell,"CanAutomaticProcessingInput"),"ordinary F5 hover permits automatic work");
            Require(!((Func<bool>)Get(Get(context,"Tools"),"CanFishingInterface"))(),"automatic fishing remains explicitly paused in F5");
        }
    }
}
