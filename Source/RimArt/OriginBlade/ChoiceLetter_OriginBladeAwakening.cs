using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Asks before awakening, rather than telling afterwards.
    ///
    /// Awakening is permanent and expensive: it destroys psylinks and psycasts and forbids
    /// ranged weapons for the rest of the pawn's life. It used to happen on its own the tick
    /// the last requirement was met, which is a trap. A player can complete five studies on a
    /// Melee 8 pawn early, and years later that pawn crosses Melee 14 and silently loses a
    /// level 4 psylink -- with the warning last shown several play sessions ago.
    ///
    /// Vanilla already solves this shape with growth moments: it stops and makes the player
    /// choose. Declining is not final. The Origin: Blade command becomes an awaken button once
    /// the requirements are met, so a pawn who was passed over can still be taken through later.
    /// </summary>
    public class ChoiceLetter_OriginBladeAwakening : ChoiceLetter
    {
        public Pawn pawn;

        public override bool CanShowInLetterStack => base.CanShowInLetterStack
            && pawn != null && !pawn.Dead && !OriginBladeUtility.HasOrigin(pawn);

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (!CanShowInLetterStack)
                {
                    yield return Option_Close;
                    yield break;
                }

                DiaOption awaken = new DiaOption("AG_OriginBladeAwakenAccept".Translate())
                {
                    action = () =>
                    {
                        OriginBladeUtility.Awaken(pawn);
                        Find.LetterStack.RemoveLetter(this);
                    },
                    resolveTree = true
                };
                yield return awaken;

                // Dismissing only closes the letter. The gizmo keeps the door open, so a
                // "not yet" never costs the player the origin permanently.
                yield return new DiaOption("AG_OriginBladeAwakenDefer".Translate())
                {
                    action = () => Find.LetterStack.RemoveLetter(this),
                    resolveTree = true
                };
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref pawn, "pawn");
        }
    }
}
