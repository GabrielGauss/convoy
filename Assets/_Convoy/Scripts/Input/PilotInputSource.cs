using Convoy.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Convoy.Input
{
    /// <summary>
    /// Temporary pilot input for the V-01 handling sandbox: keyboard and any gamepad drive one vehicle.
    /// Bindings are defined in code so the sandbox works with zero editor setup.
    /// F-03 replaces this with a per-seat Input Actions asset and device assignment.
    ///
    /// Keyboard: W/S or ↑/↓ throttle/brake · A/D or ←/→ steer · Space handbrake · R reset
    /// Gamepad:  RT throttle · LT brake/reverse · left stick steer · A/Cross handbrake · Y/Triangle reset
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PilotInputSource : MonoBehaviour
    {
        [Tooltip("Object with a component that receives Pilot commands (the vehicle).")]
        [SerializeField] private GameObject _target;

        [Header("Feel")]
        [Tooltip("How fast keyboard steering moves toward full lock, per second. Gamepad uses the stick directly.")]
        [Min(0.1f)] [SerializeField] private float _keyboardSteerSpeed = 4f;
        [Tooltip("How fast keyboard steering returns to centre, per second.")]
        [Min(0.1f)] [SerializeField] private float _keyboardSteerReturn = 6f;
        [Range(0f, 0.5f)] [SerializeField] private float _stickDeadzone = 0.12f;

        private IPilotCommandReceiver _receiver;
        private InputAction _accelerate;
        private InputAction _brake;
        private InputAction _steerKeys;
        private InputAction _steerStick;
        private InputAction _handbrake;
        private InputAction _reset;
        private float _keyboardSteer;

        private void Awake()
        {
            if (_target != null) _receiver = _target.GetComponent<IPilotCommandReceiver>();
            if (_receiver == null)
                Debug.LogError($"{name}: PilotInputSource needs a target with an IPilotCommandReceiver (e.g. VehicleController).", this);

            _accelerate = new InputAction("Accelerate", InputActionType.Value);
            _accelerate.AddBinding("<Keyboard>/w");
            _accelerate.AddBinding("<Keyboard>/upArrow");
            _accelerate.AddBinding("<Gamepad>/rightTrigger");

            _brake = new InputAction("Brake", InputActionType.Value);
            _brake.AddBinding("<Keyboard>/s");
            _brake.AddBinding("<Keyboard>/downArrow");
            _brake.AddBinding("<Gamepad>/leftTrigger");

            _steerKeys = new InputAction("SteerKeys", InputActionType.Value);
            _steerKeys.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/d");
            _steerKeys.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");

            _steerStick = new InputAction("SteerStick", InputActionType.Value, "<Gamepad>/leftStick/x");

            _handbrake = new InputAction("Handbrake", InputActionType.Button);
            _handbrake.AddBinding("<Keyboard>/space");
            _handbrake.AddBinding("<Gamepad>/buttonSouth");

            _reset = new InputAction("Reset", InputActionType.Button);
            _reset.AddBinding("<Keyboard>/r");
            _reset.AddBinding("<Gamepad>/buttonNorth");
        }

        private void OnEnable()
        {
            _accelerate.Enable();
            _brake.Enable();
            _steerKeys.Enable();
            _steerStick.Enable();
            _handbrake.Enable();
            _reset.Enable();
        }

        private void OnDisable()
        {
            _accelerate.Disable();
            _brake.Disable();
            _steerKeys.Disable();
            _steerStick.Disable();
            _handbrake.Disable();
            _reset.Disable();
            _receiver?.SetPilotCommand(PilotCommand.Neutral);
        }

        private void OnDestroy()
        {
            _accelerate.Dispose();
            _brake.Dispose();
            _steerKeys.Dispose();
            _steerStick.Dispose();
            _handbrake.Dispose();
            _reset.Dispose();
        }

        private void Update()
        {
            if (_receiver == null) return;

            float throttle = _accelerate.ReadValue<float>() - _brake.ReadValue<float>();

            // Keyboard steering ramps so digital keys don't snap the wheels to full lock.
            float keys = _steerKeys.ReadValue<float>();
            float rate = Mathf.Approximately(keys, 0f) || Mathf.Sign(keys) != Mathf.Sign(_keyboardSteer)
                ? _keyboardSteerReturn
                : _keyboardSteerSpeed;
            _keyboardSteer = Mathf.MoveTowards(_keyboardSteer, keys, rate * Time.deltaTime);

            float stick = _steerStick.ReadValue<float>();
            if (Mathf.Abs(stick) < _stickDeadzone) stick = 0f;

            float steer = Mathf.Abs(stick) > Mathf.Abs(_keyboardSteer) ? stick : _keyboardSteer;

            _receiver.SetPilotCommand(new PilotCommand
            {
                Throttle = throttle,
                Steer = steer,
                Handbrake = _handbrake.IsPressed(),
                Reset = _reset.WasPressedThisFrame(),
            });
        }
    }
}
