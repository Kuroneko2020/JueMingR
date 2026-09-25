using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using JueMingR.Features.QuickItems;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Runtime;
using JueMingR.TerrariaHost.Hotkeys;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Items;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.QuickItems
{
    // Owns saved entries and publication, not keyboard sampling or native item
    // lifecycle. At most one intent lives between shared dispatch and admission.
    internal sealed class HostQuickItems : IRuntimeFeature
    {
        internal const string FavoriteAction = "items.keep-favorited.toggle", ToggleAction = "items.quick-items.toggle";
        internal readonly QuickItemSettings Settings;
        internal readonly QuickItemUse Use;
        internal readonly HostItems Items;
        internal readonly HostInputState Input;
        internal readonly SingleFeatureRuntime Runtime;
        internal readonly int ThreadId = Thread.CurrentThread.ManagedThreadId;
        internal Func<bool> CanGameplay;
        internal KeepFavorited.HostKeepFavorited Favorite;
        private HostHotkeys hotkeys;
        private DynamicHotkeyOwner actions;
        private long published = -1;
        private string requested;
        private long requestRevision;
        private readonly Queue<string> retiredBindings = new Queue<string>();
        private readonly HashSet<string> registered = new HashSet<string>(StringComparer.Ordinal);
        private string cleaning;
        private readonly HashSet<string> failedCleanup=new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> cleanupErrors=new HashSet<string>(StringComparer.Ordinal);
        private long cleanupCommand;
        private bool cleanupFeedback, publicationFeedback, reportedFavoriteUnavailable, reportedQuickUnavailable, useFeedback;
        private long failedPublication=-1;
        private long rejectedUseOperation=-1;
        private string publicationError, requestFeedback, requestMessage;
        internal enum FeedbackKind { Success, NotExecuted, Error }
        private sealed class UseError
        {
            internal long Operation;
            internal string Text;
            internal bool Reported;
        }
        private readonly Dictionary<string,UseError> useErrors=new Dictionary<string,UseError>(StringComparer.Ordinal);
        internal bool CanRetryCleanup {get{return failedCleanup.Count!=0 && hotkeys!=null && !hotkeys.Bindings.Busy && !hotkeys.Bindings.Protected;}}
        internal string Message { get; set; }
        internal bool Available { get; private set; }
        internal Exception SetupError { get; private set; }
        public bool Enabled { get { return true; } } // Drain in-flight work even when the preference is off.
        internal HostQuickItems(string directory, SingleFeatureRuntime runtime, HostItems items, HostInputState input)
        {
            Runtime = runtime; Items = items; Input = input;
            Settings = new QuickItemSettings(new AtomicFileDocument(Path.Combine(directory,"JueMingRData","config","features","quick-items.json"),65536,true));
            Use = new QuickItemUse(this);
            try { QuickItemHooks.Install(this); Available = true; }
            catch (Exception error) { SetupError=error; QuickItemHooks.Uninstall(); Message = "快捷物品暂不可用，原版使用方式不受影响。"; }
            items.AllowsOwnedUse = slot => Use.InNativeUse && Use.Slot == slot;
            AppDomain.CurrentDomain.ProcessExit += Exit;
        }
        internal bool ControlsEnabled { get { return Available && Settings.Loaded && !Settings.Protected && !Settings.Busy; } }
        internal bool FavoriteControlsEnabled {get{return Favorite!=null && Favorite.Available && Settings.Loaded && !Settings.Protected && !Settings.Busy;}}
        internal bool FavoriteAvailable {get{return Favorite!=null && Favorite.Available;}}
        internal string FavoriteError {get{return !FavoriteAvailable?"保持收藏暂不可用，原版收藏方式不受影响。":null;}}
        internal string FavoriteHint {get{return FavoriteError??Settings.FavoriteMessage;}}
        internal string QuickHint
        {
            get
            {
                if(!Available)return "快捷物品暂不可用，原版使用方式不受影响。";
                if(Settings.QuickMessage!=null)return Settings.QuickMessage;
                if(publicationError!=null)return publicationError;
                if(cleanupErrors.Count!=0)return "条目已停止执行，但旧快捷键清理未完成。可重试清理。";
                foreach(var error in useErrors.Values)return error.Text;
                return requestMessage;
            }
        }
        internal long BindingRevision {get{return hotkeys==null || !hotkeys.Bindings.Loaded?-1:hotkeys.Bindings.CompletionId;}}
        internal string BindingText(string action) {return hotkeys?.Bindings.Get(action)?.DisplayText??"+";}
        internal Player Player { get { return Runtime.IsSessionActive && Thread.CurrentThread.ManagedThreadId == ThreadId ? Items.World.Player : null; } }
        internal void Register(HotkeyRegistry registry)
        {
            registry.Register(new HotkeyAction(FavoriteAction,"保持收藏",HotkeyContext.Gameplay,()=>FavoriteControlsEnabled,()=>ToggleFavorite()));
            registry.Register(new HotkeyAction(ToggleAction,"快捷物品开关",HotkeyContext.Gameplay,()=>ControlsEnabled,()=>ToggleQuick()));
            actions = registry.CreateDynamicOwner("items.quick-use.");
        }
        internal void Attach(HostHotkeys value) { hotkeys = value; Poll(); }
        internal void ToggleFavorite() { if(!FavoriteControlsEnabled)return;string reason; if (!Settings.TryChange(Settings.Current.Toggles(!Settings.KeepFavorited,Settings.Current.Enabled),null,out reason,changeQuick:false)) Feedback(FeedbackKind.NotExecuted,reason); }
        internal void ToggleQuick() { string reason; if (!Settings.TryChange(Settings.Current.Toggles(Settings.Current.KeepFavorited,!Settings.Enabled),null,out reason,changeFavorite:false)) Feedback(FeedbackKind.NotExecuted,reason); }
        internal bool Save(QuickItemEntry entry)
        {
            string reason;
            if (entry.Target >= ItemID.Count) { Feedback(FeedbackKind.NotExecuted,"此物品不属于当前原版版本。"); return false; }
            try { if (Settings.TryChange(Settings.Current.Change(entry),entry.Id,out reason)) { Poll(); return true; } }
            catch (Exception e) when (e is ArgumentException || e is Platform.Settings.PreferenceFormatException) { reason="快捷物品已达到数量上限。"; }
            Feedback(FeedbackKind.NotExecuted,reason); return false;
        }
        internal bool Delete(string id)
        {
            string reason; bool result=Settings.TryChange(Settings.Current.Change(null,id),id,out reason);
            if (!result) Feedback(FeedbackKind.NotExecuted,reason); else { Use.CancelEntry(id); Poll(); } return result;
        }
        internal void Poll()
        {
            Settings.Poll();
            // Runtime Poll publishes saved enablement after input dispatch, so
            // this source-observation gate is ready BEFORE its first mapped frame.
            Input.UseGesture.Enabled=Available && Settings.Enabled;
            if (actions==null || published==Settings.Revision) { CleanRetiredBinding(); return; }
            var next=new List<HotkeyAction>(); var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var entry in Settings.Current.Entries)
            {
                if (entry.Target>=ItemID.Count) continue;
                var captured=entry;
                next.Add(new HotkeyAction(entry.ActionId,"快捷使用："+SafeName(entry.Target),HotkeyContext.Gameplay,
                    ()=>Available && Settings.CanExecute(captured.Id),gesture=>Request(captured.Id,gesture),()=>Settings.Registered(captured.Id)));
                ids.Add(entry.ActionId);
            }
            string reason;
            if (!actions.TryReplace(next,out reason))
            { publicationError=reason; if(failedPublication!=Settings.Revision){failedPublication=Settings.Revision;publicationFeedback=true;} return; }
            publicationError=null;publicationFeedback=false;failedPublication=-1;
            // Persisted paused entries retain their conflict reservation. Only
            // reliable deletion releases it; otherwise a failed domain write
            // could let a second action steal its chord and corrupt next load.
            foreach(string old in registered)
                if (!ids.Contains(old) && Settings.Current.Find(old.Substring("items.quick-use.".Length))==null) retiredBindings.Enqueue(old);
            registered.RemoveWhere(id=>Settings.Current.Find(id.Substring("items.quick-use.".Length))==null);
            foreach(string id in ids) registered.Add(id);
            foreach(string id in new List<string>(useErrors.Keys))if(Settings.Current.Find(id)==null)useErrors.Remove(id);
            published=Settings.Revision; CleanRetiredBinding();
        }
        private void CleanRetiredBinding()
        {
            if(cleaning!=null && hotkeys.Bindings.CompletionId==cleanupCommand)
            {
                if(!hotkeys.Bindings.CompletionSucceeded){failedCleanup.Add(cleaning);cleanupErrors.Add(cleaning);cleanupFeedback=true;}
                else cleanupErrors.Remove(cleaning);
                cleaning=null;
            }
            if (hotkeys==null || retiredBindings.Count==0 || hotkeys.Bindings.Busy || !hotkeys.Bindings.Loaded) return;
            string id=retiredBindings.Dequeue(), reason; long command;
            if (!hotkeys.Bindings.TryRemoveRetired(actions,id,out command,out reason)) {failedCleanup.Add(id);cleanupErrors.Add(id);cleanupFeedback=true;}
            else {cleaning=id;cleanupCommand=command;}
        }
        internal void RetryCleanup(){if(!CanRetryCleanup)return;foreach(string id in failedCleanup)retiredBindings.Enqueue(id);failedCleanup.Clear();CleanRetiredBinding();}
        private void Request(string id,HotkeyChord gesture)
        {
            // Even a busy/missing-provider shortcut must not fall through into
            // an ordinary attack. Foreign input remains mapped for admission.
            Input.ClaimUseGesture(gesture);
            Use.RetireCompleted();
            if (requested!=null || Use.Active) { Feedback(FeedbackKind.NotExecuted,"上一件物品仍在使用，请结束后重新按键。"); return; }
            requestMessage=null;
            requested=id; requestRevision=Settings.Revision;
        }
        internal void AfterDispatch(bool permitted)
        {
            string id=requested; requested=null;
            if (id==null || !permitted || requestRevision!=Settings.Revision || !Settings.CanExecute(id)) return;
            Use.TryStart(Settings.Current.Find(id));
        }
        // These are presentation events, never admission or recovery permits.
        // Manual pre-emption is a quiet non-execution; a fresh missing-provider
        // request may alert once. Unknown use errors survive unrelated success.
        internal void Feedback(FeedbackKind kind,string text=null,string id=null,bool notify=true)
        {
            Message=text;
            if(kind==FeedbackKind.Success)
            {
                requestMessage=null;
                UseError prior;
                if(id!=null && useErrors.TryGetValue(id,out prior) && prior.Operation!=Use.Operation)useErrors.Remove(id);
            }
            else if(kind==FeedbackKind.NotExecuted)
            {
                requestMessage=notify?text:null;
                if(notify && (id==null || rejectedUseOperation!=Use.Operation))
                {requestFeedback=text;if(id!=null)rejectedUseOperation=Use.Operation;}
            }
            else if(id!=null)
            {
                UseError prior;
                if(!useErrors.TryGetValue(id,out prior) || prior.Operation!=Use.Operation)
                {useErrors[id]=new UseError{Text=text,Operation=Use.Operation};useFeedback=true;}
            }
        }
        internal void TakeFeedback(Action<string> display)
        {
            Settings.TakeFeedback(display);
            if(!FavoriteAvailable && !reportedFavoriteUnavailable){display(FavoriteError);reportedFavoriteUnavailable=true;}
            if(FavoriteAvailable)reportedFavoriteUnavailable=false;
            if(!Available && !reportedQuickUnavailable){display("快捷物品暂不可用，原版使用方式不受影响。");reportedQuickUnavailable=true;}
            if(Available)reportedQuickUnavailable=false;
            if(publicationFeedback){display(publicationError);publicationFeedback=false;}
            if(cleanupFeedback){display("条目已停止执行，但旧快捷键清理失败。可重试清理；其它按键不受影响。");cleanupFeedback=false;}
            if(useFeedback)
            {foreach(var error in useErrors.Values)if(!error.Reported){display(error.Text);error.Reported=true;}useFeedback=false;}
            if(requestFeedback!=null){display(requestFeedback);requestFeedback=null;}
        }
        internal static string SafeName(int type)
        {
            string name=type>0 && type<ItemID.Count ? Lang.GetItemNameValue(type) : "未知物品";
            if (String.IsNullOrEmpty(name)) name="物品 "+type;
            return name.Length>72 ? name.Substring(0,72) : name;
        }
        public void OnSessionStarted() { requested=null; Use.Retire(); ClearUseFeedback(); }
        public void OnSessionEnded() { requested=null; Use.Retire(); ClearUseFeedback(); }
        private void ClearUseFeedback(){useErrors.Clear();useFeedback=false;requestMessage=requestFeedback=null;}
        public void Update(ulong tick) { if(Player==null) Use.Retire(); }
        public void FailClosed() { Available=false; requested=null; Use.Cancel(); Message="快捷物品已停止；正在归还原版选择。"; }
        private void Exit(object sender,EventArgs args) { AppDomain.CurrentDomain.ProcessExit-=Exit; Settings.Stop(750); }
    }
}
