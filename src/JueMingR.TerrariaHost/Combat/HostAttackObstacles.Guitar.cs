using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        private HostGuitarCurveAttack.BirthState guitarBirth;
        internal HostGuitarCurveAttack.BirthState CaptureGuitarBirth(Player player)
        {return guitarBirth??(guitarBirth=new HostGuitarCurveAttack.BirthState(player,timeline.Identity.Session));}
        private bool GuitarDependenciesCurrent {get{return guitarBirth==null || guitarBirth.Current;}}
    }
}
