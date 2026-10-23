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
        private bool AdmitProjectionWindow(BlueprintProjectionPlacement item, BlueprintProjectionBounds core, BlueprintProjectionWindow effective)
        {
            var stage = item.Renderer.StageWindow(effective, item.Mask, core, item.Coverage);
            if (stage == null) return false;
            long replaced = 0;
            var replacedCores = new List<BlueprintProjectionBounds>();
            foreach (var pair in item.Windows) if (ContainsCore(core, pair.Key))
            {
                // Moving Copy shares immutable DTOs with its rollback source.
                // Replacing one reference does not release the other's bytes.
                var other = ReferenceEquals(item, candidateProjection) ? restoreProjection : ReferenceEquals(item, restoreProjection) ? candidateProjection : null;
                if (!ProjectionOwnsWindow(other, pair.Value)) replaced += pair.Value.EstimatedBytes;
                replacedCores.Add(pair.Key);
            }
            bool retained = false;
            foreach (var owner in projectionScene) foreach (var pair in owner.Windows)
                if ((!ReferenceEquals(owner, item) || !ContainsCore(core, pair.Key)) && ReferenceEquals(pair.Value, effective)) retained = true;
            if (!retained && candidateProjection != null) foreach (var pair in candidateProjection.Windows)
                if ((!ReferenceEquals(candidateProjection, item) || !ContainsCore(core, pair.Key)) && ReferenceEquals(pair.Value, effective)) retained = true;
            long bytes = (retained ? 0 : effective.EstimatedBytes) - replaced;
            long commands = (stage.CommandCount - item.Renderer.CommandCount) * 80L;
            bool priority = ReferenceEquals(item, candidateProjection) || ReferenceEquals(item, restoreProjection) || ProjectionPointDemandActive && ReferenceEquals(item, projectionPointTarget) && core.Equals(projectionPointCore);
            var victims = new List<Tuple<BlueprintProjectionPlacement, BlueprintProjectionBounds, long, long>>();
            while (ProjectionWindowBytes + bytes > ProjectionSceneWindowBudget || ProjectionCommandBytes + commands > ProjectionSceneCommandBudget)
            {
                BlueprintProjectionPlacement victim = null; BlueprintProjectionBounds victimCore = default(BlueprintProjectionBounds);
                if (priority) foreach (var other in projectionScene)
                {
                    if (ReferenceEquals(other, restoreProjection)) continue;
                    foreach (var key in other.Windows.Keys)
                    {
                        if (ReferenceEquals(other, item) && ContainsCore(core, key)) continue;
                        if (ProjectionPointDemandActive && ReferenceEquals(other, projectionPointTarget) && ContainsCore(key, projectionPointCore)) continue;
                        victim = other; victimCore = key; break;
                    }
                    if (victim != null) break;
                }
                if (victim == null) break;
                long oldCommands = victim.Renderer.CommandCount * 80L, oldBytes = victim.Windows[victimCore].EstimatedBytes;
                RemoveProjectionCore(victim, victimCore);
                if (ReferenceEquals(victim, item))
                {
                    stage = item.Renderer.StageWindow(effective, item.Mask, core, item.Coverage);
                    if (stage == null) return false;
                    commands = (stage.CommandCount - item.Renderer.CommandCount) * 80L;
                }
                victims.Add(Tuple.Create(victim, victimCore, oldBytes, oldCommands - victim.Renderer.CommandCount * 80L));
            }
            // Stamp all preemption victims after the final release, so one
            // release cannot immediately wake its sibling and evict the target.
            foreach (var victim in victims) DenyProjectionCore(victim.Item1, victim.Item2, victim.Item3, victim.Item4);
            if (ProjectionWindowBytes + bytes > ProjectionSceneWindowBudget || ProjectionCommandBytes + commands > ProjectionSceneCommandBudget)
            { DenyProjectionCore(item, core, bytes, commands); return false; }
            long beforeBytes = ProjectionWindowBytes, beforeCommands = ProjectionCommandBytes;
            foreach (var replacedCore in replacedCores) item.Windows.Remove(replacedCore);
            item.Renderer.Publish(stage); item.Windows[core] = effective; item.Window = effective; item.HasRequestedWindow = true; item.Denied.Remove(core);
            // The larger completed core replaces the extra point ownership;
            // it becomes an ordinary resident rather than a permanent priority.
            if (ReferenceEquals(item, projectionPointTarget) && !core.Equals(projectionPointCore) && ContainsCore(core, projectionPointCore)) projectionPointTarget = null;
            if (ProjectionWindowBytes < beforeBytes || ProjectionCommandBytes < beforeCommands) projectionCapacityRevision++;
            return true;
        }
    }
}
