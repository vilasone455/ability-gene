using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using T = RimArt.ThunderGodChainTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Flying Thunder God: Chain. One strip of floor script and one set of brackets per jump,
    /// all written together so the route can be read before anything moves; then for each jump what
    /// is left behind, the line, a faint line that stays so the whole route shows at the end, the
    /// arrival glint, and the slash and hit spark. No pawn and no kunai is drawn.
    /// </summary>
    public static class ThunderGodChainGraphics
    {
        private static readonly ChainHop[] hops = new ChainHop[T.MostTargets + 1];

        /// <summary><paramref name="toward"/> is the unit direction from the caster to the first target.</summary>
        public static void Draw(Vector3 centre, Vector2 toward, int targets, bool returns, float seconds, Map map)
        {
            if (seconds < 0f || seconds >= T.Duration(targets, returns)) return;
            var middle = new Vector2(centre.x, centre.z);
            int count = T.Route(middle, toward, targets, returns, hops);
            for (int k = 0; k < count; k++) if (!Shown(hops[k].from, map) || !Shown(hops[k].to, map)) return;
            Begin(middle);
            float written = (seconds - T.CastAt) / T.Seal;

            if (written > 0f)
                for (int k = 0; k < count; k++)
                {
                    ChainHop h = hops[k];
                    float landed = seconds - T.ArriveAt(k), burn = (landed - T.Flash) / T.Linger;
                    if (burn >= 1f) continue;
                    float beat = landed < 0f ? 1f : 0.75f + 0.25f * Mathf.Sin(landed * 9f);
                    float lit = Mathf.Clamp01(written) * (1f - Smooth(burn)) * beat;
                    if (h.hasTarget)
                    {
                        Sprite(h.target + h.along * ((ThunderGodTiming.Behind - ThunderGodTiming.StripBack) / 2f), 1.8f, 0.5f,
                            Fade(Gold, 0.3f * lit), glow, Floor + 0.005f + k * 0.0002f, -h.degrees);
                        Script(h.target, h.degrees, -ThunderGodTiming.StripBack, ThunderGodTiming.StripGlyphs, seconds, T.CastAt, T.Seal, burn, k * 4);
                        SealOnPawn(h.target, h.thrown, Smooth(written) * (1f - 0.7f * Smooth(landed / T.Flash)) * (1f - Smooth(burn)));
                    }
                    else
                    {
                        Sprite(h.to, 1.5f, 1.5f, Fade(Gold, 0.25f * lit), glow, Floor + 0.005f + k * 0.0002f);
                        ScriptCross(h.to, h.degrees, seconds, T.CastAt, T.Seal, burn, k * 4);
                    }
                    Brackets(h.to, h.degrees, Mathf.Clamp01((written - 0.8f) / 0.2f) * (1f - Mathf.Clamp01((burn - 0.85f) / 0.15f)));
                }

            // The caster is at the last place a jump has landed, widening out of a sliver there and
            // narrowing into one as the next jump starts.
            int stop = 0;
            while (stop < count && seconds >= T.ArriveAt(stop)) stop++;
            float thin = Mathf.Max(stop > 0 ? 1f - Smooth((seconds - T.ArriveAt(stop - 1)) / T.Squeeze) : 0f,
                stop < count ? Smooth((seconds - T.GoAt(stop)) / T.Squeeze) : 0f);
            Sliver(stop == 0 ? hops[0].from : hops[stop - 1].to, thin);

            for (int k = 0; k < count; k++)
            {
                ChainHop h = hops[k];
                float gone = seconds - T.GoAt(k), here = seconds - T.ArriveAt(k);
                Leave(h.from, gone, T.Squeeze, T.FlashRadius, k == 0);
                JumpLine(h.from, h.to, gone / T.Line, T.LineWidth);
                float faded = (gone - T.Line) / T.RouteGlow;
                if (faded >= 0f && faded < 1f)
                    Streak(new Vector2(h.from.x, h.from.y + ThunderGodTiming.Chest), new Vector2(h.to.x, h.to.y + ThunderGodTiming.Chest),
                        T.LineWidth * 0.7f, Fade(Gold, 0.45f * (1f - faded) * (1f - faded)), whiteGlow, Overhead + 0.015f + k * 0.0002f, 12);
                Star(new Vector2(h.to.x, h.to.y + ThunderGodTiming.Chest), here, T.Flash, T.FlashRadius);
                Sparks(h.to, here, 8);
                if (!h.hasTarget) continue;
                Slash(h.to, h.degrees + 180f, seconds - T.StrikeAt(k));
                HitSpark(h.target, seconds - T.HitAt(k));
            }
        }
    }
}
