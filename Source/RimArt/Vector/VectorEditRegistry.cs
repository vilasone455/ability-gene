using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Which rounds are currently flying at something other than their def's speed and damage,
    /// and by how much.
    ///
    /// Same shape and the same reason as <see cref="RecursionRegistry"/>: both hot paths behind
    /// it - Projectile.StartingTicksToImpact, read from the position lerp several times per
    /// projectile per tick, and Projectile.DamageAmount, read at every impact - must cost one
    /// static integer read when nothing has been edited, which is almost always.
    ///
    /// A round at ×1 force is not an entry. Force is absolute rather than cumulative, so the
    /// baseline is exactly "no entry" and an edit back to ×1 removes the round from the table
    /// rather than storing a redundant one.
    ///
    /// The table is committed state and is scribed with the save by
    /// <see cref="GameComponent_VectorEdit"/>; the editor's own draft settings are not.
    /// </summary>
    public static class VectorEditRegistry
    {
        private static Dictionary<Thing, float> forces = new Dictionary<Thing, float>();
        private static readonly List<Thing> scratch = new List<Thing>();

        public static int EditedCount => forces.Count;

        public static void Register(Thing projectile, float force)
        {
            if (projectile == null) return;

            if (force > 0.999f && force < 1.001f) forces.Remove(projectile);
            else forces[projectile] = force;
        }

        /// <summary>
        /// Hot path. The two postfixes that read it are already behind an EditedCount check, but
        /// the membrane's capture scan asks it of every round in the air every tick a field is
        /// open, and that one has no such gate in front of it - so the gate is in here as well.
        /// </summary>
        public static float ForceFor(Thing projectile)
        {
            if (forces.Count == 0) return 1f;

            float force;
            return forces.TryGetValue(projectile, out force) ? force : 1f;
        }

        public static void Clear()
        {
            forces.Clear();
        }

        /// <summary>
        /// Forgets rounds that have landed, been destroyed or left the map. Nothing else prunes
        /// this table: a projectile has no removal callback the registry can hang off, and an
        /// entry outliving its round would otherwise keep the hot-path check switched on for
        /// the rest of the game.
        /// </summary>
        public static void Prune()
        {
            if (forces.Count == 0) return;

            scratch.Clear();
            foreach (KeyValuePair<Thing, float> pair in forces)
            {
                // A dictionary key is never null, so destroyed and despawned are the only
                // two ways a round leaves the table.
                Thing projectile = pair.Key;
                if (projectile.Destroyed || !projectile.Spawned) scratch.Add(projectile);
            }

            for (int i = 0; i < scratch.Count; i++) forces.Remove(scratch[i]);
            scratch.Clear();
        }

        /// <summary>
        /// Saved and loaded by reference, so an edited round mid-flight when the game is saved
        /// resumes at the same speed and damage rather than snapping back to its def.
        /// </summary>
        public static void Expose()
        {
            Scribe_Collections.Look(ref forces, "AG_vectorEditForces", LookMode.Reference, LookMode.Value);

            // Deliberately not pruned here. Whether a just-loaded round counts as spawned yet
            // depends on where in the load the components are exposed, and dropping an entry
            // because the answer was "not yet" would silently un-edit a round mid-flight. The
            // periodic prune on the map component picks up anything stale a few seconds later.
            if (Scribe.mode == LoadSaveMode.PostLoadInit && forces == null)
            {
                forces = new Dictionary<Thing, float>();
            }
        }
    }
}
