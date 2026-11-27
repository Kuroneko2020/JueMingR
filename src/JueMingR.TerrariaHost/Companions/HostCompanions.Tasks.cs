using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Llm;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private const int MaxTaskSteps = 32, MaxTaskAttempts = 128;
        private readonly Dictionary<Guid, TaskJob> taskLedger = new Dictionary<Guid, TaskJob>();
        private readonly Queue<Guid> taskOrder = new Queue<Guid>();
        private readonly Dictionary<(Guid Owner, Guid Profile), Guid> taskLatest =
            new Dictionary<(Guid Owner, Guid Profile), Guid>();
        private Dictionary<string, TaskOperation> taskOperations;
        private static readonly IReadOnlyDictionary<string, object> NoTaskArguments = new Dictionary<string, object>();

        private sealed class TaskField
        {
            internal TaskField(string name, string kind = "string", bool required = false, int min = 0, int max = 120,
                params string[] options)
            { Name = name; Kind = kind; Required = required; Min = min; Max = max; Options = options; }
            internal string Name { get; }
            internal string Kind { get; }
            internal bool Required { get; }
            internal int Min { get; }
            internal int Max { get; }
            internal string[] Options { get; }
            internal bool Accepts(object value)
            {
                if (Kind == "boolean") return value is bool;
                if (Kind == "integer") return value is int number && number >= Min && number <= Max;
                return value is string text && text.Length >= Min && text.Length <= Max &&
                    (Options.Length == 0 || Array.IndexOf(Options, text) >= 0);
            }
            internal string Schema => $"\"{Name}\":{{\"type\":\"{Kind}\"" +
                (Kind == "integer" ? $",\"minimum\":{Min},\"maximum\":{Max}" :
                 Kind == "string" ? $",\"minLength\":{Min},\"maxLength\":{Max}" : "") +
                (Options.Length == 0 ? "" : ",\"enum\":[" + String.Join(",", Options.Select(SchemaLiteral)) + "]") + "}";
        }

        private sealed class TaskOperation
        {
            internal bool Observation;
            internal LlmToolDefinition Tool;
            internal TaskField[] Fields;
            internal Func<TaskExecution, CompanionTaskOutcome> Run;
            internal Action<TaskExecution> Cancel;
        }

        internal sealed class TaskJob
        {
            internal Guid Id = Guid.NewGuid();
            internal Guid Command;
            internal CompanionTaskIdentity Identity;
            internal string Title, State = "accepted", Detail = "任务已受理。";
            internal int Cursor, Iteration;
            internal uint EvaluatingSince, NextTick;
            internal bool WaitingForCondition;
            internal bool Paused;
            internal TaskTravel PausedTravel;
            internal TaskCombat PausedCombat;
            internal TaskExecution Current;
            internal List<CompanionTaskStep> Steps;
            internal List<FishingAnchor> StepAnchors;
            internal Vector2 Departure;
            internal readonly List<CompanionTaskAttempt> Receipts = new List<CompanionTaskAttempt>();
            internal CompanionTaskSnapshot View;
            internal bool Active => State == "accepted" || State == "running" || State == "waiting" || State == "blocked" || State == "paused";
        }

        internal sealed class TaskExecution
        {
            private readonly Action<TaskExecution, CompanionTaskOutcome> settlement;
            internal TaskExecution(Action<TaskExecution, CompanionTaskOutcome> settlement) { this.settlement = settlement; }
            internal TaskJob Job;
            internal Actor Actor;
            internal string Operation;
            internal Guid AttemptId { get; } = Guid.NewGuid();
            internal int StepIndex, Iteration, ReceiptIndex = -1;
            internal bool Beginning = true;
            internal uint StartedAt;
            internal IReadOnlyDictionary<string, object> Arguments;
            internal FishingAnchor Anchor;
            internal object State;
            internal bool Issued { get; private set; }
            internal void MarkSubmitted() => Issued = true;
            // Native completion can outlive the plan. It updates this exact
            // attempt without recreating eligibility for any later step.
            internal void Settle(CompanionTaskOutcome result) => settlement?.Invoke(this, result);
            internal string Text(string key, string fallback = null) => Arguments.TryGetValue(key, out var value) ? (string)value : fallback;
            internal int Number(string key, int fallback = 0) => Arguments.TryGetValue(key, out var value) ? (int)value : fallback;
            internal bool Flag(string key, bool fallback = false) => Arguments.TryGetValue(key, out var value) ? (bool)value : fallback;
        }

        private static string SchemaLiteral(string value) => "\"" + value + "\"";

        private void RegisterTaskOperation(string id, string description, Func<TaskExecution, CompanionTaskOutcome> step,
            Action<TaskExecution> cancel = null, params TaskField[] fields) =>
            RegisterTaskDefinition(id, description, step, cancel, false, fields);

        private void RegisterTaskObservation(string id, string description, Func<TaskExecution, CompanionTaskOutcome> observe,
            params TaskField[] fields) => RegisterTaskDefinition(id, description, observe, null, true, fields);

        private void RegisterTaskDefinition(string id, string description, Func<TaskExecution, CompanionTaskOutcome> run,
            Action<TaskExecution> cancel, bool observation, TaskField[] fields)
        {
            string schema = "{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{" +
                String.Join(",", fields.Select(field => field.Schema)) + "},\"required\":[" +
                String.Join(",", fields.Where(field => field.Required).Select(field => SchemaLiteral(field.Name))) + "]}";
            taskOperations.Add(id, new TaskOperation
            {
                Tool = new LlmToolDefinition(id, description, schema), Fields = fields, Run = run,
                Cancel = cancel, Observation = observation
            });
        }

        partial void RegisterResourceTaskOperations();
        partial void RegisterCraftTaskOperations();
        partial void RegisterItemUseTaskOperations();
        partial void RegisterServiceTaskOperations();
        partial void RegisterWorldTaskOperations();
        partial void PublishChatTaskFact(Guid ownerId, Guid profileId, string fact);

        private void EnsureTaskRegistry()
        {
            if (taskOperations != null) return;
            taskOperations = new Dictionary<string, TaskOperation>(StringComparer.Ordinal);
            RegisterMovementTaskOperations();
            RegisterResourceTaskOperations();
            RegisterCraftTaskOperations();
            RegisterItemUseTaskOperations();
            RegisterServiceTaskOperations();
            RegisterWorldTaskOperations();
        }

        internal LlmToolDefinition[] GetTaskCapabilities()
        {
            EnsureTaskRegistry();
            return taskOperations.Values.Select(value => value.Tool).ToArray();
        }

        internal CompanionTaskOutcome TryGetTaskObservation(CompanionTaskIdentity identity, string observation,
            IReadOnlyDictionary<string, object> arguments, FishingAnchor anchor)
        {
            if (!TaskActor(identity, out var actor)) return TaskOutcome("rejected", "当前队友或世界已变化。");
            EnsureTaskRegistry();
            if (String.IsNullOrEmpty(observation) || !taskOperations.TryGetValue(observation, out var operation) || !operation.Observation)
                return TaskOutcome("rejected", "找不到此查询能力。");
            if (!TaskArgumentsValid(operation, arguments ?? NoTaskArguments, false, out var error))
                return TaskOutcome("rejected", error);
            try { return operation.Run(new TaskExecution(null) { Actor = actor, Arguments = arguments ?? NoTaskArguments, Anchor = anchor }); }
            catch (Exception) { return TaskOutcome("failed", "当前查询未完成。"); }
        }

        internal CompanionTaskSnapshot SubmitTask(CompanionTaskIdentity identity, string title,
            IReadOnlyList<CompanionTaskStep> steps, FishingAnchor sendAnchor, Guid commandId)
        {
            if (!TaskActor(identity, out var actor)) return TaskRejected(title, "队友未加入、世界已退出或身体已更换。");
            if (commandId == Guid.Empty) return TaskRejected(title, "缺少玩家指令身份。");
            foreach (var prior in taskLedger.Values)
                if (prior.Command == commandId && SameTaskIdentity(prior.Identity, identity)) return prior.View;
            if (!ValidateTaskSteps(steps, 0, out var error)) return TaskRejected(title, error);
            if (actor.TaskWork != null) EndTask(actor, actor.TaskWork, "cancelled", "任务已被新的行动替换。");
            ClearTaskControl(actor);
            var job = new TaskJob
            {
                Command = commandId, Identity = identity, Title = String.IsNullOrWhiteSpace(title) ? "队友任务" : title.Trim(),
                Steps = new List<CompanionTaskStep>(steps), StepAnchors = Enumerable.Repeat(sendAnchor, steps.Count).ToList(),
                Departure = CompanionNativeContact.Feet(actor.Body)
            };
            if (job.Title.Length > 100) job.Title = job.Title.Substring(0, 100);
            actor.TaskWork = job;
            taskLedger.Add(job.Id, job);
            taskOrder.Enqueue(job.Id);
            taskLatest[(identity.OwnerId, identity.ProfileId)] = job.Id;
            TrimTaskLedger();
            PublishTask(job, true);
            return job.View;
        }

        internal CompanionTaskSnapshot AppendTask(CompanionTaskIdentity identity, Guid taskId,
            IReadOnlyList<CompanionTaskStep> steps, FishingAnchor sendAnchor) =>
            EditTaskTail(identity, taskId, steps, sendAnchor, replace: false);

        internal CompanionTaskSnapshot ReplaceTaskTail(CompanionTaskIdentity identity, Guid taskId,
            IReadOnlyList<CompanionTaskStep> steps, FishingAnchor sendAnchor) =>
            EditTaskTail(identity, taskId, steps, sendAnchor, replace: true);

        private CompanionTaskSnapshot EditTaskTail(CompanionTaskIdentity identity, Guid taskId,
            IReadOnlyList<CompanionTaskStep> steps, FishingAnchor sendAnchor, bool replace)
        {
            if (!TaskActor(identity, out var actor) || actor.TaskWork?.Id != taskId)
                return TaskRejected("", "任务已结束或不属于这名队友。");
            var job = actor.TaskWork;
            int retained = replace ? job.Cursor + (job.Current != null || job.Iteration != 0 ? 1 : 0) : job.Steps.Count;
            if (!ValidateTaskSteps(steps, retained, out var error) ||
                job.Steps.Take(retained).Sum(TaskRepeats) + steps.Sum(TaskRepeats) > MaxTaskAttempts)
                return TaskRejected(job.Title, error ?? "任务尝试次数超出上限。");
            // An admitted native action keeps its inputs. Only new steps
            // receive the new message's here anchor; task_target is explicit.
            if (replace)
            {
                job.Steps.RemoveRange(retained, job.Steps.Count - retained);
                job.StepAnchors.RemoveRange(retained, job.StepAnchors.Count - retained);
                if (job.Cursor == retained) job.WaitingForCondition = false;
            }
            job.Steps.AddRange(steps);
            job.StepAnchors.AddRange(Enumerable.Repeat(sendAnchor, steps.Count));
            job.Detail = "已更新后续步骤。";
            PublishTask(job, true);
            return job.View;
        }

        internal CompanionTaskSnapshot PauseTask(CompanionTaskIdentity identity, Guid taskId, bool paused)
        {
            if (!TaskActor(identity, out var actor) || actor.TaskWork?.Id != taskId)
                return TaskRejected("", "任务已结束或不属于这名队友。");
            var job = actor.TaskWork;
            if (job.Paused == paused) return job.View;
            job.Paused = paused;
            if (paused)
            {
                job.PausedTravel = actor.TaskTravel;
                job.PausedCombat = actor.TaskCombat;
                SetTaskTravel(actor, new TaskTravel { Feet = CompanionNativeContact.Feet(actor.Body), Name = "暂停位置" });
                actor.TaskCombat = null;
            }
            else
            {
                SetTaskTravel(actor, job.PausedTravel);
                actor.TaskCombat = job.PausedCombat;
                job.PausedTravel = null;
                job.PausedCombat = null;
            }
            ChangeTask(job, paused ? "paused" : "running", paused ? "任务已暂停，已提交的动作继续结算。" : "已恢复任务。", true);
            return job.View;
        }

        internal CompanionTaskSnapshot GetTask(Guid ownerId, Guid profileId, Guid? taskId = null)
        {
            if (ChatProfile(ownerId, profileId) == null) return null;
            if (taskId.HasValue)
                return taskLedger.TryGetValue(taskId.Value, out var job) && job.Identity.OwnerId == ownerId &&
                    job.Identity.ProfileId == profileId ? job.View : null;
            if (actors.TryGetValue(profileId, out var actor))
            {
                if (actor.TaskWork != null) return actor.TaskWork.View;
                if (actor.TaskControlId != Guid.Empty && taskLedger.TryGetValue(actor.TaskControlId,out var control)) return control.View;
            }
            return taskLatest.TryGetValue((ownerId, profileId), out Guid recentId) &&
                taskLedger.TryGetValue(recentId,out var recent) ? recent.View : null;
        }

        internal bool CanStopTask(Guid ownerId,Guid profileId,Guid taskId) => taskId != Guid.Empty &&
            ChatProfile(ownerId,profileId) != null && actors.TryGetValue(profileId,out var actor) &&
            (actor.TaskWork?.Id == taskId || actor.TaskControlId == taskId);

        internal CompanionTaskSnapshot StopTask(Guid ownerId, Guid profileId, Guid? taskId = null)
        {
            if (Thread.CurrentThread.ManagedThreadId != itemThread || ChatProfile(ownerId, profileId) == null ||
                !actors.TryGetValue(profileId, out var actor))
                return TaskRejected("", "当前队友无法停止任务。");
            var job = actor.TaskWork;
            if (job == null && actor.TaskControlId != Guid.Empty) taskLedger.TryGetValue(actor.TaskControlId,out job);
            if (job == null || taskId.HasValue && taskId != job.Id)
                return TaskRejected("", "指定的任务已结束或不属于这名队友。");
            if (job.Active) RetireTaskForActor(actor, "已停止当前任务。");
            else
            {
                ClearTaskControl(actor);
                ChangeTask(job, "cancelled", "已停止持续活动。", true);
            }
            return job.View;
        }

        private bool RetireTaskForActor(Actor actor, string reason)
        {
            bool had = actor.TaskWork != null || actor.TaskTravel != null || actor.TaskCombat != null;
            if (actor.TaskWork != null) EndTask(actor, actor.TaskWork, "cancelled", reason);
            ClearTaskControl(actor);
            return had;
        }

        private void TrimTaskLedger()
        {
            int inspected = taskOrder.Count;
            while (taskOrder.Count > 64 && inspected-- > 0)
            {
                Guid id = taskOrder.Dequeue();
                var job = taskLedger[id];
                if (job.Active || actors.TryGetValue(job.Identity.ProfileId,out var actor) && actor.TaskControlId == id) taskOrder.Enqueue(id);
                else
                {
                    taskLedger.Remove(id);
                    var key = (job.Identity.OwnerId, job.Identity.ProfileId);
                    if (taskLatest.TryGetValue(key, out Guid latest) && latest == id) taskLatest.Remove(key);
                }
            }
        }

        private bool TaskActor(CompanionTaskIdentity identity, out Actor actor)
        {
            actor = null;
            return Thread.CurrentThread.ManagedThreadId == itemThread && identity.WorldSession == session &&
                ChatProfile(identity.OwnerId, identity.ProfileId)?.DataEpoch == identity.DataEpoch &&
                TryFishingActor(identity.OwnerId, identity.ProfileId, out actor) && actor.Generation == identity.Activation;
        }

        private static bool SameTaskIdentity(CompanionTaskIdentity left, CompanionTaskIdentity right) =>
            left.OwnerId == right.OwnerId && left.ProfileId == right.ProfileId && left.WorldSession == right.WorldSession &&
            left.Activation == right.Activation && left.DataEpoch == right.DataEpoch;

        private static CompanionTaskSnapshot TaskRejected(string title, string reason) =>
            new CompanionTaskSnapshot(Guid.Empty, "rejected", title, 0, 0, reason, Array.Empty<CompanionTaskAttempt>());
        private static CompanionTaskOutcome TaskOutcome(string state, string text, int completed = 0, string value = null) =>
            new CompanionTaskOutcome(state, text, completed, value);

        private static bool TaskArgumentsValid(TaskOperation op, IReadOnlyDictionary<string, object> args, bool allowReferences,
            out string reason)
        {
            foreach (string name in args.Keys)
                if (!op.Fields.Any(field => field.Name == name))
                { reason = "不支持此参数：" + name; return false; }
            foreach (var field in op.Fields)
            {
                if (args.TryGetValue(field.Name, out var value))
                {
                    if (allowReferences && IsStepReference(value as string) || field.Accepts(value)) continue;
                    reason = "参数类型或范围不符：" + field.Name;
                    return false;
                }
                if (field.Required) { reason = "缺少参数：" + field.Name; return false; }
            }
            reason = null;
            return true;
        }

        private static bool IsStepReference(string text) => text == "$previous" ||
            text != null && text.StartsWith("$step.", StringComparison.Ordinal) &&
            Int32.TryParse(text.Substring(6), NumberStyles.None, CultureInfo.InvariantCulture, out _);

        private static int TaskRepeats(CompanionTaskStep step) => step.Each.Count > 0 ? step.Each.Count : step.Repeat;

        private bool ValidateTaskSteps(IReadOnlyList<CompanionTaskStep> steps, int previous, out string error)
        {
            error = "任务步骤为空或超过容量。";
            if (steps == null || steps.Count == 0 || steps.Count + previous > MaxTaskSteps) return false;
            EnsureTaskRegistry();
            int total = 0;
            foreach (var node in steps)
            {
                error = "无效的任务步骤或重复数量。";
                if (node == null || String.IsNullOrEmpty(node.Operation) || node.Repeat < 1 || node.Repeat > 64 ||
                    node.Each.Count > 64 || node.Each.Count > 0 && node.Repeat != 1 || (total += TaskRepeats(node)) > MaxTaskAttempts)
                    return false;
                error = "队友没有登记此操作：" + node.Operation;
                if (!taskOperations.TryGetValue(node.Operation, out var op)) return false;
                if (node.Each.Count == 0)
                { if (!TaskArgumentsValid(op, node.Arguments, true, out error)) return false; }
                else
                    foreach (var binding in node.Each)
                        if (!TaskArgumentsValid(op, MergeTaskArguments(node.Arguments, binding), true, out error)) return false;
                var condition = node.Condition;
                if (condition == null) continue;
                error = "等待条件无效。";
                if (String.IsNullOrEmpty(condition.Observation) || !taskOperations.TryGetValue(condition.Observation, out var observation) ||
                    !observation.Observation || condition.Wait && (condition.TimeoutTicks < 1 || condition.TimeoutTicks > 216000))
                    return false;
                if (!TaskArgumentsValid(observation, condition.Arguments, true, out error)) return false;
            }
            error = null;
            return true;
        }

        private static IReadOnlyDictionary<string, object> MergeTaskArguments(IReadOnlyDictionary<string, object> shared,
            IReadOnlyDictionary<string, object> binding)
        {
            var merged = shared.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            foreach (var pair in binding) merged[pair.Key] = pair.Value;
            return merged;
        }

        private static IReadOnlyDictionary<string, object> ResolveTaskArguments(TaskJob job, TaskOperation operation,
            IReadOnlyDictionary<string, object> args)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var pair in args)
            {
                object value = pair.Value;
                if (IsStepReference(value as string))
                {
                    string text = (string)value;
                    bool adjacent = text == "$previous";
                    int index = adjacent ? job.Cursor - 1 : Int32.Parse(text.Substring(6), CultureInfo.InvariantCulture);
                    // Select the step and its last attempt before considering
                    // success for $previous. Explicit selection can still retrieve
                    // an earlier successful iteration of the named step.
                    var prior = index < job.Cursor ? job.Receipts.LastOrDefault(item => item.StepIndex == index &&
                        (adjacent || item.State == "succeeded" && item.Value != null)) : null;
                    value = prior?.State == "succeeded" ? prior.Value : null;
                    if (operation.Fields.Any(field => field.Name == pair.Key && field.Kind == "integer") &&
                        Int32.TryParse(value as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)) value = count;
                }
                result[pair.Key] = value;
            }
            return result;
        }

        private void PumpTask(Actor actor)
        {
            var job = actor.TaskWork;
            if (job == null || unchecked((int)(Main.GameUpdateCount - job.NextTick)) < 0) return;
            job.NextTick = Main.GameUpdateCount + 1;
            if (!TaskActor(job.Identity, out _))
            { EndTask(actor, job, "cancelled", "世界、队友身体或档案已变化。"); ClearTaskControl(actor); return; }
            if (job.Paused) return;
            var node = job.Steps[job.Cursor];
            CompanionTaskOutcome outcome;
            try
            {
                if (job.Current == null && !TaskConditionReady(actor, job, node.Condition)) return;
                var operation = taskOperations[node.Operation];
                if (job.Current == null)
                {
                    var args = ResolveTaskArguments(job, operation, node.Each.Count == 0 ? node.Arguments :
                        MergeTaskArguments(node.Arguments, node.Each[job.Iteration]));
                    if (!TaskArgumentsValid(operation, args, false, out var reason))
                    { EndTask(actor, job, "failed", reason); return; }
                    job.Current = new TaskExecution(SettleTaskStep)
                    {
                        Job = job, Actor = actor, Operation = node.Operation, StartedAt = Main.GameUpdateCount,
                        StepIndex = job.Cursor, Iteration = job.Iteration, Arguments = args, Anchor = job.StepAnchors[job.Cursor]
                    };
                }
                var executing = job.Current;
                outcome = operation.Run(executing);
                executing.Beginning = false;
                if (!ReferenceEquals(job.Current, executing)) return; // A synchronous native callback already settled it.
            }
            catch (Exception) { outcome = TaskOutcome(job.Current?.Issued == true ? "unconfirmed" : "failed", "动作结果未能完成核对。"); }
            if (job.Current != null) SettleTaskStep(job.Current, outcome);
            else EndTask(actor, job, "failed", outcome.Detail);
        }

        private bool TaskConditionReady(Actor actor, TaskJob job, CompanionTaskCondition condition)
        {
            if (condition == null) return true;
            var result = TryGetTaskObservation(job.Identity, condition.Observation,
                ResolveTaskArguments(job, taskOperations[condition.Observation], condition.Arguments), job.StepAnchors[job.Cursor]);
            if (result.State == "succeeded" && Boolean.TryParse(result.Value, out bool flag) && flag == condition.Expected)
            { job.WaitingForCondition = false; return true; }
            if (result.State == "succeeded" && !condition.Wait && Boolean.TryParse(result.Value, out _))
            {
                var node = job.Steps[job.Cursor];
                int count = TaskRepeats(node) - job.Iteration;
                job.Receipts.Add(new CompanionTaskAttempt(Guid.NewGuid(),job.Cursor,job.Iteration,node.Operation,
                    TaskOutcome("skipped",count == 1 ? "条件不满足，本步未执行。" : "条件不满足，已跳过本步余下 " + count + " 次。")));
                job.Iteration += count - 1;
                AdvanceTaskStep(actor, job);
                return false;
            }
            if (!condition.Wait || result.State == "rejected" || result.State == "failed")
            { EndTask(actor, job, "failed", result.Detail); return false; }
            if (!job.WaitingForCondition) { job.WaitingForCondition = true; job.EvaluatingSince = Main.GameUpdateCount; }
            if (Main.GameUpdateCount - job.EvaluatingSince >= condition.TimeoutTicks)
            { EndTask(actor, job, "failed", "等待条件超时：" + result.Detail); return false; }
            job.NextTick = Main.GameUpdateCount + 12;
            ChangeTask(job, "waiting", result.Detail, false);
            return false;
        }

        private void SettleTaskStep(TaskExecution execution, CompanionTaskOutcome outcome)
        {
            if (Thread.CurrentThread.ManagedThreadId != itemThread || outcome == null) return;
            var job = execution.Job;
            bool current = job.Active && ReferenceEquals(job.Current, execution);
            if (execution.ReceiptIndex < 0 && !current) return;
            var old = execution.ReceiptIndex < 0 ? null : job.Receipts[execution.ReceiptIndex];
            if (old != null && old.State == outcome.State && old.Detail == outcome.Detail &&
                old.Completed == outcome.Completed && old.Value == outcome.Value)
            {
                if (current && !job.Paused && (job.State != outcome.State || job.Detail != outcome.Detail))
                    ChangeTask(job,outcome.State,outcome.Detail,false);
                return;
            }
            var receipt = new CompanionTaskAttempt(execution.AttemptId, execution.StepIndex, execution.Iteration, execution.Operation, outcome);
            if (execution.ReceiptIndex < 0)
            { execution.ReceiptIndex = job.Receipts.Count; job.Receipts.Add(receipt); }
            else job.Receipts[execution.ReceiptIndex] = receipt;
            if (!current)
            {
                PublishTask(job, false);
                PublishChatTaskFact(job.Identity.OwnerId, job.Identity.ProfileId, job.Title + " · " + outcome.Detail);
                return;
            }
            switch (outcome.State)
            {
                case "running":
                case "waiting":
                case "blocked":
                    ChangeTask(job, job.Paused ? "paused" : outcome.State, outcome.Detail, false);
                    break;
                case "succeeded":
                    job.Current = null;
                    AdvanceTaskStep(execution.Actor, job);
                    break;
                default:
                    EndTask(execution.Actor, job, outcome.State == "rejected" ? "failed" : outcome.State, outcome.Detail);
                    break;
            }
        }

        private void AdvanceTaskStep(Actor actor, TaskJob job)
        {
            if (++job.Iteration >= TaskRepeats(job.Steps[job.Cursor]))
            { job.Cursor++; job.Iteration = 0; }
            if (job.Cursor == job.Steps.Count)
            {
                int skipped = job.Receipts.Count(receipt=>receipt.State=="skipped");
                bool ran = job.Receipts.Any(receipt=>receipt.State=="succeeded");
                if (!ran && skipped > 0) EndTask(actor,job,"skipped","条件均未满足，未执行任务动作。");
                else EndTask(actor,job,"succeeded",skipped == 0 ? "任务已完成。" : "已完成满足条件的步骤，部分步骤未执行。");
            }
            else ChangeTask(job, job.Paused ? "paused" : "running", "已结算前一步，准备下一步。", false);
        }

        private void EndTask(Actor actor, TaskJob job, string state, string detail)
        {
            var step = job.Current;
            job.Current = null;
            if (ReferenceEquals(actor.TaskWork, job)) actor.TaskWork = null;
            job.State = state;
            job.Detail = detail;
            if (step != null)
            {
                try { taskOperations[step.Operation].Cancel?.Invoke(step); }
                catch (Exception) { /* The owner's native completion remains authoritative. */ }
                if (step.ReceiptIndex >= 0 && !step.Issued && state == "cancelled")
                    job.Receipts[step.ReceiptIndex] = new CompanionTaskAttempt(step.AttemptId, step.StepIndex, step.Iteration,
                        step.Operation, TaskOutcome("cancelled", "未开始的动作已撤回。"));
            }
            if (state != "succeeded" && actor.TaskControlId == job.Id) ClearTaskControl(actor);
            PublishTask(job, true);
        }

        private void ChangeTask(TaskJob job, string state, string text, bool announce)
        {
            job.State = state; job.Detail = text;
            PublishTask(job, announce);
        }

        private void PublishTask(TaskJob job, bool announce)
        {
            job.View = new CompanionTaskSnapshot(job.Id, job.State, job.Title, job.Cursor, job.Steps.Count,
                job.Detail, Array.AsReadOnly(job.Receipts.ToArray()));
            if (announce) PublishChatTaskFact(job.Identity.OwnerId, job.Identity.ProfileId, job.Title + " · " + job.Detail);
        }
    }
}
