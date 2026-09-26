using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>What the three vacuum abilities share: they need the vacuum in hand and a working hand.</summary>
    public abstract class CompAbilityEffect_Vacuum : CompAbilityEffect
    {
        protected CompVacuum Vacuum => CompVacuum.HeldBy(parent.pawn);

        public override bool CanCast => base.CanCast && Unavailable() == null;

        protected virtual string Unavailable()
        {
            if (Vacuum == null) return "Requires a vacuum in hand.";
            if (!parent.pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return parent.pawn.LabelShortCap + " cannot manipulate.";
            return null;
        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = Unavailable();
            return reason != null || base.GizmoDisabled(out reason);
        }

        public override string ExtraTooltipPart()
        {
            CompVacuum vacuum = Vacuum;
            return vacuum == null ? null : vacuum.ContentsLine() + " (" + CompVacuum.Kg(vacuum.Fullness) + " / " + CompVacuum.Kg(vacuum.Props.capacityKg) + ")";
        }
    }

    public class CompProperties_VacuumSuck : CompProperties_AbilityEffect
    {
        /// <summary>Things within this many cells of the target cell are taken.</summary>
        public float radius = 2f;
        /// <summary>At most this many pawns lose their weapon per cast: the hostile nearest the target's centre.</summary>
        public int disarmPawns = 1;

        public CompProperties_VacuumSuck()
        {
            compClass = typeof(CompAbilityEffect_VacuumSuck);
        }
    }

    /// <summary>
    /// Suck. Every loose item (stacks, chunks, corpses, weapons on the floor) and every filth within
    /// the radius of the target cell that the target cell can see flies into the wand's head, lightest
    /// first, so the heaviest arrives last and stays in the mouth. Filth weighs nothing and is simply
    /// gone. The hostile pawn nearest the target's centre that holds a weapon loses it the same way;
    /// allies and neutrals keep theirs and no pawn is ever taken. A thing that would take the vacuum
    /// over its capacity stays where it is; at capacity Suck is refused. Each thing is taken when the
    /// picture shows it entering the head (MapComponent_Vacuum).
    /// </summary>
    public class CompAbilityEffect_VacuumSuck : CompAbilityEffect_Vacuum
    {
        public new CompProperties_VacuumSuck Props => (CompProperties_VacuumSuck)props;

        protected override string Unavailable()
        {
            string basic = base.Unavailable();
            if (basic != null) return basic;
            if (Vacuum.Full) return "Full: " + CompVacuum.Kg(Vacuum.Fullness) + " inside. Digest to empty it.";
            return null;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Map map = parent.pawn.Map;
            if (map == null || !target.Cell.InBounds(map)) return false;
            if (VacuumSuckTargets.Find(parent.pawn, target.Cell, Props, Vacuum?.Fullness ?? 0f, Vacuum?.Props.capacityKg ?? 0f).Count == 0)
            {
                if (throwMessages) Messages.Message("Nothing there the vacuum can take.", new TargetInfo(target.Cell, map), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            GenDraw.DrawRadiusRing(target.Cell, Props.radius);
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            CompVacuum vacuum = Vacuum;
            Map map = parent.pawn.Map;
            if (vacuum == null || map == null || !target.Cell.InBounds(map)) return null;
            List<VacuumSuckTargets.Taken> taken = VacuumSuckTargets.Find(parent.pawn, target.Cell, Props, vacuum.Fullness, vacuum.Props.capacityKg);
            if (taken.Count == 0) return null;
            float kg = 0f;
            int things = 0, filth = 0;
            for (int i = 0; i < taken.Count; i++)
            {
                kg += taken[i].kg;
                if (taken[i].thing is Filth) filth++;
                else things++;
            }
            return things + " things, " + filth + " filth: " + CompVacuum.Kg(vacuum.Fullness) + " -> " + CompVacuum.Kg(vacuum.Fullness + kg) + " / " + CompVacuum.Kg(vacuum.Props.capacityKg);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || Vacuum == null || !target.IsValid) return;
            caster.Map.GetComponent<MapComponent_Vacuum>().LandSuck(caster, target.Cell, Props);
        }
    }

    public class CompProperties_VacuumSpit : CompProperties_AbilityEffect
    {
        /// <summary>Blunt damage per kg of the thing fired, and the most one hit deals.</summary>
        public float damagePerKg = 1.5f;
        public float maxDamage = 40f;
        public float armorPenetration = 0f;
        /// <summary>A pawn hit is dazed (stunned) this long.</summary>
        public float stunSeconds = 1.2f;
        /// <summary>A targeted pawn is hit if it is at most this far from the cell it was aimed at when the thing lands.</summary>
        public float followRadius = 1.5f;

        public CompProperties_VacuumSpit()
        {
            compClass = typeof(CompAbilityEffect_VacuumSpit);
        }
    }

    /// <summary>
    /// Spit. The thing in the mouth is fired at a cell or pawn: a pawn there (or the targeted pawn,
    /// if it is still close) takes blunt damage by the thing's mass, capped, and is dazed; the thing
    /// lands as itself, beside the pawn it hit or on the cell. Only its kg leave; the stomach stays.
    /// Greyed out when the mouth is empty. The hit and the landing happen when the picture shows them
    /// (MapComponent_Vacuum).
    /// </summary>
    public class CompAbilityEffect_VacuumSpit : CompAbilityEffect_Vacuum
    {
        public new CompProperties_VacuumSpit Props => (CompProperties_VacuumSpit)props;

        protected override string Unavailable()
        {
            string basic = base.Unavailable();
            if (basic != null) return basic;
            if (Vacuum.Mouth == null) return "Nothing in the mouth.";
            return null;
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            CompVacuum vacuum = Vacuum;
            if (vacuum?.Mouth == null) return null;
            return vacuum.Mouth.LabelShortCap + ", " + CompVacuum.Kg(vacuum.MouthKg) + ": " + Damage(vacuum.MouthKg).ToString("0") + " blunt";
        }

        public float Damage(float kg) => Mathf.Min(Props.maxDamage, Props.damagePerKg * kg);

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || Vacuum?.Mouth == null || !target.IsValid) return;
            caster.Map.GetComponent<MapComponent_Vacuum>().LandSpit(caster, target, Props);
        }
    }

    public class CompProperties_VacuumDigest : CompProperties_AbilityEffect
    {
        /// <summary>The holder stands still this long per kg inside, and at least <see cref="minSeconds"/>.</summary>
        public float secondsPerKg = 0.05f;
        public float minSeconds = 0.5f;

        public CompProperties_VacuumDigest()
        {
            compClass = typeof(CompAbilityEffect_VacuumDigest);
        }
    }

    /// <summary>
    /// Digest. No target, no cooldown. The holder stands still while the canister chews: the mouth's
    /// thing goes down first, then the stomach drains until nothing is left. Everything is destroyed.
    /// The player can interrupt it; what is left then stays. Disabled when the vacuum is empty.
    /// </summary>
    public class CompAbilityEffect_VacuumDigest : CompAbilityEffect_Vacuum
    {
        public new CompProperties_VacuumDigest Props => (CompProperties_VacuumDigest)props;

        protected override string Unavailable()
        {
            string basic = base.Unavailable();
            if (basic != null) return basic;
            if (Vacuum.Fullness <= 0.001f) return "The vacuum is empty.";
            return null;
        }

        public float Seconds(float kg) => Mathf.Max(Props.minSeconds, kg * Props.secondsPerKg);

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || Vacuum == null) return;
            caster.Map.GetComponent<MapComponent_Vacuum>().LandDigest(caster, Props);
        }
    }

    /// <summary>What a Suck at a cell takes, in the order it flies.</summary>
    public static class VacuumSuckTargets
    {
        public struct Taken
        {
            public Thing thing;
            public float kg;
            /// <summary>The pawn whose weapon this is, or null for a thing on the ground.</summary>
            public Pawn from;
        }

        private static readonly List<Taken> Buffer = new List<Taken>();

        /// <summary>
        /// Every thing Suck would take at <paramref name="centre"/>, lightest first: filth, loose items
        /// and at most <see cref="CompProperties_VacuumSuck.disarmPawns"/> hostile pawns' weapons,
        /// dropping any whose weight would take the vacuum over <paramref name="capacity"/> from
        /// <paramref name="fullness"/>. The list is reused: copy it to keep it.
        /// </summary>
        public static List<Taken> Find(Pawn caster, IntVec3 centre, CompProperties_VacuumSuck props, float fullness, float capacity)
        {
            Buffer.Clear();
            Map map = caster?.Map;
            if (map == null || !centre.InBounds(map)) return Buffer;
            var armed = new List<Pawn>();
            foreach (IntVec3 c in GenRadial.RadialCellsAround(centre, props.radius, true))
            {
                if (!c.InBounds(map) || c != centre && !GenSight.LineOfSight(centre, c, map, true)) continue;
                // Items kept in a shelf or rack are stored, not loose.
                bool stored = c.GetEdifice(map) is Building_Storage;
                List<Thing> things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing t = things[i];
                    if (t is Pawn pawn)
                    {
                        if (pawn != caster && !pawn.Dead && pawn.equipment?.Primary != null && pawn.HostileTo(caster)) armed.Add(pawn);
                        continue;
                    }
                    if (t.Destroyed || !t.def.destroyable) continue;
                    if (t is Filth) { Buffer.Add(new Taken { thing = t, kg = 0f }); continue; }
                    if (stored || t.def.category != ThingCategory.Item || !t.def.EverHaulable) continue;
                    if (t == caster.equipment?.Primary) continue;
                    Buffer.Add(new Taken { thing = t, kg = CompVacuum.KgOf(t) });
                }
            }
            // The one pawn: the armed hostile nearest the target cell's centre.
            Vector3 mid = centre.ToVector3Shifted();
            armed.Sort((a, b) => Flat(a.DrawPos, mid).CompareTo(Flat(b.DrawPos, mid)));
            for (int i = 0; i < armed.Count && i < props.disarmPawns; i++)
                Buffer.Add(new Taken { thing = armed[i].equipment.Primary, kg = CompVacuum.KgOf(armed[i].equipment.Primary), from = armed[i] });

            // Lightest first (a stable order for equal weights: nearest the centre first), and only what fits.
            var keyed = new List<(Taken taken, float d, int index)>(Buffer.Count);
            for (int i = 0; i < Buffer.Count; i++)
                keyed.Add((Buffer[i], Flat(Buffer[i].from?.DrawPos ?? Buffer[i].thing.DrawPos, mid), i));
            keyed.Sort((a, b) => a.taken.kg != b.taken.kg ? a.taken.kg.CompareTo(b.taken.kg) : a.d != b.d ? a.d.CompareTo(b.d) : a.index.CompareTo(b.index));
            Buffer.Clear();
            float kg = fullness;
            for (int i = 0; i < keyed.Count; i++)
            {
                if (kg + keyed[i].taken.kg > capacity + 0.001f) continue;
                kg += keyed[i].taken.kg;
                Buffer.Add(keyed[i].taken);
            }
            return Buffer;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
