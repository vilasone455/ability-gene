using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>A sword a Full Open volley stuck in the ground: where it stands (cells from the world's middle corner), how it leans, which weapon of the set at what size.</summary>
    public sealed class UbwLandedSword : IExposable
    {
        public int seed, row;
        public float x, z, lean, dir, size;

        public void ExposeData()
        {
            Scribe_Values.Look(ref seed, "seed");
            Scribe_Values.Look(ref row, "row");
            Scribe_Values.Look(ref x, "x");
            Scribe_Values.Look(ref z, "z");
            Scribe_Values.Look(ref lean, "lean");
            Scribe_Values.Look(ref dir, "dir");
            Scribe_Values.Look(ref size, "size", 1.3f);
        }
    }

    /// <summary>
    /// The world's swords as the commands see them: the field laid out by <see cref="UbwField.Make"/> with the numbers the
    /// bake uses (the landing spots, the v4 plates' heights, the weapon set), less the swords taken out of the ground
    /// (each leaves its hole), plus the swords Full Open stuck in it. Only the taken seeds and the stuck swords are saved;
    /// the layout is made again on first use after a load, so a world loaded mid-fight shows the same holes.
    ///
    /// Positions are cells from the world's middle corner (<see cref="MapComponent_UnlimitedBladeWorks.Origin"/>), as the
    /// sketches put them: map cell = CentreCell + floor. A sword past the map edge (<see cref="UbwSword.Far"/>) is out of
    /// reach. Every change bumps <see cref="Version"/> and notes the sword's screen foot, so the map component builds again
    /// only the rows of the field that changed.
    /// </summary>
    public sealed class UbwFieldState : IExposable
    {
        private List<int> gone = new List<int>();
        private List<UbwLandedSword> landed = new List<UbwLandedSword>();
        private int nextSeed = FirstLandedSeed;

        /// <summary>A stuck sword's seed starts here, past every seed of the laid-out field (a grid of at most 61 x 61).</summary>
        internal const int FirstLandedSeed = 100000;
        /// <summary>A stuck sword is driven in this share of its length, as the sketch's stuck() with buried 0.2.</summary>
        private const double LandedSink = 0.2;

        private List<UbwSword> all;
        private Dictionary<int, UbwSword> bySeed;
        private HashSet<int> goneSet;
        private Func<double, double, double> heightAt;
        private UbwWeapon[] weapons;
        private readonly List<double> changed = new List<double>();

        /// <summary>Goes up by one at every change, so a drawing can tell it is out of date.</summary>
        public int Version { get; private set; }

        public bool Built => all != null;

        /// <summary>How many swords are out of the ground (holes), and how many a volley stuck in it, for tests and the log.</summary>
        public int TakenCount => gone.Count;
        public int LandedCount => landed.Count;

        /// <summary>Every sword, north first by its screen foot (Z + Lift), the taken ones as holes.</summary>
        internal List<UbwSword> Swords => all;

        /// <summary>Lays the field out for these landing spots (cells from the middle) on the v4 plates (<paramref name="crest"/>) or the flat v1 ground.</summary>
        internal void Build(List<IntVec3> keep, bool crest)
        {
            weapons = UbwGraphics.Set.Weapons;
            var spots = new List<UbwXZ>(keep.Count);
            foreach (IntVec3 k in keep) spots.Add(new UbwXZ(k.x, k.z));
            heightAt = crest ? UbwTerrainGraphics.Ground().HeightAt : (Func<double, double, double>)null;
            List<UbwSword> list = UbwField.Make(crest ? UbwField.CrestLook : UbwField.Look, spots, weapons, heightAt);
            goneSet = new HashSet<int>(gone);
            foreach (UbwSword sw in list) sw.Hole = goneSet.Contains(sw.Seed);
            foreach (UbwLandedSword l in landed)
            {
                UbwSword sw = Make(l);
                sw.Hole = goneSet.Contains(sw.Seed);
                list.Add(sw);
            }
            all = Sort(list);
            bySeed = new Dictionary<int, UbwSword>(all.Count);
            foreach (UbwSword sw in all) bySeed[sw.Seed] = sw;
            changed.Clear();
            Version++;
        }

        private static List<UbwSword> Sort(List<UbwSword> list) => list.OrderByDescending(sw => sw.Z + sw.Lift).ToList();

        private UbwSword Make(UbwLandedSword l)
        {
            UbwWeapon w = weapons[Mathf.Clamp(l.row, 0, weapons.Length - 1)];
            var sw = new UbwSword
            {
                Seed = l.seed, X = l.x, Z = l.z, D = Math.Sqrt(l.x * l.x + l.z * l.z), W = w, Lean = l.lean, Dir = l.dir, Turn = 0,
                Sink = LandedSink, Size = l.size, Lift = heightAt != null ? heightAt(l.x, l.z) * UbwBlade.Lift : 0,
                Far = Math.Max(Math.Abs(l.x), Math.Abs(l.z)) > UbwField.MapHalf,
            };
            UbwField.Finish(sw);
            return sw;
        }

        /// <summary>How far north a sword standing at (x, z) is drawn for the plate under it (0 on the flat v1 ground).</summary>
        public double LiftAt(double x, double z) => heightAt != null ? heightAt(x, z) * UbwBlade.Lift : 0;

        /// <summary>A sword a command can take: in the ground and inside the map.</summary>
        public static bool Usable(UbwSword sw) => sw != null && !sw.Hole && !sw.Far;

        public UbwSword BySeed(int seed) => bySeed != null && bySeed.TryGetValue(seed, out UbwSword sw) ? sw : null;

        /// <summary>
        /// The usable sword nearest <paramref name="at"/> (cells from the middle corner) within <paramref name="within"/>
        /// cells, that <paramref name="also"/> allows; null if none.
        /// </summary>
        public UbwSword Nearest(Vector2 at, float within = float.PositiveInfinity, Predicate<UbwSword> also = null)
        {
            UbwSword best = null;
            double bestD = within * (double)within;
            foreach (UbwSword sw in all)
            {
                if (!Usable(sw) || also != null && !also(sw)) continue;
                double dx = sw.X - at.x, dz = sw.Z - at.y, d = dx * dx + dz * dz;
                if (d <= bestD)
                {
                    bestD = d;
                    best = sw;
                }
            }
            return best;
        }

        /// <summary>The sword leaves the ground: its hole stays, nothing can take it again.</summary>
        public void Take(UbwSword sw)
        {
            if (sw == null || sw.Hole) return;
            sw.Hole = true;
            goneSet.Add(sw.Seed);
            gone.Add(sw.Seed);
            Changed(sw);
        }

        /// <summary>The sword drops back into its own hole and stands again (a Full Open cancelled).</summary>
        public void PutBack(UbwSword sw)
        {
            if (sw == null || !sw.Hole) return;
            sw.Hole = false;
            goneSet.Remove(sw.Seed);
            gone.Remove(sw.Seed);
            Changed(sw);
        }

        /// <summary>
        /// A fired sword sticks in the ground at (x, z) (cells from the middle corner), its top leaning <paramref name="lean"/>
        /// degrees toward <paramref name="dir"/> (0 east, 90 north), flat side to the camera; it stands as a sword of the field.
        /// </summary>
        public UbwSword Land(float x, float z, float lean, float dir, UbwWeapon w, double size)
        {
            var l = new UbwLandedSword { seed = nextSeed++, row = w.Row, x = x, z = z, lean = lean, dir = dir, size = (float)size };
            landed.Add(l);
            UbwSword sw = Make(l);
            all.Add(sw);
            all = Sort(all);
            bySeed[sw.Seed] = sw;
            Changed(sw);
            return sw;
        }

        private void Changed(UbwSword sw)
        {
            changed.Add(sw.Z + sw.Lift);
            Version++;
        }

        /// <summary>The rows (by <paramref name="rowOf"/> of a screen foot) changed since the last call.</summary>
        internal HashSet<int> TakeChangedRows(Func<double, int> rowOf)
        {
            var rows = new HashSet<int>();
            foreach (double foot in changed) rows.Add(rowOf(foot));
            changed.Clear();
            return rows;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref gone, "gone", LookMode.Value);
            Scribe_Collections.Look(ref landed, "landed", LookMode.Deep);
            Scribe_Values.Look(ref nextSeed, "nextSeed", FirstLandedSeed);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (gone == null) gone = new List<int>();
                if (landed == null) landed = new List<UbwLandedSword>();
                all = null;
            }
        }
    }
}
