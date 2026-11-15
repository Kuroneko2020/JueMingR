using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Runtime;
using JueMingR.Infrastructure.Companions;
using JueMingR.TerrariaHost.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Net;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions : IRuntimeFeature,IDisposable
    {
        internal sealed class Actor
        {
            internal CompanionProfile Profile;internal Player Body,Previous;
            internal CompanionConnection Connection;internal long Generation;
            internal bool Ready,SpawnSent,Closing;internal string Status="加入中";
            internal uint LastUpdate=UInt32.MaxValue,NextSave;internal Vector2 Aim;
            internal NPC Target;internal int TargetGeneration;
            internal JueMingR.Platform.Combat.NpcTrajectory AimTimeline;internal PredictionTerrain.CellLease AimTerrain;internal long AimRevision;
            internal CompanionBehaviorHost Behavior=new CompanionBehaviorHost();
            internal bool WaitingPassword,HasAim;internal Item AimWeapon;internal Vector2 AimOrigin,AimTarget,AimVelocity;
            internal bool Dirty,Faulted,WasUsing;
            internal bool DeathObserved;
            internal int[] SyncType,SyncStack,SyncPrefix,SyncBuffType,SyncBuffTime;
            internal int[] NetworkSlotIds;
            internal int SyncMana=-1;internal uint NextPublish;
            internal readonly SourceFlow WorldSource=new SourceFlow();
            internal readonly Queue<object> PendingStrikes=new Queue<object>();
            internal readonly Dictionary<int,PredictedNpc> NpcViews=new Dictionary<int,PredictedNpc>();
        }
        private readonly CompanionProfiles profiles;
        private readonly CompanionSteamBridge steam=new CompanionSteamBridge();
        private readonly Dictionary<Guid,Actor> actors=new Dictionary<Guid,Actor>();
        private readonly Dictionary<Player,Actor> bodies=new Dictionary<Player,Actor>();
        private readonly Dictionary<int,Actor> slots=new Dictionary<int,Actor>();
        private readonly Dictionary<Guid,Player> previews=new Dictionary<Guid,Player>();
        private readonly Dictionary<Guid,string> states=new Dictionary<Guid,string>();
        private readonly HashSet<Guid> resetAfterRecall=new HashSet<Guid>();
        private readonly NpcPredictionSource prediction=new NpcPredictionSource();
        private readonly JueMingR.Features.Combat.CombatPredictionWork work=new JueMingR.Features.Combat.CombatPredictionWork(2);
        private object world;private Player owner;private long session;private int aimTurn;private uint aimStep;
        private Actor planningActor;
        private string password="";private bool failed;
        private string candidatePassword,acceptedPassword;private object passwordConnection;
        internal string Feedback {get;private set;}="";
        internal IReadOnlyList<CompanionProfile> Profiles {get{return profiles.Profiles;}}
        internal string ConnectionPassword {get{return password;}set{password=value??"";foreach(var a in actors.Values)if(a.WaitingPassword && a.Connection!=null && password.Length!=0){a.Connection.Send(CompanionConnection.Packet(38,w=>w.Write(password)));a.WaitingPassword=false;a.Status="正在验证密码";}}}
        public bool Enabled {get{return !failed;}}
        internal HostCompanions(string gameDirectory)
        {
            profiles=new CompanionProfiles(new FileCompanionArchive(gameDirectory));
            try{CompanionHooks.Install(this);CompanionProfiles.InstallSerializationHooks(CompanionHooks.Harmony);}
            catch(Exception e){CompanionHooks.Uninstall();failed=true;Feedback="AI队友原生接口不可用："+e.GetBaseException().Message;}
            AppDomain.CurrentDomain.ProcessExit+=Exit;
        }
        internal Player GetBody(Guid id)
        {
            Actor a;if(actors.TryGetValue(id,out a))return a.Body;
            Player p;if(previews.TryGetValue(id,out p))return p;var profile=Find(id);if(profile==null)return null;
            try{p=profiles.LoadBody(profile);previews[id]=p;return p;}catch(Exception e){Feedback=e.Message;return null;}
        }
        internal string Status(Guid id){if(profiles.SaveFailed(id))return "保存失败，请重试";Actor a;string state;return actors.TryGetValue(id,out a)?a.Status:states.TryGetValue(id,out state)?state:"已收回";}
        internal bool CanTransfer(Guid id){Actor a;return actors.TryGetValue(id,out a) && a.Ready && !a.Closing && a.Body.active && !a.Body.dead && ReferenceEquals(owner,Main.LocalPlayer) && ReferenceEquals(world,Main.ActiveWorldFileData);}
        private CompanionProfile Find(Guid id){return Profiles.FirstOrDefault(p=>p.Id==id);}
        private bool Result(bool value,string text){Feedback=text;return value;}
        internal bool Create(string name)
        {
            if(Main.gameMenu || owner==null)return Result(false,"请先进入世界。");
            try{profiles.Create(name,Main.GameMode==3);return Result(true,"正在保存新人物。");}catch(Exception e){return Result(false,e.Message);}
        }
        internal bool Join(Guid id)
        {
            if(failed || Main.gameMenu || owner==null || !ReferenceEquals(owner,Main.LocalPlayer))return Result(false,"当前不能加入队友。");
            if(actors.ContainsKey(id))return Result(false,"该人物已加入或正在加入。");var profile=Find(id);if(profile==null)return Result(false,"人物不存在。");
            long reservation=0;
            try
            {
                long generation;if(!profiles.TryReserve(profile,out generation))return Result(false,profiles.Error);
                reservation=generation;
                var body=profiles.LoadBody(profile);
                if((body.difficulty==3)!=(Main.GameMode==3)){profiles.Release(profile,generation);return Result(false,"旅行人物与当前世界模式不兼容。");}
                var a=new Actor{Profile=profile,Body=body,Generation=generation,Aim=body.Center};actors.Add(id,a);previews.Remove(id);
                if(Main.netMode==0)
                {
                    int slot=-1;for(int i=0;i<Main.maxPlayers;i++)if(i!=Main.myPlayer && !Main.player[i].active){slot=i;break;}
                    if(slot<0)throw new InvalidOperationException("没有空闲玩家槽位。");
                    Attach(a,slot);Spawn(a,PlayerSpawnContext.SpawningIntoWorld);profiles.BeginPlay(body);a.Ready=true;a.Status="跟随中";
                }
                else if(Main.netMode==1)
                {
                    var endpoint=Netplay.Connection?.Socket?.GetRemoteAddress() as TcpAddress;
                    a.Connection=endpoint==null?steam.Open():new CompanionConnection(endpoint.Address.ToString(),endpoint.Port);a.Connection.Start();a.Connection.Send(CompanionConnection.Packet(1,w=>w.Write("Terraria326")));
                }
                else throw new InvalidOperationException("当前运行模式不支持队友。");
                return Result(true,"正在加入人物。");
            }
            catch(Exception e){Actor a;if(actors.TryGetValue(id,out a))Release(a,false);else if(reservation!=0)profiles.Release(profile,reservation);return Result(false,e.Message);}
        }
        private void Attach(Actor a,int slot)
        {
            if(slot<0 || slot>=Main.maxPlayers || slot==Main.myPlayer || actors.Values.Any(x=>x!=a && x.Body.whoAmI==slot && x.Ready))throw new InvalidOperationException("服务器分配了冲突的玩家槽位。");
            a.Previous=Main.player[slot];a.Body.whoAmI=slot;a.Body.active=true;a.Body.ghost=false;Main.player[slot]=a.Body;
            a.NetworkSlotIds=NetworkSlots(a.Body);
            bodies.Add(a.Body,a);slots.Add(slot,a);
        }
        internal bool Recall(Guid id)
        {
            Actor a;if(!actors.TryGetValue(id,out a))return Result(false,"人物已离线。");
            try{a.Closing=true;a.Body.controlUseItem=false;a.Body.channel=false;RetireProjectiles(a);profiles.EndPlay(a.Body);a.Profile=profiles.CaptureAndSave(a.Profile.Id,a.Body,a.Generation);Detach(a);a.Status="正在保存并收回";return Result(true,"正在保存并收回人物。");}
            catch(Exception e){a.Closing=false;a.Status="保存失败";return Result(false,e.Message);}
        }
        private void Detach(Actor a)
        {
            a.Connection?.Dispose();a.Connection=null;a.Ready=false;var p=a.Body;
            if(p.whoAmI>=0 && p.whoAmI<Main.maxPlayers && ReferenceEquals(Main.player[p.whoAmI],p))
            {p.active=false;if(a.Previous!=null){a.Previous.active=false;Main.player[p.whoAmI]=a.Previous;}}
            bodies.Remove(p);Actor current;if(slots.TryGetValue(p.whoAmI,out current) && ReferenceEquals(current,a))slots.Remove(p.whoAmI);
            RetireObserver(a);a.NpcViews.Clear();a.PendingStrikes.Clear();
        }
        private void Release(Actor a,bool saved)
        {
            Detach(a);var p=a.Body;
            profiles.Release(a.Profile,a.Generation);actors.Remove(a.Profile.Id);previews[a.Profile.Id]=p;states[a.Profile.Id]=saved?"已收回":"加入失败";
            if(saved && resetAfterRecall.Remove(a.Profile.Id)){profiles.Reset(a.Profile.Id);previews.Remove(a.Profile.Id);Feedback="人物已清空，正在保存。";}
            if(actors.Count==0){prediction.EndWorld();CompanionThreatField.Reset();aimTurn=0;}
        }
        internal bool Reset(Guid id)
        {
            if(actors.ContainsKey(id)){resetAfterRecall.Add(id);if(!Recall(id)){resetAfterRecall.Remove(id);return false;}return Result(true,"正在收回并清空人物。");}var p=Find(id);if(p==null)return false;
            try{profiles.Reset(id);previews.Remove(id);return Result(true,"人物已清空，正在保存。");}catch(Exception e){return Result(false,e.Message);}
        }
        internal bool RestoreBehavior(Guid id){return SetBehavior(id,new CompanionBehavior());}
        internal bool SaveFailed(Guid id){return profiles.SaveFailed(id);}
        internal bool RetrySave(Guid id){try{profiles.RetrySave(id);return Result(true,"正在重试保存。");}catch(Exception e){return Result(false,e.Message);}}
        internal bool SetBehavior(Guid id,CompanionBehavior behavior)
        {
            var p=Find(id);if(p==null)return false;try{var revised=profiles.SetBehavior(id,behavior);Actor a;if(actors.TryGetValue(id,out a))a.Profile=revised;return Result(true,"行为设置已更新。");}catch(Exception e){return Result(false,e.Message);}
        }
        internal bool SelectWeapon(Guid id,int slot)
        {
            var p=GetBody(id);if(!CanTransfer(id) || slot<0 || slot>=50)return Result(false,"当前不能选择武器。");
            if(p.itemAnimation>0 || p.channel){p.controlUseItem=false;p.channel=false;return Result(false,"请等待本次用物结束。");}
            p.selectedItemState.Select(slot);return Result(true,"已选择使用槽位。");
        }
        public void OnSessionStarted(){world=Main.ActiveWorldFileData;owner=Main.LocalPlayer;session++;password=ReferenceEquals(passwordConnection,Netplay.Connection)?acceptedPassword??"":"";}
        internal void HumanPassword(byte[] frame)
        {
            using(var s=new System.IO.MemoryStream(frame,3,frame.Length-3,false))using(var r=new System.IO.BinaryReader(s)){candidatePassword=r.ReadString();passwordConnection=Netplay.Connection;acceptedPassword=null;}
        }
        internal void HumanAccepted(){if(ReferenceEquals(passwordConnection,Netplay.Connection)){acceptedPassword=candidatePassword;candidatePassword=null;}}
        public void OnSessionEnded()
        {
            foreach(var id in actors.Keys.ToArray())if(!actors[id].Closing)Recall(id);owner=null;world=null;password="";prediction.EndWorld();CompanionThreatField.Reset();steam.Dispose();ClearWorldSources();
        }
        public void Update(ulong updateTick)
        {
            profiles.Pump();foreach(var closing in actors.Values.Where(a=>a.Closing).ToArray())if(profiles.IsSaved(closing.Profile.Id))Release(closing,true);if(!String.IsNullOrEmpty(profiles.Error))Feedback=profiles.Error;if(failed)return;
            steam.Pump();
            if(Main.gameMenu || owner==null || !ReferenceEquals(world,Main.ActiveWorldFileData) || !ReferenceEquals(owner,Main.LocalPlayer))return;
            FlushSourceRequests();RequestSharedTerrain();
            bool cloud;var path=Onboarding.CharacterIdentity.Path(out cloud);if(path!=null)profiles.SetOwner(Onboarding.CharacterIdentity.Key(path,cloud));
            if(actors.Count==0)return;
            var active=actors.Values.Where(a=>a.Ready && !a.Closing).ToArray();
            planningActor=active.Length==0?null:active[aimTurn++%active.Length];
            foreach(var a in actors.Values.ToArray())
            {
                try
                {
                    if(a.Closing)continue;
                    if(a.Connection!=null){PumpConnection(a);if(a.Connection.Finished){states[a.Profile.Id]=a.Connection.Error??"连接已结束";Recall(a.Profile.Id);continue;}}
                    if(a.Ready && a.Dirty && !profiles.SaveFailed(a.Profile.Id) && Main.GameUpdateCount>=a.NextSave){a.Profile=profiles.CaptureAndSave(a.Profile.Id,a.Body,a.Generation);a.Dirty=false;a.NextSave=Main.GameUpdateCount+300;}
                }
                catch(Exception e){a.Status=e.Message;Feedback=e.Message;}
            }
        }
        public void FailClosed(){OnSessionEnded();failed=true;}
        private void Exit(object sender,EventArgs args){Dispose();}
        // CLR shutdown never reads native objects. Normal game-thread leaving
        // captures characters first; disposal only drains accepted bytes/locks.
        public void Dispose(){AppDomain.CurrentDomain.ProcessExit-=Exit;steam.Dispose();profiles.Dispose();}
        internal Actor Owned(Player p){Actor a;return p!=null && bodies.TryGetValue(p,out a) && p.whoAmI>=0 && p.whoAmI<Main.maxPlayers && ReferenceEquals(Main.player[p.whoAmI],p)?a:null;}
        internal Actor OwnedSlot(int slot){Actor a;return slots.TryGetValue(slot,out a) && ReferenceEquals(Owned(a.Body),a)?a:null;}
    }
}
