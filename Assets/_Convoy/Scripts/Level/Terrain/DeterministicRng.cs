namespace Convoy.Level
{
    /// <summary>
    /// SplitMix32: tiny, allocation-free, and its output is fixed by the algorithm, not by the runtime. Used instead of
    /// UnityEngine.Random / System.Random because generated terrain and per-run world variation must reproduce exactly
    /// from a seed across Unity versions and platforms (tests pin the first values).
    /// </summary>
    public struct DeterministicRng
    {
        private uint _state;

        public DeterministicRng(int seed)
        {
            _state = unchecked((uint)seed);
        }

        public uint NextUInt()
        {
            unchecked
            {
                _state += 0x9E3779B9u;
                uint z = _state;
                z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                return z ^ (z >> 16);
            }
        }

        /// <summary>Uniform in [0, 1). Uses the top 24 bits so every value is exactly representable as a float.</summary>
        public float NextFloat01()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        /// <summary>Uniform in [0, maxExclusive); 0 when maxExclusive &lt;= 1. Multiply-shift avoids modulo bias.</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 1) return 0;
            return (int)(((ulong)NextUInt() * (ulong)maxExclusive) >> 32);
        }
    }
}
