using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        // Component evidence deliberately stops at immutable physical geometry.
        // Real library normalization packs small chunks together; do not claim
        // that these33 legal physical descriptors came from33 saved chunks, or
        // inject them into native Match/targets to manufacture a large scenario.
        private static void FocusDirectoryBudgetComponent(object host)
        {
            var sourceType=typeof(BlueprintProjectionSourceRange);
            var constructor=sourceType.GetConstructors(F).Single(value=>value.GetParameters().Length==9);
            var dependencyType=constructor.GetParameters()[6].ParameterType.GetGenericArguments()[0];
            var sources=Enumerable.Range(0,33).Select(index=>(BlueprintProjectionSourceRange)constructor.Invoke(new object[]{
                (long)index,new BlueprintProjectionBounds(index,0,1,1),1L,new[]{(long)(uint)index},new ushort[]{1},new int[]{0},
                Array.CreateInstance(dependencyType,0),index,0})).ToArray();
            var table=host.GetType().GetField("buildPhysicalDirectories",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            var get=table.GetType().GetMethod("GetValue");
            var factory=Delegate.CreateDelegate(get.GetParameters()[1].ParameterType,host,host.GetType().GetMethod("CreateBuildPhysicalDirectory",F));
            long builds=(long)Get(host,"BuildFocusDirectoryBuilds"),construction=(long)Get(host,"BuildFocusDirectoryConstructionSteps"),
                querySteps=(long)Get(host,"BuildFocusDirectorySteps");
            object first=get.Invoke(table,new object[]{sources,factory}),second=get.Invoke(table,new object[]{sources,factory});
            Require(ReferenceEquals(first,second) && !(bool)Get(first,"Ready") && (long)Get(host,"BuildFocusDirectoryBuilds")==builds+1,
                "two owners of the same immutable qualified geometry share one unfinished directory without restarting it");
            var queryType=host.GetType().GetNestedType("BuildFocusNearQuery",BindingFlags.NonPublic);
            var queries=Enumerable.Range(0,2).Select(_=>Activator.CreateInstance(queryType,true)).ToArray();
            foreach(var query in queries)
            {
                Set(query,"Directory",first);Set(query,"Query",new BlueprintProjectionBounds(0,0,33,1));Set(query,"Started",true);
                ((Stack<int>)Get(query,"Stack")).Push(0);
            }
            int priorBudget=(int)Get(host,"materialBuildSearchBudget"),updates=0,owner=0;
            var routes=new[]{new List<int>(),new List<int>()};
            try
            {
                while(queries.Any(query=>!(bool)Get(query,"Complete")))
                {
                    Set(host,"materialBuildSearchBudget",64);long before=(long)Get(host,"BuildFocusDirectorySteps")+(long)Get(host,"BuildFocusDirectoryConstructionSteps");
                    int idle=0;
                    while((int)Get(host,"materialBuildSearchBudget")>0 && idle<2)
                    {
                        int current=owner;owner=1-owner;
                        bool progressed=(bool)Call(host,"AdvanceBuildFocusNearQuery",queries[current],new Action<int>(index=>routes[current].Add(index)));
                        idle=progressed?0:idle+1;
                    }
                    updates++;
                    Require((long)Get(host,"BuildFocusDirectorySteps")+(long)Get(host,"BuildFocusDirectoryConstructionSteps")-before<=64,
                        "directory construction and both owner queries share the unchanged64-step allowance");
                    if(updates==1)Require(!(bool)Get(first,"Ready") && routes.All(value=>value.Count==0),
                        "a65-node directory is genuinely Pending after64 construction steps, with no premature owner query");
                    Require(Math.Abs(routes[0].Count-routes[1].Count)<=1 && updates<=4,
                        "one-node owner turns converge for both later and earlier owners without starvation");
                }
                long stable=(long)Get(host,"BuildFocusDirectorySteps")+(long)Get(host,"BuildFocusDirectoryConstructionSteps");
                for(int pass=0;pass<12;pass++)foreach(var query in queries)
                    Require(!(bool)Call(host,"AdvanceBuildFocusNearQuery",query,new Action<int>(_=>throw new InvalidOperationException("stable query emitted again"))),
                        "a completed stable geometric query performs no work");
                Require(routes.All(value=>value.Count==33 && value.Distinct().Count()==33) &&
                    (long)Get(host,"BuildFocusDirectoryConstructionSteps")-construction==65 && (long)Get(host,"BuildFocusDirectorySteps")-querySteps==130 &&
                    (long)Get(host,"BuildFocusDirectoryBuilds")==builds+1 &&
                    stable==(long)Get(host,"BuildFocusDirectorySteps")+(long)Get(host,"BuildFocusDirectoryConstructionSteps") &&
                    (long)Get(first,"Bytes")==72+65*32 && (long)Get(host,"BuildFocusDirectoryTemporaryBytes")==0,
                    "the shared segment tree accounts for65 constant construction steps,130 owner node visits,2152 resident bytes and no sort buffer");
                Console.WriteLine("BlueprintFocusDirectory component physicalGroups=33 nodes=65 owners=2 updates="+updates+
                    " builds=1 constructionSteps=65 queryNodes=130 routedLeaves=33/33 residentBytes="+Get(first,"Bytes")+
                    " temporaryOrderBytes=0 stableCalls=24 stableWork=0 nativeWorld=NOT_EVALUATED");
            }
            finally { Set(host,"materialBuildSearchBudget",priorBudget); }
        }

        private static void FocusFailedQualification(object context,object host,object tools,object input)
        {
            using(var scope=new BusinessScope(context,host,tools,input))
            {
                var p=Reset(context,host,tools,input);p.position.X=42*16;
                var stone=BusinessStock(p,12,ItemID.StoneBlock,4);var dirt=BusinessStock(p,13,ItemID.DirtBlock,2);
                scope.Sources=new[]{stone,dirt};
                scope.Goals=new[]{BusinessTileGoal(40,42,stone),BusinessTileGoal(44,42,stone),BusinessTileGoal(46,42,dirt)};
                var stoneAsset=BusinessTileAsset(host,stone);
                Place(context,host,input,stoneAsset,40,42);Place(context,host,input,stoneAsset,44,42);
                var failed=Place(context,host,input,Asset(host,new CaptureCell{Layers=CaptureLayer.Wall,WallType=WallID.Wood}),42,40);
                Place(context,host,input,BusinessTileAsset(host,dirt),46,42);
                Until(context,input,()=>GetOptional(failed,"Qualification")!=null && GetOptional(failed,"Match")!=null);
                var receipt=(BlueprintProjectionQualificationReceipt)Get(failed,"Qualification");
                // Contract-level completed failure receipt, with the actual
                // owner/catalog/mask/view identity. No file-read failure or
                // worker timing claim is made, and no completion is fabricated.
                var error=(BlueprintProjectionQualificationReceipt)Activator.CreateInstance(typeof(BlueprintProjectionQualificationReceipt),F,null,new object[]{
                    receipt.Identity,receipt.Catalog,receipt.Mask,receipt.TotalRequiredLayerCount,receipt.EffectiveRequiredLayerCount,
                    receipt.EffectiveUnknownLayerCount,receipt.SourceRanges,receipt.MatchRanges,"fixture-completed-qualification-failure",receipt.View},null);
                Call(host,"ReleaseProjectionMatch",failed);Call(host,"AcceptProjectionQualification",failed,error);
                Require((bool)Call(host,"ProjectionQualificationFailed",failed) && !(bool)Call(host,"ProjectionQualificationCurrent",failed),
                    "only a completed failure under the exact current identity is classified as failed construction qualification");
                scope.Begin(true);SetAutomatic(host,input,true);
                NativeQuickItemChecks.Until(()=>{scope.Step();return scope.Goals.All(goal=>goal.Complete) && !(bool)Get(Get(tools,"Use"),"Active");});
                scope.Report("completed qualification failure beside healthy groups");
                Require(scope.Events.Count==3 && scope.Events.Count(value=>value.Type==stone.Type)==2 && scope.Events.Count(value=>value.Type==dirt.Type)==1 &&
                    stone.Item.stack==2 && dirt.Item.stack==1 && Scene(host).Length==1 && ReferenceEquals(Scene(host)[0],failed) &&
                    ReferenceEquals(Get(failed,"Qualification"),error) && GetOptional(failed,"Match")==null && (bool)Get(failed,"Opened"),
                    "the failed display remains honest while healthy same-group goals and then the available other material really consume");
                Console.WriteLine("BlueprintFocus qualificationFailure=current completed receipt-contract fixture; display retained, Match absent; native healthy order="+
                    string.Join(",",scope.Events.Select(value=>value.Type)));
            }
        }

        private static object focusRetirementHost, focusRetirementTools;
        private static object focusAdmissionHost;
        private static BusinessGoal focusAdmissionGoal;
        private static void FocusAdmissionObserved(object __instance,object __0)
        {
            if(!ReferenceEquals(__instance,focusAdmissionHost))return;
            var group=GetOptional(__instance,"buildContinuation");var p=Main.LocalPlayer;
            Console.WriteLine("BlueprintFocus actual admission world="+Main.GameUpdateCount+" host="+Get(__instance,"projectionMatchTick")+
                " revision="+Get(__instance,"MaterialBuildEligibilityRevision")+" position="+p.position+
                " oldType="+(group==null?0:Get(Get(group,"Source"),"Type"))+" nextType="+Get(Get(__0,"Source"),"Type")+
                " enteredRunnable="+businessScope.Runnable(focusAdmissionGoal)+" token="+Get(__0,"Token"));
        }
        private static void FocusAdmissionConsumer(Player __instance)
        {
            if(focusAdmissionHost==null || !ReferenceEquals(__instance,Main.LocalPlayer))return;
            Console.WriteLine("BlueprintFocus actual consumer world="+Main.GameUpdateCount+" host="+Get(focusAdmissionHost,"projectionMatchTick")+
                " revision="+Get(focusAdmissionHost,"MaterialBuildEligibilityRevision")+" position="+__instance.position+
                " item="+__instance.HeldItem.type+" enteredRunnable="+businessScope.Runnable(focusAdmissionGoal));
        }
        private static object[] focusRetirementMaterials;
        private static long focusRetirementGroup;
        private static int focusRetirementObserved, focusRetirementReleased, focusRetirementExact;
        private static void FocusRetirementBefore(object __instance)
        {
            if(!ReferenceEquals(__instance,focusRetirementHost))return;
            var group=GetOptional(__instance,"buildContinuation");focusRetirementGroup=group==null?0:(long)Get(group,"Group");
            focusRetirementMaterials=group==null?Array.Empty<object>():
                ((System.Collections.IEnumerable)Get(Get(group,"Discovery"),"Ranges")).Cast<object>()
                    .Where(range=>GetOptional(Get(range,"Owner"),"Match")!=null)
                    .Select(range=>GetOptional(range,"Materials")).Where(value=>value!=null &&
                        (long)Get(value,"PreferredGroup")==focusRetirementGroup &&
                        GetOptional(value,"PreferredNegativeOrdinals")!=null && (int)Get(Get(value,"PreferredNegativeOrdinals"),"Count")>0 &&
                        GetOptional(value,"PreferredPositionNegatives")!=null && (int)Get(Get(value,"PreferredPositionNegatives"),"Count")>0).ToArray();
        }
        private static void FocusRetirementAfter(object __instance)
        {
            if(!ReferenceEquals(__instance,focusRetirementHost) || focusRetirementGroup==0 || focusRetirementMaterials.Length==0)return;
            var group=GetOptional(__instance,"buildContinuation");if(group==null || (long)Get(group,"Group")==focusRetirementGroup)return;
            focusRetirementObserved++;
            if(focusRetirementMaterials.All(value=>GetOptional(value,"PreferredNegativeOrdinals")==null && GetOptional(value,"PreferredPositionNegatives")==null))focusRetirementReleased++;
            var flight=GetOptional(__instance,"activeBuild");var use=Get(focusRetirementTools,"Use");
            if(flight!=null && (long)Get(flight,"Token")== (long)Get(use,"Operation") && (long)Get(use,"Operation")>0 &&
                ReferenceEquals(Get(group,"Source"),Get(flight,"Source")) &&
                (bool)Call(use,"OwnsBuildMaterial",Get(Get(flight,"Source"),"Item")))focusRetirementExact++;
        }

        private static void FocusLogicalObjectNativeReach(object context,object host,object tools,object input,bool retirement)
        {
            using(var scope=new BusinessScope(context,host,tools,input))
            {
                var p=Reset(context,host,tools,input);p.position.X=44*16-p.width;p.velocity=Microsoft.Xna.Framework.Vector2.Zero;
                var table=BusinessStock(p,12,ItemID.WoodenTable,4);
                if(retirement)
                {
                    // A real unrelated long tool widens only the old ordinary
                    // broad phase. Table reach remains its exact vanilla reach.
                    p.inventory[17].SetDefaults(ItemID.SpectrePickaxe);
                    Require(p.inventory[17].tileBoost>table.Item.tileBoost,"the control supplies a genuine broader ordinary envelope without extending Table reach");
                }
                Require(WorldGen.PlaceObject(61,42,TileID.Tables,true,table.Item.placeStyle),"original writer builds the complete3x2 source Table with an offset click origin");
                var frameX=new short[6];var frameY=new short[6];
                for(int index=0;index<6;index++)
                {var cell=Main.tile[60+index%3,41+index/3];Require(cell.active() && cell.type==TileID.Tables,"all six native source Table members exist");frameX[index]=cell.frameX;frameY[index]=cell.frameY;}
                var asset=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),
                    ()=>Step(context,input),60,41,3,2,"偏置原点木桌边界");
                int left,top,right,bottom;
                Terraria.DataStructures.TileReachCheckSettings.Simple.GetTileRegion(p,out left,out top,out right,out bottom,table.Item.tileBoost+p.blockRange);
                Func<int,int,BusinessGoal> goal=(click,y)=>new BusinessGoal{X=click,Y=y,Type=table.Type,TileType=TileID.Tables,Width=3,Height=2,OriginX=1,
                    CompleteBody=()=>Enumerable.Range(0,6).All(index=>{var cell=Main.tile[click-1+index%3,y-1+index/3];return cell.active() && cell.type==TileID.Tables && cell.frameX==frameX[index] && cell.frameY==frameY[index];})};
                var first=goal(right-8,42);var entered=goal(right+1,42);var outside=goal(right+2,39);
                // The second outside object enters the discovery rectangle
                // with the real movement, but its click remains one tile too
                // far. Its separate higher floor keeps it alive and distinct
                // from the Table that will actually be placed below it.
                for(int x=outside.X-1;x<=outside.X+1;x++)WorldGen.PlaceTile(x,40,TileID.Stone,true,true);
                var dirt=BusinessStock(p,13,ItemID.DirtBlock,2);var foreign=BusinessTileGoal(right-3,42,dirt);var dirtAsset=BusinessTileAsset(host,dirt);dirt.Item.TurnToAir();
                scope.Sources=new[]{table,dirt};scope.Goals=new[]{first,entered,outside,foreign};
                Place(context,host,input,asset,first.X-1,41);var enteredOwner=Place(context,host,input,asset,entered.X-1,41);
                var retained=Place(context,host,input,asset,outside.X-1,outside.Y-1);Place(context,host,input,dirtAsset,foreign.X,42);
                Require(scope.Runnable(first) && !scope.Runnable(entered) && !scope.Runnable(outside) &&
                    entered.X-1==right && outside.X-1==right+1,
                    "the first outside body overlaps the old near boundary, and the second will overlap the entered boundary while its exact click stays outside");
                var observer=new HarmonyLib.Harmony("JueMingR.Tests.FocusRetirement");
                try
                {
                    observer.Patch(host.GetType().GetMethod("RememberBuildContinuation",F),
                        prefix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(FocusAdmissionObserved)));
                    observer.Patch(typeof(Player).GetMethod("PlaceThing_Tiles_PlaceIt",F),
                        prefix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(FocusAdmissionConsumer)));
                    if(retirement)
                    {
                        focusRetirementHost=host;focusRetirementTools=tools;focusRetirementMaterials=Array.Empty<object>();
                        focusRetirementObserved=focusRetirementReleased=focusRetirementExact=0;
                        observer.Patch(host.GetType().GetMethod("RememberBuildContinuation",F),
                            prefix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(FocusRetirementBefore)),
                            postfix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(FocusRetirementAfter)));
                    }
                    scope.Begin(true);SetAutomatic(host,input,true);
                    NativeQuickItemChecks.Until(()=>{scope.Step();return scope.Events.Count>0;});
                    Require(scope.Events.Count==1 && first.Complete,"one complete original Table establishes the actual business group");
                    var refill=new Item();refill.SetDefaults(ItemID.DirtBlock);refill.stack=2;
                    Require(p.GetItem(refill,new GetItemSettings(NoText:true,NoSound:true)).IsAir,"original GetItem enables only the foreign Dirt after the first Table");
                    dirt.Slot=Enumerable.Range(0,50).Single(slot=>p.inventory[slot].type==ItemID.DirtBlock && p.inventory[slot].stack==2);dirt.Item=p.inventory[dirt.Slot];
                    object prepared=null;
                    NativeQuickItemChecks.Until(()=>{scope.Step();prepared=GetOptional(host,"preparedBuild");return prepared!=null && ReferenceEquals(Get(Get(prepared,"Source"),"Item"),dirt.Item);});
                    Require(scope.Events.Count==1 && scope.Runnable(foreign) && !scope.Runnable(entered),"a genuine foreign plan is prepared while the logical Table click is still outside exact native reach");
                    var positionProofs=((System.Collections.IEnumerable)Get(Get(enteredOwner,"Match"),"Ranges")).Cast<object>()
                        .Select(range=>GetOptional(range,"Materials")).Where(value=>value!=null)
                        .Select(value=>new{Value=value,Proofs=GetOptional(value,"PreferredPositionNegatives")})
                        .Where(value=>value.Proofs!=null).SelectMany(value=>((System.Collections.IEnumerable)value.Proofs).Cast<object>()
                            .Select(pair=>new{Value=value.Value,Ordinal=(int)Get(pair,"Key"),Proof=Get(pair,"Value")}))
                        .Where(value=>(bool)Get(value.Proof,"Active") && (int)Get(value.Proof,"X")==entered.X && (int)Get(value.Proof,"Y")==entered.Y).ToArray();
                    Require(positionProofs.Length>0 && positionProofs.All(value=>(bool)Call(Get(value.Value,"PreferredNegativeOrdinals"),"Contains",value.Ordinal)),
                        "the actual preferred logical-object reach rejection records its own live-position dependency before a foreign plan is admitted");
                    Require(p.itemAnimation>0 || (bool)Get(Get(tools,"Use"),"Active"),
                        "native movement is observed while the preceding Table use still owns its natural interval, before any foreign admission");
                    Console.WriteLine("BlueprintFocus prepared foreign boundary world="+Main.GameUpdateCount+" host="+Get(host,"projectionMatchTick")+
                        " revision="+Get(host,"MaterialBuildEligibilityRevision")+" position="+p.position+" animation/time="+p.itemAnimation+"/"+p.itemTime+
                        " prepared="+Get(prepared,"Generation")+" currentItem="+table.Type+" nextItem="+dirt.Type+" enteredClick="+entered.X);
                    focusAdmissionHost=host;focusAdmissionGoal=entered;
                    float oldX=p.position.X;scope.Step(direction:1);
                    Require(p.position.X>oldX && (scope.Runnable(entered) || entered.Complete),"real native movement crosses the offset click boundary while the old use is still active");
                    NativeQuickItemChecks.Until(()=>{scope.Step();return first.Complete && entered.Complete && foreign.Complete && !(bool)Get(Get(tools,"Use"),"Active");});
                    scope.Report(retirement?"live-range Focus retirement":"logical-object live-position dependency and natural movement");
                    Require(scope.Events.Count==3 && scope.Events.Select(value=>value.Type).SequenceEqual(new[]{table.Type,table.Type,dirt.Type}) &&
                        table.Item.stack==2 && dirt.Item.stack==1 && !outside.Complete && !scope.Runnable(outside) &&
                        Scene(host).Length==1 && ReferenceEquals(Scene(host)[0],retained) && GetOptional(retained,"Match")!=null,
                        "the newly reachable whole Table precedes the already prepared Dirt; the still-outside logical object stays alive and unconsumed");
                    if(retirement)Require(focusRetirementObserved>0 && focusRetirementObserved==focusRetirementReleased && focusRetirementObserved==focusRetirementExact,
                        "a real switch with both negative caches present on a still-live old range releases both immediately and preserves the new exact lease; observed="+
                        focusRetirementObserved+" released="+focusRetirementReleased+" exact="+focusRetirementExact);
                    Console.WriteLine("BlueprintFocus logicalObject retirement="+retirement+" beforeX="+oldX+" afterX="+p.position.X+" enteredClick="+entered.X+
                        " retainedOutside="+outside.X+" liveCacheTransitions="+focusRetirementObserved+" released="+focusRetirementReleased+" exact="+focusRetirementExact);
                }
                finally { observer.UnpatchAll(observer.Id);focusRetirementHost=focusRetirementTools=focusAdmissionHost=null;focusAdmissionGoal=null;focusRetirementMaterials=null; }
            }
        }
    }
}
