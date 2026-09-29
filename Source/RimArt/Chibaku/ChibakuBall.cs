using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Chibaku Tensei's ball, drawn from a <see cref="ChibakuGround"/> capture (the picture; the rules are
    /// <see cref="ChibakuPull"/>; ported from Tools/VfxLab/web/sketches/pain-chibaku-tensei-v2.js with its defaults, which
    /// follow the anime's Konoha scene, Storm Connections and Naruto Mobile).
    ///
    /// Order: the core arrives 5 cells over the cell as a black sun (a black disc with a white rim in a yellow-white
    /// glare, 14 thin rays) with a 0.15 s flash; a faint warm light lies on the ground and a dark edge spreads to the
    /// true radius. For 3 s cracks run out, plates heave, tear free (inner first) and fly into the core, turning dark
    /// against its light once in the air with a pale-yellow edge on the core side; tan dust rises from the torn plates,
    /// a haze hangs under the ball and small rocks spiral up into it. The ball grows from what arrives (radius 0.3 to 1
    /// times 2 cells, by the cube root of the share arrived); the glare dims once about half the plates have arrived.
    /// It holds (the ability's holdSeconds, cut short by <see cref="BreakAt"/>), turning 14 degrees a second and
    /// squeezing once a second: earth and stone with 24 boulders set in its surface, warm light in the gaps that pulses
    /// with each squeeze, a soft light on its lower edge, a ring of cloud clumps turning round its middle at 25 degrees
    /// a second, a rock falling off its underside about every 0.45 s. Over the last 1.2 s of the hold dark hairline
    /// cracks run out from three break points; in the 0.4 s crack phase they widen and fill with warm light, the plates
    /// pull apart so light shows along every seam, the ball shakes, dust spurts out and chips fly; it bursts in a warm
    /// flash into rocks that land and stay with the crater, and a ring of dust rolls out along the ground.
    ///
    /// Drawing: the ball is a sphere under the kit's height rule (screen z = z + 0.6 h), so its outline is an
    /// ellipse 1.166 times taller than wide. Its surface is up to 84 plates on a Fibonacci sphere, rebuilt each frame on
    /// the side facing the camera, lit by a fixed sun, over a dark body with a darker limb. About 15 % of them are
    /// patches of the captured ground (taken round the centre of the ground plate that filled it); the rest came in
    /// underside out and are earth or stone. The light between the plates is an additive glow over the body and under
    /// the plates, so it only shows in the gaps. Flying plates are the ground plates' own meshes under one matrix
    /// (spin, a squash along their axis to turn over, shrink); upside down they show earth or stone. Things in the air
    /// are split into behind and in front of the ball by their depth along the view direction.
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class ChibakuBall : IDisposable
    {
        public const float Radius = 2f, DefaultHeight = 5f, PullSeconds = 3f, Gravity = 12f;
        public const float Pulse = .25f, CrackRun = .45f, CrackTime = .4f, Tail = 3f;
        public const float Pull = Pulse, Formed = Pull + PullSeconds;
        /// <summary>The core's radius over the ball, and how far it climbs over its place before it comes down onto it.</summary>
        public const float CoreR = .34f, CoreClimb = 2.5f;
        private const float Spin = 14f * Mathf.Deg2Rad, HeaveH = .18f, PatchR = .45f;
        private const float Lift = SixPathsHeight.Lift;
        private const int MaxSlots = 84, Boulders = 24;
        /// <summary>Seconds at the end of the hold over which the hairline cracks run out; seconds between rocks falling off the ball.</summary>
        private const float CrackCreep = 1.2f, DribbleEvery = .45f, CloudTurn = 25f * Mathf.Deg2Rad;

        private static readonly float Squash = Mathf.Sqrt(1f + Lift * Lift);
        private static readonly Vector3 View = new Vector3(0f, 1f, -Lift).normalized, Sun = new Vector3(.45f, 1f, .32f).normalized,
            Fill = new Vector3(-.35f, .5f, -.8f).normalized, Up = Vector3.up;
        private static readonly Vector2 ShadowPerCell = new Vector2(-.45f, -.32f);
        private static readonly Color Core = new Color(.015f, .015f, .035f), Dust = new Color(.76f, .70f, .59f),
            BallBody = new Color(.08f, .06f, .04f), SideColour = new Color(.13f, .09f, .06f), SlabEdge = new Color(.07f, .05f, .035f),
            CrackColour = new Color(.06f, .04f, .03f), Rib = new Color(.31f, .24f, .16f), HoleRim = new Color(.24f, .18f, .12f), HoleDeep = new Color(.11f, .08f, .05f),
            EarthDark = new Color(.19f, .13f, .08f), EarthLit = new Color(.46f, .34f, .22f), StoneDark = new Color(.22f, .21f, .2f), StoneLit = new Color(.56f, .54f, .5f),
            Lip = new Color(.42f, .33f, .22f);
        // The core's light (the anime's yellow-white sky), the rim and seam light, a plate dark against it, the dust and the cloud.
        private static readonly Color SunWhite = Color.white, SunPale = new Color(1f, .94f, .72f), SunWarm = new Color(1f, .76f, .4f),
            RimLight = new Color(1f, .88f, .58f), Seam = new Color(1f, .74f, .38f), Silhouette = new Color(.07f, .055f, .045f),
            DustTan = new Color(.72f, .62f, .46f), CloudColour = new Color(.9f, .9f, .88f);
        private static readonly Color RibSoft = Color.Lerp(HoleRim, Rib, .45f);
        private static readonly Material solid = new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex, name = "RimArt Chibaku ball solid" };
        private static readonly Material soft = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.Transparent);
        private static readonly Material glow = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.MoteGlow);
        private static readonly Material puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);
        private static readonly Mesh disc = Ring(0f, 1f, 64), limb = Ring(.8f, 1f, 72), coreRim = Ring(.84f, 1f, 48), rimBand = Ring(.95f, 1f, 96),
            lipBand = Ring(.84f, 1f, 96), lowArc = Arc(.07f), lowArcWide = Arc(.2f);
        private static readonly Mesh[] edgeBands = { Ring(.97f, 1f, 96), Ring(.93f, 1f, 96), Ring(.89f, 1f, 96), Ring(.84f, 1f, 96), Ring(.79f, 1f, 96), Ring(.73f, 1f, 96) };
        private static readonly float[] edgeAlphas = { .4f, .2f, .13f, .1f, .07f, .05f };
        private static readonly Mesh[] boulderShapes = BoulderShapes();
        private static readonly Vector3[] boulderDirs = Fibonacci(Boulders, 1.1f);
        private static readonly List<Fracture> fractures = Fractures();
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private static readonly Vector2[] runPoints = new Vector2[16];
        private static readonly float[] runShares = new float[16], runHalf = new float[16];

        private sealed class Flight
        {
            public ChibakuPlate plate;
            public float crackAt, heaveAt, liftAt, fly, arriveAt, spinRate, tumbleRate, axis, thick;
            public bool stone;
            public int slot = -1;
            public Mesh crack;
        }

        private struct Airborne
        {
            public Flight flight;
            public ChibakuHeld pawn;
            public Vector3 pos;
            public float nd, age, u, angle;
        }

        /// <summary>One crack on the ball: points on the unit sphere, when it runs out (a share of the growth), its width.</summary>
        private sealed class Fracture
        {
            public Vector3[] pts;
            public float t0, t1, w;
            public bool main;
        }

        private readonly ChibakuGround ground;
        private readonly Vector3 core;
        private readonly Vector2 middle;
        private readonly List<Flight> caught = new List<Flight>(), kept = new List<Flight>();
        private readonly Dictionary<IntVec3, Flight> flightAt = new Dictionary<IntVec3, Flight>();
        private readonly HashSet<int> realChunks = new HashSet<int>();
        private readonly Vector3[] slotDir;
        private readonly ChibakuPlate[] slotPlate;
        private readonly float[] slotFilledAt;
        private readonly Mesh[] slotFace, slotEdge;
        private readonly int slots;
        private readonly float spread;
        private readonly List<Airborne> air = new List<Airborne>();
        private readonly List<int> front = new List<int>(), back = new List<int>(), stonesFront = new List<int>(), stonesBack = new List<int>();
        private readonly Vector3[] faceVerts = new Vector3[7], edgeVerts = new Vector3[7];

        /// <summary>The ball's centre above the ground, in cells (drawn 0.6 cells north per cell up).</summary>
        public readonly float height;

        /// <summary>Seconds the formed ball holds before its seams open (the ability's holdSeconds); shorter once it breaks early.</summary>
        public float Hold { get; private set; }
        public float Crack => Formed + Hold;
        public float Burst => Crack + CrackTime;
        public float End => Burst + Tail;
        /// <summary>It broke before its time (<see cref="BreakAt"/>).</summary>
        public bool Broken { get; private set; }

        /// <summary>Seconds a pawn or item takes to fall out of the ball to the ground.</summary>
        public float FallTime => Mathf.Sqrt(2f * (height - .3f) / Gravity);

        public ChibakuBall(ChibakuGround ground, CompProperties_ChibakuTensei props, float height = DefaultHeight)
        {
            this.ground = ground;
            this.height = Mathf.Max(height, Radius + .4f);
            Hold = Mathf.Max(0f, props.holdSeconds);
            Vector3 mid = ground.cell.ToVector3Shifted();
            core = new Vector3(mid.x, this.height, mid.z);
            middle = new Vector2(mid.x, mid.z);
            foreach (ChibakuPlate p in ground.plates)
            {
                int i = p.index;
                float u = Mathf.Min(1f, p.along);
                var f = new Flight { plate = p, crackAt = Pull + CrackRun * u };
                f.liftAt = Pull + PullSeconds * (.06f + .5f * Mathf.Pow(u, 1.1f) + .16f * R(i * 3 + 1));
                f.fly = PullSeconds * (.17f + .1f * R(i * 3 + 2));
                f.heaveAt = Mathf.Max(f.crackAt + .03f, f.liftAt - .22f);
                f.arriveAt = f.liftAt + f.fly;
                f.spinRate = (70f + 140f * R(i * 11 + 5)) * (R(i * 11 + 6) < .5f ? -1f : 1f);
                f.tumbleRate = 3f + 5f * R(i * 11 + 7);
                f.axis = R(i * 11 + 4) * 180f;
                f.thick = .12f + .1f * R(i * 11 + 3);
                f.stone = R(i * 11 + 2) < .25f;
                f.crack = OutlineBand(p, .05f);
                (p.anchored ? kept : caught).Add(f);
                if (!p.anchored) foreach (IntVec3 c in p.cells) flightAt[c] = f;
            }
            caught.Sort((a, b) => a.arriveAt.CompareTo(b.arriveAt));

            slots = Math.Min(MaxSlots, caught.Count);
            slotDir = Fibonacci(slots, 0f);
            var order = new List<int>();
            for (int m = 0; m < slots; m++) order.Add(m);
            order.Sort((a, b) => R(a * 7 + 3).CompareTo(R(b * 7 + 3)));
            spread = 1.3f * Mathf.Sqrt(4f / Mathf.Max(1, slots));
            slotPlate = new ChibakuPlate[slots];
            slotFilledAt = new float[slots];
            for (int k = 0; k < caught.Count && slots > 0; k++)
            {
                int m = order[k % slots];
                caught[k].slot = m;
                if (k < slots)
                {
                    slotPlate[m] = caught[k].plate;
                    slotFilledAt[m] = caught[k].arriveAt;
                }
            }
            // The biggest of the thrown rocks become real chunks where they land: one for every platesPerChunk plates
            // pulled, minChunks to maxChunks.
            int real = Mathf.Clamp(Mathf.RoundToInt(caught.Count / (float)Mathf.Max(1, props.platesPerChunk)), props.minChunks, props.maxChunks);
            var bySize = new List<int>();
            for (int m = 0; m < slots; m += 2) bySize.Add(m);
            bySize.Sort((a, b) => ChunkSize(b).CompareTo(ChunkSize(a)));
            for (int k = 0; k < Mathf.Min(real, bySize.Count); k++) realChunks.Add(bySize[k]);

            slotFace = new Mesh[slots];
            slotEdge = new Mesh[slots];
            for (int m = 0; m < slots; m++)
            {
                slotFace[m] = PatchMesh(slotPlate[m]);
                slotEdge[m] = PatchMesh(null);
            }
        }

        private static float R(int i) => (float)ChibakuCut.Rand(i);

        /// <summary>Plates that have reached the ball by <paramref name="seconds"/>.</summary>
        public int Arrived(float seconds)
        {
            int n = 0;
            while (n < caught.Count && caught[n].arriveAt <= seconds) n++;
            return n;
        }

        /// <summary>Whether <paramref name="cell"/> is on a plate that is pulled, and when that plate tears free.</summary>
        public bool TryLiftOf(IntVec3 cell, out float liftAt)
        {
            liftAt = 0f;
            if (!flightAt.TryGetValue(cell, out Flight f)) return false;
            liftAt = f.liftAt;
            return true;
        }

        /// <summary>The plates that are pulled, with the moment each tears free.</summary>
        public IEnumerable<(ChibakuPlate plate, float liftAt)> PulledPlates
        {
            get { foreach (Flight f in caught) yield return (f.plate, f.liftAt); }
        }

        public int Caught => caught.Count;
        public int Slots => slots;
        public float LastArrival => caught.Count > 0 ? caught[caught.Count - 1].arriveAt : 0f;
        public int FilledSlots(float seconds)
        {
            int n = 0;
            for (int m = 0; m < slots; m++) if (slotFilledAt[m] <= seconds) n++;
            return n;
        }

        public float BallRadiusAt(float seconds)
        {
            if (seconds >= Formed) return Radius * (1f - .03f * Squeeze(seconds));
            float frac = caught.Count > 0 ? Arrived(seconds) / (float)caught.Count : 1f;
            return Radius * (.3f + .7f * Mathf.Pow(frac, 1f / 3f));
        }

        private float Squeeze(float s) => s >= Formed && s < Crack ? Bump(((s - Formed) % 1f) / .18f) : 0f;

        /// <summary>
        /// The ball breaks at <paramref name="s"/> (Pain went down, or let it go): its seams open now, or as soon as it is
        /// formed if it is still forming, so every plate and pawn already on its way arrives first.
        /// </summary>
        public void BreakAt(float s)
        {
            if (s >= Crack) return;
            Hold = Mathf.Max(0f, s - Formed);
            Broken = true;
        }

        /// <summary>The thrown rocks: slot, where it lands (x, z), seconds after the burst, and whether it becomes a real chunk item.</summary>
        public IEnumerable<(int m, Vector2 land, float after, bool real)> ThrownRocks
        {
            get
            {
                for (int m = 0; m < slots; m += 2)
                {
                    Vector3 land = Chunk(m, out _, out _, out _);
                    yield return (m, new Vector2(land.x, land.y), land.z, realChunks.Contains(m));
                }
            }
        }

        public int RealChunkCount => realChunks.Count;

        private static float ChunkSize(int m) => .6f + .45f * R(m * 13 + 2);

        /// <summary>Seconds after the burst at which the latest chunk lands.</summary>
        public float LastChunkLands()
        {
            float last = 0f;
            for (int m = 0; m < slots; m += 2) last = Mathf.Max(last, Chunk(m, out _, out _, out _).z);
            return last;
        }

        // ---- the core's path ----------------------------------------------------------------------------

        /// <summary>
        /// The core's path from Pain's raised hand (<paramref name="from"/>) to its place over the cell (<paramref name="to"/>),
        /// <paramref name="u"/> of the way: straight up out of the hand, over the top and down onto its place (a quadratic
        /// curve through a point <see cref="CoreClimb"/> over the higher end, straight above the hand). Points are (x, height, z).
        /// </summary>
        public static Vector3 CorePoint(Vector3 from, Vector3 to, float u)
        {
            var k = new Vector3(from.x, Mathf.Max(from.y, to.y) + CoreClimb, from.z);
            float a = 1f - u;
            return a * a * from + 2f * u * a * k + u * u * to;
        }

        /// <summary>Cells along <see cref="CorePoint"/>'s curve.</summary>
        public static float CorePathLength(Vector3 from, Vector3 to)
        {
            float length = 0f;
            Vector3 last = from;
            for (int i = 1; i <= 32; i++)
            {
                Vector3 q = CorePoint(from, to, i / 32f);
                length += (q - last).magnitude;
                last = q;
            }
            return length;
        }

        // ---- drawing --------------------------------------------------------------------------------------

        public void Draw(float s, List<ChibakuHeld> pawns = null)
        {
            if (s < 0f || s >= End || ground.texture == null) return;
            VfxDraw.Begin(middle);
            float top = AltitudeLayer.MoteOverhead.AltitudeFor(), floor = AltitudeLayer.Filth.AltitudeFor();
            float spin = Spin * Mathf.Max(0f, s - Pull), r = BallRadiusAt(s);
            int arrived = Arrived(s);
            float frac = caught.Count > 0 ? arrived / (float)caught.Count : 1f;
            // The core's glare: full until about half the plates have arrived, then the ball covers it.
            float light = 1f - .75f * Smooth((frac - .45f) / .55f);
            Vector2 C = Screen(core);

            DrawFloor(s, floor, spin, light);

            // In the air: flying plates, sorted by depth round the ball.
            air.Clear();
            foreach (Flight f in caught)
            {
                if (s < f.liftAt || s >= f.arriveAt) continue;
                float age = s - f.liftAt, u = Mathf.Pow(age / f.fly, 1.7f);
                var start = new Vector3(f.plate.centre.x, HeaveH, f.plate.centre.y);
                Vector3 target = core + RotY(slotDir[f.slot], spin) * (r * 1.05f);
                Vector3 pos = Vector3.Lerp(start, target, u);
                pos.y += .2f * Smooth(age / (.25f * f.fly)) * (1f - u);
                air.Add(new Airborne { flight = f, pos = pos, nd = Vector3.Dot(pos - core, View), age = age, u = u });
            }
            if (pawns != null) AddPawns(pawns, s, r);
            air.Sort((a, b) => a.nd.CompareTo(b.nd));

            // Tan dust rising from the torn plates and drifting in, and the haze that hangs under the ball.
            foreach (Flight f in caught)
            {
                if (f.plate.index % 2 != 0 || s < f.liftAt - .05f) continue;
                float u = (s - f.liftAt + .05f) / 1.5f;
                if (u >= 1f) continue;
                float e = EaseOut(u);
                Vector2 g = Vector2.Lerp(f.plate.centre, middle, .35f * e);
                Sprite(new Vector2(g.x, g.y + 1.4f * e * Lift), .55f + 1.1f * u, .45f + .9f * u, Fade(DustTan, .34f * Mathf.Sin(u * Mathf.PI)), puff,
                    top + .003f + (f.plate.index % 40) * .00001f);
            }
            float haze = s < Pull ? 0f : s < Formed ? Smooth((s - Pull) / .8f) : s < Burst ? Mathf.Lerp(1f, .45f, Smooth((s - Formed) / 1.5f)) : .45f * (1f - Mathf.Clamp01((s - Burst) / .5f));
            if (haze > 0f)
                for (int i = 0; i < 9; i++)
                {
                    float a = R(i + 820) * Mathf.PI * 2f + s * .12f * (i % 2 == 1 ? 1f : -1f), d = 1f + 2.2f * R(i + 821), h = .4f + (height - Radius) * .9f * R(i + 822);
                    float size = 2.2f + 1.4f * R(i + 823);
                    Sprite(new Vector2(middle.x + Mathf.Cos(a) * d, middle.y + Mathf.Sin(a) * d * .7f + h * Lift), size, size * .8f, Fade(DustTan, .15f * haze), puff, top + .0035f + i * .00005f);
                }
            // Small rocks spiralling up from the crater into the ball: a stream while it pulls, fewer while it holds.
            if (s >= Pull + .3f) Stream(s, Pull + .3f, Formed, 26, 1500, r, top);
            if (s >= Formed) Stream(s, Formed, Crack - 1f, 9, 1700, r, top);

            int behind = 0;
            for (int k = 0; k < air.Count; k++)
            {
                Airborne a = air[k];
                if (a.nd >= 0f) continue;
                if (a.pawn != null) PawnPicture(a, top + .005f + behind++ * .0002f);
                else FlyingSlab(a, C, light, top + .005f + behind++ * .0002f);
            }

            // The core, the back half of the cloud ring, and the ball growing round the core.
            if (s < Formed + .2f)
            {
                float grown = Mathf.Lerp(.65f, 1f, Smooth(s / Pulse));
                BlackCore(C, CoreR * grown, 1f, top + .04f, light, s);
                if (s < .15f)
                {
                    float u = s / .15f, size = Mathf.Lerp(1f, 4f, EaseOut(u));
                    Sprite(C, size, size, Fade(SunPale, .7f * (1f - u)), glow, top + .0412f);
                    Sprite(C, size * .45f, size * .45f, Fade(SunWhite, .8f * (1f - u)), glow, top + .0413f);
                }
            }
            float cloudA = Smooth((s - Formed + .8f) / 1.2f), blow = s >= Burst ? Mathf.Clamp01((s - Burst) / .8f) : 0f;
            CloudRing(s, cloudA, blow, false, top + .03f);
            if (s >= Formed) Dribble(s, top, floor);
            if (s >= Pull && s < Burst) DrawBall(s, spin, r, s < Formed ? Smooth(Mathf.Clamp01(frac * 1.6f)) : 1f, top, frac);
            if (s >= Crack) Chips(s, top, floor);
            if (s >= Formed - .3f && s < Burst)
                for (int i = 0; i < 7; i++)
                {
                    float a = s * (.6f + .15f * R(i + 500)) + i * .9f, rr = Radius * (1.12f + .1f * R(i + 501)), y = Mathf.Sin(i * 2.3f) * Radius * .5f;
                    var q = new Vector3(core.x + Mathf.Cos(a) * rr, core.y + y, core.z + Mathf.Sin(a) * rr);
                    bool inFront = Vector3.Dot(q - core, View) >= 0f;
                    PaperBombGraphics.Rock(Screen(q), .12f + .08f * R(i + 502), a * Mathf.Rad2Deg * 3f, Mathf.Clamp01((s - Formed + .3f) / .3f), i,
                        inFront ? top + .07f + i * .0006f : top + .037f - i * .0006f);
                }

            int ahead = 0;
            for (int k = 0; k < air.Count; k++)
            {
                Airborne a = air[k];
                if (a.nd < 0f) continue;
                if (a.pawn != null) PawnPicture(a, top + .075f + ahead++ * .0002f);
                else FlyingSlab(a, C, light, top + .075f + ahead++ * .0002f);
            }
            CloudRing(s, cloudA, blow, true, top + .1f);
            if (s >= Pull && s < Formed) Inflow(s, C, Mathf.Clamp01((s - Pull) / .2f) * (1f - Smooth((frac - .7f) / .3f)), top + .11f);
            if (s >= Burst) DrawBurst(s - Burst, C, top, floor, pawns != null);
        }

        /// <summary>
        /// The black core as the anime draws it in the sky: a black disc with a thin white rim in a yellow-white glare (3
        /// soft additive layers out to about 1.4 times its size over <see cref="CoreR"/>), and 14 thin rays that turn slowly
        /// and flicker. <paramref name="glowA"/> scales the glare and rays alone, so the ball can cover the core without a
        /// halo round it; <paramref name="seconds"/> turns the rays.
        /// </summary>
        public static void BlackCore(Vector2 c, float r, float alpha, float altitude, float glowA = 1f, float seconds = 0f)
        {
            if (r <= .005f || alpha <= 0f) return;
            float k = r / CoreR, L = alpha * glowA;
            if (L > .001f)
            {
                Sprite(c, 2.8f * k, 2.8f * k, Fade(SunWarm, .24f * L), glow, altitude);
                Sprite(c, 1.7f * k, 1.7f * k, Fade(SunPale, .42f * L), glow, altitude + .0002f);
                Sprite(c, 1.05f * k, 1.05f * k, Fade(SunWhite, .6f * L), glow, altitude + .0004f);
                for (int i = 0; i < 14; i++)
                {
                    float ang = (i / 14f * 360f + seconds * 6f + (R(i + 960) - .5f) * 14f) * Mathf.Deg2Rad, flick = .6f + .4f * Mathf.Sin(seconds * 17f + i * 2.1f);
                    float len = (i % 2 == 1 ? .8f + .6f * R(i + 961) : 1.6f + 1.4f * R(i + 962)) * k * flick, w = (i % 2 == 1 ? .05f : .08f) * k;
                    var way = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    Vector2 a0 = c + way * (r * 1.05f);
                    VfxDraw.Streak(a0, a0 + way * len, w, Fade(SunPale, .5f * L * flick), VfxDraw.whiteGlow, altitude + .0006f, 6);
                }
            }
            VfxDraw.DrawMesh(coreRim, c, altitude + .0008f, r * 1.1f, r * 1.1f, 0f, Fade(SunWhite, .85f * alpha), VfxDraw.whiteGlow);
            Solid(disc, c, altitude + .001f, r, r, 0f, Fade(Core, alpha));
        }

        /// <summary>
        /// The core in flight from Pain's raised hand (<paramref name="from"/>) to its place over the cell (<paramref name="to"/>),
        /// <paramref name="u"/> of the way along <see cref="CorePoint"/>'s curve, with a dark trail behind it. Points are
        /// (x, height, z) in cells.
        /// </summary>
        public static void FlyingCore(Vector3 from, Vector3 to, float u, float r, float altitude, float seconds, float glowA)
        {
            Vector2[] trail = GokuGraphics.Points(7);
            for (int k = 0; k < trail.Length; k++) trail[k] = Screen(CorePoint(from, to, Mathf.Max(0f, u - k * .035f)));
            Vector2 at = trail[0];
            GokuGraphics.Line(trail, .2f, Fade(Core, .3f), solid, altitude - .002f, GokuGraphics.Taper.End);
            BlackCore(at, r, 1f, altitude, glowA, seconds);
        }

        /// <summary>
        /// Pawns, items and trees in the air: pulled in (their picture turning as it flies to the ball and shrinking
        /// to 60 %) or falling out after the burst. A pawn still waiting to be lifted kicks up dust at its feet.
        /// </summary>
        private void AddPawns(List<ChibakuHeld> pawns, float s, float r)
        {
            foreach (ChibakuHeld h in pawns)
            {
                if (h.state == ChibakuHeld.Waiting && h.pawn != null && h.pawn.Spawned && s >= Pull)
                {
                    Vector3 feet = h.pawn.DrawPos;
                    for (int i = 0; i < 3; i++)
                    {
                        float u = (s * 1.6f + R(h.pawn.thingIDNumber + i * 7)) % 1f;
                        Sprite(new Vector2(feet.x + (R(h.pawn.thingIDNumber + i * 7 + 1) - .5f) * .5f, feet.z - .3f + u * .3f), .25f + u * .3f, .2f + u * .25f,
                            Fade(Dust, .5f * Mathf.Sin(u * Mathf.PI)), puff, AltitudeLayer.MoteOverhead.AltitudeFor() + .001f);
                    }
                    continue;
                }
                if (h.material == null) continue;
                float seed = R(h.Thing?.thingIDNumber ?? Mathf.RoundToInt(h.from.x * 131f + h.from.z * 17f)) * 360f;
                if ((h.state == ChibakuHeld.Flying || h.state == ChibakuHeld.Held) && s >= h.liftAt && s < h.liftAt + ChibakuPull.FlySeconds)
                {
                    float age = s - h.liftAt, u = Mathf.Pow(Mathf.Clamp01(age / ChibakuPull.FlySeconds), 1.6f);
                    var start = new Vector3(h.from.x, 0f, h.from.z);
                    Vector3 target = core + (start - core).normalized * (r * 1.05f), pos = Vector3.Lerp(start, target, u);
                    pos.y += .25f * Smooth(age / (.2f * ChibakuPull.FlySeconds)) * (1f - u);
                    air.Add(new Airborne { pawn = h, pos = pos, nd = Vector3.Dot(pos - core, View), u = u, angle = seed + age * 330f });
                }
                else if (h.state == ChibakuHeld.Falling && s < h.landAt)
                {
                    float age = Mathf.Max(0f, s - Burst), k = Mathf.Clamp01(age / Mathf.Max(.01f, h.landAt - Burst));
                    var start = core + new Vector3(h.drop.x * .4f, -.3f, h.drop.y * .4f);
                    Vector3 land = h.landCell.ToVector3Shifted();
                    var pos = new Vector3(Mathf.Lerp(start.x, land.x, k), Mathf.Max(0f, start.y - .5f * Gravity * age * age), Mathf.Lerp(start.z, land.z, k));
                    air.Add(new Airborne { pawn = h, pos = pos, nd = 1f, u = 0f, angle = seed + age * 360f });
                }
            }
        }

        /// <summary>A taken thing's picture (a pawn as the game draws it, an item's or a tree's graphic) turned and shrunk, with a soft shadow.</summary>
        private static void PawnPicture(Airborne a, float altitude)
        {
            float scale = Mathf.Lerp(1f, .6f, a.u);
            Vector2 size = a.pawn.size * scale;
            Sprite(new Vector2(a.pos.x + ShadowPerCell.x * a.pos.y, a.pos.z + ShadowPerCell.y * a.pos.y), .8f * scale, .4f * scale, Fade(Color.black, .35f / (1f + a.pos.y)), soft,
                AltitudeLayer.Shadows.AltitudeFor());
            properties.SetColor(ShaderPropertyIDs.Color, Color.white);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(new Vector3(a.pos.x, altitude, a.pos.z + a.pos.y * Lift), Quaternion.Euler(0f, a.angle, 0f), new Vector3(size.x, 1f, size.y)),
                a.pawn.material, 0, null, 0, properties);
        }

        /// <summary>
        /// A plate in the air: dark against the core's light once it is up (not while it is still near the ground), with
        /// a pale-yellow edge on the side facing the core.
        /// </summary>
        private void FlyingSlab(Airborne a, Vector2 C, float light, float altitude)
        {
            Flight f = a.flight;
            Vector2 S = Screen(a.pos), toCore = C - S;
            float dark = .75f * Smooth((a.u - .12f) / .45f), rimA = .6f * Smooth((a.u - .2f) / .3f) * (.35f + .65f * Mathf.Min(1f, light));
            Slab(f, a.pos, a.age * f.spinRate, .3f + a.age * f.tumbleRate, Mathf.Lerp(1f, .6f, a.u), altitude, -1f, dark, toCore.sqrMagnitude > 1e-6f ? toCore.normalized : Vector2.zero, rimA);
        }

        /// <summary>
        /// Small rocks spiralling up from the crater into the ball's underside. Each makes trips of 1.1-1.6 s; a trip starts
        /// only between <paramref name="from"/> and <paramref name="to"/>. Behind or in front of the ball by depth.
        /// </summary>
        private void Stream(float s, float from, float to, int count, int seed, float r, float top)
        {
            for (int i = 0; i < count; i++)
            {
                float cyc = 1.1f + .5f * R(seed + i * 3), since = s - from, u = (since / cyc + R(seed + i * 3 + 1)) % 1f, start = s - u * cyc;
                if (start < from || start > to) continue;
                float a = R(seed + i * 3 + 2) * Mathf.PI * 2f + u * 2.6f, r0 = 1.2f + (ground.radius - 1.6f) * R(seed + i * 5 + 7), rr = Mathf.Lerp(r0, r * .6f, u * u);
                var q = new Vector3(middle.x + Mathf.Cos(a) * rr, Mathf.Lerp(.1f, core.y - r * .7f, Mathf.Pow(u, 1.4f)), middle.y + Mathf.Sin(a) * rr);
                bool inFront = Vector3.Dot(q - core, View) >= 0f;
                PaperBombGraphics.Rock(Screen(q), .08f + .08f * R(seed + i * 5 + 8), a * Mathf.Rad2Deg * 4f, Mathf.Clamp01(u * 8f), i,
                    inFront ? top + .0745f + i * .00002f : top + .0045f + i * .00002f);
            }
        }

        /// <summary>The area circle, the core's light, cracks, holes, the crater's dark middle and lighter lip, rim cracks and rocks, heaving plates and their dust.</summary>
        private void DrawFloor(float s, float floor, float spin, float light)
        {
            float radius = ground.radius;
            float spreadOut = EaseOut(s / .3f), fade = 1f - Mathf.Clamp01((s - Formed - .6f) / .6f);
            for (int k = 0; k < edgeBands.Length; k++)
                Solid(edgeBands[k], middle, floor + .005f + k * .0002f, radius * spreadOut, radius * spreadOut, 0f, Fade(Core, edgeAlphas[k] * .45f * fade));
            if (s < Burst + .3f)
            {
                float pool = spreadOut * (s < Burst ? 1f : 1f - (s - Burst) / .3f);
                Sprite(middle, radius * 2.1f, radius * 2.1f, Fade(SunWarm, .1f * light * pool), glow, floor + .0114f);
            }

            float building = AltitudeLayer.Building.AltitudeFor(), shadows = AltitudeLayer.Shadows.AltitudeFor();
            foreach (Flight f in kept)
                if (s >= f.crackAt) Solid(f.crack, f.plate.centre, floor + .012f, 1f, 1f, 0f, Fade(CrackColour, .75f * Mathf.Clamp01((s - f.crackAt) / .1f)));
            int heaving = 0;
            foreach (Flight f in caught)
            {
                if (s < f.crackAt) continue;
                if (s < f.heaveAt)
                {
                    Solid(f.crack, f.plate.centre, floor + .012f, 1f, 1f, 0f, Fade(CrackColour, .75f * Mathf.Clamp01((s - f.crackAt) / .1f)));
                    continue;
                }
                // The whole plate's ground is gone: dark earth ribs where the edges were (low contrast, so the crater does
                // not read as tiles), the hole inside them.
                Solid(f.plate.face, f.plate.centre, floor + .0095f, 1f, 1f, 0f, RibSoft);
                Solid(f.plate.hole, f.plate.centre, floor + .01f, 1f, 1f, 0f, Color.Lerp(HoleRim, HoleDeep, 1f - f.plate.along));
                if (s < f.liftAt)
                {
                    float u = Smooth((s - f.heaveAt) / (f.liftAt - f.heaveAt)), jig = Mathf.Sin(s * 55f + f.plate.index) * .02f * u;
                    Slab(f, new Vector3(f.plate.centre.x + jig, HeaveH * u, f.plate.centre.y), 0f, .45f * u, 1f, building + .01f + heaving++ * .0002f, shadows);
                }
                if (f.plate.index % 2 == 0 && s >= f.liftAt && s < f.liftAt + .8f)
                {
                    float u = (s - f.liftAt) / .8f;
                    Sprite(new Vector2(f.plate.centre.x, f.plate.centre.y + u * .45f), .5f + u * .7f, .42f + u * .6f, Fade(Dust, .42f * (1f - u)), puff,
                        AltitudeLayer.MoteOverhead.AltitudeFor() + .002f);
                }
            }
            float bowl = Smooth((s - Pull - PullSeconds * .2f) / (PullSeconds * .6f));
            if (bowl > 0f)
            {
                Sprite(middle, radius * 1.7f, radius * 1.7f, Fade(Core, .42f * bowl), soft, floor + .011f);
                Solid(lipBand, middle, floor + .0112f, radius * 1.01f, radius * 1.01f, 0f, Fade(Lip, .35f * bowl));
            }

            for (int i = 0; i < 12; i++)
            {
                float a = (i + R(i + 300) * .6f) / 12f * Mathf.PI * 2f;
                Vector2 at = middle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius + .15f);
                RimCrack(at, Smooth((s - Pull - CrackRun) / .15f), 60 + i * 7, floor + .013f);
                float born = Pull + PullSeconds * (.6f + .1f * R(i + 310)), u = Mathf.Clamp01((s - born) / .35f);
                if (s < born) continue;
                float d = radius + .1f + .5f * R(i + 311) * EaseOut(u), hh = .35f * Mathf.Sin(u * Mathf.PI);
                PaperBombGraphics.Rock(middle + new Vector2(Mathf.Cos(a + .1f) * d, Mathf.Sin(a + .1f) * d + hh * Lift), .14f + .1f * R(i + 312), a * Mathf.Rad2Deg + u * 200f, 1f, i,
                    u < 1f ? AltitudeLayer.MoteOverhead.AltitudeFor() + .003f + i * .0006f : floor + .03f + i * .0006f);
            }
        }

        /// <summary>
        /// A plate as a solid: shadow, south side, edge, face (the captured ground, or earth/stone upside down).
        /// <paramref name="dark"/> 0..1 darkens it toward a silhouette; <paramref name="rim"/> (a unit way on screen) and
        /// <paramref name="rimA"/> light its edge on that side.
        /// </summary>
        private void Slab(Flight f, Vector3 pos, float spinDegrees, float tumble, float scale, float altitude, float shadowAltitude = -1f,
            float dark = 0f, Vector2 rim = default, float rimA = 0f)
        {
            float ct = Mathf.Cos(tumble), sq = Mathf.Max(.2f, Mathf.Abs(ct));
            Matrix4x4 shape = Matrix4x4.Rotate(Quaternion.Euler(0f, f.axis, 0f)) * Matrix4x4.Scale(new Vector3(sq * scale, 1f, scale))
                * Matrix4x4.Rotate(Quaternion.Euler(0f, spinDegrees - f.axis, 0f));
            if (shadowAltitude < 0f) shadowAltitude = AltitudeLayer.Shadows.AltitudeFor();
            float thick = f.thick * scale * (.35f + .65f * sq);
            DrawShaped(f.plate.face, new Vector3(pos.x + ShadowPerCell.x * pos.y, shadowAltitude, pos.z + ShadowPerCell.y * pos.y), shape, Fade(Color.black, .35f / (1f + pos.y * .25f)), solid);
            if (rimA > .01f)
                DrawShaped(f.plate.edge, new Vector3(pos.x + rim.x * .07f, altitude - .00005f, pos.z + pos.y * Lift + rim.y * .07f), shape, Fade(RimLight, rimA), solid);
            DrawShaped(f.plate.face, new Vector3(pos.x, altitude, pos.z + (pos.y - thick) * Lift), shape, Color.Lerp(SideColour, Silhouette, dark), solid);
            DrawShaped(f.plate.edge, new Vector3(pos.x, altitude + .00005f, pos.z + pos.y * Lift), shape, SlabEdge, solid);
            if (ct >= 0f) DrawShaped(f.plate.face, new Vector3(pos.x, altitude + .0001f, pos.z + pos.y * Lift), shape, Grey(Mathf.Lerp(.7f, 1f, sq) * (1f - dark)), ground.FaceMaterial);
            else DrawShaped(f.plate.face, new Vector3(pos.x, altitude + .0001f, pos.z + pos.y * Lift), shape,
                Color.Lerp(Color.Lerp(f.stone ? StoneDark : EarthDark, f.stone ? StoneLit : EarthLit, .3f + .55f * sq), Silhouette, dark), solid);
        }

        /// <summary>
        /// The ball: shadow, plates and boulders just past the limb, dark body, the light inside it (it shows in the gaps),
        /// the plates and boulders facing the camera back to front, darker limb, the rim light on its lower edge, and the
        /// cracks. In the crack phase it shakes and its plates pull apart.
        /// </summary>
        private void DrawBall(float s, float spin, float r, float bodyA, float top, float frac)
        {
            // Cracking: dark hairlines run out over the last CrackCreep seconds of the hold (and on 0.15 s into the crack
            // phase); in the crack phase they open and fill with light, the plates pull apart and the ball shakes.
            // A ball let go (or broken) right after it formed has a short hold: the hairlines then start no later than the crack.
            float creepFrom = Mathf.Min(Crack, Mathf.Max(Formed + .2f, Crack - CrackCreep)), grow = Mathf.Clamp01((s - creepFrom) / (Crack + .15f - creepFrom));
            float crackU = Mathf.Clamp01((s - Crack) / CrackTime), open = Mathf.Pow(crackU, 1.4f);
            Vector3 centre = core + new Vector3(Mathf.Sin(s * 71f) * .035f * open, 0f, Mathf.Cos(s * 89f) * .03f * open);
            Vector2 C = Screen(centre);
            float glowK = s < Formed ? .35f + .3f * frac : .45f + .5f * Squeeze(s) + 1.4f * crackU;
            float rimA = s < Formed ? Smooth((frac - .3f) / .7f) : .85f + .6f * crackU;

            Sprite(new Vector2(core.x + ShadowPerCell.x * core.y, core.z + ShadowPerCell.y * core.y), r * 2.3f, r * 1.7f, Fade(Color.black, .45f * bodyA), soft, AltitudeLayer.Shadows.AltitudeFor());
            front.Clear();
            back.Clear();
            for (int m = 0; m < slots; m++)
            {
                if (slotFilledAt[m] > s) continue;
                float nd = Vector3.Dot(RotY(slotDir[m], spin), View);
                if (nd > -.3f) (nd < 0f ? back : front).Add(m);
            }
            back.Sort((a, b) => Vector3.Dot(RotY(slotDir[a], spin), View).CompareTo(Vector3.Dot(RotY(slotDir[b], spin), View)));
            front.Sort((a, b) => Vector3.Dot(RotY(slotDir[a], spin), View).CompareTo(Vector3.Dot(RotY(slotDir[b], spin), View)));
            stonesFront.Clear();
            stonesBack.Clear();
            float boulderFill = frac * Boulders;
            for (int j = 0; j < Boulders; j++)
            {
                float nd = Vector3.Dot(RotY(boulderDirs[j], spin), View);
                if (boulderFill - j > 0f && nd > -.35f) (nd < 0f ? stonesBack : stonesFront).Add(j);
            }
            stonesBack.Sort((a, b) => Vector3.Dot(RotY(boulderDirs[a], spin), View).CompareTo(Vector3.Dot(RotY(boulderDirs[b], spin), View)));
            stonesFront.Sort((a, b) => Vector3.Dot(RotY(boulderDirs[a], spin), View).CompareTo(Vector3.Dot(RotY(boulderDirs[b], spin), View)));

            for (int k = 0; k < back.Count; k++) SurfacePlate(back[k], spin, r, centre, open, top + .046f + k * .0001f);
            for (int k = 0; k < stonesBack.Count; k++) BoulderOn(stonesBack[k], spin, r, centre, open, boulderFill, top + .0535f + k * .0001f);
            Solid(disc, C, top + .056f, r * .98f, r * .98f * Squash, 0f, Fade(BallBody, bodyA));
            if (glowK > 0f)
            {
                Sprite(C, r * 1.8f, r * 1.8f * Squash, Fade(Seam, Mathf.Min(1f, .8f * glowK) * bodyA), glow, top + .0561f);
                Sprite(C, r * 1.15f, r * 1.15f * Squash, Fade(SunPale, Mathf.Min(1f, .5f * glowK) * bodyA), glow, top + .0562f);
            }
            // Cracking open: the light inside is even across the ball, so every opening seam glows, not only the middle.
            if (open > 0f) VfxDraw.DrawMesh(disc, C, top + .0563f, r * .96f, r * .96f * Squash, 0f, Fade(Seam, .55f * open * bodyA), VfxDraw.whiteGlow);
            for (int k = 0; k < front.Count; k++) SurfacePlate(front[k], spin, r, centre, open, top + .057f + k * .0001f);
            for (int k = 0; k < stonesFront.Count; k++) BoulderOn(stonesFront[k], spin, r, centre, open, boulderFill, top + .0635f + k * .0001f);
            Solid(limb, C, top + .067f, r * 1.02f, r * 1.02f * Squash, 0f, Fade(Core, .32f * bodyA));
            // The rim light lies on the ball's own edge (inside the lumps, not a clean ring round them): a faint band, and a
            // soft wide arc and a thin bright one along the lower edge, where the core's light leaks out under the ball.
            if (rimA > 0f)
            {
                VfxDraw.DrawMesh(rimBand, C, top + .0672f, r * .97f, r * .97f * Squash, 0f, Fade(RimLight, .07f * rimA * bodyA), VfxDraw.whiteGlow);
                VfxDraw.DrawMesh(lowArcWide, C, top + .0673f, r * .97f, r * .97f * Squash, 0f, Fade(Seam, .18f * rimA * bodyA), VfxDraw.whiteGlow);
                VfxDraw.DrawMesh(lowArc, C, top + .0674f, r * .97f, r * .97f * Squash, 0f, Fade(RimLight, .5f * rimA * bodyA), VfxDraw.whiteGlow);
            }

            if (grow <= 0f) return;
            float rot = Spin * (s - Crack);
            foreach (Fracture f in fractures) DrawFracture(f, centre, r, rot, grow, open, top + .068f);
            if (s < Crack) return;
            int i = 0;
            foreach (Fracture f in fractures)
            {
                if (!f.main) continue;
                // Dust spurting out of the main cracks.
                Vector3 d = RotY(f.pts[1 + i % 3], rot);
                if (Vector3.Dot(d, View) > .1f)
                {
                    float u = ((s - Crack) * 2.5f + R(i + 600)) % 1f;
                    Vector2 at = Screen(centre + d * (r * 1.05f));
                    Sprite(new Vector2(at.x + d.x * u * .3f, at.y + d.z * u * .3f + u * .25f), .3f + u * .5f, .26f + u * .4f, Fade(Dust, .45f * Mathf.Sin(u * Mathf.PI)), puff,
                        top + .069f + i * .00005f);
                }
                i++;
            }
        }

        /// <summary>
        /// One surface plate: six corners round its slot direction, pushed out a little so the outline is lumpy. About 15 %
        /// are the captured ground; the rest came in underside out and are earth or stone. <paramref name="open"/> (0..1, the
        /// crack phase) pulls it apart from its neighbours: out from the centre and a little smaller.
        /// </summary>
        private void SurfacePlate(int m, float spin, float r, Vector3 centre, float open, float altitude)
        {
            Vector3 n = RotY(slotDir[m], spin);
            Vector3 t1 = Vector3.Cross(n, Mathf.Abs(n.y) > .95f ? Vector3.right : Up).normalized, t2 = Vector3.Cross(n, t1);
            float outR = r * (1f + .1f * R(m * 5 + 1) + .05f * open), ext0 = spread * (1f - .15f * open);
            Vector2 c = Screen(centre + n * outR);
            faceVerts[0] = new Vector3(c.x, 0f, c.y);
            for (int k = 0; k < 6; k++)
            {
                float a = (k + (R(m * 17 + k) - .5f) * .5f) / 6f * Mathf.PI * 2f, ext = ext0 * (.7f + .38f * R(m * 23 + k));
                Vector2 p = Screen(centre + (n + t1 * (Mathf.Cos(a) * ext) + t2 * (Mathf.Sin(a) * ext)).normalized * outR);
                faceVerts[k + 1] = new Vector3(p.x, 0f, p.y);
                Vector2 grown = p + (p - c).normalized * .02f;
                edgeVerts[k + 1] = new Vector3(grown.x, 0f, grown.y);
            }
            edgeVerts[0] = faceVerts[0];
            slotFace[m].vertices = faceVerts;
            slotFace[m].RecalculateBounds();
            slotEdge[m].vertices = edgeVerts;
            slotEdge[m].RecalculateBounds();
            float lit = Lit(n), kind = R(m * 11 + 5);
            DrawShaped(slotEdge[m], new Vector3(0f, altitude, 0f), Matrix4x4.identity, SlabEdge, solid);
            if (kind < .15f) DrawShaped(slotFace[m], new Vector3(0f, altitude + .00005f, 0f), Matrix4x4.identity, Grey(Mathf.Lerp(.3f, 1.05f, lit)), ground.FaceMaterial);
            else DrawShaped(slotFace[m], new Vector3(0f, altitude + .00005f, 0f), Matrix4x4.identity,
                kind < .55f ? Color.Lerp(EarthDark, EarthLit, lit) : Color.Lerp(StoneDark, StoneLit, lit), solid);
        }

        /// <summary>How lit a surface facing <paramref name="n"/> is: the sun, a weak camera-side fill, darker underneath.</summary>
        private static float Lit(Vector3 n) =>
            Mathf.Clamp01(Mathf.Max(0f, Vector3.Dot(n, Sun)) * .95f + Mathf.Max(0f, Vector3.Dot(n, Fill)) * .25f + .02f) * (n.y < 0f ? 1f + n.y * .45f : 1f);

        /// <summary>Boulder <paramref name="j"/> set into the ball's surface, grown in as the ball fills.</summary>
        private void BoulderOn(int j, float spin, float r, Vector3 centre, float open, float fill, float altitude)
        {
            Vector3 n = RotY(boulderDirs[j], spin);
            Vector2 at = Screen(centre + n * (r * (1f + .06f * R(j * 7 + 1) + .06f * open)));
            float size = r * (.2f + .12f * R(j * 7 + 2)) * Mathf.Clamp01(fill - j);
            if (size <= .01f) return;
            float lit = Lit(n), angle = R(j * 7 + 3) * 360f;
            bool earth = j % 3 == 0;
            Color dark = earth ? EarthDark : StoneDark, bright = earth ? EarthLit : StoneLit;
            Mesh shape = boulderShapes[j % boulderShapes.Length];
            // A dark outline, the rock lit by the sun, and a smaller lighter top pushed toward the light.
            Solid(shape, at, altitude, size * 1.1f, size, angle, SlabEdge);
            Solid(shape, at, altitude + .00002f, size, size * .9f, angle, Color.Lerp(dark, bright, lit * .75f));
            Solid(shape, new Vector2(at.x - size * .13f, at.y + size * .14f), altitude + .00004f, size * .64f, size * .52f, angle, Color.Lerp(dark, bright, Mathf.Min(1f, lit * .85f + .2f)));
        }

        /// <summary>
        /// The ring of cloud swirling round the ball's middle: clumps of three puffs (not evenly spaced, so it reads as cloud
        /// and not as a hoop) and a few short faint streaks. Only the half on the given side of the ball.
        /// <paramref name="blow"/> 0..1 throws it outward as it fades after the burst.
        /// </summary>
        private void CloudRing(float s, float amount, float blow, bool inFront, float altitude)
        {
            if (amount <= .001f || blow >= 1f) return;
            float turn = s * CloudTurn, a = amount * (1f - blow), outK = 1f + 1.6f * EaseOut(blow);
            for (int i = 0; i < 14; i++)
            {
                float start = i / 14f * Mathf.PI * 2f + (R(i + 900) - .5f) * .35f + turn * (.9f + .2f * R(i + 903)), rr0 = Radius * (1.3f + .5f * R(i + 901)) * outK;
                for (int j = 0; j < 3; j++)
                {
                    float ang = start - j * .16f, rr = rr0 * (1f + (R(i * 3 + j + 950) - .5f) * .12f);
                    float y = (R(i + 902) - .5f) * .35f * Radius + (R(i * 3 + j + 960) - .5f) * .2f * Radius;
                    var q = new Vector3(core.x + Mathf.Cos(ang) * rr, core.y + y, core.z + Mathf.Sin(ang) * rr);
                    if (Vector3.Dot(q - core, View) >= 0f != inFront) continue;
                    float size = (1.15f - .3f * j) * (.8f + .5f * R(i + 904)) * (1f + blow);
                    Sprite(Screen(q), size * 1.5f, size, Fade(CloudColour, (.34f - .08f * j) * a), puff, altitude + (i * 3 + j) * .00005f);
                }
            }
            for (int i = 0; i < 6; i++)
            {
                float a0 = i / 6f * Mathf.PI * 2f + turn * 1.15f + R(i + 930) * .8f, span = .35f + .2f * R(i + 931), rr = Radius * (1.35f + .35f * R(i + 932)) * outK;
                float yy = (R(i + 933) - .5f) * .3f * Radius, mid = a0 + span / 2f;
                if (Vector3.Dot(new Vector3(Mathf.Cos(mid) * rr, yy, Mathf.Sin(mid) * rr), View) >= 0f != inFront) continue;
                Vector2[] pts = GokuGraphics.Points(9);
                for (int k = 0; k <= 8; k++)
                {
                    float ang = a0 + span * k / 8f;
                    pts[k] = Screen(new Vector3(core.x + Mathf.Cos(ang) * rr, core.y + yy, core.z + Mathf.Sin(ang) * rr));
                }
                GokuGraphics.Line(pts, .3f, Fade(CloudColour, .13f * a), solid, altitude + .003f + i * .00005f, GokuGraphics.Taper.Both);
            }
        }

        /// <summary>Rocks falling off the ball's underside while it holds, one about every DribbleEvery seconds; each lands under where it fell and stays.</summary>
        private void Dribble(float s, float top, float floor)
        {
            float shadows = AltitudeLayer.Shadows.AltitudeFor();
            for (int k = 0; ; k++)
            {
                float te = Formed + .3f + k * DribbleEvery + R(k + 1300) * .2f;
                if (te >= Burst || s < te) break;
                float a = R(k + 1301) * Mathf.PI * 2f, o = .6f * R(k + 1302);
                Vector3 n = new Vector3(Mathf.Cos(a) * o, -1f, Mathf.Sin(a) * o).normalized, p0 = core + n * (Radius * .95f);
                float tf = Mathf.Sqrt(2f * p0.y / Gravity), age = s - te, size = .09f + .09f * R(k + 1303);
                if (age < tf)
                {
                    var q = new Vector3(p0.x, p0.y - .5f * Gravity * age * age, p0.z);
                    Sprite(new Vector2(q.x + ShadowPerCell.x * q.y, q.z + ShadowPerCell.y * q.y), size * 1.6f, size, Fade(Color.black, .2f), soft, shadows);
                    PaperBombGraphics.Rock(Screen(q), size, age * 220f, 1f, k, top + .0445f);
                }
                else
                {
                    float la = age - tf;
                    PaperBombGraphics.Rock(new Vector2(p0.x, p0.z), size, tf * 220f, 1f, k, floor + .031f + (k % 9) * .0003f);
                    if (la < .35f) Sprite(new Vector2(p0.x, p0.z + la * .3f), .25f + la, .2f + la * .7f, Fade(Dust, .4f * (1f - la / .35f)), puff, top + .006f);
                }
            }
        }

        /// <summary>Chips of rock spat out of the main cracks as they open: thrown out, they fall and stay on the crater floor.</summary>
        private void Chips(float s, float top, float floor)
        {
            int mains = 0;
            foreach (Fracture f in fractures) if (f.main) mains++;
            if (mains == 0) return;
            for (int i = 0; i < 18; i++)
            {
                Fracture cr = null;
                for (int k = 0, seen = 0; k < fractures.Count; k++)
                    if (fractures[k].main && seen++ == i % mains) { cr = fractures[k]; break; }
                int at = 1 + i * 3 % (cr.pts.Length - 1);
                float te = Crack + CrackTime * (.25f + .7f * R(i + 1900));
                if (s < te) continue;
                Vector3 n = RotY(cr.pts[at], Spin * (te - Crack)), p0 = core + n * (Radius * 1.02f);
                float sp = 1.4f + 1.6f * R(i + 1901);
                var v = new Vector3(n.x * sp, n.y * sp + 1.6f, n.z * sp);
                float tl = (v.y + Mathf.Sqrt(v.y * v.y + 2f * Gravity * p0.y)) / Gravity, age = s - te, size = .07f + .07f * R(i + 1902);
                if (age < tl)
                {
                    var q = new Vector3(p0.x + v.x * age, p0.y + v.y * age - .5f * Gravity * age * age, p0.z + v.z * age);
                    PaperBombGraphics.Rock(Screen(q), size, age * 400f, 1f, i, Vector3.Dot(n, View) >= 0f ? top + .0695f : top + .0445f);
                }
                else PaperBombGraphics.Rock(new Vector2(p0.x + v.x * tl, p0.z + v.z * tl), size, tl * 400f, 1f, i, floor + .0305f + (i % 7) * .0003f);
            }
        }

        /// <summary>
        /// One crack at <paramref name="grow"/> (0..1, how far the cracks have run) and <paramref name="open"/> (0..1, how wide
        /// they have opened): a dark lip, and once it opens the core's warm light inside it, narrower than the lip (and never
        /// white), with a faint glow. Thick where it started, a hairline at its tip; only the parts facing the camera.
        /// </summary>
        private static void DrawFracture(Fracture cr, Vector3 centre, float r, float rot, float grow, float open, float altitude)
        {
            float f = Mathf.Clamp01((grow - cr.t0) / (cr.t1 - cr.t0));
            if (f <= 0f) return;
            int n = cr.pts.Length - 1, count = 0;
            float reach = Mathf.Max(.05f, f * n);
            int last = Mathf.CeilToInt(reach);
            for (int k = 0; k <= last; k++)
            {
                Vector3 d = cr.pts[Mathf.Min(k, n)];
                if (k > reach) d = Vector3.Lerp(cr.pts[k - 1], cr.pts[Mathf.Min(k, n)], reach - (k - 1)).normalized;
                d = RotY(d, rot);
                if (Vector3.Dot(d, View) > .04f)
                {
                    runPoints[count] = Screen(centre + d * (r * 1.02f));
                    runShares[count] = Mathf.Min(k, reach) / reach;
                    count++;
                }
                else
                {
                    DrawCrackRun(count, cr.w, open, altitude);
                    count = 0;
                }
            }
            DrawCrackRun(count, cr.w, open, altitude);
        }

        private static void DrawCrackRun(int count, float w, float open, float altitude)
        {
            if (count < 2) return;
            for (int i = 0; i < count; i++) runHalf[i] = Mathf.Pow(1f - runShares[i], .6f) * w;
            TaperStrip(count, .01f, .022f + .06f * open, Fade(CrackColour, .92f), solid, altitude);
            if (open <= 0f) return;
            TaperStrip(count, 0f, .03f + .05f * open, Fade(Seam, .14f * open), VfxDraw.whiteGlow, altitude + .0001f);
            TaperStrip(count, 0f, .008f + .03f * open, Fade(Color.Lerp(Seam, SunPale, .55f * open), Mathf.Min(1f, open * 1.6f)), VfxDraw.whiteGlow, altitude + .0002f);
        }

        /// <summary>A strip along the run's points, half-width <paramref name="base"/> + <paramref name="scale"/> times each point's profile.</summary>
        private static void TaperStrip(int count, float @base, float scale, Color colour, Material material, float altitude)
        {
            VfxDraw.Sides(count, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i < count; i++)
            {
                Vector2 d = runPoints[Mathf.Min(count - 1, i + 1)] - runPoints[Mathf.Max(0, i - 1)];
                float len = d.magnitude, w = @base + scale * runHalf[i];
                if (len < 1e-5f) len = 1f;
                var normal = new Vector2(-d.y / len, d.x / len);
                a[i] = runPoints[i] + normal * w;
                b[i] = runPoints[i] - normal * w;
            }
            VfxDraw.Strip(a, b, colour, material, altitude);
        }

        /// <summary>The burst: rocks thrown out from every other surface slot, landing and staying; dust; a warm flash; a ring of dust rolling out.</summary>
        private void DrawBurst(float age, Vector2 C, float top, float floor, bool live)
        {
            for (int m = 0; m < slots; m += 2)
            {
                Vector3 land = Chunk(m, out Vector3 p0, out Vector3 v, out float size);
                float tl = land.z, turn = R(m * 13 + 3) * 360f, dir = m / 2 % 2 == 0 ? 1f : -1f;
                if (age < tl)
                {
                    var q = new Vector3(p0.x + v.x * age, p0.y + v.y * age - .5f * Gravity * age * age, p0.z + v.z * age);
                    Sprite(new Vector2(q.x + ShadowPerCell.x * q.y, q.z + ShadowPerCell.y * q.y), size * 1.2f, size * .8f, Fade(Color.black, .26f / (1f + q.y * .3f)), soft, AltitudeLayer.Shadows.AltitudeFor());
                    PaperBombGraphics.Rock(Screen(q), size, turn + age * 300f * dir, 1f, m, top + .12f + m * .0003f);
                }
                else
                {
                    var at = new Vector2(land.x, land.y);
                    // In the live ball the biggest rocks are real chunk items once they land; the game draws those.
                    if (!(live && realChunks.Contains(m))) PaperBombGraphics.Rock(at, size, turn + tl * 300f * dir, 1f, m, floor + .03f + (m % 7) * .0006f);
                    float la = age - tl;
                    if (m % 3 == 0 && la < .5f) Sprite(new Vector2(at.x, at.y + la * .3f), .35f + la, .3f + la * .8f, Fade(Dust, .45f * (1f - la / .5f)), puff, top + .006f);
                }
            }
            if (age < .12f)
            {
                float k = 1f - age / .12f;
                Sprite(C, Radius * 4.5f, Radius * 4.5f * Squash, Fade(SunPale, .85f * k), glow, top + .15f);
                Sprite(C, Radius * 2f, Radius * 2f * Squash, Fade(SunWhite, .9f * k), glow, top + .1502f);
            }
            if (age < .45f) Sprite(C, Radius * 3f, Radius * 3f * Squash, Fade(SunWarm, .3f * (1f - age / .45f)), glow, top + .1504f);
            for (int i = 0; i < 18; i++)
            {
                float life = 1f + R(i + 700) * .4f, u = age / life;
                if (u >= 1f) continue;
                float a = R(i + 701) * Mathf.PI * 2f, d = Radius * (.15f + .75f * R(i + 702)) * (1f + .7f * EaseOut(u));
                Sprite(new Vector2(C.x + Mathf.Cos(a) * d, C.y + Mathf.Sin(a) * d * Squash - u * .6f), 1.1f + u * 1.5f, .95f + u * 1.3f,
                    Fade(Dust, .5f * (1f - u) * Mathf.Clamp01(u * 12f)), puff, top + .14f + i * .0001f);
            }
            // A ring of dust rolling out along the ground from under the ball.
            float ringU = Mathf.Clamp01((age - .05f) / 1.3f);
            if (ringU > 0f && ringU < 1f)
                for (int i = 0; i < 22; i++)
                {
                    float a = i / 22f * Mathf.PI * 2f + R(i + 1400) * .25f, d = Mathf.Lerp(.8f, ground.radius + .6f, EaseOut(ringU));
                    Sprite(new Vector2(middle.x + Mathf.Cos(a) * d, middle.y + Mathf.Sin(a) * d + ringU * .25f), .9f + 1.2f * ringU, .7f + .9f * ringU,
                        Fade(Dust, .42f * (1f - ringU) * Mathf.Clamp01(ringU * 10f)), puff, top + .0055f + i * .00005f);
                }
            for (int i = 0; i < 6; i++)
            {
                float u = Mathf.Clamp01((age - .3f) / 2.2f);
                if (u <= 0f || u >= 1f) continue;
                float a = i * 1.05f + .3f, d = Radius * (.4f + u * .8f);
                Sprite(new Vector2(middle.x + Mathf.Cos(a) * d, middle.y + Mathf.Sin(a) * d * .7f + u * .4f), 1.6f + u * 1.4f, 1.2f + u * 1.1f,
                    Fade(Dust, .32f * Mathf.Sin(u * Mathf.PI)), puff, top + .005f + i * .0001f);
            }
        }

        /// <summary>Chunk <paramref name="m"/>: its start, velocity and size; returns (land x, land z, seconds to land).</summary>
        private Vector3 Chunk(int m, out Vector3 p0, out Vector3 v, out float size)
        {
            Vector3 n = RotY(slotDir[m], Spin * (Burst - Pull));
            float sp = 1.6f + 1.8f * R(m * 13 + 1);
            p0 = core + n * Radius;
            v = new Vector3(n.x * sp, n.y * sp + 1.4f, n.z * sp);
            size = ChunkSize(m);
            float tl = (v.y + Mathf.Sqrt(v.y * v.y + 2f * Gravity * p0.y)) / Gravity;
            return new Vector3(p0.x + v.x * tl, p0.z + v.z * tl, tl);
        }

        /// <summary>Faint dark streaks from all over the area into the core.</summary>
        private void Inflow(float s, Vector2 C, float alpha, float altitude)
        {
            if (alpha <= 0f) return;
            for (int i = 0; i < 22; i++)
            {
                float a = R(i + 950) * Mathf.PI * 2f, r0 = ground.radius * (.35f + .65f * R(i + 951));
                Vector2 from = middle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r0;
                float ph = (s * 1.3f + R(i + 952)) % 1f, fade = alpha * Mathf.Sin(ph * Mathf.PI);
                Vector2 a0 = Vector2.Lerp(from, C, Mathf.Max(0f, ph - .22f)), a1 = Vector2.Lerp(from, C, ph);
                SoftSegment(a0, a1, .16f, Fade(Core, .07f * fade), altitude);
                SoftSegment(a0, a1, .04f, Fade(Core, .25f * fade), altitude + .0003f);
            }
        }

        private static void RimCrack(Vector2 at, float amount, int seed, float altitude)
        {
            if (amount <= 0f) return;
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + R(i + seed + 400) * .6f, len = (.18f + R(i + seed + 410) * .2f) * amount;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Segment(at + dir * .1f, at + dir * (.1f + len), .03f, Fade(CrackColour, .6f * amount), altitude);
            }
            Sprite(at, .5f * amount, .32f * amount, Fade(CrackColour, .35f * amount), soft, altitude - .0005f);
        }

        // ---- the ball's cracks, boulders and directions (built once) -----------------------------------------

        private static Vector3 TurnAbout(Vector3 d, Vector3 n, float a) => (d * Mathf.Cos(a) + Vector3.Cross(n, d) * Mathf.Sin(a)).normalized;

        /// <summary>A crack from <paramref name="p"/> heading <paramref name="d"/> across the unit sphere: straight pieces with kinks of up to 32 degrees.</summary>
        private static void CrackWalk(Vector3 p, Vector3 d, int segs, int seed, List<Vector3> pts, List<Vector3> dirs)
        {
            pts.Add(p);
            dirs.Add(d);
            for (int k = 0; k < segs; k++)
            {
                d = TurnAbout(d, p, (R(seed + k * 7) - .5f) * 1.1f);
                float step = .15f + .1f * R(seed + k * 7 + 3);
                Vector3 q = (p * Mathf.Cos(step) + d * Mathf.Sin(step)).normalized;
                d = (d * Mathf.Cos(step) - p * Mathf.Sin(step)).normalized;     // the heading carried along the sphere
                p = q;
                pts.Add(p);
                dirs.Add(d);
            }
        }

        /// <summary>
        /// The ball's cracks, in its own frame as it faces the camera when the crack phase starts (they turn with it): three
        /// break points on the camera side, 4 or 5 cracks from each spread round it like a star fracture, side branches that
        /// split off at the second and fourth kinks and stop. The break points start one after another. The sketch's layout.
        /// </summary>
        private static List<Fracture> Fractures()
        {
            var list = new List<Fracture>();
            var centres = new List<Vector3>();
            for (int j = 0; centres.Count < 3 && j < 400; j++)
            {
                Vector3 p = new Vector3(R(j * 5 + 1650) - .5f, R(j * 5 + 1651) - .5f, R(j * 5 + 1652) - .5f).normalized;
                if (Vector3.Dot(p, View) <= .55f) continue;
                bool apart = true;
                foreach (Vector3 c in centres) if (Vector3.Dot(c, p) >= .8f) apart = false;
                if (apart) centres.Add(p);
            }
            for (int c = 0; c < centres.Count; c++)
            {
                Vector3 p = centres[c], reference = Vector3.Cross(p, Mathf.Abs(p.y) > .9f ? Vector3.right : Up).normalized;
                int count = 4 + c % 2;
                float start = c * .18f;
                for (int e = 0; e < count; e++)
                {
                    int i = c * 10 + e, segs = 4 + Mathf.FloorToInt(R(i + 1660) * 3f);
                    Vector3 d0 = TurnAbout(reference, p, (e + (R(i + 1663) - .5f) * .5f) / count * Mathf.PI * 2f);
                    float t0 = start + .08f * R(i + 1661), t1 = Mathf.Min(1f, t0 + .45f + .15f * R(i + 1662));
                    var pts = new List<Vector3>();
                    var dirs = new List<Vector3>();
                    CrackWalk(p, d0, segs, 1700 + i * 50, pts, dirs);
                    list.Add(new Fracture { pts = pts.ToArray(), t0 = t0, t1 = t1, w = 1f, main = true });
                    foreach (int k in new[] { 2, 4 })
                    {
                        if (k >= segs || R(i * 9 + k + 1800) < .35f) continue;
                        float side = R(i * 9 + k + 1801) < .5f ? -1f : 1f;
                        Vector3 bd = TurnAbout(dirs[k], pts[k], side * (.7f + .5f * R(i * 9 + k + 1802)));
                        var bpts = new List<Vector3>();
                        CrackWalk(pts[k], bd, 1 + Mathf.FloorToInt(R(i * 9 + k + 1803) * 2f), 2000 + i * 40 + k * 5, bpts, new List<Vector3>());
                        float bt0 = t0 + (t1 - t0) * k / segs;
                        list.Add(new Fracture { pts = bpts.ToArray(), t0 = bt0, t1 = Mathf.Min(1f, bt0 + .2f), w = .55f, main = false });
                    }
                }
            }
            return list;
        }

        /// <summary>Six irregular outlines of 5 to 7 corners, about 1 cell across: the boulders set into the ball.</summary>
        private static Mesh[] BoulderShapes()
        {
            var shapes = new Mesh[6];
            for (int v = 0; v < 6; v++)
            {
                int n = 5 + v % 3;
                var vertices = new Vector3[n + 1];
                var triangles = new int[n * 3];
                for (int i = 0; i < n; i++)
                {
                    float a = (i + (R(v * 13 + i + 70) - .5f) * .8f) / n * Mathf.PI * 2f, rr = .5f * (.55f + .45f * R(v * 29 + i + 90)) * (i % 2 == 1 ? .9f : 1.05f);
                    vertices[i + 1] = new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = 1 + i;
                    triangles[i * 3 + 2] = 1 + (i + 1) % n;
                }
                shapes[v] = new Mesh { name = "RimArt Chibaku boulder " + v, vertices = vertices, triangles = DoubleSided(triangles), uv = new Vector2[n + 1] };
                shapes[v].RecalculateBounds();
            }
            return shapes;
        }

        /// <summary><paramref name="count"/> directions on a Fibonacci sphere, the spiral turned by <paramref name="turn"/> radians.</summary>
        private static Vector3[] Fibonacci(int count, float turn)
        {
            var dirs = new Vector3[count];
            for (int m = 0; m < count; m++)
            {
                float y = 1f - 2f * (m + .5f) / count, rr = Mathf.Sqrt(1f - y * y), ph = m * 2.399963f + turn;
                dirs[m] = new Vector3(rr * Mathf.Cos(ph), y, rr * Mathf.Sin(ph));
            }
            return dirs;
        }

        // ---- meshes and draw helpers --------------------------------------------------------------------

        /// <summary>A ring from <paramref name="inner"/> to 1 (a disc when inner is 0), radius 1, in the x-z plane.</summary>
        private static Mesh Ring(float inner, float outer, int segments)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f, c = Mathf.Cos(a), s = Mathf.Sin(a);
                vertices.Add(new Vector3(c * inner, 0f, s * inner));
                vertices.Add(new Vector3(c * outer, 0f, s * outer));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                triangles.AddRange(new[] { a, a + 3, a + 1, a, a + 2, a + 3 });
            }
            var mesh = new Mesh { name = "RimArt Chibaku ring" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(DoubleSided(triangles.ToArray()), 0);
            mesh.SetUVs(0, new List<Vector2>(new Vector2[vertices.Count]));
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The lower edge of a unit circle, from 200 to 340 degrees (south), <paramref name="thick"/> at its thickest in the middle: the rim light.</summary>
        private static Mesh Arc(float thick)
        {
            const int n = 28;
            var vertices = new Vector3[(n + 1) * 2];
            var triangles = new int[n * 6];
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, a = (200f + 140f * u) * Mathf.Deg2Rad, w = thick * Mathf.Sin(u * Mathf.PI), c = Mathf.Cos(a), s = Mathf.Sin(a);
                vertices[i * 2] = new Vector3(c * (1f - w), 0f, s * (1f - w));
                vertices[i * 2 + 1] = new Vector3(c * (1f + w * .35f), 0f, s * (1f + w * .35f));
                if (i == 0) continue;
                int k = i * 2, t = (i - 1) * 6;
                triangles[t] = k - 2; triangles[t + 1] = k; triangles[t + 2] = k - 1;
                triangles[t + 3] = k - 1; triangles[t + 4] = k; triangles[t + 5] = k + 1;
            }
            var mesh = new Mesh { name = "RimArt Chibaku rim arc", vertices = vertices, triangles = DoubleSided(triangles), uv = new Vector2[vertices.Length] };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A band along a plate's outline, <paramref name="width"/> wide (its crack before it heaves), relative to its centre.</summary>
        private static Mesh OutlineBand(ChibakuPlate plate, float width)
        {
            int n = plate.outline.Length;
            var vertices = new Vector3[n * 2];
            var triangles = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                Vector2 d = plate.outline[i] - plate.centre;
                float length = Mathf.Max(1e-4f, d.magnitude);
                Vector2 o = d * (1f + width / 2f / length), inner = d * Mathf.Max(.1f, 1f - width / 2f / length);
                vertices[i * 2] = new Vector3(inner.x, 0f, inner.y);
                vertices[i * 2 + 1] = new Vector3(o.x, 0f, o.y);
                int a = i * 2, b = (i + 1) % n * 2;
                triangles[i * 6] = a; triangles[i * 6 + 1] = b + 1; triangles[i * 6 + 2] = a + 1;
                triangles[i * 6 + 3] = a; triangles[i * 6 + 4] = b; triangles[i * 6 + 5] = b + 1;
            }
            var mesh = new Mesh { name = "RimArt Chibaku crack", vertices = vertices, triangles = DoubleSided(triangles), uv = new Vector2[n * 2] };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A seven-vertex fan whose corners are set every frame; with UVs into the picture round <paramref name="source"/>'s centre.</summary>
        private Mesh PatchMesh(ChibakuPlate source)
        {
            var uv = new Vector2[7];
            if (source != null)
            {
                uv[0] = ground.UV(source.centre);
                for (int k = 0; k < 6; k++)
                {
                    float a = k / 6f * Mathf.PI * 2f;
                    uv[k + 1] = ground.UV(source.centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * PatchR);
                }
            }
            var triangles = new int[18];
            for (int k = 0; k < 6; k++)
            {
                triangles[k * 3] = 0;
                triangles[k * 3 + 1] = 1 + k;
                triangles[k * 3 + 2] = 1 + (k + 1) % 6;
            }
            var mesh = new Mesh { name = "RimArt Chibaku ball plate", vertices = new Vector3[7], uv = uv, triangles = DoubleSided(triangles) };
            mesh.MarkDynamic();
            return mesh;
        }

        /// <summary>The triangles again with the other winding, so a patch shows whichever way its corners turn on screen.</summary>
        private static int[] DoubleSided(int[] triangles)
        {
            var both = new int[triangles.Length * 2];
            triangles.CopyTo(both, 0);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                both[triangles.Length + i] = triangles[i];
                both[triangles.Length + i + 1] = triangles[i + 2];
                both[triangles.Length + i + 2] = triangles[i + 1];
            }
            return both;
        }

        private static Vector2 Screen(Vector3 q) => new Vector2(q.x, q.z + q.y * Lift);

        private static Vector3 RotY(Vector3 n, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector3(n.x * c - n.z * s, n.y, n.x * s + n.z * c);
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static float EaseOut(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f);

        private static float Bump(float x) => x >= 0f && x <= 1f ? Mathf.Sin(x * Mathf.PI) : 0f;

        private static Color Fade(Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * alpha);

        /// <summary>An opaque tint: the captured ground at <paramref name="k"/> of its brightness.</summary>
        private static Color Grey(float k) => new Color(k, k, k, 1f);

        private static void DrawShaped(Mesh mesh, Vector3 at, Matrix4x4 shape, Color colour, Material material)
        {
            if (colour.a <= .001f) return;
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(mesh, Matrix4x4.Translate(at) * shape, material, 0, null, 0, properties);
        }

        private static void Solid(Mesh mesh, Vector2 at, float altitude, float sx, float sz, float degrees, Color colour)
        {
            if (colour.a <= .001f) return;
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(new Vector3(at.x, altitude, at.y), Quaternion.Euler(0f, degrees, 0f), new Vector3(sx, 1f, sz)), solid, 0, null, 0, properties);
        }

        private static void Sprite(Vector2 at, float w, float h, Color colour, Material material, float altitude)
        {
            if (colour.a <= .001f || w <= 0f || h <= 0f) return;
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(new Vector3(at.x, altitude, at.y), Quaternion.identity, new Vector3(w, 1f, h)), material, 0, null, 0, properties);
        }

        /// <summary>A straight line from a to b as a quad.</summary>
        private static void Segment(Vector2 a, Vector2 b, float width, Color colour, float altitude)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 1e-4f || colour.a <= .001f) return;
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Vector2 mid = (a + b) / 2f;
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(new Vector3(mid.x, altitude, mid.y), Quaternion.Euler(0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 0f),
                new Vector3(length, 1f, width)), solid, 0, null, 0, properties);
        }

        /// <summary>A soft line from a to b: the soft disc stretched along it.</summary>
        private static void SoftSegment(Vector2 a, Vector2 b, float width, Color colour, float altitude)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 1e-4f || colour.a <= .001f) return;
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Vector2 mid = (a + b) / 2f;
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(new Vector3(mid.x, altitude, mid.y), Quaternion.Euler(0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 0f),
                new Vector3(length + width, 1f, width * 2f)), soft, 0, null, 0, properties);
        }

        public void Dispose()
        {
            foreach (Flight f in caught) UnityEngine.Object.Destroy(f.crack);
            foreach (Flight f in kept) UnityEngine.Object.Destroy(f.crack);
            for (int m = 0; m < slots; m++)
            {
                UnityEngine.Object.Destroy(slotFace[m]);
                UnityEngine.Object.Destroy(slotEdge[m]);
            }
            caught.Clear();
            kept.Clear();
        }
    }
}
