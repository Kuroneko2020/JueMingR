using System;

namespace JueMingR.Features.Blueprint
{
    // One document owns the user intentions. Presentation and native consumers
    // read this value; neither keeps an independent enabled flag or category set.
    public struct BlueprintPreferences : IEquatable<BlueprintPreferences>
    {
        public const int AllReplacementCategories = 4095;
        public readonly bool GelToolbar, AutoBuild, SameTypeReplacement;
        public readonly int ReplacementCategories;
        public BlueprintPreferences(bool gelToolbar, bool autoBuild)
            : this(gelToolbar, autoBuild, false, AllReplacementCategories) { }
        public BlueprintPreferences(bool gelToolbar, bool autoBuild, bool sameTypeReplacement, int replacementCategories)
        {
            if (replacementCategories < 0 || replacementCategories > AllReplacementCategories)
                throw new ArgumentOutOfRangeException(nameof(replacementCategories));
            GelToolbar = gelToolbar; AutoBuild = autoBuild;
            SameTypeReplacement = sameTypeReplacement; ReplacementCategories = replacementCategories;
        }
        public BlueprintPreferences WithGelToolbar(bool value)
        { return new BlueprintPreferences(value, AutoBuild, SameTypeReplacement, ReplacementCategories); }
        public BlueprintPreferences WithAutoBuild(bool value)
        { return new BlueprintPreferences(GelToolbar, value, SameTypeReplacement, ReplacementCategories); }
        public BlueprintPreferences WithSameTypeReplacement(bool value)
        { return new BlueprintPreferences(GelToolbar, AutoBuild, value, ReplacementCategories); }
        public BlueprintPreferences WithReplacementCategories(int value)
        { return new BlueprintPreferences(GelToolbar, AutoBuild, SameTypeReplacement, value); }
        public bool Equals(BlueprintPreferences other)
        { return GelToolbar == other.GelToolbar && AutoBuild == other.AutoBuild &&
            SameTypeReplacement == other.SameTypeReplacement && ReplacementCategories == other.ReplacementCategories; }
        public override bool Equals(object other)
        { return other is BlueprintPreferences && Equals((BlueprintPreferences)other); }
        public override int GetHashCode()
        { return (GelToolbar ? 1 : 0) | (AutoBuild ? 2 : 0) | (SameTypeReplacement ? 4 : 0) | ReplacementCategories << 3; }
    }
}
