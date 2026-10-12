using UnityEngine;
using UnityEngine.InputSystem;

namespace Convoy.Presentation
{
    /// <summary>
    /// Game music (AUD-01): plays the soundtrack tracks in order and loops, surviving scene reloads
    /// (sprint restarts don't restart the song). M toggles mute. Self-bootstrapping, no scene wiring.
    /// Tracks live in Resources/Audio/Music.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class MusicPlayer : MonoBehaviour
    {
        private static readonly string[] Tracks = { "Audio/Music/song1", "Audio/Music/song2" };
        private const float Volume = 0.35f;

        private static MusicPlayer _instance;

        private AudioSource _source;
        private AudioClip[] _clips;
        private int _index = -1;
        private bool _muted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[Music]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MusicPlayer>();
        }

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.spatialBlend = 0f;   // music is not positioned in the world
            _source.volume = Volume;
            _source.playOnAwake = false;
            _clips = new AudioClip[Tracks.Length];
            for (int i = 0; i < Tracks.Length; i++)
            {
                _clips[i] = Resources.Load<AudioClip>(Tracks[i]);
                if (_clips[i] == null) Debug.LogWarning($"MusicPlayer: missing track Resources/{Tracks[i]}", this);
            }
            PlayNext();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
            {
                _muted = !_muted;
                _source.mute = _muted;
            }

            // AudioSource.isPlaying goes false at the end of a track (and while the app is unfocused
            // with "Run in Background" off), so also check the playback position before advancing.
            if (!_source.isPlaying && _source.clip != null && _source.time <= 0f && Application.isFocused)
                PlayNext();
        }

        private void PlayNext()
        {
            for (int tries = 0; tries < _clips.Length; tries++)
            {
                _index = (_index + 1) % _clips.Length;
                if (_clips[_index] == null) continue;
                _source.clip = _clips[_index];
                _source.Play();
                return;
            }
        }
    }
}
