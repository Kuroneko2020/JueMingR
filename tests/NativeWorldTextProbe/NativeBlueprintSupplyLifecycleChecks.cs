using System;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static long lifecycleToken;
        private static bool lifecycleCompleted, lifecycleCompletedReleased;
        private static int lifecycleCursorStarts;
        private static bool lifecycleKillInside, lifecycleInsideSeen;
        private static object lifecycleInsideTools, lifecycleInsidePreparation;
        private static int lifecyclePaintCalls;
        private static string lifecyclePaintState;
        private static void LifecycleActualUse(Player __instance,Item __0)
        {if(ReferenceEquals(__instance,Main.LocalPlayer) && ReferenceEquals(__0,__instance.inventory[58]))lifecycleCursorStarts++;}
        private static void LifecycleCompleted(object __0)
        {
            if(__0==null || (long)Get(__0,"Token")!=lifecycleToken)return;
            lifecycleCompleted=true;
            lifecycleCompletedReleased=!PlayerInput.Triggers.Current.MouseLeft && !PlayerInput.Triggers.Current.MouseRight &&
                !PlayerInput.Triggers.Current.SmartSelect && !PlayerInput.Triggers.Current.Throw;
        }
        private static void LifecycleInsidePaint(Player __instance,Item __4)
        {
            if(lifecycleKillInside)
            {
                lifecyclePaintCalls++;lifecyclePaintState="samePlayer="+ReferenceEquals(__instance,Main.LocalPlayer)+" paint="+__4?.type+
                    " material="+((Item)Get(lifecycleInsidePreparation,"Material"))?.type+" sameMaterial="+ReferenceEquals(__4,Get(lifecycleInsidePreparation,"Material"));
            }
            if(!lifecycleKillInside || !ReferenceEquals(__instance,Main.LocalPlayer) ||
                !ReferenceEquals(__4,Get(lifecycleInsidePreparation,"Material")))return;
            lifecycleKillInside=false;
            Require((bool)Get(Get(lifecycleInsideTools,"Use"),"InNativeUse"),"death stimulus is inside the actual ApplyPaint/ItemCheck consumer");
            LifecycleNativeDeath(__instance,()=>{
                Require((bool)Get(lifecycleInsidePreparation,"Abandoning") && (bool)Get(lifecycleInsidePreparation,"HasWork") &&
                    (bool)Get(Get(lifecycleInsideTools,"Use"),"InNativeUse"),"known death receipt stays pending until this original native callback actually exits");
                lifecycleInsideSeen=true;
            },false);
        }
        private static void ConstructionSupplyLifecycle(object context,object host,object tools,object input)
        {
            var patch=new Harmony("JueMingR.Tests.SupplyLifecycle");
            patch.Patch(AccessTools.Method(typeof(Player),"ItemCheck_StartActualUse",new[]{typeof(Item)}),
                prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(LifecycleActualUse)));
            patch.Patch(host.GetType().GetMethod("EndAutomaticBuild",F),
                prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(LifecycleCompleted)));
            patch.Patch(AccessTools.Method(typeof(Player),"ApplyPaint",new[]{typeof(int),typeof(int),typeof(bool),typeof(bool),typeof(Item)}),
                postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(LifecycleInsidePaint)));
            try
            {
                foreach(bool returning in new[]{false,true})
                    foreach(string command in new[]{"throw","right","left"})
                        SupplyCursorConsumer(context,host,tools,input,returning,false,command);
                SupplyCursorConsumer(context,host,tools,input,true,true,"right");
                foreach(string command in new[]{"left","smart","selection"})SupplyUsingManualCause(context,host,tools,input,command);
                SupplyDeathBoundary(context,host,tools,input,"Ready",true);
                SupplyDeathBoundary(context,host,tools,input,"Returning",false);
                SupplyDeathBoundary(context,host,tools,input,"Preparing",false);
                SupplyDeathBoundary(context,host,tools,input,"Using",false);
                SupplyDeathInNativeConsumer(context,host,tools,input);
            }
            finally
            {
                lifecycleToken=0;lifecycleKillInside=false;lifecycleInsideTools=lifecycleInsidePreparation=null;
                foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);
            }
        }
        private static BlueprintProjectionIdentity LifecycleIdentity(object tools)
        {return new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"native-supply-lifecycle",(long)Get(Get(tools,"Runtime"),"Generation"),1);}
        private static int LifecycleQuantity(Player p,int type)
        {return p.inventory.Take(58).Where(i=>i.type==type).Sum(i=>i.stack)+(Main.mouseItem.type==type?Main.mouseItem.stack:0);}
        private static void LifecycleInput(object context,object input,string command)
        {
            NativeToolExecutionChecks.Sample(context,input,new Vector2(44*16+8,42*16+8),command=="left");
            if(command=="right")
            {
                var old=PlayerInput.MouseInfo;
                PlayerInput.MouseInfo=new MouseState(old.X,old.Y,old.ScrollWheelValue,ButtonState.Released,ButtonState.Pressed,
                    ButtonState.Released,ButtonState.Released,ButtonState.Released);
                PlayerInput.Triggers.Current.MouseRight=true;Main.mouseRight=true;Main.mouseRightRelease=true;
            }
            if(command=="throw"){PlayerInput.Triggers.Current.Throw=true;Main.LocalPlayer.controlThrow=true;Main.LocalPlayer.releaseThrow=true;}
            if(command=="smart")PlayerInput.Triggers.Current.SmartSelect=true;
            Main.LocalPlayer.mouseInterface=false;
        }
        private static void LifecycleWorldUpdate(object context)
        {NativeQuickItemChecks.BeginWorldStep();Main.LocalPlayer.Update(0);Call(context,"UpdateRuntime");constructionInputTail();}
        private static void LifecycleReleaseDisplaced(object context,object host,object input,Player p)
        {
            var preparation=Get(host,"buildMaterialPreparation");
            SetAutomatic(host,input,false);
            var use=Get(Get(context,"Tools"),"Use");
            Until(context,input,()=>!(bool)Get(use,"Active") && p.itemAnimation==0 && p.itemTime==0 && p.reuseDelay==0);
            // Reset reuses live Item objects. Finish this fixture's genuine
            // manual delivery BEFORE SetDefaults could repurpose a guarded ref.
            foreach(var item in ((System.Collections.IEnumerable)Get(preparation,"abandonedDisplaced")).Cast<Item>().ToArray())
            {
                int slot=Array.IndexOf(p.inventory,item);
                if(slot<0 || item.IsAir)continue;
                int empty=Enumerable.Range(0,50).First(i=>i!=slot && p.inventory[i].IsAir);
                if(!Main.playerInventory)p.ToggleInv();
                ManualPreparationClick(context,input,p,slot);ManualPreparationClick(context,input,p,empty);
            }
            if(Main.playerInventory)p.ToggleInv();Step(context,input);
            Require(!((System.Collections.IEnumerable)Get(preparation,"abandonedDisplaced")).Cast<Item>().Any(i=>!i.IsAir),
                "this fixture returned every still-carried old54 identity through actual manual delivery");
        }
        private static void SupplyCursorConsumer(object context,object host,object tools,object input,bool returning,bool paused,string command)
        {
            var p=Reset(context,host,tools,input);var preparation=Get(host,"buildMaterialPreparation");
            bool usable=command=="left";
            int source=12,destination=usable?0:54;
            p.inventory[source].SetDefaults(usable?ItemID.StoneBlock:ItemID.RedPaint);p.inventory[source].stack=3;
            p.inventory[destination].SetDefaults(usable?ItemID.Wood:ItemID.BluePaint);p.inventory[destination].stack=7;
            int displacedType=p.inventory[destination].type;
            Step(context,input);long clicks=(long)Get(preparation,"NativeClicks");
            Call(preparation,"TryPrepare",new object[]{p,source,destination,LifecycleIdentity(tools),-1});
            if(returning)
            {
                Until(context,input,()=>(bool)Get(preparation,"Ready"));
                Call(preparation,"RequestReturn");
                Until(context,input,()=>!Main.mouseItem.IsAir);
            }
            else Until(context,input,()=>(long)Get(preparation,"NativeClicks")>=clicks+2 && Main.mouseItem.type==displacedType);
            Require(Main.playerInventory && Main.mouseItem.type==displacedType && (bool)Get(preparation,returning?"Returning":"HasWork"),
                "native clicks actually hold the displaced Item across the requested preparation/return boundary");
            int quantity=LifecycleQuantity(p,displacedType),drops=Main.item.Count(i=>i.type==displacedType && i.stack>0),starts=lifecycleCursorStarts;
            long before=(long)Get(preparation,"NativeClicks");bool oldPause=Main.autoPause,oldPaused=Main.gamePaused;
            try
            {
                LifecycleInput(context,input,command);
                if(paused)
                {
                    Main.autoPause=true;Main.gamePaused=true;
                    Require((bool)typeof(Main).GetMethod("CanPauseGame",F).Invoke(null,null),"the real open inventory selects vanilla's paused branch");
                    typeof(Main).GetMethod("DoUpdate_WhilePaused",F).Invoke(null,null);Call(context,"UpdateRuntime");constructionInputTail();
                }
                else LifecycleWorldUpdate(context);
                Require(!(bool)Get(preparation,"HasWork") && !(bool)Get(preparation,"Unknown") &&
                    LifecycleQuantity(p,displacedType)==quantity && Main.item.Count(i=>i.type==displacedType && i.stack>0)==drops &&
                    lifecycleCursorStarts==starts && !Main.tile[44,42].active() && (long)Get(preparation,"NativeClicks")==before,
                    "the first hidden-cursor gesture hands over before real drop/slot58 ItemCheck, without loss, use, placement or old click replay");
            }
            finally{Main.autoPause=oldPause;Main.gamePaused=oldPaused;p.controlThrow=false;}
            Step(context,input); // genuine all-up precedes the new player action
            LifecycleInput(context,input,command);LifecycleWorldUpdate(context);
            Require(usable?Main.tile[44,42].active() && Main.tile[44,42].type==TileID.WoodBlock && lifecycleCursorStarts>starts:
                Main.item.Count(i=>i.type==displacedType && i.stack>0)>drops && Main.mouseItem.IsAir,
                "a new player gesture after release reaches the original same-cursor consumer normally");
            // Use only real manual inventory commands to clear the player's
            // remaining cursor; these are fixture cleanup, not automatic return.
            NativeToolExecutionChecks.Sample(context,input,new Vector2(480,540),false);p.controlThrow=false;constructionInputTail();
            if(!Main.mouseItem.IsAir)ManualPreparationClick(context,input,p,18);
            if(Main.playerInventory)p.ToggleInv();Step(context,input);
            Console.WriteLine("PASS BlueprintSupplyLifecycle cursor returning="+returning+" paused="+paused+" command="+command+" firstConsumed=0 newGestureOriginal=true");
        }
        private static Player LifecyclePaintUse(object context,object host,object tools,object input,bool singleTarget=false)
        {
            var p=Reset(context,host,tools,input);
            p.inventory[12].SetDefaults(ItemID.Paintbrush);p.inventory[20].SetDefaults(ItemID.RedPaint);p.inventory[20].stack=4;
            p.inventory[54].SetDefaults(ItemID.BluePaint);p.inventory[54].stack=7;
            NativeToolsChecks.Tile(44,42,TileID.Stone);NativeToolsChecks.Tile(46,42,TileID.Stone);
            var cells=new[]{new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Stone,TilePaint=p.inventory[20].paint},
                new CaptureCell{X=2,Layers=CaptureLayer.Tile,TileType=TileID.Stone,TilePaint=p.inventory[20].paint}};
            Place(context,host,input,Asset(host,singleTarget?cells.Take(1).ToArray():cells),44,42);
            SetAutomatic(host,input,true);var preparation=Get(host,"buildMaterialPreparation");
            Until(context,input,()=>(bool)Get(preparation,"Using") && Main.tile[44,42].color()==p.inventory[54].paint &&
                p.inventory[54].stack==3 && (bool)Get(Get(host,"activeBuild"),"Checked"));
            return p;
        }
        private static void SupplyUsingManualCause(object context,object host,object tools,object input,string command)
        {
            var p=LifecyclePaintUse(context,host,tools,input);var use=Get(tools,"Use");var preparation=Get(host,"buildMaterialPreparation");
            var red=(Item)Get(preparation,"Material");var displaced=(Item)Get(preparation,"displaced");long unknown=(long)Get(host,"BuildUnknownResults");
            lifecycleToken=(long)Get(use,"Operation");lifecycleCompleted=lifecycleCompletedReleased=false;
            LifecycleInput(context,input,command);
            // This target is empty, so a legal manual brush owns no new paint.
            Main.mouseX=10;Main.mouseY=10;PlayerInput.MouseX=10;PlayerInput.MouseY=10;
            if(command=="selection")p.selectedItemState.Select(1);
            LifecycleWorldUpdate(context);
            Require((bool)Get(use,"Active") && !(bool)Get(preparation,"HasWork") && !(bool)Get(preparation,"Unknown") && !lifecycleCompleted,
                "actual handoff ends this receipt while the original animated ToolUse still awaits Completed");
            Call(host,"SetAutoBuild",false);
            Until(context,input,()=>lifecycleCompleted && !(bool)Get(use,"Active"));
            Require(lifecycleCompletedReleased && (long)Get(host,"BuildUnknownResults")==unknown && red.stack==3 && displaced.stack==7 &&
                (bool)Call(preparation,"IsDisplaced",displaced),"delayed Completed after real release retains spent paint and old54 exclusion without manufacturing Unknown");
            // Manual handoff made no promise to return red out of slot54.
            // The player really delivers it to the main bag before asking
            // automatic construction to resume from an eligible source.
            p.ToggleInv();ManualPreparationClick(context,input,p,54);ManualPreparationClick(context,input,p,22);p.ToggleInv();Step(context,input);
            SetAutomatic(host,input,true);Until(context,input,()=>Scene(host).Length==0 && !(bool)Get(preparation,"HasWork"));
            Require(red.stack==2 && displaced.stack==7,"current true inventory can automatically resume after the known manual handoff");
            Console.WriteLine("PASS BlueprintSupplyLifecycle using command="+command+" delayedCompletedAfterRelease=true spent=2 old54=7 resumed=true");
            LifecycleReleaseDisplaced(context,host,input,p);
            lifecycleToken=0;
        }
        private static void SupplyDeathBoundary(object context,object host,object tools,object input,string stage,bool medium)
        {
            Player p;var preparation=Get(host,"buildMaterialPreparation");var tail=constructionInputTail;
            if(stage=="Using" || stage=="Returning")
            {
                p=LifecyclePaintUse(context,host,tools,input,stage=="Returning");
                if(stage=="Returning")
                {
                    constructionInputTail=()=>{};
                    Until(context,input,()=>(bool)Get(preparation,"Returning"));
                }
            }
            else
            {
                p=Reset(context,host,tools,input);p.inventory[20].SetDefaults(ItemID.RedPaint);p.inventory[20].stack=4;
                p.inventory[12].SetDefaults(ItemID.Paintbrush);
                if(stage=="Preparing"){p.inventory[30].SetDefaults(ItemID.BluePaint);p.inventory[30].stack=5;}
                p.inventory[54].SetDefaults(ItemID.BluePaint);p.inventory[54].stack=7;Step(context,input);
                long clicks=(long)Get(preparation,"NativeClicks");Call(preparation,"TryPrepare",new object[]{p,20,54,LifecycleIdentity(tools),-1});
                if(stage=="Ready")Until(context,input,()=>(bool)Get(preparation,"Ready"));
                else Until(context,input,()=>(long)Get(preparation,"NativeClicks")>=clicks+2 && Main.mouseItem.type==ItemID.BluePaint);
            }
            var displaced=(Item)Get(preparation,"displaced");int red=LifecycleQuantity(p,ItemID.RedPaint),blue=LifecycleQuantity(p,ItemID.BluePaint);
            long generation=(long)Get(Get(tools,"Runtime"),"Generation"),unknown=(long)Get(host,"BuildUnknownResults");
            byte difficulty=p.difficulty;int spawnX=Main.spawnTileX,spawnY=Main.spawnTileY;
            try
            {
                NativeToolExecutionChecks.Sample(context,input,new Vector2(480,540),false);p.dropItemCheck();
                p.difficulty=(byte)(medium?1:0);Main.spawnTileX=40;Main.spawnTileY=40;
                LifecycleNativeDeath(p,()=>{
                Require(p.dead && (long)Get(Get(tools,"Runtime"),"Generation")==generation,
                    "real KillMe/DropItems deaths retain the same Player/world session");
                constructionInputTail=tail;constructionInputTail();Call(context,"UpdateRuntime");
                Require(!(bool)Get(preparation,"HasWork") && !(bool)Get(preparation,"Unknown") &&
                    (long)Get(host,"BuildUnknownResults")==unknown,"known native death ends only its receipt without false Unknown");
                if(medium)Require(LifecycleQuantity(p,ItemID.RedPaint)==0 && LifecycleQuantity(p,ItemID.BluePaint)==0 &&
                    Main.item.Any(i=>i.type==ItemID.RedPaint && i.stack>0),"mediumcore original DropItems actually drops the prepared inventory");
                else Require(LifecycleQuantity(p,ItemID.RedPaint)==red && LifecycleQuantity(p,ItemID.BluePaint)==blue,
                    "softcore original death retains exactly the actual consumed/remainder quantities");
                });
                Require(!p.dead && (long)Get(Get(tools,"Runtime"),"Generation")==generation,"normal original Spawn revives without a fake new session");
                if(stage=="Preparing")
                {
                    var merged=p.inventory[30];
                    Require(merged.type==ItemID.BluePaint && merged.stack==blue && (bool)Call(preparation,"IsDisplaced",merged) &&
                        !(bool)Call(tools,"BuildInventoryEligible",30,merged),"softcore death's original GetItem full merge preserves exact old54 lineage on its real recipient");
                    if(!Main.playerInventory)p.ToggleInv();
                    ManualPreparationClick(context,input,p,30);ManualPreparationClick(context,input,p,31);
                    Require(!(bool)Call(preparation,"IsDisplaced",merged),"the real recipient becomes supply only after actual player delivery");
                }
                if(medium)
                {
                    foreach(var drop in Main.item.Where(i=>i.stack>0 && (i.type==ItemID.RedPaint || i.type==ItemID.Paintbrush)).ToArray())Call(p,"PickupItem",drop);
                    Require(p.inventory.Take(50).Any(i=>i.type==ItemID.RedPaint && i.stack==red) && p.inventory.Take(50).Any(i=>i.type==ItemID.Paintbrush),
                        "actual PickupItem reacquires the real mediumcore drops as fresh main-bag supply");
                    p.position=new Vector2(640,646);p.velocity=Vector2.Zero;NativeToolsChecks.Tile(44,42,TileID.Stone);
                    var paint=p.inventory.First(i=>i.type==ItemID.RedPaint);
                    Place(context,host,input,Asset(host,new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Stone,TilePaint=paint.paint}),44,42);
                    SetAutomatic(host,input,true);Until(context,input,()=>Scene(host).Length==0 && !(bool)Get(preparation,"HasWork"));
                    Require(paint.stack==red-1 && Main.tile[44,42].color()==paint.paint,"fresh actual supply resumes real paint and complete Match retirement after mediumcore Spawn");
                }
                if(!medium && stage=="Returning")
                {
                    Require(ReferenceEquals(p.inventory[20],displaced) && (bool)Call(preparation,"IsDisplaced",displaced) &&
                        !(bool)Call(tools,"BuildInventoryEligible",20,displaced),"old54 remains in the true main bag but is not promoted to supply by death/respawn");
                    ManualPreparationClick(context,input,p,20);ManualPreparationClick(context,input,p,21);
                    Require(!(bool)Call(preparation,"IsDisplaced",displaced),"a later actual player pickup/delivery requalifies this exact old54");
                }
                if(!Main.mouseItem.IsAir){if(!Main.playerInventory)p.ToggleInv();ManualPreparationClick(context,input,p,23);}
                if(Main.playerInventory)p.ToggleInv();
                Call(host,"SetAutoBuild",false);Step(context,input);
                Console.WriteLine("PASS BlueprintSupplyLifecycle death stage="+stage+" medium="+medium+" actualKillDropSpawn=true sameSession=true red="+red+" blue="+blue);
                LifecycleReleaseDisplaced(context,host,input,p);
            }
            finally{constructionInputTail=tail;p.difficulty=difficulty;Main.spawnTileX=spawnX;Main.spawnTileY=spawnY;}
        }
        private static bool LifecycleSkipSave(){return false;}
        private static void SupplyDeathInNativeConsumer(object context,object host,object tools,object input)
        {
            var p=Reset(context,host,tools,input);var preparation=Get(host,"buildMaterialPreparation");
            p.inventory[12].SetDefaults(ItemID.Paintbrush);p.inventory[20].SetDefaults(ItemID.RedPaint);p.inventory[20].stack=4;var red=p.inventory[20];
            p.inventory[54].SetDefaults(ItemID.BluePaint);p.inventory[54].stack=7;var blue=p.inventory[54];
            Require((bool)Call(tools,"BuildInventoryEligible",20,red),"native-scope death fixture starts with an independent true source; displaced="+
                Call(preparation,"IsDisplaced",red)+" protectedSlots="+Get(Get(Get(tools,"Items"),"Ownership"),"ProtectedSlots")+
                " phase="+Get(preparation,"phase")+" manual="+Get(Get(Get(tools,"Items"),"World"),"HasManualOperation"));
            NativeToolsChecks.Tile(44,42,TileID.Stone);
            Place(context,host,input,Asset(host,new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Stone,TilePaint=red.paint}),44,42);
            byte difficulty=p.difficulty;int x=Main.spawnTileX,y=Main.spawnTileY;long unknown=(long)Get(host,"BuildUnknownResults");
            lifecycleInsideTools=tools;lifecycleInsidePreparation=preparation;lifecycleInsideSeen=false;lifecycleKillInside=true;lifecyclePaintCalls=0;lifecyclePaintState=null;
            try
            {
                p.difficulty=0;Main.spawnTileX=40;Main.spawnTileY=40;SetAutomatic(host,input,true);
                Until(context,input,()=>lifecycleInsideSeen && !(bool)Get(preparation,"HasWork") && !(bool)Get(Get(tools,"Use"),"Active"));
                Require(p.dead && red.stack==3 && blue.stack==7 && (long)Get(host,"BuildUnknownResults")==unknown && !(bool)Get(preparation,"Unknown"),
                    "real ApplyPaint completed its debit before native death; callback/finalizer retires exactly once without false Unknown or material refill");
                LifecycleNativeSpawn(p);Call(host,"SetAutoBuild",false);Step(context,input);
                LifecycleReleaseDisplaced(context,host,input,p);
                Console.WriteLine("PASS BlueprintSupplyLifecycle death insideOriginalApplyPaint=true deferredNativeExit=true debit=1 old54=7 unknownAdded=0");
            }
            catch{Console.Error.WriteLine("NATIVE DEATH STIMULUS calls="+lifecyclePaintCalls+" stillArmed="+lifecycleKillInside+" seen="+lifecycleInsideSeen+" state="+lifecyclePaintState);throw;}
            finally{p.difficulty=difficulty;Main.spawnTileX=x;Main.spawnTileY=y;lifecycleKillInside=false;lifecycleInsideTools=lifecycleInsidePreparation=null;}
        }
        private static void LifecycleNativeDeath(Player p,Action afterDeath=null,bool revive=true)
        {
            string saveRoot=System.IO.Path.GetFullPath(Terraria.Program.SavePath).TrimEnd(System.IO.Path.DirectorySeparatorChar)+System.IO.Path.DirectorySeparatorChar;
            Require(Main.ActivePlayerFileData!=null && System.IO.Path.GetFullPath(Main.ActivePlayerFileData.Path).StartsWith(saveRoot,StringComparison.OrdinalIgnoreCase),
                "native death fixture player path is owned by this isolated process");
            var scope=new Harmony("JueMingR.Tests.SupplyDeathOutlets");
            // QuickItems deliberately sinks recall, which is unrelated here.
            // Temporarily remove exactly that fixture prefix so Revive executes
            // the ORIGINAL Spawn. Death's unrelated async disk-save sink is
            // denied: this CPU fixture has no achievements/map save runtime.
            scope.Patch(AccessTools.Method(typeof(WorldGen),"saveToonWhilePlaying"),
                prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(LifecycleSkipSave)));
            try{p.KillMe(PlayerDeathReason.ByOther(1),1000,0);afterDeath?.Invoke();if(revive)LifecycleNativeSpawn(p);}
            finally
            {
                foreach(var method in scope.GetPatchedMethods().ToArray())scope.Unpatch(method,HarmonyPatchType.All,scope.Id);
            }
        }
        private static void LifecycleNativeSpawn(Player p)
        {
            var spawn=AccessTools.Method(typeof(Player),"Spawn",new[]{typeof(PlayerSpawnContext)});
            var recall=AccessTools.Method(typeof(NativeQuickItemChecks),"Recall");
            var owner=new Harmony("JueMingR.Tests.QuickItemOutlets");
            owner.Unpatch(spawn,recall);
            try{p.Spawn(PlayerSpawnContext.ReviveFromDeath);}
            finally{owner.Patch(spawn,prefix:new HarmonyMethod(recall));}
        }
    }
}
