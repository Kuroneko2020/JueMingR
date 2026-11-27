using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Platform.Llm;
using static JueMingR.Infrastructure.Llm.LlmJson;

namespace JueMingR.Infrastructure.Llm
{
    internal sealed class ResponsesProtocol : LlmProtocol
    {
        private readonly SortedDictionary<int, object> finishedItems = new SortedDictionary<int, object>();

        public override Dictionary<string, object> Encode(LlmConnectionSnapshot connection, LlmConnectionSettings settings, LlmRequest request)
        {
            var input = new List<object>();
            foreach (LlmMessage entry in request.Messages)
            {
                switch (entry.Kind)
                {
                    case LlmMessageKind.User:
                        input.Add(Obj("role", "user", "content", entry.Text));
                        break;
                    case LlmMessageKind.Assistant:
                        input.AddRange(Array(Parse(entry.Turn.WireJson)));
                        break;
                    case LlmMessageKind.ToolResults:
                        foreach (LlmToolResult result in entry.Results)
                            input.Add(Obj("type", "function_call_output", "call_id", result.Id, "output", result.Output));
                        break;
                }
            }
            var body = Obj("model", settings.Model, "input", input, "stream", settings.Stream);
            if (request.Instructions.Length != 0) body["instructions"] = request.Instructions;
            // Responses output, including encrypted reasoning, is replayed from memory. No server response IDs are required.
            if (connection.Preset.Provider == "openai") body["store"] = false;
            SetOptions(body, settings, "max_output_tokens", null);
            if (!string.IsNullOrWhiteSpace(settings.Reasoning)) body["reasoning"] = Obj("effort", settings.Reasoning);
            if (request.Tools.Count != 0)
            {
                body["tools"] = request.Tools.Select(tool => Obj("type", "function", "name", tool.Name,
                    "description", tool.Description, "parameters", Schema(tool), "strict", false)).ToArray();
                if (request.TextOnly && connection.Endpoint.IdnHost == "api.openai.com") body["tool_choice"] = "none";
            }
            return body;
        }

        public override LlmTurn Decode(object response) => Finish(response, null);

        public override bool Accept(string eventName, string data, Action<string> delta, out LlmTurn turn)
        {
            turn = null;
            object frame = Parse(data);
            ThrowEmbeddedError(frame);
            string type = Text(frame, "type") ?? eventName;
            switch (type)
            {
                case "response.output_text.delta":
                case "response.refusal.delta":
                    string visible = Text(frame, "delta");
                    if (visible != null) delta(visible);
                    break;
                case "response.output_item.done":
                    int index = Number(frame, "output_index");
                    if (index < 0 || index >= 256 || Get(frame, "item") == null || finishedItems.ContainsKey(index))
                        throw Protocol();
                    finishedItems.Add(index, Get(frame, "item"));
                    break;
                case "response.failed":
                case "response.incomplete":
                    ThrowEmbeddedError(Get(frame, "response"));
                    throw Incomplete();
                case "response.completed":
                    turn = Finish(Get(frame, "response"), finishedItems);
                    return true;
            }
            return false;
        }

        private static LlmTurn Finish(object response, SortedDictionary<int, object> completedItems)
        {
            ThrowEmbeddedError(response);
            if (Text(response, "status") != "completed") throw Incomplete();
            object[] output = Array(Get(response, "output"));
            if (completedItems != null && completedItems.Count != 0)
            {
                if (output.Length == 0)
                {
                    if (completedItems.Keys.Where((index, position) => index != position).Any()) throw Incomplete();
                    output = completedItems.Values.ToArray();
                }
                else
                {
                    foreach (var item in completedItems)
                    {
                        if (item.Key >= output.Length || Text(output[item.Key], "type") != Text(item.Value, "type") ||
                            Text(output[item.Key], "id") != Text(item.Value, "id"))
                            throw Protocol();
                        // The finalized item may be the only event that carried the encrypted reasoning;
                        // fill omitted properties and reject mismatched identity instead of joining opaque fields.
                        var authoritative = RequireMap(output[item.Key]);
                        foreach (var property in RequireMap(item.Value))
                            if (Get(authoritative, property.Key) == null) authoritative[property.Key] = property.Value;
                    }
                }
            }
            var text = new System.Text.StringBuilder();
            var calls = new List<LlmToolCall>();
            foreach (object item in output)
            {
                string status = Text(item, "status");
                if (status != null && status != "completed") throw Incomplete();
                switch (Text(item, "type"))
                {
                    case "message":
                        object content = Get(item, "content");
                        text.Append(Texts(content, "output_text", "text"));
                        text.Append(Texts(content, "refusal", "refusal"));
                        break;
                    case "function_call":
                        calls.Add(Call(Text(item, "call_id"), Text(item, "name"), Text(item, "arguments")));
                        break;
                }
            }
            if (calls.Count == 0 && text.Length == 0) throw Incomplete();
            return CompleteTurn(text.ToString(), calls, output);
        }
    }
}
