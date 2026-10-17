namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        // A local-only ambient hit can assign owner immunity to zero. That
        // assignment is irrelevant only when EVERY captured receiver already
        // has zero owner immunity. Checking only the selected NPC would miss
        // a preceding receiver whose changed permission spends penetration.
        // This uses the captured receive policy, never a fresh world read.
        internal HostProjectileTargets CaptureSeedlerTargets(Terraria.Projectile shot)
        {var lease=HostProjectileTargets.CaptureChase(shot,timeline.Identity.Session,true);targetLeases.Add(lease);return lease;}
        internal HostProjectileTargets CaptureOwnerChaseTargets(Terraria.Projectile shot)
        {var lease=HostProjectileTargets.CaptureOwnerChase(shot,timeline.Identity.Session);targetLeases.Add(lease);return lease;}
        internal HostProjectileTargets CaptureMovingChaseTargets(Terraria.Projectile shot)
        {var lease=HostProjectileTargets.CaptureMovingChase(shot,timeline.Identity.Session);targetLeases.Add(lease);return lease;}
        internal HostProjectileTargets CaptureNailTargets(Terraria.Projectile shot)
        {var lease=HostProjectileTargets.CaptureNailBodies(shot,timeline.Identity.Session);targetLeases.Add(lease);return lease;}
        internal HostProjectileTargets CapturePiranhaTargets(Terraria.Projectile shot)
        {var lease=HostProjectileTargets.CapturePiranhaBodies(shot,timeline.Identity.Session);targetLeases.Add(lease);return lease;}
        internal bool HomingOwnerClear(int slot,int captured,int outer)
        {
            if(history.OwnerWritten!=null && history.OwnerWritten[slot])return outer>=history.OwnerNext[slot];
            if(captured<0)return false;
            return captured==0 || outer>=captured+(beforeNpc || nextWorld?0:1);
        }
        internal bool OwnerImmunityAlreadyClear
        {
            get
            {
                if(!selected.WithoutLocal().Allows(0))return false;
                foreach(var receiver in receivers)if(receiver.OwnerImmune!=0)return false;
                return true;
            }
        }
    }
}
