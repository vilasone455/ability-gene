using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using T = RimArt.ThunderGodJumpTiming;

namespace RimArt
{
    /// <summary>
    /// Draws the Flying Thunder God jump: the floor script from the kunai to the landing cell (a
    /// cross round a kunai on the ground), the brackets, the seal on the kunai, what is left where
    /// the caster stood, the line, the arrival glint, and the slash and hit spark when the kunai is
    /// in an enemy. Neither pawn nor the kunai is drawn.
    /// </summary>
    public static class ThunderGodJumpGraphics
    {
        /// <summary>
        /// The preview: <paramref name="centre"/> is halfway between the caster and the kunai and
        /// <paramref name="toward"/> is the unit direction from the caster to the kunai.
        /// </summary>
        public static void Draw(Vector3 centre, Vector2 toward, bool inEnemy, float seconds, Map map)
        {
            var middle = new Vector2(centre.x, centre.z);
            Draw(T.Home(middle, toward), T.Kunai(middle, toward), T.Landing(middle, toward, inEnemy), inEnemy, seconds, map);
        }

        /// <summary>
        /// A real jump. Ground points (the feet): <paramref name="home"/> where the caster left from,
        /// <paramref name="kunai"/> the kunai (the pawn it is stuck in), <paramref name="landing"/> the
        /// cell the caster lands on. In an enemy the strip runs from the kunai into the landing cell,
        /// whichever side of the pawn that is.
        /// </summary>
        public static void Draw(Vector2 home, Vector2 kunai, Vector2 landing, bool inEnemy, float seconds, Map map)
        {
            if (seconds < 0f || seconds >= T.Duration) return;
            if (!Shown(home, map) || !Shown(landing, map)) return;
            Begin((home + landing) / 2f);
            Vector2 behind = landing - kunai, travel = kunai - home;
            float reach = behind.magnitude;
            Vector2 strip = inEnemy && reach > 0.01f ? behind / reach : travel.sqrMagnitude > 1e-4f ? travel.normalized : Vector2.right;
            float aim = ThunderGodTiming.Degrees(strip), thrown = ThunderGodTiming.Degrees(travel.sqrMagnitude > 1e-4f ? travel : strip);
            int glyphs = Mathf.Max(ThunderGodTiming.StripGlyphs, Mathf.RoundToInt((reach + ThunderGodTiming.StripBack) / ThunderGodTiming.GlyphPitch));

            // No ring: a ring reads as an area of effect and this has none. The strip says where the caster lands.
            float written = (seconds - T.CastAt) / T.Seal, burn = (seconds - T.SettleAt) / T.Linger;
            if (written > 0f && burn < 1f)
            {
                float beat = seconds < T.ArriveAt ? 1f : 0.75f + 0.25f * Mathf.Sin((seconds - T.ArriveAt) * 9f);
                float lit = Mathf.Clamp01(written) * (1f - Smooth(burn)) * beat;
                if (inEnemy)
                {
                    Sprite(kunai + strip * ((reach - ThunderGodTiming.StripBack) / 2f), reach + 0.8f, 0.5f,
                        Fade(Gold, 0.3f * lit), glow, Floor + 0.005f, -aim);
                    Script(kunai, aim, -ThunderGodTiming.StripBack, glyphs, seconds, T.CastAt, T.Seal, burn);
                }
                else
                {
                    Sprite(kunai, 1.5f, 1.5f, Fade(Gold, 0.25f * lit), glow, Floor + 0.005f);
                    ScriptCross(kunai, aim, seconds, T.CastAt, T.Seal, burn);
                }
                Brackets(landing, aim, Mathf.Clamp01((written - 0.8f) / 0.2f) * (1f - Mathf.Clamp01((burn - 0.85f) / 0.15f)));
            }

            float seal = Smooth(written) * (1f - 0.7f * Smooth((seconds - T.ArriveAt) / T.Flash)) * (1f - Smooth(burn));
            if (inEnemy) SealOnPawn(kunai, thrown, seal);
            else if (seconds < T.ArriveAt) SealOnGround(kunai, seal);
            else
            {
                // The ground kunai goes back into the belt.
                float picked = seconds - T.ArriveAt;
                if (picked < 0.3f) Circle(kunai, 0.2f + picked * 0.8f, 0.8f * (1f - picked / 0.3f), Floor + 0.04f, Pale);
            }

            if (seconds < T.ArriveAt) Sliver(home, Smooth((seconds - T.GoAt) / T.Squeeze));
            else Sliver(landing, 1f - Smooth((seconds - T.ArriveAt) / T.Squeeze));

            Leave(home, seconds - T.GoAt, T.Squeeze, T.FlashRadius);
            JumpLine(home, landing, (seconds - T.GoAt) / T.Line, T.LineWidth);
            Star(new Vector2(landing.x, landing.y + ThunderGodTiming.Chest), seconds - T.ArriveAt, T.Flash, T.FlashRadius);
            Sparks(landing, seconds - T.ArriveAt, 10);
            if (!inEnemy) return;
            Slash(landing, aim + 180f, seconds - T.StrikeAt);
            HitSpark(kunai, seconds - T.HitAt);
        }
    }
}
