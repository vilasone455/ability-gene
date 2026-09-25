using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// What one Sweep covers and hits. Angles named "phi" are degrees from the aim, positive to the
    /// caster's left. A real cast fills this from the map; the preview fills it from its script.
    /// </summary>
    public sealed class PowerPoleSweepShot
    {
        /// <summary>The cast direction in degrees, 0 east and 90 north.</summary>
        public float Aim;
        public float Reach, Arc;
        /// <summary>The pole's length every <see cref="LengthStep"/> degrees, from +Half down to -Half: the reach, or up to the first wall on that line.</summary>
        public float[] Length;
        public float LengthStep = 1f;
        /// <summary>Each pawn that is hit: where it stands, when the pole passes it, and the way it is shoved in the preview's picture.</summary>
        public Vector2[] HitPlace = new Vector2[0], HitShove = new Vector2[0];
        public float[] HitTime = new float[0];

        public float Half => Arc / 2f;

        public float LengthAt(float phi)
        {
            float index = Mathf.Clamp((Half - phi) / LengthStep, 0f, Length.Length - 1);
            int low = Mathf.FloorToInt(index), high = Mathf.Min(low + 1, Length.Length - 1);
            return Mathf.Lerp(Length[low], Length[high], index - low);
        }
    }

    /// <summary>
    /// Sweep: when each part happens and the pole's angle and length. Seconds and degrees in, cells
    /// out, no drawing and no map. The defaults of the lab's power-pole-sweep.js. These are the
    /// picture's timings and shape. Reach, arc and damage are XML fields on the ability.
    /// </summary>
    public static class PowerPoleSweepTiming
    {
        public const float Windup = 0.3f, Swing = 0.4f, Hold = 0.1f, Retract = 0.25f, Tail = 1f;
        public const float Shove = 0.25f, HitShake = 0.06f, FanLinger = 0.12f, BodyRadius = 0.25f, WallGap = 0.08f;
        public const int DustPuffs = 16, FanSteps = 10, AreaSteps = 72;
        // The preview's script: cells from the caster and degrees from the aim.
        public const float ScriptReach = 4f, ScriptArc = 180f, ScriptWallRange = 2.2f, ScriptWallPhi = -45f;
        private static readonly float[] ScriptEnemyRange = { 2f, 3.5f, 3.4f, 5f, 2.5f }, ScriptEnemyPhi = { 60f, 10f, -45f, -20f, 170f };
        public static readonly float[] FanSpans = { 0.12f, 0.07f, 0.035f };

        public static float SwingAt => Windup;
        public static float SwungAt => SwingAt + Swing;
        public static float RetractAt => SwungAt + Hold;
        /// <summary>The pole is back to its carried length: the caster is free again.</summary>
        public static float HomeAt => RetractAt + Retract;
        public static float Duration => HomeAt + Tail;

        /// <summary>The preview's shot: five pawns round the caster and, optionally, one wall cell in front of the third.</summary>
        public static PowerPoleSweepShot Script(float aim, bool wall)
        {
            var shot = new PowerPoleSweepShot { Aim = aim, Reach = ScriptReach, Arc = ScriptArc, Length = new float[(int)ScriptArc * 4 + 1] };
            // Quarter degrees, so the notch a wall cuts is as sharp as the sketch's.
            for (int i = 0; i < shot.Length.Length; i++) shot.Length[i] = ScriptLength(shot.Half - i / 4f, aim, wall);
            shot.LengthStep = 0.25f;

            int hits = 0;
            for (int e = 0; e < ScriptEnemyRange.Length; e++)
                if (Passes(ScriptEnemyPhi[e], shot) >= 0f && ScriptEnemyRange[e] <= ScriptLength(ScriptEnemyPhi[e], aim, wall) + BodyRadius) hits++;
            shot.HitPlace = new Vector2[hits];
            shot.HitShove = new Vector2[hits];
            shot.HitTime = new float[hits];
            hits = 0;
            for (int e = 0; e < ScriptEnemyRange.Length; e++)
            {
                float phi = ScriptEnemyPhi[e];
                if (Passes(phi, shot) < 0f || ScriptEnemyRange[e] > ScriptLength(phi, aim, wall) + BodyRadius) continue;
                shot.HitPlace[hits] = VfxDraw.Turn(aim + phi) * ScriptEnemyRange[e];
                shot.HitShove[hits] = VfxDraw.Turn(aim + phi - 90f);
                shot.HitTime[hits++] = Passes(phi, shot);
            }
            return shot;
        }

        /// <summary>The script's pole length at <paramref name="phi"/>: the reach, or up to the near face of the script's wall cell.</summary>
        private static float ScriptLength(float phi, float aim, bool wall)
        {
            if (!wall) return ScriptReach;
            float turn = (aim + ScriptWallPhi) * Mathf.Deg2Rad, line = (aim + phi) * Mathf.Deg2Rad;
            float enter = float.NegativeInfinity, exit = float.PositiveInfinity;
            for (int axis = 0; axis < 2; axis++)
            {
                float d = axis == 0 ? Mathf.Cos(line) : Mathf.Sin(line), c = ScriptWallRange * (axis == 0 ? Mathf.Cos(turn) : Mathf.Sin(turn));
                if (Mathf.Abs(d) < 1e-6f)
                {
                    if (Mathf.Abs(c) > 0.5f) return ScriptReach;
                    continue;
                }
                float one = (c - 0.5f) / d, two = (c + 0.5f) / d;
                enter = Mathf.Max(enter, Mathf.Min(one, two));
                exit = Mathf.Min(exit, Mathf.Max(one, two));
            }
            return enter <= exit && enter > 0f ? Mathf.Min(ScriptReach, enter - WallGap) : ScriptReach;
        }

        public static float AngleAt(float time, PowerPoleSweepShot shot)
        {
            if (time < SwingAt) return shot.Half * PowerPoleGraphics.Smooth01(time / Windup);
            if (time < SwungAt) return shot.Half - shot.Arc * PowerPoleGraphics.Smooth01((time - SwingAt) / Swing);
            if (time < RetractAt) return -shot.Half;
            return -shot.Half * (1f - PowerPoleGraphics.Smooth01((time - RetractAt) / Retract));
        }

        /// <summary>When the swing passes <paramref name="phi"/>, or -1 outside the arc.</summary>
        public static float Passes(float phi, PowerPoleSweepShot shot)
        {
            if (Mathf.Abs(phi) > shot.Half) return -1f;
            for (float time = SwingAt; time <= SwungAt; time += 0.004f)
                if (AngleAt(time, shot) <= phi) return time;
            return SwungAt;
        }

        public static float TipLength(float time, PowerPoleSweepShot shot)
        {
            if (time < SwingAt) return Mathf.Lerp(PowerPoleGraphics.RestTip, shot.LengthAt(shot.Half), PowerPoleGraphics.Smooth01(time / Windup));
            if (time < RetractAt) return shot.LengthAt(AngleAt(time, shot));
            return Mathf.Lerp(shot.LengthAt(-shot.Half), PowerPoleGraphics.RestTip, PowerPoleGraphics.Smooth01((time - RetractAt) / Retract));
        }
    }
}
