using UnityEngine;
using Verse;
using static RimArt.PowerPoleGraphics;
using static RimArt.ThunderGodGraphics;
using T = RimArt.PowerPoleThrustTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Extend Thrust: the pole sliding back, shooting out to the target and carrying it, the
    /// speed lines beside the shaft, the dust along the ground under the tip, the flash, ring and
    /// dust of the hit, and the dust on a wall that stops the carry. The pole lies flat at hand
    /// height, so it turns with the direction it is given and there is no per-facing method.
    /// Neither pawn, the hands nor the wall is drawn.
    /// </summary>
    public static class PowerPoleThrustGraphics
    {
        /// <summary>
        /// <paramref name="centre"/> is the middle of the scene, as in the lab's sketch: halfway between
        /// the caster and where the target ends up. <paramref name="toward"/> is the unit direction of the thrust.
        /// </summary>
        public static void Draw(Vector3 centre, Vector2 toward, bool wall, float seconds, Map map)
        {
            if (seconds < 0f || seconds >= T.Duration) return;
            var middle = new Vector2(centre.x, centre.z);
            Vector2 feet = middle - toward * ((T.Distance + T.PushCells) / 2f), across = new Vector2(-toward.y, toward.x);
            if (!Shown(feet, map) || !Shown(feet + toward * T.Reach(wall), map)) return;
            Begin(middle);
            Sun(map, out Vector2 sun, out float shadow);

            float tip = T.TipAt(seconds, wall), back = T.BackAt(seconds), reach = T.Reach(wall);

            // Dust kicked up on the ground under the tip as it passes, so the line reads when zoomed out.
            int puffs = Mathf.Max(4, Mathf.RoundToInt(reach * 1.6f));
            for (int i = 0; i < puffs; i++)
            {
                float along = Mathf.Lerp(1.2f, reach, (i + 0.5f) / puffs), born = T.FirstTime(along, wall);
                if (born < 0f) continue;
                float u = (seconds - born) / (0.45f + Rand(i) * 0.3f), size = 0.4f + u * 0.7f;
                Vector2 at = feet + toward * (along - u * 0.3f) + across * ((Rand(i + 40) - 0.5f) * 0.5f);
                Puff(Raised(at, u * 0.25f), size, size * 0.7f, u, 0.7f, Overhead - 0.02f + i * 0.0002f);
            }

            Vector2 a = feet + toward * back, b = feet + toward * tip;
            Pole(Raised(a, HandHeight), Raised(b, HandHeight), a + sun * HandHeight, b + sun * HandHeight, tip - back, Width, shadow, Overhead);

            // Speed lines beside the shaft while the tip is moving out.
            float moving = seconds >= T.ThrustAt && seconds < T.PushedAt
                ? (seconds < T.HitAt ? 1f : 1f - EaseOut((seconds - T.HitAt) / T.Push)) : 0f;
            if (moving > 0.02f)
                for (int i = 0; i < T.SpeedLines; i++)
                {
                    float side = (i % 2 == 1 ? 1f : -1f) * (0.14f + Rand(i + 7) * 0.2f), length = (0.8f + Rand(i + 3) * 1.4f) * moving;
                    float head = tip - Rand(i + 11) * 0.6f, tail = Mathf.Max(back, head - length);
                    Vector2 from = Raised(feet + toward * tail, HandHeight);
                    Part(from, toward * (head - tail), across, 0f, 1f, side - 0.012f, side + 0.012f, Fade(Cream, 0.55f * moving), Overhead + 0.01f + i * 0.0002f);
                }

            // The hit: flash and ring on the target, a few dust puffs thrown along the aim.
            float hitAge = seconds - T.HitAt;
            if (hitAge >= 0f && hitAge < 0.5f)
            {
                Vector2 spot = feet + toward * T.Contact;
                Sprite(Raised(spot, HandHeight), 1.6f, 1.1f, Fade(Cream, Mathf.Max(0f, 1f - hitAge / 0.12f) * 0.85f), glow, Overhead + 0.02f);
                Circle(spot, 0.25f + hitAge * 2.2f, (1f - hitAge / 0.5f) * 0.6f, Floor, Cream);
                for (int i = 0; i < T.HitPuffs; i++)
                {
                    float u = hitAge / (0.3f + Rand(i + 60) * 0.2f), spread = (Rand(i + 70) - 0.5f) * 1.4f, far = u * (0.6f + Rand(i + 80) * 1.2f);
                    Vector2 at = spot + toward * far + across * (spread * u);
                    Puff(Raised(at, Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * 0.4f), 0.25f + u * 0.4f, 0.2f + u * 0.3f, u, 0.6f, Overhead + 0.012f + i * 0.0002f);
                }
            }

            // A wall stops the carry: a smaller flash and dust on the wall's near face.
            float slamAge = seconds - T.PushedAt;
            if (wall && T.Room(true) < T.PushCells && slamAge >= 0f && slamAge < 0.45f)
            {
                Vector2 face = feet + toward * (T.WallAlong - 0.5f);
                Sprite(Raised(face, 0.3f), 1.1f, 0.8f, Fade(Cream, Mathf.Max(0f, 1f - slamAge / 0.1f) * 0.5f), glow, Overhead + 0.021f);
                float u = slamAge / 0.45f;
                for (int i = 0; i < T.WallPuffs; i++)
                {
                    Vector2 at = face - toward * (u * 0.5f) + across * ((Rand(i + 90) - 0.5f) * 1.6f * (0.3f + u));
                    Puff(Raised(at, 0.3f + u * 0.3f), 0.3f + u * 0.4f, 0.25f + u * 0.3f, u, 0.55f, Overhead + 0.014f + i * 0.0002f);
                }
            }
        }
    }
}
