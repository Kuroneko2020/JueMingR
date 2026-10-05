using System;
using System.IO;
using AimTrace = JueMingR.TerrariaHost.Combat.Prediction.AimLightTrace;
using JueMingR.Features.Combat;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
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
        private readonly SingleFeatureRuntime runtime;
        private readonly HostInputState input;
        private bool collisionFailed,pathFailed,reportedCollision,reportedPath,reportedMarker,wasCollision,wasPath;
        internal Rendering.WorldLayerStatus LayerStatus;
        internal HostCombatObservation(string directory,SingleFeatureRuntime runtime,HostInputState input,NativeNpcObservation npcs,Prediction.PredictionLaunchIdentity launch=null)
            :this(directory,runtime,input,npcs,launch,null){}
        internal HostCombatObservation(string directory,SingleFeatureRuntime runtime,HostInputState input,NativeNpcObservation npcs,Prediction.PredictionLaunchIdentity launch,string package)
            :this(directory,runtime,input,npcs,launch,package,false){}
        internal HostCombatObservation(string directory,SingleFeatureRuntime runtime,HostInputState input,NativeNpcObservation npcs,Prediction.PredictionLaunchIdentity launch,string package,bool exactComparison)
        {
            this.runtime=runtime;this.input=input;
            AimTrace.Start(directory,launch,package);
            Prediction=new NpcPredictionSource(launch,exactComparison);
            Settings=new ObservationSettings(new AtomicFileDocument(System.IO.Path.Combine(directory,"JueMingRData","config","features","combat-observation.json"),65536));
            Selection=new CombatSelection(npcs);World=new CombatObservationWorldLayer(this);Hooks=new CombatGeometryHooks(this);
            AppDomain.CurrentDomain.ProcessExit+=Exit;
        }
        public ObservationOptions Options {get{return Settings.Value;}}
        public bool CanConfigure {get{return runtime.IsSessionActive && Settings.Ready;}}
        internal long Session {get{return runtime.IsSessionActive?runtime.Generation:-1;}}
        internal bool Collision {get{return Settings.CanRun && Options.Collision && Unavailable(0)==null;}}
        internal bool Path {get{return Settings.CanRun && Options.Path && Unavailable(1)==null;}}
        internal bool Marker {get{return Settings.CanRun && Options.Marker && LayerStatus!=Rendering.WorldLayerStatus.Unavailable;}}
        public string Unavailable(int field)
        {
            if(LayerStatus==Rendering.WorldLayerStatus.Unavailable)return "世界显示入口不可用，设置已保留；需要重新进入游戏。";
            if(field==0 && (!Hooks.Ready || collisionFailed))return "碰撞箱显示暂不可用，设置已保留；可点击开启重试。";
            if(field==1 && (!Hooks.Ready || pathFailed))return "NPC寻路预测暂不可用，设置已保留；可点击开启重试。";
            if(field==5 && World.Marker.Failed)return "目标标记暂不可用，设置已保留；可点击开启重试。";
            return null;
        }
        internal bool Capture {get{return Collision && runtime.IsSessionActive;}}
        internal bool CanDraw {get{return runtime.IsSessionActive && LayerStatus==Rendering.WorldLayerStatus.Ready && input.CanPrepareText;}}
        public bool Enabled {get{return Collision || Path || Marker || Prediction.Cache.Required>0;}}
        public void Set(int field,bool value)
        {
            if(!CanConfigure)return;
            bool prior=field==0?Options.Collision:field==1?Options.Path:field==2?Options.ClearLine:field==3?Options.MouseCenter:field==4?Options.Dummy:Options.Marker;
            if(field==0 && value){collisionFailed=reportedCollision=false;Geometry.Failed=false;}
            if(field==1 && value){pathFailed=reportedPath=false;if(Prediction.Native!=null && Prediction.Native.Failed)Prediction.Native.Retry();}
            if(field==5 && value){reportedMarker=false;World.Marker.Reset();}
            if(prior!=value)Settings.Set(Options.Toggle(field));
        }
        public void Radius(int value){if(CanConfigure && Options.Radius!=value)Settings.Set(Options.WithRadius(value));}
        internal void Poll()
        {
            Settings.Poll();bool collision=Collision,path=Path;
            if(wasCollision && !collision)Geometry.Clear();
            if(wasPath && !path)Prediction.Cache.Release(0);
            // Other registered consumers can outlive the path toggle. Retire
            // the final target on actual demand removal, once, even when the
            // display was already OFF before that consumer released it.
            if(!path && Prediction.Cache.Required==0){Prediction.Clear();if(!Marker && Selection.HasTarget)Selection.RetireTarget();}
            // Reliable preference intent is available in the safe Main.Update
            // callback even in menus. This does not activate any Gameplay
            // feature, sample a world, or grant input/operation ownership.
            Prediction.Native?.PollEnvironment(Hooks.Ready && (Settings.CanRun && Options.Path && !pathFailed || Prediction.Cache.Required>0),runtime.IsSessionActive);
            wasCollision=collision;wasPath=path;
            AimTrace.Host(this,pathFailed,"poll",false);
        }
        internal void SampleMouse(){if(Path || Marker || Prediction.Cache.Required>0)Selection.SampleMouse(input);}
        internal void CollisionFailed(){collisionFailed=true;Geometry.Clear();}
        public void OnSessionStarted(){Clear();Geometry.Session=runtime.Generation;}
        public void OnSessionEnded(){Clear();}
        private void Clear(){Geometry.Clear();Prediction.Cache.EndSession();Prediction.EndWorld();Selection.Clear();World.Clear();World.Marker.Reset();collisionFailed=pathFailed=reportedCollision=reportedPath=reportedMarker=false;}
        public void FailClosed(){Clear();Prediction.Stop();collisionFailed=pathFailed=true;}
        public void Update(ulong tick)
        {
            try
            {
            if(!Enabled)return;
            if(!Hooks.Ready && !Marker){Selection.RetireTarget();Prediction.Clear();return;}
            var player=Main.LocalPlayer;if(player==null || !player.active || player.dead || player.ghost){Selection.RetireTarget();Prediction.Clear();Geometry.BeginNpcs();return;}
            if(Collision)Geometry.BeginNpcs();
            // The display can show any genuinely computed future, including
            // a short known death/terrain endpoint. It still requests 120;
            // independent strict readers retain their own minimum.
            if(Path)Prediction.Cache.Demand(0,1,NpcPredictionCache.Horizon);else Prediction.Cache.Release(0);
            try{Selection.Update(Options,Session,Marker || Prediction.Cache.Required>0,Collision?Geometry:null);}catch(Exception error){AimTrace.Fault("host-selection",error,(long)tick);CollisionFailed();pathFailed=true;Selection.RetireTarget();Prediction.Clear();return;}
            if(!Selection.HasTarget){Prediction.Clear();return;}
            // Ordinary prediction is synchronous and owned by Source. Native
            // failure belongs only to the explicit comparison route; a shared
            // entry exception still latches the whole path closed.
            if(Prediction.Cache.Required>0)try{Prediction.Prepare(Selection.Target,Main.GameUpdateCount);}catch(Exception error){AimTrace.Fault("host-prepare",error,(long)tick);pathFailed=true;Prediction.Stop();}
            }
            finally{AimTrace.Host(this,pathFailed,"update-exit",true);}
        }
        internal void Register(HotkeyRegistry registry,Hotkeys.HotkeyStateFeedback feedback)
        {
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
