using System;
using System.Collections;
using System.IO;
using System.Linq;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Infrastructure.Storage;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        // Uses the surrounding original Player.Update construction fixture, but
        // owns fresh template and scene files. Only menu/normal enter retire and
        // rebuild the Host binding; no source reload fabricates the destination.
        private static void ConstructionHoikPersistence(object context, object host, object tools, object input)
        {
            var originalLibrary = (LibraryState)Get(host, "Library");
            var originalHistory = (BlueprintSceneHistory)Get(host, "projectionHistory");
            var originalWorld = Main.ActiveWorldFileData;
            bool originalAutomatic = (bool)Get(host, "AutoBuildEnabled");
            string area = Path.Combine(Terraria.Program.SavePath, "hoik-reentry-" + Guid.NewGuid().ToString("N"));
            var library = new LibraryState(new FileBlueprintLibrary(Path.Combine(area, "templates")));
            BlueprintSceneHistory history = null;
            Action menu = () => { Main.gameMenu = true; Call(context, "UpdateRuntime"); };
            Action enter = () =>
            {
                Main.gameMenu = false;
                Until(context, input, () => (bool)Get(host, "ProjectionPersistenceReady"));
            };
            Func<BlueprintSceneHistory> create = () => new BlueprintSceneHistory(scope => new AtomicFileDocument(Path.Combine(area, "instances", scope, "scene.bin"), Int32.MaxValue, true));
            menu();
            try
            {
                host.GetType().GetField("<Library>k__BackingField", F).SetValue(host, library);
                history = create(); host.GetType().GetField("projectionHistory", F).SetValue(host, history);
                var worldA = new Terraria.IO.WorldFileData(Path.Combine(area, "a.wld"), false) { UniqueId = Guid.NewGuid() };
                var worldB = new Terraria.IO.WorldFileData(Path.Combine(area, "b.wld"), false) { UniqueId = Guid.NewGuid() };
                Main.ActiveWorldFileData = worldA; enter();
                var player = Reset(context, host, tools, input); player.position.X = 42 * 16;
                player.inventory[12].SetDefaults(ItemID.WoodPlatform); player.inventory[12].stack = 2;
                player.inventory[13].SetDefaults(ItemID.StoneBlock); player.inventory[13].stack = 2;
                player.inventory[14].SetDefaults(ItemID.WoodenHammer);
                var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Platforms, FrameY = (short)(player.inventory[12].placeStyle * 18), Slope = 3 });
                object placed = Place(context, host, input, entry, 44, 42);
                long generation = ((BlueprintProjectionIdentity)Get(placed, "Identity")).SessionGeneration;
                SetAutomatic(host, input, true);
                Until(context, input, () => Main.tile[44, 42].active() && Main.tile[44, 42].type == TileID.Stone && ((IDictionary)Get(host, "hoikBases")).Count == 1);
                SetAutomatic(host, input, false);
                Until(context, input, () => !(bool)Get(Get(tools, "Use"), "Active"));
                Until(context, input, () => history.Saved);
                string scope = (string)Get(history, "requestedScope"); string path = Path.Combine(area, "instances", scope, "scene.bin");
                var saved = BlueprintSceneCodec.Decode(File.ReadAllBytes(path), scope, Main.maxTilesX, Main.maxTilesY);
                var pending = saved.Records.Single().HoikPending.Single().Expected;
                Require(saved.SourceVersion == 3 && pending.X == 0 && pending.Y == 0 && pending.TileType == TileID.Stone && pending.Slope == Main.tile[44, 42].slope() &&
                    player.inventory[13].stack == 1 && player.inventory[12].stack == 2, "native confirmed intermediate stone and exact debit are saved as relative pending, before final platform consumption");
                int spentStone = player.inventory[13].stack, unspentPlatform = player.inventory[12].stack;
                menu(); Require(history.Stop(3000), "old pending history owner drains before replacement");
                history = create(); host.GetType().GetField("projectionHistory", F).SetValue(host, history); enter();
                object restored = Scene(host).Single();
                Require(((BlueprintProjectionIdentity)Get(restored, "Identity")).SessionGeneration != generation && ((IDictionary)Get(host, "hoikBases")).Count == 1 &&
                    Main.tile[44, 42].type == TileID.Stone && player.inventory[13].stack == spentStone && player.inventory[12].stack == unspentPlatform,
                    "normal same-world reentry uses a new history/session, restores pending ownership and never refunds prior stone");
                SetAutomatic(host, input, true);
                Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
                Require(Main.tile[44, 42].type == TileID.Platforms && Main.tile[44, 42].slope() == 3 && !Main.tile[44, 42].halfBrick() &&
                    player.inventory[13].stack == spentStone && player.inventory[12].stack == unspentPlatform - 1 && ((IDictionary)Get(host, "hoikBases")).Count == 0,
                    "restored production hoik continues through native hammer/replacement to full Match without a second base charge or inventory refund");
                SetAutomatic(host, input, false);

                // A second genuine intermediate is removed while automatic work
                // is off. Its permission must end even though the world stone stays.
                WorldGen.KillTile(44, 42, false, false, true);
                Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
                Until(context, input, () => Main.tile[44, 42].active() && Main.tile[44, 42].type == TileID.Stone && ((IDictionary)Get(host, "hoikBases")).Count == 1);
                SetAutomatic(host, input, false); Until(context, input, () => !(bool)Get(Get(tools, "Use"), "Active"));
                Call(host, "ClearProjection"); Step(context, input);
                Require(Scene(host).Length == 0 && ((IDictionary)Get(host, "hoikBases")).Count == 0 && Main.tile[44, 42].type == TileID.Stone,
                    "retiring the exact projection withdraws permission without deleting the world intermediate");
                menu(); Main.ActiveWorldFileData = worldB; enter();
                Require(Scene(host).Length == 0 && ((IDictionary)Get(host, "hoikBases")).Count == 0, "different reliable world scope cannot reuse old pending permissions");
                Console.WriteLine("PASS HoikPersistence native Host menu/new-owner/same-world enter: confirmed stone saved/restored, no refund/recharge, final native platform slope3/full Match; removed projection and other world revoke permission. Real process/gameplay exit, multiplayer and FPS NOT_RUN.");
            }
            finally
            {
                Call(host, "SetAutoBuild", false); menu(); if (history != null) Require(history.Stop(3000), "isolated hoik history ends before fixture restore");
                host.GetType().GetField("projectionHistory", F).SetValue(host, originalHistory);
                host.GetType().GetField("<Library>k__BackingField", F).SetValue(host, originalLibrary);
                Main.ActiveWorldFileData = originalWorld; Require(library.Stop(3000), "isolated hoik template worker ends");
                enter(); Call(host, "SetAutoBuild", originalAutomatic);
            }
        }
    }
}
