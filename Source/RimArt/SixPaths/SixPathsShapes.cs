using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// The silhouette of one orb, and nothing else. A Truth-Seeking Ball carries no interior
    /// detail: it is a flat black shape with a lit rim, so a transformation is only a change of
    /// outline. That makes every form here one parametric curve rather than a sprite, and a
    /// transformation a lerp between two sets of four numbers.
    ///
    /// No textures are involved. GravityGraphics draws its whole black hole with a single white
    /// pixel tinted per draw (GravityGraphics.cs:9); the same material serves these orbs.
    /// </summary>
    public struct OrbShape
    {
        /// <summary>Half extent along the orb's own long axis, in orb radii.</summary>
        public float halfX;
        /// <summary>Half extent across it.</summary>
        public float halfY;
        /// <summary>Superellipse exponent: 2 is an ellipse, higher values square the ends off.</summary>
        public float corner;
        /// <summary>How far the +x end pulls to a point. 0 is blunt, 1 closes it completely.</summary>
        public float taper;
    }

    public static class SixPathsShapes
    {
        // Four forms. The long extents are bounded by the ring rather than chosen freely: the
        // orbs lie tangent to the orbit, so a form longer than the spacing between two of them
        // intersects its neighbours. At OrbitRadius 2.0 with 6 orbs that spacing is 1.70 cells
        // and the staff, the longest form, draws 1.72 -- they meet end to end and no further.
        public static readonly OrbShape Orb = new OrbShape { halfX = 1.00f, halfY = 1.00f, corner = 2.0f, taper = 0.00f };
        public static readonly OrbShape Rod = new OrbShape { halfX = 2.05f, halfY = 0.20f, corner = 3.2f, taper = 0.00f };
        public static readonly OrbShape Blade = new OrbShape { halfX = 1.70f, halfY = 0.52f, corner = 2.4f, taper = 1.00f };
        public static readonly OrbShape Shield = new OrbShape { halfX = 1.30f, halfY = 1.30f, corner = 4.5f, taper = 0.00f };

        /// <summary>The cycle the showcase walks, ending back at the orb.</summary>
        public static readonly OrbShape[] Cycle = { Orb, Rod, Blade, Shield };

        /// <summary>
        /// A point on the outline, t running 0..1 counter-clockwise from the +x end.
        /// Both shapes in a transformation are sampled at the same t, so their points correspond
        /// and the lerp between them stays a closed curve at every step.
        /// </summary>
        public static Vector2 Outline(in OrbShape shape, float t)
        {
            float phi = t * Mathf.PI * 2f;
            float c = Mathf.Cos(phi), s = Mathf.Sin(phi);
            // Superellipse in polar form. At corner 2 this is exactly 1 and the result is a plain
            // ellipse; raising it flattens the ends, which is what turns a lens into a bar.
            float radius = 1f / Mathf.Pow(
                Mathf.Pow(Mathf.Abs(c), shape.corner) + Mathf.Pow(Mathf.Abs(s), shape.corner),
                1f / shape.corner);
            // Taper narrows the +x end only. It is the single difference between a bar and a
            // blade, and at taper 1 the curve closes on a point there.
            float width = 1f - shape.taper * 0.5f * (1f + c);
            return new Vector2(shape.halfX * radius * c, shape.halfY * radius * s * width);
        }

        /// <summary>
        /// Every intermediate of a lerp between two of the forms above is itself a valid form,
        /// so the transformation interpolates the four numbers rather than the 64 outline points.
        /// </summary>
        public static OrbShape Lerp(in OrbShape a, in OrbShape b, float t) => new OrbShape
        {
            halfX = Mathf.Lerp(a.halfX, b.halfX, t),
            halfY = Mathf.Lerp(a.halfY, b.halfY, t),
            corner = Mathf.Lerp(a.corner, b.corner, t),
            taper = Mathf.Lerp(a.taper, b.taper, t),
        };
    }

    /// <summary>Pure clock for the showcase: hold a form, change, hold the next.</summary>
    public static class SixPathsTiming
    {
        public const float HoldSeconds = 1.40f, MorphSeconds = 0.26f;
        public const float OrbitSeconds = 4.5f, OrbitRadius = 2.0f, OrbitDepth = 0.60f;
        // Six, for the name. The count and the radius are one decision: more orbs on the same
        // ring is less room for each form to grow into.
        public const int Orbs = 6;
        public static float CycleSeconds => HoldSeconds + MorphSeconds;

        /// <summary>Index of the form being left, and 0..1 progress out of it.</summary>
        public static void Phase(float seconds, out int from, out float progress)
        {
            float step = seconds / CycleSeconds;
            from = Mathf.FloorToInt(step);
            float within = (step - from) * CycleSeconds;
            progress = Mathf.Clamp01((within - HoldSeconds) / MorphSeconds);
            from = ((from % SixPathsShapes.Cycle.Length) + SixPathsShapes.Cycle.Length) % SixPathsShapes.Cycle.Length;
        }

        public static OrbShape ShapeAt(float seconds)
        {
            Phase(seconds, out int from, out float progress);
            int to = (from + 1) % SixPathsShapes.Cycle.Length;
            return SixPathsShapes.Lerp(SixPathsShapes.Cycle[from], SixPathsShapes.Cycle[to], Smooth(progress));
        }

        public static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>Peaks at mid-change and is zero while a form is held. Drives the rim flash
        /// and the size overshoot, which is what sells the change as an event rather than a fade.</summary>
        public static float Surge(float seconds)
        {
            Phase(seconds, out _, out float progress);
            return Mathf.Sin(progress * Mathf.PI);
        }
    }
}
