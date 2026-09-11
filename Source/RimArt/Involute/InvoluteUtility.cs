using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The three questions the organ has to answer: where the hole goes, what the volume is,
    /// and what happens to something that arrives at the boundary.
    /// </summary>
    public static class InvoluteUtility
    {
        /// <summary>
        /// Guards against a passed-through instance passing through again. The carrier can be
        /// standing in their own volume while their aperture is being fired into, which is
        /// intended - what is not intended is one round bouncing between the two forever.
        /// </summary>
        private static bool passing;

        private static FleckDef entryFleck;
        private static bool entryFleckResolved;

        private static FleckDef[] openFlecks;
        private static FleckDef[] closeFlecks;
        private static bool doorFlecksResolved;

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

        /// <summary>
        /// Where the hole goes.
        ///
        /// The candidate set is <see cref="ResonanceUtility.CanRing"/> - outside depth, has a
        /// parent, not conceptual, not the core part, nothing vital hanging off it. That is the
        /// same question Resonance asks and it is already answered off the game's own
        /// body data, so this holds for animals, mechs and modded races without a patch.
        ///
        /// The coverage band on top is the balance. RimWorld picks hit parts by coverage weight,
        /// so the share of incoming fire the hole eats is exactly the part's coverage - a torso
        /// would be near-immunity and a finger would be nothing. Hand-sized is the band where
        /// the pass-through is a real effect and not the only one the carrier has.
        /// </summary>
        public static BodyPartRecord RollHolePart(Pawn pawn, InvoluteGeneExtension ext)
        {
            if (pawn == null || pawn.RaceProps == null || pawn.RaceProps.body == null) return null;

            // Named part wins outright, filter and all - see forceHolePart.
            if (ext != null && ext.forceHolePart != null)
            {
                BodyPartRecord named = FindPart(pawn, ext.forceHolePart);
                if (named != null) return named;

                Log.WarningOnce("[RimArt] Involute organ: forceHolePart " + ext.forceHolePart.defName
                    + " is not on this body. Rolling instead.", 0x1CF01F);
            }

            float min = ext != null ? ext.minHoleCoverage : 0.02f;
            float max = ext != null ? ext.maxHoleCoverage : 0.10f;

            List<BodyPartRecord> banded = new List<BodyPartRecord>();
            List<BodyPartRecord> any = new List<BodyPartRecord>();

            List<BodyPartRecord> parts = pawn.RaceProps.body.AllParts;
            for (int i = 0; i < parts.Count; i++)
            {
                BodyPartRecord part = parts[i];
                if (!CanHoldHole(pawn, part, ext != null && ext.allowAnyPart)) continue;

                any.Add(part);
                float coverage = part.coverageAbsWithChildren;
                if (coverage >= min && coverage <= max) banded.Add(part);
            }

            if (banded.Count > 0) return banded.RandomElement();
            if (any.Count > 0) return any.RandomElement();
            return null;
        }

        /// <summary>
        /// Whether a hole can be in this part.
        ///
        /// The strict answer is ResonanceUtility.CanRing, which is Resonance's rule:
        /// the parts a note can live in are the parts a hole can. It throws out the body's core
        /// part and anything with a vital organ hanging off it, so it can never pick a torso.
        ///
        /// allowAnyPart drops those two exclusions and keeps the rest. Skin-depth and non-
        /// conceptual still hold, so this never lands on a liver - an internal organ is not
        /// something a bullet aimed at the body picks in the first place, and a hole in one
        /// would simply never fire. What it does let in is the torso and the head, which is the
        /// whole point of the flag.
        /// </summary>
        private static bool CanHoldHole(Pawn pawn, BodyPartRecord part, bool allowAnyPart)
        {
            if (!allowAnyPart) return ResonanceUtility.CanRing(pawn, part);

            if (part.depth != BodyPartDepth.Outside) return false;
            if (part.def == null || part.def.conceptual) return false;
            return pawn.health == null || !pawn.health.hediffSet.PartIsMissing(part);
        }

        /// <summary>The first present, non-missing part of this def on this body.</summary>
        public static BodyPartRecord FindPart(Pawn pawn, BodyPartDef def)
        {
            if (pawn == null || def == null || pawn.RaceProps == null || pawn.RaceProps.body == null) return null;

            List<BodyPartRecord> parts = pawn.RaceProps.body.AllParts;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].def != def) continue;
                if (pawn.health != null && pawn.health.hediffSet.PartIsMissing(parts[i])) continue;
                return parts[i];
            }
            return null;
        }

        /// <summary>
        /// Builds the volume. Lazily, on first connection - see the note on
        /// <see cref="Gene_Involute.EnsureVolume"/>.
        /// </summary>
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

            int x = ext != null ? ext.volumeSizeX : 32;
            int z = ext != null ? ext.volumeSizeZ : 32;

            Map source = gene.pawn != null ? gene.pawn.MapHeld : null;
            return PocketMapUtility.GeneratePocketMap(new IntVec3(x, 1, z), generator, null, source);
        }

        /// <summary>
        /// The mouth. Everything that goes in on purpose - the carrier, a posted item, a
        /// swallowed pawn - arrives here, because the volume has one opening and this is the
        /// inside of it. Only things that arrive by accident land anywhere else.
        /// </summary>
        public static IntVec3 MouthCell(Map volume)
        {
            if (volume == null) return IntVec3.Invalid;

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
        /// Something arrived at the boundary. It is not stopped, reduced or cancelled - the
        /// boundary is a door, and what arrives at a door goes through it.
        /// </summary>
        public static void PassThrough(Gene_Involute gene, DamageInfo dinfo, Thing at)
        {
            if (gene == null || passing) return;

            Map volume = gene.EnsureVolume();
            if (volume == null) return;

            Flash(at);

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
        /// The player has to be able to see the hole work. Without this they watch a raider
        /// shoot, see no damage, and learn nothing about the gene they are carrying.
        /// </summary>
        public static void FlashAt(Thing at)
        {
            Flash(at);
        }

        /// <summary>
        /// The hole opening on the ground, and closing again when the carrier steps back out.
        ///
        /// Both need saying out loud. The aperture vanishing on a fold-out is correct - there is
        /// one hole and it has gone back to riding on a body - but a thing that silently stops
        /// existing reads as a bug no matter how right it is. Vanilla already draws both halves
        /// of a skip, so this costs nothing.
        /// </summary>
        public static void OpenFlash(IntVec3 cell, Map map)
        {
            DoorFlash(cell, map, true);
        }

        public static void CloseFlash(IntVec3 cell, Map map)
        {
            DoorFlash(cell, map, false);
        }

        private static void DoorFlash(IntVec3 cell, Map map, bool opening)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map)) return;

            if (!doorFlecksResolved)
            {
                openFlecks = new[]
                {
                    DefDatabase<FleckDef>.GetNamedSilentFail("PsycastSkipInnerEntry"),
                    DefDatabase<FleckDef>.GetNamedSilentFail("PsycastSkipFlashEntry")
                };
                closeFlecks = new[]
                {
                    DefDatabase<FleckDef>.GetNamedSilentFail("PsycastSkipInnerExit"),
                    DefDatabase<FleckDef>.GetNamedSilentFail("PsycastSkipFlashExit")
                };
                doorFlecksResolved = true;
            }

            FleckDef[] flecks = opening ? openFlecks : closeFlecks;
            for (int i = 0; i < flecks.Length; i++)
            {
                if (flecks[i] != null) FleckMaker.Static(cell, map, flecks[i], 1.4f);
            }
        }

        /// <summary>
        /// Whether the hole leads anywhere right now. Either it is connected on the carrier's
        /// body, or they went through it and it is standing on the ground where they left it.
        /// One hole, two places it can be.
        /// </summary>
        public static bool IsOpen(Gene_Involute gene)
        {
            if (gene == null) return false;
            if (gene.pawn != null && InvoluteRegistry.IsVented(gene.pawn)) return true;

            Building_Aperture aperture = gene.Aperture;
            return aperture != null && !aperture.Destroyed && aperture.Spawned;
        }

        private static void Flash(Thing at)
        {
            // Resolved before the null check, not after: Deliver draws its own fleck on the far
            // side and would otherwise find this still unresolved on the very first pass.
            if (!entryFleckResolved)
            {
                entryFleck = DefDatabase<FleckDef>.GetNamedSilentFail("PsycastSkipFlashEntry");
                entryFleckResolved = true;
            }

            if (entryFleck == null) return;
            if (at == null || !at.Spawned || at.Map == null) return;

            FleckMaker.Static(at.DrawPos, at.Map, entryFleck, 0.7f);
        }

        /// <summary>
        /// What the volume does with what it was handed.
        ///
        /// A round is put back in flight rather than set down. Teleporting it to a cell would
        /// make the whole mechanic cosmetic: a pawn occupies one cell out of a thousand, so a
        /// burst emptied into the aperture would touch them about three times in a hundred.
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

            IntVec3 from;
            if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(volume), volume, 0f, out from)) return false;

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
