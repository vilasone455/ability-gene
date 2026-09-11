using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// One open editor: the rounds it caught, the groups the player has drawn over them, and
    /// the paused clock it is holding.
    ///
    /// Everything here is draft state. Nothing it holds is scribed and nothing it holds is
    /// written back to a projectile until <see cref="Apply"/> runs, so cancelling is free and a
    /// session interrupted by the caster dying or the map changing simply stops existing. The
    /// committed half - which rounds are flying at what force - lives in
    /// <see cref="VectorEditRegistry"/> instead, and that is the only part that survives a save.
    ///
    /// The eligible set is frozen when the editor opens. Rounds fired after that are not offered
    /// even though the game is paused and none can arrive, because the alternative is a list
    /// that changes under a drag box.
    /// </summary>
    public class VectorEditSession
    {
        private enum DragMode { Create, Add, Remove }

        /// <summary>The one open editor, or null. Read from the Selector prefix every frame.</summary>
        public static VectorEditSession Current;

        /// <summary>Reused by the scan, so opening the editor does not allocate a second list.</summary>
        private static readonly List<Projectile> scratch = new List<Projectile>();

        public readonly Pawn Caster;
        public readonly Map Map;

        private readonly Ability ability;
        private readonly List<CapturedProjectile> captured = new List<CapturedProjectile>();
        private readonly VectorEditGroup[] groups = new VectorEditGroup[VectorEditDefaults.MaxGroups];
        private readonly TimeSpeed previousSpeed;

        private Window_VectorEdit window;
        private int selected = -1;
        private bool closed;

        private bool dragging;
        private DragMode dragMode;
        private Vector2 dragStart;
        private Vector2 dragCurrent;

        private bool rotating;
        private int rotatingGroup = -1;

        public IReadOnlyList<CapturedProjectile> Captured => captured;
        public VectorEditGroup[] Groups => groups;
        public int Selected => selected;
        public VectorEditGroup SelectedGroup => selected >= 0 ? groups[selected] : null;

        private VectorEditSession(Pawn caster, Ability ability, List<Projectile> found)
        {
            Caster = caster;
            Map = caster.Map;
            this.ability = ability;
            previousSpeed = Find.TickManager.CurTimeSpeed;

            for (int i = 0; i < groups.Length; i++) groups[i] = new VectorEditGroup(i);
            for (int i = 0; i < found.Count; i++) captured.Add(new CapturedProjectile(found[i]));
        }

        /// <summary>
        /// Opens the editor on whatever is catchable right now, and reports whether it did.
        /// An empty scan is not a failure and not a cast: nothing is spent, no cooldown starts,
        /// and the clock never stops.
        /// </summary>
        public static bool Begin(Pawn caster, Ability ability)
        {
            if (Current != null) return false;
            if (caster == null || !caster.Spawned || caster.Map == null) return false;

            Scan(caster, scratch);
            if (scratch.Count == 0)
            {
                scratch.Clear();
                return false;
            }

            VectorEditSession session = new VectorEditSession(caster, ability, scratch);
            int caught = scratch.Count;
            scratch.Clear();

            Current = session;

            // Read before the pause, so Apply can hand the clock back at the speed the fight
            // was actually running at rather than at whatever the editor left it on.
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;

            session.window = new Window_VectorEdit(session);
            Find.WindowStack.Add(session.window);

            Messages.Message("AG_VectorEditCaught".Translate(caster.LabelShort, caught),
                caster, MessageTypeDefOf.NeutralEvent, false);

            return true;
        }

        /// <summary>
        /// What the booster would take hold of if the gizmo were clicked this instant, for the
        /// hover preview. The caller owns the list, so drawing it costs no allocation per frame.
        /// </summary>
        public static void CatchableNow(Pawn caster, List<Projectile> into)
        {
            into.Clear();
            if (caster == null || !caster.Spawned || caster.Map == null) return;

            Scan(caster, into);
        }

        /// <summary>
        /// Every round the carrier can reach and the booster knows how to hold.
        ///
        /// Bullets only, which is also what takes in arrows and this mod's loosed blades - they
        /// are Bullets for exactly the same reason, so that vanilla resolves what they hit.
        /// Mortar shells and rockets are refused because an arc and a blast are not a vector the
        /// reflex can take hold of, and rounds already held by a phase barrier or standing still
        /// in a stasis field belong to those effects rather than to this one.
        /// </summary>
        private static void Scan(Pawn caster, List<Projectile> found)
        {
            found.Clear();

            Map map = caster.Map;
            List<Thing> things = map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);
            if (things.Count == 0) return;

            float radiusSquared = VectorEditDefaults.ScanRadiusCells * VectorEditDefaults.ScanRadiusCells;
            Vector3 eye = caster.DrawPos;
            eye.y = 0f;

            for (int i = 0; i < things.Count; i++)
            {
                Bullet bullet = things[i] as Bullet;
                if (bullet == null || bullet.Destroyed || !bullet.Spawned) continue;

                ProjectileProperties props = bullet.def.projectile;
                if (props == null || props.flyOverhead || props.explosionRadius > 0f) continue;

                if (RecursionRegistry.CapturedCount > 0)
                {
                    HalvingProjectile held;
                    if (RecursionRegistry.TryGetCapture(bullet, out held)) continue;
                }

                if (TimeBubbleRegistry.ActiveCount > 0 && TimeBubbleRegistry.IsFrozen(bullet)) continue;

                IntVec3 cell = bullet.Position;
                if (!cell.InBounds(map) || cell.Fogged(map)) continue;

                Vector3 offset = bullet.ExactPosition - eye;
                offset.y = 0f;
                if (offset.sqrMagnitude > radiusSquared) continue;

                if (!GenSight.LineOfSight(caster.Position, cell, map, true)) continue;

                found.Add(bullet);
            }
        }

        // ----------------------------------------------------------------- validity

        /// <summary>
        /// Checked every frame the editor draws. A session whose carrier has died, been moved
        /// off the map, or whose map is no longer the one on screen has nothing left to act on
        /// and is dropped rather than left holding the clock.
        /// </summary>
        public bool StillValid()
        {
            if (closed) return false;
            if (Caster == null || Caster.Dead || !Caster.Spawned) return false;
            if (Caster.Map != Map) return false;
            if (Find.CurrentMap != Map) return false;
            return true;
        }

        public void DropInvalidMembers()
        {
            for (int i = 0; i < groups.Length; i++) groups[i].DropInvalid(Map);
            for (int i = captured.Count - 1; i >= 0; i--)
            {
                if (!captured[i].StillValid(Map)) captured.RemoveAt(i);
            }
        }

        // ----------------------------------------------------------------- cost

        /// <summary>Groups that would actually alter a round. Empty groups are never charged for.</summary>
        public int ChangedGroupCount()
        {
            int count = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                if (!groups[i].Empty && groups[i].WouldChange(Map)) count++;
            }
            return count;
        }

        public float AddedStrain()
        {
            return VectorEditDefaults.StrainCostFor(ChangedGroupCount());
        }

        public bool HasChanges()
        {
            return ChangedGroupCount() > 0;
        }

        // ----------------------------------------------------------------- commit

        /// <summary>
        /// Writes every group's setting to its rounds at once, pays for it, and hands the clock
        /// back.
        ///
        /// Revalidation happens before anything is written. A session that has lost its carrier
        /// commits nothing and spends nothing rather than editing the half of the field that is
        /// still there - a partial manipulation is not something the player asked for and not
        /// something they could see coming.
        /// </summary>
        public void Apply()
        {
            if (closed) return;

            if (!StillValid())
            {
                Messages.Message("AG_VectorEditLost".Translate(), MessageTypeDefOf.RejectInput, false);
                Close(previousSpeed);
                return;
            }

            DropInvalidMembers();

            int changedGroups = ChangedGroupCount();
            if (changedGroups == 0)
            {
                Close(previousSpeed);
                return;
            }

            int edited = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                VectorEditGroup group = groups[i];
                if (group.Empty || !group.WouldChange(Map)) continue;

                for (int m = 0; m < group.Members.Count; m++)
                {
                    CapturedProjectile member = group.Members[m];
                    if (!member.StillValid(Map)) continue;

                    member.Commit(group.Rotation, group.Force, Caster, Map);
                    edited++;
                }
            }

            VectorStrain.Add(Caster, VectorEditDefaults.StrainCostFor(changedGroups));

            if (ability != null) ability.StartCooldown(VectorEditDefaults.CooldownTicks);

            Messages.Message("AG_VectorEditApplied".Translate(Caster.LabelShort, edited),
                Caster, MessageTypeDefOf.NeutralEvent, false);

            // Resuming from an activation that began paused would otherwise leave the player
            // staring at a still frame with nothing to press.
            Close(previousSpeed == TimeSpeed.Paused ? TimeSpeed.Normal : previousSpeed);
        }

        /// <summary>Throws the draft away and puts the clock back exactly as it was found.</summary>
        public void Cancel()
        {
            if (closed) return;
            Close(previousSpeed);
        }

        private void Close(TimeSpeed speed)
        {
            closed = true;
            if (Current == this) Current = null;

            Window_VectorEdit closing = window;
            window = null;
            if (closing != null) Find.WindowStack.TryRemove(closing, false);

            if (Find.TickManager != null) Find.TickManager.CurTimeSpeed = speed;
        }

        /// <summary>Dropped without touching the clock: the game it belonged to is gone.</summary>
        public static void Abandon()
        {
            VectorEditSession session = Current;
            Current = null;
            if (session == null) return;

            session.closed = true;
            if (session.window != null)
            {
                Window_VectorEdit closing = session.window;
                session.window = null;
                if (Find.WindowStack != null) Find.WindowStack.TryRemove(closing, false);
            }
        }

        // ----------------------------------------------------------------- group editing

        public void SelectGroup(int index)
        {
            selected = index >= 0 && index < groups.Length ? index : -1;
        }

        public void DeleteSelected()
        {
            VectorEditGroup group = SelectedGroup;
            if (group == null) return;
            group.Clear();
        }

        private int FirstFreeGroup()
        {
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i].Empty) return i;
            }
            return -1;
        }

        // ----------------------------------------------------------------- map input

        /// <summary>
        /// Every map-side event while the editor is open. Called from the Selector prefix, which
        /// is the point in the frame after the panel has taken what belongs to it and before
        /// ordinary selection would start, so a drag over the field draws a group rather than
        /// selecting four colonists and ordering them into the line of fire.
        /// </summary>
        public void HandleMapInput()
        {
            Event current = Event.current;

            if (KeyBindingDefOf.Cancel.KeyDownEvent)
            {
                Cancel();
                current.Use();
                return;
            }

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Delete)
            {
                DeleteSelected();
                current.Use();
                return;
            }

            Vector2 mouse = UI.MousePositionOnUIInverted;

            // A turn or a box already in progress is finished wherever the mouse ends up,
            // including over the panel. Dropping it there instead would leave the session
            // holding a drag nobody can see the end of.
            if (rotating)
            {
                ContinueRotate(current);
                return;
            }

            if (dragging)
            {
                ContinueDrag(current, mouse);
                return;
            }

            // Nothing in progress, so anything under the panel belongs to the panel.
            if (Find.WindowStack.GetWindowAt(mouse) != null) return;

            if (current.type == EventType.MouseDown && current.button == 0)
            {
                if (TryGrabHandle(mouse))
                {
                    rotating = true;
                    rotatingGroup = selected;
                }
                else
                {
                    dragging = true;
                    dragStart = mouse;
                    dragCurrent = mouse;
                    dragMode = current.alt ? DragMode.Remove
                        : current.shift ? DragMode.Add
                        : DragMode.Create;
                }
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 1)
            {
                SelectGroup(-1);
                current.Use();
            }
        }

        private void ContinueRotate(Event current)
        {
            if (current.rawType == EventType.MouseUp && current.button == 0)
            {
                rotating = false;
                rotatingGroup = -1;
                current.Use();
                return;
            }

            UpdateRotation();

            // Only a drag is consumed. Using a layout or repaint event here would take it away
            // from everything drawn after this point in the frame.
            if (current.type == EventType.MouseDrag) current.Use();
        }

        private void ContinueDrag(Event current, Vector2 mouse)
        {
            if (current.type == EventType.MouseDown && current.button == 1)
            {
                dragging = false;
                current.Use();
                return;
            }

            dragCurrent = mouse;

            if (current.rawType == EventType.MouseUp && current.button == 0)
            {
                dragging = false;
                FinishDrag();
                current.Use();
            }
            else if (current.type == EventType.MouseDrag)
            {
                current.Use();
            }
        }

        private bool TryGrabHandle(Vector2 mouse)
        {
            VectorEditGroup group = SelectedGroup;
            if (group == null || group.Empty) return false;

            Vector2 handle = UI.MapToUIPosition(HandlePosition(group));
            return (handle - mouse).sqrMagnitude
                   <= VectorEditDefaults.RotationHandleGrabPixels * VectorEditDefaults.RotationHandleGrabPixels;
        }

        /// <summary>
        /// Where the handle sits: out from the group's centre along the direction "straight up"
        /// has been turned to. Dragging it is the same statement as the slider, read off the map.
        /// </summary>
        public Vector3 HandlePosition(VectorEditGroup group)
        {
            Vector3 spoke = Quaternion.AngleAxis(group.Rotation, Vector3.up) * Vector3.forward;
            return group.Centre() + spoke * VectorEditDefaults.RotationHandleCells;
        }

        private void UpdateRotation()
        {
            if (rotatingGroup < 0) return;

            VectorEditGroup group = groups[rotatingGroup];
            if (group.Empty) return;

            // MouseMapPosition, not UIToMapPosition(mouse): the mouse vector everything else
            // here works in is the inverted one that MapToUIPosition returns, and feeding that
            // to UIToMapPosition would mirror the handle about the middle of the screen.
            Vector3 offset = UI.MouseMapPosition() - group.Centre();
            offset.y = 0f;
            if (offset.sqrMagnitude < 0.01f) return;

            group.Rotation = Mathf.Repeat(offset.AngleFlat() + 180f, 360f) - 180f;
        }

        private void FinishDrag()
        {
            Rect box = BoxFrom(dragStart, dragCurrent);
            bool isClick = box.width < VectorEditDefaults.DragThresholdPixels
                           && box.height < VectorEditDefaults.DragThresholdPixels;

            List<CapturedProjectile> hit = isClick ? ClickedRounds(dragCurrent) : RoundsInBox(box);

            if (hit.Count == 0)
            {
                if (isClick && dragMode == DragMode.Create) SelectGroup(-1);
                return;
            }

            // A click on something already in a group means "work on that group", never
            // "start a new one". Reaching for a group by pointing at it is the fastest way
            // to reach it, and re-grouping a round means releasing it first.
            if (isClick && dragMode == DragMode.Create && hit[0].Group >= 0)
            {
                SelectGroup(hit[0].Group);
                return;
            }

            switch (dragMode)
            {
                case DragMode.Remove:
                    RemoveFromSelected(hit);
                    break;
                case DragMode.Add:
                    AddToSelected(hit);
                    break;
                default:
                    CreateGroup(hit);
                    break;
            }
        }

        private void CreateGroup(List<CapturedProjectile> hit)
        {
            int index = FirstFreeGroup();
            if (index < 0)
            {
                Messages.Message("AG_VectorEditGroupsFull".Translate(VectorEditDefaults.MaxGroups),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            VectorEditGroup group = groups[index];
            bool any = false;
            for (int i = 0; i < hit.Count; i++)
            {
                if (hit[i].Group >= 0) continue;
                group.Add(hit[i]);
                any = true;
            }

            if (any) SelectGroup(index);
        }

        private void AddToSelected(List<CapturedProjectile> hit)
        {
            VectorEditGroup group = SelectedGroup;
            if (group == null)
            {
                CreateGroup(hit);
                return;
            }

            for (int i = 0; i < hit.Count; i++)
            {
                if (hit[i].Group >= 0) continue;
                group.Add(hit[i]);
            }
        }

        private void RemoveFromSelected(List<CapturedProjectile> hit)
        {
            VectorEditGroup group = SelectedGroup;
            if (group == null) return;

            for (int i = 0; i < hit.Count; i++)
            {
                if (hit[i].Group == group.Index) group.Remove(hit[i]);
            }
        }

        /// <summary>Nearest round to a bare click, if the click was close enough to mean one.</summary>
        private List<CapturedProjectile> ClickedRounds(Vector2 mouse)
        {
            List<CapturedProjectile> hit = new List<CapturedProjectile>();

            CapturedProjectile best = null;
            float bestDistance = VectorEditDefaults.RotationHandleGrabPixels
                                 * VectorEditDefaults.RotationHandleGrabPixels;

            for (int i = 0; i < captured.Count; i++)
            {
                Vector2 at = UI.MapToUIPosition(captured[i].Position);
                float distance = (at - mouse).sqrMagnitude;
                if (distance > bestDistance) continue;
                bestDistance = distance;
                best = captured[i];
            }

            if (best != null) hit.Add(best);
            return hit;
        }

        private List<CapturedProjectile> RoundsInBox(Rect box)
        {
            List<CapturedProjectile> hit = new List<CapturedProjectile>();
            for (int i = 0; i < captured.Count; i++)
            {
                if (box.Contains(UI.MapToUIPosition(captured[i].Position))) hit.Add(captured[i]);
            }
            return hit;
        }

        public static Rect BoxFrom(Vector2 a, Vector2 b)
        {
            return new Rect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        public bool Dragging => dragging;
        public Rect DragBox => BoxFrom(dragStart, dragCurrent);
    }
}
