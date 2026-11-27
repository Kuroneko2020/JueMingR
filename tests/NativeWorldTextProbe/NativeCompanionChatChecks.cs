using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Terraria.ID;
using JueMingR.Features.Llm;
using JueMingR.Infrastructure.Llm;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Llm;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionChatChecks
    {
        internal sealed class Transport : ILlmTransport
        {
            internal Func<LlmRequest, Task<LlmTurn>> Reply;
            public Task<LlmTurn> CompleteAsync(LlmConnectionSnapshot connection, LlmRequest request,
                Action<string> onText, CancellationToken cancellation) => Reply(request);
            public Task<IReadOnlyList<LlmModelInfo>> DiscoverAsync(LlmConnectionSnapshot connection, CancellationToken cancellation) =>
                Task.FromResult<IReadOnlyList<LlmModelInfo>>(Array.Empty<LlmModelInfo>());
        }

        internal sealed class Store : ILlmStore
        {
            internal LlmStoredConfiguration Configuration;
            internal int FailClear, ClearCount;
            public Task<LlmStoredConfiguration> LoadAsync() => Task.FromResult(Configuration);
            public Task SaveAsync(LlmStoredConfiguration configuration) => Task.CompletedTask;
            public Task<IReadOnlyList<LlmHistoryEntry>> LoadHistoryAsync(Guid owner, Guid profile) =>
                Task.FromResult<IReadOnlyList<LlmHistoryEntry>>(Array.Empty<LlmHistoryEntry>());
            public Task SaveHistoryAsync(Guid owner, Guid profile, IReadOnlyList<LlmHistoryEntry> entries) => Task.CompletedTask;
            public Task ClearHistoryAsync(Guid owner, Guid profile)
            {
                Interlocked.Increment(ref ClearCount);
                if (Interlocked.Exchange(ref FailClear, 0) != 0)
                    throw new LlmException(LlmFailure.Storage, "isolated clear failure");
                return Task.CompletedTask;
            }
        }

        internal static (Transport Remote, Store Store, Action Restore) Attach(object host)
        {
            var previousConnections = Get(host, "ChatConnections");
            var previousTransport = Get(host, "chatTransport");
            var previousStore = Get(host, "chatStore");
            var previousStoreLifetime = GetOptional(host, "chatStoreLifetime");
            var previousTransportLifetime = GetOptional(host, "chatTransportLifetime");
            var presets = LlmPresetCatalog.Load();
            var preset = presets.First(value => value.Id == "deepseek_official_chat");
            var options = new LlmConnectionSettings
            {
                PresetId = preset.Id, Protocol = preset.Protocol, BaseUrl = preset.BaseUrl,
                InferencePath = preset.InferencePath, DiscoveryPath = preset.DiscoveryPath,
                AuthMode = preset.AuthMode, DisplayName = "本地测试", Model = "deepseek-chat",
                CredentialKind = preset.CredentialKinds[0],
                GameActionsEnabled = true, LocalNoticesEnabled = true
            };
            var connectionId = Guid.NewGuid();
            var store = new Store { Configuration = new LlmStoredConfiguration { Enabled = true, DefaultId = connectionId } };
            Guid secretVersion = Guid.NewGuid();
            store.Configuration.Secrets.Add(secretVersion, new LlmConnectionSecrets("local-fixture-token", "", ""));
            store.Configuration.Connections.Add(connectionId, new LlmStoredConnection(connectionId, options, secretVersion));
            var remote = new Transport();
            var connections = new LlmConnections(presets, store, remote);
            Call(host, "ConfigureChat", connections, remote, store);
            return (remote, store, () =>
            {
                Set(host, "chatTransport", previousTransport);
                Set(host, "chatStore", previousStore);
                Set(host, "chatStoreLifetime", previousStoreLifetime);
                Set(host, "chatTransportLifetime", previousTransportLifetime);
                host.GetType().GetProperty("ChatConnections", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).SetValue(host, previousConnections);
            });
        }

        internal static void Run(object host, Player human, Player body, CompanionProfile profile, Action nativeStep)
        {
            var fixture = Attach(host);
            var remote = fixture.Remote;
            var old = human.position;
            var actor = Call(host, "Owned", body);
            var behavior = ((CompanionProfile)Get(actor, "Profile")).Behavior;
            var first = new TaskCompletionSource<LlmTurn>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                object View() => Call(host, "GetChat", profile.OwnerId, profile.Id);
                void Sent(string text)
                {
                    var args = new object[] { profile.OwnerId, profile.Id, text, null };
                    bool sent = (bool)Call(host, "SendChat", args);
                    Require(sent, "chat request admitted: " + args[3]);
                }
                void PumpUntil(Func<bool> condition) => NativeQuickItemChecks.Until(() =>
                {
                    Call(host, "PumpChat");
                    return condition();
                });
                void Ended() => PumpUntil(() => !(bool)Get(View(), "IsRunning"));
                LlmHistoryEntry[] Entries() => ((IReadOnlyList<LlmHistoryEntry>)Get(View(), "Entries")).ToArray();
                bool isFishing() => (bool)Call(host, "IsFishing", profile.OwnerId, profile.Id);
                LlmTurn textReply() => new LlmTurn("已读取真实游戏结果。", Array.Empty<LlmToolCall>(), "{}");
                LlmToolCall tool(string id, string name, params KeyValuePair<string, object>[] args) =>
                    new LlmToolCall(id, name, "{}", args.ToDictionary(pair => pair.Key, pair => pair.Value));
                LlmTurn calls(params LlmToolCall[] tools) => new LlmTurn("", tools, "{}");

                PumpUntil(() => (bool)Get(View(), "CanSend"));
                var expectedAnchor = human.gravDir >= 0 ? human.Bottom : human.Top;
                remote.Reply = request => request.Messages.Last().Kind == LlmMessageKind.ToolResults
                    ? Task.FromResult(textReply()) : first.Task;
                Sent("在这里钓鱼，缺饵告诉我。");
                human.position += new Vector2(96, 0);
                first.SetResult(calls(tool("fish", "start_fishing", new KeyValuePair<string, object>("notify_no_bait", true))));
                Ended();
                Require(isFishing(), "model tool installed the real actor fishing activity");
                var fishing = Get(actor, "Fishing");
                Require((Vector2)Get(fishing, "RequestedFeet") == expectedAnchor,
                    "changing owner position during inference keeps the send-time feet");
                Require(Entries().Any(entry => entry.Kind == "action" && entry.Text.Contains("发送时")),
                    "native activity start returns a separate real action result");
                // Gear reporting is driven by the production bait observer;
                // cross this guard with the established hold state and no bait.
                Set(fishing, "Hold", true);
                Call(host, "FishingNeedsGear", actor, false, true);
                Call(host, "FishingNeedsGear", actor, false, true);
                Require(Entries().Count(entry => entry.Kind == "notice" && entry.Text.Contains("鱼饵")) == 1,
                    "one local notice is published without another charged model round");

                const string privateClosedTurn = "{\"reasoning_content\":\"closed-a-private-field\"}";
                remote.Reply = request => Task.FromResult(request.Messages.Last().Kind == LlmMessageKind.ToolResults ?
                    new LlmTurn("跟随已设置。", Array.Empty<LlmToolCall>(), privateClosedTurn) :
                    calls(tool("stop", "stop_activity", new KeyValuePair<string, object>("mode", "follow"))));
                Sent("停止，跟我走。");
                Ended();
                Require(!isFishing() && ((CompanionProfile)Get(actor, "Profile")).Behavior.Follow,
                    "model stops the real fishing activity before applying follow");

                var delayed = new TaskCompletionSource<LlmTurn>(TaskCreationOptions.RunContinuationsAsynchronously);
                var called = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                remote.Reply = _ => { called.TrySetResult(true); return delayed.Task; };
                Sent("再次钓鱼。");
                PumpUntil(() => called.Task.IsCompleted);
                var manual = ((CompanionProfile)Get(actor, "Profile")).Behavior;
                manual.AttackEnabled = !manual.AttackEnabled;
                Require((bool)Call(host, "SetBehavior", profile.Id, manual), "manual command reaches the same behavior owner");
                delayed.SetResult(calls(tool("late", "start_fishing")));
                Ended();
                NativeQuickItemChecks.Until(() =>
                {
                    Call(host, "PumpChat");
                    return ((System.Collections.IList)Get(host, "chatRuns")).Count == 0;
                });
                Require(!isFishing() && Entries().Any(entry => entry.Kind == "status" && entry.Text.Contains("手动行为")),
                    "the late model callback cannot restart fishing after manual control");
                LlmRequest resumed = null;
                remote.Reply = request => { resumed = request; return Task.FromResult(textReply()); };
                Sent("之前最后发生了什么？");
                Ended();
                Require(resumed?.Messages.Any(message => message.Turn?.WireJson == privateClosedTurn) == true &&
                    !resumed.Messages.Any(message => message.Turn?.ToolCalls.Any(call => call.Id == "late") == true) &&
                    resumed.Messages.Last().Text.Contains("手动行为"),
                    "closed provider-private history survives an interrupted next turn while latest public facts belong to the new user message");

                int life = body.statLife;
                Item prior = body.inventory[49], priorSecond = body.inventory[48];
                var potion = new Item(ItemID.LesserHealingPotion) { stack = 2 };
                try
                {
                    body.statLife = Math.Max(1, body.statLifeMax2 / 2);
                    body.inventory[49] = potion;
                    remote.Reply = request =>
                    {
                        if (request.Messages.Last().Kind != LlmMessageKind.ToolResults)
                            return Task.FromResult(calls(tool("read-bag", "query_inventory",
                                new KeyValuePair<string, object>("offset", 49))));
                        string sourceRow = request.Messages.Last().Results.Single().Output.Split('\n').First();
                        int refStart = sourceRow.IndexOf("source=", StringComparison.Ordinal);
                        Require(refStart >= 0, "known main inventory source is returned to the model");
                        return Task.FromResult(calls(tool("once", "use_item_once",
                            new KeyValuePair<string, object>("source", sourceRow.Substring(refStart + 7)))));
                    };
                    Sent("用一次背包里的治疗药水。");
                    PumpUntil(() => GetOptional(Get(actor, "Supplies"), "Once") != null || !(bool)Get(View(), "IsRunning"));
                    Require(GetOptional(Get(actor, "Supplies"), "Once") != null && potion.stack == 2,
                        "the real native item queue accepted the order without consuming it");
                    Call(host, "StopChatReply", profile.OwnerId, profile.Id);
                    Require(GetOptional(Get(actor, "Supplies"), "Once") == null && potion.stack == 2 &&
                        Entries().Any(entry => entry.Kind == "action" && entry.Text.Contains("已取消")),
                        "stopping a model reply withdraws an accepted native item before it starts");

                    body.inventory[49] = new Item(ItemID.WoodenSword);
                    Sent("挥一次背包里的木剑。");
                    PumpUntil(() => GetOptional(Get(actor, "Supplies"), "Once") != null || !(bool)Get(View(), "IsRunning"));
                    Require(GetOptional(Get(actor, "Supplies"), "Once") != null, "the original held weapon enters the hand queue");
                    for (int i = 0; i < 4 && GetOptional(Get(actor, "Supplies"), "ChatFinishingOnce") == null; i++)
                        nativeStep();
                    Require(GetOptional(Get(actor, "Supplies"), "ChatFinishingOnce") != null &&
                        Entries().Any(entry => entry.Kind == "action" && entry.Text.Contains("正在等待动作结束")),
                        "the real ItemCheck starts the original weapon and publishes a running result");
                    Call(host, "StopChatReply", profile.OwnerId, profile.Id);
                    for (int i = 0; i < 64 && GetOptional(Get(actor, "Supplies"), "ChatFinishingOnce") != null; i++)
                        nativeStep();
                    Require(GetOptional(Get(actor, "Supplies"), "ChatFinishingOnce") == null &&
                        Entries().Any(entry => entry.Kind == "action" && entry.Text.Contains("原版用物动作已结束") &&
                            entry.Text.Contains("后续效果未确认")),
                        "stopping a reply leaves the begun native weapon to reach its real animation tail");

                    body.inventory[48] = new Item(ItemID.LesserHealingPotion) { stack = 2 };
                    body.inventory[49] = new Item(ItemID.LesserHealingPotion) { stack = 2 };
                    var testBehavior = ((CompanionProfile)Get(actor, "Profile")).Behavior;
                    testBehavior.HealthRecoveryEnabled = false;
                    testBehavior.AllowNurse = false;
                    Require((bool)Call(host, "SetBehavior", profile.Id, testBehavior),
                        "automatic treatment is disabled so only the two explicit commands can consume potions");
                    body.statLife = 1;
                    body.potionDelay = 0;
                    string SlotSource(string row) => row.Substring(row.IndexOf("source=", StringComparison.Ordinal) + 7);
                    LlmToolResult[] received = null;
                    bool next = false;
                    remote.Reply = request =>
                    {
                        var latest = request.Messages.Last();
                        if (latest.Kind != LlmMessageKind.ToolResults)
                            return Task.FromResult(calls(tool("read-48-49", "query_inventory",
                                new KeyValuePair<string, object>("offset", 48))));
                        if (latest.Results[0].Id == "read-48-49")
                        {
                            var rows = latest.Results[0].Output.Split('\n');
                            var right = tool("use-other-slot", "use_item_once", new KeyValuePair<string, object>("source", SlotSource(rows[1])));
                            var left = tool("use-first-slot", "use_item_once", new KeyValuePair<string, object>("source", SlotSource(rows[0])));
                            return Task.FromResult(next ? calls(right) : calls(left, right));
                        }
                        received = latest.Results.ToArray();
                        return Task.FromResult(textReply());
                    };
                    void NativeUseUntilReply() => NativeQuickItemChecks.Until(() =>
                    {
                        body.potionDelay = 0;
                        nativeStep();
                        return !(bool)Get(View(), "IsRunning");
                    });
                    Sent("用一次治疗药水。");
                    NativeUseUntilReply();
                    Require(received?.Length == 2 && received[0].Output == received[1].Output &&
                        body.inventory[48].stack == 1 && body.inventory[49].stack == 2,
                        "different calls with two real eligible potion sources share one operation and one actual debit");
                    next = true;
                    body.statLife = 1;
                    Sent("再用一次治疗药水。");
                    NativeUseUntilReply();
                    Require(received?.Length == 1 && body.inventory[48].stack == 1 && body.inventory[49].stack == 1,
                        "a new explicit user message has independent one-use authority");
                }
                finally { body.statLife = life; body.inventory[49] = prior; body.inventory[48] = priorSecond; }
                CheckClearAndDeletion(host, profile, fixture.Store, View, Entries, PumpUntil);
                Console.WriteLine("PASS native companion chat anchor, real activity, notices, manual replacement, closed wire recovery, one debit per message, use settlement and retried deletion.");
            }
            finally
            {
                Call(host, "StopChatReply", profile.OwnerId, profile.Id);
                Call(host, "SetFishing", profile.OwnerId, profile.Id, false);
                Call(host, "SetBehavior", profile.Id, behavior);
                human.position = old;
                // Restore composition for the surrounding native fishing checks.
                fixture.Restore();
            }
        }

        private static void CheckClearAndDeletion(object host, CompanionProfile profile, Store store, Func<object> view,
            Func<LlmHistoryEntry[]> entries, Action<Func<bool>> pump)
        {
            var firstEntry = entries()[0].Id;
            store.FailClear = 1;
            Require((bool)Call(host, "ClearChat", profile.OwnerId, profile.Id), "history clear is accepted asynchronously");
            pump(() => ((string)Get(view(), "Status")).Contains("未能清空"));
            Require(entries()[0].Id == firstEntry, "failed persisted clear keeps the preceding visible record");
            Require((bool)Call(host, "ClearChat", profile.OwnerId, profile.Id), "the owner can retry a failed history clear");
            pump(() => entries().Length == 0 && ((string)Get(view(), "Status")).Contains("已清空"));

            Require((bool)Call(host, "Create", "聊天清理验证"), "isolated profile has an actual deletion intent");
            Guid retired = (Guid)Get(host, "LastCreatedId");
            var profiles = Get(host, "profiles");
            NativeQuickItemChecks.Until(() => { Call(profiles, "Pump"); return (bool)Call(profiles, "IsSaved", retired); });
            var connections = (LlmConnections)Get(host, "ChatConnections");
            Guid link = connections.DefaultId.Value;
            connections.SetBindingAsync(profile.OwnerId, retired, link).GetAwaiter().GetResult();
            store.FailClear = 1;
            Require((bool)Call(host, "DeleteCompanion", profile.OwnerId, retired, (long)Get(host, "session")),
                "confirmed character deletion starts even if chat storage will fail");
            CompanionDeletion pending = null;
            void Poll()
            {
                var receipts = (IReadOnlyList<CompanionDeletion>)Call(host, "PollDeletionBindings");
                pending = receipts.SingleOrDefault(record => record.ProfileId == retired);
                if (pending?.FilesRemoved == true && pending.Error == null && !pending.Finishing)
                    Call(host, "FinishDeletionBindings", pending);
            }
            NativeQuickItemChecks.Until(() => { Poll(); return pending?.Error?.Contains("模型绑定清理失败") == true; });
            Require(pending.FilesRemoved && connections.GetBinding(profile.OwnerId, retired) == link,
                "failed LLM cleanup leaves the retryable archive receipt and still-known binding");
            Call(host, "RetryCompanionDeletion", profile.OwnerId, retired);
            NativeQuickItemChecks.Until(() => { Poll(); return pending == null; });
            Require(connections.GetBinding(profile.OwnerId, retired) == null && connections.DefaultId == link,
                "retry finishes the same intent and removes the retired profile's explicit binding");
        }
    }
}
