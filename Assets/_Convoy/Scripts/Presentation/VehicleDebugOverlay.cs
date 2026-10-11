#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Diagnostics;
using Convoy.Vehicle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Convoy.Presentation
{
    /// <summary>
    /// V-03 debug overlay: live vehicle numbers for handling tuning and playtests. Toggle with F1.
    /// Spawns itself in the Editor and Development builds (no scene wiring) and is compiled out of
    /// release builds. Reads <see cref="VehicleState"/> only (D-007). Live tuning = edit the
    /// VehicleConfig asset in the Inspector during Play mode.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class VehicleDebugOverlay : MonoBehaviour
    {
        private const float TextRefreshInterval = 0.1f;   // 10 Hz: readable, and keeps string building cheap
        private const float VehicleSearchInterval = 1f;
        private const float FpsSmoothing = 0.1f;
        private const int MaxWheels = 8;

        private readonly DebugOverlayText _text = new DebugOverlayText();
        private readonly WheelDebugInfo[] _wheels = new WheelDebugInfo[MaxWheels];
        private readonly Stopwatch _cost = new Stopwatch();

        private VehicleController _target;
        private bool _visible;
        private float _refreshTimer;
        private float _searchTimer;
        private float _smoothedDt = 1f / 60f;
        private float _costMs;
        private float _guiCostMs;
        private string _cached = string.Empty;
        private GUIStyle _style;
        private Rect _rect = new Rect(12f, 12f, 320f, 190f);

        private static VehicleDebugOverlay _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Survives "Enter Play Mode without domain reload".
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[Debug Overlay]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<VehicleDebugOverlay>();
        }

        private void Update()
        {
            _cost.Restart();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) _visible = !_visible;

            _smoothedDt += (Time.unscaledDeltaTime - _smoothedDt) * FpsSmoothing;

            if (_visible)
            {
                _searchTimer -= Time.unscaledDeltaTime;
                if (_target == null && _searchTimer <= 0f)
                {
                    _target = FindAnyObjectByType<VehicleController>();
                    _searchTimer = VehicleSearchInterval;
                }

                _refreshTimer -= Time.unscaledDeltaTime;
                if (_refreshTimer <= 0f)
                {
                    _refreshTimer = TextRefreshInterval;
                    RebuildText();
                }
            }

            _cost.Stop();
            // Reported cost = this Update + last frame's OnGUI, so the < 0.5 ms AC can be read live.
            _costMs = (float)_cost.Elapsed.TotalMilliseconds + _guiCostMs;
        }

        private void RebuildText()
        {
            if (_target == null || _target.Config == null)
            {
                _cached = "No vehicle in scene   <size=11>F1 hide</size>";
                return;
            }

            int count = Mathf.Min(_target.WheelCount, MaxWheels);
            for (int i = 0; i < count; i++)
                _wheels[i].Compression01 = _target.GetWheelCompression01(i);

            VehicleConfig config = _target.Config;
            _cached = _text.Build(_target.name, _target.State, config.topSpeed, _wheels, count,
                config.autoRightDelay, DebugOverlayText.Fps(_smoothedDt), _costMs);
        }

        private void OnGUI()
        {
            if (!_visible) return;
            if (Event.current.type == EventType.Layout) _guiCostMs = 0f;
            long start = Stopwatch.GetTimestamp();

            _style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                richText = true,
                fontSize = 13,
                padding = new RectOffset(10, 10, 8, 8),
                normal = { textColor = Color.white },
            };

            GUI.Box(_rect, _cached, _style);
            _guiCostMs += (float)((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
        }
    }
}
#endif
