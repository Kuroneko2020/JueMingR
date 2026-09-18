using System;
using System.Collections.Generic;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;

namespace JueMingR.Platform.Hotkeys
{
    // Game-thread owner of the effective table. Worker success, not optimistic
    // preference state, is the only transition that activates a submitted table.
    public sealed class HotkeyBindings : IDisposable
    {
        private sealed class Bound { internal HotkeyAction Action; internal HotkeyChord Chord; }
        private readonly HotkeyRegistry registry;
        private readonly DocumentWorker<HotkeyDocument> worker;
        private readonly Dictionary<string, HotkeyChord> effective = new Dictionary<string, HotkeyChord>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> errors = new Dictionary<string, string>(StringComparer.Ordinal);
        private List<Bound>[] byKey = new List<Bound>[HotkeyChord.KeyCount];
        private long compiledRevision = -1;
        private HotkeyDocument document = HotkeyDocument.Empty;
        private long nextCommand;
        private string pendingAction;
        private HotkeyAdvisory pendingWarning;
        private bool pendingClear, pendingHadBinding;
        public bool Loaded { get; private set; }
        public bool Busy { get; private set; }
        public bool Protected { get; private set; }
        public HotkeyFeedback Feedback { get; private set; } = new HotkeyFeedback(HotkeyFeedbackKind.Loading, "正在加载快捷键");
        public string Message { get { return Feedback.Message; } }
        public long CompletionId { get; private set; }
        public string CompletionAction { get; private set; }
        public bool CompletionSucceeded { get; private set; }
        public bool CommitUnconfirmed { get; private set; }
        public string LoadError { get; private set; }
        public HotkeyBindings(HotkeyRegistry registry, IPreferenceStorage storage)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry)); registry.Freeze();
            worker = new DocumentWorker<HotkeyDocument>(storage, HotkeyDocument.Decode, HotkeyDocument.Encode, HotkeyDocument.Empty);
        }
        public HotkeyChord Get(string action) { EnsureRegistry(); HotkeyChord value; return effective.TryGetValue(action, out value) ? value : null; }
        public string Error(string action) { EnsureRegistry(); string value; return errors.TryGetValue(action, out value) ? value : null; }
        public void Poll()
        {
            EnsureRegistry();
            DocumentResult<HotkeyDocument> result;
            if (!worker.TryTake(out result)) return;
            if (result.CommandId == 0)
            {
                Loaded = true;
                if (result.Success) { document = result.Value; Feedback = new HotkeyFeedback(HotkeyFeedbackKind.Ready); Compile(); }
                else { Protected = true; LoadError = result.Error; Feedback = new HotkeyFeedback(HotkeyFeedbackKind.Protected, "无法加载快捷键，暂时无法修改。", "原文件已保护。"); }
                return;
            }
            Busy = false; CompletionId = result.CommandId; CompletionAction = pendingAction; pendingAction = null;
            HotkeyAdvisory warning = pendingWarning; pendingWarning = null;
            CompletionSucceeded = result.Success; CommitUnconfirmed = result.CommitUnconfirmed;
            if (result.Success) { document = result.Value; Compile(); if (!Protected) Feedback = new HotkeyFeedback(pendingClear ? HotkeyFeedbackKind.Cleared : HotkeyFeedbackKind.Saved, pendingClear ? "已清除" : "已保存", advisory: warning); }
            else
            {
                // A protected/unknown storage result is deliberately not retried.
                // The previous effective table survives; disk rollback is unknown.
                Protected = result.IsProtected || result.CommitUnconfirmed;
                string retained = pendingHadBinding ? "原快捷键仍有效" : "当前仍未设置快捷键";
                Feedback = new HotkeyFeedback(result.CommitUnconfirmed ? HotkeyFeedbackKind.Unconfirmed : Protected ? HotkeyFeedbackKind.Protected : HotkeyFeedbackKind.Failed,
                    result.CommitUnconfirmed ? "无法确认是否保存成功，文件已保护" : Protected ? "保存受阻，文件已保护，暂时无法修改" : "保存失败，" + retained,
                    result.CommitUnconfirmed ? retained + "。" :
                    Protected ? retained + "。" : "可重新录入后再试。");
            }
        }
        public string Validate(string id, HotkeyChord chord)
        {
            EnsureRegistry();
            HotkeyAction action = registry.Find(id);
            if (action == null) return "此功能暂不支持设置快捷键。";
            if (!action.CanConfigure) return "此条目正在修改或保存失败，暂时不能修改快捷键。";
            if (!Loaded) return "正在加载快捷键……";
            if (Protected) return Message ?? "快捷键设置有问题，暂时无法修改。";
            if (chord == null) return null;
            foreach (var other in effective)
                if (other.Key != id && chord.Equals(other.Value) && (registry.Find(other.Key).Context & action.Context) != 0)
                    return "此快捷键已用于「" + registry.Find(other.Key).Name + "」。";
            return null;
        }
        public bool TrySet(string id, HotkeyChord chord, Func<HotkeyAction, HotkeyChord, HotkeyAdvisory> vanillaWarning, out long command, out string reason)
        {
            command = 0; reason = Validate(id, chord); if (reason != null) return false;
            if (Busy) { reason = "上一项还在保存，请稍候。"; return false; }
            bool existing = false; foreach (var entry in document.Entries) if (entry.Key == id) { existing = true; break; }
            if (!existing && document.Entries.Count >= 256) { reason = "已达到快捷键数量上限，无法新增。"; return false; }
            // Native overlaps are advisory, including an unavailable profile.
            // Read only for an eligible edit; never during load or dispatch.
            // The warning belongs to this command, not the file or popup lifetime.
            HotkeyAdvisory warning = null;
            if (chord != null)
            {
                var unavailable = HotkeyAdvisory.Unavailable("无法读取当前原版键盘配置。");
                try { warning = vanillaWarning == null ? unavailable : vanillaWarning(registry.Find(id), chord); }
                catch { warning = unavailable; }
            }
            HotkeyDocument candidate = document.With(id, chord);
            if (HotkeyDocument.Encode(candidate).Length > 65536) { reason = "快捷键文件已达到大小上限，无法新增。"; return false; }
            long next = ++nextCommand;
            if (!worker.TrySubmit(next, candidate)) { reason = "暂时无法保存，当前快捷键未改变。"; return false; }
            command = next; pendingAction = id; pendingWarning = warning; pendingClear = chord == null; pendingHadBinding = Get(id) != null; Busy = true;
            Feedback = new HotkeyFeedback(HotkeyFeedbackKind.Saving, "正在保存", pendingHadBinding ? "当前快捷键保持不变" : "当前仍未设置快捷键", warning); return true;
        }
        public void Dispatch(HotkeyInput input, HotkeyContext context, bool permitted, string onlyAction = null)
        {
            EnsureRegistry();
            if (!permitted || !Loaded || effective.Count == 0 || input.SystemModifier || !input.Reliable) return;
            // A command may retire its own set. Keep this dispatch snapshot, but
            // each action also checks its current registration before invoking.
            var table = byKey;
            for (int key = 1; key < table.Length; key++)
            {
                List<Bound> entries = table[key]; if (entries == null || !input.IsNew(key)) continue;
                foreach (Bound bound in entries)
                    if ((onlyAction == null || bound.Action.Id == onlyAction) && bound.Chord.Modifiers == input.Modifiers) bound.Action.Invoke(context, bound.Chord);
            }
        }
        private void Compile()
        {
            effective.Clear(); errors.Clear(); byKey = new List<Bound>[HotkeyChord.KeyCount]; compiledRevision = registry.Revision;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in document.Entries)
            {
                if (!seen.Add(entry.Key)) errors[entry.Key] = "此功能有重复的快捷键记录，已停用，原文件已保护。";
                HotkeyChord chord; string reason;
                if (entry.Value.Length == 0) continue;
                if (!HotkeyChord.TryParse(entry.Value, out chord, out reason)) { errors[entry.Key] = reason; continue; }
                if (registry.Find(entry.Key) != null) effective[entry.Key] = chord;
            }
            // A malformed/duplicate action owns no effective candidate. Remove
            // all such IDs before comparing valid bindings, so reordering the
            // invalid rows cannot disable a different, well-formed action.
            foreach (var error in errors) effective.Remove(error.Key);
            foreach (var a in effective)
                foreach (var b in effective)
                    if (a.Key != b.Key && a.Value.Equals(b.Value) && (registry.Find(a.Key).Context & registry.Find(b.Key).Context) != 0)
                    { errors[a.Key] = "多个功能使用同一快捷键，相关快捷键已停用，原文件已保护。"; errors[b.Key] = errors[a.Key]; }
            if (errors.Count != 0) { Protected = true; Feedback = new HotkeyFeedback(HotkeyFeedbackKind.Protected, "快捷键文件已保护", "无效或重复的快捷键已停用，暂时无法修改。"); }
            foreach (var error in errors) effective.Remove(error.Key);
            foreach (var entry in effective)
            {
                int key = entry.Value.MainKey;
                if (byKey[key] == null) byKey[key] = new List<Bound>();
                byKey[key].Add(new Bound { Action = registry.Find(entry.Key), Chord = entry.Value });
            }
        }
        private void EnsureRegistry() { if (Loaded && compiledRevision != registry.Revision) Compile(); }
        // Only a pre-reserved owner can remove its retired row. Other actions,
        // including unknown future IDs, are preserved byte-for-value in the model.
        // A late earlier save may leave an inert orphan; it cannot register an action.
        public bool TryRemoveRetired(DynamicHotkeyOwner owner, string id, out long command, out string reason)
        {
            command = 0; reason = null; EnsureRegistry();
            if (owner == null || owner.Registry != registry || !owner.Owns(id) || registry.Find(id) != null)
            { reason = "只能清理已经移除的本组快捷动作。"; return false; }
            if (!Loaded || Protected || Busy) { reason = "快捷键尚未就绪、受保护或正在保存，请稍后重试。"; return false; }
            var candidate = document.Without(id); long next = ++nextCommand;
            if (!worker.TrySubmit(next, candidate)) { reason = "暂时无法清理快捷键。"; return false; }
            command = next; pendingAction = id; pendingWarning = null; pendingClear = true; pendingHadBinding = false; Busy = true;
            Feedback = new HotkeyFeedback(HotkeyFeedbackKind.Saving, "正在清理已删除条目的快捷键"); return true;
        }
        public bool Stop(int milliseconds) { return worker.Stop(milliseconds); }
        public void Dispose() { worker.Dispose(); }
    }
}
