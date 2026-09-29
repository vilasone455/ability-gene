using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The Reset's numbers, on the Ajin trait (AG_Sato.xml). Balance lives here; <see cref="AjinReset"/> holds
    /// the rules.
    /// </summary>
    public class AjinExtension : DefModExtension
    {
        /// <summary>Manifested and paid: seconds until he rises at his biggest piece.</summary>
        public float resetSeconds = 20f;
        /// <summary>Not manifested, or the pool cannot pay: ticks until he rises where he fell.</summary>
        public int slowResetTicks = 60000;
        /// <summary>Share of the body's total part health that, lost during the Reset, destroys the body.</summary>
        public float destroyedDamageShare = 0.5f;
        public List<DamageDef> explosionDamage = new List<DamageDef>();
        public List<AjinPieceNumbers> pieces = new List<AjinPieceNumbers>();
        public List<HediffDef> clearHediffs = new List<HediffDef>();

        private static AjinExtension cached;
        public static AjinExtension Get => cached ?? (cached = SatoDefOf.AG_Ajin.GetModExtension<AjinExtension>() ?? new AjinExtension());

        public AjinPieceNumbers For(AjinPiece piece)
        {
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i].piece == piece) return pieces[i];
            return AjinPieceNumbers.None;
        }

        public float Cost(AjinPiece piece) => For(piece).cost;
        public float Size(AjinPiece piece) => For(piece).size;
        public bool IsExplosion(DamageDef def) => def != null && explosionDamage.Contains(def);

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (resetSeconds <= 0f) yield return "resetSeconds must be above 0";
            if (slowResetTicks <= 0) yield return "slowResetTicks must be above 0";
            foreach (AjinPiece piece in new[] { AjinPiece.Body, AjinPiece.Leg, AjinPiece.Arm, AjinPiece.Hand, AjinPiece.Finger, AjinPiece.Ear })
                if (!pieces.Any(p => p.piece == piece)) yield return "pieces has no entry for " + piece;
        }
    }

    public class AjinPieceNumbers
    {
        public AjinPiece piece;
        /// <summary>Which piece is biggest: he rises from the biggest one he has.</summary>
        public float size;
        /// <summary>Charge a manifested Reset takes when he rises from this piece.</summary>
        public float cost;

        public static readonly AjinPieceNumbers None = new AjinPieceNumbers();
    }
}
