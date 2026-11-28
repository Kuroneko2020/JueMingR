using System;
using System.Collections.Generic;
using JueMingR.Features.Fishing;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Hotkeys;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal const string FishingDepositUnconfirmedMessage = "这名队友有尚未确认的储存结果，暂不能加入或转移物品。";
        internal long CreationSessionToken { get { return session; } }
        internal Guid LastCreatedId { get; private set; }
        internal CompanionProfile SettingProfile(Guid ownerId, Guid id)
        {
            var profile = Find(id);
            return profile?.OwnerId == ownerId ? profile : null;
        }
        internal long SettingRevision(Guid id, CompanionSetting setting) => profiles.SettingRevision(id, setting);
        internal long CompletedSettingSave(Guid id, CompanionSetting setting, out bool succeeded) =>
            profiles.CompletedSettingRevision(id, setting, out succeeded);

        internal bool RecallPending(Guid id) => actors.TryGetValue(id,out var actor) && actor.Closing && Find(id)!=null;

        internal bool CanChangeCompanionPresence(Guid id, bool recall, out string reason)
        {
            var profile = Find(id);
            reason = failed || profile == null ? "当前人物不可用。" : null;
            if (reason != null) return false;
            if (actors.TryGetValue(id, out var actor))
            {
                // A pending connection remains cancelable by the actual
                // Recall command. Once closing, its save/retry owns the body.
                reason = actor.Closing && SaveFailed(id) ? "保存失败，请在配置页重试保存。" :
                    !recall || actor.Closing ? Status(id) : null;
                return recall && !actor.Closing;
            }
            reason = recall ? "人物已收回。" : profile.FishingDepositUnconfirmedSlots != 0 ? FishingDepositUnconfirmedMessage :
                profile.PermanentlyDead ? "永久死亡人物无法加入。" :
                profiles.SaveFailed(id) ? Status(id) : null;
            return reason == null;
        }

        internal void ToggleCompanionSetting(Guid ownerId, Guid id, CompanionSetting setting)
        {
            var profile = Find(id);
            if (profile?.OwnerId != ownerId) return;
            if (setting == CompanionSetting.Follow && IsFishing(ownerId, id)) return;
            if (setting == CompanionSetting.FishingMode)
            {
                SetFishing(ownerId, id, !IsFishing(ownerId, id));
                return;
            }
            var behavior = profile.Behavior;
            switch (setting)
            {
                case CompanionSetting.Follow: SetManualFollow(ownerId,id,EffectiveFollow(profile)==0); return;
                case CompanionSetting.Attack: behavior.AttackEnabled = !behavior.AttackEnabled; break;
                // Off preserves the last mode, so the same chord can resume it.
                case CompanionSetting.Life: behavior.HealthRecoveryEnabled = !behavior.HealthRecoveryEnabled; break;
                case CompanionSetting.Mana: behavior.ManaRecoveryEnabled = !behavior.ManaRecoveryEnabled; break;
                case CompanionSetting.Buff: behavior.BuffEnabled = !behavior.BuffEnabled; break;
                case CompanionSetting.Growth: behavior.GrowthEnabled = !behavior.GrowthEnabled; break;
                case CompanionSetting.Nurse: behavior.AllowNurse = !behavior.AllowNurse; break;
                case CompanionSetting.Furniture: behavior.FurnitureEnabled = !behavior.FurnitureEnabled; break;
                case CompanionSetting.Respawn: behavior.AutoRespawn = !behavior.AutoRespawn; break;
                case CompanionSetting.AutoTeleportEnabled: behavior.AutoTeleportEnabled = !behavior.AutoTeleportEnabled; break;
                case CompanionSetting.FishingTeleport: behavior.TeleportStopsFishing = !behavior.TeleportStopsFishing; break;
                case CompanionSetting.FishingEquipment:
                case CompanionSetting.FishingStorage:
                case CompanionSetting.FishingCut:
                case CompanionSetting.FishingLoadout:
                    behavior.Fishing = behavior.Fishing.Toggle(FishingFeature(setting));
                    break;
                default: return;
            }
            if (!SetBehavior(id, behavior) && !String.IsNullOrEmpty(Feedback))
                Main.NewText(Feedback, Microsoft.Xna.Framework.Color.Salmon);
        }

        internal bool SetFishingControl(Guid ownerId, Guid id, CompanionSetting setting, int value)
        {
            var profile = SettingProfile(ownerId, id);
            if (profile == null) return false;
            if (setting == CompanionSetting.FishingMode)
                return SetFishing(ownerId, id, value != 0);
            if (profile.SettingValue(setting) == value) return true;
            var behavior = profile.Behavior;
            if (setting == CompanionSetting.FishingTeleport) behavior.TeleportStopsFishing = value != 0;
            else behavior.Fishing = behavior.Fishing.Change(FishingFeature(setting), value);
            return SetBehavior(id, behavior);
        }

        internal bool SetAutoTeleportSetting(Guid ownerId, Guid id, long expectedSession, long expectedDataEpoch,
            CompanionSetting setting, int value)
        {
            var profile = SettingProfile(ownerId, id);
            if (expectedSession != session || profile == null || profile.DataEpoch != expectedDataEpoch)
                return Result(false, "队友设置已失效。");

            // Open editors can outlive unrelated inventory and preference
            // revisions. Merge this one value into the current profile only.
            var behavior = profile.Behavior;
            switch (setting)
            {
                case CompanionSetting.AutoTeleportEnabled: behavior.AutoTeleportEnabled = value == 1; break;
                case CompanionSetting.AutoTeleportBlockedEnabled: behavior.AutoTeleportBlockedEnabled = value == 1; break;
                case CompanionSetting.AutoTeleportDistanceEnabled: behavior.AutoTeleportDistanceEnabled = value == 1; break;
                case CompanionSetting.AutoTeleportBlockedSeconds: behavior.AutoTeleportBlockedSeconds = value; break;
                case CompanionSetting.AutoTeleportDistanceTiles: behavior.AutoTeleportDistanceTiles = value; break;
                default: return false;
            }
            if (value != behavior.SettingValue(setting) || behavior.AutoTeleportBlockedSeconds <= 0 ||
                behavior.AutoTeleportDistanceTiles <= 0)
                return Result(false, "自动传送设置数值无效。");
            return value == profile.SettingValue(setting) || SetBehavior(id, behavior);
        }

        private static int FishingFeature(CompanionSetting setting)
        {
            switch (setting)
            {
                case CompanionSetting.FishingEquipment: return 2;
                case CompanionSetting.FishingStorage: return 3;
                case CompanionSetting.FishingCut: return 4;
                case CompanionSetting.FishingLoadout: return 1;
                default: throw new ArgumentOutOfRangeException(nameof(setting));
            }
        }

        internal bool SetFishingPreferences(Guid ownerId, Guid id, FishingOptions expected, FishingOptions options)
        {
            var profile = SettingProfile(ownerId, id);
            // A retired pane or earlier scope cannot overwrite a subsequent
            // fishing edit, even when the character is still present.
            if (profile == null || !ReferenceEquals(profile.Fishing, expected)) return false;
            var behavior = profile.Behavior;
            behavior.Fishing = options;
            return SetBehavior(id, behavior);
        }

        internal bool FishingPreferencesSaved(Guid id, FishingOptions expected) =>
            ReferenceEquals(Find(id)?.Fishing, expected) && profiles.IsSaved(id);

        internal bool CanUseFishingPreferences(Guid id) =>
            Find(id) != null && !RecallPending(id);
        internal string RespawnStatus(Guid id)
        {
            var profile=Find(id);if(profile==null)return "";
            Actor a;
            if (profile.PermanentlyDead || actors.TryGetValue(id, out a) && a.PermanentDeathPending)
                return "已永久死亡";
            if (!actors.TryGetValue(id, out a) || !a.Ready || a.Closing || !a.Body.dead) return "";
            int seconds=(Math.Max(0,a.Body.respawnTimer)+59)/60;
            bool automatic = a.Preferences.AutoRespawn;
            if (seconds == 0) return automatic ? "等待复活" : "自动复活已关闭，等待手动开启";
            string remaining = "复活倒计时 " + seconds + " 秒";
            return automatic ? remaining : remaining + " · 自动复活已关闭";
        }
        internal bool SetTeam(Guid id,int team)
        {
            if(team<0 || team>5 || !ArchiveIdentityCurrent())return Result(false,"当前不能更改队伍。");
            var profile=Find(id);if(profile==null)return Result(false,"未找到自己的队友。");
            Actor a;
            if(!actors.TryGetValue(id,out a))
            {
                try {profiles.SetOfflineTeam(id,team);return Result(true,"队伍已更新。");}
                catch(Exception e){return Result(false,e.Message);}
            }
            if(a.Closing || !profiles.OwnsActivation(id,a.Generation))return Result(false,"当前不能更改队伍。");
            // Native team is the sole value. The pending bit belongs to this
            // actor/connection, never a naked player-slot difference that an
            // old primary echo could turn into a new send.
            a.Body.team=team;a.TeamPending=a.Connection!=null;a.TeamSavePending=true;a.NextTeamPublish=0;a.Dirty=true;
            a.TeamConnection=a.Connection;a.TeamGeneration=a.Generation;a.TeamSession=session;
            Result(true,"队伍已更新。");SaveTeam(a);PublishTeam(a);return true;
        }
        private bool SaveTeam(Actor a)
        {
            try
            {
                CaptureActorState(a);
                a.TeamSavePending=false;a.Dirty=false;ItemCaptureSucceeded(a.Profile.Id);return true;
            }
            catch(Exception e)
            {a.TeamSavePending=true;a.Dirty=true;a.NextSave=Main.GameUpdateCount+300;Feedback="队伍已更改，保存失败："+e.Message;return false;}
        }
        private void PublishTeam(Actor a)
        {
            if(!a.TeamPending)return;
            if(a.Closing || a.TeamSession!=session || a.TeamGeneration!=a.Generation || !ReferenceEquals(a.TeamConnection,a.Connection) || !profiles.OwnsActivation(a.Profile.Id,a.Generation))
            {a.TeamPending=false;return;}
            if(Main.netMode!=1 || Netplay.Connection==null || !Netplay.Connection.IsConnected() || a.Connection==null || a.Connection.Finished || !a.Ready || a.Faulted || Main.GameUpdateCount<a.NextTeamPublish || !ReferenceEquals(Owned(a.Body),a))return;
            a.NextTeamPublish=Main.GameUpdateCount+30;
            try
            {
                using(var scope=CompanionHooks.Enter(a,a.Aim))
                {
                    NetMessage.SendData(45,-1,-1,null,a.Body.whoAmI);
                    // Native SendData can return before reaching SendPacketToServer;
                    // the transport can also reject a stopped connection silently.
                    // This receipt proves only accepted bytes, never server ACK.
                    if(scope.PacketAccepted)a.TeamPending=false;
                }
            }
            catch(Exception e){Feedback="队伍已更改，等待同步："+e.Message;}
        }
        internal int ActiveWeaponSlot(Guid id)
        {
            return Find(id)!=null && actors.TryGetValue(id, out var a) && a.Ready && !a.Closing && !a.Body.dead ? a.Combat.ActiveSlot : -1;
        }

        internal bool WeaponSelectionCurrent(Guid id, long worldSession, long generation, long epoch, Player body)
        {
            return ItemCommandOwnerCurrent() && worldSession == session &&
                actors.TryGetValue(id, out var actor) && ItemActorCurrent(id, actor) &&
                generation == actor.Generation && epoch == actor.Profile.DataEpoch && ReferenceEquals(body, actor.Body);
        }

        internal bool TrySetMainWeapons(Guid id, long worldSession, long generation, long epoch,
            Player body, CompanionWeaponPreference[] chosen)
        {
            if (Main.mouseItem?.IsAir == false || itemDraft != null ||
                !WeaponSelectionCurrent(id, worldSession, generation, epoch, body))
                return Result(false, "队友已变化，请重新选择主要武器。");
            if (chosen == null || chosen.Length > CompanionBehavior.MaxMainWeapons ||
                chosen.Length == 2 && chosen[0].SlotHint == chosen[1].SlotHint)
                return Result(false, "最多选择两把主要武器。");
            foreach (var weapon in chosen)
                if (!WeaponMatches(body, weapon, weapon.SlotHint))
                    return Result(false, "所选物品已变化，请重新选择。");
            var actor = actors[id];
            var preferences = actor.Profile.Behavior;
            preferences.MainWeapons = chosen;
            try
            {
                // Saving preference has no input effect. Only combat can pick
                // an actual slot when the native hand is free.
                CaptureActorState(actor, preferences);
                return Result(true, chosen.Length == 0 ? "未指定主要武器，自动选择背包武器。" : "主要武器已更新。");
            }
            catch (Exception exception) { return Result(false, exception.Message); }
        }

        private static bool WeaponMatches(Player body, CompanionWeaponPreference weapon, int slot)
        {
            if (body == null || slot < 0 || slot >= CompanionBehavior.MainInventorySlots || slot >= body.inventory.Length) return false;
            var item = body.inventory[slot];
            return item != null && item.type == weapon.Type && item.prefix == weapon.Prefix && CompanionItemRules.WeaponEligible(item);
        }

        internal static void MapWeaponSlots(Player body, CompanionWeaponPreference[] weapons, int[] slots)
        {
            slots[0] = slots[1] = -1;
            for (int i = 0; i < weapons.Length; i++)
                if (WeaponMatches(body, weapons[i], weapons[i].SlotHint)) slots[i] = weapons[i].SlotHint;
            // Preserve exact hints first, then deterministically match moved
            // copies without claiming a slot already owned by another preference.
            for (int i = 0; i < weapons.Length; i++)
            {
                if (slots[i] >= 0) continue;
                for (int slot = 0; slot < CompanionBehavior.MainInventorySlots; slot++)
                {
                    if (slot == slots[1 - i] || !WeaponMatches(body, weapons[i], slot)) continue;
                    slots[i] = slot;
                    break;
                }
            }
        }

        private readonly int[] captureWeaponSlots = new int[CompanionBehavior.MaxMainWeapons];
        private void CaptureActorState(Actor actor, CompanionBehavior preferences = null, ulong? fishingDepositUnconfirmedSlots = null)
        {
            var weapons = actor.Preferences.MainWeapons;
            if (preferences == null && weapons.Length != 0)
            {
                MapWeaponSlots(actor.Body, weapons, captureWeaponSlots);
                bool moved = false;
                for (int i = 0; i < weapons.Length; i++) moved |= weapons[i].SlotHint != captureWeaponSlots[i];
                if (moved)
                {
                    var current = new List<CompanionWeaponPreference>(weapons.Length);
                    for (int i = 0; i < weapons.Length; i++)
                        if (captureWeaponSlots[i] >= 0)
                            current.Add(new CompanionWeaponPreference(weapons[i].Type, weapons[i].Prefix, captureWeaponSlots[i]));
                    preferences = actor.Profile.Behavior;
                    preferences.MainWeapons = current.ToArray();
                }
            }
            if (fishingDepositUnconfirmedSlots.HasValue)
            {
                preferences = preferences ?? actor.Profile.Behavior;
                preferences.FishingDepositUnconfirmedSlots |= fishingDepositUnconfirmedSlots.Value;
            }
            // Native inventory and moves/removals of weapon hints are captured
            // in one revision. A failed write retries that same latest body.
            actor.Profile = profiles.CapturePreferencesAndSave(actor.Profile.Id, actor.Body, actor.Generation, preferences,
                actor.TaskChestResolvedDepositSlots);
            actor.TaskChestResolvedDepositSlots = 0;
        }
    }
}
