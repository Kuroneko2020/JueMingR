using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static bool presentationThrows, presentationObserved, presentationCloses;
        private static Item presentationCursor;
        private static bool supplyDiscoveryFrozen;
        private static bool FreezeSupplyDiscoveryAdvance(){return !supplyDiscoveryFrozen;}
        // Match the locked Main.DoUpdate pause fork, not its unrelated visual
        // initialization. Both pause methods are original Terraria bodies.
        // Only the unpaused arm enters this fixture's existing world stage;
        // the assignments mirror Main immediately after CanPauseGame/paused UI.
        private static bool SupplyAutoPauseStep(object context,object input,bool inventoryIntent=false)
        {
            NativeToolExecutionChecks.Sample(context,input,new Vector2(480,540),false);
            if(inventoryIntent)PlayerInput.Triggers.Current.Inventory=true;
            uint world=Main.GameUpdateCount;int consumers=continuousPlacements.Count;
            Require(Main.CanUpdateGameplay,"auto-pause fixture reaches the original gameplay/pause fork");
            bool paused=(bool)typeof(Main).GetMethod("CanPauseGame",F).Invoke(null,null);
            if(paused)
            {
                typeof(Main).GetMethod("DoUpdate_WhilePaused",F).Invoke(null,null);
                Main.gamePaused=true;
            }
            else
            {
                Main.gamePaused=false;
                NativeQuickItemChecks.BeginWorldStep();Main.LocalPlayer.Update(0);
            }
            Call(context,"UpdateRuntime");constructionInputTail();
            if(paused)Require(Main.GameUpdateCount==world && continuousPlacements.Count==consumers &&
                !(bool)Get(input,"CanRunAutomaticActions"),"original paused UI and inventory tail never enter a world step or construction consumer");
            return paused;
        }
        private static void SupplyAutoPause(object context,object host,object tools,object input)
        {
            bool oldPause=Main.autoPause,oldPaused=Main.gamePaused,oldOptions=Main.ingameOptionsWindow;
            var patch=new Harmony("JueMingR.Tests.SupplyAutoPause");
            patch.Patch(typeof(WorldGen).GetMethod("paintTile",F,null,new[]{typeof(int),typeof(int),typeof(byte),typeof(bool),typeof(bool)},null),
                postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(ContinuousPainted)));
            try
            {
                var p=Reset(context,host,tools,input);var preparation=Get(host,"buildMaterialPreparation");
                p.inventory[12].SetDefaults(ItemID.Paintbrush);
                p.inventory[20].SetDefaults(ItemID.RedPaint);p.inventory[20].stack=3;var material=p.inventory[20];
                p.inventory[54].SetDefaults(ItemID.BluePaint);p.inventory[54].stack=7;var priority=p.inventory[54];
                NativeToolsChecks.Tile(44,42,TileID.Stone);
                Place(context,host,input,Asset(host,new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Stone,TilePaint=material.paint}),44,42);
                continuousHost=host;continuousUse=Get(tools,"Use");continuousPlacements.Clear();continuousWatch=continuousAutomatic=true;
                long start=(long)Get(preparation,"NativeClicks");Main.autoPause=true;SetAutomatic(host,input,true);
                NativeQuickItemChecks.Until(()=>{SupplyAutoPauseStep(context,input);return Main.playerInventory;});
                Require(Main.playerInventory && !Main.mouseItem.IsAir && (long)Get(preparation,"NativeClicks")==start+1 &&
                    (bool)typeof(Main).GetMethod("CanPauseGame",F).Invoke(null,null),
                    "the first real preparation click opens its own inventory and actually triggers vanilla CanPauseGame");
                uint preparationWorld=Main.GameUpdateCount;long before=(long)Get(preparation,"NativeClicks");var cursor=Main.mouseItem;
                Main.ingameOptionsWindow=true;
                try
                {
                    Require(SupplyAutoPauseStep(context,input) && (long)Get(preparation,"NativeClicks")==before && ReferenceEquals(Main.mouseItem,cursor),
                        "a real options menu blocks even an exact owned inventory receipt while vanilla remains paused");
                }
                finally{Main.ingameOptionsWindow=false;}
                Call(input,"BeginUpdate");constructionInputTail();
                Require(!(bool)Get(input,"CanStartActions") && (long)Get(preparation,"NativeClicks")==before && ReferenceEquals(Main.mouseItem,cursor),
                    "an outer epoch without fresh native input cannot reuse the previous paused UI permission");
                int preparePaused=0;
                for(int i=0;i<8 && !(bool)Get(preparation,"Ready");i++)if(SupplyAutoPauseStep(context,input))preparePaused++;
                Console.WriteLine("BlueprintAutoPause preparation ready="+Get(preparation,"Ready")+" pausedSteps="+preparePaused+
                    " clicks="+((long)Get(preparation,"NativeClicks")-start)+" worldDelta="+unchecked(Main.GameUpdateCount-preparationWorld)+
                    " consumers="+continuousPlacements.Count+" inventory="+Main.playerInventory+" paused="+Main.gamePaused);
                Require((bool)Get(preparation,"Ready") && preparePaused>0 && Main.GameUpdateCount==preparationWorld && Main.gamePaused &&
                    !Main.playerInventory && Main.mouseItem.IsAir && ReferenceEquals(p.inventory[54],material) && priority.stack==7 &&
                    (long)Get(preparation,"NativeClicks")==start+3 && continuousPlacements.Count==0,
                    "paused original UI/input tails must finish exactly three preparation swaps and close owned inventory without world progress");
                // Ready is reached in Draw's tail after Main chose pause. An
                // extra outer callback cannot use it until vanilla next takes
                // the unpaused world arm, even though its inventory is closed.
                Call(context,"UpdateRuntime");constructionInputTail();
                Require((bool)Get(preparation,"Ready") && !(bool)Get(continuousUse,"Active") && !(bool)Get(preparation,"CanClickNow") &&
                    continuousPlacements.Count==0 && Main.GameUpdateCount==preparationWorld,
                    "Ready grants neither inventory continuation nor ToolUse/consumer permission in the still-paused epoch");
                Require(!SupplyAutoPauseStep(context,input) && Main.GameUpdateCount==unchecked(preparationWorld+1),
                    "only the next original non-paused arm restores normal world eligibility after owned inventory closure");
                int returnPaused=0;
                for(int i=0;i<120 && ((bool)Get(preparation,"HasWork") || Scene(host).Length!=0);i++)
                {
                    bool returning=(bool)Get(preparation,"Returning");
                    if((bool)Get(preparation,"Using"))Require(!(bool)Get(preparation,"CanClickNow"),"Using never receives inventory continuation permission");
                    if(SupplyAutoPauseStep(context,input) && returning)returnPaused++;
                }
                Require(!(bool)Get(preparation,"HasWork") && Scene(host).Length==0 && returnPaused>0 && !Main.playerInventory && Main.mouseItem.IsAir &&
                    continuousPlacements.Count==1 && continuousPlacements[0].Kind=="PaintTile" && ReferenceEquals(continuousPlacements[0].Supply,material) &&
                    Main.tile[44,42].color()==material.paint && ReferenceEquals(p.inventory[20],material) && material.stack==2 &&
                    ReferenceEquals(p.inventory[54],priority) && priority.stack==7 && (long)Get(preparation,"NativeClicks")==start+6,
                    "one real paint consumer spends one exact material, then paused UI completes inverse swaps and returns the original54 identity and real remainder");
                Console.WriteLine("PASS BlueprintAutoPause preparePaused="+preparePaused+" returnPaused="+returnPaused+
                    " nativeClicks=6 paintConsumers=1 materialRemainder=2 original54=7 menuBlocked=true unsampledBlocked=true pausedWorldSteps=0");
                Require(!SupplyAutoPauseStep(context,input),"returned inventory lets vanilla leave pause naturally");
                SetAutomatic(host,input,false);
                // A foreign open inventory really pauses, but cannot start a
                // new receipt. The exception belongs only to work already held.
                Player.OpenInventory(quiet:true);Require(SupplyAutoPauseStep(context,input),"foreign inventory triggers the original pause condition");
                var identity=new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"auto-pause-new-task",(long)Get(Get(tools,"Runtime"),"Generation"),1);
                object[] args={p,20,54,identity,-1};
                Require(!(bool)Call(preparation,"TryPrepare",args) && !(bool)Get(preparation,"HasWork"),"paused input cannot start a new material preparation");
                p.ToggleInv();Require(!SupplyAutoPauseStep(context,input),"manual inventory closure resumes only through vanilla's unpaused arm");
                Call(preparation,"TryPrepare",args);constructionInputTail();
                Require(Main.playerInventory && !Main.mouseItem.IsAir,"takeover starts with a genuine owned preparation cursor");
                cursor=Main.mouseItem;before=(long)Get(preparation,"NativeClicks");
                Require(SupplyAutoPauseStep(context,input,true) && Main.playerInventory && !(bool)Get(preparation,"HasWork") &&
                    ReferenceEquals(Main.mouseItem,cursor),"the actual paused Inventory handler immediately reveals and hands over the hidden inventory");
                Require(SupplyAutoPauseStep(context,input) && (long)Get(preparation,"NativeClicks")==before && Main.playerInventory && ReferenceEquals(Main.mouseItem,cursor),
                    "a later paused update/tail cannot replay old swaps or close the player's inventory after takeover");
                ManualPreparationClick(context,input,p,20);p.ToggleInv();Require(!SupplyAutoPauseStep(context,input),"manual owner completes its real cursor move and closes its own UI");
                Console.WriteLine("PASS BlueprintAutoPause newPreparationBlocked=true readyAndUsingNotInventoryContinuation=true nativePausedInventoryTakeover=true oldTailCannotMoveOrClose=true");
            }
            finally
            {
                continuousWatch=continuousAutomatic=false;continuousHost=continuousUse=null;continuousPlacements.Clear();
                Main.autoPause=oldPause;Main.gamePaused=oldPaused;Main.ingameOptionsWindow=oldOptions;
                foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);
            }
        }
        // This deliberate interception executes the real patched interface
        // entry/exit without a GPU body. It proves scope and cleanup, never
        // pixels; the ordinary native input tail below still moves real Items.
        private static bool ObservePresentationEntry()
        {
            Require(!Main.playerInventory && ReferenceEquals(Main.mouseItem,presentationCursor),"interface entry borrows only inventory presentation, preserving its real cursor");
            Require(Main.MouseDisplayItem.IsAir,"the real MouseDisplayItem getter hides only the owned presentation cursor");
            presentationObserved=true;
            if(presentationCloses)Main.LocalPlayer.ToggleInv();
            if(presentationThrows)throw new InvalidOperationException("controlled-interface-exit");
            return false;
        }
        private static void SupplyPresentation(object context,object host,object tools,object input)
        {
            var p=Reset(context,host,tools,input);var preparation=Get(host,"buildMaterialPreparation");
            p.inventory[12].SetDefaults(ItemID.RedPaint);p.inventory[12].stack=3;var material=p.inventory[12];
            p.inventory[54].SetDefaults(ItemID.BluePaint);p.inventory[54].stack=7;var priority=p.inventory[54];
            Step(context,input);
            var identity=new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"hidden-native-ui",(long)Get(Get(tools,"Runtime"),"Generation"),1);
            object[] args={p,12,54,identity,-1};Call(preparation,"TryPrepare",args);constructionInputTail();
            Require(Main.playerInventory && !Main.mouseItem.IsAir,"presentation begins after a genuine original ItemSlot swap with owned inventory open");
            presentationCursor=Main.mouseItem;
            var patch=new Harmony("JueMingR.Tests.HiddenInventoryEntry");
            var draw=typeof(Main).GetMethod("DrawInterface",F,null,new[]{typeof(GameTime)},null);
            var drawTarget=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));
            GC.SuppressFinalize(drawTarget);
            var prefix=new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(ObservePresentationEntry)){priority=Priority.Last};
            patch.Patch(draw,prefix:prefix);
            try
            {
                presentationThrows=false;presentationObserved=false;draw.Invoke(drawTarget,new object[]{new GameTime()});
                Require(presentationObserved && Main.playerInventory,"real interface normal postfix restores logical inventory before the input tail");
                presentationThrows=true;presentationObserved=false;
                try{draw.Invoke(drawTarget,new object[]{new GameTime()});throw new InvalidOperationException("controlled fault did not run");}
                catch(TargetInvocationException error){Require(error.InnerException?.Message=="controlled-interface-exit","the expected interface fault reaches the caller");}
                Require(presentationObserved && Main.playerInventory && ReferenceEquals(Main.mouseItem,presentationCursor) && !Main.MouseDisplayItem.IsAir,
                    "real interface finalizer is idempotent and restores inventory and cursor display on exception");
            }
            finally{presentationThrows=false;presentationCloses=false;presentationCursor=null;foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);}
            for(int i=0;i<20 && !(bool)Get(preparation,"Ready");i++)Step(context,input);
            Require((bool)Get(preparation,"Ready") && !Main.playerInventory && ReferenceEquals(p.inventory[54],material),"the original input tail completes preparation after both actual interface exits");
            Call(preparation,"RequestReturn");Until(context,input,()=>!(bool)Get(preparation,"HasWork"));
            Require(ReferenceEquals(p.inventory[12],material) && ReferenceEquals(p.inventory[54],priority) && material.stack==3 && priority.stack==7,"original ItemSlot return preserves exact references and amounts");
            Console.WriteLine("PASS BlueprintSupplyPresentation actualInterfaceEntry=true normalAndExceptionRestore=true original54SwapAndReturn=true fullDrawPixels=NOT_RUN");

            Step(context,input);Call(preparation,"TryPrepare",args);constructionInputTail();presentationCursor=Main.mouseItem;
            patch.Patch(draw,prefix:prefix);
            try
            {
                presentationCloses=true;presentationObserved=false;draw.Invoke(drawTarget,new object[]{new GameTime()});
                Require(presentationObserved && !Main.playerInventory && !(bool)Get(preparation,"HasWork"),
                    "a real inventory close inside the interface restores first and revokes the old scope, so late postfix/finalizer cannot reopen it");
            }
            finally{presentationCloses=false;presentationCursor=null;foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);}
            p.ToggleInv();ManualPreparationClick(context,input,p,12);p.ToggleInv();Step(context,input);
            Console.WriteLine("PASS BlueprintSupplyPresentation nativeCloseInsideDraw=true lateRestoreCannotReopen=true");

            // Slot0 is the Flexible material destination. This shared mapping
            // must retain the displaced original Item, not merely slot number.
            p=Reset(context,host,tools,input);p.inventory[0].SetDefaults(ItemID.Wood);p.inventory[0].stack=5;var original=p.inventory[0];
            p.inventory[12].SetDefaults(ItemID.StoneBlock);p.inventory[12].stack=4;material=p.inventory[12];Step(context,input);
            object[] zero={p,12,0,identity,-1};Call(preparation,"TryPrepare",zero);
            for(int i=0;i<20 && !(bool)Get(preparation,"Ready");i++)Step(context,input);
            Require((bool)Get(preparation,"Ready") && ReferenceEquals(p.inventory[0],material),"real slot0 destination is prepared before testing display-only references");
            var view=(Item[])Call(preparation,"PresentHotbarInventory",new object[]{p.inventory});
            Require(!ReferenceEquals(view,p.inventory) && ReferenceEquals(view[0],original) && ReferenceEquals(view[12],material) && original.stack==5,
                "hotbar title/hover/draw share original protected identities during Ready without replacing the live inventory");
            long oldToken=(long)Get(preparation,"token");var ownership=Get(Get(tools,"Items"),"Ownership");
            long useToken=(long)Call(ownership,"NewUseToken");Require((bool)Call(preparation,"TryAdoptUse",useToken,12,0UL),"slot0 display case adopts a real new use owner");
            Set(preparation,"UseConsumptionMaximum",new Func<int,int>(slot=>slot==0?1:0));material.stack--;
            Require((bool)Call(preparation,"ObserveUseConsumption",useToken),"exact native-budget accounting observes the controlled one-unit remainder");
            view=(Item[])Call(preparation,"PresentHotbarInventory",new object[]{p.inventory});
            Require(ReferenceEquals(view[12],material) && view[12].stack==3 && ReferenceEquals(view[0],original),"Using display shows current real remaining amounts rather than frozen clones");
            Call(ownership,"EndUse",(long)Get(Get(tools,"Runtime"),"Generation"),oldToken);
            Require((bool)Call(ownership,"OwnsUse",(long)Get(Get(tools,"Runtime"),"Generation"),useToken),"late old owner cannot release current display receipt");
            Call(preparation,"ReturnUse",useToken);Until(context,input,()=>!(bool)Get(preparation,"HasWork"));
            Require(ReferenceEquals(p.inventory[0],original) && ReferenceEquals(p.inventory[12],material) && material.stack==3,"display mapping returns only actual current remainder");
            Console.WriteLine("PASS BlueprintSupplyPresentation slot0ReadyUsingReferences=true currentRemainder=true noLiveInventoryReplacement=true");
            SupplyFlexibleToolPresentation(context,host,tools,input);
        }
        private static void SupplyFlexibleToolPresentation(object context,object host,object tools,object input)
        {
            var p=Reset(context,host,tools,input);p.inventory[0].SetDefaults(ItemID.RubblemakerSmall);var tool=p.inventory[0];
            p.inventory[1].SetDefaults(ItemID.CopperPickaxe);var originalSelection=p.inventory[1];
            p.selectedItemState.Select(1);p.selectedItemState.Update();
            p.inventory[12].SetDefaults(ItemID.DirtBlock);p.inventory[12].stack=3;var material=p.inventory[12];
            int oldCycle=Player.FlexibleWandCycleOffset;
            Require(WorldGen.PlaceObject(60,42,650,true,8),"display counterexample has a real Flexible second-bucket object");
            var data=Terraria.ObjectData.TileObjectData.GetTileData(650,8);
            var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>Step(context,input),
                60-data.Origin.X,42-data.Origin.Y,data.Width,data.Height,"搬运后真实工具显示");
            Place(context,host,input,entry,44,43-data.Height);SetAutomatic(host,input,true);
            var preparation=Get(host,"buildMaterialPreparation");var use=Get(tools,"Use");
            Until(context,input,()=>(bool)Get(preparation,"Ready"));
            var live=p.inventory;var view=(Item[])Call(preparation,"PresentHotbarInventory",new object[]{live});
            int shown=(int)Call(preparation,"PresentHotbarSelection",p.selectedItem);
            Console.WriteLine("BlueprintSupplyPresentation flexible ready actualSelected="+p.selectedItem+" shown="+shown);
            Require(ReferenceEquals(live[12],tool) && ReferenceEquals(live[0],material) && ReferenceEquals(view[1],originalSelection),
                "real Flexible preparation moves tool0 to source12 while Ready preserves the player's different original selected Item");
            Until(context,input,()=>(bool)Get(preparation,"Using") && (bool)Get(use,"Building") && p.selectedItem==12);
            view=(Item[])Call(preparation,"PresentHotbarInventory",new object[]{live});
            shown=(int)Call(preparation,"PresentHotbarSelection",p.selectedItem);
            bool temporary=(bool)Call(preparation,"PresentHotbarOverride",p.selectedItemState.HasActiveOverride);
            var held=(Item)Call(preparation,"PresentHotbarHeldItem",p.HeldItem);
            bool extra=(temporary || shown>=10) && shown!=58 && !held.IsAir;
            Console.WriteLine("BlueprintSupplyPresentation flexible actualSelected="+p.selectedItem+" shown="+shown+" override="+temporary+
                " titleType="+view[shown].type+" heldType="+held.type+" extra="+extra+" originalSelectedType="+originalSelection.type);
            // These are the exact title, highlight and context36 extra-slot
            // reads in GUIHotbarDrawInner. The real ToolUse/SelectedItemState
            // chose 12; neither the live array nor that choice is substituted.
            Require(p.selectedItemState.HasActiveOverride && temporary && extra && shown==0 &&
                ReferenceEquals(view[shown],tool) && ReferenceEquals(held,tool) && view[shown].AffixName()==tool.AffixName() &&
                ReferenceEquals(view[0],tool) && ReferenceEquals(view[12],material) && !ReferenceEquals(view[shown],originalSelection) &&
                ReferenceEquals(p.inventory,live) && ReferenceEquals(live[12],tool) && ReferenceEquals(live[0],material) &&
                (int)Get(Get(use,"Intent"),"Slot")==12,
                "Using title and temporary selected icon follow the actual migrated Flexible tool, not material or permanently pinned original selection");
            // The last real return click still has vanilla's cursor58
            // override in this draw tail. Let the following Player.Update
            // restore it before assessing completed selection ownership.
            Until(context,input,()=>Scene(host).Length==0 && !(bool)Get(preparation,"HasWork") && !(bool)Get(use,"Active") &&
                p.selectedItem==1 && !p.selectedItemState.HasActiveOverride);
            Require(ReferenceEquals(p.inventory[0],tool) && ReferenceEquals(p.inventory[12],material) && material.stack==2 &&
                Player.FlexibleWandCycleOffset==oldCycle && Main.mouseItem.IsAir && p.selectedItem==1 && !p.selectedItemState.HasActiveOverride,
                "the actual Flexible consumer spends one material then returns exact Items, selection and cycle through native exits");
            Console.WriteLine("PASS BlueprintSupplyPresentation flexibleTool0To12=true readyOriginalItem=1 usingDisplay=0 actualSelection=12 temporaryExtraTool=true consumed=1 exactReturn=true");
        }
        private static void SupplyFirstInventoryTakeover(object context,object host,object tools,object input)
        {
            foreach(bool escape in new[]{false,true})
            {
                var p=Reset(context,host,tools,input);var preparation=Get(host,"buildMaterialPreparation");
                p.inventory[12].SetDefaults(ItemID.RedPaint);p.inventory[54].SetDefaults(ItemID.BluePaint);Step(context,input);
                var identity=new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"first-native-inventory",(long)Get(Get(tools,"Runtime"),"Generation"),1);
                object[] args={p,12,54,identity,-1};Call(preparation,"TryPrepare",args);constructionInputTail();
                var cursor=Main.mouseItem;long clicks=(long)Get(preparation,"NativeClicks");
                PlayerInput.Triggers.Current.Inventory=true;if(escape)Main.keyState=new KeyboardState(Keys.Escape);
                p.ToggleInv();
                Require(Main.playerInventory && !(bool)Get(preparation,"HasWork") && ReferenceEquals(Main.mouseItem,cursor),"first actual Inventory action reveals and hands over the existing real inventory, including Escape mapped to Inventory");
                PlayerInput.Triggers.Current.Inventory=false;Main.keyState=new KeyboardState();constructionInputTail();
                Require((long)Get(preparation,"NativeClicks")==clicks && Main.playerInventory,"old tail cannot hide or return the manually revealed inventory");
                p.ToggleInv();Require(!Main.playerInventory,"second Inventory action follows original close semantics");
                p.ToggleInv();ManualPreparationClick(context,input,p,12);p.ToggleInv();Step(context,input);
                Console.WriteLine("PASS BlueprintSupplyPresentation firstInventoryReveal=true secondNativeClose=true escapeMapped="+escape);
            }
        }
        private static void SupplyDiscoveryBoundary(object context,object host,object tools,object input)
        {
            var patch=new Harmony("JueMingR.Tests.SupplyDiscovery");
            patch.Patch(typeof(WorldGen).GetMethod("paintTile",F,null,new[]{typeof(int),typeof(int),typeof(byte),typeof(bool),typeof(bool)},null),
                postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(ContinuousPainted)));
            patch.Patch(host.GetType().GetMethod("AdvanceAutomaticBuild",F),prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(FreezeSupplyDiscoveryAdvance)));
            var p=Reset(context,host,tools,input);p.inventory[12].SetDefaults(ItemID.Paintbrush);
            p.inventory[20].SetDefaults(ItemID.RedPaint);p.inventory[20].stack=4;var red=p.inventory[20];
            p.inventory[54].SetDefaults(ItemID.BluePaint);p.inventory[54].stack=7;var priority=p.inventory[54];
            for(int x=44;x<46;x++)NativeToolsChecks.Tile(x,42,TileID.Stone);
            Place(context,host,input,Asset(host,Enumerable.Range(0,2).Select(x=>new CaptureCell{X=x,Layers=CaptureLayer.Tile,TileType=TileID.Stone,TilePaint=red.paint}).ToArray()),44,42);
            continuousHost=host;continuousUse=Get(tools,"Use");continuousPlacements.Clear();continuousWatch=continuousAutomatic=true;
            var preparation=Get(host,"buildMaterialPreparation");long start=(long)Get(preparation,"NativeClicks");
            try
            {
                SetAutomatic(host,input,true);
                Until(context,input,()=>continuousPlacements.Count==1 && GetOptional(host,"activeBuild")!=null &&
                    (bool)Get(Get(host,"activeBuild"),"Checked") && GetOptional(host,"preparedBuild")!=null && !(bool)Get(continuousUse,"InNativeUse"));
                // An explicit exhausted shared-budget boundary isolates the
                // new exit. Real paint/debit/receipt precede it; native input
                // and the next use continue through ordinary Steps afterward.
                var oldIdentity=(BlueprintProjectionIdentity)Get(Get(Get(host,"activeBuild"),"Target"),"Identity");
                Set(host,"preparedBuild",null);supplyDiscoveryFrozen=true;
                // Discovery alone is paused. Vanilla winds down its existing
                // animation and selection before the normal ToolUse exit;
                // force-Retire during a live animation is a different boundary.
                Until(context,input,()=>GetOptional(host,"buildSupplyDiscovery")!=null && !(bool)Get(continuousUse,"Active"));
                supplyDiscoveryFrozen=false;
                var ticket=GetOptional(host,"buildSupplyDiscovery");long retainedClicks=(long)Get(preparation,"NativeClicks");
                Require(ticket!=null && (bool)Get(preparation,"Ready") && !(bool)Get(continuousUse,"Active") &&
                    !(bool)Call(preparation,"Blocks",oldIdentity),"settled use releases ToolUse and retains a target-free finite discovery ticket");
                Set(host,"materialBuildSourceBudget",0);Set(host,"materialBuildSearchBudget",0);
                object[] pending={p,64};Call(host,"ContinueBuildSupply",pending);
                Require(ReferenceEquals(ticket,GetOptional(host,"buildSupplyDiscovery")) && (bool)Get(preparation,"Ready") &&
                    !(bool)Get(continuousUse,"Active") && (long)Get(preparation,"NativeClicks")==retainedClicks,"BudgetPending retains the same frozen ticket with no native clicks or input owner");
                Until(context,input,()=>Scene(host).Length==0 && !(bool)Get(continuousUse,"Active") && !(bool)Get(preparation,"HasWork"));
                Require(continuousPlacements.Count==2 && continuousPlacements.All(v=>ReferenceEquals(v.Supply,red) && v.PreparationClicks==retainedClicks) &&
                    red.stack==2 && priority.stack==7 && ReferenceEquals(p.inventory[20],red) && ReferenceEquals(p.inventory[54],priority),"ordinary next-step search rebinds the same exact supply with no extra prepare/return clicks and finite final return");
                Console.WriteLine("PASS BlueprintSupplyDiscovery paints=2 controlledSharedBudgetBoundary=true prepareClicks="+(retainedClicks-start)+
                    " betweenPaintClicks=0 totalClicks="+((long)Get(preparation,"NativeClicks")-start)+" original54Consumed=0 finalReturn=true");
            }
            finally{supplyDiscoveryFrozen=false;continuousWatch=false;SetAutomatic(host,input,false);foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);}
        }
    }
}
