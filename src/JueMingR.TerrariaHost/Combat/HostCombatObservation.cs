using System;
using System.IO;
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
        private bool collisionFailed,pathFailed,reportedCollision,reportedPath,wasCollision,wasPath;
        internal Rendering.WorldLayerStatus LayerStatus;
        internal HostCombatObservation(string directory,SingleFeatureRuntime runtime,HostInputState input,NativeNpcObservation npcs,Prediction.PredictionLaunchIdentity launch=null)
        {
            this.runtime=runtime;this.input=input;
            Prediction=new NpcPredictionSource(launch);
            Settings=new ObservationSettings(new AtomicFileDocument(System.IO.Path.Combine(directory,"JueMingRData","config","features","combat-observation.json"),65536));
            Selection=new CombatSelection(npcs);World=new CombatObservationWorldLayer(this);Hooks=new CombatGeometryHooks(this);
            AppDomain.CurrentDomain.ProcessExit+=Exit;
        }
        public ObservationOptions Options {get{return Settings.Value;}}
        public bool CanConfigure {get{return runtime.IsSessionActive && Settings.Ready;}}
        internal long Session {get{return runtime.IsSessionActive?runtime.Generation:-1;}}
        internal bool Collision {get{return Settings.CanRun && Options.Collision && Unavailable(0)==null;}}
        internal bool Path {get{return Settings.CanRun && Options.Path && Unavailable(1)==null;}}
        public string Unavailable(int field)
        {
            if(LayerStatus==Rendering.WorldLayerStatus.Unavailable)return "世界显示入口不可用，设置已保留；需要重新进入游戏。";
            if(field==0 && (!Hooks.Ready || collisionFailed))return "碰撞箱显示暂不可用，设置已保留；可点击开启重试。";
            if(field==1 && pathFailed)return "NPC寻路预测暂不可用，设置已保留；可点击开启重试。";
            return null;
        }
        internal bool Capture {get{return Collision && runtime.IsSessionActive;}}
        internal bool CanDraw {get{return runtime.IsSessionActive && LayerStatus==Rendering.WorldLayerStatus.Ready && input.CanPrepareText;}}
        public bool Enabled {get{return Collision || Path || Prediction.Cache.Required>0;}}
        public void Set(int field,bool value)
        {
            if(!CanConfigure)return;
            bool prior=field==0?Options.Collision:field==1?Options.Path:field==2?Options.ClearLine:field==3?Options.MouseCenter:Options.Dummy;
            if(field==0 && value){collisionFailed=reportedCollision=false;Geometry.Failed=false;}
            if(field==1 && value){pathFailed=reportedPath=false;if(Prediction.Native!=null && Prediction.Native.Failed)Prediction.Native.Retry();}
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
            if(!path && Prediction.Cache.Required==0 && Selection.HasTarget){Prediction.Clear();Selection.RetireTarget();}
            // Reliable preference intent is available in the safe Main.Update
            // callback even in menus. This does not activate any Gameplay
            // feature, sample a world, or grant input/operation ownership.
            Prediction.Native?.PollEnvironment(Settings.CanRun && Options.Path && !pathFailed || Prediction.Cache.Required>0,runtime.IsSessionActive);
            if(Prediction.Native!=null && Prediction.Native.Failed)pathFailed=true;
            wasCollision=collision;wasPath=path;
        }
        internal void SampleMouse(){if(Path || Prediction.Cache.Required>0)Selection.SampleMouse(input);}
        internal void CollisionFailed(){collisionFailed=true;Geometry.Clear();}
        public void OnSessionStarted(){Clear();Geometry.Session=runtime.Generation;}
        public void OnSessionEnded(){Clear();}
        private void Clear(){Geometry.Clear();Prediction.Cache.EndSession();Prediction.EndWorld();Selection.Clear();World.Clear();collisionFailed=pathFailed=reportedCollision=reportedPath=false;}
        public void FailClosed(){Clear();Prediction.Stop();collisionFailed=pathFailed=true;}
        public void Update(ulong tick)
        {
            if(!Enabled)return;
            var player=Main.LocalPlayer;if(player==null || !player.active || player.dead || player.ghost){Selection.RetireTarget();Prediction.Clear();Geometry.BeginNpcs();return;}
            if(Collision)Geometry.BeginNpcs();
            if(Path)Prediction.Cache.Demand(0,NpcPredictionCache.Horizon);else Prediction.Cache.Release(0);
            try{Selection.Update(Options,Session,Prediction.Cache.Required>0,Collision?Geometry:null);}catch{CollisionFailed();pathFailed=true;Selection.RetireTarget();Prediction.Clear();return;}
            if(!Selection.HasTarget){Prediction.Clear();return;}
            try{Prediction.Prepare(Selection.Target,Main.GameUpdateCount);if(Prediction.Native!=null && Prediction.Native.Failed)pathFailed=true;}catch{pathFailed=true;Prediction.Stop();}
        }
        internal void Register(HotkeyRegistry registry,Hotkeys.HotkeyStateFeedback feedback)
        {
            for(int i=0;i<2;i++)
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
            if(Unavailable(0)!=null && Options.Collision && !reportedCollision){reportedCollision=true;show(Unavailable(0));}
            if(Unavailable(1)!=null && Options.Path && !reportedPath){reportedPath=true;show(Unavailable(1));}
        }
        private void Exit(object sender,EventArgs e){AppDomain.CurrentDomain.ProcessExit-=Exit;Clear();Prediction.Stop();Hooks.Dispose();Settings.Stop(750);}
    }
}
