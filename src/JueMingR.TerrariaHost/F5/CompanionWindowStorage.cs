using System;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Companions;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private const float ToolbarGap = 4;
        private static readonly CompanionInventoryArea[] StorageOrder =
        {
            CompanionInventoryArea.Inventory, CompanionInventoryArea.PiggyBank, CompanionInventoryArea.Safe,
            CompanionInventoryArea.DefenderForge, CompanionInventoryArea.VoidVault
        };
        private static readonly string[] StorageNames = { "背包", "猪猪存钱罐", "保险箱", "护卫熔炉", "虚空袋" };
        private static readonly string[] StorageLabels = Array.ConvertAll(StorageNames, name => name + " ▸");
        private static readonly string[] ChestCommands = { "拿走全部", "存放全部", "快速堆叠", "补货" };
        private static readonly CompanionBulkOperation[] ChestOperations =
        {
            CompanionBulkOperation.LootAll, CompanionBulkOperation.DepositAll, CompanionBulkOperation.QuickStack, CompanionBulkOperation.Restock
        };
        private CompanionInventoryArea storage;
        private bool storageAvailable;
        private string storageReason;
        private SelectionIdentity? onceSelection;
        private F5Rect ownerToolbarView, companionToolbarView;
        private float ownerToolbarOffset, companionToolbarOffset;

        private float ToolWidth(string text) => Math.Max(32, measure(text, .7f).Width + 12);
        private float StorageButtonWidth()
        {
            float width = 0;
            foreach (string label in StorageLabels) width = Math.Max(width, ToolWidth(label));
            return width;
        }
        private float ToolbarWidth(bool owner)
        {
            if (owner)
                return ToolWidth(ChestCommands[0]) + ToolWidth(ChestCommands[1]) + ToolWidth(ChestCommands[2]) +
                    ToolWidth(ChestCommands[3]) + 3 * ToolbarGap;
            float ordinary = ToolWidth("选择主要武器") + ToolWidth("排列物品") + ToolWidth("使用一次") + 3 * ToolbarGap;
            float editing = ToolWidth("完成") + ToolWidth("取消") + WeaponCounterWidth() + 3 * ToolbarGap;
            float once = ToolWidth("取消") + ToolWidth("选择一件物品") + 2 * ToolbarGap;
            return Math.Max(ordinary, Math.Max(editing, once)) + StorageButtonWidth();
        }
        private float WeaponCounterWidth()
        {
            float width = 0;
            foreach (string label in WeaponCountLabels) width = Math.Max(width, ToolWidth(label));
            return width;
        }
        private bool ScrollToolbar(Vector2 point, int wheel)
        {
            bool own = ownerToolbarView.Contains(point.X, point.Y);
            if (!own && !companionToolbarView.Contains(point.X, point.Y)) return false;
            var view = own ? ownerToolbarView : companionToolbarView;
            float maximum = Math.Max(0, ToolbarWidth(own) - view.Width);
            if (maximum <= 0) return false;
            float offset = (own ? ownerToolbarOffset : companionToolbarOffset) - Math.Sign(wheel) * 60;
            offset = Math.Max(0, Math.Min(maximum, offset));
            if (own) ownerToolbarOffset = offset; else companionToolbarOffset = offset;
            return true;
        }

        private void InventoryToolbar(bool owner, F5Rect view, float y, float row)
        {
            var toolbar = new F5Rect(view.X + 12, y, view.Width - 24, row);
            float maximum = Math.Max(0, ToolbarWidth(owner) - toolbar.Width);
            if (owner) { ownerToolbarView = toolbar; ownerToolbarOffset = Math.Min(ownerToolbarOffset, maximum); }
            else { companionToolbarView = toolbar; companionToolbarOffset = Math.Min(companionToolbarOffset, maximum); }
            float x = toolbar.X - (owner ? ownerToolbarOffset : companionToolbarOffset);
            bool normal = weaponDraft == null && onceSelection == null;
            bool bulk = normal && selectedTransfer && (storage == CompanionInventoryArea.Inventory || storageAvailable);
            int index = Array.IndexOf(StorageOrder, storage);
            string domain = index == 0 ? "队友背包" : "队友的" + StorageNames[index];
            string blocked = storageReason ?? selectedUnavailable ?? (normal ? null : "请先完成当前选择");
            if (owner)
            {
                for (int i = 0; i < ChestCommands.Length; i++)
                {
                    var operation = ChestOperations[i];
                    AddTool(ChestCommands[i], ChestCommands[i], ref x, toolbar, view,
                        () => RunBulk(operation), bulk, blocked ?? ChestToolHint(operation, domain), scrollable: maximum > 0);
                }
                return;
            }

            weaponCountLabel = -1;
            if (weaponDraft != null)
            {
                AddTool("weapon-done", "完成", ref x, toolbar, view, FinishWeaponSelection, true,
                    "未选武器时自动选择。", scrollable: maximum > 0);
                AddTool("weapon-cancel", "取消", ref x, toolbar, view, CancelWeaponSelection, true, null, scrollable: maximum > 0);
                var count = new F5Rect(x, y, WeaponCounterWidth(), row);
                if (ActorVisible(count, toolbar) && ActorVisible(count, view)) { weaponCountLabel = labels.Count; Text(WeaponCount(), count, .65f); }
            }
            else if (onceSelection != null)
            {
                AddTool("once-cancel", "取消", ref x, toolbar, view, CancelSelectionModes, true, null, scrollable: maximum > 0);
                var hint = new F5Rect(x, y, ToolWidth("选择一件物品"), row);
                if (ActorVisible(hint, toolbar) && ActorVisible(hint, view)) Text("选择一件物品", hint, .65f);
            }
            else
            {
                bool canSelect = WeaponToolbarEligible(host.GetBody(selected));
                AddTool("weapon-select", "选择主要武器", ref x, toolbar, view, BeginWeaponSelection, canSelect,
                    canSelect ? "从队友主背包选择最多两把。金线标记当前使用的武器。" : selectedUnavailable, scrollable: maximum > 0);
                AddTool("item-sort", "排列物品", ref x, toolbar, view, () => RunBulk(CompanionBulkOperation.Sort), bulk,
                    blocked ?? "排列" + domain + "中的物品", scrollable: maximum > 0);
                AddTool("item-once", "使用一次", ref x, toolbar, view, BeginOnceSelection, canSelect,
                    canSelect ? "从队友主背包选择一次使用的物品。" : selectedUnavailable, scrollable: maximum > 0);
            }
            // Keep this control at the same end of the same row in every mode.
            float storageWidth = StorageButtonWidth();
            x = toolbar.X + ToolbarWidth(false) - storageWidth - companionToolbarOffset;
            int next = (index + 1) % StorageOrder.Length;
            string storageHint = "点击切换到" + StorageNames[next];
            if (!String.IsNullOrEmpty(storageReason)) storageHint = storageReason + "\n" + storageHint;
            AddTool("storage-" + storage, StorageLabels[index], ref x, toolbar, view,
                () => SelectStorage(StorageOrder[next], true), CurrentProfile() != null, storageHint, storageWidth, maximum > 0);
        }

        private static string ChestToolHint(CompanionBulkOperation operation, string domain)
        {
            switch (operation)
            {
                case CompanionBulkOperation.LootAll: return "把" + domain + "中的物品拿到主人背包";
                case CompanionBulkOperation.DepositAll: return "把主人背包中可存放的物品放入" + domain;
                case CompanionBulkOperation.QuickStack: return "把主人背包中可堆叠的物品放入" + domain;
                default: return "从" + domain + "补充主人背包已有的物品";
            }
        }

        private void AddTool(string key, string text, ref float x, F5Rect toolbar, F5Rect view, Action execute,
            bool enabled, string hintText, float width = 0, bool scrollable = false)
        {
            if (width == 0) width = ToolWidth(text);
            var rect = new F5Rect(x, toolbar.Y, width, toolbar.Height);
            x += width + ToolbarGap;
            if (!ActorVisible(rect, toolbar) || !ActorVisible(rect, view)) return;
            var control = Button(key, text, rect, execute, enabled);
            control.Hint = !scrollable ? hintText : String.IsNullOrEmpty(hintText) ?
                "滚轮查看其余操作" : hintText + "\n滚轮查看其余操作";
        }

        private void SelectStorage(CompanionInventoryArea area, bool open)
        {
            host.CancelItemGesture();
            rightSlot = null; rightRepeat = 0;
            CancelSelectionModes();
            notice = null;
            storage = area;
            host.SetItemStorage(selected, area);
            if (open && area != CompanionInventoryArea.Inventory && !host.OpenStorage(selected, area))
                ShowNotice(host.Feedback);
            RefreshStorage();
            Invalidate();
        }

        private void RefreshStorage()
        {
            if (page != 0) return;
            string reason = null;
            bool available = storage == CompanionInventoryArea.Inventory || host.StorageAvailable(selected, storage, out reason);
            if (storageAvailable == available && reason == storageReason) return;
            storageAvailable = available;
            storageReason = reason;
            contentInvalidated = true;
        }

        private void RunBulk(CompanionBulkOperation operation)
        {
            host.BulkItems(selected, storage, operation);
            ShowNotice(host.Feedback);
            contentInvalidated = true;
        }

        private void BeginOnceSelection()
        {
            SelectStorage(CompanionInventoryArea.Inventory, false);
            if (Main.mouseItem?.IsAir == false) { ShowNotice("请先放回鼠标上的物品。"); return; }
            onceSelection = CaptureSelection();
        }

        private void UpdateOnceSelection()
        {
            if (onceSelection.HasValue && !SelectionCurrent(onceSelection.Value))
                CancelSelectionModes();
        }

        private void CancelSelectionModes()
        {
            CancelWeaponSelection();
            if (onceSelection == null) return;
            onceSelection = null;
            Invalidate();
        }
    }
}
