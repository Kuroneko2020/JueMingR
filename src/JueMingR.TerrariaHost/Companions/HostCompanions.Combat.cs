using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Combat;
using JueMingR.TerrariaHost.Recovery;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal struct CompanionAttackChoice
    {
        internal NPC Target;
        internal int TargetGeneration, Slot, Type, Prefix;
        internal Item Weapon;
        internal bool CloseCombat;
        internal float Score;

        internal bool SameWeapon(Player body)
        {
            return Weapon != null && ReferenceEquals(Weapon, body.inventory[Slot]) &&
                Weapon.type == Type && Weapon.prefix == Prefix && !Weapon.IsAir;
        }

        internal bool SamePair(CompanionAttackChoice other)
        {
            return ReferenceEquals(Target,other.Target) && TargetGeneration==other.TargetGeneration && Slot==other.Slot &&
                ReferenceEquals(Weapon,other.Weapon) && Type==other.Type && Prefix==other.Prefix;
        }
    }

    internal struct CompanionAttackProof
    {
        internal CompanionAttackChoice Choice;
        internal AttackAmmoSnapshot Ammo;
        internal PredictionTerrain.CellLease Terrain;
        internal HostAttackReachability Reachability;
        internal Vector2 Point, Origin, Position, Velocity;
        internal uint Tick;
        internal long Revision;
    }

    internal struct CompanionAttackRejection
    {
        internal Item Weapon;
        internal int Type, Prefix;
        internal uint Until;
    }

    internal struct CompanionShotCheck
    {
        internal CompanionAttackChoice Choice;
        internal CompanionShotGeometry Geometry;
        internal Vector2 Origin, TargetPosition, Point;
        internal Rectangle TargetBounds;
        internal HostAttackReachability Opening;
        internal float Gravity, Rotation;
        internal int Facing;
    }

    internal sealed class CompanionCombatState
    {
        internal const int NearbyCapacity = 8;
        internal readonly NPC[] Nearby = new NPC[NearbyCapacity];
        internal readonly float[] Distances = new float[NearbyCapacity];
        internal readonly Vector2[] DirectOrigins = new Vector2[NearbyCapacity];
        internal readonly bool[] DirectKnown = new bool[NearbyCapacity], DirectClear = new bool[NearbyCapacity];
        internal readonly int[] Preferences = new int[CompanionBehavior.MaxMainWeapons];
        internal readonly PredictionTerrain DirectTerrain = new PredictionTerrain();
        internal readonly PredictionTerrain ShotTerrain = new PredictionTerrain();
        internal uint ShotTerrainTick = UInt32.MaxValue;
        internal readonly CompanionShotCheck[] ShotChecks = new CompanionShotCheck[12];
        internal int ShotCheckCount;
        internal int ActiveSlot = -1, InventoryCursor, ProbeCursor;
        internal int LastSlot = -1, TurretCount;
        internal Item LastWeapon;
        internal Projectile ControlledShot;
        internal uint SelectedTick, TurretCountTick = UInt32.MaxValue;
        internal int NearbyCount, NearbyOffset;
        internal CompanionAttackChoice Choice, ReadyChoice, Hand, Pursuit;
        internal CompanionAttackChoice Probe;
        internal uint NextSummon;
        internal readonly CompanionAttackRejection[] Rejections=new CompanionAttackRejection[CompanionBehavior.MainInventorySlots];
        internal readonly CompanionAttackProof[] Proofs = new CompanionAttackProof[4];
        internal int ProofCursor;
#if DEBUG
        internal int CandidateSweeps, PairProbes, NativeAdmissions, BasicShotReplays;
#endif

        internal void ResetHand()
        {
            ActiveSlot = -1;
            Hand = default(CompanionAttackChoice);
        }

        internal void Reset()
        {
            ResetHand();
            Choice = ReadyChoice = Pursuit = Probe = default(CompanionAttackChoice);
            Array.Clear(Proofs,0,Proofs.Length);ProofCursor=0;
            Array.Clear(Rejections,0,Rejections.Length);NextSummon=0;
            ControlledShot=null;
            LastSlot=-1;LastWeapon=null;TurretCountTick=UInt32.MaxValue;
            NearbyCount = InventoryCursor = ProbeCursor = NearbyOffset = 0;
            Array.Clear(Nearby, 0, Nearby.Length);
        }

        internal void Reject(CompanionAttackChoice choice,uint tick)
        {
            // Independent slots retain their own refusal briefly. Otherwise
            // two rejected weapons could alternate and starve a legal third.
            Rejections[choice.Slot]=new CompanionAttackRejection { Weapon=choice.Weapon,Type=choice.Type,Prefix=choice.Prefix,Until=tick+20 };
        }
    }

    internal sealed partial class HostCompanions
    {
        private const int CompanionCombatRadius = 1600;
        private const int FallbackWeaponsPerSweep = 5;

        private bool TargetCurrent(Actor a, NPC target, int generation)
        {
            return target != null && target.whoAmI >= 0 && target.whoAmI < Main.maxNPCs &&
                ReferenceEquals(Main.npc[target.whoAmI], target) && target.generation == generation &&
                CombatSelection.Receives(target, false) && !target.townNPC && !NPCID.Sets.IsTownPet[target.type] &&
                !NPCID.Sets.CountsAsCritter[target.type] && ObservedNpcCombatReady(target) && !PredictionDead(a, target) &&
                Vector2.DistanceSquared(a.Body.Center, target.Center + target.netOffset) <= CompanionCombatRadius * CompanionCombatRadius;
        }

        private static bool DoesStaySummoned(Item item)
        { return item.sentry || item.summon && item.shoot > 0 && !ProjectileID.Sets.IsAWhip[item.shoot]; }

        private static bool RoomToSummon(Actor a,Item item)
        {
            if (!DoesStaySummoned(item)) return true;
            var body=a.Body;var state=a.Combat;
            if (Main.GameUpdateCount < state.NextSummon || !ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out var sample)) return false;
            if (!item.sentry)
            {
                // Native FreeUpPetsAndMinions uses the staff's slot cost.
                // A companion maintains its minions without ordering a cast
                // that would force the native replacement of another one.
                float slots=ItemID.Sets.StaffMinionSlotsRequired[item.type];
                return sample.minion && slots>0 && body.slotsMinions+slots<=body.maxMinions;
            }
            if (body.maxTurrets <= 0 || !sample.sentry) return false;
            // Native capacity is per owner, not per projectile type. Only a
            // considered sentry reads the table, once for this body's decision.
            if(state.TurretCountTick!=Main.GameUpdateCount)
            {
                state.TurretCountTick=Main.GameUpdateCount;state.TurretCount=0;
                foreach(var shot in Main.projectile)
                    if(shot!=null && shot.active && shot.owner==body.whoAmI && shot.sentry)state.TurretCount++;
            }
            return state.TurretCount<body.maxTurrets;
        }

        private bool LegalResources(Actor a,int slot,Item item,out int manaCost,bool afterShapeExit=false)
        {
            manaCost = 0;
            if (!CompanionItemRules.WeaponEligible(item) || !afterShapeExit && a.Body.noItems || a.Body.CCed ||
                a.Body.silence && item.mana > 0 || !AttackSourceAvailable(a,slot)) return false;
            var rejected=a.Combat.Rejections[slot];
            if(ReferenceEquals(rejected.Weapon,item) && rejected.Type==item.type && rejected.Prefix==item.prefix && Main.GameUpdateCount<rejected.Until)return false;
            if(item.useAmmo>0)
            {
                var ammo=AttackAmmoSnapshot.Source(a.Body,item);
                if(ammo==null || !AttackSourceAvailable(a,Array.IndexOf(a.Body.inventory,ammo)))return false;
            }
            if (!RoomToSummon(a,item)) return false;
            manaCost = PotionRecovery.ManaCost(a.Body, item);
            return manaCost <= a.Body.statManaMax2;
        }

        private bool AttackSourceAvailable(Actor a,int slot)
        {
            var items=a.Body.inventory;
            return slot>=0 && slot<58 && !a.Body.inventoryChestStack[slot] &&
                itemDraft?.DependsOn(items,slot)!=true && !SupplySlotBlocked(a.Body,items,slot);
        }

        private bool CanNativeMana(Actor a)
        {
            Player body=a.Body;
            // ManaV2's pure prediction accepts an empty cost by slowing the
            // cast. An accessory is a recovery source only with a potion the
            // same native QuickMana hook can actually use for this actor.
            return body.manaFlower && !body.cursed && !body.CCed && body.manaPotionDelay<=0 && a.Supplies.ManaFrame!=Main.GameUpdateCount &&
                ChooseQuick(a,CompanionSupplyKind.Mana,nativeMana:true).Found;
        }

        private static float WeaponRange(Player player, Item item)
        {
            // Conservative basic range for hand hits, whips and thrusts. More
            // involved projectiles can still prove reach with their real solver.
            if (item.shoot <= 0) return MathHelper.Clamp(Math.Max(item.width, item.height) * player.GetAdjustedItemScale(item) + 8, 44, 96);
            if (item.sentry) return 420;
            if (DoesStaySummoned(item)) return 660;
            if (HostFlailAttack.Weapon(item)) return HostFlailAttack.ThrowRange(player,item);
            // Native short swords emit a bounded owner-centred stab (AI161),
            // even though their inventory role carries an actual projectile.
            if (HostMeleeAttack.Rapier(item.shoot)) return MathHelper.Clamp(item.shootSpeed*14+20,44,118);
            if (ProjectileID.Sets.IsAWhip[item.shoot]) return MathHelper.Clamp(item.shootSpeed * item.useAnimation * 1.4f, 76, 220);
            if (ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out var projectile) && projectile.aiStyle == 19) return 96;
            if (HostYoyoNavigation.Weapon(item)) return ProjectileID.Sets.YoyosMaximumRange[item.shoot];
            return CompanionCombatRadius;
        }

        private static float DistanceToReceiveBox(Player player, NPC target)
        {
            Vector2 position = target.position + target.netOffset, center = player.Center;
            float dx = center.X - MathHelper.Clamp(center.X, position.X, position.X + target.width);
            float dy = center.Y - MathHelper.Clamp(center.Y, position.Y, position.Y + target.height);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private CompanionAttackProof ReadAttackProof(Actor a, CompanionAttackChoice choice)
        {
            var target = choice.Target;
            foreach(var proof in a.Combat.Proofs)
            {
                if(proof.Reachability==HostAttackReachability.Unknown || !proof.Choice.SamePair(choice))continue;
                // A short negative can prevent repeated ranking of a known
                // blocked shot. A positive is a narrower real hand lease.
                uint age=Main.GameUpdateCount-proof.Tick;
                if(age>(proof.Reachability==HostAttackReachability.Reachable?1U:6U) ||
                    proof.Revision!=NpcObservationRevision(target) || proof.Position!=target.Center+target.netOffset || proof.Velocity!=target.velocity ||
                    proof.Origin!=HostAttackModels.LaunchOrigin(a.Body,choice.Weapon) || !proof.Terrain.Current || !proof.Ammo.IdentityMatches(a.Body,choice.Weapon))continue;
                return proof;
            }
            return default(CompanionAttackProof);
        }

        private void RememberAttackProof(Actor a,CompanionAttackChoice choice,HostAttackSolution result,PredictionTerrain.CellLease terrain)
        {
            if(result.Reachability==HostAttackReachability.Unknown || terrain==null)return;
            var ammo=AttackAmmoSnapshot.Capture(a.Body,choice.Weapon);
            if(ammo==null)return;
            var target=choice.Target;
            var state=a.Combat;
            int index=0;
            while(index<state.Proofs.Length && !state.Proofs[index].Choice.SamePair(choice))index++;
            if(index==state.Proofs.Length){index=state.ProofCursor;state.ProofCursor=(index+1)%state.Proofs.Length;}
            state.Proofs[index]=new CompanionAttackProof {Choice=choice,Ammo=ammo,Terrain=terrain,Reachability=result.Reachability,
                Point=result.Point,Origin=HostAttackModels.LaunchOrigin(a.Body,choice.Weapon),Position=target.Center+target.netOffset,
                Velocity=target.velocity,Tick=Main.GameUpdateCount,Revision=NpcObservationRevision(target)};
        }

        private bool AttackLine(Actor a, CompanionAttackChoice choice, CompanionShotGeometry geometry, out Vector2 point, int nearbyIndex = -1)
        {
            point = choice.Target.Center + choice.Target.netOffset;
            var proof=ReadAttackProof(a,choice);
            if(proof.Reachability==HostAttackReachability.Blocked)return false;
            if(proof.Reachability==HostAttackReachability.Reachable){point=proof.Point;return true;}
            var player = a.Body;
            float range=WeaponRange(player,choice.Weapon);
            // Enter a close-range hand slightly inside its valid reach. This
            // margin prevents a stationary boundary from alternating slots.
            if(choice.CloseCombat && a.Combat.LastSlot!=choice.Slot)range-=4;
            if (DistanceToReceiveBox(player, choice.Target) > range) return false;
            var state=a.Combat;
            if (geometry.KnownFamily)
            {
                if (state.ShotTerrainTick != Main.GameUpdateCount)
                {
                    state.ShotTerrain.Reset();
                    state.ShotTerrainTick = Main.GameUpdateCount;
                    state.ShotCheckCount = 0;
                }
                // A precise basic result does not depend on the NPC sample
                // revision. Freshness still governs the optional trajectory.
                return BasicShotOpening(state, player, choice, geometry, out point) == HostAttackReachability.Reachable;
            }
            // Sky births need their own solver. Neither an obstructed hand
            // ray nor a clear hand ray describes a projectile born above.
            if (HostSkyAttack.Handles(choice.Weapon.type)) return false;
            Vector2 origin = choice.Weapon.shoot > 0 ? HostAttackModels.LaunchOrigin(player, choice.Weapon) : player.Center;
            if (nearbyIndex<0) return HostAttackAim.ActorDirectLine(state.DirectTerrain, origin, point);
            if (!state.DirectKnown[nearbyIndex] || state.DirectOrigins[nearbyIndex]!=origin)
            {
                state.DirectClear[nearbyIndex]=HostAttackAim.ActorDirectLine(state.DirectTerrain,origin,point);
                state.DirectOrigins[nearbyIndex]=origin;state.DirectKnown[nearbyIndex]=true;
            }
            return state.DirectClear[nearbyIndex];
        }

        private static HostAttackReachability BasicShotOpening(CompanionCombatState state, Player player,
            CompanionAttackChoice choice, CompanionShotGeometry geometry, out Vector2 point)
        {
            Vector2 origin = HostAttackModels.LaunchOrigin(player, choice.Weapon);
            Vector2 targetPosition = choice.Target.position + choice.Target.netOffset;
            var bounds = choice.Target.Hitbox;
            for (int i = 0; i < state.ShotCheckCount; i++)
            {
                var check = state.ShotChecks[i];
                if (!check.Choice.SamePair(choice) || !check.Geometry.SameShot(geometry) || check.Origin != origin ||
                    check.TargetPosition != targetPosition || check.TargetBounds != bounds || check.Gravity != player.gravDir ||
                    check.Rotation != player.fullRotation || check.Facing != player.direction) continue;
                point = check.Point;
                return check.Opening;
            }
#if DEBUG
            state.BasicShotReplays++;
#endif
            var result = geometry.Opening(player, choice.Target, state.ShotTerrain, Vector2.Zero, out point);
            if (state.ShotCheckCount < state.ShotChecks.Length)
                state.ShotChecks[state.ShotCheckCount++] = new CompanionShotCheck
                {
                    Choice = choice, Geometry = geometry, Origin = origin, TargetPosition = targetPosition, TargetBounds = bounds,
                    Gravity = player.gravDir, Rotation = player.fullRotation, Facing = player.direction, Point = point, Opening = result
                };
            return result;
        }

        private bool CurrentAttack(Actor a, CompanionAttackChoice choice, out int manaCost, out Vector2 point, bool afterShapeExit = false)
        {
            manaCost = 0; point = a.Body.Center;
            return choice.SameWeapon(a.Body) && TargetCurrent(a, choice.Target, choice.TargetGeneration) &&
                LegalResources(a,choice.Slot,choice.Weapon,out manaCost,afterShapeExit) &&
                AttackLine(a, choice, new CompanionShotGeometry(a.Body, choice.Weapon), out point);
        }

        private void NearbyTargets(Actor a)
        {
            var state = a.Combat;
            state.NearbyCount = 0;
            Array.Clear(state.DirectKnown,0,state.DirectKnown.Length);
#if DEBUG
            state.CandidateSweeps++;
#endif
            // Keep the closest seven plus a rotating farther candidate when
            // many enemies are present. A crowded wall cannot starve every
            // farther opening, while the per-weapon pair count stays bounded.
            NPC extra = null;
            int start = state.NearbyOffset;
            state.NearbyOffset = (start + 17) % Main.maxNPCs;
            const int nearestCount = CompanionCombatState.NearbyCapacity - 1;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                var target = Main.npc[(i + start) % Main.maxNPCs];
                if (!TargetCurrent(a, target, target?.generation ?? 0)) continue;
                float distance = DistanceToReceiveBox(a.Body, target);
                int index = Math.Min(state.NearbyCount, nearestCount);
                while (index > 0 && distance < state.Distances[index - 1]) index--;
                if (index == nearestCount) { if (extra == null) extra = target; continue; }
                if (state.NearbyCount == nearestCount && extra == null) extra = state.Nearby[nearestCount - 1];
                if (state.NearbyCount < nearestCount) state.NearbyCount++;
                for (int j = state.NearbyCount - 1; j > index; j--)
                { state.Nearby[j] = state.Nearby[j - 1]; state.Distances[j] = state.Distances[j - 1]; }
                state.Nearby[index] = target; state.Distances[index] = distance;
            }
            if (extra != null)
            {
                int index = state.NearbyCount++;
                state.Nearby[index] = extra; state.Distances[index] = DistanceToReceiveBox(a.Body, extra);
            }
        }

        private void ChooseAttacks(Actor a, CompanionBehavior preferences, bool afterShapeExit)
        {
            var state = a.Combat;
            NearbyTargets(a);
            var desired = default(CompanionAttackChoice);
            var ready = default(CompanionAttackChoice);
            state.Pursuit = default(CompanionAttackChoice);
            state.Probe = default(CompanionAttackChoice);
            if (state.NearbyCount == 0) { state.Choice = state.ReadyChoice = desired; return; }

            int probeOrder = Int32.MaxValue;
            int[] main = state.Preferences;
            MapWeaponSlots(a.Body, preferences.MainWeapons, main);
            for (int i = 0; i < preferences.MainWeapons.Length; i++)
                EvaluateWeapon(a, main[i], afterShapeExit, ref desired, ref ready, ref probeOrder);
            var preferred = desired;

            // Even when the preferred plan needs mana, have a legal fallback
            // available if the actual native restoration cannot cover its fee.
            if (ready.Weapon == null)
            {
                int retain = state.ReadyChoice.Weapon != null ? state.ReadyChoice.Slot : state.Choice.Weapon != null ? state.Choice.Slot : -1;
                if (retain >= 0 && Array.IndexOf(main, retain, 0, preferences.MainWeapons.Length) < 0)
                    EvaluateWeapon(a, retain, afterShapeExit, ref desired, ref ready, ref probeOrder);
                int eligible = 0;
                for (int checkedSlots = 0; checkedSlots < CompanionBehavior.MainInventorySlots && eligible < FallbackWeaponsPerSweep; checkedSlots++)
                {
                    int slot = state.InventoryCursor;
                    state.InventoryCursor = (state.InventoryCursor + 1) % CompanionBehavior.MainInventorySlots;
                    if (slot == retain || Array.IndexOf(main, slot, 0, preferences.MainWeapons.Length) >= 0 ||
                        !CompanionItemRules.WeaponEligible(a.Body.inventory[slot])) continue;
                    eligible++;
                    EvaluateWeapon(a, slot, afterShapeExit, ref desired, ref ready, ref probeOrder);
                }
            }

            // Allow the explicit AI solver to find a curve, remote birth or
            // specialty contact when a direct line is unavailable. Spend at
            // most one shared, rotating probe on this whole planning slice.
            if (state.Probe.Weapon != null && TryProbeAttack(a, state.Probe))
            {
                var probe = state.Probe;
                bool mainProbe = Array.IndexOf(main, probe.Slot, 0, preferences.MainWeapons.Length) >= 0;
                int manaCost;
                if (LegalResources(a,probe.Slot,probe.Weapon,out manaCost,afterShapeExit))
                {
                    if (mainProbe) Prefer(ref preferred, probe);
                    Prefer(ref desired, probe);
                    if (manaCost <= a.Body.statMana && (mainProbe || preferred.Weapon == null || ready.Weapon == null)) Prefer(ref ready, probe);
                }
            }
            state.Choice = preferred.Weapon == null ? desired : preferred;
            state.ReadyChoice = ready;
        }

        private void EvaluateWeapon(Actor a, int slot, bool afterShapeExit, ref CompanionAttackChoice desired, ref CompanionAttackChoice ready, ref int probeOrder)
        {
            if (slot < 0) return;
            var weapon = a.Body.inventory[slot];
            int manaCost;
            if (!LegalResources(a,slot,weapon,out manaCost,afterShapeExit)) return;
            bool missingMana = manaCost > a.Body.statMana;
            var state = a.Combat;
            var geometry = new CompanionShotGeometry(a.Body, weapon);
            int pairCount = CompanionBehavior.MainInventorySlots * CompanionCombatState.NearbyCapacity;
            float range = WeaponRange(a.Body, weapon);
            for (int i = 0; i < state.NearbyCount; i++)
            {
                var target = state.Nearby[i];
                float distance = state.Distances[i];
                // Distance is primary. Close-range fit and the current hand
                // give only a modest margin, preventing equal-score swaps.
                bool close = range <= 220 || HostFlailAttack.Weapon(weapon);
                float score = distance + (!close && distance < 92 ? 18 : 0) + (missingMana ? 8 : 0);
                if (ReferenceEquals(target, a.Target) && a.TargetGeneration == target.generation) score -= 10;
                if (state.LastSlot==slot && ReferenceEquals(state.LastWeapon,weapon))
                    score-=Main.GameUpdateCount-state.SelectedTick<12?22:14;
                var candidate = new CompanionAttackChoice { Target = target, TargetGeneration = target.generation, Slot = slot,
                    Weapon = weapon, Type = weapon.type, Prefix = weapon.prefix, CloseCombat = close, Score = score };
                Prefer(ref state.Pursuit, candidate);
                if ((BetterAttack(desired, candidate) || !missingMana && BetterAttack(ready, candidate)) &&
                    AttackLine(a, candidate, geometry, out _, i))
                {
                    Prefer(ref desired, candidate);
                    if (!missingMana) Prefer(ref ready, candidate);
                }
                if (weapon.shoot <= 0 || DoesStaySummoned(weapon) || distance > Math.Max(160, range) || !FreshNpcObservation(target) ||
                    !NpcTilePhysicsKnown(target) || ReadAttackProof(a,candidate).Reachability!=HostAttackReachability.Unknown)continue;
                int index = slot * CompanionCombatState.NearbyCapacity + i;
                int order = (index - state.ProbeCursor + pairCount) % pairCount;
                if (order < probeOrder) { state.Probe = candidate; probeOrder = order; }
            }
        }

        private Projectile ControlledShot(Actor a,Item weapon)
        {
            var shot=a.Combat.ControlledShot;
            return shot!=null && shot.active && shot.type==weapon.shoot && ReferenceEquals(ProjectileActor(shot),a) ? shot:null;
        }

        private static void Prefer(ref CompanionAttackChoice best, CompanionAttackChoice choice)
        {
            if (BetterAttack(best, choice)) best = choice;
        }

        private static bool BetterAttack(CompanionAttackChoice best, CompanionAttackChoice candidate)
        {
            return best.Weapon == null || candidate.Score < best.Score ||
                candidate.Score == best.Score && candidate.Slot < best.Slot;
        }

        private bool TryProbeAttack(Actor a, CompanionAttackChoice probe)
        {
            var state = a.Combat;
            state.ProbeCursor = (probe.Slot * CompanionCombatState.NearbyCapacity + Array.IndexOf(state.Nearby, probe.Target, 0, state.NearbyCount) + 1) %
                (CompanionBehavior.MainInventorySlots * CompanionCombatState.NearbyCapacity);
            using (work.Enter(Main.GameUpdateCount))
            using (CombatPredictionWork.Limit(1))
            {
                if (!CombatPredictionWork.TryContinue()) return false;
                try
                {
#if DEBUG
                    state.PairProbes++;
#endif
                    NpcTrajectory timeline = null;
                    PredictionTerrain.CellLease terrain = null;
                    PredictionPlayerMotion? premise = null;
                    uint tick = 0, anchor = 0;
                    var result = HostAttackAim.ActorSolution(a.Body, probe.Weapon, probe.Target, session, prediction, ref timeline,
                        ref terrain, true, NpcSampleTick(probe.Target), ref premise, ref tick, ref anchor);
                    RememberAttackProof(a,probe,result,terrain);
                    return result.Reachability==HostAttackReachability.Reachable && result.HasPoint && terrain!=null;
                }
                catch (CombatPredictionBudgetExceededException) { return false; }
            }
        }
    }
}
