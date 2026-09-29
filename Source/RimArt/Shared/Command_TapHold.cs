using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimArt
{
    /// <summary>
    /// One button for a quick version and a charged version of an ability. Press it (left mouse or its hotkey):
    /// let go within <see cref="TapSeconds"/> and <see cref="tap"/> runs; hold past it and <see cref="charge"/> runs,
    /// then <see cref="release"/> runs when the button or key is let go, wherever the mouse is by then. Right-click or
    /// Esc while holding, or the game window losing focus, runs <see cref="cancel"/> instead (only once the charge has
    /// started; before that nothing has happened, so nothing is undone).
    ///
    /// Gizmos are built again every frame, so a hold is not tied to this object: <see cref="holder"/> names it (the
    /// ability's own state, say) and the callbacks are taken at the press. <see cref="GameComponent_TapHold"/> watches
    /// the button or key every frame, also while paused and after the pawn is deselected.
    ///
    /// While <see cref="fill"/> returns more than 0 the button is filled from the bottom by that share, with a line
    /// at each of <see cref="marks"/> (shares of the full bar), so the player can see where the steps are.
    /// </summary>
    public class Command_TapHold : Command
    {
        /// <summary>A press shorter than this is a tap. Real seconds, not game time: it reads the player's hand.</summary>
        public const float TapSeconds = 0.25f;

        public object holder;
        public Action tap, charge, release, cancel;
        public Func<float> fill;
        public float[] marks;
        public Color fillColour = new Color(0.55f, 0.8f, 1f, 0.35f);

        private static readonly Color MarkColour = new Color(1f, 1f, 1f, 0.75f);

        public Command_TapHold() { groupable = false; }

        public bool Held => GameComponent_TapHold.Holding(holder);

        protected override GizmoResult GizmoOnGUIInt(Rect butRect, GizmoRenderParms parms)
        {
            Event e = Event.current;
            KeyCode key = hotKey?.MainKey ?? KeyCode.None;
            // Taken before the base button sees it: the base acts when a click is let go, which is too late to tell a
            // tap from a hold. A used MouseDown also keeps the base's click from firing on the MouseUp.
            if (!disabled)
            {
                if (e.type == EventType.MouseDown && e.button == 0 && Mouse.IsOver(butRect) && GameComponent_TapHold.Press(this, KeyCode.None, Time.realtimeSinceStartup))
                {
                    CurActivateSound?.PlayOneShotOnCamera();
                    e.Use();
                }
                else if (key != KeyCode.None && !GizmoGridDrawer.drawnHotKeys.Contains(key) && hotKey.KeyDownEvent)
                {
                    // The key repeats while held; only the first press starts a hold.
                    if (!Held && GameComponent_TapHold.Press(this, key, Time.realtimeSinceStartup)) CurActivateSound?.PlayOneShotOnCamera();
                    e.Use();
                }
            }
            GizmoResult result = base.GizmoOnGUIInt(butRect, parms);
            float share = fill?.Invoke() ?? 0f;
            if (share > 0f && Event.current.type == EventType.Repaint)
            {
                share = Mathf.Clamp01(share);
                Widgets.DrawBoxSolid(new Rect(butRect.x, butRect.yMax - butRect.height * share, butRect.width, butRect.height * share), fillColour);
                if (marks != null)
                    foreach (float mark in marks)
                        Widgets.DrawBoxSolid(new Rect(butRect.x + 2f, butRect.yMax - butRect.height * mark - 1f, butRect.width - 4f, 2f), MarkColour);
            }
            return result;
        }

        /// <summary>Reached only by a gamepad or another mod activating the button: that is a tap.</summary>
        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            tap?.Invoke();
        }
    }

    /// <summary>
    /// The one hold in progress (a player has one mouse). Not saved: a hold is a hand on a button, and whatever the
    /// charge started is the ability's own state.
    /// </summary>
    public sealed class GameComponent_TapHold : GameComponent
    {
        private static object holder;
        private static Action tap, charge, release, cancel;
        private static KeyCode key;
        private static float pressedAt;
        private static bool charging;
        /// <summary>A game test drives <see cref="Step"/> itself; the real mouse and keys are not read.</summary>
        internal static bool testDriven;

        public GameComponent_TapHold(Game game) { Clear(); testDriven = false; }

        public static bool Holding(object who) => who != null && holder == who;

        /// <summary>True when the press starts a hold; false while another hold is in progress. <paramref name="now"/> is real seconds.</summary>
        internal static bool Press(Command_TapHold command, KeyCode by, float now)
        {
            if (holder != null || command.holder == null) return false;
            holder = command.holder;
            tap = command.tap;
            charge = command.charge;
            release = command.release;
            cancel = command.cancel;
            key = by;
            pressedAt = now;
            charging = false;
            return true;
        }

        public override void GameComponentUpdate()
        {
            if (holder == null || testDriven) return;
            if (!Application.isFocused) { Cancel(); return; }
            Step(key == KeyCode.None ? Input.GetMouseButton(0) : Input.GetKey(key), Time.realtimeSinceStartup);
        }

        /// <summary>One frame: the button or key is <paramref name="down"/> at real second <paramref name="now"/>.</summary>
        internal static void Step(bool down, float now)
        {
            if (holder == null) return;
            if (down)
            {
                if (!charging && now - pressedAt >= Command_TapHold.TapSeconds)
                {
                    charging = true;
                    Run(charge);
                }
                return;
            }
            Action done = charging ? release : tap;
            Clear();
            Run(done);
        }

        // Before the map and the main menu read the event (UIRoot.UIRootOnGUI calls game components first), so the
        // right-click is not also a move order and Esc does not also open the menu.
        public override void GameComponentOnGUI()
        {
            if (holder == null) return;
            Event e = Event.current;
            if ((e.type == EventType.MouseDown && e.button == 1) || (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape))
            {
                Cancel();
                e.Use();
            }
        }

        internal static void Cancel()
        {
            Action undo = charging ? cancel : null;
            Clear();
            Run(undo);
        }

        private static void Clear()
        {
            holder = null;
            tap = charge = release = cancel = null;
            key = KeyCode.None;
            charging = false;
        }

        private static void Run(Action action)
        {
            try { action?.Invoke(); }
            catch (Exception e) { Log.Error("[RimArt] A tap/hold button's action failed: " + e); }
        }
    }
}
