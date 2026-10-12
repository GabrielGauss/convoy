using Convoy.Race;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Convoy.Presentation
{
    /// <summary>
    /// Sprint timer (top centre), checkpoint counter and the "monolith reached" result screen (S-01).
    /// Spawns itself when a <see cref="SprintController"/> is in the scene. Reads race state only;
    /// the restart button asks the controller to reload (D-007: presentation never writes simulation).
    /// </summary>
    [AddComponentMenu("")]
    public sealed class SprintHud : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.06f, 0.05f, 0.05f, 0.85f);
        private static readonly Color Accent = new Color(1f, 0.55f, 0.15f, 1f);
        private static readonly Color Cyan = new Color(0.45f, 0.9f, 1f, 1f);

        private SprintController _sprint;
        private Texture2D _white;
        private GUIStyle _timer, _small, _title, _big, _hint;
        private float _finishedAt = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManagerHook.Install();
        }

        /// <summary>Spawns a HUD for each loaded scene that contains a sprint (also after a restart reload).</summary>
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
                var sprint = UnityEngine.Object.FindAnyObjectByType<SprintController>();
                if (sprint == null || sprint.GetComponent<SprintHud>() != null) return;
                sprint.gameObject.AddComponent<SprintHud>();
            }
        }

        private void Awake()
        {
            _sprint = GetComponent<SprintController>();
            _sprint.Finished += OnFinished;
        }

        private void OnDestroy()
        {
            if (_sprint != null) _sprint.Finished -= OnFinished;
            if (_white != null) Destroy(_white);
        }

        private void OnFinished() => _finishedAt = Time.unscaledTime;

        private void Update()
        {
            if (_sprint.Run == null || _sprint.Run.Phase != SprintPhase.Finished) return;
            if (Time.unscaledTime - _finishedAt < 1f) return;   // don't skip the result by accident

            bool keyboard = Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
            bool pad = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
            if (keyboard || pad) _sprint.Restart();
        }

        private void OnGUI()
        {
            SprintRun run = _sprint.Run;
            if (run == null || Event.current.type != EventType.Repaint) return;
            EnsureStyles();

            // --- Timer + checkpoints, top centre ---
            float w = 260f;
            var panel = new Rect((Screen.width - w) * 0.5f, 12f, w, 66f);
            Fill(panel, PanelColor);
            string timeText = run.Phase == SprintPhase.Ready ? "READY" : SprintRun.FormatTime(run.Elapsed);
            GUI.Label(new Rect(panel.x, panel.y + 2f, w, 40f), timeText, _timer);
            string cp = "CHECKPOINT " + (run.LastCheckpoint + 1) + " / " + run.CheckpointCount;
            if (run.BestTime > 0f) cp += "     BEST " + SprintRun.FormatTime(run.BestTime);
            GUI.Label(new Rect(panel.x, panel.y + 42f, w, 20f), cp, _small);

            if (run.Phase != SprintPhase.Finished) return;

            // --- Result screen ---
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.45f));
            float rw = 520f, rh = 230f;
            var r = new Rect((Screen.width - rw) * 0.5f, (Screen.height - rh) * 0.5f, rw, rh);
            Fill(r, PanelColor);
            Fill(new Rect(r.x, r.y, rw, 4f), Accent);
            GUI.Label(new Rect(r.x, r.y + 20f, rw, 40f), "MONOLITH REACHED", _title);
            GUI.Label(new Rect(r.x, r.y + 70f, rw, 64f), SprintRun.FormatTime(run.Elapsed), _big);
            GUI.Label(new Rect(r.x, r.y + 140f, rw, 24f),
                run.IsNewBest ? "NEW BEST TIME" : "BEST " + SprintRun.FormatTime(run.BestTime), _small);
            GUI.Label(new Rect(r.x, r.y + 185f, rw, 24f), "ENTER / START  —  run again", _hint);
        }

        private void Fill(Rect rect, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(rect, _white);
            GUI.color = Color.white;
        }

        private void EnsureStyles()
        {
            if (_white == null)
            {
                _white = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
            }
            if (_timer != null) return;
            _timer = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 30, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _small = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = Accent } };
            _title = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 26, fontStyle = FontStyle.Bold, normal = { textColor = Cyan } };
            _big = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 54, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _hint = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 13, normal = { textColor = new Color(1f, 1f, 1f, 0.6f) } };
        }
    }
}
