namespace JueMingR.Features.Blueprint
{
    // WorldGen places 171 as a single 4x8 body. Only the top-left tile
    // carries the four ornament slots; all other frames are tile ordinals.
    public static class BlueprintChristmasTreeMembers
    {
        public const ushort TileType = 171;
        public const int ItemType = 1873;
        public const int Width = 4, Height = 8;
        public const int OriginX = 1, OriginY = 7;
        public const short PlacedAnchorFrameX = 10;
        public const int OrnamentSlots = 4;
        public static bool IsAnchor(CaptureCell cell) => cell.TileType == TileType && cell.FrameX >= PlacedAnchorFrameX;

        public static bool TryMember(CaptureCell cell, out int column, out int row)
        {
            column = row = 0;
            if (cell.FrameX >= PlacedAnchorFrameX) return ValidOrnaments(cell.FrameY);
            column = cell.FrameX; row = cell.FrameY;
            return (uint)column < Width && (uint)row < Height && (column != 0 || row != 0);
        }

        private static bool ValidOrnaments(int packed)
        {
            // Draw/check read these bit fields; Player.PlaceThing_XMasTreeTops
            // defines the reachable range of each slot (zero means absent).
            return (uint)packed <= 0x3fff && (packed & 7) <= 4 && ((packed >> 3) & 7) <= 6 &&
                ((packed >> 6) & 15) <= 11 && (packed >> 10) <= 11;
        }
        public static int Ornament(int packed, int slot)
        {
            switch (slot)
            {
                case 0: return packed & 7;
                case 1: return (packed >> 3) & 7;
                case 2: return (packed >> 6) & 15;
                case 3: return (packed >> 10) & 15;
                default: return 0;
            }
        }
        public static int OrnamentItem(int packed, int slot)
        {
            int ornament = Ornament(packed, slot);
            if (ornament == 0) return 0;
            switch (slot)
            {
                case 0: return 1873 + ornament;
                case 1: return 1877 + ornament;
                case 2: return 1883 + ornament;
                case 3: return 1894 + ornament;
                default: return 0;
            }
        }

        public static bool Matches(CaptureCell cell, int index)
        {
            return (uint)index < Width * Height && TryMember(cell, out int column, out int row) &&
                index == row * Width + column;
        }

        public static void WriteMember(ref CaptureCell cell, int column, int row, CaptureCell anchor)
        {
            bool first = column == 0 && row == 0;
            cell.FrameX = first ? anchor.FrameX : (short)column;
            cell.FrameY = first ? anchor.FrameY : (short)row;
        }
    }
}
