using System;
using System.Collections;
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
        private static void ConstructionHoikSubstitution(object context,object host,object tools,object input)
        {
            try
            {
                foreach(byte slope in new byte[]{3,4})
                {
                    var sourcePlayer=Reset(context,host,tools,input);sourcePlayer.position.X=42*16;
                    sourcePlayer.inventory[12].SetDefaults(ItemID.StoneBlock);sourcePlayer.inventory[12].stack=2;
                    sourcePlayer.inventory[13].SetDefaults(ItemID.WoodenHammer);sourcePlayer.inventory[14].SetDefaults(ItemID.WoodPlatform);sourcePlayer.inventory[14].stack=2;
                    HoikManualUntil(context,input,12,44,42,()=>Main.tile[44,42].active());
                    HoikManualUntil(context,input,13,44,42,()=>Main.tile[44,42].slope()==slope && !Main.tile[44,42].halfBrick(),true);
                    HoikManualUntil(context,input,14,44,42,()=>Main.tile[44,42].type==TileID.Platforms);
                    var library=(LibraryState)Get(host,"Library");
                    var captured=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,library,()=>Step(context,input),44,42,1,1,"同类Hoik"+slope);
                    var source=OpenCapturedAppearance(library,captured);
                    Require(source.Cells.Single().ObjectId>0 && source.Objects.Single().Reliable && source.Cells.Single().Slope==slope,"originally manufactured Hoik capture/save/open supplies a positive-object immutable target");
                    var p=SubstitutionReset(context,host,tools,input);var catalog=SubstitutionCatalog(context,host,input);
                    p.inventory[12].SetDefaults(ItemID.ObsidianPlatform);p.inventory[12].stack=3;
                    p.inventory[13].SetDefaults(ItemID.StoneBlock);p.inventory[13].stack=1;p.inventory[14].SetDefaults(ItemID.WoodenHammer);
                    // One case starts on an allowed existing flat platform.
                    // Only setup writes it; all destinations after AutoBuild
                    // are real Player.Update/ItemCheck/BlockSwap effects.
                    if(slope==4)Require(WorldGen.PlaceTile(44,42,TileID.Platforms,mute:true,forced:true,plr:0,style:p.inventory[12].placeStyle),"initial wrong-shape allowed native platform");
                    var adjacent=Asset(host,new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Platforms},new CaptureCell{X=1,Layers=CaptureLayer.Tile,TileType=TileID.Platforms});
                    Place(context,host,input,captured,44,42);Place(context,host,input,adjacent,45,42);SetAutomatic(host,input,true);
                    Until(context,input,()=>Scene(host).Length==0 && !(bool)Get(Get(tools,"Use"),"Active"));
                    Require(Main.tile[44,42].slope()==slope && Enumerable.Range(44,3).All(x=>Main.tile[x,42].type==TileID.Platforms && Main.tile[x,42].frameY==SubstitutionItem(ItemID.ObsidianPlatform).placeStyle*18) &&
                        p.inventory[13].IsAir && !p.inventory.Take(50).Any(i=>!i.IsAir && i.type==ItemID.WoodPlatform) &&
                        (long)Get(host,"BuildUnknownResults")==0,
                        "no original platform: native slope"+slope+" and adjacent ordinary same-material continuation finish policy Match with actual substitute/stone debits");
                    Console.WriteLine("PASS HoikSubstitution slope="+slope+" existingWrongShape="+(slope==4)+" finalStyle="+Main.tile[44,42].frameY/18+" substituteHeld="+SubstitutionHeld(p,ItemID.ObsidianPlatform));
                }
                foreach(int refusal in new[]{0,1,2,3})
                {
                    var p=SubstitutionReset(context,host,tools,input);SubstitutionCatalog(context,host,input);
                    p.inventory[12].SetDefaults(refusal==2?ItemID.CloudPlatform:ItemID.ObsidianPlatform);p.inventory[12].stack=2;
                    p.inventory[13].SetDefaults(ItemID.StoneBlock);p.inventory[13].stack=2;p.inventory[14].SetDefaults(ItemID.WoodenHammer);
                    if(refusal==0)Call(host,"SetSameTypeReplacement",false);
                    if(refusal==1)Call(host,"SetReplacementCategories",4095&~(1<<(int)BlueprintSubstitutionCategory.Platform));
                    if(refusal==3)NativeToolsChecks.Tile(44,42,TileID.Dirt);
                    var entry=Asset(host,new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Platforms,Slope=3});
                    Place(context,host,input,entry,44,42);SetAutomatic(host,input,true);long uses=(long)Get(host,"BuildUsesStarted");
                    for(int i=0;i<180;i++)Step(context,input);
                    Require((long)Get(host,"BuildUsesStarted")==uses && p.inventory[13].stack==2 && p.inventory[12].stack==2 && ((IDictionary)Get(host,"hoikBases")).Count==0 &&
                        (refusal==3?Main.tile[44,42].type==TileID.Dirt:!Main.tile[44,42].active()),"policy OFF/category OFF/excluded cloud/unrelated occupied cell never starts partial Hoik: "+refusal);
                }
                HoikSubstitutionRestock(context,host,tools,input);
                Call(host,"SetSameTypeReplacement",false);
                foreach(byte slope in new byte[]{3,4})ConstructionPlatformBoundary(context,host,tools,input,slope);
            }
            finally {Call(host,"SetSameTypeReplacement",false);Call(host,"SetReplacementCategories",4095);Call(host,"SetMaterialViewDemand",false);}
        }
        private static void HoikSubstitutionRestock(object context,object host,object tools,object input)
        {
            var p=SubstitutionReset(context,host,tools,input);var catalog=SubstitutionCatalog(context,host,input);
            Require(catalog.Substitution.MaterialCandidates(ItemID.WoodPlatform,new BlueprintSubstitutionPolicy(true)).Contains(ItemID.BonePlatform),"existing approved set includes the distinct replenished platform");
            p.inventory[12].SetDefaults(ItemID.ObsidianPlatform);p.inventory[12].stack=2;
            p.inventory[13].SetDefaults(ItemID.StoneBlock);p.inventory[13].stack=6;p.inventory[14].SetDefaults(ItemID.WoodenHammer);
            var goals=Enumerable.Range(0,6).Select(x=>new CaptureCell{X=x*2,Layers=CaptureLayer.Tile,TileType=TileID.Platforms,Slope=(byte)(x%2==0?3:4)}).ToArray();
            foreach(var goal in goals)NativeToolsChecks.Tile(44+goal.X,37,TileID.Stone);
            var entry=Asset(host,goals);Place(context,host,input,entry,44,38);SetAutomatic(host,input,true);
            bool right=true;int frames=0;float min=p.position.X,max=min;
            Action move=()=>{
                if(p.position.X>53*16)right=false;else if(p.position.X<42*16)right=true;
                NativeToolExecutionChecks.Sample(context,input,new Vector2(480,540),false);p.controlRight=right;p.controlLeft=!right;PlayerInput.Triggers.Current.Right=right;PlayerInput.Triggers.Current.Left=!right;
                long candidates=(long)Get(host,"BuildCandidateChecks"),sources=(long)Get(host,"ProjectionMatchSourceSteps");
                NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");constructionInputTail();frames++;
                min=Math.Min(min,p.position.X);max=Math.Max(max,p.position.X);
                Require((long)Get(host,"BuildCandidateChecks")-candidates<=64 && (long)Get(host,"ProjectionMatchSourceSteps")-sources<=256,"substitution Hoik shares unchanged source/candidate budgets under natural movement");
            };
            Action<Func<bool>> until=done=>{for(int n=0;n<12000;n++){move();if(done())return;if(n%32==0)System.Threading.Thread.Sleep(1);}DumpAppearanceCase("Hoik substitution restock",host,tools);
                Console.Error.WriteLine("HOIK-SUB targets="+String.Join(";",goals.Select(c=>(44+c.X)+":"+Main.tile[44+c.X,38].active()+"/"+Main.tile[44+c.X,38].type+"/"+Main.tile[44+c.X,38].frameY+"/"+Main.tile[44+c.X,38].slope()+"/"+Main.tile[44+c.X,38].halfBrick()))+" inv="+String.Join(";",p.inventory.Take(50).Select((i,n)=>n+":"+i.type+"x"+i.stack))+" pending="+((IDictionary)Get(host,"hoikBases")).Count);
                foreach(DictionaryEntry range in (IDictionary)Get(host,"nearBuildRanges"))foreach(DictionaryEntry f in (IDictionary)Get(range.Value,"Facts")){var fact=f.Value;var seed=Get(fact,"Seed");Console.Error.WriteLine("HOIK-SUB fact="+Get(seed,"Ordinal")+" current="+Call(host,"NearFactCurrent",fact,p)+" wait="+Get(fact,"Wait")+" playerBoost="+Get(fact,"PlayerBoost")+" hammer="+Get(fact,"PlayerHammer")+" inReach="+Get(fact,"PlayerInReach")+" supply="+Get(fact,"Supply"));}
                throw new InvalidOperationException("bounded Hoik substitution restock incomplete");};
            until(()=>SubstitutionHeld(p,ItemID.ObsidianPlatform)==0 && goals.Count(c=>Main.tile[44+c.X,38].type==TileID.Platforms && Main.tile[44+c.X,38].slope()==c.Slope)==2 && !(bool)Get(Get(tools,"Use"),"Active"));
            var completed=goals.Where(c=>Main.tile[44+c.X,38].type==TileID.Platforms).ToArray();
            var unstarted=goals.Where(c=>!Main.tile[44+c.X,38].active()).ToArray();long stones=SubstitutionHeld(p,ItemID.StoneBlock);
            for(int n=0;n<100;n++)move();
            // Already-owned stones may legally finish their hammer phase with
            // no platform left. Exhaustion forbids new partial starts, not that
            // existing progress; completed platform consumers must not repeat.
            Require(completed.All(c=>Main.tile[44+c.X,38].type==TileID.Platforms && Main.tile[44+c.X,38].slope()==c.Slope) &&
                unstarted.All(c=>!Main.tile[44+c.X,38].active()) && SubstitutionHeld(p,ItemID.StoneBlock)==stones && Scene(host).Length==1,
                "exhausted allowed platforms preserve completed cells and start no new stone while existing owned phases may progress");
            var refill=SubstitutionItem(ItemID.BonePlatform);refill.stack=4;
            Require(p.GetItem(refill,new GetItemSettings(NoText:true,NoSound:true)).IsAir,"actual native pickup replenishes a different allowed platform");
            until(()=>Scene(host).Length==0 && !(bool)Get(Get(tools,"Use"),"Active"));
            Require(goals.All(c=>Main.tile[44+c.X,38].type==TileID.Platforms && Main.tile[44+c.X,38].slope()==c.Slope) && SubstitutionHeld(p,ItemID.BonePlatform)==0 && p.inventory[13].IsAir && max-min>8*16 && (long)Get(host,"BuildUnknownResults")==0,
                "stationary policy supply dependency wakes allowed restock; natural movement finishes both slopes, exact temporary stones and all current-policy Match");
            Console.WriteLine("PASS HoikSubstitution restock real types="+ItemID.ObsidianPlatform+"->"+ItemID.BonePlatform+" frames="+frames+" movement="+(max-min)+" finalMatch=true nativeBudgets=64/256");
        }
    }
}
