using System;

namespace JueMingR.Platform.Blueprint
{
    public enum BlueprintFileDialogKind { Import, Export }
    public enum BlueprintFileDialogOutcome { Selected, Cancelled, Failed }
    public enum BlueprintTargetDisposition { AutoCreateNew, ConfirmedReplace }

    // Paths and overwrite evidence remain mechanical capabilities. Features may
    // pass this ticket, but cannot manufacture a path or an overwrite consent.
    public interface IBlueprintExchangeTicket : IDisposable { Guid RequestId { get; } }

    public sealed class BlueprintFileDialogRequest
    {
        public readonly Guid RequestId;
        public readonly BlueprintFileDialogKind Kind;
        public readonly IntPtr OwnerWindow;
        public readonly long SessionGeneration;
        public readonly DateTime ExportDate;
        public readonly long CounterCandidate;
        public readonly string SuggestedLeaf;
        public BlueprintFileDialogRequest(Guid requestId, BlueprintFileDialogKind kind, IntPtr ownerWindow,
            long sessionGeneration, DateTime exportDate, long counterCandidate, string suggestedLeaf)
        {
            if (requestId == Guid.Empty || ownerWindow == IntPtr.Zero) throw new ArgumentException("dialog-identity");
            if (kind != BlueprintFileDialogKind.Import && kind != BlueprintFileDialogKind.Export) throw new ArgumentException("dialog-kind");
            if (kind == BlueprintFileDialogKind.Export && (counterCandidate < 1 || string.IsNullOrEmpty(suggestedLeaf))) throw new ArgumentException("dialog-name");
            RequestId = requestId; Kind = kind; OwnerWindow = ownerWindow; SessionGeneration = sessionGeneration;
            ExportDate = exportDate.Date; CounterCandidate = counterCandidate; SuggestedLeaf = suggestedLeaf;
        }
    }

    public sealed class BlueprintFileDialogResult
    {
        public readonly Guid RequestId;
        public readonly long SessionGeneration;
        public readonly BlueprintFileDialogOutcome Outcome;
        public readonly IBlueprintExchangeTicket Ticket;
        public readonly long CounterCandidate;
        public readonly string ErrorCode;
        public BlueprintFileDialogResult(BlueprintFileDialogRequest request, BlueprintFileDialogOutcome outcome,
            IBlueprintExchangeTicket ticket, long counterCandidate, string errorCode)
        {
            RequestId = request.RequestId; SessionGeneration = request.SessionGeneration; Outcome = outcome;
            Ticket = ticket; CounterCandidate = counterCandidate; ErrorCode = errorCode;
        }
    }

    public interface IBlueprintFileDialog
    {
        bool TryShow(BlueprintFileDialogRequest request);
        bool TryTake(out BlueprintFileDialogResult result);
        void RequestClose(Guid requestId);
        void BeginStop();
    }
}
