using Convoy.Presentation;
using Convoy.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace Convoy.Tests
{
    public class DebugOverlayTextTests
    {
        private static VehicleState Driving(float forward = 20f)
        {
            return new VehicleState
            {
                Velocity = new Vector3(0f, 0f, forward),
                ForwardSpeed = forward,
                SlipAngle = 4.26f,
                GroundedWheels = 4,
                TotalWheels = 4,
            };
        }

        private static WheelDebugInfo[] Wheels(params float[] c)
        {
            var w = new WheelDebugInfo[c.Length];
            for (int i = 0; i < c.Length; i++) w[i].Compression01 = c[i];
            return w;
        }

        [Test]
        public void SpeedFraction_IsForwardOverTop()
        {
            Assert.That(DebugOverlayText.SpeedFraction(20f, 40f), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void SpeedFraction_IsZeroInReverseOrWithoutTopSpeed()
        {
            Assert.That(DebugOverlayText.SpeedFraction(-5f, 40f), Is.EqualTo(0f));
            Assert.That(DebugOverlayText.SpeedFraction(10f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void Fps_InvertsFrameTime()
        {
            Assert.That(DebugOverlayText.Fps(1f / 50f), Is.EqualTo(50f).Within(0.01f));
            Assert.That(DebugOverlayText.Fps(0f), Is.EqualTo(0f));
        }

        [Test]
        public void Build_ShowsCoreNumbers()
        {
            string text = new DebugOverlayText().Build("Jackal", Driving(20f), 40f,
                Wheels(0.1f, 0.2f, 0.3f, 0.4f), 4, 2f, 60f, 0.12f);

            StringAssert.Contains("Jackal", text);
            StringAssert.Contains("72 km/h", text);     // 20 m/s
            StringAssert.Contains("50% top", text);
            StringAssert.Contains("4.3°", text);       // invariant culture: dot decimal
            StringAssert.Contains("4/4", text);
            StringAssert.Contains("10  20  30  40", text);
            StringAssert.Contains("FPS        60", text);
            StringAssert.Contains("0.12 ms", text);
        }

        [Test]
        public void Build_FlagsAirborneAndSliding()
        {
            VehicleState s = Driving();
            s.GroundedWheels = 0;
            s.Sliding = true;
            string text = new DebugOverlayText().Build("Jackal", s, 40f, Wheels(0f, 0f, 0f, 0f), 4, 2f, 60f, 0f);

            StringAssert.Contains("AIRBORNE", text);
            StringAssert.Contains("SLIDING", text);
        }

        [Test]
        public void Build_ShowsRecoveryOnlyWhenActive()
        {
            var overlay = new DebugOverlayText();
            string calm = overlay.Build("Jackal", Driving(), 40f, Wheels(0f), 1, 2f, 60f, 0f);
            Assert.That(calm, Does.Not.Contain("Upside down"));
            Assert.That(calm, Does.Not.Contain("Reset in"));

            VehicleState s = Driving();
            s.UpsideDownTime = 1.2f;
            s.RecoveryCooldown = 2.5f;
            string flipped = overlay.Build("Jackal", s, 40f, Wheels(0f), 1, 2f, 60f, 0f);
            StringAssert.Contains("Upside down 1.2 / 2.0 s", flipped);
            StringAssert.Contains("Reset in   2.5 s", flipped);
        }

        [Test]
        public void Build_ShowsHealthPercent()
        {
            VehicleState s = Driving();
            s.Health01 = 0.42f;
            string text = new DebugOverlayText().Build("Jackal", s, 40f, Wheels(0f), 1, 2f, 60f, 0f);

            StringAssert.Contains("HP         42%", text);
            Assert.That(text, Does.Not.Contain("WRECKED"));
        }

        [Test]
        public void Build_FlagsWrecked()
        {
            VehicleState s = Driving();
            s.Health01 = 0f;
            s.Wrecked = true;
            string text = new DebugOverlayText().Build("Jackal", s, 40f, Wheels(0f), 1, 2f, 60f, 0f);

            StringAssert.Contains("HP         0%", text);
            StringAssert.Contains("WRECKED", text);
        }

        [Test]
        public void Build_ToleratesFewerWheelsThanCount()
        {
            Assert.DoesNotThrow(() =>
                new DebugOverlayText().Build("Jackal", Driving(), 40f, Wheels(0.5f), 4, 2f, 60f, 0f));
        }
    }
}
