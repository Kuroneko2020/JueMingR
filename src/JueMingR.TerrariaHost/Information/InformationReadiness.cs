using System;
using Terraria;

namespace JueMingR.TerrariaHost.Information
{
    // Native world/connection lifetime starts before R's first active Session.
    // Keeping these few receipt flags while display is off avoids discarding
    // initial sync. Neither feature toggles nor OnSessionStarted reset them.
    internal sealed class InformationReadiness
    {
        private readonly object gate = new object();
        private object connection, questPlayer, loadedWorld;
        private int mode = -1;
        private long epoch;
        private bool clearing, infection, quest, today, loadedQuest;
        internal bool Installed { get; set; }
        internal string Failure { get; set; }
        internal struct Receipt
        {
            internal InformationReadiness Owner;
            internal object Connection, Player;
            internal long Epoch;
            internal int Mode, Message;
        }
        internal struct State
        {
            internal long Epoch;
            internal bool Infection, Quest, Today, Available;
        }
        private void Sync()
        {
            object next = Main.netMode == 1 ? Netplay.Connection : null;
            if (mode == Main.netMode && ReferenceEquals(connection, next)) return;
            mode = Main.netMode; connection = next; Retire();
        }
        private void Retire()
        { epoch++; infection = quest = today = loadedQuest = false; loadedWorld = questPlayer = null; }
        internal Receipt BeginWorld()
        {
            lock (gate)
            {
                Sync();
                return new Receipt { Owner = this, Connection = connection, Epoch = epoch, Mode = mode, Player = Main.LocalPlayer };
            }
        }
        internal Receipt BeginMessage(MessageBuffer source, int start, int length)
        {
            if (!Installed || source == null || Main.netMode != 1 || start < 0 || length < 1 || source.readBuffer == null || start > source.readBuffer.Length - length ||
                NetMessage.buffer == null || NetMessage.buffer.Length <= 256 || !ReferenceEquals(source, NetMessage.buffer[256]) || source.whoAmI != 256) return default(Receipt);
            int id = source.readBuffer[start];
            if (id != 57 && id != 74 || length != (id == 57 ? 4 : 3)) return default(Receipt);
            var receipt = BeginWorld(); receipt.Message = id; return receipt;
        }
        private bool Matches(Receipt receipt)
        {
            Sync();
            return Installed && ReferenceEquals(receipt.Owner, this) && receipt.Epoch == epoch && receipt.Mode == mode &&
                ReferenceEquals(receipt.Connection, connection) && !clearing;
        }
        internal void AppliedMessage(Receipt receipt, int id)
        {
            lock (gate)
            {
                if (!Matches(receipt) || mode != 1 || receipt.Message != id) return;
                if (id == 57) infection = true;
                else if (id == 74) { quest = today = true; questPlayer = receipt.Player; }
            }
        }
        internal void BeginClear()
        { lock (gate) { Sync(); Retire(); clearing = true; } }
        internal void EndClear() { lock (gate) { clearing = false; } }
        internal void PublishedCounts(Receipt receipt, int x)
        { lock (gate) { if (x == 0 && Matches(receipt) && mode != 1) infection = true; } }
        internal void LoadedFlags(Receipt receipt, int version)
        {
            lock (gate)
            {
                if (!Matches(receipt) || mode != 0 || version < 101) return;
                loadedQuest = true; loadedWorld = Main.ActiveWorldFileData;
            }
        }
        internal void SwappedQuest(Receipt receipt)
        {
            lock (gate)
            {
                if (!Matches(receipt) || mode != 0) return;
                quest = today = true; questPlayer = Main.LocalPlayer;
            }
        }
        internal State Snapshot()
        {
            lock (gate)
            {
                Sync();
                if (mode == 0 && loadedQuest && !clearing && !Main.gameMenu && Main.LocalPlayer != null && Main.LocalPlayer.active && ReferenceEquals(loadedWorld, Main.ActiveWorldFileData))
                { quest = today = true; questPlayer = Main.LocalPlayer; loadedQuest = false; }
                return new State { Epoch = epoch, Available = Installed && !clearing,
                    Infection = Installed && !clearing && infection, Quest = Installed && !clearing && quest,
                    Today = Installed && !clearing && today && ReferenceEquals(questPlayer, Main.LocalPlayer) };
            }
        }
    }
}
