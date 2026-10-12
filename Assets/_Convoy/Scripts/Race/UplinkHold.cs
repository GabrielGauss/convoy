using System;

namespace Convoy.Race
{
    /// <summary>
    /// Pure hold-to-finish rule for the Monolith uplink (D-025): stay inside the zone for
    /// <see cref="UplinkConfig.holdSeconds"/> to complete. No scene access; the caller does the
    /// geometric inside-test and passes the result, so this is unit-tested and allocation-free.
    /// The config is read live each Tick, so Inspector tuning applies without rebuilding.
    /// </summary>
    public sealed class UplinkHold
    {
        /// <summary>
        /// 200 x 0.02f accumulates to 3.9999... in float; without this tolerance the hold would
        /// complete one physics step late.
        /// </summary>
        public const float CompletionEpsilon = 1e-4f;

        private readonly UplinkConfig _config;

        public UplinkHold(UplinkConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public float HeldSeconds { get; private set; }

        /// <summary>HeldSeconds / holdSeconds, clamped to [0,1].</summary>
        public float Progress01
        {
            get
            {
                float p = HeldSeconds / _config.holdSeconds;
                return p < 0f ? 0f : (p > 1f ? 1f : p);
            }
        }

        /// <summary>True when inside the zone AND under the speed limit on the last Tick.</summary>
        public bool Inside { get; private set; }

        /// <summary>Sticky: once complete, stays complete until <see cref="Reset"/>.</summary>
        public bool Complete { get; private set; }

        /// <summary>
        /// Advance one physics step. Returns true only on the step the hold completes
        /// (so the caller fires the finish exactly once); false afterwards.
        /// </summary>
        public bool Tick(float deltaTime, bool insideZone, float speedMps)
        {
            // NaN fails the comparison, so this also maps NaN to 0.
            float dt = deltaTime > 0f ? deltaTime : 0f;

            float limitKmh = _config.maxHoldSpeedKmh;
            bool ok = insideZone && (limitKmh <= 0f || speedMps * 3.6f <= limitKmh);
            Inside = ok;

            if (Complete) return false;

            if (ok)
            {
                HeldSeconds += dt;
            }
            else if (_config.exitBehaviour == UplinkExitBehaviour.Decay)
            {
                float left = HeldSeconds - _config.decayPerSecond * dt;
                HeldSeconds = left > 0f ? left : 0f;
            }
            else
            {
                HeldSeconds = 0f;
            }

            if (HeldSeconds >= _config.holdSeconds - CompletionEpsilon)
            {
                Complete = true;
                HeldSeconds = _config.holdSeconds;
                return true;
            }
            return false;
        }

        /// <summary>Clear all progress (e.g. a new attempt).</summary>
        public void Reset()
        {
            HeldSeconds = 0f;
            Inside = false;
            Complete = false;
        }
    }
}
