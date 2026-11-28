using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JueMingR.Features.Llm;
using JueMingR.Infrastructure.Llm;
using JueMingR.Platform.Llm;

namespace JueMingR.ArchitectureTests
{
    internal static partial class LlmConnectionChecks
    {
        private static async Task UsageProtocolWire()
        {
            foreach (var preset in new[] { "openai_official_chat", "openai_official_responses", "claude_official_messages", "deepseek_official_chat" })
            {
                foreach (bool streaming in new[] { false, true })
                {
                    var connection = Connection(preset, streaming);
                    long expectedLength = 0;
                    using (var transport = new LlmHttpTransport(_ => new Fixture(http =>
                    {
                        string body = http.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        Require(body.Length == expectedLength && body.Contains("一二"), "measured size matches actual UTF-16 wire");
                        Require(body.Contains("include_usage") == (preset == "openai_official_chat" && streaming),
                            "optional usage stream setting stays on the supported official consumer");
                        string payload = UsageResponse(preset, streaming);
                        return streaming ? Events(payload, fragmented: true) : Json(payload);
                    })))
                    {
                        var request = new LlmRequest("test", new[] { LlmMessage.User("一二") }, Array.Empty<LlmToolDefinition>());
                        expectedLength = transport.MeasureRequest(connection, request);
                        Require(!request.WasSent && request.ReportedUsage == null, "meter alone never sends");
                        var answer = await transport.CompleteAsync(connection, request, null, CancellationToken.None).ConfigureAwait(false);
                        Require(request.WasSent && answer.Usage.Input == 300 && answer.Usage.Output == 40 && answer.Usage.Total == 340 &&
                            answer.Usage.CacheRead == 70, "one complete report normalizes cache and duplicate cumulative events: " + preset);
                        Require(preset != "claude_official_messages" || answer.Usage.CacheWrite == 30, "Anthropic cache write counted once");
                    }
                }
            }
            var interrupted = Connection("claude_official_messages", true);
            var partial = new LlmRequest("test", new[] { LlmMessage.User("stop") }, Array.Empty<LlmToolDefinition>());
            using (var transport = new LlmHttpTransport(_ => new Fixture(http => Events(
                E("{\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":220,\"output_tokens\":1}}}")))))
            {
                await Failure(() => transport.CompleteAsync(interrupted, partial, null, CancellationToken.None), LlmFailure.Incomplete).ConfigureAwait(false);
                Require(partial.WasSent && partial.ReportedUsage.Input == 220 && partial.ReportedUsage.Output == null,
                    "interruption preserves reported inputs and does not count provisional output");
            }
        }

        private static string UsageResponse(string preset, bool streaming)
        {
            if (preset == "claude_official_messages")
            {
                const string input = "{\"input_tokens\":200,\"cache_read_input_tokens\":70,\"cache_creation_input_tokens\":30";
                const string blocks = "[{\"type\":\"text\",\"text\":\"done\"}]";
                if (!streaming) return "{\"content\":" + blocks + ",\"stop_reason\":\"end_turn\",\"usage\":" + input + ",\"output_tokens\":40}}";
                const string delta = "{\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":40}}";
                return E("{\"type\":\"message_start\",\"message\":{\"content\":" + blocks + ",\"usage\":" + input + ",\"output_tokens\":1}}}") +
                    E(delta) + E(delta) + E("{\"type\":\"message_stop\"}");
            }
            if (preset == "openai_official_responses")
            {
                const string response = "{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"done\"}]}]," +
                    "\"usage\":{\"input_tokens\":300,\"output_tokens\":40,\"total_tokens\":340,\"input_tokens_details\":{\"cached_tokens\":70}}}";
                return streaming ? E("{\"type\":\"response.completed\",\"response\":" + response + "}") : response;
            }
            const string usage = "\"usage\":{\"prompt_tokens\":300,\"completion_tokens\":40,\"total_tokens\":340,\"prompt_cache_hit_tokens\":70,\"prompt_cache_miss_tokens\":230}";
            if (!streaming) return "{\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"done\"},\"finish_reason\":\"stop\"}]," + usage + "}";
            const string choice = "\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"done\"},\"finish_reason\":\"stop\"}]";
            string total = E("{\"choices\":[]," + usage + "}");
            return preset == "deepseek_official_chat" ? E("{" + choice + "," + usage + "}") + E("[DONE]") :
                E("{" + choice + "}") + total + total + E("[DONE]");
        }

        private static async Task SameTurnRecovery()
        {
            var connection = Connection("deepseek_official_chat", false);
            const string user = "去我刚才那里挖三块铜矿，挖完在那里等我";
            const string fact = "任务 f1 已受理；实际只挖了一块，另有一块待确认";
            int requests = 0, actions = 0;
            LlmChatProgress last = null;
            var total = new LlmUsageTotals();
            using (var transport = new LlmHttpTransport(_ => new Fixture(http =>
            {
                string body = http.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                ++requests;
                Require(body.Length <= connection.InputBudget, "no attempted provider request over the actual budget");
                if (requests == 1) return Json("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"reasoning_content\":\"" +
                    new string('p', connection.InputBudget) + "\",\"tool_calls\":[{\"id\":\"one\",\"type\":\"function\",\"function\":{\"name\":\"action\",\"arguments\":\"{}\"}}]},\"finish_reason\":\"tool_calls\"}]," +
                    "\"usage\":{\"prompt_tokens\":300,\"completion_tokens\":40}}");
                Require(body.Contains(user) && body.Contains(fact) && !body.Contains("\"tool_call_id\"") &&
                    !body.Contains("reasoning_content"), "fresh legal segment keeps original words and real public fact together");
                return Json("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"已知只完成一块。\"},\"finish_reason\":\"stop\"}]," +
                    "\"usage\":{\"prompt_tokens\":190,\"completion_tokens\":25}}");
            })))
            {
                var wire = new List<LlmMessage> { LlmMessage.User(user) };
                var answer = await CompanionChat.RunAsync(transport, connection, "发送脚点 20,40", wire,
                    new[] { new LlmToolDefinition("action", "One fixture action", "{\"type\":\"object\"}") },
                    (call, token) => { actions++; return Task.FromResult(new LlmToolResult(call.Id, fact, recoveryFact: fact)); }, null,
                    CancellationToken.None, originalText: user, progress: update => last = update,
                    usage: sample => total = total.Add(sample)).ConfigureAwait(false);
                Require(answer.Text.Contains("完成一块") && requests == 2 && actions == 1 && last.Compactions > 0 &&
                    last.ModelTurns == 2 && last.ToolCalls == 1 && total.Input == 490 && total.Output == 65 && total.Total == 555,
                    "one player message continues after genuine pressure and reports actual use across both requests");
                total = total.Add(null);
                Require(total.Requests == 3 && total.UnknownRequests == 1 && total.Total == 555,
                    "missing usage stays separate from 555 known tokens");
            }
        }

        private static async Task UsageArchive()
        {
            string folder = Path.Combine(Path.GetTempPath(), "jmr-usage-fixture-" + Guid.NewGuid().ToString("N"));
            Guid owner = Guid.NewGuid(), profile = Guid.NewGuid();
            var when = DateTimeOffset.UtcNow;
            var row = new LlmConnectionUsage(Guid.NewGuid(), "隔离连接", "fixture-model", new LlmUsageTotals().Add(new LlmUsageSample(300, 40)).Add(null));
            var other = new LlmConnectionUsage(Guid.NewGuid(), "另一个连接", "fixture-other", new LlmUsageTotals().Add(new LlmUsageSample(190, 25)));
            try
            {
                using (var store = new LlmFileStore(folder))
                {
                    await store.SaveHistoryAsync(owner, profile, new[] { new LlmHistoryEntry(Guid.NewGuid(), "user", "旧记录", when) }).ConfigureAwait(false);
                    Require((await store.LoadChatArchiveAsync(owner, profile).ConfigureAwait(false)).Usage.TrackingStartedAt == null,
                        "older archives never retroactively invent usage");
                    await store.SaveChatArchiveAsync(owner, profile, new LlmChatArchive(Array.Empty<LlmHistoryEntry>(),
                        new LlmChatUsage(when, other, new[] { row, other }))).ConfigureAwait(false);
                }
                using (var store = new LlmFileStore(folder))
                {
                    var loaded = await store.LoadChatArchiveAsync(owner, profile).ConfigureAwait(false);
                    Require(loaded.Entries.Count == 0 && loaded.Usage.Total.Total == 555 && loaded.Usage.Total.UnknownRequests == 1 &&
                        loaded.Usage.Latest.Total == 215 && loaded.Usage.LatestConnection.ConnectionId == other.ConnectionId,
                        "connection totals survive history pruning and reopen");
                    await store.ClearHistoryAsync(owner, profile).ConfigureAwait(false);
                    Require((await store.LoadChatArchiveAsync(owner, profile).ConfigureAwait(false)).Usage.Total.Requests == 0,
                        "clearing the archive clears accounting at the same atomic boundary");
                }
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
    }
}
