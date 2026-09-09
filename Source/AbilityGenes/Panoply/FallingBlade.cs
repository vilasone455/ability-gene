using RimWorld;
using UnityEngine;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// A summoned blade. Skyfaller owns impact timing, roof handling, sound and dust;
    /// the draw path stages a visible summon followed by an accelerating, point-first fall.
    /// </summary>
    public class FallingBlade : Skyfaller
    {
        private Pawn owner;
        private float lean;
        private int damage;

        private const int DescentTicks = 22;
        private static readonly Vector3 SummonOffset = new Vector3(-1.4f, 0f, 4.2f);

        private float Descent => Mathf.Clamp01(1f - ticksToImpact / (float)DescentTicks);

        public override Vector3 DrawPos
        {
            get
            {
                Vector3 ground = Position.ToVector3Shifted();
                ground.y = def.altitudeLayer.AltitudeFor();
                return ground + SummonOffset * (1f - Descent * Descent);
            }
        }

        /// <summary>
        /// Sets the blade up and pushes its arrival back by <paramref name="delayTicks"/>.
        ///
        /// The delay holds each blade at its gate longer before its final descent. The base
        /// age and impact timer also keep all visual phases stable across saving and loading.
        /// </summary>
        public void Configure(Pawn newOwner, int delayTicks, int impactDamage)
        {
            owner = newOwner;
            damage = impactDamage;
            lean = Rand.Range(-PanoplyDefaults.PlantedLeanRange, PanoplyDefaults.PlantedLeanRange);
            ticksToImpact += delayTicks;
        }

        /// <summary>
        /// Gates and trails are drawn without spawning per-frame motes or consuming game RNG.
        /// </summary>
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Vector3 ground = Position.ToVector3Shifted();
            ground.y = def.altitudeLayer.AltitudeFor();
            float reveal = Mathf.Clamp01(ageTicks / 16f);
            float heading = Mathf.Atan2(-SummonOffset.x, -SummonOffset.z) * Mathf.Rad2Deg;
            float gateAlpha = reveal * Mathf.Clamp01((ticksToImpact - DescentTicks + 10f) / 10f);
            PanoplyRainGraphics.DrawGate(ground + SummonOffset, gateAlpha, ageTicks, thingIDNumber);
            PanoplyRainGraphics.DrawLandingMark(ground, reveal, Descent);
            DrawDropSpotShadow();

            if (Descent > 0f)
                PanoplyRainGraphics.DrawTrail(drawLoc, SummonOffset.normalized, Descent);
            PanoplyGraphics.DrawBlade(drawLoc, heading, PanoplyDefaults.BladeDrawSize,
                Mathf.SmoothStep(0f, 1f, reveal));
        }

        /// <summary>
        /// What it hits on the way in, and what it leaves behind.
        ///
        /// One thing per cell takes the hit and a pawn is preferred over whatever is built
        /// there - a blade that lands on somebody standing in a doorway has not hit the door.
        /// The blade is planted either way, including through the body it just went into.
        ///
        /// `base.Impact` destroys this thing, so the map and cell are taken first and the
        /// planted blade goes down after - a skyfaller cannot spawn anything from inside its
        /// own impact.
        /// </summary>
        protected override void Impact()
        {
            Map map = Map;
            IntVec3 cell = Position;

            if (map != null && cell.InBounds(map))
                FleckMaker.Static(cell.ToVector3Shifted(), map, FleckDefOf.PsycastAreaEffect, 0.45f);

            if (map != null && cell.InBounds(map) && damage > 0)
            {
                Thing hit = cell.GetFirstPawn(map);
                if (hit == null) hit = cell.GetFirstBuilding(map);

                if (hit != null)
                {
                    hit.TakeDamage(new DamageInfo(DamageDefOf.Stab, damage, 0.35f, -1f, owner,
                        null, null, DamageInfo.SourceCategory.ThingOrUnknown, null));
                }
            }

            base.Impact();

            if (map == null || !cell.InBounds(map)) return;

            Thing spawned = GenSpawn.Spawn(PanoplyDefOf.AG_PanoplyBlade, cell, map);
            PlantedBlade blade = spawned as PlantedBlade;
            if (blade != null) blade.Configure(owner, lean);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref owner, "owner");
            Scribe_Values.Look(ref lean, "lean", 0f);
            Scribe_Values.Look(ref damage, "damage", 0);
        }
    }
}
