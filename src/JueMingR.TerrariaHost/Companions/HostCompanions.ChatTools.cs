using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Llm;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private static readonly LlmToolDefinition[] ChatTools =
        {
            ChatTool("get_companion_state", "Read the companion's current status and activity."),
            ChatTool("query_inventory", "List a page of items and source references. Teleport items report their native destination in their current form.",
                "\"area\":{\"type\":\"string\",\"enum\":[\"inventory\",\"piggy_bank\",\"safe\",\"defender_forge\",\"void_vault\"]}," +
                "\"filter\":{\"type\":\"string\",\"maxLength\":64},\"offset\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":58}"),
            ChatTool("set_behavior", "Set following or staying and/or change whether the companion attacks.",
                "\"mode\":{\"type\":\"string\",\"enum\":[\"follow\",\"stay\"]},\"attack\":{\"type\":\"boolean\"}"),
            ChatTool("start_fishing", "Start the existing fishing activity at the feet/seat captured when the user sent this message.",
                "\"notify_no_bait\":{\"type\":\"boolean\"}"),
            ChatTool("stop_activity", "Stop fishing and optionally set the subsequent movement mode.",
                "\"mode\":{\"type\":\"string\",\"enum\":[\"follow\",\"stay\"]}"),
            ChatTool("use_item_once", "Use one item from the companion's main inventory using a source from query_inventory. " +
                "For returning to the companion's own spawn, use a suitable mirror, phone or recall potion reported by inventory. " +
                "Set stay_after_teleport only when the player asks to wait after arriving; it applies after native confirmation.",
                "\"source\":{\"type\":\"string\"},\"stay_after_teleport\":{\"type\":\"boolean\"}", ",\"required\":[\"source\"]"),
            ChatTool("manual_recall", "Use the existing manual wormhole recall to the owner, not to the companion's spawn. May consume one wormhole potion."),
            ChatTool("get_action_status", "Read a recent game operation or fishing activity by its returned identifier.",
                "\"id\":{\"type\":\"string\"}", ",\"required\":[\"id\"]")
        };

        private static LlmToolDefinition ChatTool(string name, string description, string fields = "", string required = "") =>
            new LlmToolDefinition(name, description,
                "{\"type\":\"object\",\"properties\":{" + fields + "},\"additionalProperties\":false" + required + "}");

        private readonly struct ChatSource
        {
            internal ChatSource(int slot, Item item)
            { Slot = slot; Item = item; Type = item.type; Name = item.Name; Prefix = item.prefix; Stack = item.stack; }
            internal int Slot { get; }
            internal Item Item { get; }
            internal int Type { get; }
            internal string Name { get; }
            internal byte Prefix { get; }
            internal int Stack { get; }
            internal bool Matches(Item current) => ReferenceEquals(Item, current) &&
                current.type == Type && current.prefix == Prefix && current.stack == Stack;
        }

        private sealed class ChatResult
        {
            internal string Id, Title, State, Text, TaskId;
            internal int ItemType;
            internal string ItemName, SourceToken;
            internal bool StayAfterTeleport;
            internal bool Settled, AwaitingNativeSettlement;
            internal bool IsError => State == "failed" || State == "rejected" || State == "cancelled" ||
                State == "superseded" || State == "unconfirmed";
            internal string Output() => State + " · 操作 " + Id +
                (ItemType == 0 ? "" : " · " + ItemName + "（类型 " + ItemType + "）") +
                (TaskId == null ? "" : " · 活动 " + TaskId) + " · " + Text;
            internal string Label()
            {
                string status;
                switch (State)
                {
                    case "accepted": status = "已受理"; break;
                    case "running": status = "进行中"; break;
                    case "succeeded": status = "已完成"; break;
                    case "cancelled": status = "已取消"; break;
                    case "superseded": status = "已被替换"; break;
                    case "unconfirmed": status = "结果未确认"; break;
                    default: status = "未完成"; break;
                }
                return Title + " · " + status + (String.IsNullOrEmpty(Text) ? "" : " · " + Text);
            }
        }

        private void DispatchChatTool(ChatCommand command)
        {
            var run = command.Run;
            var chat = run.Conversation;
            var call = command.Call;
            var args = call.Arguments;
            if (!run.Boundary.CanAct || !TryFishingActor(chat.Owner, chat.Profile, out var actor))
            {
                RejectChatTool(command, "队友当前无法执行游戏操作。");
                return;
            }
            if (args == null)
            {
                RejectChatTool(command, "工具参数必须是完整对象。");
                return;
            }
            chat.Status = "正在等待游戏操作结果。";
            try
            {
                switch (call.Name)
                {
                    case "get_companion_state":
                        if (!ChatFields(args)) break;
                        string time = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                        string movement = actor.Fishing != null ? actor.Fishing.Status ?? "正在前往钓位" :
                            actor.Preferences.Follow ? "跟随" : "待命";
                        CompleteChatQuery(command, actor.Body.name + "，" + movement +
                            "，自动攻击：" + (actor.Preferences.AttackEnabled ? "开" : "关") +
                            "，生命 " + actor.Body.statLife + "/" + actor.Body.statLifeMax2 +
                            "，魔力 " + actor.Body.statMana + "/" + actor.Body.statManaMax2 +
                            "。可查询背包内的原版传送物品及其目的地。观察时间（UTC）" + time);
                        return;
                    case "query_inventory":
                        if (!ChatFields(args, "area", "filter", "offset") ||
                            !ChatString(args, "area", 24, out var area) || !ChatString(args, "filter", 64, out var filter) ||
                            !ChatOffset(args, out int offset)) break;
                        QueryChatInventory(command, actor, area, filter, offset);
                        return;
                    case "set_behavior":
                    case "stop_activity":
                        bool stop = call.Name == "stop_activity";
                        if (!(stop ? ChatFields(args, "mode") : ChatFields(args, "mode", "attack")) ||
                            !ChatString(args, "mode", 8, out var mode) || mode != null && mode != "follow" && mode != "stay" ||
                            !ChatBool(args, "attack", out var attack) || !stop && mode == null && !attack.HasValue) break;
                        ChangeChatBehavior(command, actor, mode, attack, stop);
                        return;
                    case "start_fishing":
                        if (!ChatFields(args, "notify_no_bait") || !ChatBool(args, "notify_no_bait", out var notify)) break;
                        StartChatFishing(command, actor, notify == true);
                        return;
                    case "use_item_once":
                        if (!ChatFields(args, "source", "stay_after_teleport") || !ChatString(args, "source", 32, out var source) ||
                            source == null || !ChatBool(args, "stay_after_teleport", out var stay)) break;
                        UseChatItem(command, actor, source, stay == true);
                        return;
                    case "manual_recall":
                        if (!ChatFields(args)) break;
                        RecallChatActor(command, actor);
                        return;
                    case "get_action_status":
                        if (!ChatFields(args, "id") || !ChatString(args, "id", 32, out var id) || id == null) break;
                        if (actor.Fishing?.ChatTaskId == id)
                            CompleteChatQuery(command, "running · 活动 " + id + " · " + (actor.Fishing.Status ?? "正在前往钓位"));
                        else if (chat.Actions.TryGetValue(id, out var operation) ||
                            (operation = chat.Actions.Values.FirstOrDefault(candidate => candidate.TaskId == id)) != null)
                            CompleteChatQuery(command, operation.Output(), error: operation.IsError);
                        else RejectChatTool(command, "本会话中没有这项操作。");
                        return;
                }
                RejectChatTool(command, "工具名称或参数不受支持。");
            }
            catch (Exception)
            {
                // A native throw after admission has unknown effects. The
                // action remains recorded and must not be submitted again.
                if (chat.Actions.TryGetValue(OperationId(command), out var operation))
                    CompleteChatAction(command, operation, "unconfirmed", "游戏操作结果未确认。");
                else RejectChatTool(command, "无法读取当前队友状态。");
            }
        }

        private static bool ChatFields(IReadOnlyDictionary<string, object> args, params string[] allowed)
        {
            foreach (var field in args.Keys) if (Array.IndexOf(allowed, field) < 0) return false;
            return true;
        }

        private static bool ChatString(IReadOnlyDictionary<string, object> args, string field, int max, out string result)
        {
            result = null;
            return !args.TryGetValue(field, out var value) ||
                (result = value as string) != null && result.Length <= max;
        }

        private static bool ChatBool(IReadOnlyDictionary<string, object> args, string field, out bool? result)
        {
            result = null;
            if (!args.TryGetValue(field, out var value)) return true;
            if (!(value is bool boolean)) return false;
            result = boolean;
            return true;
        }

        private static bool ChatOffset(IReadOnlyDictionary<string, object> args, out int offset)
        {
            offset = 0;
            if (!args.TryGetValue("offset", out var value)) return true;
            if (!(value is int number) || number < 0 || number > 58) return false;
            offset = number;
            return true;
        }

        private void CompleteChatQuery(ChatCommand command, string text, string publicText = null, bool error = false)
        {
            AddChatEntry(command.Run.Conversation, "query", publicText ?? text);
            command.Reply.TrySetResult(new LlmToolResult(command.Call.Id, text, error));
        }

        private static void RejectChatTool(ChatCommand command, string text) =>
            command.Reply.TrySetResult(new LlmToolResult(command.Call.Id, "rejected：" + text, true));

        // A result card is visible at admission and replaced in-place on
        // settlement. Cancellation may retire the requesting model and still
        // leave this callback owned by the original conversation.
        private ChatResult NewChatAction(ChatCommand command, string text, string dedup = null)
        {
            var run = command.Run;
            if (dedup != null && run.Consumed.TryGetValue(dedup, out var previous))
            {
                command.Reply.TrySetResult(new LlmToolResult(command.Call.Id, previous.Output(), previous.IsError));
                return null;
            }
            var result = new ChatResult { Id = OperationId(command), State = "accepted", Title = text };
            var chat = run.Conversation;
            if (chat.ActionOrder.Count >= 64) chat.Actions.Remove(chat.ActionOrder.Dequeue());
            chat.Actions[result.Id] = result;
            chat.ActionOrder.Enqueue(result.Id);
            chat.PendingActions++;
            AddChatEntry(chat, "action", result.Label(), Guid.ParseExact(result.Id, "N"));
            if (dedup != null) run.Consumed.Add(dedup, result);
            return result;
        }

        private static string OperationId(ChatCommand command)
        {
            if (command.Operation == null) command.Operation = Guid.NewGuid().ToString("N");
            return command.Operation;
        }

        private void CompleteChatAction(ChatCommand command, ChatResult result, string state, string text, bool waitForNative = false)
        {
            if (result.Settled) return;
            result.Settled = true;
            result.AwaitingNativeSettlement = waitForNative;
            UpdateChatAction(command.Run, result, state, text);
            var run = command.Run;
            if (!waitForNative) run.Conversation.PendingActions--;
            if (run.Conversation.Current == run) run.Conversation.Status = "正在继续模型回复。";
            command.Reply.TrySetResult(new LlmToolResult(command.Call.Id, result.Output(), result.IsError));
        }

        private void UpdateChatAction(ChatRun run, ChatResult result, string state, string text, bool recover = false)
        {
            result.State = state;
            result.Text = text;
            if (run.Conversation.HistoryEpoch == run.HistoryEpoch)
            {
                AddChatEntry(run.Conversation, "action", result.Label(), Guid.ParseExact(result.Id, "N"),
                    recover || run.Conversation.Current != run);
            }
        }

        private void ChangeChatBehavior(ChatCommand command, Actor actor, string mode, bool? attack, bool stop)
        {
            var action = NewChatAction(command, "切换活动");
            bool endedFishing = false;
            if ((stop || mode != null) && actor.Fishing != null)
            {
                endedFishing = true;
                EndFishingActivity(actor);
            }
            var behavior = actor.Profile.Behavior;
            if (mode != null) behavior.Follow = mode == "follow";
            if (attack.HasValue) behavior.AttackEnabled = attack.Value;
            bool changed = mode != null || attack.HasValue;
            if (changed && !ApplyBehavior(actor.Profile.Id, behavior, manual: false))
                CompleteChatAction(command, action, "failed", "未能应用队友行为。" + (endedFishing ? "钓鱼已停止。" : ""));
            else
                CompleteChatAction(command, action, "succeeded", (endedFishing ? "当前钓鱼已停止。" : "") +
                    "移动：" + (behavior.Follow ? "跟随" : "待命") + "，自动攻击：" + (behavior.AttackEnabled ? "开" : "关") + "。");
        }

        internal bool StopChatActivity(Guid ownerId, Guid profileId)
        {
            if (ChatProfile(ownerId, profileId) == null || !TryFishingActor(ownerId, profileId, out var actor))
                return false;
            InvalidateChatAction(profileId, "已停止当前活动的待执行模型指令。");
            bool fishing = actor.Fishing != null;
            if (fishing && !EndFishingActivity(actor)) return false;
            var behavior = actor.Profile.Behavior;
            bool following = behavior.Follow;
            behavior.Follow = false;
            if (following && !ApplyBehavior(profileId, behavior, manual: false)) return false;
            if ((fishing || following) && chats.TryGetValue((ownerId, profileId), out var chat))
                AddChatEntry(chat, "status", fishing ? "玩家已停止钓鱼，队友原地待命。" : "玩家已让队友原地待命。",
                    recover: true);
            return true;
        }

        private void InvalidateChatAction(Guid profileId, string reason)
        {
            if (chats.TryGetValue((profiles.OwnerId, profileId), out var chat) && chat.Current != null)
                StopChat(chat, reason, true);
        }

        private void StartChatFishing(ChatCommand command, Actor actor, bool notify)
        {
            var run = command.Run;
            var action = NewChatAction(command, "开始钓鱼");
            if (actor.Fishing != null)
            {
                action.TaskId = actor.Fishing.ChatTaskId;
                var original = actor.Fishing.RequestedFeet.ToTileCoordinates();
                CompleteChatAction(command, action, "running", "队友仍在原钓位（" + original.X + "，" + original.Y +
                    "）。未切换到本条消息的位置，请先停止现有钓鱼活动。");
                return;
            }
            if (!CanStartFishing(actor.Profile.OwnerId, actor.Profile.Id) || !run.Anchor.Valid)
            { CompleteChatAction(command, action, "failed", "队友当前不能在发送位置开始钓鱼。"); return; }
            if (!TryFishingRod(actor, -1, out _))
            { CompleteChatAction(command, action, "failed", "队友没有可用鱼竿。"); return; }
            FishingNativeBait(actor.Body, out int bait, out _);
            if (bait <= 0)
            { CompleteChatAction(command, action, "failed", "队友没有可用鱼饵。"); return; }
            action.TaskId = Guid.NewGuid().ToString("N");
            var activity = BeginFishingActivity(actor, run.Anchor);
            activity.ChatTaskId = action.TaskId;
            var conversation = run.Conversation;
            conversation.FishingTracked = true;
            activity.ChatCastConfirmed = () =>
                UpdateChatAction(run, action, "running", "钓线已进入可用水域。", recover: true);
            activity.ChatEnded = () =>
            {
                conversation.FishingTracked = false;
                UpdateChatAction(run, action, "succeeded", "钓鱼活动已停止。", recover: true);
            };
            string name = actor.Body.name;
            activity.ChatNeedsBait = () =>
            {
                if (conversation.HistoryEpoch != run.HistoryEpoch || conversation.Deleted) return;
                string message = name + " · 缺少可用鱼饵。";
                if (notify && run.Connection.LocalNoticesEnabled)
                {
                    AddChatEntry(conversation, "notice", message, recover: true);
                    Main.NewText(message);
                }
                else UpdateChatAction(run, action, "running", message, recover: true);
            };
            var tile = run.Anchor.Feet.ToTileCoordinates();
            CompleteChatAction(command, action, "running", "已开始前往发送时的钓位（" + tile.X + "，" + tile.Y + "）。");
        }

        private void QueryChatInventory(ChatCommand command, Actor actor, string area, string filter, int offset)
        {
            CompanionInventoryArea inventory;
            switch (area)
            {
                case null:
                case "inventory": inventory = CompanionInventoryArea.Inventory; break;
                case "piggy_bank": inventory = CompanionInventoryArea.PiggyBank; break;
                case "safe": inventory = CompanionInventoryArea.Safe; break;
                case "defender_forge": inventory = CompanionInventoryArea.DefenderForge; break;
                case "void_vault": inventory = CompanionInventoryArea.VoidVault; break;
                default: RejectChatTool(command, "当前仓库类别不受支持。"); return;
            }
            if (CompanionResources.IsBank(inventory))
            {
                // Query does not change the window's chosen bank or item
                // gesture. Use its underlying native provider check directly.
                if (!ActorTileReachReady(actor.Body) ||
                    inventory == CompanionInventoryArea.VoidVault && actor.Body.disableVoidBag >= 0)
                { RejectChatTool(command, "队友的储存访问尚未就绪。"); return; }
                if (SearchBank(actor, inventory) == null)
                { RejectChatTool(command, MissingBank(inventory)); return; }
            }
            var items = CompanionResources.Items(actor.Body, inventory);
            var rows = new List<string>();
            var observed = new List<string>();
            int length = inventory == CompanionInventoryArea.Inventory ? Math.Min(58, items.Length) : items.Length;
            for (int slot = offset; slot < length; slot++)
            {
                var item = items[slot];
                if (item == null || item.IsAir || !String.IsNullOrEmpty(filter) &&
                    item.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (rows.Count == 12) { rows.Add("下一页 offset=" + slot); break; }
                string line = "槽 " + slot + " · " + item.Name + "（类型 " + item.type + "，前缀 " + item.prefix + "）×" + item.stack;
                observed.Add(item.Name + " ×" + item.stack);
                var destination = ItemTeleportDestination(ItemTeleportKind(item.type));
                if (destination != null) line += " · 原版传送：" + destination;
                if (inventory == CompanionInventoryArea.Inventory && slot < CompanionBehavior.MainInventorySlots &&
                    command.Run.Sources.Count < 128)
                {
                    string reference = Guid.NewGuid().ToString("N");
                    command.Run.Sources.Add(reference, new ChatSource(slot, item));
                    line += " · source=" + reference;
                }
                rows.Add(line);
            }
            CompleteChatQuery(command, rows.Count == 0 ? "这一页没有匹配的物品。" : String.Join("\n", rows),
                "查看物品 · " + (observed.Count == 0 ? "这一页没有匹配的物品。" : String.Join("、", observed)));
        }

        private void UseChatItem(ChatCommand command, Actor actor, string source, bool stayAfterTeleport)
        {
            const string useIntent = "item-once";
            if (!command.Run.Sources.TryGetValue(source, out var recorded))
            { RejectChatTool(command, "请先查询背包并使用本轮返回的物品来源。"); return; }
            if (command.Run.Consumed.TryGetValue(useIntent, out var existing))
            {
                if (existing.ItemType == recorded.Type && existing.StayAfterTeleport == stayAfterTeleport &&
                    (existing.SourceToken == source || recorded.Matches(actor.Body.inventory[recorded.Slot])))
                    command.Reply.TrySetResult(new LlmToolResult(command.Call.Id, existing.Output(), existing.IsError));
                else RejectChatTool(command, "本条消息只受理一次用物。未执行本次请求（" + recorded.Name +
                    "，类型 " + recorded.Type + "）。已受理的是 " + existing.Output());
                return;
            }
            var action = NewChatAction(command, "使用 " + recorded.Name);
            action.ItemType = recorded.Type;
            action.ItemName = recorded.Name;
            action.SourceToken = source;
            action.StayAfterTeleport = stayAfterTeleport;
            if (!recorded.Matches(actor.Body.inventory[recorded.Slot]))
            { CompleteChatAction(command, action, "rejected", "物品来源已变化，本次请求未执行。"); return; }
            if (stayAfterTeleport && ItemTeleportKind(recorded.Type) == ChatItemTeleport.None)
            { CompleteChatAction(command, action, "rejected", "该物品没有可确认的传送目的地，本次请求未执行。"); return; }
            if (actor.Supplies.ChatTeleportPending != null && ItemTeleportStyle(ItemTeleportKind(recorded.Type)) >= 0)
            { CompleteChatAction(command, action, "rejected", "上一次原版传送尚未结束同步，本次请求未执行。"); return; }
            if (!CanUseItemOnce(actor.Profile.Id, CompanionInventoryArea.Inventory, recorded.Slot, out var reason) ||
                !TryQueueItemOnce(actor, recorded.Slot, false, out reason))
            { CompleteChatAction(command, action, "rejected", reason); return; }
            // Once the native scheduler accepts a use, this user command has
            // spent its one-use authority. A new slot, source token or call
            // ID cannot obtain another actual hand action.
            command.Run.Consumed.Add(useIntent, action);
            var once = actor.Supplies.Once;
            once.ChatRunId = command.Run.Id;
            once.ChatCancellation = command.Cancellation;
            once.ChatActivation = command.Run.Boundary.Activation;
            once.ChatDataEpoch = command.Run.Boundary.DataEpoch;
            once.ChatStayAfterTeleport = stayAfterTeleport;
            once.ChatCanStay = () => command.Run.Conversation.Current == command.Run && ChatIdentityCurrent(command.Run);
            once.ChatStarted = message => UpdateChatAction(command.Run, action, "running", message);
            once.ChatComplete = (state, message) =>
            {
                if (!action.Settled)
                    CompleteChatAction(command, action, state, message, waitForNative:
                        state == "unconfirmed" && ReferenceEquals(actor.Supplies.ChatTeleportPending, once));
                else if (action.AwaitingNativeSettlement)
                {
                    action.AwaitingNativeSettlement = false;
                    command.Run.Conversation.PendingActions--;
                    if (action.State == "unconfirmed" && (state == "succeeded" || state == "failed"))
                        UpdateChatAction(command.Run, action, state, message, recover: true);
                }
            };
        }

        private void RecallChatActor(ChatCommand command, Actor actor)
        {
            var action = NewChatAction(command, "召回队友", "recall");
            if (action == null) return;
            TryRecall(actor.Profile.Id, automatic: false,
                chatResult: (state, message, waitingForNative) =>
                {
                    if (action.Settled)
                    {
                        // Only the retained original native window can call
                        // back after the tool result has already settled.
                        if (action.AwaitingNativeSettlement)
                        {
                            action.AwaitingNativeSettlement = false;
                            command.Run.Conversation.PendingActions--;
                        }
                        if (action.State == "unconfirmed" && state == "succeeded")
                            UpdateChatAction(command.Run, action, state, message, recover: true);
                    }
                    else if (state == "running")
                        UpdateChatAction(command.Run, action, state, message);
                    else CompleteChatAction(command, action, state, message, waitForNative: waitingForNative);
                });
        }
    }
}
