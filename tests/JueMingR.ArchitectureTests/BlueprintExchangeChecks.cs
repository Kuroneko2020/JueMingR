using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Blueprint;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class BlueprintExchangeChecks
    {
        internal static void Check(IList<string> failures)
        {
            string area = Path.Combine(Path.GetTempPath(), "JueMingR-exchange-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(area);
            try { RoundTrip(area); Corruption(area); MultiBlockAndFlood(area); PreviewCooperation(area); PublishFaults(area); CounterFailure(area); ExportOutcomes(area); MechanicalTargets(area); SlowLane(area); foreach (string mode in new[] { "Read", "Write", "Flush" }) BlockedStreamLane(area, mode); Console.WriteLine("BlueprintExchange: literal byte vector, continuous identity, dedup, reliable files, upper-owner outcomes, naming and blocked external lane passed."); }
            catch (Exception error) { failures.Add("BlueprintExchange " + error + "; retained=" + area); }
        }
        private static void Need(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        private static BlueprintExchangeReceipt Await(LibraryState library)
        {
            var watch = Stopwatch.StartNew(); BlueprintExchangeReceipt value;
            while (watch.ElapsedMilliseconds < 15000) { if (library.TryTakeExchangeReceipt(out value)) return value; Thread.Sleep(1); }
            throw new TimeoutException("exchange receipt");
        }
        private static LibraryReceipt Ordinary(LibraryState state, bool admitted)
        {
            Need(admitted, "ordinary request admitted"); var watch = Stopwatch.StartNew(); LibraryReceipt value;
            while (watch.ElapsedMilliseconds < 15000) { state.Pump(); if (state.TryTakeReceipt(out value)) return value; Thread.Sleep(1); }
            throw new TimeoutException("ordinary receipt while exchange pending");
        }
        private static IBlueprintExchangeTicket Ticket(string kind, Guid id, string path)
        { return (IBlueprintExchangeTicket)typeof(WindowsBlueprintFileTicket).GetMethod(kind, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { id, path }); }
        private static BlueprintExchangeReceipt Import(LibraryState state, string source)
        {
            Guid id = Guid.NewGuid(); Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = id, Kind = BlueprintExchangeKind.Import, Ticket = Ticket("Source", id, source), SupportedTargetVersion = "1.4.5.8" }), "import admitted"); return Await(state);
        }
        private static LibraryState State(string path, string counter, IBlueprintExchangeIo io = null)
        { var result = new LibraryState(new FileBlueprintLibrary(path)); result.ConfigureExchange(io ?? new FileBlueprintExchange(), new AtomicFileDocument(counter, 65536, true), () => new DateTime(2026, 11, 1, 23, 59, 0)); return result; }
        private static void RoundTrip(string area)
        {
            string source = Path.Combine(area, "独立向量.jmbp"), path = Path.Combine(area, "templates"), counter = Path.Combine(area, "counter.json");
            byte[] root, chunk; Guid revision = Guid.NewGuid(); byte[] literal = Literal("图名与文件名独立", revision, 4, out root, out chunk); File.WriteAllBytes(source, literal);
            using (var state = State(path, counter))
            {
                var imported = Import(state, source); Need(imported.Status == BlueprintExchangeStatus.Completed && imported.AssetId != revision, "new local asset rather than capture/revision identity");
                string content = Path.Combine(path, imported.AssetId.ToString("N"), "content");
                Need(File.ReadAllBytes(Path.Combine(content, "root.bin")).SequenceEqual(root), "original root bytes/revision retained");
                string block = Directory.EnumerateFiles(content, "*.bin", SearchOption.AllDirectories).Single(p => File.ReadAllBytes(p).SequenceEqual(chunk)); Need(block != null, "original chunk byte identity");
                var page = Ordinary(state, state.TryList(1, 0)).Page; Need(page.TotalCount == 1 && page.Entries[0].Name == "图名与文件名独立", "normal visible library asset");
                var duplicate = Import(state, source); Need(duplicate.Status == BlueprintExchangeStatus.AlreadyExists && duplicate.AssetId == imported.AssetId, "healthy duplicate selects exact existing asset");
                byte[] renamed = Literal("容器名改变", revision, 4, out root, out chunk); string otherName = Path.Combine(area, "renamed.jmbp"); File.WriteAllBytes(otherName, renamed);
                Need(Import(state, otherName).Status == BlueprintExchangeStatus.AlreadyExists, "container name excluded from content identity");
                var entry = page.Entries[0]; Guid request = Guid.NewGuid();
                Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = request, Kind = BlueprintExchangeKind.PrepareExport }), "prepare naming"); var proposal = Await(state).Proposal;
                Need(proposal.SuggestedLeaf == "JM1101001.jmbp", "literal local date first number");
                Need(!state.TryRename(entry.AssetId, entry.MetadataIdentity, "冲突"), "dialog reservation blocks template mutation");
                Need(Ordinary(state, state.TryList(2, 0)).Page.TotalCount == 1, "reservation keeps list available");
                string target = Path.Combine(area, "导出.jmbp");
                Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = request, Kind = BlueprintExchangeKind.Export, Ticket = Ticket("NewTarget", request, target), SupportedTargetVersion = "1.4.5.8", Proposal = proposal, CounterCandidate = proposal.CounterCandidate, Name = entry.Name, TemplateKey = new BlueprintTemplateKey(entry.AssetId, Guid.ParseExact(entry.ContentRevision, "N"), entry.ContentHash) }), "export admitted");
                var exported = Await(state); Need(exported.Status == BlueprintExchangeStatus.Completed && exported.FileCommitted && exported.CounterCommitted, "file and counter separately reliable");
                Need(File.ReadAllBytes(target).SequenceEqual(literal), "export matches independently constructed entire literal container");
                CounterBytes(counter, 1);
                request = Guid.NewGuid(); Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = request, Kind = BlueprintExchangeKind.PrepareExport }), "second prepare"); Need(Await(state).Proposal.CounterCandidate == 2, "counter advances only after success"); state.CancelExchange(request);
                // Reuse the same LibraryState and real AtomicFileDocument. A
                // rebuild between exports would hide stale initial-read identity.
                string secondDirectory = Path.Combine(area, "second-directory"); Directory.CreateDirectory(secondDirectory);
                for (int sequence = 2; sequence <= 3; sequence++)
                {
                    request = Guid.NewGuid(); Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = request, Kind = BlueprintExchangeKind.PrepareExport }), "continuous proposal " + sequence);
                    proposal = Await(state).Proposal; Need(proposal.CounterCandidate == sequence, "cancel consumes no number; cross-directory counter " + sequence);
                    target = Path.Combine(sequence == 2 ? secondDirectory : area, proposal.SuggestedLeaf);
                    Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = request, Kind = BlueprintExchangeKind.Export, Ticket = Ticket("NewTarget", request, target), SupportedTargetVersion = "1.4.5.8", Proposal = proposal, CounterCandidate = proposal.CounterCandidate, Name = entry.Name, TemplateKey = new BlueprintTemplateKey(entry.AssetId, Guid.Parse(entry.ContentRevision), entry.ContentHash) }), "continuous export " + sequence);
                    exported = Await(state);
                    Need(exported.Status == BlueprintExchangeStatus.Completed && exported.FileCommitted && exported.CounterCommitted && File.ReadAllBytes(target).SequenceEqual(literal), "same-owner complete file and counter commit " + sequence);
                    CounterBytes(counter, sequence);
                }
            }
            using (var state = State(path, counter))
            {
                Guid request = Guid.NewGuid(); Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = request, Kind = BlueprintExchangeKind.PrepareExport }), "restart prepare"); Need(Await(state).Proposal.CounterCandidate == 4, "restart continues all three committed exports"); state.CancelExchange(request);
                // Same claimed hash with a missing body must never hide a valid import.
                string asset = Directory.EnumerateDirectories(path).First(p => Path.GetFileName(p).Length == 32); string body = Path.Combine(asset, "content");
                File.Delete(Directory.EnumerateFiles(body, "*.bin", SearchOption.AllDirectories).First(p => Path.GetFileName(p) != "root.bin" && Path.GetFileName(p) != "index.bin"));
                Need(Import(state, source).Status == BlueprintExchangeStatus.Completed, "broken same-hash candidate cannot dedup");
            }
            Need(BlueprintExportNameProposal.Leaf(new DateTime(2026, 11, 1), 1000) == "JM11011000.jmbp", "999 never wraps");
        }
        private static void CounterBytes(string path, int sequence)
        {
            string expected = "{\"format\":\"JueMingR.BlueprintExchangeState\",\"version\":1,\"localDate\":\"2026-11-01\",\"lastSequence\":" + sequence + "}\n";
            Need(File.ReadAllBytes(path).SequenceEqual(new UTF8Encoding(false, true).GetBytes(expected)), "actual counter file preserves date and committed sequence " + sequence);
        }
        private static void Corruption(string area)
        {
            byte[] root, chunk; byte[] good = Literal("同名", Guid.NewGuid(), 8, out root, out chunk); string path = Path.Combine(area, "bad-templates");
            var cases = new List<byte[]> { good.Take(15).ToArray(), good.Take(good.Length - 1).ToArray(), good.Concat(new byte[] { 0 }).ToArray() };
            var future = (byte[])good.Clone(); future[8] = 2; cases.Add(future);
            var wrong = (byte[])good.Clone(); wrong[wrong.Length - 1] ^= 1; cases.Add(wrong);
            var tooLong = (byte[])good.Clone(); for (int i = 12; i < 16; i++) tooLong[i] = 255; cases.Add(tooLong);
            var invalidUtf8 = (byte[])good.Clone(); invalidUtf8[16] = 255; cases.Add(invalidUtf8);
            using (var state = State(path, Path.Combine(area, "bad-counter.json")))
            {
                int n = 0; foreach (byte[] bytes in cases)
                {
                    string source = Path.Combine(area, "bad" + n++ + ".jmbp"); File.WriteAllBytes(source, bytes);
                    Need(Import(state, source).Status == BlueprintExchangeStatus.Failed, "bad/truncated/future/UTF8 rejected"); Need(File.ReadAllBytes(source).SequenceEqual(bytes), "bad source preserved");
                }
                Need(Ordinary(state, state.TryList(3, 0)).Page.TotalCount == 0, "no partial visible asset");
                // A correct container footer cannot launder invalid chunk enums.
                byte[] invalid = Literal("非法层", Guid.NewGuid(), 8, out root, out chunk, true); string semantic = Path.Combine(area, "semantic.jmbp"); File.WriteAllBytes(semantic, invalid); Need(Import(state, semantic).Status == BlueprintExchangeStatus.Failed, "valid transport digest still validates semantic chunk");
            }
        }
        private static void SlowLane(string area)
        {
            var io = new BlockedIo(); using (var state = State(Path.Combine(area, "slow-templates"), Path.Combine(area, "slow-counter.json"), io))
            {
                Guid request = Guid.NewGuid(); Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = request, Kind = BlueprintExchangeKind.Import, Ticket = new FakeTicket(request), SupportedTargetVersion = "1.4.5.8" }), "blocked IO import accepted");
                Need(io.Submitted.WaitOne(3000), "actual worker reached external lane");
                Need(Ordinary(state, state.TryList(44, 0)).Page.TotalCount == 0, "library list progresses while external read blocked");
                Need(!state.TryBeginStage(new CaptureToken(Guid.NewGuid(), 1, 2, 3)), "independent exchange reservation protects stage writer");
                state.CancelExchange(request); io.Release(); Need(Await(state).Status == BlueprintExchangeStatus.Cancelled, "cancel waits acknowledgement and never reuses its buffer early");
            }
        }
        private static void MultiBlockAndFlood(string area)
        {
            byte[] root, first; Literal("多块", Guid.NewGuid(), 4, out root, out first);
            byte[] second = (byte[])first.Clone(); second[16] = 5;
            Buffer.BlockCopy(BitConverter.GetBytes(2L), 0, root, 24, 8); Buffer.BlockCopy(BitConverter.GetBytes(2), 0, root, 72, 4); Buffer.BlockCopy(BitConverter.GetBytes(2L), 0, root, 80, 8);
            byte[] index = Hash(first).Concat(Hash(second)).ToArray(); Array.Copy(Hash(index), 0, root, 32, 32);
            string path = Path.Combine(area, "multiblock-templates"), source = Path.Combine(area, "multiblock.jmbp"); File.WriteAllBytes(source, Pack("多块", root, index, new[] { first, second }));
            using (var state = State(path, Path.Combine(area, "multiblock-counter.json")))
            {
                Guid request = Guid.NewGuid(); Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = request, Kind = BlueprintExchangeKind.Import, Ticket = Ticket("Source", request, source), SupportedTargetVersion = "1.4.5.8" }), "multiblock admitted");
                var watch = Stopwatch.StartNew(); BlueprintExchangeReceipt result = null; int reads = 0;
                while (watch.ElapsedMilliseconds < 10000)
                {
                    if (state.TryList(1000 + reads, 0)) { Ordinary(state, true); reads++; }
                    if (state.TryTakeExchangeReceipt(out result)) break;
                }
                Need(result != null && result.Status == BlueprintExchangeStatus.Completed && reads > 0, "ordinary command flood cannot starve cooperative exchange");
                Need(Ordinary(state, state.TryList(3000, 0)).Page.Entries[0].ContentCount == 2, "two-block body retains both cells");
                // Recompute every transport/index hash around a cross-block
                // duplicate. Only the global semantic validator can reject it.
                index = Hash(first).Concat(Hash(first)).ToArray(); Array.Copy(Hash(index), 0, root, 32, 32); File.WriteAllBytes(source, Pack("坏顺序", root, index, new[] { first, first }));
                Need(Import(state, source).Status == BlueprintExchangeStatus.Failed, "valid digests cannot launder cross-block duplicate cells");
                Need(Ordinary(state, state.TryList(3001, 0)).Page.TotalCount == 1, "bad multiblock never publishes");
            }
        }
        private static void PreviewCooperation(string area)
        {
            const int blocks = 128, records = 2048; byte[] root, first;
            Literal("large-preview", Guid.NewGuid(), 0, out root, out first);
            byte[][] chunks = Enumerable.Range(0, blocks).Select(y =>
            {
                using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(first, 0, 8); writer.Write(records); writer.Write(0);
                    for (int x = 0; x < records; x++) { writer.Write(x); writer.Write(y + 5); writer.Write(first, 24, first.Length - 24); }
                    return stream.ToArray();
                }
            }).ToArray();
            byte[] index = chunks.SelectMany(Hash).ToArray();
            Buffer.BlockCopy(BitConverter.GetBytes((long)blocks), 0, root, 24, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(records), 0, root, 72, 4); Buffer.BlockCopy(BitConverter.GetBytes(blocks), 0, root, 76, 4);
            Buffer.BlockCopy(BitConverter.GetBytes((long)blocks * records), 0, root, 80, 8); Array.Copy(Hash(index), 0, root, 32, 32);
            string path = Path.Combine(area, "preview-cooperation"), source = Path.Combine(area, "preview-large.jmbp"), small = Path.Combine(area, "preview-small.jmbp");
            File.WriteAllBytes(source, Pack("large-preview", root, index, chunks)); File.WriteAllBytes(small, Literal("small-preview", Guid.NewGuid(), 4, out root, out first));
            using (var state = State(path, Path.Combine(area, "preview-counter.json")))
            {
                Need(Import(state, source).Status == BlueprintExchangeStatus.Completed && Import(state, small).Status == BlueprintExchangeStatus.Completed, "actual multi-block preview fixture imported");
                var entries = Ordinary(state, state.TryList(19, 0)).Page.Entries;
                var large = entries.Single(x => x.Name == "large-preview"); var little = entries.Single(x => x.Name == "small-preview");
                long generation = -77;
                Need(state.TryPreview(generation, large.AssetId, large.ContentHash, 160, 100), "normal preview occupies existing ordinary slot");
                var identity = new BlueprintProjectionIdentity(little.AssetId, Guid.Parse(little.ContentRevision), little.ContentHash, 31, 1);
                var catalog = BlueprintMaterialsChecks.Catalog(); var key = new BlueprintMaterialKey(little.AssetId, false, identity.ContentRevision, little.ContentHash, false, null, catalog.Revision);
                Need(state.RequestProjectionOpen(identity) && state.RequestMaterials(key, catalog), "projection/material request while preview in flight");
                var timer = Stopwatch.StartNew(); bool projected = false; LibraryReceipt early;
                while (timer.ElapsedMilliseconds < 5000)
                {
                    BlueprintProjectionReceipt next; if (state.TryTakeProjectionReceipt(out next)) projected = next.Kind == BlueprintProjectionReceiptKind.Opened;
                    state.Pump(); Need(!state.TryTakeReceipt(out early), "large preview remains unfinished while other lanes earn their own receipts");
                    if (projected && state.Materials(key).Status == BlueprintMaterialStatus.Exact) break;
                    Thread.Sleep(1);
                }
                Need(projected && state.Materials(key).Status == BlueprintMaterialStatus.Exact, "real projection and material consumers advance before the large preview finishes");
                Need(!state.TryList(20, 0), "cooperative execution does not create a second ordinary command slot");
                state.CancelRead(generation); Need(Ordinary(state, true).Status == LibraryStatus.Cancelled, "negative list generation cancels bounded template read");
                var image = Ordinary(state, state.TryPreview(-78, little.AssetId, little.ContentHash, 160, 100));
                Need(image.Status == LibraryStatus.Completed && image.Preview != null && image.Summary.ContentCount == 1, "subsequent original library preview keeps actual image and summary semantics");
                Console.WriteLine("PASS preview cooperation: 262144 real cells/128 immutable blocks, projection and Exact materials before preview receipt, one command slot, cancellation and next original preview.");
            }
        }
        private static void PublishFaults(string area)
        {
            byte[] root, chunk; string source = Path.Combine(area, "fault-source.jmbp"); byte[] bytes = Literal("故障原件", Guid.NewGuid(), 4, out root, out chunk); File.WriteAllBytes(source, bytes);
            foreach (string fault in new[] { "block", "promote", "metadata" })
            {
                string path = Path.Combine(area, "fault-" + fault);
                using (var state = new LibraryState(new FaultFiles(new FileBlueprintLibrary(path), fault)))
                {
                    state.ConfigureExchange(new FileBlueprintExchange(), new AtomicFileDocument(Path.Combine(area, "fault-counter-" + fault + ".json"), 65536, true));
                    var result = Import(state, source);
                    Need(result.Status == (fault == "block" ? BlueprintExchangeStatus.Failed : BlueprintExchangeStatus.CommitUnconfirmed), "accurate precommit/unknown boundary " + fault);
                    Need(File.ReadAllBytes(source).SequenceEqual(bytes), "fault source untouched " + fault);
                    var page = Ordinary(state, state.TryList(500, 0)).Page;
                    if (fault != "metadata") Need(page.TotalCount == 0, "unpublished/unknown body not presented as imported " + fault);
                    if (fault != "block") Need(result.AssetId != Guid.Empty && Directory.Exists(Path.Combine(path, result.AssetId.ToString("N"), "content")), "stable uncertain asset retained " + fault);
                }
            }
        }
        private static void CounterFailure(string area)
        {
            string path = Path.Combine(area, "counter-fault-templates"), source = Path.Combine(area, "counter-fault-source.jmbp"), target = Path.Combine(area, "counter-fault-export.jmbp"); byte[] root, chunk;
            byte[] literal = Literal("计数故障", Guid.NewGuid(), 4, out root, out chunk); File.WriteAllBytes(source, literal);
            using (var state = new LibraryState(new FileBlueprintLibrary(path)))
            {
                state.ConfigureExchange(new FileBlueprintExchange(), new FailingCounter()); Need(Import(state, source).Status == BlueprintExchangeStatus.Completed, "counter failure does not impede import"); var entry = Ordinary(state, state.TryList(1, 0)).Page.Entries[0];
                Guid request = Guid.NewGuid(); Need(state.TryBeginExchange(new BlueprintExchangeRequest { Kind = BlueprintExchangeKind.PrepareExport, RequestId = request }), "counter fault proposal"); var proposal = Await(state).Proposal;
                Need(state.TryBeginExchange(new BlueprintExchangeRequest { Kind = BlueprintExchangeKind.Export, RequestId = request, Ticket = Ticket("NewTarget", request, target), SupportedTargetVersion = "1.4.5.8", TemplateKey = new BlueprintTemplateKey(entry.AssetId, Guid.Parse(entry.ContentRevision), entry.ContentHash), Name = entry.Name, Proposal = proposal, CounterCandidate = proposal.CounterCandidate }), "counter fault export admitted");
                var result = Await(state); Need(result.Status == BlueprintExchangeStatus.Completed && result.FileCommitted && !result.CounterCommitted && File.ReadAllBytes(target).SequenceEqual(literal), "successful file retained with separate failed counter fact");
                request = Guid.NewGuid(); Need(state.TryBeginExchange(new BlueprintExchangeRequest { Kind = BlueprintExchangeKind.PrepareExport, RequestId = request }), "protected next proposal gives explicit receipt"); Need(Await(state).Status == BlueprintExchangeStatus.Failed, "counter failure cannot silently restart from zero");
            }
        }
        private sealed class FailingCounter : IPreferenceStorage
        {
            public PreferenceReadResult Read() { return new PreferenceReadResult(PreferenceReadStatus.Missing, null, "missing", null); }
            public PreferenceWriteResult Write(string expected, byte[] contents) { return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, null, "controlled-counter-failure"); }
            public void Dispose() { }
        }
        private static void ExportOutcomes(string area)
        {
            foreach (string boundary in new[] { "BeforeCommit", "AfterCommit", "Cleanup" })
            {
                string folder = Path.Combine(area, "upper-" + boundary); Directory.CreateDirectory(folder);
                string source = Path.Combine(folder, "source.jmbp"), target = Path.Combine(folder, "target.jmbp"), counter = Path.Combine(folder, "counter.json");
                byte[] root, chunk, old = Encoding.UTF8.GetBytes("原正式文件"); byte[] literal = Literal("上层回执", Guid.NewGuid(), 4, out root, out chunk);
                File.WriteAllBytes(source, literal); File.WriteAllBytes(target, old);
                Guid request = Guid.NewGuid(); string candidate = Path.Combine(folder, ".target.jmbp." + request.ToString("N") + ".tmp"), backup = candidate + ".backup";
                FileStream recoveryLease = null;
                Func<string, FileMode, FileAccess, FileShare, Stream> factory = (path, mode, access, share) => new FileStream(path, mode, access, share);
                Action<string> fault = stage =>
                {
                    if (boundary == stage) throw new IOException("controlled-upper-" + boundary);
                    // Keep the actual verified backup readable but deny delete.
                    // Publication succeeds; only its later cleanup must fail.
                    if (boundary == "Cleanup" && stage == "AfterCommit") recoveryLease = new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.Read);
                };
                var io = (FileBlueprintExchange)Activator.CreateInstance(typeof(FileBlueprintExchange), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { factory, fault }, null);
                try
                {
                    using (var state = State(Path.Combine(folder, "templates"), counter, io))
                    {
                        Need(Import(state, source).Status == BlueprintExchangeStatus.Completed, "upper outcome import seed");
                        var entry = Ordinary(state, state.TryList(1, 0)).Page.Entries[0];
                        Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = request, Kind = BlueprintExchangeKind.PrepareExport }), "upper outcome prepare"); var proposal = Await(state).Proposal;
                        Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = request, Kind = BlueprintExchangeKind.Export, Ticket = Ticket("CaptureOverwrite", request, target), SupportedTargetVersion = "1.4.5.8", Proposal = proposal, CounterCandidate = proposal.CounterCandidate, Name = entry.Name, TemplateKey = new BlueprintTemplateKey(entry.AssetId, Guid.Parse(entry.ContentRevision), entry.ContentHash) }), "upper outcome export");
                        var result = Await(state);
                        if (boundary == "BeforeCommit")
                            Need(result.Status == BlueprintExchangeStatus.Failed && !result.FileCommitted && !result.CounterCommitted && !File.Exists(counter) && File.ReadAllBytes(target).SequenceEqual(old) && !File.Exists(candidate) && !File.Exists(backup), "owner known-uncommitted failure preserves original and precisely removes candidate");
                        else if (boundary == "AfterCommit")
                            Need(result.Status == BlueprintExchangeStatus.CommitUnconfirmed && !result.FileCommitted && !result.CounterCommitted && !File.Exists(counter) && File.ReadAllBytes(target).SequenceEqual(literal) && File.ReadAllBytes(backup).SequenceEqual(old), "owner real attempted-unknown retains target and original recovery backup without consuming number");
                        else
                        {
                            Need(result.Status == BlueprintExchangeStatus.Completed && result.FileCommitted && result.CounterCommitted && File.ReadAllBytes(target).SequenceEqual(literal) && File.ReadAllBytes(backup).SequenceEqual(old) && result.Message.Contains("恢复文件已保留"), "owner keeps reliable publication and counter despite real backup cleanup failure");
                            CounterBytes(counter, 1);
                        }
                    }
                }
                finally { recoveryLease?.Dispose(); Need(io.Stop(3000), "upper outcome lane stopped"); }
            }
        }
        private static BlueprintExchangeIoReceipt Io(IBlueprintExchangeIo io, BlueprintExchangeIoCommand command)
        {
            using (var signal = new ManualResetEvent(false))
            {
                Need(io.TrySubmit(command, () => signal.Set()) && signal.WaitOne(5000), "one mechanical IO command acknowledged");
                BlueprintExchangeIoReceipt result; Need(io.TryTake(out result), "take exact IO acknowledgement"); return result;
            }
        }
        private static FileBlueprintExchange FaultIo(string boundary)
        {
            Func<string, FileMode, FileAccess, FileShare, Stream> factory = (path, mode, access, share) => new FileStream(path, mode, access, share);
            Action<string> fault = stage => { if (stage == boundary) throw new IOException("controlled-" + boundary); };
            return (FileBlueprintExchange)Activator.CreateInstance(typeof(FileBlueprintExchange), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { factory, fault }, null);
        }
        private static void MechanicalTargets(string area)
        {
            byte[] old = Encoding.UTF8.GetBytes("原件绝不按新路径许可覆盖"), bytes = Encoding.UTF8.GetBytes("本次完整候选字节");
            string target = Path.Combine(area, "target-tests.jmbp");
            foreach (string arm in new[] { "cancel", "collision", "changed", "replace", "BeforeCommit", "AfterCommit", "foreign-temp" })
            {
                if (File.Exists(target)) File.Delete(target);
                bool replace = arm == "changed" || arm == "replace" || arm == "BeforeCommit" || arm == "AfterCommit";
                if (replace || arm == "collision") File.WriteAllBytes(target, old);
                Guid request = Guid.NewGuid(); var ticket = Ticket(replace ? "CaptureOverwrite" : "NewTarget", request, target);
                if (arm == "changed") File.AppendAllText(target, "外改"); byte[] actualOriginal = File.Exists(target) ? File.ReadAllBytes(target) : null;
                var io = arm == "BeforeCommit" || arm == "AfterCommit" ? FaultIo(arm) : new FileBlueprintExchange();
                string candidate = Path.Combine(area, "." + Path.GetFileName(target) + "." + request.ToString("N") + ".tmp");
                try
                {
                    var begun = Io(io, new BlueprintExchangeIoCommand { Kind = BlueprintExchangeIoKind.BeginTarget, Ticket = ticket });
                    if (arm == "collision" || arm == "changed")
                    {
                        Need(!begun.Success && !begun.CommitUnconfirmed && File.ReadAllBytes(target).SequenceEqual(actualOriginal), "unconfirmed CreateNew collision or changed consent object preserves exact target");
                        Io(io, new BlueprintExchangeIoCommand { Kind = BlueprintExchangeIoKind.Close }); continue;
                    }
                    Need(begun.Success, "target candidate opened " + arm);
                    Need(Io(io, new BlueprintExchangeIoCommand { Kind = BlueprintExchangeIoKind.Write, Buffer = bytes, Count = bytes.Length }).Success, "target bytes written " + arm);
                    if (arm == "cancel")
                    {
                        var closed = Io(io, new BlueprintExchangeIoCommand { Kind = BlueprintExchangeIoKind.Close });
                        Need(closed.Success && closed.ErrorCode == null && !File.Exists(candidate) && !File.Exists(target), "known uncommitted exact CreateNew object removed on normal cancellation"); continue;
                    }
                    if (arm == "foreign-temp")
                    {
                        string moved = candidate + ".moved"; File.Move(candidate, moved); File.WriteAllBytes(candidate, old);
                        var closed = Io(io, new BlueprintExchangeIoCommand { Kind = BlueprintExchangeIoKind.Close });
                        Need(closed.ErrorCode != null && File.ReadAllBytes(candidate).SequenceEqual(old) && File.ReadAllBytes(moved).SequenceEqual(bytes), "replaced tmp object is retained, never pathname-deleted"); continue;
                    }
                    var result = Io(io, new BlueprintExchangeIoCommand { Kind = BlueprintExchangeIoKind.CommitTarget, ExpectedDigest = Hash(bytes), ExpectedLength = bytes.Length });
                    if (arm == "replace") Need(result.Success && result.FileCommitted && File.ReadAllBytes(target).SequenceEqual(bytes) && !File.Exists(candidate + ".backup"), "explicit consent mechanical replacement and verified backup cleanup");
                    else if (arm == "BeforeCommit") Need(!result.Success && !result.CommitUnconfirmed && File.ReadAllBytes(target).SequenceEqual(old), "precommit failure never changes original target");
                    else Need(!result.Success && result.CommitUnconfirmed && File.ReadAllBytes(target).SequenceEqual(bytes) && File.ReadAllBytes(candidate + ".backup").SequenceEqual(old), "after-attempt Unknown retains both actual target and original backup bytes");
                    Io(io, new BlueprintExchangeIoCommand { Kind = BlueprintExchangeIoKind.Close });
                    if (arm == "BeforeCommit") Need(!File.Exists(candidate), "precommit failure precisely cleans its same-object candidate");
                    if (arm == "AfterCommit") Need(File.ReadAllBytes(candidate + ".backup").SequenceEqual(old), "Unknown Close never removes recovery backup");
                }
                finally { io.Stop(3000); }
            }
        }
        private sealed class FaultFiles : IBlueprintLibraryFiles, IBlueprintLibraryLayoutSteps, IBlueprintLibraryExchangeSteps
        {
            private readonly FileBlueprintLibrary inner; private readonly string fault;
            internal FaultFiles(FileBlueprintLibrary inner, string fault) { this.inner = inner; this.fault = fault; }
            public void Open() { inner.Open(); } public IEnumerable<Guid> EnumerateAssets() { return inner.EnumerateAssets(); }
            public PreferenceReadResult OpenMetadata(Guid id, bool ownedUnpublishedContent = false) { return inner.OpenMetadata(id, ownedUnpublishedContent); }
            public PreferenceWriteResult WriteMetadata(string identity, byte[] bytes) { var result = inner.WriteMetadata(identity, bytes); return fault == "metadata" ? new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, result.Identity, "controlled-after-metadata", true, true) : result; }
            public void CloseMetadata() { inner.CloseMetadata(); } public void BeginStage(Guid id) { inner.BeginStage(id); } public void CloseStageWriter() { inner.CloseStageWriter(); }
            public void WriteStageBlock(Guid id, long number, byte[] contents, byte[] hash) { if (fault == "block") throw new IOException("controlled-stage-write"); inner.WriteStageBlock(id, number, contents, hash); }
            public byte[] ReadBlock(Guid id, bool stage, long number) { return inner.ReadBlock(id, stage, number); } public Stream OpenIndex(Guid id, bool stage) { return inner.OpenIndex(id, stage); }
            public void SealStage(Guid id, byte[] root) { inner.SealStage(id, root); } public byte[] ReadRoot(Guid id, bool stage) { return inner.ReadRoot(id, stage); }
            public void ValidateLayout(Guid id, bool stage, long count) { inner.ValidateLayout(id, stage, count); } public void PromoteStage(Guid stage, Guid asset) { inner.PromoteStage(stage, asset); }
            public IEnumerable<int> ValidateLayoutSteps(Guid id, bool stage, long count, int budget) { return inner.ValidateLayoutSteps(id, stage, count, budget); }
            public IEnumerable<int> SealStageSteps(Guid id, byte[] root, int budget) { return inner.SealStageSteps(id, root, budget); }
            public IEnumerable<int> PromoteStageSteps(Guid stage, Guid asset, int budget) { foreach (int progress in inner.PromoteStageSteps(stage, asset, budget)) yield return progress; if (fault == "promote") throw new IOException("controlled-after-promote"); }
            public void PrepareCleanup(Guid id, BlueprintCleanupProof proof) { inner.PrepareCleanup(id, proof); } public bool CleanupStep(Guid id, bool stage, long count, int budget, BlueprintCleanupProof proof = null) { return inner.CleanupStep(id, stage, count, budget, proof); }
            public void CloseCleanup() { inner.CloseCleanup(); } public void Dispose() { inner.Dispose(); }
        }
        private sealed class FakeTicket : IBlueprintExchangeTicket { public FakeTicket(Guid id) { RequestId = id; } public Guid RequestId { get; } public void Dispose() { } }
        private static void BlockedStreamLane(string area, string mode)
        {
            string prefix = Path.Combine(area, "blocked-" + mode), path = prefix + "-templates", counter = prefix + "-counter.json", source = prefix + ".jmbp";
            byte[] root, chunk; File.WriteAllBytes(source, Literal("阻塞流", Guid.NewGuid(), 4, out root, out chunk)); LibraryEntry entry;
            using (var initial = State(path, counter)) { Need(Import(initial, source).Status == BlueprintExchangeStatus.Completed, "seed blocked-stream test"); entry = Ordinary(initial, initial.TryList(1, 0)).Page.Entries[0]; }
            var entered = new ManualResetEvent(false); var release = new ManualResetEvent(false);
            Func<string, FileMode, FileAccess, FileShare, Stream> factory = (file, fm, access, share) => new BlockingStream(new FileStream(file, fm, access, share), mode, entered, release);
            var io = (FileBlueprintExchange)Activator.CreateInstance(typeof(FileBlueprintExchange), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { factory }, null);
            using (var state = State(path, counter, io))
            {
                Guid request = Guid.NewGuid();
                if (mode == "Read")
                    Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = request, Kind = BlueprintExchangeKind.Import, Ticket = Ticket("Source", request, source), SupportedTargetVersion = "1.4.5.8" }), "stream import accepted");
                else
                {
                    Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = request, Kind = BlueprintExchangeKind.PrepareExport }), "stream export proposal"); var proposal = Await(state).Proposal;
                    Need(state.TryBeginExchange(new BlueprintExchangeRequest { RequestId = request, Kind = BlueprintExchangeKind.Export, Ticket = Ticket("NewTarget", request, prefix + "-out.jmbp"), SupportedTargetVersion = "1.4.5.8", TemplateKey = new BlueprintTemplateKey(entry.AssetId, Guid.Parse(entry.ContentRevision), entry.ContentHash), Name = entry.Name, Proposal = proposal, CounterCandidate = proposal.CounterCandidate }), "stream export accepted");
                }
                try
                {
                    Need(entered.WaitOne(3000), "real external " + mode + " stream entered");
                    var identity = new BlueprintProjectionIdentity(entry.AssetId, Guid.Parse(entry.ContentRevision), entry.ContentHash, 9, 1);
                    Need(state.RequestProjectionOpen(identity), "projection admitted during blocked stream");
                    var catalog = BlueprintMaterialsChecks.Catalog(); var key = new BlueprintMaterialKey(entry.AssetId, false, identity.ContentRevision, entry.ContentHash, false, null, catalog.Revision);
                    Need(state.RequestMaterials(key, catalog), "materials admitted during blocked stream");
                    var clock = Stopwatch.StartNew(); BlueprintProjectionReceipt projection = null; bool projected = false;
                    while (clock.ElapsedMilliseconds < 3000)
                    {
                        BlueprintProjectionReceipt next; if (state.TryTakeProjectionReceipt(out next)) { projection = next; projected = true; }
                        if (projected && state.Materials(key).Status != BlueprintMaterialStatus.Pending) break;
                        Thread.Sleep(1);
                    }
                    Need(projected && projection != null && state.Materials(key).Status != BlueprintMaterialStatus.Pending, "projection and materials progress before blocked " + mode + " released");
                    Need(Ordinary(state, state.TryList(50, 0)).Page.TotalCount == 1, "ordinary read also progresses during " + mode);
                    if (mode != "Flush") state.CancelExchange(request);
                }
                finally { release.Set(); }
                var result = Await(state); Need(mode == "Flush" ? result.FileCommitted : result.Status == BlueprintExchangeStatus.Cancelled, "commit boundary versus cancellable stream result");
            }
            Need(io.Stop(3000), "single external lane retires after release"); entered.Dispose(); release.Dispose();
        }
        private sealed class BlockingStream : Stream
        {
            private readonly FileStream inner; private readonly string mode; private readonly ManualResetEvent entered, release; private bool held;
            internal BlockingStream(FileStream inner, string mode, ManualResetEvent entered, ManualResetEvent release) { this.inner = inner; this.mode = mode; this.entered = entered; this.release = release; }
            private void Hold(string boundary) { if (!held && mode == boundary) { held = true; entered.Set(); release.WaitOne(); } }
            public override int Read(byte[] buffer, int offset, int count) { Hold("Read"); return inner.Read(buffer, offset, count); }
            public override void Write(byte[] buffer, int offset, int count) { Hold("Write"); inner.Write(buffer, offset, count); }
            public override void Flush() { Hold("Flush"); inner.Flush(true); }
            public override bool CanRead { get { return inner.CanRead; } } public override bool CanWrite { get { return inner.CanWrite; } } public override bool CanSeek { get { return true; } }
            public override long Length { get { return inner.Length; } } public override long Position { get { return inner.Position; } set { inner.Position = value; } }
            public override long Seek(long offset, SeekOrigin origin) { return inner.Seek(offset, origin); } public override void SetLength(long value) { inner.SetLength(value); }
            protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        }
        private sealed class BlockedIo : IBlueprintExchangeIo
        {
            internal readonly ManualResetEvent Submitted = new ManualResetEvent(false); private Action wake; private bool ready; private BlueprintExchangeIoKind kind;
            public bool TrySubmit(BlueprintExchangeIoCommand command, Action completed) { kind = command.Kind; wake = completed; if (kind == BlueprintExchangeIoKind.Close) { ready = true; completed(); } else Submitted.Set(); return true; }
            internal void Release() { ready = true; wake(); }
            public bool TryTake(out BlueprintExchangeIoReceipt value) { value = ready ? new BlueprintExchangeIoReceipt { Success = true, Length = 100 } : null; ready = false; return value != null; }
            public void BeginStop() { } public bool Stop(int milliseconds) { return true; }
        }
        // Independent binary fixture follows the published byte table directly;
        // it does not call the production encoder or decode-and-reencode bytes.
        private static byte[] Literal(string name, Guid revision, int x, out byte[] root, out byte[] chunk, bool invalidLayer = false)
        {
            using (var s = new MemoryStream()) using (var w = new BinaryWriter(s, Encoding.UTF8, true))
            {
                w.Write(0x43424D4A); w.Write(1); w.Write(1); w.Write(0); w.Write(x); w.Write(5); w.Write(0); w.Write((byte)(invalidLayer ? 255 : 1)); w.Write((ushort)1); w.Write((ushort)0); w.Write((short)0); w.Write((short)0); for (int i = 0; i < 7; i++) w.Write((byte)0); w.Flush(); chunk = s.ToArray();
            }
            byte[] blockHash = Hash(chunk);
            using (var s = new MemoryStream()) using (var w = new BinaryWriter(s, Encoding.UTF8, true))
            {
                w.Write(0x42524D4A); w.Write(1); w.Write(revision.ToByteArray()); w.Write(1L); w.Write(Hash(blockHash)); w.Write(x); w.Write(5); w.Write(1); w.Write(1); w.Write(1L); w.Write(0L); w.Write(8UL); w.Write(10UL); byte[] version = Encoding.UTF8.GetBytes("1.4.5.8"); w.Write(version.Length); w.Write(version); w.Write(0); w.Flush(); root = s.ToArray();
            }
            using (var s = new MemoryStream()) using (var w = new BinaryWriter(s, Encoding.UTF8, true))
            {
                w.Write(new byte[] { 0x4A, 0x4D, 0x52, 0x4A, 0x4D, 0x42, 0x50, 0 }); w.Write(1); byte[] text = Encoding.UTF8.GetBytes(name); w.Write((uint)text.Length); w.Write(text); w.Write((uint)root.Length); w.Write(root); w.Write(32L); w.Write(blockHash); w.Write((uint)chunk.Length); w.Write(chunk); w.Flush(); byte[] body = s.ToArray(); w.Write(Hash(body)); w.Flush(); return s.ToArray();
            }
        }
        private static byte[] Hash(byte[] value) { using (var hash = SHA256.Create()) return hash.ComputeHash(value); }
        private static byte[] Pack(string name, byte[] root, byte[] index, byte[][] chunks)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(new byte[] { 0x4A, 0x4D, 0x52, 0x4A, 0x4D, 0x42, 0x50, 0 }); writer.Write(1); byte[] text = Encoding.UTF8.GetBytes(name); writer.Write((uint)text.Length); writer.Write(text); writer.Write((uint)root.Length); writer.Write(root); writer.Write((long)index.Length); writer.Write(index);
                foreach (byte[] chunk in chunks) { writer.Write((uint)chunk.Length); writer.Write(chunk); } writer.Flush(); byte[] body = stream.ToArray(); writer.Write(Hash(body)); writer.Flush(); return stream.ToArray();
            }
        }
    }
}
