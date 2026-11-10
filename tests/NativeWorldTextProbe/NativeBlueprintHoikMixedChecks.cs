using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static void ConstructionHoikMixed(object context, object host, object tools, object input)
        {
            var p=Reset(context,host,tools,input);p.position.X=42*16;
            var library=(LibraryState)Get(host,"Library");
            var profile=(IBlueprintImportProfile)host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintHorizontalMirrorResolver").GetMethod("Create",F).Invoke(null,null);
            var imported=(BlueprintExchangeReceipt)typeof(NativeBlueprintHelperImportChecks).GetMethod("Import",F).Invoke(null,new object[]{library,profile,
                Path.Combine(Program.Repository,"references/JMR_Blueprint_Hoik_Zoom_20261110_v1/attachments/岛牌刷怪场4.0.json")});
            Require(imported.Status==BlueprintExchangeStatus.Completed,"real supplied JSON enters normal library for the mixed construction subset");
            var listed=(LibraryPage)typeof(NativeBlueprintHelperImportChecks).GetMethod("List",F).Invoke(null,new object[]{library});
            LibraryEntry importedEntry=listed.Entries.FirstOrDefault(e=>e.AssetId==imported.AssetId);
            for(int page=1;importedEntry==null && page<listed.PageCount;page++)
            {
                var receipt=(LibraryReceipt)typeof(NativeBlueprintHelperImportChecks).GetMethod("Command",F).Invoke(null,new object[]{library,library.TryList(10,page)});
                importedEntry=receipt.Page.Entries.FirstOrDefault(e=>e.AssetId==imported.AssetId);
            }
            Require(importedEntry!=null,"actual imported asset located across the existing paged library");
            var source=OpenCapturedAppearance(library,importedEntry);
            var goals=source.Cells.Where(c=>c.TileType==TileID.Platforms && c.Slope>=3).ToArray();
            Require(goals.Length==25 && goals.Count(c=>c.Slope==3)==6 && goals.Count(c=>c.Inactive)==3,"all twenty-five actual imported Hoik tile targets selected");
            var cells=new List<CaptureCell>();var objects=new List<CaptureObject>();
            // The representative rearranges only this selected Tile layer into
            // a moving strip; the complete original asset's all-layer fidelity
            // is independently checked by the standard importer round-trip.
            for(int n=0;n<goals.Length;n++)
            {
                var cell=goals[n];var obj=source.Objects.Single(o=>o.Id==cell.ObjectId);
                cell=new CaptureCell{X=n,Y=0,ObjectId=n+1,Layers=CaptureLayer.Tile,TileType=cell.TileType,FrameX=cell.FrameX,FrameY=cell.FrameY,
                    Slope=cell.Slope,HalfBrick=cell.HalfBrick,Inactive=cell.Inactive,TilePaint=cell.TilePaint,InvisibleTile=cell.InvisibleTile,FullbrightTile=cell.FullbrightTile};
                obj.X=n;obj.Y=0;obj.Id=n+1;cells.Add(cell);objects.Add(obj);
                NativeToolsChecks.Tile(44+n,37,TileID.Stone);
            }
            cells.Add(new CaptureCell{X=25,Layers=CaptureLayer.Wall|CaptureLayer.Wire,WallType=WallID.Stone,Wires=1});
            NativeToolsChecks.Tile(69,37,TileID.Stone);
            Require(WorldGen.PlaceTile(80,42,TileID.Chairs,mute:true,forced:true,plr:0),"original chair setup supplies complete native furniture metadata");
            var chairEntry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,library,()=>Step(context,input),80,41,1,2,"Hoik混合家具");
            var chair=OpenCapturedAppearance(library,chairEntry);
            foreach(var original in chair.Cells){var cell=original;cell.X=26;cell.Y+=3;cell.ObjectId=26;cells.Add(cell);}
            var chairObject=chair.Objects.Single();chairObject.X=26;chairObject.Y=3;chairObject.Id=26;objects.Add(chairObject);
            var summary=new CaptureSummary{Width=27,Height=5,ContentCount=cells.Count};
            var window=new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"hoik-mixed-subset",1,1),1,summary,
                new BlueprintProjectionBounds(0,0,27,5),cells.ToArray(),objects.ToArray());
            var entry=NativeBlueprintProjectionVisualChecks.Save(library,window);
            Call(host,"SetMaterialViewDemand",true);
            Until(context,input,()=>GetOptional(host,"MaterialCatalog")!=null);
            var catalog=(BlueprintPlacementCatalog)Get(host,"MaterialCatalog");
            int platform=catalog.Tile(cells[0],objects[0]).UseItemType;
            p.inventory[12].SetDefaults(ItemID.StoneBlock);p.inventory[12].stack=25;
            p.inventory[13].SetDefaults(platform);p.inventory[13].stack=25;
            p.inventory[14].SetDefaults(ItemID.WoodenHammer);p.inventory[15].SetDefaults(ItemID.Paintbrush);
            p.inventory[17].SetDefaults(ItemID.ActuationRod);p.inventory[18].SetDefaults(ItemID.WireCutter);
            p.inventory[19].SetDefaults(ItemID.Wrench);p.inventory[20].SetDefaults(ItemID.Actuator);p.inventory[20].stack=3;
            p.inventory[21].SetDefaults(ItemID.Wire);p.inventory[21].stack=1;
            p.inventory[22].SetDefaults(ItemID.StoneWall);p.inventory[22].stack=1;
            p.inventory[23].SetDefaults(ItemID.WoodenChair);p.inventory[23].stack=1;
            var placed=Place(context,host,input,entry,44,38);Call(host,"SetMaterialViewDemand",true);SetAutomatic(host,input,true);
            bool right=true;float min=p.position.X,max=min;int frames=0,maxPending=0;bool paused=false,diagnosed=false;
            Action move=()=>
            {
                if(p.position.X>72*16)right=false;else if(p.position.X<42*16)right=true;
                NativeToolExecutionChecks.Sample(context,input,new Vector2(480,540),false);
                p.controlRight=right;p.controlLeft=!right;PlayerInput.Triggers.Current.Right=right;PlayerInput.Triggers.Current.Left=!right;
                long candidates=(long)Get(host,"BuildCandidateChecks"),sources=(long)Get(host,"ProjectionMatchSourceSteps");
                NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");constructionInputTail();frames++;
                min=Math.Min(min,p.position.X);max=Math.Max(max,p.position.X);
                if(frames==100)Require(p.inventory[14].tileBoost<0 && ((int[])Get(host,"nearCapabilityReach"))[1]==p.inventory[14].tileBoost,
                    "present negative-reach hammer retains its actual family bound instead of zero's wider absence fallback");
                if(frames>4000 && !diagnosed && p.IsInTileInteractionRange(49,38,Terraria.DataStructures.TileReachCheckSettings.Simple,p.inventory[14].tileBoost))
                {
                    diagnosed=true;
                    Console.WriteLine("HOIK IN-REACH frame="+frames+" position="+p.position+" automatic="+Get(host,"AutoBuildEnabled")+" can="+Call(host,"CanBuildAutomatically",p,false)+
                        " sourceBudget="+Get(host,"materialBuildSourceBudget")+" planDirty/visit="+Get(host,"nearPlanDirty")+"/"+Get(host,"nearVisitNeeded")+" candidates="+Get(host,"BuildCandidateChecks"));
                    foreach(System.Collections.DictionaryEntry range in (System.Collections.IDictionary)Get(host,"nearBuildRanges"))
                        foreach(System.Collections.DictionaryEntry entryFact in (System.Collections.IDictionary)Get(range.Value,"Facts"))
                        {
                            var fact=entryFact.Value;var seed=Get(fact,"Seed");var cell=(CaptureCell)Get(seed,"Cell");if(cell.X!=5)continue;
                            Console.WriteLine("HOIK REACH-FACT ordinal="+Get(seed,"Ordinal")+" wait="+Get(fact,"Wait")+" ready="+Get(fact,"Ready")+" current="+Call(host,"NearFactCurrent",fact,p)+
                                " playerX/Y="+Get(fact,"PlayerX")+"/"+Get(fact,"PlayerY")+" storedInReach="+Get(fact,"PlayerInReach")+" boost/hammer="+Get(fact,"PlayerBoost")+"/"+Get(fact,"PlayerHammer")+
                                " planGroup="+Get(fact,"PlanGroup"));
                        }
                }
                int pending=((System.Collections.IDictionary)Get(host,"hoikBases")).Count;maxPending=Math.Max(maxPending,pending);
                Require((long)Get(host,"BuildCandidateChecks")-candidates<=64 && (long)Get(host,"ProjectionMatchSourceSteps")-sources<=256,
                    "twenty-five Hoik steps share existing finite near-source/candidate work while moving");
                if(!paused && pending>=2)
                {
                    SetAutomatic(host,input,false);long started=(long)Get(host,"BuildUsesStarted");
                    for(int idle=0;idle<20;idle++)Step(context,input);
                    Require((long)Get(host,"BuildUsesStarted")==started,"disable retains intermediate facts and starts no new world actions");
                    SetAutomatic(host,input,true);paused=true;
                }
            };
            Action<Func<bool>,string> until=(done,phase)=>
            {
                // Twenty-five natural hammer cycles exceed the unrelated
                // eight-second worker helper. Bound actual simulated updates;
                // every one still asserts the unchanged production budget.
                for(int n=0;n<20000;n++)
                {
                    move();if(done())return;if(n%32==0)System.Threading.Thread.Sleep(1);
                    if(n%4000==3999)Console.WriteLine("HOIK MIXED progress phase="+phase+" frames="+frames+" position="+p.position+" min/max="+min+"/"+max+
                        " stone="+p.inventory[12].stack+" platform="+p.inventory[13].stack+" pending="+((System.Collections.IDictionary)Get(host,"hoikBases")).Count+" checks="+Get(host,"BuildNativeChecks"));
                }
                Console.Error.WriteLine("HOIK MIXED phase="+phase+" position="+p.position+" pending="+((System.Collections.IDictionary)Get(host,"hoikBases")).Count+
                    " targets="+String.Join(";",Enumerable.Range(0,25).Select(n=>n+":"+Main.tile[44+n,38].active()+"/"+Main.tile[44+n,38].type+"/"+Main.tile[44+n,38].slope()+"/"+Main.tile[44+n,38].inActive()+"/"+Main.tile[44+n,38].fullbrightBlock())));
                DumpAppearanceCase("hoik mixed "+phase,host,tools);
                var current=GetOptional(host,"activeBuild");
                if(current!=null)Console.Error.WriteLine("HOIK CURRENT target="+Get(Get(current,"Target"),"WorldX")+","+Get(Get(current,"Target"),"WorldY")+
                    " operation="+Get(Get(current,"Operation"),"Kind")+" entered="+Get(current,"Entered")+" checked="+Get(current,"Checked")+" gesture="+Get(current,"InputGesture")+
                    " anim/time/tool/pound="+p.itemAnimation+"/"+p.itemTime+"/"+p.toolTime+"/"+p.poundRelease);
                foreach(System.Collections.DictionaryEntry range in (System.Collections.IDictionary)Get(host,"nearBuildRanges"))
                    foreach(System.Collections.DictionaryEntry entryFact in (System.Collections.IDictionary)Get(range.Value,"Facts"))
                    {
                        var fact=entryFact.Value;var seed=Get(fact,"Seed");var cell=(CaptureCell)Get(seed,"Cell");
                        if(cell.X!=5 && cell.X!=8)continue;
                        var operation=GetOptional(fact,"Operation");
                        Console.Error.WriteLine("HOIK FACT x="+cell.X+" ordinal="+Get(seed,"Ordinal")+" ready/pending="+Get(fact,"Ready")+"/"+Get(fact,"Pending")+
                            " wait="+Get(fact,"Wait")+" type="+Get(fact,"Type")+" current="+Call(host,"NearFactCurrent",fact,p)+" op="+(operation==null?"null":Get(operation,"Kind")));
                    }
                throw new InvalidOperationException("Hoik mixed bounded native updates exhausted: "+phase);
            };
            until(()=>Enumerable.Range(0,25).All(n=>Main.tile[44+n,38].active() && Main.tile[44+n,38].type==TileID.Platforms && Main.tile[44+n,38].slope()==goals[n].Slope) &&
                Main.tile[69,38].wire() && Main.tile[69,38].wall==WallID.Stone && Main.tile[70,41].type==TileID.Chairs,"bodies");
            Require(max-min>20*16 && maxPending>=2 && paused && Scene(host).Length==1 && p.inventory[12].IsAir && p.inventory[13].IsAir,
                "nearby groups continuously build all imported Hoik plus furniture/wall/wire under real movement, while missing coating stays local");
            p.inventory[16].SetDefaults(ItemID.GlowPaint);p.inventory[16].stack=25;
            until(()=>Scene(host).Length==0 && !(bool)Get(Get(tools,"Use"),"Active"),"coating refill");
            Require(Enumerable.Range(0,25).All(n=>{var tile=Main.tile[44+n,38];return tile.slope()==goals[n].Slope && !tile.halfBrick() && tile.fullbrightBlock() && tile.inActive()==goals[n].Inactive && !tile.actuator();}) && p.inventory[16].IsAir,
                "actual coating refill completes all fullbright/inactive layered Hoik with temporary actuator cleanup and final full Match");
            Console.WriteLine("PASS HoikMixed actualImported=25 slope3=6 slope4=19 inactive=3 movement="+(max-min)+" maxPending="+maxPending+" frames="+frames+" nativeMaterials=true sourceBudget=256 candidateBudget=64");
            Call(host,"SetMaterialViewDemand",false);
        }
    }
}
