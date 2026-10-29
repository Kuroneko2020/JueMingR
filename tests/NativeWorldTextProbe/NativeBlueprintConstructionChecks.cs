using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using Terraria.GameInput;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static bool constructionChecksActive;
        private static Action constructionInputTail;
        internal static void RunConstruction(object context, bool preparationOnly=false, string family=null)
        {
            var host=Get(context,"Blueprint");var tools=Get(context,"Tools");var input=Get(context,"Input");
            NativeToolExecutionChecks.Initialize(context,null);
            Require((bool)Get(tools,"Available"),"construction native hooks initialized: "+GetOptional(tools,"SetupError"));
            Framing.Initialize();Main.SetupTileMerge();TorchID.Initialize();
            var lightingMode=Lighting.Mode;Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
            var patch=new Harmony("JueMingR.Tests.BlueprintConstructionOutlets");
            faultTools=tools;reportedNativeFault=false;
            patch.Patch(typeof(Player).GetMethod("ItemCheck",F),finalizer:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(NativeFault)));
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})
                patch.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,F),prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SkipOutlet)));
            constructionChecksActive=true;
            var preparation=Get(host,"buildMaterialPreparation");
            var tail=preparation.GetType().GetMethod("NativeInputTailForChecks",F);
            Require(tail!=null,"construction CPU fixture uses the locked original native input tail");
            constructionInputTail=()=>tail.Invoke(null,null);
            try
            {
                NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(host,"ControlsEnabled");});
                using(NativeBlueprintWorldMatchChecks.EmptyWorld(host,200,160))
                {
                    Exception first=null;
                    Action<string,Action> check=(name,body)=>
                    {
                        if(first!=null && (family=="SupplySmartWiring" || family=="SupplyPresentation"))return;
                        try{body();Console.WriteLine("PASS BlueprintConstruction case="+name);}
                        catch(Exception error){Console.Error.WriteLine("CONSTRUCTION CASE "+name+": "+error);DumpConstructionPreparation(host,tools);if(first==null)first=error;}
                    };
                    check("finite original inventory input preparation",()=>ConstructionMaterialPreparation(context,host,tools,input));
                    if(!preparationOnly && first==null)
                    {
                    if(family=="SmartPlatform")check("natural smart platform slopes and fallback",()=>ConstructionSmartPlatform(context,host,tools,input));
                    if(family=="ActuatorNetwork")check("original multiplayer temporary actuator provenance",()=>ConstructionWiringActuatorNetwork(context,host,tools,input));
                    if(family=="SupplyPresentation")
                    {
                        check("owned inventory finishes across native auto pause",()=>SupplyAutoPause(context,host,tools,input));
                        check("hidden inventory presentation and migrated Flexible tool",()=>SupplyPresentation(context,host,tools,input));
                        check("first inventory action reveals manual ownership",()=>SupplyFirstInventoryTakeover(context,host,tools,input));
                    }
                    if(family=="SupplySmartWiring")
                    {
                        check("owned inventory finishes across native auto pause",()=>SupplyAutoPause(context,host,tools,input));
                        check("settled supply survives a finite shared-budget boundary",()=>SupplyDiscoveryBoundary(context,host,tools,input));
                        check("natural smart platform slopes and fallback",()=>ConstructionSmartPlatform(context,host,tools,input));
                        check("original multiplayer temporary actuator provenance",()=>ConstructionWiringActuatorNetwork(context,host,tools,input));
                        check("hidden inventory presentation entry and restoration",()=>SupplyPresentation(context,host,tools,input));
                        check("first inventory action reveals manual ownership",()=>SupplyFirstInventoryTakeover(context,host,tools,input));
                    }
                    if(family==null)
                    {
                    check("object walls preserve layer and container duties",()=>ConstructionWallContexts(context,host,tools,input));
                    check("ordinary continuous native opportunities",()=>NativeContinuousOpportunity(context,host,tools,input));
                    check("ordinary sustained throughput",()=>SustainedNativeThroughput(context,host,tools,input));
                    check("ordinary moving furniture direction",()=>MovingFurnitureDirection(context,host,tools,input));
                    check("ordinary attached accessory budgets",()=>OriginalAccessories(context,host,tools,input));
                    // The current four-fix acceptance is one consumer batch:
                    // continuation shares this same real inventory input tail.
                    check("finite same-source service and mixed appearance",()=>MaterialContinuityAndYield(context,host,tools,input));
                    check("cross-update moving continuation",()=>ExecutionSearchFairness(context,host,tools,input,true));
                    check("continuation beyond four cached sources",()=>BuildSourceFullContinuation(context,host,tools,input));
                    check("large mixed ordinary and object continuation",()=>MixedSourceContinuation(context,host,tools,input));
                    }
                    if(family==null || family=="Appearance")check("appearance consumers and native preparation",()=>ConstructionAppearance(context,host,tools,input));
                    if(family==null || family=="Wiring")check("wiring and actuation consumers",()=>ConstructionWiring(context,host,tools,input));
                    if(family==null || family=="Special")check("special producers and substrates",()=>ConstructionSpecial(context,host,tools,input));
                    if(family==null)check("moving mixed lanes and paint recovery",()=>ConstructionMovingMixed(context,host,tools,input));
                    }
                    if(first==null)check("native inventory manual takeover",()=>ConstructionPreparationManualTakeover(context,host,tools,input));
                    if(first!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
                }
                Console.WriteLine(family!=null?"PASS Blueprint"+family+": declared construction consumer family plus finite native preparation and manual takeover; real full Draw/ordinary startup/multiplayer/FPS/owner acceptance NOT_RUN":
                    preparationOnly?"PASS BlueprintPreparation: finite native ItemSlot inputs across natural Player.Update and locked original UI tail; real full Draw/ordinary startup/multiplayer/FPS/owner acceptance NOT_RUN":
                    "PASS BlueprintConstruction: production ToolUse, natural Player.Update/ItemCheck, locked original UI input tail and ItemSlot; real full Draw/ordinary startup/multiplayer/FPS/owner acceptance NOT_RUN");
            }
            finally
            {
                NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Call(host,"SetAutoBuild",false);Call(host,"ClearProjection");
                constructionChecksActive=false;constructionInputTail=null;
                foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);
                faultTools=null;
                Lighting.Clear();Lighting.Mode=lightingMode;
            }
        }

        private static void ConstructionMaterialPreparation(object context,object host,object tools,object input)
        {
            var p=Reset(context,host,tools,input);
            var preparation=Get(host,"buildMaterialPreparation");
            p.inventory[12].SetDefaults(ItemID.RedPaint);p.inventory[12].stack=2;var desired=p.inventory[12];
            p.inventory[54].SetDefaults(ItemID.RedPaint);p.inventory[54].stack=7;var oldPriority=p.inventory[54];
            var identity=new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"native-input-tail-preparation",(long)Get(Get(tools,"Runtime"),"Generation"),1);
            Step(context,input);
            Require(Main.mouseLeftRelease,"real idle native tail establishes the first inventory click edge");
            long clicks=(long)Get(preparation,"NativeClicks"),neutrals=(long)Get(preparation,"NativeNeutralTails");
            object[] args={p,12,54,identity,-1};
            Require(!(bool)Call(preparation,"TryPrepare",args),"nontrivial material preparation is not ready without native clicks");
            Require((bool)Get(preparation,"HasWork"),"the actual neutral fixture satisfies all native inventory preparation gates");
            for(int i=0;i<8;i++)Call(preparation,"TryPrepare",args);
            Require((long)Get(preparation,"NativeClicks")==clicks && ReferenceEquals(p.inventory[12],desired) && ReferenceEquals(p.inventory[54],oldPriority) && Main.mouseItem.IsAir,
                "preparation polling without the original input tail cannot move any real inventory endpoint");
            constructionInputTail();
            Require((long)Get(preparation,"NativeClicks")==clicks+1 && !Main.mouseLeftRelease && ReferenceEquals(Main.mouseItem,oldPriority),
                "one native draw epoch performs at most one real ItemSlot swap and consumes its actual mouse release edge: clicks="+Get(preparation,"NativeClicks")+
                " baseline="+clicks+" release="+Main.mouseLeftRelease+" cursorIsPriority="+ReferenceEquals(Main.mouseItem,oldPriority));
            bool priorMouseInterface=p.mouseInterface;
            p.mouseInterface=true;
            Require(!((Func<bool>)Get(tools,"CanInterface"))() && ((Func<bool>)Get(tools,"CanInventoryPreparationInterface"))(),
                "controlled native inventory hover keeps normal gameplay admission blocked and uses only the preparation UI gate");
            // A genuine simulation epoch occurs with the real cursor occupied.
            // An inventory preparation must keep that Item reachable through
            // vanilla dropItemCheck too, not just succeed in adjacent draw calls.
            Step(context,input);
            Require(!(bool)Get(preparation,"Unknown") && ReferenceEquals(Main.mouseItem,oldPriority) &&
                (long)Get(preparation,"NativeClicks")==clicks+1 && Main.mouseLeftRelease && (long)Get(preparation,"NativeNeutralTails")==neutrals+1,
                "real Player.Update preserves the owned UI cursor; only its following neutral input tail restores the next click edge: reason="+GetOptional(preparation,"Reason")+
                " cursor="+Main.mouseItem.type+" slots="+Get(preparation,"Slots"));
            Set(input,"HotkeyCapture",true);
            try
            {
                constructionInputTail();
                Require((long)Get(preparation,"NativeClicks")==clicks+1 && (long)Get(preparation,"NativeNeutralTails")==neutrals+1 && ReferenceEquals(Main.mouseItem,oldPriority),
                    "a real shell hotkey capture gate blocks even retained exact inventory preparation without clearing its cursor");
            }
            finally{Set(input,"HotkeyCapture",false);}
            for(int i=0;i<16 && !(bool)Get(preparation,"Ready");i++)Step(context,input);
            Require((bool)Get(preparation,"Ready") && ReferenceEquals(p.inventory[54],desired) && desired.stack==2 && oldPriority.stack==7 && Main.mouseItem.IsAir,
                "same-type priority stack is isolated through finite real native clicks without merging or consuming either stack");
            Require(!Main.playerInventory,"a ready native preparation closes only its own real inventory UI after the cursor is empty");
            // The controlled hover flag above represents DrawInventory's
            // output. Full DoDraw clears it when the UI closes; this CPU tail
            // does not run that unrelated UI stage, so return its fixture input.
            p.mouseInterface=priorMouseInterface;
            Call(preparation,"RequestReturn");
            for(int i=0;i<20 && (bool)Get(preparation,"HasWork");i++)Step(context,input);
            Require(!(bool)Get(preparation,"HasWork") && ReferenceEquals(p.inventory[12],desired) && ReferenceEquals(p.inventory[54],oldPriority) && Main.mouseItem.IsAir &&
                desired.stack==2 && oldPriority.stack==7,"normal preparation return uses the inverse native clicks and releases every finite endpoint");
            p.mouseInterface=true;
            Require(!(bool)Call(preparation,"TryPrepare",args) && !(bool)Get(preparation,"HasWork"),
                "the continuation-only UI exception cannot start a new preparation under a native mouseInterface claim");
            p.mouseInterface=priorMouseInterface;
            // No empty buffer: a real nonstacking third Item must rotate and
            // return through native clicks without granting it supply lineage.
            for(int slot=0;slot<50;slot++){p.inventory[slot].SetDefaults(ItemID.StoneBlock);p.inventory[slot].stack=1;}
            p.inventory[12].SetDefaults(ItemID.RedPaint);p.inventory[12].stack=2;desired=p.inventory[12];
            var original=p.inventory.Take(58).ToArray();
            Step(context,input);args=new object[]{p,12,54,identity,-1};Call(preparation,"TryPrepare",args);
            for(int i=0;i<18 && !(bool)Get(preparation,"Ready");i++)Step(context,input);
            Require((bool)Get(preparation,"Ready") && ReferenceEquals(p.inventory[54],desired) && (bool)Call(preparation,"IsDisplaced",oldPriority),
                "a full main inventory uses one actual nonstacking endpoint while old slot54 stays return-only");
            Call(preparation,"RequestReturn");
            for(int i=0;i<20 && (bool)Get(preparation,"HasWork");i++)Step(context,input);
            Require(!(bool)Get(preparation,"HasWork") && Enumerable.Range(0,58).All(slot=>ReferenceEquals(p.inventory[slot],original[slot])) &&
                desired.stack==2 && oldPriority.stack==7 && Main.mouseItem.IsAir,"full-inventory native rotation returns each original real Item without creating a stack");
        }

        private static void ConstructionPreparationManualTakeover(object context,object host,object tools,object input)
        {
            var p=Reset(context,host,tools,input);var preparation=Get(host,"buildMaterialPreparation");
            p.inventory[12].SetDefaults(ItemID.RedPaint);p.inventory[12].stack=2;
            p.inventory[54].SetDefaults(ItemID.BluePaint);p.inventory[54].stack=7;
            var identity=new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"native-inventory-takeover",(long)Get(Get(tools,"Runtime"),"Generation"),1);
            Step(context,input);object[] args={p,12,54,identity,-1};Call(preparation,"TryPrepare",args);
            for(int i=0;i<3 && Main.mouseItem.IsAir;i++)Step(context,input);
            Require((bool)Get(preparation,"HasWork") && Main.playerInventory && !Main.mouseItem.IsAir,"takeover begins with a genuine owned native cursor");
            var cursor=Main.mouseItem;long clicks=(long)Get(preparation,"NativeClicks");
            p.ToggleInv();
            Require(!(bool)Get(preparation,"HasWork") && !(bool)Get(preparation,"Unknown") && !Main.playerInventory && !(bool)Get(Get(Get(tools,"Items"),"Ownership"),"HasUse") && ReferenceEquals(Main.mouseItem,cursor),
                "the original player inventory command takes control immediately, without automatic close/reopen or moving its cursor");
            constructionInputTail();
            Require((long)Get(preparation,"NativeClicks")==clicks && !Main.playerInventory && ReferenceEquals(Main.mouseItem,cursor),
                "a late native draw tail cannot replay the old inventory click after player takeover");
            // The owner returns its actual cursor through the same native slot
            // command. No fixture restores the old Item array or stack value.
            p.ToggleInv(); ManualPreparationClick(context,input,p,12); p.ToggleInv(); Step(context,input);
            Require(Main.mouseItem.IsAir && p.inventory[12].type==ItemID.RedPaint,"the real manual click leaves a fresh, idle source");
            Call(preparation,"TryPrepare",args);
            for(int i=0;i<18 && !(bool)Get(preparation,"Ready");i++)Step(context,input);
            Require((bool)Get(preparation,"Ready"),"same-session preparation can restart after manual handoff");
            var blue=p.inventory[12];long oldToken=(long)Get(preparation,"token");
            Require(blue.type==ItemID.BluePaint && blue.stack==7,"a confirmed native swap actually displaced the old slot54 identity");
            p.ToggleInv();
            Require(!(bool)Get(preparation,"HasWork") && (bool)Call(preparation,"IsDisplaced",blue) && !(bool)Call(preparation,"Blocks",identity),
                "manual abandonment releases execution and projection duty while retaining the live old54 source exclusion");
            p.ToggleInv();Step(context,input);
            p.inventory[13].SetDefaults(ItemID.GreenPaint);p.inventory[13].stack=2;
            object[] nextArgs={p,13,54,identity,-1};Call(preparation,"TryPrepare",nextArgs);
            var ownership=Get(Get(tools,"Items"),"Ownership");long newToken=(long)Get(preparation,"token");
            Require(newToken>oldToken,"fresh preparation receives a new native owner token");
            Call(ownership,"EndUse",(long)Get(Get(tools,"Runtime"),"Generation"),oldToken);
            Require((bool)Call(ownership,"OwnsUse",(long)Get(Get(tools,"Runtime"),"Generation"),newToken),"a late old token cannot release the fresh receipt");
            for(int i=0;i<18 && !(bool)Get(preparation,"Ready");i++)Step(context,input);
            Require((bool)Get(preparation,"Ready") && (bool)Call(preparation,"IsDisplaced",blue) && blue.stack==7,
                "fresh supply really reaches Ready and cannot overwrite an earlier live displaced identity");
            var red=p.inventory[13];p.ToggleInv();p.ToggleInv();Step(context,input);
            Require((bool)Call(preparation,"IsDisplaced",blue) && (bool)Call(preparation,"IsDisplaced",red) &&
                !(bool)Call(tools,"BuildInventoryEligible",12,blue) && !(bool)Call(tools,"BuildInventoryEligible",13,red),
                "two separate manual interruptions retain both exact old54 identities without active work");
            NativeToolsChecks.Tile(46,42,TileID.Stone);
            var completed=Place(context,host,input,Asset(host,new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Stone}),46,42);
            Until(context,input,()=>!Scene(host).Contains(completed));
            Require(!(bool)Get(preparation,"HasWork") && blue.stack==7,"a complete projection naturally retires while the unrelated residual identity guard remains");
            // A nonmanual inventory relocation is not a new supply grant.
            var oldHotbar=p.inventory[2];p.inventory[2]=blue;p.inventory[12]=oldHotbar;
            Call(preparation,"ObserveInventory",p);
            for(int slot=55;slot<58;slot++){p.inventory[slot].SetDefaults(ItemID.MusketBall+slot-55);p.inventory[slot].stack=p.inventory[slot].maxStack;p.inventory[slot].favorited=true;}
            NativeToolExecutionChecks.Sample(context,input,new Microsoft.Xna.Framework.Vector2(480,540),true);
            ItemSorting.SortInventory();
            Require(ReferenceEquals(p.inventory[2],blue) && (bool)Call(preparation,"IsDisplaced",blue),
                "an actual sort that does not touch a hotbar guard cannot requalify that identity");
            Require(!(bool)Call(preparation,"IsDisplaced",red),"a real main-bag sort returns its actual participating displaced source to player supply");
            ManualPreparationClick(context,input,p,2);ManualPreparationClick(context,input,p,15);
            Require(!(bool)Call(preparation,"IsDisplaced",blue),"actual manual pickup and delivery into the main bag requalify only the involved protected Item");
            Call(preparation,"ClearSession");
            ConstructionPreparationReentry(context,host,tools,input);
        }
        private static void ManualPreparationClick(object context,object input,Player p,int slot)
        {
            NativeToolExecutionChecks.Sample(context,input,new Microsoft.Xna.Framework.Vector2(480,540),false);constructionInputTail();
            NativeToolExecutionChecks.Sample(context,input,new Microsoft.Xna.Framework.Vector2(480,540),true);
            ItemSlot.LeftClick(p.inventory,slot>=54?2:0,slot);
            NativeToolExecutionChecks.Sample(context,input,new Microsoft.Xna.Framework.Vector2(480,540),false);constructionInputTail();
        }
        private static Action preparationReentry;
        private static void PreparationReentry(){var action=preparationReentry;preparationReentry=null;action?.Invoke();}
        private static void ConstructionPreparationReentry(object context,object host,object tools,object input)
        {
            var p=Reset(context,host,tools,input);var preparation=Get(host,"buildMaterialPreparation");
            p.inventory[12].SetDefaults(ItemID.RedPaint);p.inventory[12].stack=2;p.inventory[54].SetDefaults(ItemID.BluePaint);p.inventory[54].stack=7;
            Step(context,input);var identity=new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"native-reentrant-handoff",(long)Get(Get(tools,"Runtime"),"Generation"),2);
            object[] args={p,12,54,identity,-1};Call(preparation,"TryPrepare",args);
            var patch=new Harmony("JueMingR.Tests.PreparationReentry");
            patch.Patch(typeof(ItemSlot).GetMethod("LeftClick",F,null,new[]{typeof(Item[]),typeof(int),typeof(int)},null),
                postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(PreparationReentry)));
            bool reentered=false;
            preparationReentry=()=>{reentered=true;p.ToggleInv();Require((bool)Get(preparation,"Abandoning"),"a synchronous old native click keeps abandonment pending until its own tail exits");};
            try{constructionInputTail();}
            finally{preparationReentry=null;foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);}
            Require(reentered && !(bool)Get(preparation,"HasWork") && !(bool)Get(preparation,"Unknown"),"the old returning LeftClick stack cannot publish Ready or poison a new receipt");
            // Independent unexpected mutation must retain the real Unknown
            // source protection; the manual-only exit cannot unlock it.
            if(!Main.mouseItem.IsAir){if(!Main.playerInventory)p.ToggleInv();ManualPreparationClick(context,input,p,12);if(Main.playerInventory)p.ToggleInv();}
            Step(context,input);Call(preparation,"TryPrepare",args);
            Require((bool)Get(preparation,"HasWork"),"nonmanual counterexample owns a real fresh preparation");
            p.inventory[12].stack++;
            constructionInputTail();
            Require((bool)Get(preparation,"Unknown") && (bool)Get(preparation,"HasWork") && (bool)Call(preparation,"Blocks",identity),
                "an unexplained real endpoint mutation remains Unknown and keeps its original finite protection");
            Call(preparation,"ClearSession");
        }
        private static void ConstructionMovingMixed(object context,object host,object tools,object input)
        {
            var p=Reset(context,host,tools,input);p.position=new Microsoft.Xna.Framework.Vector2(42*16,646);
            p.inventory[12].SetDefaults(ItemID.StoneBlock);p.inventory[12].stack=2;
            p.inventory[13].SetDefaults(ItemID.WoodenHammer);p.inventory[14].SetDefaults(ItemID.Paintbrush);
            p.inventory[15].SetDefaults(ItemID.Wrench);p.inventory[16].SetDefaults(ItemID.Wire);p.inventory[16].stack=2;
            p.inventory[17].SetDefaults(ItemID.StoneWall);p.inventory[17].stack=2;
            p.inventory[18].SetDefaults(ItemID.LivingWoodWand);p.inventory[20].SetDefaults(ItemID.Wood);p.inventory[20].stack=2;
            p.inventory[54].SetDefaults(ItemID.BluePaint);p.inventory[54].stack=7;var oldPriority=p.inventory[54];
            NativeToolsChecks.Tile(44,38,TileID.Stone);
            foreach(int x in new[]{44,46,50})NativeToolsChecks.Tile(x,39,TileID.Stone);
            var entry=Asset(host,new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Stone,Slope=1,TilePaint=1},
                new CaptureCell{X=2,Layers=CaptureLayer.Tile|CaptureLayer.Wall,TileType=TileID.Stone,WallType=WallID.Stone},
                new CaptureCell{X=4,Layers=CaptureLayer.Wire,Wires=1},
                new CaptureCell{X=6,Layers=CaptureLayer.Tile,TileType=(ushort)p.inventory[18].createTile});
            Place(context,host,input,entry,44,38);Call(host,"SetMaterialViewDemand",true);SetAutomatic(host,input,true);
            float minimum=p.position.X,maximum=minimum;int frames=0;bool right=true;
            Item supplied=null;bool sawPreparedInventoryUnavailable=false;
            Action move=()=>
            {
                NativeToolExecutionChecks.Sample(context,input,new Microsoft.Xna.Framework.Vector2(480,540),false);
                if(p.position.X>=50*16)right=false;else if(p.position.X<=42*16)right=true;
                p.controlRight=right;p.controlLeft=!right;PlayerInput.Triggers.Current.Right=right;PlayerInput.Triggers.Current.Left=!right;
                long checks=(long)Get(host,"BuildCandidateChecks"),sources=(long)Get(host,"ProjectionMatchSourceSteps");
                NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");constructionInputTail();frames++;
                minimum=Math.Min(minimum,p.position.X);maximum=Math.Max(maximum,p.position.X);
                Require((long)Get(host,"BuildCandidateChecks")-checks<=64 && (long)Get(host,"ProjectionMatchSourceSteps")-sources<=256 &&
                    (int)Get(host,"materialBuildSourceBudget")>=0,"mixed new lanes share the existing finite source/preflight allowance on each real moving update");
                var preparation=Get(host,"buildMaterialPreparation");
                if(supplied!=null && ReferenceEquals(p.inventory[54],supplied) && ReferenceEquals(p.inventory[21],oldPriority) && (bool)Get(preparation,"HasWork"))
                {
                    // Publication observes the same finite receipt on its next
                    // normal update; never substitute a fabricated slot record.
                    var observed=(BlueprintMaterialInventory)Get(host,"MaterialInventory");
                    if(observed.Slots[21].ItemType==oldPriority.type)
                    {
                        var view=(BlueprintMaterialSnapshot)Call(host,"MaterialWorld",0L);
                        Require(!observed.Slots[21].Eligible && !(bool)Call(tools,"BuildInventoryEligible",21,oldPriority) && !view.InventoryReady,
                            "prepared P keeps its receipt at54; displaced B never gains main supply and the view honestly defers inventory counts");
                        sawPreparedInventoryUnavailable=true;
                    }
                }
            };
            NativeQuickItemChecks.Until(()=>
            {
                move();return Main.tile[44,38].slope()==1 && Main.tile[46,38].active() && Main.tile[46,38].wall==WallID.Stone &&
                    Main.tile[48,38].wire() && Main.tile[50,38].active();
            });
            Require(Main.tile[44,38].color()==0 && Scene(host).Length==1 && maximum-minimum>16 && p.inventory[12].stack==1 && p.inventory[16].stack==1 &&
                p.inventory[17].stack==1 && p.inventory[20].stack==1,
                "real movement through one mixed blueprint completes other body/shape/wire/wand lanes while its missing paint remains pending");
            p.inventory[21].SetDefaults(ItemID.RedPaint);p.inventory[21].stack=2;supplied=p.inventory[21];
            NativeQuickItemChecks.Until(()=>{move();return Scene(host).Length==0 && !(bool)Get(Get(tools,"Use"),"Active");});
            Require(Main.tile[44,38].color()==supplied.paint && supplied.stack==1 && p.inventory[12].stack==1 && p.inventory[16].stack==1 &&
                p.inventory[17].stack==1 && p.inventory[20].stack==1 && sawPreparedInventoryUnavailable && ReferenceEquals(p.inventory[21],supplied) &&
                ReferenceEquals(p.inventory[54],oldPriority) && oldPriority.stack==7,
                "supplementing one real paint stack resumes the unfinished lane without toggling construction or repeating completed consumption, and returns exact supply lineage");
            Call(host,"SetMaterialViewDemand",false);SetAutomatic(host,input,false);
            long settledChecks=(long)Get(host,"BuildCandidateChecks"),settledReach=(long)Get(host,"BuildReachSourceReads"),settledSources=(long)Get(host,"ProjectionMatchSourceSteps");
            for(int i=0;i<24;i++)move();
            Require((long)Get(host,"BuildCandidateChecks")==settledChecks && (long)Get(host,"BuildReachSourceReads")==settledReach &&
                (long)Get(host,"ProjectionMatchSourceSteps")==settledSources,"closed construction with no in-flight receipt performs no new tool, source or preflight work");
            Console.WriteLine("PASS BlueprintConstruction mixed moving frames="+frames+" movement="+(maximum-minimum)+" missingPaintRecovered=true sameBlueprint=true shared64=true closedBusinessReads=0");
        }
        private static void DumpConstructionPreparation(object host,object tools)
        {
            var preparation=Get(host,"buildMaterialPreparation");var p=Main.LocalPlayer;
            Console.Error.WriteLine("PREPARATION phase="+Get(preparation,"phase")+" reason="+GetOptional(preparation,"Reason")+
                " slots="+Get(preparation,"Slots")+" clicks="+Get(preparation,"NativeClicks")+" neutral="+Get(preparation,"NativeNeutralTails")+
                " cursor="+Main.mouseItem.type+":"+Main.mouseItem.stack+" inventoryOpen="+Main.playerInventory+" release="+Main.mouseLeftRelease+
                " startAllowed="+Call(preparation,"StartAllowed",p)+" canClick="+((bool)Get(preparation,"HasWork")?Get(preparation,"CanClickNow"):false)+" cursorOverride="+Main.cursorOverride+
                " input="+Get(Get(tools,"Input"),"CanRunAutomaticActions")+" interface="+((Func<bool>)Get(tools,"CanInterface"))()+
                " selected="+p.selectedItem+" override="+p.selectedItemState.HasActiveOverride+" buffered="+p.selectedItemState.HasBufferedChange+
                " anim/time/reuse="+p.itemAnimation+"/"+p.itemTime+"/"+p.reuseDelay+" mouseInterface="+p.mouseInterface+" pending="+Main.LocalPlayerHasPendingInventoryActions()+
                " slot12Eligible="+Call(tools,"BuildInventoryEligible",12,p.inventory[12])+" slotsOwned="+Get(Get(Get(tools,"Items"),"Ownership"),"HasUse"));
        }
    }
}
