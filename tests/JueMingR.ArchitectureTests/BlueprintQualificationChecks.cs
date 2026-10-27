using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Blueprint;

namespace JueMingR.ArchitectureTests
{
    internal static class BlueprintQualificationChecks
    {
        private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
        internal static void Check(IList<string> failures)
        {
            try { ExactOutputAndFailures(); MixedCompiledAndFailedDeclarations(); SiblingLayersAndWorld(); }
            catch (Exception error) { failures.Add("Blueprint target qualification: " + error); }
        }
        // These are pure transport declarations, not a substitute for the
        // Native probe's actual .8 item/flexible metadata enumeration.
        internal static BlueprintPlacementCatalog Catalog(long revision = 901, bool complete = true, bool abi = true)
        {
            return new BlueprintPlacementCatalog(revision, new[] {
                new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, 3000, 5114, 1, 700, 0, flexible:true, requiresObject:true), false),
                new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, 3001, 3, 1, 648, 15, flexible:true, width:3, height:2, requiresObject:true), false, 810, 0, rowOffsets:new[]{0,18}),
                new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, 1001, 1001, 1, 1, 0), true),
                new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, 1500, 1500, 1, 500, 0), true),
                new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, 1501, 1501, 1, 500, 0), true) },
                new Dictionary<int,BlueprintPlacementDescriptor>{{7,new BlueprintPlacementDescriptor(BlueprintPlacementKind.Wall,1003,1003,1,-1,0,7)}},
                new Dictionary<int,int>{{1,1004}}, new Dictionary<int,int>{{1,1005},{2,1006}},1007,1008,
                declarations:new[]{new BlueprintPlacementDeclaration(BlueprintPlacementKind.Tile,1502,501,0,0,BlueprintProducerSource.Flexible,false)},
                coverage:new BlueprintProducerCoverage(complete,complete,abi,new[]{502}));
        }
        private static CaptureCell Tile(ushort type) { return new CaptureCell { Layers=CaptureLayer.Tile,TileType=type }; }
        private static BlueprintPlacementCatalog MixedDeclarations(bool complete, int failedStyle = 2)
        {
            var rules = new List<BlueprintPlacementRule> {
                new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile,1600,1600,1,503,2,requiresObject:true),false,36) };
            if (complete) rules.Add(new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile,1601,1600,1,503,2,flexible:true,requiresObject:true),false,36));
            return new BlueprintPlacementCatalog(905,rules,new Dictionary<int,BlueprintPlacementDescriptor>(),new Dictionary<int,int>(),new Dictionary<int,int>(),1007,1008,
                declarations:new[]{
                    new BlueprintPlacementDeclaration(BlueprintPlacementKind.Tile,1600,503,2,0,BlueprintProducerSource.Direct,true),
                    new BlueprintPlacementDeclaration(BlueprintPlacementKind.Tile,1601,503,failedStyle,0,BlueprintProducerSource.Flexible,complete)},
                coverage:new BlueprintProducerCoverage(true,true,true));
        }
        private static void MixedCompiledAndFailedDeclarations()
        {
            var cell=Tile(503);cell.FrameX=36;cell.ObjectId=9;
            var obj=new CaptureObject{Id=9,Type=503,Style=2,PlacementStyle=2,Width=1,Height=1,Reliable=true};
            var mixed=MixedDeclarations(false);var qualification=mixed.QualifyTile(cell,obj);
            Require(qualification.Disposition==BlueprintTargetDisposition.Construction && qualification.Resolution==BlueprintPlacementResolutionKind.Unsupported &&
                qualification.Reason==BlueprintTargetReason.DeclaredOutputUnsupported && qualification.Placement==null && mixed.Tile(cell,obj)==null && mixed.Tile(cell)==null,
                "a compiled rule cannot hide an uncompiled same-output producer or leak its descriptor through either automatic candidate outlet");
            var staticBill=new BlueprintMaterialAccumulator();BlueprintMaterialSteps.StaticObject(obj,cell,mixed,staticBill);
            var missingBill=new BlueprintMaterialAccumulator();BlueprintMaterialSteps.ObjectBody(BlueprintBodyState.Missing,cell,mixed,missingBill,obj);
            Require(staticBill.Status==BlueprintMaterialStatus.Unresolved && staticBill.Counts.Count==0 && missingBill.Status==BlueprintMaterialStatus.Unresolved && missingBill.Counts.Count==0,
                "mixed successful/failed declarations keep static and missing-body material bills unresolved instead of billing only the successful producer");
            var selection=mixed.Select(cell,obj);var proof=BlueprintWorldMatchComparer.CompareCell(cell,new BlueprintWorldMatchSample(true,cell),selection,BlueprintWorldMatchState.Match);
            Require(selection.RequiredBits==1 && selection.UnknownBits==0 && proof.MatchBits==1 && proof.UnknownBits==0,
                "a proven player target retains construction responsibility and accepts independent complete world proof despite the incomplete bill");
            var loose=cell;loose.ObjectId=0;
            Require(BlueprintMaterialSteps.SameBody(loose,new BlueprintWorldMatchSample(true,loose),mixed)==BlueprintBodyState.Unknown,
                "the candidate consumption gate must not erase compiled complete-object shape requirements from body comparison");
            var alias=obj;alias.Style=7;
            Require(mixed.QualifyTile(cell,alias).Resolution==BlueprintPlacementResolutionKind.Unsupported,
                "a matching original placement style still scopes a failed declaration when reverse style uses a different canonical value");
            var distinct=MixedDeclarations(false,3);
            Require(distinct.QualifyTile(cell,obj).Resolution==BlueprintPlacementResolutionKind.Exact && distinct.Tile(cell,obj)?.ConsumeItemType==1600 && distinct.Tile(cell)==null,
                "reliable different output style excludes an unrelated failure; absence of style metadata cannot assume that exclusion");
            var complete=MixedDeclarations(true);var completeBill=new BlueprintMaterialAccumulator();BlueprintMaterialSteps.StaticObject(obj,cell,complete,completeBill);
            Require(complete.QualifyTile(cell,obj).Resolution==BlueprintPlacementResolutionKind.Exact && complete.Tile(cell,obj)?.ConsumeItemType==1600 && complete.Tile(cell)?.ConsumeItemType==1600 &&
                completeBill.Status==BlueprintMaterialStatus.Exact && completeBill.Counts.Count==1 && completeBill.Counts[1600]==1,
                "fully compiled same-consumption declarations remain an exact material and automatic placement positive control");
        }
        private static void ExactOutputAndFailures()
        {
            var catalog = Catalog();
            Require(catalog.QualifyTile(Tile(624)).Disposition==BlueprintTargetDisposition.DisplayOnly && catalog.QualifyTile(Tile(187)).Disposition==BlueprintTargetDisposition.DisplayOnly,
                "no producer for the original output type is a negative only under complete locked coverage");
            var replica=Tile(700); var flower=catalog.QualifyTile(replica,new CaptureObject{Id=2,Type=700,Width=1,Height=1,Reliable=true});
            var pile=Tile(648);pile.FrameX=810;
            var pileQualification=catalog.QualifyTile(pile,new CaptureObject{Id=1,Type=648,Style=15,PlacementStyle=15,Width=3,Height=2,Reliable=true});
            Require(flower.Disposition==BlueprintTargetDisposition.Construction && flower.Resolution==BlueprintPlacementResolutionKind.Exact && flower.Placement.Flexible && flower.Placement.ConsumeItemType==5114 &&
                pileQualification.Disposition==BlueprintTargetDisposition.Construction && pileQualification.Placement.ConsumeItemType==3,
                "player-placeable replicas retain material and world responsibility even when the executor skips Flexible");
            var unsupported=catalog.QualifyTile(Tile(501)); var ambiguous=catalog.QualifyTile(Tile(500));
            Require(unsupported.Disposition==BlueprintTargetDisposition.Construction && unsupported.Resolution==BlueprintPlacementResolutionKind.Unsupported && unsupported.Reason==BlueprintTargetReason.DeclaredOutputUnsupported,
                "a retained declaration cannot disappear when its selector fails to compile");
            Require(ambiguous.Disposition==BlueprintTargetDisposition.Construction && ambiguous.Resolution==BlueprintPlacementResolutionKind.Ambiguous && ambiguous.Placement==null,
                "two different consumptions remain explicit ambiguity rather than lowest-item-ID selection or scenery");
            var badFrame=replica;badFrame.FrameX=17;
            Require(catalog.QualifyTile(badFrame).Disposition==BlueprintTargetDisposition.Unresolved && catalog.Select(badFrame).UnknownBits==1,
                "existing producer plus unexplained exact frame remains qualification unknown");
            Require(Catalog(902,abi:false).QualifyTile(Tile(624)).Disposition==BlueprintTargetDisposition.Unresolved && Catalog(903,complete:false).Select(Tile(187)).UnknownBits==1 &&
                catalog.Select(Tile(502)).UnknownBits==1,"missing flexible ABI, incomplete pass and uncovered special output domain cannot authorize a negative");
            var bill = new BlueprintMaterialAccumulator();BlueprintMaterialSteps.StaticCell(Tile(501),catalog,bill);BlueprintMaterialSteps.StaticCell(Tile(500),catalog,bill);
            Require(bill.Status==BlueprintMaterialStatus.Unresolved && bill.Counts.Count==0,"declared unsupported and ambiguous material targets are not fabricated as exact zero");
            var oldKey=new BlueprintMaterialKey(Guid.Empty,false,Guid.Empty,"",false,null,catalog.Revision,classifierVersion:1);
            var newKey=new BlueprintMaterialKey(Guid.Empty,false,Guid.Empty,"",false,null,catalog.Revision);
            Require(!oldKey.Equals(newKey) && oldKey.GetHashCode()!=newKey.GetHashCode(),"material identity actually compares and hashes classifier semantics");
        }
        private static void SiblingLayersAndWorld()
        {
            var catalog=Catalog();var target=Tile(624);target.Layers|=CaptureLayer.Wall|CaptureLayer.Wire|CaptureLayer.Actuator|CaptureLayer.Liquid;
            target.WallType=7;target.Wires=15;target.Actuator=true;target.LiquidAmount=80;target.TilePaint=target.WallPaint=1;target.InvisibleTile=target.FullbrightTile=true;
            var selection=catalog.Select(target);var construction=selection.Apply(target);
            Require(selection.RequiredBits==250 && selection.UnknownBits==0 && construction.Layers==(CaptureLayer.Wall|CaptureLayer.Wire|CaptureLayer.Actuator) &&
                construction.TileType==0 && construction.TilePaint==0 && !construction.InvisibleTile && !construction.FullbrightTile && target.TileType==624,
                "construction view strips only display Tile/appearance/liquid while preserving original geometry and valid sibling layers");
            var bill=new BlueprintMaterialAccumulator();BlueprintMaterialSteps.StaticCell(target,catalog,bill);
            Require(bill.Status==BlueprintMaterialStatus.Exact && bill.Snapshot(1).Total==7 && !bill.Counts.ContainsKey(5114),
                "display foreground contributes neither body nor paint/coating, wall/four wires/actuator/wall paint remain");
            var display=Tile(624);display.TilePaint=1;var zero=new BlueprintMaterialAccumulator();
            BlueprintMaterialSteps.RequiredSteps(display,default(BlueprintWorldMatchSample),catalog,zero);
            Require(zero.Status==BlueprintMaterialStatus.Exact && zero.Counts.Count==0,"confirmed display target is filtered before unreadable-world handling");
            var unreadable=new BlueprintMaterialAccumulator();BlueprintMaterialSteps.RequiredSteps(target,default(BlueprintWorldMatchSample),catalog,unreadable);
            Require(unreadable.Status==BlueprintMaterialStatus.Unresolved,"unreadable valid sibling layers keep their real unknown responsibility");
            var scraper=target; scraper.WallPaint=0;var actual=target;actual.WallPaint=1;
            var scrapeBill=new BlueprintMaterialAccumulator();BlueprintMaterialSteps.RequiredSteps(scraper,new BlueprintWorldMatchSample(true,actual),catalog,scrapeBill,foregroundHandledElsewhere:true);
            Require(scrapeBill.Status==BlueprintMaterialStatus.Unresolved,"display foreground is not an elsewhere-owned restoration permit for native wall scraping");
            var exact=BlueprintWorldMatchComparer.CompareCell(target,new BlueprintWorldMatchSample(true,target),selection);
            Require(exact.TargetBits==250 && exact.MatchBits==250,"display Tile never joins same-cell world Match or movement locks");
            var uncertain=Tile(502);var unknown=BlueprintWorldMatchComparer.CompareCell(uncertain,new BlueprintWorldMatchSample(true,uncertain),catalog.Select(uncertain));
            Require(unknown.UnknownBits==1 && unknown.MatchBits==0 && unknown.TargetCount==1,"same-looking world cannot resolve an unknown producer qualification");
            var ambiguous=Tile(500);var real=BlueprintWorldMatchComparer.CompareCell(ambiguous,new BlueprintWorldMatchSample(true,ambiguous),catalog.Select(ambiguous));
            Require(real.MatchBits==1 && real.UnknownBits==0,"material consumption ambiguity cannot erase an independently exact world proof");
        }
    }
}
