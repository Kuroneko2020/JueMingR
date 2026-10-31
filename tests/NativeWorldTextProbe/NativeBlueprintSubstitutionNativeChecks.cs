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
