using System;
using System.Collections.Generic;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.QuickItems
{
    // Game-thread state owner. A UI draft and a mechanical file command never
    // become executable entries until the corresponding reliable commit arrives.
    public sealed class QuickItemSettings : IDisposable
    {
        private readonly DocumentWorker<QuickItemDocument> worker;
        private readonly HashSet<string> suspended = new HashSet<string>(StringComparer.Ordinal);
        private long command;
        private string editing;
        private bool favoriteSuspended, quickSuspended, editingFavorite, editingQuick;
        private sealed class Error
        {
            internal readonly string Text;
            internal bool Reported;
            internal Error(string text) { Text = text; }
            internal void Report(Action<string> display) { if (!Reported) { display(Text); Reported = true; } }
        }
        private Error fileError, favoriteError, quickError, draftError;
        private bool adding;
        private readonly Dictionary<string, Error> entryErrors = new Dictionary<string, Error>(StringComparer.Ordinal);
        private bool feedbackPending;
        public QuickItemDocument Current { get; private set; } = QuickItemDocument.Empty;
        public bool Loaded { get; private set; }
        public bool Busy { get; private set; }
        public bool Protected { get; private set; }
        public bool CommitUnconfirmed { get; private set; }
        public long Revision { get; private set; }
        // Read-only command receipts; Poll remains the sole worker consumer.
        // A suspended false value alone does not prove a successful disable.
        public long AcceptedCommandId { get; private set; }
        public long CompletedCommandId { get; private set; }
        public bool CompletionSucceeded { get; private set; }
        public string Message { get; private set; }
        public string FavoriteMessage { get; private set; }
        public string QuickMessage { get; private set; }
        public bool Enabled { get { return Loaded && !quickSuspended && Current.Enabled; } }
        public bool KeepFavorited { get { return Loaded && !favoriteSuspended && Current.KeepFavorited; } }
        public bool CanExecute(string id) { var entry = Current.Find(id); return Enabled && entry != null && entry.Enabled && !suspended.Contains(id); }
        public bool Registered(string id) { return Loaded && Current.Find(id) != null && !suspended.Contains(id); }
        public QuickItemSettings(IPreferenceStorage storage)
        { worker = new DocumentWorker<QuickItemDocument>(storage, QuickItemDocument.Decode, QuickItemDocument.Encode, QuickItemDocument.Empty); }
        public bool TryChange(QuickItemDocument value, string affectedId, out string reason, bool changeFavorite=true, bool changeQuick=true)
        {
            reason = null;
            if (!Loaded || Busy || Protected) { reason = !Loaded ? "正在加载，请稍候。" : Busy ? "上一项还在保存，请稍候。" : "设置文件已保护，暂时无法修改。"; return false; }
            if(!changeFavorite && value.KeepFavorited!=Current.KeepFavorited || !changeQuick && value.Enabled!=Current.Enabled)
            {reason="此次操作不能修改另一项开关。";return false;}
            if (affectedId != null && value.Find(affectedId) == null && Current.Find(affectedId) == null) { reason = "此条目已经不存在。"; return false; }
            try { QuickItemDocument.Encode(value); } catch (Exception e) when (e is ArgumentException || e is PreferenceFormatException)
            { reason = "设置内容无效或超出容量。"; return false; }
            if (!worker.TrySubmit(++command, value)) { reason = "暂时无法保存。"; return false; }
            AcceptedCommandId = command;
            editing = affectedId;
            adding = affectedId != null && Current.Find(affectedId) == null;
            editingFavorite=affectedId==null && changeFavorite;editingQuick=affectedId==null && changeQuick;
            if (affectedId != null) suspended.Add(affectedId);
            else { favoriteSuspended |= editingFavorite && Current.KeepFavorited != value.KeepFavorited; quickSuspended |= editingQuick && Current.Enabled != value.Enabled; }
            Busy = true; Revision++; return true;
        }
        public void Poll()
        {
            DocumentResult<QuickItemDocument> result; if (!worker.TryTake(out result)) return;
            if (result.CommandId == 0)
            {
                Loaded = true; Protected = !result.Success;
                if (result.Success) Current = result.Value;
                if (!result.Success) { fileError = new Error("无法加载物品设置，原文件已保护。"); feedbackPending = true; }
                RefreshMessages(); Revision++; return;
            }
            Busy = false; CommitUnconfirmed = result.CommitUnconfirmed; Protected = result.IsProtected || result.CommitUnconfirmed;
            CompletedCommandId = result.CommandId; CompletionSucceeded = result.Success;
            if (result.Success)
            {
                Current = result.Value;
                if (editing != null) suspended.Remove(editing);
                // A successful save for the other independent toggle is not
                // permission to revive an earlier failed/suspended command.
                if(editingFavorite)favoriteSuspended=false;if(editingQuick)quickSuspended=false;
                if (editing != null)
                {
                    entryErrors.Remove(editing);
                    // The UI creates a fresh ID when Add is reopened; an unsaved
                    // draft has no persistent editable identity. A reliable new
                    // Add replaces that single error, unlike existing-item edits.
                    if (adding) draftError = null;
                }
                if (editingFavorite) favoriteError = null;
                if (editingQuick) quickError = null;
            }
            else
            {
                if (Protected) fileError = new Error(result.CommitUnconfirmed ? "物品设置保存结果未确认，文件已保护；修改中的项目已暂停。" :
                    "物品设置无法保存，文件已保护；修改中的项目已暂停。");
                else if (editing != null)
                {
                    var error = new Error("快捷物品保存失败，修改的条目已暂停。重新编辑可重试。");
                    // Persisted entries are bounded by the document. Failed new
                    // drafts share one slot, so repeated failed adds cannot grow
                    // a second unbounded history of entries that never existed.
                    if (Current.Find(editing) != null) entryErrors[editing] = error;
                    else draftError = new Error("快捷物品添加失败，条目尚未保存。可重新添加。");
                }
                else
                {
                    if (editingFavorite) favoriteError = new Error("保持收藏设置保存失败，已暂停。重新开启可重试。");
                    if (editingQuick) quickError = new Error("快捷物品开关保存失败，已暂停。重新开启可重试。");
                }
                feedbackPending = true;
            }
            // Error retirement follows the same scope as the existing suspend
            // flags. Saving a different toggle/entry is never proof of recovery.
            editing = null; RefreshMessages(); Revision++;
        }
        private void RefreshMessages()
        {
            FavoriteMessage = fileError?.Text ?? favoriteError?.Text;
            QuickMessage = fileError?.Text ?? quickError?.Text ?? draftError?.Text;
            if (QuickMessage == null) foreach (var error in entryErrors.Values) { QuickMessage = error.Text; break; }
            Message = FavoriteMessage ?? QuickMessage;
        }
        public void TakeFeedback(Action<string> display)
        {
            if (!feedbackPending) return;
            fileError?.Report(display); favoriteError?.Report(display); quickError?.Report(display); draftError?.Report(display);
            foreach (var error in entryErrors.Values) error.Report(display);
            feedbackPending = false;
        }
        public bool Stop(int milliseconds) { return worker.Stop(milliseconds); }
        public void Dispose() { worker.Dispose(); }
    }
}
