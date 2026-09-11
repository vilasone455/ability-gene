using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Where the editor's map half is drawn from, and where a session that has lost its footing
    /// is noticed.
    ///
    /// A map component rather than anything on the pawn because the editor is a view of the map
    /// rather than a state the carrier is in, and because these are the two points in the frame
    /// that run while the game is paused - which is the only time this draws anything at all.
    /// </summary>
    public class MapComponent_VectorEdit : MapComponent
    {
        private readonly List<Thing> hoverScan = new List<Thing>();

        public MapComponent_VectorEdit(Map map) : base(map) { }

        private VectorEditSession SessionHere()
        {
            VectorEditSession session = VectorEditSession.Current;
            if (session == null || session.Map != map) return null;

            if (!session.StillValid())
            {
                session.Cancel();
                return null;
            }

            session.DropInvalidMembers();
            return session;
        }

        public override void MapComponentUpdate()
        {
            VectorEditSession session = SessionHere();
            if (session != null)
            {
                VectorEditDrawer.DrawWorld(session);
                return;
            }

            DrawHoverReach();
        }

        /// <summary>
        /// The reach ring, and what is standing inside it, while the mouse is on the gizmo.
        ///
        /// Skipped entirely once the editor is open: the editor draws its own ring and its own
        /// markers, and two sets of both on the same cells would read as a bug rather than as
        /// emphasis.
        /// </summary>
        private void DrawHoverReach()
        {
            Pawn hovered = VectorHoverRing.Consume();
            if (hovered == null || hovered.Map != map || !hovered.Spawned) return;

            VectorEditDrawer.DrawReach(hovered, hoverScan);
        }

        public override void MapComponentOnGUI()
        {
            VectorEditSession session = SessionHere();
            if (session != null) VectorEditDrawer.DrawOverlays(session);
        }

        /// <summary>
        /// Rounds land and are destroyed constantly, and nothing tells a registry when. Pruning
        /// on a slow interval keeps the edited table from outliving the rounds in it without
        /// putting a scan in front of every impact.
        /// </summary>
        public override void MapComponentTick()
        {
            if (VectorEditRegistry.EditedCount == 0) return;
            if (Find.TickManager.TicksGame % 600 != 0) return;
            VectorEditRegistry.Prune();
        }

        public override void MapRemoved()
        {
            base.MapRemoved();

            VectorEditSession session = VectorEditSession.Current;
            if (session != null && session.Map == map) VectorEditSession.Abandon();

            VectorEditRegistry.Prune();
        }
    }
}
