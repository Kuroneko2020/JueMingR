using System;

namespace JueMingR.Features.Combat
{
    // These indices are domain commands, not keys or trigger gestures. Stored
    // preferences contain no live player, target, input or projectile identity.
    public sealed class CombatOptions
    {
        public int EnabledMask {get;}
        public int SwitchInterval {get;}
        public CombatOptions(int enabledMask=0,int switchInterval=12)
        {
            if(enabledMask<0 || enabledMask>255)throw new ArgumentOutOfRangeException(nameof(enabledMask));
            if(switchInterval<0 || switchInterval>30)throw new ArgumentOutOfRangeException(nameof(switchInterval));
            EnabledMask=enabledMask;SwitchInterval=switchInterval;
        }
        public bool Enabled(int feature)
        {Validate(feature);return (EnabledMask&(1<<feature))!=0;}
        public CombatOptions Toggle(int feature)
        {Validate(feature);return new CombatOptions(EnabledMask^(1<<feature),SwitchInterval);}
        public CombatOptions WithInterval(int interval){return new CombatOptions(EnabledMask,interval);}
        private static void Validate(int feature)
        {if(feature<0 || feature>7)throw new ArgumentOutOfRangeException(nameof(feature));}
    }
}
