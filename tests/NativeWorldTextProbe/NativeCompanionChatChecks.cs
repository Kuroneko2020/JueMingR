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
                bool recoveredBait = false;
                remote.Reply = request => Task.FromResult(request.Messages.Last().Kind == LlmMessageKind.ToolResults ?
                    new LlmTurn("跟随已设置。", Array.Empty<LlmToolCall>(), privateClosedTurn) :
                    ReadBaitAndStop(request));
                LlmTurn ReadBaitAndStop(LlmRequest request)
                {
                    recoveredBait = request.Messages.Last().Text.Contains("缺少可用鱼饵");
                    return calls(tool("stop", "stop_activity", new KeyValuePair<string, object>("mode", "follow")));
                }
                Sent("停止，跟我走。");
                Ended();
                Require(recoveredBait && !isFishing() && ((CompanionProfile)Get(actor, "Profile")).Behavior.Follow,
                    "next model request sees late bait and stops real fishing before applying follow");

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

                var beforeDead = body.dead;
                try
                {
                    body.dead = true;
                    Require((string)Get(View(), "Lifecycle") == "死亡待复活" && !(bool)Get(View(), "CanAct"),
                        "a joined dead body has lifecycle identity but no action capability");
                    LlmRequest offline = null;
                    remote.Reply = request => { offline = request; return Task.FromResult(calls(tool("unexpected", "query_inventory"))); };
                    Sent("队友，还在吗？");
                    Ended();
                    Require(offline?.Tools.Count == 0 && offline.TextOnly && offline.Instructions.Contains("死亡待复活") &&
                        !offline.Messages.Any(message => message.Turn?.WireJson == privateClosedTurn) &&
                        Entries().Any(entry => entry.Kind == "status" && entry.Text.Contains("额外工具调用未执行")),
                        "dead capability change retires old tool wire and blocks an unsolicited query");
                }
                finally { body.dead = beforeDead; }

                int life = body.statLife;
                Item prior = body.inventory[49], priorSecond = body.inventory[48];
                Item priorThird = body.inventory[47];
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
                    body.inventory[47] = new Item(ItemID.MagicMirror);
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
                                new KeyValuePair<string, object>("offset", 47))));
                        if (latest.Results[0].Id == "read-48-49")
                        {
                            var rows = latest.Results[0].Output.Split('\n');
                            var right = tool("use-other-slot", "use_item_once", new KeyValuePair<string, object>("source", SlotSource(rows[2])));
                            var left = tool("use-first-slot", "use_item_once", new KeyValuePair<string, object>("source", SlotSource(rows[1])));
                            var mirror = tool("different-item", "use_item_once", new KeyValuePair<string, object>("source", SlotSource(rows[0])));
                            var invalid = tool("invalid-source", "use_item_once", new KeyValuePair<string, object>("source", "invalid-source"));
                            return Task.FromResult(next ? calls(right) : calls(left, right, mirror, invalid));
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
                    Require(received?.Length == 4 && received[0].Output == received[1].Output && !received[0].IsError &&
                        received[0].Output.Contains(body.inventory[48].Name) && received[2].IsError && received[3].IsError &&
                        received[2].Output.Contains(body.inventory[47].Name) && body.inventory[48].stack == 1 && body.inventory[49].stack == 2,
                        "only the matching potion intent shares one real debit; different and invalid sources are independently refused");
                    next = true;
                    body.statLife = 1;
                    Sent("再用一次治疗药水。");
                    NativeUseUntilReply();
                    Require(received?.Length == 1 && body.inventory[48].stack == 1 && body.inventory[49].stack == 1,
                        "a new explicit user message has independent one-use authority");

                    var beforeTeleport = body.position;
                    var beforeHuman = human.position;
                    int originalSelection = body.selectedItem;
                    float previousCameraX = Main.cameraX, previousCameraY = Main.cameraY;
                    remote.Reply = request =>
                    {
                        var last = request.Messages.Last();
                        if (last.Kind != LlmMessageKind.ToolResults)
                            return Task.FromResult(calls(tool("query-mirror", "query_inventory", new KeyValuePair<string, object>("offset", 47))));
                        if (last.Results.Single().Id == "query-mirror")
                        {
                            string row = last.Results.Single().Output.Split('\n')[0];
                            Require(row.Contains("队友自身出生点"), "real mirror mode reports its native destination");
                            return Task.FromResult(calls(tool("mirror-home", "use_item_once",
                                new KeyValuePair<string, object>("source", SlotSource(row)),
                                new KeyValuePair<string, object>("stay_after_teleport", true))));
                        }
                        received = last.Results.ToArray();
                        return Task.FromResult(textReply());
                    };
                    Sent("用魔镜回自己的出生点，在那里等我。");
                    NativeUseUntilReply();
                    Require(received?.Length == 1 && !received[0].IsError && received[0].Output.Contains("已原地待命") &&
                        received[0].Output.Contains("出生点") && body.position != beforeTeleport &&
                        Math.Abs(body.Center.X - Main.spawnTileX * 16) < 64 && !((CompanionProfile)Get(actor, "Profile")).Behavior.Follow &&
                        human.position == beforeHuman && Main.cameraX == previousCameraX && Main.cameraY == previousCameraY &&
                        body.inventory[47].type == ItemID.MagicMirror && body.selectedItem == originalSelection,
                        "real mirror reaches native spawn before confirmed stay, preserves owner and releases the original hand");
                }
                finally { body.statLife = life; body.inventory[49] = prior; body.inventory[48] = priorSecond; body.inventory[47] = priorThird; }
                CheckClearAndDeletion(host, profile, fixture.Store, View, Entries, PumpUntil);
                var connections = (LlmConnections)Get(host, "ChatConnections");
                var draft = connections.Edit(connections.DefaultId);
                draft.HistoryLimit = 1;
                draft.MaxModelTurns = 2;
                connections.SaveAsync(draft).GetAwaiter().GetResult();
                var initialFollow = ((CompanionProfile)Get(actor, "Profile")).Behavior;
                initialFollow.Follow = true;
                Require((bool)Call(host, "SetBehavior", profile.Id, initialFollow), "single-entry history fixture starts in real follow mode");
                int exchanges = 0;
                remote.Reply = request =>
                {
                    if (++exchanges == 1)
                        return Task.FromResult(calls(tool("observe-current", "get_companion_state"),
                            tool("real-stay", "set_behavior", new KeyValuePair<string, object>("mode", "stay"))));
                    Require(request.TextOnly && request.Messages.Last().Results.Count == 2 &&
                        request.Messages.Last().Results.All(result => !result.IsError),
                        "the local closing request carries both successful native/public outcomes");
                    return Task.FromResult(calls(tool("forbidden-follow", "set_behavior",
                        new KeyValuePair<string, object>("mode", "follow"))));
                };
                Sent("先看看状态，然后在这里等。");
                Ended();
                Require(exchanges == 2 && Entries().Single().Kind == "status" &&
                    !((CompanionProfile)Get(actor, "Profile")).Behavior.Follow,
                    "local finish limits the visible history and never dispatches the extra follow action");
                LlmRequest recoveredFacts = null;
                remote.Reply = request => { recoveredFacts = request; return Task.FromResult(textReply()); };
                Sent("刚才看到了什么，后来做了什么？");
                Ended();
                Require(recoveredFacts?.Messages.Last().Text.Contains("生命 ") == true &&
                    recoveredFacts.Messages.Last().Text.Contains("移动：待命") &&
                    !recoveredFacts.Messages.Any(message => message.Kind == LlmMessageKind.ToolResults) &&
                    !((CompanionProfile)Get(actor, "Profile")).Behavior.Follow,
                    "single-entry history recovers both native facts without replaying tools or private protocol blocks");
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
