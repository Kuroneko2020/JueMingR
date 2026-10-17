using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        private HostMeleeAttack.SpearFlightState solarOwner;
        internal void CaptureSolarOwner(Player player,Projectile[] sources)
        {if(solarOwner==null)solarOwner=new HostMeleeAttack.SpearFlightState(player,sources);}
        private bool SolarOwnerCurrent {get{return solarOwner==null || solarOwner.Current;}}
    }
}
