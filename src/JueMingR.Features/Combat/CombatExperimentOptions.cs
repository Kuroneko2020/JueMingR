using System;
namespace JueMingR.Features.Combat
{
    // Only committed preferences; local attack/session eligibility belongs to Host.
    public sealed class CombatExperimentOptions
    {
        public int EnabledMask { get; }
        public CombatExperimentOptions(int enabledMask=0)
        { if(enabledMask<0 || enabledMask>7)throw new ArgumentOutOfRangeException(nameof(enabledMask)); EnabledMask=enabledMask; }
        public bool Enabled(int field) { Validate(field); return (EnabledMask & (1<<field))!=0; }
        public CombatExperimentOptions Toggle(int field) { Validate(field); return new CombatExperimentOptions(EnabledMask ^ (1<<field)); }
        public CombatExperimentOptions WithEnabled(int field,bool enabled)
        { Validate(field); return new CombatExperimentOptions(enabled ? EnabledMask | (1<<field) : EnabledMask & ~(1<<field)); }
        private static void Validate(int field) { if(field<0 || field>2)throw new ArgumentOutOfRangeException(nameof(field)); }
    }
}
