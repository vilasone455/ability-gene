using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Scatter is hard to test on purpose, for the same reason the involute organ's
    /// pass-through is: it only fires on a real hit, from a real instigator, on a carrier who
    /// is awake and holding a charge. Waiting for that in a fight is correct as balance and
    /// useless as a test loop.
    ///
    /// Both actions here drive the real path. Neither reaches into <see cref="Scatter"/>
    /// directly - the first fires a damage instance at the carrier and lets the TakeDamage
    /// prefix decide, so anything that works here works when a raider pulls a trigger.
    /// </summary>
    public static class DebugActions_Dispersal
    {
        [RimArtDebug("Dispersal", "shoot the carrier", RimArtDebugKind.Pawn)]
        private static void ShootCarrier(Pawn pawn)
        {
            Gene_Dispersal gene = DispersalRegistry.CarrierFor(pawn);
            if (gene == null)
            {
                Messages.Message(pawn.LabelShortCap + " has no dispersal plexus, or it has not ticked yet.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            // Somebody else on the map to be shot by, because damage with no live instigator is
            // exactly the case scatter declines - see Scatter.Applies.
            Pawn shooter = null;
            foreach (Pawn candidate in pawn.Map.mapPawns.AllPawnsSpawned)
            {
                if (candidate != pawn && !candidate.Dead) { shooter = candidate; break; }
            }
            if (shooter == null)
            {
                Messages.Message("Nobody else on the map to take the shot.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            pawn.TakeDamage(new DamageInfo(DamageDefOf.Bullet, 12f, 0f,
                (pawn.DrawPos - shooter.DrawPos).AngleFlat(), shooter));
        }

        [RimArtDebug("Dispersal", "fly to here")]
        private static void FlyHere()
        {
            IntVec3 cell = UI.MouseCell();
            Map map = Find.CurrentMap;

            foreach (object selected in Find.Selector.SelectedObjects)
            {
                Pawn pawn = selected as Pawn;
                if (pawn == null) continue;

                Gene_Dispersal gene = DispersalRegistry.CarrierFor(pawn);
                if (gene == null) continue;

                if (!JumpUtility.ValidJumpTarget(pawn, map, cell))
                {
                    Messages.Message("Nowhere to land there.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                DispersalFX.Depart(pawn.DrawPos, map);
                PawnFlyer flyer = PawnFlyer.MakeFlyer(DispersalDefOf.AG_DispersalFlock, pawn, cell,
                    null, null, false, null, null, cell);
                if (flyer == null) return;

                GenSpawn.Spawn(flyer, cell, map);
                Find.Selector.Select(pawn, false, false);
                return;
            }

            Messages.Message("Select a pawn with a dispersal plexus first.",
                MessageTypeDefOf.RejectInput, false);
        }

        [RimArtDebug("Dispersal", "refill the plexus", RimArtDebugKind.Pawn)]
        private static void Refill(Pawn pawn)
        {
            Gene_Dispersal gene = DispersalRegistry.CarrierFor(pawn);
            if (gene == null)
            {
                Messages.Message(pawn.LabelShortCap + " has no dispersal plexus.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            gene.Refill();
            Messages.Message(pawn.LabelShortCap + ": plexus refilled to " + gene.Charges + ".",
                MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
