using UnityEngine;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// A number of birds that were recently one person, and the routes they take between two
    /// points. Shared by both halves of the gene: <see cref="ScatterFlight"/> drives it off its
    /// own tick counter, <see cref="PawnFlyer_Murder"/> off the flight it is carrying.
    ///
    /// Everything that stops this looking like a formation is decided once, in the constructor,
    /// and then held for the life of the flight. A flock whose spread was re-rolled per frame
    /// would shimmer; one with no spread at all is a single sprite drawn nine times.
    ///
    /// Progress is a fraction of the whole route rather than a tick count, which is what lets
    /// the same class serve a fixed-length decoration and a flight whose duration is computed
    /// from its own distance.
    /// </summary>
    public class Flock
    {
        private readonly float[] lateral;
        private readonly float[] stagger;
        private readonly int[] beatOffset;
        private readonly float[] beatSpeed;
        private readonly float[] overshoot;
        private readonly float[] wobbleAmp;
        private readonly float[] wobblePhase;
        private readonly float lift;

        /// <param name="size">How many birds.</param>
        /// <param name="spread">Widest the flock opens away from the straight line, in cells.</param>
        /// <param name="lift">
        /// How high the flock rises at the midpoint, in cells. Height on this camera is a shift
        /// up the screen and nothing else, but it is the difference between birds flying over
        /// the ground and birds sliding along it.
        /// </param>
        public Flock(int size, float spread, float lift)
        {
            this.lift = lift;

            lateral = new float[size];
            stagger = new float[size];
            beatOffset = new int[size];
            beatSpeed = new float[size];
            overshoot = new float[size];
            wobbleAmp = new float[size];
            wobblePhase = new float[size];

            for (int i = 0; i < size; i++)
            {
                // Signed by index rather than purely random, so the flock reliably opens to
                // both sides instead of occasionally all going left.
                float side = (i % 2 == 0) ? 1f : -1f;
                lateral[i] = side * Rand.Range(0.25f, 1f) * spread;

                // A fraction of the route, not a tick count: the same numbers have to work for
                // a flight that lasts twenty-six ticks and one that lasts two hundred.
                stagger[i] = Rand.Range(0f, 0.18f);

                // Every bird is at a different point in its own beat. Without this the flock
                // flaps in unison, which is the single most artificial thing a group of birds
                // can do - it reads as one sprite drawn n times, because that is what it is.
                beatOffset[i] = Rand.Range(0, DispersalDefaults.BeatTicks);
                beatSpeed[i] = Rand.Range(0.85f, 1.15f);

                wobbleAmp[i] = Rand.Range(0.35f, 1f) * DispersalDefaults.WobbleDegrees;
                wobblePhase[i] = Rand.Range(0f, Mathf.PI * 2f);

                // A bird that arrives exactly on the carrier's cell disappears into them. A
                // small overshoot keeps the flock breaking up around the pawn, not into them.
                overshoot[i] = Rand.Range(0f, 0.45f);
            }
        }

        /// <param name="progress">0 to 1 along the whole route.</param>
        /// <param name="ticks">Any monotonic tick count; drives the wing beat only.</param>
        public void Draw(Vector3 from, Vector3 to, float progress, int ticks)
        {
            for (int i = 0; i < lateral.Length; i++)
            {
                float p = ProgressFor(i, progress);
                if (p <= 0f) continue;

                Vector3 position = At(i, from, to, p);
                Vector3 ahead = At(i, from, to, Mathf.Min(1f, p + 0.01f));

                Vector3 along = ahead - position;
                if (along.MagnitudeHorizontalSquared() < 0.0001f) along = to - from;
                along.y = 0f;
                if (along.sqrMagnitude < 0.0001f) along = Vector3.forward;
                along = along.normalized;

                // Where in its own wing beat this bird is. The frame comes off the same clock
                // as the surge and the bob, so a bird pushes forward and rises on the stroke
                // that the sprite is showing it pushing with.
                float beatTick = ticks * beatSpeed[i] + beatOffset[i];
                int frame = Mathf.FloorToInt(beatTick / DispersalDefaults.TicksPerFrame)
                    % DispersalDefaults.FrameCount;
                float beat = Mathf.Sin(Mathf.PI * 2f * ((beatTick % DispersalDefaults.BeatTicks)
                    / (float)DispersalDefaults.BeatTicks));

                // Heading is taken from the route before the beat pulse is added, so the pulse
                // moves the bird without ever spinning it.
                float heading = along.AngleFlat()
                    + wobbleAmp[i] * Mathf.Sin(p * Mathf.PI * 3f + wobblePhase[i]);

                // Scale the pulse to actual travel per tick so slow flights cannot jerk
                // backwards. Ease both offsets out at departure and landing.
                float travelPerTick = ticks > 0
                    ? (to - from).MagnitudeHorizontal() * progress / ticks / (1f - stagger[i])
                    : 0f;
                float envelope = Mathf.Sin(Mathf.PI * p);
                float surge = Mathf.Min(DispersalDefaults.SurgeCells, travelPerTick * 0.3f);
                position += along * (surge * beat * envelope);
                position += Vector3.forward * (DispersalDefaults.BobCells * beat * envelope);

                DispersalGraphics.DrawCrow(position, heading, frame, Alpha(p));
            }
        }

        /// <summary>How far along its own route bird <paramref name="i"/> is.</summary>
        private float ProgressFor(int i, float progress)
        {
            float span = 1f - stagger[i];
            if (span <= 0f) return 0f;
            return Mathf.Clamp01((progress - stagger[i]) / span);
        }

        /// <summary>
        /// Where that bird is at that point. The lateral push is a sine over the route, so every
        /// bird leaves the same cell and arrives at the same one, and the flock is at its widest
        /// exactly halfway - which is what makes the shape read as coming apart and going back
        /// together rather than as nine things travelling in parallel.
        /// </summary>
        private Vector3 At(int i, Vector3 from, Vector3 to, float progress)
        {
            Vector3 position = Vector3.Lerp(from, to, progress);

            Vector3 along = to - from;
            along.y = 0f;
            if (along.sqrMagnitude < 0.0001f) along = Vector3.forward;
            along = along.normalized;

            Vector3 side = new Vector3(-along.z, 0f, along.x);
            float bulge = Mathf.Sin(Mathf.PI * progress);

            position += side * lateral[i] * bulge;
            position += along * overshoot[i] * progress;
            position += Vector3.forward * (lift * bulge);

            return position;
        }

        /// <summary>
        /// Solid for most of the route, gone at both ends. The fade in is faster than the fade
        /// out because a bird that eases into existence looks like a rendering error, whereas
        /// one that thins out on arrival looks like it landed.
        /// </summary>
        private static float Alpha(float progress)
        {
            if (progress < 0.12f) return progress / 0.12f;
            if (progress > 0.78f) return Mathf.Max(0f, (1f - progress) / 0.22f);
            return 1f;
        }
    }
}
