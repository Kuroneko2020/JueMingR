using System;
using System.Collections.Generic;
using System.IO;
using JueMingR.Features.DeathHistory;
using JueMingR.Platform.DeathHistory;
using JueMingR.Platform.Settings;
using JueMingR.Infrastructure.DeathHistory;

namespace JueMingR.ArchitectureTests
{
    internal static class DeathArchiveChecks
    {
        internal static void Check(IList<string> failures)
        {
            try { OrderAndIdentity(); }
            catch (Exception e) { failures.Add("death archive order/identity: " + e.Message); }
            try { FileProtection(); }
            catch (Exception e) { failures.Add("death archive real-file protection: " + e.Message); }
            try { CancelBeforeCommit(); }
            catch (Exception e) { failures.Add("death archive cancellation: " + e.Message); }
        }
        // Catches counting a replay twice, coordinate dedup, and using UTC for
        // the map's recent ordering. These literal expectations precede code.
        private static void OrderAndIdentity()
        {
            var files = new MemoryArchive(); var archive = new DeathArchive(files, new string('a', 64));
            archive.Load(); Require(archive.Count == 0, "missing archive is reliable empty");
            DeathFact a = Fact(30, 1), b = Fact(10, 2), c = Fact(20, 3, false);
            archive.Append(new[] { a, b, c, a });
            Require(archive.Count == 3, "one stable event replay must not count twice; same position is not identity");
            DeathFact[] page = archive.Page(0, 6);
            Require(page.Length == 3 && page[0].EventId == b.EventId && page[1].EventId == c.EventId && page[2].EventId == a.EventId, "details UTC ascending, missing position still counted");
            DeathMarker[] recent = archive.Recent(128);
            Require(recent.Length == 2 && recent[0].EventId == b.EventId && recent[1].EventId == a.EventId, "map occurrence order survives UTC rollback");
            var reopened = new DeathArchive(files, new string('a', 64)); reopened.Load();
            reopened.Append(new[] { a }); Require(reopened.Count == 3, "persisted replay must be idempotent");
            Require(reopened.Page(0, 6)[0].Reason == "same reason", "frozen original reason survives load");
        }
        internal static DeathFact Fact(int seconds, int id, bool position = true)
        {
            var time = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)).AddSeconds(seconds);
            return new DeathFact(DeathEventId.Create(time, new Guid(id, 0, 0, new byte[8])), time.Offset,
                position, 160, 320, "same reason");
        }
        internal static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void CancelBeforeCommit()
        {
            bool cancelled = false; var files = new MemoryArchive();
            var archive = new DeathArchive(files, new string('d', 64), () => cancelled); archive.Load();
            files.PageCreated = bytes => { if (BitConverter.ToInt32(bytes, 8) == 3) cancelled = true; };
            bool failed = false; try { archive.Append(new[] { Fact(1, 1) }); } catch (OperationCanceledException) { failed = true; }
            Require(failed && files.RootWrites == 0 && !archive.CommitUnconfirmed, "cancel after final page forbids starting root commit and is not unknown commit");
        }
        private static void FileProtection()
        {
            string folder = Path.Combine(Path.GetTempPath(), "JueMingR-death-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string data = Path.Combine(folder, "valid");
                using (var files = new FileDeathArchive(data))
                { var archive = new DeathArchive(files, new string('b', 64)); archive.Load(); archive.Append(new[] { Fact(1, 1) }); Require(archive.Count == 1, "real atomic root commit"); }
                using (var files = new FileDeathArchive(data))
                { var archive = new DeathArchive(files, new string('b', 64)); archive.Load(); Require(archive.Page(0, 6)[0].EventId == Fact(1, 1).EventId, "real bytes reopen"); }
                string root = Path.Combine(data, "root.json"); byte[] original = File.ReadAllBytes(root);
                File.WriteAllText(root, System.Text.Encoding.UTF8.GetString(original).Replace("\"version\":1", "\"version\":99"));
                byte[] future = File.ReadAllBytes(root);
                using (var files = new FileDeathArchive(data))
                { var archive = new DeathArchive(files, new string('b', 64)); bool failed = false; try { archive.Load(); } catch (PreferenceFormatException e) { failed = e.Status == PreferenceStatus.UnsupportedVersion; } Require(failed && archive.IsProtected, "future root protected"); }
                Require(Convert.ToBase64String(File.ReadAllBytes(root)) == Convert.ToBase64String(future), "future original not overwritten");
                string orphan = Path.Combine(folder, "orphan"); Directory.CreateDirectory(Path.Combine(orphan, "pages")); File.WriteAllText(Path.Combine(orphan, "pages", "recovery"), "incomplete");
                using (var files = new FileDeathArchive(orphan))
                { var archive = new DeathArchive(files, new string('b', 64)); bool failed = false; try { archive.Load(); } catch { failed = true; } Require(failed && archive.IsProtected, "missing root plus pages is not empty history"); }
            }
            finally { Directory.Delete(folder, true); }
        }
        internal sealed class MemoryArchive : IDeathArchiveFiles
        {
            internal readonly Dictionary<string, byte[]> Pages = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            private byte[] root; private int revision;
            internal Action<byte[]> PageCreated;
            internal int RootWrites;
            internal int PageReads;
            internal volatile bool FailReads;
            public PreferenceReadResult ReadRoot() { return new PreferenceReadResult(root == null ? PreferenceReadStatus.Missing : PreferenceReadStatus.Loaded, root, revision.ToString(), null); }
            public PreferenceWriteResult WriteRoot(string expected, byte[] bytes)
            { RootWrites++; if (expected != revision.ToString()) return new PreferenceWriteResult(PreferenceWriteStatus.Conflict, null, "changed", false, true); root = (byte[])bytes.Clone(); revision++; return new PreferenceWriteResult(PreferenceWriteStatus.Saved, revision.ToString(), null); }
            public byte[] ReadPage(string id) { PageReads++; if (FailReads || !Pages.ContainsKey(id)) throw new IOException("missing page"); return (byte[])Pages[id].Clone(); }
            public string CreatePage(byte[] bytes) { string id; using (var sha = System.Security.Cryptography.SHA256.Create()) id = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); Pages[id] = (byte[])bytes.Clone(); PageCreated?.Invoke(bytes); return id; }
            public void Dispose() { }
        }
    }
}
