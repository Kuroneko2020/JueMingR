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
                Require(isFishing(), "model tool installed the real actor fishing activity: " +
                    String.Join(" | ", Entries().Reverse().Take(4).Select(entry => entry.Kind + " " + entry.Text)));
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
                CheckColdKnowledgeAndStrategy(host, remote, human, body, profile, nativeStep, Ended, Sent);
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

        private static void CheckColdKnowledgeAndStrategy(object host, Transport remote, Player human, Player body,
            CompanionProfile profile, Action nativeStep, Action ended, Action<string> send)
        {
            var knowledge = Get(host, "ChatKnowledge");
            var catalog = Get(knowledge, "Native");
            LlmToolCall Tool(string name, params KeyValuePair<string, object>[] fields) => new LlmToolCall(name,
                name, "{}", fields.ToDictionary(field => field.Key, field => field.Value));
            KeyValuePair<string, object> Field(string name, object value) => new KeyValuePair<string, object>(name, value);
            LlmTurn CallTool(LlmToolCall tool) => new LlmTurn("", new[] { tool }, "{}");
            var reply = new LlmTurn("我已读到本轮实际信息。", Array.Empty<LlmToolCall>(), "{}");
            string RunQuery(string prompt, LlmToolCall tool)
            {
                string result = null;
                remote.Reply = request =>
                {
                    if (request.Messages.Last().Kind != LlmMessageKind.ToolResults) return Task.FromResult(CallTool(tool));
                    result = request.Messages.Last().Results.Single().Output;
                    return Task.FromResult(reply);
                };
                send(prompt);
                ended();
                return result;
            }

            var search = Tool("find_game_items", Field("filter", "Magic Mirror"), Field("purpose", "home_teleport"));
            Require(!(bool)Get(catalog, "CatalogCurrent"), "the original browser catalog starts cold");
            string cold = RunQuery("什么物品能回自己的出生点？", search);
            Require(cold?.StartsWith("pending", StringComparison.Ordinal) == true,
                "live internal chat produces a demand instead of a false empty query");
            for (int i = 0; i < 192 && !(bool)Get(catalog, "CatalogCurrent"); i++) nativeStep();
            Require((bool)Get(catalog, "CatalogCurrent"), "real host updates prepared the same catalog while the browser remained closed");
            string ready = RunQuery("再查 Magic Mirror。", search);
            Require(ready?.StartsWith("ready", StringComparison.Ordinal) == true && ready.Contains("#" + ItemID.MagicMirror) &&
                ready.Contains("队友自身出生点"), "stable English name, native item identity and teleport purpose are linked");
            string chinese = RunQuery("按中文名再查魔镜。", Tool("find_game_items", Field("filter", "魔镜"), Field("purpose", "home_teleport")));
            Require(chinese?.Contains("#" + ItemID.MagicMirror) == true, "embedded Chinese and current resource aliases lead to the same identity");
            var recipes = Tool("get_game_item_sources", Field("item_type", (int)ItemID.Torch), Field("kind", "recipe"));
            var pendingRecipe = RunQuery("查制作火把的真实配方。", recipes);
            Require(pendingRecipe?.StartsWith("pending", StringComparison.Ordinal) == true,
                "recipe family wakes separately from the completed item catalog");
            for (int i = 0; i < 192 && !(bool)Get(catalog, "Ready"); i++) nativeStep();
            string torchRecipes = RunQuery("配方准备好了吗？", recipes);
            Require(torchRecipes?.StartsWith("ready", StringComparison.Ordinal) == true && torchRecipes.Contains("recipe:") &&
                torchRecipes.Contains("原料") && torchRecipes.Contains("站点"),
                "published vanilla recipe carries the real identity, materials and station");

            var previousInventory = body.inventory.Take(50).ToArray();
            var previousHuman = human.position;
            int previousLife = body.statLife, previousMaximum = body.statLifeMax;
            var previousMobility = (body.wingsLogic, body.rocketBoots, body.dashType, body.miscEquips[4]);
            var previousClock = (Main.dayTime, Main.time, Main.dayRate);
            bool previousEye = NPC.downedBoss1;
            var previousDrops = Main.ItemDropsDB;
            try
            {
                for (int slot = 0; slot < previousInventory.Length; slot++)
                    if (body.inventory[slot]?.createTile == TileID.Torches) body.inventory[slot] = new Item();
                var route = Tool("choose_game_strategy", Field("goal", "explore"), Field("destination", "here"), Field("stay", true));
                string withoutLight = RunQuery("帮我准备探索这里，到了等我。", route);
                Require(withoutLight?.Contains("照明") == true && !withoutLight.Contains("strategy_id="),
                    "missing actual light prevents a premature exploration plan");
                body.inventory[43] = new Item(ItemID.Torch) { stack = 10 };
                body.inventory[44] = new Item(ItemID.MagicMirror);
                body.inventory[45] = new Item(ItemID.LesserHealingPotion);
                human.position = body.position;
                bool proposed = false, submitted = false;
                remote.Reply = request =>
                {
                    var last = request.Messages.Last();
                    if (last.Kind != LlmMessageKind.ToolResults) return Task.FromResult(CallTool(route));
                    string result = last.Results.Single().Output;
                    if (last.Results.Single().Id == "submit_strategy")
                    {
                        submitted = result.Contains("accepted");
                        return Task.FromResult(reply);
                    }
                    const string token = "strategy_id=";
                    int index = result.IndexOf(token, StringComparison.Ordinal);
                    proposed = index >= 0 && result.Contains("move_to") && result.Contains("stand_by");
                    Require(proposed, "matching real supplies select an actual movement itinerary");
                    return Task.FromResult(CallTool(Tool("submit_strategy", Field("strategy_id", result.Substring(index + token.Length, 32)))));
                };
                send("现在有物资了，去我发送的位置等我。");
                ended();
                object TaskView() => Call(host, "GetTask", profile.OwnerId, profile.Id, null);
                for (int i = 0; i < 60 && (string)Get(TaskView(), "State") != "succeeded"; i++) nativeStep();
                Require(proposed && submitted && (string)Get(TaskView(), "State") == "succeeded" &&
                    (string)Get(TaskView(), "Detail") != null,
                    "the live strategy candidate executes through the registered task owner after the model ends");

                CheckStrategyCraftDelivery(Call(host, "Owned", body), remote, human, body, nativeStep, ended, send, TaskView, RunQuery);
                CheckTaskAuthorityAndPause(host, human, body, profile, nativeStep, RunQuery);

                var boss = Tool("choose_game_strategy", Field("goal", "prepare_boss"), Field("boss", "skeletron"));
                Main.dayTime = false;
                Main.dayRate = 1;
                Main.time = Main.nightLength - 1;
                string lateNight = RunQuery("今晚准备骷髅王。", boss);
                Main.time = 0;
                body.wingsLogic = body.rocketBoots = body.dashType = 0;
                body.miscEquips[4] = new Item();
                string noTravel = RunQuery("今晚准备骷髅王。", boss);
                body.miscEquips[4] = new Item(ItemID.GrapplingHook);
                body.rocketBoots = 1;
                for (int slot = 0; slot < previousInventory.Length; slot++)
                    if (body.inventory[slot]?.healLife > 0) body.inventory[slot] = new Item();
                string noMedicine = RunQuery("今晚准备骷髅王。", boss);
                Require(lateNight?.Contains("窗口不足") == true && noTravel?.Contains("机动手段不足") == true &&
                    noMedicine?.Contains("需要加强可消费补给") == true,
                    "the same boss goal chooses different priorities for the actual night, equipment and inventory: " +
                    String.Join(" | ", new[] { lateNight, noTravel, noMedicine }.Select(value => value?.Split('\n')[0] ?? "no tool response")));

                NPC.downedBoss1 = false;
                body.inventory[45] = new Item(ItemID.LesserHealingPotion);
                body.inventory[47] = new Item(ItemID.WoodenSword);
                var shield = Tool("choose_game_strategy", Field("goal", "prepare_boss"), Field("boss", "eye"),
                    Field("item_type", (int)ItemID.EoCShield));
                string sourcePending = RunQuery("首杀克眼能先用克苏鲁护盾吗？", shield);
                Require(sourcePending?.Contains("物品来源尚未完整建立") == true, "do not claim a first-victory dependency before its sources load");
                if (Main.ItemDropsDB == null)
                {
                    var nativeDrops = new Terraria.GameContent.ItemDropRules.ItemDropDatabase();
                    nativeDrops.Populate();
                    Main.ItemDropsDB = nativeDrops;
                }
                var origin = Get(knowledge, "Sources");
                for (int i = 0; i < 1200 && GetOptional(origin, "Drops") == null; i++) nativeStep();
                string firstEye = RunQuery("首杀克眼能先用克苏鲁护盾吗？", shield);
                Require(firstEye?.Contains("前置循环") == true, "native boss-bag and contained loot resolve the actual first-win dependency: " +
                    firstEye?.Split('\n')[0] + " / " + Get(origin, "DropStatus"));
                body.rocketBoots = 1;
                body.inventory[44] = new Item(ItemID.EoCShield);
                string ownedShield = RunQuery("首杀克眼能先用克苏鲁护盾吗？", shield);
                Require(ownedShield?.Contains("世界首胜状态不改变合法持有事实") == true,
                    "legitimate cross-world inventory overrides the first-win source block");

                body.statLifeMax = 100;
                body.inventory[46] = new Item(ItemID.LifeCrystal) { stack = 2 };
                string twoUses = RunQuery("连续用两颗生命水晶。", Tool("submit_task", Field("title", "使用两颗生命水晶"),
                    Field("steps", new object[] { new Dictionary<string, object> { ["operation"] = "use_item", ["repeat"] = 2,
                        ["arguments"] = new Dictionary<string, object> { ["item_type"] = (int)ItemID.LifeCrystal } } })));
                Require(twoUses?.Contains("accepted") == true, "chat admits multiple stable item-use iterations in one task");
                for (int i = 0; i < 200 && (string)Get(TaskView(), "State") != "succeeded"; i++) nativeStep();
                Require((string)Get(TaskView(), "State") == "succeeded" && body.statLifeMax == 140 && body.inventory[46].IsAir,
                    "two durable native steps resolve fresh inventory and each consume exactly one real growth item");
            }
            finally
            {
                Array.Copy(previousInventory, body.inventory, previousInventory.Length);
                body.statLife = previousLife;
                body.statLifeMax = previousMaximum;
                (body.wingsLogic, body.rocketBoots, body.dashType, body.miscEquips[4]) = previousMobility;
                (Main.dayTime, Main.time, Main.dayRate) = previousClock;
                NPC.downedBoss1 = previousEye;
                Main.ItemDropsDB = previousDrops;
                human.position = previousHuman;
            }
            Console.WriteLine("PASS chat cold shared native catalog, purpose and stable alias, fact-sensitive candidate and actual task settlement.");
        }

        private static void CheckStrategyCraftDelivery(object actor, Transport remote, Player human, Player body,
            Action nativeStep, Action ended, Action<string> send, Func<object> taskView,
            Func<string, LlmToolCall, string> query)
        {
            var actorItems = body.inventory.ToArray();
            var ownerItems = human.inventory.ToArray();
            bool previousNearby = Player.Settings.CraftFromNearbyChests;
            bool previousDay = Main.dayTime;
            int previousSelected = body.selectedItem;
            var feet = body.Bottom.ToTileCoordinates();
            var point = new Point(feet.X + 8, feet.Y - 2);
            int slot = Array.FindIndex(Main.chest, chest => chest == null);
            Require(slot >= 0, "private strategy fixture has a native chest slot");
            var previousTiles = new Tile[2, 2];
            LlmToolCall Choose(bool nearby) => new LlmToolCall("craft-choice", "choose_game_strategy", "{}",
                new Dictionary<string, object> { ["goal"] = "craft", ["item_type"] = (int)ItemID.Torch,
                    ["quantity"] = 3, ["allow_nearby"] = nearby, ["deliver_to_owner"] = true });
            try
            {
                Player.Settings.CraftFromNearbyChests = true;
                for (int i = 0; i < 58; i++)
                {
                    human.inventory[i] = new Item();
                    body.inventory[i] = new Item();
                }
                // Leave a real non-emissive item in the held slot. A newly
                // crafted Torch must not become the active light source in this headless fixture.
                body.inventory[0] = new Item(ItemID.DirtBlock);
                body.selectedItemState.Select(0);
                body.selectedItemState.Update();
                Require(body.selectedItem == 0, "native held slot is settled before making a light source");
                body.inventory[10] = new Item(ItemID.Wood);
                for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                    {
                        previousTiles[x, y] = Main.tile[point.X + x, point.Y + y];
                        var tile = new Tile { type = TileID.Containers, frameX = (short)(18 * x), frameY = (short)(18 * y) };
                        tile.active(true);
                        Main.tile[point.X + x, point.Y + y] = tile;
                    }
                var chest = Chest.CreateWorldChest(slot, point.X, point.Y);
                chest.item[0] = new Item(ItemID.Gel) { stack = 2 };
                Main.dayTime = true;
                var boss = new LlmToolCall("prepare-choice", "choose_game_strategy", "{}", new Dictionary<string, object>
                {
                    ["goal"] = "prepare_boss", ["boss"] = "skeletron", ["item_type"] = (int)ItemID.Torch,
                    ["quantity"] = 3, ["allow_nearby"] = true
                });
                string window = query("白天先准备骷髅王和照明。", boss);
                Require(window?.Contains("窗口不足") == true && window.Contains("原版配方") && !window.Contains("strategy_id="),
                    "the selected boss-window route cannot submit a conflicting craft even when a native recipe is ready: " + window);
                string denied = query("只用已有背包材料做三支火把交给我。", Choose(false));
                Require(denied?.Contains("strategy_id=") == false && chest.item[0].stack == 2,
                    "actual nearby-only materials require explicit authorization");
                string accepted = null;
                remote.Reply = request =>
                {
                    var last = request.Messages.Last();
                    if (last.Kind != LlmMessageKind.ToolResults)
                        return Task.FromResult(new LlmTurn("", new[] { Choose(true) }, "{}"));
                    var result = last.Results.Single();
                    if (result.Id == "submit-craft-choice")
                    {
                        accepted = result.Output;
                        return Task.FromResult(new LlmTurn("已受理本次制作。", Array.Empty<LlmToolCall>(), "{}"));
                    }
                    const string token = "strategy_id=";
                    int index = result.Output.IndexOf(token, StringComparison.Ordinal);
                    Require(index >= 0 && result.Output.Contains("craft_item → transfer_item"), result.Output);
                    return Task.FromResult(new LlmTurn("", new[] { new LlmToolCall("submit-craft-choice", "submit_strategy", "{}",
                        new Dictionary<string, object> { ["strategy_id"] = result.Output.Substring(index + token.Length, 32) }) }, "{}"));
                };
                send("可以从附近箱子用材料制作三支火把，把成品给我。");
                ended();
                Require(accepted?.Contains("accepted") == true, "scripted provider submits the actual craft candidate: " + accepted);
                var receipt = (CompanionTaskSnapshot)taskView();
                for (int i = 0; i < 240 && receipt.IsActive; i++)
                {
                    nativeStep();
                    receipt = (CompanionTaskSnapshot)taskView();
                }
                Require(receipt.State == "succeeded" && receipt.Attempts.Count == 2 &&
                    receipt.Attempts[0].Operation == "craft_item" && receipt.Attempts[0].Completed == 3 &&
                    receipt.Attempts[1].Operation == "transfer_item" && receipt.Attempts[1].Completed == 3 &&
                    human.inventory.Where(item => item.type == ItemID.Torch).Sum(item => item.stack) == 3 &&
                    !body.inventory.Any(item => item.type == ItemID.Torch) && body.inventory[10].IsAir && chest.item[0].stack == 1,
                    "native strategy used real actor wood and nearby Gel and delivered the verified output: " + receipt.Detail +
                    " / " + GetOptional(actor, "TerminalFailure") + " / " +
                    String.Join(" | ", receipt.Attempts.Select(attempt => attempt.Operation + " " + attempt.State + " " + attempt.Detail)));
                Console.WriteLine("PASS scripted chat choose and submit: authorized nearby vanilla craft and exact owner delivery.");
            }
            finally
            {
                if (Main.chest[slot] != null) Chest.RemoveChest(slot);
                for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                        if (previousTiles[x, y] != null) Main.tile[point.X + x, point.Y + y] = previousTiles[x, y];
                Array.Copy(actorItems, body.inventory, actorItems.Length);
                Array.Copy(ownerItems, human.inventory, ownerItems.Length);
                Player.Settings.CraftFromNearbyChests = previousNearby;
                Main.dayTime = previousDay;
                body.selectedItemState.Select(previousSelected);
                body.selectedItemState.Update();
            }
        }

        private static void CheckTaskAuthorityAndPause(object host, Player human, Player body, CompanionProfile profile,
            Action nativeStep, Func<string, LlmToolCall, string> query)
        {
            var actor = Call(host, "Owned", body);
            var identity = new CompanionTaskIdentity(profile.OwnerId, profile.Id, (long)Get(host, "session"),
                (long)Get(actor, "Generation"), profile.DataEpoch);
            CompanionTaskSnapshot View() => (CompanionTaskSnapshot)Call(host, "GetTask", profile.OwnerId, profile.Id, null);
            LlmToolCall Tool(string name, params (string Key, object Value)[] fields) => new LlmToolCall(name, name, "{}",
                fields.ToDictionary(field => field.Key, field => field.Value));
            IReadOnlyDictionary<string, object> Step(string name, Dictionary<string, object> args) =>
                new Dictionary<string, object> { ["operation"] = name, ["arguments"] = args };
            string Submit(string prompt, params IReadOnlyDictionary<string, object>[] steps) =>
                query(prompt, Tool("submit_task", ("title", "验证原版队友任务"), ("steps", steps.Cast<object>().ToArray())));
            var initialItems = body.inventory.ToArray();
            var ownerItems = human.inventory.ToArray();
            var previousHuman = human.position;
            int life = body.statLife, cap = body.statLifeMax, delay = body.potionDelay;
            var buffTypes = body.buffType.ToArray();
            var buffTimes = body.buffTime.ToArray();
            var feet = body.Bottom.ToTileCoordinates();
            int x = feet.X + 2, y = feet.Y - 1;
            var previousTile = Main.tile[x, y];
            var previousSupport = Main.tile[x, y + 1];
            try
            {
                human.position = body.position;
                human.inventory[46] = new Item(ItemID.StoneBlock) { stack = 3 };
                // Give each pause case exactly one legal source, including
                // the preparation potion deliberately put elsewhere upstream.
                for (int i = 0; i < 50; i++)
                    if (body.inventory[i].type == ItemID.LesserHealingPotion || body.inventory[i].type == ItemID.LifeCrystal)
                        body.inventory[i] = new Item();
                int actorStone = body.inventory.Where(item => item.type == ItemID.StoneBlock).Sum(item => item.stack);
                Main.tile[x, y] = new Tile();
                Main.tile[x, y + 1] = new Tile { type = TileID.Stone };
                Main.tile[x, y + 1].active(true);
                string observed = query("请查这里能放东西的空格。", Tool("observe_game", ("operation", "observe_tiles"),
                    ("arguments", new Dictionary<string, object> { ["around"] = "here", ["radius"] = 3,
                        ["empty"] = true, ["offset"] = 19 })));
                string row = observed?.Split('；').FirstOrDefault(part => part.Contains("(" + x + "," + y + ")"));
                Require(row != null, "chat receives an actual known empty-cell reference: " + observed);
                string target = row.Substring(row.LastIndexOf("· ", StringComparison.Ordinal) + 2).Split(' ')[0];
                var transfer = Step("transfer_item", new Dictionary<string, object> { ["item_type"] = (int)ItemID.StoneBlock,
                    ["amount"] = 1, ["source"] = "owner_inventory", ["destination"] = "self_inventory" });
                var place = Step("world_use", new Dictionary<string, object> { ["target"] = target,
                    ["action"] = "place_tile", ["item_type"] = (int)ItemID.StoneBlock });
                var anchor = Call(host, "CaptureTaskSendAnchor", human);
                var prior = (CompanionTaskSnapshot)Call(host, "SubmitTask", identity, "继续先前任务",
                    new[] { new CompanionTaskStep("stand_by", new Dictionary<string, object>()) }, anchor, Guid.NewGuid());
                const string readOnly = "本条消息没有明确委托";
                string denied = Submit("如果从我背包拿一块石头再放在这里，会怎么样？", transfer, place);
                string noStop = query("你目前的任务会自己停止吗？", Tool("stop_task", ("task_id", prior.TaskId.ToString("N"))));
                string noAppend = query("当前能追加哪些步骤？", Tool("append_task", ("task_id", prior.TaskId.ToString("N")),
                    ("steps", new object[] { transfer })));
                string noExecution = query("放置石块会花多少石头？", Tool("execute_operation", ("title", "放石块"),
                    ("operation", "world_use"), ("arguments", place["arguments"])));
                Require(new[] { denied, noStop, noAppend, noExecution }.All(result => result?.Contains(readOnly) == true) &&
                    View().TaskId == prior.TaskId && View().StepCount == 1 && human.inventory[46].stack == 3 &&
                    !Main.tile[x, y].active(), "ordinary queries cannot modify current tasks, owner inventory or the real tile");
                string badBudget = query("买一支火把，最多1银币。", Tool("execute_operation", ("title", "购买"),
                    ("operation", "buy_item"), ("arguments", new Dictionary<string, object>
                        { ["item_type"] = (int)ItemID.Torch, ["amount"] = 1, ["budget_copper"] = 101 })));
                Require(badBudget?.Contains(readOnly) == true && View().TaskId == prior.TaskId,
                    "a scripted model cannot raise a direct player's native money cap");
                const string ownerCommand = "从我的背包拿一块石块，放在刚看到的空格。";
                var oversized = Step("transfer_item", new Dictionary<string, object> { ["item_type"] = (int)ItemID.StoneBlock,
                    ["amount"] = 999, ["source"] = "owner_inventory", ["destination"] = "self_inventory" });
                var different = Step("transfer_item", new Dictionary<string, object> { ["item_type"] = (int)ItemID.Wood,
                    ["amount"] = 1, ["source"] = "owner_inventory", ["destination"] = "self_inventory" });
                Require(Submit(ownerCommand, oversized, place)?.Contains(readOnly) == true &&
                    Submit(ownerCommand, different, place)?.Contains(readOnly) == true && View().TaskId == prior.TaskId,
                    "a real owner command authorizes only the named item and explicitly bounded quantity");
                string accepted = Submit(ownerCommand, transfer, place);
                Require(accepted?.Contains("accepted") == true, "explicit current-turn owner resource and world commands are accepted: " + accepted);
                Finish();
                Require(View().State == "succeeded" && View().Attempts.Count == 2 && Main.tile[x, y].active() &&
                    human.inventory[46].stack == 2 && body.inventory.Where(item => item.type == ItemID.StoneBlock).Sum(item => item.stack) == actorStone,
                    "the approved script performs both the true owner-inventory debit and native placement: " + View().Detail);

                var prepare = Step("equip_item", new Dictionary<string, object> { ["item_type"] = (int)ItemID.GrapplingHook });
                Require(Submit("帮我准备打骷髅王。", prepare)?.Contains("accepted") == true,
                    "the direct boss-preparation request can select actual companion equipment");
                var summon = Step("trigger_event", new Dictionary<string, object> { ["item_type"] = (int)ItemID.SuspiciousLookingEye });
                Require(Submit("帮我准备打骷髅王。", summon)?.Contains(readOnly) == true,
                    "preparing for one boss does not authorize a summoning operation");
                var fish = Step("fish_at", new Dictionary<string, object> { ["destination"] = "right_ocean",
                    ["quest_fish"] = true, ["quantity"] = 1 });
                var back = Step("move_to", new Dictionary<string, object> { ["destination"] = "owner" });
                var wait = Step("stand_by", new Dictionary<string, object>());
                Require(Submit("去右海边钓任务鱼，完成后回来等我。", fish, back, wait)?.Contains("accepted") == true &&
                    View().StepCount == 3, "the requested quest fishing and return compose into the existing typed task");
                Require(Submit("如果去右海边钓任务鱼，完成后回来等我，会怎样？", fish, back, wait)?.Contains(readOnly) == true &&
                    Submit("如果去右海边钓任务鱼，完成后回来等我。", wait)?.Contains(readOnly) == true &&
                    Submit("不要去右海边钓任务鱼。", fish, back, wait)?.Contains(readOnly) == true,
                    "conditional advice and negated fishing do not retire the existing itinerary");

                body.statLife = 1;
                // A prior real use carries the native Potion Sickness buff,
                // which restores potionDelay during Player.Update.
                body.ClearBuff(BuffID.PotionSickness);
                body.potionDelay = 0;
                body.inventory[49] = new Item(ItemID.LesserHealingPotion) { stack = 2 };
                var quick = QueueUse("用一次治疗药水。", ItemID.LesserHealingPotion);
                Pause(quick, true);
                for (int i = 0; i < 8; i++) nativeStep();
                Require(View().State == "paused" && body.statLife == 1 && body.inventory[49].stack == 2,
                    "paused QuickHeal queue leaves native life and potion count unchanged");
                Pause(quick, false);
                Finish();
                Require(View().State == "succeeded" && View().Attempts.Count == 1 &&
                    body.statLife > 1 && body.inventory[49].stack == 1, "resume runs native QuickHeal once: " +
                    View().Detail + " / " + String.Join(" | ", View().Attempts.Select(attempt => attempt.State + " " + attempt.Detail)) +
                    " / life=" + body.statLife + " potion=" + body.inventory[49].stack + " delay=" + body.potionDelay);

                body.statLifeMax = 100;
                body.inventory[49] = new Item(ItemID.LifeCrystal) { stack = 2 };
                var held = QueueUse("用一颗生命水晶。", ItemID.LifeCrystal);
                Pause(held, true);
                for (int i = 0; i < 8; i++) nativeStep();
                Require(View().State == "paused" && body.statLifeMax == 100 && body.inventory[49].stack == 2,
                    "paused held-item queue does not enter native ItemCheck");
                Call(host, "StopTask", profile.OwnerId, profile.Id, held);
                for (int i = 0; i < 4; i++) nativeStep();
                Require(body.inventory[49].stack == 2 && body.statLifeMax == 100 &&
                    GetOptional(Get(actor, "Supplies"), "Once") == null,
                    "stopping a paused held queue withdraws it without native effects");
                var resumed = QueueUse("用一颗生命水晶。", ItemID.LifeCrystal);
                Pause(resumed, true);
                Pause(resumed, false);
                Finish();
                Require(View().State == "succeeded" && View().Attempts.Count == 1 &&
                    body.statLifeMax == 120 && body.inventory[49].stack == 1, "resumed held use enters native ItemCheck once");
                Console.WriteLine("PASS chat authority: read-only model errors preserve task/assets, explicit transfer and native placement, pause/resume QuickHeal and held growth.");
            }
            finally
            {
                Call(host, "StopTask", profile.OwnerId, profile.Id, null);
                Array.Copy(initialItems, body.inventory, initialItems.Length);
                Array.Copy(ownerItems, human.inventory, ownerItems.Length);
                human.position = previousHuman;
                Main.tile[x, y] = previousTile;
                Main.tile[x, y + 1] = previousSupport;
                body.statLife = life;
                body.statLifeMax = cap;
                body.potionDelay = delay;
                Array.Copy(buffTypes, body.buffType, buffTypes.Length);
                Array.Copy(buffTimes, body.buffTime, buffTimes.Length);
            }

            Guid QueueUse(string prompt, int type)
            {
                string accepted = Submit(prompt, Step("use_item", new Dictionary<string, object> { ["item_type"] = type }));
                Require(accepted?.Contains("accepted") == true, "native task action admitted for pause fixture: " + accepted);
                Call(host, "PumpTask", actor);
                var queued = GetOptional(Get(actor, "Supplies"), "Once");
                Require(queued != null && !(bool)Get(Get(queued, "Task"), "Issued"),
                    "staging a pending hand leaves the original attempt unsubmitted");
                return View().TaskId;
            }
            void Pause(Guid id, bool paused)
            {
                var result = (CompanionTaskSnapshot)Call(host, "PauseTask", identity, id, paused);
                Require(result.State != "rejected", "original pause/resume accepted against the same task: " + result.Detail);
            }
            void Finish()
            {
                for (int tick = 0; tick < 240 && View().IsActive; tick++) nativeStep();
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
