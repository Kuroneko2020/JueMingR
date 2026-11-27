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
            CancellationToken cancellation, int minimumTurns = 1)
        {
            int remaining = Math.Max(0, connection.MaxToolCalls);
            int maxTurns = Math.Max(minimumTurns, connection.MaxModelTurns);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int round = 0; round < maxTurns; round++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (MeasureWire(wire) > connection.InputBudget)
                    throw new InvalidOperationException("本轮上下文已达到设定上限，已保留可见聊天记录。");
                // Preserve the existing schema across all exchanges. Some
                // providers change how they replay reasoning when tools vanish.
                var turn = await transport.CompleteAsync(connection, new LlmRequest(instructions, wire, tools),
                    text, cancellation).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                if (turn.ToolCalls.Count == 0)
                {
                    wire.Add(LlmMessage.Assistant(turn));
                    return turn;
                }
                foreach (var call in turn.ToolCalls)
                    if (String.IsNullOrEmpty(call.Id) || !seen.Add(call.Id))
                        throw new InvalidOperationException("模型返回了无法关联的重复工具调用。");
                if (round + 1 >= maxTurns || remaining == 0 || tools.Count == 0)
                    throw new InvalidOperationException("本轮工具额度已用完，未派发额外动作。");

                var results = new List<LlmToolResult>(turn.ToolCalls.Count);
                foreach (var call in turn.ToolCalls)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (remaining == 0)
                        results.Add(new LlmToolResult(call.Id, "rejected：本轮工具额度已用完。", true));
                    else
                    {
                        remaining--;
                        results.Add(await execute(call, cancellation).ConfigureAwait(false));
                    }
                }
                wire.Add(LlmMessage.Assistant(turn));
                wire.Add(LlmMessage.ToolResults(results));
            }
            throw new InvalidOperationException("本轮模型回复次数已用完。");
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
            int executed = 0;
            var wire = new List<LlmMessage> { LlmMessage.User("Call check_connection once, then report its returned marker.") };
            var final = await RunAsync(transport, connection, "This is a connection test. Use the supplied test tool once.",
                wire, TestTools, (call, _) =>
                {
                    if (call.Name != "check_connection" || call.Arguments == null || call.Arguments.Count != 0 || executed != 0)
                        return Task.FromResult(new LlmToolResult(call.Id, "invalid test call", true));
                    executed++;
                    return Task.FromResult(new LlmToolResult(call.Id, "JMR_LOCAL_TOOL_OK", false));
                }, null, token, minimumTurns: 2).ConfigureAwait(false);
            bool succeeded = executed == 1 && !String.IsNullOrWhiteSpace(final.Text);
            return new LlmConnectionTest(succeeded ? "工具调用、回填和最终回复均已完成。" : "模型未完成测试工具闭环。", succeeded);
        }
    }
}
