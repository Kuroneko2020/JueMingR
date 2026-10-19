namespace JueMingR.Features.Movement
{
    // The native held-jump span must finish before any new release edge. Both
    // the live consumer and ordinary forecast use this rule; capabilities and
    // resource consumption remain the native consumer's responsibility.
    public static class MovementJumpRules
    {
        public static bool Release(bool intent,bool down,int remaining,bool opportunity)
        {return intent && !down && remaining<=0 && opportunity;}
    }

    public enum MovementJumpOwner { None, SimulatedJump, FallProtection }

    // One actual update, not a queued action. A higher owner may reserve before
    // writing; once a native consumer can observe a contribution it cannot be
    // stolen and later undone with a player snapshot.
    public struct MovementJumpOwnership
    {
        public MovementJumpOwner Owner {get;private set;}
        public bool Written {get;private set;}
        public bool TryAcquire(MovementJumpOwner owner)
        {
            if(owner==MovementJumpOwner.None || Written)return false;
            if(Owner>owner)return false;
            Owner=owner;return true;
        }
        public bool MarkWritten(MovementJumpOwner owner)
        {if(Owner!=owner || owner==MovementJumpOwner.None)return false;Written=true;return true;}
        public void Reset(){this=default(MovementJumpOwnership);}
    }
}
