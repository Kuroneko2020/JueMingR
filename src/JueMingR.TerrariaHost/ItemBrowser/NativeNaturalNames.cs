using Terraria;
using Terraria.ID;
using Terraria.Localization;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    internal struct NaturalName
    {
        internal readonly bool Handled;
        internal readonly int ItemType;
        internal readonly string Description;
        internal NaturalName(int item, string description) { Handled = true; ItemType = item; Description = description; }
    }

    // Only explicit target requests reach this finite read-only resolver. Native
    // type/frame identity names the visible plant, never harvest quantity, seed
    // identity or drop probability. No KillTile/drop, world scan or RNG calls.
    internal static class NativeNaturalNames
    {
        private static readonly int[] herbs = { 313, 314, 315, 316, 317, 318, 2358 };
        private static readonly string[] flowerNames = { "蓝色野花", "洋红色野花", "粉色野花", "红色野花", "黄色野花", "蓝紫色野花", "白色野花" };
        private static NaturalName Unknown { get { return new NaturalName(0, null); } }
        private static bool Grid(Tile tile, int step = 18)
        { return tile.frameX >= 0 && tile.frameY >= 0 && tile.frameX % step == 0 && tile.frameY % step == 0; }
        private static NaturalName Description(Tile tile, string text, int step = 18)
        { return Grid(tile, step) ? new NaturalName(0, text) : Unknown; }
        private static NaturalName Item(int item) { return new NaturalName(item, null); }
        internal static NaturalName Resolve(Tile tile)
        {
            if (tile == null || !tile.active()) return default(NaturalName);
            int type = tile.type, style = tile.frameX / 18;
            if (type >= 82 && type <= 84)
                return Grid(tile) && tile.frameY == 0 && style < herbs.Length ? Item(herbs[style]) : Unknown;
            switch (type)
            {
                case 3: case 73:
                    if (!Grid(tile) || tile.frameY != 0 || style > 44 || type == 73 && style == 8) return Unknown;
                    if (type == 3 && style == 8) return Item(5);
                    if (style < 6) return new NaturalName(0, type == 3 ? "野草" : "高茎草");
                    // Read the native declaration, not a copied style/color
                    // table or the packet's placeable item identity.
                    var packets = ItemID.Sets.flowerPacketInfo;
                    if (packets == null) return Unknown;
                    for (int i = 0; i < flowerNames.Length; i++)
                    {
                        int id = 4041 + i;
                        if (id < packets.Length && packets[id] != null && packets[id].stylesOnPurity.Contains(style)) return new NaturalName(0, flowerNames[i]);
                    }
                    return new NaturalName(0, "野花");
                case 24: case 110: case 113: case 201: case 637:
                    if (!Grid(tile) || tile.frameY != 0 || style > 44) return Unknown;
                    if (type == 24 && tile.frameX == 144) return Item(60);
                    if (type == 110 && tile.frameX == 144) return Item(5);
                    if (type == 201 && tile.frameX == 270) return Item(2887);
                    return new NaturalName(0, type == 24 ? "腐化植物" : type == 201 ? "猩红植物" : type == 637 ? "灰烬植物" : type == 113 ? "神圣高茎植物" : "神圣植物");
                case 61: case 74:
                    if (!Grid(tile) || tile.frameY != 0 || style > 22) return Unknown;
                    if (type == 61 && tile.frameX == 144) return Item(331);
                    if (type == 61 && tile.frameX == 162) return Item(223);
                    if (type == 74 && (style == 8 || style == 9)) return Unknown;
                    return new NaturalName(0, type == 74 ? "丛林高茎植物" : style >= 6 ? "丛林花" : "丛林植物");
                case 71:
                    return Grid(tile) && tile.frameY == 0 && style < 5 ? Item(183) : Unknown;
                case 227:
                    if (tile.frameX < 0 || tile.frameX % 34 != 0 || tile.frameY != 0 || tile.frameX / 34 > 11) return Unknown;
                    int dye = tile.frameX / 34;
                    return Item(dye < 8 ? 1107 + dye : 3385 + dye - 8);
                case 236:
                    return Grid(tile) && tile.frameX < 18 * 6 && tile.frameY < 36 ? Item(1291) : Unknown;
                case 52: return Description(tile, "藤蔓");
                case 382: return Description(tile, "开花藤蔓");
                case 62: return Description(tile, "丛林藤蔓");
                case 115: return Description(tile, "神圣藤蔓");
                case 205: return Description(tile, "猩红藤蔓");
                case 636: return Description(tile, "腐化藤蔓");
                case 638: return Description(tile, "灰烬藤蔓");
                case 528: return Description(tile, "蘑菇藤蔓");
                case 184:
                    // WorldGen frames moss color on X22 and its four facing
                    // bands on Y18. Ordinary plant X18 validation rejects real
                    // colored/glowing moss; neither axis implies an item/drop.
                    return tile.frameX >= 0 && tile.frameX <= 220 && tile.frameX % 22 == 0 &&
                        tile.frameY >= 0 && tile.frameY <= 198 && tile.frameY % 18 == 0 ? new NaturalName(0, "长苔藓") : Unknown;
                case 518: return Description(tile, "睡莲叶");
                case 519: return Description(tile, "香蒲");
                case 549: return Description(tile, "水草");
                case 529: return Description(tile, "海滨草");
                case 530: return Description(tile, "绿洲植物");
                case 5: return Description(tile, Language.GetTextValue("MapObject.Tree"), 22);
                case 20: case 590: case 595: case 615: return Description(tile, Language.GetTextValue("MapObject.Sapling"));
                case 323: return Description(tile, Language.GetTextValue("MapObject.PalmTree"), 22);
                case 72: return Description(tile, Language.GetTextValue("MapObject.GiantMushroom"), 22);
                case 32: return Description(tile, "腐化荆棘");
                case 69: return Description(tile, "丛林荆棘");
                case 352: return Description(tile, "猩红荆棘");
                case 444: return Description(tile, "蜂巢");
                case 231: return Description(tile, Language.GetTextValue("MapObject.Larva"));
                case 238: return Description(tile, Language.GetTextValue("MapObject.PlanterasBulb"));
                case 488: case 704: return Description(tile, Language.GetTextValue("MapObject.FallenLog"));
                case 165: case 693: case 694:
                    if (!Grid(tile) || tile.frameX / 54 > 12 || tile.frameY > 90 || type == 693 && tile.frameY != 72 && tile.frameY != 90 || type == 694 && tile.frameY > 54) return Unknown;
                    int group = tile.frameX / 54;
                    // The spider/hive textures are real native groups, but do
                    // not prove either approved rock or ice material identity.
                    if (group == 2 || group == 3) return Unknown;
                    bool ice = group == 0 || group >= 10;
                    bool ceiling = tile.frameY < 36 || tile.frameY == 72;
                    return new NaturalName(0, ice ? ceiling ? "冰锥" : "冰笋" : ceiling ? "钟乳石" : "石笋");
                case 185: case 649: case 650: return Description(tile, "小型天然杂物");
                case 186: case 187: case 647: case 648: return Description(tile, "大型天然杂物");
                case 233: case 651: case 652:
                    // The frame proves size/style, not biome provenance. Use
                    // the approved generic identity without reading neighbours.
                    return Description(tile, "植物丛");
                default: return default(NaturalName);
            }
        }
    }
}
