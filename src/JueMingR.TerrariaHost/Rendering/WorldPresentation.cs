using Terraria;

namespace JueMingR.TerrariaHost.Rendering
{
    // Owned by native interface setup. Update may precede the first completed
    // setup, so Pending must neither draw nor announce a confirmed failure.
    internal enum WorldLayerStatus { Pending, Ready, Unavailable }

    internal static class WorldPresentation
    {
        internal static bool CanDraw
        {
            get
            {
                var capture = Terraria.Graphics.Capture.CaptureManager.Instance;
                return !Main.gameMenu && !Main.dedServ && (Main.netMode == 0 || Main.netMode == 1) &&
                    Main.LocalPlayer != null && Main.LocalPlayer.active && !Main.mapFullscreen && !Main.hideUI &&
                    !Main.onlyDrawFancyUI && !Main.inFancyUI && !Main.ingameOptionsWindow && capture != null && !capture.Active;
            }
        }
    }
}
