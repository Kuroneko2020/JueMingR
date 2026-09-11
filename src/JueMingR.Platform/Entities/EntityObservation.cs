using System;

namespace JueMingR.Platform.Entities
{
    [Flags]
    public enum EntityObservationDemand { None = 0, Enemy = 1, Critter = 2, Npc = 4 }
    public enum EntitySegmentRole { None, Head, Body, Tail, SharedOwner, SharedMember }

    // A game-thread value, never a live game object or background-work payload.
    // The Host translates only verified native metadata/relationships; Features
    // decide category precedence and whether each requested label is meaningful.
    public struct EntityFact
    {
        public bool Active, DrawEligible, Visible, TownNpc, SkeletonMerchant, Critter, Gold, Friendly, Excluded;
        public string TypeName, GivenName;
        public float X, Y, Height;
        public int Life, LifeMax, HealthOwnerSlot, SharedFamily, NextSlot, PreviousSlot;
        public EntitySegmentRole Role;
    }

    public struct EntityObservation
    {
        private readonly EntityFact[] slots;
        // The source lends its bounded buffer until the next synchronous sample.
        public EntityObservation(EntityFact[] slots, float viewCenterX, float viewCenterY)
        { this.slots = slots ?? throw new ArgumentNullException(nameof(slots)); ViewCenterX = viewCenterX; ViewCenterY = viewCenterY; }
        public int Count { get { return slots == null ? 0 : slots.Length; } }
        public float ViewCenterX { get; private set; }
        public float ViewCenterY { get; private set; }
        public EntityFact this[int index] { get { return slots[index]; } }
    }
    public interface IEntityObservationSource
    {
        bool TryObserve(EntityObservationDemand demand, out EntityObservation observation);
    }
}
