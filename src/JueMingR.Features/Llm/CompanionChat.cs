using System;
using System.Collections.Generic;
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
            CancellationToken cancellation, string unavailableReason = null)
        {
            int remaining = Math.Max(0, connection.MaxToolCalls);
            int maxTurns = connection.MaxModelTurns;
            tools = CanOfferGameTools(connection) ? tools : Array.Empty<LlmToolDefinition>();
            int closedCount = wire.Count != 0 && wire[wire.Count - 1].Kind == LlmMessageKind.User ? wire.Count - 1 : wire.Count;
            int dispatched = 0;
            string notice = null;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int round = 0; round < maxTurns; round++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (MeasureWire(wire) > connection.InputBudget)
                    return FinishLocally(wire, closedCount, dispatched, "本轮上下文已达到设定上限。");
                string limit = tools.Count == 0
                    ? unavailableReason ?? GameToolsUnavailable(connection) ?? "本轮没有可用的游戏工具。"
                    : remaining == 0 ? "已达到本轮工具调用次数上限。"
                    : round + 1 == maxTurns ? "已达到本轮模型往返次数上限。" : null;
                // Preserve the schema and completed tool pairs for private
                // reasoning replay. Text-only intent never grants execution.
                var request = new LlmRequest(limit == null ? instructions : instructions +
                    "\n请只用文字回复。当前不接受工具调用。" + limit, wire, tools, textOnly: limit != null);
                var turn = await transport.CompleteAsync(connection, request,
                    text, cancellation).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                if (turn.ToolCalls.Count == 0)
                {
                    wire.Add(LlmMessage.Assistant(turn));
                    return notice == null ? turn : new LlmTurn(turn.Text, turn.ToolCalls, turn.WireJson, notice);
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
                            results.Add(await execute(call, cancellation).ConfigureAwait(false));
                        }
                        else results.Add(new LlmToolResult(call.Id, "rejected：模型请求了本轮未提供的工具。", true));
                    }
                }
                wire.Add(LlmMessage.Assistant(turn));
                wire.Add(LlmMessage.ToolResults(results));
            }
            return FinishLocally(wire, closedCount, dispatched, "已达到本轮模型往返次数上限。");
        }

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
                    foreach (var call in message.Turn.ToolCalls)
                        size += (call.Id?.Length ?? 0) + (call.Name?.Length ?? 0) + (call.ArgumentsJson?.Length ?? 0);
                }
                if (message.Results != null)
                    foreach (var result in message.Results)
                        size += (result.Id?.Length ?? 0) + (result.Output?.Length ?? 0);
            }
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
