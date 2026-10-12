using System;

namespace OfficinaTycoon
{
    /// <summary>
    /// Cost formulas, shared by gameplay, UI, tests and the simulator (so simulations use the real maths).
    /// cost of level (n+1), when currently at level n:  BaseCost * Growth^n
    /// </summary>
    public static class Formulas
    {
        public static double LevelCost(double baseCost, double growth, int currentLevel)
            => baseCost * Num.SafePow(growth, currentLevel);

        /// <summary>
        /// Total cost of buying <paramref name="count"/> levels starting from <paramref name="currentLevel"/>:
        /// geometric series  c0 * (g^count - 1) / (g - 1).
        /// </summary>
        public static double BulkCost(double baseCost, double growth, int currentLevel, int count)
        {
            if (count <= 0) return 0;
            double c0 = LevelCost(baseCost, growth, currentLevel);
            if (Math.Abs(growth - 1.0) < 1e-12) return c0 * count;
            return c0 * (Num.SafePow(growth, count) - 1.0) / (growth - 1.0);
        }

        /// <summary>Largest count (&lt;= maxCount) whose BulkCost fits in budget. 0 if even one level is unaffordable.</summary>
        public static int MaxAffordable(double baseCost, double growth, int currentLevel, double budget, int maxCount)
        {
            if (maxCount <= 0 || !(budget > 0)) return 0;
            double c0 = LevelCost(baseCost, growth, currentLevel);
            if (c0 > budget) return 0;
            int n;
            if (Math.Abs(growth - 1.0) < 1e-12) n = (int)Math.Min(maxCount, Math.Floor(budget / c0));
            else
            {
                double est = Math.Log(budget * (growth - 1.0) / c0 + 1.0) / Math.Log(growth);
                n = (int)Math.Max(0, Math.Min(maxCount, Math.Floor(est)));
            }
            // Correct floating point error in both directions.
            while (n > 0 && BulkCost(baseCost, growth, currentLevel, n) > budget) n--;
            while (n < maxCount && BulkCost(baseCost, growth, currentLevel, n + 1) <= budget) n++;
            return n;
        }
    }
}
