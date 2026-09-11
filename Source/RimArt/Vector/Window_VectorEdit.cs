using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The editor's panel: what is selected, what it will cost, and the two decisions the player
    /// is actually making about each group.
    ///
    /// It does not absorb input around itself. The map underneath has to stay clickable, because
    /// the selection is drawn there; the panel takes only what lands on it, and
    /// <see cref="Patch_Selector_SelectorOnGUI"/> hands the rest to the session instead of to
    /// ordinary pawn selection.
    ///
    /// Closing it by any route that is not Apply is a cancel. There is no half-committed state
    /// to leave behind, so the X, Escape and the button all mean the same thing.
    /// </summary>
    public class Window_VectorEdit : Window
    {
        private const float PanelWidth = 376f;
        private const float PanelHeight = 470f;
        private const float RowHeight = 26f;
        private const float Gap = 6f;

        private readonly VectorEditSession session;

        public Window_VectorEdit(VectorEditSession session)
        {
            this.session = session;

            doCloseX = true;
            closeOnCancel = true;
            closeOnAccept = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            draggable = true;
            drawShadow = true;
            forcePause = false;
        }

        public override Vector2 InitialSize => new Vector2(PanelWidth, PanelHeight);

        protected override void SetInitialSizeAndPosition()
        {
            windowRect = new Rect(UI.screenWidth - PanelWidth - 24f, 96f, PanelWidth, PanelHeight)
                .Rounded();
        }

        public override void PostClose()
        {
            base.PostClose();

            // Only reached by the X or Escape; Apply and Cancel have already let go of the
            // session before they take the window down.
            if (VectorEditSession.Current == session) session.Cancel();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (!session.StillValid())
            {
                session.Cancel();
                return;
            }

            session.DropInvalidMembers();

            float y = inRect.y;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, y, inRect.width - 28f, 30f), "AG_VectorEditTitle".Translate());
            y += 32f;

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.75f, 0.75f, 0.75f);
            Rect hint = new Rect(inRect.x, y, inRect.width, 42f);
            Widgets.Label(hint, "AG_VectorEditHint".Translate());
            GUI.color = Color.white;
            y += 46f;

            Text.Font = GameFont.Small;

            y = DrawStrain(inRect, y);
            y += Gap;

            y = DrawGroups(inRect, y);
            y += Gap;

            y = DrawSelectedControls(inRect, y);

            DrawButtons(inRect);
        }

        // ----------------------------------------------------------------- strain

        private float DrawStrain(Rect inRect, float y)
        {
            float current = VectorStrain.Current(session.Caster);
            float added = session.AddedStrain();
            float projected = VectorStrain.Projected(session.Caster, added);

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, RowHeight),
                "AG_VectorEditStrainNow".Translate(Percent(current)));
            y += RowHeight - 4f;

            Rect bar = new Rect(inRect.x, y, inRect.width, 12f);
            Widgets.DrawBoxSolid(bar, new Color(0.14f, 0.14f, 0.14f));
            Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(projected), bar.height),
                new Color(0.75f, 0.35f, 0.25f, 0.65f));
            Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(current), bar.height),
                new Color(0.85f, 0.55f, 0.2f));
            Widgets.DrawBox(bar, 1);
            y += 16f;

            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 18f),
                "AG_VectorEditStrainAdded".Translate(Percent(added), Percent(projected)));
            y += 18f;

            if (added > 0f && projected >= VectorEditDefaults.StrainCollapseThreshold)
            {
                GUI.color = new Color(1f, 0.45f, 0.4f);
                Widgets.Label(new Rect(inRect.x, y, inRect.width, 18f),
                    "AG_VectorEditCollapseWarning".Translate());
                GUI.color = Color.white;
                y += 18f;
            }

            Text.Font = GameFont.Small;
            return y;
        }

        private static string Percent(float severity)
        {
            return Mathf.RoundToInt(severity * 100f).ToString();
        }

        // ----------------------------------------------------------------- groups

        private float DrawGroups(Rect inRect, float y)
        {
            VectorEditGroup[] groups = session.Groups;
            for (int i = 0; i < groups.Length; i++)
            {
                VectorEditGroup group = groups[i];
                Rect row = new Rect(inRect.x, y, inRect.width, RowHeight);

                if (session.Selected == i) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);

                Rect chip = new Rect(row.x + 2f, row.y + 5f, 16f, 16f);
                Widgets.DrawBoxSolid(chip, new Color(group.Color.r, group.Color.g, group.Color.b,
                    group.Empty ? 0.25f : 1f));

                Rect label = new Rect(chip.xMax + 8f, row.y, row.width - 106f, row.height);
                string text = group.Empty
                    ? "AG_VectorEditGroupEmpty".Translate(i + 1).ToString()
                    : "AG_VectorEditGroupRow".Translate(i + 1, group.Members.Count,
                        group.Rotation.ToString("0"), group.Force.ToString("0.##")).ToString();

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(label, text);
                Text.Anchor = TextAnchor.UpperLeft;

                if (!group.Empty)
                {
                    // The invisible select button is drawn first so that it, rather than the
                    // window's own drag handling, takes a click on the row.
                    if (Widgets.ButtonInvisible(label)) session.SelectGroup(i);

                    Rect release = new Rect(row.xMax - 72f, row.y + 2f, 70f, row.height - 4f);
                    if (Widgets.ButtonText(release, "AG_VectorEditRelease".Translate()))
                    {
                        group.Clear();
                        if (session.Selected == i) session.SelectGroup(-1);
                    }
                }

                y += RowHeight + 2f;
            }

            return y;
        }

        // ----------------------------------------------------------------- rotation and force

        private float DrawSelectedControls(Rect inRect, float y)
        {
            VectorEditGroup group = session.SelectedGroup;
            if (group == null || group.Empty)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(new Rect(inRect.x, y, inRect.width, 36f), "AG_VectorEditNoGroup".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                return y + 36f;
            }

            Widgets.Label(new Rect(inRect.x, y, inRect.width, RowHeight),
                "AG_VectorEditRotation".Translate(group.Rotation.ToString("0")));
            y += RowHeight - 4f;

            Rect slider = new Rect(inRect.x, y, inRect.width - 64f, 24f);
            group.Rotation = Widgets.HorizontalSlider(slider, group.Rotation, -180f, 180f, true,
                null, "-180", "180", 1f);

            if (Widgets.ButtonText(new Rect(inRect.xMax - 58f, y - 2f, 58f, 24f),
                    "AG_VectorEditReset".Translate()))
            {
                group.Reset();
            }
            y += 30f;

            Widgets.Label(new Rect(inRect.x, y, inRect.width, RowHeight),
                "AG_VectorEditForce".Translate(group.Force.ToString("0.##"),
                    group.Range.ToString("0.#")));
            y += RowHeight - 2f;

            float[] forces = VectorEditDefaults.Forces;
            float buttonWidth = (inRect.width - (forces.Length - 1) * 4f) / forces.Length;
            for (int i = 0; i < forces.Length; i++)
            {
                Rect button = new Rect(inRect.x + i * (buttonWidth + 4f), y, buttonWidth, 28f);
                bool active = Mathf.Approximately(group.Force, forces[i]);
                if (active) Widgets.DrawHighlightSelected(button);
                if (Widgets.ButtonText(button, "x" + forces[i].ToString("0.##"))) group.Force = forces[i];
            }

            return y + 32f;
        }

        // ----------------------------------------------------------------- commit

        private void DrawButtons(Rect inRect)
        {
            float y = inRect.yMax - 34f;
            float half = (inRect.width - Gap) / 2f;

            bool changes = session.HasChanges();

            Rect apply = new Rect(inRect.x, y, half, 32f);
            if (changes)
            {
                if (Widgets.ButtonText(apply, "AG_VectorEditApply".Translate())) session.Apply();
            }
            else
            {
                Widgets.ButtonText(apply, "AG_VectorEditApply".Translate(), true, false, false);
                TooltipHandler.TipRegion(apply, "AG_VectorEditNoChange".Translate());
            }

            if (Widgets.ButtonText(new Rect(inRect.x + half + Gap, y, half, 32f),
                    "AG_VectorEditCancel".Translate()))
            {
                session.Cancel();
            }
        }
    }
}
