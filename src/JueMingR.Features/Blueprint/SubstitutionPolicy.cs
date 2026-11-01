using System;

namespace JueMingR.Features.Blueprint
{
    // These bit positions are persisted. New native facts never expand the
    // owner's twelve approved categories or turn a disabled policy on.
    public enum BlueprintSubstitutionCategory
    {
        Workbench, Table, Chair, Door, Bed, PlanterBox,
        Anvil, MythrilAnvil, Chest, Torch, Platform, Wall
    }

    public readonly struct BlueprintSubstitutionPolicy : IEquatable<BlueprintSubstitutionPolicy>
    {
        public const int AllCategories = 4095;
        public BlueprintSubstitutionPolicy(bool enabled, int categories = AllCategories, long revision = 0)
        {
            if (categories < 0 || categories > AllCategories) throw new ArgumentOutOfRangeException(nameof(categories));
            Enabled = enabled; Categories = categories; Revision = revision;
        }
        public bool Enabled { get; }
        public int Categories { get; }
        public long Revision { get; }
        public bool Allows(BlueprintSubstitutionCategory category)
        { return Enabled && (int)category >= 0 && (int)category < 12 && (Categories & 1 << (int)category) != 0; }
        public bool Equals(BlueprintSubstitutionPolicy other)
        { return Enabled == other.Enabled && Categories == other.Categories && Revision == other.Revision; }
        public override bool Equals(object other) { return other is BlueprintSubstitutionPolicy && Equals((BlueprintSubstitutionPolicy)other); }
        public override int GetHashCode() { return (Enabled ? 1 : 0) ^ Categories * 397 ^ Revision.GetHashCode(); }
    }
}
