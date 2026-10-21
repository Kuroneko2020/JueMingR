using JueMingR.Platform.Combat;

namespace JueMingR.Features.Movement
{
    // The native held-jump span must finish before any new release edge. Both
    // the live consumer and ordinary forecast use this rule; capabilities and
    // resource consumption remain the native consumer's responsibility.
    public static class MovementJumpRules
    {
        public static bool Release(bool intent,bool down,int remaining,bool opportunity)
        {return intent && !down && remaining<=0 && opportunity;}
        public static MovementJumpPhase Before(MovementJumpPhase phase,bool intent,bool down,float directedVelocity)
        {return !intent || down || phase==MovementJumpPhase.Coast && directedVelocity>=0?MovementJumpPhase.None:phase;}
        public static bool Effective(bool nativeJump,MovementJumpPhase phase)
        {return nativeJump && phase!=MovementJumpPhase.Coast;}
        // Only a pre-registered native start may establish Held. In particular,
        // enabling assistance during an external ascent cannot claim its tail.
        public static MovementJumpPhase After(MovementJumpPhase phase,bool candidate,int before,int remaining,float directedVelocity)
        {
            if(candidate && before<=0 && remaining>0 && directedVelocity<0)return MovementJumpPhase.Held;
            if(phase==MovementJumpPhase.Held && remaining<=0)return directedVelocity<0?MovementJumpPhase.Coast:MovementJumpPhase.None;
            return phase;
        }
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
