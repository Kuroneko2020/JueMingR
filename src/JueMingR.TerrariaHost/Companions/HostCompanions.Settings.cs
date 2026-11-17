using System;
using JueMingR.Platform.Companions;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal long CreationSessionToken { get { return session; } }
        internal Guid LastCreatedId { get; private set; }
        internal string RespawnStatus(Guid id)
        {
            Actor a;
            if(!actors.TryGetValue(id,out a) || !a.Ready || a.Closing || a.Profile.PermanentlyDead || a.PermanentDeathPending || !a.Body.dead)return "";
            if(!a.Profile.Behavior.AutoRespawn)return "自动复活已关闭";
            int seconds=(Math.Max(0,a.Body.respawnTimer)+59)/60;
            return seconds==0?"等待复活":"复活："+seconds+"秒";
        }
        internal bool SetTeam(Guid id,int team)
        {
            if(team<0 || team>5 || owner==null || !ReferenceEquals(owner,Main.ActivePlayerFileData?.Player) || !ReferenceEquals(world,Main.ActiveWorldFileData))return Result(false,"当前不能更改队伍。");
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
                a.Profile=profiles.CaptureAndSave(a.Profile.Id,a.Body,a.Generation);
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
        private static bool ManualWeaponValid(Player body,CompanionBehavior behavior)
        {
            int slot=behavior.ManualWeaponSlot;
            if(slot<0 || slot>=50 || slot>=body.inventory.Length)return false;
            var item=body.inventory[slot];return item!=null && !item.IsAir && item.type==behavior.ManualWeaponType && item.prefix==behavior.ManualWeaponPrefix;
        }
        internal void ValidateManualWeapon(Guid id)
        {
            Actor a;if(!actors.TryGetValue(id,out a) || a.Closing)return;
            var behavior=a.Profile.Behavior;if(behavior.ManualWeaponSlot<0 || ManualWeaponValid(a.Body,behavior))return;
            behavior.ManualWeaponSlot=-1;behavior.ManualWeaponType=behavior.ManualWeaponPrefix=0;
            a.Profile=profiles.SetBehavior(id,behavior);a.Dirty=true;
        }
        private void RestoreManualWeapon(Actor a)
        {
            var behavior=a.Profile.Behavior;
            if(behavior.ManualWeaponSlot<0)return;
            if(ManualWeaponValid(a.Body,behavior))a.Body.selectedItemState.Select(behavior.ManualWeaponSlot);
            else ValidateManualWeapon(a.Profile.Id);
            // Selection is applied only after native join/revival or an explicit
            // user choice. No per-frame Select disturbs use/channel animations.
        }
    }
}
