using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
                        tool.Description.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                    var names = capabilities.Skip(offset).Take(10).Select(tool => tool.Name);
                    CompleteChatQuery(command, "已登记 " + capabilities.Length + " 项能力。\n" + String.Join("\n", names) +
                        (offset + 10 < capabilities.Length ? "\n下一页 offset=" + (offset + 10) : ""), "查看已登记的游戏能力。");
                    return true;
                case "describe_capability":
                    if (!ChatFields(args, "operation") || !ChatString(args, "operation", 64, out var requested) || requested == null) break;
                    var capability = GetTaskCapabilities().FirstOrDefault(tool => tool.Name == requested);
                    if (capability == null)
                        RejectChatTool(command, "没有登记该能力。使用 list_capabilities 查找当前候选。");
                    else CompleteChatQuery(command, capability.Name + " · " + capability.Description + "\n参数：" + capability.SchemaJson,
                        "查看游戏能力 " + capability.Name + " 的原生执行约束。");
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
                        if (!run.Authority.Plan(extra, out var cost)) { RejectChatTool(command, cost.Denial); return true; }
                        var updated = command.Call.Name == "append_task" ? AppendTask(identity, existing.Value, extra, run.Anchor)
                            : ReplaceTaskTail(identity, existing.Value, extra, run.Anchor);
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
            if (!run.Authority.Plan(steps, out var cost)) { RejectChatTool(command, cost.Denial); return; }
            var result = SubmitTask(identity, title.Trim(), steps, run.Anchor, Guid.ParseExact(OperationId(command), "N"));
            if (result != null && result.TaskId != Guid.Empty)
            {
                run.SubmittedPlans.Add(key, result.TaskId);
                run.Authority.Commit(cost);
                run.Conversation.ControlEpoch++;
            }
            CompleteChatQuery(command, FormatTask(result), error: result == null || result.State == "rejected");
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
            if (!args.TryGetValue("steps", out var raw) || !(raw is object[] values) || values.Length == 0 || values.Length > MaxTaskSteps) return false;
            var result = new CompanionTaskStep[values.Length];
            for (int i = 0; i < values.Length; i++)
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
                    if (!(list is object[] members) || members.Length == 0 || members.Length > 64 ||
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
