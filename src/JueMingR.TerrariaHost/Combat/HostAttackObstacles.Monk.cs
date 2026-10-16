using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        private HostMonkStaffAttack.OwnerLease monkOwner;
        internal void CaptureMonkOwner(Player player,Projectile[] source,bool pose)
        {if(monkOwner==null)monkOwner=new HostMonkStaffAttack.OwnerLease(Main.player[player.whoAmI],source,pose);}
        private bool MonkOwnerCurrent {get{return monkOwner==null || monkOwner.Current;}}
    }
}
