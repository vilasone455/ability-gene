using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;
using T = RimArt.RasenganTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Rasengan: the ball in the hand (shell, five orbit rings, three arcs inside, a core that
    /// beats), the threads, wind rings, dust and blue floor light while it is held, the jump's floor
    /// script and teleport when cast from range, the grind's spiral and sparks, the burst and spiral
    /// shock of the release, and behind the thrown pawn a groove of dust, a double helix and rings
    /// that open across the path. Level circles and flat lines turned with the aim, so there is no
    /// per-facing method. Blue-white, not the kit's gold.
    ///
    /// Neither pawn is drawn. The ball sits where the sketch's stand-in caster holds it, at chest
    /// height; a real cast plays RimArt_RasenganForm and RimArt_RasenganThrust and would put the
    /// ball on the clip's hand.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RasenganGraphics
    {
        private static readonly Mesh orbit = SixPathsBurstGraphics.Band(0.86f, "Rasengan ring"),
            rim = SixPathsBurstGraphics.Band(0.94f, "Rasengan thin ring");
        private static readonly Color Blue = new Color(0.3f, 0.62f, 1f), Deep = new Color(0.1f, 0.32f, 0.9f),
            Sky = new Color(0.5f, 0.78f, 1f), Ice = new Color(0.78f, 0.91f, 1f), White = new Color(1f, 1f, 1f),
            Dust = new Color(0.52f, 0.45f, 0.37f);
        // Orbit rings of the ball: tilt speed, turn speed (degrees per second), starting angle.
        private static readonly float[] OrbitTilt = { 7f, -9f, 11f, -13f, 8f }, OrbitTurn = { 90f, -120f, 70f, -100f, 140f },
            OrbitStart = { 0f, 36f, 72f, 108f, 144f };
        // Arcs inside the ball: share of the radius, speed, span in degrees.
        private static readonly float[] ArcShare = { 0.62f, 0.44f, 0.27f }, ArcSpeed = { 1f, -1.4f, 1.9f }, ArcSpan = { 150f, 170f, 200f };

        /// <summary>
        /// <paramref name="centre"/> is the middle of what happens and <paramref name="toward"/> is the
        /// unit direction from where the caster starts to the enemy.
        /// </summary>
        public static void Draw(Vector3 centre, Vector2 toward, bool teleports, bool wall, float seconds, Map map)
        {
            if (seconds < 0f || seconds >= T.Duration(teleports, wall)) return;
            var middle = new Vector2(centre.x, centre.z);
            Vector2 enemy = T.Enemy(middle, toward, teleports), home = T.Home(middle, toward, teleports), spot = T.Spot(middle, toward, teleports);
            float direction = T.Direction(teleports);
            if (!Shown(home, map) || !Shown(enemy + toward * (direction * T.ThrowDistance), map)) return;
            Begin(middle);
            float aim = ThunderGodTiming.Degrees(toward), chest = ThunderGodTiming.Chest;
            float arriveAt = T.ArriveAt(teleports), thrustAt = T.ThrustAt(teleports), hitAt = T.HitAt(teleports),
                releaseAt = T.ReleaseAt(teleports), landAt = T.LandAt(teleports, wall), thrown = T.Thrown(wall);
            var across = new Vector2(-toward.y, toward.x);

            // The jump's floor script, written while the ball forms.
            if (teleports)
            {
                float write = T.Form * 0.4f, written = (seconds - T.CastAt) / write, burn = seconds - arriveAt - T.FlashTime;
                if (written > 0f && burn < 1f)
                {
                    Sprite(enemy + toward * ((ThunderGodTiming.Behind - ThunderGodTiming.StripBack) / 2f), 1.8f, 0.5f,
                        Fade(Gold, 0.3f * Mathf.Clamp01(written) * (1f - Smooth(burn))), glow, Floor + 0.005f, -aim);
                    Script(enemy, aim, -ThunderGodTiming.StripBack, ThunderGodTiming.StripGlyphs, seconds, T.CastAt, write, burn);
                    Brackets(spot, aim, Mathf.Clamp01((written - 0.8f) / 0.2f) * (1f - Mathf.Clamp01((burn - 0.85f) / 0.15f)));
                }
                if (seconds < releaseAt) SealOnPawn(enemy, aim, Smooth((seconds - T.CastAt) / write));
            }

            // The groove the thrown pawn leaves, and the scorch it ends on.
            float flight = T.Flight(seconds, teleports, wall), gone = T.Gone(seconds, teleports, wall);
            if (flight > 0f)
            {
                float settle = 1f - 0.4f * Smooth((seconds - landAt) / T.Tail);
                Sprite(enemy + toward * (direction * gone / 2f), gone + 0.5f, 0.42f, Fade(Ink, 0.3f * settle), soft, Floor + 0.01f, -aim);
                for (int i = 0; i < T.GrooveDust; i++)
                {
                    float where = (i + 0.5f) / T.GrooveDust * thrown;
                    if (where > gone) continue;
                    float age = seconds - T.Passes(where, teleports, wall), life = 0.5f + Rand(i) * 0.3f;
                    if (age < 0f || age > life) continue;
                    float v = age / life;
                    Vector2 at = enemy + toward * (direction * where) + across * ((Rand(i + 5) - 0.5f) * 0.4f);
                    Sprite(new Vector2(at.x, at.y + v * 0.35f), 0.4f + v * 0.5f, 0.32f + v * 0.4f,
                        Fade(Dust, 0.5f * Mathf.Max(0f, Mathf.Sin(v * Mathf.PI))), soft, Overhead + 0.005f + i * 0.0002f);
                }
            }
            if (seconds >= landAt)
                Sprite(enemy + toward * (direction * gone), 1.3f, 0.8f, Fade(Ink, 0.34f * (1f - 0.4f * Smooth((seconds - landAt) / T.Tail))),
                    soft, Floor + 0.012f, -aim);

            // Where the caster is, and the sliver of the teleport.
            bool grinding = seconds >= hitAt && seconds < releaseAt, landed = seconds >= arriveAt;
            float pressed = grinding ? Mathf.Clamp01((seconds - hitAt) / T.Press) : 0f;
            float lunge = Smooth((seconds - thrustAt) / T.Reach) * (1f - Smooth((seconds - releaseAt) / 0.3f));
            float thinOut = teleports ? Smooth((seconds - T.FormedAt) / T.Squeeze) : 0f,
                thinIn = teleports ? 1f - Smooth((seconds - arriveAt) / T.Squeeze) : 0f;
            float face = landed ? direction : 1f, thin = landed ? thinIn : thinOut;
            Vector2 stand = landed ? spot : home, caster = stand + toward * (face * T.Lean * (landed ? lunge : 0f));
            Sliver(caster, thin);

            if (teleports)
            {
                Leave(home, seconds - T.FormedAt, T.Squeeze, T.StarSize);
                JumpLine(home, spot, (seconds - T.FormedAt) / T.LineTime, T.LineWidth);
                Star(new Vector2(spot.x, spot.y + chest), seconds - arriveAt, T.FlashTime, T.StarSize);
                Sparks(spot, seconds - arriveAt, 10);
            }

            if (seconds >= T.CastAt && seconds < releaseAt)
                DrawHeld(caster, toward * face, landed ? lunge : 0f, 1f - thin, pressed, grinding, seconds, hitAt);
            DrawRelease(new Vector2(enemy.x, enemy.y + chest), seconds - releaseAt);
            DrawVortex(enemy, toward, across, direction, aim, flight, gone, seconds, teleports, wall);

            // Where it stops: dust, and a second flash if a wall stopped it.
            float stopped = seconds - landAt;
            if (stopped >= 0f && stopped < 0.6f)
            {
                float v = stopped / 0.6f;
                Vector2 at = enemy + toward * (direction * (thrown + (wall ? 0.45f : 0f)));
                if (wall && stopped < 0.12f)
                    Sprite(new Vector2(at.x, at.y + chest), 1.5f, 1.5f, Fade(White, 0.9f * (1f - stopped / 0.12f)), glow, Overhead + 0.1f);
                for (int i = 0; i < T.StopDust; i++)
                {
                    float angle = i * 0.63f + Rand(i), reach = 0.25f + v * (0.6f + Rand(i + 4) * 0.7f);
                    Sprite(new Vector2(at.x + Mathf.Cos(angle) * reach, at.y + Mathf.Sin(angle) * reach * 0.7f + v * 0.3f), 0.45f + v * 0.6f, 0.36f + v * 0.45f,
                        Fade(Dust, 0.55f * Mathf.Max(0f, Mathf.Sin(v * Mathf.PI))), soft, Overhead + 0.006f + i * 0.0002f);
                }
            }
        }

        /// <summary>The ball in the hand and everything round it while it is held. <paramref name="facing"/> is the unit direction the caster faces.</summary>
        private static void DrawHeld(Vector2 caster, Vector2 facing, float lunge, float seen, float pressed, bool grinding, float seconds, float hitAt)
        {
            float since = seconds - T.CastAt, grown = Smooth(since / T.Form), reach = T.Hand + lunge * (1f - T.Lean - T.Hand - 0.03f);
            var hand = new Vector2(caster.x + facing.x * reach, caster.y + facing.y * reach + ThunderGodTiming.Chest);
            float unsteady = T.Unsteady * (1f - grown);
            float size = T.BallSize * grown * (1f + T.Swell * Smooth(pressed)) * (1f + unsteady * Mathf.Sin(seconds * 53f));
            var held = new Vector2(hand.x + unsteady * 0.25f * Mathf.Sin(seconds * 71f), hand.y + unsteady * 0.25f * Mathf.Cos(seconds * 59f));
            float forming = Mathf.Clamp01(since / 0.12f) * (1f - Mathf.Clamp01((seconds - T.FormedAt) / 0.15f));

            // Floor: blue light under the ball, wind rings opening from the caster's feet, dust carried out with them.
            float spread = (0.6f + 0.4f * grown) * (1f + pressed);
            Sprite(new Vector2(held.x, held.y - ThunderGodTiming.Chest), 2.2f * spread, 1.5f * spread,
                Fade(Blue, 0.26f * grown * seen * (0.85f + 0.15f * Mathf.Sin(seconds * 31f))), glow, Floor + 0.006f);
            for (int n = 0; n < T.WindRings; n++)
            {
                float v = (since / T.WindEvery + n / (float)T.WindRings) % 1f;
                Circle(caster, 0.3f + v * T.Swirl, 0.55f * (1f - v) * grown * seen, Floor + 0.02f + n * 0.0002f, Ice);
            }
            for (int i = 0; i < T.Puffs; i++)
            {
                float v = (since * 1.1f + Rand(i + 30)) % 1f, radius = 0.35f + v * T.Swirl, angle = (i * 45f + Rand(i + 60) * 30f + v * 40f) * Mathf.Deg2Rad;
                Sprite(new Vector2(caster.x + Mathf.Cos(angle) * radius, caster.y + Mathf.Sin(angle) * radius * 0.85f + v * 0.1f), 0.35f + v * 0.4f, 0.28f + v * 0.3f,
                    Fade(Dust, 0.42f * grown * seen * Mathf.Max(0f, Mathf.Sin(v * Mathf.PI))), soft, Floor + 0.04f + i * 0.0002f);
            }

            // Threads of chakra curling into the palm while it forms.
            if (forming > 0f)
                for (int i = 0; i < T.Threads; i++)
                {
                    const int steps = 7;
                    float v = (since * 1.7f + Rand(i)) % 1f;
                    Sides(steps + 1, out Vector2[] left, out Vector2[] right);
                    for (int j = 0; j <= steps; j++)
                    {
                        float w = Mathf.Max(0f, v - (steps - j) * 0.03f), radius = T.Swirl * Mathf.Pow(Mathf.Clamp01(1f - w), 1.3f) + size / 2f;
                        Vector2 ray = Turn(i * 36f + Rand(i + 9) * 40f + w * 260f), at = held + ray * radius;
                        float half = 0.028f * (j / (float)steps) + 0.002f;
                        left[j] = at + ray * half;
                        right[j] = at - ray * half;
                    }
                    Strip(left, right, Fade(Ice, 0.85f * forming * Mathf.Max(0f, Mathf.Sin(v * Mathf.PI))), whiteGlow, Overhead + 0.09f + i * 0.0002f);
                }

            DrawBall(held, size, seconds, seen, pressed);
            if (!grinding) return;
            // The grind: a spiral turning on the ball and sparks thrown off round it.
            Spiral(held, size * 0.62f, 0.8f, -seconds * 1400f, 0.035f * size / T.BallSize, Fade(White, 0.85f), solid, Overhead + 0.122f);
            for (int i = 0; i < T.GrindSparks; i++)
            {
                float v = ((seconds - hitAt) * 5f + Rand(i + 12)) % 1f, angle = i * 30f + seconds * 700f + v * 50f, near = size / 2f + v * 0.8f;
                Streak(held + Turn(angle) * near, held + Turn(angle + 14f) * (near + 0.2f), 0.05f, Fade(Ice, 1f - v), whiteGlow,
                    Overhead + 0.123f + i * 0.0002f, 2);
            }
        }

        /// <summary>The ball as a sphere. <paramref name="power"/> 0 to 1 is the grind: everything turns up to twice as fast.</summary>
        private static void DrawBall(Vector2 at, float size, float seconds, float alpha, float power)
        {
            if (size <= 0.01f || alpha <= 0f) return;
            float r = size / 2f, fast = 1f + power, beat = 1f + 0.1f * Mathf.Sin(seconds * 42f);
            Sprite(at, size * 3.2f, size * 3.2f, Fade(Blue, 0.6f * alpha), glow, Overhead + 0.1f);
            DrawMesh(disc, at, Overhead + 0.11f, r, r, 0f, Fade(Blue, 0.9f * alpha), solid);
            DrawMesh(disc, at, Overhead + 0.111f, r * 0.84f, r * 0.84f, 0f, Fade(Sky, 0.8f * alpha), solid);
            DrawMesh(rim, at, Overhead + 0.112f, r, r, 0f, Fade(Deep, alpha), solid);
            for (int k = 0; k < OrbitTilt.Length; k++)
            {
                float flat = Mathf.Max(0.1f, Mathf.Abs(Mathf.Cos(seconds * OrbitTilt[k] * fast + k * 1.3f)));
                DrawMesh(orbit, at, Overhead + 0.113f + k * 0.0004f, r * 0.95f, r * 0.95f * flat, OrbitStart[k] + seconds * OrbitTurn[k] * fast,
                    Fade(White, (0.45f + 0.55f * flat) * alpha), solid);
            }
            for (int k = 0; k < ArcShare.Length; k++)
            {
                const int steps = 10;
                float start = seconds * 720f * ArcSpeed[k] * fast + k * 120f, reach = r * ArcShare[k];
                Sides(steps + 1, out Vector2[] outer, out Vector2[] inner);
                for (int i = 0; i <= steps; i++)
                {
                    float v = i / (float)steps, w = Mathf.Max(0f, Mathf.Sin(v * Mathf.PI)) * r * 0.12f;
                    Vector2 ray = Turn(start + ArcSpan[k] * v);
                    outer[i] = at + ray * (reach + w);
                    inner[i] = at + ray * (reach - w);
                }
                Strip(outer, inner, Fade(White, 0.9f * alpha), solid, Overhead + 0.116f + k * 0.001f);
            }
            DrawMesh(disc, at, Overhead + 0.12f, r * 0.26f * beat, r * 0.26f * beat, 0f, Fade(White, alpha), solid);
            // Dimmer in the grind, or the big ball washes out to white.
            float core = size * (0.7f - 0.25f * power) * beat;
            Sprite(at, core, core, Fade(White, alpha * (1f - 0.45f * power)), glow, Overhead + 0.121f);
        }

        /// <summary>A two-armed spiral round a point, out to <paramref name="radius"/>, turned by <paramref name="spin"/> degrees.</summary>
        private static void Spiral(Vector2 centre, float radius, float turns, float spin, float width, Color colour, Material material, float altitude)
        {
            const int arms = 2, steps = 36;
            for (int arm = 0; arm < arms; arm++)
            {
                Sides(steps + 1, out Vector2[] outer, out Vector2[] inner);
                for (int i = 0; i <= steps; i++)
                {
                    float w = i / (float)steps, reach = radius * (0.2f + 0.8f * w), half = width * Mathf.Max(0f, Mathf.Sin(w * Mathf.PI)) + 0.003f;
                    Vector2 ray = Turn(w * turns * 360f + spin + arm * 360f / arms);
                    outer[i] = centre + ray * (reach + half);
                    inner[i] = centre + ray * (reach - half);
                }
                Strip(outer, inner, colour, material, altitude + arm * 0.0002f);
            }
        }

        /// <summary>The sphere bursts, lines shoot outward, and a spiral shock opens.</summary>
        private static void DrawRelease(Vector2 burst, float since)
        {
            if (since >= 0f && since < T.BurstTime)
            {
                float v = since / T.BurstTime, f = 1f - v, radius = T.BallSize * (1f + T.Swell) / 2f + (T.BurstRadius - T.BallSize) * Smooth(v);
                Sprite(burst, 3.4f, 3.4f, Fade(Blue, 0.8f * f * f), glow, Overhead + 0.1f);
                DrawMesh(disc, burst, Overhead + 0.101f, radius, radius, 0f, Fade(Sky, 0.5f * f), solid);
                DrawMesh(orbit, burst, Overhead + 0.102f, radius, radius, 0f, Fade(White, f), solid);
                Sprite(burst, 1.5f, 1.5f, Fade(White, f), glow, Overhead + 0.11f);
                for (int i = 0; i < T.BurstLines; i++)
                {
                    Vector2 ray = Turn(i * 360f / T.BurstLines + Rand(i + 70) * 14f);
                    float near = 0.4f + v * 1.1f, far = near + 0.7f + Rand(i + 90) * 0.6f;
                    Streak(burst + ray * near, burst + ray * far, 0.07f, Fade(White, f), whiteGlow, Overhead + 0.105f + i * 0.0002f, 4);
                }
            }
            if (since < 0f || since >= T.ShockTime) return;
            float u = since / T.ShockTime;
            Spiral(burst, T.ShockRadius * Smooth(u), T.ShockTurns, since * 500f, 0.13f * (1f - u), Fade(Blue, 0.7f * (1f - u)), whiteGlow, Overhead + 0.08f);
            Spiral(burst, T.ShockRadius * Smooth(u), T.ShockTurns, since * 500f, 0.06f * (1f - u), Fade(Ice, 1f - u), solid, Overhead + 0.085f);
        }

        /// <summary>The vortex behind the thrown pawn: a double helix, and rings that open across the path as it passes.</summary>
        private static void DrawVortex(Vector2 enemy, Vector2 toward, Vector2 across, float direction, float aim, float flight, float gone,
            float seconds, bool teleports, bool wall)
        {
            if (flight <= 0f) return;
            float landAt = T.LandAt(teleports, wall), since = seconds - T.ReleaseAt(teleports), chest = ThunderGodTiming.Chest;
            if (seconds < landAt + 0.25f)
            {
                const int steps = 18;
                float fade = 1f - Mathf.Clamp01((seconds - landAt) / 0.25f), tail = Mathf.Max(0f, gone - T.TrailLength);
                for (int side = 1; side >= -1; side -= 2)
                {
                    // The glow and the core have the same shape, but each drawn thing has its own mesh.
                    for (int pass = 0; pass < 2; pass++)
                    {
                        Sides(steps + 1, out Vector2[] left, out Vector2[] right);
                        for (int i = 0; i <= steps; i++)
                        {
                            float w = i / (float)steps, d = tail + (gone - tail) * w;
                            float wave = side * Mathf.Sin(d * 7f - since * 30f) * T.TrailWave * (0.3f + 0.7f * w), half = 0.04f * Mathf.Max(0f, Mathf.Sin(w * Mathf.PI)) + 0.003f;
                            Vector2 c = enemy + toward * (direction * d) + across * wave + new Vector2(0f, chest);
                            left[i] = c + across * half;
                            right[i] = c - across * half;
                        }
                        if (pass == 0) Strip(left, right, Fade(Blue, 0.8f * fade), whiteGlow, Overhead + 0.07f + (side + 1) * 0.0001f);
                        else Strip(left, right, Fade(Ice, 0.9f * fade), solid, Overhead + 0.072f + (side + 1) * 0.0001f);
                    }
                }
            }
            for (int n = 0; n < T.VortexRings; n++)
            {
                float where = (n + 0.5f) / T.VortexRings * T.Thrown(wall), age = seconds - T.Passes(where, teleports, wall);
                if (age < 0f || age >= T.VortexTime) continue;
                float v = age / T.VortexTime;
                Vector2 c = enemy + toward * (direction * where) + new Vector2(0f, chest);
                DrawMesh(orbit, c, Overhead + 0.06f + n * 0.0002f, 0.07f + 0.07f * v, 0.28f + 0.4f * v, -aim, Fade(Ice, 0.85f * (1f - v)), solid);
                DrawMesh(orbit, c, Overhead + 0.059f + n * 0.0002f, 0.11f + 0.09f * v, 0.34f + 0.44f * v, -aim, Fade(Blue, 0.6f * (1f - v)), whiteGlow);
            }
        }
    }
}
