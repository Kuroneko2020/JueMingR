using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Companions;
using JueMingR.TerrariaHost.Hotkeys;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private static readonly string[] SwitchActions = { "开启", "关闭", "键" };
        private static readonly string[] ConfigurableActions = { "开启", "关闭", "配置", "键" };
        private static readonly string[] LifeActions = { "智能", "快速", "关闭", "配置", "键" };
        private CompanionSetting? expandedConfiguration;
        private int expandedBuff;
        private F5Rect configurationViewport, configurationNameRect;
        private string configurationName;
        private readonly List<F5Element> rowElements = new List<F5Element>();
        private readonly List<SupplyPresentation> supplyRows = new List<SupplyPresentation>();
        private uint nextSupplyPresentation;

        private sealed class SupplyPresentation
        {
            internal CompanionSupplyKind Kind;
            internal int[] Types;
            internal int Label;
        }

        private F5Rect ConfigurationScrollView => configurationViewport.Width > 0 ? configurationViewport : bodyView;

        private void RetireConfigurationInput()
        {
            expandedConfiguration = null; expandedBuff = 0; confirmingReset = false; CancelEdit();
        }

        private void BuildConfiguration(CompanionProfile profile, float row)
        {
            float x = bodyView.X + 8, width = bodyView.Width - 16, y = bodyView.Y;
            float nameWidth = Math.Min(width * .45f, Math.Max(48, measure(profile.Name, .85f).Width + 8));
            configurationName = profile.Name;
            configurationNameRect = new F5Rect(x, y, nameWidth, row);
            Text(profile.Name, configurationNameRect, .85f);
            selectedStatusLabel = labels.Count;
            labels.Add(new F5Element(F5ElementKind.Text, new F5Rect(x + nameWidth + 8, y, width - nameWidth - 8, row),
                host.Status(selected), default(F5Size), .65f, F5Command.None));
            respawnLabel = labels.Count;
            labels.Add(new F5Element(F5ElementKind.Text, new F5Rect(x, y + row, width, 22),
                host.RespawnStatus(selected), default(F5Size), .65f, F5Command.None));
            y += row + 24;
            configurationViewport = new F5Rect(x, y, width, Math.Max(0, bodyView.Bottom - y));
            contentY = y - bodyOffset;
            var behavior = profile.Behavior;
            ConfigurationPair(profile, behavior, CompanionSetting.Follow, CompanionSetting.Attack, row);
            ConfigurationPair(profile, behavior, CompanionSetting.Life, CompanionSetting.Mana, row);
            ConfigurationPair(profile, behavior, CompanionSetting.Buff, CompanionSetting.Growth, row);
            ConfigurationPair(profile, behavior, CompanionSetting.Respawn, null, row);

            if (host.SaveFailed(selected))
                ConfigurationAction("retry-save", "保存失败 · 重试保存", () => Command(() => host.RetrySave(selected)), row);
            ConfigurationAction("restore-behavior", "恢复行为设置", () => Command(() => host.RestoreBehavior(selected)), row);
            if (!confirmingReset)
                ConfigurationAction("request-reset", "清空人物", () => { confirmingReset = true; Invalidate(); }, row);
            else
            {
                ConfigurationText("将清空物品、装备、成长和 AI 设置。", row);
                ConfigurationText("保留名字、外观、难度和归属。", row);
                ConfigurationAction("confirm-reset", "确认清空人物", () => { if (host.Reset(selected)) confirmingReset = false; ShowNotice(host.Feedback); Invalidate(); }, row);
                ConfigurationAction("cancel-reset", "取消", () => { confirmingReset = false; Invalidate(); }, row);
            }
            bodyMaximum = Math.Max(0, (int)Math.Ceiling(contentY + bodyOffset - configurationViewport.Bottom));
            if (bodyOffset > bodyMaximum) { bodyOffset = bodyMaximum; bodyOffsetClamped = true; Invalidate(); }
        }

        private void ConfigurationPair(CompanionProfile profile, CompanionBehavior behavior, CompanionSetting left, CompanionSetting? right, float row)
        {
            float x = configurationViewport.X, width = (configurationViewport.Width - 8) / 2;
            float leftEnd = contentY, rightEnd = contentY;
            ConfigurationRow(profile, behavior, left, x, width, ref leftEnd);
            if (right.HasValue) ConfigurationRow(profile, behavior, right.Value, x + width + 8, width, ref rightEnd);
            contentY = Math.Max(leftEnd, rightEnd);
            if (expandedConfiguration.HasValue && (expandedConfiguration == left || expandedConfiguration == right))
                BuildSupplyDetails(expandedConfiguration.Value, behavior, row);
        }

        private void ConfigurationRow(CompanionProfile profile, CompanionBehavior behavior, CompanionSetting setting,
            float x, float width, ref float y)
        {
            int mode = SettingValue(behavior, setting);
            string[] actions = setting == CompanionSetting.Life ? LifeActions :
                setting == CompanionSetting.Mana || setting == CompanionSetting.Buff || setting == CompanionSetting.Growth ?
                    ConfigurableActions : SwitchActions;
            if (HotkeyClicked == null) actions = actions.Take(actions.Length - 1).ToArray();
            rowElements.Clear();
            new F5RowLayout(rowElements, measure).Row(ref y, x, width, CompanionHotkeys.Name(setting), actions);
            int actionIndex = 0;
            foreach (var element in rowElements)
            {
                var r = element.Rect;
                if (element.Kind == F5ElementKind.Panel)
                {
                    AddActorHeader(r, configurationViewport);
                    continue;
                }
                if (element.Kind == F5ElementKind.Text)
                {
                    if (ConfigurationVisible(r)) labels.Add(element);
                    continue;
                }
                string action = actions[actionIndex++];
                if (!ConfigurationVisible(r)) continue;
                if (action == "键")
                {
                    string id = CompanionHotkeys.Id(profile, setting);
                    var control = Button("binding-" + id, "", r, null);
                    control.HotkeyId = id;
                    // Read the translated control on release, which is the
                    // same rectangle the shared popup's anchor checks.
                    control.Execute = () => HotkeyClicked(id, control.Rect);
                    control.Hint = "双击设置" + CompanionHotkeys.Name(setting) + "的快捷键";
                    continue;
                }
                int value = action == "关闭" ? 0 : action == "智能" ? 2 : 1;
                bool configure = action == "配置";
                bool current = !configure && value == mode;
                var button = ConfigurationButton("config-" + setting + "/" + action, action, r,
                    configure ? (Action)(() => ExpandConfiguration(setting)) : () => SetConfigurationSetting(setting, value),
                    current, current && mode == 0);
                button.Element = element;
            }
        }

        private static int SettingValue(CompanionBehavior b, CompanionSetting setting)
        {
            switch (setting)
            {
                case CompanionSetting.Follow: return b.Follow ? 1 : 0;
                case CompanionSetting.Attack: return b.AttackEnabled ? 1 : 0;
                case CompanionSetting.Life: return b.HealthRecoveryEnabled ? (int)b.HealthRecoveryMode : 0;
                case CompanionSetting.Mana: return b.ManaRecoveryEnabled ? 1 : 0;
                case CompanionSetting.Buff: return b.BuffEnabled ? 1 : 0;
                case CompanionSetting.Growth: return b.GrowthEnabled ? 1 : 0;
                default: return b.AutoRespawn ? 1 : 0;
            }
        }

        private void SetConfigurationSetting(CompanionSetting setting, int value)
        {
            var behavior = CurrentProfile()?.Behavior;
            if (behavior == null || SettingValue(behavior, setting) == value) return;
            ApplySetting(behavior, setting, value);
            Command(() => host.SetBehavior(selected, behavior));
        }

        private static void ApplySetting(CompanionBehavior b, CompanionSetting setting, int value)
        {
            switch (setting)
            {
                case CompanionSetting.Follow: b.Follow = value != 0; break;
                case CompanionSetting.Attack: b.AttackEnabled = value != 0; break;
                case CompanionSetting.Life:
                    b.HealthRecoveryEnabled = value != 0;
                    if (value != 0) b.HealthRecoveryMode = (CompanionHealthRecoveryMode)value;
                    break;
                case CompanionSetting.Mana: b.ManaRecoveryEnabled = value != 0; break;
                case CompanionSetting.Buff: b.BuffEnabled = value != 0; break;
                case CompanionSetting.Growth: b.GrowthEnabled = value != 0; break;
                case CompanionSetting.Respawn: b.AutoRespawn = value != 0; break;
            }
        }

        private void ExpandConfiguration(CompanionSetting setting)
        {
            expandedConfiguration = expandedConfiguration == setting ? (CompanionSetting?)null : setting;
            expandedBuff = 0; Invalidate();
        }

        private void BuildSupplyDetails(CompanionSetting setting, CompanionBehavior behavior, float row)
        {
            if (setting == CompanionSetting.Life)
                ConfigurationAction("nurse", behavior.AllowNurse ? "关闭护士治疗" : "开启护士治疗",
                    () => EditBehavior(b => b.AllowNurse = !b.AllowNurse), row);
            var kind = setting == CompanionSetting.Life ? CompanionSupplyKind.Life :
                setting == CompanionSetting.Mana ? CompanionSupplyKind.Mana :
                setting == CompanionSetting.Buff ? CompanionSupplyKind.Buff : CompanionSupplyKind.Growth;
            var types = new HashSet<int>(SupplyTypes(behavior, kind));
            var body = host.GetBody(selected);
            if (kind == CompanionSupplyKind.Buff)
            {
                ConfigurationText("饱食", row);
                foreach (var group in CompanionSupplyCatalog.BuffGroups())
                {
                    if (BuffID.Sets.IsWellFed[group.Key])
                        BuildBuffGroup(body, types, group, row);
                }
                ConfigurationText("其他增益", row);
                foreach (var group in CompanionSupplyCatalog.BuffGroups())
                    if (!BuffID.Sets.IsWellFed[group.Key])
                        BuildBuffGroup(body, types, group, row);
            }
            else
                foreach (var item in CompanionSupplyCatalog.Definitions(kind))
                    ConfigureSupplyItem(body, kind, item, types, row);
            nextSupplyPresentation = Main.GameUpdateCount + 20;
        }

        private void BuildBuffGroup(Player body, HashSet<int> allowed, IGrouping<int, Item> group, float row)
        {
            if (NextSupplyRow(row, out var rect))
                ConfigureSupply(body, CompanionSupplyKind.Buff, Lang.GetBuffName(group.Key),
                    group.Select(i => i.type).ToArray(), allowed, rect, group.Count() > 1 ? group.Key : 0);
            if (expandedBuff != group.Key) return;
            foreach (var item in group)
                ConfigureSupplyItem(body, CompanionSupplyKind.Buff, item, allowed, row, indent: true);
        }

        private void ConfigureSupplyItem(Player body, CompanionSupplyKind kind, Item item, HashSet<int> allowed, float row, bool indent = false)
        {
            if (NextSupplyRow(row, out var rect))
                ConfigureSupply(body, kind, indent ? "  " + item.Name : item.Name, new[] { item.type }, allowed, rect);
        }

        private bool NextSupplyRow(float row, out F5Rect rect)
        {
            rect = new F5Rect(configurationViewport.X, contentY, configurationViewport.Width, row);
            contentY += row + 4;
            return ConfigurationVisible(rect);
        }

        private void ConfigureSupply(Player body, CompanionSupplyKind kind, string name, int[] types, HashSet<int> saved, F5Rect rect, int effect = 0)
        {
            bool potion = kind == CompanionSupplyKind.Life || kind == CompanionSupplyKind.Mana;
            int countAllowed = types.Count(type => potion != saved.Contains(type));
            bool allAllowed = countAllowed == types.Length;
            string state = allAllowed ? "已允许" : countAllowed == 0 ? "未允许" : "部分允许";
            float toggleWidth = ConfigurationWidth("部分允许", .65f), stockWidth = ConfigurationWidth("持有 9999", .65f);
            var toggle = new F5Rect(rect.Right - toggleWidth, rect.Y, toggleWidth, rect.Height);
            var stock = new F5Rect(toggle.X - stockWidth - 6, rect.Y, stockWidth, rect.Height);
            var title = new F5Rect(rect.X, rect.Y, Math.Max(0, stock.X - rect.X - 6), rect.Height);
            if (effect == 0) Text(name, title, .65f);
            else ConfigurationButton("effect:" + effect, name + (expandedBuff == effect ? " ▴" : " ▾"), title,
                () => { expandedBuff = expandedBuff == effect ? 0 : effect; Invalidate(); }, scale: .65f);
            int label = labels.Count;
            Text(ConfigurationStockText(body, kind, types), stock, .65f);
            supplyRows.Add(new SupplyPresentation { Kind = kind, Types = types, Label = label });
            ConfigurationButton("supply:" + kind + ":" + types[0] + ":" + effect + ":" + state, state, toggle,
                () => EditBehavior(b => UpdateSupplyTypes(b, kind, types, potion == allAllowed)), allAllowed || countAllowed == 0,
                countAllowed == 0, .65f).Hint = name;
        }

        private void RefreshConfigurationPresentation(Player body)
        {
            if (page != 1 || supplyRows.Count == 0 || Main.GameUpdateCount < nextSupplyPresentation) return;
            nextSupplyPresentation = Main.GameUpdateCount + 20;
            foreach (var row in supplyRows)
                UpdateLabel(row.Label, ConfigurationStockText(body, row.Kind, row.Types));
        }

        private static string ConfigurationStockText(Player body, CompanionSupplyKind kind, int[] types)
        {
            if (kind == CompanionSupplyKind.Growth && CompanionSupplyCatalog.GrowthComplete(body, types[0])) return "已完成";
            int stock = CompanionSupplyCatalog.Count(body, kind, types);
            return stock > 0 ? "持有 " + stock : "缺货";
        }

        private static int[] SupplyTypes(CompanionBehavior b, CompanionSupplyKind kind)
        {
            switch (kind)
            {
                case CompanionSupplyKind.Life: return b.ExcludedHealingTypes;
                case CompanionSupplyKind.Mana: return b.ExcludedManaTypes;
                case CompanionSupplyKind.Growth: return b.AllowedGrowthTypes;
                default: return b.AllowedBuffTypes;
            }
        }

        private static void UpdateSupplyTypes(CompanionBehavior b, CompanionSupplyKind kind, int[] members, bool add)
        {
            // Only an explicit group edit changes its concrete member types.
            // Retain all existing types outside the displayed definition.
            var types = new SortedSet<int>(SupplyTypes(b, kind));
            if (add) types.UnionWith(members); else types.ExceptWith(members);
            switch (kind)
            {
                case CompanionSupplyKind.Life: b.ExcludedHealingTypes = types.ToArray(); break;
                case CompanionSupplyKind.Mana: b.ExcludedManaTypes = types.ToArray(); break;
                case CompanionSupplyKind.Growth: b.AllowedGrowthTypes = types.ToArray(); break;
                case CompanionSupplyKind.Buff: b.AllowedBuffTypes = types.ToArray(); break;
            }
        }

        private float ConfigurationWidth(string text, float scale = .7f) => Math.Max(42, (float)Math.Ceiling(measure(text, scale).Width + 16));

        private Control ConfigurationButton(string key, string text, F5Rect rect, Action execute,
            bool selectedValue = false, bool off = false, float scale = .7f)
        {
            var control = Button(key, text, rect, execute, true, selectedValue, off);
            if (scale != .7f) control.Element = new F5Element(F5ElementKind.Button, rect, text, measure(text, scale), scale, F5Command.None);
            return control;
        }

        private bool ConfigurationVisible(F5Rect r) => ActorVisible(r, configurationViewport);

        private void ConfigurationText(string text, float height)
        {
            var rect = new F5Rect(configurationViewport.X, contentY, configurationViewport.Width, height);
            if (ConfigurationVisible(rect)) Text(text, rect, .65f);
            contentY += height + 4;
        }

        private void ConfigurationAction(string key, string text, Action execute, float row)
        {
            var rect = new F5Rect(configurationViewport.X, contentY, Math.Min(configurationViewport.Width, ConfigurationWidth(text)), row);
            if (ConfigurationVisible(rect)) ConfigurationButton(key, text, rect, execute);
            contentY += row + 6;
        }
    }
}
