using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // G11A owns operation/release/selection. This owner prepares only bounded
    // representative directions on the same published target future. Charging
    // packets are not reliable damage contacts and never feed the red marker.
    internal sealed class HostCadenceAim
    {
        private readonly HostCombat combat;private readonly HostCombatObservation observation;
        private Item weapon;private NpcTrajectory timeline;private AttackAmmoSnapshot ammo;
        private uint step;private long operation;private Vector2 origin,point;private CombatAimStage stage;
        internal HostCadenceAim(HostCombat combat,HostCombatObservation observation){this.combat=combat;this.observation=observation;}
        internal void Clear(){weapon=null;timeline=null;ammo=null;}
        internal void Prepare(Player player,Item item,AttackAmmoSnapshot captured,NpcTrajectory future,HostAttackPhase phase,AttackContact contact)
        {
            Clear();if(!combat.Use.Active || captured==null || !WeaponCatalog.Switchable(item))return;
            var clock=new HostAttackClock(future,phase);int tick=clock.FirstTick;var primary=combat.Use.AimPrimary;
            origin=player.RotatedRelativePoint(player.MountedCenter);
            if(item.type==5462)
            {stage=CombatAimStage.FlintCharge;tick+=(int)Math.Ceiling(Math.Max(0,30-(primary==null?0:primary.ai[1])));}
            else if(item.type==6153)
            {
                stage=CombatAimStage.GlacierCharge;
                // Native full charge is 60 AI increments, then the retained
                // direction travels at up to 15/subupdate. Slow-mana changes
                // and early-release scatter keep this a representative input.
                tick+=(int)Math.Ceiling(Math.Max(0,60-(primary==null?0:primary.ai[0]))/player.GetSlowMagicUseRate());
                var b=future[Math.Min(future.Count-1,tick)].ProjectileReceiveBounds;Projectile sample=ContentSamples.ProjectilesByType[1115];
                tick+=(int)Math.Ceiling(Vector2.Distance(origin,new Vector2(b.CenterX,b.CenterY))/(15*(sample.extraUpdates+1)));
            }
            else
            {
                stage=CombatAimStage.ItemRelease;
                if(contact!=null){point=new Vector2(contact.AimX,contact.AimY);Stamp(item,captured,future);return;}
                var b=future[Math.Min(future.Count-1,tick)].ProjectileReceiveBounds;Projectile sample=ContentSamples.ProjectilesByType[captured.Projectile];
                tick+=(int)Math.Ceiling(Vector2.Distance(origin,new Vector2(b.CenterX,b.CenterY))/Math.Max(1,captured.Speed*(sample.extraUpdates+1)));
            }
            if(tick<0 || tick>=future.Count)return; // A short real future is not extended by copying its tail.
            point=HostHeldAttack.DirectionPoint(player,future,tick);Stamp(item,captured,future);
        }
        private void Stamp(Item item,AttackAmmoSnapshot captured,NpcTrajectory future)
        {weapon=item;ammo=captured;timeline=future;operation=combat.Use.Operation;step=Main.GameUpdateCount;}
        internal CombatAimPoint Read(CombatAimRequest request)
        {
            if(!combat.Attack.Permission || !combat.Use.MatchesAimRequest(request))return null;
            if(request.Stage==CombatAimStage.FlailRelease)
            {Vector2 flail;return combat.Attack.Control.TryPoint(request.Projectile,out flail)?new CombatAimPoint(request,flail):null;}
            if(weapon==null || request.Stage!=stage || step!=Main.GameUpdateCount || operation!=request.Operation || !ReferenceEquals(weapon,request.Weapon) ||
                !ReferenceEquals(timeline,observation.Prediction.Cache.Read(1)) || !observation.Selection.HasTarget || !timeline.Identity.Equals(observation.Selection.Target) ||
                !CombatSelection.Valid(timeline.Identity,observation.Session) || !ammo.IdentityMatches(request.Player,weapon) ||
                Vector2.DistanceSquared(origin,request.Player.RotatedRelativePoint(request.Player.MountedCenter))>4)return null;
            return new CombatAimPoint(request,point);
        }
    }
}
