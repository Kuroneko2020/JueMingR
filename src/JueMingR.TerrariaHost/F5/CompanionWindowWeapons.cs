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
        private struct SelectionIdentity
        {
            internal Guid Profile;
            internal long Session, Generation, Epoch;
            internal Player Body;
        }
        private sealed class WeaponDraft
        {
            internal SelectionIdentity Identity;
            internal readonly List<CompanionWeaponPreference> Choices = new List<CompanionWeaponPreference>(CompanionBehavior.MaxMainWeapons);
        }

        private WeaponDraft weaponDraft;
        private CompanionProfile weaponProfile;
        private CompanionWeaponPreference[] savedWeapons = Array.Empty<CompanionWeaponPreference>();
        private readonly int[] displayedWeaponSlots = { -1, -1 };
        private static readonly string[] WeaponCountLabels = { "已选 0/2", "已选 1/2", "已选 2/2" };
        private int activeWeaponSlot = -1, weaponCountLabel = -1;

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
            if (draft != null && !SelectionCurrent(draft.Identity))
                CancelWeaponSelection();
        }

        private bool SelectionCurrent(SelectionIdentity draft) => draft.Profile == selected && page == 0 &&
            storage == CompanionInventoryArea.Inventory &&
            host.WeaponSelectionCurrent(draft.Profile, draft.Session, draft.Generation, draft.Epoch, draft.Body);

        private SelectionIdentity? CaptureSelection()
        {
            var profile = CurrentProfile();
            if (profile == null || !host.CanTransfer(profile.Id)) return null;
            var body = host.GetBody(profile.Id);
            var draft = new SelectionIdentity
            {
                Profile = profile.Id, Body = body, Epoch = profile.DataEpoch,
                Session = host.CreationSessionToken, Generation = host.ActivationGeneration(profile.Id)
            };
            return SelectionCurrent(draft) ? draft : (SelectionIdentity?)null;
        }

        private void BeginWeaponSelection()
        {
            SelectStorage(CompanionInventoryArea.Inventory, false);
            if (Main.mouseItem?.IsAir == false)
            { ShowNotice("请先放回鼠标上的物品。"); return; }
            var identity = CaptureSelection();
            if (!identity.HasValue) return;
            var draft = new WeaponDraft { Identity = identity.Value };
            var weapons = SavedWeapons();
            HostCompanions.MapWeaponSlots(draft.Identity.Body, weapons, displayedWeaponSlots);
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
            var identity = draft.Identity;
            if (host.TrySetMainWeapons(identity.Profile, identity.Session, identity.Generation, identity.Epoch, identity.Body, draft.Choices.ToArray()))
                CancelWeaponSelection();
            else ShowNotice(host.Feedback);
        }

        private void ToggleWeapon(Control hit)
        {
            if (hit?.WeaponCandidate != true) return;
            var choices = weaponDraft.Choices;
            int prior = choices.FindIndex(choice => choice.SlotHint == hit.Slot);
            if (prior >= 0) choices.RemoveAt(prior);
            else
            {
                if (choices.Count == CompanionBehavior.MaxMainWeapons) { ShowNotice("最多选择两把主要武器。"); return; }
                var item = weaponDraft.Identity.Body.inventory[hit.Slot];
                if (!CompanionItemRules.WeaponEligible(item)) return;
                choices.Add(new CompanionWeaponPreference(item.type, item.prefix, hit.Slot));
            }
            notice = null;
        }

        private bool WeaponToolbarEligible(Player body)
        { return body != null && selectedTransfer && !body.ghost; }

        private string WeaponCount() => WeaponCountLabels[weaponDraft.Choices.Count];

        private void DrawWeaponMarkers(Control control)
        {
            if (control.WeaponCandidate) DrawFrame(control.Rect, Color.LightSteelBlue * .85f);
            if (control.ActiveWeapon)
                Fill(new F5Rect(control.Rect.X + 7, control.Rect.Bottom - 4, control.Rect.Width - 14, 2), Color.Gold);
        }
    }
}
