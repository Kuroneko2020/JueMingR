using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        private HostJoustingLanceAttack.State joustingOwner;
        internal void CaptureJoustingOwner(Player value,Projectile[] sources)
        {if(joustingOwner==null)joustingOwner=new HostJoustingLanceAttack.State(Main.player[value.whoAmI],sources);}
        private bool JoustingOwnerCurrent {get{return joustingOwner==null || joustingOwner.Current;}}
    }
}
