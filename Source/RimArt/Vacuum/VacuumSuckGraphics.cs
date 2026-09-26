using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.VacuumGraphics;
using T = RimArt.VacuumSuckTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Suck: the canister rises beside the caster with a dust ring, the wand lifts and points,
    /// the true radius shows on the floor with a faint cone and air streaks from it to the head; each
    /// thing lifts off in turn, flies into the head and runs down the hose as a bulge, and the canister
    /// swells a step and blinks as it lands; filth streams in as flecks. A disarmed pawn gets circling
    /// marks. Then the canister sinks. The caster and pawn stand-ins and the hand of the sketch are not
    /// drawn.
    /// </summary>
    public static class VacuumSuckGraphics
    {
        private static readonly float[] Bulges = new float[64];
        private static readonly Vector2[] Streak = new Vector2[4];

        /// <summary>The preview. <paramref name="centre"/> is halfway between the caster and the target, as in the lab's sketch.</summary>
        public static void DrawPreview(Vector3 centre, float aimDegrees, bool armed, float seconds, Map map) =>
            Draw(T.Script(new Vector2(centre.x, centre.z), aimDegrees, armed), seconds, map);

        /// <param name="weapon">Draw the weapon. In game it is left off once the caster's job is over.</param>
        public static void Draw(VacuumSuckShot shot, float s, Map map, bool weapon = true)
        {
            int count = shot.Things.Count;
            float sink0 = T.Sink0(count, shot.Hold), result = T.Result(count), flight = T.Flight;
            if (s < 0f || s >= T.End(count, shot.Hold) || !Shown(shot.Caster, map)) return;
            Begin(shot.Caster);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            Vector2 run = shot.Target - shot.Caster, caster = shot.Caster, o = shot.Target;
            float aim = run.sqrMagnitude > 1e-6f ? Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg : 0f;
            var f = new VacuumFrame(aim, sun);

            // How far the suction is on: rises in the wind-up, holds through the pull, fades in the result.
            float suck = Smooth((s - T.Rise0) / T.Windup) * (1f - Smooth((s - result) / 0.35f));
            // The canister's size is its fullness in kg: what was inside plus each thing as it lands.
            float kg = shot.Inside, pulse = 0f, blink = 0f;
            int bulges = 0;
            for (int i = 0; i < count; i++)
            {
                if (shot.Things[i].Gone) continue;
                float land = T.Land(i, count), arrive = T.Arrive(i, count);
                if (s >= land) kg += shot.Things[i].Kg;
                pulse += Bump((s - land) / 0.25f);
                blink = Mathf.Max(blink, Bump((s - land) / T.Blink));
                float u = (s - arrive) / Bulge;
                if (u > 0f && u < 1f && bulges < Bulges.Length) Bulges[bulges++] = u;
            }
            float present = s < T.Rise0 ? 0f : s < sink0 ? Smooth((s - T.Rise0) / Rise) : 1f - Smooth((s - sink0) / Sink);
            float churn = Mathf.Max(Bump((s - T.Rise0) / Rise), Bump((s - sink0) / Sink));
            var pose = new VacuumPose
            {
                Present = present, Churn = churn, Swell = SwellFor(kg), Pulse = pulse, Blink = blink,
                MouthOpen = 0.18f + 0.82f * suck * (1f - 0.6f * blink), Bulges = Bulges, BulgeCount = bulges, Slack = shot.Slack,
            };
            Vector2 tipG = f.Place(caster, NozzleTip, 0f), tipS = f.Place(caster, NozzleTip, 0f, HandH);
            if (weapon)
            {
                VacuumParts v = Weapon(f, caster, pose, strength);
                tipG = v.TipG;
                tipS = v.TipS;
            }

            // The target area: the true radius on the floor, a faint cone from the head, air streaks.
            Circle(o, shot.Radius, 0.55f * suck, Floor, Pale);
            if (suck > 0.01f)
            {
                float ang = Mathf.Atan2(o.y - tipS.y, o.x - tipS.x), nx = -Mathf.Sin(ang), nz = Mathf.Cos(ang);
                var n = new Vector2(nx, nz);
                Quad(tipS - n * 0.1f, o - n * shot.Radius, tipS + n * 0.1f, o + n * shot.Radius, Fade(Pale, 0.07f * suck), Y - 0.06f);
                for (int i = 0; i < 18; i++)
                {
                    float th = Rand(i) * Mathf.PI * 2f, d = Mathf.Sqrt(Rand(i + 30)) * shot.Radius;
                    var g0 = new Vector2(o.x + Mathf.Cos(th) * d, o.y + Mathf.Sin(th) * d);
                    // JavaScript's % keeps the sign: before the pull u is negative and the streak's alpha is too, so it is skipped.
                    float u = ((s - T.Pull0) * (1.3f + Rand(i + 90) * 0.6f) + Rand(i + 60)) % 1f;
                    float alpha = 0.45f * suck * Mathf.Sin(u * Mathf.PI);
                    if (alpha <= 0.001f) continue;
                    for (int k = 0; k < 4; k++)
                    {
                        float vv = Mathf.Clamp01(u + (k == 0 ? -0.07f : k == 1 ? -0.035f : k == 2 ? 0f : 0.02f));
                        float h = vv * HandH + Mathf.Sin(vv * Mathf.PI) * 0.12f;
                        Streak[k] = new Vector2(g0.x + (tipG.x - g0.x) * vv, g0.y + (tipG.y - g0.y) * vv + h * SixPathsHeight.Lift);
                    }
                    Trail(Streak, 4, 0.06f, Fade(Pale, alpha), Y - 0.05f + i * 0.0001f);
                }
            }

            // The things in the radius, each at rest, in flight, or gone.
            for (int i = 0; i < count; i++)
            {
                VacuumThing thing = shot.Things[i];
                if (thing.Gone) continue;
                float start = T.Start(i, count), arrive = T.Arrive(i, count);
                Vector2 g0 = thing.Ground;
                if (thing.Kind == VacuumThingKind.Filth)
                {
                    float drained = Mathf.Clamp01((s - start) / flight);
                    if (shot.Props)
                    {
                        Sprite(g0, 1.0f * (1f - 0.4f * drained), 0.75f * (1f - 0.4f * drained), Fade(Blood, 0.85f * (1f - drained)), soft, Floor + 0.02f + i * 0.0001f);
                        Sprite(new Vector2(g0.x + 0.25f, g0.y + 0.2f), 0.4f, 0.3f, Fade(Blood, 0.7f * (1f - drained)), soft, Floor + 0.02f + i * 0.0001f + 0.00005f);
                    }
                    for (int k = 0; k < 8; k++)
                    {
                        float vv = Mathf.Clamp01((s - start - k * 0.045f) / (flight * 0.8f));
                        if (vv <= 0f || vv >= 1f) continue;
                        float e = Mathf.Pow(vv, 1.7f), jx = (Rand(k + 200) - 0.5f) * 0.5f, jz = (Rand(k + 210) - 0.5f) * 0.4f;
                        float h = e * HandH + Mathf.Sin(vv * Mathf.PI) * 0.25f;
                        var q = new Vector2(g0.x + jx + (tipG.x - g0.x - jx) * e, g0.y + jz + (tipG.y - g0.y - jz) * e + h * SixPathsHeight.Lift);
                        Disc(q, Y - 0.04f + (i * 8 + k) * 0.00002f, 0.07f * (1f - 0.5f * vv), 0.09f * (1f - 0.5f * vv), Fade(thing.Colour, 1f));
                    }
                    continue;
                }
                if (s >= arrive) continue;
                float layer;
                Vector2 g;
                float hNow, scale, fu;
                if (s < start)
                {
                    // At rest: only the preview draws it; in game the real thing is still where it lies.
                    if (!shot.Props) continue;
                    g = g0; hNow = thing.H; scale = 1f; fu = 0f;
                    layer = PawnLayer - 0.004f;
                }
                else
                {
                    fu = (s - start) / flight;
                    float e = Mathf.Pow(Mathf.Clamp01(fu), 1.7f);
                    g = new Vector2(g0.x + (tipG.x - g0.x) * e, g0.y + (tipG.y - g0.y) * e);
                    hNow = thing.H + (HandH - thing.H) * e + Mathf.Max(0f, Mathf.Sin(fu * Mathf.PI)) * 0.3f;
                    scale = 1f - 0.8f * Mathf.Clamp01((fu - 0.55f) / 0.45f);
                    layer = Y - 0.03f;
                }
                VacuumGraphics.Thing(thing, g, hNow, scale, 25f + fu * 520f, layer + i * 0.00002f, sun, strength);
            }

            // The pawn whose weapon was taken keeps standing; it is marked for a moment.
            if (shot.Disarmed >= 0 && shot.Disarmed < count && !shot.Things[shot.Disarmed].Gone)
            {
                Vector2 at = shot.DazedAt ?? shot.Things[shot.Disarmed].Ground - new Vector2(0.25f, 0.02f);
                Daze(at, s, s - T.Start(shot.Disarmed, count), T.Marked);
            }

            // A little dust lifts off the floor inside the radius while the pull is on.
            if (s >= T.Pull0 && s < result)
                for (int i = 0; i < 6; i++)
                {
                    float life = 0.5f + Rand(i + 300) * 0.3f, u = ((s - T.Pull0) / life + Rand(i + 310)) % 1f;
                    float th = Rand(i + 320) * Mathf.PI * 2f, d = Rand(i + 330) * shot.Radius * 0.9f;
                    var g = new Vector2(o.x + Mathf.Cos(th) * d, o.y + Mathf.Sin(th) * d);
                    Sprite(new Vector2(g.x + (tipG.x - g.x) * u * 0.25f, g.y + (tipG.y - g.y) * u * 0.25f + u * 0.35f * SixPathsHeight.Lift),
                        0.3f + u * 0.3f, 0.25f + u * 0.25f, Fade(Dust, Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)) * 0.45f * suck), PowerPoleGraphics.puff, Y - 0.045f + i * 0.0001f);
                }
        }
    }
}
