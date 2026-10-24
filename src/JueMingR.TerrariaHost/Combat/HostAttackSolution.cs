using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Combat
{
    internal enum HostAttackReachability { Unknown, Blocked, Reachable }

    // A representative direction is an input fact, not a full damage permit.
    // Contact remains reserved for the existing dedicated lifecycle solvers.
    internal readonly struct HostAttackSolution
    {
        internal readonly NpcTrajectory Timeline;
        internal readonly Vector2 Point;
        internal readonly bool HasPoint;
        internal readonly HostAttackReachability Reachability;
        internal readonly int ReceiveTick;
        internal readonly AttackContact Contact;
        // Unknown/custom births remain direction-sensitive by default. Only
        // the adapter that measured this exact input can remove that dependency.
        internal readonly bool DirectionIndependent;
        internal HostAttackSolution(NpcTrajectory timeline,Vector2 point,bool hasPoint,HostAttackReachability reachability,int receiveTick=-1,AttackContact contact=null,bool directionIndependent=false)
        {Timeline=timeline;Point=point;HasPoint=hasPoint;Reachability=reachability;ReceiveTick=receiveTick;Contact=contact;DirectionIndependent=directionIndependent;}
        internal static HostAttackSolution FromContact(AttackContact contact)
        {return contact==null?default(HostAttackSolution):new HostAttackSolution(contact.Timeline,new Vector2(contact.AimX,contact.AimY),true,HostAttackReachability.Reachable,contact.ReceiveTick,contact);}
    }
}
