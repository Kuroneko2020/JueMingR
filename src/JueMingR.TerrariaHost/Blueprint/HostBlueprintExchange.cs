using System;
using System.IO;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Blueprint;
using Terraria;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private IBlueprintFileDialog exchangeDialog;
        private Guid exchangeRequest;
        private long exchangeSession, exchangeUi;
        private BlueprintTemplateKey exchangeTemplate;
        private string exchangeName, exchangeVersion;
        private BlueprintExportNameProposal exchangeProposal;
        private bool exchangeShowing;
        internal bool ExchangeBusy { get { return exchangeRequest != Guid.Empty || Library?.ExchangeBusy == true; } }
        internal string ExchangeDisabledReason
        {
            get {
                if (!ControlsEnabled || Library == null || exchangeDialog == null) return "蓝图文件交换暂不可用";
                if (ExchangeBusy) return "正在处理蓝图文件，请稍候";
                if (HasActiveCaptureWork || ProjectionEditing || pendingProjection != null) return "请先完成或取消当前蓝图操作";
                if (Library.Snapshot.Busy) return "蓝图库正在处理，请稍候";
                if (typeof(Main).Assembly.GetName().Version == null) return "游戏版本信息尚未就绪";
                return null;
            }
        }
        private void InitializeExchange(string directory)
        {
            if (Library == null) return;
            Library.ConfigureExchange(new FileBlueprintExchange(), new AtomicFileDocument(Path.Combine(directory, "JueMingRData", "blueprints", "exchange-state.json"), 65536, true));
            exchangeDialog = new WindowsBlueprintFileDialog();
        }
        internal void BeginImport() { BeginExchange(null, null); }
        internal void BeginExport(BlueprintTemplateKey key, string name) { if (key != null) BeginExchange(key, name); }
        private void BeginExchange(BlueprintTemplateKey key, string name)
        {
            if (ExchangeDisabledReason != null || !Input.CanStartActions) return;
            exchangeRequest = Guid.NewGuid(); exchangeSession = generation; exchangeUi = Shell?.BlueprintUi?.ExchangePresentationGeneration ?? -1;
            // versionNumber is display text (it may contain a leading 'v').
            // The verified host assembly owns the canonical target identity.
            exchangeTemplate = key; exchangeName = name; exchangeVersion = typeof(Main).Assembly.GetName().Version.ToString(); exchangeProposal = null;
            Pointer.Cancel(); Toolbar.Cancel(); State.Interrupt();
            if (key == null) { if (Library.TryReserveExchange(exchangeRequest)) ShowExchangeDialog(); else RetireExchange(); }
            else if (!Library.TryBeginExchange(new BlueprintExchangeRequest { RequestId = exchangeRequest, Kind = BlueprintExchangeKind.PrepareExport }))
            { projectionFeedback = "蓝图库正在处理，请稍候"; RetireExchange(); }
        }
        private void ShowExchangeDialog()
        {
            Guid id = exchangeRequest;
            if (!HasSession || exchangeSession != generation || !Input.TryAcquireExternalModal(id)) { Library.CancelExchange(id); RetireExchange(); return; }
            IntPtr owner = Input.CaptureGameWindow();
            if (owner == IntPtr.Zero) { Input.ReturnExternalModal(id); Library.CancelExchange(id); projectionFeedback = "暂时无法打开文件窗口，请重新尝试"; RetireExchange(); return; }
            var request = new BlueprintFileDialogRequest(id, exchangeTemplate == null ? BlueprintFileDialogKind.Import : BlueprintFileDialogKind.Export, owner, exchangeSession,
                exchangeProposal?.ExportDate ?? default(DateTime), exchangeProposal?.CounterCandidate ?? 0, exchangeProposal?.SuggestedLeaf);
            if (!exchangeDialog.TryShow(request)) { Input.ReturnExternalModal(id); Library.CancelExchange(id); projectionFeedback = "暂时无法打开文件窗口，请重新尝试"; RetireExchange(); return; }
            exchangeShowing = true;
        }
        private void PumpExchange()
        {
            BlueprintFileDialogResult selected;
            if (exchangeDialog != null && exchangeDialog.TryTake(out selected))
            {
                Input.ReturnExternalModal(selected.RequestId); exchangeShowing = false;
                bool current = selected.RequestId == exchangeRequest && selected.SessionGeneration == exchangeSession && HasSession && generation == exchangeSession;
                if (!current || selected.Outcome != BlueprintFileDialogOutcome.Selected)
                {
                    selected.Ticket?.Dispose(); Library?.CancelExchange(selected.RequestId);
                    if (current && selected.Outcome == BlueprintFileDialogOutcome.Failed) projectionFeedback = "未能打开或选择蓝图文件，原件已保留";
                    if (selected.RequestId == exchangeRequest) RetireExchange();
                }
                else
                {
                    var request = new BlueprintExchangeRequest { RequestId = exchangeRequest, Kind = exchangeTemplate == null ? BlueprintExchangeKind.Import : BlueprintExchangeKind.Export,
                        TemplateKey = exchangeTemplate, Name = exchangeName, SupportedTargetVersion = exchangeVersion, Ticket = selected.Ticket, Proposal = exchangeProposal, CounterCandidate = selected.CounterCandidate };
                    if (!Library.TryBeginExchange(request)) { selected.Ticket.Dispose(); Library.CancelExchange(exchangeRequest); projectionFeedback = "未能开始文件操作，请重新尝试"; RetireExchange(); }
                }
            }
            BlueprintExchangeReceipt receipt;
            if (Library == null || !Library.TryTakeExchangeReceipt(out receipt)) return;
            bool owns = receipt.RequestId == exchangeRequest;
            if (receipt.Kind == BlueprintExchangeKind.PrepareExport && receipt.Status == BlueprintExchangeStatus.Completed)
            {
                if (owns && HasSession && exchangeSession == generation) { exchangeProposal = receipt.Proposal; ShowExchangeDialog(); }
                else { Library.CancelExchange(receipt.RequestId); if (owns) RetireExchange(); }
                return;
            }
            // Durable results remain facts in menus or a later session. Only
            // the still-current presentation can change selection; no receipt
            // reopens F5 or starts a placement on behalf of an old world.
            if (!String.IsNullOrEmpty(receipt.Message)) projectionFeedback = receipt.Message;
            if (owns && exchangeSession == generation && HasSession && receipt.Kind == BlueprintExchangeKind.Import &&
                (receipt.Status == BlueprintExchangeStatus.Completed || receipt.Status == BlueprintExchangeStatus.AlreadyExists))
                Shell?.BlueprintUi?.AcceptExchangeAsset(receipt.AssetId, exchangeUi);
            if (owns) RetireExchange();
        }
        private void CancelSessionExchange()
        {
            if (exchangeRequest == Guid.Empty) return;
            Library?.CancelExchange(exchangeRequest);
            if (exchangeShowing) exchangeDialog?.RequestClose(exchangeRequest);
            // Keep request identity until its accepted write/dialog reports.
            // This cannot transfer its presentation rights to the new session.
        }
        private void RetireExchange() { exchangeRequest = Guid.Empty; exchangeTemplate = null; exchangeName = exchangeVersion = null; exchangeProposal = null; exchangeShowing = false; }
    }
}
