using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class VergilDefOf
    {
        public static AbilityDef AG_VergilJudgementCut;
        public static AbilityDef AG_VergilYamatoDash;
        public static AbilityDef AG_VergilSummonedSwords;
        public static AbilityDef AG_VergilJudgementCutEnd;

        /// <summary>The cast job of all four: it starts the picture and the pose with the warmup and holds Vergil after the fire.</summary>
        public static JobDef AG_CastVergil;

        public static HediffDef AG_VergilSwordPinned;
        public static HediffDef AG_VergilGone;

        /// <summary>Vergil's katana, the Echo's forced weapon.</summary>
        public static ThingDef AG_Yamato;

        public static EchoDef AG_Echo_Vergil;

        static VergilDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(VergilDefOf));
        }
    }

    /// <summary>What the four abilities and the pictures share.</summary>
    public static class VergilKit
    {
        /// <summary>
        /// The sketches stand their pawns with the feet on the point they are given and draw the chest 0.3
        /// cells north of it; the game draws a pawn centred on its DrawPos, with the feet about 0.3 cells
        /// south. Every ported picture is handed this ground point, so the sketch's chest lands on the real
        /// pawn's chest.
        /// </summary>
        public const float FeetBelowDrawPos = 0.3f;

        public static Vector2 Ground(Vector3 drawPos) => new Vector2(drawPos.x, drawPos.z - FeetBelowDrawPos);
        public static Vector2 Ground(IntVec3 cell) => new Vector2(cell.x + 0.5f, cell.z + 0.5f - FeetBelowDrawPos);

        /// <summary>An ability def's comp properties of one kind (AbilityDef has no GetCompProperties).</summary>
        public static T Props<T>(AbilityDef def) where T : AbilityCompProperties
        {
            if (def?.comps == null) return null;
            for (int i = 0; i < def.comps.Count; i++)
                if (def.comps[i] is T props) return props;
            return null;
        }

        /// <summary>This pawn holds Yamato.</summary>
        public static bool Wields(Pawn pawn) => pawn?.equipment?.Primary?.def == VergilDefOf.AG_Yamato;

        /// <summary>The Vergil Echo's record, when this pawn is its manifested Host.</summary>
        public static EchoRecord Manifested(Pawn pawn)
        {
            EchoRecord record = GameComponent_Echoes.Get?.HostRecord(pawn);
            return record != null && record.manifested && record.def == VergilDefOf.AG_Echo_Vergil ? record : null;
        }

        /// <summary>A pawn the abilities may cut and mark: spawned, alive and hostile to Vergil.</summary>
        public static bool Foe(Pawn caster, Pawn pawn) =>
            pawn != null && pawn != caster && pawn.Spawned && !pawn.Dead && pawn.HostileTo(caster);

        /// <summary>
        /// One cut from an ability. No weapon is named, so the melee Style hook does not count it again;
        /// the ability adds its own Style.
        /// </summary>
        public static DamageWorker.DamageResult Cut(Thing victim, Pawn caster, DamageDef def, float amount, float armorPenetration, float angle)
        {
            if (victim == null || victim.Destroyed || amount <= 0f) return null;
            var dinfo = new DamageInfo(def ?? DamageDefOf.Cut, amount, armorPenetration, angle, caster);
            return victim.TakeDamage(dinfo);
        }
    }
}
