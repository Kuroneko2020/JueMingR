using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        private HostMedusaScanAttack.State medusaOwner;
        internal HostMedusaScanAttack.State CaptureMedusaOwner(Player player,Projectile[] sources)
        {return medusaOwner??(medusaOwner=new HostMedusaScanAttack.State(player,sources));}
        private bool MedusaOwnerCurrent {get{return medusaOwner==null || medusaOwner.MembersCurrent;}}
        private bool MedusaDependenciesCurrent {get{return medusaOwner==null || medusaOwner.Current;}}
    }
}
