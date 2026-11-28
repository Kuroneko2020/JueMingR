using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Runtime;
using Terraria;
using Terraria.Net;

namespace JueMingR.TerrariaHost.World
{
    // One owner per client, independent of the currently selected player, AI or chat window.
    internal sealed partial class WorldMemory : IRuntimeFeature
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly string root;
        private readonly List<Session> retiring = new List<Session>();
        private WorldTileSource tileSource;
        private Session live;
        private bool started, failed, hooksUnavailable;
        private long epoch;
        internal bool Ready => live != null && tileSource.Current;
        internal bool DocumentsLoaded => live?.Saved != null || live?.PlaceFailure != null;
        internal string DocumentIssue => live?.PlaceFailure;
        public bool Enabled => !failed;
        internal long Epoch => epoch;

        private sealed partial class Session
        {
            internal string Key, DiskStamp;
            internal Guid World;
            internal int Width, Height;
            internal bool CompleteSource;
            internal Task<string> DiskRead;
            internal DocumentWorker<PlaceBook> Places;
            internal DocumentWorker<MaterialBook> Index;
        }

        internal WorldMemory(string gameDirectory)
        {
            root = Path.Combine(Path.GetFullPath(gameDirectory), "JueMingRData", "world-memory");
            WorldMemoryHooks.Attach(this);
        }

        public void OnSessionStarted() { started = true; }
        public void OnSessionEnded() { started = false; Retire(); }
        public void FailClosed() { failed = true; OnSessionEnded(); }
        internal void MarkHooksUnavailable() { hooksUnavailable = true; }

        public void Update(ulong updateTick)
        {
            if (retiring.Count != 0) retiring.RemoveAll(session => session.Places.IsFinished && session.Index.IsFinished);
            if (!started || failed) return;
            if (live != null && (!tileSource.Current || Main.gameMenu)) Retire();
            if (live == null && !TryOpen()) return;
            PumpPlaces(live);
            PumpIndex(live, updateTick);
        }

        private bool TryOpen()
        {
            var world = Main.ActiveWorldFileData;
            if (Main.gameMenu || WorldGen.generatingWorld || WorldGen.isGeneratingOrLoadingWorld || world == null || world.UniqueId == Guid.Empty ||
                Main.tile == null || Main.maxTilesX <= 0 || Main.maxTilesY <= 0 || Main.maxTilesX > 8400 || Main.maxTilesY > 2400)
                return false;
            string origin;
            bool complete = Main.netMode == 0;
            if (complete)
            {
                if (String.IsNullOrWhiteSpace(world.Path)) return false;
                origin = (world.IsCloudSave ? "cloud:" : "local:") + world.Path.Replace('/', '\\').ToUpperInvariant();
            }
            else
            {
                var client = Netplay.Connection;
                if (Main.netMode != 1 || client?.State != 10 || Main.sectionManager == null || !client.IsConnected()) return false;
                var remote = client.Socket?.GetRemoteAddress();
                string address = remote is TcpAddress tcp ? tcp.ToString() : remote?.GetIdentifier();
                if (String.IsNullOrWhiteSpace(address)) return false;
                origin = "server:" + address;
            }
            string key = world.UniqueId.ToString("N") + "-" + Digest(Utf8.GetBytes(origin)).Substring(0, 24);
            if (retiring.Exists(session => session.Key == key)) return false;
            string directory = Path.Combine(root, key);
            var next = new Session { Key = key, World = world.UniqueId, Width = Main.maxTilesX, Height = Main.maxTilesY,
                CompleteSource = complete, DiskRead = complete ? WorldMemoryHooks.LoadedStamp(world) : null };
            tileSource = WorldTileSource.Capture();
            epoch++;
            next.Places = new DocumentWorker<PlaceBook>(new AtomicFileDocument(Path.Combine(directory, "world.md"), 4 * 1024 * 1024, true),
                bytes => DecodePlaces(bytes, key, next.Width, next.Height), EncodePlaces, new PlaceBook(key));
            next.Index = new DocumentWorker<MaterialBook>(new AtomicFileDocument(Path.Combine(directory, "materials-v1.bin"), 32 * 1024 * 1024, true),
                bytes => DecodeMaterials(bytes, key, next.Width, next.Height), EncodeMaterials, new MaterialBook(key));
            StartIndex(next);
            live = next;
            return true;
        }

        private void Retire()
        {
            if (live == null) return;
            var previous = live;
            live = null;
            epoch++;
            foreach (var waiter in previous.PlaceReceipts)
                waiter.Complete(WorldResult("unconfirmed", "世界已退出，最后一次世界文档修订仍由原档案结算。"));
            previous.PlaceReceipts.Clear();
            var corrections = previous.Corrections.Values.ToArray();
            var queued = previous.Queued;
            previous.Places.BeginStop(corrections.Length == 0 && queued == null ? (Func<PlaceBook, PlaceBook>)null : current =>
            {
                var entries = new Dictionary<Guid, Place>((queued ?? current).Entries);
                foreach (var correction in corrections)
                    if (entries.TryGetValue(correction.Id, out var old)) entries[correction.Id] = WithFact(old, correction);
                return new PlaceBook(current.Key, entries);
            });
            FinishIndex(previous);
            retiring.Add(previous);
        }

        internal static string Digest(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(bytes));
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        private static string Decimal(int number) => number.ToString(CultureInfo.InvariantCulture);
    }
}
