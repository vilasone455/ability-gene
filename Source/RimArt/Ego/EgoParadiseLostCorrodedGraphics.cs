using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.EgoParadiseLostGraphics;
using T = RimArt.EgoParadiseLostTiming;

namespace RimArt
{
    /// <summary>
    /// The corroded wielder's look, the port of star(), wings() and halo() in ego-paradise-lost-v2.js (the Library
    /// of Ruina footage and Realization sprite). The red thorn star flat on the floor round the wielder: a red
    /// circle 0.78 cells across its radius with a soft glow, then 4 long spikes on the compass points (1.15 cells),
    /// 4 shorter on the diagonals (0.72) and 8 small ones between (0.3), each inked with a lit half. The white
    /// wings (<see cref="EgoParadiseLostWingsGraphics"/>). The gold thorn halo 0.16 cells over the head: a level
    /// ring of radius 0.2 with 12 spikes, turning 12 degrees a second, and a soft gold glow.
    ///
    /// <c>look</c> runs 0 to 1 as corrosion starts (the circle grows over the first 60 %, the spikes after a
    /// quarter, the wings open from folded along the back) and back to 0 as it ends. The star and the halo are
    /// level circles and need no per-facing work; the wings have one. The star and the halo sit on the cell
    /// centre and the real head top (+0.63, lib/pawn.js), so nothing goes through PawnFit. The wings and the halo
    /// are drawn from the wielder's own height (its DrawPos.y): the halo 0.03 over <see cref="PawnBody.Over"/>, over
    /// the wings in front.
    ///
    /// Not drawn (lab aids): the thin red ring at the ring's true radius that stays while corroded, and the
    /// Sanity pips under the feet (a gizmo in game).
    ///
    /// Fully grown, the star's spikes hold still and the halo's only turn, so both are baked once
    /// (<see cref="VfxDraw.BeginBake"/>) and drawn moved and turned, as the wings are.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class EgoParadiseLostCorrodedGraphics
    {
        /// <summary>The halo's height over the head top (<see cref="PawnBody.HeadTop"/>).</summary>
        public const float HaloOver = 0.16f;

        /// <summary>
        /// Set by the debug window's timing switch: rebuild the wings, the star and the halo every frame, as before
        /// the bake, to compare the cost.
        /// </summary>
        internal static bool Rebuild;

        // The fully grown star's spikes and halo's spikes, the centre each was baked at and the halo's height.
        private static List<VfxBakedDraw> starSpikes, haloSpikes;
        private static Vector2 starAt, haloAt;
        private static float haloLayer;

        /// <summary>
        /// The star, the wings and the halo of a pawn at <paramref name="wielder"/> (its DrawPos, <paramref name="body"/>
        /// its y) facing <paramref name="facing"/>. <paramref name="seconds"/> is any running clock; it turns the halo and
        /// sways the wings.
        /// </summary>
        public static void Draw(Vector2 wielder, float body, Rot4 facing, float look, float seconds, Map map)
        {
            if (look <= 0f || !Shown(wielder, map)) return;
            Begin(wielder);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            ThornStar(wielder, look);
            EgoParadiseLostWingsGraphics.Draw(wielder, body, facing, look, seconds, sun, strength);
            Halo(wielder, body, look, seconds);
        }

        // The red thorn star flat on the floor round the wielder.
        private static void ThornStar(Vector2 c, float amount)
        {
            float rk = T.EaseOut(amount * 1.6f), sk = Smooth((amount - 0.25f) / 0.75f), r0 = T.StarRadius * rk;
            Sprite(c, 4.4f * rk, 4.4f * rk, Fade(StarGlow, 0.16f * amount), glow, Floor + 0.044f);
            Disc(c, Floor + 0.045f, r0, r0, 0f, Fade(ThornDark, 0.3f * amount));
            PaperBombGraphics.RingAt(c, r0 + 0.02f, Fade(RedInk, 0.8f * amount), Floor + 0.046f, true, solid);
            PaperBombGraphics.RingAt(c, r0, Fade(Star, amount), Floor + 0.047f, true, solid);
            if (sk <= 0f) return;
            if (amount < 1f || Rebuild)
            {
                StarSpikes(c, r0, sk, amount);
                return;
            }
            if (starSpikes == null)
            {
                starSpikes = Bake(() => StarSpikes(c, r0, 1f, 1f));
                starAt = c;
            }
            DrawBaked(starSpikes, starAt, 0f, c - starAt);
        }

        // The star's 16 spikes, sk of their full length.
        private static void StarSpikes(Vector2 c, float r0, float sk, float amount)
        {
            for (int i = 0; i < 16; i++)
            {
                bool cardinal = i % 4 == 0, diagonal = i % 4 == 2;
                float length = (cardinal ? 1.15f : diagonal ? 0.72f : 0.3f) * sk, hw = cardinal ? 0.12f : diagonal ? 0.09f : 0.05f;
                Vector2 d = Turn(i * 22.5f + 90f), n = Left(d), b = c + d * (r0 - 0.02f), tip = c + d * (r0 + length);
                float lay = Floor + 0.048f + i * 0.0002f;
                Spike(b, n, hw + 0.025f, tip + d * 0.04f, Fade(RedInk, 0.8f * amount), lay);
                Spike(b, n, hw, tip, Fade(Star, amount), lay + 0.0001f);
                // The lit half: from the left edge of the base to the centre line.
                Band(b + n * (hw * 0.9f), b, tip, tip, Fade(StarGlow, 0.55f * amount), lay + 0.0002f);
            }
        }

        // The gold thorn halo over the head: a level circle with twelve spikes, turning slowly.
        private static void Halo(Vector2 pos, float body, float amount, float seconds)
        {
            var c = new Vector2(pos.x, pos.y + PawnBody.HeadTop + HaloOver);
            float r = 0.2f * T.EaseOut(amount), L = body + PawnBody.Over + 0.03f;
            Sprite(c, 0.9f * amount, 0.7f * amount, Fade(Gold, 0.35f * amount), glow, L - 0.001f);
            PaperBombGraphics.RingAt(c, r, Fade(Gold, amount), L, false, solid);
            if (amount < 1f || Rebuild)
            {
                HaloSpikes(c, r, amount, seconds * 12f, L);
                return;
            }
            if (haloSpikes == null)
            {
                haloSpikes = Bake(() => HaloSpikes(c, r, 1f, 0f, L));
                haloAt = c;
                haloLayer = L;
            }
            // Turn() goes counter-clockwise and DrawMesh clockwise, hence the minus.
            DrawBaked(haloSpikes, haloAt, -seconds * 12f, c - haloAt, L - haloLayer);
        }

        // The halo's 12 spikes, the first at turn degrees.
        private static void HaloSpikes(Vector2 c, float r, float amount, float turn, float L)
        {
            for (int i = 0; i < 12; i++)
            {
                Vector2 d = Turn(i * 30f + turn), n = Left(d), b = c + d * (r * 0.95f);
                Spike(b, n, 0.025f, c + d * (r + 0.09f * amount), Fade(Gold, amount), L + 0.0002f + i * 0.00001f);
            }
        }

        private static List<VfxBakedDraw> Bake(Action draw)
        {
            var into = new List<VfxBakedDraw>();
            BeginBake(into);
            try { draw(); }
            finally { EndBake(); }
            return into;
        }
    }
}
