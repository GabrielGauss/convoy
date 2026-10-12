using UnityEngine;

namespace Convoy.Input
{
    /// <summary>
    /// Smooths digital keyboard steering (-1/0/+1) so keys don't snap the wheels to full lock.
    /// Same maths as PilotInputSource.Update (rampUp 4/s, return 6/s by default). Plain C# class, not a
    /// MonoBehaviour: the caller owns one per player and passes Time.deltaTime in, which keeps it testable.
    /// </summary>
    public sealed class KeyboardSteerShaper
    {
        private readonly float _rampUpPerSecond;
        private readonly float _returnPerSecond;

        public KeyboardSteerShaper(float rampUpPerSecond, float returnPerSecond)
        {
            _rampUpPerSecond = rampUpPerSecond;
            _returnPerSecond = returnPerSecond;
        }

        /// <summary>Current shaped steer in [-1, 1].</summary>
        public float Value { get; private set; }

        /// <summary>Moves Value toward <paramref name="keys"/> (-1/0/+1 from the key composite) and returns it.</summary>
        public float Step(float keys, float deltaTime)
        {
            // Release, or reversing direction -> use the faster "return" rate; otherwise the slower "ramp up" rate.
            // Mathf.Sign(0) is +1 in Unity, so the first press to the LEFT from a centred wheel also uses the return rate.
            // That quirk exists in PilotInputSource, and we keep it so driving feel stays identical.
            float rate = Mathf.Approximately(keys, 0f) || Mathf.Sign(keys) != Mathf.Sign(Value)
                ? _returnPerSecond
                : _rampUpPerSecond;
            Value = Mathf.MoveTowards(Value, keys, rate * deltaTime);
            return Value;
        }

        public void Reset()
        {
            Value = 0f;
        }
    }
}
