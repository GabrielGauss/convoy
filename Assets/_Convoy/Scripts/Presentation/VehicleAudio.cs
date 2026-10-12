using Convoy.Vehicle;
using UnityEngine;

namespace Convoy.Presentation
{
    /// <summary>
    /// Engine, nitro and tyre-squeal sounds for one vehicle (AUD-01). Added automatically to every
    /// <see cref="VehicleController"/> in a loaded scene (also future AI rivals), so no scene wiring.
    /// Reads <see cref="VehicleState"/> and the current command only (D-007).
    /// Clips live in Resources/Audio/Vehicle so they load by path without asset references.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class VehicleAudio : MonoBehaviour
    {
        // Why Resources: lets the component add itself at runtime with no Inspector setup. Fine for a
        // handful of clips; Addressables would replace this if the audio library grows.
        private const string ClipFolder = "Audio/Vehicle/";
        private const int Gears = 5;
        private const float FadeRate = 3f;          // volume units per second
        private const float NitroFadeRate = 6f;
        private const float SpatialBlend = 0.6f;    // mostly 2D for the player's car, some 3D for rivals later

        private VehicleController _vehicle;
        private AudioSource _engine, _nitro, _skid, _oneShot;
        private AudioClip _nitroStart;
        private bool _wasBoosting;

        private static bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _hooked = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            AttachToAll();
            if (_hooked) return;
            _hooked = true;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => AttachToAll();
        }

        private static void AttachToAll()
        {
            var vehicles = Object.FindObjectsByType<VehicleController>(FindObjectsSortMode.None);
            foreach (VehicleController v in vehicles)
                if (v.GetComponent<VehicleAudio>() == null) v.gameObject.AddComponent<VehicleAudio>();
        }

        private void Awake()
        {
            _vehicle = GetComponent<VehicleController>();
            _engine = CreateLoop("engine_loop");
            _nitro = CreateLoop("nitro_loop");
            _skid = CreateLoop("skid_loop");
            _oneShot = CreateSource(null, false);
            _nitroStart = Resources.Load<AudioClip>(ClipFolder + "nitro_start");
            _nitro.volume = 0f;
            _skid.volume = 0f;
        }

        private AudioSource CreateLoop(string clipName)
        {
            AudioClip clip = Resources.Load<AudioClip>(ClipFolder + clipName);
            if (clip == null) Debug.LogWarning($"VehicleAudio: missing clip Resources/{ClipFolder}{clipName}", this);
            AudioSource s = CreateSource(clip, true);
            if (clip != null) s.Play();
            return s;
        }

        private AudioSource CreateSource(AudioClip clip, bool loop)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = SpatialBlend;
            s.dopplerLevel = 0f;   // doppler on a camera-following car sounds like a broken tape
            s.minDistance = 8f;
            s.maxDistance = 120f;
            return s;
        }

        private void Update()
        {
            if (_vehicle == null || _vehicle.Config == null) return;
            VehicleState state = _vehicle.State;
            float throttle = _vehicle.CurrentCommand.Throttle;
            float dt = Time.deltaTime;

            // --- Engine ---
            float top = _vehicle.Config.topSpeed + (state.Boosting ? _vehicle.Config.boostTopSpeedBonus : 0f);
            float speedFraction = top > 0f ? Mathf.Abs(state.ForwardSpeed) / top : 0f;
            float rpm = VehicleAudioMath.EngineRpm01(speedFraction, Gears);
            if (state.IsAirborne) rpm = Mathf.Min(1f, rpm + 0.25f * Mathf.Max(0f, throttle)); // free-revving in the air
            _engine.pitch = VehicleAudioMath.EnginePitch(rpm, throttle);
            _engine.volume = VehicleAudioMath.Approach(_engine.volume, VehicleAudioMath.EngineVolume(throttle), FadeRate, dt);

            // --- Nitro ---
            _nitro.volume = VehicleAudioMath.Approach(_nitro.volume, state.Boosting ? 0.75f : 0f, NitroFadeRate, dt);
            if (state.Boosting && !_wasBoosting && _nitroStart != null) _oneShot.PlayOneShot(_nitroStart, 0.9f);
            _wasBoosting = state.Boosting;

            // --- Tyres ---
            float skid = VehicleAudioMath.SkidIntensity(state.SlipAngle, state.ForwardSpeed, throttle,
                _vehicle.CurrentCommand.Handbrake, state.GroundedWheels);
            _skid.volume = VehicleAudioMath.Approach(_skid.volume, skid * 0.7f, FadeRate * 2f, dt);
            _skid.pitch = 0.9f + 0.2f * Mathf.Clamp01(Mathf.Abs(state.ForwardSpeed) / 30f);
        }
    }
}
