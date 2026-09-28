using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    // All balance numbers of one well, read by the cast from its ability def. Gravity Well is Pain's;
    // another well can reuse the code with its own def: minRadius == maxRadius gives a fixed radius,
    // secondsPerEaten 0 a fixed duration, driftSpeed 0 no drift.
    public class CompProperties_AbilityGravityWell : CompProperties_AbilityEffect
    {
        // Cells from the caster to the well's centre.
        public float range = 20f;
        // Pull radius in cells: minRadius with nothing eaten, linear up to maxRadius at fullMass eaten.
        public float minRadius = 3f, maxRadius = 10f, fullMass = 200f;
        // Items and corpses inside this radius are eaten; pawns are held and hurt.
        public float coreRadius = 1.5f;
        // Pull in cells per second on body size 1 at the core edge, falling linearly to 0 at the pull
        // radius, divided by the body size (items: mass / bodyMass) when that is above 1.
        public float pullSpeed = 6f;
        // Direct bullets bend inside the pull radius, but never further out than this.
        public float maxBulletRadius = 5f;
        // Bullet turn in degrees per cell travelled at the core edge, falling linearly to 0 at the bullet radius.
        public float bendDegrees = 20f;
        // Duration once open: baseSeconds + secondsPerEaten x eaten, at most maxSeconds.
        public float baseSeconds = 4f, secondsPerEaten = 0.1f, maxSeconds = 15f;
        // Implosion blunt damage and radius, linear in eaten from 0 to fullMass.
        public float minDamage = 15f, maxDamage = 45f, minBurstRadius = 2f, maxBurstRadius = 3f;
        // Blunt damage per second to pawns held in the core.
        public float coreDamage = 4f;
        public float cooldownSeconds = 40f;
        // Eaten weights: a bullet or arrow, a round with an explosion radius (rocket, grenade, mortar
        // shell), a pawn or corpse per unit of body size. Items count their mass x stack count.
        public float roundMass = 2f, explosiveMass = 10f, bodyMass = 60f;
        // The centre creeps toward the heaviest thing it pulls, cells per second, never more than
        // leash cells from the cast point.
        public float driftSpeed = 0.5f, leash = 5f;

        public CompProperties_AbilityGravityWell() { compClass = typeof(CompAbilityEffect_GravityWell); }

        public static CompProperties_AbilityGravityWell For(AbilityDef def) =>
            def?.comps?.OfType<CompProperties_AbilityGravityWell>().FirstOrDefault();

        public float Growth(float eaten) => fullMass <= 0f ? 1f : GravityRules.Clamp(eaten / fullMass);
        public float PullRadius(float eaten) => Mathf.Lerp(minRadius, maxRadius, Growth(eaten));
        public float BulletRadius(float eaten) => Mathf.Min(PullRadius(eaten), maxBulletRadius);
        public int DurationTicks(float eaten) =>
            Mathf.RoundToInt(Mathf.Min(maxSeconds, baseSeconds + secondsPerEaten * eaten) * 60f);
        public float Damage(float eaten) => Mathf.Lerp(minDamage, maxDamage, Growth(eaten));
        public float BurstRadius(float eaten) => Mathf.Lerp(minBurstRadius, maxBurstRadius, Growth(eaten));
        public int CooldownTicks => Mathf.RoundToInt(cooldownSeconds * 60f);

        public float Pull(float distance, float resistance, float radius) =>
            pullSpeed * GravityRules.Clamp((radius - distance) / Mathf.Max(0.01f, radius - coreRadius)) / Mathf.Max(1f, resistance);
        public float Bend(float distance, float travel, float bulletRadius) =>
            bendDegrees * travel * GravityRules.Clamp((bulletRadius - distance) / Mathf.Max(0.01f, bulletRadius - coreRadius));

        // Weight of a pawn, corpse or item: what it adds when eaten, drift choice and pull resistance.
        public float Mass(Thing thing)
        {
            if (thing is Pawn pawn) return bodyMass * pawn.BodySize;
            if (thing is Corpse corpse) return bodyMass * corpse.InnerPawn.BodySize;
            return Mathf.Max(0f, thing.GetStatValue(StatDefOf.Mass, true, 60) * thing.stackCount);
        }
        public float Resistance(Thing thing) => thing is Pawn pawn ? pawn.BodySize : Mass(thing) / Mathf.Max(1f, bodyMass);
        public float RoundMass(Thing round) => round.def.projectile?.explosionRadius > 0f ? explosiveMass : roundMass;
    }
}
