using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class PainDefOf
    {
        public static EchoDef AG_Echo_Pain;

        public static AbilityDef AG_ShinraTensei;
        public static AbilityDef AG_GravityWell;
        public static AbilityDef AG_PainBanshoTenin;
        public static AbilityDef AG_PainBlackReceiver;

        /// <summary>Black Receiver's rods in a pawn (<see cref="Hediff_PainRods"/>).</summary>
        public static HediffDef AG_PainRods;
        public static HediffDef AG_EchoManifest_Pain;

        public static JobDef AG_CastPain;

        static PainDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PainDefOf));
        }
    }

    /// <summary>Small shared pieces of Pain's kit (docs/hero-echo.md).</summary>
    public static class PainKit
    {
        public static CompProperties_BanshoTenin BanshoProps => VergilKit.Props<CompProperties_BanshoTenin>(PainDefOf.AG_PainBanshoTenin);
        public static CompProperties_BlackReceiver ReceiverProps => VergilKit.Props<CompProperties_BlackReceiver>(PainDefOf.AG_PainBlackReceiver);

        /// <summary>The sketches stand their pawns with the feet on the point they are given (VergilKit.Ground).</summary>
        public static Vector2 Ground(Vector3 drawPos) => VergilKit.Ground(drawPos);
        public static Vector2 Ground(IntVec3 cell) => VergilKit.Ground(cell);

        /// <summary>Pain has this ability now: his Echo grants all four only while he is manifested.</summary>
        public static bool Has(Pawn pawn, AbilityDef def) => pawn?.abilities?.GetAbility(def) != null;

        /// <summary>
        /// Pinned by Black Receiver, or carried by a Banshō pull right now: Shinra Tensei, Banshō Ten'in and Gravity
        /// Well do not move it.
        /// </summary>
        public static bool Unmovable(Pawn pawn) => PainRods.Pinned(pawn) || (GameComponent_Pain.Instance?.InPull(pawn) ?? false);

        /// <summary>A pawn Pain's pull and rods take: spawned, alive, not downed, not Pain, hostile to him.</summary>
        public static bool Foe(Pawn caster, Pawn pawn) =>
            pawn != null && pawn != caster && pawn.Spawned && !pawn.Dead && !pawn.Downed && pawn.HostileTo(caster);

        /// <summary>
        /// The Echo's charge for <paramref name="def"/>, when <paramref name="pawn"/> is its manifested Host; 0 otherwise.
        /// For the abilities whose buttons start them without Ability.Activate (Shinra Tensei), where the Echo's
        /// own payment patch never runs.
        /// </summary>
        public static float Cost(Pawn pawn, AbilityDef def) => EchoUtility.ManifestedWith(pawn, def)?.def.CastCost(def) ?? 0f;

        /// <summary>Why the pool cannot pay <paramref name="def"/> now, or null.</summary>
        public static string CannotPay(Pawn pawn, AbilityDef def)
        {
            float cost = Cost(pawn, def);
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            if (cost <= 0f || echoes == null || echoes.charge >= cost) return null;
            return "AG_EchoCastNoCharge".Translate(cost.ToString("0"), echoes.charge.ToString("0"));
        }

        /// <summary>Takes the charge for <paramref name="def"/>; false (and nothing taken) when the pool cannot pay.</summary>
        public static bool Pay(Pawn pawn, AbilityDef def)
        {
            float cost = Cost(pawn, def);
            return cost <= 0f || GameComponent_Echoes.Get?.TrySpend(cost) == true;
        }

        /// <summary>
        /// Shinra Tensei and Banshō Ten'in share the canon 5 s gap (manga ch. 427): after either one, the other waits.
        /// Seconds left for <paramref name="pawn"/>, or 0.
        /// </summary>
        public static float DevaGapLeft(Pawn pawn)
        {
            int until = GameComponent_Pain.Instance?.DevaUntil(pawn) ?? 0;
            return Mathf.Max(0, until - Find.TickManager.TicksGame) / 60f;
        }

        public static void StartDevaGap(Pawn pawn)
        {
            float seconds = BanshoProps?.devaGapSeconds ?? 5f;
            GameComponent_Pain.Instance?.SetDevaUntil(pawn, Find.TickManager.TicksGame + Mathf.RoundToInt(seconds * 60f));
        }
    }
}
