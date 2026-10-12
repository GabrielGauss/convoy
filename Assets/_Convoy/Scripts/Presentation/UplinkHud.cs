using Convoy.Race;
using UnityEngine;

namespace Convoy.Presentation
{
    /// <summary>
    /// Monolith uplink HUD (UP-01): bottom-centre 24-segment progress ring + "UPLINK 63%", and a
    /// "REACH THE MONOLITH" hint once every checkpoint is passed. Spawns itself next to a
    /// <see cref="SprintController"/> that uses the uplink (same scene hook pattern as SprintHud).
    /// Reads <see cref="UplinkHold.Progress01"/> / <see cref="UplinkHold.Inside"/> and run state only (D-007).
    /// </summary>
    [AddComponentMenu("")]
    public sealed class UplinkHud : MonoBehaviour
    {
        private const int SegmentCount = 24;
        private const float RingRadius = 46f;           // px, centre to segment middle
        private const float SegmentLength = 14f;        // px, radial
        private const float SegmentWidth = 7f;          // px, tangential
        private const float BottomMargin = 40f;         // px from the screen bottom to the panel
        private const float PanelSize = 150f;
        private const float LabelHeight = 22f;

        private static readonly Color PanelColor = new Color(0.06f, 0.05f, 0.05f, 0.85f);
        private static readonly Color Accent = new Color(1f, 0.55f, 0.15f, 1f);
        private static readonly Color Cyan = new Color(0.45f, 0.9f, 1f, 1f);
        private static readonly Color Unlit = new Color(1f, 1f, 1f, 0.15f);

        // OnGUI runs every frame; concatenating "UPLINK " + n + "%" there would allocate a string per frame.
        // Build all 101 labels once instead (spec AC 6: 0 B GC in OnGUI).
        private static readonly string[] PercentLabels = BuildPercentLabels();

        private SprintController _sprint;
        private Texture2D _quad;
        private GUIStyle _label, _hint;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManagerHook.Install();
        }

        /// <summary>Spawns a HUD for each loaded scene with an uplink sprint (also after a restart reload).</summary>
        private static class SceneManagerHook
        {
            private static bool _installed;

            public static void Install()
            {
                TrySpawn();
                if (_installed) return;
                _installed = true;
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => TrySpawn();
            }

            private static void TrySpawn()
            {
                var sprint = Object.FindAnyObjectByType<SprintController>();
                // AfterSceneLoad / sceneLoaded run after Awake, so UsesUplink is already decided.
                if (sprint == null || !sprint.UsesUplink || sprint.GetComponent<UplinkHud>() != null) return;
                sprint.gameObject.AddComponent<UplinkHud>();
            }
        }

        private static string[] BuildPercentLabels()
        {
            var labels = new string[101];
            for (int i = 0; i <= 100; i++) labels[i] = "UPLINK " + i + "%";
            return labels;
        }

        private void Awake()
        {
            _sprint = GetComponent<SprintController>();
        }

        private void OnDestroy()
        {
            if (_quad != null) Destroy(_quad);
        }

        private void OnGUI()
        {
            // Repaint only: OnGUI is also called for layout/input events, and drawing there is wasted work.
            if (_sprint == null || Event.current.type != EventType.Repaint) return;
            SprintRun run = _sprint.Run;
            UplinkHold hold = _sprint.Uplink;
            if (run == null || hold == null) return;
            if (run.Phase != SprintPhase.Running || !run.AllCheckpointsPassed) return;   // hidden when Finished
            EnsureStyles();

            float progress = hold.Progress01;
            var panel = new Rect((Screen.width - PanelSize) * 0.5f, Screen.height - BottomMargin - PanelSize,
                PanelSize, PanelSize);

            // Not in the zone and nothing held yet (Decay mode can leave progress while outside): show the goal.
            if (!hold.Inside && progress <= 0f)
            {
                var hintRect = new Rect(panel.x - 80f, panel.yMax - LabelHeight * 2f, PanelSize + 160f, LabelHeight * 2f);
                Fill(hintRect, PanelColor);
                GUI.Label(hintRect, "REACH THE MONOLITH", _hint);
                return;
            }

            Fill(panel, PanelColor);
            DrawRing(panel.center, UplinkRingMath.LitSegments(progress, SegmentCount), hold.Inside);

            int percent = Mathf.Clamp(Mathf.FloorToInt(progress * 100f), 0, 100);
            GUI.Label(new Rect(panel.x, panel.center.y - LabelHeight * 0.5f, PanelSize, LabelHeight),
                PercentLabels[percent], _label);
        }

        private void DrawRing(Vector2 centre, int lit, bool inside)
        {
            // IMGUI has no ring primitive: draw one quad per segment, rotating GUI.matrix around the ring centre.
            // Matrix4x4 is a struct, so saving/restoring it does not allocate.
            Matrix4x4 saved = GUI.matrix;
            Color litColor = inside ? Cyan : Accent;   // orange = progress decaying/paused outside the zone
            var seg = new Rect(centre.x - SegmentWidth * 0.5f, centre.y - RingRadius - SegmentLength * 0.5f,
                SegmentWidth, SegmentLength);
            float step = 360f / SegmentCount;
            for (int i = 0; i < SegmentCount; i++)
            {
                GUI.matrix = saved;
                GUIUtility.RotateAroundPivot(i * step, centre);
                GUI.color = i < lit ? litColor : Unlit;
                GUI.DrawTexture(seg, _quad);
            }
            GUI.matrix = saved;
            GUI.color = Color.white;
        }

        private void Fill(Rect rect, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(rect, _quad);
            GUI.color = Color.white;
        }

        private void EnsureStyles()
        {
            if (_quad == null)
            {
                // One procedural white texel, tinted per draw via GUI.color: panel and every segment share it.
                _quad = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _quad.SetPixel(0, 0, Color.white);
                _quad.Apply();
            }
            if (_label != null) return;
            _label = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _hint = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Cyan } };
        }
    }
}
