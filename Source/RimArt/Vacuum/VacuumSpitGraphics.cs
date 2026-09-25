using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.VacuumGraphics;
using T = RimArt.VacuumSpitTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Spit: the canister rises sized by what it holds, the thing runs up the hose as a bulge
    /// while the canister shrinks by its mass and blinks, leaves the head with a puff and a flash, and
    /// flies on an arc to the cell; the impact flashes, rings and throws dust, bigger when it hits a
    /// pawn, which gets daze marks. Then the canister sinks. The caster and pawn stand-ins are not
    /// drawn.
    /// </summary>
    public static class VacuumSpitGraphics
    {
        private static readonly float[] Bulges = new float[1];

        /// <summary>The preview. <paramref name="centre"/> is halfway between the caster and the target, as in the lab's sketch.</summary>
        public static void DrawPreview(Vector3 centre, float aimDegrees, bool chunk, float seconds, Map map) =>
            Draw(T.Script(new Vector2(centre.x, centre.z), aimDegrees, chunk), seconds, map);

        /// <param name="weapon">Draw the weapon. In game it is left off once the caster's job is over.</param>
        public static void Draw(VacuumSpitShot shot, float s, Map map, bool weapon = true)
        {
            float sink0 = T.Sink0(shot.Hold);
            if (s < 0f || s >= T.End(shot.Hold) || !Shown(shot.Caster, map)) return;
            Begin(shot.Caster);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            Vector2 run = shot.Target - shot.Caster, caster = shot.Caster, o = shot.Target;
            float aim = run.sqrMagnitude > 1e-6f ? Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg : 0f;
            var f = new VacuumFrame(aim, sun);

            float present = s < T.Rise0 ? 0f : s < sink0 ? Smooth((s - T.Rise0) / Rise) : 1f - Smooth((s - sink0) / Sink);
            float churn = Mathf.Max(Bump((s - T.Rise0) / Rise), Bump((s - sink0) / Sink));
            float heaveU = (s - T.Heave) / Bulge;
            // The canister's size is its fullness in kg; the thing's own mass leaves with it at the heave.
            float kg = shot.Inside - (s < T.Heave ? 0f : Mathf.Min(shot.Inside, shot.Thing.Kg));
            float pulse = Bump((s - T.Heave) / 0.25f) * 0.6f;
            float blink = Bump((s - T.Heave) / T.Blink);
            float open = Smooth((s - T.Rise0) / T.Windup);
            int bulges = 0;
            if (heaveU > 0f && heaveU < 1f) Bulges[bulges++] = 1f - heaveU;
            var pose = new VacuumPose
            {
                Present = present, Churn = churn, Swell = SwellFor(kg), Pulse = pulse, Blink = blink,
                MouthOpen = 0.18f + 0.82f * open * (1f - Smooth((s - T.Launch - 0.2f) / 0.4f)) * (1f + 0.3f * pulse),
                Bulges = Bulges, BulgeCount = bulges, Slack = shot.Slack,
            };
            Vector2 tipG = f.Place(caster, NozzleTip, 0f), tipS = f.Place(caster, NozzleTip, 0f, HandH);
            if (weapon)
            {
                VacuumParts v = Weapon(f, caster, pose, strength);
                tipG = v.TipG;
                tipS = v.TipS;
            }

            // The pawn on the cell: dazed after the hit.
            if (shot.HitPawn) Daze(shot.DazedAt ?? o, s, s - T.Impact, shot.Dazed);

            // The thing: in flight from the head to the cell, then (the preview only) resting on the ground.
            Vector2 rest = shot.Rest;
            if (s >= T.Launch && s < T.Impact)
            {
                float u = (s - T.Launch) / T.Flight;
                var g = new Vector2(tipG.x + (rest.x - tipG.x) * u, tipG.y + (rest.y - tipG.y) * u);
                float h = HandH * (1f - u) + Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)) * T.Arc;
                VacuumGraphics.Thing(shot.Thing, g, h, 0.35f + 0.65f * Smooth(u * 3f), 25f + u * 900f, Y - 0.03f, sun, strength);
            }
            else if (s >= T.Impact && shot.Props)
                VacuumGraphics.Thing(shot.Thing, rest, 0f, 1f, 130f, PawnLayer - 0.004f, sun, strength);

            // Launch: a puff at the head, a short pale flash, a few dust motes thrown forward.
            float launchAge = s - T.Launch;
            if (launchAge >= 0f && launchAge < 0.4f)
            {
                float ang = Mathf.Atan2(o.y - tipS.y, o.x - tipS.x), c = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                Sprite(tipS, 0.9f, 0.6f, Fade(Pale, Mathf.Max(0f, 1f - launchAge / 0.1f) * 0.7f), glow, Y + 0.022f);
                for (int i = 0; i < 7; i++)
                {
                    float u = launchAge / (0.25f + Rand(i + 500) * 0.15f);
                    if (u > 1f) continue;
                    float spread = (Rand(i + 510) - 0.5f) * 0.9f, far = u * (0.5f + Rand(i + 520) * 0.8f);
                    Sprite(new Vector2(tipS.x + c * far - sn * spread * u, tipS.y + sn * far + c * spread * u),
                        0.2f + u * 0.35f, 0.18f + u * 0.3f, Fade(Dust, Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)) * 0.55f), PowerPoleGraphics.puff, Y + 0.021f + i * 0.0001f);
                }
            }

            // Impact: flash, floor ring and dust on the cell. Bigger on a pawn, a small thud on the floor.
            float hitAge = s - T.Impact, big = shot.HitPawn ? 1f : 0.45f;
            if (hitAge >= 0f && hitAge < 0.5f)
            {
                Sprite(new Vector2(o.x, o.y + 0.3f * big), 1.6f * big, 1.1f * big, Fade(Pale, Mathf.Max(0f, 1f - hitAge / 0.12f) * 0.85f * big), glow, Y + 0.02f);
                Circle(o, 0.25f + hitAge * 2.2f * big, (1f - hitAge / 0.5f) * 0.6f, Floor, Pale);
                for (int i = 0; i < 10; i++)
                {
                    float u = hitAge / (0.3f + Rand(i + 60) * 0.2f);
                    if (u > 1f) continue;
                    float th = Rand(i + 70) * Mathf.PI * 2f, far = u * (0.4f + Rand(i + 80) * 1.0f) * big;
                    float lift = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI));
                    Sprite(new Vector2(o.x + Mathf.Cos(th) * far, o.y + Mathf.Sin(th) * far * 0.7f + lift * 0.35f * big * SixPathsHeight.Lift),
                        0.25f + u * 0.4f, 0.2f + u * 0.3f, Fade(Dust, lift * 0.6f), PowerPoleGraphics.puff, Y + 0.012f + i * 0.0001f);
                }
            }
        }
    }
}
