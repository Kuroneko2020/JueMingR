using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using JueMingR.Platform.Llm;
using static JueMingR.Infrastructure.Llm.LlmJson;

namespace JueMingR.Infrastructure.Llm
{
    internal abstract class LlmProtocol
    {
        public static LlmProtocol For(string protocol)
        {
            switch (protocol)
            {
                case "openai_chat": return new ChatCompletionsProtocol();
                case "openai_responses": return new ResponsesProtocol();
                case "anthropic_messages": return new AnthropicMessagesProtocol();
                default: throw new LlmException(LlmFailure.Configuration, "未选择受支持的协议。");
            }
        }

        public abstract Dictionary<string, object> Encode(LlmConnectionSnapshot connection, LlmConnectionSettings settings, LlmRequest request);
        public abstract LlmTurn Decode(object response);
        public abstract bool Accept(string eventName, string data, Action<string> delta, out LlmTurn turn);
        public LlmUsageSample ObservedUsage { get; protected set; }
        public abstract void ReadResponseUsage(object response);

        protected void ObserveUsage(LlmUsageSample sample)
        {
            // Streaming events are cumulative snapshots of one request.
            // Preserve fields omitted from a later snapshot without summing events.
            if (sample == null) return;
            var previous = ObservedUsage;
            ObservedUsage = new LlmUsageSample(sample.Input ?? previous?.Input, sample.Output ?? previous?.Output,
                sample.Total ?? (sample.Input == null && sample.Output == null ? previous?.Total : null),
                sample.CacheRead ?? previous?.CacheRead, sample.CacheWrite ?? previous?.CacheWrite,
                sample.Reasoning ?? previous?.Reasoning);
        }

        protected static long? UsageAmount(object data, string field)
        {
            object value = Get(data, field);
            if (value is int integer && integer >= 0) return integer;
            if (value is long longer && longer >= 0 && longer <= int.MaxValue) return longer;
            return null;
        }

        protected static LlmUsageSample ReadOpenAiUsage(object data, bool chat)
        {
            if (Map(data) == null) return null;
            string input = chat ? "prompt_tokens" : "input_tokens";
            string output = chat ? "completion_tokens" : "output_tokens";
            var cached = UsageAmount(Get(data, input + "_details"), "cached_tokens");
            // DeepSeek reports prompt_tokens inclusive of its cache split,
            // same as OpenAI's cached_tokens detail within input.
            return new LlmUsageSample(UsageAmount(data, input), UsageAmount(data, output), UsageAmount(data, "total_tokens"),
                cached ?? (chat ? UsageAmount(data, "prompt_cache_hit_tokens") : null), null,
                UsageAmount(Get(data, output + "_details"), "reasoning_tokens"));
        }

        protected static Dictionary<string, object> Obj(params object[] pairs)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < pairs.Length; i += 2) result.Add((string)pairs[i], pairs[i + 1]);
            return result;
        }

        protected static object Schema(LlmToolDefinition tool) => RequireMap(Parse(tool.SchemaJson));

        protected static void SetOptions(Dictionary<string, object> body, LlmConnectionSettings settings, string maxName, string stopName,
            int? defaultMax = null)
        {
            if (settings.MaxOutput.HasValue || defaultMax.HasValue) body[maxName] = settings.MaxOutput ?? defaultMax;
            if (settings.Temperature.HasValue) body["temperature"] = settings.Temperature.Value;
            if (settings.TopP.HasValue) body["top_p"] = settings.TopP.Value;
            if (!string.IsNullOrWhiteSpace(settings.StopSequences) && stopName != null)
            {
                body[stopName] = settings.StopSequences.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim('\r')).ToArray();
            }
        }

        protected static LlmToolCall Call(string id, string name, string json, object arguments = null)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || string.IsNullOrWhiteSpace(name) ||
                name.Length > 128 || string.IsNullOrWhiteSpace(json) || json.Length > 32768)
                throw Protocol();
            Dictionary<string, object> parsed = RequireMap(arguments ?? Parse(json));
            return new LlmToolCall(id, name, json, Freeze(parsed));
        }

        private static ReadOnlyDictionary<string, object> Freeze(Dictionary<string, object> parsed) =>
            new ReadOnlyDictionary<string, object>(parsed.ToDictionary(pair => pair.Key, pair => FreezeValue(pair.Value), StringComparer.Ordinal));

        private static object FreezeValue(object value)
        {
            if (Map(value) is Dictionary<string, object> nested) return Freeze(nested);
            if (value is System.Collections.IList) return System.Array.AsReadOnly(Array(value).Select(FreezeValue).ToArray());
            return value;
        }

        protected LlmTurn CompleteTurn(string text, List<LlmToolCall> calls, object wire)
        {
            if (calls.Count > 32 || calls.Select(call => call.Id).Distinct(StringComparer.Ordinal).Count() != calls.Count)
                throw Protocol();
            return new LlmTurn(text ?? "", calls, Write(wire), usage: ObservedUsage);
        }

        protected static void ThrowEmbeddedError(object message)
        {
            object error = Get(message, "error");
            if (error == null && Text(message, "type") != "error") return;
            throw LlmProviderFailure.Classify(message) ??
                new LlmException(LlmFailure.Service, "服务在回复中报告错误，本次未提交新工具动作。");
        }
        protected static string Texts(object content, string type, string key)
        {
            var text = new StringBuilder();
            foreach (object block in Array(content))
                if (Text(block, "type") == type) text.Append(Text(block, key));
            return text.ToString();
        }
    }
}
