using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static void ConstructionHoik(object context, object host, object tools, object input)
        {
            ConstructionHoikMixed(context,host,tools,input);
            HoikNativeReachability(context,host,tools,input);
            foreach(byte slope in new byte[]{3,4})
            {
                ConstructionPlatformBoundary(context,host,tools,input,slope);
                ConstructionPlatformBoundary(context,host,tools,input,slope,true);
            }
            ConstructionHoikPersistence(context,host,tools,input);
            ConstructionHoikRecovery(context,host,tools,input);
        }
        // Setup supplies support/inventory only. All three manufacturing steps
        // below run through the original Player.Update/ItemCheck consumers.
        private static void HoikNativeReachability(object context, object host, object tools, object input)
        {
            foreach (byte slope in new byte[] { 3, 4 })
            {
                var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 4;
                p.inventory[13].SetDefaults(ItemID.WoodenHammer);
                p.inventory[14].SetDefaults(ItemID.WoodPlatform); p.inventory[14].stack = 4;
                p.inventory[15].SetDefaults(ItemID.CopperPickaxe);
                int replacement = p.builderAccStatus[10];
                try
                {
                    p.builderAccStatus[10] = 0;
                    HoikManualUntil(context, input, 12, 44, 42, () => Main.tile[44, 42].active());
                    HoikManualUntil(context, input, 13, 44, 42, () => Main.tile[44, 42].slope() == slope && !Main.tile[44, 42].halfBrick(), true);
                    HoikManualUntil(context, input, 14, 44, 42, () => Main.tile[44, 42].type == TileID.Platforms);
                    Require(Main.tile[44, 42].slope() == slope && p.inventory[12].stack == 3 && p.inventory[14].stack == 3,
                        "original block/hammer/BlockSwap produces platform slope" + slope + " with real consumption");
                    var library=(JueMingR.Features.Blueprint.LibraryState)Get(host,"Library");
                    var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,library,()=>Step(context,input),44,42,1,1,"原生Hoik"+slope);
                    var saved=OpenCapturedAppearance(library,entry);
                    var cell=saved.Cells.Single();var obj=saved.Objects.Single();var raw=Main.tile[44,42];
                    Require(cell.ObjectId>0 && obj.Reliable && obj.Width==1 && obj.Height==1 && cell.Slope==slope && cell.FrameX==raw.frameX && cell.FrameY==raw.frameY &&
                        cell.HalfBrick==raw.halfBrick() && cell.Inactive==raw.inActive() && cell.InvisibleTile==raw.invisibleBlock() && cell.FullbrightTile==raw.fullbrightBlock(),
                        "natural Hoik capture/save/reopen retains complete native frame, shape, appearance and positive object metadata");
                    HoikManualUntil(context, input, 12, 44, 42, () => Main.tile[44, 42].type == TileID.Stone);
                    Require(p.inventory[12].stack == 2, "original reverse platform-to-stone BlockSwap is reachable and consumes a stone");
                    Console.WriteLine("PASS HoikNativeReachability slope=" + slope + " forward=true reverse=true stoneSpent=2 platformSpent=1");
                    p=Reset(context,host,tools,input);p.position.X=42*16;
                    p.inventory[12].SetDefaults(ItemID.StoneBlock);p.inventory[12].stack=2;
                    p.inventory[13].SetDefaults(ItemID.WoodenHammer);p.inventory[14].SetDefaults(ItemID.WoodPlatform);p.inventory[14].stack=2;
                    Place(context,host,input,entry,44,42);SetAutomatic(host,input,true);
                    Until(context,input,()=>Scene(host).Length==0 && !(bool)Get(Get(tools,"Use"),"Active"));
                    Require(Main.tile[44,42].type==TileID.Platforms && Main.tile[44,42].slope()==slope && p.inventory[12].stack==1 && p.inventory[14].stack==1,
                        "positive ObjectId native Hoik capture reaches exact final platform through the same production construction chain");
                }
                finally { p.builderAccStatus[10] = replacement; }
            }
        }

        private static void HoikManualUntil(object context, object input, int slot, int x, int y, Func<bool> done, bool hammer = false)
        {
            var p = Main.LocalPlayer; p.selectedItemState.Select(slot); p.selectedItemState.Update();
            var target = new Vector2(x * 16 + 8, y * 16 + 8);
            for (int frame = 0; frame < 3000; frame++)
            {
                // Each pound gets an actual intervening released update;
                // neither poundRelease nor animation/cooldown is fabricated.
                bool down = !hammer || p.poundRelease;
                NativeToolExecutionChecks.Sample(context, input, target, down);
                NativeQuickItemChecks.BeginWorldStep(); p.Update(0); Call(context, "UpdateRuntime");
                if (done())
                {
                    do
                    {
                        NativeToolExecutionChecks.Sample(context, input, target, false);
                        NativeQuickItemChecks.BeginWorldStep(); p.Update(0); Call(context, "UpdateRuntime");
                    } while (p.itemAnimation > 0 || p.itemTime > 0);
                    return;
                }
            }
            throw new InvalidOperationException("native Hoik step timed out slot=" + slot + " type=" + Main.tile[x, y].type + " slope=" + Main.tile[x,y].slope());
        }
    }
}
