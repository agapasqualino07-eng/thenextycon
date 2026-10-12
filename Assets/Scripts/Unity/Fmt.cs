using System;
using System.Globalization;

namespace OfficinaTycoon.UnityLayer
{
    /// <summary>Number and time formatting for the UI (invariant digits, short suffixes).</summary>
    public static class Fmt
    {
        static readonly string[] Suffix = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp" };

        public static string Num(double v)
        {
            if (!Core.IsFinite(v)) return "0";
            if (v < 0) return "-" + Num(-v);
            if (v < 1000)
            {
                // Small values keep up to two decimals (e.g. +0.5/s) but never show a misleading rounding to 1000.
                if (v < 10 && v != Math.Floor(v)) return (Math.Floor(v * 100) / 100).ToString("0.0#", CultureInfo.InvariantCulture);
                return Math.Floor(v).ToString("0", CultureInfo.InvariantCulture);
            }
            int i = 0;
            while (v >= 1000 && i < Suffix.Length - 1) { v /= 1000; i++; }
            // Truncate (never round up) so 999,999,999 shows "999M" instead of "1000M", and a balance is never overstated.
            string fmt = v >= 100 ? "0" : (v >= 10 ? "0.0" : "0.00");
            double scale = v >= 100 ? 1 : (v >= 10 ? 10 : 100);
            double t = Math.Floor(v * scale) / scale;
            return t.ToString(fmt, CultureInfo.InvariantCulture) + Suffix[i];
        }

        public static string Cr(double v) => Num(v) + " Cr";

        public static string Percent(double p) => p.ToString(p == Math.Floor(p) ? "0" : "0.#", CultureInfo.InvariantCulture) + "%";

        public static string Mult(double m) => "x" + m.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>mm:ss for short spans, "1h 05m" for long ones.</summary>
        public static string Time(double seconds)
        {
            if (seconds < 0) seconds = 0;
            int s = (int)Math.Ceiling(seconds);
            if (s >= 3600) return (s / 3600) + "h " + ((s % 3600) / 60).ToString("00") + "m";
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        /// <summary>Wrapper so Fmt does not depend on the Core's Num name clash.</summary>
        static class Core { public static bool IsFinite(double v) => OfficinaTycoon.Num.IsFinite(v); }
    }
}
