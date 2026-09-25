namespace JueMingR.Features.CoinDeposit
{
    public enum CoinOutcome { None, Unavailable, Rejected, Executing, Completed, Partial, Failed, Unconfirmed, Cancelled }

    public static class CoinRules
    {
        public static bool IsCoin(int type) { return type >= 71 && type <= 74; }
        public static bool TryValue(int type, int stack, out long value)
        {
            value = 0;
            if (!IsCoin(type) || stack < 0) return false;
            long unit = type == 71 ? 1 : type == 72 ? 100 : type == 73 ? 10000 : 1000000;
            try { value = checked(unit * stack); return true; }
            catch (System.OverflowException) { return false; }
        }
        // -1 is unconfirmed, 0 is proved no net transfer (not necessarily no
        // native mutation). Never use zero alone as a first-deposit permit.
        public static long Conserved(long walletBefore, long walletAfter, long bankBefore, long bankAfter)
        {
            if (walletBefore < 0 || walletAfter < 0 || bankBefore < 0 || bankAfter < 0) return -1;
            try
            {
                long removed = checked(walletBefore - walletAfter), added = checked(bankAfter - bankBefore);
                return removed >= 0 && removed == added && checked(walletBefore + bankBefore) == checked(walletAfter + bankAfter) ? removed : -1;
            }
            catch (System.OverflowException) { return -1; }
        }
    }
}
