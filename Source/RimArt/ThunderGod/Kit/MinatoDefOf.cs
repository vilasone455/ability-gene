using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class MinatoDefOf
    {
        public static EchoDef AG_Echo_Minato;

        public static AbilityDef AG_ThunderGodJump;
        public static AbilityDef AG_ThunderGodChain;
        public static AbilityDef AG_GuidingThunder;
        public static AbilityDef AG_Rasengan;

        /// <summary>Sealing touch's mark on a pawn Minato hit in melee (<see cref="Hediff_MinatoSeal"/>).</summary>
        public static HediffDef AG_MinatoSeal;

        public static JobDef AG_CastMinato;

        static MinatoDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MinatoDefOf));
        }
    }

    /// <summary>Small shared pieces of Minato's kit (docs/flying-thunder-god-kit.md, docs/hero-echo.md).</summary>
    public static class MinatoKit
    {
        /// <summary>
        /// The sketches stand their pawns with the feet on the point they are given; the game draws a pawn
        /// centred on its DrawPos with the feet about 0.3 cells south (the same as <see cref="VergilKit.Ground(Vector3)"/>).
        /// </summary>
        public static Vector2 Ground(Vector3 drawPos) => VergilKit.Ground(drawPos);
        public static Vector2 Ground(IntVec3 cell) => VergilKit.Ground(cell);
        /// <summary>A kunai lying on the ground is drawn on its cell's centre, not at a pawn's feet.</summary>
        public static Vector2 Flat(IntVec3 cell) => new Vector2(cell.x + 0.5f, cell.z + 0.5f);

        public static T Props<T>(AbilityDef def) where T : AbilityCompProperties => VergilKit.Props<T>(def);

        /// <summary>The Minato Echo's record, when this pawn is its manifested Host.</summary>
        public static EchoRecord Manifested(Pawn pawn)
        {
            EchoRecord record = GameComponent_Echoes.Get?.HostRecord(pawn);
            return record != null && record.manifested && record.def == MinatoDefOf.AG_Echo_Minato ? record : null;
        }

        /// <summary>In Minato's hero form (the manifest hediff, which is also what seals his kunai).</summary>
        public static bool IsMinato(Pawn pawn) => KunaiSeal.ThrowsSealed(pawn);

        /// <summary>A pawn the abilities strike: spawned, alive, not Minato, hostile to him.</summary>
        public static bool Foe(Pawn caster, Pawn pawn) =>
            pawn != null && pawn != caster && pawn.Spawned && !pawn.Dead && pawn.HostileTo(caster);

        public static Vector3 Flat3(Vector2 v) => new Vector3(v.x, 0f, v.y);
    }
}
