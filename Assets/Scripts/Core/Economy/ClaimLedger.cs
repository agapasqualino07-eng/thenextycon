using System.Collections.Generic;

namespace OfficinaTycoon
{
    /// <summary>
    /// Idempotency ledger for rewards. Each reward has a unique key (e.g. "order:a1", "offline:7");
    /// TryClaim returns true exactly once per key, and the set is persisted, so repeated callbacks,
    /// double taps and restore-then-retry sequences cannot pay a reward twice.
    /// Bounded: oldest keys are dropped after Capacity entries (keys are sequence-numbered, so old
    /// keys cannot be generated again).
    /// </summary>
    public sealed class ClaimLedger
    {
        public const int Capacity = 600;
        readonly HashSet<string> _set = new HashSet<string>();
        readonly List<string> _order = new List<string>();

        public bool Contains(string key) => _set.Contains(key);

        public bool TryClaim(string key)
        {
            if (string.IsNullOrEmpty(key) || !_set.Add(key)) return false;
            _order.Add(key);
            if (_order.Count > Capacity)
            {
                _set.Remove(_order[0]);
                _order.RemoveAt(0);
            }
            return true;
        }

        public List<string> Snapshot() => new List<string>(_order);

        public void Restore(IEnumerable<string> keys)
        {
            _set.Clear(); _order.Clear();
            if (keys == null) return;
            foreach (var k in keys) if (!string.IsNullOrEmpty(k) && _set.Add(k)) _order.Add(k);
        }
    }
}
