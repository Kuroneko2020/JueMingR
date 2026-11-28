using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JueMingR.Features.Llm;
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
            }
            finally { fixture.Restore(); }
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
