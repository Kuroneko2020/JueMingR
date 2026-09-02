using System;

namespace JueMingR.TerrariaHost
{
    internal sealed class TerrariaReferenceProbe
    {
        internal Type TerrariaMainType
        {
            get { return typeof(Terraria.Main); }
        }

        internal Type ReLogicPlatformType
        {
            get { return typeof(ReLogic.OS.Platform); }
        }

        internal Type XnaGameType
        {
            get { return typeof(Microsoft.Xna.Framework.Game); }
        }
    }
}
