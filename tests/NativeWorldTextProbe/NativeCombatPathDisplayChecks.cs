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
        internal static void RunPlayerPremisesCpu(object context)
        {
            NativeCombatAimChecks.Initialize();var host=Get(context,"CombatObservation");
            var input=Get(context,"Input");var local=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,1229,0,0);
            local.position=new Vector2(640,640);foreach(var npc in Main.npc)npc.active=false;
            Main.screenWidth=960;Main.screenHeight=640;Main.screenPosition=new Vector2(400,300);Main.UIScale=1;Main.GameViewMatrix.Zoom=Vector2.One;Main.hideUI=Main.mapFullscreen=false;
            Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));
            if(!(bool)Get(host,"PathAccess"))NativeHiddenEntryChecks.RevealForComposition(context);
            var audit=new Harmony("JueMingR.Tests.PathPlayerPremisesCpu20261021");
            try
            {
                audit.Patch(host.GetType().GetMethod("PrepareSelectedPrediction",F),prefix:new HarmonyMethod(typeof(NativeCombatPathDisplayChecks),nameof(Requested)));
                requests=0;interruptNext=false;
                PlayerPremises(context,host,Get(host,"World"),Main.npc[2],local,(CombatPredictionWork)Get(host,"PredictionWork"),null);
                Console.WriteLine("PATH PLAYER CPU LIMIT: real renderer Prepare and Draw geometry projection checked; GraphicsDevice/Draw/pixels NOT_EXECUTED.");
            }
            finally{audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        internal static void Run(object context,ProbeGraphics graphics,string output,bool initialized=false)
        {
            Directory.CreateDirectory(output);if(!initialized)NativeCombatAimChecks.Initialize();
            var host=Get(context,"CombatObservation");var world=Get(host,"World");var input=Get(context,"Input");
            if(!(bool)Get(host,"PathAccess"))NativeHiddenEntryChecks.RevealForComposition(context);
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
            bool all=true;bool originalDay=Main.dayTime;Main.dayTime=false;
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
                PlayerPremises(context,host,world,npc,p,work,graphics);
                TerrainGuard(host,output);
                Console.WriteLine("PASS PATH DISPLAY real Source first-publication retry and GPU; exhausted unchanged same-GUC reuse plus teleport negative control. No weapon use.");
            }
            finally{Main.dayTime=originalDay;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static int Ink(Color[] pixels)
        {int ink=0;for(int y=100;y<400;y++)for(int x=300;x<800;x++)if(pixels[y*960+x].A>0)ink++;return ink;}
        private static void PlayerPremises(object context,object host,object world,NPC npc,Player local,CombatPredictionWork work,ProbeGraphics graphics)
        {
            bool day=Main.dayTime;Main.dayTime=false;int mode=Main.netMode,connection=Netplay.Connection.State;var oldRemote=Main.player[1];
            var remote=new Player{whoAmI=1,active=true,position=new Vector2(760,640),width=20,height=42,gravDir=1,gravity=.4f,maxFallSpeed=10,statLife=100};
            Main.player[1]=remote;Main.netMode=1;Netplay.Connection.State=10;
            void Warm(bool independent=false)
            {
                Call(host,"OnSessionEnded");npc.SetDefaults(independent?3:2);npc.whoAmI=2;npc.active=true;npc.life=npc.lifeMax=10000;npc.target=1;
                npc.position=new Vector2(880,560);npc.velocity=Vector2.Zero;remote.controlRight=remote.controlUp=local.controlUp=false;
                if(independent){npc.aiStyle=-1;npc.noGravity=true;npc.noTileCollide=true;npc.velocity=new Vector2(2,0);}
                NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));
                for(int i=0;i<20 && GetOptional(host,"DisplayPath")==null;i++)NativeCombatObservationChecks.Fresh(context,host);
                Require(GetOptional(host,"DisplayPath")!=null,"actual multiplayer Source publishes "+(independent?"independent":"remote-player")+" route");
            }
            void Spend(){using(work.Enter(Main.GameUpdateCount)){while(CombatPredictionWork.TryContinue())System.Threading.Thread.SpinWait(64);}}
            void Render()
            {
                Call(world,"Prepare");
                if(graphics!=null)graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                else
                {
                    // Execute Draw's existing pure projection with its real
                    // camera values; no SpriteBatch/device/pixels are claimed.
                    var zoom=Main.GameViewMatrix.ZoomMatrix;Set(world,"zoom",zoom);Set(world,"inverse",Matrix.Invert(zoom));Call(world,"ProjectPresentation");
                }
            }
            try
            {
                Warm();var path=(NpcTrajectory)Get(host,"DisplayPath");
                Console.WriteLine("PATH PREMISE OBSERVED kind="+path.PlayerCondition.Kind+" slot="+path.PlayerCondition.Sample.PlayerIndex+" tokenB="+ReferenceEquals(path.PlayerCondition.Sample.PlayerToken,remote)+" nativeTarget="+npc.target+" sourceTarget="+Get(Get(host,"Prediction"),"targetPlayer")+" mechanism="+path.PlayerCondition.Sample.ObservationMechanism+" assumptions="+path.Assumptions);
                Require(path.PlayerCondition.Kind==PredictionPlayerConditionKind.HeldControls && ReferenceEquals(path.PlayerCondition.Sample.PlayerToken,remote) && path.PlayerCondition.Sample.PlayerIndex==1,"real producer seals B, not local A");
                NpcTrajectory shortPath;Require(path.TryWindow(path.SampleTick,Math.Min(8,path.Count-2),path.Version,out shortPath) && ReferenceEquals(shortPath.PlayerCondition,path.PlayerCondition),"real publication window retains its exact condition");
                var read=Get(host,"Prediction").GetType().GetMethod("ReadPlayer",F);var oldLocal=(PredictionPlayerMotion)read.Invoke(null,new object[]{local});
                Spend();int before=requests;local.controlUp=true;Call(host,"Update",(ulong)Main.GameUpdateCount);Render();
                var changedLocal=(PredictionPlayerMotion)read.Invoke(null,new object[]{local});
                Require(!(bool)host.GetType().GetMethod("DisplayInputCurrent",F).Invoke(null,new object[]{oldLocal,changedLocal}),"unchanged original comparator rejects A Up: old CaptureDisplay's LocalPlayer binding would withdraw the same B route");
                Console.WriteLine("PATH PREMISE A-UP samePublication="+ReferenceEquals(GetOptional(host,"DisplayPath"),path)+" strokes="+Get(world,"StrokeCount")+" sourceDelta="+(requests-before));
                Require(ReferenceEquals(GetOptional(host,"DisplayPath"),path) && (int)Get(world,"StrokeCount")>0 && requests==before,"unrelated A Up retains B lease through actual Source-to-renderer chain without preparation");
                remote.controlRight=true;
                var shortPoints=new NpcTrajectoryPoint[shortPath.Count];for(int i=0;i<shortPoints.Length;i++)shortPoints[i]=shortPath[i];
                // Reuse real produced coordinates, changing only the incoming
                // publication's player payload to isolate old-lease ownership.
                var otherPlayerPrefix=new NpcTrajectory(shortPath.Identity,shortPath.SampleTick,shortPath.Version,shortPath.Assumptions,shortPath.Stop,shortPoints,shortPoints.Length,shortPath.Strategy,shortPath.RelationVersion,shortPath.Quality,
                    playerCondition:new PredictionPlayerCondition(PredictionPlayerConditionKind.HeldControls,changedLocal));
                Require(!(bool)Call(host,"KeepLongerDisplay",otherPlayerPrefix,(long)Main.GameUpdateCount),"matching new A prefix cannot renew invalid old B long-path premise");
                Call(host,"Update",(ulong)Main.GameUpdateCount);Render();
                Require(GetOptional(host,"DisplayPath")==null && (int)Get(world,"StrokeCount")==0 && requests==before,"B control change withdraws old lease in exhausted same-GUC reentry");
                Warm();path=(NpcTrajectory)Get(host,"DisplayPath");Spend();remote.position+=new Vector2(1,0);Call(host,"Update",(ulong)Main.GameUpdateCount);Render();
                Require(GetOptional(host,"DisplayPath")==null,"necessary B same-GUC position jump withdraws old lease");
                Warm();path=(NpcTrajectory)Get(host,"DisplayPath");
                var next=path[1];NativeQuickItemChecks.BeginWorldStep();npc.position=new Vector2(next.Bounds.X,next.Bounds.Y);npc.velocity=new Vector2(next.Vx,next.Vy);npc.ai[0]=next.Phase;npc.wet=next.Wet;npc.noTileCollide=next.NoTileCollide;remote.position+=new Vector2(1,0);
                Require((bool)Call(host,"ValidateDisplay",(long)Main.GameUpdateCount),"normal across-GUC B motion keeps valid route");Render();Require((int)Get(world,"StrokeCount")>0,"normal across-GUC path reaches real projection");
                Warm();path=(NpcTrajectory)Get(host,"DisplayPath");Spend();Main.player[1]=new Player{whoAmI=1,active=true,position=remote.position,width=remote.width,height=remote.height,gravDir=1};
                Call(host,"Update",(ulong)Main.GameUpdateCount);Render();Require(GetOptional(host,"DisplayPath")==null,"same slot and controls cannot inherit B identity");Main.player[1]=remote;
                Warm(true);path=(NpcTrajectory)Get(host,"DisplayPath");Require(path.PlayerCondition.Kind==PredictionPlayerConditionKind.None,"actual independent Source has None premise");
                Spend();local.controlUp=true;Call(host,"Update",(ulong)Main.GameUpdateCount);Render();Require(ReferenceEquals(GetOptional(host,"DisplayPath"),path) && (int)Get(world,"StrokeCount")>0,"None ignores unrelated local input");
                local.dead=true;Call(host,"Update",(ulong)Main.GameUpdateCount);Render();Require(GetOptional(host,"DisplayPath")==null && (int)Get(world,"StrokeCount")==0,"None retains local death safety gate");local.dead=false;
                Warm(true);Call(host,"OnSessionEnded");Render();Require(GetOptional(host,"DisplayPath")==null,"None retains Session retirement gate");
                Console.WriteLine("PASS PATH PLAYER PREMISES: real two-player Source/publication/lease/renderer; B controls/slot/same-GUC motion negatives; normal across-GUC; None/death/session; window and old long-path guard. Isolated native fixture, not live networking.");
            }
            finally{Main.dayTime=day;Main.player[1]=oldRemote;Main.netMode=mode;Netplay.Connection.State=connection;local.dead=local.controlUp=false;}
        }
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
