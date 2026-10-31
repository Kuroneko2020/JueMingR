using System;
using System.Collections.Generic;
using System.Threading;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Blueprint
{
    // Game-thread adapter only. Each TryRead reads one gated Tile through
    // capture's existing session/identity/received-section/null gate. Object
    // footprints are returned as values for the Host's budgeted group reader.
    internal sealed class BlueprintWorldMatchSource
    {
        private readonly BlueprintCaptureSource source;
        private readonly int metadataThread = Thread.CurrentThread.ManagedThreadId;
        internal BlueprintWorldMatchSource(Func<bool> session) { source = new BlueprintCaptureSource(session); }
        internal bool TryRead(int x, int y, out BlueprintWorldMatchSample sample)
        {
            CaptureCell cell;
            if (!source.Read(x, y, out cell))
            { sample = new BlueprintWorldMatchSample(false, new CaptureCell { X = x, Y = y }); return false; }
            BlueprintObjectShape shape = source.Describe(cell);
            BlueprintWorldObject obj;
            bool interpreted = BlueprintWorldObjectSemantics.TryCreate(cell, shape, out obj);
            // Legacy selected-cell platforms have no target ObjectId. Grant
            // their X-slot equivalence only after the shared native profile
            // proved this world member; Y still carries its material style.
            bool platform = interpreted && obj != null && cell.TileType < TileID.Sets.Platforms.Length && TileID.Sets.Platforms[cell.TileType];
            // Planter boxes are an independent legacy one-cell writer, with
            // four connection X frames and style in Y; they have no native
            // TileObjectData and never acquire platform/Smart permissions.
            bool planter = interpreted && shape == null && (cell.Layers & CaptureLayer.Tile) != 0 && cell.TileType == TileID.PlanterBox;
            bool frames = platform || planter || shape == null && (cell.Layers & CaptureLayer.Tile) != 0 &&
                cell.TileType < Main.tileFrameImportant.Length && Main.tileFrameImportant[cell.TileType];
            BlueprintWorldFrameKind kind = platform ? BlueprintWorldFrameKind.Platform18 : planter ? BlueprintWorldFrameKind.PlanterBox18 : BlueprintWorldFrameKind.Literal;
            if (frames && !platform && !planter)
            {
                if (cell.TileType == TileID.PalmTree) kind = BlueprintWorldFrameKind.Palm22;
                else if (cell.TileType == TileID.MushroomTrees) kind = BlueprintWorldFrameKind.Mushroom18;
                else if (cell.TileType < TileID.Sets.IsATreeTrunk.Length && TileID.Sets.IsATreeTrunk[cell.TileType]) kind = BlueprintWorldFrameKind.TreeTrunk22;
            }
            short frameX, frameY; BlueprintWorldMatchComparer.NormalizeFrame(cell, kind, out frameX, out frameY);
            sample = new BlueprintWorldMatchSample(true, cell, obj, frames, frameX, frameY, !interpreted, kind, source.WriteState);
            return true;
        }

        // Only the game-thread owner can compile native metadata into a pure
        // write certificate. Recheck the existing complete Match; unknown or
        // in-flight samples cannot gain a certificate. This reads no world cell
        // and retains neither the target DTO nor the observed member values.
        internal bool TryCreateFrameWitness(BlueprintWorldObject target, IReadOnlyList<CaptureCell> targets,
            IReadOnlyList<BlueprintWorldMatchSample> world, bool currentAndComplete, out BlueprintWorldObjectFrameWitness witness,
            BlueprintPlacementCatalog catalog = null, BlueprintSubstitutionPolicy policy = default(BlueprintSubstitutionPolicy))
        {
            witness = null;
            if (Thread.CurrentThread.ManagedThreadId != metadataThread ||
                BlueprintWorldMatchComparer.CompareObject(target, targets, world, currentAndComplete, catalog, policy) != BlueprintWorldMatchState.Match) return false;
            return BlueprintWorldObjectSemantics.TryCreateFrameWitness(world[0].Cell, world[0].Object, out witness);
        }

        // The Host supplies the budgeted, current hinge±1 3x3 union. This
        // factory never reads its halo itself. A complete door Match is still
        // required, and raw seeds must include inactive slots' actual residues.
        internal bool TryCreateDoorWitness(BlueprintWorldObject target, IReadOnlyList<CaptureCell> targets,
            IReadOnlyList<BlueprintWorldMatchSample> occupied, IReadOnlyList<BlueprintWorldMatchSample> union,
            bool currentAndComplete, out BlueprintWorldDoorWitness witness,
            BlueprintPlacementCatalog catalog = null, BlueprintSubstitutionPolicy policy = default(BlueprintSubstitutionPolicy))
        {
            witness = null;
            if (Thread.CurrentThread.ManagedThreadId != metadataThread ||
                BlueprintWorldMatchComparer.CompareObject(target, targets, occupied, currentAndComplete, catalog, policy) != BlueprintWorldMatchState.Match) return false;
            return BlueprintWorldDoorWitness.TryCreate(target, targets, occupied[0].Object, union, out witness, catalog, policy);
        }

        // A target member is already in normalized template coordinates.
        // Applying origin here is the only coordinate translation. Metadata
        // interpretation consumes a private Tile value and never reads Main.tile.
        internal bool TryDescribeTarget(CaptureObject obj, CaptureCell member, int originX, int originY, out BlueprintWorldObject target)
        {
            target = null;
            if (!obj.Reliable || obj.Id <= 0 || member.ObjectId != obj.Id || (member.Layers & CaptureLayer.Tile) == 0 || member.UnreliableObject) return false;
            try
            {
                member.X = checked(member.X + originX); member.Y = checked(member.Y + originY);
                Tile tile = BlueprintWorldObjectSemantics.TileValue(member);
                BlueprintObjectShape shape = BlueprintCaptureSource.DescribeRead(member, tile);
                if (shape == null || !shape.Object.Reliable) return false;
                CaptureObject decoded = shape.Object;
                if (decoded.Type != obj.Type || decoded.X != checked(obj.X + originX) || decoded.Y != checked(obj.Y + originY) ||
                    decoded.Width != obj.Width || decoded.Height != obj.Height || decoded.Style != obj.Style || decoded.Direction != obj.Direction ||
                    decoded.Alternate != obj.Alternate || decoded.PlacementStyle != obj.PlacementStyle || decoded.RandomVariant != obj.RandomVariant) return false;
                return BlueprintWorldObjectSemantics.TryCreate(member, shape, out target);
            }
            catch (ArgumentException) { return false; }
            catch (IndexOutOfRangeException) { return false; }
            catch (OverflowException) { return false; }
        }
    }
}
