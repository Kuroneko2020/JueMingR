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
    // Observations never write fallStart, movement, immunity or fuel. Native
    // consumers alone turn borrowed items/short input edges into protection.
    internal sealed class SmartFallHost
    {
        private readonly MovementSession session;
        private readonly HostItems items;
        private readonly SmartFallBorrowOwner borrow;
        private readonly SmartFallGrapple grapple = new SmartFallGrapple();
        private readonly SmartFallCandidate[] candidates = new SmartFallCandidate[12];
        private readonly Item[] sources = new Item[12];
        private readonly Item[] observedSources = new Item[12];
        private readonly Func<Item, bool> jumpSourceReady;
        private SmartFallProtectionOptions sampledOptions;
        private readonly Action<Item[], int> previousManualSlot;
        private readonly Func<Item, bool> previousProtection;
        private readonly Func<int, bool> previousUse;
        private Player player, retained;
        private long generation, retainedGeneration, tick, sourceRevision = -1, contactTick, sampledTick;
        private int mask = -1, count, life, ownedMount = -1, originalMount = -1, rejected;
        private Vector2 sampledPosition, sampledVelocity, actionPosition, teleportTarget;
        private bool sampledDown, active, action, landed, returnGravity, scope, permitted, targetActive, bandValid, grappleSearched, takenOver;
        private bool jumpOwned, releaseOwned, rocketOwned, upOwned, upReleaseOwned;
        private float originalGravity;
        private int originalFallStart;
        private SmartFallFamily family;
        private Item selected;
        private SmartFallContact contact;
        private SmartFallGrapplePlan grapplePlan;
        private ProjectileKey shotKey;
        private bool shot;
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
            jumpSourceReady = item => retained != null && SmartFallCapabilities.UnusedForItem(retained, item);
            borrow = new SmartFallBorrowOwner(items, currentGeneration ?? (() => generation), SmartFallCapabilities.Matches, fishing);
            if (items != null)
            {
                previousManualSlot = items.World.BeforeManualFallSlot;
                items.World.BeforeManualFallSlot = BeforeManualSlot;
                previousProtection = items.World.AdditionalProtection;
                items.World.AdditionalProtection = ProtectItem;
                previousUse = items.AllowsOwnedUse;
                items.AllowsOwnedUse = AllowsItemUse;
            }
            try { SmartFallCapabilities.Bind(); } catch { /* Mount data unavailable: other native families remain independent. */ }
            SmartFallGrappleHooks.Install();
        }
        internal void BeginUpdate(Player p, long epoch, long updateTick)
        {
            if (!ReferenceEquals(retained, p) || retainedGeneration != epoch)
            { ClearSession(); retained = p; retainedGeneration = epoch; }
            player = p; generation = epoch; tick = updateTick; scope = true; permitted = false;
        }
        private bool Current(Player p) { return scope && ReferenceEquals(player, p) && ReferenceEquals(retained, p) && generation == retainedGeneration; }
        internal void Capture(Player p, bool canAct)
        {
            if (!Current(p)) return;
            permitted = canAct;
            if (HasPending) { Continue(p); if (HasPending) return; }
            if (!canAct || !session.Value.SmartFallProtection.Enabled) return;
            if (takenOver) { if (SmartFallCapabilities.Risk(p)) return; takenOver = false; }
            if (!SmartFallCapabilities.Risk(p)) { active = false; bandValid = false; rejected = 0; contact = default(SmartFallContact); return; }
            int currentMask = SmartFallCapabilities.NativeMask(p);
            long revision = items == null ? 0 : items.World.Revision;
            if (!active || sourceRevision != revision || mask != currentMask || !ReferenceEquals(sampledOptions, session.Value.SmartFallProtection))
            { Refresh(p, revision, currentMask); active = true; }
            if (count == 0) return;
            // A retained contact is consumed only while its input and simple
            // local geometry remain current; otherwise query the finite band.
            bool stale = !bandValid || p.controlDown != sampledDown || p.gravDir != originalGravity ||
                Vector2.DistanceSquared(p.position, sampledPosition) > 128 * 128 ||
                Math.Abs(p.velocity.X - sampledVelocity.X) > .1f || Math.Abs(p.position.X - (sampledPosition.X + sampledVelocity.X * (tick - sampledTick))) > 8 ||
                Math.Abs(p.velocity.Y * p.gravDir - Math.Min(p.maxFallSpeed, sampledVelocity.Y * p.gravDir + p.gravity * (tick - sampledTick))) > .5f;
            if (!stale && contact.Found)
            {
                var tile = SmartFallGeometry.Read(contact.TileX, contact.TileY, ref TileReads);
                stale = tile == null || !tile.nactive() || tile.slope() != contact.Slope || tile.type != contact.Type || tile.halfBrick() != contact.HalfBrick || tile.frameY != contact.FrameY;
            }
            if (stale)
            {
                Count(ref Plans); contact = SmartFallGeometry.Contact(p, ref TileReads); contactTick = sampledTick = tick; bandValid = true; grappleSearched = false;
                sampledPosition = p.position; sampledVelocity = p.velocity; sampledDown = p.controlDown; originalGravity = p.gravDir;
            }
            else if (contact.Found)
            {
                contact.Ticks = Math.Max(0, contact.Ticks - (tick - contactTick)); contactTick = tick;
            }
            if (!contact.Found || contact.SafeSurface) return;
            BuildCandidates(p);
            int choice = SmartFallDecision.Choose(candidates, count, contact.Ticks);
            if (choice < 0 || !Due(p, candidates[choice])) return;
            if (!session.CanAct(p) || !SmartFallCapabilities.Risk(p)) return;
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
            life = p.statLife; originalFallStart = p.fallStart; actionPosition = p.position;
            originalGravity = p.gravDir; originalMount = p.mount.Active ? p.mount.Type : -1;
            mountReturnAttempted = false;
            Result = SmartFallResult.Prepared;
            if (family == SmartFallFamily.GravityGlobe) LendUp(p);
            if (family == SmartFallFamily.FlyingMount || family == SmartFallFamily.SafeMount)
            {
                if (p.mount.Active && originalMount != item.mountType) { p.QuickMount(); Count(ref NativeActions); }
                if (!p.mount.Active && item != null) { p.QuickMount(); Count(ref NativeActions); }
                if (p.mount.Active && item != null && p.mount.Type == item.mountType) { ownedMount = p.mount.Type; Result = SmartFallResult.Consumed; }
                else Fail(p);
            }
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
            Count(ref SourceScans); sourceRevision = revision; int previousMask = mask; mask = nativeMask; count = 0; grappleSearched = false;
            var options = session.Value.SmartFallProtection;
            sampledOptions = options;
            for (int i = 0; i < 12; i++)
            {
                Item next = options.Allows(i) ? i == (int)SmartFallFamily.DoubleJump ? borrow.GetCandidate(p, (SmartFallFamily)i, jumpSourceReady) : borrow.GetCandidate(p, (SmartFallFamily)i) : null;
                // Our own failed action's restore legitimately increments the
                // inventory revision. It is not a new capability and cannot
                // reopen the identical failed transaction every Update.
                if (!ReferenceEquals(observedSources[i], next) || ((previousMask ^ nativeMask) & 1 << i) != 0 || i == (int)SmartFallFamily.DoubleJump && ((previousMask ^ nativeMask) & (511 << 12)) != 0) rejected &= ~(1 << i);
                sources[i] = observedSources[i] = next;
                // Native ready abilities may belong to a mount rather than an
                // Item endpoint. They still go through their real consumers.
                if (options.Allows(i) && (sources[i] != null || (nativeMask & 1 << i) != 0)) count++;
            }
        }
        private void BuildCandidates(Player p)
        {
            count = 0; int bestReady = int.MaxValue;
            var options = session.Value.SmartFallProtection;
            for (int i = 0; i < 12; i++)
            {
                if (!options.Allows(i) || (rejected & 1 << i) != 0) continue;
                var f = (SmartFallFamily)i; Item item = sources[i];
                bool native = (mask & 1 << i) != 0, ready = false; int delay = 0, cost = 0;
                if (item == null && !native) continue;
                switch (f)
                {
                    case SmartFallFamily.Horseshoe: ready = item != null && !p.stoned; cost = 10; break;
                    case SmartFallFamily.Wings: ready = item != null && !p.stoned; cost = 12; break;
                    case SmartFallFamily.DjinnLegs: ready = item != null && !p.stoned; delay = 1; cost = 14; break;
                    case SmartFallFamily.DoubleJump: ready = (SmartFallCapabilities.UnusedForItem(p,item) || SmartFallCapabilities.MountJump(p)) && p.grapCount == 0 && (!p.mount.Active || !p.mount.BlockExtraJumps); cost = item == null ? 20 : 25; break;
                    case SmartFallFamily.RocketBoots:
                        ready = p.rocketTime > 0 && p.CanUseBootFlyingAbilities && p.grapCount == 0 && !p.mount.Active;
                        // Delay is not first ignition: native rocketDelay
                        // resets the baseline only in the NEXT early Update.
                        float first = p.velocity.Y * p.gravDir - .1f;
                        if (first > 0) first -= .5f; else if (first > -Player.jumpSpeed * .5f) first -= .1f;
                        delay = first + Math.Abs(p.gravity) <= 0 ? 0 : 1; cost = 26; break;
                    case SmartFallFamily.Carpet: ready = p.gravDir > 0 && (p.canCarpet || p.carpetTime > 0) && !MovementJumpHost.ExtraJump(p) && p.rocketTime == 0 && p.wingTime == 0 && !p.mount.Active && p.grapCount == 0; cost = 28; break;
                    case SmartFallFamily.Umbrella: ready = item != null && !p.controlDown && p.itemAnimation == 0 && !p.pulley; delay = 1; cost = 32; break;
                    case SmartFallFamily.Grapple:
                        if (bestReady > 40 && item != null && SmartFallGrappleHooks.Available && !p.controlHook && p.grapCount == 0)
                        {
                            if (!grappleSearched)
                            {
                            grapplePlan = grapple.Find(p, item, contact, ref TileReads);
                            // Real selection is part of the plan; a faster
                            // carried hook can supersede the first native hook.
                            if (items != null) for (int slot = 0; slot < 50; slot++)
                            {
                                Item other = p.inventory[slot];
                                if (ReferenceEquals(other, item) || !SmartFallCapabilities.Matches(other, f) || other.shootSpeed <= item.shootSpeed) continue;
                                var alternative = grapple.Find(p, other, contact, ref TileReads);
                                if (alternative.Found && (!grapplePlan.Found || alternative.Delay < grapplePlan.Delay)) grapplePlan = alternative;
                            }
                            grappleSearched = true;
                            }
                            else if (grapplePlan.Found)
                            {
                                int now = grapple.FirstProtection(p, grapplePlan.Item, grapplePlan.Target, ref TileReads);
                                grapplePlan.Delay = now; grapplePlan.Found = now >= 0;
                            }
                            ready = grapplePlan.Found; delay = grapplePlan.Delay; if (ready) sources[i] = grapplePlan.Item;
                        }
                        cost = 40; break;
                    case SmartFallFamily.SafeMount:
                        // Native CanMount tests the proposed box at the next
                        // velocity offset. Its legal summon window closes one
                        // phase before a short final gap, even though its fall
                        // immunity itself needs no Hover consumer.
                        ready = CanMount(p, item); delay = 1; cost = 46; break;
                    case SmartFallFamily.FlyingMount:
                        // New mounts enter with frameState=0; PlayerFrame
                        // establishes the airborne frame after collision. The
                        // first timely Hover can therefore be next Update.
                        ready = CanMount(p, item) && (!contact.Platform || !BypassesPlatform(item.mountType) || SmartFallCapabilities.HoverMount(item.mountType)); delay = 1; cost = 50; break;
                    case SmartFallFamily.GravityGlobe:
                        ready = bestReady > 60 && item != null && !p.mount.Active && !p.controlUp && p.forcedGravity <= 0 && OppositeClear(p); cost = 60; break;
                    case SmartFallFamily.TeleportRod:
                        ready = bestReady > 70 && item != null && p.itemAnimation == 0 && p.itemTime == 0 && !p.noItems &&
                            (item.type != 1326 || !p.chaosState || p.statLife > p.statLifeMax2 / 7) && TeleportPoint(p);
                        delay = 1; cost = item != null && item.type == 5335 ? 70 : 90; break;
                }
                candidates[count++] = new SmartFallCandidate { Family = f, Ready = ready, Delay = delay, Interference = cost };
                if (ready && contact.Ticks > delay) bestReady = Math.Min(bestReady, cost);
            }
        }
        private static bool CanMount(Player p, Item item)
        { return item != null && p.gravDir > 0 && (!p.mount.Active || p.mount.CanDismount(p)) && p.mount.CanMount(item.mountType, p); }
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
            if (!jump || p.grapCount > 0 || !session.JumpHost.ReserveFallProtection(p)) return;
            if (!p.controlJump) { p.controlJump = true; jumpOwned = true; }
            if (!p.releaseJump) { p.releaseJump = true; releaseOwned = true; }
            if (family == SmartFallFamily.RocketBoots && !p.rocketRelease) { p.rocketRelease = true; rocketOwned = true; }
            Count(ref NativeActions); Result = SmartFallResult.Consumed;
        }
        internal void AfterJump(Player p)
        {
            if (!Current(p) || !action) return;
            if (family == SmartFallFamily.GravityGlobe && p.gravDir != originalGravity)
            { Result = SmartFallResult.Consumed; returnGravity = true; }
            else if (family == SmartFallFamily.GravityGlobe && returnGravity && p.gravDir == originalGravity)
            { returnGravity = false; Protected(p, true); }
            if (family == SmartFallFamily.DoubleJump && p.velocity.Y * p.gravDir < 0 && p.jump > 0) Protected(p, true);
        }
        internal void AfterFlight(Player p) { if (Current(p)) ObserveMotion(p); }
        private void ObserveMotion(Player p)
        {
            if (!action) return;
            if ((family == SmartFallFamily.RocketBoots && p.rocketDelay > 0 && (p.fallStart != originalFallStart || p.velocity.Y * p.gravDir < 0) || family == SmartFallFamily.Carpet && p.carpetFrame >= 0 && p.fallStart != originalFallStart))
                Protected(p, true);
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
            if (!action || family != SmartFallFamily.Grapple || shot || !permitted || !session.CanAct(p) || !SmartFallCapabilities.Risk(p)) return;
            if (!ReferenceEquals(p.QuickGrapple_GetItemToUse(), selected)) { Fail(p); return; }
            int mouseX = Main.mouseX, mouseY = Main.mouseY;
            try
            {
                Main.mouseX = (int)(grapplePlan.Target.X - Main.screenPosition.X);
                Main.mouseY = p.gravDir > 0 ? (int)(grapplePlan.Target.Y - Main.screenPosition.Y) : (int)(Main.screenPosition.Y + Main.screenHeight - grapplePlan.Target.Y);
                SmartFallGrappleHooks.Launching = this; p.QuickGrapple(); Count(ref NativeActions);
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
            if (returnGravity)
            {
                if (p.gravDir == originalGravity) { returnGravity = false; Protected(p, true); }
                else if (p.controlUp) { returnGravity = false; action = false; takenOver = true; Result = SmartFallResult.TakenOver; borrow.Restore(p, generation); }
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
            if (!p.controlUp) { p.controlUp = true; upOwned = true; }
            if (!p.releaseUp) { p.releaseUp = true; upReleaseOwned = true; }
        }
        private void ReturnInputs(Player p)
        {
            if (jumpOwned && p.controlJump) p.controlJump = false;
            if (releaseOwned && p.releaseJump) p.releaseJump = false;
            if (rocketOwned && p.rocketRelease) p.rocketRelease = false;
            if (upOwned && p.controlUp) p.controlUp = false;
            if (upReleaseOwned && p.releaseUp) p.releaseUp = false;
            jumpOwned = releaseOwned = rocketOwned = upOwned = upReleaseOwned = false;
        }
        private void Protected(Player p, bool immediate)
        {
            Result = SmartFallResult.Protected; Count(ref Protections); action = false; shot = false; targetActive = false; active = false;
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
            if (borrow.HasPending || !MountReturnChanged(p)) return;
            if (p.mount.CanMount(originalMount, p))
            { p.mount.SetMount(originalMount, p); originalMount = -1; mountReturnAttempted = false; Count(ref NativeActions); }
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
        { rejected |= 1 << (int)family; action = false; shot = false; targetActive = false; ReturnInputs(p); Result = SmartFallResult.Failed; borrow.Restore(p, generation); }
        internal void Pick(Player p, ref int slot, ref bool result) { borrow.Pick(p, ref slot, ref result); }
        internal void BeforeSync(Player p)
        {
            if (!Current(p)) return;
            bool before = borrow.HasPending; borrow.BeforeSync(p, action && family == SmartFallFamily.TeleportRod && permitted);
            if (before && !borrow.HasPending && borrow.ReturnResult == SmartFallResult.TakenOver)
            { ReturnInputs(p); action = targetActive = false; takenOver = true; Result = SmartFallResult.TakenOver; }
        }
        internal void ManualSelection(Player p)
        { if (!ReferenceEquals(p, retained) || items != null && items.ReturningSelection) return; borrow.ManualSelection(); if (action && (family == SmartFallFamily.Umbrella || family == SmartFallFamily.TeleportRod)) { action = false; takenOver = true; Result = SmartFallResult.TakenOver; } }
        internal void ManualEquipment(Item[] array, int slot)
        {
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
        {
            if (!Current(p)) return;
            try
            {
                ObserveMotion(p); ReturnInputs(p);
                if (action && p.velocity.Y == 0) landed = true;
                borrow.EndUpdate(p, generation);
            }
            finally { scope = permitted = false; player = null; targetActive = false; }
        }
        internal void ClearSession()
        {
            if (player != null) ReturnInputs(player);
            borrow.EndSession(); player = retained = null; scope = permitted = active = action = landed = shot = returnGravity = takenOver = false;
            ownedMount = originalMount = -1; sourceRevision = -1; mask = -1; count = 0; rejected = 0; bandValid = grappleSearched = false; contact = default(SmartFallContact); targetActive = false;
            Array.Clear(sources, 0, sources.Length);
            Array.Clear(observedSources, 0, observedSources.Length);
        }
        internal void Uninstall()
        {
            ClearSession(); SmartFallGrappleHooks.Uninstall();
            if (items != null) { items.World.BeforeManualFallSlot = previousManualSlot; items.World.AdditionalProtection = previousProtection; items.AllowsOwnedUse = previousUse; }
        }
        // Development-only bounded work counters. Release call sites are
        // omitted; ordinary packages have no recorder, log, or per-tile count.
        [Conditional("DEBUG")] private static void Count(ref long value) { value++; }
    }
}
