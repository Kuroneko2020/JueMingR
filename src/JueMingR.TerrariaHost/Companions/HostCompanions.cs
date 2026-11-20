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
using JueMingR.TerrariaHost.Onboarding;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions : IRuntimeFeature,IDisposable
    {
        internal sealed class Actor
        {
            internal CompanionProfile Profile;internal Player Body,Previous;
            private CompanionProfile preferencesSource;
            private CompanionBehavior preferences;
            // The mutable edit copy belongs to CompanionProfile.Behavior. This
            // game-thread snapshot is only for readers and lasts one revision.
            internal CompanionBehavior Preferences
            {
                get
                {
                    if (!ReferenceEquals(preferencesSource, Profile))
                    { preferences = Profile.Behavior; preferencesSource = Profile; }
                    return preferences;
                }
            }
            internal readonly CompanionCombatState Combat = new CompanionCombatState();
            internal readonly CompanionSupplyState Supplies = new CompanionSupplyState();
            internal CompanionConnection Connection;internal long Generation;
            internal PasswordAttempt PasswordAttempt;
            internal bool Ready,SpawnSent,Closing,CloseCaptured;internal string Status="加入中",TerminalFailure;
            internal uint LastUpdate=UInt32.MaxValue,NextSave;internal Vector2 Aim;
            internal NPC Target;internal int TargetGeneration;
            internal JueMingR.Platform.Combat.NpcTrajectory AimTimeline;internal PredictionTerrain.CellLease AimTerrain;internal long AimRevision;
            internal JueMingR.Platform.Combat.PredictionPlayerMotion? AimPlayer;internal uint AimPlayerTick,AimPlayerAnchor;
            internal long LastAimAttempt;
            internal CompanionBehaviorHost Behavior=new CompanionBehaviorHost();
            internal bool HasAim;
            internal bool Dirty,Faulted,WasUsing;
            internal bool DeathObserved,PermanentDeathPending,TileReachPending;
            internal bool TeamPending,TeamSavePending;
            internal uint NextTeamPublish;
            internal long TeamSession,TeamGeneration;
            internal CompanionConnection TeamConnection;
            internal readonly MinionRespawner Minions=new MinionRespawner();
            internal readonly Dictionary<int,WorldItem> PersonalItems=new Dictionary<int,WorldItem>();
            internal uint PersonalItemsStep=UInt32.MaxValue;
            internal readonly Dictionary<int,byte[]> InitialNpc=new Dictionary<int,byte[]>(),InitialNpcBuffs=new Dictionary<int,byte[]>();
            internal readonly Dictionary<int,byte[]> InitialNpcHousing=new Dictionary<int,byte[]>();
            internal readonly Dictionary<int,uint> InitialNpcTicks=new Dictionary<int,uint>();
            internal bool InitialNpcComplete;
            internal int[] SyncType,SyncStack,SyncPrefix,SyncBuffType,SyncBuffTime;
            internal bool[] SyncFavorite;
            internal int SyncLife=int.MinValue,SyncLifeMax=int.MinValue;
            internal int[] NetworkSlotIds;
            internal int ReservedSlot=-1;
            internal long PrimarySlotFence, PrimarySlotDeadline;
            internal bool PrimarySlotConfirmed, PrimarySlotSawJoin, BirthSubmitted;
            internal int SyncMana=-1;internal uint NextPublish;
            internal readonly SourceFlow WorldSource=new SourceFlow();
            internal readonly Queue<object> PendingStrikes=new Queue<object>();
            internal readonly Dictionary<int,PredictedNpc> NpcViews=new Dictionary<int,PredictedNpc>();
        }
        private readonly CompanionProfiles profiles;
        private readonly CompanionSteamBridge steam=new CompanionSteamBridge();
        private readonly Dictionary<Guid,Actor> actors=new Dictionary<Guid,Actor>();
        private Actor joiningFeedback;
        private readonly Dictionary<Player,Actor> bodies=new Dictionary<Player,Actor>();
        private readonly Dictionary<int,Actor> slots=new Dictionary<int,Actor>();
        private readonly Dictionary<Guid,Player> previews=new Dictionary<Guid,Player>();
        private readonly Dictionary<Guid,long> previewRevisions=new Dictionary<Guid,long>();
        private readonly Dictionary<Guid,string> states=new Dictionary<Guid,string>();
        private readonly HashSet<Guid> resetAfterRecall=new HashSet<Guid>();
        private readonly NpcPredictionSource prediction=new NpcPredictionSource();
        private readonly JueMingR.Features.Combat.CombatPredictionWork work=new JueMingR.Features.Combat.CombatPredictionWork(2);
        private object world;private Player owner;private long session;private int planningTurn;private uint planningStep=UInt32.MaxValue,aimStep;
        private Actor planningActor;
        private long aimAttempt;
        private uint aimSelectionStep=UInt32.MaxValue;
        private Actor aimSelected;
        private bool failed;private Guid savingCreation;
        private string ownerArchivePath;
        private bool ownerArchiveCloud;
        private static readonly IReadOnlyList<CompanionProfile> EmptyProfiles = Array.Empty<CompanionProfile>();
        internal string Feedback {get;private set;}="";
        internal IReadOnlyList<CompanionProfile> Profiles => ArchiveIdentityCurrent() ? profiles.Profiles : EmptyProfiles;
        internal bool CanCreate => ArchiveIdentityCurrent() && profiles.CanCreate;
        internal bool CanRetryArchive => ArchiveIdentityCurrent() && profiles.LoadError != null && !profiles.Loading;
        internal string ArchiveNotice => failed ? Feedback :
            !ArchiveIdentityCurrent() ? "当前角色没有可用的本地或云端档案身份。" :
            profiles.Loading ? "正在读取队友档案。" : profiles.Error;
        internal void RetryArchive()
        {
            if (!CanRetryArchive) return;
            Result(true, "");
            profiles.RetryLoad();
        }
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
            var profile=Find(id);if(profile==null)return null;
            Actor a;if(actors.TryGetValue(id,out a))return a.Body;
            Player p;long revision;if(previews.TryGetValue(id,out p) && previewRevisions.TryGetValue(id,out revision) && revision==profile.Revision)return p;
            try{p=profiles.LoadBody(profile);previews[id]=p;previewRevisions[id]=profile.Revision;return p;}catch(Exception e){Feedback=e.Message;return null;}
        }
        internal string Status(Guid id)
        {
            var profile=Find(id);if(profile==null)return "";
            actors.TryGetValue(id,out var actor);
            if(profiles.SaveFailed(id))return (actor?.TerminalFailure==null?"":actor.TerminalFailure+"；")+"保存失败，请重试";
            return actor!=null?actor.Status:profile.PermanentlyDead?"已永久死亡":states.TryGetValue(id,out var state)?state:"已收回";
        }
        internal bool IsJoined(Guid id){return Find(id)!=null && actors.ContainsKey(id);}
        internal long ActivationGeneration(Guid id){Actor a;return Find(id)!=null && actors.TryGetValue(id,out a)?a.Generation:0;}
        internal bool CanTransfer(Guid id){Actor a;return Find(id)!=null && actors.TryGetValue(id,out a) && a.Ready && !a.Closing && a.Body.active && !a.Body.dead && !a.Profile.PermanentlyDead;}
        private CompanionProfile Find(Guid id){return ArchiveIdentityCurrent()?profiles.Find(id):null;}
        private bool Result(bool value,string text){joiningFeedback=null;Feedback=text;return value;}
        private void RetireJoinFeedback(Actor actor)
        {
            // A progress message belongs to the exact pending activation, not
            // every actor or a timeout. Never erase a newer operation or error.
            if (!ReferenceEquals(joiningFeedback,actor)) return;
            joiningFeedback=null;
            if (Feedback=="正在加入人物。") Feedback="";
        }
        internal bool Create(string name)
        {
            if(!CanCreate)return Result(false,ArchiveNotice??"队友档案尚未就绪。");
            try{return Created(profiles.Create(name,Main.GameMode==3));}catch(Exception e){return Result(false,e.Message);}
        }
        private bool Created(CompanionProfile profile)
        {savingCreation=profile.Id;LastCreatedId=profile.Id;return Result(true,"正在保存新人物。");}
        internal bool Join(Guid id)
        {
            if(failed || !ArchiveIdentityCurrent())return Result(false,"当前不能加入队友。");
            if(actors.ContainsKey(id))return Result(false,"该人物已加入或正在加入。");var profile=Find(id);if(profile==null)return Result(false,"人物不存在。");
            if(profile.PermanentlyDead)return Result(false,"该人物已永久死亡；明确清空人物后可重新创建身体。");
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
                    StartConnectionAttempt(a, null);
                }
                else throw new InvalidOperationException("当前运行模式不支持队友。");
                if(a.Ready)return Result(true,"");
                Result(true,"正在加入人物。");joiningFeedback=a;return true;
            }
            catch(Exception e){Actor a;if(actors.TryGetValue(id,out a)){a.TerminalFailure=e.Message;Release(a,false);}else if(reservation!=0)profiles.Release(profile,reservation);return Result(false,e.Message);}
        }
        private void Attach(Actor a,int slot)
        {
            if(a.Profile.PermanentlyDead)throw new InvalidOperationException("已永久死亡的人物不能加入。");
            if(slot<0 || slot>=Main.maxPlayers || slot==Main.myPlayer || slots.ContainsKey(slot))throw new InvalidOperationException("服务器分配了冲突的玩家槽位。");
            a.Previous=Main.player[slot];a.Body.whoAmI=slot;a.Body.active=true;a.Body.ghost=false;Main.player[slot]=a.Body;
            if(a.NetworkSlotIds==null)a.NetworkSlotIds=NetworkSlots(a.Body);
            bodies.Add(a.Body,a);slots.Add(slot,a);
        }
        internal bool Recall(Guid id)
        {
            Actor a;if(Find(id)==null || !actors.TryGetValue(id,out a) || a.Closing)return Result(false,"人物已离线或正在收回。");
            return CloseActor(a,null);
        }
        private bool CloseActor(Actor a,string failure)
        {
            if(failure!=null)a.TerminalFailure=failure;
            a.Closing=true;a.Body.controlUseItem=false;a.Body.channel=false;
            try
            {
                if(a.PermanentDeathPending){a.Profile=profiles.MarkPermanentDeath(a.Profile.Id,a.Generation);a.PermanentDeathPending=false;}
                RetireProjectiles(a);profiles.EndPlay(a.Body);
                CaptureActorState(a);a.TeamSavePending=false;ItemCaptureSucceeded(a.Profile.Id);a.CloseCaptured=true;
                a.Status=a.TerminalFailure??"正在保存并收回";
                // Success here means the latest snapshot was accepted for
                // closing/retry, not that the original connection succeeded.
                // Its terminal cause remains independently visible after save.
                return Result(true,failure??"正在保存并收回人物。");
            }
            catch(Exception e){a.Status=(a.TerminalFailure==null?"":a.TerminalFailure+"；")+"保存失败："+e.Message;return Result(false,a.Status);}
            finally
            {
                // A terminal flow cannot stay alive while an archive retry is
                // pending. Keep this one latest body/snapshot and activation
                // until accepted bytes are saved; never reconnect or relabel a
                // failure as a successful user recall when the writer completes.
                Detach(a);
            }
        }
        private void Detach(Actor a)
        {
            RetireJoinFeedback(a);
            a.TeamPending=false;a.TeamConnection=null;
            CancelCompanionItemOnce(a);
            CancelItemGestureForActor(a);
            if(ReferenceEquals(planningActor,a))planningActor=null;
            if(ReferenceEquals(aimSelected,a)){aimSelected=null;aimSelectionStep=UInt32.MaxValue;}
            a.Minions.Clear();
            a.PersonalItems.Clear();
            a.InitialNpc.Clear();a.InitialNpcBuffs.Clear();a.InitialNpcTicks.Clear();a.InitialNpcHousing.Clear();
            ReleaseReservedSlot(a);
            RetireConnection(a);a.Connection=null;a.PasswordAttempt=null;a.Ready=false;var p=a.Body;
            if(p.whoAmI>=0 && p.whoAmI<Main.maxPlayers && ReferenceEquals(Main.player[p.whoAmI],p))
            {p.active=false;if(a.Previous!=null){a.Previous.active=false;Main.player[p.whoAmI]=a.Previous;}}
            bodies.Remove(p);Actor current;if(slots.TryGetValue(p.whoAmI,out current) && ReferenceEquals(current,a))slots.Remove(p.whoAmI);
            RetireObserver(a);a.NpcViews.Clear();a.PendingStrikes.Clear();
        }
        private void Release(Actor a,bool saved)
        {
            Detach(a);
            Guid id=a.Profile.Id;
            if(saved && resetAfterRecall.Remove(id))
            {
                try
                {
                    a.Profile=profiles.Reset(id,a.Generation);
                    if(Find(id)!=null)Feedback="人物已清空，正在保存。";
                    a.TerminalFailure=null;
                }
                catch(Exception error) { a.TerminalFailure=error.Message; }
            }
            // Offline presentation is loaded from saved base limits. The
            // former actor still has live equipment/buff-derived maxima.
            profiles.Release(a.Profile,a.Generation);actors.Remove(id);previews.Remove(id);previewRevisions.Remove(id);
            states[id]=a.Profile.PermanentlyDead?"已永久死亡":a.TerminalFailure??(saved?"已收回":"加入失败");
            if(actors.Count==0){prediction.EndWorld();CompanionThreatField.Reset();planningTurn=0;}
        }
        internal bool Reset(Guid id)
        {
            if(Find(id)==null)return false;
            if(actors.TryGetValue(id,out var actor))
            {
                resetAfterRecall.Add(id);
                if(!actor.Closing) CloseActor(actor,null);
                return Result(true,"正在收回并清空人物。");
            }
            try{profiles.Reset(id);previews.Remove(id);states.Remove(id);return Result(true,"人物已清空，正在保存。");}catch(Exception e){return Result(false,e.Message);}
        }
        internal bool RestoreBehavior(Guid id){return SetBehavior(id,new CompanionBehavior());}
        internal bool SaveFailed(Guid id){Actor a;return Find(id)!=null && (HasPendingItemCapture(id) || profiles.SaveFailed(id) || actors.TryGetValue(id,out a) && (a.TeamSavePending || a.Closing && !a.CloseCaptured));}
        internal bool RetrySave(Guid id){if(Find(id)==null)return false;try{Actor a;if(actors.TryGetValue(id,out a) && a.Closing && !a.CloseCaptured)return CloseActor(a,a.TerminalFailure);if(HasPendingItemCapture(id))return RetryItemCapture(id);if(a!=null && a.TeamSavePending)return SaveTeam(a);profiles.RetrySave(id);return Result(true,"正在重试保存。");}catch(Exception e){return Result(false,e.Message);}}
        internal bool SetBehavior(Guid id,CompanionBehavior behavior)
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != itemThread || Find(id) == null) return false;
            try
            {
                if (actors.TryGetValue(id, out var a) && !a.Closing)
                    CaptureActorState(a, behavior);
                else if (a != null) return Result(false, "等待队友收回后再修改设置。");
                else profiles.SetBehavior(id, behavior);
                return Result(true, "");
            }
            catch (Exception e) { return Result(false, e.Message); }
        }
        public void OnSessionStarted(){world=Main.ActiveWorldFileData;owner=Main.ActivePlayerFileData?.Player;session++;BeginPasswordSession();UpdateArchiveIdentity();}
        public void OnSessionEnded()
        {
            RetireArchiveIdentity();
            owner=null;world=null;EndPasswordSession();prediction.EndWorld();CompanionThreatField.Reset();EndRetiringConnectionSession();steam.Dispose();ClearWorldSources();
        }
        private void RetireArchiveIdentity()
        {
            F5.CompanionCreationSession.DisposeFor(this);
            CancelItemGesture();
            foreach(var actor in actors.Values.ToArray())if(!actor.Closing)CloseActor(actor,null);
            ownerArchivePath=null;ownerArchiveCloud=false;
            // The selected roster disappears immediately. Archive I/O and
            // closing activations still own the bytes they already accepted.
            profiles.SetOwner(null);
            savingCreation=Guid.Empty;LastCreatedId=Guid.Empty;Result(true,"");
        }
        internal bool ArchiveIdentityCurrent()
        {
            if (ownerArchivePath==null || Main.gameMenu || owner==null || !ReferenceEquals(world,Main.ActiveWorldFileData) ||
                !ReferenceEquals(owner,Main.ActivePlayerFileData?.Player)) return false;
            string path=CharacterIdentity.Path(out bool cloud);
            return cloud==ownerArchiveCloud && String.Equals(path,ownerArchivePath,StringComparison.Ordinal);
        }
        private void UpdateArchiveIdentity()
        {
            if (failed || Main.gameMenu || world==null || !ReferenceEquals(world,Main.ActiveWorldFileData)) return;
            Player nextOwner=Main.ActivePlayerFileData?.Player;
            string path=CharacterIdentity.Path(out bool cloud);
            if (ReferenceEquals(owner,nextOwner) && String.Equals(path,ownerArchivePath,StringComparison.Ordinal) && cloud==ownerArchiveCloud) return;
            if (ownerArchivePath!=null || !ReferenceEquals(owner,nextOwner))
            {
                RetireArchiveIdentity();
                EndPasswordSession();session++;BeginPasswordSession();
            }
            owner=nextOwner;ownerArchivePath=path;ownerArchiveCloud=cloud;
            if(path!=null)profiles.SetOwner(CharacterIdentity.Key(path,cloud));
        }
        public void Update(ulong updateTick)
        {
            // Native bodies borrow Main.myPlayer in a bounded scope. Such a
            // callback is neither a new human owner nor a missing file identity.
            if(CompanionHooks.CurrentActor!=null)return;
            UpdateArchiveIdentity();
            F5.CompanionCreationSession.Validate(this);
            ValidateItemGesture();
            profiles.Pump();
            if(savingCreation!=Guid.Empty && profiles.IsSaved(savingCreation)){savingCreation=Guid.Empty;if(Feedback=="正在保存新人物。")Feedback="";}
            foreach(var closing in actors.Values.Where(a=>a.Closing && a.CloseCaptured).ToArray())if(profiles.IsSaved(closing.Profile.Id))Release(closing,true);if(failed)return;
            steam.Pump();
            PumpRetiringConnections();
            if(!ArchiveIdentityCurrent())return;
            FlushItemSync();
            FlushSourceRequests();RequestSharedTerrain();
            if(actors.Count==0)return;
            if(planningStep!=Main.GameUpdateCount)
            {
                planningStep=Main.GameUpdateCount;
                var active=actors.Values.Where(a=>a.Ready && !a.Closing && !a.Faulted && a.Body.active && !a.Body.dead && !a.Body.CCed).ToArray();
                // One movement/target-refresh slice per real world step,
                // independent of attacking, channel release or packet phase.
                // Retained navigation prefixes remain usable by every body;
                // fresh aiming opportunities have their separate scheduler.
                planningActor=active.Length==0?null:active[(int)((uint)planningTurn++%(uint)active.Length)];
            }
            foreach(var a in actors.Values.ToArray())
            {
                try
                {
                    if(a.Closing)continue;
                    if(a.Connection!=null){PumpConnection(a);if(a.Connection?.Finished==true){CloseActor(a,a.Connection.Error??"连接已结束");continue;}}
                    UpdatePasswordAttempt(a);
                    PublishTeam(a);
                    if(a.TeamSavePending && Main.GameUpdateCount>=a.NextSave)SaveTeam(a);
                    if(a.Ready && a.Dirty && !a.TeamSavePending && !profiles.SaveFailed(a.Profile.Id) && Main.GameUpdateCount>=a.NextSave){CaptureActorState(a);ItemCaptureSucceeded(a.Profile.Id);a.Dirty=false;a.NextSave=Main.GameUpdateCount+300;}
                }
                catch(Exception e){CloseActor(a,e.Message);}
            }
        }
        public void FailClosed(){OnSessionEnded();failed=true;}
        private void Exit(object sender,EventArgs args){Dispose();}
        // CLR shutdown never reads native objects. Normal game-thread leaving
        // captures characters first; disposal only drains accepted bytes/locks.
        public void Dispose(){AppDomain.CurrentDomain.ProcessExit-=Exit;ClearRetiringConnections();steam.Dispose();profiles.Dispose();}
        internal Actor Owned(Player p){Actor a;return p!=null && bodies.TryGetValue(p,out a) && p.whoAmI>=0 && p.whoAmI<Main.maxPlayers && ReferenceEquals(Main.player[p.whoAmI],p)?a:null;}
        internal Actor OwnedSlot(int slot){Actor a;return slots.TryGetValue(slot,out a) && ReferenceEquals(Owned(a.Body),a)?a:null;}
    }
}
