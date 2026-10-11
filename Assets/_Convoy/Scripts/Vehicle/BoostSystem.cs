using UnityEngine;

namespace Convoy.Vehicle
{
    /// <summary>
    /// Nitro tank logic (pilot-side boost). Pure C#, unit-tested. M-01 will later tie boost to
    /// Engineer power and heat; for now the trade-off is a finite tank that refills slowly.
    /// </summary>
    public sealed class BoostSystem
    {
        private readonly VehicleConfig _config;
        private float _sinceLastUse;

        public BoostSystem(VehicleConfig config)
        {
            _config = config;
            Fraction = 1f;
            _sinceLastUse = float.MaxValue;
        }

        /// <summary>Tank level, 0 = empty, 1 = full.</summary>
        public float Fraction { get; private set; }

        /// <summary>True while boost is being applied this step.</summary>
        public bool Active { get; private set; }

        /// <summary>Advance one physics step. Returns true while boost should push the vehicle.</summary>
        public bool Step(float deltaTime, bool requested)
        {
            float dt = Mathf.Max(0f, deltaTime);

            // Hysteresis: once empty you need a minimum refill before boosting again (no flicker on/off).
            bool canStart = Fraction >= _config.boostMinToStart;
            bool canContinue = Active && Fraction > 0f;
            Active = requested && (canContinue || canStart);

            if (Active)
            {
                Fraction = Mathf.Max(0f, Fraction - _config.boostDrainPerSecond * dt);
                _sinceLastUse = 0f;
                if (Fraction <= 0f) Active = false;
            }
            else
            {
                _sinceLastUse += dt;
                if (_sinceLastUse >= _config.boostRegenDelay)
                    Fraction = Mathf.Min(1f, Fraction + _config.boostRegenPerSecond * dt);
            }

            return Active;
        }

        public void Refill()
        {
            Fraction = 1f;
            Active = false;
        }
    }
}
