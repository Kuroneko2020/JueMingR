namespace JueMingR.TerrariaHost.Companions
{
    internal struct CompanionMobilityFrame
    {
        internal int Move, Vertical, Dash;
        internal bool Jump, Drop, EmergencyHand;
        internal CompanionMovementActivation Activation;
        internal float WorldX, WorldY;
        internal int ItemSlot, Sequence, MountTypePlusOne;
        internal string Status;
    }

    internal enum CompanionMovementActivation
    {
        None, Hook, ReleaseHook, QuickMount, Dismount, QuickCart, Door, Pylon, Rod, Portal, Gravity, Teleporter
    }
}
