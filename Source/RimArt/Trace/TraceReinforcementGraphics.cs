using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.TraceHandGraphics;
using T = RimArt.TraceReinforcementTiming;

namespace RimArt
{
    /// <summary>
    /// Reinforcement's picture (Tools/VfxLab/web/sketches/trace-reinforcement.js). Cast: a ring opens at the feet,
    /// circuit lines run out from the chest, the line climbs the held weapon's outline from the grip to the point,
    /// a glint at the point, and the body's lines fade. While the buff lasts the weapon's edge keeps a pulsing glow
    /// and a running pawn leaves footprints that fade and streaks at its heels; each melee hit gets a slash and a
    /// spark. When it ends the lines run back into the chest and the glow fades.
    ///
    /// The lines show only while casting and ending: a pattern left on the body for 20 s read as a skeleton at game
    /// zoom, so the lasting signs are the weapon's glow (damage) and the footprints (speed).
    /// </summary>
    internal static class TraceReinforcementGraphics
    {
        /// <summary>The cast's ring at the feet and the flash at the chest, <paramref name="age"/> s into the cast.</summary>
        internal static void CastRing(Vector2 me, float age)
        {
            if (age < 0f || age >= T.Ring) return;
            float u = age / T.Ring;
            PaperBombGraphics.RingAt(me, 0.25f + 0.8f * Smooth(u), Fade(Trace, 0.7f * (1f - u)), Floor + 0.01f, false, whiteGlow);
            Sprite(PawnFit.At(me, 0f, 0.36f), 0.6f, 0.6f, Fade(Trace, 0.35f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI))), glow, PawnLayer + 0.005f);
        }

        /// <summary>The circuit while casting (<paramref name="age"/> into a cast of <paramref name="cast"/> s): it runs out, then fades once the weapon is lit.</summary>
        internal static void CastLines(string key, TraceBody body, float age, float cast)
        {
            if (age < 0f || age >= cast + T.LinesFade) return;
            float reach = TraceCircuit.Travel * Mathf.Clamp01(age / T.Lines(cast));
            Circuit(key, body, reach, T.Bright * (1f - Smooth((age - cast) / T.LinesFade)));
        }

        /// <summary>The circuit when the buff ends, <paramref name="age"/> s after: it runs back into the chest.</summary>
        internal static void EndLines(string key, TraceBody body, float age)
        {
            if (age < 0f || age >= T.EndLines) return;
            Circuit(key, body, TraceCircuit.Travel * (1f - Smooth(age / T.EndLines)), T.Bright * 0.8f);
        }

        /// <summary>One footprint <paramref name="age"/> s old; the k-th of a run, alternate ones either side of the path.</summary>
        internal static void Footprint(Vector2 at, Vector2 heading, int k, float age)
        {
            if (age < 0f || age >= T.Print) return;
            var perp = new Vector2(-heading.y, heading.x);
            float off = k % 2 == 1 ? 0.07f : -0.07f;
            Sprite(at + perp * off, 0.2f, 0.12f, Fade(Trace, 0.75f * (1f - age / T.Print)), glow, Floor + 0.005f,
                -Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
        }

        /// <summary>The faint streaks at the heels of a pawn at <paramref name="me"/> running along <paramref name="heading"/>.</summary>
        internal static void Streaks(string key, Vector2 me, Vector2 heading)
        {
            var perp = new Vector2(-heading.y, heading.x);
            for (int k = -1; k <= 1; k += 2)
            {
                Vector2 side = perp * (0.06f * k) + new Vector2(0f, 0.05f);
                Line(key + " streak " + k, new[] { me - heading * 0.45f + side, me + side }, 0.03f, Fade(Trace, 0.4f), Floor + 0.006f, 2);
            }
        }

        /// <summary>
        /// The glint at the point when the cast is done, and the weapon's glow: climbing from the grip while casting,
        /// then the pulsing idle; <paramref name="fade"/> takes it out when the buff ends.
        /// </summary>
        internal static void WeaponGlow(string key, TraceShape shape, UbwPose b, float climb, float sinceLit, float clock, float fade, float altitude)
        {
            if (climb > 0f && fade > 0f) Glow(key, shape, b, climb, T.GlowLevel(sinceLit, clock) * fade, altitude);
            if (sinceLit >= 0f && sinceLit < T.Glint) GokuGraphics.Glint(Tip(b), 0.3f, 1f - sinceLit / T.Glint, TraceHot);
        }

        /// <summary>
        /// A hit on the pawn drawn at <paramref name="foe"/>, <paramref name="age"/> s ago: a diagonal cut across its body,
        /// bowed a little and drawn in over the first third of its life (odd hits cut the other way), and a spark on
        /// the side the blow came from (<paramref name="from"/>, a unit direction the blow travelled).
        /// </summary>
        internal static void Slash(string key, Vector2 foe, int hit, float age, Vector2 from)
        {
            if (age < 0f || age >= T.Slash) return;
            float u = age / T.Slash, k = hit % 2 == 1 ? -1f : 1f;
            Vector2 a = PawnFit.At(foe, -0.26f * k, 0.56f), b = PawnFit.At(foe, 0.26f * k, 0.04f);
            var arc = new List<Vector2>(9);
            for (int j = 0; j <= 8 && j / 8f <= Mathf.Min(1f, u / 0.35f); j++)
            {
                float v = j / 8f, bow = Mathf.Max(0f, Mathf.Sin(v * Mathf.PI)) * 0.07f;
                arc.Add(new Vector2(Mathf.Lerp(a.x, b.x, v) + bow * k, Mathf.Lerp(a.y, b.y, v) + bow));
            }
            Line(key + " slash halo", arc, 0.16f, Fade(Trace, 0.35f * (1f - u)), PawnLayer + 0.02f, 2);
            Line(key + " slash", arc, 0.05f, Fade(White, 1f - u), PawnLayer + 0.021f, 2);
            if (age < T.Spark)
                GokuGraphics.Glint(PawnFit.At(foe, -from.x * 0.12f, 0.32f), 0.3f, 1f - age / T.Spark, White);
        }

        // ---- the preview ---------------------------------------------------------------------------------------

        /// <summary>
        /// The sketch at <paramref name="s"/>: the pawn casts 2 cells north of <paramref name="cell"/>, runs east at the
        /// raider 7 cells away, strikes twice and the buff ends. The pawn moving and the hits are the preview's script
        /// (<see cref="TraceReinforcementTiming"/>); the pawns are not drawn, the held longsword is.
        /// </summary>
        internal static void DrawPreview(Vector3 cell, float s)
        {
            if (s < 0f || s >= T.Duration) return;
            Vector2 dir = Vector2.right;
            const float side = 1f;
            var c = new Vector2(cell.x, cell.z + 2f);
            Vector2 start = c - dir * (T.Distance / 2f), foeAt = start + dir * T.Distance;
            Vector2 Walked(float x) => start + dir * T.Walked(x);

            float swing = T.SwingAge(s), lunge = swing >= 0f ? 0.14f * Mathf.Sin(swing / T.Swing * Mathf.PI) : 0f;
            Vector2 me = Walked(s) + dir * lunge;
            TraceBody body = TraceBody.Lab(me, side);

            CastRing(me, s - T.CastAt);
            if (s >= T.CastAt) CastLines("reinforce", body, s - T.CastAt, T.Cast);
            EndLines("reinforce", body, s - T.End);

            for (int k = 0; T.Run + k * T.Step < Mathf.Min(s, T.Arrive); k++)
            {
                float at = T.Run + k * T.Step;
                Footprint(Walked(at), dir, k, s - at);
            }
            if (s >= T.Run && s < T.Arrive) Streaks("reinforce", me, dir);

            // The held copy: it swings at the raider on each hit.
            TraceShape w = Shape("LongSword");
            float toFoe = Mathf.Atan2(foeAt.y - me.y, foeAt.x - me.x) * Mathf.Rad2Deg, rest = TraceOnTiming.RestEast;
            float turn = (toFoe - rest + 540f) % 360f - 180f, angle = rest + (swing >= 0f ? turn * Mathf.Sin(swing / T.Swing * Mathf.PI) : 0f);
            UbwPose b = UbwBlade.HeldCopy(w.W, T.Size, new UbwXZ(me.x, me.y), angle, side);
            Plain("reinforce weapon", w, b, PawnLayer + 0.012f, true);
            float climb = Mathf.Clamp01((s - T.CastAt - T.Climb(T.Cast)) / T.ClimbTime(T.Cast));
            float fade = s < T.End ? 1f : 1f - Smooth((s - T.End) / T.GlowFade);
            WeaponGlow("reinforce weapon", w, b, climb, s - T.Lit, s, fade, PawnLayer + 0.012f);

            int landed = 0;
            for (int i = 0; i < 2; i++) if (s >= T.Hit(i)) landed++;
            Vector2 foe = foeAt + dir * (landed * 0.08f);
            for (int i = 0; i < 2; i++) Slash("reinforce " + i, foe, i, s - T.Hit(i), dir);
        }
    }
}
