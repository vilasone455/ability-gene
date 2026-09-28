using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using T = RimArt.GuidingThunderTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Guiding Thunder: a turning ring of script on the floor round the caster framed by two
    /// thin rings, the brackets and the lit seal at the exit kunai, the light the caster holds at
    /// the chest, and for each shot taken at the barrier a glint where it went in, the glyphs there
    /// going white, a thin line to the kunai, a glint at the kunai and the hit there. A level ring,
    /// so it looks the same whichever way the shots come from.
    /// </summary>
    public static class GuidingThunderGraphics
    {
        private static readonly GuidedShot[] shots = new GuidedShot[T.MostShots];
        private static readonly float[] flashDegrees = new float[T.MostShots], flashAges = new float[T.MostShots];
        private static readonly Color Dust = new Color(0.52f, 0.45f, 0.37f);

        /// <summary>The preview: <paramref name="toward"/> is the unit direction from the caster to the shooters.</summary>
        public static void Draw(Vector3 centre, Vector2 toward, bool inEnemy, float seconds, Map map)
        {
            var middle = new Vector2(centre.x, centre.z);
            Vector2 caster = T.Caster(middle, toward), exit = inEnemy ? T.Shooter(middle, toward, 1) : T.GroundKunai(middle, toward);
            int count = T.Shots(middle, toward, inEnemy, shots);
            // In the preview's script the marked shooter goes down at its 4th hit and its kunai's seal goes out with it.
            bool down = inEnemy && count >= T.DownAfter && seconds >= shots[T.DownAfter - 1].leftAt;
            Draw(caster, exit, ThunderGodTiming.Degrees(toward), inEnemy, !down, shots, count, T.Radius, T.OverAt, seconds, map);
        }

        /// <summary>
        /// A real barrier. Ground points: <paramref name="caster"/> the feet it stands round, <paramref name="exit"/>
        /// the kunai the shots come out at. <paramref name="aim"/> turns the exit's brackets. <paramref name="sealShown"/>
        /// is false once the exit has lost its kunai. <paramref name="taken"/> holds the <paramref name="count"/> shots
        /// taken so far, on this clock; <paramref name="radius"/> is the barrier's (the ability's XML value in game);
        /// <paramref name="overAt"/> is when it starts to burn away.
        /// </summary>
        public static void Draw(Vector2 caster, Vector2 exit, float aim, bool inEnemy, bool sealShown, GuidedShot[] taken, int count,
            float radius, float overAt, float seconds, Map map)
        {
            if (seconds < 0f || seconds >= overAt + T.Fade + T.Tail) return;
            if (!Shown(caster, map) || !Shown(exit, map)) return;
            Begin(caster);

            float written = (seconds - T.CastAt) / T.Write, burn = (seconds - overAt) / T.Fade;
            float live = Mathf.Clamp01(written) * (1f - Mathf.Clamp01(burn));
            int flashes = Mathf.Min(count, flashDegrees.Length);
            if (written > 0f && burn < 1f)
            {
                for (int i = 0; i < flashes; i++)
                {
                    flashDegrees[i] = taken[i].degrees;
                    flashAges[i] = seconds - taken[i].reached;
                }
                Sprite(caster, radius * 2.8f, radius * 2.8f, Fade(Gold, 0.13f * live), glow, Floor + 0.005f);
                Circle(caster, radius + T.RingFrame, 0.55f * live, Floor + 0.02f, Gold);
                Circle(caster, radius - T.RingFrame, 0.4f * live, Floor + 0.0202f, Gold);
                ScriptRing(caster, radius, T.RingGlyphs(radius), seconds, T.CastAt, T.Write, burn, T.Spin * seconds, flashDegrees, flashAges, flashes);
            }

            // Where the shots will come out.
            float seal = Smooth(written) * (1f - Smooth(burn));
            Brackets(exit, aim, Mathf.Clamp01((written - 0.6f) / 0.4f) * (1f - Mathf.Clamp01(burn)));
            if (!inEnemy) SealOnGround(exit, seal);
            else if (sealShown) SealOnPawn(exit, aim, seal);
            // The caster holds the barrier: a steady light at the chest.
            Sprite(new Vector2(caster.x, caster.y + ThunderGodTiming.Chest), 0.7f, 0.7f, Fade(Gold, 0.35f * live), glow, Overhead + 0.01f);

            var leaves = new Vector2(exit.x, exit.y + (inEnemy ? ThunderGodTiming.Chest : 0f));
            for (int i = 0; i < count; i++)
            {
                GuidedShot shot = taken[i];
                Star(shot.entry, seconds - shot.reached, T.GlintTime, T.Glint);
                float link = (seconds - shot.reached) / T.LinkTime;
                if (link >= 0f && link < 1f)
                {
                    Streak(shot.entry, leaves, T.LinkWidth * 2.4f, Fade(Gold, 0.55f * (1f - link)), whiteGlow, Overhead + 0.02f + i * 0.0002f, 12);
                    Streak(shot.entry, leaves, T.LinkWidth, Fade(Pale, 1f - link), solid, Overhead + 0.03f + i * 0.0002f, 12);
                }
                Star(leaves, seconds - shot.leftAt, T.GlintTime, T.Glint * T.ExitGlint);
                float landed = seconds - shot.leftAt;
                if (inEnemy) HitSpark(exit, landed);
                else if (landed >= 0f && landed < 0.45f)
                {
                    float u = landed / 0.45f;
                    Sprite(new Vector2(leaves.x, leaves.y + u * 0.15f), 0.35f + u * 0.6f, 0.3f + u * 0.45f,
                        Fade(Dust, 0.55f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI))), soft, Overhead + 0.005f + i * 0.0002f);
                }
            }
        }
    }
}
