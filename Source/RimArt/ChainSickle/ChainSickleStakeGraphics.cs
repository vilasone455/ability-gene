using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.ChainSickleGraphics;
using T = RimArt.ChainSickleStakeTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Stake: the holder yanks, the coil tightens on the chest, the weight drops on a short
    /// tether and drives into the floor beside the feet with dust and a crack that stays, and a floor
    /// ring shrinks over the pin. The preview's script adds the pawn straining against the chain,
    /// the holder stepping in and the 1.5x sickle cut with its flash and blood. The caster, target
    /// and dropped rifle stand-ins are not drawn.
    /// </summary>
    public static class ChainSickleStakeGraphics
    {
        private static readonly Vector3[] PathPts = new Vector3[25], CoilPts = new Vector3[37], TetherPts = new Vector3[7];

        /// <summary>
        /// The preview. <paramref name="centre"/> is halfway between the holder and the pinned pawn, as
        /// in the lab's sketch; <paramref name="target"/> indexes ChainSickleRule.ScriptTargets.
        /// <paramref name="refused"/>: the rule does not allow Stake (too heavy), so nothing past the
        /// snagged pose is drawn.
        /// </summary>
        public static void DrawPreview(Vector3 centre, float aimDegrees, int target, bool runs, bool refused, float seconds, Map map)
        {
            var c = new Vector2(centre.x, centre.z);
            Vector2 toward = Turn(aimDegrees);
            ChainSickleWeight rule = ChainSickleRule.Script(target);
            float s = refused ? Mathf.Min(seconds, T.Yank0 - 0.001f) : seconds;
            float step = refused ? 0f : T.ScriptStep(s, T.ScriptSwingAt);
            float d = T.ScriptDistance;
            var shot = new ChainStakeShot
            {
                Caster = c + toward * Mathf.Lerp(-d / 2f, d / 2f - 1f, step), Target = c + toward * (d / 2f), Aim = aimDegrees,
                Strain = runs && !refused ? T.ScriptStrain(s) : 0f, Slack = step, Size = ChainSickleRule.ScriptTargets[target].Size,
                Pin = rule.Pin, SwingAt = refused ? -1f : T.ScriptSwingAt,
            };
            Draw(shot, s, map);
        }

        /// <param name="weapon">Draw the sickle in the hand. In game it is left off once the holder's cast job is over.</param>
        public static void Draw(in ChainStakeShot shot, float s, Map map, bool weapon = true)
        {
            if (s < 0f || !Shown(shot.Target, map)) return;
            Begin(shot.Target);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            var f = new ChainSickleFrame(shot.Aim, sun);
            Vector2 o = shot.Target;
            float big = Mathf.Sqrt(Mathf.Max(0.1f, shot.Size)), pin = shot.Pin > 0f ? shot.Pin : T.NoPin;
            Vector2 hand = f.Ground(shot.Caster, 0.20f, 0.18f);
            Vector2 body = shot.BodyAt ?? f.Ground(o, shot.Strain, 0f);

            // The stake point: beside the far foot, in the floor.
            Vector2 stakeAt = f.Ground(o, 0.22f * big, -0.30f * big);
            float yankU = s < T.Yank0 ? 0f : Clamp01((s - T.Yank0) / T.Yank);
            float tightU = s < T.Yank0 ? 0f : Smooth((s - T.Yank0) / T.Tighten);
            float stakedU = s < T.Staked ? 0f : 1f;

            // Floor first: the crack, the pin ring counting down, the blood drops.
            float stakeAge = s - T.Staked;
            Crack(stakeAt, stakeAge < 0f ? 0f : Smooth(stakeAge / 0.12f), 3);
            if (stakeAge >= 0f)
            {
                float left = 1f - Clamp01(stakeAge / pin);
                Circle(o, 0.55f * big, 0.3f, Floor + 0.01f, Cream);
                Circle(o, 0.55f * big * left, 0.55f, Floor + 0.012f, Cream);
            }
            float cutAge = shot.SwingAt >= 0f ? s - T.Cut(shot.SwingAt) : -1f;
            if (cutAge >= 0f)
            {
                for (int i = 0; i < 5; i++)
                {
                    float u = Clamp01(cutAge / (0.25f + Rand(i + 70) * 0.15f)), a = (Rand(i + 80) - 0.5f) * 2.4f + f.radians + Mathf.PI / 2f;
                    float dd = (0.25f + Rand(i + 90) * 0.5f) * big * EaseOut(u), h = Mathf.Max(0f, ChestH * big * (1f - u) * (1f - u) + 0.3f * u * (1f - u));
                    var q = new Vector2(body.x + Mathf.Cos(a) * dd, body.y + Mathf.Sin(a) * dd);
                    if (u >= 1f) Sprite(q, 0.12f + Rand(i) * 0.08f, 0.09f + Rand(i + 5) * 0.05f, Fade(Blood, 0.8f), soft, Floor + 0.02f + i * 0.0001f);
                    else Sprite(new Vector2(q.x, q.y + h * Lift), 0.08f, 0.1f, Fade(Blood, 0.95f), soft, Y + 0.06f + i * 0.0001f);
                }
            }

            // The coil: stays at the chest and tightens on the arms.
            float cr = Mathf.Lerp(CoilR, 0.21f, tightU) * big;
            int coil = CoilPath(CoilPts, body, CoilTurns, cr, (ChestH + 0.1f) * big, 0.25f * big, shot.Aim + 180f);
            Vector3 coilEnd = CoilPts[coil - 1];

            // The weight: rides the coil's end, then drops to the stake point during the yank.
            Vector3 w;
            if (yankU <= 0f) w = coilEnd;
            else if (yankU < 1f) w = new Vector3(Mathf.Lerp(coilEnd.x, stakeAt.x, yankU), Mathf.Lerp(coilEnd.y, 0f, yankU * yankU), Mathf.Lerp(coilEnd.z, stakeAt.y, yankU));
            else w = At(stakeAt, 0f);

            // The chain from hand to coil: taut while the holder stands back, slack once next to the
            // pawn, snapped straight while the pawn strains. A short tether coil -> stake is always taut.
            float slack = shot.Slack * 0.3f + (s < T.Yank0 ? 0.03f : 0.1f * (1f - shot.Slack)) - (shot.Strain > 0f ? 0.06f : 0f);
            int path = ChainPath(PathPts, At(hand, HandH), CoilPts[0], Mathf.Max(0.01f, slack), 24, shot.Strain > 0f ? 0.01f : 0f, s);
            int tether = yankU > 0f ? ChainPath(TetherPts, coilEnd, w, 0.01f, 6) : 0;

            if (shot.Strain > 0.05f) Kick(body, s, 0.6f, 11);
            if (stakeAge >= 0f && stakeAge < 0.5f)
            {
                // Dust from the stake hitting the floor.
                for (int i = 0; i < 8; i++)
                {
                    float u = Clamp01(stakeAge / (0.3f + Rand(i + 50) * 0.2f));
                    if (u >= 1f) continue;
                    float a = Rand(i + 60) * Mathf.PI * 2f, dd = 0.15f + u * 0.5f, lift = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI));
                    Sprite(new Vector2(stakeAt.x + Mathf.Cos(a) * dd, stakeAt.y + Mathf.Sin(a) * dd * 0.7f + lift * 0.2f * Lift),
                        0.2f + u * 0.25f, 0.16f + u * 0.2f, Fade(Dust, lift * 0.6f), PowerPoleGraphics.puff, Y + 0.01f + i * 0.0001f);
                }
            }

            // The sickle: held along the aim, then (the preview's cut) swung 120 degrees across the
            // pawn about the hand, leaving a pale arc at hand height.
            float deg = shot.Aim - 10f;
            float swingAge = shot.SwingAt >= 0f ? s - shot.SwingAt : -1f;
            if (swingAge >= 0f)
            {
                float u = Clamp01(swingAge / T.Swing);
                deg = shot.Aim - 70f + 120f * EaseOut(u);
                if (swingAge < T.Swing + 0.2f)
                {
                    float fade = 1f - Clamp01((swingAge - T.Swing) / 0.2f), a0 = (shot.Aim - 70f) * Mathf.Deg2Rad, a1 = deg * Mathf.Deg2Rad, hz = HandH * Lift;
                    const int n = 12;
                    Sides(n + 1, out Vector2[] inner, out Vector2[] outer);
                    for (int i = 0; i <= n; i++)
                    {
                        float a = Mathf.Lerp(a0, a1, i / (float)n);
                        inner[i] = new Vector2(hand.x + Mathf.Cos(a) * 0.45f, hand.y + hz + Mathf.Sin(a) * 0.45f);
                        outer[i] = new Vector2(hand.x + Mathf.Cos(a) * 0.72f, hand.y + hz + Mathf.Sin(a) * 0.72f);
                    }
                    Strip(inner, outer, Fade(Cream, 0.55f * fade), solid, Y + 0.045f);
                }
            }
            if (weapon) Sickle(hand, HandH, deg, sun, strength, Y + 0.05f);
            Chain(PathPts, path, sun, strength, Y + 0.02f);
            Coil(CoilPts, coil, body.y + 0.02f, sun, strength);
            if (tether > 0) Chain(TetherPts, tether, sun, strength, Y + 0.021f);
            Weight(w, sun, strength, Y + 0.03f, stakedU * Smooth(stakeAge / 0.1f));

            // The preview's 1.5x hit: a large flash across the chest and a red gash.
            if (cutAge >= 0f && cutAge < 0.25f)
            {
                float fl = 1f - cutAge / 0.25f;
                var chest = new Vector2(body.x, body.y + ChestH * big * Lift);
                Sprite(chest, 1.4f * fl + 0.3f, 1.0f * fl + 0.2f, Fade(Flash, fl * 0.9f), glow, Y + 0.08f);
                Sprite(chest, 0.5f, 0.12f, Fade(Blood, fl), soft, Y + 0.085f, shot.Aim + 40f);
            }
        }
    }
}
