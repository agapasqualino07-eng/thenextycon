using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OfficinaTycoon.Simulator
{
    /// <summary>Behaviour profile of a simulated player. These are ASSUMPTIONS, not measured behaviour.</summary>
    sealed class Profile
    {
        public string Name;
        public int SessionsPerDay;
        public double SessionMinutes;
        public double TapsPerSecond;
        public bool WatchAds;
        public ExperimentChoice ExperimentalChoice;   // None = decline
        public SurgeChoice SurgeChoice;               // None = ignore
    }

    sealed class Result
    {
        public Profile P;
        public Dictionary<string, double> FirstMachineHours = new Dictionary<string, double>();   // wall-clock hours
        public Dictionary<string, double> FirstMachineActiveMin = new Dictionary<string, double>(); // active minutes played
        public Dictionary<int, double> OpsAtDay = new Dictionary<int, double>();
        public Dictionary<int, double> BalanceAtDay = new Dictionary<int, double>();
        public Dictionary<int, double> EarnedAtDay = new Dictionary<int, double>();
        public double[] PhaseHours = new double[3] { 0, -1, -1 };
        public double LongestNoPurchaseHours;
        public int AdsWatched;
        public double EventNetCurrency;     // payouts minus costs from experimental events
        public int EventsCompleted;
        public double ExpPaid, ExpReturned;
        public int Days;
        public Dictionary<string, double> OrderClaimHours = new Dictionary<string, double>();
    }

    static class Program
    {
        static bool Trace = Environment.GetEnvironmentVariable("SIM_TRACE") == "1";
        static int Main(string[] args)
        {
            int days = 30;
            if (args.Length > 0) int.TryParse(args[0], out days);
            var profiles = new List<Profile>
            {
                new Profile { Name = "Occasionale (3x4min/giorno, con annunci, rifiuta eventi)", SessionsPerDay = 3, SessionMinutes = 4, TapsPerSecond = 1.5, WatchAds = true, ExperimentalChoice = ExperimentChoice.None, SurgeChoice = SurgeChoice.None },
                new Profile { Name = "Regolare (5x6min/giorno, con annunci, usa eventi)", SessionsPerDay = 5, SessionMinutes = 6, TapsPerSecond = 2, WatchAds = true, ExperimentalChoice = ExperimentChoice.Experimental, SurgeChoice = SurgeChoice.Experiment },
                new Profile { Name = "Molto attivo (10x10min/giorno, con annunci, usa eventi)", SessionsPerDay = 10, SessionMinutes = 10, TapsPerSecond = 3, WatchAds = true, ExperimentalChoice = ExperimentChoice.Experimental, SurgeChoice = SurgeChoice.Experiment },
                new Profile { Name = "Regolare SENZA annunci (5x6min, usa eventi)", SessionsPerDay = 5, SessionMinutes = 6, TapsPerSecond = 2, WatchAds = false, ExperimentalChoice = ExperimentChoice.Experimental, SurgeChoice = SurgeChoice.Experiment },
                new Profile { Name = "Regolare, eventi prudenti/garantiti (5x6min, annunci)", SessionsPerDay = 5, SessionMinutes = 6, TapsPerSecond = 2, WatchAds = true, ExperimentalChoice = ExperimentChoice.Prudent, SurgeChoice = SurgeChoice.Guaranteed },
                new Profile { Name = "Regolare che RIFIUTA gli eventi casuali (5x6min, annunci)", SessionsPerDay = 5, SessionMinutes = 6, TapsPerSecond = 2, WatchAds = true, ExperimentalChoice = ExperimentChoice.None, SurgeChoice = SurgeChoice.None },
            };

            var cfg = DefaultConfig.Create();
            var errs = ConfigValidator.Validate(cfg);
            if (errs.Count > 0) { Console.Error.WriteLine("CONFIG INVALID:\n" + string.Join("\n", errs)); return 1; }

            var results = profiles.Select(p => Run(p, days)).ToList();
            Console.WriteLine(Report(cfg, results, days));
            return 0;
        }

        static Result Run(Profile p, int days)
        {
            var cfg = DefaultConfig.Create();
            var clock = new ManualClock();
            var ads = new MockRewardedAdService { Behavior = MockAdBehavior.Complete };
            var rng = new XorShiftRandom(12345);
            var s = new GameSession(cfg, clock, new InMemorySaveStorage(), ads, new NullAnalytics(), rng);
            s.Start();
            var res = new Result { P = p, Days = days };
            double wall = 0, active = 0, lastPurchaseWall = 0;
            var first = new HashSet<string>();
            var claimedAt = res.OrderClaimHours;
            double expCost = 0;

            for (int day = 1; day <= days; day++)
            {
                double dayLen = 24 * 3600.0;
                double awake = 16 * 3600.0;
                double gap = awake / p.SessionsPerDay;
                double sessionLen = p.SessionMinutes * 60.0;
                double t = 0; // seconds into the day
                for (int k = 0; k < p.SessionsPerDay; k++)
                {
                    // wait until the session starts (offline)
                    double startAt = k * gap;
                    double wait = startAt - t;
                    if (wait > 0) { clock.Advance(wait); wall += wait; t += wait; }
                    s.Resume();
                    HandleOffline(s, p, ads, res);
                    int ticks = (int)sessionLen;
                    for (int i = 0; i < ticks; i++)
                    {
                        // taps
                        int taps = (int)Math.Round(p.TapsPerSecond);
                        if (p.TapsPerSecond % 1 != 0 && i % 2 == 0) taps = (int)Math.Ceiling(p.TapsPerSecond); else if (p.TapsPerSecond % 1 != 0) taps = (int)Math.Floor(p.TapsPerSecond);
                        for (int tp = 0; tp < taps; tp++) s.Production.Tap();
                        clock.Advance(1); s.Update(1); wall += 1; active += 1; t += 1;
                        if (Trace && day == 1 && k == 0 && i % 30 == 0)
                            Console.Error.WriteLine($"t={i}s ops={s.Production.BaseOutputPerSecond:0.##} bal={s.Wallet.Balance:0.##} earned={s.Wallet.TotalEarned:0.##} lv=" + string.Join(",", cfg.Machines.Select(m => s.Production.Level(m.Id))) + " tmp=" + s.Production.TempMultiplier);
                        if (i % 3 == 0)
                        {
                            Claim(s, res, wall / 3600);
                            if (Spend(s, p))
                            {
                                lastPurchaseWall = wall;
                            }
                            foreach (var m in cfg.Machines)
                                if (s.Production.Level(m.Id) > 0 && first.Add(m.Id))
                                { res.FirstMachineHours[m.Id] = wall / 3600; res.FirstMachineActiveMin[m.Id] = active / 60; }
                            if (res.PhaseHours[1] < 0 && s.Progression.PhaseIndex >= 1) res.PhaseHours[1] = wall / 3600;
                            if (res.PhaseHours[2] < 0 && s.Progression.PhaseIndex >= 2) res.PhaseHours[2] = wall / 3600;
                            res.LongestNoPurchaseHours = Math.Max(res.LongestNoPurchaseHours, (wall - lastPurchaseWall) / 3600);
                            Events(s, p, res, ref expCost);
                            if (p.WatchAds && s.Ads.CanRequest() == AdFailReason.None && s.Monetization.CanOfferBoost() == AdFailReason.None)
                            {
                                s.Monetization.RequestBoost((ok, r) => { if (ok) res.AdsWatched++; });
                            }
                        }
                    }
                    s.Pause();
                }
                // rest of day offline
                double rest = dayLen - t;
                if (rest > 0) { clock.Advance(rest); wall += rest; }
                res.OpsAtDay[day] = s.Production.BaseOutputPerSecond;
                res.BalanceAtDay[day] = s.Wallet.Balance;
                res.EarnedAtDay[day] = s.Wallet.TotalEarned;
                res.LongestNoPurchaseHours = Math.Max(res.LongestNoPurchaseHours, (wall - lastPurchaseWall) / 3600);
            }
            if (Trace) Console.Error.WriteLine("FINAL " + p.Name + " lv=" + string.Join(",", cfg.Machines.Select(m => s.Production.Level(m.Id))) + " bal=" + s.Wallet.Balance.ToString("0.##e0") + " upg=" + s.Production.Capture().Upgrades.Count + "/" + cfg.Upgrades.Count + " parts=" + s.Production.Parts + " nextcosts=" + string.Join(",", cfg.Machines.Select(m => s.Production.NextCost(m.Id).ToString("0.##e0"))));
            res.ExpPaid = expCost;
            return res;
        }

        static void HandleOffline(GameSession s, Profile p, MockRewardedAdService ads, Result r)
        {
            if (s.Offline.Pending == null) return;
            if (p.WatchAds && s.Monetization.CanOfferOfflineBonus() == AdFailReason.None)
                s.Monetization.RequestOfflineBonus((ok, rs) => { if (ok) r.AdsWatched++; });
            s.ClaimOffline();
        }

        static void Claim(GameSession s, Result res, double wallHours)
        {
            foreach (var v in s.Orders.Views().Where(v => v.Status == OrderStatus.Completed).ToList()) { if (s.Orders.TryClaim(v.Def.Id)) res.OrderClaimHours[v.Def.Id] = wallHours; }
            while (s.Crates.Available > 0) s.Crates.TryOpen();
        }

        // Greedy by payback time (cost / extra output per second); buys when affordable, otherwise saves.
        static bool Spend(GameSession s, Profile p)
        {
            var prod = s.Production; var cfg = s.Config;
            string bestId = null; bool bestIsUpgrade = false; double bestPayback = double.MaxValue; double bestCost = 0;
            double ops = prod.BaseOutputPerSecond;
            foreach (var m in cfg.Machines)
            {
                if (!prod.IsUnlocked(m.Id) || prod.Level(m.Id) >= m.MaxLevel) continue;
                int lvl = prod.Level(m.Id);
                double cost = prod.NextCost(m.Id);
                double delta = lvl > 0 ? prod.MachineOutput(m.Id) / (prod.TempMultiplier) / lvl : m.BaseOutput;
                // milestone jumps make some levels extra valuable
                if (cfg.Economy.MilestoneEvery > 0 && (lvl + 1) % cfg.Economy.MilestoneEvery == 0) delta *= 1 + lvl;
                double pb = cost / delta;
                if (pb < bestPayback) { bestPayback = pb; bestId = m.Id; bestIsUpgrade = false; bestCost = cost; }
            }
            foreach (var u in cfg.Upgrades)
            {
                if (!prod.IsUpgradeAvailable(u.Id)) continue;
                double delta;
                switch (u.Effect)
                {
                    case UpgradeEffect.GlobalMultiplier: delta = ops * (u.Value - 1); break;
                    case UpgradeEffect.MachineMultiplier: delta = prod.MachineOutput(u.TargetMachineId) / prod.TempMultiplier * (u.Value - 1); break;
                    default: delta = ops * 0.02; break; // offline/tap upgrades: modest assumed value
                }
                if (u.Effect == UpgradeEffect.TapMultiplier) delta = prod.TapValue * p.TapsPerSecond * (u.Value - 1);
                if (delta <= 0) continue;
                double pb = u.Cost / delta;
                if (pb < bestPayback) { bestPayback = pb; bestId = u.Id; bestIsUpgrade = true; bestCost = u.Cost; }
            }
            // Policy: wait for the best-payback item only if it is affordable within 15 minutes; otherwise buy the best affordable one.
            if (bestId != null && s.Wallet.Balance < bestCost && ops > 0 && (bestCost - s.Wallet.Balance) / ops > 900)
            {
                string alt = null; bool altUp = false; double altPb = double.MaxValue;
                foreach (var m in cfg.Machines)
                {
                    if (!prod.IsUnlocked(m.Id) || prod.Level(m.Id) >= m.MaxLevel) continue;
                    double cost = prod.NextCost(m.Id); if (cost > s.Wallet.Balance) continue;
                    int lvl = prod.Level(m.Id);
                    double delta = lvl > 0 ? prod.MachineOutput(m.Id) / prod.TempMultiplier / lvl : m.BaseOutput;
                    if (cost / delta < altPb) { altPb = cost / delta; alt = m.Id; altUp = false; }
                }
                foreach (var u in cfg.Upgrades)
                {
                    if (!prod.IsUpgradeAvailable(u.Id) || u.Cost > s.Wallet.Balance) continue;
                    double delta = u.Effect == UpgradeEffect.GlobalMultiplier ? ops * (u.Value - 1)
                        : u.Effect == UpgradeEffect.MachineMultiplier ? prod.MachineOutput(u.TargetMachineId) / prod.TempMultiplier * (u.Value - 1) : ops * 0.02;
                    if (delta > 0 && u.Cost / delta < altPb) { altPb = u.Cost / delta; alt = u.Id; altUp = true; }
                }
                if (alt != null) { bestId = alt; bestIsUpgrade = altUp; bestCost = 0; }
            }
            // overhaul with parts on the strongest machine
            foreach (var m in cfg.Machines.OrderByDescending(m => prod.MachineOutput(m.Id)))
                if (prod.TryOverhaul(m.Id)) break;
            if (bestId == null) return false;
            if (s.Wallet.Balance < bestCost) return false;
            return bestIsUpgrade ? prod.TryBuyUpgrade(bestId) : prod.TryBuy(bestId);
        }

        static void Events(GameSession s, Profile p, Result r, ref double expCost)
        {
            var e = s.Experimental;
            if (e.Stage == EventStage.Offered)
            {
                if (p.ExperimentalChoice == ExperimentChoice.None) e.TryDecline();
                else
                {
                    var pv = e.GetPreview();
                    double cost = p.ExperimentalChoice == ExperimentChoice.Prudent ? pv.CostPrudent : pv.CostExperimental;
                    // only invests when it leaves a safety margin of at least the cost (sensible player)
                    if (s.Wallet.Balance >= cost * 1.5 && e.TryAccept(p.ExperimentalChoice, cost)) { expCost += cost; }
                    else e.TryDecline();
                }
            }
            if (e.Stage == EventStage.ReadyToCollect)
            {
                double payout = e.PendingPayout;
                if (e.TryCollect()) { r.ExpReturned += payout; r.EventsCompleted++; }
            }
            if (s.Surge.Stage == EventStage.Offered)
            {
                if (p.SurgeChoice == SurgeChoice.None) s.Surge.TryDismiss();
                else if (s.Surge.TryChoose(p.SurgeChoice)) r.EventsCompleted++;
            }
        }

        // ------------------------------------------------------------------ report
        static string Fmt(double v)
        {
            if (v < 1000) return v.ToString("0.#");
            string[] suf = { "", "K", "M", "B", "T", "Qa", "Qi" };
            int i = 0; while (v >= 1000 && i < suf.Length - 1) { v /= 1000; i++; }
            return v.ToString("0.##") + suf[i];
        }

        static string Report(GameConfig cfg, List<Result> rs, int days)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Risultati simulazione economia");
            sb.AppendLine();
            sb.AppendLine("> Generato da `dotnet run --project Tools/Simulator -- " + days + "` con `GameSession` reale (stesse formule del gioco).");
            sb.AppendLine("> I profili sono ipotesi di comportamento, **non** dati di retention o ricavi reali.");
            sb.AppendLine();
            var ex = new ExperimentalMachineEvent(cfg, new Wallet(1e24), new ProductionSystem(cfg, new Wallet(1e24), new GameSignals()), new ClaimLedger(), new XorShiftRandom(1), new GameSignals());
            var su = new ProductionSurgeEvent(cfg, new ProductionSystem(cfg, new Wallet(1e24), new GameSignals()), new ClaimLedger(), new XorShiftRandom(1), new GameSignals());
            sb.AppendLine("## Valore atteso delle meccaniche casuali");
            sb.AppendLine();
            sb.AppendLine("| Meccanica | Opzione | Valore atteso |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine("| Macchinario sperimentale | Collaudo prudente | " + ex.PrudentExpectedMultiple.ToString("0.000") + "x il costo (esito certo) |");
            sb.AppendLine("| Macchinario sperimentale | Test sperimentale | " + ex.ExperimentalExpectedMultiple.ToString("0.000") + "x il costo (perdita massima " + ((1 - cfg.Experimental.Outcomes.Min(o => o.ReturnMultiplier)) * 100).ToString("0") + "% del costo) |");
            sb.AppendLine("| Produzione fuori scala | Bonus garantito | " + su.GuaranteedValue.ToString("0.0") + " secondi-produzione extra |");
            sb.AppendLine("| Produzione fuori scala | Esperimento | " + su.ExperimentExpectedValue.ToString("0.0") + " secondi-produzione extra |");
            var cr = new FreePartsCrate(cfg, new Wallet(1e24), new ProductionSystem(cfg, new Wallet(1e24), new GameSignals()), new ClaimLedger(), new XorShiftRandom(1), new GameSignals());
            sb.AppendLine("| Cassa gratuita | Fondi (valore atteso valuta) | " + cr.ExpectedCurrencyOpsSeconds().ToString("0.0") + " secondi di produzione per cassa |");
            sb.AppendLine();

            sb.AppendLine("## Tempo al primo acquisto di ogni macchinario (ore reali dall'inizio / minuti attivi di gioco)");
            sb.AppendLine();
            sb.Append("| Profilo |");
            foreach (var m in cfg.Machines) sb.Append(" " + m.Name + " |");
            sb.AppendLine();
            sb.Append("|---|");
            foreach (var m in cfg.Machines) sb.Append("---|");
            sb.AppendLine();
            foreach (var r in rs)
            {
                sb.Append("| " + r.P.Name + " |");
                foreach (var m in cfg.Machines)
                    sb.Append(r.FirstMachineHours.ContainsKey(m.Id) ? " " + r.FirstMachineHours[m.Id].ToString("0.0") + " h / " + r.FirstMachineActiveMin[m.Id].ToString("0") + " min |" : " non raggiunto |");
                sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine("## Produzione di base al secondo, saldo e fasi");
            sb.AppendLine();
            sb.AppendLine("| Profilo | Giorno 1 | Giorno 3 | Giorno 7 | Giorno 14 | Giorno " + days + " | Fase 2 raggiunta (h) | Fase 3 raggiunta (h) | Max ore senza acquisti | Annunci guardati | Eventi completati | Eventi sperimentali: speso -> restituito |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var r in rs)
            {
                string Ops(int d) => d <= days ? Fmt(r.OpsAtDay[d]) : "-";
                sb.AppendLine("| " + r.P.Name + " | " + Ops(1) + " | " + Ops(3) + " | " + Ops(7) + " | " + Ops(14) + " | " + Fmt(r.OpsAtDay[days]) + " | " +
                    (r.PhaseHours[1] >= 0 ? r.PhaseHours[1].ToString("0.0") : "mai") + " | " + (r.PhaseHours[2] >= 0 ? r.PhaseHours[2].ToString("0.0") : "mai") + " | " +
                    r.LongestNoPurchaseHours.ToString("0.0") + " | " + r.AdsWatched + " | " + r.EventsCompleted + " | " + Fmt(r.ExpPaid) + " -> " + Fmt(r.ExpReturned) + " |");
            }
            sb.AppendLine();
            sb.AppendLine("## Ora (dall'inizio) in cui ogni ordine viene riscosso - profilo Regolare con annunci");
            sb.AppendLine();
            var reg = rs[1];
            sb.AppendLine("| Ordine | Tipo | Ore |");
            sb.AppendLine("|---|---|---|");
            foreach (var o in cfg.Orders)
                sb.AppendLine("| " + o.Id + " " + o.Title + " | " + o.Type + " | " + (reg.OrderClaimHours.ContainsKey(o.Id) ? reg.OrderClaimHours[o.Id].ToString("0.00") : "non completato") + " |");
            sb.AppendLine();
            sb.AppendLine("## Guadagni cumulati (valuta totale guadagnata) e saldo finale");
            sb.AppendLine();
            sb.AppendLine("| Profilo | Giorno 1 | Giorno 7 | Giorno " + days + " | Saldo finale |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var r in rs)
                sb.AppendLine("| " + r.P.Name + " | " + Fmt(r.EarnedAtDay[1]) + " | " + Fmt(r.EarnedAtDay[Math.Min(7, days)]) + " | " + Fmt(r.EarnedAtDay[days]) + " | " + Fmt(r.BalanceAtDay[days]) + " |");
            return sb.ToString();
        }
    }
}
