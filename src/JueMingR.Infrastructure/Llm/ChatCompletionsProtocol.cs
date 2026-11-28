using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Platform.Llm;
using static JueMingR.Infrastructure.Llm.LlmJson;

namespace JueMingR.Infrastructure.Llm
{
    internal sealed class ChatCompletionsProtocol : LlmProtocol
    {
        private readonly Dictionary<string, object> assistant = Obj("role", "assistant", "content", null);
        private readonly SortedDictionary<int, Dictionary<string, object>> calls = new SortedDictionary<int, Dictionary<string, object>>();
        private string finishReason;

        public override Dictionary<string, object> Encode(LlmConnectionSnapshot connection, LlmConnectionSettings settings, LlmRequest request)
        {
            var messages = new List<object>();
            if (request.Instructions.Length != 0) messages.Add(Obj("role", "system", "content", request.Instructions));
            foreach (LlmMessage entry in request.Messages)
            {
                switch (entry.Kind)
                {
                    case LlmMessageKind.User:
                        messages.Add(Obj("role", "user", "content", entry.Text));
                        break;
                    case LlmMessageKind.Assistant:
                        messages.Add(Parse(entry.Turn.WireJson));
                        break;
                    case LlmMessageKind.ToolResults:
                        foreach (LlmToolResult result in entry.Results)
                            messages.Add(Obj("role", "tool", "tool_call_id", result.Id, "content", result.Output));
                        break;
                }
            }
            var body = Obj("model", settings.Model, "messages", messages, "stream", settings.Stream);
            if (settings.Stream && connection.Preset.Provider == "openai" && connection.Endpoint.IdnHost == "api.openai.com")
                body["stream_options"] = Obj("include_usage", true);
            string maxName = connection.Preset.Provider == "openai" ? "max_completion_tokens" : "max_tokens";
            SetOptions(body, settings, maxName, "stop");
            if (request.Tools.Count != 0)
            {
                body["tools"] = request.Tools.Select(tool => Obj("type", "function", "function",
                    Obj("name", tool.Name, "description", tool.Description, "parameters", Schema(tool)))).ToArray();
                if (request.TextOnly && (connection.Endpoint.IdnHost == "api.openai.com" ||
                    connection.Endpoint.IdnHost == "api.deepseek.com")) body["tool_choice"] = "none";
            }
            if (!string.IsNullOrWhiteSpace(settings.Reasoning))
            {
                if (connection.Preset.Provider == "deepseek" || connection.Preset.Provider == "glm")
                {
                    bool off = settings.Reasoning == "disabled" || settings.Reasoning == "none";
                    body["thinking"] = Obj("type", off ? "disabled" : "enabled");
                    if (!off && settings.Reasoning != "enabled") body["reasoning_effort"] = settings.Reasoning;
                }
                else body["reasoning_effort"] = settings.Reasoning;
            }
            return body;
        }

        public override LlmTurn Decode(object response)
        {
            ReadResponseUsage(response);
            ThrowEmbeddedError(response);
            object choice = Array(Get(response, "choices")).FirstOrDefault(row => Number(row, "index", 0) == 0);
            return Finish(RequireMap(Get(choice, "message")), Text(choice, "finish_reason"));
        }

        public override void ReadResponseUsage(object response) => ObserveUsage(ReadOpenAiUsage(Get(response, "usage"), chat: true));

        public override bool Accept(string eventName, string data, Action<string> delta, out LlmTurn turn)
        {
            turn = null;
            if (data == "[DONE]")
            {
                if (calls.Count != 0) assistant["tool_calls"] = calls.Values.ToArray();
                turn = Finish(assistant, finishReason);
                return true;
            }
            object frame = Parse(data);
            ReadResponseUsage(frame);
            ThrowEmbeddedError(frame);
            foreach (object choice in Array(Get(frame, "choices")))
            {
                if (Number(choice, "index", 0) != 0) continue;
                string stop = Text(choice, "finish_reason");
                if (stop != null) finishReason = stop;
                Dictionary<string, object> update = Map(Get(choice, "delta"));
                if (update == null) continue;
                if (Text(update, "content") is string visible) delta(visible);
                foreach (object value in Array(Get(update, "tool_calls")))
                {
                    var tool = RequireMap(value);
                    int index = Number(tool, "index");
                    if (index < 0 || index >= 32) throw Protocol();
                    if (!calls.TryGetValue(index, out Dictionary<string, object> current))
                    {
                        current = Obj();
                        calls.Add(index, current);
                    }
                    tool.Remove("index");
                    Merge(current, tool);
                }
                update.Remove("tool_calls");
                Merge(assistant, update);
            }
            return false;
        }

        private LlmTurn Finish(Dictionary<string, object> message, string reason)
        {
            if (reason != "stop" && reason != "tool_calls") throw Incomplete();
            if (Text(message, "role") != "assistant") throw Protocol();
            var tools = new List<LlmToolCall>();
            foreach (object raw in Array(Get(message, "tool_calls")))
            {
                object function = Get(raw, "function");
                if (Text(raw, "type") != "function") throw Protocol();
                tools.Add(Call(Text(raw, "id"), Text(function, "name"), Text(function, "arguments")));
            }
            if ((reason == "tool_calls") != (tools.Count != 0)) throw Incomplete();
            string text = Text(message, "content") ?? Texts(Get(message, "content"), "text", "text");
            if (string.IsNullOrWhiteSpace(text) && tools.Count == 0 && !LlmJson.Has(message, "refusal"))
                throw Incomplete();
            if (string.IsNullOrWhiteSpace(text) && tools.Count == 0) text = Text(message, "refusal");
            return CompleteTurn(text, tools, message);
        }

        // Streamed private fields are part of the assistant wire message. Preserve their original nesting and
        // tool index, including extensions attached to otherwise empty text updates.
        private static void Merge(Dictionary<string, object> target, Dictionary<string, object> update)
        {
            foreach (var pair in update)
            {
                if (pair.Value == null && target.ContainsKey(pair.Key)) continue;
                if (Map(pair.Value) is Dictionary<string, object> fields)
                {
                    if (!(Map(Get(target, pair.Key)) is Dictionary<string, object> existing))
                        target[pair.Key] = existing = Obj();
                    Merge(existing, fields);
                    continue;
                }
                if (pair.Value is string fragment && Text(target, pair.Key) is string previous &&
                    (pair.Key == "arguments" || pair.Key == "content" || pair.Key == "reasoning_content" ||
                     pair.Key == "reasoning" || pair.Key == "encrypted_content" || pair.Key == "refusal" || pair.Key == "name"))
                {
                    target[pair.Key] = previous + fragment;
                    continue;
                }
                target[pair.Key] = pair.Value;
            }
        }
    }
}
