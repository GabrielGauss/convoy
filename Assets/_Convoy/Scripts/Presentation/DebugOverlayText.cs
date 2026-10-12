using System.Globalization;
using System.Text;
using Convoy.Vehicle;

namespace Convoy.Presentation
{
    /// <summary>Per-wheel numbers the overlay shows next to <see cref="VehicleState"/>.</summary>
    public struct WheelDebugInfo
    {
        /// <summary>0 = fully extended, 1 = bottomed out.</summary>
        public float Compression01;
    }

    /// <summary>
    /// Builds the V-03 debug overlay text. Plain class (no MonoBehaviour) so it is unit-tested,
    /// and it reuses one <see cref="StringBuilder"/> to keep per-refresh allocations to the final string.
    /// </summary>
    public sealed class DebugOverlayText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly StringBuilder _sb = new StringBuilder(512);

        /// <summary>Speed as a fraction of the vehicle's top speed; stands in for RPM until there is an engine model.</summary>
        public static float SpeedFraction(float forwardSpeed, float topSpeed)
        {
            if (topSpeed <= 0f) return 0f;
            float f = forwardSpeed / topSpeed;
            return f < 0f ? 0f : f;
        }

        /// <summary>Frames per second from a smoothed frame time in seconds.</summary>
        public static float Fps(float smoothedDeltaTime)
        {
            return smoothedDeltaTime > 1e-5f ? 1f / smoothedDeltaTime : 0f;
        }

        public string Build(string vehicleName, in VehicleState state, float topSpeed,
            WheelDebugInfo[] wheels, int wheelCount, float autoRightDelay, float fps, float overlayCostMs)
        {
            _sb.Clear();
            _sb.Append("<b>").Append(vehicleName).Append("</b>   <size=11>F1 hide</size>\n");

            _sb.Append("Speed      ").Append(state.SpeedKmh.ToString("0", Inv)).Append(" km/h");
            _sb.Append("  (").Append((SpeedFraction(state.ForwardSpeed, topSpeed) * 100f).ToString("0", Inv)).Append("% top)\n");

            _sb.Append("Slip       ").Append(state.SlipAngle.ToString("0.0", Inv)).Append("°");
            if (state.Sliding) _sb.Append("  <color=#ffb000>SLIDING</color>");
            _sb.Append('\n');

            _sb.Append("Grounded   ").Append(state.GroundedWheels).Append('/').Append(state.TotalWheels);
            if (state.IsAirborne) _sb.Append("  <color=#5ac8ff>AIRBORNE</color>");
            _sb.Append('\n');

            _sb.Append("Susp %     ");
            for (int i = 0; i < wheelCount && i < wheels.Length; i++)
            {
                if (i > 0) _sb.Append("  ");
                _sb.Append((wheels[i].Compression01 * 100f).ToString("0", Inv));
            }
            _sb.Append('\n');

            // D-01: hull health. Reads VehicleState only (presentation never touches VehicleDamage directly).
            _sb.Append("HP         ").Append((state.Health01 * 100f).ToString("0", Inv)).Append('%');
            if (state.Wrecked) _sb.Append("  <color=#ff5a5a>WRECKED</color>");
            _sb.Append('\n');

            if (state.UpsideDownTime > 0f)
                _sb.Append("<color=#ff5a5a>Upside down ")
                   .Append(state.UpsideDownTime.ToString("0.0", Inv)).Append(" / ")
                   .Append(autoRightDelay.ToString("0.0", Inv)).Append(" s</color>\n");
            if (state.RecoveryCooldown > 0f)
                _sb.Append("Reset in   ").Append(state.RecoveryCooldown.ToString("0.0", Inv)).Append(" s\n");

            _sb.Append("FPS        ").Append(fps.ToString("0", Inv));
            _sb.Append("   overlay ").Append(overlayCostMs.ToString("0.00", Inv)).Append(" ms");
            return _sb.ToString();
        }
    }
}
