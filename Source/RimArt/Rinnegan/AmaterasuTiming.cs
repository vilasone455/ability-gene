using UnityEngine;

namespace RimArt
{
    /// <summary>The light inside the black flame (the sketch's "Light inside the black"). Additive, so it only shows on the black.</summary>
    public enum AmaterasuLight
    {
        /// <summary>Storm 4, 1:07-1:09 of the reference clip: (0.50, 0.24, 0.95). The default.</summary>
        Violet,
        /// <summary>Shinobi Striker's jutsu: (0.85, 0.10, 0.12).</summary>
        Crimson,
        /// <summary>The anime: pure black, no streaks.</summary>
        None,
    }

    /// <summary>How a pawn caught the black flame. The picture of the first second differs.</summary>
    public enum AmaterasuCatch
    {
        /// <summary>
        /// Sasuke cast it on the pawn: a black mass bursts out of the chest (0.2-0.3 s) and throws 18 shards up and
        /// out, a soot splash rings the feet, 6 small flames pop up 0.6-1.05 cells round it, and the fire starts 1.45x
        /// as wide and half as tall and stretches into its column over 0.9 s.
        /// </summary>
        Cast,
        /// <summary>
        /// It caught from a burning neighbour: 3 flecks jump across in <see cref="AmaterasuTiming.JumpTime"/> s, then
        /// the parts catch from the touching side over 0.3 s. No burst.
        /// </summary>
        Spread,
        /// <summary>A burning kunai or the Fūma struck it: a small burst at the chest, and the parts catch from the chest outward.</summary>
        Hit,
        /// <summary>It stepped on a burning weapon or patch: the parts catch from the feet up over 0.35 s.</summary>
        Stepped,
    }

    /// <summary>
    /// Times and sizes of the Amaterasu picture, the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/rinnegan-amaterasu.js). Seconds in, numbers out, no drawing.
    ///
    /// None of this is balance: the burn time (20 s on a pawn and on the floor), damage, spread chance, warmup and
    /// the Bleeding eye are in the defs and read by the kit code. The pictures take their times as "seconds since X"
    /// from the game; <see cref="Warmup"/> is only the sketch's number, and the preview's.
    ///
    /// The second half of this class is the preview's script (DebugActions_AmaterasuPreview.cs): the sketch's showcase
    /// times and its held scenes laid out round the middle of the targets. The game does not use it.
    /// </summary>
    public static class AmaterasuTiming
    {
        // ---- The cast --------------------------------------------------------------------------------------

        /// <summary>The sketch's gaze: seconds from the start of the warmup to ignition (the AbilityDef's warmup).</summary>
        public const float Warmup = 0.5f;
        /// <summary>The red glint on the caster's eye goes out this long after ignition.</summary>
        public const float GlintFade = 0.08f;
        /// <summary>Camera shake at ignition (a cast on a pawn or on held weapons).</summary>
        public const float Shake = 0.075f;
        /// <summary>Screen dim at ignition: 0.18 black, in over 0.08 s, out by 0.18 s.</summary>
        public const float Dim = 0.18f, DimIn = 0.08f, DimLife = 0.18f;
        /// <summary>
        /// The cast's burst: the soot splash lasts 0.35 s, the black masses 0.2-0.3 s, the shards 0.32-0.57 s; all
        /// gone by 0.6 s. The 6 small ground flames are gone by 1.4 s.
        /// </summary>
        public const float EruptionLife = 0.6f, SplashLife = 0.35f, ClumpLife = 1.4f;
        public const int Masses = 5, Shards = 18, Clumps = 6;
        /// <summary>A cast fire starts <see cref="SwellWidth"/> wider and <see cref="SwellHeight"/> lower and reaches its column over <see cref="Swell"/> s.</summary>
        public const float Swell = 0.9f, SwellWidth = 0.45f, SwellHeight = 0.5f;
        /// <summary>The first blood streak runs to full length in 1.2 s; the second starts 0.3 s later.</summary>
        public const float BloodRun = 1.2f, SecondStreak = 0.3f;

        // ---- The fire on a pawn ----------------------------------------------------------------------------

        /// <summary>Half-width and height of the fire on a pawn of body size 1, cells.</summary>
        public const float Width = 0.55f, Height = 2.4f;
        /// <summary>The flame clock runs at 1x; each part catches over 0.12 s; flecks at 1x the counts below.</summary>
        public const float Speed = 1f, Rise = 0.12f, Flecks = 1f;
        public const int Strands = 26, BaseTongues = 5, CoreTongues = 3, Blotches = 8, LooseFlecks = 12;
        /// <summary>
        /// How long the catch creeps over the body: from the touching side on a spread (0.3 s), from the feet up on a
        /// step-in (0.35 s over the first 0.9 cells of height), from the chest out on a cast or a hit (0.08 s).
        /// </summary>
        public const float SpreadCreep = 0.3f, StepCreep = 0.35f, StepHeight = 0.9f, ChestCreep = 0.08f;
        /// <summary>The flecks jump across to a neighbour in this long before its fire shows.</summary>
        public const float JumpTime = 0.22f;
        /// <summary>The floor darkens in over this long.</summary>
        public const float StainIn = 0.25f;

        // ---- Burning weapons -------------------------------------------------------------------------------

        /// <summary>A held kunai's and the Fūma's flames grow in over these; the burst where one catches lasts 0.5 s.</summary>
        public const float KunaiCatch = 0.15f, FumaCatch = 0.2f, BurstLife = 0.5f;
        /// <summary>The preview's held kunai catch this far apart, one after another (the sketch's stagger; the kit lights them in one tick).</summary>
        public const float KunaiStagger = 0.05f;
        /// <summary>
        /// In flight: a kunai's flames are 0.7 as tall and lean back 1.3 cells per cell of height, the Fūma's 1.2; a
        /// kunai drags a black tail over the last 0.09 s of its path.
        /// </summary>
        public const float KunaiFlying = 0.7f, TailLean = 1.3f, FumaLean = 1.2f, TailTime = 0.09f;
        /// <summary>
        /// A flying weapon leaves one fleck in the air every 0.5 cells of its path. The sketch spread 8 (kunai) and 14
        /// (Fūma) over each whole flight, which the game does not know in advance; 0.5 cells gives 7, 7 and 9 on the
        /// sketch's kunai flights (3.4, 3.6 and 4.6 cells) and 14 on the Fūma's (7.4 cells).
        /// </summary>
        public const float FleckGap = 0.5f;
        /// <summary>Half the spread of a burning kunai's patch on the floor; the size its stain is drawn at.</summary>
        public const float KunaiPatch = 0.36f, KunaiStain = 0.45f, FumaStain = 0.75f;

        // ---- Going out -------------------------------------------------------------------------------------

        /// <summary>After Release (or when its time runs out) the flames sink over 0.3 s and grey smoke rises for 0.8 s.</summary>
        public const float Sink = 0.3f, SmokeLife = 0.8f;
        /// <summary>
        /// Keep calling a fire's picture this long after it went out: the smoke is gone at 0.8 s and the last loose
        /// fleck (period up to 0.95 s) at 0.95 s.
        /// </summary>
        public const float OutDuration = 1f;
        /// <summary>
        /// The stain on the floor holds for 15 s after the fire went out and fades over 5 s more. The sketch keeps it
        /// to its end ("the scorch stays"); these two are the port's.
        /// </summary>
        public const float StainHold = 15f, StainFade = 5f, StainDuration = StainHold + StainFade;

        /// <summary>
        /// The age to pass to <see cref="AmaterasuGraphics.Pawn"/> and <see cref="AmaterasuGraphics.PawnStain"/>:
        /// <paramref name="sinceCaught"/> seconds after the hediff was added, less <see cref="JumpTime"/> for a
        /// spread, whose flecks jump across first.
        /// </summary>
        public static float FireAge(float sinceCaught, AmaterasuCatch how) =>
            how == AmaterasuCatch.Spread ? sinceCaught - JumpTime : sinceCaught;

        // ---- The preview's script: the sketch's showcase ---------------------------------------------------

        /// <summary>The showcase: the caster stands 6 cells west; release 3.5 s after ignition; the neighbour (1 cell east, 0.9 the size) catches 1.5 s after.</summary>
        public const float Distance = 6f, ReleaseAt = 3.5f, SpreadAt = 1.5f, NeighbourScale = 0.9f;
        /// <summary>Amenoyodomi lets go 1.2 s after ignition.</summary>
        public const float LetGoAfter = 1.2f;
        public const float Ignite = Warmup, Release = Ignite + ReleaseAt, LetGo = Ignite + LetGoAfter, Spreads = Ignite + SpreadAt;
        /// <summary>The showcase cuts 0.2 s after the smoke.</summary>
        public const float Duration = Release + SmokeLife + 0.2f;

        // Held scenes (lib/amenoyodomi.js's rule): each weapon hangs over the point it was thrown at from 0 s,
        // creeping on at 1 % of its speed. HoldShare: how far along the way to its raider a kunai hangs; FumaHold:
        // how far out the Fūma hangs; the walls stand this far past the middle of the targets and a weapon that hits
        // nobody drops LandBefore short of the wall's face. A raider steps on the landed kunai 1 s after it lands.
        public const float Hang = 0.01f, HoldShare = 0.4f, FumaHold = 1.6f, KunaiWall = 2.8f, FumaWall = 4f, LandBefore = 0.3f, StepAfter = 1f;
    }

    /// <summary>
    /// One weapon of the preview's held scenes, as lib/amenoyodomi.js's placed() makes it: thrown from
    /// <see cref="from"/> before the clip, hanging over the point it was thrown at from 0 s and creeping on along its
    /// heading at <see cref="AmaterasuTiming.Hang"/> of its speed, let go at <see cref="letGo"/> and flying on at full
    /// speed until <see cref="stopAt"/>, where it hits (<see cref="hit"/>) or drops to the floor.
    /// </summary>
    public struct AmaterasuThrow
    {
        public bool fuma, hit;
        public Vector2 from, dir;
        /// <summary>Heading, degrees: 0 east, 90 north.</summary>
        public float deg;
        /// <summary>Cells from <see cref="from"/> to the point it hangs over at 0 s; its flight speed, cells/s.</summary>
        public float dist, speed;
        public float letGo, stopAt, stopU;
        /// <summary>When it catches fire; where it ends (its raider, or the floor in front of the wall).</summary>
        public float lit;
        public Vector2 end;

        /// <summary>Cells along its heading from <see cref="from"/> at time <paramref name="t"/>.</summary>
        public float U(float t)
        {
            if (t < letGo) return dist + Mathf.Max(0f, t) * AmaterasuTiming.Hang * speed;
            float letGoU = dist + letGo * AmaterasuTiming.Hang * speed;
            return t < stopAt ? letGoU + speed * (t - letGo) : stopU;
        }

        /// <summary>The ground point it is over at <paramref name="t"/>.</summary>
        public Vector2 Ground(float t) => from + dir * U(t);

        /// <summary>
        /// The ground point up to the moment it stops (the sketch's posAt): a stopped weapon's picture in the air
        /// ends there.
        /// </summary>
        public Vector2 InAir(float t) => Ground(Mathf.Min(t, stopAt - 1e-4f));

        /// <summary>The Fūma's turn, degrees clockwise: 720 degrees per second of full-speed flight, in step with the distance.</summary>
        public float Turn(float t) => fuma ? AmenoyodomiGraphics.FumaSpin * U(t) / speed : 0f;

        public static AmaterasuThrow Placed(bool fuma, Vector2 from, Vector2 cell, float letGo)
        {
            Vector2 d = cell - from;
            float dist = Mathf.Max(1e-6f, d.magnitude);
            var w = new AmaterasuThrow
            {
                fuma = fuma, from = from, dir = d / dist, dist = dist,
                speed = fuma ? AmenoyodomiGraphics.FumaSpeed : AmenoyodomiGraphics.KunaiSpeed, letGo = letGo,
                stopAt = float.PositiveInfinity, stopU = float.PositiveInfinity,
            };
            w.deg = Mathf.Atan2(w.dir.y, w.dir.x) * Mathf.Rad2Deg;
            return w;
        }

        /// <summary>Sets where it stops: <paramref name="reach"/> cells on from where it was let go.</summary>
        public void StopAfter(float reach, bool hits)
        {
            float letGoU = dist + letGo * AmaterasuTiming.Hang * speed;
            stopAt = letGo + reach / speed;
            stopU = letGoU + reach;
            hit = hits;
        }
    }

    /// <summary>
    /// The preview's held scenes, the sketch's layout() with its defaults (aim east): o is the middle of the targets,
    /// the caster stands <see cref="AmaterasuTiming.Distance"/> cells back along the aim.
    /// Kunai: three hang 40 % of the way to two raiders and to a point beside them; the first two hit their raiders,
    /// the third passes them and drops in front of the wall 2.8 cells past o, where a raider steps on it 1 s later.
    /// Fūma: it hangs 1.6 cells out, three raiders stand on its line and it drops in front of the wall 4 cells past o.
    /// </summary>
    public sealed class AmaterasuHeldScene
    {
        public Vector2 o, aim, caster;
        public readonly AmaterasuThrow[] kunai = new AmaterasuThrow[3];
        /// <summary>The kunai drawn north first: index of kunai i in that order.</summary>
        public readonly int[] order = new int[3];
        public AmaterasuThrow fuma;
        public readonly Vector2[] raiders = new Vector2[2], line = new Vector2[3], walls = new Vector2[3];
        public readonly float[] cuts = new float[3];
        /// <summary>Where the third kunai lies and when the raider steps on it; where the Fūma lies and when it lands.</summary>
        public Vector2 landed, land;
        public float step, landedAt;

        private Vector2 Go(Vector2 q, float along, float left = 0f) =>
            q + aim * along + new Vector2(-aim.y, aim.x) * left;

        private static float AlongTo(in AmaterasuThrow w, Vector2 q, Vector2 point) => Vector2.Dot(point - q, w.dir);

        private float ToWall(in AmaterasuThrow w, Vector2 q, float wall) =>
            (wall - 0.5f - AmaterasuTiming.LandBefore - Vector2.Dot(q - o, aim)) / Vector2.Dot(w.dir, aim);

        public static AmaterasuHeldScene Build(Vector2 o, bool fumaScene)
        {
            var s = new AmaterasuHeldScene { o = o, aim = Vector2.right };
            float letGo = AmaterasuTiming.LetGo;
            s.caster = s.Go(o, -AmaterasuTiming.Distance);
            s.raiders[0] = s.Go(o, 0f, 0.9f);
            s.raiders[1] = s.Go(o, 0.5f, -0.8f);
            Vector2[] aims = { s.raiders[0], s.raiders[1], s.Go(o, 1.9f, 0.25f) };
            var cells = new Vector2[3];
            for (int i = 0; i < 3; i++)
            {
                cells[i] = Vector2.Lerp(s.caster, aims[i], AmaterasuTiming.HoldShare);
                AmaterasuThrow w = AmaterasuThrow.Placed(false, s.caster, cells[i], letGo);
                Vector2 q = w.Ground(letGo);
                w.StopAfter(i < 2 ? AlongTo(w, q, aims[i]) : s.ToWall(w, q, AmaterasuTiming.KunaiWall), i < 2);
                w.lit = AmaterasuTiming.Ignite + i * AmaterasuTiming.KunaiStagger;
                w.end = i < 2 ? aims[i] : w.from + w.dir * w.stopU;
                s.kunai[i] = w;
            }
            // Drawn north first: the kunai hanging furthest north gets order 0.
            for (int i = 0; i < 3; i++)
            {
                int ahead = 0;
                for (int j = 0; j < 3; j++)
                    if (cells[j].y > cells[i].y || (cells[j].y == cells[i].y && j < i)) ahead++;
                s.order[i] = ahead;
            }
            s.landed = s.kunai[2].end;
            s.step = s.kunai[2].stopAt + AmaterasuTiming.StepAfter;

            AmaterasuThrow f = AmaterasuThrow.Placed(true, s.caster, s.Go(s.caster, AmaterasuTiming.FumaHold), letGo);
            Vector2 qF = f.Ground(letGo);
            f.StopAfter(s.ToWall(f, qF, AmaterasuTiming.FumaWall), false);
            f.lit = AmaterasuTiming.Ignite;
            f.end = f.from + f.dir * f.stopU;
            s.fuma = f;
            s.land = f.end;
            s.landedAt = f.stopAt;
            s.line[0] = s.Go(o, -1.2f);
            s.line[1] = s.Go(o, 0.2f);
            s.line[2] = s.Go(o, 1.6f);
            for (int i = 0; i < 3; i++) s.cuts[i] = letGo + AlongTo(f, qF, s.line[i]) / f.speed;
            for (int i = 0; i < 3; i++) s.walls[i] = s.Go(o, fumaScene ? AmaterasuTiming.FumaWall : AmaterasuTiming.KunaiWall, i - 1);
            return s;
        }
    }
}
