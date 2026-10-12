using System;
using UnityEngine;

namespace Convoy.AI
{
    /// <summary>
    /// Obstacle avoidance by discrete lanes (ADR-004). Lanes are −1 (left), 0 (centre), +1 (right) × <c>laneWidth</c>,
    /// clamped to the route's half-width. The selector keeps its current lane while that lane is clear beyond
    /// <c>obstacleCheckDistance</c>; otherwise it picks the lane with the most free distance (ties → closest to
    /// centre, then the side nearer to where the car already is). The actual offset slides toward the chosen
    /// lane at <c>laneChangeRate</c> m/s, so the car never snaps sideways.
    /// Pure logic: the obstacle distances come from sphere-casts done by <c>AiPilot</c> (A-01b).
    /// </summary>
    public sealed class LaneSelector
    {
        private readonly AiDriverProfile _profile;
        private readonly float _halfWidth;
        private int _lane;

        /// <param name="halfWidth">Route half-width in metres; lane offsets never exceed it.</param>
        public LaneSelector(AiDriverProfile profile, float halfWidth)
        {
            _profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            _halfWidth = Math.Max(0f, halfWidth);
        }

        /// <summary>Current sideways offset from the path, metres, + = right of the path.</summary>
        public float Offset { get; private set; }

        /// <summary>Chosen lane: −1 left, 0 centre, +1 right.</summary>
        public int Lane => _lane;

        /// <summary>Offset the selector is moving toward, metres.</summary>
        public float TargetOffset => LaneOffset(_lane);

        /// <param name="obstacleLeft">Metres to the first obstacle in the left lane; <see cref="float.PositiveInfinity"/> = clear.</param>
        /// <param name="obstacleCentre">Same for the centre lane.</param>
        /// <param name="obstacleRight">Same for the right lane.</param>
        public void Step(float obstacleLeft, float obstacleCentre, float obstacleRight, float deltaTime)
        {
            float check = _profile.obstacleCheckDistance;
            // Everything beyond the check distance counts as equally "clear", so ties resolve by the rules below
            // instead of by which far-away obstacle happens to be a metre further.
            float left = Clear(obstacleLeft, check);
            float centre = Clear(obstacleCentre, check);
            float right = Clear(obstacleRight, check);

            if (Free(_lane, left, centre, right) < check)
            {
                // Candidate order encodes the tie-break: centre first, then the side the car is already on.
                int nearSide = Offset >= 0f ? 1 : -1;
                int best = 0;
                float bestFree = centre;
                float nearFree = Free(nearSide, left, centre, right);
                if (nearFree > bestFree) { best = nearSide; bestFree = nearFree; }
                if (Free(-nearSide, left, centre, right) > bestFree) best = -nearSide;
                _lane = best;
            }

            if (deltaTime > 0f)
                Offset = Mathf.MoveTowards(Offset, LaneOffset(_lane), _profile.laneChangeRate * deltaTime);
        }

        private float LaneOffset(int lane)
        {
            return Mathf.Clamp(lane * _profile.laneWidth, -_halfWidth, _halfWidth);
        }

        private static float Free(int lane, float left, float centre, float right)
        {
            return lane < 0 ? left : lane > 0 ? right : centre;
        }

        private static float Clear(float distance, float check)
        {
            // NaN (bad sensor data) is treated as blocked-at-zero rather than clear: safer to steer away.
            if (float.IsNaN(distance)) return 0f;
            return Math.Min(Math.Max(distance, 0f), check);
        }
    }
}
