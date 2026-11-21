using System;
using System.IO;
using System.Net.Sockets;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.UI;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionFurnitureChecks
    {
        private static readonly int[] Tiles = { 125, 287, 354, 377, 464, 621, 699 };
        private static readonly int[] Effects = { 29, 93, 150, 159, 348, 192, 366 };
        private static readonly int[] Widths = { 2, 2, 3, 3, 5, 2, 4 }, Heights = { 2, 2, 3, 2, 4, 2, 4 };

        internal static void Run(object host, object first, object second)
        {
            var body = (Player)Get(first, "Body"); var other = (Player)Get(second, "Body");
            var profile = (CompanionProfile)Get(first, "Profile");
            var oldPosition = body.position; var oldOther = other.position; var oldAccessory = body.armor[3];
            var oldBuffs = (int[])body.buffType.Clone(); var oldTimes = (int[])body.buffTime.Clone();
            bool inventory = Main.playerInventory, otherTarget = Main.HasInteractableObjectThatIsNotATile;
            var radial = (WiresUI.WiresRadial)AccessTools.Field(typeof(WiresUI), "radial").GetValue(null);
            bool wire = radial.active;
            var oldTiles = new Tile[28, 18];
            for (int x = 0; x < 28; x++) for (int y = 0; y < 18; y++) oldTiles[x, y] = Main.tile[32 + x, 34 + y];
            Action resetArea = () => { for (int x = 0; x < 28; x++) for (int y = 0; y < 18; y++) Main.tile[32 + x, 34 + y] = new Tile(); };
            Action step = () =>
            {
                NativeQuickItemChecks.BeginWorldStep();
                body.position = new Vector2(40 * 16, 40 * 16);
                body.velocity = Vector2.Zero;
                body.Update(body.whoAmI);
                Require(!(bool)Get(first, "Faulted"), "real furniture actor update succeeds: " + Get(first, "Status"));
            };
            try
            {
                resetArea(); Place(0, 42, 40);
                other.position = body.position = new Vector2(40 * 16, 40 * 16);
                step();
                Require(GetOptional(first, "Furniture") == null && body.FindBuffIndex(Effects[0]) < 0, "disabled furniture performs no discovery or native use");
                var enabled = profile.Behavior;
                enabled.FurnitureEnabled = true; enabled.Follow = enabled.AttackEnabled = enabled.AllowNurse = false;
                Require((bool)Call(host, "SetBehavior", profile.Id, enabled), "the actor owns a separately enabled furniture setting");
                Main.HasInteractableObjectThatIsNotATile = true; radial.active = true;
                for (int kind = 0; kind < Tiles.Length; kind++)
                {
                    resetArea(); Place(kind, 42, 40); Main.playerInventory = kind % 2 == 1;
                    step();
                    int index = body.FindBuffIndex(Effects[kind]);
                    Require(index >= 0 && body.buffTime[index] == (kind == 5 ? 7200 : 108000),
                        "real AfterBody uses the complete furniture at native duration " + Tiles[kind] + " " + GetOptional(host, "Feedback"));
                    Require(Main.playerInventory == (kind % 2 == 1) && radial.active && Main.HasInteractableObjectThatIsNotATile &&
                        !body.tileInteractAttempted && body.releaseUseTile && Main.LocalPlayer.FindBuffIndex(Effects[kind]) < 0 &&
                        other.FindBuffIndex(Effects[kind]) < 0 && GetOptional(second, "Furniture") == null,
                        "only the enabled actor gains the effect and the owner's open UI survives");
                }
                var runner = Get(first, "Furniture");
                long reads = (long)Get(runner, "TileReads"), calls = (long)Get(runner, "NativeCalls");
                for (int i = 0; i < 40; i++) step();
                Require((long)Get(runner, "TileReads") == reads && (long)Get(runner, "NativeCalls") == calls,
                    "seven active effects suppress scans and repeat uses while their timers count down");
                const int effect = BuffID.Clairvoyance;
                body.ClearBuff(effect); resetArea(); Place(0, 42, 40); Main.tile[43, 41].active(false); step();
                Require(body.FindBuffIndex(effect) < 0, "a missing furniture part prevents renewal of an expired effect");
                resetArea();
                var reach = (TileReachCheckSettings)Call(host, "ActorTileReach", body);
                int edge = reach.GetTileRegion(body).Right;
                Place(0, edge, 40);
                int previousRange = Player.tileRangeX;
                try
                {
                    Player.tileRangeX = 20;
                    for (int i = 0; i < 35; i++) step();
                    Require(body.FindBuffIndex(effect) < 0, "the human extended hand cannot authorize an adjacent out-of-range companion tile");
                    body.armor[3] = new Item(ItemID.ExtendoGrip); step();
                    Require(body.equippedAnyTileRangeAcc && body.FindBuffIndex(effect) >= 0,
                        "the same exact furniture becomes usable through the actor's actual equipped Extendo Grip");
                }
                finally { Player.tileRangeX = previousRange; }
                enabled.FurnitureEnabled = false; Call(host, "SetBehavior", profile.Id, enabled); step();
                Require(GetOptional(first, "Furniture") == null && (long)Get(runner, "NativeCalls") == calls + 1,
                    "turning furniture off retires this actor's bounded scanner");
                Console.WriteLine("PASS seven native companion furniture durations, independent body/UI/range, missing part, stopping and resuming.");
            }
            finally
            {
                for (int x = 0; x < 28; x++) for (int y = 0; y < 18; y++) Main.tile[32 + x, 34 + y] = oldTiles[x, y];
                body.position = oldPosition; other.position = oldOther; body.armor[3] = oldAccessory;
                Array.Copy(oldBuffs, body.buffType, oldBuffs.Length); Array.Copy(oldTimes, body.buffTime, oldTimes.Length);
                Main.playerInventory = inventory; Main.HasInteractableObjectThatIsNotATile = otherTarget; radial.active = wire;
                Call(host, "SetBehavior", profile.Id, profile.Behavior);
            }
        }

        internal static byte[] SendToServer(object host, object actor, TcpClient peer)
        {
            var body = (Player)Get(actor, "Body");
            var profile = (CompanionProfile)Get(actor, "Profile");
            var savedSections = Main.sectionManager;
            bool oldRelease = body.releaseUseTile;
            var center = body.Center.ToTileCoordinates();
            var previousTiles = new Tile[2, 2];
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) previousTiles[x, y] = Main.tile[center.X + x, center.Y + y];
            try
            {
                Place(0, center.X, center.Y);
                var behavior = profile.Behavior; behavior.FurnitureEnabled = true;
                Require((bool)Call(host, "SetBehavior", profile.Id, behavior), "enable furniture on this actual TCP actor");
                // This socket fixture drives packet handling without ticking
                // the full world. Use the same native character phase that
                // precedes AfterBody, including its lock and UI conditions.
                Call(body, "UpdateReleaseUseTile");
                Require(body.releaseUseTile, "the native player's update admits a new tile interaction at this handoff");
                Main.sectionManager = new WorldSections(2, 2);
                Call(host, "UpdateCompanionFurniture", actor);
                var scanner = Get(actor, "Furniture");
                Require((long)Get(scanner,"TileReads") > 0 && (long)Get(scanner,"NativeCalls") == 0 &&
                    body.FindBuffIndex(BuffID.Clairvoyance) < 0, "unreceived sections are never furniture entrances");
                Main.sectionManager = savedSections;
                Require(Main.sectionManager.TileLoaded(center.X, center.Y) &&
                    Terraria.ObjectData.TileObjectData.GetTileData(Main.tile[center.X, center.Y])?.Width == 2,
                    "the admitted connection knows this complete native crystal ball");
                for (int i = 0; i < 32; i++) NativeQuickItemChecks.BeginWorldStep();
                Call(host, "UpdateCompanionFurniture", actor);
                Require((long)Get(scanner,"NativeCalls") == 1 && body.FindBuffIndex(BuffID.Clairvoyance) >= 0,
                    "received terrain grants the native TCP actor's own furniture buff");
                Call(host, "Publish", actor);
                using (var reader = new BinaryReader(peer.GetStream(), System.Text.Encoding.UTF8, leaveOpen: true))
                    while (true)
                    {
                        int length = reader.ReadUInt16();
                        var packet = reader.ReadBytes(length - 2);
                        Require(packet.Length == length - 2 && packet.Length > 0, "complete original furniture TCP frame");
                        if (packet[0] != MessageID.PlayerBuffs) continue;
                        Require(packet[1] == body.whoAmI, "buff packet identifies the real companion connection");
                        for (int i = 2; i + 1 < packet.Length; i += 2)
                            if (BitConverter.ToUInt16(packet, i) == BuffID.Clairvoyance)
                            {
                                var framed = new byte[length]; framed[0] = (byte)length; framed[1] = (byte)(length >> 8);
                                Buffer.BlockCopy(packet, 0, framed, 2, packet.Length);
                                return framed;
                            }
                    }
            }
            finally
            {
                for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) Main.tile[center.X + x, center.Y + y] = previousTiles[x, y];
                Main.sectionManager = savedSections;
                body.releaseUseTile = oldRelease;
                Call(host, "SetBehavior", profile.Id, profile.Behavior);
            }
        }

        private static void Place(int kind, int left, int top)
        {
            for (int x = 0; x < Widths[kind]; x++) for (int y = 0; y < Heights[kind]; y++)
            {
                var tile = new Tile { type = (ushort)Tiles[kind], frameX = (short)(18 * x), frameY = (short)(18 * y) };
                tile.active(true); Main.tile[left + x, top + y] = tile;
            }
        }
    }
}
