using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.IO;
using Terraria.Net.Sockets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Isolated mode=1 and actual .8 consumers establish client ownership, not
    // a real server's acceptance or a Host & Play/remote synchronization pass.
    internal static class NativeJourneyMultiplayerChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Cpu(object context)
        {
            object journey = Get(context, "Journey");
            int mode = Main.netMode; WorldFileData world = Main.ActiveWorldFileData;
            byte[] originalWorld = Capture(w => CreativePowerManager.Instance.SaveToWorld(w));
            object originalOverride = typeof(Main).GetField("_gameModeDifficultyOverride", Flags).GetValue(null);
            var time = CreativePowerManager.Instance.GetPower<CreativePowers.ModifyTimeRate>();
            object originalTarget = Shared("_currentTargetValue").GetValue(time), originalPending = Shared("_needsToCommitChange").GetValue(time);
            try
            {
                Main.netMode = 1; Main.ActiveWorldFileData = new WorldFileData(); Call(context, "UpdateRuntime");
                Require((bool)Get(journey, "CanEnable"), "connected world descriptor needs no local world file path or write access");
                Type type = context.GetType().Assembly.GetType("JueMingR.TerrariaHost.Journey.JourneyPowers", true);
                var difficulty = CreativePowerManager.Instance.GetPower<CreativePowers.DifficultySliderPower>();
                Receive(difficulty, .8f);
                Receive(time, .3f);
                object borrowed = Activator.CreateInstance(type, Flags, null,
                    new object[] { Main.LocalPlayer, Main.ActivePlayerFileData, Main.ActiveWorldFileData }, null);
                Require(GetOptional(borrowed, "WorldBytes") == null, "client borrows no persistent server-world stream");
                Call(borrowed, "InitializeNeutral");
                Require(Cache(difficulty) == .8f, "connected initialization preserves the received difficulty rather than writing a single-player neutral value");
                Pending(time, .9f);
                Receive(time, .6f); Receive(difficulty, .7f);
                Call(borrowed, "Restore");
                Require(Cache(time) == .6f && Cache(difficulty) == .7f && !(bool)Shared("_needsToCommitChange").GetValue(time) &&
                    (float)Shared("_currentTargetValue").GetValue(time) == .6f,
                    "closing preserves later native server data and cancels the borrowed pending target without a new packet");
                Pending(time, .4f);
                borrowed = Activator.CreateInstance(type, Flags, null,
                    new object[] { Main.LocalPlayer, Main.ActivePlayerFileData, Main.ActiveWorldFileData }, null);
                Receive(time, .5f); Call(borrowed, "Restore");
                Require((bool)Shared("_needsToCommitChange").GetValue(time) && (float)Shared("_currentTargetValue").GetValue(time) == .4f && Cache(time) == .5f,
                    "an unchanged native intent predating activation is not claimed by the borrowed window");
                Shared("_needsToCommitChange").SetValue(time, false);
            }
            finally
            {
                Main.netMode = mode; Main.ActiveWorldFileData = world; Call(context, "UpdateRuntime");
                RestoreWorld(originalWorld); Shared("_currentTargetValue").SetValue(time, originalTarget); Shared("_needsToCommitChange").SetValue(time, originalPending);
                typeof(Main).GetField("_gameModeDifficultyOverride", Flags).SetValue(null, originalOverride);
            }
            Console.WriteLine("PASS JOURNEY CLIENT CPU pathless remote descriptor, unchanged received difficulty, later shared packets, borrowed-only pending cancellation; no live multiplayer claim.");
        }

        internal static void Menu(object context)
        {
            object journey = Get(context, "Journey");
            int mode = Main.netMode, connection = Netplay.Connection.State;
            int gameMode = Main.GameMode;
            WorldFileData world = Main.ActiveWorldFileData; PlayerFileData playerFile = Main.ActivePlayerFileData;
            var socket = Netplay.Connection.Socket;
            var time = CreativePowerManager.Instance.GetPower<CreativePowers.ModifyTimeRate>();
            var god = CreativePowerManager.Instance.GetPower<CreativePowers.GodmodePower>();
            var range = CreativePowerManager.Instance.GetPower<CreativePowers.FarPlacementRangePower>();
            bool godBefore = god.IsEnabledForPlayer(Main.myPlayer);
            bool rangeBefore = range.IsEnabledForPlayer(Main.myPlayer), creativeGodBefore = Main.LocalPlayer.creativeGodMode, ssc = Main.ServerSideCharacter;
            byte[] originalWorld = Capture(w => CreativePowerManager.Instance.SaveToWorld(w));
            object originalOverride = typeof(Main).GetField("_gameModeDifficultyOverride", Flags).GetValue(null);
            object originalTarget = Shared("_currentTargetValue").GetValue(time), originalPending = Shared("_needsToCommitChange").GetValue(time);
            Terraria.Net.Sockets.ISocket replacement = null;
            try
            {
                Main.netMode = 1; Netplay.Connection.State = 10;
                Main.ActiveWorldFileData = new WorldFileData(); Call(context, "UpdateRuntime");
                Main.ServerSideCharacter = true;
                Require(!(bool)Call(journey, "SetEnabled", true) && GetOptional(journey, "Persistence") == null, "SSC still refuses before any borrowed state");
                Main.ServerSideCharacter = false;
                Main.ActivePlayerFileData = new PlayerFileData("isolated-cloud-identity.plr", true) { Player = Main.LocalPlayer };
                Require(!(bool)Call(journey, "SetEnabled", true) && GetOptional(journey, "Persistence") == null, "connected local player cloud provider remains protected");
                Main.ActivePlayerFileData = playerFile;
                Main.GameMode = 0;
                var difficulty = CreativePowerManager.Instance.GetPower<CreativePowers.DifficultySliderPower>();
                Receive(difficulty, 0f);
                Require((bool)Call(journey, "SetEnabled", true) && (bool)Get(journey, "IsMenuOpen"), "connected owner reaches actual original ToggleCreativeMenu and confirms its window");
                Require(Main.Difficulty == 1f && Cache(difficulty) == 0f, "opening an ordinary connected world never activates vanilla's dormant reset x0.5 difficulty");
                Receive(difficulty, .8f); typeof(Main).GetMethod("UpdateCreativeGameModeOverride", Flags).Invoke(null, null);
                Require(Main.Difficulty == 1f && Cache(difficulty) == .8f, "ordinary connected world keeps its real mode after a later native difficulty packet");
                Main.GameMode = 3; typeof(Main).GetMethod("UpdateCreativeGameModeOverride", Flags).Invoke(null, null);
                Require(Main.Difficulty == difficulty.StrengthMultiplierToGiveNPCs, "a real Journey world still consumes its received native difficulty");
                Main.GameMode = 0; typeof(Main).GetMethod("UpdateCreativeGameModeOverride", Flags).Invoke(null, null);
                object baseline = Get(journey, "Persistence");
                Call(context, "UpdateRuntime");
                Require((bool)Get(journey, "IsActive") && (bool)Call(journey, "SetEnabled", true) && ReferenceEquals(baseline, Get(journey, "Persistence")),
                    "connected continuous qualification and idempotent reopen retain the same borrowed baseline");
                god.SetEnabledState(Main.myPlayer, !godBefore); Pending(time, .9f); Receive(time, .6f);
                byte[] current = Capture(w => CreativePowerManager.Instance.SaveToWorld(w));
                Type hooks = context.GetType().Assembly.GetType("JueMingR.TerrariaHost.Journey.JourneyHooks", true);
                // Exercise the actual installed save scope and creative writer
                // without pretending a remote world has a writable local file.
                object[] begin = { null }; hooks.GetMethod("BeginWorldSave", Flags).Invoke(null, begin);
                try { Require(Capture(w => CreativePowerManager.Instance.SaveToWorld(w)).SequenceEqual(current), "connected world serialization uses current server powers, never a frozen old client baseline"); }
                finally { hooks.GetMethod("EndSave", Flags).Invoke(null, new[] { null, begin[0] }); }
                byte[] persistentPlayer = (byte[])Get(baseline, "PlayerBytes");
                object[] playerBegin = { playerFile, null }; hooks.GetMethod("BeginPlayerSave", Flags).Invoke(null, playerBegin);
                Require((bool)Call(journey, "SetEnabled", false), "connected explicit close returns borrowed personal state");
                try { Require(Capture(w => CreativePowerManager.Instance.SaveToPlayer(Main.LocalPlayer, w)).SequenceEqual(persistentPlayer), "already-entered connected player save retains immutable personal bytes across close"); }
                finally { hooks.GetMethod("EndSave", Flags).Invoke(null, new[] { null, playerBegin[1] }); }
                Require(god.IsEnabledForPlayer(Main.myPlayer) == godBefore && Cache(time) == .6f && !(bool)Shared("_needsToCommitChange").GetValue(time),
                    "connected close returns local personal powers while retaining newer shared server state");
                Require((bool)Call(journey, "SetEnabled", true), "connected owner can begin a new explicit lifetime");
                Pending(time, .95f); Receive(time, .7f);
                Netplay.Connection.Socket = replacement = new TcpSocket(); Call(context, "UpdateRuntime");
                Require(!(bool)Get(journey, "IsActive") && GetOptional(journey, "Persistence") == null && Cache(time) == .7f &&
                    !Main.CreativeMenu.Enabled && !(bool)Shared("_needsToCommitChange").GetValue(time),
                    "same player/world with a new socket closes the old window and drops its pending edit without replaying powers");
                if (Main.CreativeMenu.Enabled) Main.CreativeMenu.ToggleMenu();
                Require((bool)Call(journey, "SetEnabled", true), "a new connection requires and permits a fresh explicit enable");
                Receive(time, .4f); Main.netMode = 0; Call(context, "UpdateRuntime");
                Require(!(bool)Get(journey, "IsActive") && GetOptional(journey, "Persistence") == null && Cache(time) == .4f,
                    "mode transition with reused objects never restores the preceding connection's state");
            }
            finally
            {
                Call(journey, "SetEnabled", false);
                if (Main.CreativeMenu.Enabled) Main.CreativeMenu.ToggleMenu();
                Main.ServerSideCharacter = ssc; Main.netMode = mode; Main.GameMode = gameMode; Netplay.Connection.State = connection;
                Netplay.Connection.Socket = socket; Main.ActiveWorldFileData = world; Main.ActivePlayerFileData = playerFile;
                if (replacement != null) replacement.Close();
                god.SetEnabledState(Main.myPlayer, godBefore); range.SetEnabledState(Main.myPlayer, rangeBefore);
                Main.LocalPlayer.creativeGodMode = creativeGodBefore; Call(context, "UpdateRuntime");
                RestoreWorld(originalWorld); Shared("_currentTargetValue").SetValue(time, originalTarget); Shared("_needsToCommitChange").SetValue(time, originalPending);
                typeof(Main).GetField("_gameModeDifficultyOverride", Flags).SetValue(null, originalOverride);
            }
            Console.WriteLine("PASS JOURNEY CLIENT native menu, continuous qualification/reopen, concrete safety gates, current world serialization, in-flight personal view, close and socket/mode retirement; server acceptance remains unverified.");
        }

        private static byte[] Capture(Action<BinaryWriter> write)
        { using (var stream = new MemoryStream()) { using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) write(writer); return stream.ToArray(); } }
        private static float Cache(CreativePowers.ASharedSliderPower power) { return (float)Get(power, "_sliderCurrentValueCache"); }
        private static FieldInfo Shared(string name) { return typeof(CreativePowers.ASharedSliderPower).GetField(name, Flags); }
        private static void Pending(CreativePowers.ASharedSliderPower power, float target)
        { Shared("_currentTargetValue").SetValue(power, target); Shared("_needsToCommitChange").SetValue(power, true); }
        private static void RestoreWorld(byte[] bytes)
        { using (var reader = new BinaryReader(new MemoryStream(bytes, false))) CreativePowerManager.Instance.LoadFromWorld(reader, 326); }
        private static void Receive(CreativePowers.ASharedSliderPower power, float value)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) writer.Write(value);
                stream.Position = 0; using (var reader = new BinaryReader(stream)) power.DeserializeNetMessage(reader, 0);
            }
        }
    }
}
