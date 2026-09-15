using System;
using System.Globalization;
using JueMingR.Platform.Settings;

namespace JueMingR.Platform.DeathHistory
{
    // Time is part of the immutable event token, not an identity heuristic.
    // Independent random identity distinguishes two deaths in the same instant.
    public static class DeathEventId
    {
        public static string Create(DateTimeOffset time, Guid nonce)
        { if (nonce == Guid.Empty) throw new ArgumentException("empty-death-id"); return time.UtcTicks.ToString("D19", CultureInfo.InvariantCulture) + "-" + nonce.ToString("N"); }
        public static DateTimeOffset Time(string id, TimeSpan offset)
        {
            long ticks; Guid nonce;
            if (id == null || id.Length != 52 || id[19] != '-' || !Int64.TryParse(id.Substring(0, 19), NumberStyles.None, CultureInfo.InvariantCulture, out ticks) ||
                !Guid.TryParseExact(id.Substring(20), "N", out nonce) || nonce == Guid.Empty) throw new ArgumentException("invalid-death-id");
            var time = new DateTimeOffset(ticks, TimeSpan.Zero).ToOffset(offset);
            if (id != Create(time, nonce)) throw new ArgumentException("noncanonical-death-id"); return time;
        }
    }
    public sealed class DeathFact
    {
        public DeathFact(string eventId, TimeSpan offset, bool hasPosition, float x, float y, string reason)
        {
            EventId = eventId; Time = DeathEventId.Time(eventId, offset);
            HasPosition = hasPosition && !Single.IsNaN(x) && !Single.IsInfinity(x) && !Single.IsNaN(y) && !Single.IsInfinity(y) && x >= 0 && y >= 0;
            X = HasPosition ? x : 0; Y = HasPosition ? y : 0; Reason = reason;
        }
        public string EventId { get; }
        public DateTimeOffset Time { get; }
        public bool HasPosition { get; }
        public float X { get; }
        public float Y { get; }
        public string Reason { get; }
        public bool SameSource(DeathFact other)
        { return other != null && EventId == other.EventId && Time.Offset == other.Time.Offset && HasPosition == other.HasPosition && X == other.X && Y == other.Y && Reason == other.Reason; }
    }
    public sealed class DeathMarker
    {
        public DeathMarker(string eventId, long occurrence, float x, float y)
        { EventId = eventId; Occurrence = occurrence; X = x; Y = y; }
        public string EventId { get; }
        public long Occurrence { get; }
        public float X { get; }
        public float Y { get; }
    }
    // The domain owns formats and commit order; infrastructure only stores
    // bounded immutable bytes under its one root-document lease.
    public interface IDeathArchiveFiles : IDisposable
    {
        PreferenceReadResult ReadRoot();
        PreferenceWriteResult WriteRoot(string expectedIdentity, byte[] bytes);
        byte[] ReadPage(string id);
        string CreatePage(byte[] bytes);
    }
}
