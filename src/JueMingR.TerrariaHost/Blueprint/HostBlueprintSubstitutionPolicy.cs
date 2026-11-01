using System;
using JueMingR.Features.Blueprint;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private BlueprintSubstitutionPolicy substitutionPolicy = new BlueprintSubstitutionPolicy(false);
        private BlueprintPlacementCatalog substitutionPlacementCatalog;
        private long substitutionRevision;

        // Preferences own user intent; this one publication owns the effective
        // relation. An off-state category edit saves intent without revoking
        // exact proofs. A real policy change retires only derived world work:
        // raw templates, scene masks and completed/retired instances survive.
        private void RefreshSubstitutionPolicy()
        {
            var snapshot = preference.Snapshot;
            bool enabled = snapshot.IsLoaded && snapshot.Value.SameTypeReplacement;
            int categories = snapshot.Value.ReplacementCategories;
            var catalog = MaterialCatalog;
            bool changed = enabled != substitutionPolicy.Enabled || enabled &&
                (categories != substitutionPolicy.Categories || !ReferenceEquals(catalog, substitutionPlacementCatalog));
            substitutionPlacementCatalog = catalog;
            substitutionPolicy = new BlueprintSubstitutionPolicy(enabled, categories, changed ? ++substitutionRevision : substitutionRevision);
            if (!changed) return;

            // Cancel requests new work/return through the existing owner. It
            // never rewinds an entered consumer, unknown debit or packet receipt;
            // those retain their original token and settle independently.
            CancelAutomaticBuild();
            buildActivationRevision++;
            MaterialBuildEligibilityRevision++;
            foreach (var item in projectionScene) ReleaseProjectionMatch(item);
            projectionMatchTickSet = false;
            CancelMaterialPublication();
        }

        private bool SameSubstitutionBody(BlueprintWorldObject expected, BlueprintWorldObject actual)
        {
            return expected != null && actual != null && (expected.SamePlacement(actual) ||
                MaterialCatalog?.Substitution?.AllowsObject(expected, actual, substitutionPolicy) == true);
        }

        private bool SubstitutionSupplyFor(int originalItemType, int actualItemType)
        {
            if (originalItemType == actualItemType) return true;
            var candidates = MaterialCatalog?.Substitution?.MaterialCandidates(originalItemType, substitutionPolicy);
            if (candidates != null) foreach (int candidate in candidates) if (candidate == actualItemType) return true;
            return false;
        }

        private bool HasBuildSupply(BlueprintPlacementDescriptor placement)
        {
            if (placement == null) return false;
            if (buildSupply.ContainsKey(placement.UseItemType)) return true;
            if (!substitutionPolicy.Enabled) return false;
            var candidates = MaterialCatalog?.Substitution?.Candidates(placement, substitutionPolicy);
            if (candidates != null) foreach (var candidate in candidates)
                if (BasicPlacement(candidate) && buildSupply.ContainsKey(candidate.UseItemType)) return true;
            return false;
        }
    }
}
