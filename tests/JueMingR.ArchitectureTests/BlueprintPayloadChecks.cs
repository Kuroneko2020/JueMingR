using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;

namespace JueMingR.ArchitectureTests
{
    internal static class BlueprintPayloadChecks
    {
        private static readonly Type Codec = typeof(LibraryState).Assembly.GetType("JueMingR.Features.Blueprint.LibraryCodec", true);
        private static readonly BindingFlags F = BindingFlags.Static | BindingFlags.NonPublic;
        private static byte[] Encode(params CaptureCell[] cells) { return (byte[])Codec.GetMethod("EncodeChunk", F).Invoke(null, new object[] { new CaptureChunk(cells, cells.Length) }); }
        private static CaptureChunk Decode(byte[] bytes) { return (CaptureChunk)Codec.GetMethod("DecodeChunk", F).Invoke(null, new object[] { bytes }); }
        internal static void Check(IList<string> failures)
        {
            try
            {
                var old = new CaptureCell { Layers = CaptureLayer.Tile, TileType = 1 };
                byte[] plain = Encode(old);
                Require(BitConverter.ToInt32(plain, 4) == 1 && Encode(Decode(plain).Cells).SequenceEqual(plain), "payload-free v1 remains byte stable");
                var known = new CaptureCell { X = 1, Layers = CaptureLayer.Tile, TileType = 21,
                    Payload = new BlueprintObjectPayload { NameState = BlueprintPayloadState.Known, Name = "" } };
                byte[] named = Encode(known);
                Require(BitConverter.ToInt32(named, 4) == 2 && Decode(named).Cells[0].Payload.NameState == BlueprintPayloadState.Known &&
                    Decode(named).Cells[0].Payload.Name == "", "explicit empty custom name is v2 and survives round trip");
                known.Payload.Name = "材料箱";
                Require(!Encode(known).SequenceEqual(named), "custom names participate in immutable block identity");
                known.Payload.NameState = BlueprintPayloadState.Unknown; known.Payload.Name = null;
                Require(Decode(Encode(known)).Cells[0].Payload.NameState == BlueprintPayloadState.Unknown, "unreceived is retained and never encoded as default name");
                var entity = new CaptureCell { Layers = CaptureLayer.Tile, TileType = 724,
                    Payload = new BlueprintObjectPayload { EntityState = BlueprintPayloadState.Known, EntityItemType = 2002 } };
                byte[] first = Encode(entity); entity.Payload.EntityItemType++;
                Require(Decode(first).Cells[0].Payload.EntityItemType == 2002 && !Encode(entity).SequenceEqual(first), "equal tile frames retain distinct real item identities");
                var expected = entity; var actual = entity; actual.Payload = default(BlueprintObjectPayload);
                Require(expected.Payload.Compare(actual.Payload) == BlueprintWorldMatchState.Unknown, "Tile before entity is waiting");
                actual.Payload = expected.Payload; actual.Payload.EntityItemType++;
                Require(expected.Payload.Compare(actual.Payload) == BlueprintWorldMatchState.Conflict, "different entity is conflict");
                var wall = known.WithoutTile();
                Require(!wall.Payload.HasValue, "Tile masking removes its payload without naming a coincident wall");
                Require(known.Payload.Compare(default(BlueprintObjectPayload)) == BlueprintWorldMatchState.Unknown &&
                    default(BlueprintObjectPayload).Compare(known.Payload) == BlueprintWorldMatchState.Match, "unknown and old absence are different requirements");
                var pose = new CaptureCell { Layers = CaptureLayer.Tile, TileType = 470,
                    Payload = new BlueprintObjectPayload { PoseState = BlueprintPayloadState.Known, Pose = 8 } };
                Require(Decode(Encode(pose)).Cells[0].Payload.Pose == 8, "all native pose values survive even when equipment is not copied");
                byte[] truncated = named.Take(named.Length - 1).ToArray();
                Reject(() => Decode(truncated), "truncated v2 fails");
                byte[] future = (byte[])named.Clone(); future[4] = 3;
                Reject(() => Decode(future), "future block version fails");
                known.Payload.NameState = BlueprintPayloadState.Known; known.Payload.Name = new string('a', 21);
                Reject(() => Encode(known), "native custom-name length is enforced");
            }
            catch (Exception error) { failures.Add("BlueprintPayload: " + error); }
        }
        private static void Reject(Action run, string reason)
        { bool rejected = false; try { run(); } catch (TargetInvocationException) { rejected = true; } Require(rejected, reason); }
        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    }
}
