namespace JueMingR.Platform.Operations
{
    public enum GameOperationOutcome
    {
        Rejected = 0,
        Succeeded = 1,
        PartiallySucceeded = 2,
        Failed = 3,
        Cancelled = 4,
        TimedOut = 5,
        Unconfirmed = 6
    }
}
