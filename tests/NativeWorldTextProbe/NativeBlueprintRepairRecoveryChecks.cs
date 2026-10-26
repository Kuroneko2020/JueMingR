using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Platform.Blueprint;
using JueMingR.Platform.Settings;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintRepairRecoveryChecks
    {
        internal static void Run(object context)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var input = Get(context, "Input");
            var editor = (BlueprintCaptureState)Get(host, "State"); var flow = (BlueprintCaptureFlow)Get(host, "Flow");
            var field = host.GetType().GetField("<Library>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            var original = (LibraryState)field.GetValue(host);
            Action pump = () => { NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime"); Call(host, "Poll"); };
            Action begin = () => { Call(shell, "CloseAndSubmitPosition"); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); editor.Enter(); editor.Selection.Clear(); editor.Selection.ToggleRectangle(10, 10, 10, 10, 120, 120); Require((bool)Call(host, "Save"), "actual Host accepts a new capture after a retired operation"); };
            try
            {
                foreach (string fault in new[] { "append", "seal", "precommit", "unknown" })
                {
                    string directory = Path.Combine(Terraria.Program.SavePath, "repair-host-" + fault);
                    var files = new FaultFiles(directory) { Fault = fault };
                    using (var state = new LibraryState(files))
                    {
                        field.SetValue(host, state); begin();
                        NativeQuickItemChecks.Until(() => { pump(); return flow.Status == BlueprintCaptureStatus.DraftReady || flow.Status == BlueprintCaptureStatus.Failed; });
                        if (fault == "precommit" || fault == "unknown")
                        { Require(flow.Status == BlueprintCaptureStatus.DraftReady, "metadata fault is reached only after a real sealed draft"); Call(host, "ConfirmSave"); NativeQuickItemChecks.Until(() => { pump(); return flow.Status == BlueprintCaptureStatus.Failed || flow.Status == BlueprintCaptureStatus.CommitUnconfirmed; }); }
                        var old = state.Snapshot.LastReceipt; var token = flow.Token;
                        Require(old.OperationRetired && old.OperationId == token.OperationId && flow.ContentHandle.IsEmpty && flow.Summary == null && editor.Selection.HasSelection && (bool)Get(host, "CanSave"), "Host removes retired retry qualification, preserves selection and enables fresh capture: " + fault);
                        Require(flow.Status == (fault == "unknown" ? BlueprintCaptureStatus.CommitUnconfirmed : BlueprintCaptureStatus.Failed), "Host keeps definite precommit failure distinct from unknown commit: " + fault);
                        // The live process owns the exclusive library lease;
                        // it is not recovery content and cannot be opened for
                        // a byte snapshot until this state is disposed.
                        var protectedBytes = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Where(p => p != Path.Combine(directory, ".library.lock")).ToDictionary(p => p, Hash);
                        files.Fault = null; begin(); var fresh = flow.Token;
                        Call(host, "ApplyReceipt", old);
                        Require(flow.Token.OperationId == fresh.OperationId && fresh.OperationId != token.OperationId && flow.Status == BlueprintCaptureStatus.Starting, "late old receipt cannot retire or adopt a newer Host Flow");
                        NativeQuickItemChecks.Until(() => { pump(); return flow.Status == BlueprintCaptureStatus.DraftReady || flow.Status == BlueprintCaptureStatus.Failed; });
                        Require(flow.Status == BlueprintCaptureStatus.DraftReady, "healthy new Host operation completes after the mechanical fault is removed");
                        // Try the retired handle while a healthy draft owns the
                        // slot. Failure must neither acquire its token nor
                        // retire the healthy stage or affect its Host projection.
                        Require(state.TryDiscardDraft(new ContentHandle(token.OperationId)), "stale handle command reaches worker boundary");
                        NativeQuickItemChecks.Until(() => { pump(); return !state.Snapshot.Busy; });
                        Require(flow.Status == BlueprintCaptureStatus.DraftReady && flow.Token.OperationId == fresh.OperationId, "stale handle cannot affect a newer actual Host draft");
                        Call(host, "ConfirmSave"); NativeQuickItemChecks.Until(() => { pump(); return flow.Status == BlueprintCaptureStatus.Saved || flow.Status == BlueprintCaptureStatus.Failed; });
                        Require(flow.Status == BlueprintCaptureStatus.Saved && !editor.Selection.HasSelection, "actual healthy Host new operation reaches Saved after fault recovery");
                        Require(protectedBytes.All(p => File.Exists(p.Key) && Hash(p.Key) == p.Value), "retirement and healthy save preserve all old recovery bytes: " + fault);
                        Call(shell, "CloseAndSubmitPosition"); editor.Exit();
                    }
                }
            }
            finally { field.SetValue(host, original); Call(shell, "CloseAndSubmitPosition"); editor.Exit(); }
            Console.WriteLine("PASS actual Host mechanical append/seal/precommit/unknown retirement, no stale retry, new operation Saved and old recovery bytes preserved.");
        }
        private static string Hash(string path) { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
        private sealed class FaultFiles : IBlueprintLibraryFiles
        {
            private readonly FileBlueprintLibrary inner; private readonly string path; internal string Fault;
            internal Guid HoldAsset = Guid.Empty;
            internal long HoldBlock = -1;
            internal readonly System.Threading.ManualResetEvent ReadEntered = new System.Threading.ManualResetEvent(false), ReadRelease = new System.Threading.ManualResetEvent(true);
            internal FaultFiles(string path) { this.path = path; inner = new FileBlueprintLibrary(path); }
            public void Open() { inner.Open(); }
            public IEnumerable<Guid> EnumerateAssets() { return inner.EnumerateAssets(); }
            public PreferenceReadResult OpenMetadata(Guid id, bool ownedUnpublishedContent = false) { return inner.OpenMetadata(id, ownedUnpublishedContent); }
            public PreferenceWriteResult WriteMetadata(string identity, byte[] bytes)
            {
                if (Fault == "precommit")
                {
                    var metadata = typeof(FileBlueprintLibrary).GetField("metadata", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inner);
                    metadata.GetType().GetProperty("DebugCandidateWriteFailure", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(metadata, true, null);
                }
                var result = inner.WriteMetadata(identity, bytes);
                if (Fault == "precommit") Require(!result.CommitUnconfirmed && !result.IsProtected && result.Status != PreferenceWriteStatus.Saved, "real owned temporary write failure is known precommit at the mechanical layer");
                if (Fault == "unknown" && result.Status == PreferenceWriteStatus.Saved) return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, null, "isolated-receipt-lost", true, true);
                return result;
            }
            public void CloseMetadata() { inner.CloseMetadata(); }
            public void BeginStage(Guid id) { inner.BeginStage(id); }
            public void WriteStageBlock(Guid id, long number, byte[] bytes, byte[] hash)
            {
                if (Fault == "append") using (File.Open(Path.Combine(path, ".staging", id.ToString("N"), "chunks", number.ToString("D20") + ".bin"), FileMode.CreateNew)) { }
                inner.WriteStageBlock(id, number, bytes, hash);
            }
            public byte[] ReadBlock(Guid id, bool stage, long number)
            {
                if (!stage && id == HoldAsset && number == HoldBlock)
                { HoldBlock = -1; ReadEntered.Set(); if (!ReadRelease.WaitOne(10000)) throw new TimeoutException("isolated projection read gate was not released"); }
                return inner.ReadBlock(id, stage, number);
            }
            public Stream OpenIndex(Guid id, bool stage) { return inner.OpenIndex(id, stage); }
            public void SealStage(Guid id, byte[] root)
            { if (Fault == "seal") using (File.Open(Path.Combine(path, ".staging", id.ToString("N"), "root.bin"), FileMode.CreateNew)) { } inner.SealStage(id, root); }
            public byte[] ReadRoot(Guid id, bool stage) { return inner.ReadRoot(id, stage); }
            public void ValidateLayout(Guid id, bool stage, long blocks) { inner.ValidateLayout(id, stage, blocks); }
            public void PromoteStage(Guid stage, Guid asset) { inner.PromoteStage(stage, asset); }
            public void PrepareCleanup(Guid id, BlueprintCleanupProof proof) { inner.PrepareCleanup(id, proof); }
            public bool CleanupStep(Guid id, bool stage, long blocks, int budget, BlueprintCleanupProof proof = null) { return inner.CleanupStep(id, stage, blocks, budget, proof); }
            public void CloseCleanup() { inner.CloseCleanup(); }
            public void CloseStageWriter() { inner.CloseStageWriter(); }
            public void Dispose() { ReadRelease.Set(); inner.Dispose(); ReadEntered.Dispose(); ReadRelease.Dispose(); }
        }
    }
}
