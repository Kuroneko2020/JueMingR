using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Llm;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private const string TaskIdField = "\"task_id\":{\"type\":\"string\"}";
        private const string OperationField = "\"operation\":{\"type\":\"string\",\"maxLength\":64}";
        private const string StepFields = OperationField + ",\"arguments\":{\"type\":\"object\"}," +
            "\"repeat\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":64}," +
            "\"condition\":{\"type\":\"object\",\"properties\":{" +
                "\"observation\":{\"type\":\"string\"},\"arguments\":{\"type\":\"object\"}," +
                "\"expected\":{\"type\":\"boolean\"},\"wait\":{\"type\":\"boolean\"},\"timeout_ticks\":{\"type\":\"integer\"}}," +
                "\"required\":[\"observation\"],\"additionalProperties\":false}," +
            "\"each\":{\"type\":\"array\",\"items\":{\"type\":\"object\"}}";
        private const string StepsField = "\"steps\":{\"type\":\"array\",\"maxItems\":32,\"items\":{\"type\":\"object\",\"properties\":{" +
            StepFields + "},\"required\":[\"operation\"],\"additionalProperties\":false}}";
        private const string EditStepsField = "\"steps\":{\"type\":\"array\",\"maxItems\":32,\"items\":{\"type\":\"object\"}}";

        private static readonly LlmToolDefinition[] TaskChatTools =
        {
            ChatTool("list_capabilities", "List registered real game operations and observations. Use filter, offset and pagination.",
                "\"filter\":{\"type\":\"string\"},\"offset\":{\"type\":\"integer\",\"minimum\":0}"),
            ChatTool("describe_capability", "Read a registered operation's usage and typed parameter schema before planning.",
                OperationField, ",\"required\":[\"operation\"]"),
            ChatTool("submit_task", "After an explicit player request, start a durable sequence of registered operations. " +
                "Use registered IDs and strong parameters. Steps may repeat, bind each member of a resolved set, or use conditions. Return status later with get_task.",
                "\"title\":{\"type\":\"string\",\"maxLength\":100}," + StepsField, ",\"required\":[\"title\",\"steps\"]"),
            ChatTool("append_task", "Append future steps using submit_task's step shape.",
                TaskIdField + "," + EditStepsField, ",\"required\":[\"task_id\",\"steps\"]"),
            ChatTool("replace_task_tail", "Replace future steps using submit_task's step shape. The current native action is preserved.",
                TaskIdField + "," + EditStepsField, ",\"required\":[\"task_id\",\"steps\"]"),
            ChatTool("set_task_paused", "Pause or resume the identified task while retaining actual progress.",
                TaskIdField + ",\"paused\":{\"type\":\"boolean\"}", ",\"required\":[\"task_id\",\"paused\"]"),
            ChatTool("execute_operation", "Execute a single registered operation through the same task admission path.",
                "\"title\":{\"type\":\"string\",\"maxLength\":100}," + OperationField + ",\"arguments\":{\"type\":\"object\"}",
                ",\"required\":[\"title\",\"operation\"]"),
            ChatTool("observe_game", "Read a registered live observation using its typed parameters. This does not replace the current task.",
                OperationField + ",\"arguments\":{\"type\":\"object\"}", ",\"required\":[\"operation\"]"),
            ChatTool("get_task", "Read actual task status and recent real native operation outcomes.", TaskIdField),
            ChatTool("stop_task", "Stop an existing task and retire actions that have not reached a native consumer.", TaskIdField)
        };

        private static readonly IReadOnlyDictionary<string, object> NoOperationArguments = new Dictionary<string, object>();
        private readonly ConditionalWeakTable<ChatRun,Dictionary<string,string>> capabilityQueries =
            new ConditionalWeakTable<ChatRun,Dictionary<string,string>>();

        private CompanionTaskIdentity TaskIdentity(ChatBoundary boundary) => new CompanionTaskIdentity(boundary.Owner, boundary.Profile,
            boundary.WorldSession, boundary.Activation, boundary.DataEpoch);

        private bool DispatchChatTaskTool(ChatCommand command)
        {
            var args = command.Call.Arguments;
            var run = command.Run;
            var identity = TaskIdentity(run.Boundary);
            switch (command.Call.Name)
            {
                case "list_capabilities":
                    if (!ChatFields(args, "filter", "offset") || !ChatString(args, "filter", 48, out var filter) ||
                        !ReadChatInt(args, "offset", 0, 0, 1000, out int offset)) break;
                    var capabilities = GetTaskCapabilities().Where(tool => String.IsNullOrEmpty(filter) ||
                        tool.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        tool.Description.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || LocalTaskLabel(tool.Name).Contains(filter))
                        .Select(tool=>tool.Name+LocalTaskLabel(tool.Name)).Concat(GetTaskCapabilities().Any(tool=>tool.Name=="world_use") ?
                            WorldActions.Where(action=>MatchesWorldAction(action.Id,action.Label,filter))
                                .Select(action=>"world_use/"+action.Id+" · "+action.Label) : Enumerable.Empty<string>()).ToArray();
                    CompleteCapabilityQuery(command,"list:"+(filter??"")+":"+offset,
                        "已登记 " + capabilities.Length + " 个匹配入口。\n" + String.Join("\n",capabilities.Skip(offset).Take(10))+
                        (offset+10<capabilities.Length?"\n下一页 offset="+(offset+10):""),"查看已登记的游戏能力。");
                    return true;
                case "describe_capability":
                    if (!ChatFields(args, "operation") || !ChatString(args, "operation", 64, out var requested) || requested == null) break;
                    var actionInfo = WorldActions.FirstOrDefault(action=>requested=="world_use/"+action.Id ||
                        action.Id==requested || action.Label.Split('、').Contains(requested));
                    var capability = GetTaskCapabilities().FirstOrDefault(tool => tool.Name == (actionInfo.Id!=null?"world_use":requested) ||
                        actionInfo.Id==null && LocalTaskLabel(tool.Name)==" · "+requested);
                    if (capability == null)
                        RejectChatTool(command, "没有登记该能力。使用 list_capabilities 查找当前候选。");
                    else
                    {
                        string label=capability.Name+(actionInfo.Id==null?LocalTaskLabel(capability.Name):"/"+actionInfo.Id+" · "+actionInfo.Label);
                        string details=actionInfo.Id==null?capability.Description:actionInfo.Requirements+" 参数 action="+actionInfo.Id+"。";
                        if (actionInfo.Id=="mine" && actors.TryGetValue(run.Boundary.Profile,out var workActor))
                        {
                            int pick=0;
                            for(int slot=0;slot<CompanionBehavior.MainInventorySlots;slot++)
                            {
                                var item=workActor.Body.inventory[slot];
                                if (item.pick>pick && SupplyAdmitted(workActor,workActor.Body.inventory,0,slot,ownsSelected:slot==workActor.Body.selectedItem)) pick=item.pick;
                            }
                            details += pick>0?" 当前已可选镐力最高为 "+pick+"，还需根据所选矿物检查可挖条件。":" 当前队友没有可用的镐力工具。";
                        }
                        CompleteCapabilityQuery(command,"describe:"+capability.Name+":"+actionInfo.Id,
                            label+" · "+details+"\n参数："+capability.SchemaJson,"查看 "+label+" 的原生执行条件。");
                    }
                    return true;
                case "get_task":
                case "stop_task":
                    if (!ChatFields(args, "task_id") || !ReadTaskGuid(args, out var taskId)) break;
                    if (command.Call.Name == "get_task")
                        CompleteChatQuery(command, FormatTask(GetTask(run.Boundary.Owner, run.Boundary.Profile, taskId)));
                    else
                    {
                        if (!run.Authority.Stop) { RejectChatTool(command, ChatAuthority.Denied); return true; }
                        var stopped = StopTask(run.Boundary.Owner, run.Boundary.Profile, taskId);
                        if (stopped != null && stopped.State != "rejected") run.Conversation.ControlEpoch++;
                        CompleteChatQuery(command, FormatTask(stopped), error: stopped == null || stopped.State == "rejected");
                    }
                    return true;
                case "observe_game":
                    if (!ChatFields(args, "operation", "arguments") || !ChatString(args, "operation", 64, out var observedId) ||
                        String.IsNullOrWhiteSpace(observedId) || !ReadTaskArguments(args, out var observedArgs)) break;
                    var outcome = TryGetTaskObservation(identity, observedId, observedArgs, run.Anchor);
                    CompleteChatQuery(command, outcome.State + " · " + outcome.Detail +
                        (String.IsNullOrEmpty(outcome.Value) ? "" : " · 引用 " + outcome.Value));
                    return true;
                case "execute_operation":
                    if (!ChatFields(args, "title", "operation", "arguments") ||
                        !ChatString(args, "title", 100, out var oneTitle) || String.IsNullOrWhiteSpace(oneTitle) ||
                        !ChatString(args, "operation", 64, out var once) || String.IsNullOrWhiteSpace(once) ||
                        !ReadTaskArguments(args, out var oneArguments)) break;
                    SubmitChatTask(command, identity, oneTitle, new[] { new CompanionTaskStep(once, oneArguments) });
                    return true;
                case "submit_task":
                    if (!ChatFields(args, "title", "steps") || !ChatString(args, "title", 100, out var title) ||
                        String.IsNullOrWhiteSpace(title) || !ReadTaskSteps(args, out var steps)) break;
                    SubmitChatTask(command, identity, title, steps);
                    return true;
                case "append_task":
                case "replace_task_tail":
                    if (!ChatFields(args, "task_id", "steps") || !ReadTaskGuid(args, out var existing) || !existing.HasValue ||
                        !ReadTaskSteps(args, out var extra)) break;
                    string appendKey = command.Call.Name + ":" + existing.Value.ToString("N") + ":" + PlanFingerprint(extra);
                    if (run.SubmittedPlans.TryGetValue(appendKey, out _))
                        CompleteChatQuery(command, FormatTask(GetTask(run.Boundary.Owner, run.Boundary.Profile, existing)));
                    else
                    {
                        if (!AuthorizeChatPlan(run, extra, out var cost, out var authorizedAnchor)) { RejectChatTool(command, cost.Denial); return true; }
                        var updated = command.Call.Name == "append_task" ? AppendTask(identity, existing.Value, extra, authorizedAnchor)
                            : ReplaceTaskTail(identity, existing.Value, extra, authorizedAnchor);
                        if (updated != null && updated.State != "rejected")
                        {
                            run.SubmittedPlans.Add(appendKey, existing.Value);
                            run.Authority.Commit(cost);
                        }
                        CompleteChatQuery(command, FormatTask(updated), error: updated == null || updated.State == "rejected");
                    }
                    return true;
                case "set_task_paused":
                    if (!ChatFields(args, "task_id", "paused") || !ReadTaskGuid(args, out var pauseId) || !pauseId.HasValue ||
                        !ChatBool(args, "paused", out bool? paused) || !paused.HasValue) break;
                    if (!run.Authority.Pause(paused.Value)) { RejectChatTool(command, ChatAuthority.Denied); return true; }
                    var state = PauseTask(identity, pauseId.Value, paused.Value);
                    CompleteChatQuery(command, FormatTask(state), error: state == null || state.State == "rejected");
                    return true;
                default: return false;
            }
            RejectChatTool(command, "任务参数不完整或超出范围。");
            return true;
        }

        private static bool MatchesWorldAction(string id,string label,string filter) => String.IsNullOrEmpty(filter) ||
            id.IndexOf(filter,StringComparison.OrdinalIgnoreCase)>=0 || label.Contains(filter) ||
            ("world_use/"+id).IndexOf(filter,StringComparison.OrdinalIgnoreCase)>=0;

        private static string LocalTaskLabel(string name)
        {
            switch(name)
            {
                case "clear_area": return " · 半径清场";
                case "mine_area": return " · 区域采矿";
                case "observe_tiles": return " · 查看地形";
                case "observe_npcs": return " · 查看生物";
                case "observe_settings": return " · 查看队友设置";
                case "move_to": return " · 前往地点";
                case "stand_by": return " · 原地待命";
                case "follow_owner": return " · 跟随主人";
                default: return "";
            }
        }

        private void CompleteCapabilityQuery(ChatCommand command,string key,string text,string summary)
        {
            var seen=capabilityQueries.GetOrCreateValue(command.Run);
            if (seen.TryGetValue(key,out var old) && String.Equals(text,old,StringComparison.Ordinal))
            {
                command.Reply.TrySetResult(new LlmToolResult(command.Call.Id,"本轮已查到同一详情。"+ShortChatFact(text),
                    recoveryFact:ShortChatFact(text),repeated:true));
                return;
            }
            seen[key]=text;
            CompleteChatQuery(command,text,summary);
        }

        private void SubmitChatTask(ChatCommand command, CompanionTaskIdentity identity, string title,
            IReadOnlyList<CompanionTaskStep> steps)
        {
            var run = command.Run;
            string key = PlanFingerprint(steps);
            if (run.SubmittedPlans.TryGetValue(key, out Guid previous))
            {
                CompleteChatQuery(command, FormatTask(GetTask(run.Boundary.Owner, run.Boundary.Profile, previous)));
                return;
            }
            if (!AuthorizeChatPlan(run, steps, out var cost, out var authorizedAnchor)) { RejectChatTool(command, cost.Denial); return; }
            var result = SubmitTask(identity, title.Trim(), steps, authorizedAnchor, Guid.ParseExact(OperationId(command), "N"));
            if (result != null && result.TaskId != Guid.Empty)
            {
                run.SubmittedPlans.Add(key, result.TaskId);
                run.Authority.Commit(cost);
                run.Conversation.ControlEpoch++;
            }
            CompleteChatQuery(command, FormatTask(result), error: result == null || result.State == "rejected");
        }

        private bool AuthorizeChatPlan(ChatRun run,IReadOnlyList<CompanionTaskStep> steps,out ChatAuthority.Cost cost,out FishingAnchor anchor)
        {
            anchor=run.Anchor;
            if (!run.Authority.Plan(steps,out cost) || !AuthorizeChatWorldSite(run,steps,cost,ref anchor)) return false;
            foreach (var step in steps.Where(step=>step.Operation=="shimmer_item"))
            {
                if (step.Each.Count!=0) return false;
                if (run.Authority.ReferencesLastShimmer)
                {
                    string target=StepText(step.Arguments,"target");
                    if (anchor.WorkPoint.HasValue || !run.Authority.PriorShimmerPoint.HasValue || target!=run.Authority.PriorShimmer ||
                        !TryTaskMemoryDestination(target,out var point,out _,out _) || point!=run.Authority.PriorShimmerPoint.Value)
                    { cost.Denial="操作地点与发话时上次记录的微光池不一致。"; return false; }
                    anchor=new FishingAnchor(anchor,target,point);
                }
                if (!run.Authority.ReferencesHeld) continue;
                if (!actors.TryGetValue(run.Boundary.Profile,out var actor) || !TryTaskTalkItem(actor,run.Anchor,out _,out var held) ||
                    (!taskTalkItems[run.Anchor.TalkItemWitness].OwnerMatched && !run.Authority.NamesActorHand) ||
                    !step.Arguments.TryGetValue("item_type",out var type) || !(type is int id) || held.type!=id)
                {
                    cost.Denial="无法确认‘这件物品’是队友手里的哪一件。请写明物品名称，或让队友持有它再发送。";
                    return false;
                }
            }
            return true;
        }

        private static bool ReadTaskGuid(IReadOnlyDictionary<string, object> args, out Guid? taskId)
        {
            taskId = null;
            if (!ChatString(args, "task_id", 36, out var value)) return false;
            if (value == null) return true;
            if (!Guid.TryParseExact(value, "N", out Guid id) && !Guid.TryParseExact(value, "D", out id)) return false;
            taskId = id;
            return true;
        }

        private static bool ReadChatInt(IReadOnlyDictionary<string, object> args, string field, int defaultValue,
            int min, int max, out int value)
        {
            value = defaultValue;
            if (!args.TryGetValue(field, out var raw)) return true;
            if (!(raw is int parsed) || parsed < min || parsed > max) return false;
            value = parsed;
            return true;
        }

        private static bool ReadTaskArguments(IReadOnlyDictionary<string, object> fields,
            out IReadOnlyDictionary<string, object> arguments)
        {
            arguments = NoOperationArguments;
            if (!fields.TryGetValue("arguments", out var raw)) return true;
            arguments = raw as IReadOnlyDictionary<string, object>;
            return arguments != null;
        }

        private static bool ReadTaskSteps(IReadOnlyDictionary<string, object> args, out IReadOnlyList<CompanionTaskStep> steps)
        {
            steps = null;
            if (!args.TryGetValue("steps", out var raw) || !(raw is IReadOnlyList<object> values) || values.Count == 0 || values.Count > MaxTaskSteps) return false;
            var result = new CompanionTaskStep[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                if (!(values[i] is IReadOnlyDictionary<string, object> step) || !ChatFields(step, "operation", "arguments", "repeat", "condition", "each") ||
                    !ChatString(step, "operation", 64, out var operation) || String.IsNullOrWhiteSpace(operation) ||
                    !ReadTaskArguments(step, out var parameters) || !ReadChatInt(step, "repeat", 1, 1, 64, out int repeat)) return false;
                CompanionTaskCondition condition = null;
                if (step.TryGetValue("condition", out var test))
                {
                    if (!(test is IReadOnlyDictionary<string, object> predicate) || !ChatFields(predicate, "observation", "arguments", "expected", "wait", "timeout_ticks") ||
                        !ChatString(predicate, "observation", 64, out var observation) || String.IsNullOrWhiteSpace(observation) ||
                        !ReadTaskArguments(predicate, out var observed) || !ChatBool(predicate, "expected", out bool? expected) ||
                        !ChatBool(predicate, "wait", out bool? wait) || !ReadChatInt(predicate, "timeout_ticks", 0, 0, 216000, out int timeout))
                        return false;
                    condition = new CompanionTaskCondition(observation, observed, expected ?? true, wait ?? false, timeout);
                }
                IReadOnlyList<IReadOnlyDictionary<string, object>> each = null;
                if (step.TryGetValue("each", out var list))
                {
                    if (!(list is IReadOnlyList<object> members) || members.Count == 0 || members.Count > 64 ||
                        members.Any(member => !(member is IReadOnlyDictionary<string, object>))) return false;
                    each = members.Cast<IReadOnlyDictionary<string, object>>().ToArray();
                }
                result[i] = new CompanionTaskStep(operation, parameters, repeat, condition, each);
            }
            steps = result;
            return true;
        }

        private static string PlanFingerprint(IReadOnlyList<CompanionTaskStep> steps)
        {
            var key = new StringBuilder();
            foreach (var step in steps)
            {
                KeyPart(key, step.Operation);
                KeyPart(key, step.Arguments);
                KeyPart(key, step.Repeat);
                KeyPart(key, step.Each?.ToArray());
                if (step.Condition == null) { KeyPart(key, null); continue; }
                KeyPart(key, step.Condition.Observation);
                KeyPart(key, step.Condition.Arguments);
                KeyPart(key, step.Condition.Expected);
                KeyPart(key, step.Condition.Wait);
                KeyPart(key, step.Condition.TimeoutTicks);
            }
            return key.ToString();
        }

        private static void KeyPart(StringBuilder key, object value)
        {
            switch (value)
            {
                case IReadOnlyDictionary<string, object> map:
                    key.Append('{');
                    foreach (var field in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    { KeyPart(key, field.Key); KeyPart(key, field.Value); }
                    key.Append('}');
                    break;
                case object[] list:
                    key.Append('[');
                    foreach (var part in list) KeyPart(key, part);
                    key.Append(']');
                    break;
                case string text:
                    key.Append('s').Append(text.Length).Append(':').Append(text);
                    break;
                case null: key.Append('n'); break;
                default: key.Append('v').Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); break;
            }
        }

        private static string FormatTask(CompanionTaskSnapshot task)
        {
            if (task == null) return "没有符合查询范围的当前或最近任务。";
            return "任务 " + task.TaskId.ToString("N") + " · " + task.Title + " · " + task.State +
                " · 步骤 " + task.StepIndex + "/" + task.StepCount + " · " + task.Detail +
                String.Concat(task.Attempts.Skip(Math.Max(0, task.Attempts.Count - 6))
                    .Select(attempt => "\n" + attempt.Operation + " " + attempt.State + " · 操作 " + attempt.AttemptId.ToString("N") +
                        " · 完成 " + attempt.Completed + " · " + attempt.Detail +
                        (String.IsNullOrEmpty(attempt.Value) ? "" : " · 结果引用 " + attempt.Value)));
        }

        partial void PublishChatTaskFact(Guid ownerId, Guid profileId, string fact)
        {
            if (chats.TryGetValue((ownerId, profileId), out var chat))
                AddChatEntry(chat, "action", fact, recover: true);
        }
    }
}
