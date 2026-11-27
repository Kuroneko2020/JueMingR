using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using JueMingR.Platform.Llm;
using static JueMingR.Infrastructure.Llm.LlmJson;

namespace JueMingR.Infrastructure.Llm
{
    internal sealed class AnthropicMessagesProtocol : LlmProtocol
    {
        private readonly SortedDictionary<int, Block> blocks = new SortedDictionary<int, Block>();
        private bool started;
        private string stopReason;

        public override Dictionary<string, object> Encode(LlmConnectionSnapshot connection, LlmConnectionSettings settings, LlmRequest request)
        {
            var messages = new List<object>();
            foreach (LlmMessage entry in request.Messages)
            {
                switch (entry.Kind)
                {
                    case LlmMessageKind.User:
                        messages.Add(Obj("role", "user", "content", entry.Text));
                        break;
                    case LlmMessageKind.Assistant:
                        messages.Add(Obj("role", "assistant", "content", Parse(entry.Turn.WireJson)));
                        break;
                    case LlmMessageKind.ToolResults:
                        messages.Add(Obj("role", "user", "content", entry.Results.Select(result =>
                            Obj("type", "tool_result", "tool_use_id", result.Id, "content", result.Output, "is_error", result.IsError)).ToArray()));
                        break;
                }
            }
            var body = Obj("model", settings.Model, "messages", messages, "stream", settings.Stream);
            if (request.Instructions.Length != 0) body["system"] = request.Instructions;
            SetOptions(body, settings, "max_tokens", "stop_sequences", 4096);
            if (request.Tools.Count != 0)
            {
                body["tools"] = request.Tools.Select(tool => Obj("name", tool.Name,
                    "description", tool.Description, "input_schema", Schema(tool))).ToArray();
                if (request.TextOnly && connection.Endpoint.IdnHost == "api.anthropic.com") body["tool_choice"] = Obj("type", "none");
            }
            if (!string.IsNullOrWhiteSpace(settings.Reasoning))
            {
                if (int.TryParse(settings.Reasoning, NumberStyles.None, CultureInfo.InvariantCulture, out int budget))
                    body["thinking"] = Obj("type", "enabled", "budget_tokens", budget);
                else
                    body["thinking"] = Obj("type", settings.Reasoning);
            }
            return body;
        }

        public override LlmTurn Decode(object response)
        {
            ThrowEmbeddedError(response);
            return Finish(Array(Get(response, "content")), Text(response, "stop_reason"));
        }

        public override bool Accept(string eventName, string data, Action<string> delta, out LlmTurn turn)
        {
            turn = null;
            object frame = Parse(data);
            ThrowEmbeddedError(frame);
            string type = Text(frame, "type") ?? eventName;
            if (type == "ping") return false;
            if (type == "message_start")
            {
                if (started) throw Protocol();
                started = true;
                stopReason = Text(Get(frame, "message"), "stop_reason");
                int offset = 0;
                foreach (object content in Array(Get(Get(frame, "message"), "content")))
                    blocks.Add(offset++, new Block(RequireMap(content)) { Closed = true });
                return false;
            }

            if (!started) throw Incomplete();
            int index = Number(frame, "index");
            if (type == "content_block_start")
            {
                if (index < 0 || index >= 256 || blocks.ContainsKey(index)) throw Protocol();
                var block = new Block(RequireMap(Get(frame, "content_block")));
                blocks.Add(index, block);
                if (block.Type == "text" && Text(block.Wire, "text") is string visible) delta(visible);
            }
            else if (type == "content_block_delta")
            {
                Block block = OpenBlock(index);
                object update = Get(frame, "delta");
                switch (Text(update, "type"))
                {
                    case "input_json_delta":
                        if (block.Type != "tool_use") throw Protocol();
                        block.ToolJson.Append(Text(update, "partial_json"));
                        break;
                    case "text_delta":
                        if (block.Type != "text") throw Protocol();
                        string text = Text(update, "text");
                        block.Append("text", text);
                        if (text != null) delta(text);
                        break;
                    case "thinking_delta":
                        block.Append("thinking", Text(update, "thinking"));
                        break;
                    case "signature_delta":
                        block.Append("signature", Text(update, "signature"));
                        break;
                    case "citations_delta":
                        var citations = Array(Get(block.Wire, "citations")).ToList();
                        citations.Add(Get(update, "citation"));
                        block.Wire["citations"] = citations;
                        break;
                    default:
                        // Preserve scalar extensions delivered in the content block rather than projecting them into public text.
                        if (Map(update) is Dictionary<string, object> fields)
                            foreach (var field in fields.Where(field => field.Key != "type"))
                                block.Wire[field.Key] = field.Value;
                        break;
                }
            }
            else if (type == "content_block_stop")
            {
                Block block = OpenBlock(index);
                if (block.ToolJson.Length != 0)
                    block.Wire["input"] = RequireMap(Parse(block.ToolJson.ToString()));
                block.Closed = true;
            }
            else if (type == "message_delta")
            {
                stopReason = Text(Get(frame, "delta"), "stop_reason") ?? stopReason;
            }
            else if (type == "message_stop")
            {
                if (blocks.Values.Any(block => !block.Closed)) throw Incomplete();
                turn = Finish(blocks.Values.Select(block => (object)block.Wire).ToArray(), stopReason);
                return true;
            }
            return false;
        }

        private Block OpenBlock(int index)
        {
            if (!blocks.TryGetValue(index, out Block block) || block.Closed) throw Protocol();
            return block;
        }

        private static LlmTurn Finish(object[] content, string reason)
        {
            if (reason != "end_turn" && reason != "tool_use" && reason != "stop_sequence")
                throw Incomplete();
            var calls = new List<LlmToolCall>();
            foreach (object block in content)
            {
                if (Text(block, "type") != "tool_use") continue;
                object arguments = Get(block, "input");
                calls.Add(Call(Text(block, "id"), Text(block, "name"), Write(arguments), arguments));
            }
            if ((reason == "tool_use") != (calls.Count != 0)) throw Incomplete();
            string text = Texts(content, "text", "text");
            if (text.Length == 0 && calls.Count == 0) throw Incomplete();
            return CompleteTurn(text, calls, content);
        }

        private sealed class Block
        {
            public Block(Dictionary<string, object> wire) { Wire = wire; }
            public Dictionary<string, object> Wire { get; }
            public string Type => Text(Wire, "type");
            public StringBuilder ToolJson { get; } = new StringBuilder();
            public bool Closed { get; set; }
            public void Append(string name, string text) => Wire[name] = Text(Wire, name) + text;
        }
    }
}
