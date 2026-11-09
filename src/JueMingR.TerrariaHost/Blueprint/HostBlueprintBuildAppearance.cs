using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private const int AppearanceCoatingMode = 0x100;
        private BlueprintMaterialInventory appearanceInventory;
        private readonly int[,] appearanceTools = new int[4, 50];
        private readonly int[] appearanceToolCounts = new int[4];
        private readonly int[] appearancePaintNext = new int[50];
        private readonly Dictionary<int, int> appearancePaintHeads = new Dictionary<int, int>();

        private bool EnsureAppearanceInventory(Player p)
        {
            var inventory = MaterialInventory;
            if (inventory == null || !inventory.Ready) return false;
            if (ReferenceEquals(appearanceInventory, inventory)) return true;
            appearanceInventory = inventory;
            Array.Clear(appearanceToolCounts, 0, appearanceToolCounts.Length); appearancePaintHeads.Clear();
            // Extend the already frozen 50-slot observation once per actual
            // inventory change. Candidate discovery never scans 50 slots for
            // every member; execution still verifies each selected live Item.
            foreach (var source in inventory.Slots)
            {
                int slot = source.Slot;
                if (!source.Eligible || slot < 0 || slot >= 50 || slot >= p.inventory.Length) continue;
                var item = p.inventory[slot];
                if (item == null || item.IsAir || item.type != source.ItemType) continue;
                if (item.hammer > 0 && item.pick == 0 && !item.channel && item.shoot <= 0) appearanceTools[0, appearanceToolCounts[0]++] = slot;
                if (item.type == ItemID.Paintbrush || item.type == ItemID.SpectrePaintbrush) appearanceTools[1, appearanceToolCounts[1]++] = slot;
                if (item.type == ItemID.PaintRoller || item.type == ItemID.SpectrePaintRoller) appearanceTools[2, appearanceToolCounts[2]++] = slot;
                if (ItemID.Sets.IsPaintScraper[item.type]) appearanceTools[3, appearanceToolCounts[3]++] = slot;
                if (item.PaintOrCoating)
                {
                    int head; appearancePaintNext[slot] = appearancePaintHeads.TryGetValue(item.type, out head) ? head : -1;
                    appearancePaintHeads[item.type] = slot;
                }
            }
            return true;
        }

        private bool TryPlanAppearanceBuild(BlueprintBuildTarget target, BlueprintWorldMatchSample actual,
            BlueprintBuildLane lane, out BlueprintBuildOperation operation)
        {
            operation = null;
            if (!actual.Available || target == null || MaterialCatalog == null) return false;
            bool tileSame = (target.Cell.Layers & CaptureLayer.Tile) != 0 && BuildOperationBodySame(target, actual);
            if (lane == BlueprintBuildLane.Shape)
            {
                var shape = MaterialCatalog?.TileShape(target.Cell);
                int type = target.Cell.TileType;
                if (!tileSame || target.Cell.Slope > 4 || target.Cell.HalfBrick && target.Cell.Slope != 0 ||
                    shape == null || shape.RequiresObject && !shape.Platform ||
                    !OrdinaryHammerShapeType(type) ||
                    target.Cell.Slope == actual.Cell.Slope && target.Cell.HalfBrick == actual.Cell.HalfBrick) return false;
                // Platforms never produce slope 3/4 in their native hammer
                // cycle. Match can retain an already correct one; manufacturing
                // a temporary block or replacing a different body is not ours.
                if (shape.Platform && target.Cell.Slope > 2) return false;
                operation = new BlueprintBuildOperation {
                    Kind = BlueprintBuildOperationKind.Hammer, Consumer = ToolBuildConsumer.Hammer,
                    Mode = actual.Cell.Slope | (actual.Cell.HalfBrick ? 8 : 0), PrimaryMaximum = 0, HeldMaximum = 0
                };
                return true;
            }
            if (lane != BlueprintBuildLane.Appearance) return false;
            CaptureCell expected; BlueprintWorldObject doorForm; BlueprintDoorBuildProof doorProof;
            if (!TryAppearanceTarget(target, actual, out expected, out tileSame, out doorForm, out doorProof)) return false;
            bool wallSame = (expected.Layers & CaptureLayer.Wall) != 0 &&
                BlueprintMaterialSteps.SameBody(expected, actual, MaterialCatalog,true,substitutionPolicy) == BlueprintBodyState.Same;
            var step = BlueprintConstructionAppearance.Next(expected, actual.Cell, tileSame, wallSame);
            if (step.Action == BlueprintAppearanceAction.BlockedForeground)
            {
                // This is a local wall prerequisite. Other lanes and instances
                // keep their ordinary bounded service while the foreground is
                // outside this projection's restoration contract.
                return false;
            }
            if (!step.HasAction) return false;
            if (step.IsScrape)
            {
                if (!AppearanceRestorationReady(target, player, actual)) return false;
                operation = new BlueprintBuildOperation {
                    Kind = BlueprintBuildOperationKind.Scrape, Consumer = ToolBuildConsumer.PaintScraper,
                    Mode = (int)step.Action, PrimaryMaximum = 0, HeldMaximum = 0, DoorForm = doorForm, DoorProof = doorProof
                };
                return true;
            }
            bool coating = step.Action == BlueprintAppearanceAction.CoatTile || step.Action == BlueprintAppearanceAction.CoatWall;
            int supply = coating ? MaterialCatalog.Coating(step.Mode) : MaterialCatalog.Paint(step.Mode);
            if (supply <= 0) return false;
            operation = new BlueprintBuildOperation {
                Kind = step.IsWall ? BlueprintBuildOperationKind.PaintWall : BlueprintBuildOperationKind.PaintTile,
                Consumer = step.IsWall ? ToolBuildConsumer.PaintRoller : ToolBuildConsumer.Paintbrush,
                Mode = step.Mode | (coating ? AppearanceCoatingMode : 0), SupplyType = supply,
                SupplyDestination = 54, PrimaryMaximum = 1, HeldMaximum = 0, DoorForm = doorForm, DoorProof = doorProof
            };
            return AppearanceRestorationConsumptionReady(target, player, supply, 1, operation);
        }

        private static bool OrdinaryHammerShapeType(int type)
        {
            // IsTilePoundable also admits special trap/track/pipe/pad consumers
            // and doll poses. Only the ordinary shape branch is
            // ours; NonSolidSaveSlopes additionally includes unpoundable 131.
            if (type == TileID.Traps || type == TileID.MinecartTrack || type == TileID.WirePipe ||
                type == TileID.ProjectilePressurePad || type == TileID.DisplayDoll || Main.tileHammer[type] || Main.tileAxe[type]) return false;
            return Main.tileSolid[type] || TileID.Sets.Platforms[type] || TileID.Sets.IsLivingFire[type] || type == TileID.ChimneySmoke;
        }

        private bool TryPrepareAppearanceBuild(BlueprintBuildTarget target, Player p, out BuildFlight result, BuildOperationPlan plan)
        {
            result = null;
            var operation = target.Operation;
            bool outsideReach = false;
            int tool = plan == null ? FindAppearanceTool(target, p, out outsideReach) : PlannedBuildToolSlot(p, plan, buildMaterialPreparation?.HasWork == true);
            if (tool < 0) { WaitForAppearanceAvailability(target, outsideReach); return false; }
            BlueprintWorldMatchSample actual;
            if (!ReadBuildCell(target.WorldX, target.WorldY, out actual))return DeferBuildTarget(target);
            long reads=buildUnavailableReads;
            if(!AppearanceRestorationReady(target,p,actual))return FinishBuildPredicate(target,false,reads);
            reads=buildUnavailableReads;
            if (operation.Kind == BlueprintBuildOperationKind.Hammer && !ShapePreflight(target, p, p.inventory[tool]))
                return FinishBuildPredicate(target,false,reads);
            int primary = -1;
            if (operation.SupplyType > 0)
            {
                if (!AppearanceRestorationConsumptionReady(target, p, operation.SupplyType, operation.PrimaryMaximum)) return false;
                if (!TryPrepareBuildSupply(target, p, operation.SupplyType, operation.SupplyDestination, out primary, plan)) return false;
                if (!PlannedBuildSupplyCurrent(p, plan, primary)) { WaitForDynamicBuild(target); return false; }
                // Native preparation or finite paint conversion can move the tool.
                // Select its current real position, never the pre-swap slot.
                tool = plan == null ? FindAppearanceTool(target, p, out outsideReach) : PlannedBuildToolSlot(p, plan, true);
                if (tool < 0) { WaitForAppearanceAvailability(target, outsideReach); return false; }
            }
            if (!TryCreateOperationFlight(target, p, tool, primary, 0, operation.PrimaryMaximum, out result)) return false;
            result.InputGesture = operation.Kind == BlueprintBuildOperationKind.Hammer && !p.poundRelease ?
                ToolInputGesture.NeutralRelease : ToolInputGesture.HeldPress;
            return true;
        }

        private void WaitForAppearanceTool(BlueprintBuildTarget target)
        {
            // A temporary tool/supply shortage is not a native rejection.
            // Its actual material types / shared tool family wake it;
            // adding a native deadline would delay later steps of this cell.
            // Planning can discover this before assigning target.Operation.
            // Keep the explicit appearance-capability dependency even then.
            WaitForBuild(target, BlueprintBuildWait.Material | BlueprintBuildWait.Capability);
        }

        private void WaitForAppearanceAvailability(BlueprintBuildTarget target, bool outsideReach)
        { if (outsideReach) WaitForBuild(target, BlueprintBuildWait.Player); else WaitForAppearanceTool(target); }

        private int FindAppearanceTool(BlueprintBuildTarget target, Player p, out bool outsideReach, BuildSource sourceHint = null)
        {
            outsideReach = false;
            if (!EnsureAppearanceInventory(p)) return -1;
            int selected = -1, bestTime = int.MaxValue;
            var preferred = sourceHint ?? PreferredBuildSourceHint(p);
            int kind = target.Operation.Kind == BlueprintBuildOperationKind.Hammer ? 0 :
                target.Operation.Kind == BlueprintBuildOperationKind.PaintTile ? 1 : target.Operation.Kind == BlueprintBuildOperationKind.PaintWall ? 2 : 3;
            for (int index = 0; index < appearanceToolCounts[kind]; index++)
            {
                int slot = appearanceTools[kind, index];
                var item = p.inventory[slot];
                if (item == null || item.IsAir || !buildTools.BuildInventoryEligible(slot, item) || !AppearanceToolMatches(target, item)) continue;
                int boost = item.tileBoost + (target.Operation.Kind == BlueprintBuildOperationKind.Hammer ? 0 : p.blockRange);
                if (!p.IsInTileInteractionRange(target.WorldX, target.WorldY, TileReachCheckSettings.Simple, boost)) { outsideReach = true; continue; }
                if (preferred != null && preferred.Slot == slot && ReferenceEquals(preferred.Item, item)) return slot;
                if (item.useTime < bestTime) { selected = slot; bestTime = item.useTime; }
            }
            return selected;
        }

        private static bool AppearanceToolMatches(BlueprintBuildTarget target, Item item)
        {return AppearanceToolMatches(target.Operation,target.Cell,item);}
        private static bool AppearanceToolMatches(BlueprintBuildOperation operation,CaptureCell cell,Item item)
        {
            switch (operation.Kind)
            {
                case BlueprintBuildOperationKind.Hammer:
                    // Pick runs before pounding in ActuallyUseMiningTool. An
                    // ordinary hammer/hamaxe avoids entering that destructive
                    // branch; independent projectile tools have another owner.
                    return item.hammer > 0 && item.pick == 0 && !item.channel && item.shoot <= 0 &&
                        !Main.tileHammer[cell.TileType] && !Main.tileAxe[cell.TileType];
                case BlueprintBuildOperationKind.PaintTile: return item.type == ItemID.Paintbrush || item.type == ItemID.SpectrePaintbrush;
                case BlueprintBuildOperationKind.PaintWall: return item.type == ItemID.PaintRoller || item.type == ItemID.SpectrePaintRoller;
                case BlueprintBuildOperationKind.Scrape: return ItemID.Sets.IsPaintScraper[item.type];
                default: return false;
            }
        }

        private bool ShapePreflight(BlueprintBuildTarget target, Player p, Item tool)
        {
            if (p.noBuilding || !p.IsInTileInteractionRange(target.WorldX, target.WorldY, TileReachCheckSettings.Simple, tool.tileBoost)) return false;
            // CanPoundTile fills null Tile entries. Prove the native neighbor
            // window exists first so the planning query cannot create world
            // objects. This also covers the cycle's diagonal platform reads.
            for (int y = target.WorldY - 1; y <= target.WorldY + 1; y++)
                for (int x = target.WorldX - 1; x <= target.WorldX + 1; x++)
                {
                    BlueprintWorldMatchSample neighbor;
                    if (!ReadBuildCell(x, y, out neighbor) || Main.tile[x, y] == null) return false;
                }
            // The player's pounding consumer checks these separately from
            // CanPoundTile, after spending item time. Wait before starting the
            // use, with the existing world-change / bounded-retry recovery.
            if (WorldGen.IsLockedDoor(Main.tile[target.WorldX, target.WorldY - 1]) ||
                WorldGen.IsLockedDoor(Main.tile[target.WorldX, target.WorldY + 1]))
            { RequestBuildEligibilityRetry(target); return false; }
            var bounds = new Rectangle(target.WorldX * 16, target.WorldY * 16, 16, 16);
            for (int index = 0; index < Main.maxPlayers; index++)
            {
                var occupant = Main.player[index];
                if (occupant != null && occupant.active && !occupant.dead && bounds.Intersects(occupant.Hitbox))
                { RequestBuildEligibilityRetry(target); return false; }
            }
            if (!WorldGen.CanPoundTile(target.WorldX, target.WorldY))
            { RequestBuildEligibilityRetry(target); return false; }
            return true;
        }

        private bool AppearanceRestorationReady(BlueprintBuildTarget target, Player p, BlueprintWorldMatchSample actual)
        {
            CaptureCell expected; bool tileSame; BlueprintWorldObject doorForm;
            if (!TryAppearanceTarget(target, actual, out expected, out tileSame, out doorForm)) return false;
            bool wallSame = (expected.Layers & CaptureLayer.Wall) != 0 && BlueprintMaterialSteps.SameBody(expected, actual, MaterialCatalog,true,substitutionPolicy) == BlueprintBodyState.Same;
            var next = BlueprintConstructionAppearance.Next(expected, actual.Cell, tileSame, wallSame);
            if (next.Action == BlueprintAppearanceAction.BlockedForeground) return false;
            if (!NewAppearanceRestorationReady(target, expected, actual, next)) return false;
            if (!next.RequiresForegroundRestoration) return true;
            if (p == null || !EnsureAppearanceInventory(p)) { WaitForAppearanceTool(target); return false; }
            bool brush = false;
            for (int index = 0; index < appearanceToolCounts[1]; index++)
            {
                int slot = appearanceTools[1, index];
                var item = p.inventory[slot];
                if (item == null || item.IsAir || !buildTools.BuildInventoryEligible(slot, item)) continue;
                if ((item.type == ItemID.Paintbrush || item.type == ItemID.SpectrePaintbrush) &&
                    p.IsInTileInteractionRange(target.WorldX, target.WorldY, TileReachCheckSettings.Simple, item.tileBoost + p.blockRange)) brush = true;
            }
            // Billing and admission share the same current-world clear/restore
            // rules. A foreground-only coating scrape must not be mistaken for
            // a wall repaint, or omit its retained foreground paint entirely.
            if (!wallSame) expected.Layers &= ~CaptureLayer.Wall;
            var required = BlueprintConstructionAppearance.RequiredMaterials(expected, actual.Cell, tileSame, wallSame);
            int tilePaint = required.TilePaint == 0 ? 0 : MaterialCatalog.Paint(required.TilePaint);
            int wallPaint = required.WallPaint == 0 ? 0 : MaterialCatalog.Paint(required.WallPaint);
            int fullbright = MaterialCatalog.Coating(1), invisible = MaterialCatalog.Coating(2);
            if (!brush || required.TilePaint != 0 && !HasAppearanceRestoreSupply(p, tilePaint, required.Count(tilePaint, MaterialCatalog)) ||
                required.WallPaint != 0 && !HasAppearanceRestoreSupply(p, wallPaint, required.Count(wallPaint, MaterialCatalog)) ||
                required.Fullbright > 0 && !HasAppearanceRestoreSupply(p, fullbright, required.Count(fullbright, MaterialCatalog)) ||
                required.Invisible > 0 && !HasAppearanceRestoreSupply(p, invisible, required.Count(invisible, MaterialCatalog)))
            {
                WaitForAppearanceTool(target); WaitForBuild(target, BlueprintBuildWait.Player);
                RecordNearAppearanceRestorationWait(target,tilePaint,wallPaint,required.Fullbright>0?fullbright:0,required.Invisible>0?invisible:0);
                return false;
            }
            return true;
        }

        private bool HasAppearanceRestoreSupply(Player p, int type, int required)
        {
            int slot;
            if (p == null || type <= 0 || !EnsureAppearanceInventory(p)) return false;
            if (appearancePaintHeads.TryGetValue(type, out slot)) for (; slot >= 0; slot = appearancePaintNext[slot])
            {
                var item = p.inventory[slot];
                if (item != null && item.type == type && item.stack > 0 && buildTools.BuildInventoryEligible(slot, item))
                { required -= Math.Min(required, item.stack); if (required == 0) return true; }
            }
            // A real main-bag source may currently occupy native paint slot54.
            // Count only our exact ready/using source there. The displaced old
            // 54 Item remains return-only and never funds restoration.
            int prepared = -1;
            if (buildMaterialPreparation?.TryReadSupply(p, type, 54, out prepared) != true)
            {
                var flight = activeBuild; var use = buildTools?.Use;
                if (flight != null && flight.Token > 0 && flight.Token == use?.Operation && ReferenceEquals(flight.Player, p) &&
                    flight.Target.Identity.SessionGeneration == generation && !IsUnconfirmedTarget(flight.Target))
                    for (int index = 50; index < Math.Min(58, p.inventory.Length); index++)
                    {
                        var item = p.inventory[index];
                        if (item != null && item.type == type && item.stack > 0 && ReferenceEquals(buildMaterialPreparation?.Material, item) && use.OwnsBuildMaterial(item) &&
                            ReferenceEquals(ReadBuildPaintSlot(p, index), item)) { prepared = index; break; }
                    }
            }
            if (prepared >= 50 && prepared < p.inventory.Length)
            {
                var item = p.inventory[prepared];
                if (item != null && item.type == type && item.stack >= required && ReferenceEquals(ReadBuildPaintSlot(p, prepared), item)) return true;
            }
            return false;
        }

        private bool CaptureAppearanceRestoreDependencies(BlueprintBuildTarget target, Player p, List<BuildSource> dependencies, ref ulong slots)
        {
            if (!p.autoPaint || target.Operation?.Kind != BlueprintBuildOperationKind.Scrape) return true;
            BlueprintWorldMatchSample actual; CaptureCell expected; bool same; BlueprintWorldObject form;
            if (!ReadBuildCell(target.WorldX, target.WorldY, out actual))return DeferBuildTarget(target);
            long reads=buildUnavailableReads;
            if(!TryAppearanceTarget(target,actual,out expected,out same,out form))return FinishBuildPredicate(target,false,reads);
            bool wallSame = (expected.Layers & CaptureLayer.Wall) != 0 &&
                BlueprintMaterialSteps.SameBody(expected, actual, MaterialCatalog, true, substitutionPolicy) == BlueprintBodyState.Same;
            var next = BlueprintConstructionAppearance.Next(expected, actual.Cell, same, wallSame);
            if (!next.RequiresForegroundRestoration) return true;
            if (!wallSame) expected.Layers &= ~CaptureLayer.Wall;
            var required = BlueprintConstructionAppearance.RequiredMaterials(expected, actual.Cell, same, wallSame);
            var types = new[] { MaterialCatalog.Paint(required.TilePaint), MaterialCatalog.Paint(required.WallPaint),
                required.Fullbright > 0 ? MaterialCatalog.Coating(1) : 0, required.Invisible > 0 ? MaterialCatalog.Coating(2) : 0 };
            for (int index = 0; index < types.Length; index++)
            {
                int type = types[index]; if (type <= 0) continue;
                bool duplicate = false; for (int prior = 0; prior < index; prior++) duplicate |= types[prior] == type;
                if (duplicate) continue;
                int remaining = required.Count(type, MaterialCatalog), slot;
                if (appearancePaintHeads.TryGetValue(type, out slot)) for (; slot >= 0 && remaining > 0; slot = appearancePaintNext[slot])
                {
                    var item = p.inventory[slot];
                    if (item == null || item.type != type || item.stack <= 0 || !buildTools.BuildInventoryEligible(slot, item)) continue;
                    // This exact native scrape depends on these already
                    // qualified restoration Items. Its zero-consumption lease
                    // prevents its own autoPaint animation from becoming a
                    // foreign-use refusal. Manual/pending guards still apply;
                    // the lease ends with this flight, never with the Focus.
                    if ((slots & (1UL << slot)) == 0)
                    { var source = SnapshotSource(slot, item); source.Maximum = 0; dependencies.Add(source); slots |= 1UL << slot; }
                    remaining -= Math.Min(remaining, item.stack);
                }
                if (remaining > 0)return BlockBuildTarget(target,BlueprintBuildWait.Material|BlueprintBuildWait.Capability);
            }
            return true;
        }

        private bool AppearanceBuildCurrent(BuildFlight flight)
        {
            BlueprintWorldMatchSample actual;
            if (!ReadBuildCell(flight.Target.WorldX, flight.Target.WorldY, out actual)) return false;
            bool tileSame = (flight.Target.Cell.Layers & CaptureLayer.Tile) != 0 && BuildOperationBodySame(flight.Target, actual);
            if (flight.Operation.Kind == BlueprintBuildOperationKind.Hammer)
            {
                if (!tileSame || flight.Target.Cell.Slope == actual.Cell.Slope && flight.Target.Cell.HalfBrick == actual.Cell.HalfBrick ||
                    (actual.Cell.Slope | (actual.Cell.HalfBrick ? 8 : 0)) != flight.Operation.Mode) return false;
            }
            else
            {
                CaptureCell expected; BlueprintWorldObject doorForm;
                if (!TryAppearanceTarget(flight.Target, actual, out expected, out tileSame, out doorForm)) return false;
                bool wallSame = (expected.Layers & CaptureLayer.Wall) != 0 &&
                    BlueprintMaterialSteps.SameBody(expected, actual, MaterialCatalog,true,substitutionPolicy) == BlueprintBodyState.Same;
                var step = BlueprintConstructionAppearance.Next(expected, actual.Cell, tileSame, wallSame);
                if (!CurrentAppearanceStep(flight.Operation, step)) return false;
            }
            if (!AppearanceToolMatches(flight.Target, flight.Source.Item) || !AppearanceRestorationReady(flight.Target, flight.Player, actual)) return false;
            if (flight.Operation.SupplyType > 0 && !AppearanceRestorationConsumptionReady(flight.Target, flight.Player,
                flight.Operation.SupplyType, flight.Operation.PrimaryMaximum)) return false;
            if (flight.Operation.Kind == BlueprintBuildOperationKind.Hammer)
            {
                if (!ShapePreflight(flight.Target, flight.Player, flight.Source.Item)) return false;
                // Only vanilla's real neutral ItemCheck can restore this flag.
                // Reading it advances our gesture, never the native state.
                flight.InputGesture = flight.Player.poundRelease ? ToolInputGesture.HeldPress : ToolInputGesture.NeutralRelease;
            }
            return true;
        }

        private bool CurrentAppearanceStep(BlueprintBuildOperation operation, BlueprintAppearanceStep step)
        {
            if (!step.HasAction) return false;
            if (step.IsScrape) return operation.Kind == BlueprintBuildOperationKind.Scrape && operation.Mode == (int)step.Action;
            bool coating = step.Action == BlueprintAppearanceAction.CoatTile || step.Action == BlueprintAppearanceAction.CoatWall;
            return operation.Kind == (step.IsWall ? BlueprintBuildOperationKind.PaintWall : BlueprintBuildOperationKind.PaintTile) &&
                operation.Mode == (step.Mode | (coating ? AppearanceCoatingMode : 0)) &&
                operation.SupplyType == (coating ? MaterialCatalog.Coating(step.Mode) : MaterialCatalog.Paint(step.Mode));
        }

        private bool NativeAppearanceConsumer(BuildFlight flight, ToolBuildConsumer consumer, Item material = null,
            bool wall = false, bool applyItemAnimation = true)
        {
            if (consumer == ToolBuildConsumer.HittingWall || consumer == ToolBuildConsumer.LongMoss || !AppearanceBuildCurrent(flight)) return false;
            var operation = flight.Operation; var p = flight.Player;
            if (operation.Kind == BlueprintBuildOperationKind.Hammer)
            {
                if (consumer != ToolBuildConsumer.Hammer || p.toolTime != 0 || p.itemAnimation <= 0 || !p.controlUseItem || !p.poundRelease) return false;
            }
            else
            {
                if (!p.ItemTimeIsZero || p.itemAnimation <= 0 || !p.controlUseItem) return false;
                if (consumer == ToolBuildConsumer.ApplyPaint || consumer == ToolBuildConsumer.ApplyCoating)
                {
                    bool coating = (operation.Mode & AppearanceCoatingMode) != 0;
                    if ((operation.Kind != BlueprintBuildOperationKind.PaintTile && operation.Kind != BlueprintBuildOperationKind.PaintWall) ||
                        wall != (operation.Kind == BlueprintBuildOperationKind.PaintWall) || !applyItemAnimation ||
                        coating != (consumer == ToolBuildConsumer.ApplyCoating) || material == null || material.type != operation.SupplyType || material.stack <= 0 ||
                        (coating ? material.paintCoating : material.paint) != (operation.Mode & 255) || !OwnsBuildDependency(flight, material)) return false;
                }
                else if (consumer != operation.Consumer) return false;
            }
            CaptureAppearanceRestorationAttempt(flight);
            flight.Entered = true;
            return true;
        }

        private bool AppearanceBuildApplied(BuildFlight flight)
        {
            BlueprintWorldMatchSample actual;
            if (!ReadBuildCell(flight.Target.WorldX, flight.Target.WorldY, out actual)) return false;
            var operation = flight.Operation;
            if (operation.Kind == BlueprintBuildOperationKind.Hammer)
                return (actual.Cell.Layers & CaptureLayer.Tile) != 0 && actual.Cell.TileType == flight.Target.Cell.TileType &&
                    (actual.Cell.Slope | (actual.Cell.HalfBrick ? 8 : 0)) != operation.Mode;
            if (operation.Kind == BlueprintBuildOperationKind.PaintTile || operation.Kind == BlueprintBuildOperationKind.PaintWall)
            {
                bool wall = operation.Kind == BlueprintBuildOperationKind.PaintWall;
                if (!AppearanceResultBodySame(flight.Target, actual, wall)) return false;
                if ((operation.Mode & AppearanceCoatingMode) == 0) return (wall ? actual.Cell.WallPaint : actual.Cell.TilePaint) == operation.Mode;
                return (operation.Mode & 255) == 1 ? wall ? actual.Cell.FullbrightWall : actual.Cell.FullbrightTile : wall ? actual.Cell.InvisibleWall : actual.Cell.InvisibleTile;
            }
            switch ((BlueprintAppearanceAction)operation.Mode)
            {
                case BlueprintAppearanceAction.ScrapeTilePaint: return AppearanceResultBodySame(flight.Target, actual, false) && actual.Cell.TilePaint == 0;
                case BlueprintAppearanceAction.ScrapeTileCoatings: return AppearanceResultBodySame(flight.Target, actual, false) && !actual.Cell.InvisibleTile && !actual.Cell.FullbrightTile;
                case BlueprintAppearanceAction.ScrapeWallPaint: return AppearanceResultBodySame(flight.Target, actual, true) && actual.Cell.WallPaint == 0;
                case BlueprintAppearanceAction.ScrapeWallCoatings: return AppearanceResultBodySame(flight.Target, actual, true) && !actual.Cell.InvisibleWall && !actual.Cell.FullbrightWall;
                default: return false;
            }
        }

        private bool AppearanceResultBodySame(BlueprintBuildTarget target, BlueprintWorldMatchSample actual, bool wall)
        {
            if ((actual.Cell.Layers & (wall ? CaptureLayer.Wall : CaptureLayer.Tile)) == 0) return false;
            CaptureCell expected; bool tileSame; BlueprintWorldObject doorForm;
            if (!TryAppearanceTarget(target, actual, out expected, out tileSame, out doorForm)) return false;
            if (wall) return (expected.Layers & CaptureLayer.Wall) != 0 && BlueprintMaterialSteps.SameBody(expected,actual,MaterialCatalog,true,substitutionPolicy)==BlueprintBodyState.Same;
            if (doorForm != null) return tileSame;
            return target.Body != null ? SameSubstitutionBody(target.Body,actual.Object) :
                BlueprintMaterialSteps.SameBody(target.Cell,actual,MaterialCatalog,policy:substitutionPolicy)==BlueprintBodyState.Same;
        }
    }
}
