using System;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static void SubstitutionSafeWalls(object context,object host,object tools,object input)
        {
            SubstitutionReset(context,host,tools,input);
            int[] originals={ItemID.GlassWall,ItemID.PurpleStainedGlass,ItemID.WoodenFence};
            for(int i=0;i<originals.Length;i++)
            {
                var item=SubstitutionItem(originals[i]);
                Require(Main.wallHouse[item.createWall],"source safety is an initialized original wall fact");
                WorldGen.PlaceWall(60+i,42,item.createWall);WorldGen.paintWall(60+i,42,PaintID.RedPaint,false,false);
            }
            var library=(LibraryState)Get(host,"Library");
            var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,library,()=>Step(context,input),60,42,3,1,"安全玻璃透明栅栏墙");
            var raw=OpenCapturedAppearance(library,entry);
            Require(raw.Objects.Count==0 && raw.Cells.Count==3 && raw.Cells.All(c=>c.Layers==CaptureLayer.Wall && c.WallPaint==PaintID.RedPaint) &&
                Enumerable.Range(0,3).All(i=>raw.Cells[i].WallType==SubstitutionItem(originals[i]).createWall),
                "capture/save/open retains three distinct safe wall outputs and their independent paint requirement");
            foreach(bool manual in new[]{true,false})
            {
                var p=SubstitutionReset(context,host,tools,input);var stone=SubstitutionItem(ItemID.StoneWall);
                if(manual)for(int i=0;i<3;i++)WorldGen.PlaceWall(44+i,42,stone.createWall);
                else
                {
                    p.inventory[12].SetDefaults(ItemID.StoneWall);p.inventory[12].stack=3;
                    p.inventory[14].SetDefaults(ItemID.PaintRoller);p.inventory[15].SetDefaults(ItemID.RedPaint);p.inventory[15].stack=3;
                }
                Place(context,host,input,entry,44,42);var bill=SubstitutionBill(context,host,input);
                Require(bill.Rows.Single(r=>r.ItemType==ItemID.RedPaint).Required==3 && Scene(host).Length==1,
                    "safe replacement never erases the three remaining wall-paint requirements");
                if(manual)
                    Require(!bill.Rows.Any(r=>originals.Contains(r.ItemType)) && !(bool)Get(host,"AutoBuildEnabled"),
                        "automatic OFF accepts manually placed stone as all three bodies without charging another wall");
                else foreach(int original in originals)
                {
                    var row=bill.Rows.Single(r=>r.ItemType==original);
                    Require(row.Required==1 && row.Held==0 && row.SubstitutionAvailable==1 && row.Missing==0,
                        "glass/transparent/fence nominal rows share three actual stone units while Held remains exact");
                }
                Call(host,"SetMaterialViewDemand",false);
                long unknown=(long)Get(host,"BuildUnknownResults"),checks=(long)Get(host,"BuildNativeChecks"),uses=(long)Get(host,"BuildUsesStarted");
                if(manual)
                {
                    for(int i=0;i<3;i++)WorldGen.paintWall(44+i,42,PaintID.RedPaint,false,false);
                    Until(context,input,()=>Scene(host).Length==0);
                    Require(!(bool)Get(host,"AutoBuildEnabled") && (long)Get(host,"BuildUsesStarted")==uses,
                        "manual safe-wall appearance completes and retires while automation stays OFF");
                }
                else
                {
                    SetAutomatic(host,input,true);Until(context,input,()=>Scene(host).Length==0 && !(bool)Get(Get(tools,"Use"),"Active"));
                    Require(SubstitutionHeld(p,ItemID.StoneWall)==0 && SubstitutionHeld(p,ItemID.RedPaint)==0 &&
                        (long)Get(host,"BuildNativeChecks")>checks && p.inventory[14].type==ItemID.PaintRoller,
                        "only stone stock supplies the glass target and its siblings through actual placement and paint consumers");
                }
                Require(Enumerable.Range(44,3).All(x=>Main.tile[x,42].wall==stone.createWall && Main.tile[x,42].wallColor()==PaintID.RedPaint) &&
                    (long)Get(host,"BuildUnknownResults")==unknown,"final safe-wall Match preserves the selected stone output, full appearance and known consumption");
            }
            Require(OpenCapturedAppearance(library,entry).Cells.SequenceEqual(raw.Cells),"safe-wall replacement never rewrites captured original requirements");
        }

        private static void SubstitutionPicnicTables(object context,object host,object tools,object input)
        {
            foreach(int original in new[]{ItemID.PicnicTable,ItemID.PicnicTableWithCloth})
            {
                SubstitutionReset(context,host,tools,input);var desired=SubstitutionItem(original);
                int alternative=original==ItemID.PicnicTable?ItemID.PicnicTableWithCloth:ItemID.PicnicTable;
                Require(WorldGen.PlaceObject(61,42,desired.createTile,mute:true,style:desired.placeStyle),"real source picnic table uses its original style and 4x2 anchor");
                for(int y=41;y<=42;y++)for(int x=60;x<64;x++)WorldGen.paintTile(x,y,PaintID.RedPaint,false,false);
                var library=(LibraryState)Get(host,"Library");
                var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,library,()=>Step(context,input),60,41,4,2,"双向完整野餐桌 "+original);
                var raw=OpenCapturedAppearance(library,entry);var obj=raw.Objects.Single();
                Require(obj.Reliable && obj.Id>0 && obj.Type==TileID.PicnicTable && obj.Width==4 && obj.Height==2 && obj.Style==desired.placeStyle &&
                    raw.Cells.Count==8 && raw.Cells.All(c=>c.ObjectId==obj.Id && c.TilePaint==PaintID.RedPaint) &&
                    raw.Cells.Select(c=>c.FrameX).Distinct().Count()==4 && raw.Cells.Select(c=>c.FrameY).Distinct().Count()==2,
                    "both original styles are separately captured/saved/read as eight real distinct members");
                foreach(bool manual in new[]{true,false})
                {
                    var p=SubstitutionReset(context,host,tools,input);var actual=SubstitutionItem(alternative);
                    var catalog=SubstitutionCatalog(context,host,input);
                    if(manual)Require(WorldGen.PlaceObject(45,42,actual.createTile,mute:true,style:actual.placeStyle),"manual other-style picnic table uses real eight-member native placement");
                    else
                    {
                        p.inventory[12].SetDefaults(alternative);p.inventory[12].stack=1;
                        p.inventory[14].SetDefaults(ItemID.Paintbrush);p.inventory[15].SetDefaults(ItemID.RedPaint);p.inventory[15].stack=8;
                    }
                    Place(context,host,input,entry,44,41);var bill=SubstitutionBill(context,host,input);
                    Require(bill.Rows.Single(r=>r.ItemType==ItemID.RedPaint).Required==8 && Scene(host).Length==1,
                        "all eight source appearance requirements remain before completion for each picnic direction");
                    long checks=(long)Get(host,"BuildNativeChecks"),unknown=(long)Get(host,"BuildUnknownResults"),uses=(long)Get(host,"BuildUsesStarted");
                    if(manual)
                    {
                        Require(!bill.Rows.Any(r=>r.ItemType==original || r.ItemType==alternative) && !(bool)Get(host,"AutoBuildEnabled"),
                            "manual alternate removes only picnic body demand with automatic OFF");
                        SubstitutionPicnicProof(host,raw,catalog,false);
                        p.inventory[12].SetDefaults(original);p.inventory[12].stack=2;
                        bill=SubstitutionBill(context,host,input);
                        Require(!bill.Rows.Any(r=>r.ItemType==original),"later exact stock does not invalidate an already accepted other-style picnic body");
                        for(int y=41;y<=42;y++)for(int x=44;x<48;x++)WorldGen.paintTile(x,y,PaintID.RedPaint,false,false);
                        Until(context,input,()=>Scene(host).Length==0);
                        Require(!(bool)Get(host,"AutoBuildEnabled") && SubstitutionHeld(p,original)==2 && (long)Get(host,"BuildUsesStarted")==uses,
                            "manual complete alternate retires without consuming later exact stock or starting automation");
                    }
                    else
                    {
                        var row=bill.Rows.Single(r=>r.ItemType==original);
                        Require(row.Required==1 && row.Held==0 && row.SubstitutionAvailable==1 && row.Missing==0,
                            "one real alternative table, rather than eight fake items, supplies the missing original body");
                        Call(host,"SetMaterialViewDemand",false);SetAutomatic(host,input,true);
                        Until(context,input,()=>Scene(host).Length==0 && !(bool)Get(Get(tools,"Use"),"Active"));
                        Require(SubstitutionHeld(p,alternative)==0 && SubstitutionHeld(p,original)==0 && SubstitutionHeld(p,ItemID.RedPaint)==0 &&
                            p.inventory[14].type==ItemID.Paintbrush && (long)Get(host,"BuildNativeChecks")>checks,
                            "actual chosen alternative is consumed once and native postprocessing paints all eight members before retirement");
                    }
                    SubstitutionPicnicProof(host,raw,catalog,true);
                    Require((long)Get(host,"BuildUnknownResults")==unknown,"complete picnic substitution never converts precise native consumption into an unknown result");
                    Call(host,"SetMaterialViewDemand",false);
                }
                var reopened=OpenCapturedAppearance(library,entry);
                Require(reopened.Cells.SequenceEqual(raw.Cells) && reopened.Objects.SequenceEqual(raw.Objects),"both picnic directions preserve source cells, frames and complete object metadata");
            }
        }

        private static void SubstitutionPicnicProof(object host,BlueprintProjectionWindow raw,BlueprintPlacementCatalog catalog,bool painted)
        {
            var type=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldMatchSource",true);
            var reader=Activator.CreateInstance(type,F,null,new object[]{(Func<bool>)(()=>true)},null);
            var members=raw.Cells.ToArray();object[] describe={raw.Objects.Single(),members[0],44,41,null};
            Require((bool)type.GetMethod("TryDescribeTarget",F).Invoke(reader,describe),"saved picnic target decodes through the production canonical reader");
            var target=(BlueprintWorldObject)describe[4];var world=new BlueprintWorldMatchSample[8];
            for(int i=0;i<world.Length;i++)
            {
                object[] read={44+i%4,41+i/4,null};
                Require((bool)type.GetMethod("TryRead",F).Invoke(reader,read),"each current picnic member is read independently through the native world gate");
                world[i]=(BlueprintWorldMatchSample)read[2];
            }
            var actual=world[0].Object;var policy=new BlueprintSubstitutionPolicy(true);
            Require(actual!=null && actual.Width==4 && actual.Height==2 && !target.SamePlacement(actual) &&
                catalog.Substitution.AllowsObject(target,actual,policy) && catalog.Substitution.AllowsObject(actual,target,policy) &&
                world.All(s=>s.Object!=null && actual.SameObservedObject(s.Object)) &&
                BlueprintMaterialSteps.SameBody(target,members,world,true,catalog,policy)==BlueprintBodyState.Same &&
                BlueprintWorldMatchComparer.CompareObject(target,members,world,true,catalog,policy)==(painted?BlueprintWorldMatchState.Match:BlueprintWorldMatchState.Conflict),
                "eight independently read members prove a different native style, shared body and separate appearance state");
            var duplicate=(BlueprintWorldMatchSample[])world.Clone();duplicate[7]=duplicate[0];
            Require(BlueprintWorldMatchComparer.CompareObject(target,members,duplicate,true,catalog,policy)!=BlueprintWorldMatchState.Match &&
                BlueprintMaterialSteps.SameBody(target,members,duplicate,true,catalog,policy)!=BlueprintBodyState.Same,
                "duplicated picnic members cannot masquerade as a complete eight-cell object");
        }

        private static void SubstitutionTorches(object context,object host,object tools,object input)
        {
            var p=SubstitutionReset(context,host,tools,input);
            Require(WorldGen.PlaceTile(60,42,TileID.Torches,mute:true,forced:true,plr:0,style:0),"water torch source is an original ordinary torch");
            Require(WorldGen.PlaceTile(62,42,TileID.Stone,mute:true,forced:true,plr:0),"independent target beside torch");
            var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>Step(context,input),60,42,3,1,"原版液体火把候选");
            p=SubstitutionReset(context,host,tools,input);p.inventory[12].SetDefaults(ItemID.Torch);p.inventory[12].stack=2;
            p.inventory[13].SetDefaults(ItemID.CoralTorch);p.inventory[13].stack=1;p.inventory[14].SetDefaults(ItemID.StoneBlock);p.inventory[14].stack=1;
            var coral=SubstitutionItem(ItemID.CoralTorch);Main.tile[44,42].liquid=200;
            Place(context,host,input,entry,44,42);long uses=(long)Get(host,"BuildUsesStarted"),unknown=(long)Get(host,"BuildUnknownResults");
            SetAutomatic(host,input,true);Until(context,input,()=>Scene(host).Length==0);
            Require(Main.tile[44,42].active() && Main.tile[44,42].type==TileID.Torches && Main.tile[44,42].frameY==coral.placeStyle*22 &&
                SubstitutionHeld(p,ItemID.Torch)==2 && SubstitutionHeld(p,ItemID.CoralTorch)==0 && SubstitutionHeld(p,ItemID.StoneBlock)==0 &&
                (long)Get(host,"BuildUsesStarted")==uses+2 && (long)Get(host,"BuildUnknownResults")==unknown,
                "unplaceable exact torch is skipped before animation; legal coral output and independent stone consume exactly once");

            p=SubstitutionReset(context,host,tools,input);p.inventory[12].SetDefaults(ItemID.BlueTorch);p.inventory[12].stack=1;
            p.inventory[14].SetDefaults(ItemID.StoneBlock);p.inventory[14].stack=1;
            PreflightLiquidStore(44,42,200,0);Place(context,host,input,entry,44,42);
            uses=(long)Get(host,"BuildUsesStarted");unknown=(long)Get(host,"BuildUnknownResults");SetAutomatic(host,input,true);
            Until(context,input,()=>Main.tile[46,42].active());
            for(int i=0;i<80;i++)Step(context,input);
            Require(Scene(host).Length==1 && !Main.tile[44,42].active() && SubstitutionHeld(p,ItemID.BlueTorch)==1 &&
                (long)Get(host,"BuildUsesStarted")==uses+1,"an only stocked but genuinely wet-illegal replacement waits without an empty swing while the later stone finishes");
            long attempts=(long)Get(host,"SubstitutionCandidateChecks");
            for(int i=0;i<20;i++)Step(context,input);
            Require((long)Get(host,"SubstitutionCandidateChecks")-attempts<20 && (long)Get(host,"BuildUsesStarted")==uses+1,
                "stable native refusal does not rescan its replacement every frame or repeatedly start native uses");
            PreflightLiquidStore(44,42,0,0);Until(context,input,()=>Scene(host).Length==0);
            Require(Main.tile[44,42].active() && Main.tile[44,42].frameY==SubstitutionItem(ItemID.BlueTorch).placeStyle*22 &&
                SubstitutionHeld(p,ItemID.BlueTorch)==0 && (long)Get(host,"BuildUsesStarted")==uses+2 && (long)Get(host,"BuildUnknownResults")==unknown,
                "the real original liquid packet restores that same replacement's eligibility and bounded waiting resumes actual consumption without movement or toggles");

            using(NativeBlueprintWorldMatchChecks.EmptyWorld(host,200,400))
            {
                p=SubstitutionReset(context,host,tools,input);bool favor=p.unlockedBiomeTorches;int toggle=p.builderAccStatus[11];
                try
                {
                    Require(WorldGen.PlaceTile(60,42,TileID.Torches,mute:true,forced:true,plr:0,style:0),"conversion source is the captured ordinary output");
                    entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>Step(context,input),60,42,1,1,"原版群落转换火把");
                    p.inventory[12].SetDefaults(ItemID.Torch);p.inventory[12].stack=2;p.unlockedBiomeTorches=true;p.UsingBiomeTorches=true;
                    for(int x=10;x<91;x++)for(int y=75;y<101;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=TileID.SnowBlock;}
                    Main.PlayerSceneMetrics.Reset();p.UpdateSceneMetrics();p.UpdateBiomes();Require(p.ZoneSnow,"real native SceneMetrics derive snow, no direct biome flag write");
                    int type=p.inventory[12].createTile,style=p.inventory[12].placeStyle;p.BiomeTorchPlaceStyle(ref type,ref style);
                    Require(style!=p.inventory[12].placeStyle && type==TileID.Torches,"locked original helper derives a different exact output");
                    Place(context,host,input,entry,44,42);unknown=(long)Get(host,"BuildUnknownResults");SetAutomatic(host,input,true);
                    Until(context,input,()=>Scene(host).Length==0);
                    Require(Main.tile[44,42].active() && Main.tile[44,42].type==type && Main.tile[44,42].frameY==style*22 &&
                        SubstitutionHeld(p,ItemID.Torch)==1 && (long)Get(host,"BuildUnknownResults")==unknown,
                        "natural writer uses derived snow output while exact original torch source/slot debit stays owned");
                }
                finally { Call(host,"SetAutoBuild",false);Call(host,"ClearProjection");p.unlockedBiomeTorches=favor;p.builderAccStatus[11]=toggle;Main.PlayerSceneMetrics.Reset();p.UpdateSceneMetrics();p.UpdateBiomes(); }
            }
        }

        private static void SubstitutionSmartPlatforms(object context,object host,object tools,object input)
        {
            var p=SubstitutionReset(context,host,tools,input);var wood=SubstitutionItem(ItemID.WoodPlatform);
            for(int i=0;i<3;i++){SeedSmartPlatform(wood,60+i,39+i,1);}
            var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>Step(context,input),60,39,3,3,"正式同类智能斜平台");
            var saved=OpenCapturedAppearance((LibraryState)Get(host,"Library"),entry);
            Require(saved.Objects.Count==3 && saved.Cells.All(c=>c.ObjectId>0) && saved.Cells.All(c=>c.Slope==1),"Smart replacement uses actual captured positive ObjectIds and raw slopes");
            p=SubstitutionReset(context,host,tools,input);p.inventory[12].SetDefaults(ItemID.ObsidianPlatform);p.inventory[12].stack=3;
            var actual=SubstitutionItem(ItemID.ObsidianPlatform);SeedSmartPlatform(actual,43,38,1);
            var patch=new Harmony("JueMingR.Tests.SubstitutionSmartPlatform");var self=typeof(NativeBlueprintBuildChecks);
            patch.Patch(typeof(Terraria.GameContent.SmartCursorHelper).GetMethod("SmartCursorLookup",F),prefix:new HarmonyMethod(self,nameof(SmartPlatformLookupBefore)),postfix:new HarmonyMethod(self,nameof(SmartPlatformLookupAfter)));
            patch.Patch(host.GetType().GetMethod("NativeBuildChoice",F),postfix:new HarmonyMethod(self,nameof(SmartPlatformChoiceAfter)));
            patch.Patch(host.GetType().GetMethod("NativeAppearanceConsumer",F),postfix:new HarmonyMethod(self,nameof(SmartPlatformHammerAfter)));
            bool oldMouse=Main.SmartCursorWanted_Mouse,oldPad=Main.SmartCursorWanted_GamePad;smartPlatformHost=host;smartPlatformInput=input;
            try
            {
                Place(context,host,input,entry,44,39);ResetSmartPlatformCounts();smartPlatformWatch=true;SetAutomatic(host,input,true);
                Until(context,input,()=>Scene(host).Length==0 && !(bool)Get(Get(tools,"Use"),"Active"));smartPlatformWatch=false;
                Require(Enumerable.Range(0,3).All(i=>Main.tile[44+i,39+i].active() && Main.tile[44+i,39+i].frameY==actual.placeStyle*18 && Main.tile[44+i,39+i].slope()==1) &&
                    SubstitutionHeld(p,ItemID.ObsidianPlatform)==0 && smartPlatformChoices==3 && smartPlatformHammers==0 && smartPlatformPreciseChoices==0 && smartPlatformProofValid,
                    "captured Wood targets directly become three legal Obsidian slopes through real Smart Lookup/PlaceIt without hammer fallback");
                Console.WriteLine("SUBSTITUTION SMART placed="+smartPlatformChoices+" hammer="+smartPlatformHammers+" precise="+smartPlatformPreciseChoices+" nativeLookups="+smartPlatformLookups.Count);
            }
            finally
            {
                smartPlatformWatch=false;Call(host,"SetAutoBuild",false);Main.SmartCursorWanted_Mouse=oldMouse;Main.SmartCursorWanted_GamePad=oldPad;
                foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);
                smartPlatformHost=smartPlatformInput=null;smartPlatformLookups.Clear();smartPlatformMissingLookups.Clear();
            }
        }

        private static void SubstitutionDoors(object context,object host,object tools,object input)
        {
            SubstitutionReset(context,host,tools,input);var entry=DoorRepairCapture(context,host,input,0,1,1);
            foreach(bool existing in new[]{false,true})
            {
                var p=SubstitutionReset(context,host,tools,input);var boreal=SubstitutionItem(ItemID.BorealWoodDoor);
                WorldGen.PlaceWall(44,40,WallID.Stone);WorldGen.PlaceWall(46,40,WallID.Stone);
                Require(WorldGen.PlaceTile(45,39,TileID.Stone,mute:true,forced:true,plr:0),"actual destination upper door anchor");
                p.inventory[14].SetDefaults(ItemID.Paintbrush);p.inventory[15].SetDefaults(ItemID.RedPaint);p.inventory[15].stack=existing?6:3;
                if(existing)
                {
                    Require(WorldGen.PlaceObject(45,40,boreal.createTile,mute:true,style:boreal.placeStyle) && WorldGen.OpenDoor(45,40,1),"manually placed actual Boreal door opens to the right");
                }
                else {p.inventory[12].SetDefaults(ItemID.BorealWoodDoor);p.inventory[12].stack=1;}
                Place(context,host,input,entry,44,40);var bill=SubstitutionBill(context,host,input);
                Require(bill.Rows.Where(r=>r.ItemType==ItemID.RedPaint).Sum(r=>r.Required)==(existing?6:3),
                    "closed source retains three future paint steps for missing body or maps six actual open-door members for an accepted existing body");
                Call(host,"SetMaterialViewDemand",false);long unknown=(long)Get(host,"BuildUnknownResults");SetAutomatic(host,input,true);
                Until(context,input,()=>Scene(host).Length==0);
                Require(SubstitutionHeld(p,ItemID.BorealWoodDoor)==0 && SubstitutionHeld(p,ItemID.RedPaint)==0 &&
                    Enumerable.Range(0,3).All(y=>Main.tile[45,40+y].color()==1 && (!existing || Main.tile[46,40+y].color()==1)) &&
                    (long)Get(host,"BuildUnknownResults")==unknown,"actual replacement body and complete current door appearance consume exact sources and fully retire; existing="+existing);
            }
        }
    }
}
