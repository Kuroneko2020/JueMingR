using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using JueMingR.Features.WorldObjectText;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldObjectText;

namespace JueMingR.ArchitectureTests
{
    internal static class OpenedFailureChecks
    {
        internal static void Run()
        {
            foreach (string mode in new[] { "future", "corrupt", "read-failed", "unconfirmed" }) ProtectOriginal(mode);
            PairNoticeIsolation();
            foreach (string mode in new[] { "committed-unconfirmed", "conflict", "committed-throw", "healthy", "retry" }) ReentryProtection(mode);
        }
        private static void ReentryProtection(string mode)
        {
            string root=Root(), pair=new string('d',64), other=new string('e',64), path=Path.Combine(root,pair+".json");
            var codec=new OpenedPositionCodec();File.WriteAllBytes(path,codec.Encode(pair,new OpenedPositionIndex(new[]{WorldObject.PositionKey(2,3)})));
            int totalWrites=0,acquired=0;ReentryStorage first=null;
            var owner=new OpenedPositionHistory(key=>
            {
                var storage=new ReentryStorage(new AtomicFileDocument(Path.Combine(root,key+".json"),OpenedPositionCodec.MaximumBytes,true),
                    key==pair && Interlocked.Increment(ref acquired)==1?mode:"healthy",()=>Interlocked.Increment(ref totalWrites));
                if(key==pair && first==null)first=storage;return storage;
            });
            bool protect=mode=="committed-unconfirmed" || mode=="conflict" || mode=="committed-throw";
            try
            {
                owner.BeginSession(1);owner.UsePair(pair);Until(()=>{owner.Poll();return owner.Loaded;});owner.Opened(5,6);
                Until(()=>{owner.Poll();return first.Writes>0 && (protect?owner.Error!=null:owner.Status==PreferenceStatus.Saved);});
                Require(owner.CommitUnconfirmed==(mode.StartsWith("committed",StringComparison.Ordinal)),"only actual unknown commits retain unconfirmed meaning");
                owner.TakeBackgroundFailure(); // Acknowledging a notice cannot unlock this pair.
                owner.EndSession();Until(()=>Volatile.Read(ref first.Disposed)!=0);
                byte[] main=File.ReadAllBytes(path),backup=File.Exists(path+".bak")?File.ReadAllBytes(path+".bak"):null;
                int before=Volatile.Read(ref totalWrites);
                owner.BeginSession(2);owner.UsePair(pair);Until(()=>{owner.Poll();return owner.Loaded;});
                Require(owner.CommitUnconfirmed==(mode.StartsWith("committed",StringComparison.Ordinal)),"reentry preserves unknown-commit status after handle disposal");
                owner.Opened(7,8);Require(owner.Contains(WorldObject.PositionKey(7,8)),"protected pair retains session-only facts");
                owner.EndSession();Until(()=>Volatile.Read(ref acquired)>=2);
                // Switch to a healthy independent pair and wait for a real save;
                // serial worker ordering also settles the released second lease.
                owner.BeginSession(3);owner.UsePair(other);owner.Opened(9,10);
                Until(()=>{owner.Poll();return owner.Status==PreferenceStatus.Saved && File.Exists(Path.Combine(root,other+".json"));});
                Require(Volatile.Read(ref totalWrites)==before+(protect?1:2),"only protected reentry rejects a second write; healthy/retry pairs and independent pair still save");
                if(protect)
                {
                    Require(File.ReadAllBytes(path).SequenceEqual(main),"same-pair reentry preserves the post-fault main bytes");
                    Require(backup==null?!File.Exists(path+".bak"):File.ReadAllBytes(path+".bak").SequenceEqual(backup),"same-pair reentry never rotates recovery backup");
                    owner.BeginSession(4);owner.UsePair(pair);Until(()=>{owner.Poll();return owner.Loaded;});
                    Require(owner.Error!=null,"another pair saving cannot unlock the protected pair");
                }
                Console.WriteLine("Opened reentry "+mode+": real storage/replace, lease disposed, writes="+totalWrites+"; owner-result fault simulation, not OS fault reproduction");
            }
            finally{owner.Stop(5000);}
        }
        private sealed class ReentryStorage : IPreferenceStorage
        {
            private readonly IPreferenceStorage inner;private readonly string mode;private readonly Action written;internal int Writes,Disposed;
            internal ReentryStorage(IPreferenceStorage inner,string mode,Action written){this.inner=inner;this.mode=mode;this.written=written;}
            public PreferenceReadResult Read(){return inner.Read();}
            public PreferenceWriteResult Write(string identity,byte[] bytes)
            {
                int count=Interlocked.Increment(ref Writes);written();
                if(mode=="conflict")return new PreferenceWriteResult(PreferenceWriteStatus.Conflict,identity,"reentry-conflict",isProtected:true);
                if(mode=="retry" && count==1)return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure,identity,"reentry-retry");
                PreferenceWriteResult result=inner.Write(identity,bytes);Require(result.Status==PreferenceWriteStatus.Saved,"fault wrapper first performs an actual confirmed AtomicFileDocument write");
                if(mode=="committed-throw")throw new IOException("simulated lost owner result after real replace");
                return mode=="committed-unconfirmed"?new PreferenceWriteResult(PreferenceWriteStatus.IoFailure,result.Identity,"reentry-unconfirmed",true,true):result;
            }
            public void Dispose(){inner.Dispose();Interlocked.Exchange(ref Disposed,1);}
        }
        private static void ProtectOriginal(string mode)
        {
            string root = Root(), pair = new string('a', 64), path = Path.Combine(root, "record.json");
            byte[] valid = new OpenedPositionCodec().Encode(pair, new OpenedPositionIndex(new[] { WorldObject.PositionKey(2, 3) }));
            byte[] original = mode == "future" ? Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(valid).Replace("\"version\":1", "\"version\":9")) : mode == "corrupt" ? Encoding.UTF8.GetBytes("{broken") : valid;
            byte[] backup = { 1, 88, 255 }; File.WriteAllBytes(path, original); File.WriteAllBytes(path + ".bak", backup);
            Storage storage = null; var owner = new OpenedPositionHistory(key => storage = new Storage(new AtomicFileDocument(path, OpenedPositionCodec.MaximumBytes, true), mode));
            try
            {
                owner.BeginSession(1); owner.Opened(5, 6); owner.UsePair(pair);
                Until(() => { owner.Poll(); return owner.Loaded && owner.Error != null; });
                Require(owner.Status == (mode == "future" ? PreferenceStatus.UnsupportedVersion : mode == "corrupt" ? PreferenceStatus.Invalid : PreferenceStatus.IoFailure), "owner distinguishes protected " + mode);
                Require(owner.Contains(WorldObject.PositionKey(5, 6)) && owner.CommitUnconfirmed == (mode == "unconfirmed"), "memory fact and uncertain commit stay distinct");
                for (int i = 0; i < 10; i++) { owner.Opened(5, 6); owner.Poll(); }
                Require(owner.Stop(5000), "protected owner stops its worker");
                Require(storage.Writes == (mode == "unconfirmed" ? 1 : 0), "protected source cannot turn into an empty/retried write");
                Require(File.ReadAllBytes(path).SequenceEqual(original) && File.ReadAllBytes(path + ".bak").SequenceEqual(backup), "real original and prior backup bytes survive " + mode);
            }
            finally { owner.Stop(5000); }
        }
        private static void PairNoticeIsolation()
        {
            string root = Root(), a = new string('b', 64), b = new string('c', 64); Storage first = null, second = null;
            var owner = new OpenedPositionHistory(key => key == a ? first = new Storage(new AtomicFileDocument(Path.Combine(root, a + ".json"), OpenedPositionCodec.MaximumBytes, true), "A-failed") : second = new Storage(new AtomicFileDocument(Path.Combine(root, b + ".json"), OpenedPositionCodec.MaximumBytes, true), "B-retry"));
            try
            {
                owner.BeginSession(1); owner.UsePair(a); owner.Opened(10, 10);
                Until(() => { owner.Poll(); return first != null && first.Writes >= 3 && owner.Status == PreferenceStatus.IoFailure; });
                owner.EndSession(); owner.BeginSession(2); owner.UsePair(b); owner.Opened(20, 20);
                Until(() => { owner.Poll(); return second != null && second.Writes >= 2 && owner.Status == PreferenceStatus.Saved; });
                Require(owner.TakeBackgroundFailure() == "test-A-failed", "B recovery cannot erase the unconsumed failed A notice");
                Require(owner.TakeBackgroundFailure() == null && owner.Error == null, "repeated unchanged failure is not republished and active B is healthy");
                Require(!File.Exists(Path.Combine(root, a + ".json")) && new OpenedPositionCodec().Decode(File.ReadAllBytes(Path.Combine(root, b + ".json")), b).Count == 1, "failed old pair never contaminates new pair file");
            }
            finally { owner.Stop(5000); }
        }
        private static string Root() { string value = Path.Combine(Path.GetTempPath(), "JueMingR-opened-failure-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(value); return value; }
        private static void Until(Func<bool> condition) { var time = System.Diagnostics.Stopwatch.StartNew(); while (!condition()) { if (time.ElapsedMilliseconds > 5000) throw new TimeoutException("opened failure case"); Thread.Sleep(1); } }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private sealed class Storage : IPreferenceStorage
        {
            private readonly IPreferenceStorage inner; private readonly string mode; internal int Writes;
            internal Storage(IPreferenceStorage inner, string mode) { this.inner = inner; this.mode = mode; }
            public PreferenceReadResult Read() { return mode == "read-failed" ? new PreferenceReadResult(PreferenceReadStatus.IoFailure, null, null, "test-read-failed") : inner.Read(); }
            public PreferenceWriteResult Write(string identity, byte[] bytes)
            {
                int count = Interlocked.Increment(ref Writes);
                if (mode == "unconfirmed") return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, identity, "test-unconfirmed", true, true);
                if (mode == "A-failed" || mode == "B-retry" && count == 1) return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, identity, "test-" + mode);
                return inner.Write(identity, bytes);
            }
            public void Dispose() { inner.Dispose(); }
        }
    }
}
