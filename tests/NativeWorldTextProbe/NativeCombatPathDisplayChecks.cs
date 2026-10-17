using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Uses the real synchronous Source and renderer. The sole controlled
    // interruption consumes the existing work owner with the real clock;
    // trajectories, source outcomes and GPU pixels are never injected.
    internal static class NativeCombatPathDisplayChecks
    {
        private const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static int requests,requested;private static bool interruptNext;
        private static readonly List<uint> requestTicks=new List<uint>();
        private static void Requested(int __3)
        {
            requests++;requested=__3;requestTicks.Add(Main.GameUpdateCount);
            if(interruptNext){interruptNext=false;while(CombatPredictionWork.TryContinue())System.Threading.Thread.SpinWait(64);}
        }
        internal static void Run(object context,ProbeGraphics graphics,string output,bool initialized=false)
        {
            Directory.CreateDirectory(output);if(!initialized)NativeCombatAimChecks.Initialize();
            var host=Get(context,"CombatObservation");var world=Get(host,"World");var input=Get(context,"Input");
            var work=(CombatPredictionWork)Get(host,"PredictionWork");
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,1229,0,0);
            p.position=new Vector2(640,640);p.velocity=Vector2.Zero;p.itemAnimation=p.itemTime=0;
            foreach(var n in Main.npc)n.active=false;
            var npc=Main.npc[2];npc.SetDefaults(2);npc.whoAmI=2;npc.active=true;npc.position=new Vector2(880,560);npc.target=0;npc.life=npc.lifeMax=10000;
            Main.screenWidth=960;Main.screenHeight=640;Main.screenPosition=new Vector2(400,300);Main.UIScale=1;Main.GameViewMatrix.Zoom=Vector2.One;Main.hideUI=Main.mapFullscreen=false;
            Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));
            var audit=new Harmony("JueMingR.Tests.PathDisplay20261017");
            audit.Patch(host.GetType().GetMethod("PrepareSelectedPrediction",F),prefix:new HarmonyMethod(typeof(NativeCombatPathDisplayChecks),nameof(Requested)));
            var rows=new List<string>{"mode,step,guc,requests,requested,displayCount,lease,terrainCells,pixels,usedMs"};
            bool all=true;
            try
            {
                foreach(bool aim in new[]{false,true})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,aim));
                    // Warm the real model/JIT before the deterministic quota
                    // handoff. Separate ordinary Main windows retain cold
                    // startup timings; this oracle tests retry ownership.
                    for(int warm=0;warm<16 && GetOptional(host,"DisplayPath")==null;warm++)NativeCombatObservationChecks.Fresh(context,host);
                    Require(GetOptional(host,"DisplayPath")!=null,"real Source warms before the finite interruption");
                    Call(host,"OnSessionEnded");requests=0;requestTicks.Clear();interruptNext=true;
                    NativeToolExecutionChecks.Sample(context,input,npc.Center,false);Call(host,"SampleMouse");
                    NativeQuickItemChecks.BeginWorldStep();Call(Get(context,"nativeNpcs"),"BeginTick");
                    Call(host,"Update",(ulong)Main.GameUpdateCount);
                    int first=-1;
                    for(int step=0;step<12;step++)
                    {
                        if(step>0)NativeCombatObservationChecks.Fresh(context,host);
                        Call(world,"Prepare");var pixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                        int ink=Ink(pixels);
                        var path=(NpcTrajectory)GetOptional(host,"DisplayPath");var terrain=GetOptional(host,"displayTerrain");
                        rows.Add(string.Join(",",aim?"both-idle":"path-idle",step,Main.GameUpdateCount,requests,requested,path?.Count??0,Get(host,"displayLeaseTick"),terrain==null?0:Get(terrain,"Count"),ink,work.UsedMilliseconds));
                        if(path!=null && ink>0){first=step;break;}
                    }
                    Console.WriteLine("PATH DISPLAY FIRST aim="+aim+" interruptedFirst=true firstDrawGucDelta="+first+" sourceRequests="+requests+" lastRequested="+requested);
                    long retry=requestTicks.Count>1?(long)requestTicks[1]-requestTicks[0]:-1;
                    Console.WriteLine("PATH DISPLAY RETRY aim="+aim+" firstRequestInterrupted=true actualRetryDelta="+retry);
                    all&=first>=0 && first<=3 && retry>=0 && retry<=2;
                    if(first<0)continue;
                    long tick=Main.GameUpdateCount;var prior=GetOptional(host,"DisplayPath");int before=requests;
                    using(work.Enter(Main.GameUpdateCount)){while(CombatPredictionWork.TryContinue())System.Threading.Thread.SpinWait(64);}
                    Call(host,"Update",(ulong)Main.GameUpdateCount);Call(world,"Prepare");
                    int strokesBefore=(int)Get(world,"StrokeCount");var repeatedPixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                    bool retained=ReferenceEquals(GetOptional(host,"DisplayPath"),prior) && (int)Get(world,"StrokeCount")>0 && Ink(repeatedPixels)>0;
                    Console.WriteLine("PATH DISPLAY SAME GUC budgetSpent="+work.UsedMilliseconds+" retained="+retained+" sourceDelta="+(requests-before)+" terrain="+Get(GetOptional(host,"displayTerrain"),"Count"));
                    all&=retained && requests==before;
                    // Same-GUC real position changes cannot inherit the lease,
                    // even though the optional budget is already exhausted.
                    npc.position+=new Vector2(160,0);Call(host,"Update",(ulong)Main.GameUpdateCount);Call(world,"Prepare");graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                    bool rejected=GetOptional(host,"DisplayPath")==null && (int)Get(world,"StrokeCount")==0;
                    Console.WriteLine("PATH DISPLAY SAME GUC teleportRejected="+rejected);all&=rejected;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());Call(world,"Prepare");
                    all&=GetOptional(host,"DisplayPath")==null;
                }
                File.WriteAllLines(Path.Combine(output,"path-display-source-gpu.csv"),rows);
                Require(all,"After actual first Source quota interruption, the warm independent display retries within two GUC and really draws; unchanged same-GUC exhausted reentry preserves route; teleport retires it.");
                NegativeChanges(context,host,world,npc,p,work,graphics);
                TerrainGuard(host,output);
                Console.WriteLine("PASS PATH DISPLAY real Source first-publication retry and GPU; exhausted unchanged same-GUC reuse plus teleport negative control. No weapon use.");
            }
            finally{audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static int Ink(Color[] pixels)
        {int ink=0;for(int y=100;y<400;y++)for(int x=300;x<800;x++)if(pixels[y*960+x].A>0)ink++;return ink;}
        private static void NegativeChanges(object context,object host,object world,NPC npc,Player p,CombatPredictionWork work,ProbeGraphics graphics)
        {
            foreach(string change in new[]{"life","input","terrain","identity","off","session"})
            {
                npc.active=true;p.controlLeft=false;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));
                for(int i=0;i<12 && GetOptional(host,"DisplayPath")==null;i++)NativeCombatObservationChecks.Fresh(context,host);
                Require(GetOptional(host,"DisplayPath")!=null,"real source reacquires before negative "+change);
                using(work.Enter(Main.GameUpdateCount)){while(CombatPredictionWork.TryContinue())System.Threading.Thread.SpinWait(64);}
                Tile edited=null;ushort wall=0;
                if(change=="life")npc.life--;
                if(change=="input")p.controlLeft=true;
                if(change=="identity"){var replacement=new NPC();replacement.SetDefaults(2);replacement.whoAmI=2;replacement.active=true;replacement.position=npc.position;replacement.target=0;Main.npc[2]=replacement;}
                if(change=="terrain")
                {
                    var lease=Get(host,"displayTerrain");var cells=(Array)Get(lease,"values");Require(cells.Length>0,"real selected forecast has terrain dependencies");
                    int key=(int)Get(cells.GetValue(cells.Length-1),"Key");edited=Main.tile[key%Main.maxTilesX,key/Main.maxTilesX];wall=edited.wall;edited.wall=(ushort)(wall==0?1:0);
                }
                if(change=="off")NativeCombatObservationChecks.Save(host,new ObservationOptions());
                if(change=="session")Call(host,"OnSessionEnded");
                Call(host,"Update",(ulong)Main.GameUpdateCount);Call(world,"Prepare");graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                Require(GetOptional(host,"DisplayPath")==null && (int)Get(world,"StrokeCount")==0,"same-GUC actual "+change+" must withdraw route even after budget exhaustion");
                if(edited!=null)edited.wall=wall;if(change=="identity")Main.npc[2]=npc;p.controlLeft=false;
                Console.WriteLine("PATH DISPLAY NEGATIVE "+change+" rejected=True");
            }
        }
        private static void TerrainGuard(object host,string output)
        {
            var type=Get(Get(host,"Prediction"),"Terrain").GetType();var display=type.GetNestedType("CellLease",F).GetMethod("DisplayCurrent",F);
            // The old baseline lacks this new seam; the old/new behavioral
            // proof above does not depend on reaching this bounded cost check.
            if(display==null)return;
            var results=new List<string>{"cells,iterations,meanMs,p95Ms,maxMs,lastCellChangeRejected"};
            foreach(int count in new[]{0,64,4096})
            {
                var terrain=Activator.CreateInstance(type,true);Call(terrain,"Reset");var tile=type.GetMethod("Tile",F);
                for(int i=0;i<count;i++){int x=10+i%64,y=10+i/64;if(Main.tile[x,y]==null)Main.tile[x,y]=new Tile();object[] args={x,y,null,null};Require((bool)tile.Invoke(terrain,args),"capture bounded actual terrain cell");}
                var lease=Call(terrain,"FreezeCells");Require((int)Get(lease,"Count")==count,"exact bounded terrain denominator");Require((bool)Get(lease,"Current"),"initial real terrain qualification");
                var elapsed=new double[200];for(int j=0;j<elapsed.Length;j++){long start=Stopwatch.GetTimestamp();Require((bool)display.Invoke(lease,new object[]{true}),"unchanged complete terrain remains current");elapsed[j]=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;}
                Array.Sort(elapsed);double sum=0;foreach(double e in elapsed)sum+=e;bool rejected=true;
                if(count>0){var last=Main.tile[10+(count-1)%64,10+(count-1)/64];byte liquid=last.liquid;last.liquid=(byte)(liquid==0?1:0);rejected=!(bool)display.Invoke(lease,new object[]{true});last.liquid=liquid;Require(rejected,"last dependent cell mutation must be read");}
                results.Add(string.Join(",",count,200,sum/200,elapsed[190],elapsed[199],rejected));
                Console.WriteLine("PATH DISPLAY GUARD cells="+count+" meanMs="+sum/200+" p95Ms="+elapsed[190]+" maxMs="+elapsed[199]+" lastCellRejected="+rejected);
            }
            File.WriteAllLines(Path.Combine(output,"bounded-terrain-cost.csv"),results);
        }
    }
}
