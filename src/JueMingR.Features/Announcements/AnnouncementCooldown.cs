namespace JueMingR.Features.Announcements
{
    public sealed class AnnouncementCooldown
    {
        private long lastSeen = -1, ordinary = -1, air = -1;
        public bool TryTake(long milliseconds, bool emptyAir)
        {
            if (milliseconds < 0) return false;
            if (lastSeen >= 0 && milliseconds < lastSeen)
            { ordinary = air = lastSeen = milliseconds; return false; }
            lastSeen = milliseconds;
            // Every accepted message pays the ordinary gate; air additionally
            // has its own longer gate and cannot delay real targets for 2s.
            if (ordinary >= 0 && milliseconds - ordinary < 500 || emptyAir && air >= 0 && milliseconds - air < 2000) return false;
            ordinary = milliseconds; if (emptyAir) air = milliseconds;
            return true;
        }
        public void Clear() { lastSeen = ordinary = air = -1; }
    }
}
