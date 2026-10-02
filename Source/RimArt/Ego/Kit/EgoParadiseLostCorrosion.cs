using Verse;

namespace RimArt
{
    /// <summary>
    /// Paradise Lost's corroded attack, WhiteNight's ring (docs/ego-weapons.md, Weapon 4): no target; each firing strikes
    /// every pawn of any faction within the ring, through walls (<see cref="EgoParadiseLost.Ring"/>). The wielder holds its
    /// cell. Corroded, each ring is ringGrowth cells wider than the one before: 6, 9, 12, 15 at the placeholders. Overclock
    /// strikes only standing hostiles and its rings stay at ringRadius, so the growth is the cost of losing control.
    ///
    /// The look (star, wings, halo) is drawn by <see cref="GameComponent_EgoParadiseLost"/> from <see cref="Begin"/> to a
    /// second after <see cref="End"/>, so the wings fold after the state is over; <see cref="EgoCorrosionAction.DrawCorroded"/>
    /// stays empty.
    /// </summary>
    public class EgoParadiseLostCorrosion : EgoCorrosionAction
    {
        public override bool TakesTarget => false;

        public override void Begin(Pawn wielder, CompEgoWeapon weapon, bool overclock)
        {
            if (!overclock && weapon is CompEgoParadiseLost staff) staff.rings = 0;
            if (wielder.Spawned) GameComponent_EgoParadiseLost.Instance?.BeginLook(wielder, overclock);
        }

        public override void End(Pawn wielder, CompEgoWeapon weapon, bool overclock)
        {
            if (!overclock && weapon is CompEgoParadiseLost staff) staff.rings = 0;
            GameComponent_EgoParadiseLost.Instance?.EndLook(wielder);
        }

        public override void Fire(Pawn wielder, CompEgoWeapon weapon, Pawn target, bool hostilesOnly)
        {
            if (!(weapon is CompEgoParadiseLost staff) || !wielder.Spawned) return;
            float radius = hostilesOnly ? staff.Props.ringRadius : staff.NextRingRadius;
            if (!hostilesOnly) staff.rings++;
            EgoParadiseLost.Ring(wielder, staff, radius, hostilesOnly);
        }
    }
}
