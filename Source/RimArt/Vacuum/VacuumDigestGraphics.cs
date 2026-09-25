using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.VacuumGraphics;
using T = RimArt.VacuumDigestTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Digest: the canister comes up fat with what it holds, chomps four times a second with a
    /// squeeze on each bite while lumps shift on its top and crumbs fly from its mouth and stay on the
    /// floor, slimming as it goes; then a green burp, a squash, and it sinks. The caster stand-in is
    /// not drawn.
    /// </summary>
    public static class VacuumDigestGraphics
    {
        private static readonly Color Burped = new Color(0.55f, 0.72f, 0.40f), Crumb = new Color(0.16f, 0.13f, 0.10f);
        private static readonly float[] NoBulges = new float[0];

        /// <summary>The preview. <paramref name="centre"/> is where the caster stands, as in the lab's sketch.</summary>
        public static void DrawPreview(Vector3 centre, float aimDegrees, float seconds, Map map) =>
            Draw(T.Script(new Vector2(centre.x, centre.z), aimDegrees), seconds, map);

        /// <param name="weapon">Draw the weapon. In game it is left off once the caster's job is over.</param>
        public static void Draw(VacuumDigestShot shot, float s, Map map, bool weapon = true)
        {
            if (s < 0f || s >= T.End(shot) || !Shown(shot.Caster, map)) return;
            Begin(shot.Caster);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            var f = new VacuumFrame(shot.Aim, sun);
            Vector2 caster = shot.Caster;
            float chew0 = T.Chew0, done = T.Done(shot), sink0 = T.Sink0(shot), stop = Mathf.Min(done, shot.StopAt);
            bool burps = !T.Stopped(shot);

            float present = s < T.Rise0 ? 0f : s < sink0 ? Smooth((s - T.Rise0) / Rise) : 1f - Smooth((s - sink0) / Sink);
            float churn = Mathf.Max(Bump((s - T.Rise0) / Rise), Bump((s - sink0) / Sink));
            float digested = T.Digested(shot, s);
            bool chewing = s >= chew0 && s < stop;
            float chomp = chewing ? (Mathf.Sin((s - chew0) * T.ChompHz * Mathf.PI * 2f) + 1f) / 2f : 0f;   // 1 = mouth wide, 0 = shut
            float bite = chewing ? Mathf.Pow(Mathf.Clamp01(1f - chomp), 3f) : 0f;                         // sharp at the shut moment
            float burpAge = burps ? s - done : -1f;
            float pulse = bite * 0.35f - Bump(burpAge / T.Burp) * 0.5f;
            float blink = chewing ? (Mathf.FloorToInt((s - chew0) * T.ChompHz) % 4 == 3 ? bite : 0f) : Bump(burpAge / (T.Burp * 1.6f));
            float mouthOpen = chewing ? 0.15f + 0.85f * chomp
                : burpAge >= 0f && burpAge < T.Burp ? 0.9f
                : 0.18f + 0.5f * Smooth((s - T.Rise0 - Rise) / 0.2f) * (s < chew0 ? 1f : 0f);
            var pose = new VacuumPose
            {
                Present = present, Churn = churn, Swell = SwellFor(shot.Kg * (1f - digested)), Pulse = pulse, Blink = blink,
                MouthOpen = mouthOpen, Bulges = NoBulges, BulgeCount = 0, Slack = shot.Slack,
            };
            if (!weapon) return;
            VacuumParts v = Weapon(f, caster, pose, strength);
            Vector2 C = v.C;
            float R = v.R, H = v.H, top = v.Top, layer = v.Layer;

            // Lumps shifting inside: darker soft spots wandering over the top face while it chews,
            // fading out as the mass goes.
            if (present > 0.9f && s < done)
            {
                float alive = (1f - digested) * (chewing ? 1f : 0.6f);
                for (int i = 0; i < 5; i++)
                {
                    float th = Rand(i + 700) * Mathf.PI * 2f + (chewing ? (s - chew0) * (0.8f + Rand(i + 710)) * (i % 2 == 1 ? 1f : -1f) : 0f);
                    float d = R * (0.25f + Rand(i + 720) * 0.45f) * (1f - digested * 0.5f), size = R * (0.35f + Rand(i + 730) * 0.25f);
                    Sprite(new Vector2(C.x + Mathf.Cos(th) * d, top + Mathf.Sin(th) * d), size, size * 0.8f, Fade(CanDark, 0.55f * alive), soft, layer + 0.0055f + i * 0.00005f);
                }
            }

            // Crumbs: on each bite a few flecks leave the mouth and land on the floor south of the
            // canister. They stay where they land.
            var mouth = new Vector2(C.x, C.y - R + H * SixPathsHeight.Lift * 0.30f);   // the face's mouth, on the south side
            for (int i = 0; i < T.Crumbs; i++)
            {
                float born = chew0 + (i / (float)T.Crumbs) * shot.Chew + Rand(i + 800) * (1f / T.ChompHz);
                if (s < born || born >= stop) continue;
                float age = s - born, life = 0.35f + Rand(i + 810) * 0.15f, u = Mathf.Min(1f, age / life);
                float dx = (Rand(i + 820) - 0.5f) * 1.0f, dz = -(0.25f + Rand(i + 830) * 0.5f);
                float h = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)) * 0.3f * (1f - u * 0.3f);
                float size = 0.03f + Rand(i + 840) * 0.025f;
                Disc(new Vector2(mouth.x + dx * u, mouth.y + dz * u + h * SixPathsHeight.Lift), (u < 1f ? Y - 0.03f : Floor + 0.03f) + i * 0.00005f, size, size * 0.8f, Fade(Crumb, 0.9f));
            }

            // The burp: a green puff rolling out of the mouth, and a short pale flash inside it.
            if (burpAge >= 0f && burpAge < T.Burp * 2f)
            {
                float u = burpAge / (T.Burp * 2f);
                for (int i = 0; i < 6; i++)
                {
                    float d = u * (0.4f + Rand(i + 900) * 0.6f), spread = (Rand(i + 910) - 0.5f) * 0.8f * u;
                    Sprite(new Vector2(mouth.x + spread, mouth.y - d + u * 0.25f * SixPathsHeight.Lift), 0.3f + u * 0.6f, 0.25f + u * 0.5f,
                        Fade(Burped, (1f - u) * 0.55f), PowerPoleGraphics.puff, Y + 0.03f + i * 0.0001f);
                }
                Sprite(mouth, 0.6f, 0.3f, Fade(Pale, Mathf.Max(0f, 1f - burpAge / 0.1f) * 0.5f), glow, layer + 0.011f);
            }
        }
    }
}
