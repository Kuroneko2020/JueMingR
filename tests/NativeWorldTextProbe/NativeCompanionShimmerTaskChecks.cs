using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionShimmerTaskChecks
    {
        internal static void Run(object host, object actor, Player body, object anchor, Action tick)
        {
            var profile = (CompanionProfile)Get(actor, "Profile");
            var identity = new CompanionTaskIdentity(profile.OwnerId, profile.Id, (long)Get(host, "session"),
                (long)Get(actor, "Generation"), profile.DataEpoch);
            var position = body.Bottom;
            var speed = body.velocity;
            var original = body.inventory[21];
            var existing = new HashSet<WorldItem>(Main.item.Where(entry => entry != null && entry.active));
            var originalTiles = new Dictionary<Point, Tile>();
            try
            {
                body.Bottom = new Vector2(77 * 16 + 8, 55 * 16);
                body.velocity = Vector2.Zero;
                Call(Get(actor, "Behavior"), "PositionChanged", body);
                for (int x = 79; x <= 84; x++)
                    for (int y = 47; y < 55; y++)
                    {
                        originalTiles[new Point(x, y)] = Main.tile[x, y];
                        var liquid = new Tile { liquid = 255 };
                        liquid.shimmer(true);
                        Main.tile[x, y] = liquid;
                    }
                int type = ItemID.AngelStatue;
                body.inventory[21] = new Item(type) { stack = 2 };
                int product = ShimmerTransforms.GetTransformToItem(type);
                Require(product > 0 && body.inventory[21].CanShimmer(), "same-version native source confirms a direct shimmer product");
                var query = new Dictionary<string, object> { ["around"] = "companion", ["radius"] = 4, ["empty"] = true, ["offset"] = 24 };
                var view = (CompanionTaskOutcome)Call(host, "TryGetTaskObservation", identity, "observe_tiles", query, anchor);
                string field = view.Detail.Split('；').FirstOrDefault(row => row.StartsWith("(79,53)", StringComparison.Ordinal));
                Require(field != null, "world observation exposes the actual Shimmer cell: " + view.Detail);
                var target = field.Substring(field.LastIndexOf("· ", StringComparison.Ordinal) + 2).Split(' ')[0];
                var step = new CompanionTaskStep("shimmer_item", new Dictionary<string, object> { ["target"] = target, ["item_type"] = type, ["amount"] = 1 });
                var job = (CompanionTaskSnapshot)Call(host, "SubmitTask", identity, "原版微光转换", new[] { step }, anchor, Guid.NewGuid());
                Require(job.State == "accepted", "native shimmer task admission: " + job.Detail);
                for (int frame = 0; frame < 210 && job.IsActive; frame++)
                {
                    tick();
                    for (int i = 0; i < Main.maxItems; i++)
                        if (Main.item[i]?.active == true && !existing.Contains(Main.item[i])) Main.item[i].UpdateItem(i);
                    Call(host, "AdvanceTaskShimmerDrops");
                    job = (CompanionTaskSnapshot)Call(host, "GetTask", profile.OwnerId, profile.Id, job.TaskId);
                }
                Require(job.State == "succeeded" && job.Attempts.Last().Completed == 1 && body.inventory[21].type == type &&
                    body.inventory[21].stack == 1 && Main.item.Any(entry => entry != null && entry.active && !existing.Contains(entry) &&
                        entry.shimmered && entry.type == product && entry.stack == 1),
                    "native drop motion, liquid and world transform consume exactly one chosen actor source: " + job.Detail);
                Console.WriteLine("PASS actual single-player companion throw and native Shimmer transformation with a partial source stack.");
            }
            finally
            {
                Call(host, "StopTask", profile.OwnerId, profile.Id, null);
                foreach (var pair in originalTiles) Main.tile[pair.Key.X, pair.Key.Y] = pair.Value;
                for (int i = 0; i < Main.maxItems; i++)
                    if (Main.item[i]?.active == true && !existing.Contains(Main.item[i])) Main.item[i].TurnToAir();
                body.inventory[21] = original;
                body.Bottom = position;
                body.velocity = speed;
                Call(Get(actor, "Behavior"), "PositionChanged", body);
            }
        }
    }
}
