using System;
using System.Collections;
using System.Linq;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static void ConstructionHoikRecovery(object context,object host,object tools,object input)
        {
            HoikLivePickRecovery(context);
            foreach(int missing in new[]{ItemID.StoneBlock,ItemID.WoodenHammer,ItemID.CopperPickaxe})
            {
                var p=Reset(context,host,tools,input);p.position.X=42*16;
                p.inventory[12].SetDefaults(ItemID.WoodPlatform);p.inventory[12].stack=2;
                p.inventory[13].SetDefaults(ItemID.StoneBlock);p.inventory[13].stack=2;
                p.inventory[14].SetDefaults(ItemID.WoodenHammer);
                p.inventory[15].SetDefaults(ItemID.StoneWall);p.inventory[15].stack=1;
                foreach(var item in p.inventory)if(item.type==missing)item.TurnToAir();
                NativeToolsChecks.Tile(44,37,TileID.Stone);NativeToolsChecks.Tile(46,37,TileID.Stone);
                var entry=Asset(host,new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Platforms,FrameY=(short)(p.inventory[12].placeStyle*18),Slope=4},
                    new CaptureCell{X=2,Layers=CaptureLayer.Wall,WallType=WallID.Stone});
                Place(context,host,input,entry,44,38);SetAutomatic(host,input,true);
                Until(context,input,()=>Main.tile[46,38].wall==WallID.Stone && !(bool)Get(Get(tools,"Use"),"Active"));
                long uses=(long)Get(host,"BuildUsesStarted");for(int n=0;n<50;n++)Step(context,input);
                Require(!Main.tile[44,38].active() && p.inventory[12].stack==2 && ((IDictionary)Get(host,"hoikBases")).Count==0 && (long)Get(host,"BuildUsesStarted")==uses,
                    "missing Hoik prerequisite starts no partial construction or empty hammer while independent wall completes: "+missing);
                var incoming=new Item();incoming.SetDefaults(missing);incoming.stack=missing==ItemID.StoneBlock?2:1;
                Require(p.GetItem(incoming,new GetItemSettings(NoText:true,NoSound:true)).IsAir,"original pickup restores the missing prerequisite");
                Until(context,input,()=>Scene(host).Length==0 && !(bool)Get(Get(tools,"Use"),"Active"));
                Require(Main.tile[44,38].type==TileID.Platforms && Main.tile[44,38].slope()==4,"stationary native supply/tool recovery resumes exact Hoik without toggling: "+missing);
            }
            {
                var p=Reset(context,host,tools,input);p.position.X=42*16;
                p.inventory[12].SetDefaults(ItemID.WoodPlatform);p.inventory[12].stack=2;p.inventory[13].SetDefaults(ItemID.StoneBlock);p.inventory[13].stack=2;
                p.inventory[14].SetDefaults(ItemID.WoodenHammer);
                var entry=Asset(host,new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Platforms,FrameY=(short)(p.inventory[12].placeStyle*18),Slope=3});
                Place(context,host,input,entry,44,42);Call(host,"SetMaterialViewDemand",true);SetAutomatic(host,input,true);
                Until(context,input,()=>Main.tile[44,42].active() && Main.tile[44,42].type==TileID.Stone && ((IDictionary)Get(host,"hoikBases")).Count==1);
                SetAutomatic(host,input,false);Until(context,input,()=>!(bool)Get(Get(tools,"Use"),"Active"));
                Until(context,input,()=>{var view=(BlueprintMaterialSnapshot)Call(host,"MaterialWorld",0L);return view.Status==BlueprintMaterialStatus.Exact &&
                    !view.Rows.Any(r=>r.ItemType==ItemID.StoneBlock && r.Required>0) && view.Rows.Any(r=>r.ItemType==ItemID.WoodPlatform && r.Required==1);});
                WorldGen.PlaceWall(44,42,WallID.Stone);WorldGen.PlaceWire(44,42);
                Require(((IDictionary)Get(host,"hoikBases")).Count==1,"independent same-cell wall/wire writes preserve foreground provenance");
                WorldGen.KillTile(44,42,false,false,true);Require(WorldGen.PlaceTile(44,42,TileID.Stone,mute:true,forced:true,plr:0),"foreign original removal/replacement creates same-looking stone");
                Require(((IDictionary)Get(host,"hoikBases")).Count==0,"foreign ABA retires ownership even when the last type/shape are equal");
                SetAutomatic(host,input,true);long uses=(long)Get(host,"BuildUsesStarted");for(int n=0;n<80;n++)Step(context,input);
                Require(Main.tile[44,42].type==TileID.Stone && Main.tile[44,42].slope()==0 && (long)Get(host,"BuildUsesStarted")==uses && Scene(host).Length==1,
                    "a formerly owned target never authorizes shaping or replacing a player's same-looking building");
                SetAutomatic(host,input,false);Call(host,"ClearProjection");Call(host,"SetMaterialViewDemand",false);
            }
            Console.WriteLine("PASS HoikRecovery real stone/hammer/pick pickup wakes local negative facts; no incomplete start, independent wall progresses; foreign same-looking stone ABA revokes provenance");
        }
        // This is the final live admission fence, before another inventory
        // publication. A speculative tool must disappear with its actual slot;
        // its recovery serial cannot leak into the next callback or session.
        internal static void HoikLivePickRecovery(object context)
        {
            var host=Get(context,"Blueprint");var p=Main.LocalPlayer;
            foreach(var item in p.inventory)item.TurnToAir();
            Call(host,"ClearNearCapabilityDependencies");
            try
            {
                Call(host,"BeginNearCapabilityObservation",true);Call(host,"PublishNearCapabilityObservation");
                var operationType=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintBuildOperation",true);
                var operation=Activator.CreateInstance(operationType,true);
                operationType.GetField("Kind",F).SetValue(operation,Enum.Parse(operationType.GetField("Kind",F).FieldType,"HoikSwap"));
                operationType.GetField("Hoik",F).SetValue(operation,true);
                operationType.GetField("HoikWaitTool",F).SetValue(operation,2);
                var dependency=Call(host,"GetNearCapabilityDependency",operation,new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Platforms,Slope=3},null,0);
                long cutoff=(long)Call(host,"CurrentNearCapabilitySerial",dependency);
                p.inventory[15].SetDefaults(ItemID.CopperPickaxe);
                Call(host,"BeginNearCapabilityLiveValidation",p,true);
                Require((bool)Call(host,"LiveNearCapabilityRecovery",dependency,cutoff),"a newly present real pick invalidates the old negative dependency at live admission");
                p.inventory[15].TurnToAir();Call(host,"BeginNearCapabilityLiveValidation",p,true);
                Require(!(bool)Call(host,"LiveNearCapabilityRecovery",dependency,cutoff),"removing that unpublished pick removes its speculative recovery at the next live admission");
                // Publish once to cover the corresponding actual session clear.
                p.inventory[15].SetDefaults(ItemID.CopperPickaxe);Call(host,"BeginNearCapabilityObservation",true);
                Call(host,"ObserveNearCapabilityItem",15,p.inventory[15]);Call(host,"PublishNearCapabilityObservation");
                Call(host,"BeginNearCapabilityLiveValidation",p,true);Call(host,"ClearNearCapabilityDependencies");
                Require(((long[])Get(host,"nearCapabilityFamilies"))[5]==0 && ((long[])Get(host,"nearCapabilityLiveFamilies"))[5]==0,
                    "session clear resets the added pick family in both published and live snapshots");
                Console.WriteLine("PASS Hoik live pick present -> removed before publication; both pick-family snapshots clear with session");
            }
            finally {Call(host,"ClearNearCapabilityDependencies");p.inventory[15].TurnToAir();}
        }
    }
}
