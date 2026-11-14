using System;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private float contentY;
        private bool bodyOffsetClamped;
        private void BuildCreate(float row)
        {
            contentY = bodyView.Y - bodyOffset;
            BodyText("创建队友", row);
            BodyText("人物名字", row);
            field = BodyRect(row);
            Button("name", editor == null ? "点击输入名字" : "", field, () => BeginEdit("name", ""));
            contentY += row + 8;
            BodyButton("save-create", "创建", row, () => RequestFinish());
            BodyButton("cancel-create", "取消", row, () => { CancelEdit(); creating = false; Invalidate(); });
            FinishBody();
        }
        private CompanionProfile CurrentProfile()
        {
            var profiles = host.Profiles;
            for (int i = 0; i < profiles.Count; i++) if (profiles[i].Id == selected) return profiles[i];
            return null;
        }
        private void BuildSelected(float row)
        {
            field = default(F5Rect); contentY = bodyView.Y - bodyOffset;
            var profile = CurrentProfile();
            if (profile == null) { BodyText("点击左下角＋创建队友", row); FinishBody(); return; }
            var body = host.GetBody(selected);
            BodyText(profile.Name + " · " + host.Status(selected), row);
            if (host.SaveFailed(selected)) BodyButton("retry-save", "保存失败 · 重试保存", row, () => Command(() => host.RetrySave(selected)));
            if (body != null) BodyText("生命 " + body.statLife + "/" + body.statLifeMax2 + "　魔力 " + body.statMana + "/" + body.statManaMax2, row);
            bool online = host.CanTransfer(selected);
            Pair("join", "加入当前世界", () => Command(() => host.Join(selected)), false,
                "recall", "收回", () => Command(() => host.Recall(selected)), false, row);
            if (page == 0) BuildInventory(body, online, row);
            else BuildConfiguration(profile, row);
            FinishBody();
        }
        private void BuildInventory(Player body, bool online, float row)
        {
            if (!online) BodyText(host.Status(selected) == "已收回" ? "已收回，加入后可转移" : "当前不可转移：" + host.Status(selected), row);
            if (sourceSlot >= 0)
            {
                Item source = SourceItem(body);
                if (source == null || source.IsAir || Identity(source) != sourceIdentity)
                { sourceSlot = -1; sourceIdentity = null; Invalidate(); }
                else
                {
                    BodyText((sourceOwner ? "给队友：" : "取回：") + source.Name + " ×" + source.stack, row);
                    Pair("transfer-one:" + sourceIdentity, "转移 1 个", () => Transfer(false), false,
                        "transfer-stack:" + sourceIdentity, "转移整组", () => Transfer(true), false, row, online);
                    if (sourceArea != CompanionInventoryArea.Inventory && !sourceOwner)
                        BodyButton("unequip:" + sourceIdentity, "卸下", row, () => Command(() => host.Unequip(selected, sourceArea, sourceSlot)), online);
                    else if (sourceArea == CompanionInventoryArea.Inventory)
                    {
                        BodyButton("equip:" + sourceIdentity, "装备", row, () => Command(() => host.Equip(selected, sourceOwner, sourceSlot)), online);
                        if (!sourceOwner && sourceSlot < 50) BodyButton("weapon:" + sourceIdentity, "设为武器", row,
                            () => Command(() => host.SelectWeapon(selected, sourceSlot)), online);
                    }
                }
            }
            BodyText("队友背包", row);
            if (body != null) Slots(body.inventory, false, CompanionInventoryArea.Inventory, 58);
            BodyText("装备", row);
            if (body != null) Slots(body.armor, false, CompanionInventoryArea.Armor, body.armor.Length);
            BodyText("装备染料", row);
            if (body != null) Slots(body.dye, false, CompanionInventoryArea.Dye, body.dye.Length);
            BodyText("宠物、坐骑与钩爪", row);
            if (body != null) Slots(body.miscEquips, false, CompanionInventoryArea.Misc, body.miscEquips.Length);
            BodyText("特殊装备染料", row);
            if (body != null) Slots(body.miscDyes, false, CompanionInventoryArea.MiscDye, body.miscDyes.Length);
            BodyText("我的背包", row);
            if (Main.LocalPlayer != null) Slots(Main.LocalPlayer.inventory, true, CompanionInventoryArea.Inventory, 58);
            BodyText("点击物品后选择转移数量；装备可选中后卸下。", row);
        }
        private void Slots(Item[] items, bool owner, CompanionInventoryArea area, int count)
        {
            count = Math.Min(count, items.Length);
            int columns = Math.Max(1, (int)(bodyView.Width / 44));
            float size = Math.Min(44, bodyView.Width / columns);
            for (int i = 0; i < count; i++)
            {
                int slot = i; Item item = items[i];
                var rect = new F5Rect(bodyView.X + i % columns * size, contentY + i / columns * size, size - 3, size - 3);
                if (!FullyVisible(rect)) continue;
                string identity = Identity(item);
                var c = Button("slot:" + owner + ":" + area + ":" + slot + ":" + identity, "", rect, () =>
                {
                    sourceOwner = owner; sourceArea = area; sourceSlot = slot; sourceIdentity = identity; Invalidate();
                }, item != null && !item.IsAir, sourceSlot == slot && sourceOwner == owner && sourceArea == area);
                c.Element = null;
                c.ItemType = item?.type ?? 0; c.Stack = item?.stack ?? 0;
                c.Hint = item == null || item.IsAir ? null : item.Name + " ×" + item.stack;
                icons.AddVisible(c.ItemType);
            }
            contentY += (float)Math.Ceiling((double)count / columns) * size + 8;
        }
        private Item SourceItem(Player body)
        {
            var player = sourceOwner ? Main.LocalPlayer : body;
            if (player == null || sourceSlot < 0) return null;
            var items = Companions.CompanionResources.Items(player, sourceArea);
            return sourceSlot < items.Length ? items[sourceSlot] : null;
        }
        private static string Identity(Item item)
        { return item == null || item.IsAir ? "empty" : item.type + ":" + item.prefix + ":" + item.stack; }
        private void Transfer(bool whole)
        {
            // The release key contains the observed source identity; the Host
            // additionally arbitrates current use/consumption on the game thread.
            if (sourceSlot < 0) return;
            Command(() => host.Transfer(selected, sourceOwner, sourceArea, sourceSlot, whole));
        }
        private void BuildConfiguration(CompanionProfile profile, float row)
        {
            var behavior = profile.Behavior;
            BodyText("移动", row);
            Pair("follow", "跟随主人", () => Behavior(true, null, null, null), behavior.Follow,
                "stay", "原地停留", () => Behavior(false, null, null, null), !behavior.Follow, row);
            BodyText("攻击", row);
            Pair("attack-on", "开启", () => Behavior(null, true, null, null), behavior.AttackEnabled,
                "attack-off", "关闭", () => Behavior(null, false, null, null), !behavior.AttackEnabled, row, true, true);
            BodyText("攻击模式", row);
            Pair("proactive", "主动攻击", () => Behavior(null, null, false, null), !behavior.SelfDefense,
                "defensive", "仅自卫", () => Behavior(null, null, true, null), behavior.SelfDefense, row);
            BodyText("目标优先", row);
            Pair("prefer-owner", "主人附近优先", () => Behavior(null, null, null, true), behavior.PreferOwner,
                "prefer-self", "自身附近优先", () => Behavior(null, null, null, false), !behavior.PreferOwner, row);
            var body = host.GetBody(selected);
            if (body != null && body.selectedItem >= 0 && body.selectedItem < body.inventory.Length)
                BodyText("当前武器：" + body.inventory[body.selectedItem].Name, row);
            BodyButton("password", editor != null && editKind == "password" ? "" : "连接密码：点击输入", row,
                () => BeginEdit("password", host.ConnectionPassword ?? ""));
            if (editKind == "password")
            {
                field = new F5Rect(bodyView.X + 5, contentY - row - 8, bodyView.Width - 10, row);
                BodyButton("save-password", "应用密码", row, () => RequestFinish());
            }
            BodyButton("restore-behavior", "恢复行为设置", row, () => Command(() => host.RestoreBehavior(selected)));
            if (!confirmingReset) BodyButton("request-reset", "重置人物档案", row, () => { confirmingReset = true; Invalidate(); });
            else
            {
                BodyText("将清空物品、装备、成长和 AI 设置。", row);
                BodyText("保留名字、外观和归属。", row);
                Pair("confirm-reset", "确认重置", () => { if (host.Reset(selected)) confirmingReset = false; notice = null; Invalidate(); }, false,
                    "cancel-reset", "取消", () => { confirmingReset = false; Invalidate(); }, false, row);
            }
        }
        private void Behavior(bool? follow, bool? attack, bool? defense, bool? prefer)
        {
            var profile = CurrentProfile(); if (profile == null) return;
            var b = profile.Behavior;
            Command(() => host.SetBehavior(selected, new CompanionBehavior {
                Follow = follow ?? b.Follow, AttackEnabled = attack ?? b.AttackEnabled,
                SelfDefense = defense ?? b.SelfDefense, PreferOwner = prefer ?? b.PreferOwner }));
        }
        private void Command(Func<bool> command)
        { command(); notice = null; Invalidate(); }
        private F5Rect BodyRect(float row) { return new F5Rect(bodyView.X, contentY, bodyView.Width, row); }
        private bool FullyVisible(F5Rect rect) { return rect.Y >= bodyView.Y && rect.Bottom <= bodyView.Bottom; }
        private void BodyText(string text, float row)
        { var rect = BodyRect(row); if (FullyVisible(rect)) Text(text, rect); contentY += row + 4; }
        private void BodyButton(string key, string text, float row, Action action, bool enabled = true)
        { var rect = BodyRect(row); if (FullyVisible(rect)) Button(key, text, rect, action, enabled); contentY += row + 8; }
        private void Pair(string key1, string text1, Action action1, bool selected1, string key2, string text2, Action action2, bool selected2,
            float row, bool enabled = true, bool offSecond = false)
        {
            float width = (bodyView.Width - 6) / 2;
            // The shared button renderer converts its underline to exactly two
            // device pixels using this pass's actual UI matrix.
            var first = new F5Rect(bodyView.X, contentY, width, row);
            var second = new F5Rect(bodyView.X + width + 6, contentY, width, row);
            if (FullyVisible(first)) Button(key1, text1, first, action1, enabled, selected1);
            if (FullyVisible(second)) Button(key2, text2, second, action2, enabled, selected2, offSecond);
            contentY += row + 8;
        }
        private void FinishBody()
        {
            int maximum = Math.Max(0, (int)Math.Ceiling(contentY + bodyOffset - bodyView.Bottom));
            bodyMaximum = maximum;
            if (bodyOffset > maximum) { bodyOffset = maximum; bodyOffsetClamped = true; Invalidate(); }
        }
    }
}
