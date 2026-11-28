using System;
using System.Collections.Generic;
using System.Linq;

namespace JueMingR.Platform.Llm
{
    public sealed class LlmUsageSample
    {
        public LlmUsageSample(long? input = null, long? output = null, long? total = null,
            long? cacheRead = null, long? cacheWrite = null, long? reasoning = null)
        {
            Input = input; Output = output; Total = total ?? (input + output);
            CacheRead = cacheRead; CacheWrite = cacheWrite; Reasoning = reasoning;
        }
        public long? Input { get; }
        public long? Output { get; }
        public long? Total { get; }
        public long? CacheRead { get; }
        public long? CacheWrite { get; }
        public long? Reasoning { get; }
    }

    public sealed class LlmUsageTotals
    {
        public LlmUsageTotals(long requests = 0, long unknownRequests = 0, long? input = null, long? output = null,
            long? total = null, long? cacheRead = null, long? cacheWrite = null, long? reasoning = null)
        {
            Requests = requests; UnknownRequests = unknownRequests;
            Input = input; Output = output; Total = total;
            CacheRead = cacheRead; CacheWrite = cacheWrite; Reasoning = reasoning;
        }
        public long Requests { get; }
        // Partial input/output/total reports also count as incomplete.
        public long UnknownRequests { get; }
        public long? Input { get; }
        public long? Output { get; }
        public long? Total { get; }
        public long? CacheRead { get; }
        public long? CacheWrite { get; }
        public long? Reasoning { get; }
        public LlmUsageTotals Add(LlmUsageSample sample) => Combine(new LlmUsageTotals(1,
            sample?.Input == null || sample.Output == null || sample.Total == null ? 1 : 0,
            sample?.Input, sample?.Output, sample?.Total, sample?.CacheRead, sample?.CacheWrite, sample?.Reasoning));

        public LlmUsageTotals Combine(LlmUsageTotals other) => new LlmUsageTotals(
            Sum(Requests, other.Requests), Sum(UnknownRequests, other.UnknownRequests),
            Combine(Input, other.Input), Combine(Output, other.Output), Combine(Total, other.Total),
            Combine(CacheRead, other.CacheRead), Combine(CacheWrite, other.CacheWrite), Combine(Reasoning, other.Reasoning));
        private static long? Combine(long? left, long? right) =>
            left.HasValue && right.HasValue ? Sum(left.Value, right.Value) : left ?? right;
        private static long Sum(long left, long right) => long.MaxValue - left < right ? long.MaxValue : left + right;
    }

    public sealed class LlmConnectionUsage
    {
        public LlmConnectionUsage(Guid connectionId, string connection, string model, LlmUsageTotals totals)
        { ConnectionId = connectionId; Connection = connection; Model = model; Totals = totals; }
        public Guid ConnectionId { get; }
        public string Connection { get; }
        public string Model { get; }
        public LlmUsageTotals Totals { get; }
    }

    public sealed class LlmChatUsage
    {
        private static readonly LlmUsageTotals EmptyTotals = new LlmUsageTotals();
        public static readonly LlmChatUsage Empty = new LlmChatUsage(null, null, Array.Empty<LlmConnectionUsage>());
        public LlmChatUsage(DateTimeOffset? trackingStartedAt, LlmConnectionUsage latest, IReadOnlyList<LlmConnectionUsage> connections)
        {
            TrackingStartedAt = trackingStartedAt; LatestConnection = latest;
            Connections = Array.AsReadOnly(connections.ToArray());
            Total = connections.Aggregate(EmptyTotals, (total, row) => total.Combine(row.Totals));
        }
        public DateTimeOffset? TrackingStartedAt { get; }
        public LlmConnectionUsage LatestConnection { get; }
        public LlmUsageTotals Latest => LatestConnection?.Totals ?? EmptyTotals;
        public LlmUsageTotals Total { get; }
        public IReadOnlyList<LlmConnectionUsage> Connections { get; }
    }

    public sealed class LlmChatProgress
    {
        public LlmChatProgress(long estimatedInput, int inputLimit, int modelTurns, int modelTurnLimit,
            int toolCalls, int toolCallLimit, int compactions)
        {
            EstimatedInput = estimatedInput; InputLimit = inputLimit;
            ModelTurns = modelTurns; ModelTurnLimit = modelTurnLimit;
            ToolCalls = toolCalls; ToolCallLimit = toolCallLimit; Compactions = compactions;
        }
        public long EstimatedInput { get; }
        public int InputLimit { get; }
        public int ModelTurns { get; }
        public int ModelTurnLimit { get; }
        public int ToolCalls { get; }
        public int ToolCallLimit { get; }
        public int Compactions { get; }
    }

    public sealed class LlmChatArchive
    {
        public LlmChatArchive(IReadOnlyList<LlmHistoryEntry> entries, LlmChatUsage usage)
        { Entries = Array.AsReadOnly(entries.ToArray()); Usage = usage ?? LlmChatUsage.Empty; }
        public IReadOnlyList<LlmHistoryEntry> Entries { get; }
        public LlmChatUsage Usage { get; }
    }
}
