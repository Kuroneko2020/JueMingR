using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal enum CompanionMovementDeferral
    {
        None, HandBusy, ReleaseInput, NativeTransition, NativeCondition, EquipmentObservation, PlanningSlice
    }

    internal readonly struct CompanionMovementDelivery
    {
        internal readonly long ActionId, Generation;
        internal readonly Vector2 Position;
        internal readonly int Move, Vertical, Sequence;
        internal readonly bool Jump, Dash, StairFall, InputsDeferred, ToolAdmitted, NativeShapeExit;
        internal readonly CompanionMovementActivation Activation;
        internal readonly CompanionMovementDeferral Deferral;

        internal CompanionMovementDelivery(Player body, CompanionMobilityState native, CompanionMobilityFrame request, CompanionMovementDeferral deferral)
        {
            ActionId = request.ActionId;
            Generation = native.Generation;
            Position = body.position;
            Sequence = request.Sequence;
            Activation = request.Activation;
            Deferral = deferral;
            Move = body.controlLeft ? -1 : body.controlRight ? 1 : 0;
            Vertical = body.controlUp ? -1 : body.controlDown ? 1 : 0;
            Jump = body.controlJump;
            Dash = body.controlDash;
            StairFall = body.stairFall;
            NativeShapeExit = native.ShapeHandExit && native.Action == CompanionMovementActivation.Dismount && native.Prepared;
            ToolAdmitted = !NativeShapeExit && Activation != CompanionMovementActivation.None && native.Sequence == Sequence && native.Action == Activation &&
                (native.Prepared || native.Result == CompanionMobilityResult.Started);
            // Withholding a requested key is distinct from delivering a
            // deliberately neutral update (including native release edges).
            InputsDeferred = request.Move != 0 && Move != request.Move ||
                request.Dash != 0 && !Dash || request.Jump && !Jump ||
                (request.Drop || request.Vertical > 0) && !body.controlDown || request.Vertical < 0 && !body.controlUp;
        }
    }
}
