using System;
using JueMingR.Features.Blueprint;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private static BlueprintProjectionBounds IntersectBuildBounds(BlueprintProjectionBounds a, BlueprintProjectionBounds b)
        {
            int left = Math.Max(a.Left, b.Left), top = Math.Max(a.Top, b.Top);
            return new BlueprintProjectionBounds(left, top, Math.Max(0, Math.Min(a.Right, b.Right) - left),
                Math.Max(0, Math.Min(a.Bottom, b.Bottom) - top));
        }
        private static bool EmptyBuildBounds(BlueprintProjectionBounds value) { return value.Width <= 0 || value.Height <= 0; }

        // Coverage is a derived negative query, never Match/completion. Small
        // current-near ordinal proofs let a strip accumulate checked portions
        // across legal visits, even when it takes several update allowances.
        // Position-sensitive failures carry their own exact recovery below.
        private static BlueprintProjectionBounds NextBuildFocusCoverage(BlueprintRangeMaterials value, BlueprintProjectionBounds near)
        {
            var covered = IntersectBuildBounds(value.PreferredCoveredBounds, near);
            value.PreferredCoveredBounds = covered;
            if (EmptyBuildBounds(covered)) return near;
            if (near.Left < covered.Left) return new BlueprintProjectionBounds(near.Left, covered.Top, covered.Left - near.Left, covered.Height);
            if (near.Right > covered.Right) return new BlueprintProjectionBounds(covered.Right, covered.Top, near.Right - covered.Right, covered.Height);
            if (near.Top < covered.Top) return new BlueprintProjectionBounds(covered.Left, near.Top, covered.Width, covered.Top - near.Top);
            return new BlueprintProjectionBounds(covered.Left, covered.Bottom, covered.Width, near.Bottom - covered.Bottom);
        }
        private static void CompleteBuildFocusCoverage(BlueprintRangeMaterials value)
        {
            var part = value.PreferredPassCovered;
            if (EmptyBuildBounds(part)) return;
            var covered = value.PreferredCoveredBounds;
            if (EmptyBuildBounds(covered)) { value.PreferredCoveredBounds = part; return; }
            var union = BuildBoundsUnion(covered, part);
            var overlap = IntersectBuildBounds(covered, part);
            if ((long)union.Width * union.Height == (long)covered.Width * covered.Height + (long)part.Width * part.Height - (long)overlap.Width * overlap.Height)
                value.PreferredCoveredBounds = union;
        }
        private void RefreshPreferredPositionNegatives(BlueprintRangeMaterials value, Terraria.Player p)
        {
            if(value.PreferredPositionNegatives==null)return;
            var world=BuildFocusWorldBounds(p);
            foreach(var pair in value.PreferredPositionNegatives)
            {
                var proof=pair.Value;
                if(!proof.Active || proof.Player==MaterialBuildEligibilityRevision && proof.Position==p.position && proof.Reach.Equals(world) &&
                    proof.Width==p.width && proof.Height==p.height && proof.Direction==p.direction && proof.PulleyDirection==p.pulleyDir && proof.Pulley==p.pulley)continue;
                if(proof.OutOfReach && (proof.X<world.Left || proof.X>=world.Right || proof.Y<world.Top || proof.Y>=world.Bottom))
                {CapturePreferredPositionNegative(proof,p,world);continue;}
                proof.Active=false;value.PreferredNegativeOrdinals?.Remove(pair.Key);
                value.PreferredRevisit=true;value.PreferredCoveredBounds=default(BlueprintProjectionBounds);
            }
        }
        private void CapturePreferredPositionNegative(BlueprintBuildPositionNegative proof,Terraria.Player p,BlueprintProjectionBounds world)
        {
            // Player.Update can move before the next Host observation. A
            // prepared foreign consumer must see that live position/reach,
            // even while the canonical signal revision still has its old value.
            proof.Player=MaterialBuildEligibilityRevision;proof.Position=p.position;proof.Reach=world;
            proof.Width=p.width;proof.Height=p.height;proof.Direction=p.direction;proof.PulleyDirection=p.pulleyDir;proof.Pulley=p.pulley;
        }
        private bool CandidateMayContinueBuild(BlueprintBuildTarget target)
        {
            var group = buildContinuation;
            var operation = target.Operation;
            bool ordinary = group.Operation == null || group.Operation.Kind == BlueprintBuildOperationKind.PlaceBase ||
                group.Operation.Kind == BlueprintBuildOperationKind.ConvertSubstrate;
            if (!ordinary) return OperationContinuesBuild(target, operation, group.Source.Type, operation?.SupplyType ?? 0);
            if (operation != null && operation.Kind != BlueprintBuildOperationKind.PlaceBase && operation.Kind != BlueprintBuildOperationKind.ConvertSubstrate) return false;
            var placement = operation?.NativePlacement ?? target.Placement;
            if (placement?.UseItemType == group.Source.Type) return true;
            if (operation != null || placement == null) return false;
            var candidates = MaterialCatalog.Substitution?.Candidates(placement, substitutionPolicy);
            if (candidates != null) foreach (var candidate in candidates) if (candidate.UseItemType == group.Source.Type) return true;
            return false;
        }
    }
}
