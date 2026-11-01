using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // The disposable world prepares source/support/manual fixtures only.
    // Automatic destinations are written by production ToolUse and the real
    // Player.Update/ItemCheck consumer, including its natural inventory tail.
    internal static partial class NativeBlueprintBuildChecks
    {
        private static void ConstructionSubstitution(object context, object host, object tools, object input,
            Action<string,Action> check)
        {
            try
            {
                check("substitution locked client catalogue",()=>SubstitutionCatalogue(context,host,tools,input));
                check("substitution mixed stock and shared bill",()=>SubstitutionMixedStock(context,host,tools,input));
                check("substitution allocation changed and stable workload",()=>SubstitutionAllocationCost(context,host,tools,input));
                check("substitution restock and local fairness",()=>SubstitutionRestock(context,host,tools,input));
                check("substitution manual objects appearance and policy",()=>SubstitutionManual(context,host,tools,input));
                check("substitution safe glass transparent and fence native walls",()=>SubstitutionSafeWalls(context,host,tools,input));
                check("substitution complete picnic table pairs and native appearance",()=>SubstitutionPicnicTables(context,host,tools,input));
                check("substitution prepared and consumed policy retirement",()=>SubstitutionSettings(context,host,tools,input));
                check("substitution unsafe exact native consumer",()=>SubstitutionUnsafe(context,host,tools,input));
                check("substitution captured legacy planter native consumer",()=>SubstitutionPlanters(context,host,tools,input));
                check("substitution torch liquid and effective native output",()=>SubstitutionTorches(context,host,tools,input));
                check("substitution captured smart platform direct slopes",()=>SubstitutionSmartPlatforms(context,host,tools,input));
                check("substitution native furniture and open door appearance",()=>SubstitutionDoors(context,host,tools,input));
            }
            finally { Call(host,"SetSameTypeReplacement",false); Call(host,"SetReplacementCategories",4095); }
        }

        private static Player SubstitutionReset(object context,object host,object tools,object input)
        {
            Call(host,"SetSameTypeReplacement",false);
            var p=Reset(context,host,tools,input); p.position.X=42*16;
            Call(host,"SetReplacementCategories",4095); Call(host,"SetSameTypeReplacement",true);
            Require((bool)Get(host,"SameTypeReplacementEnabled"),"the unique loaded preference owner accepts substitution");
            return p;
        }
        private static BlueprintPlacementCatalog SubstitutionCatalog(object context,object host,object input)
        {
            bool demand=(bool)Get(host,"materialViewDemand");Call(host,"SetMaterialViewDemand",true);
            try
            {
                Until(context,input,()=>GetOptional(host,"MaterialCatalog") is BlueprintPlacementCatalog c && c.Substitution.Ready);
                return (BlueprintPlacementCatalog)Get(host,"MaterialCatalog");
            }
            finally { Call(host,"SetMaterialViewDemand",demand); }
        }
        private static Item SubstitutionItem(int type)
        { var item=new Item();item.SetDefaults(type);return item; }
        private static long SubstitutionHeld(Player p,int type)
        { return p.inventory.Take(50).Where(i=>!i.IsAir && i.type==type).Sum(i=>(long)i.stack); }
        private static BlueprintMaterialSnapshot SubstitutionBill(object context,object host,object input)
        {
            Call(host,"SetMaterialViewDemand",true); BlueprintMaterialSnapshot bill=null;
            Until(context,input,()=>{bill=(BlueprintMaterialSnapshot)Call(host,"MaterialWorld",0L);return bill.Status==BlueprintMaterialStatus.Exact && bill.InventoryReady;});
            return bill;
        }
        private static void SubstitutionCatalogue(object context,object host,object tools,object input)
        {
            SubstitutionReset(context,host,tools,input);var catalog=SubstitutionCatalog(context,host,input);
            var policy=new BlueprintSubstitutionPolicy(true);
            foreach(var group in catalog.Substitution.Members.GroupBy(m=>m.Category).OrderBy(g=>g.Key))
                Console.WriteLine("SUBSTITUTION CLIENT category="+group.Key+" variants="+group.Count()+" items="+group.Select(m=>m.Placement.UseItemType).Distinct().Count());
            int[,] positive={
                {ItemID.WorkBench,ItemID.BorealWoodWorkBench},{ItemID.WoodenTable,ItemID.GlassTable},
                {ItemID.WoodenChair,ItemID.GlassChair},{ItemID.WoodenDoor,ItemID.BorealWoodDoor},
                {ItemID.Bed,ItemID.BorealWoodBed},{ItemID.DayBloomPlanterBox,ItemID.MoonglowPlanterBox},
                {ItemID.IronAnvil,ItemID.LeadAnvil},{ItemID.MythrilAnvil,ItemID.OrichalcumAnvil},
                {ItemID.Chest,ItemID.DeadMansChest},{ItemID.Torch,ItemID.CoralTorch},
                {ItemID.WoodPlatform,ItemID.ObsidianPlatform},{ItemID.WoodWall,ItemID.StoneWall},
                {ItemID.GlassWall,ItemID.StoneWall},{ItemID.PurpleStainedGlass,ItemID.StoneWall},
                {ItemID.WoodenFence,ItemID.StoneWall},{ItemID.PicnicTable,ItemID.PicnicTableWithCloth}};
            for(int i=0;i<positive.GetLength(0);i++)
            {
                int a=positive[i,0],b=positive[i,1];
                Require(catalog.Substitution.MaterialCandidates(a,policy).Contains(b) && catalog.Substitution.MaterialCandidates(b,policy).Contains(a),
                    "independent approved category pair is bidirectional: "+a+"/"+b);
            }
            int[,] excluded={{ItemID.WoodenTable,ItemID.DynastyTable},{ItemID.WoodPlatform,ItemID.CloudPlatform},
                {ItemID.WoodPlatform,ItemID.AetheriumPlatform},{ItemID.IronAnvil,ItemID.MythrilAnvil},{ItemID.Chest,ItemID.Safe},
                {ItemID.WoodWall,ItemID.DirtWallUnsafe},{ItemID.WoodenTable,ItemID.PicnicTable},
                {ItemID.WoodenTable,ItemID.PicnicTableWithCloth},{ItemID.PicnicTable,ItemID.DynastyTable}};
            for(int i=0;i<excluded.GetLength(0);i++)
                Require(!catalog.Substitution.MaterialCandidates(excluded[i,0],policy).Contains(excluded[i,1]) &&
                    !catalog.Substitution.MaterialCandidates(excluded[i,1],policy).Contains(excluded[i,0]),"explicit excluded endpoint stays excluded in both directions");
            var dynasty=SubstitutionItem(ItemID.DynastyTable);var cloud=SubstitutionItem(ItemID.CloudPlatform);
            var aether=SubstitutionItem(ItemID.AetheriumPlatform);var dead=SubstitutionItem(ItemID.DeadMansChest);
            var unsafeWall=SubstitutionItem(ItemID.DirtWallUnsafe);var unsafeOther=SubstitutionItem(ItemID.SpiderWallUnsafe);
            var safe=SubstitutionItem(ItemID.WoodWall);
            Require(dynasty.createTile==14 && dynasty.placeStyle==25 && cloud.createTile==19 && cloud.placeStyle==49 &&
                aether.createTile==19 && aether.placeStyle==50 && dead.createTile==467 && dead.placeStyle==4,
                "actual locked client SetDefaults confirms all finite exclusion/Dead Man output keys");
            Require(Main.wallHouse[safe.createWall] && !Main.wallHouse[unsafeWall.createWall] && !Main.wallHouse[unsafeOther.createWall] && unsafeWall.createWall==2,
                "initialized locked client safety values distinguish carryable unsafe from safe");
            // The superseded oracle excluded GlassWall by purpose. These
            // independent initialized facts now certify all three formerly
            // filtered sets without inferring safety from their names.
            int[] safePurposeWalls={ItemID.GlassWall,ItemID.PurpleStainedGlass,ItemID.WoodenFence};
            foreach(int type in safePurposeWalls)
            {
                var wall=SubstitutionItem(type);
                Require(wall.createWall>0 && Main.wallHouse[wall.createWall] && catalog.Wall(wall.createWall)?.UseItemType==type &&
                    catalog.Substitution.AllowsWall(wall.createWall,safe.createWall,policy) &&
                    catalog.Substitution.AllowsWall(safe.createWall,wall.createWall,policy),
                    "real initialized safe glass/transparent/fence wall and reliable producer participate bidirectionally: "+type+"/"+wall.createWall);
            }
            Require(WallID.Sets.Glass[SubstitutionItem(ItemID.GlassWall).createWall] &&
                WallID.Sets.Transparent[SubstitutionItem(ItemID.PurpleStainedGlass).createWall] &&
                WallID.Sets.Fences[SubstitutionItem(ItemID.WoodenFence).createWall],"representatives actually distinguish all three removed purpose filters");
            foreach(int type in new[]{ItemID.PicnicTable,ItemID.PicnicTableWithCloth})
            {
                var picnic=SubstitutionItem(type);var data=TileObjectData.GetTileData(picnic.createTile,picnic.placeStyle);
                var member=catalog.Substitution.Members.Single(m=>m.Placement.UseItemType==type);
                Require(picnic.createTile==TileID.PicnicTable && picnic.placeStyle==(type==ItemID.PicnicTable?0:1) &&
                    data!=null && data.Width==4 && data.Height==2 && data.Origin.X==1 && data.Origin.Y==1 &&
                    data.AnchorBottom.tileCount==4 && member.Category==BlueprintSubstitutionCategory.Table &&
                    member.Placement.Width==4 && member.Placement.Height==2 && member.Placement.Style==picnic.placeStyle &&
                    member.Prototype.Width==4 && member.Prototype.Height==2,
                    "two real distinct picnic outputs keep their native 4x2/origin/support facts in the Table category");
            }
            Require(!catalog.Substitution.Members.Any(m=>m.Placement.UseItemType==ItemID.DynastyTable),
                "Dynasty output remains excluded from every Table structural pool");
            Require(catalog.QualifyWall(unsafeWall.createWall).Disposition==BlueprintTargetDisposition.Construction &&
                catalog.Wall(unsafeWall.createWall)?.UseItemType==ItemID.DirtWallUnsafe,
                "unsafe exact qualification and actual producer survive the additive relation");
            Require(!catalog.Substitution.AllowsWall(safe.createWall,unsafeWall.createWall,policy) &&
                !catalog.Substitution.AllowsWall(unsafeWall.createWall,safe.createWall,policy) &&
                !catalog.Substitution.AllowsWall(unsafeWall.createWall,unsafeOther.createWall,policy),"all unsafe extra relations refuse");
            Require(catalog.Substitution.Members.Select(m=>m.Category).Distinct().Count()==12,"all twelve finite categories have actual native facts");
            Console.WriteLine("SUBSTITUTION CLIENT unsafe item="+unsafeWall.type+" wall="+unsafeWall.createWall+" house="+Main.wallHouse[unsafeWall.createWall]+
                " exactProducer="+catalog.Wall(unsafeWall.createWall).UseItemType+" dynasty="+dynasty.placeStyle+" cloud="+cloud.placeStyle+" aether="+aether.placeStyle+" dead="+dead.createTile+"/"+dead.placeStyle);
        }

        private static void SubstitutionMixedStock(object context,object host,object tools,object input)
        {
            var p=SubstitutionReset(context,host,tools,input);SubstitutionCatalog(context,host,input);
            int[] materials={ItemID.WoodPlatform,ItemID.BonePlatform,ItemID.ObsidianPlatform};
            for(int i=0;i<materials.Length;i++){p.inventory[12+i].SetDefaults(materials[i]);p.inventory[12+i].stack=i==0?1:2;}
            var wood=SubstitutionItem(materials[0]);
            for(int x=44;x<49;x++)WorldGen.PlaceWall(x,42,WallID.Stone);
            var entry=Asset(host,Enumerable.Range(0,5).Select(i=>new CaptureCell{X=i,Layers=CaptureLayer.Tile,TileType=(ushort)wood.createTile,FrameY=(short)(18*wood.placeStyle)}).ToArray());
            Place(context,host,input,entry,44,42);
            var bill=SubstitutionBill(context,host,input);var row=bill.Rows.Single(r=>r.ItemType==materials[0]);
            Require(row.Required==5 && row.Held==1 && row.AllocatedExact==1 && row.SubstitutionAvailable==4 && row.Missing==0,
                "world bill keeps exact Held while one shared allocation explains four replacement units");
            SubstitutionMaterialLayout(context,bill);
            Call(host,"SetMaterialViewDemand",false);
            var position=p.position;long checks=(long)Get(host,"BuildNativeChecks");SetAutomatic(host,input,true);
            Until(context,input,()=>Scene(host).Length==0);
            Require(materials.All(m=>SubstitutionHeld(p,m)==0) && p.position==position && (long)Get(host,"BuildNativeChecks")>=checks+5,
                "one original and two distinct replacement stacks really consume through native checks without player movement");
            int[] styles=Enumerable.Range(44,5).Select(x=>Main.tile[x,42].frameY/18).ToArray();
            Require(styles.Count(s=>s==SubstitutionItem(materials[0]).placeStyle)==1 && styles.Count(s=>s==SubstitutionItem(materials[1]).placeStyle)==2 &&
                styles.Count(s=>s==SubstitutionItem(materials[2]).placeStyle)==2,"actual destination mixes the exact and both approved outputs");
            Until(context,input,()=>!(bool)Get(Get(tools,"Use"),"Active"));
            long uses=(long)Get(host,"BuildUsesStarted"),preflights=(long)Get(host,"SubstitutionCandidateChecks");
            for(int i=0;i<40;i++)Step(context,input);
            Require((long)Get(host,"BuildUsesStarted")==uses && (long)Get(host,"SubstitutionCandidateChecks")==preflights,"retired/no-scene has no replacement discovery work");

            p=SubstitutionReset(context,host,tools,input);p.inventory[14].SetDefaults(ItemID.ObsidianPlatform);p.inventory[14].stack=4;
            var bone=SubstitutionItem(ItemID.BonePlatform);
            var competing=Asset(host,Enumerable.Range(0,6).Select(i=>new CaptureCell{X=i,Layers=CaptureLayer.Tile,TileType=TileID.Platforms,FrameY=(short)((i<3?wood.placeStyle:bone.placeStyle)*18)}).ToArray());
            Place(context,host,input,competing,44,42);bill=SubstitutionBill(context,host,input);
            Require(bill.Rows.Count==2 && bill.Rows.Sum(r=>r.Held)==0 && bill.Rows.Sum(r=>r.SubstitutionAvailable)==4 && bill.Rows.Sum(r=>r.Missing)==2,
                "two nominal rows cannot both count the same four replacement units");
            Call(host,"SetMaterialViewDemand",false);
        }

        private static void SubstitutionRestock(object context,object host,object tools,object input)
        {
            var p=SubstitutionReset(context,host,tools,input);var material=SubstitutionItem(ItemID.WoodPlatform);
            p.inventory[12].SetDefaults(ItemID.StoneBlock);p.inventory[12].stack=1;
            WorldGen.PlaceWall(44,42,WallID.Stone);WorldGen.PlaceWall(46,42,WallID.Stone);
            var entry=Asset(host,new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Platforms,FrameY=(short)(material.placeStyle*18)},
                new CaptureCell{X=2,Layers=CaptureLayer.Tile,TileType=TileID.Stone});
            Place(context,host,input,entry,44,42);SetAutomatic(host,input,true);
            Until(context,input,()=>Main.tile[46,42].active() && Scene(host).Length==1);
            for(int i=0;i<80;i++)Step(context,input);
            long uses=(long)Get(host,"BuildUsesStarted"),plans=(long)Get(host,"SubstitutionCandidateChecks");var position=p.position;
            for(int i=0;i<40;i++)Step(context,input);
            Require(!Main.tile[44,42].active() && (long)Get(host,"BuildUsesStarted")==uses && (long)Get(host,"SubstitutionCandidateChecks")==plans,
                "missing exact/alternative sleeps without empty swings or stable repeated preflight, while later stone already completed");
            p.inventory[13].SetDefaults(ItemID.BonePlatform);p.inventory[13].stack=1;
            Until(context,input,()=>Scene(host).Length==0);
            Require(Main.tile[44,42].active() && Main.tile[44,42].frameY==SubstitutionItem(ItemID.BonePlatform).placeStyle*18 &&
                SubstitutionHeld(p,ItemID.BonePlatform)==0 && p.position==position,"only replacement replenishment wakes and really consumes with stationary player and unchanged support");
        }

        private static void SubstitutionMaterialLayout(object context,BlueprintMaterialSnapshot bill)
        {
            var oldFont=Terraria.GameContent.FontAssets.MouseText;
            try
            {
                FiniteCostChecks.SetCpuFont(10);var shell=Get(context,"Shell");var state=Get(shell,"State");
                var type=shell.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintMaterialsLayout",true);
                var layout=Activator.CreateInstance(type,true);var measureType=type.GetMethod("Prepare",F).GetParameters()[3].ParameterType;
                var f5Layout=Get(state,"Layout");var measure=Delegate.CreateDelegate(measureType,f5Layout,f5Layout.GetType().GetMethod("DynamicTextSize",F));
                Func<int,string> names=id=>"木平台";
                Action<BlueprintMaterialSnapshot> prepare=value=>Call(layout,"Prepare",value,true,320f,measure,names,1L,1,0f);
                prepare(bill);var cell=((IEnumerable)Get(layout,"cells")).Cast<object>().Single();
                Require((string)Get(cell,"HeldText")=="1" && (string)Get(cell,"MissingText")=="0" && (string)Get(cell,"SubstitutionText")=="同类可补 4",
                    "actual published mixed bill reaches the existing three numeric columns and a separate four-unit explanation");
                int generation=(int)Get(layout,"Generation");
                for(int i=0;i<120;i++)prepare(bill);
                Require((int)Get(layout,"Generation")==generation,"stable replacement explanation reuses the actual measured material rows");
                var rowRect=Get(cell,"Rect");
                foreach(var glyph in ((IEnumerable)Get(cell,"Glyphs")).Cast<object>())
                {
                    var rect=Get(Get(glyph,"Element"),"Rect");
                    Require((float)Get(rect,"X")>=0 && (float)Get(rect,"Right")<=(float)Get(rowRect,"Right") &&
                        (float)Get(rect,"Y")>=0 && (float)Get(rect,"Bottom")<=(float)Get(rowRect,"Bottom"),"replacement note and numeric glyphs fit their measured material row");
                }
                prepare(BlueprintMaterialSnapshot.Pending);
                var rectType=type.Assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect",true);
                var clip=Activator.CreateInstance(rectType,F,null,new object[]{0f,0f,320f,1000f},null);
                Call(layout,"Project",0f,0f,clip);
                var drawing=((IEnumerable)Get(layout,"Visible")).Cast<object>().Single();
                var texts=((IEnumerable)Get(drawing,"Glyphs")).Cast<object>().Select(g=>(string)Get(Get(g,"Element"),"Text")).ToArray();
                Require((bool)Get(layout,"HoldingPending") && texts.Count(t=>t=="—")==3 && !texts.Any(t=>t.Contains("同类")),
                    "revoked material publication retains row geometry with unknown quantities and no stale replacement sufficiency claim");
            }
            finally { Terraria.GameContent.FontAssets.MouseText=oldFont; }
        }

        private static int substitutionGraphBuilds,substitutionGraphRows,substitutionGraphSources;
        private static void SubstitutionGraphBoundary(List<int> __0,SortedDictionary<int,long> __2)
        { substitutionGraphBuilds++;substitutionGraphRows=__0.Count;substitutionGraphSources=__2.Count; }
        private static void SubstitutionAllocationCost(object context,object host,object tools,object input)
        {
            var p=SubstitutionReset(context,host,tools,input);SubstitutionCatalog(context,host,input);
            int[] originals={ItemID.WoodPlatform,ItemID.BonePlatform,ItemID.ObsidianPlatform};
            int[] supply={ItemID.WoodPlatform,ItemID.BonePlatform,ItemID.ObsidianPlatform,ItemID.GlassPlatform,ItemID.BorealWoodPlatform,ItemID.SlimePlatform};
            for(int i=0;i<supply.Length;i++){p.inventory[12+i].SetDefaults(supply[i]);p.inventory[12+i].stack=4;}
            var cells=Enumerable.Range(0,24).Select(i=>new CaptureCell{X=i%8,Y=i/8,Layers=CaptureLayer.Tile,TileType=TileID.Platforms,
                FrameY=(short)(SubstitutionItem(originals[i/8]).placeStyle*18)}).ToArray();
            Place(context,host,input,Asset(host,cells),44,39);var bill=SubstitutionBill(context,host,input);
            Require(bill.Rows.Count==3 && bill.Rows.Sum(r=>r.Required)==24 && bill.Rows.Sum(r=>r.Missing)==0,
                "cost fixture is a finite real Host bill: 24 target cells, three original rows and six carried platform types");
            var patch=new HarmonyLib.Harmony("JueMingR.Tests.SubstitutionAllocationCost");
            patch.Patch(typeof(BlueprintMaterialAllocation).GetMethod("Allocate",F),prefix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SubstitutionGraphBoundary)));
            substitutionGraphBuilds=substitutionGraphRows=substitutionGraphSources=0;
            try
            {
                for(int i=0;i<120;i++){Step(context,input);Call(host,"MaterialWorld",0L);}
                Require(substitutionGraphBuilds==0,"120 unchanged real Update/material reads do not reconstruct the shared supply graph");
                long inventoryRevision=((BlueprintMaterialInventory)Get(host,"MaterialInventory")).Revision;long billRevision=bill.Revision;
                p.inventory[17].stack++;
                Until(context,input,()=>{bill=(BlueprintMaterialSnapshot)Call(host,"MaterialWorld",0L);return
                    ((BlueprintMaterialInventory)Get(host,"MaterialInventory")).Revision!=inventoryRevision && bill.Revision!=billRevision && bill.Status==BlueprintMaterialStatus.Exact && bill.InventoryReady;});
                Require(substitutionGraphBuilds==1 && substitutionGraphRows==3 && substitutionGraphSources>=6 && substitutionGraphSources<=50,
                    "one changed frozen inventory publication constructs one whole-scope graph over the actual three rows and qualified main-bag source types");
                int sourceTypes=substitutionGraphSources;
                for(int i=0;i<120;i++){Step(context,input);Call(host,"MaterialWorld",0L);}
                Require(substitutionGraphBuilds==1,"the new exact publication returns to zero graph work on another 120 stable updates");
                Call(host,"SetSameTypeReplacement",false);bill=SubstitutionBill(context,host,input);
                for(int i=0;i<40;i++){Step(context,input);Call(host,"MaterialWorld",0L);}
                Require(substitutionGraphBuilds==1 && bill.Rows.Sum(r=>r.SubstitutionAvailable)==0,"OFF policy performs ordinary exact accounting without a substitution graph");
                Console.WriteLine("SUBSTITUTION ALLOCATION targets=24 rows=3 sourceTypes="+sourceTypes+" stableUpdates=240 stableGraphs=0 changedPublications=1 changedGraphs=1 offGraphs=0; no frame-time/FPS claim");
            }
            finally
            {
                foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyLib.HarmonyPatchType.All,patch.Id);
                Call(host,"SetMaterialViewDemand",false);
            }
        }

        private static void SubstitutionManual(object context,object host,object tools,object input)
        {
            var p=SubstitutionReset(context,host,tools,input);p.direction=-1;
            Require(WorldGen.PlaceTile(60,42,TileID.Chairs,mute:true,forced:true,plr:0,style:0),"native source chair placed");
            WorldGen.paintTile(60,41,1,false,false);WorldGen.paintTile(60,42,1,false,false);
            Require(WorldGen.PlaceTile(63,42,TileID.Stone,mute:true,forced:true,plr:0),"source independent incomplete sentinel");
            var library=(LibraryState)Get(host,"Library");
            var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,library,()=>Step(context,input),60,41,4,2,"同类手放与外观");
            var raw=OpenCapturedAppearance(library,entry);
            Require(raw.Objects.Any(o=>o.Reliable && o.Id>0 && o.Type==TileID.Chairs),"manual fixture comes from actual positive complete Capture object");
            p=SubstitutionReset(context,host,tools,input);p.direction=-1;var glass=SubstitutionItem(ItemID.GlassChair);
            Require(WorldGen.PlaceTile(44,42,glass.createTile,mute:true,forced:true,plr:0,style:glass.placeStyle),"manual destination is a real different chair");
            short manualFrameX=Main.tile[44,42].frameX,manualFrameY=Main.tile[44,42].frameY;
            Place(context,host,input,entry,44,41);var bill=SubstitutionBill(context,host,input);
            Require(!bill.Rows.Any(r=>r.ItemType==ItemID.WoodenChair) && bill.Rows.Any(r=>r.ItemType==ItemID.RedPaint && r.Required==2) && Scene(host).Length==1,
                "accepted replacement body removes chair demand but leaves two real paint requirements and incomplete instance with automatic OFF");
            var before=Get(Scene(host)[0],"Match");Call(host,"SetSameTypeReplacement",false);
            Require(GetOptional(Scene(host)[0],"Match")==null && ((BlueprintMaterialSnapshot)Call(host,"MaterialWorld",0L)).Status==BlueprintMaterialStatus.Pending,
                "effective policy revokes old complete/partial proof and material publication immediately");
            bill=SubstitutionBill(context,host,input);Require(bill.Rows.Any(r=>r.ItemType==ItemID.WoodenChair),"off restores the exact missing body bill without deleting actual chair");
            Call(host,"SetSameTypeReplacement",true);bill=SubstitutionBill(context,host,input);
            Require(!ReferenceEquals(before,Get(Scene(host)[0],"Match")) && !bill.Rows.Any(r=>r.ItemType==ItemID.WoodenChair),"on builds a new bounded proof for already existing replacement");
            WorldGen.paintTile(44,41,1,false,false);WorldGen.paintTile(44,42,1,false,false);
            Require(WorldGen.PlaceTile(47,42,TileID.Stone,mute:true,forced:true,plr:0),"manual sentinel completion");
            Until(context,input,()=>Scene(host).Length==0);
            Require(!(bool)Get(host,"AutoBuildEnabled") && Main.tile[44,42].type==glass.createTile && Main.tile[44,42].frameX==manualFrameX &&
                Main.tile[44,42].frameY==manualFrameY,"manual complete replacement retires independently from automation and preserves its actual decoded native frame");
            Call(host,"SetSameTypeReplacement",false);Call(host,"SetSameTypeReplacement",true);
            for(int i=0;i<20;i++)Step(context,input);
            Require(Scene(host).Length==0 && OpenCapturedAppearance(library,entry).Cells.SequenceEqual(raw.Cells),"policy does not resurrect retired instances or rewrite captured raw cells");
            Call(host,"SetMaterialViewDemand",false);
        }

        private static void SubstitutionUnsafe(object context,object host,object tools,object input)
        {
            int[,] rejected={{ItemID.WoodWall,ItemID.DirtWallUnsafe},{ItemID.DirtWallUnsafe,ItemID.WoodWall},{ItemID.DirtWallUnsafe,ItemID.SpiderWallUnsafe}};
            for(int i=0;i<rejected.GetLength(0);i++)
            {
                var p=SubstitutionReset(context,host,tools,input);var desired=SubstitutionItem(rejected[i,0]);var wrong=SubstitutionItem(rejected[i,1]);
                p.inventory[12].SetDefaults(wrong.type);p.inventory[12].stack=2;
                WorldGen.PlaceWall(44,42,wrong.createWall);
                var entry=Asset(host,new CaptureCell{Layers=CaptureLayer.Wall,WallType=(ushort)desired.createWall},
                    new CaptureCell{X=1,Layers=CaptureLayer.Wall,WallType=(ushort)desired.createWall});
                Place(context,host,input,entry,44,42);var bill=SubstitutionBill(context,host,input);
                var row=bill.Rows.Single(r=>r.ItemType==desired.type);
                Require(row.Required==2 && row.Held==0 && row.SubstitutionAvailable==0 && row.Missing==2,"unsafe endpoint is neither accepted body nor allocated material");
                Call(host,"SetMaterialViewDemand",false);long uses=(long)Get(host,"BuildUsesStarted");SetAutomatic(host,input,true);
                for(int n=0;n<80;n++)Step(context,input);
                Require(Scene(host).Length==1 && Main.tile[44,42].wall==wrong.createWall && Main.tile[45,42].wall==0 &&
                    SubstitutionHeld(p,wrong.type)==2 && (long)Get(host,"BuildUsesStarted")==uses,
                    "three unsafe relation directions reject actual Match, empty-target selection and wrong native consumption");
            }
            foreach(bool enabled in new[]{true,false})
            {
                var p=SubstitutionReset(context,host,tools,input);Call(host,"SetSameTypeReplacement",enabled);
                var unsafeWall=SubstitutionItem(ItemID.DirtWallUnsafe);p.inventory[12].SetDefaults(unsafeWall.type);p.inventory[12].stack=1;
                var entry=Asset(host,new CaptureCell{Layers=CaptureLayer.Wall,WallType=(ushort)unsafeWall.createWall});
                Place(context,host,input,entry,44,42);long checks=(long)Get(host,"BuildNativeChecks");SetAutomatic(host,input,true);
                Until(context,input,()=>Scene(host).Length==0);
                Require(Main.tile[44,42].wall==unsafeWall.createWall && !Main.wallHouse[Main.tile[44,42].wall] &&
                    SubstitutionHeld(p,unsafeWall.type)==0 && (long)Get(host,"BuildNativeChecks")>checks,
                    "real carryable unsafe exact U consumes and places through original ItemCheck with substitution "+enabled);
            }
        }

        private static void SubstitutionPlanters(object context,object host,object tools,object input)
        {
            SubstitutionReset(context,host,tools,input);
            for(int x=60;x<63;x++)
                Require(WorldGen.PlaceTile(x,42,TileID.PlanterBox,mute:true,forced:true,plr:0,style:0),"native source Daybloom planter");
            for(int x=60;x<63;x++)WorldGen.SquareTileFrame(x,42);
            var library=(LibraryState)Get(host,"Library");
            var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,library,()=>Step(context,input),60,42,3,1,"种植盆原版单格");
            var raw=OpenCapturedAppearance(library,entry);
            Require(raw.Objects.Count==0 && raw.Cells.All(c=>c.ObjectId==0 && c.TileType==TileID.PlanterBox && c.FrameY==0) &&
                raw.Cells.Select(c=>c.FrameX).Distinct().Count()>1,"actual Capture retains legacy connection frames without a fabricated metadata object");
            foreach(bool enabled in new[]{true,false})
            {
                var p=SubstitutionReset(context,host,tools,input);Call(host,"SetSameTypeReplacement",enabled);
                int type=enabled?ItemID.MoonglowPlanterBox:ItemID.DayBloomPlanterBox;
                p.inventory[12].SetDefaults(type);p.inventory[12].stack=3;
                for(int x=44;x<47;x++)WorldGen.PlaceWall(x,42,WallID.Stone);
                Place(context,host,input,entry,44,42);long unknown=(long)Get(host,"BuildUnknownResults");
                long checks=(long)Get(host,"BuildNativeChecks"),candidates=(long)Get(host,"SubstitutionCandidateChecks");
                SetAutomatic(host,input,true);Until(context,input,()=>Scene(host).Length==0);
                var item=SubstitutionItem(type);
                Require(Enumerable.Range(44,3).All(x=>Main.tile[x,42].active() && Main.tile[x,42].type==item.createTile && Main.tile[x,42].frameY==18*item.placeStyle &&
                    Main.tile[x,42].slope()==0 && !Main.tile[x,42].halfBrick()) && SubstitutionHeld(p,type)==0 &&
                    (long)Get(host,"BuildNativeChecks")>=checks+3 && (long)Get(host,"BuildUnknownResults")==unknown,
                    "three legacy planters consume through the real Player writer and normalize only connection X; substitution="+enabled);
                if(!enabled)Require((long)Get(host,"SubstitutionCandidateChecks")==candidates,"ordinary exact OFF path performs no replacement candidate preflight");
            }
            Require(OpenCapturedAppearance(library,entry).Cells.SequenceEqual(raw.Cells),"planter replacement preserves captured source bytes");
        }

        private static object substitutionBoundaryHost,substitutionOldFlight;
        private static int substitutionBoundary;
        private static void SubstitutionPreparedBoundary()
        {
            if(substitutionBoundary!=1)return;
            var next=GetOptional(substitutionBoundaryHost,"preparedBuild");if(next==null)return;
            substitutionOldFlight=next;substitutionBoundary=0;
            Call(substitutionBoundaryHost,"SetReplacementCategories",4095 & ~(1<<10));
        }
        private static void SubstitutionConsumedBoundary(object __0)
        {
            if(substitutionBoundary!=2 || !(bool)Get(__0,"Checked") || !(bool)Get(__0,"SettledConsumption"))return;
            substitutionOldFlight=__0;substitutionBoundary=0;Call(substitutionBoundaryHost,"SetSameTypeReplacement",false);
        }
        private static void SubstitutionSettings(object context,object host,object tools,object input)
        {
            var p=SubstitutionReset(context,host,tools,input);var wood=SubstitutionItem(ItemID.WoodPlatform);
            p.inventory[12].SetDefaults(ItemID.BonePlatform);p.inventory[12].stack=2;
            p.inventory[54].SetDefaults(ItemID.BluePaint);p.inventory[54].stack=7;var priority=p.inventory[54];
            for(int x=44;x<=46;x++)WorldGen.PlaceWall(x,42,WallID.Stone);
            var entry=Asset(host,new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Platforms,FrameY=(short)(wood.placeStyle*18)},
                new CaptureCell{X=2,Layers=CaptureLayer.Tile,TileType=TileID.Platforms,FrameY=(short)(wood.placeStyle*18)});
            Place(context,host,input,entry,44,42);Call(host,"SetSameTypeReplacement",false);
            var bill=SubstitutionBill(context,host,input);Require(bill.Rows.Single().Missing==2,"off policy does not allocate two carried alternatives");
            Call(host,"SetMaterialViewDemand",false);long uses=(long)Get(host,"BuildUsesStarted");SetAutomatic(host,input,true);
            for(int i=0;i<80;i++)Step(context,input);
            Require(SubstitutionHeld(p,ItemID.BonePlatform)==2 && (long)Get(host,"BuildUsesStarted")==uses && !Main.tile[44,42].active(),
                "automatic on/substitution off cannot start a replacement use");

            var patch=new HarmonyLib.Harmony("JueMingR.Tests.SubstitutionPolicyBoundary");var self=typeof(NativeBlueprintBuildChecks);
            patch.Patch(host.GetType().GetMethod("AdvanceAutomaticBuild",F),postfix:new HarmonyLib.HarmonyMethod(self,nameof(SubstitutionPreparedBoundary)));
            patch.Patch(host.GetType().GetMethod("CheckedAutomaticBuild",F),postfix:new HarmonyLib.HarmonyMethod(self,nameof(SubstitutionConsumedBoundary)));
            substitutionBoundaryHost=host;
            try
            {
                substitutionBoundary=1;substitutionOldFlight=null;Call(host,"SetSameTypeReplacement",true);
                Until(context,input,()=>substitutionBoundary==0);
                var prepared=substitutionOldFlight;
                Require(prepared!=null && GetOptional(host,"preparedBuild")==null &&
                    !(bool)Call(host,"IsBuildTargetIdentityCurrent",Get(prepared,"Target")),"real prepared concrete target is synchronously retired by a category change");
                for(int i=0;i<40;i++)Step(context,input);
                Require((long)Get(host,"BuildUsesStarted")==uses && SubstitutionHeld(p,ItemID.BonePlatform)==2 &&
                    !Main.tile[44,42].active() && !Main.tile[46,42].active(),"late prepared work cannot enter the native consumer under the changed category");

                substitutionBoundary=2;substitutionOldFlight=null;long unknown=(long)Get(host,"BuildUnknownResults");
                Call(host,"SetReplacementCategories",4095);Until(context,input,()=>substitutionBoundary==0);
                var consumed=substitutionOldFlight;
                Require(consumed!=null && (bool)Get(consumed,"SettledConsumption") && !(bool)Get(host,"SameTypeReplacementEnabled"),
                    "policy command runs after a real settled native debit, inside that flight's unchanged cleanup ownership");
                Until(context,input,()=>!(bool)Get(Get(tools,"Use"),"Active") && GetOptional(host,"activeBuild")==null);
                Require(SubstitutionHeld(p,ItemID.BonePlatform)==1 && new[]{44,46}.Count(x=>Main.tile[x,42].active())==1 &&
                    ReferenceEquals(p.inventory[54],priority) && priority.type==ItemID.BluePaint && priority.stack==7 &&
                    (long)Get(host,"BuildUnknownResults")==unknown,"policy retirement settles the consumed unit once and returns the old 54 without rollback or a fabricated unknown");
                for(int i=0;i<30;i++)Step(context,input);
                Require(SubstitutionHeld(p,ItemID.BonePlatform)==1 && Scene(host).Length==1,"off policy keeps remaining alternative and does not certify the already built different body");
                Call(host,"SetSameTypeReplacement",true);Until(context,input,()=>Scene(host).Length==0 && !(bool)Get(Get(tools,"Use"),"Active"));
                Require(SubstitutionHeld(p,ItemID.BonePlatform)==0 && new[]{44,46}.All(x=>Main.tile[x,42].active() &&
                    Main.tile[x,42].frameY==SubstitutionItem(ItemID.BonePlatform).placeStyle*18) && ReferenceEquals(p.inventory[54],priority),
                    "new policy re-observes the existing alternative and consumes only the remaining missing unit");
                Call(host,"SetReplacementCategories",0);Call(host,"SetSameTypeReplacement",false);Call(host,"SetSameTypeReplacement",true);
                for(int i=0;i<15;i++)Step(context,input);Require(Scene(host).Length==0,"effective policy changes never resurrect the retired sibling");
            }
            finally
            {
                substitutionBoundary=0;substitutionBoundaryHost=substitutionOldFlight=null;
                foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyLib.HarmonyPatchType.All,patch.Id);
            }
        }
    }
}
