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
            if (v < 1000) return v < 10 && v != Math.Floor(v) ? v.ToString("0.0#", CultureInfo.InvariantCulture) : Math.Floor(v).ToString("0", CultureInfo.InvariantCulture);
            int i = 0;
            while (v >= 1000 && i < Suffix.Length - 1) { v /= 1000; i++; }
            return v.ToString(v >= 100 ? "0" : (v >= 10 ? "0.0" : "0.00"), CultureInfo.InvariantCulture) + Suffix[i];
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
