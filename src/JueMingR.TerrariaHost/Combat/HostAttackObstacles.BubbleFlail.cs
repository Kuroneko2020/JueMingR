using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        private HostBubbleFlailFamilyAttack.Inputs bubbleFlailInputs;
        internal HostBubbleFlailFamilyAttack.Inputs BubbleFlailInputs {get{return bubbleFlailInputs;}}
        internal void CaptureBubbleFlailInputs(Player player,Projectile[] sources)
        {if(bubbleFlailInputs==null)bubbleFlailInputs=new HostBubbleFlailFamilyAttack.Inputs(player,sources);}
        private bool BubbleFlailOwnerCurrent {get{return bubbleFlailInputs==null || bubbleFlailInputs.MembersCurrent;}}
        private bool BubbleFlailDependenciesCurrent {get{return bubbleFlailInputs==null || bubbleFlailInputs.Current;}}
    }
}
