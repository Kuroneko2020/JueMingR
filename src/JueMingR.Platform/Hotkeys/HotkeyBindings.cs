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
        private readonly List<Bound>[] byKey = new List<Bound>[HotkeyChord.KeyCount];
        private HotkeyDocument document = HotkeyDocument.Empty;
        private long nextCommand;
        private string pendingAction;
        private string pendingWarning;
        public bool Loaded { get; private set; }
        public bool Busy { get; private set; }
        public bool Protected { get; private set; }
        public string Message { get; private set; }
        public long CompletionId { get; private set; }
        public string CompletionAction { get; private set; }
        public bool CompletionSucceeded { get; private set; }
        public bool CommitUnconfirmed { get; private set; }
        public HotkeyBindings(HotkeyRegistry registry, IPreferenceStorage storage)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry)); registry.Freeze();
            worker = new DocumentWorker<HotkeyDocument>(storage, HotkeyDocument.Decode, HotkeyDocument.Encode, HotkeyDocument.Empty);
        }
        public HotkeyChord Get(string action) { HotkeyChord value; return effective.TryGetValue(action, out value) ? value : null; }
        public string Error(string action) { string value; return errors.TryGetValue(action, out value) ? value : null; }
        public void Poll()
        {
            DocumentResult<HotkeyDocument> result;
            if (!worker.TryTake(out result)) return;
            if (result.CommandId == 0)
            {
                Loaded = true;
                if (result.Success) { document = result.Value; Compile(); }
                else { Protected = true; Message = "快捷键文件未能可靠加载（" + result.Error + "），原文件已保护。"; }
                return;
            }
            Busy = false; CompletionId = result.CommandId; CompletionAction = pendingAction; pendingAction = null;
            string warning = pendingWarning; pendingWarning = null;
            CompletionSucceeded = result.Success; CommitUnconfirmed = result.CommitUnconfirmed;
            if (result.Success) { document = result.Value; Compile(); Message = WithWarning("已保存并生效。", warning); }
            else
            {
                // A protected/unknown storage result is deliberately not retried.
                // The previous effective table survives; disk rollback is unknown.
                Protected = result.IsProtected || result.CommitUnconfirmed;
                Message = result.CommitUnconfirmed ? "磁盘提交结果未确认；旧绑定继续有效，文件已保护。请退出后保留文件及恢复材料检查。" :
                    Protected ? "未能可靠保存；旧绑定继续有效，原文件已保护。请退出后检查权限或外部文件变化。" :
                    "此次保存失败，旧绑定继续有效。可以重新录入后再试。";
            }
        }
        public string Validate(string id, HotkeyChord chord)
        {
            HotkeyAction action = registry.Find(id);
            if (action == null) return "该功能尚未注册快捷键。";
            if (!Loaded) return "正在读取快捷键文件。";
            if (Protected) return Message ?? "快捷键文件有受保护条目，请退出后检查。";
            if (chord == null) return null;
            foreach (var other in effective)
                if (other.Key != id && chord.Equals(other.Value) && (registry.Find(other.Key).Context & action.Context) != 0)
                    return "与「" + registry.Find(other.Key).Name + "」的绑定冲突；功能关闭时仍保留其切换绑定。";
            return null;
        }
        public bool TrySet(string id, HotkeyChord chord, Func<HotkeyAction, HotkeyChord, string> vanillaWarning, out long command, out string reason)
        {
            command = 0; reason = Validate(id, chord); if (reason != null) return false;
            if (Busy) { reason = "上一项仍在保存，请稍候。"; return false; }
            bool existing = false; foreach (var entry in document.Entries) if (entry.Key == id) { existing = true; break; }
            if (!existing && document.Entries.Count >= 256) { reason = "快捷键文件已达条目上限；保留已有和未知动作，未新增绑定。"; return false; }
            // Native overlaps are advisory, including an unavailable profile.
            // Read only for an eligible edit; never during load or dispatch.
            // The warning belongs to this command, not the file or popup lifetime.
            string warning = null;
            if (chord != null)
            {
                const string unavailable = "无法核对当前原版按键，请自行确认是否重合。";
                try { warning = vanillaWarning == null ? unavailable : vanillaWarning(registry.Find(id), chord); }
                catch { warning = unavailable; }
            }
            HotkeyDocument candidate = document.With(id, chord);
            long next = ++nextCommand;
            if (!worker.TrySubmit(next, candidate)) { reason = "保存入口暂不可用，旧绑定保留。"; return false; }
            command = next; pendingAction = id; pendingWarning = warning; Busy = true; Message = WithWarning("正在保存；成功前仍使用旧绑定。", warning); return true;
        }
        private static string WithWarning(string message, string warning)
        { return String.IsNullOrEmpty(warning) ? message : message + " 提醒：" + warning; }
        public void Dispatch(HotkeyInput input, HotkeyContext context, bool permitted)
        {
            if (!permitted || !Loaded || effective.Count == 0 || input.SystemModifier || !input.Reliable) return;
            for (int key = 1; key < byKey.Length; key++)
            {
                List<Bound> entries = byKey[key]; if (entries == null || !input.IsNew(key)) continue;
                foreach (Bound bound in entries)
                    if (bound.Chord.Modifiers == input.Modifiers) bound.Action.Invoke(context);
            }
        }
        private void Compile()
        {
            effective.Clear(); errors.Clear(); Array.Clear(byKey, 0, byKey.Length);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in document.Entries)
            {
                if (!seen.Add(entry.Key)) errors[entry.Key] = "重复动作条目，原文件已保护。";
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
                    { errors[a.Key] = "多个功能使用相同组合，相关条目均已停用，原文件已保护。"; errors[b.Key] = errors[a.Key]; }
            if (errors.Count != 0) { Protected = true; Message = "快捷键文件含非法或重复绑定；相关条目停用，原件已保护。"; }
            foreach (var error in errors) effective.Remove(error.Key);
            foreach (var entry in effective)
            {
                int key = entry.Value.MainKey;
                if (byKey[key] == null) byKey[key] = new List<Bound>();
                byKey[key].Add(new Bound { Action = registry.Find(entry.Key), Chord = entry.Value });
            }
        }
        public bool Stop(int milliseconds) { return worker.Stop(milliseconds); }
        public void Dispose() { worker.Dispose(); }
    }
}
