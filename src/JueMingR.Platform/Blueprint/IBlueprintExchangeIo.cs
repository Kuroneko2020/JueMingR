using System;

namespace JueMingR.Platform.Blueprint
{
    public enum BlueprintExchangeIoKind { OpenSource, Read, BeginTarget, Write, CommitTarget, Close }
    // One request and one unread acknowledgement share a single transfer buffer.
    // The caller relinquishes Buffer until its acknowledgement has been taken.
    public sealed class BlueprintExchangeIoCommand
    {
        public BlueprintExchangeIoKind Kind { get; set; }
        public IBlueprintExchangeTicket Ticket { get; set; }
        public long Offset { get; set; }
        public byte[] Buffer { get; set; }
        public int Count { get; set; }
        public byte[] ExpectedDigest { get; set; }
        public long ExpectedLength { get; set; }
    }
    public sealed class BlueprintExchangeIoReceipt
    {
        public bool Success { get; set; }
        public bool CommitUnconfirmed { get; set; }
        public bool FileCommitted { get; set; }
        public long Length { get; set; }
        public int Count { get; set; }
        public string ErrorCode { get; set; }
    }
    public interface IBlueprintExchangeIo
    {
        bool TrySubmit(BlueprintExchangeIoCommand command, Action completed);
        bool TryTake(out BlueprintExchangeIoReceipt receipt);
        void BeginStop();
        bool Stop(int milliseconds);
    }
}
