namespace JueMingR.Features.CoinDeposit
{
    // This is user intent, not an inventory lock or a money ledger. Only a
    // proved new session / explicit off-on / complete empty range retires it.
    public sealed class CoinIntent
    {
        private long session, preference;
        public long Generation { get; private set; }
        public bool Enabled { get; private set; }
        public bool Protected { get; private set; }
        public bool Pending { get; private set; }
        public bool Faulted { get; private set; }
        public void BeginSession(long value)
        {
            if (value == session) return;
            session = value; Protected = Pending = Faulted = false; Generation++;
        }
        public void Configure(bool enabled, long revision)
        {
            if (enabled == Enabled && revision == preference) return;
            if (enabled && !Enabled) Protected = Pending = false;
            Enabled = enabled; preference = revision; Generation++;
        }
        public bool Allows(long currentSession, long generation)
        { return session > 0 && currentSession == session && generation == Generation && Enabled && !Protected && !Pending && !Faulted; }
        public void Withdrawal(long currentSession, long generation, long sourceDecrease, long arrived)
        {
            if (!Enabled || currentSession != session || generation != Generation || sourceDecrease <= 0) return;
            // A known removal without a proved arrival suspends writes. Its
            // native scope owns follow-up; unrelated wallet growth cannot settle it.
            if (arrived == sourceDecrease) { Protected = true; Pending = false; }
            else Pending = true;
            Generation++;
        }
        public void ObserveRange(bool complete, bool any)
        {
            if ((!Protected && !Pending) || !complete || any) return;
            Protected = Pending = false; Generation++;
        }
        public void Invalidate() { Generation++; }
        public void Unconfirmed() { Faulted = true; Generation++; }
    }
}
