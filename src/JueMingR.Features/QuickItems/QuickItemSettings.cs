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
        public QuickItemDocument Current { get; private set; } = QuickItemDocument.Empty;
        public bool Loaded { get; private set; }
        public bool Busy { get; private set; }
        public bool Protected { get; private set; }
        public bool CommitUnconfirmed { get; private set; }
        public long Revision { get; private set; }
        public string Message { get; private set; } = "正在加载物品设置";
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
            editing = affectedId;
            editingFavorite=affectedId==null && changeFavorite;editingQuick=affectedId==null && changeQuick;
            if (affectedId != null) suspended.Add(affectedId);
            else { favoriteSuspended |= editingFavorite && Current.KeepFavorited != value.KeepFavorited; quickSuspended |= editingQuick && Current.Enabled != value.Enabled; }
            Busy = true; Revision++; Message = "正在保存；修改中的条目暂不执行"; return true;
        }
        public void Poll()
        {
            DocumentResult<QuickItemDocument> result; if (!worker.TryTake(out result)) return;
            if (result.CommandId == 0)
            {
                Loaded = true; Protected = !result.Success;
                if (result.Success) Current = result.Value;
                Message = result.Success ? "" : "无法加载物品设置，原文件已保护。"; Revision++; return;
            }
            Busy = false; CommitUnconfirmed = result.CommitUnconfirmed; Protected = result.IsProtected || result.CommitUnconfirmed;
            if (result.Success)
            {
                Current = result.Value;
                if (editing != null) suspended.Remove(editing);
                // A successful save for the other independent toggle is not
                // permission to revive an earlier failed/suspended command.
                if(editingFavorite)favoriteSuspended=false;if(editingQuick)quickSuspended=false;
                Message = "已保存";
            }
            else Message = result.CommitUnconfirmed ? "无法确认保存结果，文件已保护；修改中的条目已停用。" :
                "保存失败，修改中的条目已停用。重新编辑可重试；重启将读取原有设置。";
            editing = null; Revision++;
        }
        public bool Stop(int milliseconds) { return worker.Stop(milliseconds); }
        public void Dispose() { worker.Dispose(); }
    }
}
