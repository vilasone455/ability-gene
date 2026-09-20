using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Vault Strike: when each part happens, where the pawn is in the air and where the pole's two
    /// ends are. Seconds in, cells out, no drawing and no map. The defaults of the lab's
    /// power-pole-vault-strike.js. A place is (along, height): cells from the start cell toward the
    /// target, and cells above the ground.
    ///
    /// Distance is the preview's script. The ability takes range, damage, stun and the stagger
    /// radius from XML; StaggerRadius here only sizes the preview's ring and cracks.
    /// </summary>
    public static class PowerPoleStrikeTiming
    {
        public const float Windup = 0.3f, Flight = 0.9f, WhipTime = 0.25f, StrikeTime = 0.15f, StrikeEarly = 0.05f, Pinned = 0.15f,
            Retract = 0.25f, Tail = 1.2f;
        public const float Peak = 2.5f, LandShort = 1f, StaggerRadius = 1.5f, Range = 10f;
        /// <summary>Where the tip is planted, cells ahead of the pawn.</summary>
        public const float Foot = 0.4f;
        /// <summary>The pole's hand end while the pawn hangs on it, from the pawn's feet.</summary>
        public const float TopBack = -0.1f, TopHeight = 1.1f;
        /// <summary>Hands over the head in the air, and the pole left behind the hands.</summary>
        public const float HandsHigh = 0.95f, HandsBack = -0.3f, Butt = 0.3f;
        /// <summary>Pole length and angle (degrees up from the aim) held while falling.</summary>
        public const float WhipLong = 3f, RaisedAngle = 70f;
        /// <summary>North and south aims: cells east per cell of height, so the pole does not collapse to a dot.</summary>
        public const float Bow = 0.45f;
        public const float LaunchShake = 0.04f, StrikeShake = 0.15f, LandShake = 0.04f;
        public const int Cracks = 9, CrackPoints = 5, FanSteps = 10;
        // The preview's script.
        public const float Distance = 8f;
        public static readonly float[] FanSpans = { 0.12f, 0.07f, 0.035f };

        public static float LaunchAt => Windup;
        public static float PeakAt => LaunchAt + Flight / 2f;
        public static float LandAt => LaunchAt + Flight;
        public static float StrikeHitAt => LandAt - StrikeEarly;
        public static float StrikeStartAt => StrikeHitAt - StrikeTime;
        public static float WhipEndAt => Mathf.Min(PeakAt + WhipTime, StrikeStartAt);
        public static float PinEndAt => LandAt + Pinned;
        public static float HomeAt => PinEndAt + Retract;
        public static float Duration => HomeAt + Tail;
        public static float Landing => Distance - LandShort;

        /// <summary>The pawn's feet: on the ground, a ballistic arc at constant ground speed, on the ground one cell short of the target.</summary>
        public static Vector2 PawnAt(float time)
        {
            float u = Mathf.Clamp01((time - LaunchAt) / Flight);
            return new Vector2(Landing * u, Peak * 4f * u * (1f - u));
        }

        /// <summary>The hands once they are above the head: they come forward during the whip and drop to carry height at the end.</summary>
        public static Vector2 HandsAt(float time)
        {
            Vector2 pawn = PawnAt(time);
            return new Vector2(pawn.x + Mathf.Lerp(HandsBack, 0f, PowerPoleGraphics.Smooth01((time - PeakAt) / WhipTime)),
                pawn.y + Mathf.Lerp(HandsHigh, PowerPoleGraphics.HandHeight, PowerPoleGraphics.Smooth01((time - PinEndAt) / Retract)));
        }

        /// <summary>
        /// The pole's two ends. <paramref name="tip"/> is the end that is planted and later strikes,
        /// <paramref name="grip"/> the end in the hands. Returns true while the pole is swung from the hands.
        /// </summary>
        public static bool PoleAt(float time, out Vector2 tip, out Vector2 grip)
        {
            Vector2 pawn = PawnAt(time), planted = new Vector2(Foot, 0f), target = new Vector2(Distance, 0f);
            Vector2 carryTip = new Vector2(pawn.x + PowerPoleGraphics.RestTip, pawn.y + PowerPoleGraphics.HandHeight);
            Vector2 carryGrip = new Vector2(pawn.x + PowerPoleGraphics.RestBack, pawn.y + PowerPoleGraphics.HandHeight);
            Vector2 top = new Vector2(pawn.x + TopBack, pawn.y + TopHeight);
            if (time < LaunchAt)
            {
                float k = PowerPoleGraphics.Smooth01(time / Windup);
                tip = Vector2.Lerp(carryTip, planted, k);
                grip = Vector2.Lerp(carryGrip, top, k);
                return false;
            }
            if (time < PeakAt)
            {
                tip = planted;
                grip = top;
                return false;
            }

            // Angles are in the along-height plane, 0 along the aim and 90 straight up. The whip goes from
            // pointing down and back at the planted tip, over the top, to RaisedAngle: the angle only falls.
            Vector2 hands = HandsAt(time), peakHands = HandsAt(PeakAt);
            float whipFrom = Mathf.Atan2(-peakHands.y, Foot - peakHands.x) * Mathf.Rad2Deg, whipFromLength = (planted - peakHands).magnitude;
            float raised = RaisedAngle - 360f, toTarget = Mathf.Atan2(-hands.y, Distance - hands.x) * Mathf.Rad2Deg - 360f;
            float targetLength = (target - hands).magnitude, angle, length;
            if (time < WhipEndAt)
            {
                float k = PowerPoleGraphics.Smooth01((time - PeakAt) / (WhipEndAt - PeakAt));
                angle = Mathf.Lerp(whipFrom, raised, k);
                length = Mathf.Lerp(whipFromLength, WhipLong, k);
            }
            else if (time < StrikeStartAt) { angle = raised; length = WhipLong; }
            else if (time < StrikeHitAt)
            {
                float k = (time - StrikeStartAt) / StrikeTime;
                k *= k;
                angle = Mathf.Lerp(raised, toTarget, k);
                length = Mathf.Lerp(WhipLong, targetLength, k);
            }
            else { angle = toTarget; length = targetLength; }

            var along = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            tip = hands + along * length;
            tip.y = Mathf.Max(0f, tip.y);
            grip = hands - along * Butt;
            if (time < PinEndAt) return true;

            // The struck end comes back to the pawn and the staff is carried again.
            float home = PowerPoleGraphics.Smooth01((time - PinEndAt) / Retract);
            tip = Vector2.Lerp(target, carryTip, home);
            grip = Vector2.Lerp(grip, carryGrip, home);
            return false;
        }
    }
}
