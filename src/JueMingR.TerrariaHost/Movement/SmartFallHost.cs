using System;
using System.Diagnostics;
using JueMingR.Features.Movement;
using JueMingR.TerrariaHost.Fishing;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Movement
{
    // One finite rescue, owned by MovementSession's exact local player epoch.
    // Native consumers alone own motion, fallStart, immunity and resources.
    // An equipment loan never creates a charge or bypasses a release phase.
    internal sealed class SmartFallHost
    {
        private readonly MovementSession session;
        private readonly HostItems items;
        private readonly SmartFallBorrowOwner borrow;
        private readonly SmartFallGrapple grapple = new SmartFallGrapple();
        private readonly SmartFallCandidate[] candidates = new SmartFallCandidate[12];
        private readonly Item[] sources = new Item[12];
        private readonly Item[] observedSources = new Item[12];
        private readonly SmartFallPreparation[] preparations = new SmartFallPreparation[12];
        private readonly Item[] hookSources = new Item[51];
        private readonly SmartFallPreparation[] hookPreparations = new SmartFallPreparation[51];
        private readonly SmartFallGrapplePlan[] hookGeometry = new SmartFallGrapplePlan[51];
        private readonly ProjectileKey[] blockingHooks = new ProjectileKey[32];
        private readonly bool[] blockingAttached = new bool[32];
        private readonly SmartFallContactBand contactBand = new SmartFallContactBand();
        // Separate bounded read set: upper anchors/rays remain relevant even
        // after the player has passed their Y; liquid also affects native flight.
        private readonly SmartFallContactBand grappleDependencies = new SmartFallContactBand(false, true);
        private int sourceCount, hookCount, blockingCount, sampledWidth, sampledHeight;
        private long blockerStamp;
        private readonly Func<Item, bool> jumpSourceReady;
        private readonly Func<Item, bool> rocketSourceReady, carpetSourceReady;
        private int actionResourceBit, resourceBefore;
        private bool resourceConsumed, resourceHadCarpetStart, jumpReleasing, jumpPressed;
        private long releaseTick=-1, upPressTick=-1;
        private Item originalMountItem, originalSelectionItem;
        private int mountSelection;
        private SmartFallProtectionOptions sampledOptions;
        private readonly Action<Item[], int> previousManualSlot;
        private readonly Func<Item, bool> previousProtection;
        private readonly Func<int, bool> previousUse;
        private readonly Func<Player, Item, bool> previousNativeUse;
        private Player player, retained;
        private long generation, retainedGeneration, tick, sourceRevision = -1, contactTick, sampledTick;
        private int mask = -1, count, life, ownedMount = -1, originalMount = -1, rejected;
        private Vector2 sampledPosition, sampledVelocity, actionPosition, teleportTarget;
        private bool sampledDown, active, action, landed, returnGravity, scope, permitted, targetActive, bandValid, grappleSearched, takenOver;
        private bool jumpOwned, upOwned;
        private float originalGravity;
        private int originalFallStart;
        private SmartFallFamily family;
        private Item selected;
        private SmartFallContact contact;
        private SmartFallGrapplePlan grapplePlan;
        private ProjectileKey shotKey;
        private bool shot;
        private bool failedShot, grappleDependenciesValid;
        private Vector2 lastMountReturnPosition;
        private long lastMountReturnRevision = -1;
        private bool mountReturnAttempted;
        internal SmartFallResult Result { get; private set; }
        internal SmartFallResult ReturnResult { get { return borrow.ReturnResult; } }
        internal long TileReads = 0, SourceScans = 0, Plans = 0, NativeActions = 0, Protections = 0;
        internal bool HasPending { get { return action || returnGravity || ownedMount >= 0 || originalMount >= 0 || borrow.HasPending; } }

        internal SmartFallHost(MovementSession session, HostItems items = null, Func<long> currentGeneration = null, FishingEquipment fishing = null)
        {
            this.session = session; this.items = items;
            jumpSourceReady = item => retained != null && SmartFallCapabilities.UnusedForItem(retained,item);
            rocketSourceReady = item => retained != null && (retained.rocketTime > 0 || retained.empressBrooch);
            carpetSourceReady = item => retained != null && (retained.canCarpet || retained.carpetTime > 0);
            borrow = new SmartFallBorrowOwner(items, currentGeneration ?? (() => generation), SmartFallCapabilities.Matches, fishing);
            if (items != null)
            {
                previousManualSlot = items.World.BeforeManualFallSlot;
                items.World.BeforeManualFallSlot = BeforeManualSlot;
                previousProtection = items.World.AdditionalProtection;
                items.World.AdditionalProtection = ProtectItem;
                previousUse = items.AllowsOwnedUse;
                items.AllowsOwnedUse = AllowsItemUse;
                previousNativeUse = items.World.AllowsOwnedFallNativeUse;
                items.World.AllowsOwnedFallNativeUse = AllowsNativeUse;
            }
            try { SmartFallCapabilities.Bind(); } catch { /* Mount data unavailable: other native families remain independent. */ }
            SmartFallGrappleHooks.Install();
        }
        internal void BeginUpdate(Player p, long epoch, long updateTick)
        {
            if (!ReferenceEquals(retained, p) || retainedGeneration != epoch)
            { ClearState(); retained = p; retainedGeneration = epoch; }
            player = p; generation = epoch; tick = updateTick; scope = true; permitted = false;jumpPressed=false;
        }
        private bool Current(Player p) { return scope && ReferenceEquals(player, p) && ReferenceEquals(retained, p) && generation == retainedGeneration; }
        internal void Capture(Player p, bool canAct)
        {
            if (!Current(p)) return;
            permitted = canAct;
            if (HasPending) { Continue(p); if (HasPending) return; }
            if (!canAct || !session.Value.SmartFallProtection.Enabled) return;
            if (takenOver) { if (SmartFallCapabilities.Risk(p) || SmartFallCapabilities.CriticalApproach(p)) return; takenOver = false; }
            bool currentRisk = SmartFallCapabilities.Risk(p);
            if (!currentRisk && !SmartFallCapabilities.CriticalApproach(p)) { active = false; bandValid = false; rejected = blockingCount = 0; failedShot = grappleDependenciesValid = false; contact = default(SmartFallContact); return; }
            int currentMask = SmartFallCapabilities.NativeMask(p);
            long revision = items == null ? 0 : items.World.Revision;
            if (!active || sourceRevision != revision || mask != currentMask || !ReferenceEquals(sampledOptions, session.Value.SmartFallProtection))
            { Refresh(p, revision, currentMask); active = true; }
            // Native busy/selection gates are O(1) booleans, not countdown
            // values. Only an actual change re-reads endpoint qualification.
            long blockers = borrow.BlockerStamp(p);
            if (blockers != blockerStamp)
            { rejected &= blockingCount == 0 ? 0 : 1 << (int)SmartFallFamily.Grapple; Refresh(p, revision, currentMask); }
            if (sourceCount == 0) return;
            if (blockingCount > 0 && BlockingHookChanged(p))
            { blockingCount = 0; rejected &= ~(1 << (int)SmartFallFamily.Grapple); grappleSearched = false; bandValid = false; }
            // A failed shot owns its exact identity even after the item loan
            // returns. Retirement and relevant geometry must be observed BEFORE
            // the all-endpoints-rejected exit; no projectile-pool polling.
            Projectile failed;
            if (failedShot && (!shotKey.TryGetActive(out failed) || failed.owner != p.whoAmI || failed.aiStyle != 7))
            { failedShot = false; ReopenGrapple(); }
            // Ordinary braking changes velocity every Update, but does not
            // invalidate a level, continuously supported SAFE landing. Its
            // bounded reachable support strip is checked before exact-path
            // staleness; dangerous contacts keep the full original timing path.
            if (!currentRisk && bandValid && p.width == sampledWidth && p.height == sampledHeight &&
                p.controlDown == sampledDown && p.gravDir == originalGravity &&
                !SmartFallCapabilities.ContactRisk(p, contact) && SmartFallGeometry.SafeSupport(p, contact, ref TileReads)) return;
            // A retained contact is consumed only while its input and simple
            // local geometry remain current; otherwise query the finite band.
            bool stale = !bandValid || p.controlDown != sampledDown || p.gravDir != originalGravity ||
                Vector2.DistanceSquared(p.position, sampledPosition) > 128 * 128 ||
                Math.Abs(p.velocity.X - sampledVelocity.X) > .1f || Math.Abs(p.position.X - (sampledPosition.X + sampledVelocity.X * (tick - sampledTick))) > 8 ||
                Math.Abs(p.velocity.Y * p.gravDir - Math.Min(p.maxFallSpeed, sampledVelocity.Y * p.gravDir + p.gravity * (tick - sampledTick))) > .5f;
            if (!stale) stale = contactBand.Changed(p, ref TileReads);
            if (grappleDependenciesValid && grappleDependencies.Changed(p, ref TileReads)) ReopenGrapple();
            if (stale)
            {
                if (failedShot || grappleSearched) ReopenGrapple();
                Count(ref Plans); contact = SmartFallGeometry.Contact(p, ref TileReads, contactBand); contactTick = sampledTick = tick; bandValid = true; grappleSearched = false;
                sampledPosition = p.position; sampledVelocity = p.velocity; sampledDown = p.controlDown; originalGravity = p.gravDir;
                sampledWidth = p.width; sampledHeight = p.height;
            }
            else if (contact.Found)
            {
                contact.Ticks = Math.Max(0, contact.Ticks - (tick - contactTick)); contactTick = tick;
            }
            if (!contact.Found || contact.SafeSurface) return;
            bool contactRisk = currentRisk || SmartFallCapabilities.ContactRisk(p, contact);
            if (!contactRisk) return;
            bool usableEndpoint = false;
            for (int i = 0; i < 12; i++)
                if (session.Value.SmartFallProtection.Allows(i) && (rejected & 1 << i) == 0 &&
                    (preparations[i].AvailableNow || (mask & 1 << i) != 0 || i == (int)SmartFallFamily.Grapple && hookCount > 0)) { usableEndpoint = true; break; }
            if (!usableEndpoint) return;
            BuildCandidates(p);
            int choice = SmartFallDecision.Choose(candidates, count, contact.Ticks);
            if (choice < 0 || !Due(p, candidates[choice])) return;
            if (!session.CanAct(p) || !(SmartFallCapabilities.Risk(p) || SmartFallCapabilities.ContactRisk(p, contact))) return;
            // Capture the original native selection BEFORE the rescue item is
            // exchanged into its misc slot. Type alone cannot authorize reuse.
            Item priorMount = p.mount.Active ? MountID.Sets.Cart[p.mount.Type] ? p.miscEquips[2] : p.QuickMount_GetItemToUse() : null;
            int priorMountType = p.mount.Active ? p.mount.Type : -1;
            var c = candidates[choice]; Item item = sources[(int)c.Family];
            while (item != null && !borrow.TryBorrow(p, generation, item, c.Family))
            {
                rejected |= 1 << (int)c.Family; candidates[choice].Ready = false;
                if (borrow.HasPending) return;
                choice = SmartFallDecision.Choose(candidates, count, contact.Ticks);
                if (choice < 0 || !Due(p, candidates[choice])) return;
                c = candidates[choice]; item = sources[(int)c.Family];
            }
            family = c.Family; selected = item; action = true; landed = false; shot = false;
            actionResourceBit=resourceBefore=0;resourceConsumed=resourceHadCarpetStart=jumpReleasing=jumpPressed=false;releaseTick=upPressTick=-1;
            life = p.statLife; originalFallStart = p.fallStart; actionPosition = p.position;
            originalGravity = p.gravDir; originalMount = family==SmartFallFamily.FlyingMount || family==SmartFallFamily.SafeMount ? priorMountType : -1;
            originalMountItem = priorMount; mountSelection = p.selectedItem;
            originalSelectionItem = p.HeldItem;
            mountReturnAttempted = false;
            Result = SmartFallResult.Prepared;
            if (family == SmartFallFamily.GravityGlobe) LendUp(p);
            if (family == SmartFallFamily.FlyingMount || family == SmartFallFamily.SafeMount)
            {
                if (p.mount.Active && originalMount != item.mountType) { p.QuickMount(); Count(ref NativeActions); }
                if (!p.mount.Active && item != null) QuickMount(p, item, family);
                if (p.mount.Active && item != null && p.mount.Type == item.mountType) { ownedMount = p.mount.Type; Result = SmartFallResult.Consumed; }
                else Fail(p);
            }
        }
        private void ReopenGrapple()
        { rejected &= ~(1 << (int)SmartFallFamily.Grapple); grappleSearched = grappleDependenciesValid = false; }
        private bool AllowsNativeUse(Player p, Item item)
        { return borrow.AllowsNativeUse(p, item) || (previousNativeUse?.Invoke(p, item) ?? false); }
        private void QuickMount(Player p, Item item, SmartFallFamily actionFamily)
        {
            long invocation = borrow.BeginNativeUse(p, generation, item, actionFamily);
            try { p.QuickMount(); Count(ref NativeActions); }
            finally { borrow.EndNativeUse(invocation); }
        }
        private bool Due(Player p, SmartFallCandidate candidate)
        {
            if (SmartFallDecision.Due(candidate, contact.Ticks)) return true;
            if (candidate.Family != SmartFallFamily.Grapple || !grapplePlan.Found) return false;
            // A moving player's time-to-anchor can increase while time to
            // contact falls. Check the next legal launch explicitly; a fixed
            // margin or binary search can jump over the last feasible phase.
            int next = grapple.FirstProtection(p, grapplePlan.Item, grapplePlan.Target, ref TileReads, 1);
            return contact.Ticks > candidate.Delay && (next < 0 || contact.Ticks - 1 <= next);
        }
        private void Refresh(Player p, long revision, int nativeMask)
        {
            Count(ref SourceScans); sourceRevision = revision; int previousMask = mask; mask = nativeMask; sourceCount = 0; grappleSearched = false; hookCount = 0;
            blockerStamp = borrow.BlockerStamp(p);
            var options = session.Value.SmartFallProtection;
            sampledOptions = options;
            if (!options.Grapple) { failedShot = grappleDependenciesValid = false; blockingCount = 0; }
            for (int i = 0; i < 12; i++)
            {
                Func<Item,bool> eligible=i==(int)SmartFallFamily.DoubleJump?jumpSourceReady:i==(int)SmartFallFamily.RocketBoots?rocketSourceReady:i==(int)SmartFallFamily.Carpet?carpetSourceReady:null;
                Item next = options.Allows(i) ? borrow.GetCandidate(p,(SmartFallFamily)i,eligible) : null;
                // Our own failed action's restore legitimately increments the
                // inventory revision. It is not a new capability and cannot
                // reopen the identical failed transaction every Update.
                if (!ReferenceEquals(observedSources[i], next) || ((previousMask ^ nativeMask) & 1 << i) != 0 || i == (int)SmartFallFamily.DoubleJump && ((previousMask ^ nativeMask) & (511 << 12)) != 0) rejected &= ~(1 << i);
                sources[i] = observedSources[i] = next;
                preparations[i] = next == null ? default(SmartFallPreparation) : borrow.GetPreparation(p, generation, next, (SmartFallFamily)i);
                // Native ready abilities may belong to a mount rather than an
                // Item endpoint. They still go through their real consumers.
                if (options.Allows(i) && (sources[i] != null || (nativeMask & 1 << i) != 0)) sourceCount++;
            }
            if (options.Grapple)
            {
                AddHook(p, p.miscEquips[4]);
                if (items != null) for (int slot = 0; slot < 50; slot++) AddHook(p, p.inventory[slot]);
            }
        }
        private void AddHook(Player p, Item item)
        {
            if (!SmartFallCapabilities.Matches(item, SmartFallFamily.Grapple)) return;
            for (int i = 0; i < hookCount; i++) if (ReferenceEquals(hookSources[i], item)) return;
            var preparation = borrow.GetPreparation(p, generation, item, SmartFallFamily.Grapple);
            if (!preparation.AvailableNow) return;
            hookSources[hookCount] = item; hookPreparations[hookCount++] = preparation;
        }
        private void BuildCandidates(Player p)
        {
            count = 0;
            var options = session.Value.SmartFallProtection;
            for (int i = 0; i < 12; i++)
            {
                if (!options.Allows(i) || (rejected & 1 << i) != 0) continue;
                var f = (SmartFallFamily)i; Item item = sources[i];
                bool native = (mask & 1 << i) != 0, ready = false; int delay = 0;
                if (item == null && !native) continue;
                var preparation = preparations[i];
                if (item != null && !preparation.AvailableNow && f != SmartFallFamily.Grapple) continue;
                switch (f)
                {
                    case SmartFallFamily.Horseshoe: ready = item != null && !p.stoned; break;
                    case SmartFallFamily.Wings: ready = item != null && !p.stoned; break;
                    case SmartFallFamily.DjinnLegs: ready = item != null && !p.stoned; delay = 1; break;
                    case SmartFallFamily.DoubleJump: ready = (JumpAvailable(p,item)!=0 || SmartFallCapabilities.MountJump(p)) && p.grapCount == 0 && (!p.mount.Active || !p.mount.BlockExtraJumps); break;
                    case SmartFallFamily.RocketBoots:
                        ready = (p.rocketTime > 0 || p.empressBrooch) && p.CanUseBootFlyingAbilities && p.grapCount == 0 && !p.mount.Active;
                        // Delay is not first ignition: native rocketDelay
                        // resets the baseline only in the NEXT early Update.
                        float first = p.velocity.Y * p.gravDir - .1f;
                        if (first > 0) first -= .5f; else if (first > -Player.jumpSpeed * .5f) first -= .1f;
                        delay = first + Math.Abs(p.gravity) <= 0 ? 0 : 1; break;
                    case SmartFallFamily.Carpet: ready = p.gravDir > 0 && (p.canCarpet || p.carpetTime > 0) && !MovementJumpHost.ExtraJump(p) && p.jump==0 && p.rocketTime == 0 && p.wingTime == 0 && !p.mount.Active && p.grapCount == 0; break;
                    case SmartFallFamily.Umbrella: ready = item != null && !p.controlDown && p.itemAnimation == 0 && !p.pulley; delay = 1; break;
                    case SmartFallFamily.Grapple:
                        if (hookCount > 0 && SmartFallGrappleHooks.Available && !p.noItems && !p.CCed && !p.controlHook && p.grapCount == 0)
                        {
                            if (!grappleSearched)
                            {
                            grappleDependencies.Begin();
                            grapplePlan = default(SmartFallGrapplePlan); int bestCost = int.MaxValue;
                            // Nominal speed does not order native flight:
                            // Queen uses four discrete sub-updates. Geometry
                            // is shared only within this search for identical
                            // native flight parameters; endpoint preparation
                            // and interference remain independent. Even a
                            // shared failed search must not be repeated N times.
                            for (int slot = 0; slot < hookCount; slot++)
                            {
                                Item other = hookSources[slot];
                                int same = -1;
                                for (int prior = 0; prior < slot; prior++)
                                    if (SmartFallGrapple.SameFlight(hookSources[prior], other)) { same = prior; break; }
                                var alternative = same < 0 ? grapple.Find(p, other, contact, ref TileReads, grappleDependencies) : hookGeometry[same];
                                // A shared ray never lends its representative
                                // Item identity to another inventory endpoint.
                                alternative.Item = other; hookGeometry[slot] = alternative;
                                if (!alternative.Found) continue;
                                int alternativeCost = Interference(p, f, other, hookPreparations[slot], alternative.Target);
                                if (!grapplePlan.Found || alternativeCost < bestCost || alternativeCost == bestCost && alternative.Delay < grapplePlan.Delay)
                                { grapplePlan = alternative; bestCost = alternativeCost; preparations[i] = hookPreparations[slot]; }
                            }
                            grappleSearched = true;
                            grappleDependenciesValid = grappleDependencies.Complete;
                            }
                            else if (grapplePlan.Found)
                            {
                                int now = grapple.FirstProtection(p, grapplePlan.Item, grapplePlan.Target, ref TileReads);
                                grapplePlan.Delay = now; grapplePlan.Found = now >= 0;
                            }
                            ready = grapplePlan.Found; delay = grapplePlan.Delay; if (ready) sources[i] = grapplePlan.Item;
                        }
                        break;
                    case SmartFallFamily.SafeMount:
                        // Native CanMount tests the proposed box at the next
                        // velocity offset. Its legal summon window closes one
                        // phase before a short final gap, even though its fall
                        // immunity itself needs no Hover consumer.
                        ready = CanMount(p, item); delay = 1; break;
                    case SmartFallFamily.FlyingMount:
                        // New mounts enter with frameState=0; PlayerFrame
                        // establishes the airborne frame after collision. The
                        // first timely Hover can therefore be next Update.
                        ready = CanMount(p, item) && (!contact.Platform || !BypassesPlatform(item.mountType) || SmartFallCapabilities.HoverMount(item.mountType));
                        // A bypass mount's previous collision leaves stairFall.
                        // Only a native slope pass AFTER real Hover/dismount can
                        // clear it. Flat platforms need no additional phase.
                        delay = contact.Platform && contact.Slope != 0 && BypassesPlatform(item.mountType) ? 2 : 1; break;
                    case SmartFallFamily.GravityGlobe:
                        ready = item != null && !p.mount.Active && !p.controlUp && p.forcedGravity <= 0 && OppositeClear(p); break;
                    case SmartFallFamily.TeleportRod:
                        ready = item != null && p.itemAnimation == 0 && p.itemTime == 0 && !p.noItems &&
                            (item.type != 1326 || !p.chaosState || p.statLife > p.statLifeMax2 / 7) && TeleportPoint(p);
                        delay = 1; break;
                }
                if (ready && f == SmartFallFamily.DoubleJump && !p.releaseJump) delay++;
                if (ready && f == SmartFallFamily.RocketBoots && !p.rocketRelease) delay++;
                if (ready && f == SmartFallFamily.GravityGlobe && !p.releaseUp) delay++;
                int cost = Interference(p, f, sources[i], preparations[i], f == SmartFallFamily.Grapple ? grapplePlan.Target : teleportTarget);
                candidates[count++] = new SmartFallCandidate { Family = f, Ready = ready, Delay = delay, Interference = cost };
            }
        }
        // Compare complete short plans, rather than assigning a rank to a
        // family. Endpoint writes include their return, occupied/fishing
        // endpoints add recovery coupling, and native input/resource/trajectory
        // effects depend on this player's current state. Units are deliberately
        // coarse consequences; they are not milliseconds or a native simulator.
        private int Interference(Player p, SmartFallFamily f, Item item, SmartFallPreparation preparation, Vector2 target)
        {
            int cost = preparation.BorrowWrites * 4 + preparation.SelectionWrites * 4;
            if (preparation.BorrowWrites > 0 && ReferenceEquals(preparation.Source, p.inventory) && preparation.SourceSlot == p.selectedItem) cost += 4;
            if (preparation.Displaced) cost += 8;
            if (preparation.FishingEndpoint) cost += 12;
            if (preparation.ReturnPending) cost += 4;
            float speed = Math.Abs(p.velocity.Y);
            switch (f)
            {
                case SmartFallFamily.DoubleJump:
                    int charges = 0, available = JumpAvailable(p,item);
                    for (int bits = available; bits != 0; bits &= bits - 1) charges++;
                    cost += (p.controlJump ? 0 : 1) + (p.releaseJump ? 0 : 1) + 8 / Math.Max(1, charges) + (int)Math.Ceiling((speed + Player.jumpSpeed) / 4); break;
                case SmartFallFamily.RocketBoots:
                    cost += (p.controlJump ? 0 : 1) + (p.rocketRelease ? 0 : 1) + (int)Math.Ceiling(8 / Math.Max(1f, p.rocketTime)) + (int)Math.Ceiling(Math.Min(speed, .6f) / 4); break;
                case SmartFallFamily.Carpet:
                    cost += (p.controlJump ? 0 : 1) + (p.releaseJump ? 0 : 1) + (int)Math.Ceiling(8f / Math.Max(1, p.canCarpet ? 300 : p.carpetTime)) + (int)Math.Ceiling(speed / 4); break;
                case SmartFallFamily.DjinnLegs: case SmartFallFamily.Umbrella:
                    cost += (int)Math.Ceiling(Math.Max(0, speed - 2) / 4) + (preparation.HeldSelectionReady ? 0 : f == SmartFallFamily.Umbrella ? 1 : 0); break;
                case SmartFallFamily.Grapple:
                    // Native QuickGrapple performs its own one-time capacity
                    // scan. Latching changes trajectory and leaves a live hook
                    // under manual ownership after our finite return.
                    cost += 4 + (int)Math.Ceiling(Vector2.Distance(p.Center, target) / 32) + (p.mount.Active ? 8 : 0); break;
                case SmartFallFamily.SafeMount: case SmartFallFamily.FlyingMount:
                    cost += 2 + (p.mount.Active ? 12 : 0);
                    if (f == SmartFallFamily.FlyingMount) cost += (p.controlJump ? 0 : 1) + (int)Math.Ceiling(speed / 4); break;
                case SmartFallFamily.GravityGlobe:
                    cost += (p.controlUp ? 0 : 2) + (int)Math.Ceiling(speed / 2) + 4; break;
                case SmartFallFamily.TeleportRod:
                    cost += (p.controlUseItem ? 0 : 1) + Math.Max(item == null ? 0 : item.useAnimation, item == null ? 0 : item.useTime) / 6 + (int)Math.Ceiling(Vector2.Distance(p.position, target) / 16);
                    if (item != null && item.type == 1326 && p.chaosState) cost += Math.Max(1, p.statLifeMax2 / 7); break;
            }
            return cost;
        }
        private static bool CanMount(Player p, Item item)
        { return item != null && !p.noItems && !p.CCed && !p.tongued && p.gravDir > 0 && (!p.mount.Active || p.mount.CanDismount(p)) && p.mount.CanMount(item.mountType, p); }
        private static bool BypassesPlatform(int type)
        { return MountID.Sets.Cart[type] || type == 12 || type == 7 || type == 8 || type == 23 || type == 44 || type == 48; }
        private bool OppositeClear(Player p)
        {
            Vector2 velocity = new Vector2(p.velocity.X, -p.gravDir * Math.Max(2, Math.Abs(p.velocity.Y)));
            var other = SmartFallGeometry.Contact(p.position, velocity, p.width, p.height, p.gravity, p.maxFallSpeed, -p.gravDir, false, ref TileReads);
            return !other.Found || other.Ticks > 2;
        }
        private bool TeleportPoint(Player p)
        {
            var point = new Vector2(contact.X, contact.Y - p.gravDir);
            if (!SmartFallGeometry.Empty(point, p.width, p.height, ref TileReads)) return false;
            Vector2 limited = point; p.LimitPointToPlayerReachableArea(ref limited);
            if (limited != point) return false;
            // TryCorrect reports whether it CHANGED a blocked original point;
            // false also means that the original point was already legal.
            // This finite contact-derived point needs native eligibility, not
            // another nearby search or that API's changed/not-changed result.
            if (point.X <= 50 || point.Y <= 50 || point.X >= Main.maxTilesX * 16 - 50 || point.Y >= Main.maxTilesY * 16 - 50) return false;
            Point from = p.Center.ToTileCoordinates(), to = (point + new Vector2(p.width / 2, p.height / 2)).ToTileCoordinates();
            if (Collision.AnyWallOfTypeOnLine(from.X, from.Y, to.X, to.Y, 350)) return false;
            var tile = SmartFallGeometry.Read((int)(point.X / 16), (int)(point.Y / 16), ref TileReads);
            if (tile == null || tile.wall == 87 && !NPC.downedPlantBoss && (Main.remixWorld || point.Y / 16 > Main.worldSurface) || Collision.SolidCollision(point, p.width, p.height)) return false;
            teleportTarget = point; return true;
        }
        internal void AfterEquips(Player p)
        {
            if (!Current(p) || !action) return;
            if (landed) { if (p.statLife >= life && p.velocity.Y == 0) Protected(p, false); else Fail(p); return; }
            ObserveMotion(p);
            if (action && family == SmartFallFamily.DjinnLegs && p.slowFall && p.fallStart != originalFallStart) Protected(p, true);
            if (family == SmartFallFamily.GravityGlobe && p.gravControl2 && (permitted || returnGravity)) LendUp(p);
        }
        internal void BeforeJump(Player p)
        {
            if (!Current(p) || !action || !permitted || !session.CanAct(p)) return;
            bool jump = family == SmartFallFamily.DoubleJump || family == SmartFallFamily.RocketBoots || family == SmartFallFamily.Carpet || family == SmartFallFamily.FlyingMount;
            if (!jump || p.grapCount > 0 || p.jump > 0 || p.velocity.Y * p.gravDir <= 0) return;
            if(family!=SmartFallFamily.FlyingMount && !PrepareResource(p)){Fail(p);return;}
            jumpReleasing = family==SmartFallFamily.DoubleJump && !p.releaseJump || family==SmartFallFamily.RocketBoots && !p.rocketRelease;
            if (!session.JumpHost.ReserveFallInput(p,jumpReleasing)) return;
            if (jumpReleasing)
            {
                // Keep the effective release through ALL later wing/rocket
                // reads this Update. Only native consumers set release fields.
                releaseTick=tick;return;
            }
            if (!p.controlJump) { p.controlJump = true; jumpOwned = true; }
            jumpPressed=true;Count(ref NativeActions);
        }
        internal void AfterJump(Player p)
        {
            if (!Current(p) || !action) return;
            if (family == SmartFallFamily.GravityGlobe && p.gravDir != originalGravity)
            { Result = SmartFallResult.Consumed; returnGravity = true; }
            else if (family == SmartFallFamily.GravityGlobe && returnGravity && p.gravDir == originalGravity)
            { returnGravity = false; Protected(p, true); }
            if(family==SmartFallFamily.DoubleJump && jumpPressed)
            {
                resourceConsumed=actionResourceBit!=0 && (SmartFallCapabilities.UnusedJumps(p)&actionResourceBit)==0;
                if(resourceConsumed && p.velocity.Y*p.gravDir<0 && p.jump>0)Protected(p,true);
                else Fail(p);
            }
        }
        internal void AfterFlight(Player p) { if (Current(p)) ObserveMotion(p); }
        private void ObserveMotion(Player p)
        {
            if (!action) return;
            if(family==SmartFallFamily.RocketBoots && actionResourceBit==32 && jumpPressed && p.rocketDelay>0 && (p.rocketTime<resourceBefore || p.empressBrooch))resourceConsumed=true;
            if(family==SmartFallFamily.Carpet && actionResourceBit==64 && p.carpetFrame>=0 && (resourceHadCarpetStart?!p.canCarpet && p.carpetTime>0:p.carpetTime<resourceBefore))resourceConsumed=true;
            if (resourceConsumed && (family == SmartFallFamily.RocketBoots && p.rocketDelay > 0 && (p.fallStart != originalFallStart || p.velocity.Y * p.gravDir < 0) || family == SmartFallFamily.Carpet && p.carpetFrame >= 0 && p.fallStart != originalFallStart))
                Protected(p, true);
        }
        private static int JumpAvailable(Player p,Item item)
        { return SmartFallCapabilities.UnusedJumps(p) & SmartFallCapabilities.JumpFamilies(item); }
        private static int JumpPriority(int bits)
        {
            // Native JumpMovement consumes mount jumps first, then these five
            // accessory families. No allocation in the per-Update boundary.
            if((bits&256)!=0)return 256;if((bits&128)!=0)return 128;if((bits&64)!=0)return 64;if((bits&32)!=0)return 32;
            if((bits&2)!=0)return 2;if((bits&4)!=0)return 4;if((bits&8)!=0)return 8;if((bits&16)!=0)return 16;return bits&1;
        }
        private bool PrepareResource(Player p)
        {
            if(actionResourceBit!=0)return true;
            if(p.jump>0 || p.velocity.Y*p.gravDir<=0 || p.mount.Active && p.mount.BlockExtraJumps)return false;
            if(family==SmartFallFamily.DoubleJump)
            {
                int ready=SmartFallCapabilities.UnusedJumps(p);
                if(ready==0)return false;
                actionResourceBit=JumpPriority(ready);return true;
            }
            if(family==SmartFallFamily.RocketBoots)
            {
                if(p.mount.Active || !p.CanUseBootFlyingAbilities || p.rocketBoots<=0 || p.rocketDelay!=0 || p.wingTime>0 && p.wingsLogic!=0)return false;
                // Empress refill happens in the real downstream consumer.
                if(p.rocketTime<=0 && !p.empressBrooch)return false;
                actionResourceBit=32;resourceBefore=p.rocketTime;return true;
            }
            if(family==SmartFallFamily.Carpet)
            {
                if(!p.carpet || p.mount.Active || p.grappling[0]!=-1 || MovementJumpHost.ExtraJump(p) || p.rocketTime!=0 || p.wingTime!=0 || p.gravDir<0)return false;
                if(!p.canCarpet && p.carpetTime<=0)return false;
                actionResourceBit=64;resourceBefore=p.carpetTime;resourceHadCarpetStart=p.canCarpet;return true;
            }
            return false;
        }
        internal void BeforeGrapple(Player p)
        {
            if (!Current(p)) return;
            ObserveMotion(p); ReturnInputs(p);
            if (ownedMount >= 0 && family == SmartFallFamily.FlyingMount && p.mount.Active && p.mount.Type == ownedMount &&
                p.fallStart != originalFallStart && !SmartFallCapabilities.Risk(p))
            {
                // Hover has acted; dismount before native fallThrough flags.
                // Waiting until Update's finalizer would already miss a platform.
                TryReturnMount(p);
                if (!p.mount.Active) { ownedMount = -1; mountReturnAttempted = false; Protected(p, true); }
            }
            if (!action || family != SmartFallFamily.Grapple || shot || !permitted || !session.CanAct(p) || !(SmartFallCapabilities.Risk(p) || SmartFallCapabilities.ContactRisk(p, contact))) return;
            if (!ReferenceEquals(p.QuickGrapple_GetItemToUse(), selected)) { Fail(p); return; }
            int mouseX = Main.mouseX, mouseY = Main.mouseY;
            try
            {
                Main.mouseX = (int)(grapplePlan.Target.X - Main.screenPosition.X);
                Main.mouseY = p.gravDir > 0 ? (int)(grapplePlan.Target.Y - Main.screenPosition.Y) : (int)(Main.screenPosition.Y + Main.screenHeight - grapplePlan.Target.Y);
                blockingCount = 0; SmartFallGrappleHooks.Launching = this;
                long invocation = borrow.BeginNativeUse(p, generation, selected, family);
                try { p.QuickGrapple(); Count(ref NativeActions); }
                finally { borrow.EndNativeUse(invocation); }
            }
            finally { SmartFallGrappleHooks.Launching = null; Main.mouseX = mouseX; Main.mouseY = mouseY; }
            if (shot) Result = SmartFallResult.Consumed; else Fail(p);
        }
        internal void RecordGrapple(Player p, int index)
        {
            if (!Current(p) || !action || family != SmartFallFamily.Grapple || index < 0 || index >= Main.maxProjectiles) return;
            var projectile = Main.projectile[index];
            if (projectile == null || !projectile.active || projectile.owner != p.whoAmI || projectile.aiStyle != 7) return;
            shotKey = projectile.key; shot = true;
        }
        internal void RecordExistingHook(Projectile projectile, Player p)
        {
            if (!Current(p) || !action || family != SmartFallFamily.Grapple || projectile == null || !projectile.active || projectile.owner != p.whoAmI || projectile.aiStyle != 7) return;
            // Observed inside the original capacity loop, never another 1000
            // scan. Native maximum hook capacity is below this finite bound.
            if (blockingCount == blockingHooks.Length) return;
            blockingHooks[blockingCount] = projectile.key; blockingAttached[blockingCount++] = projectile.ai[0] == 2;
        }
        private bool BlockingHookChanged(Player p)
        {
            for (int i = 0; i < blockingCount; i++)
            {
                Projectile existing;
                if (!blockingHooks[i].TryGetActive(out existing) || existing.owner != p.whoAmI || existing.aiStyle != 7 || (existing.ai[0] == 2) != blockingAttached[i]) return true;
            }
            return false;
        }
        internal void AfterGrapple(Player p)
        {
            if (!Current(p) || !action || family != SmartFallFamily.Grapple || !shot) return;
            Projectile projectile;
            if (!shotKey.TryGetActive(out projectile) || projectile.owner != p.whoAmI) { Fail(p); return; }
            if (projectile.ai[0] == 2 && p.grapCount > 0 && p.statLife >= life && p.fallStart == (int)(p.position.Y / 16))
                Protected(p, true); // real GrappleMovement, never ai2 alone
        }
        private void Continue(Player p)
        {
            if (p.dead || p.ghost) { returnGravity = false; ownedMount = originalMount = -1; action = false; borrow.Restore(p, generation); return; }
            // A real Up edge owns gravity even before our first legal Press.
            // Do not adopt the player's native flip as our Consumed result and
            // schedule a later reverse after that player releases the key.
            if (family==SmartFallFamily.GravityGlobe && (action || returnGravity) &&
                (p.controlUp && !upOwned || p.forcedGravity>0))
            { ReturnInputs(p);returnGravity=action=false;takenOver=true;Result=SmartFallResult.TakenOver;borrow.Restore(p,generation);return; }
            if (returnGravity)
            {
                if (p.gravDir == originalGravity) { returnGravity = false; Protected(p, true); }
                else if (!permitted || !session.CanAct(p) || p.forcedGravity > 0 || !p.gravControl2)
                { returnGravity=false;action=false;takenOver=true;Result=SmartFallResult.TakenOver;borrow.Restore(p,generation); }
                else LendUp(p);
                return;
            }
            if (ownedMount >= 0 && (!p.mount.Active || p.mount.Type != ownedMount))
            { ownedMount = originalMount = -1; action = false; takenOver = true; Result = SmartFallResult.TakenOver; borrow.Restore(p, generation); return; }
            if (!action)
            {
                if (ownedMount >= 0)
                {
                    if (!TryReturnMount(p)) return;
                    ownedMount = -1; mountReturnAttempted = false;
                }
                borrow.Restore(p, generation); ReturnOriginalMount(p); return;
            }
            if (family == SmartFallFamily.TeleportRod && !permitted && Result == SmartFallResult.Prepared)
            { action = false; targetActive = false; borrow.Restore(p, generation); Result = SmartFallResult.TakenOver; takenOver = true; return; }
            if (landed) return; // Hurt precedes AfterEquips, but follows Capture.
            if (family == SmartFallFamily.Grapple)
            {
                Projectile projectile;
                if (!shot || !shotKey.TryGetActive(out projectile) || projectile.ai[0] == 1 || p.statLife < life) Fail(p);
                return;
            }
            if (!session.Value.SmartFallProtection.Enabled || !permitted)
            {
                // An unconsumed Release/Press plan owns no protection yet.
                // Cancellation withdraws input and returns its real equipment;
                // native resource history is never rolled back.
                if (!resourceConsumed && (family==SmartFallFamily.DoubleJump || family==SmartFallFamily.RocketBoots || family==SmartFallFamily.Carpet))
                { Fail(p);return; }
                // Passive/held protection remains until native landing or a
                // renewed fall baseline makes removal safe. Closing a menu
                // cannot strip the only protection immediately before damage.
                // Umbrella's freshly reset *current* baseline alone cannot
                // justify dropping it after the predicted floor disappeared;
                // its real held consumer separately proves the remaining
                // landing distance safe before returning that selection.
                if (ownedMount < 0 && family != SmartFallFamily.Umbrella && !SmartFallCapabilities.Risk(p) && !p.noFallDmg && !p.hasWings && !p.slowFall) Protected(p, true);
            }
        }
        private void LendUp(Player p)
        {
            // The first press is followed by at least one whole native Up=false
            // update. UpdateControlHolds, never this owner, rearms releaseUp.
            if (!permitted || !session.CanAct(p) || p.forcedGravity>0 || tick<=upPressTick+1 || !p.releaseUp) return;
            if (!p.controlUp) { p.controlUp = true; upOwned = true; upPressTick=tick; Count(ref NativeActions); }
        }
        private void ReturnInputs(Player p)
        {
            if (jumpOwned && p.controlJump) p.controlJump = false;
            if (upOwned && p.controlUp) p.controlUp = false;
            jumpOwned = upOwned = false;
        }
        private void Protected(Player p, bool immediate)
        {
            Result = SmartFallResult.Protected; Count(ref Protections); action = false; shot = false; targetActive = false; active = false; blockingCount = 0; failedShot = grappleDependenciesValid = false;
            ReturnInputs(p);
            if (ownedMount >= 0 && p.mount.Active && p.mount.Type == ownedMount)
            {
                if (!immediate || !SmartFallCapabilities.Risk(p)) TryReturnMount(p);
                if (p.mount.Active) { Result = SmartFallResult.Returning; return; }
                ownedMount = -1; mountReturnAttempted = false;
            }
            borrow.Restore(p, generation);
            ReturnOriginalMount(p);
        }
        private void ReturnOriginalMount(Player p)
        {
            if (originalMount < 0) return;
            // The rescue mount has already retired. A newly active mount is
            // a native/manual successor, so the old return permission ends
            // here rather than resurfacing after the player dismounts it.
            if (p.mount.Active)
            { originalMount = -1; mountReturnAttempted = false; takenOver = true; Result = SmartFallResult.TakenOver; return; }
            if (borrow.HasPending) return;
            int expected=originalMount;Item item=originalMountItem;
            // One finite reactivation attempt after the exact item return. A
            // native denial ends this intention; motion is never a retry clock.
            originalMount=-1;originalMountItem=null;
            if (!permitted || !session.CanAct(p) || p.CCed || p.tongued || p.dead || p.noItems || p.gravDir<0 ||
                p.selectedItem!=mountSelection || !ReferenceEquals(p.HeldItem,originalSelectionItem)) return;
            if (item!=null && !MountID.Sets.Cart[expected] && item.mountType==expected && ReferenceEquals(p.QuickMount_GetItemToUse(),item))
            { p.QuickMount(); Count(ref NativeActions); }
            // Cart restoration uses QuickMount's native QuickMinecart fallback;
            // it alone decides whether a reachable rail can restore a cart.
            else if (MountID.Sets.Cart[expected] && ReferenceEquals(p.miscEquips[2],item) &&
                (item.IsAir ? expected==13 : item.mountType==expected) && p.QuickMount_GetItemToUse()==null)
            { p.QuickMount(); Count(ref NativeActions); }
            if (!p.mount.Active || p.mount.Type!=expected) Result=SmartFallResult.Failed;
        }
        private bool MountReturnChanged(Player p)
        {
            long revision = items == null ? 0 : items.World.Revision;
            if (mountReturnAttempted && p.position == lastMountReturnPosition && revision == lastMountReturnRevision) return false;
            mountReturnAttempted = true; lastMountReturnPosition = p.position; lastMountReturnRevision = revision; return true;
        }
        private bool TryReturnMount(Player p)
        {
            if (!MountReturnChanged(p)) return false;
            Count(ref NativeActions); return p.mount.TryDismount(p);
        }
        private void Fail(Player p)
        { if (family == SmartFallFamily.Grapple && shot) failedShot = true; rejected |= 1 << (int)family; action = false; shot = false; targetActive = false; ReturnInputs(p); Result = SmartFallResult.Failed; borrow.Restore(p, generation); blockerStamp = borrow.BlockerStamp(p); }
        internal void Pick(Player p, ref int slot, ref bool result) { borrow.Pick(p, ref slot, ref result); }
        internal void BeforeSync(Player p)
        {
            if (!Current(p)) return;
            bool before = borrow.HasPending; borrow.BeforeSync(p, action && family == SmartFallFamily.TeleportRod && permitted);
            if (before && !borrow.HasPending && borrow.ReturnResult == SmartFallResult.TakenOver)
            { ReturnInputs(p); action = targetActive = false; takenOver = true; Result = SmartFallResult.TakenOver; }
        }
        internal void ManualSelection(Player p)
        { if (!ReferenceEquals(p, retained) || items != null && items.ReturningSelection) return; originalMount=-1;originalMountItem=null;borrow.ManualSelection(); if (action && (family == SmartFallFamily.Umbrella || family == SmartFallFamily.TeleportRod)) { action = false; takenOver = true; Result = SmartFallResult.TakenOver; } }
        internal void ManualEquipment(Item[] array, int slot)
        {
            if (originalMount>=0 && retained!=null && array!=null && slot>=0 && slot<array.Length &&
                (ReferenceEquals(array[slot],originalMountItem) || ReferenceEquals(array,retained.inventory) && slot==mountSelection))
            { originalMount=-1;originalMountItem=null; }
            bool before = borrow.HasPending; borrow.ManualEquipment(array, slot);
            if (before && !borrow.HasPending)
            { if (player != null) ReturnInputs(player); action = returnGravity = false; ownedMount = originalMount = -1; takenOver = true; Result = SmartFallResult.TakenOver; }
        }
        internal void ManualLoadout(Player p)
        {
            if (p == null || !ReferenceEquals(p, retained) || !ReferenceEquals(p, Main.LocalPlayer) || Main.dedServ || Main.netMode == 2 || Main.gameMenu || items != null && System.Threading.Thread.CurrentThread.ManagedThreadId != items.ThreadId) return;
            // Successful native loadout swaps replace elements in the SAME
            // armor array. This real event invalidates cached source refs even
            // when no lease exists and ability readiness bits happen to match.
            items?.World.InvalidateObservation();
            originalMount=-1;originalMountItem=null;
            bool before = borrow.HasPending; borrow.ManualLoadout(p);
            if (before && !borrow.HasPending)
            { if (player != null) ReturnInputs(player); action = returnGravity = false; ownedMount = originalMount = -1; takenOver = true; Result = SmartFallResult.TakenOver; }
        }
        private void BeforeManualSlot(Item[] array, int slot) { previousManualSlot?.Invoke(array, slot); ManualEquipment(array, slot); }
        private bool ProtectItem(Item item) { return borrow.Protect(item) || (previousProtection?.Invoke(item) ?? false); }
        internal bool AllowsUse(int slot) { return borrow.AllowsUse(slot); }
        private bool AllowsItemUse(int slot) { return borrow.AllowsUse(slot) || (previousUse?.Invoke(slot) ?? false); }
        internal bool BeginItemCheck(Player p, out long operation)
        {
            operation = borrow.Operation;
            if (!Current(p)) return false;
            if (action && family == SmartFallFamily.TeleportRod && (!permitted || !session.CanAct(p)))
            { action = false; targetActive = false; borrow.Restore(p, generation); Result = SmartFallResult.TakenOver; takenOver = true; return false; }
            targetActive = action && family == SmartFallFamily.TeleportRod && borrow.SelectionReady && permitted && session.CanAct(p);
            return borrow.BeginItemCheck(p);
        }
        internal void EndItemCheck(Player p, long operation, bool entered, Exception error)
        {
            if (!Current(p)) return;
            try
            {
                borrow.EndItemCheck(p, operation, entered, error);
                if (error != null) { Fail(p); return; }
                if (targetActive && Vector2.DistanceSquared(p.position, actionPosition) > 16 && Vector2.DistanceSquared(p.position, teleportTarget) < 64) Protected(p, true);
                if (action && family == SmartFallFamily.Umbrella && borrow.SelectionReady && p.fallStart != originalFallStart && p.velocity.Y * p.gravDir <= 2.01f)
                {
                    Result = SmartFallResult.Consumed;
                    var remaining = SmartFallGeometry.Contact(p, ref TileReads);
                    if (remaining.Found && ((int)(remaining.Y / 16) - p.fallStart) * p.gravDir <= 25 + p.extraFall)
                        Protected(p, true);
                }
            }
            finally { targetActive = false; }
        }
        internal bool TryTeleportTarget(Player p, Item item, ref Vector2 target)
        {
            if (!Current(p) || !targetActive || !ReferenceEquals(item, selected) || !session.CanAct(p)) return false;
            target = teleportTarget; return true;
        }
        internal void EndUpdate(Player p)
        { EndUpdateAfterNative(p,null); }
        internal void EndUpdateAfterNative(Player p,Exception error)
        {
            if (!Current(p)) return;
            try
            {
                // A thrown native consumer did not complete a Release update.
                // Retire its finite intent and request exact return before the
                // scope is cleared; native resource history is never rewound.
                if (error!=null && HasPending)
                { returnGravity=false;originalMount=-1;Fail(p); }
                ObserveMotion(p); ReturnInputs(p);
                if(action && (family==SmartFallFamily.DoubleJump || family==SmartFallFamily.RocketBoots || family==SmartFallFamily.Carpet) && !resourceConsumed && releaseTick!=tick && jumpPressed)Fail(p);
                if (action && p.velocity.Y == 0) landed = true;
                borrow.EndUpdate(p, generation);
            }
            finally { scope = permitted = false; player = null; targetActive = false; }
        }
        internal void ClearSession()
        { ClearState(); }
        private void ClearState()
        {
            if (player != null) ReturnInputs(player);
            borrow.EndSession(); player = retained = null; scope = permitted = active = action = landed = shot = returnGravity = takenOver = false;
            actionResourceBit=0;resourceConsumed=jumpReleasing=jumpPressed=false;releaseTick=upPressTick=-1;originalMountItem=originalSelectionItem=null;
            ownedMount = originalMount = -1; sourceRevision = -1; mask = -1; count = sourceCount = hookCount = blockingCount = 0; blockerStamp = 0; rejected = 0; bandValid = grappleSearched = failedShot = grappleDependenciesValid = false; contact = default(SmartFallContact); targetActive = false;
            Array.Clear(sources, 0, sources.Length);
            Array.Clear(observedSources, 0, observedSources.Length);
            Array.Clear(hookGeometry, 0, hookGeometry.Length);
        }
        internal void Uninstall()
        {
            ClearSession(); SmartFallGrappleHooks.Uninstall();
            if (items != null) { items.World.BeforeManualFallSlot = previousManualSlot; items.World.AdditionalProtection = previousProtection; items.AllowsOwnedUse = previousUse; items.World.AllowsOwnedFallNativeUse = previousNativeUse; }
        }
        // Development-only bounded work counters. Release call sites are
        // omitted; ordinary packages have no recorder, log, or per-tile count.
        [Conditional("DEBUG")] private static void Count(ref long value) { value++; }
    }
}
