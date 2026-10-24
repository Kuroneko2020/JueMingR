using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        // Only actual retirement/compaction advances capacity. Poll/Draw and
        // successful completion are not new capacity or a new demand identity.
        private long projectionCapacityRevision;
        private BlueprintProjectionPlacement projectionPointTarget;
        private BlueprintProjectionBounds projectionPointCore;
        private bool ProjectionPointDemandActive { get { return ProjectionMode == BlueprintProjectionMode.SelectingMove || ProjectionMode == BlueprintProjectionMode.SelectingRemove; } }

        private void ClearProjectionPoint()
        {
            var previous = projectionPointTarget; projectionPointTarget = null;
            if (previous != null && previous.WindowPending && previous.RequestedCore.Equals(projectionPointCore))
            {
                Library?.ClearProjectionWindow(previous.Identity); previous.WindowGeneration++; previous.WindowPending = false;
                previous.HasRequestedWindow = previous.Windows.Count != 0;
            }
            if (previous != null && projectionPointCore.Width != previous.CoreSize) RemoveProjectionCore(previous, projectionPointCore);
        }
        private void SetProjectionPoint(BlueprintProjectionPlacement item, int x, int y)
        {
            var core = new BlueprintProjectionBounds(x / 16 * 16, y / 16 * 16, 16, 16);
            if (ReferenceEquals(projectionPointTarget, item) && projectionPointCore.Equals(core)) return;
            // A single explicit point owns priority. Hover never creates one;
            // arrival only loads content and requires another physical click.
            ClearProjectionPoint();
            projectionPointTarget = item; projectionPointCore = core;
            item.Denied.Remove(core);
            if (item.WindowPending) { Library.ClearProjectionWindow(item.Identity); item.WindowGeneration++; item.WindowPending = false; }
            UpdateProjectionPoint();
        }
        private void UpdateProjectionPoint()
        {
            var item = projectionPointTarget;
            if (!ProjectionPointDemandActive || item == null || !projectionScene.Contains(item) || !item.Opened) return;
            if (!item.Coverage.Contains(projectionPointCore.Left, projectionPointCore.Top)) return;
            foreach (var core in item.Windows.Keys)
                if (ContainsCore(core, projectionPointCore)) return;
            if (item.WindowPending || ProjectionDenied(item, projectionPointCore) || projectionWindowAdmissions >= 4) return;
            item.RequestedCore = projectionPointCore;
            RequestProjectionWindow(item, projectionPointCore.Expand(item.Renderer.RequiredHalo));
        }
        private static bool ContainsCore(BlueprintProjectionBounds outer, BlueprintProjectionBounds inner)
        { return outer.Left <= inner.Left && outer.Top <= inner.Top && outer.Right >= inner.Right && outer.Bottom >= inner.Bottom; }
        private static bool ProjectionOwnsWindow(BlueprintProjectionPlacement owner, BlueprintProjectionWindow window)
        { if (owner != null) foreach (var value in owner.Windows.Values) if (ReferenceEquals(value, window)) return true; return false; }
        private bool ProjectionDenied(BlueprintProjectionPlacement item, BlueprintProjectionBounds core)
        {
            BlueprintProjectionDenial denial;
            if (!item.Denied.TryGetValue(core, out denial)) return false;
            if (!ReferenceEquals(denial.Mask, item.Mask)) { item.Denied.Remove(core); return false; }
            if (denial.CapacityRevision == projectionCapacityRevision) return true;
            if (ProjectionWindowBytes + denial.WindowBytes > ProjectionSceneWindowBudget || ProjectionCommandBytes + denial.CommandBytes > ProjectionSceneCommandBudget)
            { denial.CapacityRevision = projectionCapacityRevision; return true; }
            item.Denied.Remove(core); return false;
        }
        private void DenyProjectionCore(BlueprintProjectionPlacement item, BlueprintProjectionBounds core, long bytes, long commands)
        {
            item.Denied[core] = new BlueprintProjectionDenial { Mask = item.Mask, CapacityRevision = projectionCapacityRevision, WindowBytes = Math.Max(0, bytes), CommandBytes = Math.Max(0, commands) };
            projectionFeedback = "部分可见蓝图内容暂未加载";
        }
        private void RemoveProjectionCore(BlueprintProjectionPlacement item, BlueprintProjectionBounds core)
        {
            BlueprintProjectionWindow removed;
            if (!item.Windows.TryGetValue(core, out removed)) return;
            long beforeBytes = ProjectionWindowBytes, beforeCommands = ProjectionCommandBytes;
            item.Windows.Remove(core); item.Renderer.RemoveCore(core);
            if (ReferenceEquals(item.Window, removed)) { item.Window = null; foreach (var value in item.Windows.Values) { item.Window = value; break; } }
            item.HasRequestedWindow = item.Windows.Count != 0 || item.WindowPending;
            if (ProjectionWindowBytes < beforeBytes || ProjectionCommandBytes < beforeCommands) projectionCapacityRevision++;
        }
        private static bool OwnsAfterReplacement(BlueprintProjectionPlacement owner, BlueprintProjectionPlacement incoming,
            BlueprintProjectionBounds core, BlueprintProjectionWindow effective, BlueprintProjectionWindow value)
        {
            if (owner == null) return false;
            if (ReferenceEquals(owner, incoming) && ReferenceEquals(effective, value)) return true;
            foreach (var pair in owner.Windows)
                if ((!ReferenceEquals(owner, incoming) || !ContainsCore(core, pair.Key)) && ReferenceEquals(pair.Value, value)) return true;
            return false;
        }
        private long ProjectionBytesAfterReplacement(BlueprintProjectionPlacement item, BlueprintProjectionBounds core, BlueprintProjectionWindow effective)
        {
            long bytes = 0;
            foreach (var owner in projectionScene)
            {
                foreach (var pair in owner.Windows) if (!ReferenceEquals(owner, item) || !ContainsCore(core, pair.Key)) bytes += pair.Value.EstimatedBytes;
                if (ReferenceEquals(owner, item)) bytes += effective.EstimatedBytes;
            }
            if (candidateProjection != null)
            {
                foreach (var pair in candidateProjection.Windows)
                    if ((!ReferenceEquals(candidateProjection, item) || !ContainsCore(core, pair.Key)) &&
                        !OwnsAfterReplacement(restoreProjection, item, core, effective, pair.Value)) bytes += pair.Value.EstimatedBytes;
                if (ReferenceEquals(candidateProjection, item) && !OwnsAfterReplacement(restoreProjection, item, core, effective, effective)) bytes += effective.EstimatedBytes;
            }
            return bytes;
        }
        // This exists only for an over-budget priority receipt. Scene windows
        // retain their normal charges; only candidate/rollback aliases share a
        // charge. Removing one reference cannot release the other's DTO.
        private sealed class ProjectionRawRetention
        {
            private sealed class References { internal int Scene, Candidate, Restore; }
            private readonly Dictionary<BlueprintProjectionWindow, References> windows = new Dictionary<BlueprintProjectionWindow, References>();
            internal long Bytes { get; private set; }
            internal void Add(BlueprintProjectionWindow window, int scene, int candidate, int restore)
            {
                References refs;
                if (!windows.TryGetValue(window, out refs)) { refs = new References(); windows.Add(window, refs); }
                Bytes -= (refs.Scene + (refs.Restore == 0 ? refs.Candidate : 0)) * window.EstimatedBytes;
                refs.Scene += scene; refs.Candidate += candidate; refs.Restore += restore;
                Bytes += (refs.Scene + (refs.Restore == 0 ? refs.Candidate : 0)) * window.EstimatedBytes;
            }
        }
        private bool AdmitProjectionWindow(BlueprintProjectionPlacement item, BlueprintProjectionBounds core, BlueprintProjectionWindow effective)
        {
            var stage = item.Renderer.StageWindow(effective, item.Mask, core, item.Coverage);
            if (stage == null) return false;
            long beforeRaw = ProjectionWindowBytes, beforeCommandBytes = ProjectionCommandBytes;
            long plannedRaw = ProjectionBytesAfterReplacement(item, core, effective);
            long plannedCommands = beforeCommandBytes + (stage.CommandCount - item.Renderer.CommandCount) * 80L;
            // Denial costs always describe the unchanged real baseline, even
            // when a hypothetical eviction plan ultimately cannot admit it.
            long bytes = plannedRaw - beforeRaw, commands = plannedCommands - beforeCommandBytes;
            var replacedCores = new List<BlueprintProjectionBounds>();
            foreach (var pair in item.Windows) if (ContainsCore(core, pair.Key)) replacedCores.Add(pair.Key);
            var victims = new List<Tuple<BlueprintProjectionPlacement, BlueprintProjectionBounds, long, long>>();
            if (plannedRaw > ProjectionSceneWindowBudget || plannedCommands > ProjectionSceneCommandBudget)
            {
                bool priority = ReferenceEquals(item, candidateProjection) || ReferenceEquals(item, restoreProjection) || ProjectionPointDemandActive && ReferenceEquals(item, projectionPointTarget) && core.Equals(projectionPointCore);
                if (!priority) { DenyProjectionCore(item, core, bytes, commands); return false; }
                var raw = new ProjectionRawRetention();
                foreach (var owner in projectionScene) foreach (var value in owner.Windows.Values) raw.Add(value, 1, 0, 0);
                if (candidateProjection != null) foreach (var value in candidateProjection.Windows.Values) raw.Add(value, 0, 1, 0);
                if (restoreProjection != null) foreach (var value in restoreProjection.Windows.Values) raw.Add(value, 0, 0, 1);
                int sceneCharge = projectionScene.Contains(item) ? 1 : 0;
                int candidateCharge = ReferenceEquals(item, candidateProjection) ? 1 : 0, restoreCharge = ReferenceEquals(item, restoreProjection) ? 1 : 0;
                foreach (var key in replacedCores) raw.Add(item.Windows[key], -sceneCharge, -candidateCharge, -restoreCharge);
                raw.Add(effective, sceneCharge, candidateCharge, restoreCharge);
                var commandPlans = new Dictionary<BlueprintProjectionPlacement, BlueprintProjectionRenderer.CommandRetention>();
                // Iteration preserves the existing victim order. No resident,
                // denial or capacity revision changes until both budgets fit.
                foreach (var other in projectionScene)
                {
                    if (ReferenceEquals(other, restoreProjection)) continue;
                    foreach (var pair in other.Windows)
                    {
                        if (ReferenceEquals(other, item) && ContainsCore(core, pair.Key)) continue;
                        if (ProjectionPointDemandActive && ReferenceEquals(other, projectionPointTarget) && ContainsCore(pair.Key, projectionPointCore)) continue;
                        BlueprintProjectionRenderer.CommandRetention retained;
                        if (!commandPlans.TryGetValue(other, out retained))
                        { retained = other.Renderer.PlanCommands(ReferenceEquals(other, item) ? stage : null); commandPlans.Add(other, retained); }
                        int oldCount = retained.CommandCount;
                        retained.RemoveCore(pair.Key);
                        plannedCommands -= (oldCount - retained.CommandCount) * 80L;
                        if (ReferenceEquals(other, item)) stage.CommandCount = retained.CommandCount;
                        raw.Add(pair.Value, -1, ReferenceEquals(other, candidateProjection) ? -1 : 0, 0);
                        victims.Add(Tuple.Create(other, pair.Key, pair.Value.EstimatedBytes, 0L));
                        if (raw.Bytes <= ProjectionSceneWindowBudget && plannedCommands <= ProjectionSceneCommandBudget) break;
                    }
                    if (raw.Bytes <= ProjectionSceneWindowBudget && plannedCommands <= ProjectionSceneCommandBudget) break;
                }
                if (raw.Bytes > ProjectionSceneWindowBudget || plannedCommands > ProjectionSceneCommandBudget)
                { DenyProjectionCore(item, core, bytes, commands); return false; }
            }
            // The successful plan is the publication: the incoming stage has
            // already lost same-owner victims, so Publish cannot resurrect them.
            for (int i = 0; i < victims.Count; i++)
            {
                var victim = victims[i]; long oldCommands = victim.Item1.Renderer.CommandCount * 80L;
                RemoveProjectionCore(victim.Item1, victim.Item2);
                victims[i] = Tuple.Create(victim.Item1, victim.Item2, victim.Item3, oldCommands - victim.Item1.Renderer.CommandCount * 80L);
            }
            long beforeBytes = ProjectionWindowBytes, beforeCommands = ProjectionCommandBytes;
            foreach (var replacedCore in replacedCores) item.Windows.Remove(replacedCore);
            item.Renderer.Publish(stage); item.Windows[core] = effective; item.Window = effective; item.HasRequestedWindow = true; item.Denied.Remove(core);
            // The larger completed core replaces the extra point ownership;
            // it becomes an ordinary resident rather than a permanent priority.
            if (ReferenceEquals(item, projectionPointTarget) && !core.Equals(projectionPointCore) && ContainsCore(core, projectionPointCore)) projectionPointTarget = null;
            if (ProjectionWindowBytes < beforeBytes || ProjectionCommandBytes < beforeCommands) projectionCapacityRevision++;
            // All victims observe the final real capacity revision, including
            // any compaction in the replacement, and cannot wake one another.
            foreach (var victim in victims) DenyProjectionCore(victim.Item1, victim.Item2, victim.Item3, victim.Item4);
            return true;
        }
    }
}
