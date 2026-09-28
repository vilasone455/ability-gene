using RimWorld;
using UnityEngine;
using Verse;
using T = RimArt.ThunderGodJumpTiming;

namespace RimArt
{
    public class CompProperties_ThunderGodJump : CompProperties_AbilityEffect
    {
        /// <summary>Share of Minato's melee damage in the cut he lands with behind a pawn.</summary>
        public float damageFactor = 1.5f;

        public CompProperties_ThunderGodJump()
        {
            compClass = typeof(CompAbilityEffect_ThunderGodJump);
        }
    }

    /// <summary>
    /// Flying Thunder God. The target is one of Minato's marks (<see cref="ThunderGodMarks"/>): a pawn holding his
    /// kunai or seal, a sealed kunai on the ground, or a cell with either. No line of sight; the range is the
    /// verb's. The jump itself is <see cref="ThunderGodJumpCast"/>.
    /// </summary>
    public class CompAbilityEffect_ThunderGodJump : CompAbilityEffect_Minato
    {
        public new CompProperties_ThunderGodJump Props => (CompProperties_ThunderGodJump)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            string problem = ThunderGodMarks.Problem(parent.pawn, target, out _, out _);
            if (problem == null) return base.Valid(target, throwMessages);
            Reject(problem, parent.pawn, throwMessages);
            return false;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (!ThunderGodMarks.TryResolve(target, parent.pawn.Map, out Pawn pawn, out KunaiItem item)) return;
            MinatoCasts.For<ThunderGodJumpCast>(parent, target)?.Launch(pawn, item, Find.TickManager.TicksGame);
        }
    }

    /// <summary>
    /// One jump (the sketch kunai-flying-thunder-god.js). No warmup: the ability fires on the click and the clock
    /// starts at the sketch's cast time. The seal lights and the floor script is written; Minato narrows into a
    /// sliver and is gone; he arrives 0.21 s after the click and widens back.
    ///   Kunai in a pawn (or the seal): he lands in the free cell most behind it, seen from where he stood, and
    ///   0.1 s later cuts once with his melee attack at x1.5 damage, a sure hit. The kunai stays in.
    ///   Kunai on the ground: he lands on it and it goes back into his belt (<see cref="ThunderGodMarks.TakeKunai"/>).
    /// The landing is decided again on arrival, so a pawn that moved is still landed behind.
    /// </summary>
    public sealed class ThunderGodJumpCast : MinatoCast
    {
        public Pawn mark;
        public KunaiItem kunai;
        public IntVec3 from, landing;
        /// <summary>Where the mark was, for the picture after the kunai is picked up or the pawn is gone.</summary>
        public Vector2 markAt;
        public bool inEnemy, arrived, struck, aborted;

        public override AbilityDef Def => MinatoDefOf.AG_ThunderGodJump;
        protected override float Lead => T.Lead;
        protected override float FireAt => T.CastAt;

        private float DamageFactor => MinatoKit.Props<CompProperties_ThunderGodJump>(Def)?.damageFactor ?? 1.5f;

        public void Launch(Pawn pawn, KunaiItem item, int now)
        {
            MarkFired(now);
            mark = pawn;
            kunai = item;
            inEnemy = pawn != null;
            from = caster.Position;
            landing = inEnemy ? ThunderGodMarks.Behind(caster, from, pawn) : ThunderGodMarks.OnKunai(caster, item);
            markAt = inEnemy ? MinatoKit.Ground(pawn.DrawPos) : MinatoKit.Flat(item.Position);
        }

        public override bool Holds(int now) => Fired && !aborted && !struck && now < TickAt(T.HitAt) + 1;

        public override Rot4 Facing(int now)
        {
            Vector2 look = markAt - MinatoKit.Ground(caster.Position);
            return look.sqrMagnitude < 1e-4f ? Rot4.Invalid : Rot4.FromAngleFlat(90f - ThunderGodTiming.Degrees(look));
        }

        public override bool Tick(int now)
        {
            if (!Fired) return now - startTick < 600;
            if (!arrived && now >= TickAt(T.ArriveAt)) Arrive();
            if (!struck && now >= TickAt(T.HitAt)) Strike();
            return Seconds(now) < T.Duration;
        }

        private void Arrive()
        {
            arrived = true;
            if (aborted || !CasterFit) return;
            if (inEnemy)
            {
                if (mark == null || !mark.Spawned || mark.Map != home) return;
                markAt = MinatoKit.Ground(mark.DrawPos);
                IntVec3 now = ThunderGodMarks.Behind(caster, from, mark);
                if (now.IsValid) landing = now;
            }
            else
            {
                if (kunai == null || !kunai.Spawned || kunai.Map != home) return;
                IntVec3 now = ThunderGodMarks.OnKunai(caster, kunai);
                if (now.IsValid) landing = now;
            }
            if (!landing.IsValid || !ThunderGodMarks.Free(caster, landing, home)) return;
            Teleport(landing);
            if (!inEnemy) ThunderGodMarks.TakeKunai(caster, kunai);
        }

        /// <summary>The cut, if he did land behind a pawn that is still next to him.</summary>
        private void Strike()
        {
            struck = true;
            if (!inEnemy || aborted || !CasterFit || mark == null || !mark.Spawned || mark.Dead) return;
            if (caster.Position != landing || !caster.Position.AdjacentTo8WayOrInside(mark.Position)) return;
            // An ally the kunai was stuck in is landed behind, not cut.
            if (!mark.HostileTo(caster)) return;
            ThunderGodStrike.Hit(caster, mark, DamageFactor);
            Find.CameraDriver.shaker.DoShake(T.Shake);
        }

        public override void JobEnded(int now)
        {
            aborted = true;
        }

        public override void Pose(float s)
        {
            if (!Fired || caster == null || !caster.Spawned || caster.Map != home) return;
            // Narrowing into the sliver where he stood, then widening out of one where he landed.
            float thin = s < T.ArriveAt ? VfxMath.Smooth((s - T.GoAt) / T.Squeeze) : 1f - VfxMath.Smooth((s - T.ArriveAt) / T.Squeeze);
            if (!aborted) MinatoLooks.Thin(caster, thin);
        }

        public override void Draw(float s)
        {
            if (!Fired || home == null || !landing.IsValid) return;
            Vector2 at = markAt;
            if (inEnemy && mark != null && mark.Spawned && mark.Map == home) at = MinatoKit.Ground(mark.DrawPos);
            ThunderGodJumpGraphics.Draw(MinatoKit.Ground(from), at, MinatoKit.Ground(landing), inEnemy, s, home);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref mark, "mark");
            Scribe_References.Look(ref kunai, "kunai");
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref landing, "landing");
            Scribe_Values.Look(ref markAt, "markAt");
            Scribe_Values.Look(ref inEnemy, "inEnemy");
            Scribe_Values.Look(ref arrived, "arrived");
            Scribe_Values.Look(ref struck, "struck");
            Scribe_Values.Look(ref aborted, "aborted");
        }
    }
}
