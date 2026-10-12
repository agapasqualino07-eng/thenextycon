using System;

namespace OfficinaTycoon
{
    /// <summary>Numeric safety helpers. All currency math is double with explicit caps.</summary>
    public static class Num
    {
        public static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        /// <summary>Returns v if finite, otherwise fallback.</summary>
        public static double Sanitize(double v, double fallback = 0.0) => IsFinite(v) ? v : fallback;

        public static double Clamp(double v, double min, double max)
        {
            if (!IsFinite(v)) return min;
            return v < min ? min : (v > max ? max : v);
        }

        /// <summary>Math.Pow that never returns Infinity/NaN (clamped to double.MaxValue/1e100).</summary>
        public static double SafePow(double b, double e)
        {
            double r = Math.Pow(b, e);
            if (!IsFinite(r)) return 1e100;
            return r > 1e100 ? 1e100 : r;
        }
    }
}
