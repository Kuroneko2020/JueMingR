using System;

namespace JueMingR.Features.Blueprint
{
    // One document owns both user intentions. Presentation and the native use
    // consumer read this value; neither keeps an independent enabled flag.
    public struct BlueprintPreferences : IEquatable<BlueprintPreferences>
    {
        public readonly bool GelToolbar, AutoBuild;
        public BlueprintPreferences(bool gelToolbar, bool autoBuild)
        { GelToolbar = gelToolbar; AutoBuild = autoBuild; }
        public bool Equals(BlueprintPreferences other)
        { return GelToolbar == other.GelToolbar && AutoBuild == other.AutoBuild; }
        public override bool Equals(object other)
        { return other is BlueprintPreferences && Equals((BlueprintPreferences)other); }
        public override int GetHashCode() { return (GelToolbar ? 1 : 0) | (AutoBuild ? 2 : 0); }
    }
}
