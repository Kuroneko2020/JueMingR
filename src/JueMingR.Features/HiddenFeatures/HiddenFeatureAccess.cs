namespace JueMingR.Features.HiddenFeatures
{
    // One composition-root instance lives for the process, independently of
    // world sessions. This is local visibility/operation eligibility, not auth.
    public sealed class HiddenFeatureAccess
    {
        private readonly long[] completed = new long[5];
        private int count;
        private long lastIdentity = -1;
        private long lastTime = -1;
        public bool IsUnlocked { get; private set; }

        // The UI proves a complete, uncancelled title press/release and supplies
        // a monotonically increasing physical press identity and real-time ms.
        // A false return includes later clicks after the sole successful edge.
        public bool TryCompleteTitleClick(long nowMilliseconds, long physicalClickIdentity)
        {
            if (IsUnlocked || nowMilliseconds < 0 || physicalClickIdentity <= lastIdentity) return false;
            lastIdentity = physicalClickIdentity;
            if (nowMilliseconds < lastTime) { count = 0; lastTime = nowMilliseconds; return false; }
            lastTime = nowMilliseconds;
            int expired = 0;
            while (expired < count && nowMilliseconds - completed[expired] > 2000) expired++;
            if (expired > 0)
            {
                for (int i = expired; i < count; i++) completed[i - expired] = completed[i];
                count -= expired;
            }
            completed[count++] = nowMilliseconds;
            if (count < completed.Length) return false;
            IsUnlocked = true; count = 0; return true;
        }

        // Context loss cancels only unfinished gestures. Never persist or reset
        // the successful process edge here; a fresh process constructs Locked.
        public void CancelGesture() { count = 0; }
    }
}
