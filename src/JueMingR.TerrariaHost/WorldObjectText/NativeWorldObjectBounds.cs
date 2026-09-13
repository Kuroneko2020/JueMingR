using JueMingR.Platform.WorldObjectText;

namespace JueMingR.TerrariaHost.WorldObjectText
{
    // Terraria 1.4.5.8 closed-frame visible bounds, measured from shipped XNB
    // alpha and TileDrawing.GetTileDrawData (see the implementation design).
    // These are art bounds, not placement/hit boxes. 441/468 have separate
    // textures but the same bounds within the styles our resolver accepts.
    // Keep this version adapter finite: no texture readback or scan in Draw.
    internal static class NativeWorldObjectBounds
    {
        private static readonly byte[] chestTop = { 6,6,6,6,6,4,2,6,6,6,6,6,6,8,6,6,6,6,2,2,2,2,2,2,2,2,2,2,10,6,6,8,6,2,6,6,6,6,6,6,6,6,6,6,6,6,6,8,6,4,4,2 };
        private static readonly byte[] chest2Top = { 4,4,6,4,6,6,6,6,6,4,4,6,2,2,6,6,6,6,6,4,4,2,4,0,4,6,2,6,6,6,0,2,4,8,6,12,0,2 };
        private static readonly byte[] signTop = { 0,0,6,6,6 }, signBottom = { 32,32,28,28,28 };
        private static readonly byte[] announcementTop = { 0,0,6,6,8 }, announcementBottom = { 32,32,30,30,26 };
        private static readonly byte[] tombstoneTop = { 4,2,2,2,2,2,2,4,2,2,2 };

        internal static float AnchorY(WorldObject value, float screenY, int screenHeight, bool inverted)
        {
            int top = 0, bottom = 32, style = value.Style;
            switch (value.Type)
            {
                case 21: case 441:
                    if ((uint)style < chestTop.Length) { top = chestTop[style]; bottom = 34; } break;
                case 467: case 468:
                    if ((uint)style < chest2Top.Length) { top = chest2Top[style]; bottom = style == 31 ? 28 : 34; } break;
                case 88: if (style == 57) bottom = 28; break;
                case 55: case 425:
                    if ((uint)style < signTop.Length) { top = signTop[style]; bottom = signBottom[style]; } break;
                case 573:
                    if ((uint)style < announcementTop.Length) { top = announcementTop[style]; bottom = announcementBottom[style]; } break;
                case 85:
                    if ((uint)style < tombstoneTop.Length) { top = tombstoneTop[style]; bottom = 34; } break;
            }
            float tileY = value.TileY * 16f - screenY;
            // Inversion exposes the original bottom; a transparent top inset
            // cannot be added to a mirrored, fixed-height tile rectangle.
            return inverted ? screenHeight - tileY - bottom : tileY + top;
        }
    }
}
