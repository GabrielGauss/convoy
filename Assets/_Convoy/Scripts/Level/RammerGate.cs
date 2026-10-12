using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convoy.Level
{
    /// <summary>
    /// Speed-breakable rock wall (LV-01, D-022/D-031 "Rammer rock"). Thin runtime adapter: every decision is made by
    /// <see cref="RammerGateRules"/>; this component only reads physics data, toggles colliders and wakes the debris.
    ///
    /// Editor wiring (done by LV-01c's generator, described here for humans):
    /// - <c>_solid</c> and <c>_sensor</c> must both be BoxColliders on THIS GameObject (the root). Unity sends
    ///   OnTrigger* messages to the GameObject that owns the trigger collider (or to its Rigidbody's GameObject);
    ///   the root has no Rigidbody, so a sensor on a child object would never reach this script.
    /// - <c>_chunks</c> are child Rigidbodies (kinematic) each with one Collider; Awake disables those colliders.
    ///
    /// Module rule (D-023): Convoy.Level references Core only, so the breaker is read purely through its Rigidbody
    /// (velocity, mass, centre of mass). Any heavy, non-kinematic body can break it; VehicleController is never touched.
    /// Restart is a scene reload (D-029): all state lives on this instance, nothing static, so a reload restores the wall.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RammerGate : MonoBehaviour
    {
        // OnValidate warning only (spec perf constraint): more chunks = more rigidbodies waking at once.
        private const int ChunkHardCap = 30;
        // GetComponentsInChildren into this list allocates nothing while the breaker has ≤ 16 colliders (spec).
        private const int BreakerColliderCapacity = 16;

        [SerializeField] private RammerGateConfig _config;
        [Tooltip("Non-trigger BoxCollider on this GameObject: the wall itself.")]
        [SerializeField] private BoxCollider _solid;
        [Tooltip("Trigger BoxCollider on this GameObject. Resized in Awake to the solid plus sensorDepth on both faces.")]
        [SerializeField] private BoxCollider _sensor;
        [Tooltip("Child debris bodies. Kept kinematic with colliders disabled until the wall breaks.")]
        [SerializeField] private Rigidbody[] _chunks = Array.Empty<Rigidbody>();
        [Tooltip("Seed for debris velocity spread; same seed = same break every time.")]
        [SerializeField] private int _seed;

        private readonly List<Collider> _breakerColliders = new List<Collider>(BreakerColliderCapacity);
        private Collider[] _chunkColliders = Array.Empty<Collider>();
        private bool _isBroken;
        private bool _isValid;
        private float _settleRemaining;

        public bool IsBroken => _isBroken;

        /// <summary>Raised once when the wall shatters. Presentation/telemetry only (VFX, audio, camera shake).</summary>
        public event Action<RammerGate> Broken;

        private void Awake()
        {
            _isValid = Validate();
            if (!_isValid) return;

            // The config is the single source of truth for the sensor depth: rebuild the trigger box from the
            // solid box so a tuned sensorDepth can never disagree with a stale prefab value.
            _sensor.isTrigger = true;
            _sensor.center = _solid.center;
            Vector3 size = _solid.size;
            size.z += 2f * _config.sensorDepth;
            _sensor.size = size;

            // Cache chunk colliders once here so Break and the settle step never call GetComponent.
            _chunkColliders = new Collider[_chunks.Length];
            for (int i = 0; i < _chunks.Length; i++)
            {
                Rigidbody chunk = _chunks[i];
                if (chunk == null) continue;
                // Kinematic = the physics engine moves nothing until we say so; an intact wall costs ~0 CPU.
                chunk.isKinematic = true;
                _chunkColliders[i] = chunk.GetComponent<Collider>();
                if (_chunkColliders[i] != null) _chunkColliders[i].enabled = false;
            }
        }

        private void OnDestroy()
        {
            // Drop subscribers so a reloaded scene's listeners never keep a destroyed gate alive.
            Broken = null;
        }

        // Why a trigger sensor instead of OnCollisionEnter on the solid wall: by the time OnCollisionEnter runs, PhysX
        // has already resolved the impact and the car's velocity is ~0, so we could neither read the true closing
        // speed nor let the car keep 85 % of it. The sensor sits sensorDepth (2.5 m) in front of each face; at the
        // 52 m/s boost top speed a car moves ~1 m per 50 Hz step, so it is always caught in the sensor (with an
        // un-resolved, real velocity) a step or more before it touches the solid. Disabling the solid there means
        // the car never collides with it at all.
        private void OnTriggerEnter(Collider other)
        {
            TryBreak(other);
        }

        // Why Stay as well: a car can enter the sensor below the threshold and then reach it inside (nitro kick),
        // and Enter fires only once. Stay runs every physics step while overlapping, so it must not allocate.
        private void OnTriggerStay(Collider other)
        {
            TryBreak(other);
        }

        private void TryBreak(Collider other)
        {
            // Unity delivers trigger messages even to disabled components, so the IsBroken guard is what stops repeats.
            if (_isBroken || !_isValid || other.isTrigger) return;

            // attachedRigidbody resolves a wheel/body-part collider to the car's single Rigidbody. Static colliders
            // (ground) have none; kinematic bodies (other gates' chunks, moving platforms) have no physical momentum.
            Rigidbody body = other.attachedRigidbody;
            if (body == null || body.isKinematic) return;

            // Velocity relative to the wall = the body's own velocity, because the wall never moves (static collider).
            // worldCenterOfMass instead of transform.position: which face the car is on is decided by its mass centre.
            float closing = RammerGateRules.ClosingSpeed(
                body.linearVelocity, transform.forward, transform.position - body.worldCenterOfMass);
            if (RammerGateRules.ShouldBreak(closing, body.mass, _config)) Break(body);
        }

        private void Break(Rigidbody breaker)
        {
            _isBroken = true;
            _solid.enabled = false;

            Vector3 impactVelocity = breaker.linearVelocity;
            // The only write to a foreign body (ADR-006 § 2): the cost of smashing through. Horizontal only, so the
            // car's suspension/jump state (y) is untouched.
            breaker.linearVelocity = RammerGateRules.RetainedVelocity(impactVelocity, _config.speedRetention);

            // Debris spawns ahead of the car at a slower speed (factor 0.6 < retention 0.85), so the car would drive
            // into it and could flip. Make the breaker ignore every chunk permanently; other vehicles still collide.
            // Colliders are enabled first: IgnoreCollision pairs are only reliable between enabled colliders.
            breaker.GetComponentsInChildren(false, _breakerColliders);
            for (int i = 0; i < _chunks.Length; i++)
            {
                Rigidbody chunk = _chunks[i];
                if (chunk == null) continue;
                Collider chunkCollider = _chunkColliders[i];
                if (chunkCollider != null)
                {
                    chunkCollider.enabled = true;
                    for (int c = 0; c < _breakerColliders.Count; c++)
                        Physics.IgnoreCollision(_breakerColliders[c], chunkCollider, true);
                }

                // Order matters: a body must be non-kinematic before its velocity can be set.
                chunk.isKinematic = false;
                chunk.mass = _config.chunkMassKg;
                chunk.linearVelocity = RammerGateRules.DebrisVelocity(
                    impactVelocity, _config.debrisSpeedFactor, _config.debrisSpreadMps, i, _seed);
            }
            _breakerColliders.Clear();

            _settleRemaining = _config.debrisSettleSeconds;
            Broken?.Invoke(this);
        }

        // Gameplay timing runs in FixedUpdate (CLAUDE.md): the settle timer counts physics time, not frame time.
        private void FixedUpdate()
        {
            if (!_isBroken) return;

            _settleRemaining -= Time.fixedDeltaTime;
            if (_settleRemaining > 0f) return;

            // Freeze the debris where it landed: kinematic + collider off = zero physics cost from here on, and no
            // rubble for AI rivals to snag on forever.
            for (int i = 0; i < _chunks.Length; i++)
            {
                Rigidbody chunk = _chunks[i];
                if (chunk == null) continue;
                // Zero velocity first: setting velocity on a kinematic body logs a warning in Unity 6.
                chunk.linearVelocity = Vector3.zero;
                chunk.angularVelocity = Vector3.zero;
                chunk.isKinematic = true;
                if (_chunkColliders[i] != null) _chunkColliders[i].enabled = false;
            }

            // Nothing left to do: a disabled MonoBehaviour gets no FixedUpdate calls at all.
            enabled = false;
        }

        private bool Validate()
        {
            bool ok = true;
            if (_config == null) { Debug.LogError($"RammerGate '{name}': no RammerGateConfig assigned.", this); ok = false; }
            if (_solid == null) { Debug.LogError($"RammerGate '{name}': no solid BoxCollider assigned.", this); ok = false; }
            if (_sensor == null) { Debug.LogError($"RammerGate '{name}': no sensor BoxCollider assigned.", this); ok = false; }
            if (_sensor != null && _sensor == _solid)
            {
                Debug.LogError($"RammerGate '{name}': solid and sensor must be two different BoxColliders.", this);
                ok = false;
            }
            if (_sensor != null && _sensor.gameObject != gameObject)
            {
                Debug.LogError($"RammerGate '{name}': the sensor must be on the RammerGate GameObject, " +
                               "otherwise Unity never sends its trigger messages to this script.", this);
                ok = false;
            }
            if (_chunks == null) _chunks = Array.Empty<Rigidbody>();
            return ok;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_chunks != null && _chunks.Length > ChunkHardCap)
                Debug.LogWarning($"RammerGate '{name}': {_chunks.Length} chunks exceeds the {ChunkHardCap} cap; " +
                                 "breaking will wake that many rigidbodies in one step.", this);
        }
#endif
    }
}
