using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;
namespace NativeWorldTextProbe
{
    internal static class NativeCombatPresentationChecks
    {
        internal static void Project(object world)
        {
            var zoom=Main.GameViewMatrix.ZoomMatrix;var inverse=Matrix.Invert(zoom);
            Set(world,"zoom",zoom);Set(world,"inverse",inverse);Call(world,"ProjectPresentation");
            Call(Get(world,"Marker"),"Project",zoom,inverse);
        }
    }
}
