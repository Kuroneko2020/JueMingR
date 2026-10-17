using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        private HostWhipStarAttack.State whipStarOwner;
        internal HostWhipStarAttack.State CaptureWhipStarOwner(Player value,Projectile[] sources,int tag)
        {return whipStarOwner??(whipStarOwner=new HostWhipStarAttack.State(Main.player[value.whoAmI],sources,tag));}
        private bool WhipStarOwnerCurrent {get{return whipStarOwner==null || whipStarOwner.MembersCurrent;}}
        private bool WhipStarDependenciesCurrent {get{return whipStarOwner==null || whipStarOwner.Current;}}
    }
}
