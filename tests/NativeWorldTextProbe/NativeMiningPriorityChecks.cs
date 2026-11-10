using System;
using System.Linq;
using JueMingR.Features.Tools;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeMiningPriorityChecks
    {
        internal static void Run(object host, Player p)
        {
            object mining = Get(host, "Mining"); var assembly = host.GetType().Assembly;
            object support = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Tools.MiningSupport"), true);
            Vector2 oldPosition = p.position; float gravity = p.gravDir;
            int[] hooks = (int[])p.grappling.Clone(); int count = p.grapCount;
            try
            {
                foreach (var q in Main.projectile) q.active = false;
                for (int x = 34; x < 58; x++) for (int y = 34; y < 50; y++) Main.tile[x, y].ClearEverything();
                p.inventory[0].SetDefaults(ItemID.CopperPickaxe); p.selectedItemState.Select(0); p.selectedItemState.Update(); p.HeldItem.pick = 210;
                p.gravDir = 1; p.position = new Vector2(40 * 16 + 8, 43 * 16 - p.height); p.grapCount = 0; p.grappling[0] = -1;
                NativeToolsChecks.Tile(40, 43, 6); NativeToolsChecks.Tile(41, 43, 6); NativeToolsChecks.Tile(43, 41, 6);
                foreach(int x in new[]{46,49,52,55})NativeToolsChecks.Tile(x,41,6);
                // Region member order deliberately starts at a foot. The real
                // selector must choose a feasible ordinary member instead.
                NativeToolsChecks.SetMode(host, 2, 1); Require((bool)Call(mining, "Select", p, 40, 43, 6, false), "priority fixture selects unchanged connected membership");
                Require(Target(mining, p).X == 43, "ordinary candidate wins over both full-width supporting tiles");
                Main.tile[43, 41].ClearEverything(); Main.tile[46, 41].ClearEverything(); Call(mining, "Update");
                var foot = Target(mining, p); Require(foot.Y == 43 && (foot.X == 40 || foot.X == 41), "only feasible support is selected rather than permanently skipped");
                Require(((MiningRegion)Get(mining,"Region")).Count>2,"remote ordinary red members remain selected without blocking deferred fallback");
                int stable = (int)Get(mining, "cursor"); Target(mining, p); Require((int)Get(mining, "cursor") == stable, "same-priority target retains native partial-hit cursor");
                NativeToolsChecks.Tile(43, 41, 6); Call(mining, "Clear"); Require((bool)Call(mining, "Select", p, 40, 43, 6, false), "moving-priority fixture selects");
                p.position.X = 38 * 16; Require(Target(mining, p).Y == 43, "moving off support immediately restores the former foot to ordinary priority");
                p.position.X = 40 * 16 + 8;
                // The native attached AI publishes exactly the used grappling
                // members. Unused zero tail and foreign/retracting slots fail.
                for (int i = 0; i < 2; i++)
                {
                    int x = 43 + i; NativeToolsChecks.Tile(x, 40, 6);
                    Projectile q = Main.projectile[20 + i]; q.SetDefaults(13); q.whoAmI = 20 + i; q.owner = p.whoAmI; q.active = true; q.ai[0] = 2; q.Center = new Vector2(x * 16 + 8, 40 * 16 + 8); q.AI();
                }
                Call(support, "Capture", p);
                Require(p.grapCount == 2 && (bool)Call(support, "Deferred", new MiningPoint(43, 40, 6)) && (bool)Call(support, "Deferred", new MiningPoint(44, 40, 6)), "both actual native attached hooks defer their current anchor tiles");
                Main.projectile[20].owner = p.whoAmI + 1; Main.projectile[21].ai[0] = 1; Call(support, "Capture", p);
                Require(!(bool)Call(support, "Deferred", new MiningPoint(43, 40, 6)) && !(bool)Call(support, "Deferred", new MiningPoint(44, 40, 6)), "foreign/retracting hooks do not defer stale anchors");
                foreach (int type in new[] { 48, 232, 745, 750 }) Require(!(bool)Call(support, "Deferred", new MiningPoint(40, 43, type)), "all four supported static spikes bypass support priority " + type);
                p.grapCount = 0; p.grappling[0] = -1;
                Main.tile[40, 43].halfBrick(true); Main.tile[41, 43].ClearEverything(); p.position.Y += 8; Call(support, "Capture", p);
                Require((bool)Call(support, "Deferred", new MiningPoint(40, 43, 6)), "half-brick surface is current support");
                p.position.Y -= 2; Call(support, "Capture", p); Require(!(bool)Call(support, "Deferred", new MiningPoint(40, 43, 6)), "air gap is not future support");
                Main.tile[40, 43].halfBrick(false); p.gravDir = -1; p.position.Y = 44 * 16; Call(support, "Capture", p);
                Require((bool)Call(support, "Deferred", new MiningPoint(40, 43, 6)), "inverted gravity defers direct ceiling contact");
                p.gravDir = 1; Main.tile[40, 43].slope(1); p.position.Y = 43 * 16 + 8 - p.height; Call(support, "Capture", p);
                Require((bool)Call(support, "Deferred", new MiningPoint(40, 43, 6)), "full-width leading edge on a floor slope is current support");
                Console.WriteLine("PASS E1 production mining priority: full-width/half/slope/inverted current support, native multi-hook anchors, immediate motion, deferred fallback, stable cursor and four spike exceptions.");
            }
            finally
            {
                Call(mining, "Clear"); NativeToolsChecks.SetMode(host, 2, 0); p.position = oldPosition; p.gravDir = gravity;
                p.grapCount = count; Array.Copy(hooks, p.grappling, hooks.Length); Main.projectile[20].active = Main.projectile[21].active = false;
                for (int x = 34; x < 58; x++) for (int y = 34; y < 50; y++) Main.tile[x, y].ClearEverything();
            }
        }
        private static MiningPoint Target(object mining, Player player)
        {
            Require((bool)Call(mining, "FindTarget", player), "production target selection succeeds");
            return ((MiningRegion)Get(mining, "Region"))[(int)Get(mining, "cursor")];
        }
    }
}
