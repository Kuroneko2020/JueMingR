using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private static readonly Func<Player, Item, bool, bool> nativeCanUse =
            (Func<Player, Item, bool, bool>)Delegate.CreateDelegate(typeof(Func<Player, Item, bool, bool>),
                typeof(Player).GetMethod("ItemCheck_CanUse", BindingFlags.Instance | BindingFlags.NonPublic));
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
            if((p.selectedItem<0 || p.selectedItem>=CompanionBehavior.MainInventorySlots) && FreeHand(p))p.selectedItemState.Select(0);
            p.controlLeft=p.controlRight=p.controlJump=p.controlDown=p.controlUp=p.controlUseItem=p.controlUseTile=p.controlHook=p.controlThrow=false;
            if(p.dead || !p.active){CancelCompanionItemOnce(a);a.Combat.ResetHand();a.Status="等待复活";return;}
            var behavior=a.Preferences;
            var combat=a.Combat;
            bool planning=ReferenceEquals(a,planningActor) || planningActor==null && actors.Count<=1;

            // Native recovery can coexist with movement and an active hand.
            // Read its result before selecting the next actual item action.
            bool manualSupply = RunCompanionItemOnce(a);
            if (!manualSupply) TryCompanionLife(a);
            if(behavior.AttackEnabled && planning && !p.noItems && !p.CCed)ChooseAttacks(a,behavior);
            if(!behavior.AttackEnabled)combat.Reset();
            var chosen=combat.Choice;
            Vector2 point=Vector2.Zero;
            int manaCost=0;
            bool valid=behavior.AttackEnabled && CurrentAttack(a,chosen,out manaCost,out point);
            bool clearOpening=valid, waitingForMana=false;
            int growth=CompanionGrowthSlot(a, includeAutomatic: !manualSupply);
            bool buff=!manualSupply && growth<0 && CompanionBuffPending(a);

            // A pending exclusive maintenance action owns the release edge.
            // ItemCheck and live projectiles finish their normal animation and
            // channel; no other policy may reassert use during that interval.
            if(growth>=0 || buff)
            {
                combat.ResetHand();
                SetAttackTarget(a,null);
                if(FreeHand(p) && p.releaseUseItem)
                {
                    if(growth>=0)
                    {
                        p.selectedItemState.Select(growth);
                        p.controlUseItem=true;
                        BeginCompanionGrowth(a,growth);
                    }
                    else TryCompanionBuff(a);
                }
            }
            else if(valid && !manualSupply)
            {
                if(manaCost>p.statMana)
                {
                    bool continued=p.channel && chosen.Slot==p.selectedItem && chosen.Weapon.channel;
                    bool canRestore=continued || FreeHand(p) && NativeAttackAllowed(a,chosen,point,manaCost);
                    if(canRestore)TryCompanionMana(a,chosen.Weapon);
                    else if(FreeHand(p))combat.Reject(chosen,Main.GameUpdateCount);
                    if(!canRestore || manaCost>p.statMana && !CanNativeMana(a))
                    {
                        chosen=combat.ReadyChoice;
                        valid=CurrentAttack(a,chosen,out int fallbackFee,out point) && fallbackFee<=p.statMana;
                        waitingForMana=canRestore && !valid;
                    }
                }
                if(valid)ApplyAttack(a,chosen,point);
                else combat.ResetHand();
            }
            else combat.ResetHand();

            if(!behavior.AttackEnabled || !valid)
            {
                SetAttackTarget(a,null);
                if(FreeHand(p))IdleAim(a);
            }
            var pursuit=combat.Pursuit;
            var movementTarget=TargetCurrent(a,pursuit.Target,pursuit.TargetGeneration)?pursuit.Target:null;
            var frame=a.Behavior.DecideMovement(p,owner,behavior,planning,movementTarget,pursuit.CloseCombat,clearOpening || valid,pursuit.Weapon);
            p.controlLeft=frame.Move<0;p.controlRight=frame.Move>0;p.controlJump=frame.Jump;p.controlDown=frame.Drop;
            a.Status=frame.Status;
            if(frame.Status=="停留" && behavior.AttackEnabled && combat.NearbyCount>0)
            {
                if(growth>=0 || buff)a.Status="准备使用补给";
                else if(p.controlUseItem && combat.ActiveSlot>=0)a.Status="正在攻击";
                else if(waitingForMana)a.Status="等待魔力恢复";
                else if(!valid)a.Status=clearOpening?"等待武器可用":movementTarget!=null?"等待可攻击位置":"等待可用武器或弹药";
            }
        }

        private static bool FreeHand(Player p)
        { return !p.UsingOrReusingItem && p.itemTime<=0; }

        private static bool NativeAttackAllowed(Actor a,CompanionAttackChoice choice,Vector2 point,int prospectiveMana=0)
        {
            using(var scope=CompanionHooks.Enter(a,point))
            {
#if DEBUG
                a.Combat.NativeAdmissions++;
#endif
                if(choice.Weapon.sentry && !scope.TileInteractionReady)return false;
                var body=a.Body;
                // A potion is admissible only if mana is the outstanding
                // condition. ManaV2 already ignores the fee in its pure query.
                // Classic mana uses the same query on a private body view.
                if(prospectiveMana>body.statMana && !Terraria.Testing.DebugOptions.ManaV2)
                {body=HostMeleeAttack.PlayerCopy(body);body.statMana=prospectiveMana;}
                return nativeCanUse(body,choice.Weapon,false);
            }
        }

        private static void SetAttackTarget(Actor a,NPC target)
        {
            if(!ReferenceEquals(a.Target,target) || a.TargetGeneration!=(target?.generation??0))
            {a.HasAim=false;a.AimTimeline=null;a.AimTerrain=null;a.AimPlayer=null;}
            a.Target=target;a.TargetGeneration=target?.generation??0;
        }

        private void ApplyAttack(Actor a,CompanionAttackChoice choice,Vector2 point)
        {
            var p=a.Body;var combat=a.Combat;
            if(p.selectedItem!=choice.Slot)
            {
                if(!FreeHand(p) || !p.releaseUseItem){combat.ResetHand();return;}
                p.selectedItemState.Select(choice.Slot);
                a.HasAim=false;
            }
            SetAttackTarget(a,choice.Target);
            a.Aim=point;
            combat.ActiveSlot=choice.Slot;combat.Hand=choice;
            if(combat.LastSlot!=choice.Slot || !ReferenceEquals(combat.LastWeapon,choice.Weapon))
            {combat.LastSlot=choice.Slot;combat.LastWeapon=choice.Weapon;combat.SelectedTick=Main.GameUpdateCount;}
            var item=choice.Weapon;
            // Semi-auto, summon and sentry starts require a real previous
            // release. Held/ticking hands keep their input until the native
            // owner or this frame's scheduler asks for an ordinary release.
            p.controlUseItem=p.releaseUseItem || p.channel || !DoesStaySummoned(item) &&
                (item.autoReuse || p.autoReuseAllWeapons || !FreeHand(p));
            // Native spinning flails only hit nearby receivers while held.
            // Let the real release throw at an enemy outside that ellipse,
            // then keep releasing until the original projectile returns.
            if(HostFlailAttack.Weapon(item) && ControlledShot(a,item) is Projectile flail &&
                (flail.ai[0]!=0 || flail.localAI[1]>=13 && DistanceToReceiveBox(p,choice.Target)>40))p.controlUseItem=false;
            a.WasUsing=p.controlUseItem;
            if(point.X!=p.Center.X && p.itemAnimation<=0)p.ChangeDir(point.X>p.Center.X?1:-1);
        }
        internal void PrepareUse(Actor a)
        {
            var p=a.Body;
            var hand=a.Combat.Hand;
            if(p.controlUseItem && hand.Weapon!=null)
            {
                Vector2 point;
                // Player.Update may move the body and can change what the
                // current weapon geometry reads before native ItemCheck.
                a.Combat.ShotTerrainTick = UInt32.MaxValue;
                if(p.selectedItem!=hand.Slot || !CurrentAttack(a,hand,out int fee,out point) || fee>p.statMana && !CanNativeMana(a))
                {p.controlUseItem=false;a.WasUsing=false;}
                else a.Aim=point;
            }
            if(p.controlUseItem && hand.Weapon!=null && p.releaseUseItem && FreeHand(p))
            {
                // Check the actual weapon-specific gate at this final aim.
                // Native ItemCheck still owns the start and payment.
                if(!NativeAttackAllowed(a,hand,a.Aim))
                {a.Combat.Reject(hand,Main.GameUpdateCount);p.controlUseItem=false;a.WasUsing=false;}
            }
            if(a.Target==null || !NpcTilePhysicsKnown(a.Target) || DoesStaySummoned(p.HeldItem))return;
            bool fresh=FreshNpcObservation(a.Target);
            if(fresh && aimStep!=Main.GameUpdateCount && aimSelectionStep!=Main.GameUpdateCount)
            {
                aimSelectionStep=Main.GameUpdateCount;aimSelected=null;
                // Least recently offered eligible role wins this coherent
                // window. Dead/disabled/faulted/no-target/stale roles cannot
                // monopolize another target's fresh packet phase.
                foreach(var candidate in actors.Values)
                {
                    var body=candidate.Body;var n=candidate.Target;
                    if(!candidate.Ready || candidate.Closing || candidate.Faulted || body.dead || !candidate.Preferences.AttackEnabled || body.noItems || body.CCed || !TargetCurrent(candidate,n,candidate.TargetGeneration) || !FreshNpcObservation(n) || body.HeldItem.shoot<=0 || !CompanionItemRules.WeaponEligible(body.HeldItem) || DoesStaySummoned(body.HeldItem))continue;
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
                    var controlled=ControlledShot(a,p.HeldItem);
                    var point=HostAttackAim.ActorPoint(a.Body,a.Target,session,prediction,ref a.AimTimeline,ref a.AimTerrain,capture,NpcSampleTick(a.Target),ref a.AimPlayer,ref a.AimPlayerTick,ref a.AimPlayerAnchor,controlled,out HostAttackSolution result);
                    // A controller's solve uses the exact live shot phase. It
                    // may steer this action but cannot license a future hand.
                    if(controlled==null && hand.Weapon!=null && hand.SameWeapon(p) && ReferenceEquals(hand.Target,a.Target))
                    {
                        RememberAttackProof(a,hand,result,a.AimTerrain);
                        if(result.Reachability==HostAttackReachability.Blocked){p.controlUseItem=false;a.WasUsing=false;}
                    }
                    if(result.Reachability==HostAttackReachability.Reachable)a.Aim=point;
                    a.HasAim=result.Reachability==HostAttackReachability.Reachable;
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
            if(a.Profile.PermanentlyDead || a.PermanentDeathPending)return;
            // A completed live update has recomputed effective accessories.
            // Inventory changes must not use the previous accessory result.
            if(!a.Body.dead)a.TileReachPending=false;
            a.Behavior.ObserveCapabilities(a.Body);
            var hand=a.Combat.Hand;
            if(hand.Weapon!=null && DoesStaySummoned(hand.Weapon) && a.Body.ItemAnimationJustStarted && Main.GameUpdateCount>=a.Combat.NextSummon)
                a.Combat.NextSummon=Main.GameUpdateCount+90;
            if(a.Body.dead && !a.Preferences.AutoRespawn){Publish(a);return;}
            // Native skip sets the timer to one. Do not reset the zero reached
            // by the next native UpdateDead, or a legally skipped death never ends.
            if(a.Body.dead && a.Body.respawnTimer>1 && a.Body.AllowsRespawnTimerSkip())a.Body.SkipRespawnTime();
            if(a.Body.dead && a.Body.respawnTimer<=0)
            {
                Spawn(a,PlayerSpawnContext.ReviveFromDeath);
                a.DeathObserved=false;
                if(a.Connection!=null)using(CompanionHooks.Enter(a,a.Aim))NetMessage.SendData(12,-1,-1,null,a.Body.whoAmI,(byte)PlayerSpawnContext.ReviveFromDeath);
            }
            Publish(a);
        }
        internal void ObserveDeath(Actor a)
        {
            if(a.Closing)return;
            CancelCompanionItemOnce(a);
            if(a.PermanentDeathPending)
            {
                // Only the actual native terminal exit sets this marker. The
                // surviveHardcoreDeath life-loss exception never reaches it.
                CancelItemGestureForActor(a);CloseActor(a,"已永久死亡");return;
            }
            if(!a.Body.dead || a.DeathObserved)return;a.DeathObserved=true;
            CancelItemGestureForActor(a);
            SetAttackTarget(a,null);a.Combat.Reset();a.Behavior=new CompanionBehaviorHost();IdleAim(a);a.Body.controlUseItem=false;a.Status="等待复活";
            RetireProjectiles(a);
            CaptureActorState(a);a.TeamSavePending=false;
            long previous=a.Generation;profiles.Release(a.Profile,previous);long next;if(!profiles.TryReserve(a.Profile,out next))throw new InvalidOperationException("队友复活身份不可用。");a.Generation=next;
            // A legal death renews this same body's activation. Only its own
            // latest native team intent follows that transition, never a new
            // connection/actor that happens to reuse the player slot.
            if(a.TeamPending && a.TeamGeneration==previous && a.TeamSession==session && ReferenceEquals(a.TeamConnection,a.Connection))a.TeamGeneration=next;
        }
        internal bool AcceptItemPacket(Actor a,byte[] p)
        {
            if(p.Length<3)return false;int index=BitConverter.ToInt16(p,1);if(index<0 || index>=Main.maxItems)return false;
            if(p[0]==21 && p.Length>=11)return Observer(new Vector2(BitConverter.ToSingle(p,3),BitConverter.ToSingle(p,7)))==a;
            var item=Main.item[index];return item!=null && Observer(item.Center)==a;
        }
    }
}
