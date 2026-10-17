namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        private HostPumpkinEffectAttack.Inputs pumpkinInputs;
        internal HostPumpkinEffectAttack.Inputs CapturePumpkinInputs()
        {return pumpkinInputs??(pumpkinInputs=new HostPumpkinEffectAttack.Inputs(timeline.Identity.Session));}
        private bool PumpkinInputsCurrent {get{return pumpkinInputs==null || pumpkinInputs.MembersCurrent;}}
        private bool PumpkinDependenciesCurrent {get{return pumpkinInputs==null || pumpkinInputs.Current;}}
    }
}
