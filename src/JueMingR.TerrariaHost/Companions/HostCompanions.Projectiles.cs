using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private sealed class ProjectileLease
        {internal Projectile Body;internal int Key;internal Guid Profile;internal long Generation;internal bool Attached;}
        // Indexed by the native pool slot, with its actual key and activation.
        // Reusing a pool or player slot can never inherit this control lease.
        private readonly Dictionary<int,ProjectileLease> projectiles=new Dictionary<int,ProjectileLease>();
        internal void ProjectileBorn(Player player,Projectile projectile)
        {
            var a=Owned(player);if(a==null || !a.Ready || a.Closing || player.dead || projectile.owner!=player.whoAmI || projectile.key.Spawner!=player.whoAmI)return;
            bool hook=projectile.aiStyle==7;
            bool attached=player.HeldItem.channel || projectile.minion || projectile.sentry || Main.projPet[projectile.type] || projectile.ownerHitCheck || hook || projectile.aiStyle==3 || projectile.aiStyle==15 || projectile.aiStyle==19 || projectile.aiStyle==20;
            projectiles[projectile.whoAmI]=new ProjectileLease{Body=projectile,Key=(int)projectile.key,Profile=a.Profile.Id,Generation=a.Generation,Attached=attached};
            if(hook)a.Mobility.SetHook(projectile);
            if(player.HeldItem.channel && projectile.type==player.HeldItem.shoot)a.Combat.ControlledShot=projectile;
        }
        internal Actor ProjectileActor(Projectile p)
        {
            ProjectileLease lease;Actor a;
            return projectiles.TryGetValue(p.whoAmI,out lease) && ReferenceEquals(lease.Body,p) && lease.Key==(int)p.key && actors.TryGetValue(lease.Profile,out a) && a.Generation==lease.Generation && ReferenceEquals(Owned(a.Body),a)?a:null;
        }
        private void RetireProjectiles(Actor a)
        {
            RetireMobilityHand(a);
            // Native Kill may create another native projectile. Retirement is
            // a low-frequency boundary; snapshot leases before that callback.
            foreach(var lease in new List<ProjectileLease>(projectiles.Values))
            {
                var p=lease.Body;if(lease.Profile!=a.Profile.Id || lease.Generation!=a.Generation || !p.active || lease.Key!=(int)p.key || !lease.Attached)continue;
                // End owner-following/channel/minion controllers while their
                // original body still exists. Ordinary released arrows remain
                // in flight and never acquire a successor activation's scope.
                using(CompanionHooks.Enter(a,a.Aim))p.Kill();
            }
        }
    }
}
