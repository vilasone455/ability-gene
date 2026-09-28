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
        private static readonly ChainHop[] preview = new ChainHop[T.MostTargets + 1];

        /// <summary>The preview: <paramref name="toward"/> is the unit direction from the caster to the first target.</summary>
        public static void Draw(Vector3 centre, Vector2 toward, int targets, bool returns, float seconds, Map map)
        {
            var middle = new Vector2(centre.x, centre.z);
            int count = T.Route(middle, toward, targets, returns, preview);
            Draw(preview, count, T.Duration(targets, returns), seconds, map);
        }

        /// <summary>
        /// A real chain: <paramref name="hops"/> holds <paramref name="count"/> jumps in order, as
        /// <see cref="ThunderGodChainTiming.Route"/> fills them for the preview; <paramref name="duration"/> is
        /// when the last strip has burnt away.
        /// </summary>
        public static void Draw(ChainHop[] hops, int count, float duration, float seconds, Map map)
        {
            if (seconds < 0f || seconds >= duration || count <= 0) return;
            for (int k = 0; k < count; k++) if (!hops[k].skipped && (!Shown(hops[k].from, map) || !Shown(hops[k].to, map))) return;
            Begin(hops[0].from);
            float written = (seconds - T.CastAt) / T.Seal;

            if (written > 0f)
                for (int k = 0; k < count; k++)
                {
                    ChainHop h = hops[k];
                    float landed = seconds - T.ArriveAt(k), burn = (landed - T.Flash) / T.Linger;
                    if (burn >= 1f || h.skipped) continue;
                    float beat = landed < 0f ? 1f : 0.75f + 0.25f * Mathf.Sin(landed * 9f);
                    float lit = Mathf.Clamp01(written) * (1f - Smooth(burn)) * beat;
                    if (h.hasTarget)
                    {
                        // The strip runs from the target into the landing cell: 1 cell straight behind, 1.4 on a diagonal.
                        float reach = Vector2.Distance(h.target, h.to);
                        int glyphs = Mathf.Max(ThunderGodTiming.StripGlyphs, Mathf.RoundToInt((reach + ThunderGodTiming.StripBack) / ThunderGodTiming.GlyphPitch));
                        float strip = reach > 0.01f ? ThunderGodTiming.Degrees(h.to - h.target) : h.degrees;
                        Sprite(h.target + Turn(strip) * ((reach - ThunderGodTiming.StripBack) / 2f), reach + 0.8f, 0.5f,
                            Fade(Gold, 0.3f * lit), glow, Floor + 0.005f + k * 0.0002f, -strip);
                        Script(h.target, strip, -ThunderGodTiming.StripBack, glyphs, seconds, T.CastAt, T.Seal, burn, k * 4);
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
                if (h.skipped) continue;
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
                Slash(h.to, ThunderGodTiming.Degrees(h.target - h.to), seconds - T.StrikeAt(k));
                HitSpark(h.target, seconds - T.HitAt(k));
            }
        }
    }
}
