using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimArt
{
    /// <summary>
    /// What a dash sounds like and what it kicks up, kept away from the run that decides when.
    ///
    /// The arc deliberately does not borrow the anchor organ's skip flashes. A clap is a psychic
    /// exchange of two places and is dressed as one; an arc is a person covering nine cells faster
    /// than the eye follows, and what that should leave behind is disturbed ground and a bright
    /// line, not a purple bloom. Two kits that both move somebody instantly should not look the
    /// same, or the player learns nothing from watching either.
    ///
    /// Everything here is resolved by name and cached, the same as <see cref="AnchorFX"/>, so a
    /// missing def makes the arc quiet rather than throwing on the draw path.
    /// </summary>
    public static class ArcFX
    {
        private static FleckDef airPuff;
        private static FleckDef flash;
        private static FleckDef sparks;
        private static SoundDef launchSound;
        private static SoundDef landSound;
        private static bool resolved;

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;

            airPuff = DefDatabase<FleckDef>.GetNamedSilentFail("AirPuff");
            flash = DefDatabase<FleckDef>.GetNamedSilentFail("PlainFlash");
            sparks = DefDatabase<FleckDef>.GetNamedSilentFail("MicroSparks");

            // Biotech's long-jump legs, which is the closest thing in the game to a body leaving
            // the ground faster than it should. This mod already requires Biotech.
            launchSound = DefDatabase<SoundDef>.GetNamedSilentFail("Longjump_Jump");
            landSound = DefDatabase<SoundDef>.GetNamedSilentFail("Longjump_Land");
        }

        /// <summary>The cell the carrier just left: air closing, dust thrown the way they went.</summary>
        public static void Depart(Vector3 position, Vector3 towards, Map map)
        {
            Resolve();
            if (map == null) return;

            Static(airPuff, position, map, 1.3f);
            FleckMaker.ThrowDustPuffThick(position, map, 1.7f, new Color(1f, 0.96f, 0.85f, 0.7f));

            Vector3 kick = (towards - position).normalized * 0.4f;
            FleckMaker.ThrowDustPuff(position - kick, map, 1.1f);

            if (launchSound != null) launchSound.PlayOneShot(new TargetInfo(position.ToIntVec3(), map, false));
        }

        /// <summary>The cell they arrived in: a hard flash, sparks off the stop, dust settling.</summary>
        public static void Arrive(Vector3 position, Map map)
        {
            Resolve();
            if (map == null) return;

            Static(flash, position, map, 0.9f);
            Static(sparks, position, map, 1.1f);
            FleckMaker.ThrowDustPuffThick(position, map, 1.9f, new Color(1f, 0.94f, 0.78f, 0.8f));

            if (landSound != null) landSound.PlayOneShot(new TargetInfo(position.ToIntVec3(), map, false));
        }

        /// <summary>
        /// The ground under the line itself. Three puffs is enough to say that something passed
        /// along here, and few enough that a three-hop chain does not fill the screen with dust.
        /// </summary>
        public static void Trail(Vector3 from, Vector3 to, Map map)
        {
            if (map == null) return;

            for (int i = 1; i <= 3; i++)
            {
                Vector3 point = Vector3.Lerp(from, to, i / 4f);
                if (!point.ToIntVec3().InBounds(map)) continue;
                FleckMaker.ThrowDustPuff(point, map, 0.9f);
            }
        }

        private static void Static(FleckDef def, Vector3 position, Map map, float scale)
        {
            if (def == null) return;
            if (!position.ToIntVec3().InBounds(map)) return;
            FleckMaker.Static(position, map, def, scale);
        }
    }
}
