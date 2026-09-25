using System;
using System.Collections.Generic;
using System.Diagnostics;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;
namespace JueMingR.Features.Onboarding
{
    // Game-thread owner. Entries retain only hashed identities and bounded document
    // state, never players/worlds. File workers receive immutable bools and keys.
    public sealed class OnboardingState
    {
        private sealed class Entry
        {
            internal string Key;
            internal DocumentWorker<bool> Worker;
            internal bool Loaded, Seen, Saved, Attempted, Failed, Retiring;
            internal long LastUse;
        }
        public const int MaximumEntries = 64, MaximumWorkers = 4;
        private readonly Func<string, IPreferenceStorage> storage;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly List<Entry> workers = new List<Entry>();
        private Entry current;
        private string waitingKey;
        private long usage;
        private bool capacityReported;
        private bool active, shown, suppressed, notified, stopped;
        private long generation;
        private double lastDraw = -1, visibleMilliseconds;
        public string Failure { get; private set; }
        public int WorkerCount { get { return workers.Count; } }
        public int EntryCount { get { return entries.Count; } }
        public bool Shown { get { return shown; } }
        public bool Saved { get { return current != null && current.Saved; } }
        public bool Ready { get { return active && !stopped && !suppressed && (shown ? visibleMilliseconds < 3600 : waitingKey == null && (current == null || current.Loaded && !current.Seen)); } }
        public float Opacity { get { return (float)Math.Max(0, Math.Min(1, (3600 - visibleMilliseconds) / 600)); } }
        public OnboardingState(Func<string, IPreferenceStorage> storage)
        { this.storage = storage ?? throw new ArgumentNullException(nameof(storage)); }
        public void Begin(long sessionGeneration)
        { End(); generation = sessionGeneration; active = !stopped; shown = suppressed = false; visibleMilliseconds = 0; lastDraw = -1; capacityReported = false; }
        public void Resolve(string key)
        {
            if (!active || key == null || current != null) return;
            if (!OnboardingMarkerCodec.ValidKey(key)) throw new ArgumentException("Invalid character key.");
            Entry entry;
            if (!entries.TryGetValue(key, out entry))
            {
                foreach (Entry retiring in workers)
                    if (retiring.Key == key && retiring.Retiring) { waitingKey = key; return; }
                if (entries.Count >= MaximumEntries)
                {
                    Entry oldest = null;
                    foreach (Entry cached in entries.Values)
                        if (cached != current && cached.Saved && cached.Worker == null && (oldest == null || cached.LastUse < oldest.LastUse)) oldest = cached;
                    if (oldest != null) entries.Remove(oldest.Key);
                }
                if (entries.Count >= MaximumEntries || workers.Count >= MaximumWorkers)
                { if (!capacityReported) { capacityReported = true; Report("首次提示记录暂不可用，本次仍可查看帮助；下次启动后再尝试保存。"); } return; }
                entry = new Entry { Key = key }; entries.Add(key, entry);
                try
                {
                    var codec = new OnboardingMarkerCodec(key);
                    entry.Worker = new DocumentWorker<bool>(storage(key), codec.Decode, codec.Encode, false);
                    workers.Add(entry);
                }
                catch { entry.Loaded = entry.Failed = true; Report("首次提示记录无法读取，原文件已保留；下次启动后再试。"); }
            }
            current = entry;
            entry.LastUse = ++usage;
            // Unknown identity may already have drawn in this exact session.
            // Resolve is admitted only by the host's same-player continuity gate.
            if (shown) entry.Seen = true;
            else if (entry.Seen) suppressed = true;
        }
        public void Poll()
        {
            for (int i = workers.Count - 1; i >= 0; i--)
            {
                Entry entry = workers[i]; var worker = entry.Worker;
                DocumentResult<bool> result;
                if (!entry.Retiring && worker.TryTake(out result))
                {
                    if (result.CommandId == 0)
                    {
                        entry.Loaded = true; entry.Failed = !result.Success;
                        if (result.Success && result.Value)
                        { entry.Seen = entry.Saved = true; if (entry == current && !shown) suppressed = true; }
                    }
                    else { entry.Saved = result.Success; entry.Failed = !result.Success; }
                    if (!result.Success) Report(FailureMessage(result));
                }
                if (!entry.Retiring && entry.Loaded && entry.Seen && !entry.Saved && !entry.Failed && !entry.Attempted)
                {
                    entry.Attempted = true;
                    if (!worker.TrySubmit(1, true)) { entry.Failed = true; Report("首次提示记录未能保存；下次启动后再试。帮助仍可随时查看。"); }
                }
                if (!entry.Retiring && (entry.Saved || entry.Failed)) { entry.Retiring = true; worker.BeginStop(); }
                if (entry.Retiring && worker.IsFinished) { workers.RemoveAt(i); entry.Worker = null; }
            }
            if (waitingKey != null && active)
            {
                bool waiting = false; foreach (Entry entry in workers) if (entry.Key == waitingKey) waiting = true;
                if (!waiting) { string key = waitingKey; waitingKey = null; Resolve(key); }
            }
        }
        public void Presented(long sessionGeneration, double monotonicMilliseconds)
        {
            if (sessionGeneration != generation || !Ready || Double.IsNaN(monotonicMilliseconds) || Double.IsInfinity(monotonicMilliseconds)) return;
            shown = true;
            if (current != null) current.Seen = true;
            // Only adjacent successfully visible frames contribute. A stall is
            // never repaid as unseen reading time; Pause also breaks the chain.
            double gap = monotonicMilliseconds - lastDraw;
            if (lastDraw >= 0 && gap >= 0 && gap <= 250) visibleMilliseconds += gap;
            lastDraw = monotonicMilliseconds;
        }
        public void Pause() { lastDraw = -1; }
        public void End()
        {
            DetachIdentity(); active = false;
        }
        public void DetachIdentity()
        {
            // Suppression belongs to this role admission, not to the transient
            // file observation. Only Begin for a new admission clears it.
            if (current != null && current.Seen && !shown) suppressed = true;
            if (current != null && !current.Seen && !current.Failed && !current.Saved)
            {
                if (current.Worker != null) { current.Retiring = true; current.Worker.BeginStop(); }
                entries.Remove(current.Key);
            }
            current = null; waitingKey = null; Pause();
        }
        private void Report(string message) { Failure = message; notified = false; }
        private static string FailureMessage(DocumentResult<bool> result)
        {
            if (result.CommitUnconfirmed) return "首次提示记录的保存结果无法确认，已停止写入；下次启动后重新读取。帮助仍可随时查看。";
            switch (result.Error)
            {
                case "first-create-conflict":
                case "unexpected-source-identity":
                case "external-document-change":
                case "document-disappeared": return "首次提示记录在保存前被其它操作更改，已停止写入以保护现有文件；下次启动后重新读取。帮助仍可随时查看。";
                case "document-write-access-denied": return "没有权限保存首次提示记录，已停止写入；恢复权限后下次启动再试。帮助仍可随时查看。";
                case "UnsupportedVersion": return "首次提示记录来自当前程序不支持的版本，已保留文件且不写入。请使用兼容版本；帮助仍可随时查看。";
                case "UnknownFields": return "首次提示记录含当前程序不认识的字段，已保留文件且不写入。帮助仍可随时查看。";
                case "Invalid": return "首次提示记录格式损坏或身份不符，已保留文件且不写入。帮助仍可随时查看。";
                case "document-too-large": return "首次提示记录过大，已保留文件且不写入。帮助仍可随时查看。";
                case "another-writer": return "首次提示记录正由另一实例使用，本次不写入；关闭占用后下次启动再试。帮助仍可随时查看。";
                case "missing-document-with-recovery-material": return "首次提示记录缺失但存在恢复文件，已保留恢复材料且不写入。帮助仍可随时查看。";
                case "data-root-access-denied":
                case "document-read-access-denied": return "没有权限读取首次提示记录，本次不写入；恢复权限后下次启动再试。帮助仍可随时查看。";
                default: return result.CommandId != 0 ? "首次提示记录保存失败，已停止本次写入；下次启动后重新读取。帮助仍可随时查看。" : "首次提示记录读取失败，本次不写入；下次启动后再试。帮助仍可随时查看。";
            }
        }
        public void TakeFeedback(Action<string> display)
        { if (!notified && Failure != null) { notified = true; display(Failure); } }
        public void Stop(int totalMilliseconds)
        {
            stopped = true; active = false; Pause();
            // An accepted Draw receipt survives exit even if initial reading is
            // still in flight. A protected read is never made writable here.
            var clock = Stopwatch.StartNew();
            foreach (Entry entry in workers) entry.Worker.BeginStop(entry.Seen && !entry.Attempted && !entry.Failed ? (Func<bool, bool>)(value => true) : null);
            foreach (Entry entry in workers) entry.Worker.Stop(Math.Max(0, totalMilliseconds - (int)clock.ElapsedMilliseconds));
        }
    }
}
