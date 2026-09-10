using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AbilityGenes
{
    /// <summary>
    /// What a body coming apart sounds like and what it leaves on the ground.
    ///
    /// Everything is resolved by name and cached, the same as <see cref="ArcFX"/>, so a missing
    /// def makes the scatter quiet rather than throwing on a path that runs inside
    /// Thing.TakeDamage.
    ///
    /// The two ends are deliberately not symmetrical. Departure is loud and messy - the carrier
    /// was hit, and the feathers are the evidence that something was standing there. Arrival is
    /// nearly silent, because the interesting thing about a crow clone is that the person you
    /// were fighting is now behind you and did not announce it.
    /// </summary>
    public static class DispersalFX
    {
        private static FleckDef feather;
        private static FleckDef smoke;
        private static FleckDef blood;
        private static SoundDef departSound;
        private static bool resolved;

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;

            feather = DefDatabase<FleckDef>.GetNamedSilentFail("AG_DispersalFeather");
            smoke = DefDatabase<FleckDef>.GetNamedSilentFail("JumpSmoke");
            blood = DefDatabase<FleckDef>.GetNamedSilentFail("BloodSplash");

            // Biotech's long-jump legs, which is the closest thing in Core or Biotech to a body
            // leaving a cell faster than a body leaves cells. This mod already requires Biotech.
            departSound = DefDatabase<SoundDef>.GetNamedSilentFail("Longjump_Jump");
        }

        /// <summary>The cell the carrier came apart in.</summary>
        public static void Depart(Vector3 position, Map map)
        {
            Resolve();
            if (map == null) return;

            if (smoke != null) FleckMaker.Static(position, map, smoke, 1.4f);
            Feathers(position, map, DispersalDefaults.FeathersOnDeparture, 0.85f);

            if (departSound != null)
            {
                departSound.PlayOneShot(new TargetInfo(position.ToIntVec3(), map, false));
            }
        }

        /// <summary>Where they put themselves back together. Fewer feathers, no sound.</summary>
        public static void Arrive(Vector3 position, Map map)
        {
            Resolve();
            if (map == null) return;

            if (smoke != null) FleckMaker.Static(position, map, smoke, 0.8f);
            Feathers(position, map, DispersalDefaults.FeathersOnArrival, 0.55f);
        }

        /// <summary>Feeding stays readable: a drifting feather, without repeated smoke puffs.</summary>
        public static void Feed(Vector3 position, Map map)
        {
            if (map == null) return;
            Resolve();
            Feathers(position, map, 1, 0.25f);
            if (blood != null && position.ShouldSpawnMotesAt(map))
                FleckMaker.Static(position, map, blood, 0.25f);
        }

        /// <summary>A sparse feather wake along the same lifted route as the flock.</summary>
        public static void Travel(Vector3 from, Vector3 to, float progress, float lift, Map map)
        {
            if (map == null || progress <= 0f || progress >= 1f) return;
            Resolve();

            Vector3 position = Vector3.Lerp(from, to, progress);
            position += Vector3.forward * (lift * Mathf.Sin(Mathf.PI * progress));
            Feathers(position, map, 1, 0.3f);
        }

        /// <summary>
        /// Feathers thrown outward and left to drift down. Each one gets its own heading and
        /// its own spin, because a ring of identically-rotated feathers reads as a decal.
        /// </summary>
        private static void Feathers(Vector3 position, Map map, int count, float speed)
        {
            if (feather == null || !position.ShouldSpawnMotesAt(map)) return;

            for (int i = 0; i < count; i++)
            {
                FleckCreationData data = FleckMaker.GetDataStatic(position, map, feather,
                    Rand.Range(0.7f, 1.15f));
                data.rotation = Rand.Range(0f, 360f);
                data.rotationRate = Rand.Range(-90f, 90f);
                data.velocityAngle = Rand.Range(0f, 360f);
                data.velocitySpeed = Rand.Range(speed * 0.4f, speed);
                map.flecks.CreateFleck(data);
            }
        }
    }
}
