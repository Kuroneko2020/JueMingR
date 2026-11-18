using System;
using System.Collections.Generic;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Companions;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private sealed class WeaponDraft
        {
            internal Guid Profile;
            internal long Session, Generation, Epoch;
            internal Player Body;
            internal readonly List<CompanionWeaponPreference> Choices = new List<CompanionWeaponPreference>(CompanionBehavior.MaxMainWeapons);
        }

        private WeaponDraft weaponDraft;
        private CompanionProfile weaponProfile;
        private CompanionWeaponPreference[] savedWeapons = Array.Empty<CompanionWeaponPreference>();
        private readonly int[] displayedWeaponSlots = { -1, -1 };
        private int activeWeaponSlot = -1, weaponCountLabel = -1;
        private Control weaponSelectButton;

        private CompanionWeaponPreference[] SavedWeapons()
        {
            var profile = CurrentProfile();
            if (!ReferenceEquals(profile, weaponProfile))
            {
                weaponProfile = profile;
                savedWeapons = profile?.Behavior.MainWeapons ?? Array.Empty<CompanionWeaponPreference>();
            }
            return savedWeapons;
        }

        private void RefreshWeaponSlots(Player body)
        {
            if (weaponDraft == null) HostCompanions.MapWeaponSlots(body, SavedWeapons(), displayedWeaponSlots);
            else
            {
                displayedWeaponSlots[0] = weaponDraft.Choices.Count >= 1 ? weaponDraft.Choices[0].SlotHint : -1;
                displayedWeaponSlots[1] = weaponDraft.Choices.Count == 2 ? weaponDraft.Choices[1].SlotHint : -1;
            }
            activeWeaponSlot = host.ActiveWeaponSlot(selected);
        }

        private void UpdateWeaponDraft()
        {
            var draft = weaponDraft;
            if (draft != null && (draft.Profile != selected || page != 0 ||
                !host.WeaponSelectionCurrent(draft.Profile, draft.Session, draft.Generation, draft.Epoch, draft.Body)))
                CancelWeaponSelection();
        }

        private void BeginWeaponSelection()
        {
            host.CancelItemGesture();
            host.EndItemRepeat();
            rightSlot = null;
            rightRepeat = 0;
            Invalidate();
            if (Main.mouseItem?.IsAir == false)
            { notice = "请先放回鼠标上的物品。"; return; }
            var profile = CurrentProfile();
            if (profile == null || !host.CanTransfer(profile.Id)) return;
            var body = host.GetBody(profile.Id);
            var draft = new WeaponDraft
            {
                Profile = profile.Id, Body = body, Epoch = profile.DataEpoch,
                Session = host.CreationSessionToken, Generation = host.ActivationGeneration(profile.Id)
            };
            if (!host.WeaponSelectionCurrent(draft.Profile, draft.Session, draft.Generation, draft.Epoch, body)) return;
            var weapons = SavedWeapons();
            HostCompanions.MapWeaponSlots(body, weapons, displayedWeaponSlots);
            for (int i = 0; i < weapons.Length; i++)
                if (displayedWeaponSlots[i] >= 0)
                    draft.Choices.Add(new CompanionWeaponPreference(weapons[i].Type, weapons[i].Prefix, displayedWeaponSlots[i]));
            // A draft is tied to the body and data epoch. An ordinary native
            // capture may advance Revision while the same press stays armed.
            weaponDraft = draft;
            notice = null;
        }

        private void CancelWeaponSelection()
        {
            if (weaponDraft == null) return;
            weaponDraft = null;
            Invalidate();
            notice = null;
        }

        private void FinishWeaponSelection()
        {
            var draft = weaponDraft;
            if (draft == null) return;
            if (host.TrySetMainWeapons(draft.Profile, draft.Session, draft.Generation, draft.Epoch, draft.Body, draft.Choices.ToArray()))
                CancelWeaponSelection();
            notice = host.Feedback;
        }

        private void ToggleWeapon(Control hit)
        {
            if (hit?.WeaponCandidate != true) return;
            var choices = weaponDraft.Choices;
            int prior = choices.FindIndex(choice => choice.SlotHint == hit.Slot);
            if (prior >= 0) choices.RemoveAt(prior);
            else
            {
                if (choices.Count == CompanionBehavior.MaxMainWeapons) { notice = "最多选择两把主要武器。"; return; }
                var item = weaponDraft.Body.inventory[hit.Slot];
                if (!CompanionItemRules.WeaponEligible(item)) return;
                choices.Add(new CompanionWeaponPreference(item.type, item.prefix, hit.Slot));
            }
            notice = null;
        }

        private bool WeaponToolbarEligible(Player body)
        { return body != null && selectedTransfer && !body.ghost; }

        private void WeaponToolbar(float row)
        {
            weaponCountLabel = -1;
            weaponSelectButton = null;
            float x = companionView.X + 12, y = bodyView.Y;
            if (weaponDraft == null)
            {
                weaponSelectButton = Button("weapon-select", "选择主要武器", new F5Rect(x, y, ConfigurationWidth("选择主要武器"), row), BeginWeaponSelection);
                RefreshWeaponToolbar(host.GetBody(selected));
                return;
            }
            float doneWidth = ConfigurationWidth("完成"), cancelWidth = ConfigurationWidth("取消");
            Button("weapon-done", "完成", new F5Rect(x, y, doneWidth, row), FinishWeaponSelection);
            Button("weapon-cancel", "取消", new F5Rect(x + doneWidth + 6, y, cancelWidth, row), CancelWeaponSelection);
            weaponCountLabel = labels.Count;
            Text(WeaponCount(), new F5Rect(x + doneWidth + cancelWidth + 14, y,
                Math.Max(0, companionView.Right - (x + doneWidth + cancelWidth + 20)), row), .65f);
        }

        private void RefreshWeaponToolbar(Player body)
        {
            if (weaponSelectButton == null) return;
            weaponSelectButton.Enabled = WeaponToolbarEligible(body);
            weaponSelectButton.Hint = weaponSelectButton.Enabled ? "从队友主背包选择最多两把。金线标记当前使用的武器。" : selectedUnavailable;
        }

        private string WeaponCount() => "已选 " + weaponDraft.Choices.Count + "/2";
        private string WeaponEditingNotice() => weaponDraft == null ? null : weaponDraft.Choices.Count == 0 ?
            "未指定主要武器，自动选择背包武器。" : "点击主背包中的武器进行选择。";

        private void DrawWeaponMarkers(Control control)
        {
            if (control.WeaponCandidate) DrawFrame(control.Rect, Color.LightSteelBlue * .85f);
            if (control.ActiveWeapon)
                Fill(new F5Rect(control.Rect.X + 7, control.Rect.Bottom - 4, control.Rect.Width - 14, 2), Color.Gold);
        }
    }
}
