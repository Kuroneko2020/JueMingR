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

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed class HostCombatObservation : IRuntimeFeature,F5.ICombatObservationControls
    {
        internal readonly ObservationSettings Settings;
        internal readonly CombatSelection Selection;
        internal readonly NpcPredictionSource Prediction;
        internal readonly CombatGeometry Geometry=new CombatGeometry();
        internal readonly CombatGeometryHooks Hooks;
        internal readonly CombatObservationWorldLayer World;
        internal HostAttackAim Attack;
        private readonly SingleFeatureRuntime runtime;
        private readonly HostInputState input;
        private readonly NativeNpcObservation npcs;
        private bool collisionFailed,pathFailed,reportedCollision,reportedPath,reportedMarker,wasCollision,wasPath;
        private bool selectionFailed;
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
        internal long Session {get{return runtime.IsSessionActive?runtime.Generation:-1;}}
        internal bool Collision {get{return !World.Failed && Settings.CanRun && Options.Collision && Unavailable(0)==null;}}
        internal bool Path {get{return !World.Failed && Settings.CanRun && Options.Path && Unavailable(1)==null;}}
        internal bool Marker {get{return !World.Failed && !World.Marker.Failed && !selectionFailed && Settings.CanRun && Options.Marker && LayerStatus!=Rendering.WorldLayerStatus.Unavailable;}}
        public string Unavailable(int field)
        {
            if(selectionFailed)return "战斗目标观察暂不可用，设置已保留；可点击开启重试。";
            if(field==6)return pathFailed || Attack?.Failed==true?"辅助瞄准暂不可用，设置已保留；可点击开启重试。":null;
            if(LayerStatus==Rendering.WorldLayerStatus.Unavailable)return "世界显示入口不可用，设置已保留；需要重新进入游戏。";
            if(World.Failed && (field==0 || field==1 || field==5))return "战斗显示暂不可用，设置已保留；可点击开启重试。";
            if(field==0 && (!Hooks.Ready || collisionFailed))return "碰撞箱显示暂不可用，设置已保留；可点击开启重试。";
            if(field==1 && (!Hooks.Ready || pathFailed))return "NPC寻路预测暂不可用，设置已保留；可点击开启重试。";
            if(field==5 && World.Marker.Failed)return "目标标记暂不可用，设置已保留；可点击开启重试。";
            return null;
        }
        internal bool Capture {get{return Collision && runtime.IsSessionActive;}}
        internal bool CanDraw {get{return runtime.IsSessionActive && LayerStatus==Rendering.WorldLayerStatus.Ready && input.CanPrepareText;}}
        public bool Enabled {get{return !selectionFailed && (Collision || Path || Marker || !pathFailed && Prediction.Cache.Required>0);}}
        public void Set(int field,bool value)
        {
            if(!CanConfigure)return;
            if(field==6 && value){selectionFailed=pathFailed=reportedPath=false;Array.Clear(failedTargets,0,failedTargets.Length);Attack?.Reset();}
            if(value && (field==0 || field==1 || field==5))selectionFailed=false;
            if(value && (field==0 || field==1 || field==5))World.Recover();
            bool prior=field==0?Options.Collision:field==1?Options.Path:field==2?Options.ClearLine:field==3?Options.MouseCenter:field==4?Options.Dummy:field==5?Options.Marker:Options.Aim;
            if(field==0 && value){collisionFailed=reportedCollision=false;Geometry.Failed=false;}
            if(field==1 && value){pathFailed=reportedPath=false;Array.Clear(failedTargets,0,failedTargets.Length);if(Prediction.Native!=null && Prediction.Native.Failed)Prediction.Native.Retry();}
            if(field==5 && value){reportedMarker=false;World.Marker.Reset();}
            if(prior!=value)Settings.Set(Options.Toggle(field));
            if(field==6 && !value)Attack?.Clear();
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
            if(!path && Prediction.Cache.Required==0){Prediction.Clear();if(!Marker && Selection.HasTarget)Selection.RetireTarget();}
            // Reliable preference intent is available in the safe Main.Update
            // callback even in menus. This does not activate any Gameplay
            // feature, sample a world, or grant input/operation ownership.
            Prediction.Native?.PollEnvironment(Hooks.Ready && !selectionFailed && !pathFailed && (Settings.CanRun && Options.Path || Prediction.Cache.Required>0),runtime.IsSessionActive);
            wasCollision=collision;wasPath=path;
            AimTrace.Host(this,pathFailed,"poll",false);
        }
        internal void SampleMouse(){if(!selectionFailed && (Path || Marker || !pathFailed && Prediction.Cache.Required>0))Selection.SampleMouse(input);}
        internal void CollisionFailed(){collisionFailed=true;Geometry.Clear();}
        public void OnSessionStarted(){Clear();Geometry.Session=runtime.Generation;}
        public void OnSessionEnded(){Clear();}
        private void Clear(){actionFrame=-1;Attack?.Reset();Geometry.Clear();Prediction.Cache.EndSession();Prediction.EndWorld();Selection.Clear();World.Recover();World.Marker.Reset();Array.Clear(failedTargets,0,failedTargets.Length);selectionFailed=collisionFailed=pathFailed=reportedCollision=reportedPath=reportedMarker=false;}
        public void FailClosed(){Clear();Prediction.Stop();selectionFailed=collisionFailed=pathFailed=true;}
        public void Update(ulong tick)
        {UpdateSample(Main.GameUpdateCount,false,tick);}
        private long actionFrame=-1;
        internal void PrepareAction()
        {
            if(Attack==null || !Attack.Permission || actionFrame==input.Frame)return;
            actionFrame=input.Frame;
            npcs.BeginActions(input.Frame);
            // ItemCheck runs after this player's movement/selection, before
            // NPCs and projectiles advance. Their live sample is still T-1.
            // One preparation per native input epoch also covers first clicks;
            // individual Shoot/AI consumers only validate/borrow the result.
            UpdateSample((long)Main.GameUpdateCount-1,true,Main.GameUpdateCount);
        }
        private void UpdateSample(long sampleTick,bool beforeNpc,ulong tick)
        {
            try
            {
            Attack?.Demand();
            if(!Enabled)return;
            if(!Hooks.Ready && !Marker){Selection.RetireTarget();Prediction.Clear();return;}
            var player=Main.LocalPlayer;if(player==null || !player.active || player.dead || player.ghost){Selection.RetireTarget();Prediction.Clear();Geometry.BeginNpcs();return;}
            if(Collision)Geometry.BeginNpcs();
            // The display can show any genuinely computed future, including
            // a short known death/terrain endpoint. It still requests 120;
            // independent strict readers retain their own minimum.
            if(Path)Prediction.Cache.Demand(0,1,NpcPredictionCache.Horizon);else Prediction.Cache.Release(0);
            try{Selection.Update(Options,Session,Marker || !pathFailed && Prediction.Cache.Required>0,Collision?Geometry:null);}catch(Exception error){AimTrace.Fault("host-selection",error,(long)tick);selectionFailed=true;CollisionFailed();pathFailed=true;Selection.RetireTarget();Prediction.Stop();World.Clear();return;}
            if(!Selection.HasTarget){Prediction.Clear();return;}
            // Ordinary prediction is synchronous and owned by Source. Native
            // failure belongs only to the explicit comparison route; a shared
            // entry exception still latches the whole path closed.
            if(TargetPredictionFailed){Prediction.Clear();return;}
            if(!pathFailed && Prediction.Cache.Required>0)try{if(beforeNpc)Prediction.PrepareAction(Selection.Target,sampleTick);else Prediction.Prepare(Selection.Target,sampleTick);}
            catch(NpcObservationFailure error){AimTrace.Fault("host-npc-observation",error,(long)tick);failedTargets[Selection.Target.Slot]=Selection.Target;Prediction.Clear();}
            catch(Exception error){AimTrace.Fault("host-prepare",error,(long)tick);pathFailed=true;Prediction.Stop();}
            }
            finally{if(beforeNpc)Attack?.PrepareAction();else Attack?.Prepare();AimTrace.Host(this,pathFailed,"update-exit",true);}
        }
        internal void Register(HotkeyRegistry registry,Hotkeys.HotkeyStateFeedback feedback)
        {
            Action aimCommand=()=>Set(6,!Options.Aim);
            if(feedback!=null)aimCommand=feedback.Committed("combat.aim","辅助瞄准",aimCommand,()=>Settings.CanRun && Options.Aim?1:0,()=>CanConfigure && Unavailable(6)==null,
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
