using System;

namespace Convoy.AI
{
    /// <summary>What the AI driver is doing right now.</summary>
    public enum AiDriveMode
    {
        /// <summary>Held at the start (sends Neutral). Set by <see cref="AiDriverBrain.Held"/>, never by the detector.</summary>
        Holding,
        Driving,
        /// <summary>Backing out: full reverse with mirrored steering.</summary>
        Reversing,
        /// <summary>Asking the vehicle for a V-02 reset.</summary>
        RequestReset,
    }

    /// <summary>
    /// Stuck recovery state machine (ADR-004): <c>Driving → Reversing → RequestReset</c>.
    /// <list type="bullet">
    /// <item>Stuck = |speed| &lt; <c>stuckSpeed</c> for <c>stuckTime</c> while the brain asks for throttle &gt; 0.5
    /// → reverse for <c>reverseTime</c>, up to <c>maxReverseAttempts</c> times, then request a reset.</item>
    /// <item>Off the path by more than <c>offPathResetDistance</c> → request a reset immediately.</item>
    /// <item>Negative progress for <c>wrongWayTime</c> while driving → request a reset.</item>
    /// <item>The attempt counter clears after <c>stuckTime</c> seconds of healthy driving (moving and making progress),
    /// so one rock early in the race doesn't use up the attempts for a different rock later.</item>
    /// </list>
    /// The mode stays <see cref="AiDriveMode.RequestReset"/> until <see cref="NotifyResetIssued"/> is called.
    /// </summary>
    public sealed class StuckDetector
    {
        // "Asking for throttle" threshold from ADR-004: below this the driver is braking/coasting on purpose.
        private const float ThrottleIntent = 0.5f;

        private readonly AiDriverProfile _profile;
        private float _stuckTimer;
        private float _reverseTimer;
        private float _wrongWayTimer;
        private float _healthyTimer;

        public StuckDetector(AiDriverProfile profile)
        {
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            Mode = AiDriveMode.Driving;
        }

        public AiDriveMode Mode { get; private set; }

        /// <summary>Reverse attempts used since the last healthy stretch or reset.</summary>
        public int ReverseAttempts { get; private set; }

        /// <param name="speed">Signed forward speed, m/s.</param>
        /// <param name="requestedThrottle">Throttle the brain wants (before any recovery override), −1..1.</param>
        /// <param name="lateralOffset">Signed distance from the path, metres.</param>
        /// <param name="progressDelta">Change in progress along the path since last step, metres.</param>
        public void Step(float speed, float requestedThrottle, float lateralOffset, float progressDelta, float deltaTime)
        {
            if (deltaTime <= 0f || Mode == AiDriveMode.RequestReset) return;

            if (Math.Abs(lateralOffset) > _profile.offPathResetDistance)
            {
                Mode = AiDriveMode.RequestReset;
                return;
            }

            if (Mode == AiDriveMode.Reversing)
            {
                _reverseTimer += deltaTime;
                if (_reverseTimer >= _profile.reverseTime) EnterDriving();
                return;
            }

            // Driving. Negative progress is only "wrong way" here; while reversing it is expected.
            _wrongWayTimer = progressDelta < 0f ? _wrongWayTimer + deltaTime : 0f;
            if (_wrongWayTimer >= _profile.wrongWayTime && _profile.wrongWayTime > 0f)
            {
                Mode = AiDriveMode.RequestReset;
                return;
            }

            bool slow = Math.Abs(speed) < _profile.stuckSpeed;
            _stuckTimer = slow && requestedThrottle > ThrottleIntent ? _stuckTimer + deltaTime : 0f;

            _healthyTimer = !slow && progressDelta > 0f ? _healthyTimer + deltaTime : 0f;
            if (_healthyTimer >= _profile.stuckTime) ReverseAttempts = 0;

            if (_stuckTimer < _profile.stuckTime || _stuckTimer <= 0f) return;

            if (ReverseAttempts < _profile.maxReverseAttempts)
            {
                ReverseAttempts++;
                Mode = AiDriveMode.Reversing;
                _reverseTimer = 0f;
                _stuckTimer = 0f;
            }
            else
            {
                Mode = AiDriveMode.RequestReset;
            }
        }

        /// <summary>Call once the reset command has been sent: back to Driving with all timers and attempts cleared.</summary>
        public void NotifyResetIssued()
        {
            ReverseAttempts = 0;
            EnterDriving();
        }

        private void EnterDriving()
        {
            Mode = AiDriveMode.Driving;
            _stuckTimer = 0f;
            _reverseTimer = 0f;
            _wrongWayTimer = 0f;
            _healthyTimer = 0f;
        }
    }
}
