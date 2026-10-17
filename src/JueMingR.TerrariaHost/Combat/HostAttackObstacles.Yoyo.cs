using System.Collections.Generic;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        private Player yoyoOwner;private int yoyoCounter;private bool[] yoyoNoTile;
        internal void CaptureYoyoOwner(Player value)
        {
            if(yoyoOwner!=null)return;yoyoOwner=Main.player[value.whoAmI];yoyoCounter=value.yoyoHitCounter;yoyoNoTile=new bool[Main.maxNPCs];
            for(int i=0;i<yoyoNoTile.Length;i++)yoyoNoTile[i]=Main.npc[i].noTileCollide;
        }
        private bool YoyoOwnerCurrent {get{return yoyoOwner==null || ReferenceEquals(Main.player[yoyoOwner.whoAmI],yoyoOwner) && yoyoOwner.yoyoHitCounter==yoyoCounter;}}
        private bool YoyoDependenciesCurrent
        {get{if(!YoyoOwnerCurrent)return false;if(yoyoNoTile!=null)for(int i=0;i<yoyoNoTile.Length;i++)if(Main.npc[i].noTileCollide!=yoyoNoTile[i])return false;return true;}}
        // Native OnHit can initialize a child's static victim exclusion
        // before that child reaches its own first Update. Do not write live
        // perIDStaticNPCImmunity or silently replace this with local immunity.
        internal void BlockStaticUntil(Projectile child,int victim,int outer)
        {
            if(history.StaticNext==null)history.StaticNext=new Dictionary<int,int[]>();int[] values;
            if(!history.StaticNext.TryGetValue(child.immunityIdentity,out values)){values=new int[Main.maxNPCs];history.StaticNext.Add(child.immunityIdentity,values);}values[victim]=outer;
        }
    }
}
