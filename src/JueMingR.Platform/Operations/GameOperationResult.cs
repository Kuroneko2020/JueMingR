using System;

namespace JueMingR.Platform.Operations
{
    public sealed class GameOperationResult
    {
        public GameOperationResult(GameOperationOutcome outcome)
        {
            if (!Enum.IsDefined(typeof(GameOperationOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(nameof(outcome));
            }

            Outcome = outcome;
        }

        public GameOperationOutcome Outcome { get; }
    }
}
