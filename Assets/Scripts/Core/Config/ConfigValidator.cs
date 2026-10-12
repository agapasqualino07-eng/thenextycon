using System;
using System.Collections.Generic;
using System.Linq;

namespace OfficinaTycoon
{
    /// <summary>Static validation of a GameConfig. Run at startup (dev builds) and by the test-suite.</summary>
    public static class ConfigValidator
    {
        public const double ProbabilityTolerance = 1e-6;

        public static List<string> Validate(GameConfig c)
        {
            var e = new List<string>();
            if (c == null) { e.Add("config is null"); return e; }

            if (c.Phases.Count == 0) e.Add("no phases");
            for (int i = 0; i < c.Phases.Count; i++)
                if (i > 0 && c.Phases[i].TotalEarnedRequired <= c.Phases[i - 1].TotalEarnedRequired)
                    e.Add("phase thresholds must be strictly increasing at " + c.Phases[i].Id);

            Unique(e, "machine", c.Machines.Select(m => m.Id));
            Unique(e, "upgrade", c.Upgrades.Select(u => u.Id));
            Unique(e, "order", c.Orders.Select(o => o.Id));
            Unique(e, "synergy", c.Synergies.Select(s => s.Id));

            var machineIds = new HashSet<string>(c.Machines.Select(m => m.Id));
            var upgradeIds = new HashSet<string>(c.Upgrades.Select(u => u.Id));
            var orderIds = new HashSet<string>(c.Orders.Select(o => o.Id));

            if (c.Economy.MaxBalance <= 0 || !Num.IsFinite(c.Economy.MaxBalance)) e.Add("Economy.MaxBalance invalid");
            if (c.Economy.MaxTempMultiplier < 1) e.Add("Economy.MaxTempMultiplier must be >= 1");

            foreach (var m in c.Machines)
            {
                if (string.IsNullOrEmpty(m.Id)) e.Add("machine with empty id");
                if (m.BaseCost <= 0) e.Add(m.Id + ": BaseCost must be > 0");
                if (m.CostGrowth <= 1.0) e.Add(m.Id + ": CostGrowth must be > 1");
                if (m.BaseOutput <= 0) e.Add(m.Id + ": BaseOutput must be > 0");
                if (m.MaxLevel < 1) e.Add(m.Id + ": MaxLevel must be >= 1");
                if (m.UnlockPhase < 0 || m.UnlockPhase >= c.Phases.Count) e.Add(m.Id + ": UnlockPhase out of range");
                if (!string.IsNullOrEmpty(m.UnlockRequiresMachineId) && !machineIds.Contains(m.UnlockRequiresMachineId))
                    e.Add(m.Id + ": unknown prerequisite machine " + m.UnlockRequiresMachineId);
                // Cost at max level must stay representable (Num.SafePow clamps at 1e100); beyond the balance cap it is simply unaffordable.
                double top = m.BaseCost * Math.Pow(m.CostGrowth, m.MaxLevel);
                if (!Num.IsFinite(top) || top > 1e90)
                    e.Add(m.Id + ": cost at MaxLevel overflows sane range");
            }
            DetectMachineCycles(c, e);

            foreach (var u in c.Upgrades)
            {
                if (u.Cost <= 0) e.Add(u.Id + ": Cost must be > 0");
                if (u.Effect == UpgradeEffect.MachineMultiplier && !machineIds.Contains(u.TargetMachineId))
                    e.Add(u.Id + ": unknown target machine");
                if ((u.Effect == UpgradeEffect.MachineMultiplier || u.Effect == UpgradeEffect.GlobalMultiplier ||
                     u.Effect == UpgradeEffect.TapMultiplier) && u.Value < 1)
                    e.Add(u.Id + ": multiplier must be >= 1");
                if (!string.IsNullOrEmpty(u.RequiresMachineId) && !machineIds.Contains(u.RequiresMachineId))
                    e.Add(u.Id + ": unknown required machine");
            }
            foreach (var s in c.Synergies)
                if (!machineIds.Contains(s.SourceMachineId) || !machineIds.Contains(s.TargetMachineId))
                    e.Add(s.Id + ": unknown machine reference");

            foreach (var o in c.Orders)
            {
                if (o.Target <= 0) e.Add(o.Id + ": Target must be > 0");
                if (!string.IsNullOrEmpty(o.PrerequisiteOrderId) && !orderIds.Contains(o.PrerequisiteOrderId))
                    e.Add(o.Id + ": unknown prerequisite order");
                if ((o.Type == OrderType.MachineLevel || o.Type == OrderType.ProduceWithMachine) && !machineIds.Contains(o.TargetId))
                    e.Add(o.Id + ": unknown target machine " + o.TargetId);
                if (o.Type == OrderType.BuyUpgrade && !upgradeIds.Contains(o.TargetId))
                    e.Add(o.Id + ": unknown target upgrade " + o.TargetId);
                if (o.UnlockPhase < 0 || o.UnlockPhase >= c.Phases.Count) e.Add(o.Id + ": UnlockPhase out of range");
            }
            DetectOrderCycles(c, e);

            CheckProbabilities(e, "Experimental.Outcomes", c.Experimental.Outcomes.Select(x => x.ProbabilityPercent));
            CheckProbabilities(e, "Surge.Outcomes", c.Surge.Outcomes.Select(x => x.ProbabilityPercent));
            CheckProbabilities(e, "Crate.Entries", c.Crate.Entries.Select(x => x.ProbabilityPercent));
            foreach (var o in c.Experimental.Outcomes)
                if (o.ReturnMultiplier < 0) e.Add("Experimental outcome " + o.Id + ": negative return");
            if (c.Experimental.Outcomes.Count > 0 && c.Experimental.Outcomes.Min(x => x.ReturnMultiplier) < 0.0)
                e.Add("Experimental loss is unbounded");

            if (c.Offline.MaxSeconds <= 0) e.Add("Offline.MaxSeconds must be > 0");
            if (c.Offline.Efficiency < 0 || c.Offline.Efficiency > 1) e.Add("Offline.Efficiency must be in [0,1]");
            if (c.Ads.MaxPerDay < 0) e.Add("Ads.MaxPerDay invalid");
            return e;
        }

        public static void CheckProbabilities(List<string> errors, string name, IEnumerable<double> percents)
        {
            var list = percents.ToList();
            if (list.Count == 0) { errors.Add(name + ": empty table"); return; }
            if (list.Any(p => p < 0 || !Num.IsFinite(p))) errors.Add(name + ": negative or non-finite probability");
            double sum = list.Sum();
            if (Math.Abs(sum - 100.0) > ProbabilityTolerance) errors.Add(name + ": probabilities sum to " + sum + ", expected 100");
        }

        static void Unique(List<string> e, string what, IEnumerable<string> ids)
        {
            var seen = new HashSet<string>();
            foreach (var id in ids) if (!seen.Add(id)) e.Add("duplicate " + what + " id " + id);
        }

        static void DetectMachineCycles(GameConfig c, List<string> e)
        {
            var map = new Dictionary<string, string>();
            foreach (var m in c.Machines) map[m.Id ?? ""] = m.UnlockRequiresMachineId;
            foreach (var id in map.Keys)
            {
                var seen = new HashSet<string>();
                string cur = id;
                while (!string.IsNullOrEmpty(cur) && map.ContainsKey(cur))
                {
                    if (!seen.Add(cur)) { e.Add("machine prerequisite cycle at " + id); break; }
                    cur = map[cur];
                }
            }
        }

        static void DetectOrderCycles(GameConfig c, List<string> e)
        {
            var map = new Dictionary<string, string>();
            foreach (var o in c.Orders) map[o.Id] = o.PrerequisiteOrderId;
            foreach (var id in map.Keys.ToList())
            {
                var seen = new HashSet<string>();
                string cur = id;
                while (!string.IsNullOrEmpty(cur) && map.ContainsKey(cur))
                {
                    if (!seen.Add(cur)) { e.Add("order prerequisite cycle at " + id); break; }
                    cur = map[cur];
                }
            }
        }
    }
}
