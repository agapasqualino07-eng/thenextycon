using System;
using System.Collections.Generic;

namespace OfficinaTycoon
{
    /// <summary>Shared probability maths: picking from percentage tables and expected values.</summary>
    public static class Probability
    {
        /// <summary>
        /// Picks an index from a table of percentages (sum 100) using a roll in [0,1).
        /// The same table drives both the roll and what the UI displays; there is no hidden adjustment.
        /// </summary>
        public static int Pick(IRandom rng, IList<double> percents)
        {
            if (percents == null || percents.Count == 0) return -1;
            double roll = rng.NextDouble() * 100.0;
            double acc = 0;
            for (int i = 0; i < percents.Count; i++)
            {
                acc += percents[i];
                if (roll < acc) return i;
            }
            return percents.Count - 1; // only reachable through floating point rounding at the very top
        }

        /// <summary>Expected value = sum(p_i/100 * v_i).</summary>
        public static double ExpectedValue(IList<double> percents, IList<double> values)
        {
            double ev = 0;
            for (int i = 0; i < percents.Count; i++) ev += percents[i] / 100.0 * values[i];
            return ev;
        }
    }
}
