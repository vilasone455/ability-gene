using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.ChainSickleGraphics;
using T = RimArt.ChainSickleSnagTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Snag: the weight hangs at the hip, circles overhead on a taut chain, flies to the
    /// target's chest with the chain paying out, the chain coils twice round the body, then the
    /// target slides toward the holder (or the holder is dragged toward a target too heavy to move)
    /// with heel scuffs that stay and dust at the feet. The caster and target stand-ins are not drawn.
    /// </summary>
    public static class ChainSickleSnagGraphics
    {
        private static readonly Vector3[] PathPts = new Vector3[25], CoilPts = new Vector3[37];

        /// <summary>
        /// The preview. <paramref name="centre"/> is halfway between the holder and the target, as in
        /// the lab's sketch; <paramref name="target"/> indexes ChainSickleRule.ScriptTargets.
        /// </summary>
        public static void DrawPreview(Vector3 centre, float aimDegrees, int target, float seconds, Map map)
        {
            var c = new Vector2(centre.x, centre.z);
            Vector2 toward = Turn(aimDegrees);
            ChainSickleWeight rule = ChainSickleRule.Script(target);
            T.ScriptMoves(rule, T.ScriptDistance, out float pull, out float back);
            var shot = new ChainSnagShot
            {
                Caster0 = c - toward * (T.ScriptDistance / 2f), Start = c + toward * (T.ScriptDistance / 2f), Aim = aimDegrees,
                Pull = pull, Back = back, Reel = rule.Reel, Dragged = rule.Dragged, Size = ChainSickleRule.ScriptTargets[target].Size,
                Range = Range, Hold = T.Hold,
            };
            Draw(shot, seconds, map);
        }

        /// <param name="weapon">Draw the sickle in the hand. In game it is left off once the holder's cast job is over.</param>
        public static void Draw(in ChainSnagShot shot, float s, Map map, bool weapon = true)
        {
            if (s < 0f || s >= T.End(shot.Reel, shot.Hold) || !Shown(shot.Caster0, map)) return;
            Begin(shot.Caster0);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            var f = new ChainSickleFrame(shot.Aim, sun);

            // Where the target and the holder are: the target slides Pull cells toward the holder
            // during the reel, or the holder slides Back cells toward the target.
            float reel = Mathf.Max(0.001f, shot.Reel), reelEnd = T.ReelEnd(reel);
            float reelU = T.ReelShare(s, reel);
            Vector2 o = shot.TargetAt ?? f.Ground(shot.Start, -shot.Pull * reelU, 0f);
            bool reeling = s >= T.Reel0 && s < reelEnd;
            float lean = reeling ? T.Lean * Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(Mathf.Min(1f, (s - T.Reel0) / reel) * Mathf.PI))) : 0f;
            Vector2 holder = shot.CasterAt ?? f.Ground(shot.Caster0, shot.Back * reelU, 0f);
            Vector2 caster = f.Ground(holder, shot.Dragged ? 0f : -lean, 0f);
            Vector2 hand = f.Ground(caster, 0.20f, 0.18f);
            Vector3 hip = At(f.Ground(caster, -0.08f, 0.30f), 0.32f);

            // Floor first: the scuffs behind whoever is dragged, the range ring at the throw, a pale
            // ring round the target at the hit.
            if (s >= T.Reel0 && shot.Pull > 0f) Scuff(shot.Start, f.Ground(shot.Start, -shot.Pull, 0f), reelU);
            if (s >= T.Reel0 && shot.Back > 0f) Scuff(shot.Caster0, f.Ground(shot.Caster0, shot.Back, 0f), reelU, 0.22f);
            RangeRing(shot.Caster0, shot.Range, s - T.Throw0);
            if (s >= T.Hit) Circle(o, 0.45f, 0.35f * (1f - Clamp01((s - T.Hit) / 1.2f)), Floor + 0.012f, Cream);

            // The coil: a level spiral from the chest down to the waist, scaled with the body.
            float big = Mathf.Sqrt(Mathf.Max(0.1f, shot.Size)), cr = CoilR * big, ch0 = (ChestH + 0.1f) * big, ch1 = 0.25f * big;
            float wrapU = s < T.Hit ? 0f : Smooth((s - T.Hit) / T.Wrap);
            int coil = wrapU > 0f
                ? CoilPath(CoilPts, o, CoilTurns * wrapU, cr, ch0, ch1, shot.Aim + 180f, Mathf.Max(4, Round(36f * wrapU)))
                : 0;

            // The weight through the phases, and the chain from the hand to it.
            Vector3 w;
            int path;
            if (s < T.Spin0)
            {
                w = hip;
                path = ChainPath(PathPts, At(hand, HandH), w, 0.12f);
            }
            else if (s < T.Throw0)
            {
                float u = (s - T.Spin0) / T.Spin, ang = f.radians + (u * u * 0.5f + u * 0.5f - 1f) * T.Turns * Mathf.PI * 2f;
                float up = Smooth(u * 3f);
                w = new Vector3(caster.x + Mathf.Cos(ang) * T.SpinR, T.SpinH * up + 0.32f * (1f - up), caster.y + Mathf.Sin(ang) * T.SpinR);
                path = ChainPath(PathPts, At(hand, HandH), w, 0.03f);
                // Motion blur: a faint arc behind the weight.
                float blur = up * 0.7f;
                for (int i = 1; i <= 9; i++)
                {
                    float a2 = ang - i * 0.11f;
                    var q = new Vector2(caster.x + Mathf.Cos(a2) * T.SpinR, caster.y + Mathf.Sin(a2) * T.SpinR + w.y * Lift);
                    Sprite(q, 0.26f, 0.2f, Fade(Body, blur * (1f - i / 10f)), soft, Y + 0.025f + i * 0.0001f);
                }
            }
            else if (s < T.Hit || coil == 0)
            {
                float u = Clamp01((s - T.Throw0) / T.Flight), arc = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI));
                var from = new Vector3(caster.x + f.ca * T.SpinR, T.SpinH, caster.y + f.sa * T.SpinR);
                var to = new Vector3(o.x, ChestH * big, o.y);
                w = new Vector3(Mathf.Lerp(from.x, to.x, u), Mathf.Lerp(from.y, to.y, u) + 0.15f * arc, Mathf.Lerp(from.z, to.z, u));
                path = ChainPath(PathPts, At(hand, HandH), w, 0.08f * arc + 0.02f);
            }
            else
            {
                // Wrapped: the chain runs hand -> first coil point, the weight rides the coil's end.
                w = CoilPts[coil - 1];
                float sag = reeling ? 0.01f : s < T.Reel0 ? Mathf.Lerp(0.02f, 0.06f, Smooth((s - T.Wrapped) / T.Settle)) : 0.05f;
                path = ChainPath(PathPts, At(hand, HandH), CoilPts[0], sag, 24, reeling ? 0.012f : 0f, s);
            }

            // Dust at the feet of whoever is dragged.
            if (reeling && shot.Pull > 0f) Kick(o, s - T.Reel0, 1f - reelU * 0.6f, 7);
            if (reeling && shot.Back > 0f) Kick(caster, s - T.Reel0, 1f - reelU * 0.6f, 9);

            // The chain sits on MoteOverhead; the coil's far half under the pawn, so the pawn stands
            // between the two halves.
            if (weapon) Sickle(hand, HandH, shot.Aim, sun, strength, Y + 0.05f);
            Chain(PathPts, path, sun, strength, Y + 0.02f);
            if (coil > 0) Coil(CoilPts, coil, o.y + 0.02f, sun, strength);
            Weight(w, sun, strength, Y + 0.03f);

            // Wrap hit: a short pale flash at the chest.
            float hitAge = s - T.Hit;
            if (hitAge >= 0f && hitAge < 0.15f)
                Sprite(new Vector2(o.x, o.y + ChestH * big * Lift), 0.6f, 0.5f, Fade(Cream, (1f - hitAge / 0.15f) * 0.7f), soft, Y + 0.06f);
        }
    }
}
