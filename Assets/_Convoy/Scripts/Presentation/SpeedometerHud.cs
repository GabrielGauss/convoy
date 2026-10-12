using Convoy.Vehicle;
using UnityEngine;

namespace Convoy.Presentation
{
    /// <summary>
    /// U-01 (first slice): analogue speedometer with digital km/h and a nitro bar above it.
    /// Prototype HUD drawn with IMGUI from procedurally generated textures, so it needs no assets
    /// or scene wiring. Reads <see cref="VehicleState"/> only (D-007). U-01 will move this to a
    /// per-seat UI Toolkit HUD; keep the look, not the implementation.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class SpeedometerHud : MonoBehaviour
    {
        private const int DialTextureSize = 256;
        private const float DialStartDeg = -135f;
        private const float DialSweepDeg = 270f;
        private const float DialStepKmh = 20f;
        private const float DialSize = 210f;
        private const float Margin = 24f;
        private const float BarHeight = 14f;
        private const float BarGap = 10f;
        private const float VehicleSearchInterval = 1f;

        private static readonly Color DialFace = new Color(0.07f, 0.07f, 0.08f, 0.82f);
        private static readonly Color DialRim = new Color(0.55f, 0.52f, 0.46f, 1f);
        private static readonly Color TickColor = new Color(1f, 0.72f, 0.25f, 1f);
        private static readonly Color RedZone = new Color(0.9f, 0.2f, 0.15f, 1f);
        private static readonly Color NeedleColor = new Color(1f, 0.3f, 0.2f, 1f);
        private static readonly Color BarBack = new Color(0.07f, 0.07f, 0.08f, 0.82f);
        private static readonly Color BarReady = new Color(0.25f, 0.8f, 1f, 1f);
        private static readonly Color BarBoosting = new Color(1f, 0.55f, 0.1f, 1f);
        private static readonly Color BarLow = new Color(0.6f, 0.25f, 0.25f, 1f);

        private static SpeedometerHud _instance;

        private VehicleController _target;
        private float _searchTimer;
        private float _dialMax = -1f;
        private Texture2D _dial;
        private Texture2D _white;
        private GUIStyle _speedStyle;
        private GUIStyle _unitStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _tickStyle;
        private readonly string[] _speedStrings = new string[1000];
        private string[] _tickLabels = System.Array.Empty<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[HUD Speedometer]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SpeedometerHud>();
        }

        private void Update()
        {
            if (_target != null) return;
            _searchTimer -= Time.unscaledDeltaTime;
            if (_searchTimer > 0f) return;
            _searchTimer = VehicleSearchInterval;
            _target = FindAnyObjectByType<VehicleController>();
        }

        private void OnDestroy()
        {
            if (_dial != null) Destroy(_dial);
            if (_white != null) Destroy(_white);
        }

        private void OnGUI()
        {
            if (_target == null || _target.Config == null) return;
            if (Event.current.type != EventType.Repaint) return;

            VehicleConfig config = _target.Config;
            VehicleState state = _target.State;
            EnsureResources(config);

            float x = Screen.width - DialSize - Margin;
            float y = Screen.height - DialSize - Margin;
            var dialRect = new Rect(x, y, DialSize, DialSize);
            Vector2 centre = dialRect.center;

            // --- Nitro bar above the dial ---
            var barRect = new Rect(x, y - BarGap - BarHeight, DialSize, BarHeight);
            DrawRect(barRect, BarBack);
            Color fill = state.Boosting ? BarBoosting
                : state.BoostFraction < config.boostMinToStart ? BarLow
                : BarReady;
            var inner = new Rect(barRect.x + 2f, barRect.y + 2f, (barRect.width - 4f) * Mathf.Clamp01(state.BoostFraction), barRect.height - 4f);
            DrawRect(inner, fill);
            GUI.Label(new Rect(barRect.x, barRect.y - 18f, DialSize, 18f), state.Boosting ? "NITRO >>>" : "NITRO  [Shift]", _labelStyle);

            // --- Dial face + ticks ---
            GUI.DrawTexture(dialRect, _dial);
            DrawTickLabels(centre);

            // --- Needle ---
            float kmh = Mathf.Abs(state.ForwardSpeed) * 3.6f;
            float angle = GaugeMath.NeedleAngle(kmh, _dialMax, DialStartDeg, DialSweepDeg);
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, centre);
            float needleLength = DialSize * 0.40f;
            GUI.color = NeedleColor;
            GUI.DrawTexture(new Rect(centre.x - 2f, centre.y - needleLength, 4f, needleLength + 12f), _white);
            GUI.color = Color.white;
            GUI.matrix = saved;
            DrawRect(new Rect(centre.x - 7f, centre.y - 7f, 14f, 14f), DialRim);

            // --- Digital readout ---
            int display = Mathf.Min(GaugeMath.DisplaySpeed(kmh), _speedStrings.Length - 1);
            _speedStrings[display] ??= display.ToString();
            GUI.Label(new Rect(x, centre.y + DialSize * 0.12f, DialSize, 40f), _speedStrings[display], _speedStyle);
            GUI.Label(new Rect(x, centre.y + DialSize * 0.12f + 36f, DialSize, 18f), "km/h", _unitStyle);
        }

        private void DrawTickLabels(Vector2 centre)
        {
            float radius = DialSize * 0.34f;
            for (int i = 0; i < _tickLabels.Length; i++)
            {
                float value = i * DialStepKmh;
                float a = GaugeMath.NeedleAngle(value, _dialMax, DialStartDeg, DialSweepDeg) * Mathf.Deg2Rad;
                var p = new Vector2(centre.x + Mathf.Sin(a) * radius, centre.y - Mathf.Cos(a) * radius);
                GUI.Label(new Rect(p.x - 20f, p.y - 9f, 40f, 18f), _tickLabels[i], _tickStyle);
            }
        }

        private void DrawRect(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = Color.white;
        }

        private void EnsureResources(VehicleConfig config)
        {
            if (_white == null)
            {
                _white = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
            }

            float max = GaugeMath.DialMax((config.topSpeed + config.boostTopSpeedBonus) * 3.6f, DialStepKmh);
            if (_dial == null || !Mathf.Approximately(max, _dialMax))
            {
                _dialMax = max;
                if (_dial != null) Destroy(_dial);
                _dial = BuildDialTexture(_dialMax, config.topSpeed * 3.6f);
                int labels = Mathf.RoundToInt(_dialMax / DialStepKmh) + 1;
                _tickLabels = new string[labels];
                for (int i = 0; i < labels; i++) _tickLabels[i] = (i * DialStepKmh).ToString("0");
            }

            if (_speedStyle == null)
            {
                _speedStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 34, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
                _unitStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = new Color(1f, 1f, 1f, 0.6f) } };
                _labelStyle = new GUIStyle { alignment = TextAnchor.MiddleLeft, fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = TickColor } };
                _tickStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 11, normal = { textColor = new Color(1f, 1f, 1f, 0.75f) } };
            }
        }

        /// <summary>Dark dial face, rim, ticks every 10 km/h (major every 20), red zone above the un-boosted top speed.</summary>
        private static Texture2D BuildDialTexture(float dialMax, float redFromKmh)
        {
            int n = DialTextureSize;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f;
            float rOuter = n * 0.5f - 1f;

            for (int yTex = 0; yTex < n; yTex++)
            for (int xTex = 0; xTex < n; xTex++)
            {
                float dx = xTex - c;
                float dyGui = (n - 1 - yTex) - c;          // GUI y grows downwards
                float r = Mathf.Sqrt(dx * dx + dyGui * dyGui);
                Color col = Color.clear;

                if (r <= rOuter)
                {
                    col = DialFace;
                    if (r > rOuter - 5f) col = DialRim;
                    else
                    {
                        // Angle clockwise from straight up, -180..180.
                        float a = Mathf.Atan2(dx, -dyGui) * Mathf.Rad2Deg;
                        float rel = a - DialStartDeg;
                        if (rel < 0f) rel += 360f;
                        if (rel <= DialSweepDeg)
                        {
                            float value = rel / DialSweepDeg * dialMax;
                            bool inRed = value >= redFromKmh;
                            // Arc band
                            if (r > rOuter - 13f && r < rOuter - 9f) col = inRed ? RedZone : new Color(TickColor.r, TickColor.g, TickColor.b, 0.35f);
                            // Ticks
                            float pxPerDeg = r * Mathf.Deg2Rad;
                            float degPer10 = DialSweepDeg / (dialMax / 10f);
                            float mod = rel % degPer10;
                            float distDeg = Mathf.Min(mod, degPer10 - mod);
                            bool major = Mathf.RoundToInt(rel / degPer10) % 2 == 0;
                            float len = major ? 18f : 10f;
                            if (distDeg * pxPerDeg < (major ? 1.6f : 1.0f) && r > rOuter - 7f - len && r < rOuter - 6f)
                                col = inRed ? RedZone : TickColor;
                        }
                    }
                }
                px[yTex * n + xTex] = col;
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
