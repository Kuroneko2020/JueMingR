using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Platform.Companions;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private static readonly (CompanionSetting Setting,string Name,int Maximum)[] ChatSettings =
        {
            (CompanionSetting.Follow,"跟随主人",1),(CompanionSetting.Attack,"自动攻击",1),
            (CompanionSetting.Life,"生命恢复",2),(CompanionSetting.Mana,"魔力恢复",1),
            (CompanionSetting.Buff,"自动增益",1),(CompanionSetting.Growth,"自动成长",1),
            (CompanionSetting.Respawn,"自动复活",1),(CompanionSetting.Nurse,"护士治疗",1),
            (CompanionSetting.Furniture,"使用家具",1),(CompanionSetting.AutoTeleportEnabled,"自动传送",1),
            (CompanionSetting.FishingEquipment,"自动配装",1),(CompanionSetting.FishingLoadout,"自动换装",1),
            (CompanionSetting.FishingStorage,"自动存放鱼",2),(CompanionSetting.FishingCut,"切杆跳过",1),
            (CompanionSetting.FishingTeleport,"传送后停止钓鱼",1)
        };

        private sealed class ChatSettingWrite
        {
            internal ChatRun Run;
            internal ChatResult Result;
            internal CompanionSetting Setting;
            internal int Value;
            internal long Revision;
        }
        private readonly List<ChatSettingWrite> chatSettingWrites = new List<ChatSettingWrite>();

        private CompanionTaskOutcome ReadChatSettings(TaskExecution execution)
        {
            var profile=execution.Actor.Profile;
            return TaskOutcome("succeeded",String.Join("；",ChatSettings.Select(entry=>
                entry.Name+" ("+entry.Setting+")="+(entry.Setting==CompanionSetting.Follow ? EffectiveFollow(profile) : profile.SettingValue(entry.Setting))+
                "，"+ChatSettingValueLabels(entry.Setting))));
        }

        private static string ChatSettingValueLabels(CompanionSetting setting)
        {
            switch(setting)
            {
                case CompanionSetting.Life: return "0 关闭，1 快速，2 智能";
                case CompanionSetting.FishingStorage: return "0 关闭，1 所有，2 任务鱼";
                default: return "0 关闭，1 开启";
            }
        }

        private void SetChatSetting(ChatCommand command,Actor actor,string key,int value)
        {
            var setting=ChatSettings.FirstOrDefault(entry=>String.Equals(entry.Name,key,StringComparison.Ordinal) ||
                String.Equals(entry.Setting.ToString(),key,StringComparison.OrdinalIgnoreCase));
            if (setting.Name==null || value<0 || value>setting.Maximum) { RejectChatTool(command,"设置名称或值不可用，请先查询队友设置。"); return; }
            if (!command.Run.Authority.Setting(setting.Name,value)) { RejectChatTool(command,ChatAuthority.Denied); return; }
            var profile=actor.Profile;
            var result=NewChatAction(command,"修改"+setting.Name,"setting:"+setting.Setting+":"+value);
            if (result==null) return;
            long prior=SettingRevision(profile.Id,setting.Setting);
            var preferences=profile.Behavior;
            bool accepted;
            if (setting.Setting==CompanionSetting.Follow) accepted=ApplyFollowSetting(profile.OwnerId,profile.Id,value!=0,false);
            else if (CompanionBehavior.IsFishingSetting(setting.Setting)) accepted=SetFishingControl(profile.OwnerId,profile.Id,setting.Setting,value);
            else if (setting.Setting==CompanionSetting.AutoTeleportEnabled)
                accepted=SetAutoTeleportSetting(profile.OwnerId,profile.Id,session,profile.DataEpoch,setting.Setting,value);
            else
            {
                switch(setting.Setting)
                {
                    case CompanionSetting.Attack: preferences.AttackEnabled=value!=0; break;
                    case CompanionSetting.Life:
                        preferences.HealthRecoveryEnabled=value!=0;
                        if (value!=0) preferences.HealthRecoveryMode=(CompanionHealthRecoveryMode)value;
                        break;
                    case CompanionSetting.Mana: preferences.ManaRecoveryEnabled=value!=0; break;
                    case CompanionSetting.Buff: preferences.BuffEnabled=value!=0; break;
                    case CompanionSetting.Growth: preferences.GrowthEnabled=value!=0; break;
                    case CompanionSetting.Respawn: preferences.AutoRespawn=value!=0; break;
                    case CompanionSetting.Nurse: preferences.AllowNurse=value!=0; break;
                    case CompanionSetting.Furniture: preferences.FurnitureEnabled=value!=0; break;
                }
                accepted=profile.SettingValue(setting.Setting)==value || ApplyBehavior(profile.Id,preferences,manual:false);
            }
            if (!accepted) { CompleteChatAction(command,result,"failed","设置未受理。"+Feedback); return; }
            long revision=SettingRevision(profile.Id,setting.Setting);
            if (revision==prior) { CompleteChatAction(command,result,"succeeded","该设置已是指定值。"); return; }
            chatSettingWrites.Add(new ChatSettingWrite { Run=command.Run,Result=result,Setting=setting.Setting,Value=value,Revision=revision });
            CompleteChatAction(command,result,"running","指定设置已生效，等待档案写入。",waitForNative:true);
        }

        private void PumpChatSettingWrites()
        {
            for(int index=chatSettingWrites.Count-1;index>=0;index--)
            {
                var pending=chatSettingWrites[index];
                var boundary=pending.Run.Boundary;
                var profile=SettingProfile(boundary.Owner,boundary.Profile);
                bool current=profile!=null && profile.DataEpoch==boundary.DataEpoch;
                bool superseded=current && profile.SettingValue(pending.Setting)!=pending.Value;
                bool saved=false;
                if (current && !superseded && CompletedSettingSave(boundary.Profile,pending.Setting,out saved)<pending.Revision) continue;
                chatSettingWrites.RemoveAt(index);
                pending.Result.AwaitingNativeSettlement=false;
                pending.Run.Conversation.PendingActions--;
                string state=!current || superseded?"unconfirmed":saved?"succeeded":"failed";
                string text=!current?"已离开原档案，未能确认本次保存。":superseded?"当前已有更新的设置值，未能确认原值保存。":
                    saved?"设置已写入队友档案。":"档案保存失败，当前设置留在内存，请重试保存。";
                UpdateChatAction(pending.Run,pending.Result,state,text,recover:true);
            }
        }
    }
}
