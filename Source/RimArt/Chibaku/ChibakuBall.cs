using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Chibaku Tensei's ball, drawn from a <see cref="ChibakuGround"/> capture (test stage: the picture only, no
    /// pawns and no rules; ported from Tools/VfxLab/web/sketches/pain-chibaku-tensei.js with its defaults).
    ///
    /// Order: the core appears 5 cells over the cell with a soft flash and a dark circle spreads to the true
    /// radius; for 3 s cracks run out, plates heave, tear free (inner first) and tumble into the core, leaving a
    /// crater; the ball grows from what arrives (radius 0.3 to 1 times 2 cells, by the cube root of the share
    /// arrived); it holds, turning 14 degrees a second and squeezing once a second; seams run round it for
    /// 0.4 s; it bursts into rocks that land and stay with the crater.
    ///
    /// Drawing: the ball is a sphere under the kit's height rule (screen z = z + 0.6 h), so its outline is an
    /// ellipse 1.166 times taller than wide. Its surface is up to 84 plates on a Fibonacci sphere, each a
    /// six-cornered patch of the captured ground (taken round the centre of the ground plate that filled it),
    /// rebuilt each frame on the side facing the camera, lit by a fixed sun, over a dark body with a darker
    /// limb. Flying plates are the ground plates' own meshes under one matrix (spin, a squash along their axis
    /// to turn over, shrink); upside down they show earth or stone. Things in the air are split into behind
    /// and in front of the ball by their depth along the view direction.
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class ChibakuBall : IDisposable
    {
        public const float Radius = 2f, DefaultHeight = 5f, PullSeconds = 3f, HoldSeconds = 12f, Gravity = 12f;
        public const float Pulse = .25f, CrackRun = .45f, CrackTime = .4f, Tail = 3f;
        public const float Pull = Pulse, Formed = Pull + PullSeconds, Crack = Formed + HoldSeconds, Burst = Crack + CrackTime, End = Burst + Tail;
        private const float Spin = 14f * Mathf.Deg2Rad, HeaveH = .18f, CoreR = .34f, PatchR = .45f;
        private const float Lift = SixPathsHeight.Lift;
        private const int MaxSlots = 84;

        private static readonly float Squash = Mathf.Sqrt(1f + Lift * Lift);
        private static readonly Vector3 View = new Vector3(0f, 1f, -Lift).normalized, Sun = new Vector3(.45f, 1f, .32f).normalized,
            Fill = new Vector3(-.35f, .5f, -.8f).normalized, Up = Vector3.up;
        private static readonly Vector2 ShadowPerCell = new Vector2(-.45f, -.32f);
        private static readonly Color Core = new Color(.015f, .015f, .035f), PaleBlue = new Color(.85f, .94f, 1f), Dust = new Color(.76f, .70f, .59f),
            BallBody = new Color(.08f, .06f, .04f), SideColour = new Color(.13f, .09f, .06f), SlabEdge = new Color(.07f, .05f, .035f),
            CrackColour = new Color(.06f, .04f, .03f), Rib = new Color(.31f, .24f, .16f), HoleRim = new Color(.24f, .18f, .12f), HoleDeep = new Color(.11f, .08f, .05f),
            EarthDark = new Color(.19f, .13f, .08f), EarthLit = new Color(.46f, .34f, .22f), StoneDark = new Color(.22f, .21f, .2f), StoneLit = new Color(.56f, .54f, .5f);
        private static readonly Material solid = new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex, name = "RimArt Chibaku ball solid" };
        private static readonly Material soft = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.Transparent);
        private static readonly Material glow = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.MoteGlow);
        private static readonly Material puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);
        private static readonly Mesh disc = Ring(0f, 1f, 64), limb = Ring(.8f, 1f, 72);
        private static readonly Mesh[] edgeBands = { Ring(.97f, 1f, 96), Ring(.93f, 1f, 96), Ring(.89f, 1f, 96), Ring(.84f, 1f, 96), Ring(.79f, 1f, 96), Ring(.73f, 1f, 96) };
        private static readonly float[] edgeAlphas = { .4f, .2f, .13f, .1f, .07f, .05f };
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

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
        private readonly List<int> front = new List<int>(), back = new List<int>();
        private readonly Vector3[] faceVerts = new Vector3[7], edgeVerts = new Vector3[7];

        /// <summary>The ball's centre above the ground, in cells (drawn 0.6 cells north per cell up).</summary>
        public readonly float height;

        /// <summary>Seconds a pawn or item takes to fall out of the ball to the ground.</summary>
        public float FallTime => Mathf.Sqrt(2f * (height - .3f) / Gravity);

        public ChibakuBall(ChibakuGround ground, float height = DefaultHeight)
        {
            this.ground = ground;
            this.height = Mathf.Max(height, Radius + .4f);
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
            slotDir = new Vector3[slots];
            var order = new List<int>();
            for (int m = 0; m < slots; m++)
            {
                float y = 1f - 2f * (m + .5f) / slots, rr = Mathf.Sqrt(1f - y * y), ph = m * 2.399963f;
                slotDir[m] = new Vector3(rr * Mathf.Cos(ph), y, rr * Mathf.Sin(ph));
                order.Add(m);
            }
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
            // The biggest of the thrown rocks become real chunks where they land: one for every 16 plates pulled, 6 to 10.
            int real = Mathf.Clamp(Mathf.RoundToInt(caught.Count / 16f), 6, 10);
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

        private static float Squeeze(float s) => s >= Formed && s < Crack ? Bump(((s - Formed) % 1f) / .18f) : 0f;

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

        // ---- drawing --------------------------------------------------------------------------------------

        public void Draw(float s, List<ChibakuHeld> pawns = null)
        {
            if (s < 0f || s >= End || ground.texture == null) return;
            float top = AltitudeLayer.MoteOverhead.AltitudeFor(), floor = AltitudeLayer.Filth.AltitudeFor();
            float spin = Spin * Mathf.Max(0f, s - Pull), r = BallRadiusAt(s);
            int arrived = Arrived(s);
            float frac = caught.Count > 0 ? arrived / (float)caught.Count : 1f;
            Vector2 C = Screen(core);

            DrawFloor(s, floor, spin);

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
            int behind = 0;
            for (int k = 0; k < air.Count; k++)
            {
                Airborne a = air[k];
                if (a.nd >= 0f) continue;
                if (a.pawn != null) PawnPicture(a, top + .005f + behind++ * .0002f);
                else Slab(a.flight, a.pos, a.age * a.flight.spinRate, .3f + a.age * a.flight.tumbleRate, Mathf.Lerp(1f, .6f, a.u), top + .005f + behind++ * .0002f);
            }

            // The core and the ball growing round it.
            if (s < Formed + .2f)
            {
                float grown = Mathf.Lerp(.65f, 1f, Smooth(s / Pulse)), glowA = 1f - Smooth(frac);
                Sprite(C, CoreR * grown * 5.6f, CoreR * grown * 5.6f, Fade(PaleBlue, .35f * glowA), glow, top + .04f);
                Sprite(C, CoreR * grown * 3.4f, CoreR * grown * 3.4f, Fade(PaleBlue, .8f * glowA), glow, top + .0403f);
                Sprite(C, CoreR * grown * 2.5f, CoreR * grown * 2.5f, Fade(PaleBlue, .9f * glowA), glow, top + .0406f);
                Solid(disc, C, top + .041f, CoreR * grown, CoreR * grown, 0f, Core);
                if (s < .2f)
                {
                    float u = s / .2f, size = Mathf.Lerp(1f, 3.2f, EaseOut(u));
                    Sprite(C, size, size, Fade(PaleBlue, .55f * (1f - u)), glow, top + .0412f);
                }
            }
            if (s >= Pull && s < Burst) DrawBall(s, spin, r, s < Formed ? Smooth(Mathf.Clamp01(frac * 1.6f)) : 1f, C, top);
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
                else Slab(a.flight, a.pos, a.age * a.flight.spinRate, .3f + a.age * a.flight.tumbleRate, Mathf.Lerp(1f, .6f, a.u), top + .075f + ahead++ * .0002f);
            }
            if (s >= Pull && s < Formed) Inflow(s, C, Mathf.Clamp01((s - Pull) / .2f) * (1f - Smooth((frac - .7f) / .3f)), top + .11f);
            if (s >= Burst) DrawBurst(s - Burst, C, top, floor, pawns != null);
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

        /// <summary>The area circle, cracks, holes, the crater's dark middle, rim cracks and rocks, heaving plates and their dust.</summary>
        private void DrawFloor(float s, float floor, float spin)
        {
            float radius = ground.radius;
            float spreadOut = EaseOut(s / .3f), fade = 1f - Mathf.Clamp01((s - Formed - .6f) / .6f);
            Solid(disc, middle, floor + .004f, radius * spreadOut, radius * spreadOut, 0f, Fade(Core, .1f * fade));
            for (int k = 0; k < edgeBands.Length; k++)
                Solid(edgeBands[k], middle, floor + .005f + k * .0002f, radius * spreadOut, radius * spreadOut, 0f, Fade(Core, edgeAlphas[k] * .6f * fade));

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
                // The whole plate's ground is gone: dark earth ribs where the edges were, the hole inside them.
                Solid(f.plate.face, f.plate.centre, floor + .0095f, 1f, 1f, 0f, Rib);
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
            if (bowl > 0f) Sprite(middle, radius * 1.7f, radius * 1.7f, Fade(Core, .3f * bowl), soft, floor + .011f);

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

        /// <summary>A plate as a solid: shadow, south side, edge, face (the captured ground, or earth/stone upside down).</summary>
        private void Slab(Flight f, Vector3 pos, float spinDegrees, float tumble, float scale, float altitude, float shadowAltitude = -1f)
        {
            float ct = Mathf.Cos(tumble), sq = Mathf.Max(.2f, Mathf.Abs(ct));
            Matrix4x4 shape = Matrix4x4.Rotate(Quaternion.Euler(0f, f.axis, 0f)) * Matrix4x4.Scale(new Vector3(sq * scale, 1f, scale))
                * Matrix4x4.Rotate(Quaternion.Euler(0f, spinDegrees - f.axis, 0f));
            if (shadowAltitude < 0f) shadowAltitude = AltitudeLayer.Shadows.AltitudeFor();
            float thick = f.thick * scale * (.35f + .65f * sq);
            DrawShaped(f.plate.face, new Vector3(pos.x + ShadowPerCell.x * pos.y, shadowAltitude, pos.z + ShadowPerCell.y * pos.y), shape, Fade(Color.black, .35f / (1f + pos.y * .25f)), solid);
            DrawShaped(f.plate.face, new Vector3(pos.x, altitude, pos.z + (pos.y - thick) * Lift), shape, SideColour, solid);
            DrawShaped(f.plate.edge, new Vector3(pos.x, altitude + .00005f, pos.z + pos.y * Lift), shape, SlabEdge, solid);
            if (ct >= 0f) DrawShaped(f.plate.face, new Vector3(pos.x, altitude + .0001f, pos.z + pos.y * Lift), shape, Grey(Mathf.Lerp(.7f, 1f, sq)), ground.FaceMaterial);
            else DrawShaped(f.plate.face, new Vector3(pos.x, altitude + .0001f, pos.z + pos.y * Lift), shape,
                Color.Lerp(f.stone ? StoneDark : EarthDark, f.stone ? StoneLit : EarthLit, .3f + .55f * sq), solid);
        }

        /// <summary>The ball: shadow, plates just past the limb, dark body, the plates facing the camera back to front, darker limb, seams.</summary>
        private void DrawBall(float s, float spin, float r, float bodyA, Vector2 C, float top)
        {
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
            for (int k = 0; k < back.Count; k++) SurfacePlate(back[k], spin, r, top + .046f + k * .0001f);
            Solid(disc, C, top + .056f, r * .98f, r * .98f * Squash, 0f, Fade(BallBody, bodyA));
            for (int k = 0; k < front.Count; k++) SurfacePlate(front[k], spin, r, top + .057f + k * .0001f);
            Solid(limb, C, top + .067f, r * 1.02f, r * 1.02f * Squash, 0f, Fade(Core, .32f * bodyA));

            float crackU = Mathf.Clamp01((s - Crack) / CrackTime);
            if (crackU <= 0f) return;
            for (int i = 0; i < 4; i++)
            {
                Vector3 g = RotY(new Vector3(R(i * 3 + 1) - .5f, R(i * 3 + 2) - .5f, R(i * 3 + 3) - .5f).normalized, spin);
                Vector3 e1 = Vector3.Cross(g, Mathf.Abs(g.y) > .9f ? Vector3.right : Up).normalized, e2 = Vector3.Cross(g, e1);
                float start = R(i + 40) * Mathf.PI * 2f, span = Mathf.Min(1f, crackU * 1.5f);
                Vector2? last = null;
                for (int k = 0; k <= 48; k++)
                {
                    float a = start + (k / 48f - .5f) * Mathf.PI * 2f * span;
                    Vector3 d = (e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a) + g * ((R(i * 60 + k) - .5f) * .09f)).normalized;
                    if (Vector3.Dot(d, View) <= .06f) { last = null; continue; }
                    Vector2 p = Screen(core + d * (r * 1.04f));
                    if (last.HasValue) Segment(last.Value, p, .03f + .06f * crackU, CrackColour, top + .068f);
                    last = p;
                }
            }
            for (int i = 0; i < 8; i++)
            {
                float u = ((s - Crack) * 2.5f + R(i + 600)) % 1f, a = R(i + 601) * Mathf.PI * 2f;
                Vector2 at = Screen(core + new Vector3(Mathf.Cos(a) * .8f, .3f + R(i + 602) * .5f, Mathf.Sin(a) * .8f - .4f) * r);
                Sprite(new Vector2(at.x, at.y + u * .3f), .3f + u * .5f, .26f + u * .4f, Fade(Dust, .5f * Mathf.Sin(u * Mathf.PI)), puff, top + .069f);
            }
        }

        /// <summary>One surface plate: six corners round its slot direction, pushed out a little so the outline is lumpy.</summary>
        private void SurfacePlate(int m, float spin, float r, float altitude)
        {
            Vector3 n = RotY(slotDir[m], spin);
            Vector3 t1 = Vector3.Cross(n, Mathf.Abs(n.y) > .95f ? Vector3.right : Up).normalized, t2 = Vector3.Cross(n, t1);
            float outR = r * (1f + .07f * R(m * 5 + 1));
            Vector2 c = Screen(core + n * outR);
            faceVerts[0] = new Vector3(c.x, 0f, c.y);
            for (int k = 0; k < 6; k++)
            {
                float a = (k + (R(m * 17 + k) - .5f) * .5f) / 6f * Mathf.PI * 2f, ext = spread * (.8f + .45f * R(m * 23 + k));
                Vector2 p = Screen(core + (n + t1 * (Mathf.Cos(a) * ext) + t2 * (Mathf.Sin(a) * ext)).normalized * outR);
                faceVerts[k + 1] = new Vector3(p.x, 0f, p.y);
                Vector2 grown = p + (p - c).normalized * .035f;
                edgeVerts[k + 1] = new Vector3(grown.x, 0f, grown.y);
            }
            edgeVerts[0] = faceVerts[0];
            slotFace[m].vertices = faceVerts;
            slotFace[m].RecalculateBounds();
            slotEdge[m].vertices = edgeVerts;
            slotEdge[m].RecalculateBounds();
            float lit = Mathf.Clamp01(Mathf.Max(0f, Vector3.Dot(n, Sun)) * .95f + Mathf.Max(0f, Vector3.Dot(n, Fill)) * .25f + .02f) * (n.y < 0f ? 1f + n.y * .45f : 1f);
            DrawShaped(slotEdge[m], new Vector3(0f, altitude, 0f), Matrix4x4.identity, SlabEdge, solid);
            DrawShaped(slotFace[m], new Vector3(0f, altitude + .00005f, 0f), Matrix4x4.identity, Grey(Mathf.Lerp(.3f, 1.05f, lit)), ground.FaceMaterial);
        }

        /// <summary>The burst: rocks thrown out from every other surface slot, landing and staying; dust; a flash.</summary>
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
            if (age < .15f) Sprite(C, Radius * 3f, Radius * 3f * Squash, Fade(PaleBlue, .5f * (1f - age / .15f)), glow, top + .15f);
            for (int i = 0; i < 18; i++)
            {
                float life = 1f + R(i + 700) * .4f, u = age / life;
                if (u >= 1f) continue;
                float a = R(i + 701) * Mathf.PI * 2f, d = Radius * (.15f + .75f * R(i + 702)) * (1f + .7f * EaseOut(u));
                Sprite(new Vector2(C.x + Mathf.Cos(a) * d, C.y + Mathf.Sin(a) * d * Squash - u * .6f), 1.1f + u * 1.5f, .95f + u * 1.3f,
                    Fade(Dust, .5f * (1f - u) * Mathf.Clamp01(u * 12f)), puff, top + .14f + i * .0001f);
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

        /// <summary>Dark streaks from all over the area into the core.</summary>
        private void Inflow(float s, Vector2 C, float alpha, float altitude)
        {
            if (alpha <= 0f) return;
            for (int i = 0; i < 22; i++)
            {
                float a = R(i + 950) * Mathf.PI * 2f, r0 = ground.radius * (.35f + .65f * R(i + 951));
                Vector2 from = middle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r0;
                float ph = (s * 1.3f + R(i + 952)) % 1f, fade = alpha * Mathf.Sin(ph * Mathf.PI);
                Vector2 a0 = Vector2.Lerp(from, C, Mathf.Max(0f, ph - .22f)), a1 = Vector2.Lerp(from, C, ph);
                SoftSegment(a0, a1, .16f, Fade(Core, .1f * fade), altitude);
                SoftSegment(a0, a1, .04f, Fade(Core, .38f * fade), altitude + .0003f);
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
