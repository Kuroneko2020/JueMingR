using System;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeSwitchIdentityChecks
    {
        internal static void Run(object catalog)
        {
            var saved = Main.tile; var flags = (bool[])Main.tileFrameImportant.Clone(); int checks = 0;
            try
            {
                Main.tile = new Tile[Main.maxTilesX, Main.maxTilesY];
                foreach (var sample in ContentSamples.ItemsByType.Values.Where(i => Toggle(i.createTile) != null))
                {
                    var root = TileObjectData.GetTileData(sample.createTile, sample.placeStyle);
                    Require(root != null, "native switch fixture metadata"); Main.tileFrameImportant[sample.createTile] = true;
                    for (int alternate = 0; alternate <= root.AlternatesCount; alternate++)
                    {
                        var data = TileObjectData.GetTileData(sample.createTile, sample.placeStyle, alternate);
                        for (int variant = 0; variant < Math.Max(1, data.RandomStyleRange); variant++)
                        {
                            for (int x = 18; x < 38; x++) for (int y = 18; y < 38; y++) Main.tile[x, y] = new Tile();
                            Require(TileObject.Place(new TileObject { xCoord = 20, yCoord = 20, type = sample.createTile, style = sample.placeStyle, alternate = alternate, random = variant }), "native switched fixture placement");
                            string operation = Toggle(sample.createTile);
                            var method = (operation.StartsWith("Switch") ? typeof(WorldGen) : typeof(Wiring)).GetMethod(operation);
                            for (int state = 0; state < (sample.createTile == 658 ? 4 : 3); state++)
                            {
                                if (state > 0) method.Invoke(null, method.GetParameters().Length == 2 ? new object[] { 20, 20 } : method.GetParameters().Length == 4 ? new object[] { 20, 20, Main.tile[20, 20], null } : new object[] { 20, 20, Main.tile[20, 20], null, false });
                                for (int x = 0; x < data.Width; x++) for (int y = 0; y < data.Height; y++)
                                {
                                    int actual = (int)Call(catalog, "PlacedItem", Main.tile[20 + x, 20 + y]);
                                    Require(actual == sample.type, "native switch identity item/alternate/variant/state/cell " + sample.type + "/" + alternate + "/" + variant + "/" + state + "/" + x + "/" + y + " returned " + actual);
                                    checks++;
                                }
                            }
                        }
                    }
                }
                Console.WriteLine("PASS: " + checks + " native placement -> Wiring/WorldGen switch -> restored identities across switch-family item styles and cells.");
            }
            finally { Main.tile = saved; Array.Copy(flags, Main.tileFrameImportant, flags.Length); }
        }
        private static string Toggle(int type)
        {
            switch (type)
            {
                case 4: return "ToggleTorch";
                case 33: case 49: case 174: case 372: case 646: return "ToggleCandle";
                case 42: return "ToggleHangingLantern";
                case 93: return "ToggleLamp";
                case 95: case 100: case 126: case 173: case 564: return "Toggle2x2Light";
                case 92: return "ToggleLampPost";
                case 34: return "ToggleChandelier";
                case 149: return "ToggleHolidayLight";
                case 215: return "ToggleCampFire";
                case 405: return "ToggleFirePlace";
                case 35: case 139: return "SwitchMB";
                case 207: return "SwitchFountain";
                case 410: case 480: case 509: case 657: case 658: case 720: case 721: case 725: case 733: return "SwitchMonolith";
                default: return null;
            }
        }
    }
}
