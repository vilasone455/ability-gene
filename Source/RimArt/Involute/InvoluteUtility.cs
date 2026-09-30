using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Obito's dimension: building it, where things arrive in it, and what happens to a hit that went through
    /// him while he was intangible.
    /// </summary>
    public static class InvoluteUtility
    {
        /// <summary>
        /// Guards against a passed-through instance passing through again: Obito can stand intangible in his
        /// own dimension while a relaunched round crosses it, and that round must not go round forever.
        /// </summary>
        private static bool passing;

        private static FleckDef entryFleck;
        private static bool entryFleckResolved;

        /// <summary>Rounds put back in flight inside a dimension since the game started, for the tests.</summary>
        public static int Relaunched { get; private set; }

        public static Gene_Involute GeneOf(Pawn pawn)
        {
            if (pawn == null || pawn.genes == null) return null;
            List<Gene> genes = pawn.genes.GenesListForReading;
            for (int i = 0; i < genes.Count; i++)
            {
                Gene_Involute gene = genes[i] as Gene_Involute;
                if (gene != null && gene.Active) return gene;
            }
            return null;
        }

        /// <summary>Builds the volume: see <see cref="Gene_Involute.EnsureVolume"/> for when.</summary>
        public static Map GenerateVolume(Gene_Involute gene)
        {
            if (gene == null) return null;

            InvoluteGeneExtension ext = gene.Ext;
            MapGeneratorDef generator = ext != null ? ext.volumeGenerator : null;
            if (generator == null)
            {
                Log.ErrorOnce("[RimArt] Involute organ has no volumeGenerator on its gene def.", 0x1CF01E);
                return null;
            }

            // Kamui's dimension is square: its layout is one size a side, so volumeSizeX is used for both.
            int size = ext != null ? ext.volumeSizeX : KamuiLayout.DefaultSize;

            Map source = gene.pawn != null ? gene.pawn.MapHeld : null;
            return PocketMapUtility.GeneratePocketMap(new IntVec3(size, 1, size), generator, null, source);
        }

        /// <summary>
        /// The mouth: Obito, absorbed allies and absorbed items arrive here. Only held enemies (on islands) and
        /// hits that went through him land anywhere else. In Kamui's dimension it is the middle of the main top.
        /// </summary>
        public static IntVec3 MouthCell(Map volume)
        {
            if (volume == null) return IntVec3.Invalid;

            MapComponent_KamuiDimension kamui = volume.GetComponent<MapComponent_KamuiDimension>();
            if (kamui != null && kamui.IsKamui)
            {
                IntVec3 mouth = kamui.MouthCell;
                if (mouth.IsValid && mouth.Standable(volume)) return mouth;
            }

            IntVec3 centre = volume.Center;
            if (centre.Standable(volume)) return centre;

            IntVec3 found;
            if (CellFinder.TryFindRandomCellNear(centre, volume, 12, c => c.Standable(volume), out found, 200))
            {
                return found;
            }
            return centre;
        }

        /// <summary>
        /// Where an absorbed pawn arrives. In Kamui's dimension a pawn hostile to the one who put it
        /// there lands on an island of its own, away from the main top, where nobody can walk to or
        /// from it; everyone else arrives at the mouth. A volume with no islands, or none with room,
        /// takes everyone at the mouth.
        /// </summary>
        public static IntVec3 LandingCell(Map volume, Pawn arriving, Pawn sender)
        {
            if (volume == null) return IntVec3.Invalid;

            MapComponent_KamuiDimension kamui = volume.GetComponent<MapComponent_KamuiDimension>();
            if (kamui != null && kamui.IsKamui && arriving != null && sender != null && arriving.HostileTo(sender))
            {
                IntVec3 island = kamui.IslandLanding();
                if (island.IsValid) return island;
            }
            return MouthCell(volume);
        }

        /// <summary>
        /// A hit went through Obito while he was intangible. It is not stopped, reduced or cancelled: it arrives
        /// in the dimension (a round is relaunched there, anything else lands on a random cell).
        /// </summary>
        public static void PassThrough(Gene_Involute gene, DamageInfo dinfo)
        {
            if (gene == null || passing) return;

            Map volume = gene.EnsureVolume();
            if (volume == null) return;

            passing = true;
            try
            {
                Deliver(volume, dinfo);
            }
            finally
            {
                passing = false;
            }
        }

        /// <summary>
        /// What the volume does with what it was handed.
        ///
        /// A round is put back in flight rather than set down. Teleporting it to a cell would
        /// make the whole mechanic cosmetic: a pawn occupies one cell out of a thousand, so a
        /// burst emptied into Obito would touch them about three times in a hundred.
        /// A round that crosses the room can hit anything standing in the way of it, which is
        /// two orders of magnitude more often and is the entire reason this is dangerous.
        ///
        /// It comes in from a random edge heading for a random point. The volume is not
        /// anywhere, so it has no orientation relative to whoever fired - there is no honest
        /// direction to preserve, and neither side gets to aim.
        /// </summary>
        private static void Deliver(Map volume, DamageInfo dinfo)
        {
            if (TryRelaunch(volume, dinfo)) return;

            // Anything with no round behind it - a swing, a blast, a fire - simply arrives.
            IntVec3 cell = InteriorCell(volume);
            if (!cell.IsValid) return;

            if (!entryFleckResolved)
            {
                entryFleck = DefDatabase<FleckDef>.GetNamedSilentFail("PsycastSkipFlashEntry");
                entryFleckResolved = true;
            }
            if (entryFleck != null) FleckMaker.Static(cell, volume, entryFleck, 0.9f);

            Thing hit = cell.GetFirstPawn(volume);
            if (hit == null) hit = cell.GetFirstBuilding(volume);
            if (hit == null) return;

            DamageInfo landed = dinfo;
            landed.SetHitPart(null);
            hit.TakeDamage(landed);
        }

        private static bool TryRelaunch(Map volume, DamageInfo dinfo)
        {
            ThingDef projectile = ProjectileOf(dinfo.Weapon);
            if (projectile == null) return false;

            // Any edge cell. Kamui's dimension keeps void along its edge, which nothing stands on and a
            // round flies over; asking for a standable edge cell there would find none.
            IntVec3 from;
            if (!CellFinder.TryFindRandomEdgeCellWith(c => true, volume, 0f, out from)) return false;

            IntVec3 to = InteriorCell(volume);
            if (!to.IsValid || to == from) return false;

            Thing spawned = GenSpawn.Spawn(projectile, from, volume);
            RoundBackend backend = Rounds.For(spawned);
            if (backend == null)
            {
                if (spawned != null && !spawned.Destroyed) spawned.Destroy();
                return false;
            }

            // No launcher, either way. Nothing in the volume knows who fired, and there is nobody
            // on this side for a kill to be credited to.
            Projectile round = spawned as Projectile;
            if (round != null)
            {
                round.Launch(null, from.ToVector3Shifted(), to, to, ProjectileHitFlags.All, false, null, null);
                Relaunched++;
                return true;
            }

            // A Combat Extended round, which has no Launch this can call without also handing it
            // a shooter, a muzzle height and a firing angle it does not have. Redirected instead,
            // which is the same straight line from the same place and is what the vector kit does
            // to CE's rounds already.
            Vector3 origin = from.ToVector3Shifted();
            Vector3 endpoint = to.ToVector3Shifted();

            Vector3 travel = endpoint - origin;
            travel.y = 0f;
            int ticks = Mathf.Max(1, Mathf.CeilToInt(travel.magnitude / Rounds.BaseSpeedPerTick(spawned)));

            backend.Redirect(spawned, origin, endpoint, ticks, 1f, null);
            Relaunched++;
            return true;
        }

        private static ThingDef ProjectileOf(ThingDef weapon)
        {
            if (weapon == null || weapon.Verbs == null) return null;
            for (int i = 0; i < weapon.Verbs.Count; i++)
            {
                ThingDef projectile = weapon.Verbs[i].defaultProjectile;
                if (projectile != null && projectile.thingClass != null) return projectile;
            }
            return null;
        }

        private static IntVec3 InteriorCell(Map volume)
        {
            if (volume == null) return IntVec3.Invalid;

            IntVec3 found;
            if (CellFinderLoose.TryGetRandomCellWith(c => c.Standable(volume), volume, 200, out found))
            {
                return found;
            }
            if (CellFinder.TryFindRandomCell(volume, c => c.Standable(volume), out found)) return found;
            return volume.Center;
        }
    }
}
