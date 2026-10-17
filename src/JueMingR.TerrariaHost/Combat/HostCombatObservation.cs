using System;
using System.IO;
using AimTrace = JueMingR.TerrariaHost.Combat.Prediction.AimLightTrace;
using JueMingR.Features.Combat;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Combat;
using JueMingR.Platform.Runtime;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Npcs;
using Terraria;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed class HostCombatObservation : IRuntimeFeature,F5.ICombatObservationControls
    {
        // Presentation alone uses this measured cadence/age pair. Actual
        // BeforeNpc/control preparation and candidate intent keep their own
        // real-phase timing; any dependency change still invalidates display.
        internal const int DisplayRefreshUpdates=5;
        internal const int DisplayMaximumAge=DisplayRefreshUpdates-1;
        private const int DisplayShortPrefix=32;
        private const int DisplayShortRefreshUpdates=2;
        internal readonly ObservationSettings Settings;
        internal readonly CombatSelection Selection;
        internal readonly NpcPredictionSource Prediction;
        internal readonly CombatGeometry Geometry=new CombatGeometry();
        internal readonly CombatGeometryHooks Hooks;
        internal readonly CombatObservationWorldLayer World;
        internal readonly CombatPredictionWork PredictionWork=new CombatPredictionWork(6);
        internal HostAttackAim Attack;
        private readonly SingleFeatureRuntime runtime;
        private readonly HostInputState input;
        private readonly NativeNpcObservation npcs;
        private bool collisionFailed,pathFailed,reportedCollision,reportedPath,reportedMarker,wasCollision,wasPath,hadTargetDemand;
        private bool selectionFailed;
        // Presentation may keep a short-lived conditional result. It never
        // enters Cache's attack reader and retains the original capture clock.
        private NpcTrajectory displayPath;
        private PredictionTerrain.CellLease displayTerrain;
        private PredictionPlayerMotion displayPlayer;
        private PredictionPlayerMotion displayLeasePlayer;
        private int displayLife;
        // previewOptionalTick is retained only for old read-only probe compatibility; no producer advances it.
        private long displayLeaseTick=-1,optionalTick=-1,previewOptionalTick=-1,displayShortTick=-1;
        private NpcIdentity optionalTarget;
        internal NpcTrajectory DisplayPath {get{return displayLeaseTick==(long)Main.GameUpdateCount && Path && Selection.HasTarget && displayPath!=null && displayPath.Identity.Equals(Selection.Target)?displayPath:null;}}
        internal int DisplayAge {get{return displayPath==null?0:(int)Math.Max(0,(long)Main.GameUpdateCount-displayPath.SampleTick);}}
        internal bool OptionalPreparation {get;private set;}
        // One full identity per native slot bounds local fault retention. A
        // different selected NPC remains usable; slot reuse never inherits it.
        private readonly NpcIdentity[] failedTargets=new NpcIdentity[NpcPredictionCache.Capacity];
        internal bool TargetPredictionFailed {get{return Selection.HasTarget && failedTargets[Selection.Target.Slot].Equals(Selection.Target);}}
        internal Rendering.WorldLayerStatus LayerStatus;
        internal HostCombatObservation(string directory,SingleFeatureRuntime runtime,HostInputState input,NativeNpcObservation npcs,Prediction.PredictionLaunchIdentity launch=null)
            :this(directory,runtime,input,npcs,launch,null){}
        internal HostCombatObservation(string directory,SingleFeatureRuntime runtime,HostInputState input,NativeNpcObservation npcs,Prediction.PredictionLaunchIdentity launch,string package)
            :this(directory,runtime,input,npcs,launch,package,false){}
        internal HostCombatObservation(string directory,SingleFeatureRuntime runtime,HostInputState input,NativeNpcObservation npcs,Prediction.PredictionLaunchIdentity launch,string package,bool exactComparison)
        {
            this.runtime=runtime;this.input=input;this.npcs=npcs;
            AimTrace.Start(directory,launch,package);
            Prediction=new NpcPredictionSource(launch,exactComparison);
            Settings=new ObservationSettings(new AtomicFileDocument(System.IO.Path.Combine(directory,"JueMingRData","config","features","combat-observation.json"),65536));
            Selection=new CombatSelection(npcs);World=new CombatObservationWorldLayer(this);Hooks=new CombatGeometryHooks(this);
            AppDomain.CurrentDomain.ProcessExit+=Exit;
        }
        public ObservationOptions Options {get{return Settings.Value;}}
        public bool CanConfigure {get{return runtime.IsSessionActive && Settings.Ready;}}
        internal bool SelectionAvailable {get{return !selectionFailed;}}
        internal bool PredictionAvailable {get{return !pathFailed;}}
        internal bool PredictionAvailableFor(NpcIdentity identity)
        {return !pathFailed && !failedTargets[identity.Slot].Equals(identity);}
        internal long Session {get{return runtime.IsSessionActive?runtime.Generation:-1;}}
        internal bool Collision {get{return !World.Failed && Settings.CanRun && Options.Collision && Unavailable(0)==null;}}
        internal bool Path {get{return !World.Failed && Settings.CanRun && Options.Path && Unavailable(1)==null;}}
        internal bool Marker {get{return !World.Failed && !World.Marker.Failed && !selectionFailed && Settings.CanRun && Options.Marker && (Path || BasicSelectionDemand) && LayerStatus!=Rendering.WorldLayerStatus.Unavailable;}}
        public string Unavailable(int field)
        {
            if(selectionFailed)return "战斗目标观察暂不可用，设置已保留；可点击开启重试。";
            if(field==6)return selectionFailed?"战斗目标观察暂不可用，设置已保留；可点击开启重试。":pathFailed || Attack?.Failed==true?"预测增强暂不可用，基础瞄准仍可运行；可点击开启重试增强。":null;
            if(LayerStatus==Rendering.WorldLayerStatus.Unavailable)return "世界显示入口不可用，设置已保留；需要重新进入游戏。";
            if(World.Failed && (field==0 || field==1 || field==5))return "战斗显示暂不可用，设置已保留；可点击开启重试。";
            if(field==0 && (!Hooks.Ready || collisionFailed))return "碰撞箱显示暂不可用，设置已保留；可点击开启重试。";
            if(field==1 && (!Hooks.Ready || pathFailed))return "寻路预测暂不可用，设置已保留；可点击开启重试。";
            if(field==5 && World.Marker.Failed)return "目标标记暂不可用，设置已保留；可点击开启重试。";
            return null;
        }
        internal bool Capture {get{return Collision && runtime.IsSessionActive;}}
        internal bool CanDraw {get{return runtime.IsSessionActive && LayerStatus==Rendering.WorldLayerStatus.Ready && input.CanPrepareText;}}
        // Aim still needs a fresh ordinary selection when optional prediction
        // fails or both displays are OFF. Only selection's own failure revokes
        // that source; it never borrows a stale marker as an input target.
        private bool BasicSelectionDemand {get{return Settings.CanRun && Options.Aim && runtime.IsSessionActive;}}
        public bool Enabled {get{return !selectionFailed && (Collision || Path || Marker || BasicSelectionDemand || !pathFailed && Prediction.Cache.Required>0);}}
        public void Set(int field,bool value)
        {
            if(!CanConfigure)return;
            if(field==6 && value)
            {
                selectionFailed=pathFailed=reportedPath=false;Array.Clear(failedTargets,0,failedTargets.Length);Attack?.Reset();
            }
            if(value && (field==0 || field==1 || field==5))selectionFailed=false;
            if(value && (field==0 || field==1 || field==5))World.Recover();
            bool prior=field==0?Options.Collision:field==1?Options.Path:field==2?Options.ClearLine:field==3?Options.MouseCenter:field==4?Options.Dummy:field==5?Options.Marker:Options.Aim;
            if(field==0 && value){collisionFailed=reportedCollision=false;Geometry.Failed=false;}
            if(field==1 && value){pathFailed=reportedPath=false;Array.Clear(failedTargets,0,failedTargets.Length);Attack?.RetryFaults();if(Prediction.Native!=null && Prediction.Native.Failed)Prediction.Native.Retry();}
            if(field==5 && value){reportedMarker=false;World.Marker.Reset();}
            if(prior!=value)Settings.Set(Options.Toggle(field));
            if(field==6 && !value)Attack?.ClearExecution();
        }
        public void Radius(int value){if(CanConfigure && Options.Radius!=value)Settings.Set(Options.WithRadius(value));}
        internal void Poll()
        {
            Settings.Poll();Attack?.Demand();World.PollResources();bool collision=Collision,path=Path;
            if(wasCollision && !collision)Geometry.Clear();
            if(wasPath && !path)Prediction.Cache.Release(0);
            // Other registered consumers can outlive the path toggle. Retire
            // the final target on actual demand removal, once, even when the
            // display was already OFF before that consumer released it.
            bool targetDemand=path || BasicSelectionDemand;
            if(hadTargetDemand && !targetDemand)
            {
                // Saved Marker is a child preference, never a producer demand.
                // Retire only our two consumers and state on the final edge;
                // independently registered prediction consumers remain intact.
                Attack?.Reset();Prediction.Cache.Release(0);Prediction.Cache.Release(1);
                displayPath=null;displayTerrain=null;displayLeasePlayer=default(PredictionPlayerMotion);displayLeaseTick=optionalTick=previewOptionalTick=displayShortTick=-1;OptionalPreparation=false;
                World.Marker.Clear();World.Impact.Clear();
            }
            hadTargetDemand=targetDemand;
            // An independent reader may release after the Path/Aim falling
            // edge already passed. Retire that final target exactly once;
            // an idle OFF Poll must not repeatedly clear Source's arrays.
            if(!targetDemand && Prediction.Cache.Required==0 && Selection.HasTarget){Prediction.Clear();Selection.RetireTarget();}
            // Reliable preference intent is available in the safe Main.Update
            // callback even in menus. This does not activate any Gameplay
            // feature, sample a world, or grant input/operation ownership.
            Prediction.Native?.PollEnvironment(Hooks.Ready && !selectionFailed && !pathFailed && (Settings.CanRun && Options.Path || Prediction.Cache.Required>0),runtime.IsSessionActive);
            wasCollision=collision;wasPath=path;
            AimTrace.Host(this,pathFailed,"poll",false);
        }
        internal void SampleMouse(){if(!selectionFailed && (Path || Marker || BasicSelectionDemand || !pathFailed && Prediction.Cache.Required>0))Selection.SampleMouse(input);}
        internal void CollisionFailed(){collisionFailed=true;Geometry.Clear();}
        public void OnSessionStarted(){Clear();Geometry.Session=runtime.Generation;}
        public void OnSessionEnded(){Clear();}
        private void Clear(){actionFrame=optionalTick=displayLeaseTick=previewOptionalTick=displayShortTick=-1;displayPath=null;displayTerrain=null;displayLeasePlayer=default(PredictionPlayerMotion);OptionalPreparation=false;Attack?.Reset();Geometry.Clear();Prediction.Cache.EndSession();Prediction.EndWorld();Selection.Clear();World.Recover();World.Marker.Reset();Array.Clear(failedTargets,0,failedTargets.Length);selectionFailed=collisionFailed=pathFailed=reportedCollision=reportedPath=reportedMarker=false;}
        public void FailClosed(){Clear();Prediction.Stop();selectionFailed=collisionFailed=pathFailed=true;}
        public void Update(ulong tick)
        {
            // Runtime's callback counter is not the native world clock. All
            // enhancement scopes in this world update share the same GUC,
            // including earlier ItemCheck/AI consumers and final display.
            UpdateSample(Main.GameUpdateCount,false,Main.GameUpdateCount);
        }
        private long actionFrame=-1;
        internal void PrepareAction()
        {
            if(Attack==null || !Attack.BasicPermission || actionFrame==input.Frame)return;
            // Sealed ordinary flights do not read another hand input during
            // cooldown. Completed-world prediction/preview still runs every
            // update; only a real upcoming hand emission needs this earlier
            // phase. Registered finite AI075 controllers request this Source
            // only for their native mouse-read window or a real new opening;
            // other charge/release and hand consumers remain continuous.
            if(!Attack.ActionPreparationNeeded)return;
            actionFrame=input.Frame;
            Attack.BeginActionDemand();
            npcs.BeginActions(input.Frame);
            // ItemCheck runs after this player's movement/selection, before
            // NPCs and projectiles advance. Their live sample is still T-1.
            // One preparation per native input epoch also covers first clicks;
            // individual Shoot/AI consumers only validate/borrow the result.
            UpdateSample((long)Main.GameUpdateCount-1,true,Main.GameUpdateCount);
        }
        private void UpdateSample(long sampleTick,bool beforeNpc,ulong tick)
        {
            using(PredictionWork.Enter(tick))
            {
                try{UpdateSampleCore(sampleTick,beforeNpc,tick);}
                catch(CombatPredictionBudgetExceededException){ /* Basic and completed prefixes survive optional exhaustion. */ }
            }
        }
        private void UpdateSampleCore(long sampleTick,bool beforeNpc,ulong tick)
        {
            try
            {
            Attack?.Demand(beforeNpc);
            if(!Enabled)return;
            if(!Hooks.Ready && !Marker && !BasicSelectionDemand){Selection.RetireTarget();Prediction.Clear();return;}
            var player=Main.LocalPlayer;if(player==null || !player.active || player.dead || player.ghost){Selection.RetireTarget();Prediction.Clear();Geometry.BeginNpcs();return;}
            if(Collision)Geometry.BeginNpcs();
            // Preferred demand records desired coverage, not this synchronous
            // call's work. Actual action prefixes run before optional tails.
            if(Path && !beforeNpc)Prediction.Cache.Demand(0,1,NpcPredictionCache.Horizon);else Prediction.Cache.Release(0);
            try{Selection.Update(Options,Session,Marker || BasicSelectionDemand || !pathFailed && Prediction.Cache.Required>0,Collision?Geometry:null);}catch(Exception error){AimTrace.Fault("host-selection",error,(long)tick);selectionFailed=true;CollisionFailed();pathFailed=true;Selection.RetireTarget();Prediction.Stop();World.Clear();return;}
            // The final-consumer edge already retired our producer. Collision
            // observation can remain active without clearing inactive Source
            // arrays on every empty update. Independent readers still retire
            // their unavailable selected publication normally.
            if(!Selection.HasTarget){if(Prediction.Cache.Required>0)Prediction.Clear();return;}
            if(!beforeNpc)Attack?.RestorePresentationSelection(sampleTick);
            // Validate the existing publication before optional producers can
            // spend this update's remainder. A repeat checks live premises;
            // an unavailable replacement is not evidence against this lease.
            if(!beforeNpc)using(CombatPredictionWork.Optional(3))
                try{ValidateDisplay(sampleTick);}catch(CombatPredictionBudgetExceededException){ }
            bool displayValidated=false;
            NpcTrajectory coreDisplayPrefix=null;
            // Invalidating a lease is immediate; granting production is a
            // separate cadence. Attempts advance this clock even for null or
            // paused results, and a duplicate outer callback cannot re-admit.
            OptionalPreparation=!beforeNpc && Path && optionalTick!=sampleTick && (optionalTick<0 || sampleTick-optionalTick>=DisplayRefreshUpdates || !optionalTarget.Equals(Selection.Target));
            // Held clear-line intent prepares the next real hand during its
            // native cooldown. ResolveSelection keeps its existing cumulative
            // 3ms limit and warm cadence; charging it to display's shared 2ms
            // producer slice can repeatedly pause a reachable candidate before
            // Plan completes. Pure Path hypotheses retain the optional slice.
            bool actionSelection=Attack!=null && Options.ClearLine && Attack.BasicPermission;
            if(beforeNpc || Attack!=null && Attack.UrgentPreparation || actionSelection)Attack?.ResolveSelection(sampleTick,beforeNpc);
            else using(CombatPredictionWork.Optional(2))Attack?.ResolveSelection(sampleTick,false);
            Attack?.Demand(beforeNpc); // Keep a candidate's explicitly proven action prefix.
            // Ordinary prediction is synchronous and owned by Source. Native
            // failure belongs only to the explicit comparison route; a shared
            // entry exception still latches the whole path closed.
            if(TargetPredictionFailed){Prediction.Clear();return;}
            int urgent=Attack!=null && Attack.UrgentPreparation?Attack.ActionPreferred(Selection.Target):0;
            // A newly selected candidate's proven late receive is an actual
            // action need. Keep it when moving from the candidate owner to
            // the selected owner; Path's desired tail never enters this value.
            if(beforeNpc && Attack!=null)urgent=Math.Max(urgent,Attack.ActionRequired);
            if(beforeNpc || urgent>0)
            {
                var priorPublication=beforeNpc?null:Prediction.Cache.Read(0);
                PrepareSelectedPrediction(sampleTick,beforeNpc,tick,Math.Max(1,urgent));
                if(beforeNpc)Attack?.PrepareAction();else Attack?.Prepare();
                var publication=beforeNpc?null:Prediction.Cache.Read(0);
                // Harvest only a new immutable CompletedWorld publication
                // produced by this core pass. An earlier phase or a budget-
                // retained object cannot be relabelled with current life/input.
                if(publication!=null && !ReferenceEquals(publication,priorPublication) && publication.SampleTick==sampleTick)coreDisplayPrefix=publication;
            }
            if(!beforeNpc && OptionalPreparation)
            {
                // Producers share the lower cumulative ceiling. The same
                // owner's remaining optional slice is reserved for validating
                // real repeated outer entries, rather than spent on a tail
                // whose next callback would immediately hide its publication.
                using(CombatPredictionWork.Optional(3))
                {
                    int full=Prediction.Cache.Required;
                    int prefix=Math.Min(full,DisplayShortPrefix);
                    bool admitted=false,preparedPrefix=false;
                    NpcTrajectory candidatePath;PredictionTerrain.CellLease candidateTerrain;
                    if(Attack!=null && Attack.TryCandidateDisplay(sampleTick,out candidatePath,out candidateTerrain) && CombatPredictionWork.TryContinue())
                    {
                        // Optional selection already completed this fresh World
                        // prefix and hand plan. Publish those immutable results
                        // without resampling the same actor after its slice has
                        // gone. Keep any longer display whose real lease passes.
                        bool keep=KeepLongerDisplay(candidatePath,sampleTick);
                        displayValidated=keep;
                        if(!keep)
                        {
                            CaptureDisplay(candidatePath,candidateTerrain);
                            ValidateDisplay(sampleTick);displayValidated=true;
                        }
                        optionalTick=displayShortTick=sampleTick;optionalTarget=Selection.Target;
                        prefix=Math.Min(full,candidatePath.Count-1);admitted=true;
                    }
                    using(CombatPredictionWork.Optional(2))
                    if(!admitted && CombatPredictionWork.TryContinue())
                    {
                        admitted=true;
                        optionalTick=displayShortTick=sampleTick;optionalTarget=Selection.Target;
                        // First display and refresh both earn a useful short
                        // future without attack/Contact or an old path pointer.
                        // Only a validated prefix may be followed by a tail.
                        var priorPublication=Prediction.Cache.Read(0);
                        PrepareSelectedPrediction(sampleTick,false,tick,prefix);
                        preparedPrefix=!ReferenceEquals(priorPublication,Prediction.Cache.Read(0));

                    }
                    // Validation uses the existing reserved remainder after
                    // the lower producer scope has ended, including a prefix
                    // completed exactly as the producer slice ran out.
                    if(preparedPrefix && CombatPredictionWork.TryContinue())CaptureDisplay(sampleTick);
                    if(admitted && CombatPredictionWork.TryContinue())
                    {
                        // No world or producer action follows candidate publication. Reuse
                        // only this call's completed checks; a later tail checks its new lease.
                        if(!displayValidated)ValidateDisplay(sampleTick);
                        displayValidated=true;
                        var prefixPath=displayPath;
                        bool extended=false;
                        using(CombatPredictionWork.Optional(2))
                        if(full>prefix && displayLeaseTick==sampleTick && CombatPredictionWork.TryContinue())
                        {
                            PrepareSelectedPrediction(sampleTick,false,tick,full);
                            extended=true;
                        }
                        if(extended)
                        {
                            if(CombatPredictionWork.TryContinue())
                            {
                                var replacement=Prediction.Cache.Read(0);
                                bool replaces=replacement!=null && replacement.Identity.Equals(Selection.Target) && replacement.SampleTick==sampleTick &&
                                    (prefixPath==null || replacement.Count>=prefixPath.Count-Math.Max(0,sampleTick-prefixPath.SampleTick));
                                if(replaces)CaptureDisplay(sampleTick);
                                try{if(replaces)ValidateDisplay(sampleTick);}
                                catch(CombatPredictionBudgetExceededException)
                                {
                                    // Extending private scratch performs no
                                    // world action. Its exhausted new lease
                                    // cannot revoke the just-validated prefix.
                                    // CaptureDisplay commits only after validation.
                                }

                            }
                        }

                    }
                }
            }
            if(!beforeNpc)
            {
                // Production/first validation remain optional. Repeated leases
                // retain their permit only while live facts still agree.
                using(CombatPredictionWork.Optional(3))
                {
                    try
                    {
                        // Selection and real native premises can change inside
                        // this GUC. Recheck them even after a producer completed.
                        {
                            bool displayCurrent=ValidatePreparedDisplay(sampleTick);
                            // Preserve an already valid long display. When a
                            // real hit/control change retired it, consume the
                            // fresh core prefix without another Source sample
                            // or waiting for the optional tail's due cycle.
                            if(!displayCurrent && coreDisplayPrefix!=null && ReferenceEquals(coreDisplayPrefix,Prediction.Cache.Read(0)))
                            {CaptureDisplay(sampleTick);displayCurrent=ValidateDisplay(sampleTick);if(displayCurrent)displayShortTick=sampleTick;}
                            if(!displayCurrent && Path && !TargetPredictionFailed &&
                                (displayShortTick<0 || sampleTick-displayShortTick>=DisplayShortRefreshUpdates))
                            {
                                bool shortCaptured=false;
                                using(CombatPredictionWork.Optional(2))
                                if(CombatPredictionWork.TryContinue())
                                {
                                    // A real changed display premise earns only
                                    // a bounded prefix, never ClearLine's full
                                    // optional pass or another 120-step tail.
                                    displayShortTick=sampleTick;
                                    var priorPublication=Prediction.Cache.Read(0);
                                    PrepareSelectedPrediction(sampleTick,false,tick,Math.Min(DisplayShortPrefix,Prediction.Cache.Required));
                                    var publication=Prediction.Cache.Read(0);
                                    if(publication!=null && publication.SampleTick==sampleTick && !ReferenceEquals(publication,priorPublication))
                                    {
                                        shortCaptured=true;
                                    }
                                }
                                if(shortCaptured){CaptureDisplay(sampleTick);ValidatePreparedDisplay(sampleTick);}
                            }
                        }

                    }
                    catch(CombatPredictionBudgetExceededException){ /* The last completed validation retains its real clock. */ }
                }
            }
            }
            finally
            {
                // ResolveSelection may replace the target before a budgeted
                // producer exits. Even that exit must retire a changed lease.
                if(!beforeNpc && displayLeaseTick==sampleTick)
                    try{ValidatePreparedDisplay(sampleTick);}catch(CombatPredictionBudgetExceededException){ }
                Attack?.EndPreparation();AimTrace.Host(this,pathFailed,"update-exit",true);
            }
        }
        private void PrepareSelectedPrediction(long sampleTick,bool beforeNpc,ulong tick,int advanceTo)
        {
            if(!Selection.HasTarget || TargetPredictionFailed)return;
            if(!pathFailed && Prediction.Cache.Required>0 && advanceTo>0)try{if(beforeNpc)Prediction.PrepareActionPrefix(Selection.Target,sampleTick,advanceTo,NpcPredictionCache.Horizon);else Prediction.PreparePrefix(Selection.Target,sampleTick,advanceTo,NpcPredictionCache.Horizon);}
            catch(CombatPredictionBudgetExceededException){ }
            catch(NpcObservationFailure error){AimTrace.Fault("host-npc-observation",error,(long)tick);failedTargets[Selection.Target.Slot]=Selection.Target;Prediction.Clear();}
            catch(Exception error){AimTrace.Fault("host-prepare",error,(long)tick);pathFailed=true;Prediction.Stop();}
        }
        internal bool ExpandActionPrediction()
        {return ExpandControlPrediction(true);}
        internal bool ExpandControlPrediction(bool beforeNpc)
        {
            if(pathFailed || TargetPredictionFailed || !Selection.HasTarget)return false;
            var prior=Prediction.Cache.Read(1);if(prior==null || prior.BudgetPaused || prior.Stop!=PredictionStop.None || prior.Count>NpcPredictionCache.Horizon || !CombatPredictionWork.TryContinue())return false;
            int required=Math.Min(NpcPredictionCache.Horizon,Math.Max(prior.Count+7,(prior.Count-1)*2));
            Attack.SetActionDemand(required);
            PrepareSelectedPrediction((long)Main.GameUpdateCount-(beforeNpc?1:0),beforeNpc,Main.GameUpdateCount,required);
            var result=Prediction.Cache.Read(1);return result!=null && result.Count>prior.Count;
        }
        private void CaptureDisplay(long tick)
        {
            var path=Path?Prediction.Cache.Read(0):null;
            if(path==null || !path.Identity.Equals(Selection.Target) || path.SampleTick!=tick)return;
            if(KeepLongerDisplay(path,tick))return;
            CaptureDisplay(path,Prediction.Terrain.FreezeCells());
        }
        private bool KeepLongerDisplay(NpcTrajectory replacement,long tick)
        {
            // A budget-limited refresh must not shrink a still-valid tail.
            // Compare the overlap at absolute future times before retaining
            // it: a real new terminal, segment or changed prediction wins.
            var prior=displayPath;
            if(prior==null || replacement.Stop!=PredictionStop.None || !prior.Identity.Equals(replacement.Identity) ||
                prior.Strategy!=replacement.Strategy || prior.Quality!=replacement.Quality || prior.Assumptions!=replacement.Assumptions || prior.RelationVersion!=replacement.RelationVersion)return false;
            long offset=replacement.SampleTick-prior.SampleTick;
            if(offset<0 || offset>=prior.Count || prior.Count-offset<=replacement.Count)return false;
            for(int i=0;i<replacement.Count;i++)
            {
                var a=prior[(int)offset+i];var b=replacement[i];
                if(a.Bounds.X!=b.Bounds.X || a.Bounds.Y!=b.Bounds.Y || a.Bounds.Width!=b.Bounds.Width || a.Bounds.Height!=b.Bounds.Height ||
                    a.Vx!=b.Vx || a.Vy!=b.Vy || a.Phase!=b.Phase || a.Wet!=b.Wet || a.NoTileCollide!=b.NoTileCollide || a.NewSegment!=b.NewSegment)return false;
            }
            return ValidateDisplay(tick);
        }
        private void CaptureDisplay(NpcTrajectory path,PredictionTerrain.CellLease terrain)
        {
            if(path==null || terrain==null || !Selection.HasTarget || !path.Identity.Equals(Selection.Target) || !CombatSelection.Valid(path.Identity,Session))return;
            var player=NpcPredictionSource.ReadPlayer(Main.LocalPlayer);int life=Main.npc[path.Identity.Slot].life;
            long tick=(long)Main.GameUpdateCount;
            // A candidate owns no presentation state until ALL its premises
            // pass. Paused validation cannot replace a previously valid path,
            // its original player/life sample or its current visibility lease.
            if(!DisplayFactsCurrent(path,player,life,tick,player) || !terrain.Current)return;
            displayPath=path;displayTerrain=terrain;displayPlayer=displayLeasePlayer=player;displayLife=life;displayLeaseTick=tick;
        }
        private bool ValidateDisplay(long tick)
        {return RevalidateDisplay(tick,true);}
        private bool ValidatePreparedDisplay(long tick)
        {
            // Source and attack preparation touch private forecast state only.
            // The entry already checked these tiles; no native action occurs
            // before this final facts check. A new real entry uses the full
            // terrain guard again, even when GUC has not advanced.
            return RevalidateDisplay(tick,displayLeaseTick!=tick);
        }
        private bool RevalidateDisplay(long tick,bool terrain)
        {
            if(!Path || displayPath==null || !Selection.HasTarget || TargetPredictionFailed){displayLeaseTick=-1;return false;}
            var nativePlayer=Main.LocalPlayer;
            if(nativePlayer==null || !nativePlayer.active || nativePlayer.dead || nativePlayer.ghost){displayLeaseTick=-1;return false;}
            var player=NpcPredictionSource.ReadPlayer(nativePlayer);bool repeated=displayLeaseTick==tick;
            bool changedDuringUpdate=repeated && (displayLeasePlayer.X!=player.X || displayLeasePlayer.Y!=player.Y || displayLeasePlayer.Vx!=player.Vx || displayLeasePlayer.Vy!=player.Vy);
            if(changedDuringUpdate || !DisplayFactsCurrent(displayPath,displayPlayer,displayLife,tick,player) || displayTerrain==null)
            {displayLeaseTick=-1;return false;}
            if(terrain && !displayTerrain.DisplayCurrent(repeated)){displayLeaseTick=-1;return false;}
            displayLeasePlayer=player;displayLeaseTick=tick;return true;
        }
        private bool DisplayFactsCurrent(NpcTrajectory path,PredictionPlayerMotion player,int life,long tick,PredictionPlayerMotion currentPlayer)
        {
            if(!Path || path==null || !Selection.HasTarget || TargetPredictionFailed || !Selection.Target.Equals(path.Identity) || !CombatSelection.Valid(path.Identity,Session))return false;
            long age=tick-path.SampleTick;if(age<0 || age>DisplayMaximumAge || age>=path.Count-1)return false;
            var npc=Main.npc[path.Identity.Slot];var point=path[(int)age];
            return npc.life==life && npc.ai[0]==point.Phase && npc.wet==point.Wet && npc.noTileCollide==point.NoTileCollide && Vector2.DistanceSquared(npc.position,new Vector2(point.Bounds.X,point.Bounds.Y))<=4 && Vector2.DistanceSquared(npc.velocity,new Vector2(point.Vx,point.Vy))<=.01f &&
                DisplayInputCurrent(player,currentPlayer);
        }
        private static bool DisplayInputCurrent(PredictionPlayerMotion a,PredictionPlayerMotion b)
        {
            // This is a bounded-age presentation condition, not a proof of
            // current attack motion. Normal XY/V and countdown advancement
            // do not invalidate it; controls, mechanism and physics changes do.
            return ReferenceEquals(a.PlayerToken,b.PlayerToken) && a.PlayerIndex==b.PlayerIndex && a.ObservationMechanism==b.ObservationMechanism && a.Left==b.Left && a.Right==b.Right && a.Up==b.Up && a.Down==b.Down && a.HoldJump==b.HoldJump && a.HoverUp==b.HoverUp && a.HoverDown==b.HoverDown &&
                a.GravityDirection==b.GravityDirection && a.GravityControl==b.GravityControl && a.GravityControl2==b.GravityControl2 && a.Complex==b.Complex && a.Mounted==b.Mounted && a.Rope==b.Rope && a.Grappled==b.Grappled && a.OnTrack==b.OnTrack && a.Wet==b.Wet && a.Honey==b.Honey && a.Lava==b.Lava && a.Shimmer==b.Shimmer &&
                a.Width==b.Width && a.Height==b.Height && a.Acceleration==b.Acceleration && a.Slowdown==b.Slowdown && a.MaxSpeed==b.MaxSpeed && a.JumpSpeed==b.JumpSpeed && a.DefaultGravity==b.DefaultGravity && a.WaterWalk==b.WaterWalk && a.LavaWalk==b.LavaWalk && a.IgnorePlatforms==b.IgnorePlatforms && a.Vortex==b.Vortex && a.SlowFall==b.SlowFall && a.FloatInWater==b.FloatInWater;
        }
        internal void Register(HotkeyRegistry registry,Hotkeys.HotkeyStateFeedback feedback)
        {
            Action aimCommand=()=>Set(6,!Options.Aim);
            if(feedback!=null)aimCommand=feedback.Committed("combat.aim","辅助瞄准",aimCommand,()=>Settings.CanRun && Options.Aim?1:0,()=>CanConfigure && !selectionFailed,
                ()=>Settings.AcceptedCommandId,()=>Settings.CompletedCommandId,()=>Settings.CompletionSucceeded);
            registry.Register(new HotkeyAction("combat.aim","辅助瞄准",HotkeyContext.Gameplay,()=>CanConfigure,aimCommand));
            for(int i=0;i<F5.CombatObservationControls.Actions.Length;i++)
            {
                int field=i;string id=F5.CombatObservationControls.Actions[i],name=F5.CombatObservationControls.Names[i];
                Action command=()=>Set(field,!(field==0?Options.Collision:Options.Path));
                if(feedback!=null)command=feedback.Committed(id,name,command,()=>field==0?(Collision?1:0):(Path?1:0),()=>CanConfigure && Unavailable(field)==null,
                    ()=>Settings.AcceptedCommandId,()=>Settings.CompletedCommandId,()=>Settings.CompletionSucceeded);
                registry.Register(new HotkeyAction(id,name,HotkeyContext.Gameplay,()=>CanConfigure,command));
            }
        }
        internal void TakeFeedback(Action<string> show)
        {
            Settings.TakeFeedback(show);
            if(Unavailable(5)!=null && Options.Marker && !reportedMarker){reportedMarker=true;show(Unavailable(5));}
            if(Unavailable(0)!=null && Options.Collision && !reportedCollision){reportedCollision=true;show(Unavailable(0));}
            if(Unavailable(1)!=null && Options.Path && !reportedPath){reportedPath=true;show(Unavailable(1));}
            else if(Prediction.Native!=null && Prediction.Native.Failed && Options.Path && !reportedPath)
            {reportedPath=true;show("普通敌怪预测暂不可用，分节预测仍可用；可点击开启重试。");}
        }
        private void Exit(object sender,EventArgs e){AppDomain.CurrentDomain.ProcessExit-=Exit;Clear();Prediction.Stop();Hooks.Dispose();Settings.Stop(750);AimTrace.End("process-exit");}
    }
}
