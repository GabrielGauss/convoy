using System;
using Convoy.Core;
using UnityEngine;

namespace Convoy.Vehicle
{
    /// <summary>
    /// Runtime adapter for the hull-health pool (ADR-003 / D-018). Thin MonoBehaviour: the rules live in
    /// <see cref="VehicleHealth"/>, <see cref="ImpactDamage"/>, <see cref="ImpactWindow"/> and <see cref="HandlingModifiers"/>
    /// (pure C#, EditMode-tested). This class only turns Unity collisions into <see cref="DamageEvent"/>s and caches
    /// the handling multipliers <see cref="VehicleController"/> reads every physics step.
    /// Other modules (Combat, hazards) reach it through <see cref="IDamageable"/> only, so they never reference Vehicle.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class VehicleDamage : MonoBehaviour, IDamageable
    {
        [SerializeField] private DamageConfig _config;

        private Rigidbody _body;
        private VehicleHealth _health;
        private ImpactWindow _window;
        private HandlingModifiers _modifiers = HandlingModifiers.None;

        /// <summary>Race-assigned id (R-01). None until assigned; used as <see cref="DamageEvent.Source"/> when this vehicle rams another.</summary>
        public VehicleId Id { get; set; }

        public DamageConfig Config => _config;

        /// <summary>Full health (Health01 = 1) when no config is assigned, so a mis-wired vehicle still drives normally.</summary>
        public VehicleDamageState State => _health != null
            ? _health.State
            : new VehicleDamageState { Health = 1f, MaxHealth = 1f };

        /// <summary>Cached: recomputed only when health changes, so FixedUpdate reads a struct and never evaluates the curve.</summary>
        public HandlingModifiers Modifiers => _modifiers;

        /// <summary>
        /// Raised after health actually dropped. Amount is the health removed (may be less than requested when clamped at 0).
        /// Presentation/telemetry only: listeners must not write simulation state (D-007).
        /// </summary>
        public event Action<DamageEvent> Damaged;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            if (_config == null)
            {
                Debug.LogError($"{name}: VehicleDamage has no DamageConfig assigned; damage is disabled.", this);
                return;
            }

            // Allocated once here so collisions and hits never allocate.
            _health = new VehicleHealth(_config);
            _window = new ImpactWindow(_config);
            _modifiers = HandlingModifiers.FromHealth(_health.State.Health01, _config);
        }

        public float ApplyDamage(in DamageEvent damage)
        {
            if (_health == null) return 0f;

            float applied = _health.Apply(damage);
            if (applied <= 0f) return 0f;

            RefreshModifiers();
            // Copy with the real amount so listeners see what actually happened (struct copy, no allocation).
            DamageEvent dealt = damage;
            dealt.Amount = applied;
            Damaged?.Invoke(dealt);
            return applied;
        }

        /// <summary>Adds health (clamped at max; no effect when wrecked). M-02 calls this with rate × dt. Returns health added.</summary>
        public float Repair(float amount)
        {
            if (_health == null) return 0f;

            float applied = _health.Repair(amount);
            if (applied > 0f) RefreshModifiers();
            return applied;
        }

        private void RefreshModifiers()
        {
            _modifiers = HandlingModifiers.FromHealth(_health.State.Health01, _config);
        }

        // Unity calls this once per new contact pair, inside the physics step. With Physics.reuseCollisionCallbacks
        // (on by default) the Collision object is recycled, so reading impulse/GetContact allocates nothing.
        private void OnCollisionEnter(Collision collision)
        {
            if (_health == null || collision.contactCount == 0) return;

            // impulse / mass = the velocity change this crash imposed on us; it already accounts for the other body's mass.
            float deltaV = collision.impulse.magnitude / _body.mass;
            float upDot = Vector3.Dot(collision.GetContact(0).normal, Vector3.up);

            float raw = ImpactDamage.Compute(deltaV, upDot, _config);
            // Fixed time, not Time.time: this runs inside the physics step, and several contacts of one crash
            // must share the same clock to be merged by the window.
            float amount = _window.Filter(raw, Time.fixedTimeAsDouble);
            if (amount <= 0f) return;

            // TryGetComponent does not allocate (unlike GetComponent returning null in the Editor).
            Rigidbody other = collision.rigidbody;
            VehicleId source = other != null && other.TryGetComponent(out VehicleDamage otherDamage)
                ? otherDamage.Id
                : VehicleId.None;

            ApplyDamage(new DamageEvent { Amount = amount, Kind = DamageKind.Impact, Source = source });
        }
    }
}
