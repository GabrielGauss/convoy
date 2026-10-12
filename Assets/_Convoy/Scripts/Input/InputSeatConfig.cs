using Convoy.Core;
using UnityEngine;

namespace Convoy.Input
{
    /// <summary>
    /// Tuning for per-seat human input (F-03). A ScriptableObject so feel numbers live in an asset, not in code
    /// (project rule). The Pilot defaults equal PilotInputSource's serialized defaults so driving feels identical.
    /// </summary>
    [CreateAssetMenu(fileName = "InputSeatConfig", menuName = "Convoy/Input Seat Config")]
    public sealed class InputSeatConfig : ScriptableObject
    {
        [Tooltip("Join keyboard+mouse as player 0 at scene start, so Sandbox/Sprint work with zero clicks. " +
                 "Turn off to let a gamepad take the Pilot seat first.")]
        public bool autoJoinKeyboard = true;

        [Tooltip("Seat given to each joining player, in order.")]
        public SeatId[] joinOrder = { SeatId.Pilot, SeatId.Gunner, SeatId.Engineer };

        [Header("Pilot (same numbers as PilotInputSource)")]
        [Tooltip("Keyboard steering ramp toward full lock, per second (= PilotInputSource._keyboardSteerSpeed).")]
        [Min(0.1f)] public float keyboardSteerRampUp = 4f;
        [Tooltip("Keyboard steering return to centre, per second (= PilotInputSource._keyboardSteerReturn).")]
        [Min(0.1f)] public float keyboardSteerReturn = 6f;
        [Tooltip("Gamepad steer stick deadzone (= PilotInputSource._stickDeadzone).")]
        [Range(0f, 0.5f)] public float stickDeadzone = 0.12f;

        [Header("Gunner aim")]
        [Tooltip("Mouse: degrees of aim per pixel of mouse movement (frame-rate independent by nature).")]
        [Min(0f)] public float mouseAimDegreesPerPixel = 0.12f;
        [Tooltip("Right stick: degrees per second at full deflection (multiplied by Time.deltaTime).")]
        [Min(0f)] public float stickAimDegreesPerSecond = 180f;
        [Range(-89f, 0f)] public float minAimPitch = -30f;
        [Range(0f, 89f)] public float maxAimPitch = 60f;
    }
}
