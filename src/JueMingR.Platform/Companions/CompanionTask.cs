using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace JueMingR.Platform.Companions
{
    public readonly struct CompanionTaskIdentity
    {
        public CompanionTaskIdentity(Guid ownerId, Guid profileId, long worldSession, long activation, long dataEpoch)
        { OwnerId = ownerId; ProfileId = profileId; WorldSession = worldSession; Activation = activation; DataEpoch = dataEpoch; }
        public Guid OwnerId { get; }
        public Guid ProfileId { get; }
        public long WorldSession { get; }
        public long Activation { get; }
        public long DataEpoch { get; }
    }

    public sealed class CompanionTaskCondition
    {
        public CompanionTaskCondition(string observation, IReadOnlyDictionary<string, object> arguments, bool expected = true,
            bool wait = false, int timeoutTicks = 0)
        {
            Observation = observation; Arguments = CompanionTaskStep.Copy(arguments); Expected = expected;
            Wait = wait; TimeoutTicks = timeoutTicks;
        }
        public string Observation { get; }
        public IReadOnlyDictionary<string, object> Arguments { get; }
        public bool Expected { get; }
        public bool Wait { get; }
        public int TimeoutTicks { get; }
    }

    public sealed class CompanionTaskStep
    {
        public CompanionTaskStep(string operation, IReadOnlyDictionary<string, object> arguments, int repeat = 1,
            CompanionTaskCondition condition = null, IReadOnlyList<IReadOnlyDictionary<string, object>> each = null)
        {
            Operation = operation; Arguments = Copy(arguments); Repeat = repeat; Condition = condition;
            Each = each == null ? (IReadOnlyList<IReadOnlyDictionary<string, object>>)Array.Empty<IReadOnlyDictionary<string, object>>() :
                Array.AsReadOnly(each.Select(Copy).ToArray());
        }
        public string Operation { get; }
        public IReadOnlyDictionary<string, object> Arguments { get; }
        public int Repeat { get; }
        public CompanionTaskCondition Condition { get; }
        public IReadOnlyList<IReadOnlyDictionary<string, object>> Each { get; }

        internal static IReadOnlyDictionary<string, object> Copy(IReadOnlyDictionary<string, object> source) =>
            new ReadOnlyDictionary<string, object>(source == null ? new Dictionary<string, object>(StringComparer.Ordinal) :
                source.ToDictionary(field => field.Key, field => field.Value, StringComparer.Ordinal));
    }

    public sealed class CompanionTaskOutcome
    {
        public CompanionTaskOutcome(string state, string detail, int completed = 0, string value = null)
        { State = state; Detail = detail; Completed = completed; Value = value; }
        public string State { get; }
        public string Detail { get; }
        public int Completed { get; }
        public string Value { get; }
    }

    public sealed class CompanionTaskAttempt
    {
        public CompanionTaskAttempt(Guid attemptId, int stepIndex, int iteration, string operation, CompanionTaskOutcome result)
        {
            AttemptId = attemptId; StepIndex = stepIndex; Iteration = iteration; Operation = operation;
            State = result.State; Detail = result.Detail; Completed = result.Completed; Value = result.Value;
        }
        public Guid AttemptId { get; }
        public int StepIndex { get; }
        public int Iteration { get; }
        public string Operation { get; }
        public string State { get; }
        public string Detail { get; }
        public int Completed { get; }
        public string Value { get; }
    }

    public sealed class CompanionTaskSnapshot
    {
        public CompanionTaskSnapshot(Guid taskId, string state, string title, int stepIndex, int stepCount, string detail,
            IReadOnlyList<CompanionTaskAttempt> attempts)
        { TaskId = taskId; State = state; Title = title; StepIndex = stepIndex; StepCount = stepCount; Detail = detail; Attempts = attempts; }
        public Guid TaskId { get; }
        public string State { get; }
        public string Title { get; }
        public int StepIndex { get; }
        public int StepCount { get; }
        public string Detail { get; }
        public IReadOnlyList<CompanionTaskAttempt> Attempts { get; }
        public bool IsActive => State == "accepted" || State == "running" || State == "waiting" || State == "blocked" || State == "paused";
    }
}
