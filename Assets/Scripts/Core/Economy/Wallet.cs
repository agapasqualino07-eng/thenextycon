using System;
using System.Collections.Generic;

namespace OfficinaTycoon
{
    public struct Transaction
    {
        public long Sequence;
        public double Amount;        // positive = credit, negative = debit
        public string Reason;
        public double BalanceAfter;
        public bool Applied;
    }

    /// <summary>
    /// The single authority for the soft currency balance. Every credit/debit in the game goes
    /// through here; nothing else keeps a copy of the balance. Operations are synchronous and
    /// atomic, so two rapid calls can never both spend the same funds.
    /// </summary>
    public sealed class Wallet
    {
        const int LogCapacity = 200;

        readonly double _max;
        readonly List<Transaction> _log = new List<Transaction>();
        long _seq;

        public double Balance { get; private set; }
        public double TotalEarned { get; private set; }
        public double TotalSpent { get; private set; }
        public IReadOnlyList<Transaction> Log => _log;

        /// <summary>Raised after every applied transaction.</summary>
        public event Action<Transaction> Changed;

        public Wallet(double maxBalance, double startingBalance = 0)
        {
            _max = maxBalance;
            Balance = Num.Clamp(startingBalance, 0, maxBalance);
        }

        public bool CanAfford(double amount) => Num.IsFinite(amount) && amount >= 0 && Balance + 1e-9 >= amount;

        /// <summary>Debits amount if (and only if) the balance covers it. Never makes the balance negative.</summary>
        public bool TrySpend(double amount, string reason)
        {
            if (!Num.IsFinite(amount) || amount < 0) { Record(0, reason + " [rejected: invalid amount]", false); return false; }
            if (amount == 0) return true;
            if (!CanAfford(amount)) { Record(-amount, reason + " [rejected: insufficient]", false); return false; }
            Balance = Math.Max(0, Balance - amount);
            TotalSpent += amount;
            Record(-amount, reason, true);
            return true;
        }

        /// <summary>Credits amount, clamped at MaxBalance. Returns the amount actually credited.</summary>
        public double Add(double amount, string reason)
        {
            if (!Num.IsFinite(amount) || amount <= 0) { Record(0, reason + " [rejected: invalid amount]", false); return 0; }
            double room = _max - Balance;
            double applied = Math.Min(amount, Math.Max(0, room));
            Balance += applied;
            TotalEarned = Math.Min(TotalEarned + applied, double.MaxValue / 4);
            Record(applied, reason, applied > 0);
            return applied;
        }

        /// <summary>Restore persisted values (sanitised and clamped).</summary>
        public void Restore(double balance, double totalEarned, double totalSpent)
        {
            Balance = Num.Clamp(balance, 0, _max);
            TotalEarned = Math.Max(0, Num.Sanitize(totalEarned));
            TotalSpent = Math.Max(0, Num.Sanitize(totalSpent));
        }

        void Record(double amount, string reason, bool applied)
        {
            var t = new Transaction { Sequence = ++_seq, Amount = amount, Reason = reason, BalanceAfter = Balance, Applied = applied };
            if (_log.Count >= LogCapacity) _log.RemoveAt(0);
            _log.Add(t);
            if (applied) Changed?.Invoke(t);
        }
    }
}
