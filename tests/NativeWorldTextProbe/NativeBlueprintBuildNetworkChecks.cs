using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static readonly List<byte[]> buildPackets = new List<byte[]>();
        private static void RecordBuildPacket(int __0)
        {
            if (Main.netMode != 1 || __0 != 17 && __0 != 34 && __0 != 79) return;
            var bytes = NetMessage.buffer[256].writeBuffer; int length = BitConverter.ToUInt16(bytes, 0);
            Require(length >= 3 && length <= bytes.Length && bytes[2] == __0, "original client packet serialized before the fixture's final socket block");
            buildPackets.Add(bytes.Take(length).ToArray());
        }
        private static void NetworkProgress(object context, object host, object tools, object input)
        {
            int mode = Main.netMode; var connection = Netplay.Connection; var sections = Main.sectionManager;
            var client = Main.clientPlayer; var buffer = NetMessage.buffer[256]; int acceptedIndex = -1;
            var patch = new Harmony("JueMingR.Tests.BlueprintBuildPackets");
            var send = typeof(NetMessage).GetMethod("SendData", F);
            patch.Patch(send, postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(RecordBuildPacket)));
            try
            {
                // The existing NativeQuickItem fixture blocks only SendPacket.
                // SendData and GetData below are the original .8 serializer and
                // receiver. This is not an executed server or socket session.
                Netplay.Connection = new RemoteServer { PendingTermination = true, State = 10 };
                NetMessage.buffer[256] = new MessageBuffer(); Main.clientPlayer = new Player(); Main.netMode = 1;
                Main.sectionManager = new WorldSections(1, 1); Main.sectionManager.SetAllSectionsLoaded(); Call(context, "UpdateRuntime");
                var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 2;
                var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone });
                Place(context, host, input, entry, 44, 42); buildPackets.Clear(); SetAutomatic(host, input, true);
                Until(context, input, () => Scene(host).Length == 0);
                Require(Main.tile[44, 42].active() && p.inventory[12].stack == 1 && buildPackets.Any(b => b[2] == 17) && GetOptional(host, "buildContainer") == null,
                    "ordinary client tile uses original packet17, real local body and current match; no invented success ACK gate");
                buildPackets.Clear(); Furniture(context, host, tools, input, ItemID.WoodenChair, 1);
                Require(buildPackets.Any(b => b[2] == 79) && GetOptional(host, "buildContainer") == null,
                    "ordinary complete furniture uses original packet79 and does not borrow container confirmation");

                p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.Chest); p.inventory[12].stack = 2;
                var data = TileObjectData.GetTileData(p.inventory[12].createTile, p.inventory[12].placeStyle);
                Require(WorldGen.PlaceObject(60, 42, p.inventory[12].createTile, true, p.inventory[12].placeStyle), "independent source chest uses original complete object placement");
                var library = (LibraryState)Get(host, "Library");
                entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, library, () => Step(context, input),
                    60 - data.Origin.X, 42 - data.Origin.Y, data.Width, data.Height, "真实容器回执");
                Place(context, host, input, entry, 44, 41); buildPackets.Clear(); SetAutomatic(host, input, true);
                Until(context, input, () => GetOptional(host, "buildContainer") != null && (bool)Get(Get(host, "buildContainer"), "Sent"));
                var first = Get(host, "buildContainer"); long uses = (long)Get(host, "BuildUsesStarted");
                Require(Main.tile[44, 41].active() && p.inventory[12].stack == 1 && buildPackets.Any(b => b[2] == 34 && b[3] == 0),
                    "real native chest post-hook emits packet34 and consumes one item while carrying its exact request identity");
                for (int i = 0; i < 80; i++) Step(context, input);
                Require(Scene(host).Length == 1 && ReferenceEquals(first, Get(host, "buildContainer")) && uses == (long)Get(host, "BuildUsesStarted"),
                    "locally complete predicted chest neither retires nor repeats before the explicit original reply");
                Call(host, "RemoveProjection"); Call(host, "RemoveAllProjections");
                Require(Scene(host).Length == 0 && ReferenceEquals(first, GetOptional(host, "buildContainer")), "real RemoveAll retains only the already-sent original reply responsibility");
                Place(context, host, input, entry, 44, 41);
                for (int i = 0; i < 80; i++) Step(context, input);
                Require(Scene(host).Length == 1 && ReferenceEquals(first, GetOptional(host, "buildContainer")) && uses == (long)Get(host, "BuildUsesStarted"),
                    "a new same-position instance neither consumes nor retires from the old request's predicted body");
                ReceiveBuildReply(0, 90, 40, 0, -1);
                Require(ReferenceEquals(first, Get(host, "buildContainer")), "a real receiver packet for another coordinate cannot end this chest request");
                ReceiveBuildReply((int)Get(first, "Action"), (int)Get(first, "X"), (int)Get(first, "Y"), (int)Get(first, "Style"), -1);
                SetAutomatic(host, input, false);
                Require(GetOptional(host, "buildContainer") == null && !Main.tile[44, 41].active() && Scene(host).Length == 1 && p.inventory[12].stack == 1,
                    "actual packet34 rejection removes the native predicted chest, keeps its diagram and never fabricates a material refund; receipt=" +
                    (GetOptional(host, "buildContainer") != null) + " active=" + Main.tile[44, 41].active() + " scene=" + Scene(host).Length + " stack=" + p.inventory[12].stack +
                    " chestIndex=" + Chest.FindChest(44, 41) + " canDestroy=" + Chest.CanDestroyChest(44, 41) + " canKill=" + WorldGen.CanKillTile(44, 41) +
                    " destroyObject=" + WorldGen.destroyObject);

                SetAutomatic(host, input, true);
                Until(context, input, () => GetOptional(host, "buildContainer") != null && (bool)Get(Get(host, "buildContainer"), "Sent"));
                var second = Get(host, "buildContainer");
                Call(host, "ReceiveBuildContainer", first, true, null);
                Require(!ReferenceEquals(first, second) && ReferenceEquals(second, Get(host, "buildContainer")), "a delayed callback retains its old receipt and cannot release the successor");
                acceptedIndex = Enumerable.Range(0, Main.chest.Length).First(i => Main.chest[i] == null);
                ReceiveBuildReply((int)Get(second, "Action"), (int)Get(second, "X"), (int)Get(second, "Y"), (int)Get(second, "Style"), acceptedIndex);
                Until(context, input, () => Scene(host).Length == 0);
                Require(Main.tile[44, 41].active() && Main.chest[acceptedIndex] != null && p.inventory[12].IsAir && GetOptional(host, "buildContainer") == null,
                    "actual accepted original reply initializes the chest before full current Match retires the exact diagram");
                Console.WriteLine("PASS BlueprintBuild .8 client packet17/79/34 serialization, real packet34 rejection/acceptance receiver, precise pending retirement and delayed receipt isolation; server execution/socket/friend-host-client NOT_RUN.");
            }
            finally
            {
                NativeQuickItemChecks.Sample(input, Array.Empty<Microsoft.Xna.Framework.Input.Keys>()); Call(host, "SetAutoBuild", false); Call(host, "ClearProjection");
                if (acceptedIndex >= 0 && Main.chest[acceptedIndex] != null) Chest.DestroyChestDirect(44, 41, acceptedIndex);
                Main.netMode = mode; Netplay.Connection = connection; Main.sectionManager = sections; Main.clientPlayer = client; NetMessage.buffer[256] = buffer;
                patch.Unpatch(send, HarmonyPatchType.All, patch.Id); Call(context, "UpdateRuntime");
            }
        }
        private static void ReceiveBuildReply(int action, int x, int y, int style, int index)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                { writer.Write((byte)34); writer.Write((byte)action); writer.Write((short)x); writer.Write((short)y); writer.Write((short)style); writer.Write((short)index); }
                byte[] payload = stream.ToArray(); var receiver = new MessageBuffer { whoAmI = 256 };
                Array.Copy(payload, receiver.readBuffer, payload.Length); receiver.ResetReader(); int kind;
                receiver.GetData(0, payload.Length, out kind); Require(kind == 34, "original GetData dispatches the actual container payload");
            }
        }
    }
}
