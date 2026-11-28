using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JueMingR.Platform.Llm;

namespace JueMingR.Features.Llm
{
    public static class CompanionChat
    {
        private static readonly LlmToolDefinition[] TestTools =
        {
            new LlmToolDefinition("check_connection", "Return a local connection test marker.",
                "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}")
        };

        // Keep a completed assistant tool group and ALL its results together.
        // The caller commits the working wire segment only on normal completion.
        public static async Task<LlmTurn> RunAsync(ILlmTransport transport, LlmConnectionSnapshot connection,
            string instructions, List<LlmMessage> wire, IReadOnlyList<LlmToolDefinition> tools,
            Func<LlmToolCall, CancellationToken, Task<LlmToolResult>> execute, Action<string> text,
            CancellationToken cancellation, string unavailableReason = null, string originalText = null,
            Action<LlmChatProgress> progress = null, Action<LlmUsageSample> usage = null)
        {
            int remaining = Math.Max(0, connection.MaxToolCalls);
            int maxTurns = connection.MaxModelTurns;
            tools = CanOfferGameTools(connection) ? tools : Array.Empty<LlmToolDefinition>();
            int closedCount = wire.Count != 0 && wire[wire.Count - 1].Kind == LlmMessageKind.User ? wire.Count - 1 : wire.Count;
            int dispatched = 0;
            int compactions = 0;
            int repeatedRounds = 0;
            string notice = null;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var observed = new List<(int Priority, string Text)>();
            for (int round = 0; round < maxTurns; round++)
            {
                cancellation.ThrowIfCancellationRequested();
                string limit = tools.Count == 0
                    ? unavailableReason ?? GameToolsUnavailable(connection) ?? "本轮没有可用的游戏工具。"
                    : remaining == 0 ? "已达到本轮工具调用次数上限。"
                    : round + 1 == maxTurns ? "已达到本轮模型往返次数上限。"
                    : repeatedRounds >= 2 ? "重复查询没有得到新资料，请根据已核实的回执回答。" : null;
                // Preserve the schema and completed tool pairs for private
                // reasoning replay. Text-only intent never grants execution.
                string requestInstructions = limit == null ? instructions : instructions + "\n请只用文字回复。当前不接受工具调用。" + limit;
                var request = new LlmRequest(requestInstructions, wire, tools, textOnly: limit != null);
                long size = MeasureRequest(transport, connection, request);
                if (originalText != null && wire.Count > 1 && size > connection.InputBudget - Math.Min(960, connection.InputBudget / 10))
                {
                    request = Compact(transport, connection, requestInstructions, wire, tools, limit != null, originalText, observed, out size);
                    closedCount = 0;
                    ++compactions;
                }
                if (size > connection.InputBudget && originalText != null)
                {
                    const string textOnly = "本轮上下文余量只允许文字收尾。";
                    request = Compact(transport, connection, instructions + "\n" + textOnly, wire, Array.Empty<LlmToolDefinition>(),
                        true, originalText, observed, out size);
                    closedCount = 0;
                    ++compactions;
                    limit = textOnly;
                }
                progress?.Invoke(new LlmChatProgress(size, connection.InputBudget, round, maxTurns,
                    connection.MaxToolCalls - remaining, connection.MaxToolCalls, compactions));
                if (size > connection.InputBudget)
                    return FinishLocally(wire, closedCount, dispatched, "本轮上下文已达到设定上限。最小请求仍需要 " +
                        size + " / " + connection.InputBudget + " 个 UTF-16 单位。");
                LlmTurn turn = null;
                try
                {
                    if (!(transport is ILlmRequestMeter)) request.MarkSent();
                    turn = await transport.CompleteAsync(connection, request, text, cancellation).ConfigureAwait(false);
                }
                finally
                {
                    if (request.WasSent) usage?.Invoke(turn?.Usage ?? request.ReportedUsage);
                    progress?.Invoke(new LlmChatProgress(size, connection.InputBudget, round + (request.WasSent ? 1 : 0), maxTurns,
                        connection.MaxToolCalls - remaining, connection.MaxToolCalls, compactions));
                }
                cancellation.ThrowIfCancellationRequested();
                if (turn.ToolCalls.Count == 0)
                {
                    wire.Add(LlmMessage.Assistant(turn));
                    return notice == null ? turn : new LlmTurn(turn.Text, turn.ToolCalls, turn.WireJson, notice, turn.Usage);
                }
                if (limit != null)
                    return FinishLocally(wire, closedCount, dispatched, limit + "模型提出的额外工具调用未执行。");
                foreach (var call in turn.ToolCalls)
                    if (String.IsNullOrEmpty(call.Id) || !seen.Add(call.Id))
                        throw new LlmException(LlmFailure.Protocol, "模型返回了无法关联的工具调用。");

                var results = new List<LlmToolResult>(turn.ToolCalls.Count);
                foreach (var call in turn.ToolCalls)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (remaining == 0)
                    {
                        notice = "已达到本轮工具调用次数上限。超出的调用未执行。";
                        results.Add(new LlmToolResult(call.Id, "rejected：" + notice, true));
                    }
                    else
                    {
                        remaining--;
                        if (Offered(tools, call.Name))
                        {
                            dispatched++;
                            var result = await execute(call, cancellation).ConfigureAwait(false);
                            results.Add(result);
                            RememberOfficialResult(observed, call.Name, result);
                        }
                        else results.Add(new LlmToolResult(call.Id, "rejected：模型请求了本轮未提供的工具。", true));
                    }
                }
                wire.Add(LlmMessage.Assistant(turn));
                wire.Add(LlmMessage.ToolResults(results));
                repeatedRounds = results.All(result => result.Repeated) ? repeatedRounds + 1 : 0;
                progress?.Invoke(new LlmChatProgress(size, connection.InputBudget, round + 1, maxTurns,
                    connection.MaxToolCalls - remaining, connection.MaxToolCalls, compactions));
            }
            return FinishLocally(wire, closedCount, dispatched, "已达到本轮模型往返次数上限。");
        }

        private static void RememberOfficialResult(List<(int Priority, string Text)> facts, string name, LlmToolResult result)
        {
            string fact = result.RecoveryFact ?? result.Output;
            if (String.IsNullOrWhiteSpace(fact)) return;
            if (fact.Length > 740) fact = fact.Substring(0, 739) + "…";
            fact = name + " · " + fact;
            if (facts.Any(entry => entry.Text == fact)) return;
            int priority = name == "list_capabilities" || name == "describe_capability" || result.Repeated ? 0 :
                name.StartsWith("observe_", StringComparison.Ordinal) || name.StartsWith("find_", StringComparison.Ordinal) ||
                name.StartsWith("query_", StringComparison.Ordinal) ? 1 : 2;
            if (facts.Count >= 32)
            {
                var weakest = facts.Min(entry => entry.Priority);
                if (weakest > priority) return;
                facts.RemoveAt(facts.FindIndex(entry => entry.Priority == weakest));
            }
            facts.Add((priority, fact));
        }

        private static LlmRequest Compact(ILlmTransport transport, LlmConnectionSnapshot connection, string instructions,
            List<LlmMessage> wire, IReadOnlyList<LlmToolDefinition> tools, bool textOnly, string original,
            List<(int Priority, string Text)> facts, out long size)
        {
            // Never split an assistant response (especially signed thinking)
            // from any of its results. A fresh segment starts with the intact
            // player message and factual local observations instead.
            var lastPair = wire.Count >= 3 && wire[wire.Count - 1].Kind == LlmMessageKind.ToolResults &&
                wire[wire.Count - 2].Kind == LlmMessageKind.Assistant
                ? wire.Skip(wire.Count - 2).ToArray() : Array.Empty<LlmMessage>();
            wire.Clear();
            string header = original + "\n\n[同一玩家消息的本机回执。使用原发送位置和同一授权。切勿重复已处理的动作。]";
            var kept = new List<string>();
            wire.Add(LlmMessage.User(facts.Count == 0 ? original : header));
            var request = new LlmRequest(instructions, wire, tools, textOnly);
            size = MeasureRequest(transport, connection, request);
            if (size > connection.InputBudget && facts.Count != 0)
            {
                wire[0] = LlmMessage.User(original);
                request = new LlmRequest(instructions, wire, tools, textOnly);
                size = MeasureRequest(transport, connection, request);
            }
            int room = (int)Math.Max(0, Math.Min(3000, connection.InputBudget - size - 180));
            foreach (int index in Enumerable.Range(0, facts.Count).OrderByDescending(index => facts[index].Priority)
                .ThenByDescending(index => index))
            {
                string fact = facts[index].Text;
                if (fact.Length + 1 > room) continue;
                kept.Add(fact);
                room -= fact.Length + 1;
            }
            if (kept.Count != 0)
            {
                wire[0] = LlmMessage.User(header + "\n" + String.Join("\n", kept));
                request = new LlmRequest(instructions, wire, tools, textOnly);
                size = MeasureRequest(transport, connection, request);
                while (size > connection.InputBudget && kept.Count != 0)
                {
                    kept.RemoveAt(kept.Count - 1);
                    wire[0] = LlmMessage.User(kept.Count == 0 ? original : header + "\n" + String.Join("\n", kept));
                    request = new LlmRequest(instructions, wire, tools, textOnly);
                    size = MeasureRequest(transport, connection, request);
                }
            }
            if (lastPair.Length != 0 && tools.Count != 0)
            {
                var withPair = new LlmRequest(instructions, wire.Concat(lastPair).ToArray(), tools, textOnly);
                long pairSize = MeasureRequest(transport, connection, withPair);
                if (pairSize <= connection.InputBudget - Math.Min(240, connection.InputBudget / 16))
                {
                    wire.AddRange(lastPair);
                    size = pairSize;
                    return withPair;
                }
            }
            return request;
        }

        public static long MeasureRequest(ILlmTransport transport, LlmConnectionSnapshot connection, LlmRequest request) =>
            transport is ILlmRequestMeter meter ? meter.MeasureRequest(connection, request) :
                MeasureWire(request.Messages) + MeasureEnvelope(request.Instructions, request.Tools);

        private static bool Offered(IReadOnlyList<LlmToolDefinition> tools, string name)
        {
            foreach (var tool in tools)
                if (String.Equals(tool.Name, name, StringComparison.Ordinal)) return true;
            return false;
        }

        private static LlmTurn FinishLocally(List<LlmMessage> wire, int closedCount, int dispatched, string reason)
        {
            if (dispatched != 0) reason += "已处理的游戏请求以聊天中的查询和操作回执为准。";
            // Local text cannot impersonate a provider assistant block or its
            // private fields. Resume from earlier closed turns and public facts.
            wire.RemoveRange(closedCount, wire.Count - closedCount);
            return new LlmTurn("", Array.Empty<LlmToolCall>(), null, reason);
        }

        public static bool CanOfferGameTools(LlmConnectionSnapshot connection) => GameToolsUnavailable(connection) == null;

        public static string GameToolsUnavailable(LlmConnectionSnapshot connection)
        {
            if (!connection.GameActionsEnabled) return "当前模型连接已关闭游戏行动。";
            if (connection.MaxModelTurns < 2) return "模型往返次数设为 1，本轮仅聊天。";
            if (connection.MaxToolCalls < 1) return "工具调用次数设为 0，本轮仅聊天。";
            return null;
        }

        public static long MeasureWire(IEnumerable<LlmMessage> messages)
        {
            long size = 0;
            foreach (var message in messages)
            {
                size += message.Text?.Length ?? 0;
                if (message.Turn != null)
                {
                    size += message.Turn.WireJson?.Length ?? message.Turn.Text?.Length ?? 0;
                    if (message.Turn.WireJson == null)
                        foreach (var call in message.Turn.ToolCalls)
                            size += (call.Id?.Length ?? 0) + (call.Name?.Length ?? 0) + (call.ArgumentsJson?.Length ?? 0);
                }
                if (message.Results != null)
                    foreach (var result in message.Results)
                        size += (result.Id?.Length ?? 0) + (result.Output?.Length ?? 0);
            }
            return size;
        }

        // Counts UTF-16 text units as a local input estimate; it does not
        // predict service-provider tokens or exclude their encoding overhead.
        public static long MeasureEnvelope(string instructions, IReadOnlyList<LlmToolDefinition> tools)
        {
            long size = instructions?.Length ?? 0;
            foreach (var tool in tools) size += tool.Name.Length + tool.Description.Length + tool.SchemaJson.Length;
            return size;
        }

        public static async Task<LlmConnectionTest> TestToolsAsync(ILlmTransport transport,
            LlmConnectionSnapshot connection, CancellationToken token)
        {
            string unavailable = GameToolsUnavailable(connection);
            if (unavailable != null) return new LlmConnectionTest(unavailable, false);
            int executed = 0;
            var wire = new List<LlmMessage> { LlmMessage.User("Call check_connection once, then report its returned marker.") };
            var final = await RunAsync(transport, connection, "This is a connection test. Use the supplied test tool once.",
                wire, TestTools, (call, _) =>
                {
                    if (call.Name != "check_connection" || call.Arguments == null || call.Arguments.Count != 0 || executed != 0)
                        return Task.FromResult(new LlmToolResult(call.Id, "invalid test call", true));
                    executed++;
                    return Task.FromResult(new LlmToolResult(call.Id, "JMR_LOCAL_TOOL_OK", false));
                }, null, token).ConfigureAwait(false);
            bool succeeded = executed == 1 && final.LocalNotice == null && !String.IsNullOrWhiteSpace(final.Text);
            return new LlmConnectionTest(succeeded ? "工具调用、回填和最终回复均已完成。" : "模型未完成测试工具闭环。", succeeded);
        }
    }
}
