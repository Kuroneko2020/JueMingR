using System.Collections.Generic;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        // Explosive Bullet's Kill clears only entries equal to the OLD
        // cooldown's expiry. It neither clears the new five-tick assignment
        // nor all static immunity. Siblings share these candidate-only writes.
        private sealed class AmmoStaticResets
        {internal readonly uint[] Initial=new uint[Main.maxNPCs];internal readonly bool[] Cleared=new bool[Main.maxNPCs];internal int Identity;}
        private AmmoStaticResets ammoStaticResets;
        internal void CaptureAmmoImpactStatic(Projectile p)
        {
            if(!p.usesIDStaticNPCImmunity || p.type!=286 || ammoStaticResets!=null)return;
            ammoStaticResets=new AmmoStaticResets{Identity=p.immunityIdentity};for(int i=0;i<Main.maxNPCs;i++)ammoStaticResets.Initial[i]=Projectile.perIDStaticNPCImmunity[p.immunityIdentity,i];
        }
        internal void ResetAmmoImpactCandidate()
        {if(ammoStaticResets!=null)System.Array.Clear(ammoStaticResets.Cleared,0,ammoStaticResets.Cleared.Length);}
        private HostAttackReceive AmmoImpactCapturedReceive(HostAttackReceive receive,int slot)
        {return ammoStaticResets!=null && shot.usesIDStaticNPCImmunity && !shot.usesLocalNPCImmunity && shot.immunityIdentity==ammoStaticResets.Identity && ammoStaticResets.Cleared[slot]?receive.WithoutLocal():receive;}
        internal void ResetAmmoImpactStaticMatching(int outer,int oldCooldown)
        {
            if(ammoStaticResets==null)return;int[] values;
            if(history.StaticNext!=null && history.StaticNext.TryGetValue(ammoStaticResets.Identity,out values))for(int i=0;i<values.Length;i++)if(values[i]==outer+oldCooldown)values[i]=0;
            long expected=Main.GameUpdateCount+outer-(nextWorld?0:1)+oldCooldown;
            for(int i=0;i<Main.maxNPCs;i++)if(ammoStaticResets.Initial[i]==expected)ammoStaticResets.Cleared[i]=true;
        }
    }
}
