using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ObjectData;

namespace JueMingR.TerrariaHost.Blueprint
{
    // Construction belongs to the native metadata owner on the game thread.
    // The published graph contains copied scalars/arrays only. In particular,
    // calls from the library worker never reach TileObjectData, Main or a Tile.
    internal sealed class BlueprintHorizontalMirrorResolver : IBlueprintHorizontalMirrorProfile
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static BlueprintHorizontalMirrorResolver current;
        private readonly Layout[] roots;
        private readonly bool[] platforms, trees, campfires;
        public long Revision { get { return Ready ? 14580001L : 0; } }
        public bool Ready { get; }

        internal static BlueprintHorizontalMirrorResolver Create()
        {
            // Startup can precede native Initialize. Do not permanently cache
            // that early unavailable result; a ready table is immutable in .8.
            if (current != null && current.Ready) return current;
            var value = new BlueprintHorizontalMirrorResolver();
            if (value.Ready) current = value;
            return value;
        }

        private BlueprintHorizontalMirrorResolver()
        {
            roots = new Layout[TileID.Count];
            platforms = new bool[TileID.Count]; trees = new bool[TileID.Count]; campfires = new bool[TileID.Count];
            try
            {
                if (typeof(TileObjectData).Assembly.GetName().Version != new Version(1, 4, 5, 8)) return;
                PropertyInfo alternates = typeof(TileObjectData).GetProperty("Alternates", PrivateInstance);
                PropertyInfo subTiles = typeof(TileObjectData).GetProperty("SubTiles", PrivateInstance);
                PropertyInfo linked = typeof(TileObjectData).GetProperty("LinkedAlternates", PrivateInstance);
                if (alternates == null || subTiles == null || linked == null || linked.PropertyType != typeof(bool) ||
                    alternates.PropertyType != typeof(List<TileObjectData>) || subTiles.PropertyType != typeof(List<TileObjectData>)) return;
                // A root probe establishes normal startup; no native startup,
                // unlock, world framing or random generator is invoked here.
                var door = TileObjectData.GetTileData(TileID.ClosedDoor, int.MaxValue);
                if (door == null || door.Width != 1 || door.Height != 3) return;
                var copied = new Dictionary<TileObjectData, Layout>();
                for (int type = 0; type < roots.Length; type++)
                {
                    roots[type] = Copy(TileObjectData.GetTileData(type, int.MaxValue), copied, alternates, subTiles);
                    platforms[type] = type < TileID.Sets.Platforms.Length && TileID.Sets.Platforms[type];
                    trees[type] = type < TileID.Sets.IsATreeTrunk.Length && TileID.Sets.IsATreeTrunk[type];
                    campfires[type] = type < TileID.Sets.Campfires.Length && TileID.Sets.Campfires[type];
                }
                Ready = true;
            }
            catch (NullReferenceException) { }
            catch (ArgumentException) { }
            catch (TargetInvocationException) { }
            catch (OverflowException) { }
        }

        private sealed class Layout
        {
            internal int Width, Height, OriginX, OriginY, Step, FullWidth, FullHeight, Direction, Style, Multiplier, Wrap, LineSkip, Random, Selector;
            internal int[] Rows;
            internal Anchor Top, Bottom, Left, Right;
            internal bool Wall, Horizontal, ValidGrid;
            internal Layout[] Alternates, SubTiles;
            internal int SheetStep, SheetSpan;
        }
        private readonly struct Anchor
        {
            internal Anchor(AnchorData value) { Type = (int)value.type; Count = value.tileCount; Start = value.checkStart; }
            internal readonly int Type, Count, Start;
            internal bool Same(Anchor other) { return Type == other.Type && Count == other.Count && (Count == 0 || Start == other.Start); }
            internal bool Reflected(Anchor other, int width)
            { return Type == other.Type && Count == other.Count && (Count == 0 || width - Start - Count == other.Start); }
        }
        private static Layout Copy(TileObjectData native, Dictionary<TileObjectData, Layout> copied, PropertyInfo alternates, PropertyInfo subTiles)
        {
            if (native == null) return null;
            Layout value;
            if (copied.TryGetValue(native, out value)) return value;
            value = new Layout(); copied.Add(native, value);
            value.Width = native.Width; value.Height = native.Height; value.OriginX = native.Origin.X; value.OriginY = native.Origin.Y;
            value.Step = checked(native.CoordinateWidth + native.CoordinatePadding); value.FullWidth = native.CoordinateFullWidth; value.FullHeight = native.CoordinateFullHeight;
            value.Horizontal = native.StyleHorizontal; value.Direction = (int)native.Direction; value.Style = native.Style; value.Multiplier = native.StyleMultiplier;
            value.Wrap = native.StyleWrapLimit; value.LineSkip = native.StyleLineSkip; value.Random = native.RandomStyleRange;
            value.Top = new Anchor(native.AnchorTop); value.Bottom = new Anchor(native.AnchorBottom); value.Left = new Anchor(native.AnchorLeft); value.Right = new Anchor(native.AnchorRight); value.Wall = native.AnchorWall;
            value.Selector = native.GetStyleOverride == null ? 0 : native.GetStyleOverride == TileObjectData.GetStyle_Detritus ? 1 :
                native.GetStyleOverride == TileObjectData.GetStyle_SmallPiles ? 2 : native.GetStyleOverride == TileObjectData.GetStyle_Stalactite ? 3 : -1;
            int[] heights = native.CoordinateHeights;
            value.Rows = new int[heights == null ? 0 : heights.Length];
            int row = 0;
            for (int i = 0; i < value.Rows.Length; i++) { value.Rows[i] = row; row = checked(row + heights[i] + native.CoordinatePadding); }
            value.ValidGrid = native.CoordinateWidth > 0 && native.CoordinatePadding >= 0 && value.Rows.Length == value.Height &&
                checked(value.Step * value.Width + native.CoordinatePaddingFix.X) == value.FullWidth &&
                checked(row + native.CoordinatePaddingFix.Y) == value.FullHeight;
            if (heights != null) foreach (int height in heights) value.ValidGrid &= height > 0;
            var a = (List<TileObjectData>)alternates.GetValue(native, null);
            var s = (List<TileObjectData>)subTiles.GetValue(native, null);
            value.Alternates = new Layout[a == null ? 0 : a.Count]; value.SubTiles = new Layout[s == null ? 0 : s.Count];
            for (int i = 0; i < value.Alternates.Length; i++) value.Alternates[i] = Copy(a[i], copied, alternates, subTiles);
            for (int i = 0; i < value.SubTiles.Length; i++) value.SubTiles[i] = Copy(s[i], copied, alternates, subTiles);
            // Some original specialized placers encode direction/attachment in
            // complete style sheets despite Multiplier=1. The declared sheet
            // offsets, rather than alternate indices, define their period.
            // Random biome ranges (saplings) do not meet this mechanism.
            bool horizontal = value.Direction != 0 || value.Left.Count != 0 || value.Right.Count != 0;
            int step = int.MaxValue, maximum = 0;
            foreach (Layout item in value.Alternates) if (item != null)
            {
                horizontal |= item.Direction != 0 || item.Left.Count != 0 || item.Right.Count != 0;
                if (item.Style > 0) { step = Math.Min(step, item.Style); maximum = Math.Max(maximum, item.Style); }
            }
            if (value.Multiplier == 1 && value.LineSkip == 1 && value.Random == 0 && horizontal && step != int.MaxValue)
            { value.SheetStep = step; value.SheetSpan = checked(maximum + step); }
            return value;
        }

        private sealed class Decoded
        {
            internal CaptureObject Object;
            internal Layout Data, Main;
            internal int BaseX, BaseY;
            internal bool Independent, Sheet;
        }
        private static Layout Styled(Layout root, int style)
        { return style >= 0 && style < root.SubTiles.Length && root.SubTiles[style] != null ? root.SubTiles[style] : root; }

        private bool Decode(CaptureCell cell, bool legacy, out Decoded result)
        {
            result = null;
            if (!Ready || cell.TileType >= roots.Length || roots[cell.TileType] == null || cell.FrameX < 0 || cell.FrameY < 0) return false;
            CaptureCell raw = cell;
            if (!legacy) cell = Normalize(cell);
            Layout basis = roots[cell.TileType];
            if (!Grid(basis)) return false;
            int column = cell.FrameX / basis.FullWidth, row = cell.FrameY / basis.FullHeight, wrap = Math.Max(1, basis.Wrap);
            int placement = checked(basis.Horizontal ? row * wrap + column : column * wrap + row);
            int style = placement / basis.Multiplier, variant = placement % basis.Multiplier;
            if (basis.LineSkip > 1)
            {
                int line = basis.Horizontal ? row : column, primary = basis.Horizontal ? column : row;
                style = checked(line / basis.LineSkip * wrap + primary); variant = line % basis.LineSkip;
            }
            bool independent = cell.TileType == TileID.ClosedDoor;
            Layout main = Styled(basis, style), data = main;
            int alternate = 0, regionY = 0;
            bool sheet = !legacy && basis.SheetStep > 0;
            if (independent)
            {
                style = checked(cell.FrameX / 54 * 36 + cell.FrameY / 54); placement = style; variant = 0;
                main = data = Styled(basis, style); sheet = false;
            }
            else if (basis.Selector != 0)
            {
                if (basis.Selector < 0) return false;
                int index = -1;
                if (basis.Selector == 1 && cell.FrameY >= 36) { index = 0; regionY = 36; }
                else if (basis.Selector == 2 && cell.FrameY >= 18) { index = 0; regionY = 18; }
                else if (basis.Selector == 3)
                {
                    if (cell.FrameY >= 90) { index = 2; regionY = 90; }
                    else if (cell.FrameY >= 72) { index = 1; regionY = 72; }
                    else if (cell.FrameY >= 36) { index = 0; regionY = 36; }
                }
                data = index < 0 ? basis : index < basis.SubTiles.Length ? basis.SubTiles[index] : null;
                if (!Grid(data)) return false;
                style = cell.FrameX / data.FullWidth; placement = style; variant = 0; main = data; sheet = false;
            }
            else
            {
                if (sheet)
                {
                    int sheetSlot = placement % basis.SheetSpan, offset = sheetSlot / basis.SheetStep * basis.SheetStep;
                    style = placement - offset; variant = 0; main = Styled(basis, style); data = main;
                    if (offset != main.Style)
                    {
                        data = null;
                        for (int i = 0; i < main.Alternates.Length; i++) if (main.Alternates[i] != null && main.Alternates[i].Style == offset)
                        { data = main.Alternates[i]; alternate = i + 1; break; }
                        if (data == null) return false;
                    }
                }
                else
                {
                    for (int i = 0; i < main.Alternates.Length; i++)
                    {
                        Layout item = main.Alternates[i];
                        if (item != null && variant >= item.Style && variant <= (long)item.Style + item.Random)
                        { data = item; alternate = i + 1; break; }
                    }
                }
            }
            if (!Grid(data)) return false;
            int baseX = independent ? cell.FrameX / 54 * 54 : cell.FrameX / data.FullWidth * data.FullWidth;
            int baseY = basis.Selector != 0 ? regionY : cell.FrameY / data.FullHeight * data.FullHeight;
            int localX = cell.FrameX - baseX, localY = cell.FrameY - baseY;
            if (localX % data.Step != 0 || (independent ? localX >= 54 : localX / data.Step >= data.Width)) return false;
            int memberRow = Array.IndexOf(data.Rows, localY);
            if (memberRow < 0) return false;
            // Operating displacement is saved in the raw member grid; it must
            // not masquerade as another material, direction or random variant.
            result = new Decoded { Data = data, Main = main, BaseX = checked(baseX + raw.FrameX - cell.FrameX), BaseY = checked(baseY + raw.FrameY - cell.FrameY), Independent = independent,
                Sheet = sheet || raw.FrameX != cell.FrameX || raw.FrameY != cell.FrameY,
                Object = new CaptureObject { X = cell.X - (independent ? 0 : localX / data.Step), Y = cell.Y - memberRow,
                    Width = data.Width, Height = data.Height, Type = cell.TileType, Style = style, Alternate = alternate, Direction = data.Direction,
                    OriginX = data.OriginX, OriginY = data.OriginY, PlacementStyle = placement, RandomVariant = variant, Reliable = true } };
            return true;
        }
        private static bool Grid(Layout value)
        { return value != null && value.ValidGrid && value.Width > 0 && value.Height > 0 && (long)value.Width * value.Height < CaptureChunk.MaximumRecords && value.Step > 0 && value.FullWidth > 0 && value.FullHeight > 0 && value.Multiplier > 0 && value.Rows != null && value.Rows.Length == value.Height; }

        public bool TryNormalizeObject(CaptureObject source, IReadOnlyList<CaptureCell> sourceMembers, out CaptureObject target, out string reason)
        {
            target = source; reason = null;
            Decoded first;
            if (!Complete(source, sourceMembers, out first)) { reason = "对象完整成员或原版布局无法确认"; return false; }
            target = first.Object; target.Id = source.Id; target.MissingPayload = source.MissingPayload;
            return true;
        }
        private bool Complete(CaptureObject source, IReadOnlyList<CaptureCell> members, out Decoded first)
        {
            first = null;
            if (!Ready || !source.Reliable || source.Id <= 0 || members == null || source.Width <= 0 || source.Height <= 0 ||
                (long)source.Width * source.Height != members.Count || members.Count == 0) return false;
            try
            {
                if (!Decode(members[0], false, out first)) return false;
                if (!Same(source, first.Object))
                {
                    // Only the exact legacy decoder alias of this proven sheet
                    // or finite operating displacement
                    // can be corrected. An arbitrary reliable flag or metadata
                    // mismatch does not grant permission to reinterpret an asset.
                    Decoded legacy;
                    if (!first.Sheet || !Decode(members[0], true, out legacy) || !Same(source, legacy.Object)) return false;
                }
                for (int i = 0; i < members.Count; i++)
                {
                    CaptureCell cell = members[i]; Decoded decoded;
                    if ((cell.Layers & CaptureLayer.Tile) == 0 || cell.ObjectId != source.Id || cell.UnreliableObject ||
                        cell.X != checked(source.X + i % source.Width) || cell.Y != checked(source.Y + i / source.Width) ||
                        !Shape(cell) || !Decode(cell, false, out decoded) || !Same(first.Object, decoded.Object) ||
                        decoded.BaseX != first.BaseX || decoded.BaseY != first.BaseY) return false;
                }
                return true;
            }
            catch (OverflowException) { return false; }
        }
        private static bool Same(CaptureObject a, CaptureObject b)
        { return a.Type == b.Type && a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height && a.Style == b.Style && a.Alternate == b.Alternate && a.Direction == b.Direction && a.OriginX == b.OriginX && a.OriginY == b.OriginY && a.PlacementStyle == b.PlacementStyle && a.RandomVariant == b.RandomVariant; }
        private static bool Shape(CaptureCell value) { return value.Slope <= 4 && (!value.HalfBrick || value.Slope == 0); }
        private static byte MirrorSlope(byte value) { return value == 1 ? (byte)2 : value == 2 ? (byte)1 : value == 3 ? (byte)4 : value == 4 ? (byte)3 : value; }

        public bool TryMirrorCell(CaptureCell source, int templateWidth, out CaptureCell target, out string reason)
        {
            target = source; reason = null;
            if (!Ready || templateWidth <= 0 || source.X < 0 || source.X >= templateWidth || !Shape(source) || source.UnreliableObject)
            { reason = "镜像坐标或形状无法确认"; return false; }
            if ((source.Layers & CaptureLayer.Tile) != 0 && source.ObjectId > 0)
            { reason = "对象镜像需要完整成员"; return false; }
            target.X = templateWidth - 1 - source.X; target.Slope = MirrorSlope(source.Slope);
            if ((source.Layers & CaptureLayer.Tile) == 0) return true;
            try
            {
                if (source.TileType < platforms.Length && platforms[source.TileType])
                {
                    if (!Platform(source, ref target)) { reason = "平台帧超出原版二十七槽"; return false; }
                }
                else if (source.TileType == TileID.PalmTree)
                {
                    // Palm frameY is signed horizontal lean in original .8,
                    // not a material/style row and not destination ecology.
                    target.FrameY = checked((short)-source.FrameY);
                }
                else if (source.TileType < trees.Length && trees[source.TileType]) Tree(source, ref target);
                else if (source.TileType < roots.Length && roots[source.TileType] != null)
                { reason = "原版注册对象需要完整成员"; return false; }
                return true;
            }
            catch (OverflowException) { reason = "镜像帧超出范围"; return false; }
        }
        private static readonly int[] platformSlots = { 0, 2, 1, 4, 3, 5, 7, 6, 10, 11, 8, 9, 13, 12, 14, 16, 15, 18, 17, 20, 19, 22, 21, 24, 23, 26, 25 };
        private static bool Platform(CaptureCell source, ref CaptureCell target)
        {
            if (source.FrameX < 0 || source.FrameX >= 27 * 18 || source.FrameX % 18 != 0 || source.FrameY < 0 || source.FrameY % 18 != 0) return false;
            target.FrameX = (short)(platformSlots[source.FrameX / 18] * 18); return true;
        }
        private static void Tree(CaptureCell source, ref CaptureCell target)
        {
            int x = source.FrameX, y = source.FrameY;
            // GrowTree/CheckTree use these structural pairs; random 22px rows
            // within a form remain the saved decoration. Selected fragments
            // are transformed alone, without inventing the rest of a tree.
            if (y >= 198 && y <= 242) { if (x == 44) x = 66; else if (x == 66) x = 44; }
            else if (y >= 132 && y <= 176)
            {
                if (x == 22) x = 44; else if (x == 44) x = 22; else if (x == 0) x = 66; else if (x == 66) x = 0;
            }
            else if (y >= 0 && y <= 44)
            {
                if (x == 66) { x = 88; y += 66; } else if (x == 88) { x = 66; y += 66; }
                else if (x == 132 || x == 154) y += 66;
            }
            else if (y >= 66 && y <= 110)
            {
                if (x == 66) { x = 88; y -= 66; } else if (x == 88) { x = 66; y -= 66; }
                else if (x == 132 || x == 154) y -= 66;
            }
            target.FrameX = (short)x; target.FrameY = (short)y;
        }

        public bool TryMirrorObject(CaptureObject source, IReadOnlyList<CaptureCell> sourceMembers, int templateWidth,
            out CaptureObject target, out CaptureCell[] targetMembers, out string reason)
        {
            target = source; targetMembers = null; reason = null;
            Decoded raw;
            if (!Complete(source, sourceMembers, out raw) || templateWidth <= 0 || source.X < 0 || (long)source.X + source.Width > templateWidth)
            { reason = "对象完整成员或原版布局无法确认"; return false; }
            try
            {
                var members = new CaptureCell[sourceMembers.Count];
                int x = checked(templateWidth - source.X - source.Width);
                Decoded semantic;
                CaptureCell normalizedFirst = Normalize(sourceMembers[0]);
                if (!Decode(normalizedFirst, false, out semantic)) { reason = "对象运行帧无法确认"; return false; }
                Layout counterpart = semantic.Data;
                int baseX = semantic.BaseX, baseY = semantic.BaseY;
                bool door = source.Type == TileID.OpenDoor || source.Type == TileID.ClosedDoor;
                bool platform = source.Type < platforms.Length && platforms[source.Type];
                if (source.Type == TileID.OpenDoor) baseX = baseX / 72 * 72 + (baseX % 72 < 36 ? 36 : 0);
                else if (!door && !platform)
                {
                    counterpart = Counterpart(semantic);
                    if (counterpart == null) { reason = "原版登记布局缺少对应镜像形态"; return false; }
                    if (!ReferenceEquals(counterpart, semantic.Data))
                    {
                        int random = checked(semantic.Object.PlacementStyle - semantic.Object.Style * semantic.Data.Multiplier - semantic.Data.Style);
                        FrameBase(counterpart, checked(semantic.Object.Style * counterpart.Multiplier + counterpart.Style + random), out baseX, out baseY);
                    }
                }
                for (int i = 0; i < members.Length; i++)
                {
                    int row = i / source.Width, col = i % source.Width;
                    CaptureCell original = sourceMembers[row * source.Width + source.Width - 1 - col], value = original;
                    value.X = x + col; value.Slope = MirrorSlope(value.Slope);
                    if (platform)
                    { if (!Platform(original, ref value)) { reason = "平台帧超出原版二十七槽"; return false; } }
                    else if (source.Type != TileID.ClosedDoor)
                    {
                        CaptureCell normalized = Normalize(original);
                        value.FrameX = checked((short)(baseX + col * counterpart.Step + original.FrameX - normalized.FrameX));
                        value.FrameY = checked((short)(baseY + counterpart.Rows[row] + original.FrameY - normalized.FrameY));
                    }
                    members[i] = value;
                }
                Decoded decoded;
                if (!Decode(members[0], false, out decoded)) { reason = "镜像后的合法布局无法确认"; return false; }
                target = decoded.Object; target.Id = source.Id; target.MissingPayload = source.MissingPayload;
                Decoded proof;
                if (!Complete(target, members, out proof)) { reason = "镜像后的完整成员证明失败"; return false; }
                targetMembers = members; return true;
            }
            catch (OverflowException) { reason = "镜像对象帧超出范围"; return false; }
        }
        private static Layout Counterpart(Decoded decoded)
        {
            Layout data = decoded.Data, main = decoded.Main;
            bool directional = main.Direction != 0;
            foreach (Layout item in main.Alternates) if (item != null && item.Direction != 0) directional = true;
            // An undirected native body keeps its legal member grid and real
            // placement anchors. Asymmetric click/support anchors (e.g. a
            // hanging body's single support) are not a second visual direction.
            // Actual left/right attachment families still need a counterpart.
            if (!directional && data.Left.Count == 0 && data.Right.Count == 0) return data;
            bool declaredLeft = main.Direction == 1;
            foreach (Layout item in main.Alternates) if (item != null && item.Direction == 1) declaredLeft = true;
            int direction = data.Direction == 1 ? 2 : data.Direction == 2 ? (declaredLeft ? 1 : 0) : 0;
            if (data.Direction == 0)
                foreach (Layout item in main.Alternates) if (item != null && item.Direction == 2 && !declaredLeft) { direction = 2; break; }
            if (MirrorLayout(data, data, direction)) return data;
            if (MirrorLayout(data, main, direction)) return main;
            foreach (Layout item in main.Alternates) if (MirrorLayout(data, item, direction)) return item;
            return null;
        }
        private static bool MirrorLayout(Layout a, Layout b, int direction)
        {
            if (!Grid(b) || b.Direction != direction || a.Width != b.Width || a.Height != b.Height || a.Step != b.Step || a.FullWidth != b.FullWidth || a.FullHeight != b.FullHeight ||
                a.Multiplier != b.Multiplier || a.Random != b.Random || a.Wall != b.Wall || !a.Top.Reflected(b.Top, a.Width) || !a.Bottom.Reflected(b.Bottom, a.Width) || !a.Left.Same(b.Right) || !a.Right.Same(b.Left)) return false;
            for (int i = 0; i < a.Rows.Length; i++) if (a.Rows[i] != b.Rows[i]) return false;
            return true;
        }
        private static void FrameBase(Layout data, int placement, out int x, out int y)
        {
            int cross = 0;
            if (data.Wrap > 0) { cross = checked(placement / data.Wrap * data.LineSkip); placement %= data.Wrap; }
            x = checked((data.Horizontal ? placement : cross) * data.FullWidth);
            y = checked((data.Horizontal ? cross : placement) * data.FullHeight);
        }

        private CaptureCell Normalize(CaptureCell cell)
        {
            int x = cell.FrameX, y = cell.FrameY;
            switch (cell.TileType)
            {
                case 33: case 49: case 174: case 372: case 646: case 42: case 93: case 92: case 593: if (x >= 18 && x < 36) x -= 18; break;
                case 95: case 100: case 126: case 173: case 564: case 35: case 139: case 565: case 594: if (x >= 36 && x < 72) x -= 36; break;
                case 34: if (x % 108 >= 54) x -= 54; break;
                case 4: if (x >= 66 && x < 132) x -= 66; break;
                case 149: case 405: case 244: if (x >= 54 && x < 108) x -= 54; break;
                case 136: case 144: if (y >= 18 && y < 36) y -= 18; break;
                case 207: if (y >= 72 && y < 144) y -= 72; break;
                case 410: if (y >= 56 && y < 112) y -= 56; break;
                case 480: case 509: case 657: case 720: case 721: case 725: case 733: if (y >= 54 && y < 108) y -= 54; break;
                case 658: if (y >= 54 && y < 162) y %= 54; break;
            }
            if (cell.TileType < campfires.Length && campfires[cell.TileType] && y >= 36 && y < 72) y -= 36;
            cell.FrameX = checked((short)x); cell.FrameY = checked((short)y); return cell;
        }
        internal static CaptureCell NormalizeOperatingFrame(CaptureCell cell) { return Create().Normalize(cell); }

        // These are game-thread decoder adapters, never worker entry points.
        // A true result includes both reliable and explicit unknown shapes;
        // only an unregistered selected-cell tile returns false/no shape.
        internal static bool TryDescribeNative(CaptureCell cell, out BlueprintObjectShape shape)
        {
            shape = null; var profile = Create();
            if (profile.Ready && (cell.TileType >= profile.roots.Length || profile.roots[cell.TileType] == null)) return false;
            Decoded decoded;
            try
            {
                if (profile.Decode(cell, false, out decoded))
                {
                    shape = new BlueprintObjectShape { Object = decoded.Object, BaseFrameX = decoded.BaseX, BaseFrameY = decoded.BaseY,
                        ColumnStep = decoded.Data.Step, RowOffsets = (int[])decoded.Data.Rows.Clone(), IndependentMemberFrames = decoded.Independent };
                    return true;
                }
            }
            catch (OverflowException) { }
            shape = new BlueprintObjectShape { Object = new CaptureObject { X = cell.X, Y = cell.Y, Width = 1, Height = 1, Type = cell.TileType, Reliable = false, Reason = "原版对象布局无法确认" } };
            return true;
        }
        internal static TileObjectData NativeData(CaptureCell cell)
        {
            var profile = Create(); Decoded decoded;
            if (!profile.Decode(cell, false, out decoded)) return null;
            TileObjectData basis = TileObjectData.GetTileData(cell.TileType, int.MaxValue);
            if (basis.GetStyleOverride != null) { var tile = new Tile { type = cell.TileType, frameX = cell.FrameX, frameY = cell.FrameY }; tile.active(true); return basis.GetStyleOverride(tile, basis); }
            return TileObjectData.GetTileData(cell.TileType, decoded.Object.Style, decoded.Object.Alternate);
        }
    }
}
