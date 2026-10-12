using System;
using Convoy.Core;

namespace Convoy.Vehicle
{
    /// <summary>Snapshot of a vehicle's hull health. Serializable plain data (telemetry, future netcode).</summary>
    [Serializable]
    public struct VehicleDamageState
    {
        public float Health;
        public float MaxHealth;
        /// <summary>Cumulative, never decreases (telemetry for Q-01).</summary>
        public float TotalDamageTaken;
        /// <summary>Events with Amount &gt; 0 that were applied.</summary>
        public int HitCount;
        /// <summary>Health &lt;= 0.</summary>
        public bool Wrecked;

        public float Health01 => MaxHealth > 0f ? Health / MaxHealth : 0f;
    }

    /// <summary>Single hull-health pool (ADR-003 option A). Pure C#, no engine dependency beyond the config.</summary>
    public sealed class VehicleHealth
    {
        private VehicleDamageState _state;

        public VehicleHealth(DamageConfig config)
        {
            _state.MaxHealth = config.maxHealth;
            _state.Health = config.maxHealth;
        }

        public VehicleDamageState State => _state;

        /// <summary>Ignores non-positive/NaN amounts and anything once wrecked. Clamps at 0. Returns health removed.</summary>
        public float Apply(in DamageEvent damage)
        {
            if (_state.Wrecked || !(damage.Amount > 0f)) return 0f;

            float applied = damage.Amount < _state.Health ? damage.Amount : _state.Health;
            _state.Health -= applied;
            _state.TotalDamageTaken += applied;
            _state.HitCount++;
            if (_state.Health <= 0f)
            {
                _state.Health = 0f;
                _state.Wrecked = true;
            }
            return applied;
        }

        /// <summary>Clamps at max. No effect when wrecked (W-01 decides revival via <see cref="Restore"/>). Returns health added.</summary>
        public float Repair(float amount)
        {
            if (_state.Wrecked || !(amount > 0f)) return 0f;

            float room = _state.MaxHealth - _state.Health;
            float applied = amount < room ? amount : room;
            if (applied <= 0f) return 0f;
            _state.Health += applied;
            return applied;
        }

        /// <summary>Full health and not wrecked; keeps TotalDamageTaken and HitCount (they are telemetry).</summary>
        public void Restore()
        {
            _state.Health = _state.MaxHealth;
            _state.Wrecked = false;
        }
    }
}
