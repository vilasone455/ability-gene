using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.SamehadaGraphics;
using T = RimArt.SamehadaFusionTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Fusion. The preview plays samehada-fusion.js's script: the blade is drawn back into the
    /// hand while the tally empties and the hide spreads over the body with a purple flash at the arm and
    /// chakra motes; the fused holder walks with a dorsal fin, tail and gills drawn by facing and a green
    /// regen ring every second; then the hide fades and the blade grows back out at no charge. The water
    /// patch, the second pawn and the holder's stand-in are not drawn.
    ///
    /// In game <see cref="DrawFused"/> draws the merge, the shark form and the rings, and the revert,
    /// on a holder that keeps the weapon in hand.
    /// </summary>
    public static class SamehadaFusionGraphics
    {
        /// <summary>The shark form's altitudes in the sketch: over the pawn layer, and 0.03 under that for what hangs behind.</summary>
        public static float SketchOver => PawnLayer + 0.012f;

        /// <summary>The preview. <paramref name="centre"/> is the sketch's centre; the holder starts 1.5 cells behind it.</summary>
        public static void DrawPreview(Vector3 centre, float aim, float s, Map map)
        {
            if (s < 0f || s >= T.End) return;
            var o = new Vector2(centre.x, centre.z);
            if (!Shown(o, map)) return;
            Begin(o);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            Vector2 start = Ground(o, aim, -T.ScriptBack, 0f);
            float sign = Mirror(aim);

            float shark = T.SharkAt(s, T.Revert0), charges = T.ChargesAt(s, MaxCharges), bladeOut = T.BladeOut(s);
            Vector2 holder = Ground(start, aim, T.Walked(s), 0f);

            Tally(holder, charges, aim);
            Flashes(holder, sign, s, T.Revert0);
            SharkForm(holder, aim, shark, s, SketchOver, SketchOver - 0.03f);
            if (s >= T.Fused0 && s < T.Revert0) RegenPulse(holder, s - T.Fused0);

            // The blade at rest, shrinking into the hand and growing back out: drawn at the charge whose
            // length matches, so at no length it is inside the hand.
            if (bladeOut > 0.02f)
            {
                float deg = aim + sign * T.Rest;
                float wantLen = BladeLength(charges) * bladeOut, pseudo = (wantLen - BaseLength) / LengthPerCharge;
                Blade(Hand(holder, deg), deg, pseudo, sun, strength, alpha: Mathf.Min(1f, bladeOut * 3f));
            }
        }

        /// <summary>The merge's purple flash at the arm and its chakra motes; the revert's flash.</summary>
        private static void Flashes(Vector2 holder, float sign, float s, float revert0)
        {
            if (s >= T.Merge0 && s < T.Fused0 + 0.2f)
            {
                float u = Mathf.Clamp01((s - T.Merge0) / (T.Merge + 0.2f));
                Sprite(new Vector2(holder.x + 0.2f * sign, holder.y + 0.35f), 0.7f, 0.6f, Fade(FleshLit, 0.5f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI))), glow, Y + 0.06f);
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * Mathf.PI * 2f + s * 4f, r = 0.35f * (1f - u);
                    Sprite(new Vector2(holder.x + Mathf.Cos(a) * r, holder.y + 0.3f + Mathf.Sin(a) * r * 0.6f), 0.1f, 0.1f, Fade(Chakra, 0.8f * (1f - u)), glow, Y + 0.061f + i * 0.00002f);
                }
            }
            if (s >= revert0 && s < revert0 + T.Revert + 0.2f)
            {
                float u = Mathf.Clamp01((s - revert0) / (T.Revert + 0.2f));
                Sprite(new Vector2(holder.x + 0.2f * sign, holder.y + 0.35f), 0.6f, 0.5f, Fade(FleshLit, 0.4f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI))), glow, Y + 0.06f);
            }
        }

        /// <summary>
        /// A fused holder in game at its feet <paramref name="feet"/>, <paramref name="s"/> seconds on the
        /// fusion's clock (the merge starts at Merge0) for a fusion lasting <paramref name="seconds"/> from
        /// the merge: flashes, the shark form for <paramref name="facingDeg"/>, the regen ring every second.
        /// The shark goes over the pawn's own layers (head and headgear included) and its tail under them.
        /// </summary>
        /// <param name="pawnAltitude">The pawn's DrawPos.y. Each pawn is drawn up to one altitude step (0.037) above or
        /// below the Pawn layer (Pawn_DrawTracker.SeededYOffset), so the form is placed from the pawn's own altitude.</param>
        public static void DrawFused(Vector2 feet, float facingDeg, float s, float seconds, Map map, float pawnAltitude)
        {
            if (!Shown(feet, map)) return;
            Begin(feet);
            float revert0 = T.Merge0 + Mathf.Max(T.Merge + T.Revert, seconds) - T.Revert;
            if (s >= revert0 + T.Revert + 0.2f) return;
            float sign = Mirror(facingDeg);
            Flashes(feet, sign, s, revert0);
            SharkForm(feet, facingDeg, T.SharkAt(s, revert0), s, pawnAltitude + 0.0295f, pawnAltitude - 0.012f);
            if (s >= T.Fused0 && s < revert0) RegenPulse(feet, s - T.Fused0, 1f, pawnAltitude);
        }
    }
}
