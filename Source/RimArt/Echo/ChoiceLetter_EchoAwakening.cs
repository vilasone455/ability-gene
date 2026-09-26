using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Awaken or Not yet, never automatic: the cost is permanent, so the player decides when it is
    /// taken, as with Origin: Blade. Declining only closes the letter; the candidate's gizmo keeps
    /// the way in open.
    /// </summary>
    public class ChoiceLetter_EchoAwakening : ChoiceLetter
    {
        public EchoDef echo;
        public Pawn pawn;

        private EchoRecord Record => echo == null ? null : GameComponent_Echoes.Get?.RecordFor(echo);

        public override bool CanShowInLetterStack => base.CanShowInLetterStack && pawn != null && !pawn.Dead
            && Record?.state == EchoState.Tracking && Record.candidate == pawn;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (!CanShowInLetterStack)
                {
                    yield return Option_Close;
                    yield break;
                }
                var awaken = new DiaOption("AG_EchoAwakenAccept".Translate())
                {
                    action = () =>
                    {
                        if (EchoUtility.Awaken(Record)) Find.LetterStack.RemoveLetter(this);
                    },
                    resolveTree = true
                };
                if (!EchoUtility.CanAwaken(Record, out string reason)) awaken.Disable(reason);
                yield return awaken;
                yield return new DiaOption("AG_EchoAwakenDefer".Translate())
                {
                    action = () => Find.LetterStack.RemoveLetter(this),
                    resolveTree = true
                };
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref echo, "echo");
            Scribe_References.Look(ref pawn, "pawn");
        }
    }
}
