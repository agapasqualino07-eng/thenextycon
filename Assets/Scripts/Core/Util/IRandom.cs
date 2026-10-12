using System;
using System.Collections.Generic;

namespace OfficinaTycoon
{
    /// <summary>Injectable RNG so every random mechanic is deterministic in tests.</summary>
    public interface IRandom
    {
        /// <summary>Uniform in [0,1).</summary>
        double NextDouble();
    }

    /// <summary>
    /// xorshift64* generator whose full state is a single ulong, so it can be saved.
    /// Persisting the state means restoring an old save replays the SAME rolls
    /// (it does not allow re-rolling an unfavourable outcome). This is accident
    /// robustness, not tamper protection.
    /// </summary>
    public sealed class XorShiftRandom : IRandom
    {
        const ulong Fallback = 0x9E3779B97F4A7C15UL;
        ulong _state;

        public XorShiftRandom(ulong seed) { _state = seed == 0 ? Fallback : seed; }

        public ulong State
        {
            get => _state;
            set => _state = value == 0 ? Fallback : value;
        }

        public double NextDouble()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            ulong r = _state * 0x2545F4914F6CDD1DUL;
            return (r >> 11) * (1.0 / 9007199254740992.0); // 2^53
        }
    }

    /// <summary>Returns a scripted, cycling sequence. For tests.</summary>
    public sealed class FixedRandom : IRandom
    {
        readonly List<double> _values;
        int _i;
        public FixedRandom(params double[] values)
        {
            if (values == null || values.Length == 0) throw new ArgumentException("need at least one value");
            _values = new List<double>(values);
        }
        public double NextDouble()
        {
            double v = _values[_i % _values.Count];
            _i++;
            return v;
        }
    }
}
