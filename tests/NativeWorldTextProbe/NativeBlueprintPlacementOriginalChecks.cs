using System;
using System.Collections;
using System.Linq;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintPlacementOriginalChecks
    {
        // Controlled input in the initialized original specialist. The real
        // DrawInterface certificate, toolbar and world action consumers run;
        // physical OS input and ordinary complete startup remain unclaimed.
        internal static void Run(object context, Action<int, int, bool> sample, Action draw)
        {
            var host = Get(context, "Blueprint"); var library = (LibraryState)Get(host, "Library"); var state = (BlueprintCaptureState)Get(host, "State");
            Call(Get(context, "Shell"), "CloseAndSubmitPosition"); state.Exit(); Main.UIScale = 1;
            sample(1050, 550, false); sample(1050, 550, false);
            var entry = NativeBlueprintPlacementChecks.Seed(library, 4, 2, "原版指针投影");
            sample(1050, 550, true); Require((bool)Call(host, "Place", entry), "initialized native Host accepts library projection");
            NativeQuickItemChecks.Until(() => { sample(1050, 550, true); return GetOptional(host, "CurrentProjection") != null; });
            sample(1066, 566, true); sample(1066, 566, true);
            Require(Get(host, "ProjectionMode").ToString() == "Following", "startup held click cannot certify placement even after native UI draw");
            sample(1050, 550, false); sample(1050, 550, false); sample(1050, 550, true); sample(1050, 550, true);
            NativeQuickItemChecks.Until(() => { Call(host,"Poll"); return Get(host,"ProjectionMode").ToString()=="Fixed"; });
            Require(Get(host, "ProjectionMode").ToString() == "Fixed", "new certified native world press fixes projection");
            Func<int> x = () => (int)Get(Get(host, "CurrentProjection"), "OriginX"), y = () => (int)Get(Get(host, "CurrentProjection"), "OriginY");
            int oldX = x(), oldY = y();
            sample(1050, 550, false); sample(1050, 550, false);
            var toolbar = Get(host, "Toolbar"); var layout = Get(toolbar, "Layout"); draw();
            Require(((IEnumerable)Get(layout, "Buttons")).Cast<object>().Count() == 4, "native gel toolbar adds move/remove for the fixed projection");
            Action<bool> move = down =>
            {
                var panel = Get(layout, "Panel"); var rect = Get(((IEnumerable)Get(layout, "Buttons")).Cast<object>().ElementAt(2), "Rect");
                sample((int)((float)Get(panel, "X") + (float)Get(rect, "X") + 10), (int)((float)Get(panel, "Y") + (float)Get(rect, "Y") + 10), down);
            };
            move(false); move(true); move(false);
            Require(Get(host, "ProjectionMode").ToString() == "SelectingMove" && x() == oldX && y() == oldY, "actual native toolbar Move starts target selection without a UI-coordinate jump");
            var target=Microsoft.Xna.Framework.Vector2.Transform(Main.ReverseGravitySupport(new Microsoft.Xna.Framework.Vector2(oldX*16+8,oldY*16+8)-Main.screenPosition),Main.GameViewMatrix.ZoomMatrix);
            int tx=(int)target.X,ty=(int)target.Y;
            NativeQuickItemChecks.Until(()=>{sample(tx,ty,false);return GetOptional(Get(host,"CurrentProjection"),"Window")!=null;});
            sample(tx,ty,false);sample(tx,ty,true);sample(tx,ty,true);
            Require(Get(host,"ProjectionMode").ToString()=="Moving" && x()==oldX && y()==oldY,"new certified target press selects visible tile and retains grab offset");
            sample(tx,ty,false); sample(tx,ty,false);
            Require(x() == oldX && y() == oldY, "native target release does not confirm or jump");
            sample(tx+16,ty+16,false); sample(tx+16,ty+16,false);
            Require(x() == oldX + 1 && y() == oldY + 1, "certified native mouse tile delta moves projection one world tile");
            PlayerInput.Triggers.Current.MouseLeft = true; PlayerInput.Triggers.Current.CopyInto(Main.LocalPlayer);
            Require(!Main.LocalPlayer.controlUseItem, "Moving blocks the actual original world-use consumer");
            Call(host, "CancelProjection"); Require(x() == oldX && y() == oldY, "cancel through shared Host restores original fixed coordinates");
            sample(1050, 550, false); sample(1050, 550, false);
            PlayerInput.Triggers.Current.MouseLeft = true; PlayerInput.Triggers.Current.CopyInto(Main.LocalPlayer);
            Require(Main.LocalPlayer.controlUseItem, "Fixed restores native use after trusted release");
            Call(host, "RemoveProjection");sample(tx,ty,false);sample(tx,ty,false);sample(tx,ty,true);sample(tx,ty,true);
            Require(GetOptional(host,"CurrentProjection")==null,"new certified selection removes only the chosen world instance");
            Console.WriteLine("PASS Blueprint initialized-native placement: real pointer certificate/startup/new click, gel Move/Remove layout, zero entry jump, one tile delta, world-use gate and cancel restoration. Controlled specialist input; OS/multiplayer/ordinary load NOT_EXECUTED.");
        }
    }
}
