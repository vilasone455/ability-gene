using Verse;

namespace RimArt
{
    /// <summary>
    /// What one E.G.O. weapon does when its Abnormality fires it: one firing of the corroded attack, and the look drawn
    /// over the wielder while corroded or overclocking (docs/ego-weapons.md, "What each weapon supplies"). The weapon
    /// names its subclass in <see cref="CompProperties_EgoWeapon.actionClass"/>; one instance serves every copy of the
    /// weapon, so per-pawn state belongs on the weapon's comp.
    /// </summary>
    public abstract class EgoCorrosionAction
    {
        /// <summary>
        /// False for an action that is an area round the wielder (Solemn Lament's coffin, Paradise Lost's ring): it fires
        /// with no target, and the nearest-pawn rule does not apply.
        /// </summary>
        public virtual bool TakesTarget => true;

        /// <summary>
        /// True when the corroded wielder walks to the nearest living pawn of any faction, downed ones included, and stays
        /// next to it (<see cref="JobDriver_EgoCorrodedWalk"/>): Solemn Lament's coffin, Mimicry. False holds the cell.
        /// Overclock never walks.
        /// </summary>
        public virtual bool WalksToNearest => false;

        /// <summary>
        /// Whether <paramref name="target"/> can take a firing: the nearest-pawn rule, the corroded walk and Overclock's button
        /// and job all skip a pawn that fails it, so they agree on whom the action reaches (Mimicry: a pawn it can walk to
        /// while corroded, a hostile in melee reach in Overclock). Every pawn by default.
        /// </summary>
        public virtual bool CanTarget(Pawn wielder, CompEgoWeapon weapon, Pawn target, bool hostilesOnly) => true;

        /// <summary>
        /// True while the last firing is still going (Mimicry's lunge and swing): the corroded wielder stands, and a new
        /// firing waits. Every other action's firing is over on its own tick.
        /// </summary>
        public virtual bool Busy(Pawn wielder, CompEgoWeapon weapon) => false;

        /// <summary>The corrosion (<paramref name="overclock"/> false) or an Overclock starts: before its first firing.</summary>
        public virtual void Begin(Pawn wielder, CompEgoWeapon weapon, bool overclock)
        {
        }

        /// <summary>The corrosion or the Overclock is over, however it ended (time, downed, the weapon dropped, cancelled).</summary>
        public virtual void End(Pawn wielder, CompEgoWeapon weapon, bool overclock)
        {
        }

        /// <summary>
        /// One firing. <paramref name="target"/> is the nearest living pawn of any faction while corroded, the nearest
        /// hostile within overclockRange while overclocking, and null when <see cref="TakesTarget"/> is false. With
        /// <paramref name="hostilesOnly"/> (Overclock) an area action must skip every pawn not hostile to the wielder.
        /// </summary>
        public abstract void Fire(Pawn wielder, CompEgoWeapon weapon, Pawn target, bool hostilesOnly);

        /// <summary>
        /// Draws the corroded look over the wielder, every frame from <see cref="MapComponent_EgoCorrosion"/>.
        /// <paramref name="seconds"/> counts from the start of the corrosion or the Overclock.
        /// </summary>
        public virtual void DrawCorroded(Pawn wielder, CompEgoWeapon weapon, float seconds, bool overclock)
        {
        }
    }
}
