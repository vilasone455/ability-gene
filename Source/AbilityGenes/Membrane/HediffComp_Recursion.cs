using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbilityGenes
{
    public class HediffCompProperties_Recursion : HediffCompProperties
    {
        /// <summary>How far out the membrane starts halving an approach.</summary>
        public float fieldRadius = 3.9f;

        /// <summary>
        /// Ticks for a captured round to halve its remaining distance. At 360 a round entering
        /// at the field edge takes roughly the whole ability to drift in to touching distance.
        /// </summary>
        public float projectileHalfLifeTicks = 360f;

        /// <summary>
        /// Floor on a held round's distance, in cells. Purely cosmetic - after enough halvings
        /// the round would render inside the holder's sprite.
        /// </summary>
        public float projectileMinDistance = 0.35f;

        /// <summary>
        /// Ticks for an attacker's chance to connect to halve. At 60, ten seconds of swinging
        /// is about a thousandfold reduction, and it never reaches zero.
        /// </summary>
        public float meleeHalfLifeTicks = 60f;

        /// <summary>
        /// What is left of damage that arrives with no verb behind it - explosions, fire,
        /// collapsing roofs. These have nothing to miss with and nothing in flight to hold, so
        /// the approach is scaled directly. Set to 1 to let blasts through Infinity untouched.
        /// </summary>
        public float verblessDamageFactor = 0.15f;

        /// <summary>
        /// Ticks from opening the membrane to a severity of 1, which is where the last stage
        /// takes Breathing to zero. The pawn suffocates; this is the ability's only real limit.
        /// </summary>
        public int suffocationTicks = 2400;

        public HediffCompProperties_Recursion()
        {
            compClass = typeof(HediffComp_Recursion);
        }
    }

    /// <summary>
    /// Holds the membrane open. Owns three things the patches read: which projectiles are
    /// captured, how long each attacker has been inside the field, and the suffocation clock.
    ///
    /// Everything rate-sensitive runs once per *game* tick rather than once per pawn tick, for
    /// the same reason the time lattice does it - a pawn running accelerated ticks its own
    /// hediffs several times per game tick, which would suffocate them at the multiplier.
    /// </summary>
    public class HediffComp_Recursion : HediffComp
    {
        private static Texture2D closeIcon;

        private int lastGameTickProcessed = -1;
        private int ticksOpen;

        /// <summary>Attacker -> ticks spent inside the field. Rebuilt every game tick.</summary>
        private readonly Dictionary<Pawn, int> contact = new Dictionary<Pawn, int>();
        private readonly List<Pawn> contactScratch = new List<Pawn>();

        public HediffCompProperties_Recursion Props => (HediffCompProperties_Recursion)props;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead)
            {
                Close();
                return;
            }

            RecursionRegistry.Report(this);

            int now = Find.TickManager.TicksGame;
            if (now == lastGameTickProcessed) return;
            lastGameTickProcessed = now;

            ticksOpen++;

            if (pawn.Spawned)
            {
                CaptureNearbyProjectiles(pawn);
                TrackContact(pawn);
            }
            RecursionRegistry.AdvanceHeldBy(this);

            // The clock. Severity drives the Breathing ramp in XML; at 1 the last stage takes
            // Breathing to zero and the pawn dies, which is the whole cost of the ability.
            if (Props.suffocationTicks > 0)
            {
                parent.Severity += 1f / Props.suffocationTicks;
            }
        }

        /// <summary>
        /// Hostile rounds that come inside the field are taken off the engine's clock and put
        /// on the halving curve. Friendly fire is left alone: a colonist shooting past the
        /// holder should not have their round parked on their own ally's face.
        /// </summary>
        private void CaptureNearbyProjectiles(Pawn pawn)
        {
            Map map = pawn.Map;
            if (map == null) return;

            List<Thing> projectiles = map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);
            if (projectiles.Count == 0) return;

            float radiusSquared = Props.fieldRadius * Props.fieldRadius;
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile projectile = projectiles[i] as Projectile;
                if (projectile == null || projectile.Destroyed) continue;

                HalvingProjectile existing;
                if (RecursionRegistry.TryGetCapture(projectile, out existing)) continue;

                Thing launcher = projectile.Launcher;
                if (launcher == null || launcher == pawn) continue;
                if (!launcher.HostileTo(pawn)) continue;

                Vector3 offset = projectile.ExactPosition - pawn.DrawPos;
                offset.y = 0f;
                if (offset.sqrMagnitude > radiusSquared) continue;

                RecursionRegistry.Capture(projectile, new HalvingProjectile(
                    projectile, this, Props.projectileHalfLifeTicks, Props.projectileMinDistance));
            }
        }

        /// <summary>
        /// Counts how long each hostile has been inside the field. Melee has no position and no
        /// travel - the swing resolves in the instant it is cast - so the curve cannot run on
        /// distance the way a projectile's does. It runs on time in contact instead, which is
        /// nearer the original idea anyway: it is the number of halvings that matters.
        /// </summary>
        private void TrackContact(Pawn pawn)
        {
            Map map = pawn.Map;
            if (map == null) return;

            contactScratch.Clear();
            foreach (Pawn other in map.mapPawns.AllPawnsSpawned)
            {
                if (other == pawn || other.Dead) continue;
                if (!other.Position.InHorDistOf(pawn.Position, Props.fieldRadius)) continue;
                if (!other.HostileTo(pawn)) continue;
                contactScratch.Add(other);
            }

            for (int i = 0; i < contactScratch.Count; i++)
            {
                Pawn other = contactScratch[i];
                int ticks;
                contact[other] = contact.TryGetValue(other, out ticks) ? ticks + 1 : 0;
            }

            // Anyone who left the field starts their halvings again if they come back. Counts
            // alone cannot decide this - one attacker leaving as another arrives leaves the
            // dictionary the same size with the wrong contents.
            List<Pawn> gone = null;
            foreach (KeyValuePair<Pawn, int> pair in contact)
            {
                if (contactScratch.Contains(pair.Key)) continue;
                if (gone == null) gone = new List<Pawn>();
                gone.Add(pair.Key);
            }
            if (gone == null) return;
            for (int i = 0; i < gone.Count; i++) contact.Remove(gone[i]);
        }

        /// <summary>
        /// What is left of an attacker's chance to connect. Never zero - the fist is still
        /// arriving, it is just doing so in steps that keep halving.
        /// </summary>
        public float MeleeConnectFactor(Pawn attacker)
        {
            if (attacker == null) return 1f;

            int ticks;
            if (!contact.TryGetValue(attacker, out ticks)) return 1f;

            return Mathf.Pow(0.5f, ticks / Mathf.Max(1f, Props.meleeHalfLifeTicks));
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.IsColonistPlayerControlled) yield break;

            if (closeIcon == null)
            {
                // Resolved from the draw path, which is always the main thread.
                closeIcon = ContentFinder<Texture2D>.Get("UI/Abilities/MechSmokepop", false);
            }

            yield return new Command_Action
            {
                defaultLabel = "AG_RecursionCloseLabel".Translate(),
                defaultDesc = "AG_RecursionCloseDesc".Translate(),
                icon = closeIcon,
                action = delegate
                {
                    Messages.Message("AG_RecursionCollapsed".Translate(pawn.LabelShort),
                        pawn, MessageTypeDefOf.NeutralEvent, false);
                    pawn.health.RemoveHediff(parent);
                }
            };
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            RecursionRegistry.Report(this);

            Pawn pawn = Pawn;
            if (pawn != null)
            {
                Messages.Message("AG_RecursionUp".Translate(pawn.LabelShort),
                    pawn, MessageTypeDefOf.NeutralEvent, false);
            }
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            Close();
        }

        private void Close()
        {
            RecursionRegistry.ReleaseAllHeldBy(this);
            RecursionRegistry.Drop(this);
            contact.Clear();
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksOpen, "ticksOpen", 0);
        }
    }
}
