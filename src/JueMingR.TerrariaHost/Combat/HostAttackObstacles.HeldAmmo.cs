using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        private HeldAmmoInputs heldAmmoInputs;
        internal void CaptureHeldAmmoInputs(Player value)
        {if(heldAmmoInputs==null)heldAmmoInputs=new HeldAmmoInputs(Main.player[value.whoAmI]);}
        private bool HeldAmmoInputsCurrent {get{return heldAmmoInputs==null || heldAmmoInputs.Current;}}
        private sealed class HeldAmmoInputs
        {
            private readonly Player player;private readonly int slot,phantasm;private readonly bool channel,noItems,cc,quiver,harpy,seraph,phoenix;
            internal HeldAmmoInputs(Player p){player=p;slot=p.whoAmI;phantasm=p.phantasmTime;channel=p.channel;noItems=p.noItems;cc=p.CCed;quiver=p.magicQuiver;harpy=p.accHarpyCharm;seraph=p.accSeraphNecklace;phoenix=p.hasPhoenixQuiver;}
            // Capture the OFF values too: a sealed no-charm scene must retire if
            // the next Player prefix would now charm it. This scalar gate does
            // not allocate a Harpy world for ordinary equipment-off shots.
            internal bool Current {get{return slot>=0 && slot<Main.player.Length && ReferenceEquals(Main.player[slot],player) && player.phantasmTime==phantasm && player.channel==channel && player.noItems==noItems && player.CCed==cc && player.magicQuiver==quiver && player.accHarpyCharm==harpy && player.accSeraphNecklace==seraph && player.hasPhoenixQuiver==phoenix;}}
        }
    }
}
