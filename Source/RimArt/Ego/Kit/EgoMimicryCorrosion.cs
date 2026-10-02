using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// Mimicry's corroded attack (docs/ego-weapons.md, Weapon 3). Corroded, the wielder hunts: it walks to the nearest living
    /// pawn it can reach, of any faction and downed ones included (the shared walk, <see cref="WalksToNearest"/>), and at
    /// every firing (corrodedInterval, 1.5 s) it swings at that pawn if it is in reach, or lunges up to lungeCells (2) to a
    /// free cell next to it first (<see cref="EgoMimicry.Landing"/>). Further than that the firing passes and the walk goes
    /// on. Overclock stands still and swings at the nearest standing hostile in reach, overclockCount (5) times, one every
    /// overclockInterval (1 s); it never lunges. Both are the ordinary swing with Core's hit and dodge rolls, never the grown
    /// one, and never roll Corrosion.
    ///
    /// The arm: <see cref="Begin"/> sets it to armStart (1), each damaging hit dealt adds a stage and each damaging hit taken
    /// removes one (<see cref="EgoMimicry"/>), and <see cref="End"/> takes it away at once. The look is drawn by
    /// <see cref="GameComponent_EgoMimicry"/>, so the arm recedes after the state; <see cref="EgoCorrosionAction.DrawCorroded"/>
    /// stays empty.
    /// </summary>
    public class EgoMimicryCorrosion : EgoCorrosionAction
    {
        public override bool WalksToNearest => true;

        /// <summary>Corroded: a pawn the wielder can walk to. Overclock: a hostile in melee reach, not round a closed corner.</summary>
        public override bool CanTarget(Pawn wielder, CompEgoWeapon weapon, Pawn target, bool hostilesOnly) =>
            hostilesOnly ? EgoMimicry.Reaches(wielder, target) : wielder.CanReach(target, PathEndMode.Touch, Danger.Deadly);

        public override bool Busy(Pawn wielder, CompEgoWeapon weapon) => GameComponent_EgoMimicry.Instance?.Busy(wielder) == true;

        public override void Begin(Pawn wielder, CompEgoWeapon weapon, bool overclock)
        {
            if (weapon is CompEgoMimicry sword) sword.stage = sword.Props.armStart;
        }

        public override void End(Pawn wielder, CompEgoWeapon weapon, bool overclock)
        {
            if (weapon is CompEgoMimicry sword) sword.stage = 0;
            GameComponent_EgoMimicry.Instance?.EndSpecial(wielder);
        }

        public override void Fire(Pawn wielder, CompEgoWeapon weapon, Pawn target, bool hostilesOnly)
        {
            GameComponent_EgoMimicry game = GameComponent_EgoMimicry.Instance;
            if (!(weapon is CompEgoMimicry sword) || game == null || target == null || !wielder.Spawned) return;
            EgoMimicrySource source = hostilesOnly ? EgoMimicrySource.Overclock : EgoMimicrySource.Corroded;
            IntVec3 lungeTo = IntVec3.Invalid;
            if (!EgoMimicry.Reaches(wielder, target))
            {
                if (hostilesOnly || !EgoMimicry.Landing(wielder, target, sword.Props.lungeCells, out lungeTo)) return;
            }
            game.BeginSwing(wielder, sword, target, source, grown: false, surprise: false, lungeTo);
            // The walk stops for the lunge and the swing: while the swing is busy the job giver gives the hold.
            if (!hostilesOnly && wielder.CurJobDef == EgoDefOf.AG_EgoCorrodedWalk) wielder.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }
    }
}
