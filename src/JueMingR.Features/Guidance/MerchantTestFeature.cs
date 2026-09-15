using System;
using JueMingR.Platform.Guidance;
using JueMingR.Platform.Operations;

namespace JueMingR.Features.Guidance
{
    public sealed class MerchantTestFeature
    {
        private readonly IMerchantTestPort port;
        private MerchantTestRequest pending;
        public MerchantTestFeature(IMerchantTestPort port) { this.port = port; }
        public bool Pending { get { return pending != null; } }
        public MerchantTestReceipt Result { get; private set; }
        public bool Request(long session)
        {
            if (pending != null || session < 0) return false;
            pending = new MerchantTestRequest(session); Result = null; return true;
        }
        public void Cancel()
        {
            if (pending == null) return;
            pending = null; Result = new MerchantTestReceipt(GameOperationOutcome.Cancelled, "召唤已取消。");
        }
        public void Update(long session, bool inputAllowed)
        {
            if (pending == null) return; // No intent means no world/house reads.
            var request = pending; pending = null;
            if (request.Session != session || !inputAllowed)
            { Result = new MerchantTestReceipt(GameOperationOutcome.Cancelled, "召唤已取消。"); return; }
            try { Result = port.Execute(request) ?? new MerchantTestReceipt(GameOperationOutcome.Unconfirmed, "召唤结果未确认；不会自动重试。"); }
            catch (Exception) { Result = new MerchantTestReceipt(GameOperationOutcome.Failed, "召唤出错，结果未确认；不会自动重试。"); }
        }
    }
}
