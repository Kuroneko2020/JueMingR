using System;
using System.Linq;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        // This leaf runs inside RunConstruction's disposable EmptyWorld. The
        // oracle is world differences and real carried paint, independent of
        // any production continuation or restoration bookkeeping.
        internal static void ConstructionAppearanceRestoration(object context, object host, object tools, object input)
        {
            Exception first = null;
            Action<string, Action> check = (name, body) =>
            {
                try { body(); Console.WriteLine("PASS BlueprintAppearanceRestoration case=" + name); }
                catch (Exception error)
                {
                    Console.Error.WriteLine("RESTORATION CASE " + name + ": " + error);
                    if (first == null) first = error;
                }
            };
            check("two foregrounds share one restoration paint", () => ConstructionSharedRestorationPaint(context, host, tools, input));
            check("ordinary earlier target cannot spend restoration paint", () => ConstructionOrdinaryRestorationPaint(context, host, tools, input));
            check("native paint sprayer cannot spend restoration paint", () => ConstructionOrdinaryRestorationPaint(context, host, tools, input,true));
            check("foreground coating clear keeps one restoration owner", () => ConstructionForegroundCoatingRecovery(context, host, tools, input));
            check("pause and native movement preserve restoration supply", () => ConstructionRestorationPauseAndMovement(context, host, tools, input));
            if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
        }

        private sealed class AppearanceRecoveryTrace
        {
            private readonly object context, input;
            private readonly Player player;
            private readonly byte red;
            private readonly int[] restoreX;
            private readonly byte[] tile, wall;
            private int paint;
            internal int Clears, Restores, WallClears, Debits;
            internal Action AfterStep;

            internal AppearanceRecoveryTrace(object context, object input, Player player, byte red, params int[] restoreX)
            {
                this.context = context; this.input = input; this.player = player; this.red = red; this.restoreX = restoreX;
                tile = restoreX.Select(x => Main.tile[x, 42].color()).ToArray();
                wall = restoreX.Select(x => Main.tile[x, 42].wallColor()).ToArray();
                paint = Paint;
            }

            internal int Paint
            {
                get
                {
                    // Native inventory[58] mirrors the cursor and is not a
                    // second supply; count main slots0..57 plus the real cursor.
                    return player.inventory.Take(58).Concat(new[] { Main.mouseItem }).Where(item => item != null).Distinct()
                        .Where(item => item.type == ItemID.RedPaint && item.stack > 0).Sum(item => item.stack);
                }
            }

            internal void ObservePickup() { paint = Paint; }

            internal void Step()
            {
                NativeBlueprintBuildChecks.Step(context, input);
                Observe();
                AfterStep?.Invoke();
            }

            internal void Move(int direction)
            {
                NativeToolExecutionChecks.Sample(context, input, new Microsoft.Xna.Framework.Vector2(480, 540), false);
                player.controlLeft = direction < 0; player.controlRight = direction > 0;
                Terraria.GameInput.PlayerInput.Triggers.Current.Left = direction < 0;
                Terraria.GameInput.PlayerInput.Triggers.Current.Right = direction > 0;
                NativeQuickItemChecks.BeginWorldStep(); player.Update(0); Call(context, "UpdateRuntime");
                if (constructionChecksActive) constructionInputTail?.Invoke();
                Observe();
            }

            private void Observe()
            {
                int current = Paint;
                if (current < paint)
                {
                    Debits += paint - current;
                    Console.WriteLine("RESTORATION event=red-debit world=" + Main.GameUpdateCount + " input=" + Get(input, "Frame") +
                        " amount=" + (paint - current) + " remaining=" + current);
                }
                paint = current;
                int unfinished = 0;
                for (int index = 0; index < restoreX.Length; index++)
                {
                    var actual = Main.tile[restoreX[index], 42]; byte color = actual.color(), wallColor = actual.wallColor();
                    if (tile[index] == red && color == 0) { Clears++; Event("foreground-clear", restoreX[index], tile[index], color); }
                    if (tile[index] == 0 && color == red) { Restores++; Event("foreground-restore", restoreX[index], tile[index], color); }
                    if (wall[index] != 0 && wallColor == 0) { WallClears++; Event("wall-clear", restoreX[index], wall[index], wallColor); }
                    tile[index] = color; wall[index] = wallColor;
                    if (color != red) unfinished++;
                }
                Require(unfinished <= current,
                    "every actually cleared selected red foreground remains funded by real red paint: pending=" + unfinished + " available=" + current);
            }

            private void Event(string kind, int x, byte before, byte after)
            {
                Console.WriteLine("RESTORATION event=" + kind + " world=" + Main.GameUpdateCount + " input=" + Get(input, "Frame") +
                    " target=" + x + ",42 before=" + before + " after=" + after + " red=" + Paint);
            }

            internal void Until(Func<bool> condition)
            { NativeQuickItemChecks.Until(() => { Step(); return condition(); }); }
        }

        private static Player SetupRestorationPaint(object context, object host, object tools, object input, out byte red, out byte blue)
        {
            var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
            p.inventory[10].SetDefaults(ItemID.RedPaint); p.inventory[10].stack = 1; red = p.inventory[10].paint;
            p.inventory[11].SetDefaults(ItemID.BluePaint); blue = p.inventory[11].paint; p.inventory[11].TurnToAir();
            p.inventory[14].SetDefaults(ItemID.Paintbrush); p.inventory[16].SetDefaults(ItemID.PaintScraper);
            return p;
        }

        private static void SeedRestorationPaint(int x, byte red, byte blue)
        {
            NativeToolsChecks.Tile(x, 42, TileID.Stone); WorldGen.PlaceWall(x, 42, WallID.Stone);
            WorldGen.paintTile(x, 42, red, false, false); WorldGen.paintWall(x, 42, blue, false, false);
            Require(Main.tile[x, 42].color() == red && Main.tile[x, 42].wallColor() == blue,
                "initial original world has a red selected foreground and blue wall");
        }

        private static CaptureCell RestorationPaintTarget(int x, byte red)
        {
            return new CaptureCell { X = x, Layers = CaptureLayer.Tile | CaptureLayer.Wall,
                TileType = TileID.Stone, WallType = WallID.Stone, TilePaint = red };
        }

        private static void PickUpRestorationItem(Player player, int type)
        {
            var incoming = new Item(); incoming.SetDefaults(type); incoming.stack = 1;
            Require(player.GetItem(incoming, new GetItemSettings(NoText: true, NoSound: true)).IsAir,
                "original GetItem accepts exactly one real refill");
        }
        private static void DumpRestorationState(object host,object tools,Player p,string name)
        {
            Console.Error.WriteLine("RESTORATION state="+name+" position="+p.position+" velocity="+p.velocity+" autoPaint="+p.autoPaint+
                " inventory="+Main.playerInventory+" mouse="+Main.mouseItem.type+":"+Main.mouseItem.stack+" selected="+p.selectedItem);
            DumpConstructionPreparation(host,tools);
            Console.Error.WriteLine("RESTORATION "+RestorationFlightState(host,tools));
            var focus=GetOptional(host,"buildContinuation");var operation=focus==null?null:GetOptional(focus,"Operation");
            Console.Error.WriteLine("RESTORATION focus="+(focus==null?"none":Get(focus,"Group")+":"+(operation==null?"Body":Get(operation,"Kind"))+":"+Get(focus,"Pending")));
            foreach(var owner in Scene(host))if(GetOptional(owner,"Match")!=null)foreach(var range in EligibilityRanges(owner))
            {
                var value=GetOptional(range,"Materials");if(value==null)continue;
                Console.Error.WriteLine("RESTORATION sharedNear="+NearRangeSummary(host,range)+
                    " waits="+Get(value,"BuildWaits")+" forward="+Get(value,"CandidateCursor")+" exhausted="+Get(value,"BuildExhausted"));
            }
        }
        private static string RestorationFlightState(object host,object tools)
        {
            Func<string,string> flight=name=>
            {
                var value=GetOptional(host,name);if(value==null)return name+"=none";
                var target=Get(value,"Target");var operation=GetOptional(value,"Operation");
                return name+"="+Get(value,"Generation")+":"+Get(value,"Checked")+"/"+Get(value,"Rejected")+"/"+Get(value,"Entered")+
                    ":"+Get(target,"WorldX")+","+Get(target,"WorldY")+":"+(operation==null?"Body":Get(operation,"Kind")+"/"+Get(operation,"Mode"));
            };
            var use=Get(tools,"Use");
            return flight("activeBuild")+" "+flight("preparedBuild")+" use="+Get(use,"Operation")+":"+GetOptional(use,"cancelled")+"/"+
                GetOptional(use,"nativeReady")+"/"+GetOptional(use,"pulseFrame");
        }

        private static void ConstructionSharedRestorationPaint(object context, object host, object tools, object input)
        {
            byte red, blue; var p = SetupRestorationPaint(context, host, tools, input, out red, out blue);
            SeedRestorationPaint(44, red, blue); SeedRestorationPaint(46, red, blue);
            Place(context, host, input, Asset(host, RestorationPaintTarget(0, red), RestorationPaintTarget(2, red)), 44, 42);
            var trace = new AppearanceRecoveryTrace(context, input, p, red, 44, 46); SetAutomatic(host, input, true);
            trace.Until(() => trace.Restores == 1 && trace.Paint == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
            for (int frame = 0; frame < 80; frame++) trace.Step();
            Require(trace.Clears == 1 && trace.WallClears == 1 && trace.Debits == 1 &&
                Main.tile[44, 42].color() == red && Main.tile[46, 42].color() == red && Scene(host).Length == 1,
                "one red paint funds one complete recovery while the second original foreground remains intact at stable shortage");
            PickUpRestorationItem(p, ItemID.RedPaint);
            trace.ObservePickup();
            trace.Until(() => Scene(host).Length == 0 && Main.mouseItem.IsAir);
            Require(trace.Clears == 2 && trace.Restores == 2 && trace.WallClears == 2 && trace.Debits == 2 && trace.Paint == 0 &&
                Main.tile[44, 42].color() == red && Main.tile[46, 42].color() == red &&
                Main.tile[44, 42].wallColor() == 0 && Main.tile[46, 42].wallColor() == 0,
                "stationary real refill wakes the second recovery with two exact total debits and complete native world results");
        }

        private static void ConstructionOrdinaryRestorationPaint(object context, object host, object tools, object input,bool paintSprayer=false)
        {
            byte red, blue; var p = SetupRestorationPaint(context, host, tools, input, out red, out blue);
            try
            {
            SeedRestorationPaint(46, red, blue);
            // The earlier source initially lacks its body and material. Only
            // after the real foreground-clear event does original GetItem
            // enable production to place that body and compete for red paint.
            Place(context, host, input, Asset(host,
                new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = red },
                RestorationPaintTarget(2, red)), 44, 42);
            var trace = new AppearanceRecoveryTrace(context, input, p, red, 46); SetAutomatic(host, input, true);
            trace.Until(() => trace.Clears == 1);
            bool guardsChecked=!paintSprayer;
            if(paintSprayer)
            {
                p.armor[3].SetDefaults(ItemID.PaintSprayer);p.builderAccStatus[3]=0;
                int lines=0;string previous=null;
                trace.AfterStep=()=>
                {
                    string state=RestorationFlightState(host,tools)+" animation="+p.itemAnimation+"/"+p.itemTime+" red="+
                        string.Join(",",Enumerable.Range(0,50).Where(supplySlot=>p.inventory[supplySlot].type==ItemID.RedPaint && p.inventory[supplySlot].stack>0)
                            .Select(supplySlot=>supplySlot+":"+p.inventory[supplySlot].stack+":"+Call(tools,"BuildInventoryEligible",supplySlot,p.inventory[supplySlot])));
                    if(state!=previous && lines++<64){Console.WriteLine("RESTORATION sprayer world="+Main.GameUpdateCount+" "+state);previous=state;}
                    int slot=Array.FindIndex(p.inventory,0,50,item=>item!=null && item.type==ItemID.RedPaint && item.stack>0);
                    if(!guardsChecked && slot>=0 && p.autoPaint && p.itemAnimation>0 && (bool)Call(Get(tools,"Use"),"OwnsBuildMaterial",p.inventory[slot]))
                    {
                        var manual=(System.Collections.Generic.HashSet<Item>)Get(Get(Get(tools,"Items"),"World"),"ManualMaterials");
                        Require((bool)Call(tools,"BuildInventoryEligible",slot,p.inventory[slot]),"the exact zero-consumption scrape lease retains its own restoration source during native autoPaint animation");
                        manual.Add(p.inventory[slot]);
                        try { Require(!(bool)Call(tools,"BuildInventoryEligible",slot,p.inventory[slot]),"exact owned restoration dependency does not bypass real manual-material protection"); }
                        finally { manual.Remove(p.inventory[slot]); }
                        bool prior=p.inventoryChestStack[slot];p.inventoryChestStack[slot]=true;
                        try { Require(!(bool)Call(tools,"BuildInventoryEligible",slot,p.inventory[slot]),"exact owned restoration dependency does not bypass chest-transfer protection"); }
                        finally { p.inventoryChestStack[slot]=prior; }
                        guardsChecked=true;Console.WriteLine("RESTORATION sprayer exact zero-consumption dependency keeps manual/chest guards live world="+Main.GameUpdateCount);
                    }
                };
            }
            PickUpRestorationItem(p, ItemID.StoneBlock);
            trace.Until(() => trace.Restores == 1 && Main.tile[44, 42].active() && !(bool)Get(Get(tools, "Use"), "Active"));
            Require(guardsChecked && (!paintSprayer || p.autoPaint) && trace.Debits == 1 && trace.Paint == 0 && Main.tile[44, 42].color() == 0 && Main.tile[46, 42].color() == red &&
                Main.tile[46, 42].wallColor() == 0 && Scene(host).Length == 1,
                "ordinary earlier target may receive its independent body but cannot spend the sole paint required by existing recovery");
            PickUpRestorationItem(p, ItemID.RedPaint);
            trace.ObservePickup();
            trace.Until(() => Scene(host).Length == 0 && Main.mouseItem.IsAir);
            Require(trace.Debits == 2 && trace.Paint == 0 && Main.tile[44, 42].color() == red && Main.tile[46, 42].color() == red,
                "one later real paint completes only the ordinary pending color without copying or reviving restoration supply");
            }
            catch {DumpRestorationState(host,tools,p,"ordinary paintSprayer="+paintSprayer);throw;}
            finally {if(paintSprayer)p.armor[3].TurnToAir();}
        }

        private static void ConstructionForegroundCoatingRecovery(object context, object host, object tools, object input)
        {
            byte red, blue; var p = SetupRestorationPaint(context, host, tools, input, out red, out blue);
            p.inventory[12].SetDefaults(ItemID.GlowPaint); p.inventory[12].stack = 1;
            foreach (int x in new[] { 44, 46 })
            {
                NativeToolsChecks.Tile(x, 42, TileID.Stone); WorldGen.paintTile(x, 42, red, false, false);
                WorldGen.paintCoatTile(x, 42, 1, false, false); WorldGen.paintCoatTile(x, 42, 2, false, false);
            }
            Place(context, host, input, Asset(host,
                new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = red, FullbrightTile = true },
                new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = red, FullbrightTile = true }), 44, 42);
            var trace = new AppearanceRecoveryTrace(context, input, p, red, 44, 46); SetAutomatic(host, input, true);
            trace.Until(() => trace.Restores == 1 && trace.Paint == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
            for (int frame = 0; frame < 80; frame++) trace.Step();
            Require(trace.Clears == 1 && new[] { 44, 46 }.Count(x => Main.tile[x, 42].invisibleBlock()) == 1 &&
                new[] { 44, 46 }.All(x => Main.tile[x, 42].color() == red && Main.tile[x, 42].fullbrightBlock()) &&
                !p.inventory.Any(item => item.type == ItemID.GlowPaint && item.stack > 0) && Scene(host).Length == 1,
                "foreground-only coating removal restores retained red and glow before another foreground can borrow their sole resources");
            PickUpRestorationItem(p, ItemID.RedPaint); PickUpRestorationItem(p, ItemID.GlowPaint); trace.ObservePickup();
            trace.Until(() => Scene(host).Length == 0 && Main.mouseItem.IsAir);
            Require(trace.Clears == 2 && trace.Restores == 2 && trace.Debits == 2 &&
                new[] { 44, 46 }.All(x => Main.tile[x, 42].color() == red && Main.tile[x, 42].fullbrightBlock() && !Main.tile[x, 42].invisibleBlock()) &&
                !p.inventory.Any(item => item.type == ItemID.GlowPaint && item.stack > 0),
                "real paint/coating refills complete both foreground-only restoration chains with exact native costs");
        }

        private static void ConstructionRestorationPauseAndMovement(object context, object host, object tools, object input)
        {
            byte red, blue; var p = SetupRestorationPaint(context, host, tools, input, out red, out blue);
            const int recoveryX=49;p.position.X=45*16;
            NativeToolsChecks.Tile(36, 42, TileID.Stone); SeedRestorationPaint(recoveryX, red, blue);
            NativeToolsChecks.Tile(38, 38, TileID.Stone);
            Require(!p.IsInTileInteractionRange(36, 42, Terraria.DataStructures.TileReachCheckSettings.Simple, p.inventory[14].tileBoost + p.blockRange),
                "ordinary red target begins outside the actual brush range");
            var recovery=RestorationPaintTarget(recoveryX-36,red);recovery.Y=3;
            Place(context, host, input, Asset(host,
                new CaptureCell { Y=3, Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = red },
                new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone },
                recovery), 36, 39);
            var trace = new AppearanceRecoveryTrace(context, input, p, red, recoveryX); SetAutomatic(host, input, true);
            try
            {
                trace.Until(() => trace.Clears == 1); SetAutomatic(host, input, false);
                PickUpRestorationItem(p, ItemID.StoneBlock);
                NativeQuickItemChecks.Until(() => { trace.Move(-1); return
                    p.IsInTileInteractionRange(36,42,Terraria.DataStructures.TileReachCheckSettings.Simple,p.inventory[14].tileBoost+p.blockRange) &&
                    !p.IsInTileInteractionRange(recoveryX,42,Terraria.DataStructures.TileReachCheckSettings.Simple,p.inventory[14].tileBoost+p.blockRange); });
                trace.Move(0);
                Console.WriteLine("RESTORATION native movement out position="+p.position+" velocity="+p.velocity+" brush36=true brush49=false");
                Require(Main.tile[recoveryX, 42].color() == 0 && Main.tile[recoveryX, 42].wallColor() == blue && trace.Paint == 1,
                    "temporary OFF and real movement retain the actual intermediate foreground without spending or rolling back paint");
                SetAutomatic(host, input, true);
                trace.Until(() => Main.tile[38, 39].active() && !(bool)Get(Get(tools, "Use"), "Active"));
                for (int frame = 0; frame < 80; frame++) trace.Step();
                Require(Main.tile[36, 42].color() == 0 && Main.tile[recoveryX, 42].color() == 0 && trace.Paint == 1 && trace.Debits == 0 &&
                    Main.tile[38, 39].type == TileID.Stone && !p.inventory.Take(58).Any(item => item.type == ItemID.StoneBlock && item.stack > 0),
                    "an out-of-range recovery keeps its sole red paint while an independent no-paint body completes after re-enabling");
                NativeQuickItemChecks.Until(() => { trace.Move(1); return trace.Restores == 1; }); trace.Move(0);
                Require(Main.tile[recoveryX, 42].color() == red && Main.tile[recoveryX, 42].wallColor() == 0 && trace.Paint == 0 && trace.Debits == 1,
                    "native return into range wakes the exact original recovery and settles its one real debit");
                PickUpRestorationItem(p, ItemID.RedPaint); trace.ObservePickup();
                NativeQuickItemChecks.Until(() => { trace.Move(-1); return Main.tile[36, 42].color() == red; }); trace.Move(0);
                trace.Until(() => Scene(host).Length == 0 && Main.mouseItem.IsAir);
                Require(trace.Debits == 2 && trace.Paint == 0 && Main.tile[36, 42].color() == red && Main.tile[recoveryX, 42].color() == red,
                    "after recovery, later paint legally completes the other target and the original Match retires the instance");
            }
            catch {DumpRestorationState(host,tools,p,"pause/native movement");throw;}
            finally
            {
                p.controlLeft = p.controlRight = false;
                Terraria.GameInput.PlayerInput.Triggers.Current.Left = Terraria.GameInput.PlayerInput.Triggers.Current.Right = false;
            }
        }
    }
}
