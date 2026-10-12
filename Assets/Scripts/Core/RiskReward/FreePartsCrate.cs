using System;
using System.Collections.Generic;
using System.Linq;

namespace OfficinaTycoon
{
    public sealed class CrateResult
    {
        public string EntryId = "", Label = "";
        public CrateRewardKind Kind;
        public double Currency;
        public int Parts;
        public double BoostMultiplier, BoostSeconds;
    }

    /// <summary>
    /// "Cassa gratuita di ricambi". Crates are earned only through gameplay (orders, phase milestones);
    /// there is no way to buy them. Probabilities come from CrateConfig, are validated to sum to 100,
    /// and are exposed (Rows) so the UI can show them before opening. Rewards have no external value.
    /// </summary>
    public sealed class FreePartsCrate
    {
        public struct Row { public string Id, Label; public double Percent; public CrateRewardKind Kind; public double OpsSeconds, BoostMultiplier, BoostSeconds; public int Parts; }

        readonly CrateConfig _c;
        readonly Wallet _wallet;
        readonly ProductionSystem _production;
        readonly ClaimLedger _ledger;
        readonly IRandom _rng;
        readonly GameSignals _signals;
        readonly List<double> _percents;

        public int Available { get; private set; }
        public int OpenCounter { get; private set; }
        public int TotalOpened { get; private set; }
        public int MaxStored => _c.MaxStored;
        public event Action<int, string> Granted;           // count actually stored, source
        public event Action<CrateResult> Opened;

        public FreePartsCrate(GameConfig cfg, Wallet wallet, ProductionSystem production, ClaimLedger ledger, IRandom rng, GameSignals signals)
        {
            _c = cfg.Crate; _wallet = wallet; _production = production; _ledger = ledger; _rng = rng; _signals = signals;
            _percents = _c.Entries.Select(e => e.ProbabilityPercent).ToList();
        }

        public IReadOnlyList<Row> Rows()
            => _c.Entries.Select(e => new Row { Id = e.Id, Label = e.Label, Percent = e.ProbabilityPercent, Kind = e.Kind,
                OpsSeconds = e.OpsSeconds, BoostMultiplier = e.BoostMultiplier, BoostSeconds = e.BoostSeconds, Parts = e.Parts }).ToList();

        /// <summary>Stores earned crates. Extras beyond MaxStored are converted to currency so a reward is never silently lost.</summary>
        public void Grant(int count, string source)
        {
            if (count <= 0) return;
            int room = Math.Max(0, _c.MaxStored - Available);
            int stored = Math.Min(room, count);
            Available += stored;
            int overflow = count - stored;
            if (overflow > 0)
                _wallet.Add(Math.Max(_c.MinCurrency, overflow * _c.OverflowOpsSeconds * _production.BaseOutputPerSecond), "crate_overflow:" + source);
            Granted?.Invoke(stored, source);
        }

        /// <summary>Expected currency value (in production-seconds) of one crate's currency entries — for balance tools.</summary>
        public double ExpectedCurrencyOpsSeconds()
            => _c.Entries.Where(e => e.Kind == CrateRewardKind.Currency).Sum(e => e.ProbabilityPercent / 100.0 * Math.Min(e.OpsSeconds, _c.MaxCurrencyOpsSeconds));

        public CrateResult TryOpen()
        {
            if (Available <= 0) return null;
            string key = "crate:" + OpenCounter;
            Available--; OpenCounter++; TotalOpened++;               // consume first: a repeated call cannot open the same crate again
            if (!_ledger.TryClaim(key)) return null;

            var e = _c.Entries[Probability.Pick(_rng, _percents)];
            var r = new CrateResult { EntryId = e.Id, Label = e.Label, Kind = e.Kind };
            double ops = _production.BaseOutputPerSecond;
            switch (e.Kind)
            {
                case CrateRewardKind.Currency:
                    double cap = Math.Max(_c.MinCurrency, _c.MaxCurrencyOpsSeconds * ops);
                    r.Currency = Math.Min(cap, Math.Max(_c.MinCurrency, e.OpsSeconds * ops));
                    _wallet.Add(r.Currency, key);
                    break;
                case CrateRewardKind.TempBoost:
                    r.BoostMultiplier = e.BoostMultiplier; r.BoostSeconds = e.BoostSeconds;
                    _production.AddBoost("crate", e.BoostMultiplier, e.BoostSeconds);
                    break;
                case CrateRewardKind.Parts:
                    r.Parts = e.Parts;
                    _production.AddParts(e.Parts);
                    break;
            }
            Opened?.Invoke(r);
            _signals.RaiseCrateOpened(e.Id);
            return r;
        }

        public CrateSave Capture() => new CrateSave { Available = Available, OpenCounter = OpenCounter, TotalOpened = TotalOpened };

        public void Restore(CrateSave s)
        {
            Available = 0; OpenCounter = 0; TotalOpened = 0;
            if (s == null) return;
            Available = Math.Max(0, Math.Min(_c.MaxStored, s.Available));
            OpenCounter = Math.Max(0, s.OpenCounter);
            TotalOpened = Math.Max(0, s.TotalOpened);
        }
    }
}
