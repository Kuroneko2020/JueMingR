using System;

namespace JueMingR.Platform.Guidance
{
    [Flags]
    public enum NpcDemand { Basic = 0, Direction = 1, Danger = 2, Housing = 4 }
    // Borrowed, current-update values. Identity is opaque and never persisted.
    public struct GuidanceNpc
    {
        public object Identity;
        public int Slot, Type, NetId, Generation, Rarity, Life, StableIndex;
        public bool Active, Hidden, Boss, Town, Homeless;
        public float X, Y, DrawX, DrawY;
        public int HomeX, HomeY;
        public bool SameIdentity(GuidanceNpc other)
        { return ReferenceEquals(Identity, other.Identity) && Slot == other.Slot && Type == other.Type && NetId == other.NetId && Generation == other.Generation; }
    }
    public interface IGuidanceNpcSource
    {
        int Count { get; }
        bool TryRead(int slot, NpcDemand demand, out GuidanceNpc npc);
    }
    public struct GuidancePylon { public int Type, X, Y; }
    public interface IGuidanceLocationSource
    {
        int PylonCount { get; }
        bool TryPylon(int index, out GuidancePylon pylon);
        bool TryWorld(out int width, out int height, out double surface);
    }
}
