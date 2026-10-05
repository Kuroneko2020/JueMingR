using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace NativeWorldTextProbe
{
    // Freeze before future original updates. This extra oracle has its own
    // run/output and stays OFF during the three matched cost rounds.
    internal sealed class NativeCombatRollingQualityChecks : IDisposable
    {
        private sealed class Sample
        {
            internal string Phase;internal int Frame,Life,PlayerLife,Mount,Next;
            internal bool MountActive,Wet,Honey,Lava,Shimmer,LifeChanged,PlayerChanged,SceneChanged,TerrainChanged;
            internal PredictionPlayerMotion Player;
            internal NpcTrajectory Path;internal MotionRect[] Baseline;internal int BaselineCount;
        }
        private readonly List<Sample> pending=new List<Sample>();
        private readonly List<string> rows=new List<string>{"phase,frame,captureTick,future,actualTick,status,type,width,height,modelError,baselineError,modelWidths,modelHeights,lifeChanged,playerConditionChanged,modelX,modelY,actualX,actualY,stop,quality,sceneConditionChanged,terrainConditionChanged"};
        private readonly List<string> frozen=new List<string>{"phase,frame,captureTick,offset,x,y,vx,vy,width,height,baselineX,baselineY"};
        private static readonly int[] Futures={15,30,60,120};
        private readonly object source;private readonly Type terrainType;private readonly string output;
        internal NativeCombatRollingQualityChecks(object source,string output)
        {this.source=source;this.output=output;terrainType=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);}
        internal void ChangeScene(string phase,bool terrain)
        {foreach(var sample in pending){sample.SceneChanged|=sample.Phase!=phase;sample.TerrainChanged|=terrain;}}
        internal void Observe()
        {
            foreach(var s in pending)
            {
                var key=s.Path.Identity;var n=Main.npc[key.Slot];var p=Main.LocalPlayer;
                bool same=ReferenceEquals(key.Token,n) && n.type==key.Type && n.netID==key.NetId && n.generation==key.Generation;
                s.LifeChanged|=same && n.life!=s.Life;
                s.PlayerChanged|=p.statLife!=s.PlayerLife || p.mount.Type!=s.Mount || p.mount.Active!=s.MountActive || p.wet!=s.Wet || p.honeyWet!=s.Honey || p.lavaWet!=s.Lava || p.shimmerWet!=s.Shimmer || !SamePremises(s.Player,PlayerMotion(p));
                long age=(long)Main.GameUpdateCount-s.Path.CaptureTick;
                while(s.Next<Futures.Length && (age>=Futures[s.Next] || !same || !n.active || n.life<=0 || p.dead))
                {
                    int future=Futures[s.Next++];string status=!same?"instance-ended":!n.active || n.life<=0?"original-death":p.dead?"player-death":age!=future?"actual-interval-missing":future>=s.Path.Count?"forecast-prefix-ended":"executed";
                    MotionRect model=future<s.Path.Count?s.Path[future].Bounds:default(MotionRect),actual=new MotionRect(n.position.X,n.position.Y,n.width,n.height);
                    double error=status=="executed"?Distance(model,actual):double.NaN;
                    double simple=age==future && same && n.active && future<s.BaselineCount?Distance(s.Baseline[future],actual):double.NaN;
                    rows.Add(Csv(s.Phase,s.Frame,s.Path.CaptureTick,future,Main.GameUpdateCount,status,key.Type,n.width,n.height,error,simple,error/n.width,error/n.height,s.LifeChanged,s.PlayerChanged,model.X,model.Y,n.position.X,n.position.Y,s.Path.Stop,s.Path.Quality,s.SceneChanged,s.TerrainChanged));
                }
            }
            pending.RemoveAll(s=>s.Next==Futures.Length);
        }
        internal void Capture(string phase,int frame,NpcTrajectory path,int cadence=30)
        {
            if(cadence<1 || cadence>30)throw new ArgumentOutOfRangeException(nameof(cadence));
            if(path==null || frame%cadence!=0)return;
            var n=Main.npc[path.Identity.Slot];var p=Main.LocalPlayer;
            var s=new Sample{Phase=phase,Frame=frame,Life=n.life,PlayerLife=p.statLife,Mount=p.mount.Type,MountActive=p.mount.Active,Wet=p.wet,Honey=p.honeyWet,Lava=p.lavaWet,Shimmer=p.shimmerWet,Player=PlayerMotion(p),Path=path,Baseline=new MotionRect[121],BaselineCount=1};
            // Simple same-sample speed/gravity/local collision comparator.
            // It has no target AI, future attack or live-entity write.
            const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
            var state=(NpcMotionState)source.GetType().GetMethod("Read",flags).Invoke(null,new object[]{n,path.Identity.Session});
            s.Baseline[0]=state.Bounds;var terrain=(IPredictionTerrain)Activator.CreateInstance(terrainType,true);terrain.Reset();
            var env=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,Multiplayer=Main.netMode==1,Remix=Main.remixWorld};
            for(int i=1;i<=120;i++)
            {
                if(!state.NoGravity)
                {
                    float world=env.WorldWidth/4200f;world*=world;
                    float gravity=.3f*Math.Max(.25f,Math.Min(1,(state.Y/16-(60+10*world))/Math.Max(1,env.WorldSurface/6))),fall=10;
                    if(state.Wet){gravity=state.Shimmer?.15f:state.Honey?.1f:.2f;fall=state.Shimmer?5.5f:state.Honey?4:7;}
                    state.Vy=Math.Min(fall,state.Vy+gravity);
                }
                PredictionStop stop;if(!terrain.Move(ref state,env,out stop))break;
                s.Baseline[s.BaselineCount++]=state.Bounds;
            }
            for(int i=0;i<path.Count;i++)
            {var point=path[i];frozen.Add(Csv(phase,frame,path.CaptureTick,i,point.Bounds.X,point.Bounds.Y,point.Vx,point.Vy,point.Bounds.Width,point.Bounds.Height,i<s.BaselineCount?(object)s.Baseline[i].X:"NA",i<s.BaselineCount?(object)s.Baseline[i].Y:"NA"));}
            pending.Add(s);
        }
        public void Dispose()
        {
            foreach(var s in pending)while(s.Next<Futures.Length)
                rows.Add(Csv(s.Phase,s.Frame,s.Path.CaptureTick,Futures[s.Next++],Main.GameUpdateCount,"actual-not-executed",s.Path.Identity.Type,0,0,double.NaN,double.NaN,double.NaN,double.NaN,s.LifeChanged,s.PlayerChanged,"NA","NA","NA","NA",s.Path.Stop,s.Path.Quality,s.SceneChanged,s.TerrainChanged));
            File.WriteAllLines(Path.Combine(output,"rolling-quality.csv"),rows);File.WriteAllLines(Path.Combine(output,"rolling-quality-frozen.csv"),frozen);
        }
        private static double Distance(MotionRect a,MotionRect b){double x=a.CenterX-b.CenterX,y=a.CenterY-b.CenterY;return Math.Sqrt(x*x+y*y);}
        private PredictionPlayerMotion PlayerMotion(Player p)
        {return (PredictionPlayerMotion)source.GetType().GetMethod("ReadPlayer",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{p});}
        private static bool SamePremises(PredictionPlayerMotion a,PredictionPlayerMotion b)
        {
            // Position/velocity/jump clocks evolve under the frozen controls;
            // comparing them would mislabel every genuine movement as a hit.
            // All sampled controls, motion parameters and mode flags remain
            // part of the premise, including mount activity and vertical input.
            return a.WaterWalk==b.WaterWalk && a.LavaWalk==b.LavaWalk && a.Width==b.Width && a.Height==b.Height && a.Left==b.Left && a.Right==b.Right && a.Up==b.Up && a.Down==b.Down && a.HoldJump==b.HoldJump && a.AutoJump==b.AutoJump && a.Hover==b.Hover && a.Complex==b.Complex && a.Gravity==b.Gravity && a.GravityDirection==b.GravityDirection && a.MaxFall==b.MaxFall && a.Acceleration==b.Acceleration && a.Slowdown==b.Slowdown && a.MaxSpeed==b.MaxSpeed && a.JumpHeight==b.JumpHeight && a.JumpSpeed==b.JumpSpeed;
        }
        private static string Csv(params object[] values){var fields=new string[values.Length];for(int i=0;i<fields.Length;i++)fields[i]="\""+Convert.ToString(values[i],CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\"";return string.Join(",",fields);}
    }
}
