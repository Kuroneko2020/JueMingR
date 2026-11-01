using System;
using System.Globalization;
using System.Text;
using JueMingR.Platform.Blueprint;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Blueprint
{
    public enum BlueprintExchangeKind { Import, Export, PrepareExport }
    public enum BlueprintExchangePhase { Preparing, ReadingOrWriting, Validating, Publishing, Completed }
    public enum BlueprintExchangeStatus { Completed, AlreadyExists, Cancelled, Failed, CommitUnconfirmed }
    public sealed class BlueprintExportNameProposal
    {
        public Guid RequestId { get; internal set; }
        public DateTime ExportDate { get; internal set; }
        public long CounterCandidate { get; internal set; }
        public string SuggestedLeaf { get { return Leaf(ExportDate, CounterCandidate); } }
        public static string Leaf(DateTime date, long sequence)
        { if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence)); return "JM" + date.ToString("MMdd", CultureInfo.InvariantCulture) + sequence.ToString("D3", CultureInfo.InvariantCulture) + ".jmbp"; }
    }
    public sealed class BlueprintExchangeRequest
    {
        public Guid RequestId { get; set; }
        public BlueprintExchangeKind Kind { get; set; }
        public BlueprintTemplateKey TemplateKey { get; set; }
        public string Name { get; set; }
        public string SupportedTargetVersion { get; set; }
        public IBlueprintExchangeTicket Ticket { get; set; }
        public BlueprintExportNameProposal Proposal { get; set; }
        public long CounterCandidate { get; set; }
    }
    public sealed class BlueprintExchangeReceipt
    {
        public Guid RequestId { get; internal set; }
        public BlueprintExchangeKind Kind { get; internal set; }
        public BlueprintExchangeStatus Status { get; internal set; }
        public Guid AssetId { get; internal set; }
        public string Message { get; internal set; }
        public bool FileCommitted { get; internal set; }
        public bool CounterCommitted { get; internal set; }
        public BlueprintExportNameProposal Proposal { get; internal set; }
    }
    internal static class BlueprintExchangeCounter
    {
        internal static void Decode(byte[] bytes, out DateTime date, out long sequence)
        {
            var root = PreferenceJson.Read(bytes, "JueMingR.BlueprintExchangeState", "format", "version", "localDate", "lastSequence");
            string text = PreferenceJson.Required(root, "localDate", "string").Value;
            if (!DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ||
                !Int64.TryParse(PreferenceJson.Required(root, "lastSequence", "number").Value, NumberStyles.None, CultureInfo.InvariantCulture, out sequence) || sequence < 0) throw PreferenceJson.Invalid();
        }
        internal static byte[] Encode(DateTime date, long sequence)
        { return new UTF8Encoding(false, true).GetBytes("{\"format\":\"JueMingR.BlueprintExchangeState\",\"version\":1,\"localDate\":\"" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\",\"lastSequence\":" + sequence.ToString(CultureInfo.InvariantCulture) + "}\n"); }
    }
}
