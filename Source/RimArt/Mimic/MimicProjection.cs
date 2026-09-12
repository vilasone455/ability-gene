using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Putting a decoy on the ground, which the thrown beacon and the debug action both need and
    /// neither should own.
    /// </summary>
    internal static class MimicProjection
    {
        /// <summary>
        /// Projects a copy of <paramref name="source"/> at or near <paramref name="cell"/>, and
        /// returns null if there was nowhere to put it.
        ///
        /// The old decoy goes first, before anything else can fail, so a second beacon never
        /// leaves the thrower with two - see <see cref="MapComponent_Mimics.ClearFor"/>.
        /// </summary>
        public static MimicDecoy Spawn(Pawn source, IntVec3 cell, Map map)
        {
            if (source == null || map == null) return null;

            IntVec3 where = FreeCellNear(cell, map);
            if (!where.IsValid) return null;

            MapComponent_Mimics.ClearFor(source, map);

            Thing thing = ThingMaker.MakeThing(MimicDefOf.AG_MimicDecoy);

            // The faction goes on before the spawn, and the order is not cosmetic.
            //
            // Spawning is what registers the decoy with AttackTargetsCache, and
            // AttackTargetsCache.RegisterTarget files a new target under every faction it is
            // hostile to *at that moment*, once. A decoy that spawns factionless is hostile to
            // nobody, is filed under nobody, and is never returned to any raider looking for
            // something to shoot - it would stand there as scenery with no error anywhere.
            // Faction assigned afterwards does not re-file it; only a hostility change does.
            thing.SetFactionDirect(source.Faction);

            MimicDecoy decoy = (MimicDecoy)GenSpawn.Spawn(thing, where, map);
            decoy.Project(source, source.Rotation);

            return decoy;
        }

        /// <summary>
        /// Somewhere to stand, starting at the cell asked for and walking outward.
        ///
        /// The same shape as the toy car's deploy search and for the same reason: a beacon thrown
        /// at a cell somebody is already standing in should still produce a decoy, one cell over,
        /// rather than nothing. Two decoys in one cell overlap exactly and only one can be
        /// clicked, so an existing decoy blocks a cell as firmly as a pawn does.
        /// </summary>
        private static IntVec3 FreeCellNear(IntVec3 cell, Map map)
        {
            foreach (IntVec3 candidate in
                     GenRadial.RadialCellsAround(cell, MimicDefaults.LandingSearchRadius, true))
            {
                if (!candidate.InBounds(map) || !candidate.Standable(map)) continue;
                if (candidate.GetFirstPawn(map) != null) continue;
                if (candidate.GetFirstThing<MimicDecoy>(map) != null) continue;

                Building_Door door = candidate.GetDoor(map);
                if (door != null) continue;

                return candidate;
            }

            return IntVec3.Invalid;
        }
    }
}
