using JueMingR.Platform.Operations;

namespace JueMingR.Platform.Guidance
{
    public sealed class MerchantTestRequest : IGameOperationRequest
    {
        public MerchantTestRequest(long session) { Session = session; }
        public long Session { get; }
    }
    public sealed class MerchantTestReceipt
    {
        public MerchantTestReceipt(GameOperationOutcome outcome, string message) { Outcome = outcome; Message = message; }
        public GameOperationOutcome Outcome { get; }
        public string Message { get; }
    }
    public interface IMerchantTestPort { MerchantTestReceipt Execute(MerchantTestRequest request); }
}
