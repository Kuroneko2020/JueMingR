using System;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Recovery;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal sealed class ItemOnce
        {
            internal RecoverySource Source;
            internal CompanionSupplyKind Kind;
            internal bool Portable;
            internal long Session;
            internal object World;
            internal uint Expires;
            internal int PreviousSelection;
        }

        internal bool CanUseItemOnce(Guid id, CompanionInventoryArea area, int slot, out string reason)
        {
            if (area != CompanionInventoryArea.Inventory)
            { reason = "请先将物品移到队友主背包。"; return false; }
            if (!ItemOwnerCurrent() || !actors.TryGetValue(id, out var actor) || !ItemActorCurrent(id, actor))
            { reason = "队友当前不能使用物品。"; return false; }
            if (itemDraft != null || Main.mouseItem?.IsAir == false)
            { reason = "请先放回手中的物品。"; return false; }
            if (actor.Supplies.Once != null || actor.Supplies.Growth.Found || actor.Supplies.Quick.Found)
            { reason = "队友正在使用物品。"; return false; }
            return ItemOnceEligible(actor, slot, false, out _, out reason);
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
            if (actor.Supplies.Once != null || actor.Supplies.Growth.Found || actor.Supplies.Quick.Found)
            { reason = "队友正在使用物品。"; return false; }
            if (!ItemOnceEligible(actor, slot, portable, out var kind, out reason)) return false;
            actor.Supplies.Once = new ItemOnce { Source = SupplySource(actor, actor.Body.inventory, 0, slot),
                Kind = kind, Portable = portable, Session = session, World = world,
                Expires = Main.GameUpdateCount + 180, PreviousSelection = actor.Body.selectedItem };
            reason = portable ? "已请求队友召出储存入口。" : "已请求队友使用一次。";
            return true;
        }

        private bool ItemOnceEligible(Actor actor, int slot, bool portable, out CompanionSupplyKind kind, out string reason)
        {
            var body = actor.Body;
            kind = CompanionSupplyKind.Growth;
            reason = null;
            if (!SupplyActorReady(actor) || body.ghost || body.spectating >= 0 || body.noItems || body.CCed || body.cursed)
            { reason = "队友当前无法使用物品。"; return false; }
            if (slot < 0 || slot >= 50 || body.inventory[slot]?.IsAir != false)
            { reason = "请选择队友主背包中的物品。"; return false; }
            if (!SupplyAdmitted(actor, body.inventory, 0, slot))
            { reason = "该物品正在使用或等待同步。"; return false; }
            var item = body.inventory[slot];
            if (portable) return item.type == ItemID.MoneyTrough || item.type == ItemID.VoidLens || item.type == ItemID.ClosedVoidBag;
            if (CompanionSupplyCatalog.Candidate(CompanionSupplyKind.Growth, item))
            {
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
            reason = "该物品不支持使用一次。";
            return false;
        }

        // One pending command belongs to this body's activation and exact
        // source. The hand scheduler takes it once, after the native release.
        internal bool RunCompanionItemOnce(Actor actor)
        {
            var state = actor.Supplies;
            if (state.RestoreSelection >= 0 && FreeHand(actor.Body))
            {
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
                !ItemOnceEligible(actor, command.Source.Slot, command.Portable, out var admittedKind, out reason))
            {
                state.Once = null;
                Feedback = reason ?? "物品或队友状态已变化，本次使用已取消。";
                return false;
            }
            if (admittedKind == CompanionSupplyKind.Growth) return true;
            state.Once = null;
            UseQuick(actor, admittedKind, command.Source, manual: true);
            return true;
        }

        internal void CancelCompanionItemOnce(Actor actor)
        {
            var state = actor.Supplies;
            if (state.Once != null) Feedback = "队友状态已变化，本次使用已取消。";
            state.Once = state.HeldOnce = null;
            state.RestoreSelection = -1;
        }
    }
}
