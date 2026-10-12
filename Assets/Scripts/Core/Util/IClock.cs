using System;

namespace OfficinaTycoon
{
    public interface IClock
    {
        DateTime UtcNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>Test/simulation clock that only moves when told to.</summary>
    public sealed class ManualClock : IClock
    {
        DateTime _now;
        public ManualClock(DateTime startUtc) { _now = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc); }
        public ManualClock() : this(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)) { }
        public DateTime UtcNow => _now;
        public void Advance(double seconds) { _now = _now.AddSeconds(seconds); }
        public void Set(DateTime utc) { _now = DateTime.SpecifyKind(utc, DateTimeKind.Utc); }
    }
}
