using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            try { VersionedText(); }
            catch (Exception e) { failures.Add("death text compatibility: " + e.Message); }
        }
        private static void VersionedText()
        {
            string original = "原版完整报丧文案\uD800", direct = "死于摔落\uDC00", cause;
            byte[] bytes = TextPage(2, original, direct);
            Require(ReadText(bytes, out cause) == original && cause == direct, "independent v2 fixture retains both exact UTF16 fields");
            Require(ReadText(TextPage(1, original, null), out cause) == original && cause == null, "independent v1 text falls back to original");
            foreach (byte[] invalid in new[] { TextPage(3, original, direct), TextPage(2, original, " "), TextPage(2, original, new string('x', 1025)), TextPage(2, new string('x', 262145), null), new ArraySegment<byte>(bytes, 0, bytes.Length - 1).ToArray(), new ArraySegment<byte>(bytes, 0, 13).ToArray() })
            {
                bool rejected = false; try { ReadText(invalid, out cause); } catch (PreferenceFormatException) { rejected = true; } catch (EndOfStreamException) { rejected = true; }
                Require(rejected, "future, oversized, blank and truncated text must be rejected");
            }
            byte[] trailing = new byte[bytes.Length + 1]; Array.Copy(bytes, trailing, bytes.Length);
            bool badTail = false; try { ReadText(trailing, out cause); } catch (PreferenceFormatException) { badTail = true; } Require(badTail, "trailing bytes rejected");

            // A complete v1 archive, encoded without the production codec. New
            // commits may refer to this old immutable text and new v2 text.
            var files = new MemoryArchive(); var old = Fact(1, 1); string pair = new string('a', 64);
            string text = files.CreatePage(TextPage(1, old.Reason, null));
            string header = files.CreatePage(Page(1, 2, writer =>
            { Literal(writer, old.EventId); Literal(writer, text); Literal(writer, ""); Literal(writer, ""); writer.Write(1L); writer.Write((short)480); writer.Write(true); writer.Write(160f); writer.Write(320f); }));
            string index = files.CreatePage(Page(1, 3, writer =>
            { writer.Write(0); writer.Write(1); Literal(writer, old.EventId); Literal(writer, header); writer.Write(1L); }));
            files.WriteRoot("0", System.Text.Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.Deaths\",\"version\":1,\"pair\":\"" + pair + "\",\"count\":\"1\",\"tree\":\"" + index + "\",\"last\":\"" + header + "\",\"position\":\"" + header + "\"}"));
            var archive = new DeathArchive(files, pair); archive.Load();
            Require(archive.Find(old.EventId).DisplayCause == old.Reason, "old archive display uses original");
            var next = Fact(2, 2); var current = new DeathFact(next.EventId, next.Time.Offset, true, 160, 320, original, direct);
            archive.Append(new[] { current });
            var reopened = new DeathArchive(files, pair); reopened.Load(); var restored = reopened.Find(current.EventId);
            Require(reopened.Count == 2 && restored.Reason == original && restored.DirectCause == direct && restored.DisplayCause == direct, "mixed archive reopens both text versions");
            Require(Convert.ToBase64String(files.ReadPage(text)) == Convert.ToBase64String(TextPage(1, old.Reason, null)), "old text never rewritten");
            reopened.Append(new[] { current }); Require(reopened.Count == 2, "same direct-cause replay is idempotent");
            bool conflict = false;
            try { reopened.Append(new[] { new DeathFact(current.EventId, current.Time.Offset, true, 160, 320, original, "另一个死因") }); }
            catch (InvalidOperationException) { conflict = true; }
            Require(conflict && reopened.IsProtected && reopened.Count == 2, "same event with changed direct cause protects history");

            var maximum = new DeathFact(Fact(3, 3).EventId, TimeSpan.Zero, true, 16, 16, new string('x', 262144), new string('y', 1024));
            archive.Append(new[] { maximum });
            Require(archive.Find(maximum.EventId).SameSource(maximum), "combined maximum fields fit one bounded page");
            byte[] futureIndex = (byte[])files.Pages[index].Clone(); futureIndex[4] = 2;
            var codec = typeof(DeathArchive).Assembly.GetType("JueMingR.Features.DeathHistory.DeathArchiveCodec", true);
            bool strict = false;
            try { codec.GetMethod("Reader", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(byte[]), typeof(int) }, null).Invoke(null, new object[] { futureIndex, 3 }); }
            catch (System.Reflection.TargetInvocationException e) { strict = e.InnerException is PreferenceFormatException; }
            Require(strict, "index version 2 remains unsupported");
        }
        private static byte[] TextPage(int version, string original, string direct)
        { return Page(version, 1, writer => { writer.Write(original != null); if (original != null) Literal(writer, original); if (version >= 2) { writer.Write(direct != null); if (direct != null) Literal(writer, direct); } }); }
        private static byte[] Page(int version, int kind, Action<BinaryWriter> body)
        { using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream)) { writer.Write(0x4A524448); writer.Write(version); writer.Write(kind); body(writer); writer.Flush(); return stream.ToArray(); } }
        private static void Literal(BinaryWriter writer, string text)
        { writer.Write(text.Length); foreach (char c in text) writer.Write((ushort)c); }
        private static string ReadText(byte[] bytes, out string cause)
        {
            var codec = typeof(DeathArchive).Assembly.GetType("JueMingR.Features.DeathHistory.DeathArchiveCodec", true);
            object[] arguments = { bytes, null }; cause = null;
            try { string text = (string)codec.GetMethod("Text", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(byte[]), typeof(string).MakeByRefType() }, null).Invoke(null, arguments); cause = (string)arguments[1]; return text; }
            catch (System.Reflection.TargetInvocationException e) { throw e.InnerException; }
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
