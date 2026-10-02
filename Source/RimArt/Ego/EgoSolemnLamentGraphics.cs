using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using T = RimArt.EgoSolemnLamentTiming;

namespace RimArt
{
    /// <summary>
    /// One burst as the picture needs it. Points are DrawPos-style points on the map (cell centres in the
    /// preview); in game EgoSolemnLamentBurstCast fills it from the verb's shots.
    /// </summary>
    public struct EgoSolemnLamentBurst
    {
        /// <summary>The wielder's point now, and the aim in degrees (0 east, 90 north).</summary>
        public Vector2 Wielder;
        public float Aim;
        /// <summary>The target's point while it stands: the guns aim at it and the hits land on its chest.</summary>
        public Vector2 Target;
        /// <summary>The shots, the first <see cref="ShotCount"/> of <see cref="Shots"/>; each draws from its own fire and hit times.</summary>
        public EgoSolemnLamentShot[] Shots;
        public int ShotCount;
        /// <summary>The last hit: the guns lower 0.5 to 0.9 s after it.</summary>
        public float Last;
        /// <summary>When the target reached the cap (a white flash as the swarm covers it), or negative.</summary>
        public float DownAt;
    }

    /// <summary>One coffin (the corrosion action) as the picture needs it.</summary>
    public struct EgoSolemnLamentCoffin
    {
        /// <summary>
        /// The corroded wielder's point now, and the aim the hanging guns point along (degrees). The cloud circles
        /// it and the floor ring is drawn round it, wherever it walks.
        /// </summary>
        public Vector2 Wielder;
        public float Aim;
        /// <summary>Where the wielder stood when the coffin rose: the coffin stays there, its foot CoffinBackX/Z off it.</summary>
        public Vector2 Coffin;
        /// <summary>The cloud's radius in cells (balance, from the weapon's XML): the floor ring is drawn at it and the orbits scale with it.</summary>
        public float Radius;
        /// <summary>Seconds the cloud stays out after the opening: the rule's 30, overclock 5; the preview shows 4 (the funeral 23).</summary>
        public float Cloud;
        /// <summary>
        /// Seconds the cloud takes back into the coffin from where the wielder stopped when it ended
        /// (<see cref="EgoSolemnLamentTiming.FarFly"/>); never less than ReturnTime, 0.7. The lid closes after it.
        /// </summary>
        public float Home;
        /// <summary>Corroded: the Abnormality's face over the head. Overclock draws none.</summary>
        public bool Face;
        /// <summary>
        /// The cloud butterflies that left for a pawn: slot, time and how long that slot's new one then flies from
        /// the coffin to the cloud (<see cref="EgoSolemnLamentTiming.FarFly"/> with the wielder's distance from the
        /// coffin at the dive), in the order they left.
        /// </summary>
        public List<int> DiveSlots;
        public List<float> DiveTimes, DiveFlys;
    }

    /// <summary>
    /// Draws Solemn Lament (E.G.O. weapon 2), the port of the lab's ego-solemn-lament.js. Monochrome, as the
    /// source: white and pale for The Living (the white gun), ink and soot for The Departed (the black gun).
    ///
    /// The burst: the white and the black pistol drawn up from the hips, kicking 28 degrees with each shot
    /// and lowered after the last; per shot the muzzle (white: a crescent, a ragged white blast and spikes;
    /// black: an ink splat with a white glow behind it, a torn ink smear, drips and slivers), the line (white:
    /// a smoke band that thins and hangs with dashes and sparks; black: a dark line) and the hit (white: a white
    /// splat with grey cracks breaking into see-through shards; black: an ink splat with red inside, breaking
    /// into chunks, blood and a floor spatter that stays). The butterflies, pips and the cap's swarm are
    /// <see cref="EgoSolemnLamentButterflies"/>; a white flash covers the body as the swarm lands.
    ///
    /// The coffin: the coffin rising behind the wielder with dust at its foot, opening with a flash, rays, beams
    /// and smoke (<see cref="EgoSolemnLamentCoffinGraphics"/>), and staying there; round the wielder wherever it
    /// walks, the floor ring at the true radius, the guns hanging down, the face over the head, and the cloud
    /// circling and diving; new butterflies fly out of the coffin to the cloud, and at the end it flies back in.
    ///
    /// Level shapes at chest height and level circles, so they turn with the aim; the guns are side views laid
    /// flat (mirrored aiming west, under the pawn aiming north) and the coffin faces the viewer at every aim.
    ///
    /// Not drawn (the sketch's stand-ins): the pawns, their shadows, their sway, flinch and fall (the preview's
    /// script moves the butterflies with them), and the lab aids.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class EgoSolemnLamentGraphics
    {
        internal static readonly Color White = new Color(1f, 1f, 1f), Pale = new Color(0.90f, 0.90f, 0.93f), Ash = new Color(0.50f, 0.50f, 0.55f);
        internal static readonly Color Ink = new Color(0.05f, 0.05f, 0.06f), Soot = new Color(0.16f, 0.16f, 0.18f), Smoke = new Color(0.78f, 0.78f, 0.82f);
        internal static readonly Color Red = new Color(0.9f, 0.15f, 0.12f), CoffinEdge = new Color(0.86f, 0.86f, 0.90f), CoffinTop = new Color(0.24f, 0.24f, 0.27f);
        /// <summary>lib/chain-sickle.js's blood and dust.</summary>
        internal static readonly Color Blood = ChainSickleGraphics.Blood, Dust = ChainSickleGraphics.Dust;
        internal static readonly Material Puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);
        internal static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor(), ShadowLayer = AltitudeLayer.Shadows.AltitudeFor();
        internal const float Lift = SixPathsHeight.Lift;
        /// <summary>Each shot's pieces are raised k x ShotStep so two shots' pieces at one altitude still draw in order, the later on top.</summary>
        internal const float ShotStep = 0.00001f;
        /// <summary>The cloud's butterflies sit this far over the marked pawns' at the same index, as the sketch draws them after.</summary>
        private const float CloudShift = 0.00005f;

        /// <summary>
        /// The unit direction of <paramref name="degrees"/>, worked out in double as the sketch does: single
        /// precision gives cos 90 = -4.4e-8, which flips a sign test (which way the target falls, which side a
        /// gun's grip hangs) that the sketch's cos 90 = +6.1e-17 passes.
        /// </summary>
        internal static Vector2 Dir(float degrees)
        {
            double r = degrees * System.Math.PI / 180.0;
            return new Vector2((float)System.Math.Cos(r), (float)System.Math.Sin(r));
        }
        internal static Vector2 Left(Vector2 d) => new Vector2(-d.y, d.x);

        /// <summary>The guns, each shot's muzzle, line and hit, and the flash at the cap. <paramref name="s"/> is the burst's clock, 0 as the guns start up.</summary>
        public static void DrawBurst(in EgoSolemnLamentBurst b, float s, Map map)
        {
            if (s < 0f || !Shown(b.Wielder, map)) return;
            Begin(b.Wielder);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            Vector2 d = Dir(b.Aim), across = Left(d), chest = new Vector2(b.Target.x, b.Target.y + PawnBody.Chest);
            float raised = Smooth(s / (T.Lead * 0.8f)), lower = Smooth((s - b.Last - 0.5f) / 0.4f);
            float reach = 0.04f + (T.HandReach - 0.04f) * raised * (1f - 0.75f * lower);
            Vector2 forward = b.Wielder + d * reach;
            // Right hand white, left hand black. Aiming north the guns are held in front of the body, away from the viewer.
            Vector2 whiteHand = forward - across * T.HandAcross, blackHand = forward + across * T.HandAcross;
            float gunLayer = d.y > 0.35f ? PawnLayer - 0.012f : PawnLayer + 0.06f;
            Vector2 whiteAim = (b.Target - whiteHand).normalized, blackAim = (b.Target - blackHand).normalized;
            Gun(b, s, true, whiteHand, whiteAim, raised, lower, gunLayer, sun, strength);
            Gun(b, s, false, blackHand, blackAim, raised, lower, gunLayer + 0.004f, sun, strength);

            for (int n = 0; n < b.ShotCount; n++)
            {
                EgoSolemnLamentShot sh = b.Shots[n];
                float age = s - sh.Fire, shift = sh.K * ShotStep;
                if (age < 0f) continue;
                Vector2 hand = sh.White ? whiteHand : blackHand, aim = sh.White ? whiteAim : blackAim, muzzle = T.MuzzleAt(hand, aim);
                if (sh.White)
                {
                    EgoSolemnLamentShotGraphics.WhiteMuzzle(muzzle, aim, hand, -1f, age, sh.K, shift);
                    EgoSolemnLamentShotGraphics.WhiteTrail(muzzle, chest, age, sh.K, shift);
                    EgoSolemnLamentHitGraphics.WhiteHit(chest, s - sh.Hit, sh.K, shift);
                }
                else
                {
                    EgoSolemnLamentShotGraphics.BlackMuzzle(muzzle, aim, age, sh.K, shift);
                    EgoSolemnLamentShotGraphics.BlackTrail(muzzle, chest, age, shift);
                    EgoSolemnLamentHitGraphics.BlackHit(chest, b.Target, d, s - sh.Hit, sh.K, shift);
                }
            }
            if (b.DownAt >= 0f)
            {
                float a = s - b.DownAt - T.SwarmIn * 0.8f;
                if (a >= 0f && a < 0.25f) Sprite(chest, 1.1f, 1.2f, Fade(White, 0.45f * (1f - a / 0.25f)), glow, Overhead + 0.13f);
            }
        }

        /// <summary>One gun: tipped down 50 degrees until drawn, 40 more as it lowers, kicked up after its last shot and slid back 0.05 cells over 0.12 s.</summary>
        private static void Gun(in EgoSolemnLamentBurst b, float s, bool white, Vector2 hand, Vector2 aim, float raised, float lower, float layer,
            Vector2 sun, float strength)
        {
            float age = -1f;
            for (int n = 0; n < b.ShotCount; n++)
                if (b.Shots[n].White == white && b.Shots[n].Fire <= s) age = s - b.Shots[n].Fire;
            float tilt = (T.KickTilt * T.KickAt(age) - (1f - raised) * 50f - lower * 40f) * Mathf.Deg2Rad;
            float slide = age >= 0f ? T.KickSlide * T.Bump(Mathf.Min(1f, age / 0.12f)) : 0f;
            EgoSolemnLamentShotGraphics.Pistol(hand, aim, white, tilt, slide, layer, sun, strength);
        }

        /// <summary>
        /// The coffin and its cloud, the hanging guns and the face. <paramref name="s"/> is the clock from the
        /// coffin starting to rise. The marked pawns are <see cref="EgoSolemnLamentButterflies.DrawMark"/>.
        /// </summary>
        public static void DrawCoffin(in EgoSolemnLamentCoffin c, float s, Map map)
        {
            if (s < 0f || !Shown(c.Wielder, map)) return;
            Begin(c.Wielder);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            Vector2 o = c.Wielder;
            float home = Mathf.Max(T.ReturnTime, c.Home), closeAt = T.CloseAt(c.Cloud, home), sinkAt = T.SinkAt(c.Cloud, home);

            // The floor: a pale ring at the true radius and a dim floor inside it while the coffin is open.
            float ringA = Smooth((s - T.Open) / 0.3f) * (1f - Smooth((s - closeAt) / 0.3f));
            if (ringA > 0f)
            {
                Sprite(o, c.Radius * 2.3f, c.Radius * 2.3f, Fade(Ink, 0.16f * ringA), soft, Floor + 0.02f);
                Circle(o, c.Radius * (0.9f + 0.1f * ringA), 0.75f * ringA, Floor + 0.03f, Pale);
                Circle(o, c.Radius * 0.985f, 0.3f * ringA, Floor + 0.0301f, Pale);
            }

            // The coffin where the wielder stood when it rose.
            var foot = new Vector2(c.Coffin.x + T.CoffinBackX, c.Coffin.y + T.CoffinBackZ);
            float riseU = Mathf.Clamp01(s / T.Rise), sinkU = Mathf.Clamp01((s - sinkAt) / T.Sink);
            float rise = T.CoffinRise(s, sinkAt);
            float lid = Smooth((s - T.Rise) / T.LidOpen) * (1f - Smooth((s - closeAt) / T.LidClose));
            EgoSolemnLamentPoint mouth = EgoSolemnLamentCoffinGraphics.Coffin(foot, rise, lid, sun, strength);
            EgoSolemnLamentCoffinGraphics.FootDust(foot, riseU < 1f ? riseU : sinkU);
            EgoSolemnLamentCoffinGraphics.Opening(foot, mouth, s - T.Open);

            // The guns hang down, tipped 55 degrees, 0.2 either side.
            Vector2 d = Dir(c.Aim), across = Left(d);
            for (int i = 0; i < 2; i++)
            {
                Vector2 hand = o + d * 0.04f + across * (i == 1 ? 0.2f : -0.2f);
                EgoSolemnLamentShotGraphics.Pistol(hand, d, i == 0, -55f * Mathf.Deg2Rad, 0f, PawnLayer + 0.06f + i * 0.004f, sun, strength);
            }
            if (c.Face)
                EgoSolemnLamentButterflies.Face(new Vector2(o.x, o.y + PawnBody.Head), s, Smooth(s / 0.4f) * (1f - Smooth((s - sinkAt) / T.Sink)));

            Cloud(c, s, o, home, mouth, sun, strength);
        }

        /// <summary>
        /// The cloud, circling the wielder at <paramref name="o"/>. On the flash 24 butterflies burst out within 0.12 s
        /// and fly to their orbits in 0.55 s, bowed out through the smoke; the other 12 follow 0.05 s apart and take
        /// 0.7 s. A slot that dove sends a new one out of the coffin 0.25 s later, which takes its dive's DiveFlys s.
        /// A flight out of the coffin aims at its orbit round where the wielder is now, so it bends after a walking
        /// wielder and lands where the orbit is. At the end all fly back into the coffin over <paramref name="home"/>
        /// s, shrinking.
        /// </summary>
        private static void Cloud(in EgoSolemnLamentCoffin c, float s, Vector2 o, float home, EgoSolemnLamentPoint mouth, Vector2 sun, float strength)
        {
            float cloudEnd = T.CloudEnd(c.Cloud);
            for (int i = 0; i < T.CloudN; i++)
            {
                float last = -1f, lastFly = T.RefillFly;
                for (int v = 0; v < c.DiveSlots.Count; v++)
                    if (c.DiveSlots[v] == i && c.DiveTimes[v] <= s)
                    {
                        last = c.DiveTimes[v];
                        lastFly = c.DiveFlys != null && v < c.DiveFlys.Count ? c.DiveFlys[v] : T.RefillFly;
                    }
                bool first = last < 0f;
                float rel = first ? T.CloudOut(i) : last + T.Refill, dur = first ? T.CloudFly(i) : lastFly, back = cloudEnd + 0.1f + Rand(i + 800) * 0.15f;
                if (s < rel || s >= back + home || rel >= back) continue;
                float size = T.Span, alpha = 1f, heading;
                EgoSolemnLamentPoint q;
                if (s >= back)
                {
                    float u = (s - back) / home;
                    EgoSolemnLamentPoint from = T.Orbit(i, back, o, c.Radius);
                    q = T.FlyAt(from.Ground, from.Height, Vector2.zero, 0.3f, i, u, mouth.Ground, mouth.Height);
                    heading = Heading(q, T.FlyAt(from.Ground, from.Height, Vector2.zero, 0.3f, i, Mathf.Min(1f, u + 0.03f), mouth.Ground, mouth.Height));
                    size *= 1f - 0.5f * u;
                    alpha = 1f - Smooth((u - 0.7f) / 0.3f);
                }
                else if (s < rel + dur)
                {
                    float u = (s - rel) / dur, wide = first && i < T.CloudBurst ? 1.1f : 0.4f;
                    EgoSolemnLamentPoint to = T.Orbit(i, rel + dur, o, c.Radius);
                    Vector2 way = to.Ground - mouth.Ground;
                    float len = way.magnitude;
                    way = len > 0f ? way / len : Vector2.zero;
                    var via = new Vector2(way.x * wide + (Rand(i + 810) - 0.5f) * 0.8f, way.y * wide + (Rand(i + 820) - 0.5f) * 0.8f);
                    q = T.FlyAt(mouth.Ground, mouth.Height, via, 0.6f, i, u, to.Ground, to.Height);
                    heading = Heading(q, T.FlyAt(mouth.Ground, mouth.Height, via, 0.6f, i, Mathf.Min(1f, u + 0.03f), to.Ground, to.Height));
                    size *= 0.5f + 0.5f * Mathf.Clamp01(u * 2f);
                }
                else
                {
                    q = T.Orbit(i, s, o, c.Radius);
                    heading = q.Heading;
                }
                EgoSolemnLamentButterflies.Shadow(q.Ground, Mathf.Max(0f, q.Height), size, sun, strength, alpha);
                EgoSolemnLamentButterflies.Butterfly(q.Screen, size, heading, T.FlapAt(s, 3f, 0.15f, i), i % 2 == 1, alpha,
                    Overhead + 0.14f + i * 0.0008f + CloudShift);
            }
        }

        /// <summary>The heading in degrees from one drawn point of a flight to the next.</summary>
        internal static float Heading(EgoSolemnLamentPoint a, EgoSolemnLamentPoint b)
        {
            Vector2 p = a.Screen, q = b.Screen;
            return Mathf.Atan2(q.y - p.y, q.x - p.x) * Mathf.Rad2Deg;
        }
    }
}
