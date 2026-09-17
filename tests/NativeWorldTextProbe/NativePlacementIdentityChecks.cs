using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativePlacementIdentityChecks
    {
        internal static void Run(object catalog)
        {
            var saved = Main.tile; var flags = (bool[])Main.tileFrameImportant.Clone();
            var random = Main.rand; int checks = 0;
            try
            {
                // Native TileObject.Place is an independent frame oracle on an
                // isolated empty tile array; production never calls this writer.
                Main.tile = new Tile[Main.maxTilesX, Main.maxTilesY];
                foreach (int itemType in new[] { 48, 306, 3270, 2699, 5472, 3977, 498, 1989, 4326, 275, 2625, 2626, 4072, 4073, 4071, 149, 1813, 4420, 4609, 966 })
                {
                    Item sample = ContentSamples.ItemsByType[itemType];
                    var styles = new List<int> { sample.placeStyle };
                    if (itemType == 5472) styles.AddRange(new[] { 1, 2 });
                    if (itemType == 2625) styles.AddRange(new[] { 1, 2 });
                    if (itemType == 2626) styles.AddRange(new[] { 4, 5 });
                    if (itemType == 4072) styles.AddRange(new[] { 7, 8 });
                    if (itemType == 4073) styles.AddRange(new[] { 10, 11 });
                    if (itemType == 4071) styles.AddRange(new[] { 13, 14 });
                    foreach (int style in styles)
                    {
                        var root = TileObjectData.GetTileData(sample.createTile, style); Require(root != null, "native placed fixture metadata " + itemType);
                        Main.tileFrameImportant[sample.createTile] = true;
                        for (int alternate = 0; alternate <= root.AlternatesCount; alternate++)
                        {
                            var data = TileObjectData.GetTileData(sample.createTile, style, alternate);
                            for (int variant = 0; variant < Math.Max(1, data.RandomStyleRange); variant++)
                            {
                                for (int x = 18; x < 38; x++) for (int y = 18; y < 38; y++) Main.tile[x, y] = new Tile();
                                Require(TileObject.Place(new TileObject { xCoord = 20, yCoord = 20, type = sample.createTile, style = style, alternate = alternate, random = variant }), "native isolated fixture placement");
                                for (int x = 0; x < data.Width; x++) for (int y = 0; y < data.Height; y++)
                                {
                                    int observed = (int)Call(catalog, "PlacedItem", Main.tile[20 + x, 20 + y]);
                                    Require(observed == itemType, "native placed identity item/style/alternate/random/cell " + itemType + "/" + style + "/" + alternate + "/" + variant + "/" + x + "/" + y + " returned " + observed);
                                    checks++;
                                }
                            }
                        }
                    }
                }
                Main.rand = new Terraria.Utilities.UnifiedRandom(295); var expected = new Terraria.Utilities.UnifiedRandom(295);
                for (int i = 0; i < 50; i++) Call(catalog, "PlacedItem", Main.tile[20, 20]);
                Require(Main.rand.Next() == expected.Next(), "placement identity does not use gameplay RNG");
                Console.WriteLine("PASS: " + checks + " native placed frame identities across variants/alternates/all cells, distinct same-tile items, and no resolver RNG.");
            }
            finally { Main.tile = saved; Main.rand = random; Array.Copy(flags, Main.tileFrameImportant, flags.Length); }
        }
    }
}
