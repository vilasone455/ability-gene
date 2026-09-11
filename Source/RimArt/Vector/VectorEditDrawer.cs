using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// What the paused map looks like while the editor is open.
    ///
    /// Two halves, drawn from two different places in the frame for two different reasons.
    /// Lines and the reach ring are world-space and go down in MapComponentUpdate, so they lie
    /// on the ground under everything and scale with the camera. Markers, numbers and the drag
    /// box are screen-space and go down in MapComponentOnGUI, which runs before the window
    /// stack - so the panel is drawn on top of them rather than under them.
    /// </summary>
    public static class VectorEditDrawer
    {
        private const float MarkerPixels = 14f;
        private const float EndpointPixels = 10f;
        private const float HeadingStubCells = 1.4f;

        private static readonly Color UnassignedColor = new Color(0.85f, 0.85f, 0.85f, 0.9f);

        /// <summary>The reach ring, in the editor's own first-group colour so the two read as one thing.</summary>
        private static readonly Color ReachRingColor = new Color(0.35f, 0.85f, 1f, 0.55f);

        /// <summary>
        /// The hover preview: how far the booster reaches, and which rounds are currently inside
        /// it.
        ///
        /// Both halves are needed. The ring alone answers "how far", which the player can also
        /// learn once by reading the description; the markers answer "is this worth pressing",
        /// which changes every tick and is the only question being asked at the moment the
        /// cursor is on the gizmo.
        /// </summary>
        public static void DrawReach(Pawn carrier, List<Projectile> scratch)
        {
            GenDraw.DrawRadiusRing(carrier.Position, VectorEditDefaults.ScanRadiusCells,
                ReachRingColor, null);

            VectorEditSession.CatchableNow(carrier, scratch);

            for (int i = 0; i < scratch.Count; i++)
            {
                Projectile round = scratch[i];
                if (round == null || round.Destroyed) continue;

                Vector3 at = round.ExactPosition;
                at.y = 0f;
                GenDraw.DrawCircleOutline(at, 0.5f, SimpleColor.Cyan);
            }

            scratch.Clear();
        }

        public static void DrawWorld(VectorEditSession session)
        {
            Map map = session.Map;

            GenDraw.DrawCircleOutline(session.Caster.DrawPos, VectorEditDefaults.ScanRadiusCells,
                SimpleColor.White);

            IReadOnlyList<CapturedProjectile> captured = session.Captured;
            for (int i = 0; i < captured.Count; i++)
            {
                CapturedProjectile round = captured[i];
                if (!round.StillValid(map)) continue;

                // The heading it came in on, always drawn: rotation is only legible against
                // the line the round was already following.
                GenDraw.DrawLineBetween(round.Position,
                    round.Position + round.Heading * HeadingStubCells, SimpleColor.White, 0.12f);
            }

            VectorEditGroup[] groups = session.Groups;
            for (int g = 0; g < groups.Length; g++)
            {
                VectorEditGroup group = groups[g];
                if (group.Empty) continue;

                bool isSelected = session.Selected == group.Index;
                float width = isSelected ? 0.24f : 0.16f;

                for (int i = 0; i < group.Members.Count; i++)
                {
                    CapturedProjectile round = group.Members[i];
                    if (!round.StillValid(map)) continue;

                    GenDraw.DrawLineBetween(round.Position,
                        round.EndpointFor(group.Rotation, group.Force, map), group.LineColor, width);
                }

                if (!isSelected) continue;

                Vector3 centre = group.Centre();
                Vector3 handle = session.HandlePosition(group);
                GenDraw.DrawLineBetween(centre, handle, group.LineColor, 0.1f);
                GenDraw.DrawCircleOutline(handle, 0.45f, group.LineColor);
            }
        }

        public static void DrawOverlays(VectorEditSession session)
        {
            if (Event.current.type != EventType.Repaint) return;

            Map map = session.Map;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;

            IReadOnlyList<CapturedProjectile> captured = session.Captured;
            for (int i = 0; i < captured.Count; i++)
            {
                CapturedProjectile round = captured[i];
                if (!round.StillValid(map)) continue;

                Color color = round.Group >= 0
                    ? session.Groups[round.Group].Color
                    : UnassignedColor;

                DrawMarker(UI.MapToUIPosition(round.Position), MarkerPixels, color,
                    round.Group >= 0 ? (round.Group + 1).ToString() : null);
            }

            VectorEditGroup[] groups = session.Groups;
            for (int g = 0; g < groups.Length; g++)
            {
                VectorEditGroup group = groups[g];
                if (group.Empty) continue;

                for (int i = 0; i < group.Members.Count; i++)
                {
                    CapturedProjectile round = group.Members[i];
                    if (!round.StillValid(map)) continue;

                    Vector3 endpoint = round.EndpointFor(group.Rotation, group.Force, map);
                    DrawMarker(UI.MapToUIPosition(endpoint), EndpointPixels, group.Color, null);
                }

                DrawGroupLabel(session, group);
            }

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            if (!session.Dragging) return;

            Rect box = session.DragBox;
            if (box.width < 1f && box.height < 1f) return;

            Widgets.DrawBox(box, 1);
            Widgets.DrawBoxSolid(box, new Color(1f, 1f, 1f, 0.08f));
        }

        /// <summary>Force and the range it buys, put where the group is rather than in the panel.</summary>
        private static void DrawGroupLabel(VectorEditSession session, VectorEditGroup group)
        {
            Vector2 at = UI.MapToUIPosition(group.Centre());
            Rect rect = new Rect(at.x - 60f, at.y - 34f, 120f, 18f);

            string text = "AG_VectorEditGroupTag".Translate(
                group.Index + 1, group.Force.ToString("0.##"), group.Range.ToString("0.#"));

            GUI.color = group.Color;
            Widgets.Label(rect, text);
            GUI.color = Color.white;
        }

        private static void DrawMarker(Vector2 at, float size, Color color, string label)
        {
            Rect rect = new Rect(at.x - size / 2f, at.y - size / 2f, size, size);

            Widgets.DrawBoxSolid(rect, new Color(color.r, color.g, color.b, 0.35f));
            GUI.color = color;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            if (label == null) return;

            GUI.color = color;
            Widgets.Label(new Rect(rect.x, rect.y - 14f, rect.width, 14f), label);
            GUI.color = Color.white;
        }
    }
}
