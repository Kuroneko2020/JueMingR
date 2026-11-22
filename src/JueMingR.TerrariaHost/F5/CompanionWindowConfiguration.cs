using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Companions;
using JueMingR.TerrariaHost.Hotkeys;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private static readonly string[] SwitchActions = { "开启", "关闭", "键" };
        private static readonly string[] ConfigurableActions = { "开启", "关闭", "配置", "键" };
        private static readonly string[] LifeActions = { "智能", "快速", "关闭", "配置", "键" };
        private F5Rect configurationViewport, configurationNameRect;
        private string configurationName;
        private CompanionProfile deletionTarget;
        private long deletionSession;
        private IReadOnlyList<CompanionDeletion> shownDeletions;
        private Guid shownDeletionOwner;
        private readonly List<F5Element> rowElements = new List<F5Element>();
        private F5Rect ConfigurationScrollView => configurationViewport.Width > 0 ? configurationViewport : bodyView;

        private void RetireConfigurationInput()
        {
            CloseSupplyPopup(); confirmingReset = false; deletionTarget = null; CancelEdit();
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
            BuildDeletionReceipts(row);
            var behavior = profile.Behavior;
            ConfigurationPair(profile, behavior, CompanionSetting.Follow, CompanionSetting.Attack);
            ConfigurationPair(profile, behavior, CompanionSetting.Life, CompanionSetting.Mana);
            ConfigurationPair(profile, behavior, CompanionSetting.Buff, CompanionSetting.Growth);
            ConfigurationPair(profile, behavior, CompanionSetting.Nurse, CompanionSetting.Furniture);
            ConfigurationPair(profile, behavior, CompanionSetting.Respawn, null);

            if (host.SaveFailed(selected))
                ConfigurationAction("retry-save", "保存失败 · 重试保存", () => Command(() => host.RetrySave(selected)), row);
            ConfigurationAction("restore-behavior", "恢复行为设置", () => Command(() => host.RestoreBehavior(selected)), row);
            if (!confirmingReset)
                ConfigurationAction("request-reset", "清空配置", () => { RetireConfigurationInput(); confirmingReset = true; Invalidate(); }, row);
            else
            {
                ConfigurationText("将清空物品、装备、成长和 AI 设置。", row);
                ConfigurationText("保留名字、外观、难度和归属。", row);
                ConfigurationAction("confirm-reset", "确认清空配置", () => { if (host.Reset(selected)) confirmingReset = false; ShowNotice(host.Feedback); Invalidate(); }, row);
                ConfigurationAction("cancel-reset", "取消", () => { confirmingReset = false; Invalidate(); }, row);
            }
            if (deletionTarget == null)
                ConfigurationAction("request-delete", "删除人物", () =>
                {
                    RetireConfigurationInput();
                    deletionTarget = profile; deletionSession = host.CreationSessionToken;
                    Invalidate();
                }, row);
            else
            {
                var target = deletionTarget;
                long session = deletionSession;
                ConfigurationText("删除「" + target.Name + "」及其物品、成长和专属配置。", row);
                ConfigurationText("删除后无法通过重新加入恢复。", row);
                ConfigurationAction("confirm-delete", "确认删除人物", () =>
                {
                    if (!ReferenceEquals(deletionTarget, target) || selected != target.Id) return;
                    RetireConfigurationInput();
                    if (host.DeleteCompanion(target.OwnerId, target.Id, session))
                    {
                        CancelSelectionModes(); pendingCreatedId = Guid.Empty; pendingCreatedName = null;
                        bodyOffset = 0;
                    }
                    ShowNotice(host.Feedback); Invalidate();
                }, row);
                ConfigurationAction("cancel-delete", "取消", () => { deletionTarget = null; Invalidate(); }, row);
            }
            FinishConfiguration();
        }

        private void BuildDeletionReceipts(float row)
        {
            Guid owner = host.DeletionViewOwner;
            if (owner == Guid.Empty) return;
            foreach (var deletion in host.Deletions)
            {
                if (deletion.OwnerId != owner) continue;
                string status = deletion.Error ?? (deletion.Finishing ? "正在确认删除结果。" : deletion.FilesRemoved ? "正在清理快捷键。" : "正在删除人物。");
                string line = deletion.Name + " · " + status;
                float width = configurationViewport.Width;
                if (measure(line, .65f).Width <= width) ConfigurationText(line, row);
                else
                {
                    ConfigurationText(deletion.Name, row);
                    if (deletion.Error != null)
                    {
                        var bounds = new F5Rect(configurationViewport.X, contentY, width, row);
                        if (ConfigurationVisible(bounds)) Text(StatusFirstSentence(status), bounds, .65f);
                        contentY += row + 4;
                    }
                    else ConfigurationText(status, row);
                }
                if (deletion.Error != null)
                {
                    var retry = ConfigurationAction("retry-delete-" + deletion.ProfileId, "重试删除", () =>
                    {
                        host.RetryCompanionDeletion(deletion.OwnerId, deletion.ProfileId);
                        ShowNotice(host.Feedback); Invalidate();
                    }, row);
                    if (retry != null) retry.Hint = deletion.Error;
                }
            }
        }

        private static string StatusFirstSentence(string text)
        {
            int end = text.IndexOf('。');
            return end < 0 ? text : text.Substring(0, end + 1);
        }

        private void FinishConfiguration()
        {
            bodyMaximum = Math.Max(0, (int)Math.Ceiling(contentY + bodyOffset - configurationViewport.Bottom));
            if (bodyOffset > bodyMaximum) { bodyOffset = bodyMaximum; bodyOffsetClamped = true; Invalidate(); }
        }

        private void ConfigurationPair(CompanionProfile profile, CompanionBehavior behavior, CompanionSetting left, CompanionSetting? right)
        {
            float x = configurationViewport.X, width = (configurationViewport.Width - 8) / 2;
            float leftEnd = contentY, rightEnd = contentY;
            ConfigurationRow(profile, behavior, left, x, width, ref leftEnd);
            if (right.HasValue) ConfigurationRow(profile, behavior, right.Value, x + width + 8, width, ref rightEnd);
            contentY = Math.Max(leftEnd, rightEnd);
        }

        private void ConfigurationRow(CompanionProfile profile, CompanionBehavior behavior, CompanionSetting setting,
            float x, float width, ref float y)
        {
            int mode = behavior.SettingValue(setting);
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
                    control.Execute = () => { CloseSupplyPopup(); HotkeyClicked(id, control.Rect); };
                    control.Hint = F5HotkeyHints.Toggle;
                    continue;
                }
                int value = action == "关闭" ? 0 : action == "智能" ? 2 : 1;
                bool configure = action == "配置";
                bool current = !configure && value == mode;
                var button = ConfigurationButton("config-" + setting + "/" + action, action, r,
                    configure ? null : (Action)(() => SetConfigurationSetting(setting, value)),
                    current, current && mode == 0);
                button.Element = element;
                if (configure)
                {
                    button.Execute = () => ToggleSupplyPopup(profile, setting, button);
                    if (supplySetting == setting) supplyAnchor = button;
                }
            }
        }

        private void SetConfigurationSetting(CompanionSetting setting, int value)
        {
            var behavior = CurrentProfile()?.Behavior;
            if (behavior == null || behavior.SettingValue(setting) == value) return;
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
                case CompanionSetting.Nurse: b.AllowNurse = value != 0; break;
                case CompanionSetting.Furniture: b.FurnitureEnabled = value != 0; break;
                case CompanionSetting.Respawn: b.AutoRespawn = value != 0; break;
            }
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

        private Control ConfigurationAction(string key, string text, Action execute, float row)
        {
            var rect = new F5Rect(configurationViewport.X, contentY, Math.Min(configurationViewport.Width, ConfigurationWidth(text)), row);
            var control = ConfigurationVisible(rect) ? ConfigurationButton(key, text, rect, execute) : null;
            contentY += row + 6;
            return control;
        }
    }
}
