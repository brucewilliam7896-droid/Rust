using System;

namespace RustPlus.Core.Determinism
{
    [Serializable]
    public struct DeterministicRandomState
    {
        public ulong State;
        public ulong Increment;

        public DeterministicRandomState(ulong state, ulong increment)
        {
            State = state;
            Increment = increment;
        }
    }

    public sealed class DeterministicRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private ulong _state;
        private ulong _increment;

        public DeterministicRandom(ulong seed, ulong stream = 54UL)
        {
            _increment = (stream << 1) | 1UL;
            _state = 0UL;
            NextUInt();
            _state = unchecked(_state + seed);
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong previousState = _state;
            _state = unchecked(previousState * Multiplier + _increment);

            uint shifted = (uint)(((previousState >> 18) ^ previousState) >> 27);
            int rotation = (int)(previousState >> 59);
            return (shifted >> rotation) | (shifted << ((-rotation) & 31));
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "The upper bound must be greater than the lower bound.");
            }

            uint range = (uint)((long)maxExclusive - minInclusive);
            uint threshold = unchecked(0U - range) % range;
            uint sample;
            do
            {
                sample = NextUInt();
            }
            while (sample < threshold);

            return (int)(minInclusive + (long)(sample % range));
        }

        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1.0f / 16777216.0f);
        }

        public DeterministicRandomState CaptureState()
        {
            return new DeterministicRandomState(_state, _increment);
        }

        public void RestoreState(DeterministicRandomState state)
        {
            if ((state.Increment & 1UL) == 0UL)
            {
                throw new ArgumentException("The random stream increment must be odd.", nameof(state));
            }

            _state = state.State;
            _increment = state.Increment;
        }
    }
}