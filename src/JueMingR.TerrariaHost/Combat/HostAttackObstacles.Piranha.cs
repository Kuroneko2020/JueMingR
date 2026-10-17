using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        private HostPiranhaAttack.OwnerLease piranhaOwner;
        internal void CapturePiranhaOwner(Player player,bool observed)
        {if(piranhaOwner==null)piranhaOwner=new HostPiranhaAttack.OwnerLease(Main.player[player.whoAmI],observed);}
        private bool PiranhaOwnerCurrent {get{return piranhaOwner==null || piranhaOwner.Current;}}
    }
}
