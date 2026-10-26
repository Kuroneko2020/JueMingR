using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Same Host fixture owner as the matching tests. These regressions observe
    // actual worker deliveries and native writes, never inject proof state.
    internal static partial class NativeBlueprintWorldMatchChecks
    {
        private static readonly List<long> completionUpdateCosts = new List<long>();
        private static long completionUpdateStarted;
        private static void CompletionUpdatePrefix() { completionUpdateStarted = Stopwatch.GetTimestamp(); }
        private static void CompletionUpdatePostfix() { completionUpdateCosts.Add(Stopwatch.GetTimestamp() - completionUpdateStarted); }

        private static void CompletionProgress(object context, object host, object input, LibraryState library, Action step)
        {
            using (EmptyWorld(host, 1024, 160))
            {
                DenseCompletion(host, input, library, step, false);
                DenseCompletion(host, input, library, step, true);
                ContinuousLampProof(host, input, library, step, false);
                ContinuousLampProof(host, input, library, step, true);
                HardDirtyFairness(host, input, library, step);
            }
        }

        // Wait only in the test driver, before declaring the next Update. The
        // Host still owns every Take/sample and never spins for worker output.
        private static bool AwaitSourceSupply(object host, LibraryState library)
        {
            var work = GetOptional(host, "projectionMatchWork") as IDictionary;
            if(work!=null)
            {
                bool pending=work.Values.Cast<object>().Any(w=>(bool)Get(w,"Pending"));
                if(!pending)return work.Values.Cast<object>().Any(w=>((IList)Get(w,"Parts")).Cast<object>().Any(p=>p!=null));
                var sourceWorker=Get(library,"worker");var sourceGate=Get(sourceWorker,"gate");
                NativeQuickItemChecks.Until(()=>{lock(sourceGate)return (int)Get(Get(sourceWorker,"projectionSourceReceipts"),"Count")>0 || (int)Get(Get(sourceWorker,"projectionFailures"),"Count")>0;});
                return true;
            }
            var read = GetOptional(host, "projectionMatchRead");
            if (read == null) return false;
            if (GetOptional(read, "Batch") != null) return true;
            if (!(bool)Get(read, "Pending")) return false;
            var worker = Get(library, "worker"); var gate = Get(worker, "gate");
            NativeQuickItemChecks.Until(() =>
            {
                lock (gate) return (int)Get(Get(worker,"projectionSourceReceipts"),"Count") > 0 || (int)Get(Get(worker,"projectionFailures"),"Count") > 0;
            });
            return true;
        }

        private static object ConfirmCompletion(object host, object input, LibraryEntry entry, int x, int y, Action step)
        {
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            Require((bool)Call(host,"Place",entry), "completion fixture uses real Place admission");
            NativeQuickItemChecks.Until(() => { step(); return Mode(host)=="Following"; });
            var candidate = Get(host,"ProjectionCandidate"); var summary = (CaptureSummary)Get(candidate,"Summary");
            int px=x+(summary.Width-1)/2, py=y+(summary.Height-1)/2;
            Pointer(host,false,px,py); for(int n=0;n<3;n++)step();
            Call(host,"UpdateProjectionWindow",new WorldTargetView(x,y,32,16,0));
            NativeQuickItemChecks.Until(()=>{step();return GetOptional(candidate,"Window")!=null && !(bool)Get(candidate,"WindowPending");});
            Pointer(host,true,px,py);
            Require(Mode(host)=="Committing", "completion fixture first confirms a legal fixed placement");
            return candidate;
        }

        private static void DenseCompletion(object host, object input, LibraryState library, Action step, bool controlledSupply)
        {
            const int width=512,height=128,count=width*height,ox=20,oy=16;
            var cells=new CaptureCell[count];
            for(int n=0;n<count;n++)
            {
                int x=n%width,y=n/width; var tile=Main.tile[ox+x,oy+y]; tile.active(true); tile.type=1; tile.color(0); tile.slope(0); tile.halfBrick(false);
                cells[n]=new CaptureCell{X=x,Y=y,Layers=CaptureLayer.Tile,TileType=1};
            }
            // Reuse the real streaming save fixture: a CaptureChunk admits at
            // most 2,048 total records, rather than an entire 65,536-cell array.
            var entry=NativeBlueprintBudgetChecks.Save(library,cells,width,height);
            var item=ConfirmCompletion(host,input,entry,ox,oy,step);
            long reads=(long)Get(host,"ProjectionMatchCellReads"),requests=(long)Get(host,"ProjectionMatchSourceRequests"),batches=(long)Get(host,"ProjectionMatchSourceBatches"),steps=(long)Get(host,"ProjectionMatchSourceSteps");
            var before=library.DebugProjectionStatistics; int updates=0,productive=0,supplyWait=0,readyIdle=0,firstCoverage=-1,visibleHidden=-1;long sourcePeak=0;
            var costs=new HarmonyLib.Harmony("JueMingR.Tests.Blueprint.CompletionCosts"); completionUpdateCosts.Clear();
            costs.Patch(host.GetType().GetMethod("AdvanceProjectionMatch",Flags),prefix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks).GetMethod(nameof(CompletionUpdatePrefix),Flags)),postfix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks).GetMethod(nameof(CompletionUpdatePostfix),Flags)));
            var elapsed=Stopwatch.StartNew();
            try
            {
                do
                {
                    bool ready=controlledSupply && AwaitSourceSupply(host,library);
                    var active=GetOptional(host,"projectionMatchRead");
                    if(active!=null && GetOptional(active,"Batch")!=null)ready=true;
                    long prior=(long)Get(host,"ProjectionMatchCellReads"),priorSteps=(long)Get(host,"ProjectionMatchSourceSteps");
                    step();updates++;
                    sourcePeak=Math.Max(sourcePeak,(long)Get(host,"ProjectionMatchSourceRetainedBytes"));
                    int used=checked((int)((long)Get(host,"ProjectionMatchCellReads")-prior));
                    Require(used<=256 && (long)Get(host,"ProjectionMatchSourceSteps")-priorSteps<=256,"dense Update bounds both world sampling and masked/source cursor work");
                    if(used>0){productive++;Require(used==256,"ready dense ordinary work consumes its actual declared Update allowance without a fixed protocol gap");}
                    else if(ready)readyIdle++;else supplyWait++;
                    long actual=(long)Get(host,"ProjectionMatchCellReads")-reads;
                    if(actual==count && firstCoverage<0)firstCoverage=updates;
                    var state=GetOptional(item,"Match");
                    if(state!=null && visibleHidden<0)
                    {
                        bool hidden=true;for(int y=0;y<16 && hidden;y++)for(int x=0;x<32;x++)if(Result(item,x,y).MatchBits!=1){hidden=false;break;}
                        if(hidden)visibleHidden=updates;
                    }
                    Require(actual<=count,"stable complete ordinary graph never performs an unconditional second world pass");
                    if(elapsed.ElapsedMilliseconds>15000)throw new Exception("Dense completion exceeded its finite test facility guard");
                    if(!controlledSupply)System.Threading.Thread.Sleep(2);
                }while(Mode(host)=="Committing" || Scene(host).Contains(item));
                var after=library.DebugProjectionStatistics;
                long actualRequests=(long)Get(host,"ProjectionMatchSourceRequests")-requests,actualBatches=(long)Get(host,"ProjectionMatchSourceBatches")-batches;
                Require(firstCoverage==updates && visibleHidden>0 && visibleHidden<=firstCoverage && productive==count/256 && readyIdle==0 && actualRequests==32 && actualBatches==32 && (long)Get(host,"ProjectionMatchSourceSteps")-steps==count,
                    "32 physical worksets cover 65536 targets in 256 productive Updates and retire at the first complete current coverage without input");
                Require(!Scene(host).Contains(item) && GetOptional(item,"Match")==null && GetOptional(item,"SourceRanges")==null && GetOptional(item,"Window")==null && ((IDictionary)Get(item,"Windows")).Count==0 && (int)Get(Get(item,"Renderer"),"CommandCount")==0 && (int)Get(host,"ProjectionMatchWatchCount")==0 && !(bool)Get(host,"ProjectionMatchReading"),
                    "dense completion retires scene identity, match/watch/window/renderer and the unique active slot");
                Require(after.BlockReads-before.BlockReads<=32 && after.DecodedRecords-before.DecodedRecords<=count,"hot 32-block source does not repeatedly decode each small protocol fragment");
                // The Host lifetime peak now also covers earlier four/six-owner
                // cases. Measure this dense single-owner interval from its
                // real retained state rather than relabel that global peak.
                long peak=sourcePeak;Require(peak>0 && peak<=8192L+2048L*64,"ordinary scene charges its single physical source payload plus actual transport/Host descriptors");
                double[] ms=completionUpdateCosts.Select(t=>t*1000.0/Stopwatch.Frequency).OrderBy(v=>v).ToArray();
                Console.WriteLine("PASS BlueprintCompletion dense mode="+(controlledSupply?"controlled-worker-supply":"natural-worker-production")+" records=65536 objects=0 physicalBlocks=32 targets=65536 sourceRequests="+actualRequests+" sourceArrivals="+actualBatches+" worldReads="+((long)Get(host,"ProjectionMatchCellReads")-reads)+" sourceSteps="+((long)Get(host,"ProjectionMatchSourceSteps")-steps)+" updates="+updates+" productive="+productive+" zeroReadsWaitingForSupplyOrCommit="+supplyWait+" readyIdle="+readyIdle+" firstCoverageUpdate="+firstCoverage+" firstVisibleMatchUpdate="+visibleHidden+" retirementUpdate="+updates+" bodyReads="+(after.BlockReads-before.BlockReads)+" decoded="+(after.DecodedRecords-before.DecodedRecords)+" sourceBytesPeak="+peak+" elapsedMs="+elapsed.ElapsedMilliseconds+" matchUpdateMedianMs="+ms[ms.Length/2].ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" p95Ms="+ms[(ms.Length-1)*95/100].ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" maxMs="+ms[ms.Length-1].ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"; no 60Hz conversion, no FPS/owner acceptance claim");
                WorldGen.KillTile(ox,oy,false,false,true);for(int n=0;n<3;n++)step();
                Require(!Scene(host).Contains(item),"later native demolition cannot resurrect the completed identity");
                RequireListedAsset(library,entry.AssetId,"dense automatic retirement preserves the source template");
            }
            finally { costs.UnpatchAll(costs.Id);Call(host,"ClearProjection"); }
        }

        private static void ContinuousLampProof(object host, object input, LibraryState library, Action step, bool established)
        {
            const int ox=800,oy=20;
            for(int row=0;row<4;row++)Main.tile[ox,oy+row]=new Tile();
            Require(WorldGen.PlaceTile(ox,oy+3,1,true,true),"continuous lamp has genuine original support");
            for(int row=0;row<3;row++){var tile=Main.tile[ox,oy+row];tile.active(true);tile.type=93;tile.frameX=0;tile.frameY=(short)(row*18);}
            Main.tile[ox+8,oy]=new Tile();
            var source=(IBlueprintCaptureSource)Activator.CreateInstance(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource",true),Flags,null,new object[]{(Func<bool>)(()=>true)},null);
            CaptureCell cell;Require(source.Read(ox,oy,out cell),"continuous lamp source is read through native capture");var shape=source.Describe(cell);Require(shape!=null && shape.Object.Reliable,"continuous lamp has reliable original metadata");
            var obj=shape.Object;obj.Id=1;obj.X=obj.Y=0;var cells=new List<CaptureCell>();
            for(int row=0;row<3;row++){Require(source.Read(ox,oy+row,out cell),"continuous lamp complete member");cell.X=0;cell.Y=row;cell.ObjectId=1;cells.Add(cell);}
            cells.Add(new CaptureCell{X=8,Y=0,Layers=CaptureLayer.Wire,Wires=8});
            var entry=NativeBlueprintProjectionVisualChecks.Save(library,new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"continuous-lamp",1,1),1,new CaptureSummary{Width=9,Height=3,ContentCount=4},new BlueprintProjectionBounds(0,0,9,3),cells.OrderBy(c=>c.Y).ThenBy(c=>c.X).ToArray(),new[]{obj}));
            int toggles=0,updates=0;long reads=(long)Get(host,"ProjectionMatchCellReads"),requests=(long)Get(host,"ProjectionMatchSourceRequests");
            Action writing=()=>{Wiring.ToggleLamp(ox,oy,Main.tile[ox,oy],null,false);toggles++;step();updates++;};
            object item=Place(host,input,entry,ox,oy,established?step:writing);
            NativeQuickItemChecks.Until(()=>{if(established)step();else writing();return Matches(item)==3;});
            var observer=Get(host,"projectionChanges");var watches=(IDictionary)Get(observer,"watches");
            Require(watches.Values.Cast<object>().Any(w=>GetOptional(w,"Frames") is IDictionary f && f.Count>0),"existing-proof start is a real complete native frame witness, never a supplied Match bit");
            int before=toggles;
            // Keep this proof alive across genuine synchronous raw changes,
            // then satisfy the independent target through an original writer.
            for(int n=0;n<4;n++)writing();
            Require(Matches(item)==3 && Scene(host).Contains(item),"each-update equivalent raw writes preserve the proven lamp while the independent sentinel is missing");
            WorldGen.PlaceWire4(ox+8,oy);
            var clock=Stopwatch.StartNew();
            while(Scene(host).Contains(item)){writing();System.Threading.Thread.Sleep(1);if(clock.ElapsedMilliseconds>8000)throw new Exception("Continuous native lamp writer prevented natural completion");}
            Require(toggles>before+4 && !Scene(host).Contains(item) && (long)Get(host,"ProjectionMatchCellReads")-reads>=4,"writer continues through the final real sample and natural retirement");
            Console.WriteLine("PASS BlueprintCompletion everyUpdateToggleLamp start="+(established?"existing-complete-witness":"first-source-request")+" realToggles="+toggles+" writerUpdates="+updates+" worldReads="+((long)Get(host,"ProjectionMatchCellReads")-reads)+" sourceRequests="+((long)Get(host,"ProjectionMatchSourceRequests")-requests)+" stoppedWriterToComplete=false retired=true");
            Call(host,"ClearProjection");
        }

        private static void HardDirtyFairness(object host, object input, LibraryState library, Action step)
        {
            const int ox=20,oy=10,bx=900,by=150;
            var cells=Enumerable.Range(0,2048).Select(n=>new CaptureCell{X=n%512,Y=n/512,Layers=CaptureLayer.Tile,TileType=1}).ToArray();
            for(int n=0;n<2048;n++)Main.tile[ox+n%512,oy+n/512]=new Tile();Main.tile[bx,by]=new Tile();
            var entry=NativeBlueprintProjectionVisualChecks.Save(library,new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"hard-dirty",1,1),1,new CaptureSummary{Width=512,Height=4,ContentCount=2048},new BlueprintProjectionBounds(0,0,512,4),cells,Array.Empty<CaptureObject>()));
            var a=Place(host,input,entry,ox,oy,step);
            Require(WorldGen.PlaceTile(ox,oy,1,true,true),"hard-dirty owner has a genuine native target to repaint");
            int changes=0,bReads=0;
            Action dirty=()=>{WorldGen.paintTile(ox,oy,(byte)(1+(changes++&1)),false,false);AwaitSourceSupply(host,library);step();};
            for(int n=0;n<4;n++)dirty();
            var bEntry=Small(library,new CaptureCell{Layers=CaptureLayer.Tile,TileType=1});
            Require(WorldGen.PlaceTile(bx,by,1,true,true),"different template B is actually complete in the world");
            var timing=new HarmonyLib.Harmony("JueMingR.Tests.Blueprint.FairRead");
            try
            {
                objectSample=(x,y)=>{if(x==bx && y==by)bReads++;};
                timing.Patch(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldMatchSource",true).GetMethod("TryRead",Flags),postfix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks).GetMethod(nameof(AfterObjectSample),Flags)));
                var b=Place(host,input,bEntry,bx,by,dirty);int updates=0;
                while(Scene(host).Contains(b) && updates++<32)dirty();
                Require(Scene(host).Contains(a) && !Scene(host).Contains(b) && bReads>0 && changes>4,"a continuously hard-dirty owner leaves finite scene service for another template's actual world sampling and retirement without clicks");
                Console.WriteLine("PASS BlueprintCompletion hardDirtyFairness nativeChanges="+changes+" BActualWorldReads="+bReads+" afterConfirmUpdates="+updates+" AStillIncomplete=true BRetired=true sceneWorkBounded=true");
            }
            finally { objectSample=null;timing.UnpatchAll(timing.Id);Call(host,"ClearProjection"); }
        }
    }
}
