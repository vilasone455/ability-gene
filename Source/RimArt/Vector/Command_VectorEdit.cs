using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimArt
{
    /// <summary>
    /// The gizmo for vector manipulation, which is not a cast.
    ///
    /// Everything <see cref="Command_Ability"/> normally does after a click - pick a target,
    /// queue a job, walk there, warm up - is wrong for this ability. The rounds it acts on are
    /// already in the air a few cells away and the point is to stop the clock before the next
    /// one arrives, so the command opens the editor on the spot, in the same frame, whether the
    /// game is running or already paused. The cost is paid at Apply, not here.
    ///
    /// Grouped casting is refused because two carriers cannot share one paused editor, and
    /// aiCanUse is off in the def for the same reason a raider cannot be handed the panel.
    ///
    /// Reacting to a round is not something a player can be asked to do at normal speed - the
    /// catch runs twenty-one ticks ahead of a round's arrival, which is a third of a second.
    /// Reflex surge is what buys the time to press this; it is a separate decision, made before
    /// the shooting, with its own cost.
    /// </summary>
    public class Command_VectorEdit : Command_Ability
    {
        /// <summary>Reused by the in-reach count, which is recomputed every frame the gizmo draws.</summary>
        private static readonly List<Thing> inReach = new List<Thing>();

        public Command_VectorEdit(Ability ability, Pawn pawn) : base(ability, pawn)
        {
        }

        /// <summary>
        /// Reports the hover so the map can draw the reach ring.
        ///
        /// Vector shove shows its range the moment you pick it up, because picking it up starts
        /// a targeting cursor. This ability has no cursor: the click is the whole cast. Without
        /// something on hover there is no moment at all where the player can see what twelve
        /// cells covers, which is the one thing they need in order to decide.
        /// </summary>
        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            if (Mouse.IsOver(rect)) VectorHoverRing.Report(Ability.pawn);

            return base.GizmoOnGUI(topLeft, maxWidth, parms);
        }

        /// <summary>
        /// How many rounds are in reach, in the gizmo's corner.
        ///
        /// Deliberately not the tooltip. Command_Ability caches one tooltip string at
        /// construction for the multiple-selected path, so a count put there would be frozen at
        /// whatever was flying when the gizmo was first built - wrong in exactly the case where
        /// the player has their whole squad selected. TopRightLabel is read every frame instead,
        /// and it answers the question without needing a hover at all.
        ///
        /// Null when nothing is in reach, so the gizmo is clean in the nine-tenths of the game
        /// where nobody is shooting.
        /// </summary>
        public override string TopRightLabel
        {
            get
            {
                Pawn pawn = Ability.pawn;
                if (pawn == null || !pawn.Spawned || pawn.Map == null) return null;

                VectorEditSession.CatchableNow(pawn, inReach);
                int count = inReach.Count;
                inReach.Clear();

                return count > 0 ? count.ToString() : null;
            }
        }

        public override void ProcessInput(Event ev)
        {
            if (CurActivateSound != null) CurActivateSound.PlayOneShotOnCamera();
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();

            Find.DesignatorManager.Deselect();

            Pawn pawn = Ability.pawn;
            if (VectorEditSession.Begin(pawn, Ability)) return;

            Messages.Message("AG_VectorEditNothing".Translate(pawn.LabelShort),
                pawn, MessageTypeDefOf.RejectInput, false);
        }

        public override bool GroupsWith(Gizmo other)
        {
            return false;
        }
    }
}
