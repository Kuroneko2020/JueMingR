using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        internal HostProjectileTargets CapturePhantasmTargets(Projectile shot)
        {var lease=HostProjectileTargets.CaptureActiveBodies(shot,timeline.Identity.Session);targetLeases.Add(lease);return lease;}
    }
}
