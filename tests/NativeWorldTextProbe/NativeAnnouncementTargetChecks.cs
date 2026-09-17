using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Tile_Entities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeAnnouncementTargetChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static Color TestLight = Color.White;
        internal static void Run(Assembly assembly, object catalog)
        {
            var method = assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeTargetObservation").GetMethod("World", Flags);
            Func<object> observe = () => method.Invoke(null, new object[] { new Vector2(20, 20), catalog, true });
            var lighting = new Harmony("JueMingR.Tests.AnnouncementLighting");
            var getColor = typeof(Lighting).GetMethod("GetColor", new[] { typeof(int), typeof(int) });
            var origin = new Point16(0, 0); TileEntity previous; bool hadPrevious = TileEntity.ByPosition.TryGetValue(origin, out previous);
            try
            {
                // Only lighting is controlled; production object recognition uses
                // real .8 tiles, items and the actual tile-entity dictionary.
                lighting.Patch(getColor, prefix: new HarmonyMethod(typeof(NativeAnnouncementTargetChecks).GetMethod(nameof(Lit), Flags)));
                NativeAnnouncementObjectChecks.Run(assembly, catalog);
                NativePlacementIdentityChecks.Run(catalog);
                NativeSwitchIdentityChecks.Run(catalog);
                Main.tile[1, 1] = new Tile();
                var air = observe(); Require((bool)Get(air, "EmptyAir") && (bool)Get(air, "Literal"), "visible air retains its separate cooldown kind and complete notice");
                string phrase = ((List<string>)Get(air, "Entries"))[0];
                Require(phrase != "这里是空气" && phrase != "这里看不清，无法确认目标", "air uses the accepted Legacy phrase list");
                foreach (var entry in new[] { Tuple.Create(5, 0, "Tree"), Tuple.Create(72, 0, "GiantMushroom"), Tuple.Create(26, 0, "DemonAltar"), Tuple.Create(26, 54, "CrimsonAltar") })
                {
                    var tile = new Tile { type = (ushort)entry.Item1, frameX = (short)entry.Item2 }; tile.active(true); Main.tile[1, 1] = tile;
                    var value = observe();
                    string expected = Terraria.Localization.Language.GetTextValue("MapObject." + entry.Item3);
                    string mapName = (string)method.DeclaringType.GetMethod("MapName", Flags).Invoke(null, new object[] { tile, 1, 1 });
                    Require(mapName == expected, "actual native map label/style " + entry.Item1 + "/" + entry.Item2);
                    if (entry.Item1 != 26) Require((int)Get(value, "ItemType") == 0 && ((List<string>)Get(value, "Entries"))[0] == expected, "tree/mushroom fallback names the object without inventing an item identity");
                }
                int[] herbItems = { 313, 314, 315, 316, 317, 318, 2358 };
                foreach (int stage in new[] { 82, 83, 84 }) for (int herb = 0; herb < 7; herb++)
                {
                    var tile = new Tile { type = (ushort)stage, frameX = (short)(herb * 18) }; tile.active(true); Main.tile[1, 1] = tile;
                    var value = observe();
                    Require((int)Get(value, "ItemType") == herbItems[herb] && ((List<string>)Get(value, "Entries"))[0] == Lang.GetItemNameValue(herbItems[herb]), "real growing/mature/blooming herb identifies plant, not seed or generic object");
                }
                var frame = new TEItemFrame { Position = origin, item = new Item() }; frame.item.SetDefaults(4); frame.item.stack = 1;
                TileEntity.ByPosition[origin] = frame;
                for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) { var tile = new Tile { type = 395, frameX = (short)(x * 18), frameY = (short)(y * 18) }; tile.active(true); Main.tile[x, y] = tile; }
                object framed = observe(); Require((int)Get(framed, "ItemType") == 4 && (int)Get(framed, "Quantity") == 1 && GetOptional(framed, "Placement") == null, "frame contents resolve immediately from the normalized real tile entity");
                frame.item.TurnToAir(); Require((int)Get(observe(), "ItemType") != 4, "empty frame does not reuse former display contents");
                TileEntity.ByPosition.Remove(origin); Require((int)Get(observe(), "ItemType") != 4, "missing client entity cannot invent display contents");
                var hidden = new Tile { type = 395, frameX = 18, frameY = 18 }; hidden.active(true); hidden.invisibleBlock(true); Main.tile[1, 1] = hidden;
                var invisible = observe(); Require(((List<string>)Get(invisible, "Entries"))[0] == "这里看不见东西" && (bool)Get(invisible, "Literal") && !(bool)Get(invisible, "EmptyAir"), "hidden target preserves the exact Legacy notice without gaining the air cooldown");
                Console.WriteLine("PASS: actual 21 herb stage/style identities, display-frame content/empty/missing/hidden boundaries and Legacy notices.");
            }
            finally { lighting.Unpatch(getColor, HarmonyPatchType.All, lighting.Id); if (hadPrevious) TileEntity.ByPosition[origin] = previous; else TileEntity.ByPosition.Remove(origin); Main.tile[1, 1] = new Tile(); }
        }
        private static bool Lit(ref Color __result) { __result = TestLight; return false; }
    }
}
