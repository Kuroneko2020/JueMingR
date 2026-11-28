using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using JueMingR.Features.Llm;
using JueMingR.Infrastructure.Llm;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Llm;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionChatBudgetChecks
    {
        internal static void Run(object host, Player body, CompanionProfile profile, Action tick, bool legacy = false)
        {
            const string text = "你能干啥，你能挖矿吗？";
            var fixture = NativeCompanionChatChecks.Attach(host);
            var requests = new List<LlmRequest>();
            object View() => Call(host, "GetChat", profile.OwnerId, profile.Id);
            void PollUntil(Func<bool> done) => NativeQuickItemChecks.Until(() => { Call(host, "PumpChat"); return done(); });
            void Send(string message)
            {
                object[] args = { profile.OwnerId, profile.Id, message, null };
                Require((bool)Call(host, "SendChat", args), "native SendChat admitted: " + args[3]);
            }
            LlmTurn Tools(string argument, string tool, string id, int privateUnits, LlmUsageSample use) =>
                new LlmTurn("", new[] { new LlmToolCall(id, tool, "{}", new Dictionary<string, object>
                { [tool == "describe_capability" ? "operation" : "filter"] = argument }) },
                    "{\"role\":\"assistant\",\"reasoning_content\":\"" + new string('r', privateUnits) +
                    "\",\"tool_calls\":[{\"id\":\"" + id + "\",\"type\":\"function\",\"function\":{\"name\":\"" + tool +
                    "\",\"arguments\":\"{}\"}}]}", usage: use);
            try
            {
                PollUntil(() => (bool)Get(View(), "CanSend"));
                Call(host, "RestartChatContext", profile.OwnerId, profile.Id);
                var before = (CompanionTaskSnapshot)Call(host, "GetTask", profile.OwnerId, profile.Id, null);
                int held = body.inventory.Sum(item => item.type == ItemID.CopperPickaxe ? item.stack : 0);
                fixture.Remote.Reply = request =>
                {
                    requests.Add(request);
                    switch (requests.Count)
                    {
                        case 1:
                            return Task.FromResult(Tools("挖矿", "list_capabilities", "catalog", 3500, new LlmUsageSample(300, 40)));
                        case 2:
                            return Task.FromResult(Tools("挖矿", "list_capabilities", "same-catalog", 7400, new LlmUsageSample(190, 25)));
                        case 3:
                            return Task.FromResult(Tools(legacy ? "world_use" : "挖矿", "describe_capability", "mining-detail", 5, null));
                        default:
                            return Task.FromResult(new LlmTurn("能挖矿。请先确认可达目标和镐力。", Array.Empty<LlmToolCall>(),
                                "{\"role\":\"assistant\",\"content\":\"能挖矿\"}"));
                    }
                };
                Send(text);
                PollUntil(() => !(bool)Get(View(), "IsRunning"));
                Console.WriteLine("Chat mining screenshot phrase native requests=" + requests.Count +
                    " tools=" + requests[0].Tools.Count + " status=" + Get(View(), "Status"));
                if (legacy)
                {
                    Require(requests.Count < 4 && ((string)Get(View(), "Status")).Contains("上下文已达到设定上限"),
                        "original screenshot wording reproduces local context stop before final response");
                    return;
                }
                var progress = (LlmChatProgress)Get(View(), "Progress");
                var usage = (LlmChatUsage)Get(View(), "Usage");
                var entries = (IReadOnlyList<LlmHistoryEntry>)Get(View(), "Entries");
                Require(requests.Count == 4 && requests[3].TextOnly && progress.Compactions >= 1 &&
                    progress.ModelTurns == 4 && progress.ToolCalls == 3 && progress.InputLimit == 12000,
                    "default budget keeps same-page and sub-action lookup through text-only final response");
                Require(requests[2].Messages.Any(message => message.Kind == LlmMessageKind.User && message.Text.Contains(text)) &&
                    entries.Any(entry => entry.Kind == "query" && entry.Text.Contains("挖矿")) &&
                    entries.Any(entry => entry.Kind == "assistant" && entry.Text.Contains("能挖矿")),
                    "fresh protocol segment retains full player text, A04 actual observation and a final answer");
                Require(usage.Latest.Input == 490 && usage.Latest.Output == 65 && usage.Latest.Total == 555 &&
                    usage.Latest.UnknownRequests == 2 && usage.Latest.Requests == 4 &&
                    usage.Total.Total >= 555 && usage.LatestConnection.Model == "deepseek-chat",
                    "native Host publishes known and missing usage from all four actual exchanges");
                var after = (CompanionTaskSnapshot)Call(host, "GetTask", profile.OwnerId, profile.Id, null);
                Require(before?.TaskId == after?.TaskId && held == body.inventory.Sum(item => item.type == ItemID.CopperPickaxe ? item.stack : 0),
                    "the screenshot question is read-only through the normal host command boundary");
                Call(host, "RestartChatContext", profile.OwnerId, profile.Id);
                Require(((LlmChatUsage)Get(View(), "Usage")).Total.Total == usage.Total.Total,
                    "restarting the context leaves historical accounting intact");
                Console.WriteLine("PASS native screenshot wording, same-turn compaction, mining detail and visible latest/cumulative usage.");

                CheckIssuedTask(host, body, profile, tick, fixture.Remote, Send, View);
                CheckPendingFactsOnHttp(host, profile, fixture.Store, Send, View, PollUntil);
            }
            finally { fixture.Restore(); }
        }

        private static void CheckPendingFactsOnHttp(object host, CompanionProfile profile,
            NativeCompanionChatChecks.Store store, Action<string> send, Func<object> view, Action<Func<bool>> poll)
        {
            const string late = "探针晚到事实：同一动作的实得数量已经确认。";
            const string oversized = "待续长回执：";
            const string original = "请按已经确认的回执说明这条消息。";
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                using (var transport = new LlmHttpTransport())
                {
                    var presets = LlmPresetCatalog.Load();
                    var preset = presets.Single(value => value.Id == "ollama_local_chat");
                    var options = new LlmConnectionSettings
                    {
                        PresetId = preset.Id, Protocol = preset.Protocol, InferencePath = preset.InferencePath,
                        AuthMode = preset.AuthMode, CredentialKind = preset.CredentialKinds[0],
                        BaseUrl = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/v1",
                        DisplayName = "本机预算探针", Model = "receipt-fixture", ProxyMode = "direct",
                        Stream = false, GameActionsEnabled = false, InputBudget = 10500,
                        FirstByteSeconds = 2, RunTimeoutSeconds = 5,
                        ExtraParameters = "{\"fixture_padding\":\"" + new string('p', 7400) + "\"}"
                    };
                    Guid connection = Guid.NewGuid();
                    store.Configuration = new LlmStoredConfiguration { Enabled = true, DefaultId = connection };
                    store.Configuration.Connections.Add(connection, new LlmStoredConnection(connection, options, Guid.Empty));
                    Call(host, "ConfigureChat", new LlmConnections(presets, store, transport), transport, store);
                    poll(() => (bool)Get(view(), "CanSend"));
                    Call(host, "RestartChatContext", profile.OwnerId, profile.Id);
                    var served = Task.Run(() => new[] { Reply("旧会话种子。", new string('r', 2750)), Reply("已读取晚到回执。", "") });
                    send("请保存这次回复。");
                    poll(() => !(bool)Get(view(), "IsRunning"));
                    Call(host, "PublishChatTaskFact", profile.OwnerId, profile.Id, oversized + new string('宽', 3900));
                    Call(host, "PublishChatTaskFact", profile.OwnerId, profile.Id, late);
                    var session = Call(host, "GetChatSession", profile);
                    var pending = (IReadOnlyList<LlmHistoryEntry>)Get(session, "PendingFacts");
                    Require(pending.Count == 2, "both prior late facts entered the real recovery queue");

                    send(original);
                    poll(() => !(bool)Get(view(), "IsRunning"));
                    Require(served.Wait(TimeSpan.FromSeconds(3)), "both loopback requests reached the real HTTP peer: " + Get(view(), "Status"));
                    string[] packets = served.GetAwaiter().GetResult();
                    var progress = (LlmChatProgress)Get(view(), "Progress");
                    Require(progress.ModelTurns == 1 && progress.Compactions >= 1 && packets[1].Length <= options.InputBudget &&
                        packets[1].Contains(original) && packets[1].Contains(late) && !packets[1].Contains(oversized) &&
                        !packets[1].Contains(new string('r', 100)),
                        "pre-first real HTTP compaction preserved original player text and one whole older receipt: " +
                        progress.ModelTurns + "/" + progress.Compactions + "/" + packets[1].Length);
                    Require(pending.Count == 1 && pending[0].Text.StartsWith(oversized, StringComparison.Ordinal),
                        "normal model closure consumes only the receipt carried by the actual HTTP body");

                    send(new string('"', 5500));
                    poll(() => !(bool)Get(view(), "IsRunning"));
                    progress = (LlmChatProgress)Get(view(), "Progress");
                    Require(progress.ModelTurns == 0 && ((LlmChatUsage)Get(view(), "Usage")).Latest.Requests == 0 &&
                        pending.Count == 1 && pending[0].Text.StartsWith(oversized, StringComparison.Ordinal),
                        "locally rejected minimum encoded request consumes no prior pending receipt");
                    Call(host, "RestartChatContext", profile.OwnerId, profile.Id);
                    Console.WriteLine("PASS real loopback HTTP pre-first compression preserves earlier pending facts and consumes only sent receipts.");
                }
            }
            finally { listener.Stop(); }

            string Reply(string text, string reasoning)
            {
                using (var peer = listener.AcceptTcpClient())
                using (var stream = peer.GetStream())
                {
                    peer.ReceiveTimeout = 5000;
                    var header = new StringBuilder();
                    for (int unit; (unit = stream.ReadByte()) >= 0 && header.Length < 8192;)
                    {
                        header.Append((char)unit);
                        int end = header.Length - 1;
                        if (end >= 3 && header[end] == '\n' && header[end - 1] == '\r' &&
                            header[end - 2] == '\n' && header[end - 3] == '\r') break;
                    }
                    string length = header.ToString().Split('\n').Single(line =>
                        line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                    var body = new byte[Int32.Parse(length.Substring(length.IndexOf(':') + 1))];
                    for (int read = 0; read < body.Length;)
                    {
                        int count = stream.Read(body, read, body.Length - read);
                        if (count == 0) throw new EndOfStreamException();
                        read += count;
                    }
                    byte[] payload = Encoding.UTF8.GetBytes("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"" +
                        text + "\",\"reasoning_content\":\"" + reasoning +
                        "\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":20,\"completion_tokens\":3}}");
                    byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                        payload.Length + "\r\nConnection: close\r\n\r\n");
                    stream.Write(response, 0, response.Length);
                    stream.Write(payload, 0, payload.Length);
                    return Encoding.UTF8.GetString(body);
                }
            }
        }

        private static void CheckIssuedTask(object host, Player body, CompanionProfile profile, Action tick,
            NativeCompanionChatChecks.Transport remote, Action<string> send, Func<object> view)
        {
            const string original = "用一颗生命水晶。";
            var bag = body.inventory.ToArray();
            int maxLife = body.statLifeMax, currentLife = body.statLife;
            int rounds = 0;
            string firstReceipt = null, retryReceipt = null;
            try
            {
                body.statLifeMax = 100;
                for (int slot = 0; slot < 50; slot++)
                    if (body.inventory[slot].type == ItemID.LifeCrystal) body.inventory[slot] = new Item();
                body.inventory[49] = new Item(ItemID.LifeCrystal) { stack = 2 };
                remote.Reply = request =>
                {
                    ++rounds;
                    if (rounds == 2)
                    {
                        string context = String.Join("\n", request.Messages.Where(message => message.Kind == LlmMessageKind.User)
                            .Select(message => message.Text));
                        Require(context.Contains(original) && (context.Contains("已受理") || context.Contains("accepted")),
                            "same-message compaction retains the actual task acceptance for recovery");
                    }
                    if (rounds == 3)
                    {
                        retryReceipt = request.Messages.Last().Results.Single().Output;
                        return Task.FromResult(new LlmTurn("这一条只提交了一次。", Array.Empty<LlmToolCall>(), "{\"role\":\"assistant\",\"content\":\"完成\"}"));
                    }
                    if (rounds == 2) firstReceipt = request.Messages.Last().Text;
                    string id = "item-budget-" + rounds;
                    return Task.FromResult(new LlmTurn("", new[] { new LlmToolCall(id, "execute_operation", "{}", new Dictionary<string, object>
                    {
                        ["title"] = "使用一颗生命水晶", ["operation"] = "use_item",
                        ["arguments"] = new Dictionary<string, object> { ["item_type"] = (int)ItemID.LifeCrystal }
                    }) }, "{\"role\":\"assistant\",\"reasoning_content\":\"" + new string('s', rounds == 1 ? 12000 : 8) + "\"}"));
                };
                send(original);
                CompanionTaskSnapshot TaskView() => (CompanionTaskSnapshot)Call(host, "GetTask", profile.OwnerId, profile.Id, null);
                NativeQuickItemChecks.Until(() =>
                {
                    tick();
                    Call(host, "PumpChat");
                    return !(bool)Get(view(), "IsRunning") && TaskView()?.IsActive != true;
                });
                var completed = TaskView();
                Require(rounds == 3 && firstReceipt?.Contains(completed.TaskId.ToString("N")) == true &&
                    retryReceipt?.Contains(completed.TaskId.ToString("N")) == true &&
                    completed.State == "succeeded" && completed.Attempts.Count == 1 &&
                    body.statLifeMax == 120 && body.inventory[49].stack == 1,
                    "provider changed tool call ID after compaction and original task settled exactly one native life crystal");
                Console.WriteLine("PASS same-user replay after compaction preserves the task ID and one original native debit.");
            }
            finally
            {
                Array.Copy(bag, body.inventory, bag.Length);
                body.statLifeMax = maxLife;
                body.statLife = currentLife;
            }
        }
    }
}
