using System.Text;
using Convoy.Core;
using Convoy.Input;
using UnityEngine;

namespace Convoy.Presentation
{
    /// <summary>
    /// F-03 debug label: which device sits in which seat, top-right, e.g. "P0 Keyboard&amp;Mouse → Pilot",
    /// "P1 Gamepad (XInputControllerWindows) → Gunner (lost)", "Engineer: empty".
    /// Self-bootstrapping like <see cref="SpeedometerHud"/> (no scene wiring), and only in the Editor and
    /// development builds. Reads the router's bindings only (D-007: presentation never writes state) and rebuilds
    /// its string only when <see cref="SeatInputRouter.BindingsVersion"/> changes, so OnGUI allocates nothing
    /// in the steady state.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class SeatDebugLabel : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float RouterSearchInterval = 1f;
        private const float Margin = 12f;
        private const float Width = 420f;
        private const int FontSize = 14;

        private static readonly string[] SeatNames = { "Pilot", "Gunner", "Engineer" };
        private static SeatDebugLabel _instance;

        private readonly StringBuilder _builder = new StringBuilder(256);
        private readonly bool[] _seatTaken = new bool[Seats.Count];
        private SeatInputRouter _router;
        private float _searchTimer;
        private int _builtVersion = -1;
        private string _text = string.Empty;
        private GUIStyle _style;

        // Statics survive "Enter Play Mode" without a domain reload; reset them explicitly.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[Debug Seat Label]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SeatDebugLabel>();
        }

        private void Update()
        {
            if (_router == null)
            {
                // FindAnyObjectByType is slow; poll it at most once per second until a scene with crew input loads.
                _searchTimer -= Time.unscaledDeltaTime;
                if (_searchTimer > 0f) return;
                _searchTimer = RouterSearchInterval;
                _router = FindAnyObjectByType<SeatInputRouter>();
                _builtVersion = -1;
                if (_router == null) return;
            }

            if (_router.BindingsVersion != _builtVersion) Rebuild();
        }

        private void Rebuild()
        {
            _builtVersion = _router.BindingsVersion;
            _builder.Clear();
            for (int s = 0; s < _seatTaken.Length; s++) _seatTaken[s] = false;

            var bindings = _router.Bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                SeatBinding b = bindings[i];
                _builder.Append('P').Append(b.PlayerIndex).Append(' ').Append(b.DeviceLabel).Append(" → ");
                if (b.HasSeat)
                {
                    _builder.Append(SeatNames[(int)b.Seat]);
                    _seatTaken[(int)b.Seat] = true;
                }
                else
                {
                    _builder.Append("spectating");
                }
                if (b.DeviceLost) _builder.Append(" (lost)");
                _builder.Append('\n');
            }
            for (int s = 0; s < _seatTaken.Length; s++)
                if (!_seatTaken[s]) _builder.Append(SeatNames[s]).Append(": empty\n");

            // Gamepad Y/Triangle is Reset in the Pilot map but SwitchStation for Gunner/Engineer (spec risk note).
            _builder.Append("F2: cycle seat (keyboard) · Pad Y: Pilot reset / station switch");
            _text = _builder.ToString();
        }

        private void OnGUI()
        {
            if (_router == null || Event.current.type != EventType.Repaint) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = FontSize,
                    alignment = TextAnchor.UpperRight,
                    wordWrap = false,
                };
                _style.normal.textColor = Color.white;
            }

            GUI.Label(new Rect(Screen.width - Width - Margin, Margin, Width, Screen.height * 0.5f), _text, _style);
        }
#endif
    }
}
