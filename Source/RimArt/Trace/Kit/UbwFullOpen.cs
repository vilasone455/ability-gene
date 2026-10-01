using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using static RimArt.UbwCommands;
using static RimArt.VfxMath;
using T = RimArt.UbwCommandTiming;

namespace RimArt
{
    /// <summary>One sword of a Full Open: pulled out of its hole, hovering, fired, then stuck in the ground past the target.</summary>
    public sealed class UbwVolleySword : IExposable
    {
        public int seed;
        /// <summary>When it left the ground, when it was fired (int.MinValue while it hovers), when it reaches the target, when it stands in the field again.</summary>
        public int pulled, launch = int.MinValue, arrive, stand;
        /// <summary>When it began to drop back into its hole (int.MinValue unless the charge was cancelled).</summary>
        public int dropped = int.MinValue;
        public Pawn target;
        public bool struck;
        /// <summary>Cells from the world's middle corner: the target when the sword was fired or dropped, where it hits, where it sticks, and the way it leans.</summary>
        public Vector2 foe, hit, land, toward;

        public void ExposeData()
        {
            Scribe_Values.Look(ref seed, "seed");
            Scribe_Values.Look(ref pulled, "pulled");
            Scribe_Values.Look(ref launch, "launch", int.MinValue);
            Scribe_Values.Look(ref arrive, "arrive");
            Scribe_Values.Look(ref stand, "stand");
            Scribe_Values.Look(ref dropped, "dropped", int.MinValue);
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref struck, "struck");
            Scribe_Values.Look(ref foe, "foe");
            Scribe_Values.Look(ref hit, "hit");
            Scribe_Values.Look(ref land, "land");
            Scribe_Values.Look(ref toward, "toward");
        }
    }

    /// <summary>
    /// Full Open (the caster's command, docs/unlimited-blade-works.md). Picking a pawn in the world starts the charge at once:
    /// the caster stands still (<see cref="JobDriver_UbwFullOpen"/>) and every fullOpenSwordSeconds one sword pulls out of
    /// its hole, the nearest to the target first, and hovers 1 cell up aimed at it, up to fullOpenSwords. Each costs
    /// swordCostSeconds of the world as it leaves the ground. Release fires them all within fullOpenVolleySeconds at
    /// fullOpenSpeed: each hits the target as its own weapon (<see cref="UbwSwordHit"/>) and sticks in the ground 1 to 2
    /// cells past it, leaning back in, where it stands as a sword of the field again. Cancel, a move order, the caster
    /// going down, the target dying or leaving the world, or the world closing drops the hovering swords back into their
    /// own holes; the time they cost stays spent. No cooldown: a new charge can start while a volley is still flying.
    /// </summary>
    public sealed class UbwFullOpen : IExposable
    {
        public Pawn target;
        public int startTick = int.MinValue;
        public List<UbwVolleySword> hovering = new List<UbwVolleySword>();
        public List<UbwVolleySword> flying = new List<UbwVolleySword>();
        public List<UbwVolleySword> dropping = new List<UbwVolleySword>();

        public bool Charging => target != null;

        /// <summary>Starts the charge on <paramref name="foe"/>: the caster is held still from now on.</summary>
        public void Begin(UbwCast cast, Pawn foe, int now)
        {
            if (Charging || !cast.CommandsOpen || !cast.InWorld(foe) || foe == cast.caster) return;
            target = foe;
            startTick = now;
            Pawn caster = cast.caster;
            caster.jobs.StartJob(JobMaker.MakeJob(UbwDefOf.AG_UbwFullOpen, foe), JobCondition.InterruptForced);
        }

        /// <summary>Why the charge stops by itself now, or null.</summary>
        private string Broken(UbwCast cast, int now)
        {
            if (!cast.CommandsOpen) return "the world is closing";
            if (!cast.InWorld(target)) return "the target is gone";
            if (now > startTick && cast.caster.CurJobDef != UbwDefOf.AG_UbwFullOpen) return "the caster stopped";
            return null;
        }

        /// <summary>The Release button: every hovering sword is fired, one after another within fullOpenVolleySeconds.</summary>
        public void Release(UbwCast cast, int now)
        {
            if (!Charging) return;
            UbwRules rules = UbwRules.Of;
            int n = hovering.Count;
            MapComponent_UnlimitedBladeWorks inside = cast.Inside;
            Vector2 foe = inside != null && cast.InWorld(target) ? inside.Local(target.DrawPos) : Vector2.zero;
            // A shuffled order, fixed by the swords' seeds as the sketch's is.
            hovering.Sort((a, b) => Rand(a.seed * 31).CompareTo(Rand(b.seed * 31)));
            for (int k = 0; k < n; k++)
            {
                UbwVolleySword v = hovering[k];
                v.target = target;
                v.foe = foe;
                v.launch = now + (n > 1 ? Ticks(rules.fullOpenVolleySeconds * k / (n - 1)) : 0);
                flying.Add(v);
            }
            hovering.Clear();
            End(cast);
        }

        /// <summary>The Cancel button and every other end of a charge: the hovering swords drop back into their holes.</summary>
        public void Cancel(UbwCast cast, int now)
        {
            if (!Charging) return;
            MapComponent_UnlimitedBladeWorks inside = cast.Inside;
            Vector2 foe = inside != null && target.Spawned && target.Map == cast.world ? inside.Local(target.DrawPos) : Vector2.zero;
            foreach (UbwVolleySword v in hovering)
            {
                v.dropped = now;
                v.foe = foe;
                dropping.Add(v);
            }
            hovering.Clear();
            End(cast);
        }

        private void End(UbwCast cast)
        {
            target = null;
            startTick = int.MinValue;
            Pawn caster = cast.caster;
            if (caster != null && caster.Spawned && caster.CurJobDef == UbwDefOf.AG_UbwFullOpen) caster.jobs.EndCurrentJob(JobCondition.Succeeded);
        }

        public void Tick(UbwCast cast, int now)
        {
            MapComponent_UnlimitedBladeWorks inside = cast.Inside;
            if (inside == null) return;
            UbwFieldState field = inside.Field;
            UbwRules rules = UbwRules.Of;

            if (Charging)
            {
                if (Broken(cast, now) != null) Cancel(cast, now);
                else if (hovering.Count < rules.fullOpenSwords && now >= startTick + (hovering.Count + 1) * Mathf.Max(1, Ticks(rules.fullOpenSwordSeconds)))
                {
                    UbwSword sw = field.Nearest(inside.Local(target.DrawPos));
                    if (sw != null)
                    {
                        field.Take(sw);
                        cast.Spend(1);
                        hovering.Add(new UbwVolleySword { seed = sw.Seed, pulled = now });
                    }
                }
            }

            for (int i = flying.Count - 1; i >= 0; i--)
            {
                UbwVolleySword v = flying[i];
                UbwSword sw = field.BySeed(v.seed);
                if (sw == null)
                {
                    flying.RemoveAt(i);
                    continue;
                }
                if (now >= v.launch && v.arrive == 0) Fire(cast, inside, v, sw, now);
                if (v.arrive != 0 && now >= v.arrive && !v.struck)
                {
                    v.struck = true;
                    if (cast.InWorld(v.target)) UbwSwordHit.Strike(cast.caster, v.target, UbwSwordHit.WeaponOf(sw.W), -v.toward);
                }
                if (v.arrive != 0 && now >= v.stand)
                {
                    float dir = Mathf.Atan2(v.toward.y, v.toward.x) * Mathf.Rad2Deg;
                    field.Land(v.land.x, v.land.y, (float)T.StickLean, dir, sw.W, sw.Size);
                    flying.RemoveAt(i);
                }
            }

            for (int i = dropping.Count - 1; i >= 0; i--)
                if (now >= dropping[i].dropped + Ticks(T.DropTime))
                {
                    field.PutBack(field.BySeed(dropping[i].seed));
                    dropping.RemoveAt(i);
                }
        }

        /// <summary>
        /// The sword leaves its hover for the target where it is now: its flight time from 1 cell up over its hole, the
        /// point it hits, and the cell 1 to 2 cells past the target (scattered sideways, kept on the map) where it sticks.
        /// </summary>
        private void Fire(UbwCast cast, MapComponent_UnlimitedBladeWorks inside, UbwVolleySword v, UbwSword sw, int now)
        {
            if (cast.InWorld(v.target)) v.foe = inside.Local(v.target.DrawPos);
            var from = new Vector2((float)sw.X, (float)sw.Z);
            Vector2 dir = v.foe - from;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.up;
            dir.Normalize();
            v.hit = v.foe - dir * 0.15f;
            float rise = (float)(T.Hover - T.HitHeight), flat = (v.hit - from).magnitude, d = Mathf.Sqrt(flat * flat + rise * rise);
            v.arrive = now + Mathf.Max(1, Ticks(T.VolleyLift + d / UbwRules.Of.fullOpenSpeed));
            v.stand = v.arrive + Ticks(T.StandAfter);
            float past = (float)(T.LandPastMin + (T.LandPastMax - T.LandPastMin) * Rand(v.seed * 29)), side = (float)((Rand(v.seed * 23) - 0.5) * T.LandSide);
            v.land = inside.ClampInside(v.foe + dir * past + new Vector2(-dir.y, dir.x) * side);
            v.toward = -dir;
        }

        /// <summary>
        /// The ring under the target while charging, the hovering swords, the volley in flight and stuck, the swords dropping
        /// back. The scratch builders are keyed by the sword's place in its list this frame, not by its seed: UbwGraphics keeps
        /// a builder and its mesh for every key it has ever seen.
        /// </summary>
        internal void Draw(UbwCast cast, MapComponent_UnlimitedBladeWorks inside, in UbwCommandLook k)
        {
            UbwFieldState field = inside.Field;
            UbwXZ foe = default;
            int slot = 0;
            if (Charging && cast.InWorld(target))
            {
                foe = XZ(inside.Local(target.DrawPos));
                float s = UbwClock.Since(startTick);
                UbwCommandGraphics.TargetRing(k, foe, 0.42 + 0.04 * System.Math.Sin(s * 9f), 0.55f * Smooth(s / 0.3f));
            }
            foreach (UbwVolleySword v in hovering)
            {
                UbwSword sw = field.BySeed(v.seed);
                if (sw != null) UbwCommandGraphics.Gathering(k, "ubw hover " + slot++, sw, UbwClock.Since(v.pulled), foe);
            }
            foreach (UbwVolleySword v in flying)
            {
                UbwSword sw = field.BySeed(v.seed);
                if (sw == null) continue;
                string key = "ubw volley " + slot++;
                double s = UbwClock.Since(v.pulled);
                if (v.arrive == 0)
                {
                    // Released, waiting for its turn in the volley.
                    UbwCommandGraphics.Gathering(k, key, sw, s, XZ(v.foe));
                    continue;
                }
                double launch = (v.launch - v.pulled) / 60.0;
                UbwPose hover = T.Gathered(sw, launch, XZ(v.foe), out _, out _);
                var shot = new UbwSwordShot
                {
                    Launch = launch, Lift = T.VolleyLift, Fly = System.Math.Max(0.02, (v.arrive - v.launch) / 60.0 - T.VolleyLift),
                    Start = hover.Tip, Hit = new UbwV3(v.hit.x, T.HitHeight, v.hit.y), Dir = XZ(-v.toward),
                    Land = new UbwXZ(v.land.x, v.land.y + field.LiftAt(v.land.x, v.land.y)), Toward = XZ(v.toward), Lean = T.StickLean, Buried = T.StickBuried,
                };
                UbwCommandGraphics.Shot(k, key, sw, hover, shot, s, 0, false);
                UbwCommandGraphics.BreakOut(k, sw.Cut, s, sw.Seed + 3);
            }
            foreach (UbwVolleySword v in dropping)
            {
                UbwSword sw = field.BySeed(v.seed);
                if (sw == null) continue;
                UbwPose from = T.Gathered(sw, (v.dropped - v.pulled) / 60.0, XZ(v.foe), out _, out _);
                UbwCommandGraphics.DropBack(k, "ubw drop " + slot++, sw, from, System.Math.Min(1.0, UbwClock.Since(v.dropped) / T.DropTime));
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref startTick, "startTick", int.MinValue);
            Scribe_Collections.Look(ref hovering, "hovering", LookMode.Deep);
            Scribe_Collections.Look(ref flying, "flying", LookMode.Deep);
            Scribe_Collections.Look(ref dropping, "dropping", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (hovering == null) hovering = new List<UbwVolleySword>();
                if (flying == null) flying = new List<UbwVolleySword>();
                if (dropping == null) dropping = new List<UbwVolleySword>();
            }
        }
    }

    /// <summary>
    /// Full Open's charge: the caster stands still facing the target while the swords rise. A move order, a mental break
    /// or going down ends the job, and <see cref="UbwFullOpen"/> then drops the swords back; the job ends by itself on
    /// Release or Cancel.
    /// </summary>
    public class JobDriver_UbwFullOpen : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil charge = ToilMaker.MakeToil("UbwFullOpen");
            charge.initAction = () => pawn.pather.StopDead();
            charge.tickIntervalAction = delta =>
            {
                Pawn foe = job.targetA.Pawn;
                if (foe != null && foe.Spawned && foe.Map == pawn.Map) pawn.rotationTracker.FaceTarget(foe);
                if (GameComponent_UnlimitedBladeWorks.Instance?.For(pawn)?.fullOpen.Charging != true) EndJobWith(JobCondition.Succeeded);
            };
            charge.handlingFacing = true;
            charge.defaultCompleteMode = ToilCompleteMode.Never;
            yield return charge;
        }
    }
}
