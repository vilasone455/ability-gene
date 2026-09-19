using UnityEngine;

namespace RimArt
{
    /// <summary>One carried orb: its point on the ground, its height, and which side of the sage it is on.</summary>
    public struct CarriedSlot
    {
        public Vector3 ground;
        /// <summary>Cells above the ground.</summary>
        public float height;
        /// <summary>North of the sage, so it draws under the pawn; the rest draw over it.</summary>
        public bool behind;
    }

    /// <summary>
    /// Pure clock for the orbs the sage carries: six slots on a level ring at head height that
    /// turns slowly, the same picture for every facing. Seconds in, geometry out.
    ///
    /// An orb is <see cref="IdleRadius"/> while carried. One that an ability uses leaves its slot,
    /// grows to that ability's cast radius over the flight out and shrinks back over the flight
    /// home. Its slot stays empty while it is away, so the ring is also the orb counter.
    ///
    /// The numbers are the ones picked in the VFX lab (Tools/VfxLab/web/sketches/lib/six-paths-sage.js).
    /// </summary>
    public static class SixPathsCarried
    {
        public const float IdleRadius = 0.09f, RingRadius = 0.45f, RingHeight = 0.95f;
        /// <summary>Seconds for one turn of the ring.</summary>
        public const float RingTurn = 10f;
        /// <summary>Height wobble per orb, phase offset by slot.</summary>
        public const float Bob = 0.04f, BobHz = 0.5f;

        /// <summary>
        /// Cast radius by job: forms held on the pawn, casts out on the field, and the heavy single
        /// masses, which is the size the slam's orbs always had.
        /// </summary>
        public const float OnPawn = 0.20f, Field = 0.30f, Heavy = SixPathsSlamTiming.OrbSize;

        /// <summary>Radius of an orb <paramref name="flown"/> of the way (0 to 1) from its slot to where it works.</summary>
        public static float DeployRadius(float flown, float target) =>
            Mathf.Lerp(IdleRadius, target, SixPathsSlamTiming.Smooth(flown));

        /// <summary><paramref name="ground"/> is where the sage stands.</summary>
        public static CarriedSlot Slot(Vector3 ground, int index, float seconds)
        {
            float turn = index / (float)SixPathsTiming.Orbs;
            float angle = (seconds / RingTurn + turn) * Mathf.PI * 2f;
            float north = Mathf.Sin(angle) * RingRadius;
            return new CarriedSlot
            {
                ground = ground + new Vector3(Mathf.Cos(angle) * RingRadius, 0f, north),
                height = RingHeight + Mathf.Sin((seconds * BobHz + turn) * Mathf.PI * 2f) * Bob,
                behind = north > 0f,
            };
        }
    }
}
