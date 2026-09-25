using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.GojoGraphics;
using T = RimArt.UnlimitedVoidInsideTiming;

namespace RimArt
{
    /// <summary>
    /// Draws the inside of Unlimited Void round the cell Gojo lands on: white over the whole view at the
    /// arrival (the move to the pocket map happens behind it), a white burst ring out to the radius and
    /// the splatter burst; the space flying in from the vanishing point 7.5 cells north while the
    /// speed-line tunnel and its dust stream out of it; the white light there and the black hole opening
    /// under it; the void holding; at the end the black hole collapsing to a point and white filling
    /// the view (the move back happens behind it).
    ///
    /// The port of Tools/VfxLab/web/sketches/gojo-unlimited-void-inside.js with the Cursed Clash colours
    /// (gojo-unlimited-void-inside-cc.js). The camera push is <see cref="CameraMove"/>'s, played by the
    /// preview with <see cref="UnlimitedVoidInsideTiming.CameraEvents"/>. The sketch's stand-in pawns are
    /// not ported, and with them go everything drawn on them: the specks into frozen heads, Gojo's touches
    /// and blows, the overload marks and the immune pair's fight. Those are the ability's.
    /// </summary>
    internal static class UnlimitedVoidInsideGraphics
    {
        /// <summary>
        /// The preview: the void drawn over the map's ground (<see cref="VoidLayers.Preview"/>).
        /// <paramref name="reach"/> is <see cref="VoidSpaceGraphics.ReachFor"/> of the vanishing point when it started.
        /// </summary>
        public static void DrawPreview(Vector3 centre, float reach, float seconds, Map map) =>
            Draw(new Vector2(centre.x, centre.z), reach, VoidLayers.Preview, seconds, map);

        public static void Draw(Vector2 o, float reach, in VoidLayers layers, float s, Map map)
        {
            if (s < 0f || s >= T.Duration || !Shown(o, map)) return;
            Begin(o);
            Vector2 point = T.PointFor(o);
            float ending = T.Ending(s), open = T.Open(s), lines = T.Lines(s);

            // --- the space, the speed-line tunnel and the black hole --------------------------------------------
            // The space flies in from the vanishing point while the lines run and settles as they end; the
            // white light grows at that point and the black hole opens under it as it fades.
            VoidSpaceGraphics.SpaceFloor(o, s, T.FadeIn(s), layers, point, T.Flight(s), T.Flight(s - 0.1f));
            VoidSpaceGraphics.Tunnel(point, s, lines, T.Streaks, T.Length, T.LineTrip, reach, T.Hollow(s), layers);
            VoidSpaceGraphics.TunnelDust(point, s, lines, T.LineTrip, reach, layers);
            VoidSpaceGraphics.TunnelLight(point, T.Light(s), layers);
            VoidSpaceGraphics.Hole(point, T.HoleRadius * open * (1f - Smooth(ending / 0.6f)), s * (1f + 3f * ending), open, layers);

            // --- the arrival: white, the burst and ring on Gojo (anime ep. 33), the splatter burst -------------------
            WhiteView(1f - Smooth(s / T.WhiteFade));
            float ringAge = s / T.BurstRing;
            if (ringAge < 1f)
            {
                float r = T.Radius * 1.05f * Smooth(ringAge), fade = 1f - ringAge, flash = 5f * (1f - ringAge * 0.5f);
                PaperBombGraphics.RingAt(o, r, Fade(White, 0.9f * fade), Overhead + 0.1f, true, whiteGlow);
                PaperBombGraphics.RingAt(o, r * 1.03f, Fade(Pink, 0.35f * fade), Overhead + 0.101f, false, whiteGlow);
                PaperBombGraphics.RingAt(o, r * 0.97f, Fade(Teal, 0.35f * fade), Overhead + 0.102f, false, whiteGlow);
                Sprite(new Vector2(o.x, o.y + 0.5f), flash, flash, Fade(White, 0.7f * fade), glow, Overhead + 0.103f);
            }
            Splatter(new Vector2(o.x, o.y + 0.5f), T.SplatterReach, (1f - Smooth((s - 0.1f) / 0.8f)) * Mathf.Clamp01(s / 0.05f));

            // --- the end: the black hole collapses to a point and white fills the view ---------------------------------
            if (ending > 0f)
            {
                float toPoint = Smooth(ending / 0.6f), fill = Smooth((ending - 0.45f) / 0.55f);
                Sprite(point, 0.6f + 3f * toPoint, 0.6f + 3f * toPoint, Fade(White, 0.9f * toPoint), glow, Overhead + 0.2f);
                Sprite(point, 40f * fill + 1f, 40f * fill + 1f, Fade(White, fill), glow, Overhead + 0.21f);
                WhiteView(fill * fill);
                PaperBombGraphics.RingAt(point, 0.5f + 14f * toPoint, Fade(Ice, 0.6f * (1f - toPoint)), Overhead + 0.205f, true, whiteGlow);
            }
        }
    }
}
