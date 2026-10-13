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
        private int displayLife;
        private long displayLeaseTick=-1,optionalTick=-1,previewOptionalTick=-1;
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
            if(field==6)return selectionFailed?"战斗目标观察暂不可用，设置已保留；可点击开启重试。":pathFailed || Attack?.Failed==true?"预测增强暂不可用，基础瞄准仍可运行；可点击开启重试增强。":World.Impact.Failed?"预计命中位置显示暂不可用，辅助瞄准仍可运行；可点击开启重试显示。":null;
            if(LayerStatus==Rendering.WorldLayerStatus.Unavailable)return "世界显示入口不可用，设置已保留；需要重新进入游戏。";
            if(World.Failed && (field==0 || field==1 || field==5))return "战斗显示暂不可用，设置已保留；可点击开启重试。";
            if(field==0 && (!Hooks.Ready || collisionFailed))return "碰撞箱显示暂不可用，设置已保留；可点击开启重试。";
            if(field==1 && (!Hooks.Ready || pathFailed))return "NPC寻路预测暂不可用，设置已保留；可点击开启重试。";
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
                bool displayOnly=Options.Aim && World.Impact.Failed && !selectionFailed && !pathFailed && Attack?.Failed!=true;
                World.Impact.Reset();
                // Retrying an optional red display must not revoke a healthy
                // in-flight source capability (including a released bubble).
                if(!displayOnly){selectionFailed=pathFailed=reportedPath=false;Array.Clear(failedTargets,0,failedTargets.Length);Attack?.Reset();}
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
                displayPath=null;displayTerrain=null;displayLeaseTick=optionalTick=-1;OptionalPreparation=false;
                World.Marker.Clear();World.Impact.Clear();
                if(Prediction.Cache.Required==0){Prediction.Clear();Selection.RetireTarget();}
            }
            hadTargetDemand=targetDemand;
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
        private void Clear(){actionFrame=optionalTick=displayLeaseTick=-1;displayPath=null;displayTerrain=null;OptionalPreparation=false;Attack?.Reset();Geometry.Clear();Prediction.Cache.EndSession();Prediction.EndWorld();Selection.Clear();World.Recover();World.Marker.Reset();Array.Clear(failedTargets,0,failedTargets.Length);selectionFailed=collisionFailed=pathFailed=reportedCollision=reportedPath=reportedMarker=false;}
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
            // phase. Controllers, charge/release and pure hand consumers keep
            // NextAction's existing continuous contract.
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
            // A repeated real entry may follow native work in this same GUC.
            // Withdraw the old publication before any budgeted producer can
            // exit early; only the final consumer validation grants it again.
            if(!beforeNpc){displayLeaseTick=-1;Attack?.InvalidatePreviewPresentation();}
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
            bool displayValidated=false,previewValidated=false;
            // Invalidating a lease is immediate; granting production is a
            // separate cadence. Attempts advance this clock even for null or
            // paused results, and a duplicate outer callback cannot re-admit.
            OptionalPreparation=!beforeNpc && Path && optionalTick!=sampleTick && (optionalTick<0 || sampleTick-optionalTick>=DisplayRefreshUpdates || !optionalTarget.Equals(Selection.Target));
            if(beforeNpc || Attack!=null && Attack.UrgentPreparation)Attack?.ResolveSelection(sampleTick,beforeNpc);
            else using(CombatPredictionWork.Optional(3))Attack?.ResolveSelection(sampleTick,false);
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
                PrepareSelectedPrediction(sampleTick,beforeNpc,tick,Math.Max(1,urgent));
                if(beforeNpc)Attack?.PrepareAction();else Attack?.Prepare();
            }
            if(!beforeNpc && OptionalPreparation)
            {
                using(CombatPredictionWork.Optional(3))
                {
                    if(CombatPredictionWork.TryContinue())
                    {
                        optionalTick=sampleTick;optionalTarget=Selection.Target;
                        int full=Prediction.Cache.Required;
                        int prefix=Attack!=null && Attack.PreviewDemand?Math.Min(full,Attack.PreviewPreferred):full;
                        // A display tail cannot consume the allowance before
                        // its useful contact. Preserve the same-sample prefix,
                        // solve its preview, then extend only with the remainder.
                        PrepareSelectedPrediction(sampleTick,false,tick,prefix);
                        CaptureDisplay(sampleTick);
                        previewOptionalTick=sampleTick;
                        Attack?.PreparePreviewOnly();
                        ValidateDisplay(sampleTick);Attack?.ValidatePreviewPresentation();
                        displayValidated=previewValidated=true;
                        var prefixPath=displayPath;var prefixTerrain=displayTerrain;
                        long prefixLease=displayLeaseTick;
                        if(full>prefix && CombatPredictionWork.TryContinue())
                        {
                            PrepareSelectedPrediction(sampleTick,false,tick,full);
                            if(CombatPredictionWork.TryContinue())
                            {
                                CaptureDisplay(sampleTick);
                                try{ValidateDisplay(sampleTick);}
                                catch(CombatPredictionBudgetExceededException)
                                {
                                    // Extending private scratch performs no
                                    // world action. Its exhausted new lease
                                    // cannot revoke the just-validated prefix.
                                    displayPath=prefixPath;displayTerrain=prefixTerrain;displayLeaseTick=prefixLease;
                                }
                                if(Attack!=null && Attack.PreviewNeedsMore && CombatPredictionWork.TryContinue()){Attack.PreparePreviewOnly();previewValidated=false;}
                            }
                        }
                    }
                }
            }
            if(!beforeNpc)
            {
                // Validation is optional work too, after actual attack owners.
                // Each real entry checks one current lease once; exhaustion
                // withdraws display and cannot skip a legal core preparation.
                using(CombatPredictionWork.Optional(3))
                {
                    try
                    {
                        if(!displayValidated)ValidateDisplay(sampleTick);if(!previewValidated)Attack?.ValidatePreviewPresentation();
                        if(Attack!=null && Attack.PreviewRefreshPending && DisplayPath!=null && (previewOptionalTick<0 || sampleTick-previewOptionalTick>=2) && CombatPredictionWork.TryContinue())
                        {
                            previewOptionalTick=sampleTick;
                            Attack.PreparePreviewWindow(DisplayPath);
                            Attack.ValidatePreviewPresentation();
                        }
                    }
                    catch(CombatPredictionBudgetExceededException){displayLeaseTick=-1;}
                }
            }
            }
            finally{Attack?.EndPreparation();AimTrace.Host(this,pathFailed,"update-exit",true);}
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
            displayPath=path;displayLeaseTick=-1;displayTerrain=Prediction.Terrain.FreezeCells();displayPlayer=NpcPredictionSource.ReadPlayer(Main.LocalPlayer);displayLife=Main.npc[path.Identity.Slot].life;
        }
        private bool ValidateDisplay(long tick)
        {
            displayLeaseTick=-1;
            if(!Path || displayPath==null || !Selection.HasTarget || !Selection.Target.Equals(displayPath.Identity) || !CombatSelection.Valid(displayPath.Identity,Session))return false;
            long age=tick-displayPath.SampleTick;if(age<0 || age>DisplayMaximumAge || age>=displayPath.Count)return false;
            var npc=Main.npc[displayPath.Identity.Slot];var point=displayPath[(int)age];
            if(npc.life!=displayLife || npc.ai[0]!=point.Phase || npc.wet!=point.Wet || npc.noTileCollide!=point.NoTileCollide || Vector2.DistanceSquared(npc.position,new Vector2(point.Bounds.X,point.Bounds.Y))>4 || Vector2.DistanceSquared(npc.velocity,new Vector2(point.Vx,point.Vy))>.01f ||
                !DisplayInputCurrent(displayPlayer,NpcPredictionSource.ReadPlayer(Main.LocalPlayer)) || displayTerrain==null || !displayTerrain.Current)return false;
            displayLeaseTick=tick;return true;
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
