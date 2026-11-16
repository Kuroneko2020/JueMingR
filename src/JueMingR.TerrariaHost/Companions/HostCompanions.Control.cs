using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Combat;
using JueMingR.TerrariaHost.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal IEnumerable<Actor> ActiveActors {get{return actors.Values;}}
        internal bool HasActors {get{return bodies.Count!=0;}}
        private static void IdleAim(Actor a)
        {
            // A loaded character has not spawned yet. No-target/disabled aim
            // follows this body's current position, never the old load/world
            // or dead target. Do not call this for budget-delayed valid shots.
            a.Aim=a.Body.Center;a.HasAim=a.WasUsing=false;
            a.AimTimeline=null;a.AimTerrain=null;a.AimRevision=0;a.AimPlayer=null;
        }
        internal void Control(Actor a)
        {
            var p=a.Body;
            if(p.selectedItem<0 || p.selectedItem>=50)p.selectedItemState.Select(0);
            p.controlLeft=p.controlRight=p.controlJump=p.controlDown=p.controlUp=p.controlUseItem=p.controlUseTile=p.controlHook=p.controlThrow=false;
            if(p.dead){a.Status="等待复活";return;}
            var behavior=a.Profile.Behavior;
            var frame=a.Behavior.Decide(p,owner,behavior,ReferenceEquals(a,planningActor));
            p.controlLeft=frame.Move<0;p.controlRight=frame.Move>0;p.controlJump=frame.Jump;p.controlDown=frame.Drop;
            if(frame.Heal)using(CompanionHooks.Enter(a,a.Aim))p.QuickHeal();
            if(!behavior.AttackEnabled){a.Target=null;IdleAim(a);p.channel=false;a.Status=frame.Status;return;}
            bool valid=a.Target!=null && ReferenceEquals(Main.npc[a.Target.whoAmI],a.Target) && a.TargetGeneration==a.Target.generation && CombatSelection.Receives(a.Target,false) && ObservedNpcCombatReady(a.Target) && !PredictionDead(a,a.Target) && Vector2.DistanceSquared(p.Center,a.Target.Center)<1600*1600;
            if(!valid || ReferenceEquals(a,planningActor))SelectTarget(a);
            if(a.Target!=null)
            {
                bool prepared=a.HasAim && a.AimRevision==NpcObservationRevision(a.Target) && ReferenceEquals(a.AimWeapon,p.HeldItem) && a.AimOrigin==HostAttackModels.LaunchOrigin(p,p.HeldItem) && a.AimTarget==a.Target.Center+a.Target.netOffset && a.AimVelocity==a.Target.velocity;
                if(!prepared){a.HasAim=false;a.Aim=a.Target.Center+a.Target.netOffset;}
                var item=p.HeldItem;
                bool weapon=item!=null && !item.IsAir && item.damage>0 && item.useStyle>0 && item.pick==0 && item.axe==0 && item.hammer==0;
                bool reachable=item!=null && item.shoot>0?Collision.CanHitLine(p.Center,1,1,a.Target.Center,1,1):Vector2.DistanceSquared(p.Center,a.Target.Center)<Math.Max(80,item?.width??0)*Math.Max(80,item?.width??0);
                p.controlUseItem=weapon && reachable && !p.noItems && !p.CCed;
                if(p.controlUseItem && a.WasUsing && !item.channel && !item.autoReuse && !p.autoReuseAllWeapons && p.itemAnimation==0 && p.itemTime==0)p.controlUseItem=false;
                a.WasUsing=p.controlUseItem;
                if(!p.controlUseItem)p.channel=false;
                if(a.Aim.X!=p.Center.X && p.itemAnimation<=0)p.ChangeDir(a.Aim.X>p.Center.X?1:-1);
            }
            else {IdleAim(a);p.channel=false;}
            a.Status=frame.Status;
        }
        private void SelectTarget(Actor a)
        {
            var p=a.Body;var behavior=a.Profile.Behavior;var center=behavior.PreferOwner && owner!=null?owner.Center:p.Center;
            NPC selected=null;float best=Single.MaxValue;
            for(int i=0;i<Main.maxNPCs;i++)
            {
                var n=Main.npc[i];if(!CombatSelection.Receives(n,false) || !ObservedNpcCombatReady(n) || n.townNPC || PredictionDead(a,n))continue;
                float own=Vector2.DistanceSquared(n.Center,p.Center);if(own>1600*1600)continue;
                if(behavior.SelfDefense && own>400*400 && (owner==null || Vector2.DistanceSquared(n.Center,owner.Center)>400*400))continue;
                float score=Vector2.DistanceSquared(n.Center,center)+own*.1f;
                if(ReferenceEquals(n,a.Target) && a.TargetGeneration==n.generation)score*=.85f;
                if(score<best){best=score;selected=n;}
            }
            if(!ReferenceEquals(a.Target,selected) || a.TargetGeneration!=(selected?.generation??0)){a.HasAim=false;a.AimTimeline=null;a.AimTerrain=null;}
            a.Target=selected;a.TargetGeneration=selected?.generation??0;
        }
        internal void PrepareUse(Actor a)
        {
            if(a.Target==null || !NpcTilePhysicsKnown(a.Target))return;
            bool fresh=FreshNpcObservation(a.Target);
            if(fresh && aimStep!=Main.GameUpdateCount && aimSelectionStep!=Main.GameUpdateCount)
            {
                aimSelectionStep=Main.GameUpdateCount;aimSelected=null;
                // Least recently offered eligible role wins this coherent
                // window. Dead/disabled/faulted/no-target/stale roles cannot
                // monopolize another target's fresh packet phase.
                foreach(var candidate in actors.Values)
                {
                    var p=candidate.Body;var n=candidate.Target;
                    if(!candidate.Ready || candidate.Closing || candidate.Faulted || p.dead || !candidate.Profile.Behavior.AttackEnabled || p.noItems || p.CCed || n==null || candidate.TargetGeneration!=n.generation || !CombatSelection.Receives(n,false) || !ObservedNpcCombatReady(n) || n.townNPC || PredictionDead(candidate,n) || Vector2.DistanceSquared(p.Center,n.Center)>=1600*1600 || !FreshNpcObservation(n) || p.HeldItem.shoot<=0 || p.HeldItem.damage<=0)continue;
                    if(aimSelected==null || candidate.LastAimAttempt<aimSelected.LastAimAttempt)aimSelected=candidate;
                }
            }
            // The opportunity to capture a coherent sample is independent of
            // a semi-auto release edge. Only native ItemCheck may actually use
            // the item. Selection is O(team) once per world step, with no
            // per-candidate visibility rays and one shared enhancement budget.
            bool capture=fresh && ReferenceEquals(a,aimSelected) && aimStep!=Main.GameUpdateCount;
            if(capture){aimStep=Main.GameUpdateCount;a.LastAimAttempt=++aimAttempt;}
            if(!capture && a.AimTimeline==null)return;
            // One shared enhancement allowance, never one human-sized budget
            // per actor. Exhaustion retains legal basic input and movement.
            using(work.Enter(Main.GameUpdateCount))
            {
                try
                {
                    long revision=NpcObservationRevision(a.Target);
                    if(a.AimRevision!=revision){a.AimTimeline=null;a.AimTerrain=null;a.AimPlayer=null;a.AimRevision=revision;}
                    // Raw AI clocks belong to the last server 23, not the
                    // geometry-only interpolation between packets. Capture a
                    // model only from a fresh coherent sample. Its immutable
                    // timeline may still be consumed at its real age while
                    // identity, player premise, geometry and cells stay valid.
                    a.Aim=HostAttackAim.ActorPoint(a.Body,a.Target,session,prediction,ref a.AimTimeline,ref a.AimTerrain,capture,NpcSampleTick(a.Target),ref a.AimPlayer,ref a.AimPlayerTick,ref a.AimPlayerAnchor);a.HasAim=true;a.AimWeapon=a.Body.HeldItem;a.AimOrigin=HostAttackModels.LaunchOrigin(a.Body,a.Body.HeldItem);a.AimTarget=a.Target.Center+a.Target.netOffset;a.AimVelocity=a.Target.velocity;
                }
                catch(CombatPredictionBudgetExceededException){}
            }
        }
        internal Vector2 ProjectileAim(Actor a,Projectile projectile)
        {
            if(projectile.type!=633)return a.Aim;
            var view=new HostPrismSteering.View(a.Body.Center-new Vector2(960,540),1920,1080,1);
            return HostPrismSteering.PointInView(a.Body.RotatedRelativePoint(a.Body.MountedCenter),projectile.velocity,a.Aim,a.Body.gravDir,view);
        }
        internal void AfterBody(Actor a)
        {
            a.Behavior.ObserveCapabilities(a.Body);
            // Native skip sets the timer to one. Do not reset the zero reached
            // by the next native UpdateDead, or a legally skipped death never ends.
            if(a.Body.dead && a.Body.respawnTimer>1 && a.Body.AllowsRespawnTimerSkip())a.Body.SkipRespawnTime();
            if(a.Body.dead && a.Body.respawnTimer<=0)
            {
                Spawn(a,PlayerSpawnContext.ReviveFromDeath);
                a.DeathObserved=false;a.Behavior=new CompanionBehaviorHost();a.HasAim=a.WasUsing=false;
                if(a.Connection!=null)using(CompanionHooks.Enter(a,a.Aim))NetMessage.SendData(12,-1,-1,null,a.Body.whoAmI,(byte)PlayerSpawnContext.ReviveFromDeath);
            }
            Publish(a);
        }
        internal void ObserveDeath(Actor a)
        {
            if(!a.Body.dead || a.DeathObserved)return;a.DeathObserved=true;
            a.Target=null;a.Body.controlUseItem=false;a.Body.channel=false;a.Status="等待复活";
            RetireProjectiles(a);
            a.Profile=profiles.CaptureAndSave(a.Profile.Id,a.Body,a.Generation);
            profiles.Release(a.Profile,a.Generation);long next;if(!profiles.TryReserve(a.Profile,out next))throw new InvalidOperationException("队友复活身份不可用。");a.Generation=next;
        }
        internal bool AcceptItemPacket(Actor a,byte[] p)
        {
            if(p.Length<3)return false;int index=BitConverter.ToInt16(p,1);if(index<0 || index>=Main.maxItems)return false;
            if(p[0]==21 && p.Length>=11)return Observer(new Vector2(BitConverter.ToSingle(p,3),BitConverter.ToSingle(p,7)))==a;
            var item=Main.item[index];return item!=null && Observer(item.Center)==a;
        }
    }
}
