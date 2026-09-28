using RimWorld;
using UnityEngine;
using Verse;
using T = RimArt.RasenganTiming;

namespace RimArt
{
    public class CompProperties_Rasengan : CompProperties_AbilityEffect
    {
        public float damage = 30f;
        public DamageDef damageDef;
        public float armorPenetration = 0.4f;
        /// <summary>Cells the target is thrown, away from Minato.</summary>
        public float throwCells = 3f;
        /// <summary>Extra damage when something solid stops the throw short.</summary>
        public float slamDamage = 10f;
        /// <summary>Stun after the release. The target is also held for the 0.3 s grind before it.</summary>
        public int stunTicks = 120;
        /// <summary>Farthest a pawn with no mark may be when the ball is driven in: touch range.</summary>
        public float touchRange = 1.9f;

        public CompProperties_Rasengan()
        {
            compClass = typeof(CompAbilityEffect_Rasengan);
        }
    }

    /// <summary>
    /// Rasengan. The verb's range is the marked range (29.9); a pawn with no mark is reached by walking up to it
    /// first (<see cref="JobDriver_CastMinato"/>), as a touch ability. The cast is <see cref="RasenganCast"/>.
    /// </summary>
    public class CompAbilityEffect_Rasengan : CompAbilityEffect_Minato
    {
        public new CompProperties_Rasengan Props => (CompProperties_Rasengan)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            string problem = null;
            if (!(target.Thing is Pawn pawn) || !pawn.Spawned || pawn.Dead) problem = "Needs a pawn.";
            else if (pawn == parent.pawn) problem = "Minato cannot hit himself.";
            else if (RasenganCast.FromRange(parent.pawn, pawn) && !ThunderGodMarks.Behind(parent.pawn, parent.pawn.Position, pawn).IsValid)
                problem = "No room to land behind " + pawn.LabelShort + ".";
            if (problem == null) return base.Valid(target, throwMessages);
            Reject(problem, parent.pawn, throwMessages);
            return false;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (!(target.Thing is Pawn pawn)) return;
            MinatoCasts.For<RasenganCast>(parent, target)?.Launch(pawn, Find.TickManager.TicksGame);
        }
    }

    /// <summary>
    /// One Rasengan (the sketch kunai-rasengan.js). The warmup is the ball forming in the hand (0.6 s, standing).
    /// Then:
    ///   The target holds one of Minato's marks and is not next to him: he jumps to the free cell most behind it
    ///   (0.05 s) and thrusts from there, so it is thrown back toward where he came from.
    ///   Otherwise: he has walked up to it; if it has stepped out of touch range meanwhile, the ball bursts on
    ///   nothing.
    /// 0.1 s after the fire the ball reaches the body and grinds for 0.3 s (the target is stunned from here); on
    /// the release it takes 30 blunt (40 % armour penetration), is thrown 3 cells away from him over up to 0.35 s
    /// (its cell changes once, on landing; smaller bodies are not thrown farther) and is stunned 2 s. Something
    /// solid that stops it short (a wall, a building, the map edge) adds 10 blunt. A pawn killed by the hit is not
    /// thrown.
    /// </summary>
    public sealed class RasenganCast : MinatoCast
    {
        public Pawn target;
        public IntVec3 from, spot;
        public bool teleports, arrived, hit, released, landedThrow, aborted, missed, wall;
        /// <summary>The throw: where it started (x, z), its direction, how far it goes, and the cell it ends on.</summary>
        public Vector2 throwFrom, away;
        public float thrown;
        public IntVec3 throwTo;

        public override AbilityDef Def => MinatoDefOf.AG_Rasengan;
        protected override float Lead => T.Lead;
        protected override float FireAt => T.FormedAt;

        private CompProperties_Rasengan Props => MinatoKit.Props<CompProperties_Rasengan>(Def) ?? new CompProperties_Rasengan();

        /// <summary>The Rasengan is cast from range: the pawn holds one of his marks and is not already next to him.</summary>
        public static bool FromRange(Pawn caster, Pawn target) =>
            caster != null && target != null && ThunderGodMarks.Marked(target) && !caster.Position.AdjacentTo8WayOrInside(target.Position);

        public void Launch(Pawn pawn, int now)
        {
            target = pawn;
            MarkFired(now);
            from = caster.Position;
            teleports = FromRange(caster, pawn);
            spot = teleports ? ThunderGodMarks.Behind(caster, from, pawn) : from;
            if (teleports && !spot.IsValid) missed = true;
            if (!teleports && pawn.Position.DistanceTo(from) > Props.touchRange) missed = true;
        }

        private float Thrown => released ? thrown : Props.throwCells;

        public override bool Holds(int now) => Fired && !aborted && now < TickAt(T.ReleaseAt(teleports)) + 6;

        public override Rot4 Facing(int now)
        {
            if (target == null || !target.Spawned || released) return Rot4.Invalid;
            Vector2 look = MinatoKit.Ground(target.Position) - MinatoKit.Ground(caster.Position);
            return look.sqrMagnitude < 1e-4f ? Rot4.Invalid : Rot4.FromAngleFlat(90f - ThunderGodTiming.Degrees(look));
        }

        public override bool Tick(int now)
        {
            if (!Fired) return now - startTick < 900;
            if (!arrived && now >= TickAt(T.ArriveAt(teleports))) Arrive();
            if (!hit && now >= TickAt(T.HitAt(teleports))) Hit();
            if (!released && now >= TickAt(T.ReleaseAt(teleports))) Release();
            if (released && !landedThrow && now >= TickAt(T.LandAt(teleports, thrown))) Land();
            return Seconds(now) < T.LandAt(teleports, Thrown) + T.Tail;
        }

        private bool TargetFit => target != null && target.Spawned && target.Map == home && !target.Dead;

        private void Arrive()
        {
            arrived = true;
            if (!teleports || missed || aborted || !CasterFit || !TargetFit) return;
            IntVec3 now = ThunderGodMarks.Behind(caster, from, target);
            if (now.IsValid) spot = now;
            if (!ThunderGodMarks.Free(caster, spot, home)) { missed = true; return; }
            Teleport(spot);
        }

        /// <summary>The ball reaches the body: the grind holds it (a stun through the release and the throw).</summary>
        private void Hit()
        {
            hit = true;
            if (missed || aborted || !CasterFit || !TargetFit) return;
            if (!caster.Position.AdjacentTo8WayOrInside(target.Position)) { missed = true; return; }
            int grind = TickAt(T.ReleaseAt(teleports)) - TickAt(T.HitAt(teleports));
            target.stances?.stunner?.StunFor(grind + Props.stunTicks, caster, false, false);
            Find.CameraDriver.shaker.DoShake(T.HitShake);
        }

        /// <summary>The release: the damage, and where the throw goes.</summary>
        private void Release()
        {
            released = true;
            thrown = 0f;
            if (missed || aborted || !CasterFit || !TargetFit || !caster.Position.AdjacentTo8WayOrInside(target.Position))
            {
                missed = true;
                return;
            }
            CompProperties_Rasengan props = Props;
            IntVec3 start = target.Position;
            Vector3 dir = (start - caster.Position).ToVector3();
            if (dir.sqrMagnitude < 0.01f) dir = caster.Rotation.FacingCell.ToVector3();
            dir = dir.normalized;
            throwFrom = MinatoKit.Ground(target.DrawPos);
            away = new Vector2(dir.x, dir.z);
            var dinfo = new DamageInfo(props.damageDef ?? DamageDefOf.Blunt, props.damage, props.armorPenetration, dir.AngleFlat(), caster);
            target.TakeDamage(dinfo);
            Find.CameraDriver.shaker.DoShake(T.ReleaseShake);
            if (target.Dead || !target.Spawned) return;

            // Walk the throw out a cell at a time; it stops before the first cell it cannot stand in or see.
            throwTo = start;
            int most = Mathf.RoundToInt(props.throwCells);
            for (int i = 1; i <= most; i++)
            {
                IntVec3 c = (start.ToVector3Shifted() + dir * i).ToIntVec3();
                if (c == throwTo) continue;
                if (!c.InBounds(home) || !c.Standable(home) || !GenSight.LineOfSight(start, c, home, true))
                {
                    wall = true;
                    break;
                }
                throwTo = c;
            }
            thrown = (throwTo - start).LengthHorizontal;
            if (thrown < 0.01f) Land();
        }

        /// <summary>The throw ends: the cell changes once, and a wall adds its slam.</summary>
        private void Land()
        {
            landedThrow = true;
            if (!TargetFit) return;
            if (throwTo.IsValid && throwTo != target.Position && throwTo.InBounds(home) && throwTo.Standable(home))
            {
                target.pather?.StopDead();
                target.Position = throwTo;
                target.Notify_Teleported(true, false);
            }
            if (wall && Props.slamDamage > 0f)
            {
                target.TakeDamage(new DamageInfo(DamageDefOf.Blunt, Props.slamDamage, 0f, new Vector3(away.x, 0f, away.y).AngleFlat(), caster));
                Find.CameraDriver.shaker.DoShake(T.WallShake);
            }
        }

        public override void JobEnded(int now)
        {
            // Downed or killed before the release: the ball goes out. After it, the throw finishes on its own.
            if (!released) aborted = true;
        }

        public override void Discard()
        {
            aborted = true;
        }

        public override void Pose(float s)
        {
            if (!Fired || caster == null || !caster.Spawned || caster.Map != home) return;
            if (teleports && !missed && !aborted)
            {
                float thin = s < T.ArriveAt(true) ? VfxMath.Smooth((s - T.FormedAt) / T.Squeeze) : 1f - VfxMath.Smooth((s - T.ArriveAt(true)) / T.Squeeze);
                MinatoLooks.Thin(caster, thin);
            }
            if (target == null || !target.Spawned || target.Map != home || missed || aborted) return;
            float hitAt = T.HitAt(teleports), releaseAt = T.ReleaseAt(teleports);
            if (s >= hitAt && s < releaseAt + 0.1f)
            {
                MinatoLook look = MinatoLooks.For(target);
                look.pale = 0.55f * VfxMath.Smooth((s - hitAt) / 0.1f) * (1f - VfxMath.Smooth((s - releaseAt) / 0.1f));
            }
            // The thrown pawn is drawn along the throw until its cell changes on landing.
            if (released && !landedThrow && thrown > 0.01f)
            {
                MinatoLook look = MinatoLooks.For(target);
                float gone = T.Gone(s, teleports, thrown, wall);
                Vector2 at = throwFrom + away * gone;
                look.moved = true;
                look.drawAt = new Vector3(at.x, 0f, at.y + VergilKit.FeetBelowDrawPos);
            }
        }

        public override void Draw(float s)
        {
            if (home == null || caster == null) return;
            if (missed || aborted)
            {
                // The ball bursts on nothing: only its forming, cut short where it stood.
                if (!Fired || s >= T.FormedAt + 0.2f) return;
            }
            Vector2 homeAt = Fired ? MinatoKit.Ground(from) : MinatoKit.Ground(caster.DrawPos);
            Vector2 enemy = released ? throwFrom : target != null && target.Spawned ? MinatoKit.Ground(target.DrawPos) : homeAt + Vector2.right;
            bool jump = Fired ? teleports && !missed : FromRange(caster, target);
            IntVec3 planned = Fired ? spot : jump ? ThunderGodMarks.Behind(caster, caster.Position, target) : caster.Position;
            Vector2 spotAt = jump && planned.IsValid ? MinatoKit.Ground(planned) : homeAt;
            Vector2 dir = released ? away : enemy - spotAt;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.right;
            dir.Normalize();
            float seconds = missed || aborted ? Mathf.Min(s, T.FormedAt - 0.001f) : s;
            RasenganGraphics.Draw(homeAt, spotAt, enemy, dir, Thrown, jump, wall, seconds, home);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref spot, "spot");
            Scribe_Values.Look(ref teleports, "teleports");
            Scribe_Values.Look(ref arrived, "arrived");
            Scribe_Values.Look(ref hit, "hit");
            Scribe_Values.Look(ref released, "released");
            Scribe_Values.Look(ref landedThrow, "landedThrow");
            Scribe_Values.Look(ref aborted, "aborted");
            Scribe_Values.Look(ref missed, "missed");
            Scribe_Values.Look(ref wall, "wall");
            Scribe_Values.Look(ref throwFrom, "throwFrom");
            Scribe_Values.Look(ref away, "away");
            Scribe_Values.Look(ref thrown, "thrown");
            Scribe_Values.Look(ref throwTo, "throwTo");
        }
    }
}
