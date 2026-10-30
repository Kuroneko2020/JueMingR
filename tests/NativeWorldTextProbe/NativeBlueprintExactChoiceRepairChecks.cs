using System;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static void ConstructionExactChoiceRepair(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
            long unknown = (long)Get(host, "BuildUnknownResults");
            p.inventory[12].SetDefaults(ItemID.GeyserTrap); p.inventory[12].stack = 2; var material = p.inventory[12];
            for (int x = 60; x < 62; x++) Require(WorldGen.PlaceTile(x, 38, TileID.Stone, true, true), "source has real ceiling anchors");
            Require(WorldGen.PlaceObject(60, 39, material.createTile, true, material.placeStyle), "native selector places ceiling GeyserTrap without floor");
            Require(Main.tile[60, 39].frameX == 72, "the source is native encoded top style2, independent of capture alternate numbering");
            var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, (LibraryState)Get(host, "Library"),
                () => Step(context, input), 60, 39, 2, 1, "精确顶挂对象预检");
            for (int x = 44; x < 46; x++)
            {
                Require(WorldGen.PlaceTile(x, 38, TileID.Stone, true, true) && WorldGen.PlaceTile(x, 40, TileID.Stone, true, true), "destination has both genuine floor and ceiling anchors");
            }
            TileObject chosen;
            // onlyCheck builds a preview and leaves the out value Empty in
            // vanilla. The complete query fills its chosen without PlaceIt.
            bool first = TileObject.CanPlace(44, 39, material.createTile, material.placeStyle, p.direction, out chosen);
            Console.WriteLine("EXACT_CHOICE dualSupport originalCanPlace=" + first + " chosen=" + chosen.type + "/" + chosen.style + "/" + chosen.alternate + " at=" + chosen.xCoord + "," + chosen.yCoord);
            Require(first && chosen.type == material.createTile && chosen.xCoord == 44 && chosen.yCoord == 39 && chosen.alternate == 0,
                "real CanPlace chooses root floor before legal ceiling despite equal footprint");
            Place(context, host, input, entry, 44, 39);
            p.inventory[13].SetDefaults(ItemID.StoneBlock); p.inventory[13].stack = 2;
            Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone }), 40, 42);
            long starts = (long)Get(host, "BuildUsesStarted"), checks = (long)Get(host, "BuildCandidateChecks");
            SetAutomatic(host, input, true);
            Until(context, input, () => Main.tile[40, 42].active() && (long)Get(host, "BuildCandidateChecks") > checks);
            for (int n = 0; n < 270; n++) Step(context, input);
            Require((long)Get(host, "BuildUsesStarted") == starts + 1 && material.stack == 2 && p.inventory[13].stack == 1 &&
                !Main.tile[44, 39].active() && !Main.tile[45, 39].active() && (long)Get(host, "BuildUnknownResults") == unknown && Scene(host).Length == 1,
                "stable wrong chosen causes zero object starts/spend/Unknown across the retry interval while an independent target finishes");
            WorldGen.KillTile(44, 40, false, false, true); WorldGen.KillTile(45, 40, false, false, true);
            bool resumed = TileObject.CanPlace(44, 39, material.createTile, material.placeStyle, p.direction, out chosen);
            Console.WriteLine("EXACT_CHOICE removedFloor originalCanPlace=" + resumed + " alternate=" + chosen.alternate + " direction=" + p.direction +
                " chosen=" + chosen.type + "/" + chosen.style + " at=" + chosen.xCoord + "," + chosen.yCoord +
                " supports=" + Main.tile[44, 38].active() + "/" + Main.tile[45, 38].active() + "/" + Main.tile[44, 40].active() + "/" + Main.tile[45, 40].active() +
                " body=" + Main.tile[44, 39].active() + "/" + Main.tile[45, 39].active() + " material=" + material.type + ":" + material.stack + ":" + material.createTile + ":" + material.placeStyle);
            Require(resumed && chosen.type == material.createTile && chosen.xCoord == 44 && chosen.yCoord == 39 && chosen.alternate == 2,
                "only real support removal naturally selects the correct ceiling alternate");
            Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
            Require(Main.tile[44, 39].frameX == 72 && Main.tile[45, 39].frameX == 90 && material.stack == 1 &&
                (long)Get(host, "BuildUsesStarted") == starts + 2 && (long)Get(host, "BuildUnknownResults") == unknown,
                "restored exact chosen reaches original PlaceIt and complete Match with one real object consumption");
            // Existing observed CanPlace/PlaceIt controls include the solid to
            // platform anchor alias: alternate IDs differ, encoded body does not.
            WrongNativeFurnitureChoice(context, host, tools, input);
            Console.WriteLine("PASS BlueprintExactChoiceRepair nativeTopFrame=72 dualSupportFloor=0 refusedObjectStarts=0 recoveredAlternate=2 consumed=1 completeMatch=true aliasPreserved=true");
        }
    }
}
