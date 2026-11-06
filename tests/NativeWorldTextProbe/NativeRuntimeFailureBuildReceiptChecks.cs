using System;
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
        internal static void RunCommonFailureBuildReceipt(object context)
        {
            var host = Get(context, "Blueprint"); var tools = Get(context, "Tools"); var input = Get(context, "Input");
            NativeToolExecutionChecks.Initialize(context, null);
            Framing.Initialize(); Main.SetupTileMerge(); TorchID.Initialize();
            var lighting = Lighting.Mode; Lighting.Mode = Terraria.Graphics.Light.LightMode.Color;
            int mode = Main.netMode; var connection = Netplay.Connection; var sections = Main.sectionManager;
            var client = Main.clientPlayer; var buffer = NetMessage.buffer[256]; var world = Main.ActiveWorldFileData;
            var patch = new Harmony("JueMingR.Tests.RuntimeFailureBuildReceipt");
            var send = typeof(NetMessage).GetMethod("SendData", F);
            patch.Patch(send, postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(RecordBuildPacket)));
            foreach (string name in new[] { "HandleSpecialEvent", "HandleMining", "HandleRunning" })
                patch.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name, F), prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SkipOutlet)));
            try
            {
                NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(host, "ControlsEnabled"); });
                using (NativeBlueprintWorldMatchChecks.EmptyWorld(host, 200, 160))
                {
                    // This existing full ordinary case proves original17/79
                    // requests and both original34 outcomes before terminal
                    // isolation. Only the final socket outlet is intercepted.
                    NetworkProgress(context, host, tools, input);
                    Netplay.Connection = new RemoteServer { PendingTermination = true, State = 10 };
                    NetMessage.buffer[256] = new MessageBuffer(); Main.clientPlayer = new Player(); Main.netMode = 1;
                    Main.sectionManager = new WorldSections(1, 1); Main.sectionManager.SetAllSectionsLoaded(); Call(context, "UpdateRuntime");
                    var p = Reset(context, host, tools, input); p.ResetFallDamage();
                    p.inventory[12].SetDefaults(ItemID.Chest); p.inventory[12].stack = 2;
                    var data = TileObjectData.GetTileData(p.inventory[12].createTile, p.inventory[12].placeStyle);
                    Require(WorldGen.PlaceObject(60, 42, p.inventory[12].createTile, true, p.inventory[12].placeStyle),
                        "receipt fixture obtains an independent original complete chest source");
                    var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, (LibraryState)Get(host, "Library"), () => Step(context, input),
                        60 - data.Origin.X, 42 - data.Origin.Y, data.Width, data.Height, "共同故障既有容器回执");
                    Place(context, host, input, entry, 44, 41); buildPackets.Clear(); SetAutomatic(host, input, true);
                    Until(context, input, () => GetOptional(host, "buildContainer") != null && (bool)Get(Get(host, "buildContainer"), "Sent"));
                    var receipt = Get(host, "buildContainer"); var source = p.inventory[12];
                    Require(source.stack == 1 && buildPackets.Any(b => b[2] == 34 && b[3] == 0),
                        "original native chest placement consumed one exact source and serialized its actual34 request");
                    int action = (int)Get(receipt, "Action"), x = (int)Get(receipt, "X"), y = (int)Get(receipt, "Y"), style = (int)Get(receipt, "Style");
                    typeof(NativeRuntimeFailureReturnChecks).GetMethod("CommonFault", F).Invoke(null, new[] { context });
                    Require(ReferenceEquals(receipt, GetOptional(host, "buildContainer")) && !(bool)Get(receipt, "Resolved"),
                        "common failure preserves the genuinely sent original container receipt");
                    ReceiveBuildReply(action, x + 20, y, style, -1);
                    Require(ReferenceEquals(receipt, GetOptional(host, "buildContainer")), "terminal receipt still rejects an actual decoder reply for another coordinate");
                    var socket = Netplay.Connection.Socket; var replacement = new RemoteServer { PendingTermination = true, State = 10 };
                    Require(!ReferenceEquals(socket, replacement.Socket), "receipt negative control has a genuinely different native socket identity");
                    try
                    {
                        Netplay.Connection.Socket = replacement.Socket;
                        ReceiveBuildReply(action, x, y, style, -1);
                        Require(ReferenceEquals(receipt, GetOptional(host, "buildContainer")) && !(bool)Get(receipt, "Resolved"),
                            "a different socket cannot consume the old receipt before the outer Runtime observes replacement");
                    }
                    finally { Netplay.Connection.Socket = socket; replacement.Socket.Close(); }
                    try
                    {
                        Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(Terraria.Program.SavePath, "receipt-successor.wld"), false) { UniqueId = Guid.NewGuid() };
                        ReceiveBuildReply(action, x, y, style, -1);
                        Require(ReferenceEquals(receipt, GetOptional(host, "buildContainer")) && !(bool)Get(receipt, "Resolved"),
                            "a different world cannot consume the old receipt before the outer Runtime observes replacement");
                    }
                    finally { Main.ActiveWorldFileData = world; }
                    long candidates = (long)Get(host, "BuildCandidateChecks"), uses = (long)Get(host, "BuildUsesStarted");
                    ReceiveBuildReply(action, x, y, style, -1);
                    Require(GetOptional(host, "buildContainer") == null && (bool)Get(receipt, "Resolved"),
                        "same-identity terminal cleanup consumes the exact already-sent receipt through the original34 decoder");
                    for (int i = 0; i < 8; i++) Step(context, input);
                    Require(ReferenceEquals(p.inventory[12], source) && source.stack == 1 && (long)Get(host, "BuildCandidateChecks") == candidates &&
                        (long)Get(host, "BuildUsesStarted") == uses && GetOptional(host, "buildContainer") == null,
                        "the old authoritative rejection neither refunds its real source nor starts a new request or build candidate");
                    Console.WriteLine("PASS common container receipt original request/34 decoder, exact old identity, wrong socket/world refusal and ordinary17/79/34 regression; final socket outlet intercepted, server/gameplay NOT_RUN.");
                }
            }
            finally
            {
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                Main.netMode = mode; Netplay.Connection = connection; Main.sectionManager = sections; Main.clientPlayer = client; NetMessage.buffer[256] = buffer;
                Main.ActiveWorldFileData = world; Lighting.Clear(); Lighting.Mode = lighting; Call(context, "UpdateRuntime");
            }
        }
    }
}
