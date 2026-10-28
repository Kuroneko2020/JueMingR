#if JMR_BLUEPRINT_DIAGNOSTIC
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class BlueprintBuildTrace
    {
        private void Export()
        {
            bool entered=false;
            try
            {
                entered=Monitor.TryEnter(exportGate,250);if(!entered || attemptedExport)return;attemptedExport=true;
                // Optional detail is dropped before any byte is written. The
                // smaller retry is still a complete JSON with explicit loss.
                byte[] bytes=Encode(true,true,true);
                if(bytes.Length>TraceByteLimit)bytes=Encode(false,true,true);
                if(bytes.Length>TraceByteLimit)bytes=Encode(false,false,true);
                if(bytes.Length>TraceByteLimit)bytes=Encode(false,false,false);
                if(bytes.Length>TraceByteLimit)throw new InvalidDataException("Bounded trace exceeded its raw byte allocation.");
                string directory=Path.Combine(root,"run-"+runId);Directory.CreateDirectory(directory);
                using(var file=new FileStream(Path.Combine(directory,"trace.json"),FileMode.CreateNew,FileAccess.Write,FileShare.Read))
                {file.Write(bytes,0,bytes.Length);file.Flush(true);}
            }
            catch(Exception error)
            {
                failure=Limit(error.GetType().FullName,160);
                try
                {
                    byte[] bytes=Encoding.UTF8.GetBytes("{\"status\":\"export-failed\",\"runId\":"+Quote(runId)+",\"failure\":"+Quote(failure)+"}");
                    using(var file=new FileStream(Path.Combine(root,"export-failure-"+runId+".json"),FileMode.CreateNew,FileAccess.Write,FileShare.Read))file.Write(bytes,0,bytes.Length);
                }
                catch { }
            }
            finally{if(entered)Monitor.Exit(exportGate);}
        }
        private byte[] Encode(bool withTargets,bool withEvents,bool withSources)
        {
            var text=new StringBuilder(262144);
            text.Append("{\"schema\":\"jmr-blueprint-autobuild-diag-v1\",\"run\":{\"runId\":").Append(Quote(runId));
            text.Append(",\"hostMvid\":").Append(Quote(hostMvid)).Append(",\"hostAssembly\":").Append(Quote(hostAssembly));
            text.Append(",\"hostSha256\":").Append(Quote(hostSha)).Append(",\"packageId\":").Append(Quote(packageId));
            text.Append(",\"gameVersion\":").Append(Quote(gameVersion)).Append(",\"netMode\":").Append(initialMode).Append(",\"worldId\":").Append(initialWorld);
            text.Append(",\"arm\":\"used-<runId>.txt; exact hostSha256 checked before atomic claim\"},");
            text.Append("\"limits\":{\"seconds\":120,\"summaries\":120,\"events\":512,\"targets\":8,\"rawBytes\":1048576,\"traceByteAllocation\":").Append(TraceByteLimit).Append("},");
            int buckets=Math.Max(1,Math.Min(SecondsLimit,(int)((stoppedAt+999)/1000)));
            long outer=0,moving=0,stationary=0,auto=0,scene=0;int gaps=0;
            for(int i=0;i<buckets;i++){var s=seconds[i];outer+=s.OuterCalls;moving+=s.MovingUpdates;stationary+=s.StationaryUpdates;auto+=s.AutoEnabledUpdates;scene+=s.SceneUpdates;if(s.OuterCalls==0)gaps++;}
            text.Append("\"coverage\":{\"elapsedMs\":").Append(stoppedAt).Append(",\"outerUpdates\":").Append(outer);
            text.Append(",\"movingUpdates\":").Append(moving).Append(",\"stationaryUpdates\":").Append(stationary);
            text.Append(",\"autoEnabledObservedUpdates\":").Append(auto).Append(",\"sceneObservedUpdates\":").Append(scene);
            text.Append(",\"nominalBuckets\":").Append(buckets).Append(",\"coverageGapBuckets\":").Append(gaps).Append("},");
            text.Append("\"dictionary\":{\"stages\":");Names(text,typeof(BuildTraceStage));text.Append(",\"reasons\":");Names(text,typeof(BuildTraceReason));
            text.Append(",\"unknown\":-1,\"stageLastSeenNever\":-1,\"inputStateBits\":\"focused=1,observed=2,mapped=4,finalized=8,nativePermission=16,quarantine=32,rearming=64,rawLeft=128,rawRight=256,hotkeyCapture=512,ownedMouse=1024\",");
            text.Append("\"playerStateBits\":\"active=1,dead=2,ghost=4,noBuilding=8,mouseInterface=16,left=32,right=64,controlUse=128,releaseUse=256,override=512,buffered=1024,inventory=2048,paused=4096,menu=8192,f5Visible=16384\",");
            text.Append("\"blueprintStateBits\":\"autoEnabled=1,ready=2,failed=4,runtimeSession=8,preferencesLoaded=16,toolsAvailable=32,capture=64,captureWork=128,editing=256,pendingProjection=512,deletePending=1024,prepared=2048,activeFlight=4096,container=8192\",");
            text.Append("\"sourceFlags\":\"exhausted=1,candidate=2,demand=4,reading=8,progress=16,materialCurrent=32,rangeCurrent=64,retryPending=128,eligibilityRetry=256,frontier=512\",");
            text.Append("\"wakeBlockedBits\":\"notExhausted=1,candidate=2,demand=4,reading=8,progress=16,materialNotCurrent=32,rangeNotCurrent=64,noPermissionDue=128\",");
            text.Append("\"targetFlags\":\"localProof=1,localPriority=2,started=4,entered=8,chosen=16,checked=32,rejected=64,directionBorrowed=128\",");
            text.Append("\"gates\":\"GateEvaluated/GatePassed are per-second any-evaluated/any-passed: InputGate=1,InterfaceGate=2,MouseGate=4,InventoryNotReady=8,ToolsGate=16,other=32. Absence is not_evaluated. False compound gates identify only that gate.\",");
            text.Append("\"reasonEpochs\":\"reasonObservedOuterEpochs deduplicates each reason between consecutive observed outer arrivals; occurrences retain actual callback/work counts. Neither array is wall-clock duration.\",");
            text.Append("\"resultSemantics\":\"Started/Entered/Chosen/Checked/spend are callback or consumption evidence, never placement success. Match is the independent existing publication.\"},");
            text.Append("\"totals\":{\"stageCalls\":[");
            for(int j=0;j<StageCount;j++){if(j>0)text.Append(',');long total=0;for(int i=0;i<buckets;i++)total+=stages[i*StageCount+j];text.Append(total);}
            text.Append("],\"reasonOccurrences\":[");for(int j=0;j<ReasonCount;j++){if(j>0)text.Append(',');long total=0;for(int i=0;i<buckets;i++)total+=reasons[i*ReasonCount+j];text.Append(total);}
            text.Append("],\"reasonObservedOuterEpochs\":[");for(int j=0;j<ReasonCount;j++){if(j>0)text.Append(',');long total=0;for(int i=0;i<buckets;i++)total+=reasonUpdates[i*ReasonCount+j];text.Append(total);}
            text.Append("],\"semanticOccurrences\":").Append(sequence+suppressedEvents).Append(",\"eventDistinctOccurrences\":").Append(sequence);
            text.Append(",\"eventRetained\":").Append(withEvents?Math.Min(EventLimit,sequence):0).Append(",\"eventOverwritten\":").Append(overwritten);
            text.Append(",\"eventRepeatedSuppressed\":").Append(suppressedEvents).Append(",\"observerContentionDropped\":").Append(contention);
            text.Append(",\"targetDetailReplaced\":").Append(targetReplaced).Append(",\"optionalTargetsOmitted\":").Append(withTargets?0:targetCount);
            text.Append(",\"optionalEventsOmitted\":").Append(withEvents?0:Math.Min(EventLimit,sequence)).Append(",\"optionalSourcesOmitted\":").Append(withSources?"false":"true").Append("},");
            text.Append("\"seconds\":[");var previous=new long[StageCount];for(int j=0;j<StageCount;j++)previous[j]=-1;
            for(int i=0;i<buckets;i++)
            {
                if(i>0)text.Append(',');var s=seconds[i];text.Append("{\"second\":").Append(i).Append(",\"observed\":").Append(s.OuterCalls>0?"true":"false");
                text.Append(",\"coverage_gap\":").Append(s.OuterCalls==0?"true":"false");
                text.Append(",\"outerCalls\":").Append(s.OuterCalls).Append(",\"movingUpdates\":").Append(s.MovingUpdates).Append(",\"stationaryUpdates\":").Append(s.StationaryUpdates);
                text.Append(",\"stageCalls\":[");Numbers(text,stages,i*StageCount,StageCount);
                text.Append("],\"stage_last_seen_age_ms\":[");
                for(int j=0;j<StageCount;j++){if(j>0)text.Append(',');long seen=lastSeen[i*StageCount+j];if(seen>=0)previous[j]=seen;text.Append(previous[j]<0?-1:Math.Max(0,Math.Min(stoppedAt,(i+1L)*1000)-previous[j]));}
                text.Append("],\"reasonOccurrences\":[");Numbers(text,reasons,i*ReasonCount,ReasonCount);text.Append("],\"reasonObservedOuterEpochs\":[");Numbers(text,reasonUpdates,i*ReasonCount,ReasonCount);text.Append("],\"gateAnyEvaluated\":").Append(s.GateEvaluated).Append(",\"gateAnyPassed\":").Append(s.GatePassed);
                text.Append(",\"outer\":");if(s.OuterCalls==0)text.Append("null");else OuterJson(text,s.Outer);
                text.Append(",\"blueprint\":");if(!s.BlueprintObserved)text.Append("null");else BlueprintJson(text,s.Blueprint);
                text.Append(",\"retainedSourceCount\":").Append(s.SourceCount-1).Append(",\"sources\":[");
                if(withSources && s.SourceCount>1)for(int n=0;n<Math.Min(4,s.SourceCount-1);n++){if(n>0)text.Append(',');SourceJson(text,sources[i*4+n]);}
                text.Append("]}");
            }
            text.Append("],\"events\":[");bool first=true;
            if(withEvents)
            {
                for(long seq=1;seq<=Math.Min(HeadEvents,sequence);seq++){if(!first)text.Append(',');first=false;EventJson(text,events[(int)seq-1],withTargets);}
                for(long seq=Math.Max(HeadEvents+1,sequence-TailEvents+1);seq<=sequence;seq++){if(!first)text.Append(',');first=false;EventJson(text,events[HeadEvents+(int)((seq-HeadEvents-1)%TailEvents)],withTargets);}
            }
            text.Append("],\"targets\":[");if(withTargets)for(int i=0;i<targetCount;i++){if(i>0)text.Append(',');TargetJson(text,targets[i]);}
            text.Append("],\"materials\":[");for(int i=0;i<materialCount;i++){if(i>0)text.Append(',');MaterialJson(text,materials[i]);}
            text.Append("],\"stop\":{\"reason\":").Append(Quote(stopReason)).Append(",\"elapsedMs\":").Append(stoppedAt).Append(",\"observerFailure\":").Append(Quote(failure)).Append("}}");
            return Encoding.UTF8.GetBytes(text.ToString());
        }
        private static void Names(StringBuilder text,Type type)
        {text.Append('[');var names=Enum.GetNames(type);for(int i=0;i<names.Length-1;i++){if(i>0)text.Append(',');text.Append(Quote(names[i]));}text.Append(']');}
        private static void Numbers(StringBuilder text,long[] values,int offset,int count)
        {for(int i=0;i<count;i++){if(i>0)text.Append(',');text.Append(values[offset+i]);}}
        private static void Row(StringBuilder text,string columns,params long[] values)
        {text.Append("{\"columns\":").Append(Quote(columns)).Append(",\"values\":[");Numbers(text,values,0,values.Length);text.Append("]}");}
        private static void OuterJson(StringBuilder text,BuildTraceOuter o)
        {
            text.Append("{\"frame\":").Append(o.Frame).Append(",\"gameUpdateCount\":").Append(o.GameUpdateCount).Append(",\"updateTick\":").Append(o.UpdateTick);
            text.Append(",\"inputState\":").Append(o.InputState).Append(",\"inputMapping\":").Append(o.InputMapping).Append(",\"worldAtInputBegin\":").Append(o.WorldAtInputBegin);
            text.Append(",\"playerState\":").Append(o.PlayerState).Append(",\"selected\":").Append(o.Selected).Append(",\"direction\":").Append(o.Direction);
            text.Append(",\"uiState\":").Append(o.UiState).Append(",\"animation\":").Append(o.Animation).Append(",\"itemTime\":").Append(o.ItemTime).Append(",\"reuse\":").Append(o.Reuse).Append(",\"chest\":").Append(o.Chest).Append(",\"talkNpc\":").Append(o.TalkNpc);
            text.Append(",\"x\":");Float(text,o.X);text.Append(",\"y\":");Float(text,o.Y);text.Append(",\"velocityX\":");Float(text,o.VelocityX);text.Append(",\"velocityY\":");Float(text,o.VelocityY);
            text.Append(",\"tileX\":").Append(o.TileX).Append(",\"tileY\":").Append(o.TileY).Append('}');
        }
        private static void Float(StringBuilder text,float value)
        {if(float.IsNaN(value)||float.IsInfinity(value))text.Append("null");else text.Append(value.ToString("R",CultureInfo.InvariantCulture));}
        private static void BlueprintJson(StringBuilder text,BuildTraceBlueprint b)
        {Row(text,"session,state,sceneRevision,catalogRevision,inventoryRevision,eligibilityRevision,supplyRevision,materialTick,projectionMatchTick,sceneCount,captureStatus,projectionMode,sourceCount,sourceBudget,searchBudget,materialState,sceneRestoreState",b.Session,b.State,b.SceneRevision,b.CatalogRevision,b.InventoryRevision,b.EligibilityRevision,b.SupplyRevision,unchecked((long)b.MaterialTick),unchecked((long)b.ProjectionMatchTick),b.SceneCount,b.CaptureStatus,b.ProjectionMode,b.SourceCount,b.SourceBudget,b.SearchBudget,b.MaterialState,b.SceneRestoreState);}
        private static void SourceJson(StringBuilder text,BuildTraceSource s)
        {Row(text,"instance,range,version,wake,nearVersion,nearWake,nearPlayer,nearSupply,nearCursor,forwardCursor,frontierCursor,left,top,width,height,flags,retryOrdinal,retryAt,materialTick,lastResetBefore,lastResetAfter,lastResetReasons",s.Instance,s.Range,s.Version,s.Wake,s.NearVersion,s.NearWake,s.NearPlayer,s.NearSupply,s.NearCursor,s.ForwardCursor,s.FrontierCursor,s.Left,s.Top,s.Width,s.Height,s.Flags,s.RetryOrdinal,unchecked((long)s.RetryAt),unchecked((long)s.MaterialTick),s.ResetBefore,s.ResetAfter,s.ResetReasons);}
        private void EventJson(StringBuilder text,BuildTraceEvent e,bool withTargets)
        {
            int slot=withTargets && e.Target>=0 && e.Target<targetCount && targets[e.Target].DetailId==e.TargetDetailId?e.Target:-1;
            Row(text,"seq,elapsedMs,stage,reason,targetDetailSlot,flight,token,nativeGeneration,a,b,c,d,targetDetailId",e.Sequence,e.At,e.Stage,e.Reason,slot,e.Flight,e.Token,e.Generation,e.A,e.B,e.C,e.D,e.TargetDetailId);
        }
        private static void TargetJson(StringBuilder text,BuildTraceTarget t)
        {Row(text,"session,instance,range,ordinal,rangeVersion,supportStamp,catalog,originX,originY,x,y,left,top,width,height,kind,tileType,wallType,itemType,style,alternate,expectedDirection,sourceSlot,sourceType,stackBefore,stackAfter,tileBoost,actualDirection,actualX,actualY,animation,itemTime,reuse,flags,flight,token,nativeGeneration,site,reason,elapsedMs,rangeX,rangeY,blockRange,pulley,pulleyDirection,choiceType,choiceStyle,choiceAlternate,choiceRandom,choiceX,choiceY,choiceElapsedMs,canPlaceResult,canPlaceElapsedMs,detailId",t.Session,t.Instance,t.Range,t.Ordinal,t.RangeVersion,t.SupportStamp,t.Catalog,t.OriginX,t.OriginY,t.X,t.Y,t.Left,t.Top,t.Width,t.Height,t.Kind,t.TileType,t.WallType,t.ItemType,t.Style,t.Alternate,t.ExpectedDirection,t.SourceSlot,t.SourceType,t.StackBefore,t.StackAfter,t.TileBoost,t.ActualDirection,t.ActualX,t.ActualY,t.Animation,t.ItemTime,t.Reuse,t.Flags,t.Flight,t.Token,t.NativeGeneration,t.Site,t.Reason,t.At,t.RangeX,t.RangeY,t.BlockRange,t.Pulley,t.PulleyDirection,t.ChoiceType,t.ChoiceStyle,t.ChoiceAlternate,t.ChoiceRandom,t.ChoiceX,t.ChoiceY,t.ChoiceAt,t.CanPlaceResult,t.CanPlaceAt,t.DetailId);}
        private static void MaterialJson(StringBuilder text,BuildTraceMaterial m)
        {Row(text,"itemType,matchingSlots,totalStack,eligibleSlots,eligibleStack,coveredSlots,sampleObserved,sampleTick,sampleElapsedMs,requestedElapsedMs,supplyObserved,supplyRevision,supplied,prepareVisited,prepareEligible,prepareFiltered",m.ItemType,m.MatchingSlots,m.Stack,m.EligibleSlots,m.EligibleStack,m.CoveredSlots,m.Sampled?1:0,unchecked((long)m.SampleTick),m.Sampled?m.SampleAt:-1,m.RequestedAt,m.SupplyObserved?1:0,m.SupplyObserved?m.SupplyRevision:-1,m.SupplyObserved?(m.Supplied?1:0):-1,m.PrepareVisited,m.PrepareEligible,m.PrepareFiltered);}
        private static string Quote(string value)
        {
            if(value==null)return "null";value=Limit(value,1024);var text=new StringBuilder(value.Length+2);text.Append('"');
            foreach(char c in value){if(c=='"'||c=='\\')text.Append('\\').Append(c);else if(c<32)text.Append("\\u").Append(((int)c).ToString("x4",CultureInfo.InvariantCulture));else text.Append(c);}
            return text.Append('"').ToString();
        }
    }
}
#endif
