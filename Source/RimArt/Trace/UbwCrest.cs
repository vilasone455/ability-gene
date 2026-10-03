using System;

namespace RimArt
{
    /// <summary>
    /// The world v4's shapes and numbers, no drawing: the sword crest just past the map's north edge (its
    /// foot, height and depth along x, the kinds of weapon on it), the side-view backdrop behind it (three
    /// ridges and fourteen rows of swords on the plain, each with its share of the camera's pan), the seven
    /// gears of the sky, and the sketch's slider defaults. The port of lib/ubw-crest.js and the parts of
    /// lib/ubw-horizon.js it uses (Tools/VfxLab/web/sketches), in double precision with the lab's hash and
    /// fbm, so the crest is the same in game and in the lab (Tests/Ubw checks it). System only.
    ///
    /// The game's camera looks straight down, so a sky can only show where the ground ends. v4 ends it
    /// with a bank of earth past the north edge, its top packed with swords against the sky, and hangs a
    /// picture drawn from the side behind it that pans slower than the map (Final Fantasy VI's cliffs do
    /// the same). The map's north edge is <see cref="North"/> cells north of the caster, so the crest is on
    /// screen at the usual zoom; nothing walkable rises.
    /// </summary>
    public static class UbwCrest
    {
        /// <summary>The map's north edge, in cells north of the caster: the v4 map is 40 x 33 with the caster 20 cells from the other three edges.</summary>
        public const int North = 13;
        /// <summary>The sketch's defaults: the crest's height (cells), swords per cell along it, the horizon past the edge at the usual framing, the backdrop's parallax (x each part's share), the camera haze at the top of the screen, the smoke's opacity, embers rising past the crest, the gears' spin (x 14 / radius degrees a second), shadow length (x height).</summary>
        public const double Height = 1.6, SwordsPerCell = 2.2, Horizon = 2.6, Parallax = 1, Haze = .3, Smoke = .32, Spin = 1, ShadowLength = 1.7;
        public const int Updraft = 40;
        /// <summary>Plates past the east, west and south edges; how far the field's density follows its slow noise (bare patches); the gears' faint shadows on the map.</summary>
        public const double OuterPlate = 3.8, Cluster = 1, GearShadows = .06;

        /// <summary>The crest's foot is Foot to Foot + Wobble cells past the edge; the map's plates end there.</summary>
        public const double Foot = .15, Wobble = .35;
        /// <summary>The lit rim's width; the crest's swords against a field sword's size; the bake runs Span cells either side of the caster in Step pieces, the face's texture repeating every Chunk cells; one dip in DipEvery cells on average.</summary>
        public const double RimW = .07, SwordScale = 1.35, Span = 88, Step = .25, Chunk = 8, DipEvery = 26;
        /// <summary>The usual framing, 7 cells north of the caster, where the horizon is measured; the horizon's share of the camera's pan up and down; the backdrop's lowest line, past the map edge.</summary>
        public const double CamRef = 7, HorizonMove = .25, CoverAt = .45;
        /// <summary>Screen cells per degree of the sky across (azimuth) and up (elevation); the sky band's height; the sun's elevation in degrees.</summary>
        public const double KA = .5, KE = .3, SkyCells = 40, SunUp = 4.5;

        public const double D2R = Math.PI / 180, Lift = UbwBlade.Lift;

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
        private static double Hash(int x, int y, int seed) => UbwTerrain.Hash(x, y, seed);

        // ---- the crest's profile, x in cells from the caster ---------------------------------------------------

        /// <summary>The foot, in cells past the edge.</summary>
        public static double FootOf(double x) => Foot + Wobble * UbwTerrain.Fbm(x * .23 + 11, 3.7, 131, 2, 64);

        /// <summary>0 to 1: how deep a dip cuts the crest at x. 60 % of the places one could be have one, 4 to 8 cells wide.</summary>
        public static double DipOf(double x)
        {
            // JavaScript's Math.round: halves go up, also below zero.
            int k = (int)Math.Floor(x / DipEvery + .5);
            double centre = k * DipEvery + (Hash(k, 1, 137) - .5) * 10, w = 2 + 2 * Hash(k, 2, 137);
            double d = Clamp01(1 - Math.Abs(x - centre) / w);
            return Hash(k, 3, 137) < .6 ? d * d * (3 - 2 * d) : 0;
        }

        /// <summary>The height, 0.4 to 1.3 of <paramref name="H"/> on a mix of slow and quick waves, cut down by the dips.</summary>
        public static double HeightOf(double x, double H)
        {
            double n = .5 + .26 * Math.Sin(x * .13 + 1.3) + .16 * Math.Sin(x * .37 + 4.1) + .1 * Math.Sin(x * .9 + .7) + .34 * (UbwTerrain.Fbm(x * .45 + 7, 1.1, 133, 3, 64) - .5);
            return H * (.4 + .9 * Clamp01(n)) * (1 - .6 * DipOf(x));
        }

        /// <summary>How far north of the foot the top is, on the ground: 0.5 to 1.4 cells, so the top line wanders more than the foot.</summary>
        public static double DepthOf(double x) => .5 + .9 * UbwTerrain.Fbm(x * .17 + 3, 5.3, 139, 2, 64);

        /// <summary>The ground under the top, in cells past the edge.</summary>
        public static double GroundOf(double x) => FootOf(x) + DepthOf(x);

        /// <summary>The top on the screen, in cells past the edge: the ground under it plus its height drawn 0.6 north per cell.</summary>
        public static double TopOf(double x, double H) => GroundOf(x) + HeightOf(x, H) * Lift;

        /// <summary>A weapon on the crest: 0 a sword and 1 a greatsword (point in the ground), 2 a spear (butt in the ground); its real height (cells), a shortening, and its lean on the screen (radians, east positive).</summary>
        public struct Weapon
        {
            public int Type;
            public double Tall, Short, Lean;
        }

        /// <summary>Weapon j of a row: 60 % swords, 20 % greatswords, 20 % spears, leaning up to 26 degrees (one in seven up to 45).</summary>
        public static Weapon WeaponOf(int j, int seed)
        {
            double r = Hash(j, 3, seed);
            int type = r < .6 ? 0 : r < .8 ? 1 : 2;
            double lo = type == 0 ? 1 : type == 1 ? 1.3 : 2, hi = type == 0 ? 1.5 : type == 1 ? 1.9 : 2.8;
            double lean = Hash(j, 8, seed) < .15 ? 45 : 26;
            return new Weapon { Type = type, Tall = Lerp(lo, hi, Hash(j, 4, seed)), Short = .85 + .15 * Hash(j, 5, seed), Lean = (Hash(j, 6, seed) - .5) * 2 * lean * D2R };
        }

        // ---- the backdrop ----------------------------------------------------------------------------------------

        /// <summary>A ridge of the backdrop: base and most height (screen cells from the horizon line), wave number and phase, parallax p (its share of the pan), haze, swords per cell along its top and their height.</summary>
        public struct Ridge
        {
            public double Base, H, F, Ph, P, Haze, Swords, Tall;
        }

        /// <summary>Far to near: mountains standing over the horizon, a middle ridge, a low near one, the last two with swords.</summary>
        public static readonly Ridge[] Ridges =
        {
            new Ridge { Base = -.3, H = 3.2, F = .05, Ph = 3, P = .06, Haze = .72, Swords = 0 },
            new Ridge { Base = -.9, H = 1.4, F = .12, Ph = 2, P = .14, Haze = .5, Swords = 1.4, Tall = .22 },
            new Ridge { Base = -1.9, H = 1.1, F = .2, Ph = 4, P = .24, Haze = .32, Swords = 1.1, Tall = .38 },
        };
        /// <summary>A ridge's fill runs RidgeDepth screen cells below its base; RowCount rows of swords on the plain, the nearest RowDeep below the horizon.</summary>
        public const double RidgeDepth = 4, RowDeep = 5.5;
        public const int RowCount = 14;

        /// <summary>0 to 1 along a ridge's top.</summary>
        public static double Profile(in Ridge R, double x) => Clamp01(.5 + .25 * Math.Sin(x * R.F + R.Ph) + .15 * Math.Sin(x * R.F * 2.7 + R.Ph * 3) + .1 * Math.Sin(x * R.F * 6.1 + R.Ph * 5)
            + .24 * (UbwTerrain.Fbm(x * R.F * 4 + R.Ph * 10, R.Ph, 87, 2, 64) - .5));

        /// <summary>A row of swords on the plain: how far below the horizon it sits at the usual framing, its parallax, spacing, sword height and haze.</summary>
        public struct Row
        {
            public double D, P, Gap, Tall, Haze;
        }

        /// <summary>Row k, 0 at the horizon: further down, nearer, sparser, taller and clearer.</summary>
        public static Row RowOf(int k)
        {
            double n = k / (double)(RowCount - 1);
            return new Row { D = .08 + RowDeep * Math.Pow(n, 1.7), P = .08 + .37 * n, Gap = .35 + .9 * n, Tall = .06 + .5 * Math.Pow(n, 1.3), Haze = .9 - .6 * n };
        }

        /// <summary>A gear of the sky: azimuth and elevation of its centre, radius (degrees), type (0 a six-spoke wheel, 1 a double ring on four spokes, 2 a heavy three-spoke wheel), teeth, which way it turns, parallax, haze toward the sky's colour.</summary>
        public struct SkyGear
        {
            public double A, E, R, Spin, P, Haze;
            public int Type, Teeth;
        }

        public static readonly SkyGear[] Gears =
        {
            new SkyGear { A = -40, E = 3, R = 26, Type = 0, Teeth = 18, Spin = 2, P = .5, Haze = .04 },
            new SkyGear { A = 31, E = 21, R = 15, Type = 1, Teeth = 16, Spin = -3, P = .36, Haze = .14 },
            new SkyGear { A = 5, E = 8, R = 9, Type = 2, Teeth = 12, Spin = 4, P = .42, Haze = .34 },
            new SkyGear { A = 54, E = 2, R = 12, Type = 0, Teeth = 14, Spin = -3.5, P = .46, Haze = .2 },
            new SkyGear { A = -11, E = 27, R = 7, Type = 1, Teeth = 12, Spin = 5, P = .26, Haze = .46 },
            new SkyGear { A = -64, E = 23, R = 10, Type = 2, Teeth = 12, Spin = -2.5, P = .3, Haze = .36 },
            new SkyGear { A = 15, E = 41, R = 5.5, Type = 0, Teeth = 10, Spin = 6, P = .2, Haze = .58 },
        };

        /// <summary>
        /// The low sun's azimuth in degrees east of north, from the shadow vector (x, z). The sketch means where the
        /// shadows point away from, kept within 70 degrees of north, but the clamp it calls (lib/trace.js clamp,
        /// Mathf.Clamp01) ignores the bounds, so it is 0 to 1: the sun stands about over the caster (1 degree for the
        /// lab's sun, which would be 55). Ported as the sketch draws it.
        /// </summary>
        public static double SunAzimuth(double sunX, double sunZ) => Clamp01(Math.Atan2(-sunX, -sunZ) / D2R);
    }
}
