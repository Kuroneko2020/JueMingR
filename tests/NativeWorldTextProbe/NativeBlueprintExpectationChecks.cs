using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ObjectData;
using Terraria.GameContent.UI;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static bool nearQueueHostFaultReported;
        private static Exception NearQueueHostFault(Exception __exception)
        {
            // The outer runtime records a terminal fault before fixture cleanup
            // can inspect it. Preserve the first exact source stack and rethrow
            // unchanged; this diagnostic never converts that failure to success.
            if (__exception != null && !nearQueueHostFaultReported)
            {
                nearQueueHostFaultReported = true;
                Console.Error.WriteLine("NEAR QUEUE HOST UPDATE FAULT: " + __exception);
            }
            return __exception;
        }
        internal static void RunNearQueue(object context,bool movingOnly=false,bool repairsOnly=false)
        {
            var host = Get(context, "Blueprint"); var tools = Get(context, "Tools"); var input = Get(context, "Input");
            NativeToolExecutionChecks.Initialize(context, null);
            Require((bool)Get(tools, "Available"), "near queue uses actual production tool hooks");
            Framing.Initialize(); Main.SetupTileMerge(); TorchID.Initialize();
            var lighting = Lighting.Mode; Lighting.Mode = Terraria.Graphics.Light.LightMode.Color;
            var patch = new Harmony("JueMingR.Tests.NearQueueOutlets");
            nearQueueHostFaultReported = false;
            var hostUpdate = host.GetType().GetMethod("Update", F, null, new[] { typeof(ulong) }, null);
            Require(hostUpdate != null, "near queue fault diagnostic binds the exact HostBlueprint.Update(ulong) entry");
            patch.Patch(hostUpdate, finalizer: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(NearQueueHostFault)));
            faultTools = tools; reportedNativeFault = false;
            patch.Patch(typeof(Player).GetMethod("ItemCheck", F), finalizer: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(NativeFault)));
            foreach (string name in new[] { "HandleSpecialEvent", "HandleMining", "HandleRunning" })
                patch.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name, F), prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SkipOutlet)));
            var tail = Get(host, "buildMaterialPreparation").GetType().GetMethod("NativeInputTailForChecks", F);
            Require(tail != null, "near queue invokes the locked original inventory input tail");
            constructionChecksActive = true; constructionInputTail = () => tail.Invoke(null, null);
            try
            {
                NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(host, "ControlsEnabled"); });
                using (NativeBlueprintWorldMatchChecks.EmptyWorld(host, 200, 160))
                {
                    if(repairsOnly){NearQueueGrandScreenReentry(context,host,tools,input);NearQueueNativeReentry(context,host,tools,input);return;}
                    QueueContinuousMovement(context,host,tools,input,false);
                    QueueContinuousMovement(context,host,tools,input,true);
                    if(movingOnly)return;
                    BusinessNearQueue(context, host, tools, input, false);
                    BusinessNearQueue(context, host, tools, input, false, warm: true);
                    BusinessNearQueue(context, host, tools, input, true);
                    BusinessNearQueue(context, host, tools, input, false, logicalCount: 4);
                    BusinessNearQueue(context, host, tools, input, false, logicalCount: 5);
                    BusinessNearQueue(context, host, tools, input, false, logicalCount: 8);
                    if (GetOptional(host, "NearBuildFacts") != null)
                    {
                        QueueFurnitureMovement(context, host, tools, input, false);
                        QueueFurnitureMovement(context, host, tools, input, true);
                        QueueMirrorCellMovement(context, host, tools, input);
                        QueueBudgetProgress(context, host, tools, input, false);
                        QueueBudgetProgress(context, host, tools, input, true);
                        QueueBudgetProgress(context, host, tools, input, true, true);
                        QueueActualSourceWaiting(context, host, tools, input);
                        QueueReachRecovery(context, host, tools, input, false);
                        QueueReachRecovery(context, host, tools, input, true);
                        QueueMissingWiringToolRecovery(context, host, tools, input);
                        QueueReachAdmissionFence(context, host, tools, input, true);
                        QueueReachAdmissionFence(context, host, tools, input, false);
                        NearQueueLifecycle(context, host, tools, input);
                        NearQueueLookahead(context, host, tools, input);
                        NearQueueNativeReentry(context, host, tools, input);
                        BusinessPreparedForeign(context, host, tools, input);
                        NearQueueExhaustedSource(context, host, tools, input, false);
                        NearQueueExhaustedSource(context, host, tools, input, true);
                        SupplyCurrentRestockPriority(context, host, tools, input);
                        SchedulerNativeHandoffCadence(context, host, tools, input);
                        NearQueueMaterialDependency(context, host, tools, input);
                        NearQueueGrandScreenReentry(context,host,tools,input);
                        NearQueueGrandDesignMovement(context, host, tools, input);
                        NearQueueGrandDesignMovement(context, host, tools, input, true);
                        long oneCapabilitySlots = NearQueueCapabilityRecovery(context, host, tools, input, 1);
                        long fourCapabilitySlots = NearQueueCapabilityRecovery(context, host, tools, input, 4);
                        Require(fourCapabilitySlots <= oneCapabilitySlots,
                            "four real missing-brush goals cannot multiply the one inventory publication's slot visits relative to one goal");
                        NearQueueLateNativeDependency(context, host, tools, input);
                        NearQueueRemoteScale(context, host, tools, input);
                        NearQueueWallOnlyScrape(context, host, tools, input);
                    }
                    else Console.WriteLine("NearQueue historical candidate lacks planner observations; new-planner lifecycle/lookahead NOT_EVALUATED");
                }
                Console.WriteLine("PASS BlueprintNearQueue: single captured/saved normalized mixed template, full furniture bodies, 9999 stock, original consumer gap replay and newly built supports; OS input/full Draw/multiplayer/FPS/owner acceptance NOT_RUN");
            }
            finally
            {
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "SetAutoBuild", false); Call(host, "ClearProjection");
                constructionChecksActive = false; constructionInputTail = null;
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                faultTools = null; Lighting.Clear(); Lighting.Mode = lighting;
            }
        }

        private static BusinessScope movingAdmissionScope;
        private static void MovingBuildAdmitted(object __instance,object __0)
        {
            var scope=movingAdmissionScope;if(scope==null || !ReferenceEquals(scope.Host,__instance))return;
            var group=GetOptional(__instance,"buildContinuation");
            int next=(int)Get(Get(__0,"Source"),"Type"),previous=group==null?0:(int)Get(Get(group,"Source"),"Type");
            if(previous==0 || previous==next)return;
            bool runnable=scope.Goals.Any(g=>g.Type==previous && scope.Runnable(g));
            Console.WriteLine("MovingBuild admission world="+Main.GameUpdateCount+" position="+Main.LocalPlayer.position+" previous="+previous+" next="+next+" previousRunnable="+runnable+" token="+Get(__0,"Token"));
            Require(!runnable,"actual foreign admission must see no real currently runnable A; later movement cannot retroactively revoke an admitted B");
        }
        // One real input/native-consumption trajectory owns moving service,
        // same-material order, natural foreign handoff and the shared work bill.
        // The dense arm obtains its old prefix through normal saved-source
        // extraction with an actual Laser Drill, never by inserting facts.
        private static Player SetupContinuousMovement(BusinessScope scope,bool dense)
        {
            var p=Reset(scope.Context,scope.Host,scope.Tools,scope.Input);p.position.X=44*16;
            var wall=BusinessStock(p,12,ItemID.StoneWall,9999);
            var platform=BusinessStock(p,13,ItemID.WoodPlatform,9999);
            var bench=BusinessStock(p,14,ItemID.WorkBench,9999);
            var chair=BusinessStock(p,15,ItemID.WoodenChair,9999);
            if(dense)p.inventory[16].SetDefaults(ItemID.LaserDrill);
            scope.Sources=new[]{wall,platform,bench,chair};
            var goals=new List<BusinessGoal>();
            int left=dense?28:37,right=dense?65:60,top=dense?30:39;
            // Reset's shared walking arena ends at x114. This source-only
            // extension belongs to the current fixture and is reset on replay.
            for(int x=89;x<=91+right-left;x++)for(int y=top-1;y<=40;y++)Main.tile[x,y].ClearEverything();
            // Source and destination are disjoint. Permanent overhead
            // supports never intersect the player's standing/walking body.
            for(int x=left;x<=right;x++)
            {
                NativeToolsChecks.Tile(x,38,TileID.Stone);NativeToolsChecks.Tile(90+x-left,38,TileID.Stone);
                for(int y=top;y<=39;y+=2)
                {
                    WorldGen.PlaceWall(90+x-left,y,wall.Item.createWall,true);
                    goals.Add(BusinessWallGoal(x,y,wall));
                    if(y!=39)WorldGen.PlaceWall(x,y-1,wall.Item.createWall,true);
                }
            }
            // Keep all four real consumers in the same normalized source.
            Require(WorldGen.PlaceTile(90+55-left,37,platform.Item.createTile,true,true,style:platform.Item.placeStyle),"moving source contains an original platform");
            goals.Add(BusinessTileGoal(55,37,platform));
            goals.Add(NearQueueFurniture(bench,90+57-left,37,57,37));
            goals.Add(NearQueueFurniture(chair,90+60-left,37,60,37));
            var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(scope.Context,scope.Host,scope.Input,(LibraryState)Get(scope.Host,"Library"),
                ()=>scope.Step(),90,Math.Min(top,36),right-left+1,40-Math.Min(top,36),"持续移动全料 "+dense);
            Place(scope.Context,scope.Host,scope.Input,entry,left,Math.Min(top,36));scope.Goals=goals.ToArray();
            return p;
        }
        private static void QueueContinuousMovement(object context,object host,object tools,object input,bool dense)
        {
            BusinessEvent[] automatic=null;var directions=new List<int>();var edgeRows=new Dictionary<uint,string>();int firstAutomatic=-1;
            using(var scope=new BusinessScope(context,host,tools,input))
            using(var observation=new NearQueueObservation(host,required:true))
            {
                var p=SetupContinuousMovement(scope,dense);
                var wall=scope.Sources[0];
                scope.Begin(true);scope.OrderCheckedAtAdmission=true;SetAutomatic(host,input,true);
                var admissionPatch=new Harmony("JueMingR.Tests.MovingBuildAdmission");movingAdmissionScope=scope;
                admissionPatch.Patch(host.GetType().GetMethod("RememberBuildContinuation",F),prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(MovingBuildAdmitted)));
                int updates=0,movedUses=0,nativeEdges=0,peakOutside=0,peakPrefix=0,peakUncurrent=0,peakFacts=0,peakWork=0,peakClassPrepare=0;
                int pauseMoves=0,pauseTicks=0,resumedUses=0,missed=0,maxMissed=0,initialPhysicalWait=0,dataEpoch=-1;
                bool initialData=false,paused=false,forward=true,partial=false,sawForeign=false;
                float initial=p.position.X,min=initial,max=initial;Rectangle? previous=null;
                var movingEvents=new List<uint>();
                try
                {
                    for(;updates<700;updates++)
                    {
                        // Movement is genuine original Player.Update input after
                        // Sample. A brief neutral window retains normal inertia.
                        int direction,wallUses=scope.Events.Count(e=>e.Type==wall.Type);
                        bool neutral=wallUses>=2 && !paused;
                        if(neutral){direction=0;if(++pauseTicks==6)paused=true;}
                        else if(wallUses<8){if(p.position.X>=47*16)forward=false;else if(p.position.X<=43*16)forward=true;direction=forward?1:-1;}
                        else {if(p.position.X>=58*16)forward=false;else if(p.position.X<=56*16)forward=true;direction=forward?1:-1;}
                        directions.Add(direction);
                        long factsBefore=(long)Get(host,"NearBuildFactChecks"),classes=(long)Get(host,"NearBuildClassifications"),prepares=(long)Get(host,"BuildCandidateChecks");
                        int events=scope.Events.Count;float before=p.position.X;
                        if(p.itemTime<=1)
                        {
                            Func<object,string> flight=v=>v==null?"none":Get(Get(v,"Source"),"Type")+":"+Get(Get(v,"Target"),"WorldX")+","+Get(Get(v,"Target"),"WorldY")+":checked="+Get(v,"Checked")+":rejected="+Get(v,"Rejected");
                            var head=GetOptional(host,"nearPreferredHead");var seed=head==null?null:Get(head,"Seed");
                            var foreign=GetOptional(host,"nearForeignHead");var group=GetOptional(host,"buildContinuation");
                            string runnable=string.Join(";",scope.Goals.Where(scope.Runnable).Take(3).Select(g=>g.Type+":"+g.X+","+g.Y));
                            edgeRows[Main.GameUpdateCount+1]="position="+p.position+" v="+p.velocity+" dir="+direction+" time/anim="+p.itemTime+"/"+p.itemAnimation+
                                " prepared="+flight(GetOptional(host,"preparedBuild"))+" active="+flight(GetOptional(host,"activeBuild"))+" head="+(head==null?"none":Get(Get(seed,"Cell"),"X")+":"+Get(head,"Ready")+":"+Get(head,"Pending"))+
                                " absence="+Get(host,"nearAbsenceReady")+" group="+(group==null?"none":Get(group,"Group")+":"+Get(group,"Pending"))+" foreign="+(foreign==null?"none":Get(foreign,"Type")+":"+Get(foreign,"Ready")+":"+Get(foreign,"Pending"))+" runnable="+runnable;
                        }
                        if(!initialData)
                        {
                            var delivered=((IDictionary)Get(host,"nearBuildRanges")).Values.Cast<object>().ToArray();
                            // One-time source prerequisite, not executable Ready
                            // or first consumption. Later movement/Pending never
                            // closes the physical-opportunity observation window.
                            initialData=(bool)Get(host,"nearCoverageCurrent") && delivered.Length>0 && delivered.All(r=>(bool)Get(r,"Complete") && (bool)Get(r,"RawCurrent"));
                            if(initialData){dataEpoch=updates;Console.WriteLine("MovingBuild initial-data dense="+dense+" update="+updates+" world="+Main.GameUpdateCount+" facts="+Get(host,"NearBuildFacts")+" directoryAndSourceCovered=True executableReadyNotRead=True firstConsumerSeen="+(scope.Events.Count>0));}
                        }
                        bool sameRunnable=scope.CurrentType!=0 && scope.Goals.Any(g=>g.Type==scope.CurrentType && scope.Runnable(g));
                        Func<BusinessGoal,bool> qualified=g=>scope.Runnable(g) && scope.Sources.Any(v=>v.Type==g.Type && (bool)Call(tools,"BuildInventoryEligible",v.Slot,v.Item));
                        bool opportunity=p.itemTime<=1 && p.reuseDelay==0 && (sameRunnable && p.itemAnimation>0 && p.HeldItem.type==scope.CurrentType ||
                            p.itemAnimation==0 && p.releaseUseItem) && scope.Goals.Any(qualified);
                        observation.Step(scope,()=>scope.Step(direction:direction));
                        int dl,dt,dr,db;TileReachCheckSettings.Simple.GetTileRegion(p,out dl,out dt,out dr,out db,wall.Item.tileBoost+p.blockRange);
                        var region=new Rectangle(dl,dt,dr-dl+1,db-dt+1);
                        if(previous.HasValue && previous.Value!=region)nativeEdges++;previous=region;
                        min=Math.Min(min,p.position.X);max=Math.Max(max,p.position.X);
                        if(firstAutomatic<0 && scope.Events.Count>0)firstAutomatic=updates;
                        bool moved=p.position.X!=before;
                        if(neutral && moved)pauseMoves++;
                        if(moved && scope.Events.Count>events){movedUses+=scope.Events.Count-events;movingEvents.Add(Main.GameUpdateCount);if(paused)resumedUses+=scope.Events.Count-events;}
                        partial|=scope.Goals.Any(g=>g.Type==wall.Type && !g.Complete && !p.IsInTileInteractionRange(g.X,g.Y,TileReachCheckSettings.Simple,wall.Item.tileBoost+p.blockRange)) &&
                            scope.Goals.Any(g=>g.Type==wall.Type && scope.Runnable(g));
                        sawForeign|=scope.Events.Any(e=>e.Type!=wall.Type);
                        var facts=((IDictionary)Get(host,"nearBuildRanges")).Values.Cast<object>().SelectMany(r=>((IDictionary)Get(r,"Facts")).Values.Cast<object>()).ToArray();
                        int outside=facts.Count(f=>{var seed=Get(f,"Seed");var cell=(CaptureCell)Get(seed,"Cell");var owner=Get(seed,"Owner");return Convert.ToString(Call(host,"BuildLane",(int)Get(seed,"Ordinal")))=="WallBody" &&
                            !p.IsInTileInteractionRange((int)Get(owner,"OriginX")+cell.X,(int)Get(owner,"OriginY")+cell.Y,TileReachCheckSettings.Simple,wall.Item.tileBoost+p.blockRange);});
                        peakOutside=Math.Max(peakOutside,outside);peakFacts=Math.Max(peakFacts,facts.Length);
                        int prefix=0;
                        foreach(var fact in facts)
                        {
                            var seed=Get(fact,"Seed");if(Convert.ToString(Call(host,"BuildLane",(int)Get(seed,"Ordinal")))!="WallBody")continue;
                            var cell=(CaptureCell)Get(seed,"Cell");var owner=Get(seed,"Owner");int x=(int)Get(owner,"OriginX")+cell.X,y=(int)Get(owner,"OriginY")+cell.Y;
                            if(scope.Goals.Any(g=>g.Type==wall.Type && g.X==x && g.Y==y && scope.Runnable(g)))break;
                            if(!p.IsInTileInteractionRange(x,y,TileReachCheckSettings.Simple,wall.Item.tileBoost+p.blockRange))prefix++;
                        }
                        if(scope.CurrentType==wall.Type && scope.Goals.Any(g=>g.Type==wall.Type && scope.Runnable(g)))peakPrefix=Math.Max(peakPrefix,prefix);
                        peakUncurrent=Math.Max(peakUncurrent,facts.Count(f=>(bool)Get(f,"Pending") || (long)Get(f,"Player")<0 || (long)Get(f,"Version")!=(long)Get(Get(Get(f,"Seed"),"Range"),"Version")));
                        peakWork=Math.Max(peakWork,checked((int)((long)Get(host,"NearBuildFactChecks")-factsBefore)));
                        int charged=checked((int)((long)Get(host,"NearBuildClassifications")-classes+(long)Get(host,"BuildCandidateChecks")-prepares));
                        peakClassPrepare=Math.Max(peakClassPrepare,charged);Require(charged<=64,"moving classification and actual preparation share the original 64 allowance");
                        // Independent opportunities use the real current native
                        // clocks/release and known physical goal oracle. Pending
                        // or a planner Ready bit never defines this denominator.
                        if(!initialData && opportunity && scope.Events.Count==events)initialPhysicalWait++;
                        missed=initialData && opportunity && scope.Events.Count==events && scope.Goals.Any(qualified)?missed+1:0;maxMissed=Math.Max(maxMissed,missed);
                        if(missed==3)foreach(var row in edgeRows.Where(row=>row.Key>=Main.GameUpdateCount-4))Console.Error.WriteLine("MovingBuild opportunity-miss world="+row.Key+" "+row.Value);
                        if(updates>150 && sawForeign && !scope.Goals.Any(scope.Runnable) && !(bool)Get(Get(tools,"Use"),"Active"))break;
                    }
                    scope.Report("continuous moving dense="+dense);
                    Console.WriteLine("MovingBuild dense="+dense+" updates="+updates+" actualX="+min+".."+max+" nativeEdges="+nativeEdges+
                        " movingUses="+movedUses+" resumedUses="+resumedUses+" partialA="+partial+" foreign="+sawForeign+" cachedOutsideA="+peakOutside+" oldPrefix="+peakPrefix+" uncurrent="+peakUncurrent+" facts="+peakFacts+
                        " peakVisit="+peakWork+" peakClassPrepare="+peakClassPrepare+" initialDataEpoch="+dataEpoch+" physicalWaitBeforeSource="+initialPhysicalWait+" maxIndependentMissed="+maxMissed+" neutralWindowInertialMoves="+pauseMoves+
                        " movingWorlds="+string.Join(",",movingEvents)+" nativeEventGaps="+string.Join(",",scope.Events.Zip(scope.Events.Skip(1),(a,b)=>unchecked(b.World-a.World))));
                    Require(updates<700 && max-min>16 && nativeEdges>2 && movedUses>=3 && paused && resumedUses>0 && partial && sawForeign,
                        "one abundant-stock trajectory consumes repeatedly while moving across real domains, briefly pauses/resumes, keeps available A and then naturally admits B");
                    Require(scope.Sources.All(source=>source.Item.stack>9000 && source.Item.stack==9999-scope.Events.Where(e=>e.Type==source.Type).Sum(e=>e.Count)),
                        "every moving native world effect has exactly its real debit while all stocks remain above 9000");
                    int nativeActionWindow=scope.Events.Where(e=>e.Time>0).Min(e=>e.Time);
                    Require(initialData && maxMissed<nativeActionWindow,"after actual directory/source delivery, known reachable qualified goals cannot miss an entire measured original action window while clocks permit use");
                    if(dense)Require(peakOutside>=32 && peakPrefix>=32 && peakUncurrent>64 && peakFacts>64,"normal source discovery actually retains at least 32 short-material out-of-range A facts and exceeds one classification allowance");
                    if(GetOptional(host,"nearBuildVisits")!=null)Require(peakWork<=64,"all actual fact visits, including cheap current/retained visits, remain bounded instead of moving work to free whole-plan scans");
                    automatic=scope.Events.ToArray();
                    long starts=(long)Get(host,"BuildUsesStarted");SetAutomatic(host,input,false);
                    for(int n=0;n<50;n++)observation.Step(scope,()=>scope.Step(direction:1));
                    Require(starts==(long)Get(host,"BuildUsesStarted"),"stopping during native movement starts no new automatic action while the original finite tail settles");
                }
                catch{scope.Report("moving failure dense="+dense);Console.Error.WriteLine("MovingBuild failed update="+updates+" position="+p.position+" velocity="+p.velocity+" facts="+peakFacts+" outside="+peakOutside+" uses="+movedUses+" foreign="+sawForeign+" missed="+maxMissed);throw;}
                finally{movingAdmissionScope=null;foreach(var method in admissionPatch.GetPatchedMethods().ToArray())admissionPatch.Unpatch(method,HarmonyPatchType.All,admissionPatch.Id);}
            }
            using(var scope=new BusinessScope(context,host,tools,input))
            {
                var p=SetupContinuousMovement(scope,dense);scope.Begin(false);int cursor=0;
                for(int frame=0;frame<directions.Count+80 && cursor<automatic.Length;frame++)
                {
                    int direction=directions[Math.Min(frame,directions.Count-1)];
                    if(frame<firstAutomatic){scope.Step(direction:direction);continue;}
                    var expected=automatic[cursor];var source=scope.Sources.Single(v=>v.Type==expected.Type);
                    // The reference performs the same captured right-facing
                    // chair through a legal right control, instead of borrowing
                    // the automatic direction field. Its reach/support oracle
                    // still fences each native use during actual movement.
                    if(expected.Type==ItemID.WoodenChair && p.itemTime==0 && p.itemAnimation==0 && p.releaseUseItem)direction=1;
                    if(p.selectedItem!=source.Slot)
                    {
                        if(!p.selectedItemState.HasBufferedChange)p.selectedItemState.Select(source.Slot);
                        if(!p.selectedItemState.CanChangeSelectedItemImmediately){scope.Step(direction:direction);continue;}
                    }
                    int before=scope.Events.Count;
                    var goal=scope.Goals.Single(g=>g.X==expected.X && g.Y==expected.Y && g.Type==expected.Type);
                    bool held=(scope.Runnable(goal) || p.itemAnimation>0) && (p.itemAnimation>0 || p.releaseUseItem || source.Item.autoReuse);
                    scope.Step(new Vector2(expected.X*16+8,expected.Y*16+8),held,direction);
                    if(scope.Events.Count>before)
                    {
                        Require(scope.Events.Count==before+1 && scope.Events.Last().Type==expected.Type && scope.Events.Last().Count==expected.Count,
                            "same saved world and movement replay use the identical original consumer and exact debit");
                        cursor++;
                    }
                }
                scope.Report("continuous moving original dense="+dense+" cursor="+cursor+" position="+p.position);
                Console.WriteLine("MovingBuild original coverage dense="+dense+" reached="+cursor+" automatic="+automatic.Length+" alignment=legal-input-reference; per-frame directions and one chair facing input do not prove identical later positions/domains");
                for(int i=1;i<Math.Min(automatic.Length,scope.Events.Count);i++)
                {
                    uint actual=unchecked(automatic[i].World-automatic[i-1].World),native=unchecked(scope.Events[i].World-scope.Events[i-1].World);
                    Console.WriteLine("MovingBuild original-gap dense="+dense+" index="+i+" previous="+automatic[i-1].Type+" next="+automatic[i].Type+" automatic="+actual+" original="+native+" extra="+((long)actual-native));
                    // These intervals are observations, not a state-equivalence
                    // claim. Actual-side qualified reach/body/support and native
                    // opportunity misses above decide whether scheduling waited.
                }
            }
        }

        private sealed class NearQueueObservation : IDisposable
        {
            private readonly object host;
            private readonly bool available;
            private readonly bool workChecksAvailable;
            private long peakBytes, peakSource, steps, classifications, eligibilityReads, peakEligibilityReads;
            private long factChecks, retirementChecks, rangeChecks,nativeRegions,peakNativeRegions;
            private long headChecks, proofChecks, capabilityChecks, peakHeads, peakProofs, peakCapabilities;
            private int peakFacts, peakRanges;
            internal NearQueueObservation(object host, bool required)
            {
                this.host = host; available = GetOptional(host, "NearBuildFacts") != null;
                workChecksAvailable = GetOptional(host, "NearBuildFactChecks") != null;
                Require(!required || available, "new planner validation requires its actual read-only work observations");
                Require(!required || workChecksAvailable, "new planner performance validation requires actual fact/retirement/range work counters");
            }
            internal void Step(BusinessScope scope, Action nativeStep = null)
            {
                long beforeSteps = available ? (long)Get(host, "NearBuildSemanticSteps") : 0;
                long beforeClasses = available ? (long)Get(host, "NearBuildClassifications") : 0;
                long beforeReads = (long)Get(host, "BuildEligibilityReads");
                long beforeFacts = workChecksAvailable ? (long)Get(host, "NearBuildFactChecks") : 0;
                long beforeRetirement = workChecksAvailable ? (long)Get(host, "NearBuildRetirementChecks") : 0;
                long beforeRanges = workChecksAvailable ? (long)Get(host, "NearBuildRangeChecks") : 0;
                long beforeHeads = workChecksAvailable ? (long)Get(host, "NearBuildHeadChecks") : 0;
                long beforeProofs = workChecksAvailable ? (long)Get(host, "NearBuildProofChecks") : 0;
                long beforeCapabilities = workChecksAvailable ? (long)Get(host, "NearBuildCapabilityChecks") : 0;
                long beforeRegions=GetOptional(host,"NearBuildNativeRegionChecks") is long?(long)Get(host,"NearBuildNativeRegionChecks"):0;
                if (nativeStep == null) scope.Step(); else nativeStep();
                if(GetOptional(host,"NearBuildNativeRegionChecks") is long){long regions=(long)Get(host,"NearBuildNativeRegionChecks")-beforeRegions;nativeRegions+=regions;peakNativeRegions=Math.Max(peakNativeRegions,regions);}
                // Native eligibility reads predate the new planner. Measure
                // them independently for historical candidates as well.
                long reads = (long)Get(host, "BuildEligibilityReads") - beforeReads;
                eligibilityReads += reads; peakEligibilityReads = Math.Max(peakEligibilityReads, reads);
                if (!available) return;
                long ds = (long)Get(host, "NearBuildSemanticSteps") - beforeSteps;
                long dc = (long)Get(host, "NearBuildClassifications") - beforeClasses;
                Require(ds >= 0 && ds <= 64 && dc >= 0 && dc <= 64,
                    "all nearby business groups share one Update's semantic-source and classification allowance");
                steps += ds; classifications += dc;
                if (workChecksAvailable)
                {
                    factChecks += (long)Get(host, "NearBuildFactChecks") - beforeFacts;
                    retirementChecks += (long)Get(host, "NearBuildRetirementChecks") - beforeRetirement;
                    rangeChecks += (long)Get(host, "NearBuildRangeChecks") - beforeRanges;
                    long heads = (long)Get(host, "NearBuildHeadChecks") - beforeHeads, proofs = (long)Get(host, "NearBuildProofChecks") - beforeProofs;
                    long capabilities = (long)Get(host, "NearBuildCapabilityChecks") - beforeCapabilities;
                    headChecks += heads; proofChecks += proofs; capabilityChecks += capabilities;
                    peakHeads = Math.Max(peakHeads, heads); peakProofs = Math.Max(peakProofs, proofs); peakCapabilities = Math.Max(peakCapabilities, capabilities);
                }
                peakBytes = Math.Max(peakBytes, (long)Get(host, "NearBuildPlanBytes"));
                peakSource = Math.Max(peakSource, (long)Get(host, "buildSourceBytes"));
                peakFacts = Math.Max(peakFacts, (int)Get(host, "NearBuildFacts"));
                peakRanges = Math.Max(peakRanges, ((IDictionary)Get(host, "nearBuildRanges")).Count);
            }
            public void Dispose()
            {
                Console.WriteLine("NearQueue work observed=" + available + " semantic=" + steps + " classifications=" + classifications +
                    " peakFacts=" + peakFacts + " peakRanges=" + peakRanges + " estimatedPlanBytes=" + peakBytes + " actualRetainedSourceBytes=" + peakSource +
                    " plannerNativeRegions="+nativeRegions+" maxUpdatePlannerNativeRegions="+peakNativeRegions+
                    " nativeEligibilityReads=" + eligibilityReads + " maxUpdateEligibilityReads=" + peakEligibilityReads +
                    " factWorkObserved=" + workChecksAvailable + " factChecks=" + factChecks + " retirementChecks=" + retirementChecks + " rangeMetadataChecks=" + rangeChecks +
                    " headChecks=" + headChecks + " maxUpdateHeads=" + peakHeads + " proofChecks=" + proofChecks + " maxUpdateProofs=" + peakProofs +
                    " capabilityChecks=" + capabilityChecks + " maxUpdateCapabilities=" + peakCapabilities);
            }
        }

        private static void NearQueueLifecycle(object context, object host, object tools, object input)
        {
            using (var scope = new BusinessScope(context, host, tools, input))
            using (var observation = new NearQueueObservation(host, required: true))
            {
                NearQueueSetup(scope, false, false, 7); scope.Begin(true); SetAutomatic(host, input, true);
                NativeQuickItemChecks.Until(() => { observation.Step(scope); return (int)Get(host, "NearBuildFacts") > 0; });
                long semantic = (long)Get(host, "NearBuildSemanticSteps"), classified = (long)Get(host, "NearBuildClassifications");
                long uses = (long)Get(host, "BuildUsesStarted");
                Func<long[]> capabilityWork = () => new[] { (long)Get(host, "NearBuildCapabilityChecks"),
                    (long)Get(host, "NearBuildCapabilitySlotChecks"), (long)Get(host, "NearBuildCapabilityDependencyChecks") };
                var noInputCapability = capabilityWork();
                for (int n = 0; n < 8; n++) { Call(input, "BeginUpdate"); Call(context, "UpdateRuntime"); }
                Require(semantic == (long)Get(host, "NearBuildSemanticSteps") && classified == (long)Get(host, "NearBuildClassifications") && uses == (long)Get(host, "BuildUsesStarted"),
                    "outer updates without native world/input epochs preserve work without advancing any planner business or native use");
                Require(noInputCapability.SequenceEqual(capabilityWork()),
                    "outer updates without native input perform zero capability work, including shared slot and dependency publication");
                SetAutomatic(host, input, false);
                NativeQuickItemChecks.Until(() => { scope.Step(); return !(bool)Get(Get(tools, "Use"), "Active") && !(bool)Get(Get(host, "buildMaterialPreparation"), "HasWork"); });
                semantic = (long)Get(host, "NearBuildSemanticSteps"); classified = (long)Get(host, "NearBuildClassifications");
                var emptyPlan = Get(host, "nearBuildDiscovery");
                long emptyFactChecks = (long)Get(host, "NearBuildFactChecks"), emptyRetirementChecks = (long)Get(host, "NearBuildRetirementChecks");
                var emptyCapability = capabilityWork();
                int events = scope.Events.Count;
                Call(host, "SetMaterialViewDemand", true);
                try
                {
                    for (int n = 0; n < 16; n++) observation.Step(scope);
                    Require(emptyCapability.SequenceEqual(capabilityWork()),
                        "material-view-only updates after natural cleanup perform no automatic capability publication or dependency checks");
                    Require(semantic == (long)Get(host, "NearBuildSemanticSteps") && classified == (long)Get(host, "NearBuildClassifications") &&
                        scope.Events.Count == events && (int)Get(host, "NearBuildFacts") == 0 && ((IDictionary)Get(host, "nearBuildRanges")).Count == 0 &&
                        ReferenceEquals(emptyPlan, Get(host, "nearBuildDiscovery")) && emptyFactChecks == (long)Get(host, "NearBuildFactChecks") &&
                        emptyRetirementChecks == (long)Get(host, "NearBuildRetirementChecks"),
                        "material-view-only sampling with construction OFF neither advances business nor rebuilds an already empty plan owner");
                }
                finally { Call(host, "SetMaterialViewDemand", false); }
                for (int n = 0; n < 8; n++) observation.Step(scope);
                Require(emptyCapability.SequenceEqual(capabilityWork()),
                    "ordinary OFF updates after cleanup perform zero total, slot and dependency capability work");
                Require(ReferenceEquals(emptyPlan, Get(host, "nearBuildDiscovery")) && emptyFactChecks == (long)Get(host, "NearBuildFactChecks") &&
                    emptyRetirementChecks == (long)Get(host, "NearBuildRetirementChecks"),
                    "plain OFF updates preserve the same already empty query owner without traversing facts or retirement candidates");
                Console.WriteLine("NearQueue OFF/UI-only emptyOwnerRetained=True factChecks=" + ((long)Get(host, "NearBuildFactChecks") - emptyFactChecks) +
                    " retirementChecks=" + ((long)Get(host, "NearBuildRetirementChecks") - emptyRetirementChecks));
                SetAutomatic(host, input, true);
                NativeQuickItemChecks.Until(() => { observation.Step(scope); return (int)Get(host, "NearBuildFacts") > 0; });
                var previousPlan = Get(host, "nearBuildDiscovery");
                Main.LocalPlayer.selectedItemState.Select(2); Main.LocalPlayer.selectedItemState.Update();
                observation.Step(scope);
                Require(!ReferenceEquals(previousPlan, Get(host, "nearBuildDiscovery")),
                    "actual manual Select retires the old nearby query owner before a successor can plan or consume");
                SetAutomatic(host, input, false); Call(host, "ClearProjection"); scope.Step();
                Require((int)Get(host, "NearBuildFacts") == 0 && ((IDictionary)Get(host, "nearBuildRanges")).Count == 0,
                    "projection retirement releases every semantic fact and range rather than keeping prior traveled regions alive");
            }
        }

        private static HashSet<int> NearQueueReadyTypes(object host)
        {
            var types = new HashSet<int>();
            foreach (object range in ((IDictionary)Get(host, "nearBuildRanges")).Values)
                foreach (object fact in ((IDictionary)Get(range, "Facts")).Values)
                    if ((bool)Get(fact, "Ready") && !(bool)Get(fact, "Pending")) types.Add((int)Get(fact, "Type"));
            return types;
        }

        private static object grandScreenReentryHost;
        private static bool grandScreenEntering;
        private static void GrandScreenReentryAdmission(object __instance,object __0)
        {
            if(!ReferenceEquals(__instance,grandScreenReentryHost) || !grandScreenEntering || (int)Get(Get(__0,"Source"),"Type")!=ItemID.StoneBlock)return;
            Require(Main.tile[96,42].wire2(),"a GD A re-entering the real screen must consume before genuinely admitting the newly unblocked ordinary B");
        }
        private static Rectangle GrandScreenTileBounds()
        {
            int left=(int)Math.Floor(Main.screenPosition.X/16d),top=(int)Math.Floor(Main.screenPosition.Y/16d);
            return new Rectangle(left,top,(int)Math.Ceiling((Main.screenPosition.X+Main.screenWidth)/16d)-left,(int)Math.Ceiling((Main.screenPosition.Y+Main.screenHeight)/16d)-top);
        }
        private static void NearQueueGrandScreenReentry(object context,object host,object tools,object input)
        {
            using(var scope=new BusinessScope(context,host,tools,input))
            using(var observation=new NearQueueObservation(host,required:true))
            {
                var p=Reset(context,host,tools,input);p.inventory[12].SetDefaults(3611);var gd=p.inventory[12];
                var stone=BusinessStock(p,13,ItemID.StoneBlock,9999);var wire=BusinessStock(p,20,ItemID.Wire,9999);
                scope.Sources=new[]{stone};scope.Goals=new[]{BusinessTileGoal(46,42,stone)};
                NativeToolsChecks.Tile(46,42,TileID.Dirt);
                var screen=Main.screenPosition;var mode=WiresUI.Settings.ToolMode;
                var patch=new Harmony("JueMingR.Tests.GrandScreenReentry");
                try
                {
                    WiresUI.Settings.ToolMode=WiresUI.Settings.MultiToolMode.Green|WiresUI.Settings.MultiToolMode.Cutter;
                    // The tile is already in coarse discovery, but its center
                    // is 0.25px outside the actual GD consumer's right edge.
                    Main.screenPosition=new Vector2(96*16+8-Main.screenWidth-0.25f,400);
                    Place(context,host,input,Asset(host,new CaptureCell{Layers=CaptureLayer.Wire,Wires=2},
                        new CaptureCell{X=2,Layers=CaptureLayer.Tile,TileType=TileID.Stone},new CaptureCell{X=52,Layers=CaptureLayer.Wire,Wires=2}),44,42);
                    scope.Begin(true);SetAutomatic(host,input,true);object rejected=null;bool entry=false;long version=0,support=0;float start=p.position.X;
                    var screenTiles=GrandScreenTileBounds();
                    Action<int> native=direction=>
                    {
                        NativeToolExecutionChecks.Sample(context,input,new Vector2(480,540),false);
                        p.controlLeft=direction<0;p.controlRight=direction>0;PlayerInput.Triggers.Current.Left=direction<0;PlayerInput.Triggers.Current.Right=direction>0;
                        float before=p.position.X;Call(Get(scope,"pending"),"Clear");NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(scope,"FinishNativeStep");
                        Main.screenPosition.X+=p.position.X-before;
                        if(rejected!=null && !entry && (bool)Call(host,"GrandBuildPointVisible",p,96,42))
                        {
                            entry=true;grandScreenEntering=true;
                            Require(GrandScreenTileBounds()==screenTiles && !p.IsInTileInteractionRange(96,42,TileReachCheckSettings.Simple,gd.tileBoost+p.blockRange) &&
                                (long)Get(rejected,"Version")==version && (long)Get(rejected,"Support")==support,
                                "the SAME retained Player-negative enters GD by target-center visibility while coarse screen tiles, native out-of-range result and its world proof stay unchanged");
                            Console.WriteLine("NearQueue GD center-entry world="+Main.GameUpdateCount+" displacement="+(p.position.X-start)+" sameCoarseTiles=True nativeReach=False priorWait="+Get(rejected,"Wait")+" priorPending="+Get(rejected,"Pending"));
                            WorldGen.KillTile(46,42,false,false,true);Require(!Main.tile[46,42].active(),"original writer makes ordinary B actually legal only after A becomes visible");
                        }
                        for(int i=0;i<Main.projectile.Length;i++)if(Main.projectile[i].active && Main.projectile[i].type==651 && Main.projectile[i].owner==Main.myPlayer)Main.projectile[i].Update(i);
                        Call(context,"UpdateRuntime");constructionInputTail?.Invoke();
                    };
                    NativeQuickItemChecks.Until(()=>
                    {
                        observation.Step(scope,()=>native(0));
                        rejected=QueueRecoveryFacts(host).FirstOrDefault(f=>{var seed=Get(f,"Seed");return (int)Get(Get(seed,"Cell"),"X")==52 &&
                            Convert.ToString(Get(f,"Wait")).Contains("Player") && !(bool)Get(f,"Pending");});
                        return rejected!=null && Main.tile[44,42].wire2() && GetOptional(host,"grandBuild")==null && !(bool)Get(host,"nearVisitNeeded");
                    });
                    version=(long)Get(rejected,"Version");support=(long)Get(rejected,"Support");
                    Require(!Main.tile[96,42].wire2() && !(bool)Call(host,"GrandBuildPointVisible",p,96,42) && wire.Item.stack==9998 && stone.Item.stack==9999,
                        "GD and 9999-stock sources remained present while the saved far A naturally acquired a stable offscreen Player rejection");
                    Console.WriteLine("NearQueue GD saved-negative world="+Main.GameUpdateCount+" factVersion="+version+" support="+support+" awake="+Get(host,"nearVisitNeeded")+" GDAlwaysPresent=True");
                    grandScreenReentryHost=host;grandScreenEntering=false;
                    patch.Patch(host.GetType().GetMethod("RememberBuildContinuation",F),prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(GrandScreenReentryAdmission)));
                    NativeQuickItemChecks.Until(()=>{observation.Step(scope,()=>native(1));return entry && Main.tile[96,42].wire2() && scope.Goals[0].Complete && !(bool)Get(Get(tools,"Use"),"Active");});
                    Require(p.position.X>start && entry && scope.Events.Count==1 && stone.Item.stack==9998 && wire.Item.stack==9997,
                        "same-direction original movement wakes the saved GD negative, commits its exact wire and then ordinary B once with both stocks still above9000");
                }
                finally{grandScreenReentryHost=null;grandScreenEntering=false;foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);WiresUI.Settings.ToolMode=mode;Main.screenPosition=screen;}
            }
        }

        private static void NearQueueGrandDesignMovement(object context, object host, object tools, object input, bool separateFurniture = false)
        {
            using (var scope = new BusinessScope(context, host, tools, input))
            using (var observation = new NearQueueObservation(host, required: true))
            {
                var p = Reset(context, host, tools, input);
                p.inventory[12].SetDefaults(3611);
                var stone = BusinessStock(p, 13, separateFurniture ? ItemID.WorkBench : ItemID.StoneBlock, separateFurniture ? 9999 : 4);
                p.inventory[20].SetDefaults(ItemID.Wire); p.inventory[20].stack = 3; var wire = p.inventory[20];
                scope.Sources = new[] { stone }; scope.Goals = new[] { BusinessTileGoal(57, 42, stone) };
                var screen = Main.screenPosition; var mode = WiresUI.Settings.ToolMode;
                try
                {
                    WiresUI.Settings.ToolMode = WiresUI.Settings.MultiToolMode.Green | WiresUI.Settings.MultiToolMode.Cutter;
                    LibraryEntry entry;
                    if(separateFurniture)
                    {
                        NativeToolsChecks.Tile(83,43,TileID.Stone);NativeToolsChecks.Tile(84,43,TileID.Stone);
                        scope.Goals=new[]{NearQueueFurniture(stone,83,42,57,42)};
                        WorldGen.PlaceWire2(70,42);WorldGen.PlaceWire2(82,42);
                        entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>scope.Step(),70,42,15,1,"GD独立家具范围");
                    }
                    else entry=Asset(host,
                        new CaptureCell { Layers = CaptureLayer.Wire, Wires = 2 },
                        new CaptureCell { X = 12, Layers = CaptureLayer.Wire, Wires = 2 },
                        new CaptureCell { X = 13, Layers = CaptureLayer.Tile, TileType = TileID.Stone });
                    var instance=Place(context,host,input,entry,44,42);
                    var actualRanges=((IEnumerable)Get(instance,"MatchRanges")).Cast<object>().ToArray();
                    Require((separateFurniture?actualRanges.Length==2:actualRanges.Length==1) && !scope.Runnable(scope.Goals[0]) &&
                        (bool)Call(host,"GrandBuildPointVisible",p,56,42),"GD fixture has the declared actual range separation and native out-of-range ordinary goal");
                    if(separateFurniture)Require(actualRanges.Count(range=>(int)Get(range,"ObjectId")>0)==1,
                        "captured complete native bench occupies a distinct object range from wiring");
                    scope.Begin(true); SetAutomatic(host, input, true);
                    Action<int> native = direction =>
                    {
                        // Same native order as the established wiring fixture:
                        // Player.Update -> exact owned 651 -> Runtime -> UI tail.
                        NativeToolExecutionChecks.Sample(context, input, new Vector2(480, 540), false);
                        p.controlLeft = direction < 0; p.controlRight = direction > 0;
                        PlayerInput.Triggers.Current.Left = direction < 0; PlayerInput.Triggers.Current.Right = direction > 0;
                        Call(Get(scope, "pending"), "Clear"); NativeQuickItemChecks.BeginWorldStep(); p.Update(0);
                        Call(scope, "FinishNativeStep");
                        for (int i = 0; i < Main.projectile.Length; i++)
                            if (Main.projectile[i].active && Main.projectile[i].type == 651 && Main.projectile[i].owner == Main.myPlayer) Main.projectile[i].Update(i);
                        Call(context, "UpdateRuntime"); constructionInputTail?.Invoke();
                    };
                    bool domainChecked = false;
                    NativeQuickItemChecks.Until(() =>
                    {
                        observation.Step(scope, () => native(0));
                        if (!domainChecked && Main.tile[44, 42].wire2())
                        {
                            Require((bool)Get(Get(host, "MaterialInventory"), "Ready"),
                                "the near-wire native commit is followed by a qualified inventory publication before measuring the planner domain");
                            // The bare tool's vanilla reach is not the shared
                            // planner's conservative ordinary envelope. Read
                            // that actual pure domain after real admission.
                            var ordinary = (BlueprintProjectionBounds)Call(host, "BuildNearOrdinaryWorldBounds", p);
                            bool visible = (bool)Call(host, "GrandBuildPointVisible", p, 56, 42);
                            Console.WriteLine("NearQueue GD domain ordinary=" + ordinary.Left + "," + ordinary.Top + ".." + ordinary.Right + "," + ordinary.Bottom +
                                " screen=" + Main.screenPosition + ":" + Main.screenWidth + "x" + Main.screenHeight +
                                " far=56,42 ordinaryContains=" + ordinary.Contains(56, 42) + " GDVisible=" + visible);
                            Require(!ordinary.Contains(56, 42) && visible,
                                "the real far-wire point lies outside the production ordinary planner envelope while remaining in Grand Design's visible screen domain");
                            domainChecked = true;
                        }
                        return domainChecked && Main.tile[44, 42].wire2() && Main.tile[56, 42].wire2() && GetOptional(host, "grandBuild") == null;
                    });
                    Require(wire.stack == 1 && stone.Item.stack == (separateFurniture ? 9999 : 4) && !scope.Goals[0].Complete,
                        "real Grand Design screen-domain wire commits once without lending its reach to the ordinary stone body");
                    NativeQuickItemChecks.Until(() =>
                    {
                        observation.Step(scope, () => native(p.position.X < 53 * 16 ? 1 : 0));
                        Require(Main.screenPosition == screen, "the screen stays fixed while actual native controls move the ordinary reach domain");
                        return scope.Goals[0].Complete;
                    });
                    Require(scope.Events.Count == 1 && stone.Item.stack == (separateFurniture ? 9998 : 3) && wire.stack == 1 && Main.tile[44, 42].wire2() && Main.tile[56, 42].wire2(),
                        "the previously screened-out ordinary seed is rediscovered and consumed exactly once after real reach entry, preserving wiring");
                }
                finally { WiresUI.Settings.ToolMode = mode; Main.screenPosition = screen; }
            }
        }

        private static long NearQueueCapabilityRecovery(object context, object host, object tools, object input, int goalCount)
        {
            using (var probe = new PostProcessingProbe(context, host, tools, input))
            {
                var p = Reset(context, host, tools, input);
                p.position.X = 42 * 16;
                p.inventory[20].SetDefaults(ItemID.RedPaint); p.inventory[20].stack = goalCount + 2; var paint = p.inventory[20];
                var goals = Enumerable.Range(0, goalCount).Select(i => PostProcessingTarget("TilePaint", 44 + i, 42, paint.paint, ItemID.RedPaint)).ToArray();
                foreach (var goal in goals) NativeToolsChecks.Tile(goal.X, goal.Y, TileID.Stone);
                probe.Goals = goals; probe.Supplies = new[] { paint }; probe.ToolItems = Array.Empty<Item>();
                PostProcessingPlace(context, host, input, probe.Goals); probe.Begin(true); SetAutomatic(host, input, true);
                long startHeads = (long)Get(host, "NearBuildHeadChecks"), startProofs = (long)Get(host, "NearBuildProofChecks");
                long startCaps = (long)Get(host, "NearBuildCapabilityChecks"), peakHeads = 0, peakProofs = 0, peakCaps = 0;
                Action advance = () =>
                {
                    long heads = (long)Get(host, "NearBuildHeadChecks"), proofs = (long)Get(host, "NearBuildProofChecks"), caps = (long)Get(host, "NearBuildCapabilityChecks");
                    probe.Advance();
                    peakHeads = Math.Max(peakHeads, (long)Get(host, "NearBuildHeadChecks") - heads);
                    peakProofs = Math.Max(peakProofs, (long)Get(host, "NearBuildProofChecks") - proofs);
                    peakCaps = Math.Max(peakCaps, (long)Get(host, "NearBuildCapabilityChecks") - caps);
                };
                NativeQuickItemChecks.Until(() =>
                {
                    advance();
                    var ranges = ((IDictionary)Get(host, "nearBuildRanges")).Values.Cast<object>().ToArray();
                    var facts = ranges.SelectMany(range => ((IDictionary)Get(range, "Facts")).Values.Cast<object>()).ToArray();
                    return ranges.All(range => (bool)Get(range, "Complete")) && facts.All(fact => !(bool)Get(fact, "Pending")) &&
                        facts.Where(fact => Convert.ToString(Get(fact, "Wait")).Contains("Capability")).Select(fact =>
                        {
                            var cell = Get(Get(fact, "Seed"), "Cell"); return Get(cell, "X") + "," + Get(cell, "Y");
                        }).Distinct().Count() == goalCount;
                });
                Require(goals.All(goal => !goal.Complete) && paint.stack == goalCount + 2 && probe.Events.Count == 0,
                    "every real paint goal independently reaches a classified missing-tool Capability wait with sufficient paint already present");
                for (int i = 0; i < 8; i++) advance();
                long before = (long)Get(host, "NearBuildCapabilityChecks");
                long slotBefore = (long)Get(host, "NearBuildCapabilitySlotChecks");
                long dependencyBefore = (long)Get(host, "NearBuildCapabilityDependencyChecks");
                var unrelated = NearQueueNativeRefill(p, ItemID.SnowBlock, 3); advance();
                long afterRecovery = (long)Get(host, "NearBuildCapabilityChecks");
                long recoverySlots = (long)Get(host, "NearBuildCapabilitySlotChecks") - slotBefore;
                long recoveryDependencies = (long)Get(host, "NearBuildCapabilityDependencyChecks") - dependencyBefore;
                // This scene has no runnable target or foreign admission, so
                // only ObserveBuildSignals' existing one main-bag publication
                // may visit slots. Family/key/serial reads are a separate bill,
                // never another 50-slot walk per waiting fact or new type.
                Require(recoverySlots > 0 && recoverySlots <= 50 && afterRecovery - before == recoverySlots + recoveryDependencies,
                    "one unrelated recovery pays at most the existing single 50-slot observation, with every capability check partitioned into shared slots or dependency reads");
                Console.WriteLine("NearQueue capability firstRecovery goals=" + goalCount + " sharedSlotVisits=" + recoverySlots +
                    " familyKeySerialChecks=" + recoveryDependencies + " total=" + (afterRecovery - before));
                long stableFacts = (long)Get(host, "NearBuildFactChecks"), stableClasses = (long)Get(host, "NearBuildClassifications");
                for (int i = 0; i < 8; i++)
                {
                    long slots = (long)Get(host, "NearBuildCapabilitySlotChecks"), dependencies = (long)Get(host, "NearBuildCapabilityDependencyChecks");
                    long total = (long)Get(host, "NearBuildCapabilityChecks");
                    advance();
                    long slotDelta = (long)Get(host, "NearBuildCapabilitySlotChecks") - slots;
                    Require(slotDelta <= 50 && (long)Get(host, "NearBuildCapabilityChecks") - total ==
                        slotDelta + (long)Get(host, "NearBuildCapabilityDependencyChecks") - dependencies,
                        "each stable Update charges its existing single shared publisher, without hiding another bag walk in dependency reads");
                }
                Require(stableFacts == (long)Get(host, "NearBuildFactChecks") && stableClasses == (long)Get(host, "NearBuildClassifications") &&
                    goals.All(goal => !goal.Complete) && paint.stack == goalCount + 2 && unrelated.Item.stack == 3,
                    "eight stable Updates after unrelated recovery perform no further fact traversal/classification and cannot release the paint-tool wait");
                var brush = NearQueueNativeRefill(p, ItemID.Paintbrush, 1); probe.ToolItems = new[] { brush.Item };
                NativeQuickItemChecks.Until(() => { advance(); return goals.All(goal => goal.Complete) && !(bool)Get(Get(tools, "Use"), "Active"); });
                probe.Report("near queue missing brush unrelated snow and real brush recovery");
                Console.WriteLine("NearQueue capability goals=" + goalCount + " unrelatedRecoveryChecks=" + (afterRecovery - before) + " relatedRecoveryChecks=" + ((long)Get(host, "NearBuildCapabilityChecks") - afterRecovery));
                Console.WriteLine("NearQueue capability work heads=" + ((long)Get(host, "NearBuildHeadChecks") - startHeads) + " proofs=" +
                    ((long)Get(host, "NearBuildProofChecks") - startProofs) + " capabilities=" + ((long)Get(host, "NearBuildCapabilityChecks") - startCaps) +
                    " peakUpdateHead/Proof/Capability=" + peakHeads + "/" + peakProofs + "/" + peakCaps);
                Require(probe.Events.Count == goalCount && paint.stack == 2 && brush.Item.stack == 1 && unrelated.Item.stack == 3,
                    "real matching tool-family recovery wakes all classified goals and original native paint consumers spend once per goal");
                return recoverySlots;
            }
        }

        private static byte[] nearLatePacket;
        private static Item nearLateBItem;
        private static object nearLateHost, nearLateTools;
        private static bool nearLatePacketApplied;
        private static void NearQueueLateGuardTrace(string stage, Player p)
        {
            var host = nearLateHost; var tools = nearLateTools;
            var group = GetOptional(host, "buildContinuation");
            var operation = group == null ? null : GetOptional(group, "Operation");
            Func<object, string, string> value = (owner, name) => owner == null ? "none" : Convert.ToString(GetOptional(owner, name)) ?? "none";
            bool released = (bool)host.GetType().GetMethod("BuildDependencyReleased", F, null, new[] { typeof(ulong), typeof(bool) }, null)
                .Invoke(host, new object[] { 1UL << 20, false });
            var facts = ((IDictionary)Get(host, "nearBuildRanges")).Values.Cast<object>()
                .SelectMany(range => ((IDictionary)Get(range, "Facts")).Values.Cast<object>())
                .Where(fact => (int)Get(Get(Get(fact, "Seed"), "Cell"), "X") == 1).Take(8)
                .Select(fact => "ready=" + value(fact, "Ready") + ":pending=" + value(fact, "Pending") + ":wait=" + value(fact, "Wait") +
                    ":deps=" + value(fact, "Dependencies") + ":retry=" + value(fact, "Retry") + ":planGroup=" + value(fact, "PlanGroup"));
            Console.WriteLine("NearQueue late guards stage=" + stage + " world=" + Main.GameUpdateCount +
                " proofDeps=" + value(host, "nearProofDependencies") + " absence=" + value(host, "nearAbsenceReady") +
                " proofSameGroup=" + ReferenceEquals(GetOptional(host, "nearProofGroup"), group) +
                " group=" + value(group, "Group") + ":" + value(operation, "Kind") + ":selection=" + value(group, "Selection") +
                " currentSelection=" + Get(tools, "SelectionIntent") + " pendingWire20=" + Call(host, "WiringPendingMaterial", p.inventory[20]) +
                " actualReleased20=" + released + " eligible20/22=" + Call(tools, "BuildInventoryEligible", 20, p.inventory[20]) + "/" +
                Call(tools, "BuildInventoryEligible", 22, p.inventory[22]) + " actual20/22=" + p.inventory[20].type + ":" + p.inventory[20].stack + "/" +
                p.inventory[22].type + ":" + p.inventory[22].stack + " A2Facts=[" + string.Join(";", facts) + "]");
        }
        private static void NearQueueDeliverLatePacket(Player __instance)
        {
            if (nearLatePacket == null || !ReferenceEquals(__instance, Main.LocalPlayer)) return;
            Require(nearLateBItem != null && nearLateBItem.stack == 3 && __instance.selectedItem != 13,
                "the delayed native decoder arrives before any actual selection or debit of prepared B");
            var packet = nearLatePacket; nearLatePacket = null;
            NearQueueLateGuardTrace("before110", __instance);
            ReplayConstructionPacket(packet, 256); nearLatePacketApplied = true;
            NearQueueLateGuardTrace("after110", __instance);
            Console.WriteLine("NearQueue late dependency original110 delivered before Player.Update body world=" + Main.GameUpdateCount);
        }

        private sealed class NearWallScrapeObservation
        {
            internal object Tools, Host;
            internal int TileClears, WallClears, TraceRows, WallX;
            internal bool WallReachObserved;
            internal byte Paint;
        }
        private static NearWallScrapeObservation nearWallScrape;
        private static void NearWallScrapeTrace(string stage, bool force = false)
        {
            var observation = nearWallScrape;
            if (observation == null || observation.TraceRows >= 16 && !force) return;
            observation.TraceRows++;
            Func<object, string, string> value = (owner, name) => owner == null ? "none" : Convert.ToString(GetOptional(owner, name)) ?? "none";
            Func<object, string> flight = entry =>
            {
                if (entry == null) return "none";
                var source = GetOptional(entry, "Source"); var target = GetOptional(entry, "Target");
                return value(source, "Type") + ":" + value(target, "WorldX") + "," + value(target, "WorldY") +
                    ":checked=" + value(entry, "Checked") + ":token=" + value(entry, "Token");
            };
            var host = observation.Host; var p = Main.LocalPlayer;
            var group = GetOptional(host, "buildContinuation"); var operation = group == null ? null : GetOptional(group, "Operation");
            var sourceGroup = group == null ? null : GetOptional(group, "Source");
            var wall = new List<string>();
            foreach (DictionaryEntry pair in (IDictionary)Get(host, "nearBuildRanges"))
            {
                var owner = Get(pair.Key, "Owner"); if ((int)Get(owner, "OriginX") != observation.WallX) continue;
                var covered = GetOptional(pair.Value, "Covered");
                wall.Add("range covered=" + value(covered, "Left") + "," + value(covered, "Top") + ".." + value(covered, "Right") + "," + value(covered, "Bottom") +
                    ":complete=" + value(pair.Value, "Complete") + ":rawCurrent=" + value(pair.Value, "RawCurrent") + ":pending=" + value(pair.Value, "Pending"));
                foreach (object fact in ((IDictionary)Get(pair.Value, "Facts")).Values)
                {
                    if (wall.Count >= 9) break;
                    var seed = Get(fact, "Seed");
                    wall.Add("ordinal=" + value(seed, "Ordinal") + ":type=" + value(fact, "Type") + ":ready=" + value(fact, "Ready") +
                        ":pending=" + value(fact, "Pending") + ":wait=" + value(fact, "Wait") + ":group=" + value(fact, "PlanGroup") +
                        ":version=" + value(fact, "Version") + "/" + value(pair.Key, "Version") + ":support=" + value(fact, "Support") + ":retry=" + value(fact, "Retry"));
                }
            }
            bool wallReach = p.IsInTileInteractionRange(observation.WallX, 42, TileReachCheckSettings.Simple, p.inventory[12].tileBoost + p.blockRange);
            if (observation.TileClears > 0 && wallReach) observation.WallReachObserved = true;
            Console.WriteLine("NearQueue wallScrape stage=" + stage + " world=" + Main.GameUpdateCount + " position=" + p.position + " velocity=" + p.velocity +
                " wallX=" + observation.WallX + " wallReach=" + wallReach + " entered=" + observation.WallReachObserved +
                " tile/wallClears=" + observation.TileClears + "/" + observation.WallClears + " group=" + value(group, "Group") + ":" + value(operation, "Kind") +
                ":source=" + value(sourceGroup, "Type") + ":pending=" + value(group, "Pending") +
                " active=" + flight(GetOptional(host, "activeBuild")) + " prepared=" + flight(GetOptional(host, "preparedBuild")) +
                " coverage=" + value(host, "nearCoverageCurrent") + " absence=" + value(host, "nearAbsenceReady") + " wallFacts=[" + string.Join(";", wall) + "]");
        }
        private static void NearWallScrapeBefore(Player __instance, int __0, int __1, out Tuple<int,int,byte,byte> __state)
        {
            __state = null;
            if (nearWallScrape == null || !ReferenceEquals(__instance, Main.LocalPlayer) || __instance.HeldItem.type != ItemID.PaintScraper) return;
            var tile = Main.tile[__0, __1]; __state = Tuple.Create(__0, __1, tile.color(), tile.wallColor());
        }
        private static void NearWallScrapeAfter(bool __runOriginal, Tuple<int,int,byte,byte> __state)
        {
            if (__state == null) return;
            var tile = Main.tile[__state.Item1, __state.Item2];
            if (tile.color() == __state.Item3 && tile.wallColor() == __state.Item4) return;
            Require(__runOriginal && (long)Get(Get(nearWallScrape.Tools, "Use"), "Operation") > 0,
                "only the original scraper under one current use token records a real layer change");
            if (__state.Item1 == 40 && __state.Item2 == 42 && __state.Item3 == nearWallScrape.Paint && tile.color() == 0) nearWallScrape.TileClears++;
            else if (__state.Item1 == nearWallScrape.WallX && __state.Item2 == 42 && __state.Item4 == nearWallScrape.Paint && tile.wallColor() == 0) nearWallScrape.WallClears++;
            else Require(false, "scraper changes only the two explicitly declared foreground/wall goals");
            NearWallScrapeTrace("original-scrape");
        }
        private static void NearWallScrapeBeforeForeign(Player __instance, int __0, int __1)
        {
            if (nearWallScrape == null || !ReferenceEquals(__instance, Main.LocalPlayer) || __0 != 48 || __1 != 42) return;
            NearWallScrapeTrace("before-foreign-consumer", force: true);
            Require(nearWallScrape.TileClears == 1 && nearWallScrape.WallReachObserved &&
                Main.tile[nearWallScrape.WallX, 42].wallColor() == 0 && nearWallScrape.WallClears == 1,
                "a real wall-only range remains current Scrape A and completes before the competing paint B consumer");
        }
        private static void NearQueueWallOnlyScrape(object context, object host, object tools, object input)
        {
            using (var probe = new PostProcessingProbe(context, host, tools, input))
            {
                var p = Reset(context, host, tools, input); p.position.X = 44 * 16 - p.width;
                p.inventory[12].SetDefaults(ItemID.PaintScraper); p.inventory[13].SetDefaults(ItemID.Paintbrush);
                int left, top, right, bottom;
                TileReachCheckSettings.Simple.GetTileRegion(p, out left, out top, out right, out bottom, p.inventory[12].tileBoost + p.blockRange);
                int wallX = right + 1;
                p.inventory[20].SetDefaults(ItemID.RedPaint); p.inventory[20].stack = 2; var paint = p.inventory[20];
                var blue = new Item(); blue.SetDefaults(ItemID.BluePaint);
                NativeToolsChecks.Tile(40, 42, TileID.Stone); NativeToolsChecks.Tile(48, 42, TileID.Stone);
                WorldGen.PlaceWall(wallX, 42, WallID.Stone, true);
                Require(WorldGen.paintTile(40, 42, blue.paint, false, false) && WorldGen.paintWall(wallX, 42, blue.paint, false, false),
                    "original paint writers create independent blue foreground and wall-only starting layers");
                var tileA = PostProcessingTarget("TilePaint", 40, 42, 0);
                var wallA = PostProcessingTarget("WallPaint", wallX, 42, 0);
                var b = PostProcessingTarget("TilePaint", 48, 42, paint.paint, ItemID.RedPaint);
                probe.Goals = new[] { tileA, wallA, b }; probe.ToolItems = new[] { p.inventory[12], p.inventory[13] }; probe.Supplies = new[] { paint };
                foreach (var goal in probe.Goals) PostProcessingPlace(context, host, input, new[] { goal });
                Require(Scene(host).Length == 3 && !Main.tile[wallX, 42].active() && Main.tile[wallX, 42].wall == WallID.Stone &&
                    !p.IsInTileInteractionRange(wallX, 42, TileReachCheckSettings.Simple, p.inventory[12].tileBoost + p.blockRange),
                    "the wall A has its own truly wall-only saved range, separate from foreground A and paint B");
                var patch = new Harmony("JueMingR.Tests.NearQueueWallScrape");
                nearWallScrape = new NearWallScrapeObservation { Tools = tools, Host = host, Paint = blue.paint, WallX = wallX };
                Console.WriteLine("NearQueue wallScrape initialRegion=" + left + "," + top + ".." + right + "," + bottom + " nextWallX=" + wallX);
                patch.Patch(typeof(Player).GetMethod("PlaceThing_PaintScraper_TryScraping", F),
                    prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(NearWallScrapeBefore)),
                    postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(NearWallScrapeAfter)));
                patch.Patch(typeof(Player).GetMethod("ApplyPaint", F), prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(NearWallScrapeBeforeForeign)));
                try
                {
                    probe.Begin(true); SetAutomatic(host, input, true);
                    NativeQuickItemChecks.Until(() =>
                    {
                        bool enter = nearWallScrape.TileClears > 0 && !wallA.Complete &&
                            !p.IsInTileInteractionRange(wallX, 42, TileReachCheckSettings.Simple, p.inventory[12].tileBoost + p.blockRange);
                        probe.Advance(horizontal: enter ? 1 : 0);
                        if (nearWallScrape.TileClears > 0 && nearWallScrape.WallClears == 0) NearWallScrapeTrace("after-update");
                        return probe.Goals.All(goal => goal.Complete) && !(bool)Get(Get(tools, "Use"), "Active");
                    });
                    Require(nearWallScrape.TileClears == 1 && nearWallScrape.WallClears == 1 && paint.stack == 1 &&
                        p.inventory[12].type == ItemID.PaintScraper && p.inventory[12].stack == 1,
                        "same Scrape group clears both real source layers without tool debit before the one real paint debit");
                    probe.Report("near queue foreground Scrape then wall-only Scrape before paint");
                }
                finally
                {
                    foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                    nearWallScrape = null;
                }
            }
        }

        private static void NearQueueLateNativeDependency(object context, object host, object tools, object input)
        {
            int mode = Main.netMode; var connection = Netplay.Connection; var sections = Main.sectionManager; var client = Main.clientPlayer;
            var buffers = (MessageBuffer[])NetMessage.buffer.Clone(); var clients = (RemoteClient[])Netplay.Clients.Clone();
            var wireMode = WiresUI.Settings.ToolMode;
            var patch = new Harmony("JueMingR.Tests.NearQueueLateDependency");
            patch.Patch(typeof(NetMessage).GetMethod("SendData", F), postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(RecordConstructionWiringPacket)));
            patch.Patch(typeof(Player).GetMethod("Update", F, null, new[] { typeof(int) }, null),
                prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(NearQueueDeliverLatePacket)));
            try
            {
                Netplay.Connection = new RemoteServer { PendingTermination = true, State = 10 }; Main.clientPlayer = new Player();
                for (int i = 0; i < NetMessage.buffer.Length; i++) if (NetMessage.buffer[i] == null) NetMessage.buffer[i] = new MessageBuffer();
                for (int i = 0; i < Netplay.Clients.Length; i++) if (Netplay.Clients[i] == null) Netplay.Clients[i] = new RemoteClient { Id = i, State = 10 };
                NetMessage.buffer[256] = new MessageBuffer(); NetMessage.buffer[0] = new MessageBuffer(); Netplay.Clients[0] = new RemoteClient { Id = 0, State = 10 };
                Main.netMode = 1; Main.sectionManager = new WorldSections(1, 2); Main.sectionManager.SetAllSectionsLoaded(); Call(context, "UpdateRuntime");
                using (var scope = new BusinessScope(context, host, tools, input))
                {
                    var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.MulticolorWrench);
                    p.inventory[20].SetDefaults(ItemID.Wire); p.inventory[20].stack = 2; var wire = p.inventory[20];
                    // The native selector must still see protected slot20 first.
                    // A later eligible real stack lets planning distinguish
                    // blocked native selection (Dependencies20) from no supply.
                    p.inventory[22].SetDefaults(ItemID.Wire); p.inventory[22].stack = 1; var laterWire = p.inventory[22];
                    Action checkLaterWire = () => Require(ReferenceEquals(p.inventory[22], laterWire) && laterWire.type == ItemID.Wire && laterWire.stack == 1,
                        "the later qualified wire stack is only a planning witness and never moves or pays the native first-stack debit");
                    var stone = BusinessStock(p, 13, ItemID.StoneBlock, 3);
                    scope.Sources = new[] { stone }; scope.Goals = new[] { BusinessTileGoal(46, 42, stone) };
                    Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Wire, Wires = 2 },
                        new CaptureCell { X = 1, Layers = CaptureLayer.Wire, Wires = 2 },
                        new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone }), 44, 42);
                    constructionWiringPackets.Clear(); scope.Begin(true); SetAutomatic(host, input, true);
                    ConstructionWaitWiringReply(context, host, input);
                    var request = constructionWiringPackets.Single(packet => packet[2] == 109);
                    long originalAToken = (long)Get(Get(tools, "Use"), "Operation");
                    Require(BitConverter.ToInt16(request, 3) == 44 && !scope.Goals[0].Complete,
                        "the real current A begins with the first wire109 before an ordinary competing B consumer");
                    var serverTiles = (Tile[,])Main.tile.Clone(); serverTiles[44, 42] = (Tile)Main.tile[44, 42].Clone(); serverTiles[45, 42] = (Tile)Main.tile[45, 42].Clone();
                    var replies = ConstructionZeroAuthorityPackets(request, serverTiles, 44, 42, false, true);
                    ConstructionRequireWiringPair(replies, false, 0);
                    ReplayConstructionPacket(replies[0], 256); ReplayConstructionPacket(replies[1], 256);
                    checkLaterWire();
                    // Multicolor/GD themselves are Dynamic while their old
                    // request is pending. A genuine ordinary blue wrench can
                    // continue the same AddWire mode, exposing the still-owned
                    // wire supply dependency rather than that tool's own gate.
                    var alternateTool = NearQueueNativeRefill(p, ItemID.BlueWrench, 1);
                    checkLaterWire();
                    Require((bool)Call(host, "WiringPendingMaterial", wire),
                        "new ordinary wiring capability does not release the exact old wire110 receipt");
                    Console.WriteLine("NearQueue late dependency ordinaryBlueWrench slot=" + alternateTool.Slot + " pendingWire=" + wire.type + ":" + wire.stack);
                    bool window = false;
                    for (int i = 0; i < 80 && !scope.Goals[0].Complete; i++)
                    {
                        ConstructionStepWithProjectiles(context, input);
                        checkLaterWire();
                        var prepared = GetOptional(host, "preparedBuild");
                        bool dependency = ((IDictionary)Get(host, "nearBuildRanges")).Values.Cast<object>().SelectMany(range =>
                            ((IDictionary)Get(range, "Facts")).Values.Cast<object>()).Any(fact =>
                                (int)Get(Get(Get(fact, "Seed"), "Cell"), "X") == 1 &&
                                Convert.ToString(Get(fact, "Wait")).Contains("Dependencies") && ((ulong)Get(fact, "Dependencies") & (1UL << 20)) != 0);
                        var use = Get(tools, "Use"); var active = GetOptional(host, "activeBuild");
                        bool sameOldTail = !(bool)Get(use, "Active") || (long)Get(use, "Operation") == originalAToken &&
                            active != null && (long)Get(active, "Token") == originalAToken && (bool)Get(active, "Checked");
                        if (i < 16)
                            Console.WriteLine("NearQueue late window step=" + i + " sameOldCheckedTail=" + sameOldTail + " A2Dependency=" + dependency +
                                " use=" + Get(use, "Active") + ":" + Get(use, "Operation") + " originalA=" + originalAToken +
                                " activeChecked=" + (active == null ? "none" : Get(active, "Checked")) + " preparedType=" +
                                (prepared == null ? "none" : Get(Get(prepared, "Source"), "Type")) + " selected=" + p.selectedItem +
                                " timers=" + p.itemTime + "/" + p.itemAnimation + " BStock=" + stone.Item.stack);
                        if (prepared != null && (int)Get(Get(prepared, "Source"), "Type") == stone.Type &&
                            sameOldTail && dependency && !scope.Goals[0].Complete && stone.Item.stack == 3) { window = true; break; }
                    }
                    Console.WriteLine("NearQueue late dependency window=" + window + " useActive=" + Get(Get(tools, "Use"), "Active") +
                        " receipt=" + (GetOptional(host, "wiringReply") != null) + " pendingSource=" + Call(host, "WiringPendingMaterial", wire) +
                        " BComplete=" + scope.Goals[0].Complete + " BStock=" + stone.Item.stack);
                    Require(window && !Main.tile[45, 42].wire2() && wire.stack == 2 && stone.Item.stack == 3 && p.selectedItem != 13,
                        "actual110 receipt protects A2 while B is unadmitted and A has only its checked natural tail or no active use");
                    nearLateHost = host; nearLateTools = tools; nearLateBItem = stone.Item; nearLatePacket = replies[2]; nearLatePacketApplied = false;
                    ConstructionStepWithProjectiles(context, input);
                    checkLaterWire();
                    Require(nearLatePacketApplied && !scope.Goals[0].Complete && stone.Item.stack == 3,
                        "native110 releases the dependency after Host classification and final admission rejects the now-obsolete B absence proof");
                    NativeQuickItemChecks.Until(() => { ConstructionStepWithProjectiles(context, input); checkLaterWire(); return constructionWiringPackets.Any(packet => packet[2] == 109); });
                    request = constructionWiringPackets.Single(packet => packet[2] == 109);
                    Require(BitConverter.ToInt16(request, 3) == 45 && !scope.Goals[0].Complete,
                        "the next original wire consumer resumes restored A2 before admitting already prepared ordinary B");
                    serverTiles = (Tile[,])Main.tile.Clone(); serverTiles[45, 42] = (Tile)Main.tile[45, 42].Clone();
                    replies = ConstructionZeroAuthorityPackets(request, serverTiles, 45, 42, false, false);
                    ConstructionRequireWiringPair(replies, false, 1);
                    foreach (var packet in replies) ReplayConstructionPacket(packet, 256);
                    checkLaterWire();
                    NativeQuickItemChecks.Until(() => { scope.Step(); checkLaterWire(); return scope.Goals[0].Complete && !(bool)Get(Get(tools, "Use"), "Active"); });
                    Require(Main.tile[44, 42].wire2() && Main.tile[45, 42].wire2() && wire.stack == 1 && stone.Item.stack == 2 && scope.Events.Count == 1 &&
                        ReferenceEquals(p.inventory[22], laterWire) && laterWire.stack == 1,
                        "late dependency handoff keeps exact authority wire debit and one later original ordinary debit without a second executor");
                }
            }
            finally
            {
                nearLatePacket = null; nearLateBItem = null; nearLateHost = nearLateTools = null;
                Call(host, "SetAutoBuild", false); Call(host, "ClearProjection");
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                Main.netMode = mode; Netplay.Connection = connection; Main.sectionManager = sections; Main.clientPlayer = client;
                Array.Copy(buffers, NetMessage.buffer, buffers.Length); Array.Copy(clients, Netplay.Clients, clients.Length);
                WiresUI.Settings.ToolMode = wireMode; Call(context, "UpdateRuntime");
            }
        }

        private static void NearQueueExhaustedSource(object context, object host, object tools, object input, bool alternate)
        {
            using (var scope = new BusinessScope(context, host, tools, input))
            {
                Call(host, "SetSameTypeReplacement", false);
                var p = Reset(context, host, tools, input);
                var a = BusinessStock(p, 12, ItemID.DirtBlock, 1); var b = BusinessStock(p, 13, ItemID.StoneBlock, 3);
                var second = alternate ? BusinessStock(p, 14, ItemID.DirtBlock, 1) : null;
                scope.Sources = alternate ? new[] { a, b, second } : new[] { a, b };
                scope.Goals = new[] { BusinessTileGoal(44, 42, a), BusinessTileGoal(45, 42, a), BusinessTileGoal(46, 42, b) };
                Place(context, host, input, Asset(host, scope.Goals.Select(goal => new CaptureCell { X = goal.X - 44,
                    Layers = CaptureLayer.Tile, TileType = (ushort)goal.TileType }).ToArray()), 44, 42);
                scope.Begin(true); SetAutomatic(host, input, true);
                NativeQuickItemChecks.Until(() => { scope.Step(); return scope.Goals[2].Complete && !(bool)Get(Get(tools, "Use"), "Active"); });
                scope.Report("ordinary true depletion alternate=" + alternate);
                Require(a.Item.IsAir && b.Item.stack == 2 && !(bool)Get(Get(host, "buildMaterialPreparation"), "HasWork") &&
                    !(bool)Get(Get(Get(tools, "Items"), "Ownership"), "HasUse"),
                    "ordinary depleted source settles its exact debit without an owned/unknown material tail blocking the foreign source");
                Require(alternate ? second.Item.IsAir && scope.Goals.All(goal => goal.Complete) &&
                    scope.Events.Select(entry => entry.Type).SequenceEqual(new[] { a.Type, a.Type, b.Type }) &&
                    scope.Events.Take(2).Select(entry => entry.Slot).Distinct().Count() == 2 :
                    !scope.Goals[1].Complete && scope.Events.Select(entry => entry.Type).SequenceEqual(new[] { a.Type, b.Type }),
                    "another exact eligible A slot continues A first; only genuine total A depletion permits B with an unfilled A goal");
            }
        }

        private static object[] NearQueueFactsForItem(object host, int type)
        {
            return ((IDictionary)Get(host, "nearBuildRanges")).Values.Cast<object>().SelectMany(range =>
                ((IDictionary)Get(range, "Facts")).Values.Cast<object>()).Where(fact =>
                {
                    var seed = Get(fact, "Seed"); var placement = GetOptional(seed, "Placement");
                    return placement != null && (int)Get(placement, "UseItemType") == type;
                }).ToArray();
        }

        private static BusinessSource NearQueueNativeRefill(Player p, int type, int count)
        {
            var supply = new Item(); supply.SetDefaults(type); supply.stack = count;
            Require(p.GetItem(supply, new GetItemSettings(NoText: true, NoSound: true)).IsAir, "original GetItem accepts the real finite supply recovery");
            int slot = Enumerable.Range(0, 50).Single(index => p.inventory[index].type == type && p.inventory[index].stack == count);
            return new BusinessSource { Type = type, Slot = slot, Item = p.inventory[slot] };
        }

        private static void NearQueueMaterialDependency(object context, object host, object tools, object input)
        {
            using (var scope = new BusinessScope(context, host, tools, input))
            {
                Call(host, "SetSameTypeReplacement", false);
                var p = Reset(context, host, tools, input);
                var a = BusinessStock(p, 12, ItemID.DirtBlock, 4); var b = BusinessStock(p, 13, ItemID.Wood, 3);
                var c = new BusinessSource { Type = ItemID.StoneBlock, Slot = 14, Item = p.inventory[14] };
                var sample = new Item(); sample.SetDefaults(c.Type);
                scope.Sources = new[] { a, b, c };
                scope.Goals = new[] { BusinessTileGoal(44, 42, a),
                    new BusinessGoal { X = 45, Y = 42, Type = c.Type, TileType = sample.createTile, Style = sample.placeStyle }, BusinessTileGoal(46, 42, b) };
                Place(context, host, input, Asset(host, scope.Goals.Select(goal => new CaptureCell { X = goal.X - 44,
                    Layers = CaptureLayer.Tile, TileType = (ushort)goal.TileType }).ToArray()), 44, 42);
                scope.Begin(true); SetAutomatic(host, input, true);
                NativeQuickItemChecks.Until(() =>
                {
                    scope.Step(); var prepared = GetOptional(host, "preparedBuild");
                    return scope.Events.Count == 1 && prepared != null && (int)Get(Get(prepared, "Source"), "Type") == b.Type &&
                        (bool)Get(host, "nearAbsenceReady");
                });
                Require(scope.Events[0].Type == a.Type && a.Item.stack == 3 && (p.itemTime > 0 || p.itemAnimation > 0),
                    "the independent R4 case has abundant A, completed A goals and B prepared before legal foreign admission");
                Require(NearQueueFactsForItem(host, c.Type).Any(fact => Convert.ToString(Get(fact, "Wait")).Split(',').Any(flag => flag.Trim() == "Material")),
                    "missing C really entered a Tile-body material dependency beside A in the same ordinary lane");
                var query = Get(host, "nearBuildDiscovery"); var proofGroup = Get(host, "nearProofGroup");
                long semantic = (long)Get(host, "NearBuildSemanticSteps"), classifications = (long)Get(host, "NearBuildClassifications");
                var unrelated = NearQueueNativeRefill(p, ItemID.SnowBlock, 3);
                scope.Step();
                Require(scope.Events.Count == 1 && a.Item.stack == 3 && unrelated.Item.stack == 3 &&
                    ReferenceEquals(query, Get(host, "nearBuildDiscovery")) && ReferenceEquals(proofGroup, Get(host, "nearProofGroup")) &&
                    (bool)Get(host, "nearAbsenceReady") && semantic == (long)Get(host, "NearBuildSemanticSteps") &&
                    classifications == (long)Get(host, "NearBuildClassifications"),
                    "unrelated original D recovery cannot reopen A's already paid absence proof, source enumeration or business classification through missing C");
                c = NearQueueNativeRefill(p, ItemID.StoneBlock, 3); scope.Sources = new[] { a, b, c, unrelated };
                NativeQuickItemChecks.Until(() => { scope.Step(); return scope.Goals.All(goal => goal.Complete) && !(bool)Get(Get(tools, "Use"), "Active"); });
                scope.Report("same-lane missing C unrelated D and actual C recovery");
                Require(scope.Events.Count == 3 && scope.Events.Count(entry => entry.Type == c.Type) == 1 &&
                    scope.Events.Count(entry => entry.Type == b.Type) == 1 && a.Item.stack == 3 && b.Item.stack == 2 &&
                    c.Item.stack == 2 && unrelated.Item.stack == 3,
                    "real related C restock revokes its own wait and consumes exactly once, with abundant A and unrelated D untouched");
            }
        }

        private static Point[] NearQueueBlockBodies(BusinessScope scope, Func<BusinessGoal, bool> choose)
        {
            var blockers = new List<Point>();
            foreach (var goal in scope.Goals.Where(choose))
                for (int x = goal.X - goal.OriginX; x < goal.X - goal.OriginX + goal.Width; x++)
                    for (int y = goal.Y - goal.Height + 1; y <= goal.Y; y++)
                    {
                        Require(WorldGen.PlaceTile(x, y, TileID.Stone, true, true), "independent original writer establishes a real occupied foreign body");
                        blockers.Add(new Point(x, y));
                    }
            return blockers.ToArray();
        }

        private static void NearQueueLookahead(object context, object host, object tools, object input)
        {
            using (var scope = new BusinessScope(context, host, tools, input))
            using (var observation = new NearQueueObservation(host, required: true))
            {
                NearQueueSetup(scope, false, false, 7);
                var blockers = NearQueueBlockBodies(scope, goal => goal.Type == ItemID.WoodPlatform || goal.Type == ItemID.WoodenChair);
                scope.Begin(true); SetAutomatic(host, input, true);
                NativeQuickItemChecks.Until(() => { observation.Step(scope); return scope.Events.Count > 0; });
                Require(scope.Events[0].Type == ItemID.WorkBench && scope.Goals.Any(goal => goal.Type == ItemID.WorkBench && scope.Runnable(goal)),
                    "lookahead fixture has a real current workbench A with another supplied native-runnable A after its first consumption");
                var before = NearQueueReadyTypes(host);
                Require(!before.Contains(ItemID.WoodPlatform) && !before.Contains(ItemID.WoodenChair),
                    "occupied platform/chair bodies are not already claimed as usable future groups");
                long classes = (long)Get(host, "NearBuildClassifications"); uint releaseWorld = Main.GameUpdateCount;
                long clicks = (long)Get(Get(host, "buildMaterialPreparation"), "NativeClicks");
                foreach (var point in blockers)
                {
                    WorldGen.KillTile(point.X, point.Y, false, false, true);
                    Require(!Main.tile[point.X, point.Y].active(), "actual native removal releases the foreign body without altering any planner state");
                }
                bool ahead = false; int preparedSamples = 0;
                NativeQuickItemChecks.Until(() =>
                {
                    observation.Step(scope);
                    if (GetOptional(host, "preparedBuild") != null) preparedSamples++;
                    var ready = NearQueueReadyTypes(host);
                    if (ready.Contains(ItemID.WoodPlatform) && ready.Contains(ItemID.WoodenChair) &&
                        scope.Goals.Any(goal => goal.Type == ItemID.WorkBench && scope.Runnable(goal))) ahead = true;
                    return ahead || scope.Events.Count(entry => entry.Type == ItemID.WorkBench) == 2;
                });
                Console.WriteLine("NearQueue lookahead release=" + releaseWorld + " observed=" + Main.GameUpdateCount +
                    " classifications=" + ((long)Get(host, "NearBuildClassifications") - classes) + " preparedSamples=" + preparedSamples);
                Require(ahead && preparedSamples > 0 && (long)Get(host, "NearBuildClassifications") > classes,
                    "while current A still has a legal next target, ordinary Update refreshes both released B/C groups despite an existing prepared executor");
                Require(clicks == (long)Get(Get(host, "buildMaterialPreparation"), "NativeClicks") &&
                    scope.Events.All(entry => entry.Type == ItemID.WorkBench),
                    "lookahead candidates neither borrow a second material receipt nor interrupt the still-runnable current material");
                NativeQuickItemChecks.Until(() => { observation.Step(scope); return scope.Goals.All(goal => goal.Complete) && !(bool)Get(Get(tools, "Use"), "Active"); });
                Require(scope.Sources.All(source => source.Item.stack > 9000), "lookahead discovery never relies on stock exhaustion or a second supply lease");
            }
        }

        private static void NearQueueNativeReentry(object context, object host, object tools, object input)
        {
            using (var scope = new BusinessScope(context, host, tools, input))
            {
                NearQueueSetup(scope, false, false, 7);
                Call(host, "SetSameTypeReplacement", false);
                var blocker = new Item(); blocker.SetDefaults(ItemID.GlassPlatform);
                foreach (var goal in scope.Goals.Where(goal => goal.WallType == 0))
                {
                    Require(WorldGen.PlaceTile(goal.X, goal.Y, blocker.createTile, true, true, style: blocker.placeStyle),
                        "a real one-way foreign platform occupies each requested body without blocking the horizontal walking corridor");
                    Require(!goal.Complete, "foreign platform style/body does not complete a strict requested platform or furniture");
                }
                // Saved/qualified bytes are immutable. Remove only their live
                // prototype bodies before this walking scenario starts, so the
                // player need not jump the separate source furniture at 60..70.
                for (int x = 60; x <= 70; x++) for (int y = 38; y <= 42; y++) WorldGen.KillTile(x, y, false, false, true);
                scope.Begin(true); SetAutomatic(host, input, true);
                NativeQuickItemChecks.Until(() => { scope.Step(); return (int)Get(host, "NearBuildFacts") > 0; });
                var p = Main.LocalPlayer; float start = p.position.X;
                // A construction target is not a semantic fact count: source
                // members also have shape/appearance/wire/actuation lanes, and
                // normalized objects expose their own complete member stream.
                // Bound those known immutable fixture members and lane IDs;
                // native consumption still has the separate one-goal oracle.
                int lanes = Enum.GetValues(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintBuildLane", true)).Length;
                long maximumFacts = lanes * (scope.Goals.Sum(goal => goal.Width * goal.Height) +
                    scope.Goals.Where(goal => goal.TileType != 0).Sum(goal => goal.Width * goal.Height));
                long initialBytes = (long)Get(host, "NearBuildPlanBytes"); int initialFacts = (int)Get(host, "NearBuildFacts");
                long priorRetainedBytes = -1;
                // Move through actual controls and Player.Update. Never assign
                // a later player position or enlarge reach to force discovery.
                for (int pass = 0; pass < 2; pass++)
                {
                    int movementSteps = 0;
                    Action<int> move = direction =>
                    {
                        movementSteps++;
                        NearQueueMovementStep(scope, direction, false);
                        if (movementSteps <= 8 || movementSteps % 128 == 0 && movementSteps <= 2048)
                        {
                            int x = Math.Max(1, Math.Min(Main.maxTilesX - 2, (int)(p.Center.X / 16) + direction * 2));
                            int y = Math.Max(1, Math.Min(Main.maxTilesY - 2, (int)(p.Bottom.Y / 16) - 1));
                            var front = Main.tile[x, y];
                            Console.WriteLine("NearQueue movement pass=" + pass + " step=" + movementSteps + " direction=" + direction +
                                " position=" + p.position + " velocity=" + p.velocity + " jumpControl/trigger=" + p.controlJump + "/" + PlayerInput.Triggers.Current.Jump +
                                " front=" + x + "," + y + ":" + front.active() + ":" + front.type + " inventory=" + Main.playerInventory + " input=" + Get(input, "CanRunAutomaticActions"));
                        }
                    };
                    NativeQuickItemChecks.Until(() => { move(1); return p.position.X > 80 * 16; });
                    p.controlJump = false;
                    scope.Step();
                    Require((int)Get(host, "NearBuildFacts") == 0 && ((IDictionary)Get(host, "nearBuildRanges")).Count == 0,
                        "native movement out of every exact near range releases previous semantic facts and query ranges");
                    NativeQuickItemChecks.Until(() => { move(-1); return p.position.X <= start; });
                    p.controlJump = false;
                    NativeQuickItemChecks.Until(() =>
                    {
                        scope.Step();
                        var ranges = ((IDictionary)Get(host, "nearBuildRanges")).Values.Cast<object>().ToArray();
                        return p.velocity == Vector2.Zero && (int)Get(host, "NearBuildFacts") > 0 && ranges.All(range =>
                            (bool)Get(range, "Complete") && ((IDictionary)Get(range, "Facts")).Values.Cast<object>().All(fact => !(bool)Get(fact, "Pending")));
                    });
                    int facts = (int)Get(host, "NearBuildFacts"); long bytes = (long)Get(host, "NearBuildPlanBytes");
                    Console.WriteLine("NearQueue native reentry pass=" + pass + " facts=" + facts + " estimatedBytes=" + bytes + " initialFacts=" + initialFacts + " initialBytes=" + initialBytes);
                    Require(facts <= maximumFacts && ((IDictionary)Get(host, "nearBuildRanges")).Count <= 7,
                        "returning to the same finite template retains only its finite local goals, without accumulating traveled-region history");
                    Require(priorRetainedBytes < 0 || bytes <= priorRetainedBytes,
                        "a second native out-and-back trip cannot grow retained semantic bytes for the unchanged local template");
                    priorRetainedBytes = bytes;
                    long stableSteps = (long)Get(host, "NearBuildSemanticSteps"), stableClasses = (long)Get(host, "NearBuildClassifications");
                    long stableFactChecks = (long)Get(host, "NearBuildFactChecks"), stableRetirementChecks = (long)Get(host, "NearBuildRetirementChecks");
                    long stableRangeChecks = (long)Get(host, "NearBuildRangeChecks");
                    bool nativeRetry = ((IDictionary)Get(host, "nearBuildRanges")).Values.Cast<object>().Any(range =>
                        ((IDictionary)Get(range, "Facts")).Values.Cast<object>().Any(fact => (ulong)Get(fact, "Retry") != 0));
                    int quietSamples = 0;
                    for (int n = 0; n < 8; n++)
                    {
                        ulong tick = (ulong)Get(host, "materialTick");
                        bool due = ((IDictionary)Get(host, "nearBuildRanges")).Values.Cast<object>().Any(range =>
                            ((IDictionary)Get(range, "Facts")).Values.Cast<object>().Any(fact => (bool)Get(fact, "Pending") ||
                                (ulong)Get(fact, "Retry") != 0 && (ulong)Get(fact, "Retry") <= tick + 1));
                        long checks = (long)Get(host, "NearBuildFactChecks");
                        scope.Step();
                        if (!due)
                        {
                            quietSamples++;
                            Require(checks == (long)Get(host, "NearBuildFactChecks"),
                                "a stationary classified Update without an actually due retry performs zero fact checks");
                        }
                    }
                    Require(stableSteps == (long)Get(host, "NearBuildSemanticSteps") && (nativeRetry || stableClasses == (long)Get(host, "NearBuildClassifications")),
                        "settled stationary facts do not re-enumerate source; only declared native retry duties may repeat business classification");
                    Require(stableRetirementChecks == (long)Get(host, "NearBuildRetirementChecks") &&
                        quietSamples > 0 && (nativeRetry || stableFactChecks == (long)Get(host, "NearBuildFactChecks")),
                        "settled unchanged ranges never traverse retirement members or repeatedly check classified facts without a declared due native retry");
                    Console.WriteLine("NearQueue stable quietSamples=" + quietSamples + " nativeRetry=" + nativeRetry + " classifications=" + ((long)Get(host, "NearBuildClassifications") - stableClasses) +
                        " factChecks=" + ((long)Get(host, "NearBuildFactChecks") - stableFactChecks) +
                        " retirementChecks=" + ((long)Get(host, "NearBuildRetirementChecks") - stableRetirementChecks) +
                        " rangeMetadataChecks=" + ((long)Get(host, "NearBuildRangeChecks") - stableRangeChecks));
                }
                Require(scope.Goals.Where(goal => goal.WallType == 0).All(goal => !goal.Complete),
                    "blocked and out-of-range bodies never consume merely because an old queue fact survived movement");
            }
        }

        private static void NearQueueMovementStep(BusinessScope scope, int direction, bool jump)
        {
            var p = Main.LocalPlayer;
            NativeToolExecutionChecks.Sample(scope.Context, scope.Input, new Vector2(480, 540), false, direction);
            // Sample clears vanilla's trigger set. Both movement axes and jump
            // must be delivered afterwards, as in the existing native movement
            // fixtures. Never manufacture jump/release timers or a position.
            p.controlLeft = direction < 0; p.controlRight = direction > 0; p.controlJump = jump;
            PlayerInput.Triggers.Current.Left = direction < 0; PlayerInput.Triggers.Current.Right = direction > 0;
            PlayerInput.Triggers.Current.Jump = jump;
            Call(Get(scope, "pending"), "Clear"); NativeQuickItemChecks.BeginWorldStep(); p.Update(0);
            Call(scope, "FinishNativeStep"); Call(scope.Context, "UpdateRuntime"); constructionInputTail?.Invoke();
        }

        private static BusinessGoal NearQueueFurniture(BusinessSource source, int sourceX, int sourceY, int targetX, int targetY)
        {
            var data = TileObjectData.GetTileData(source.Item.createTile, source.Item.placeStyle, 0);
            Require(data != null && data.Height > 0 && data.Width > 0, "near queue furniture uses actual native object metadata");
            Require(WorldGen.PlaceObject(sourceX, sourceY, source.Item.createTile, true, source.Item.placeStyle, direction: 1),
                "near queue source furniture is produced by the original object writer");
            int width = data.Width, height = data.Height, originX = data.Origin.X, originY = data.Origin.Y;
            var frames = new List<Point>();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var tile = Main.tile[sourceX - originX + x, sourceY - originY + y];
                    Require(tile.active() && tile.type == source.Item.createTile, "every captured furniture member has its actual source body");
                    frames.Add(new Point(tile.frameX, tile.frameY));
                }
            var goal = new BusinessGoal { X = targetX, Y = targetY, Width = width, Height = height,
                OriginX = originX, Type = source.Type, TileType = source.Item.createTile, Style = source.Item.placeStyle };
            goal.CompleteBody = () =>
            {
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        var tile = Main.tile[targetX - originX + x, targetY - originY + y]; var frame = frames[y * width + x];
                        if (!tile.active() || tile.type != goal.TileType || tile.frameX != frame.X || tile.frameY != frame.Y || tile.slope() != 0 || tile.halfBrick()) return false;
                    }
                return true;
            };
            return goal;
        }

        private static void NearQueueSetup(BusinessScope scope, bool newSupports, bool warm, int logicalCount)
        {
            var p = Reset(scope.Context, scope.Host, scope.Tools, scope.Input); p.position.X = 42 * 16;
            var wall = BusinessStock(p, 12, ItemID.StoneWall, 9999);
            var platform = BusinessStock(p, 13, ItemID.WoodPlatform, 9999);
            var bench = BusinessStock(p, 14, ItemID.WorkBench, 9999);
            var chair = BusinessStock(p, 15, ItemID.WoodenChair, 9999);
            var stone = newSupports ? BusinessStock(p, 16, ItemID.StoneBlock, 9999) : null;
            scope.Sources = newSupports ? new[] { wall, platform, bench, chair, stone } : new[] { wall, platform, bench, chair };
            Require(scope.Sources.All(source => source.Item.maxStack >= 9999 && source.Item.stack == 9999),
                "every real source stack is legal native 9999 stock in ordinary main inventory");
            var goals = new List<BusinessGoal>();
            foreach (int dx in new[] { 2, 10 })
            {
                WorldGen.PlaceWall(60 + dx, 37, wall.Item.createWall, true);
                WorldGen.PlaceWall(60 + dx, 38, wall.Item.createWall, true);
                WorldGen.PlaceWall(38 + dx, 37, wall.Item.createWall, true);
                goals.Add(BusinessWallGoal(38 + dx, 38, wall));
            }
            foreach (int dx in logicalCount <= 5 ? new[] { 0 } : new[] { 0, 10 })
            {
                Require(WorldGen.PlaceTile(60 + dx, 42, platform.Item.createTile, true, true, style: platform.Item.placeStyle), "source has a real native platform");
                goals.Add(BusinessTileGoal(38 + dx, 42, platform));
            }
            foreach (int dx in logicalCount == 4 ? new[] { 7 } : new[] { 2, 7 })
                goals.Add(NearQueueFurniture(bench, 60 + dx, 42, 38 + dx, 42));
            foreach (int dx in logicalCount <= 5 ? new[] { 9 } : logicalCount == 8 ? new[] { 1, 6, 9 } : new[] { 1, 9 })
                goals.Add(NearQueueFurniture(chair, 60 + dx, 42, 38 + dx, 42));
            Require(goals.Where(goal => goal.Type == bench.Type).All(goal => goal.Width == 2 && goal.Height == 1),
                "both workbench goals have the actual complete two-member native footprint");
            if (newSupports)
                for (int x = 38; x <= 48; x++)
                {
                    NativeToolsChecks.Tile(x, 44, TileID.Stone);
                    if (x == 42 || x == 43) continue; // keep the player's real standing floor
                    WorldGen.KillTile(x, 43, false, false, true);
                    Require(!Main.tile[x, 43].active(), "dependent destination begins without its genuine furniture/platform anchor");
                    goals.Add(BusinessTileGoal(x, 43, stone));
                }
            scope.Goals = goals.ToArray();
            // Independent known-goal oracle: original reach, actual empty body,
            // real supports and collision. Never read production planner proof.
            scope.RunnableOracle = goal =>
            {
                var source = scope.Sources.Single(value => value.Type == goal.Type);
                if (goal.Complete || source.Item.IsAir || !ReferenceEquals(p.inventory[source.Slot], source.Item) || p.noBuilding ||
                    !p.IsInTileInteractionRange(goal.X, goal.Y, TileReachCheckSettings.Simple, source.Item.tileBoost + p.blockRange)) return false;
                if (goal.WallType != 0)
                    return Main.tile[goal.X, goal.Y].wall == 0 && new[] { new Point(-1, 0), new Point(1, 0), new Point(0, -1), new Point(0, 1) }.Any(offset =>
                    { var tile = Main.tile[goal.X + offset.X, goal.Y + offset.Y]; return tile.wall != 0 || tile.active() && Main.tileSolid[tile.type]; });
                for (int x = goal.X - goal.OriginX; x < goal.X - goal.OriginX + goal.Width; x++)
                    for (int y = goal.Y - goal.Height + 1; y <= goal.Y; y++) if (Main.tile[x, y].active()) return false;
                var body = new Rectangle((goal.X - goal.OriginX) * 16, (goal.Y - goal.Height + 1) * 16, goal.Width * 16, goal.Height * 16);
                return !Main.player.Any(player => player != null && player.active && !player.dead && body.Intersects(player.Hitbox)) &&
                    Enumerable.Range(goal.X - goal.OriginX, goal.Width).All(x => WorldGen.SolidTile(x, goal.Y + 1));
            };
            var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(scope.Context, scope.Host, scope.Input,
                (LibraryState)Get(scope.Host, "Library"), () => Step(scope.Context, scope.Input), 60, 38, 11, newSupports ? 6 : 5, "近域全料桌椅 " + newSupports);
            var instance = Place(scope.Context, scope.Host, scope.Input, entry, 38, 38);
            var logical = ((IEnumerable)Get(instance, "MatchRanges")).Cast<object>().ToArray();
            var physical = ((IEnumerable)Get(instance, "SourceRanges")).Cast<object>().ToArray();
            Console.WriteLine("NearQueue normalized single asset=" + entry.AssetId + " logical=" + logical.Length + " physical=" + physical.Length + " newSupports=" + newSupports);
            foreach (var range in logical)
                Console.WriteLine("NearQueue logical object=" + Get(range, "ObjectId") + " block=" + Get(range, "Block"));
            Require(Scene(scope.Host).Length == 1 && logical.Length == logicalCount &&
                logical.Count(range => (int)Get(range, "ObjectId") > 0) == goals.Count(goal => goal.WallType == 0 && goal.Type != stone?.Type),
                "saved normalization counts each platform and full furniture as an independent logical object beside one ordinary proof");
            Require(newSupports ? scope.Goals.Where(goal => goal.Type == chair.Type || goal.Type == bench.Type).All(goal => !scope.Runnable(goal)) : scope.Goals.All(scope.Runnable),
                "known mixed goals start with genuine missing supports or all independently native-runnable as declared");
            if (warm)
            {
                // Explicit WARM arm only: actual material demand obtains its
                // receipts while automation is OFF. Cold arms never do this.
                Call(scope.Host, "SetMaterialViewDemand", true);
                long generation = ((BlueprintProjectionIdentity)Get(instance, "Identity")).ProjectionGeneration;
                NativeQuickItemChecks.Until(() =>
                {
                    scope.Step();
                    var bill = (BlueprintMaterialSnapshot)Call(scope.Host, "MaterialWorld", generation);
                    return bill.InventoryReady && bill.Status == BlueprintMaterialStatus.Exact &&
                        EligibilityRanges(instance).All(range => (bool)Get(range, "Current") && !(bool)Get(range, "Reading") && !(bool)Get(range, "Demand"));
                });
                Call(scope.Host, "SetMaterialViewDemand", false);
                Require(scope.Sources.All(source => source.Item.stack == 9999), "explicit warm receipt preparation has no native consumption");
            }
        }

        private static void BusinessNearQueue(object context, object host, object tools, object input, bool newSupports, bool warm = false, int logicalCount = 7)
        {
            BusinessEvent[] automatic; uint enabled;
            using (var scope = new BusinessScope(context, host, tools, input))
            {
                NearQueueSetup(scope, newSupports, warm, logicalCount); scope.Begin(true); enabled = Main.GameUpdateCount; SetAutomatic(host, input, true);
                using (var observation = new NearQueueObservation(host, required: false))
                using (var tail = new NearQueueTailSnapshot(scope, newSupports || !warm && logicalCount == 7))
                    NativeQuickItemChecks.Until(() =>
                    {
                        tail.Capture("before"); observation.Step(scope); tail.Capture("after");
                        return scope.Goals.All(goal => goal.Complete) && !(bool)Get(Get(tools, "Use"), "Active");
                    });
                scope.Report("single full-stock near queue automatic supports=" + newSupports + " warm=" + warm);
                Require(scope.Events.Sum(entry => entry.Count) == scope.Goals.Length && scope.Sources.All(source => source.Item.stack > 9000) &&
                    scope.Sources.All(source => source.Item.stack == 9999 - scope.Goals.Count(goal => goal.Type == source.Type)),
                    "every single-template goal consumes once with all material stocks remaining above 9000");
                automatic = scope.Events.ToArray();
                if (!newSupports && !warm && logicalCount == 7)
                    Require(automatic.Zip(automatic.Skip(1), (a, b) => a.Type == ItemID.WorkBench && b.Type == ItemID.WoodenChair).Any(bridge => bridge),
                        "the principal abundant-stock template actually crosses last workbench to first chair through real adjacent consumer events");
                Console.WriteLine("NearQueue enabledToFirstWorld=" + unchecked(automatic[0].World - enabled) + " firstType=" + automatic[0].Type + " newSupports=" + newSupports + " warm=" + warm);
            }
            using (var scope = new BusinessScope(context, host, tools, input))
            {
                NearQueueSetup(scope, newSupports, warm, logicalCount); scope.Begin(false); int cursor = 0;
                NativeQuickItemChecks.Until(() =>
                {
                    Require(cursor < automatic.Length, "native queue replay needs no undeclared extra consumption");
                    var expected = automatic[cursor]; var source = scope.Sources.Single(value => value.Type == expected.Type); var p = Main.LocalPlayer;
                    if (p.selectedItem != source.Slot)
                    {
                        if (!p.selectedItemState.HasBufferedChange) p.selectedItemState.Select(source.Slot);
                        if (!p.selectedItemState.CanChangeSelectedItemImmediately) { scope.Step(); return false; }
                    }
                    int before = scope.Events.Count;
                    scope.Step(new Vector2(expected.X * 16 + 8, expected.Y * 16 + 8), p.itemAnimation > 0 || p.releaseUseItem || source.Item.autoReuse);
                    if (scope.Events.Count > before) cursor++;
                    return scope.Goals.All(goal => goal.Complete);
                });
                scope.Report("single full-stock near queue original supports=" + newSupports);
                Require(scope.Events.Count == automatic.Length && scope.Events.Zip(automatic,
                    (native, actual) => native.Type == actual.Type && native.Count == actual.Count && native.ChangedGoals.SequenceEqual(actual.ChangedGoals)).All(same => same),
                    "same initial native world and inventory replay each full furniture/wall/platform goal with exact independent item costs");
                for (int i = 1; i < automatic.Length; i++)
                {
                    uint actualGap = unchecked(automatic[i].World - automatic[i - 1].World);
                    uint nativeGap = unchecked(scope.Events[i].World - scope.Events[i - 1].World);
                    Console.WriteLine("NearQueue gap index=" + i + " previousType=" + automatic[i - 1].Type + " nextType=" + automatic[i].Type + " automatic=" + actualGap + " original=" + nativeGap + " extra=" + ((long)actualGap - nativeGap));
                    Require(actualGap == nativeGap, "each full-stock same-template transition uses its original input opportunity, including whole workbenches and chairs");
                }
            }
        }

        // Temporary fixture-local diagnostic: bounded strings, no product
        // logging and no planner/eligibility/selection method invocation.
        // Capture the exact already-computed state across last bench -> chair.
        private sealed class NearQueueTailSnapshot : IDisposable
        {
            private readonly BusinessScope scope;
            private readonly bool enabled;
            private readonly List<string> rows = new List<string>();
            private bool started, ended;
            internal NearQueueTailSnapshot(BusinessScope scope, bool enabled) { this.scope = scope; this.enabled = enabled; }
            private static string Value(object owner, string name)
            { return owner == null ? "none" : Convert.ToString(GetOptional(owner, name)) ?? "none"; }
            private static string Flight(object flight)
            {
                if (flight == null) return "none";
                var target = GetOptional(flight, "Target"); var source = GetOptional(flight, "Source");
                return Value(source, "Type") + "@" + Value(source, "Slot") + ":" + Value(target, "WorldX") + "," + Value(target, "WorldY") +
                    ":checked=" + Value(flight, "Checked") + ":rejected=" + Value(flight, "Rejected") + ":gen=" + Value(flight, "Generation");
            }
            private static string Head(object head)
            {
                if (head == null) return "none";
                var seed = GetOptional(head, "Seed"); var range = seed == null ? null : GetOptional(seed, "Range");
                var cell = seed == null ? null : GetOptional(seed, "Cell");
                return "type=" + Value(head, "Type") + ":ready=" + Value(head, "Ready") + ":pending=" + Value(head, "Pending") +
                    ":cell=" + Value(cell, "X") + "," + Value(cell, "Y") + ":ordinal=" + Value(seed, "Ordinal") +
                    ":v=" + Value(head, "Version") + "/" + Value(range, "Version") + ":raw=" + Value(range, "RawVersion") +
                    ":support=" + Value(head, "Support") + ":player=" + Value(head, "Player") + ":supply=" + Value(head, "SupplyRevision") +
                    ":group=" + Value(head, "PlanGroup") + ":wait=" + Value(head, "Wait") + ":retry=" + Value(head, "Retry");
            }
            internal void Capture(string phase)
            {
                if (!enabled || ended || rows.Count >= 40) return;
                bool benchDone = scope.Events.Any(entry => entry.Type == ItemID.WorkBench) &&
                    scope.Goals.Where(goal => goal.Type == ItemID.WorkBench).All(goal => goal.Complete);
                if (!started && !benchDone) return;
                started = true;
                var host = scope.Host; var p = Main.LocalPlayer; var use = Get(scope.Tools, "Use");
                var group = GetOptional(host, "buildContinuation");
                var plans = GetOptional(host, "nearBuildRanges") as IDictionary;
                var supports = GetOptional(host, "nearProofSupports") as IDictionary;
                var rangeRows = new List<string>();
                if (plans != null)
                    foreach (DictionaryEntry entry in plans)
                    {
                        if (rangeRows.Count >= 8) break;
                        var range = entry.Key; var state = entry.Value;
                        var proof = supports != null && supports.Contains(range) ? supports[range] : null;
                        // This observer query only reads the existing routed
                        // stamp under its lock; it does not advance world work.
                        var changes = Get(host, "projectionChanges");
                        object[] stamp = { Get(range, "Watch"), Get(range, "PartIndex"), 0L };
                        bool stampReady = (bool)changes.GetType().GetMethod("TryGetBuildStamp", F).Invoke(changes, stamp);
                        rangeRows.Add("object=" + Value(range, "ObjectId") + ":v=" + Value(range, "Version") + ":raw=" + Value(range, "RawVersion") +
                            ":current=" + Value(range, "Current") + ":reading=" + Value(range, "Reading") + ":complete=" + Value(state, "Complete") +
                            ":cursor=" + Value(state, "Cursor") + ":facts=" + Value(GetOptional(state, "Facts"), "Count") +
                            ":wake=" + Value(GetOptional(range, "Materials"), "BuildWakeRevision") +
                            ":currentSupportReady/stamp=" + stampReady + "/" + stamp[2] + ":classifiedSupport=" + Value(state, "Support") +
                            ":proofVersion/raw/wake/support=" + Value(proof, "Version") + "/" + Value(proof, "RawVersion") + "/" +
                            Value(proof, "Wake") + "/" + Value(proof, "Support"));
                    }
                rows.Add("phase=" + phase + " world=" + Main.GameUpdateCount + " input=" + Get(scope.Input, "Frame") +
                    " time/animation/reuse=" + p.itemTime + "/" + p.itemAnimation + "/" + p.reuseDelay + " release=" + p.releaseUseItem +
                    " selected=" + p.selectedItem + " override/buffer/immediate=" + p.selectedItemState.HasActiveOverride + "/" +
                    p.selectedItemState.HasBufferedChange + "/" + p.selectedItemState.CanChangeSelectedItemImmediately +
                    " useActive/native/token=" + Get(use, "Active") + "/" + Get(use, "InNativeUse") + "/" + Get(use, "Operation") +
                    " group=" + Value(group, "Group") + ":pending=" + Value(group, "Pending") + " absence=" + Value(host, "nearAbsenceReady") +
                    ":coverage=" + Value(host, "nearCoverageCurrent") + ":dirty=" + Value(host, "nearPlanDirty") +
                    " proofContainer/Restoration/Dependencies/Retry=" + Value(host, "nearProofContainer") + "/" + Value(host, "nearProofRestoration") + "/" +
                    Value(host, "nearProofDependencies") + "/" + Value(host, "nearProofRetry") +
                    " currentContainer/Restoration/materialTick=" + Value(host, "buildContainerRevision") + "/" + Value(host, "appearanceRestorationRevision") + "/" + Value(host, "materialTick") +
                    " active=" + Flight(GetOptional(host, "activeBuild")) + " prepared=" + Flight(GetOptional(host, "preparedBuild")) +
                    " preferred=" + Head(GetOptional(host, "nearPreferredHead")) + " foreign=" + Head(GetOptional(host, "nearForeignHead")) +
                    " sourceCount/bytes=" + Value(GetOptional(host, "buildSources"), "Count") + "/" + Value(host, "buildSourceBytes") +
                    " facts/bytes=" + Value(host, "NearBuildFacts") + "/" + Value(host, "NearBuildPlanBytes") +
                    " workFact/retirement/range=" + Value(host, "NearBuildFactChecks") + "/" + Value(host, "NearBuildRetirementChecks") + "/" + Value(host, "NearBuildRangeChecks") +
                    " ranges=[" + string.Join(";", rangeRows) + "]");
                if (scope.Events.Any(entry => entry.Type == ItemID.WoodenChair)) ended = true;
            }
            public void Dispose()
            { if (enabled) foreach (string row in rows) Console.WriteLine("NearQueue tail snapshot " + row); }
        }
    }
}
