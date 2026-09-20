using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Vault Strike: when each part happens, where the pawn is in the air and where the pole's two
    /// ends are. Seconds in, cells out, no drawing and no map. The defaults of the lab's
    /// power-pole-vault-strike.js. A place is (along, height): cells from the start cell toward the
    /// target, and cells above the ground.
    ///
    /// These are the picture's timings and shape. Range, damage, stun and the stagger radius are XML
    /// fields on the ability; the time in the air and the peak are the flyer def's.
    /// </summary>
    public struct PowerPoleStrikeShot
    {
        /// <summary>Cells from the start cell to the landing cell. "Along" is measured on this line.</summary>
        public float Landing;
        /// <summary>The target cell: cells along that line, and cells to its left.</summary>
        public float TargetAlong, TargetAcross;
        /// <summary>Seconds in the air and the arc's peak in cells: the flyer def's numbers.</summary>
        public float Flight, Peak;

        public float LaunchAt => PowerPoleStrikeTiming.Windup;
        public float PeakAt => LaunchAt + Flight / 2f;
        public float LandAt => LaunchAt + Flight;
        public float StrikeHitAt => LandAt - PowerPoleStrikeTiming.StrikeEarly;
        public float StrikeStartAt => StrikeHitAt - PowerPoleStrikeTiming.StrikeTime;
        public float WhipEndAt => Mathf.Min(PeakAt + PowerPoleStrikeTiming.WhipTime, StrikeStartAt);
        public float PinEndAt => LandAt + PowerPoleStrikeTiming.Pinned;
        /// <summary>The pole is back to its carried length: the caster is free again.</summary>
        public float HomeAt => PinEndAt + PowerPoleStrikeTiming.Retract;
        public float Duration => HomeAt + PowerPoleStrikeTiming.Tail;
    }

    public static class PowerPoleStrikeTiming
    {
        public const float Windup = 0.3f, WhipTime = 0.25f, StrikeTime = 0.15f, StrikeEarly = 0.05f, Pinned = 0.15f,
            Retract = 0.25f, Tail = 1.2f;
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
        public const float ScriptDistance = 8f, ScriptLandShort = 1f, ScriptFlight = 0.9f, ScriptPeak = 2.5f, ScriptRange = 10f, ScriptStaggerRadius = 1.5f;
        public static readonly float[] FanSpans = { 0.12f, 0.07f, 0.035f };

        public static PowerPoleStrikeShot Script => new PowerPoleStrikeShot
        {
            Landing = ScriptDistance - ScriptLandShort, TargetAlong = ScriptDistance, TargetAcross = 0f, Flight = ScriptFlight, Peak = ScriptPeak,
        };

        /// <summary>The pawn's feet: on the ground, a ballistic arc at constant ground speed, on the ground one cell short of the target.</summary>
        public static Vector2 PawnAt(float time, in PowerPoleStrikeShot shot)
        {
            float u = Mathf.Clamp01((time - shot.LaunchAt) / shot.Flight);
            return new Vector2(shot.Landing * u, shot.Peak * 4f * u * (1f - u));
        }

        /// <summary>The hands once they are above the head: they come forward during the whip and drop to carry height at the end.</summary>
        public static Vector2 HandsAt(float time, in PowerPoleStrikeShot shot)
        {
            Vector2 pawn = PawnAt(time, shot);
            return new Vector2(pawn.x + Mathf.Lerp(HandsBack, 0f, PowerPoleGraphics.Smooth01((time - shot.PeakAt) / WhipTime)),
                pawn.y + Mathf.Lerp(HandsHigh, PowerPoleGraphics.HandHeight, PowerPoleGraphics.Smooth01((time - shot.PinEndAt) / Retract)));
        }

        /// <summary>
        /// The pole's two ends. <paramref name="tip"/> is the end that is planted and later strikes,
        /// <paramref name="grip"/> the end in the hands, both as (along, height). The target cell may lie
        /// off the line the pawn flies along; <paramref name="tipAcross"/> is how much of that
        /// sideways offset the tip has taken on, 0 to 1. Returns true while the pole is swung from the hands.
        /// </summary>
        public static bool PoleAt(float time, in PowerPoleStrikeShot shot, out Vector2 tip, out Vector2 grip, out float tipAcross)
        {
            tipAcross = 0f;
            Vector2 pawn = PawnAt(time, shot), planted = new Vector2(Foot, 0f), target = new Vector2(shot.TargetAlong, 0f);
            Vector2 carryTip = new Vector2(pawn.x + PowerPoleGraphics.RestTip, pawn.y + PowerPoleGraphics.HandHeight);
            Vector2 carryGrip = new Vector2(pawn.x + PowerPoleGraphics.RestBack, pawn.y + PowerPoleGraphics.HandHeight);
            Vector2 top = new Vector2(pawn.x + TopBack, pawn.y + TopHeight);
            if (time < shot.LaunchAt)
            {
                float k = PowerPoleGraphics.Smooth01(time / Windup);
                tip = Vector2.Lerp(carryTip, planted, k);
                grip = Vector2.Lerp(carryGrip, top, k);
                return false;
            }
            if (time < shot.PeakAt)
            {
                tip = planted;
                grip = top;
                return false;
            }

            // Angles are in the along-height plane, 0 along the aim and 90 straight up. The whip goes from
            // pointing down and back at the planted tip, over the top, to RaisedAngle: the angle only falls.
            Vector2 hands = HandsAt(time, shot), peakHands = HandsAt(shot.PeakAt, shot);
            float whipFrom = Mathf.Atan2(-peakHands.y, Foot - peakHands.x) * Mathf.Rad2Deg, whipFromLength = (planted - peakHands).magnitude;
            float raised = RaisedAngle - 360f, toTarget = Mathf.Atan2(-hands.y, shot.TargetAlong - hands.x) * Mathf.Rad2Deg - 360f;
            float targetLength = (target - hands).magnitude, angle, length;
            if (time < shot.WhipEndAt)
            {
                float k = PowerPoleGraphics.Smooth01((time - shot.PeakAt) / (shot.WhipEndAt - shot.PeakAt));
                angle = Mathf.Lerp(whipFrom, raised, k);
                length = Mathf.Lerp(whipFromLength, WhipLong, k);
            }
            else if (time < shot.StrikeStartAt) { angle = raised; length = WhipLong; }
            else if (time < shot.StrikeHitAt)
            {
                float k = (time - shot.StrikeStartAt) / StrikeTime;
                k *= k;
                tipAcross = k;
                angle = Mathf.Lerp(raised, toTarget, k);
                length = Mathf.Lerp(WhipLong, targetLength, k);
            }
            else { angle = toTarget; length = targetLength; tipAcross = 1f; }

            var along = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            tip = hands + along * length;
            tip.y = Mathf.Max(0f, tip.y);
            grip = hands - along * Butt;
            if (time < shot.PinEndAt) return true;

            // The struck end comes back to the pawn and the staff is carried again.
            float home = PowerPoleGraphics.Smooth01((time - shot.PinEndAt) / Retract);
            tipAcross = 1f - home;
            tip = Vector2.Lerp(target, carryTip, home);
            grip = Vector2.Lerp(grip, carryGrip, home);
            return false;
        }
    }
}
