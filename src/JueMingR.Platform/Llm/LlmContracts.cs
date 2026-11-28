using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JueMingR.Platform.Llm
{
    public interface ILlmTransport
    {
        Task<LlmTurn> CompleteAsync(LlmConnectionSnapshot connection, LlmRequest request,
            Action<string> onText, CancellationToken cancellation);
        Task<IReadOnlyList<LlmModelInfo>> DiscoverAsync(LlmConnectionSnapshot connection, CancellationToken cancellation);
    }

    public enum LlmFailure
    {
        Configuration, Disabled, Authentication, ModelUnavailable, DiscoveryUnavailable, RateLimited,
        Service, Timeout, Incomplete, Protocol, Network, Storage, Balance, Quota, PlanExpired
    }

    public interface ILlmRequestMeter
    {
        long MeasureRequest(LlmConnectionSnapshot connection, LlmRequest request);
    }

    // Errors crossing into UI contain fixed local messages only, never remote error text or exception details.
    public sealed class LlmException : Exception
    {
        public LlmException(LlmFailure failure, string message, int? status = null) : base(message)
        {
            Failure = failure;
            HttpStatus = status;
        }

        public LlmFailure Failure { get; }
        public int? HttpStatus { get; }
    }

    public sealed class LlmRequest
    {
        public LlmRequest(string instructions, IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools,
            bool textOnly = false)
        {
            Instructions = instructions ?? "";
            Messages = Array.AsReadOnly(messages.ToArray());
            Tools = Array.AsReadOnly(tools.ToArray());
            TextOnly = textOnly;
        }

        public string Instructions { get; }
        public IReadOnlyList<LlmMessage> Messages { get; }
        public IReadOnlyList<LlmToolDefinition> Tools { get; }
        // A provider may still emit tools. The runner always enforces this intent locally.
        public bool TextOnly { get; }
        public bool WasSent { get; private set; }
        public LlmUsageSample ReportedUsage { get; private set; }
        public void MarkSent() => WasSent = true;
        public void ReportUsage(LlmUsageSample usage) => ReportedUsage = usage;
    }

    public enum LlmMessageKind { User, Assistant, ToolResults }

    public sealed class LlmMessage
    {
        private LlmMessage(LlmMessageKind kind, string text, LlmTurn turn, IReadOnlyList<LlmToolResult> results,
            IReadOnlyList<LlmHistoryEntry> recoveryFacts = null)
        {
            Kind = kind;
            Text = text;
            Turn = turn;
            Results = results;
            RecoveryFacts = recoveryFacts == null || recoveryFacts.Count == 0 ? (IReadOnlyList<LlmHistoryEntry>)Array.Empty<LlmHistoryEntry>() :
                Array.AsReadOnly(recoveryFacts.ToArray());
        }

        public LlmMessageKind Kind { get; }
        public string Text { get; }
        public LlmTurn Turn { get; }
        public IReadOnlyList<LlmToolResult> Results { get; }
        // Local receipt provenance. The provider receives only the text.
        public IReadOnlyList<LlmHistoryEntry> RecoveryFacts { get; }
        public static LlmMessage User(string text, IReadOnlyList<LlmHistoryEntry> recoveryFacts = null) =>
            new LlmMessage(LlmMessageKind.User, text, null, null, recoveryFacts);
        public static LlmMessage Assistant(LlmTurn turn) => new LlmMessage(LlmMessageKind.Assistant, null, turn, null);
        public static LlmMessage ToolResults(IReadOnlyList<LlmToolResult> results) =>
            new LlmMessage(LlmMessageKind.ToolResults, null, null, Array.AsReadOnly(results.ToArray()));
    }

    public sealed class LlmTurn
    {
        public LlmTurn(string text, IReadOnlyList<LlmToolCall> toolCalls, string wireJson, string localNotice = null, LlmUsageSample usage = null)
        {
            Text = text;
            ToolCalls = Array.AsReadOnly(toolCalls.ToArray());
            WireJson = wireJson;
            LocalNotice = localNotice;
            Usage = usage;
        }

        public string Text { get; }
        public IReadOnlyList<LlmToolCall> ToolCalls { get; }
        // Only the transport reads this content. Keep it inside the model's current in-memory wire segment.
        public string WireJson { get; }
        public string LocalNotice { get; }
        public LlmUsageSample Usage { get; }
    }

    public sealed class LlmToolDefinition
    {
        public LlmToolDefinition(string name, string description, string schemaJson)
        {
            Name = name; Description = description; SchemaJson = schemaJson;
        }

        public string Name { get; }
        public string Description { get; }
        public string SchemaJson { get; }
    }

    public sealed class LlmToolCall
    {
        public LlmToolCall(string id, string name, string argumentsJson, IReadOnlyDictionary<string, object> arguments = null)
        {
            Id = id; Name = name; ArgumentsJson = argumentsJson;
            Arguments = arguments;
        }

        public string Id { get; }
        public string Name { get; }
        public string ArgumentsJson { get; }
        public IReadOnlyDictionary<string, object> Arguments { get; }
    }

    public sealed class LlmToolResult
    {
        public LlmToolResult(string id, string output, bool isError = false, string recoveryFact = null, bool repeated = false)
        {
            Id = id; Output = output; IsError = isError;
            RecoveryFact = recoveryFact;
            Repeated = repeated;
        }

        public string Id { get; }
        public string Output { get; }
        public bool IsError { get; }
        public string RecoveryFact { get; }
        public bool Repeated { get; }
    }

    public sealed class LlmHistoryEntry
    {
        public LlmHistoryEntry(Guid id, string kind, string text, DateTimeOffset time)
        {
            Id = id; Kind = kind; Text = text; Time = time;
        }

        public Guid Id { get; }
        public string Kind { get; }
        public string Text { get; }
        public DateTimeOffset Time { get; }
    }

    public sealed class LlmConnectionTest
    {
        public LlmConnectionTest(string description, bool success = true)
        {
            Description = description; Success = success;
        }

        public string Description { get; }
        public bool Success { get; }
    }

    public sealed class LlmModelInfo
    {
        public LlmModelInfo(string id, string capability)
        {
            Id = id; Capability = capability;
        }

        public string Id { get; }
        public string Capability { get; }
    }
}
