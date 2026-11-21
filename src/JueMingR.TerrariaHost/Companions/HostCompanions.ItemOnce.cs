using System;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Combat;
using JueMingR.TerrariaHost.Recovery;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal sealed class ItemOnce
        {
            internal RecoverySource Source;
            // A null kind is an ordinary held-item action. Quick-supply
            // policy and its allow lists do not define the manual catalogue.
            internal CompanionSupplyKind? Kind;
            internal bool Portable;
            internal long Session;
            internal object World;
            internal uint Expires;
            internal int PreviousSelection;
            internal byte Prefix;
        }

        internal bool CanUseItemOnce(Guid id, CompanionInventoryArea area, int slot, out string reason)
        {
            if (area != CompanionInventoryArea.Inventory)
            { reason = "请先将物品移到队友主背包。"; return false; }
            if (!ItemCommandOwnerCurrent() || !actors.TryGetValue(id, out var actor) || !ItemActorCurrent(id, actor))
            { reason = "队友当前不能使用物品。"; return false; }
            if (itemDraft != null || Main.mouseItem?.IsAir == false)
            { reason = "请先放回手中的物品。"; return false; }
            if (ManualHandBusy(actor))
            { reason = "队友正在使用物品。"; return false; }
            if (!ItemOnceEligible(actor, slot, false, out var kind, out reason)) return false;
            return kind.HasValue || ManualAmmoAvailable(actor, actor.Body.inventory[slot], out reason);
        }

        internal bool UseItemOnce(Guid id, CompanionInventoryArea area, int slot)
        {
            if (!CanUseItemOnce(id, area, slot, out string reason)) return Result(false, reason);
            return Result(TryQueueItemOnce(actors[id], slot, false, out reason), reason);
        }

        private bool TryQueueItemOnce(Actor actor, int slot, bool portable, out string reason)
        {
            if (itemDraft != null || Main.mouseItem?.IsAir == false)
            { reason = "请先放回手中的物品。"; return false; }
            if (ManualHandBusy(actor))
            { reason = "队友正在使用物品。"; return false; }
            if (!ItemOnceEligible(actor, slot, portable, out var kind, out reason)) return false;
            actor.Supplies.Once = new ItemOnce { Source = SupplySource(actor, actor.Body.inventory, 0, slot),
                Kind = kind, Portable = portable, Session = session, World = world,
                Expires = Main.GameUpdateCount + 180, PreviousSelection = actor.Body.selectedItem, Prefix = actor.Body.inventory[slot].prefix };
            reason = portable ? "已请求队友召出储存入口。" : "已请求队友使用一次。";
            return true;
        }

        private static bool ManualHandBusy(Actor actor)
        {
            var state = actor.Supplies;
            return state.Once != null || state.HeldSource.Found || state.Quick.Found || state.RestoreSelection >= 0;
        }

        private bool ItemOnceEligible(Actor actor, int slot, bool portable, out CompanionSupplyKind? kind, out string reason)
        {
            var body = actor.Body;
            kind = null;
            reason = null;
            bool canWaitForShapeExit = !portable && HandlessShape(body);
            if (!SupplyActorReady(actor) || body.ghost || body.spectating >= 0 || body.cursed ||
                !canWaitForShapeExit && (body.noItems || body.CCed))
            { reason = "队友当前无法使用物品。"; return false; }
            if (slot < 0 || slot >= 50 || body.inventory[slot]?.IsAir != false)
            { reason = "请选择队友主背包中的物品。"; return false; }
            if (!SupplyAdmitted(actor, body.inventory, 0, slot))
            { reason = "该物品正在使用或等待同步。"; return false; }
            var item = body.inventory[slot];
            if (portable)
            {
                if (IsPortableBankItem(item.type)) return true;
                reason = "储存道具已变化。";
                return false;
            }
            if (CompanionSupplyCatalog.Candidate(CompanionSupplyKind.Growth, item))
            {
                kind = CompanionSupplyKind.Growth;
                if (CompanionSupplyCatalog.GrowthEligible(body, item.type)) return true;
                reason = "队友已达到成长上限或尚未满足物品要求。";
                return false;
            }
            bool healing = CompanionSupplyCatalog.Candidate(CompanionSupplyKind.Life, item);
            bool restoringMana = CompanionSupplyCatalog.Candidate(CompanionSupplyKind.Mana, item);
            if (healing && body.statLife < body.statLifeMax2 && body.potionDelay <= 0)
            {
                kind = CompanionSupplyKind.Life;
                return true;
            }
            if (restoringMana && body.statMana < body.statManaMax2 && body.manaPotionDelay <= 0 &&
                (body.potionDelay <= 0 || !item.potion))
            {
                kind = CompanionSupplyKind.Mana;
                return true;
            }
            if (healing || restoringMana)
            {
                if (healing && body.statLife < body.statLifeMax2 || restoringMana && body.statMana < body.statManaMax2)
                    reason = "队友正在等待药水冷却。";
                else if (healing && restoringMana) reason = "队友生命和魔力已满。";
                else reason = healing ? "队友生命已满。" : "队友魔力已满。";
                return false;
            }
            if (CompanionSupplyCatalog.Candidate(CompanionSupplyKind.Buff, item))
            {
                kind = CompanionSupplyKind.Buff;
                if (!BuffRecovery.Missing(body, item.buffType)) reason = "队友已有该类增益。";
                else if (body.CountBuffs() >= Player.maxBuffs) reason = "队友的增益栏已满。";
                return reason == null;
            }
            if (NeedsManualWorldPoint(item))
            { reason = "这件物品需要指定世界中的地点。"; return false; }
            if (IsManualPlacement(item))
            { reason = "队友暂时不能放置物品。"; return false; }
            if (item.type == ItemID.GolfWhistle)
            { reason = "队友没有自己的高尔夫球可召回。"; return false; }
            if (item.useStyle <= 0 || item.useAnimation <= 0)
            { reason = "这件物品没有原版主动用法。"; return false; }
            return true;
        }

        private static bool IsPortableBankItem(int type)
        { return type == ItemID.MoneyTrough || type == ItemID.VoidLens || type == ItemID.ClosedVoidBag; }

        private static bool NeedsManualWorldPoint(Item item)
        {
            // Native mining, mowing, ordinary critter release and golf all
            // consume a world tile or ball. Never fabricate it from the
            // human cursor. The explosive bunny releases at the actor's feet.
            return item.type == ItemID.RodofDiscord || item.type == ItemID.RodOfHarmony || item.type == ItemID.PortalGun ||
                item.type == ItemID.ChestLock || item.chlorophyteExtractinatorConsumable ||
                item.shoot == ProjectileID.PalworldDigtoise || item.shoot == ProjectileID.PalworldTrustyDigtoise ||
                ItemID.Sets.ExtractinatorMode[item.type] >= 0 && item.createTile < 0 ||
                item.useAmmo == AmmoID.Solution || item.type == ItemID.Fertilizer || item.type == ItemID.SuperFertilizer ||
                item.type == ItemID.LawnMower || item.type == ItemID.GravediggerShovel || item.fishingPole > 0 ||
                item.pick > 0 || item.hammer > 0 || item.axe > 0 && !CompanionItemRules.WeaponEligible(item) && item.type != ItemID.AcornAxe ||
                item.shoot > 0 && (Main.projHook[item.shoot] || ProjectileID.Sets.IsAGolfBall[item.shoot]) ||
                item.useStyle == ItemUseStyleID.GolfPlay || item.makeNPC > 0 && item.makeNPC != NPCID.ExplosiveBunny ||
                item.type == ItemID.EmptyBucket || item.type == ItemID.SuperAbsorbantSponge || item.type == ItemID.LavaAbsorbantSponge ||
                item.type == ItemID.HoneyAbsorbantSponge || item.type == ItemID.UltraAbsorbantSponge || item.type == ItemID.WireCutter ||
                item.type == ItemID.ActuationRod || ItemID.Sets.IsPaintScraper[item.type] || item.type == ItemID.Paintbrush ||
                item.type == ItemID.SpectrePaintbrush || item.type == ItemID.PaintRoller || item.type == ItemID.SpectrePaintRoller;
        }

        private static bool IsManualPlacement(Item item)
        {
            // Kites and critters have a separate alternate tile placement.
            // This command issues only primary use. Wands, wire, buckets,
            // stationary sentries and terrain-producing shots also have
            // placement consumers outside the ordinary createTile branch.
            return item.createTile >= 0 && !ItemID.Sets.PlaceTileOnAltUse[item.type] || item.createWall >= 0 ||
                item.tileWand >= 0 || item.GetFlexibleTileWand() != null || item.sentry || ItemID.Sets.AlsoABuildingItem[item.type] ||
                item.type == ItemID.YellowWrench || item.type >= ItemID.StarTopper1 && item.type <= ItemID.BlueAndYellowLights ||
                PlacesWorldProjectile(item.shoot) ||
                item.type == ItemID.StaffofRegrowth || item.type == ItemID.AcornAxe;
        }

        private static bool PlacesWorldProjectile(int projectile)
        {
            if (projectile <= 0) return false;
            // .8 falling blocks lay their tile on death (including player mud
            // and sand ammunition). Rope, acorns, ice and fluid use separate
            // impact/AI consumers, often long after this hand has returned.
            return ContentSamples.ProjectilesByType.TryGetValue(projectile, out var sample) && sample.aiStyle == 10 ||
                projectile == ProjectileID.DirtBall || projectile == ProjectileID.IceBlock || projectile == ProjectileID.AcornSlingshotAcorn ||
                projectile == ProjectileID.MysticSnakeCoil || projectile == ProjectileID.RopeCoil || projectile == ProjectileID.VineRopeCoil ||
                projectile == ProjectileID.SilkRopeCoil || projectile == ProjectileID.WebRopeCoil || projectile == ProjectileID.FreezeBomb ||
                projectile == ProjectileID.DirtBomb || projectile == ProjectileID.DirtStickyBomb || projectile == ProjectileID.LavaBoulder ||
                projectile == ProjectileID.SpiderBoulder || projectile == ProjectileID.WebSpit && Main.getGoodWorld ||
                projectile >= ProjectileID.WetBomb && projectile <= ProjectileID.HoneyBomb ||
                projectile >= ProjectileID.WetRocket && projectile <= ProjectileID.HoneyMine ||
                projectile >= ProjectileID.WetSnowmanRocket && projectile <= ProjectileID.HoneySnowmanRocket;
        }

        private bool ManualAmmoAvailable(Actor actor, Item item, out string reason)
        {
            reason = null;
            if (item.useAmmo <= 0) return true;
            var body = actor.Body;
            var ammo = AttackAmmoSnapshot.Source(body, item);
            if (ammo == null || !AttackSourceAvailable(actor, Array.IndexOf(body.inventory, ammo)))
            { reason = "队友没有可用的弹药。"; return false; }
            // Match the actual launcher as well as the selected rocket. For
            // example Celebration Mk2 converts liquid ammo to its ordinary
            // celebration projectile; the Rocket Launcher really adds liquid.
            if (AmmoID.Sets.SpecificLauncherAmmoProjectileMatches.TryGetValue(item.type, out var choices) &&
                choices.TryGetValue(ammo.type, out int projectile) && PlacesWorldProjectile(projectile))
            { reason = "当前弹药会放置液体，队友暂时不能使用。"; return false; }
            return true;
        }

        private bool ManualHeldAim(Actor actor, ItemOnce command, out Vector2 aim, out string reason)
        {
            var body = actor.Body;
            var item = command.Source.Item;
            aim = body.Center + new Vector2(body.direction * 48, 0);
            if (!ManualAmmoAvailable(actor, item, out reason)) return false;
            if (IsPortableBankItem(item.type)) { aim = body.Center; return true; }
            if (!CompanionItemRules.WeaponEligible(item)) return true;
            // Only a submitted weapon command pays for a target sweep. Use
            // this actual weapon's opening, even with automatic combat off.
            NearbyTargets(actor);
            var state = actor.Combat;
            if (state.NearbyCount == 0) return true;
            var geometry = new CompanionShotGeometry(body, item);
            for (int i = 0; i < state.NearbyCount; i++)
            {
                var target = state.Nearby[i];
                var choice = new CompanionAttackChoice { Slot = command.Source.Slot, Weapon = item,
                    Type = item.type, Prefix = item.prefix, Target = target, TargetGeneration = target.generation };
                if (AttackLine(actor, choice, geometry, out aim, i)) return true;
            }
            reason = "队友当前没有合适的攻击位置。";
            return false;
        }

        // One pending command belongs to this body's activation and exact
        // source. The hand scheduler takes it once, after the native release.
        internal bool RunCompanionItemOnce(Actor actor)
        {
            var state = actor.Supplies;
            if (state.RestoreSelection >= 0)
            {
                // Native phones act halfway through the animation. Keep this
                // selection, release the button and prevent combat from
                // repeating an auto-reuse weapon before the native tail ends.
                if (!FreeHand(actor.Body)) return true;
                actor.Body.selectedItemState.Select(state.RestoreSelection);
                state.RestoreSelection = -1;
            }
            var command = state.Once;
            if (command == null) return false;
            if (unchecked((int)(Main.GameUpdateCount - command.Expires)) >= 0)
            {
                state.Once = null;
                Feedback = "队友一直忙于当前动作，本次使用已取消。";
                return false;
            }
            string reason = null;
            if (command.Session != session || !ReferenceEquals(command.World, world) || !SourceCurrent(actor, command.Source) ||
                command.Source.Item.prefix != command.Prefix ||
                !ItemOnceEligible(actor, command.Source.Slot, command.Portable, out var admittedKind, out reason))
            {
                state.Once = null;
                Feedback = reason ?? "物品或队友状态已变化，本次使用已取消。";
                return false;
            }
            // Keep the exact source until the movement scheduler makes room.
            // Even QuickHeal's native item check can refuse while a handless
            // shape cannot dismount, so the manual command is still pending.
            if (HandlessShape(actor.Body)) return true;
            if (!admittedKind.HasValue || admittedKind == CompanionSupplyKind.Growth) return true;
            state.Once = null;
            UseQuick(actor, admittedKind.Value, command.Source, manual: true);
            return true;
        }

        internal void CancelCompanionItemOnce(Actor actor)
        {
            var state = actor.Supplies;
            if (state.Once != null) Feedback = "队友状态已变化，本次使用已取消。";
            if (state.HeldOnce != null) state.HeldSource = default(RecoverySource);
            state.Once = state.HeldOnce = null;
            state.RestoreSelection = -1;
        }
    }
}
