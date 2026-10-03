using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>Vector Flick's numbers (AG_VectorFlick). Range, warm-up and cooldown are the AbilityDef's.</summary>
    public class CompProperties_VectorFlick : CompProperties_AbilityEffect
    {
        public float damage = 14f, armorPenetration = 0.3f;
        /// <summary>The pebble's speed: the hit lands distance / cellsPerSecond after the kick.</summary>
        public float cellsPerSecond = 42f;

        public CompProperties_VectorFlick()
        {
            compClass = typeof(CompAbilityEffect_VectorFlick);
        }
    }

    /// <summary>
    /// Vector Flick (accelerator-vector-flick.js): at the end of the warm-up he kicks a pebble at the target.
    /// It always hits (decided at the port, 2026-09-29): the damage lands on the target when the pebble gets
    /// there, wherever it has moved, and pawns in between are not hit. <see cref="MapComponent_Flicks"/> times
    /// the hit and draws the picture.
    /// </summary>
    public class CompAbilityEffect_VectorFlick : CompAbilityEffect
    {
        public new CompProperties_VectorFlick Props => (CompProperties_VectorFlick)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!(target.Thing is Pawn pawn) || pawn == parent.pawn) return false;
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !(target.Thing is Pawn victim) || !victim.Spawned) return;
            float warmup = parent.verb.verbProps.warmupTime * caster.GetStatValue(StatDefOf.AimingDelayFactor);
            caster.Map.GetComponent<MapComponent_Flicks>().Kick(caster, victim, Props, warmup);
        }
    }

    /// <summary>
    /// Runs Vector Flick on a map: each kicked pebble in flight and its hit. Several can be in the air at once
    /// (cooldown 2 s). The clock is game ticks; nothing is saved but the owed hits.
    /// </summary>
    public class MapComponent_Flicks : MapComponent
    {
        public class Flick : IExposable
        {
            public Pawn caster, target;
            public int kickTick, hitTick;
            public bool hit;
            public float damage, armorPenetration, warmup, distance, speed;
            public int seed;
            /// <summary>Where the caster stood and the aim at the kick, for the picture.</summary>
            public Vector2 feet, aim;

            public void ExposeData()
            {
                Scribe_References.Look(ref caster, "caster");
                Scribe_References.Look(ref target, "target");
                Scribe_Values.Look(ref kickTick, "kickTick");
                Scribe_Values.Look(ref hitTick, "hitTick");
                Scribe_Values.Look(ref hit, "hit");
                Scribe_Values.Look(ref damage, "damage");
                Scribe_Values.Look(ref armorPenetration, "armorPenetration");
                Scribe_Values.Look(ref warmup, "warmup");
                Scribe_Values.Look(ref distance, "distance");
                Scribe_Values.Look(ref speed, "speed", 42f);
                Scribe_Values.Look(ref seed, "seed");
                Scribe_Values.Look(ref feet, "feet");
                Scribe_Values.Look(ref aim, "aim");
            }
        }

        /// <summary>After the hit a flick is kept this long for the picture's tail (the bits that stay).</summary>
        public const int TailTicks = 90;

        private List<Flick> flicks = new List<Flick>();
        private int kicks;

        public IReadOnlyList<Flick> Flicks => flicks;

        public MapComponent_Flicks(Map map) : base(map) { }

        public void Kick(Pawn caster, Pawn target, CompProperties_VectorFlick props, float warmup)
        {
            int now = Find.TickManager.TicksGame;
            Vector3 from = caster.DrawPos, to = target.DrawPos;
            var run = new Vector2(to.x - from.x, to.z - from.z);
            float distance = run.magnitude;
            float speed = Mathf.Max(1f, props.cellsPerSecond);
            flicks.Add(new Flick
            {
                caster = caster, target = target, kickTick = now, damage = props.damage, armorPenetration = props.armorPenetration,
                warmup = warmup, distance = distance, speed = speed, seed = kicks++,
                feet = new Vector2(from.x, from.z), aim = distance < 1e-4f ? Vector2.right : run / distance,
                // The pebble leaves from his foot, not his centre (the picture's HitAt).
                hitTick = now + Mathf.Max(1, Mathf.RoundToInt(VectorFlick.HitAt(0f, distance, speed) * 60f)),
            });
            if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(VectorFlick.KickShake);
        }

        public override void MapComponentTick()
        {
            if (flicks.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = flicks.Count - 1; i >= 0; i--)
            {
                Flick f = flicks[i];
                if (!f.hit && now >= f.hitTick)
                {
                    f.hit = true;
                    Pawn target = f.target;
                    if (target != null && !target.Dead && target.Spawned && target.Map == map)
                    {
                        float angle = new Vector3(f.aim.x, 0f, f.aim.y).AngleFlat();
                        target.TakeDamage(new DamageInfo(DamageDefOf.Blunt, f.damage, f.armorPenetration, angle, f.caster));
                    }
                    if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(VectorFlick.HitShake);
                }
                if (f.hit && now - f.hitTick > TailTicks) flicks.RemoveAt(i);
            }
        }

        // ---- the picture (VectorFlickGraphics, the port of accelerator-vector-flick.js) -----------------------------

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            PawnFit.Begin();
            try
            {
                DrawWarmups();
                for (int i = 0; i < flicks.Count; i++) Draw(flicks[i]);
            }
            finally
            {
                PawnFit.End();
            }
        }

        private static readonly Color Shoe = new Color(0.07f, 0.07f, 0.08f);

        private void Draw(Flick f)
        {
            Vector2? target = null;
            if (f.target != null && f.target.Spawned && f.target.Map == map) target = new Vector2(f.target.DrawPos.x, f.target.DrawPos.z);
            VectorFlickGraphics.Draw(Shot(f.caster, f.feet, f.aim, f.distance, f.warmup, f.speed, target, f.seed), f.warmup + PictureClock.Since(f.kickTick), map);
        }

        private static VectorFlickShot Shot(Pawn caster, Vector2 feet, Vector2 aim, float distance, float warmup, float speed, Vector2? target, int seed) =>
            new VectorFlickShot
            {
                Feet = feet, Aim = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg, Distance = distance, Warmup = warmup, Speed = speed,
                Pants = AcceleratorKit.Pants(caster), Shoe = Shoe, Target = target, Seed = seed,
            };

        /// <summary>The warm-up (the pebble popping out of the floor, the leg drawn back) from his aiming stance, before the kick.</summary>
        private void DrawWarmups()
        {
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (!(pawn.stances?.curStance is Stance_Warmup warmup) || !(warmup.verb is Verb_CastAbility verb)) continue;
                if (verb.ability?.def != AcceleratorDefOf.AG_VectorFlick || !(warmup.focusTarg.Thing is Pawn target) || !target.Spawned) continue;
                float elapsed = PictureClock.Since(warmup.startedTick), total = (Find.TickManager.TicksGame - warmup.startedTick + warmup.ticksLeft) / 60f;
                Vector3 from = pawn.DrawPos, to = target.DrawPos;
                var run = new Vector2(to.x - from.x, to.z - from.z);
                float distance = run.magnitude;
                var props = AcceleratorKit.Props<CompProperties_VectorFlick>(verb.ability.def);
                VectorFlickGraphics.Draw(Shot(pawn, new Vector2(from.x, from.z), distance < 1e-4f ? Vector2.right : run / distance, distance,
                    Mathf.Max(0.05f, total), props?.cellsPerSecond ?? 42f, new Vector2(to.x, to.z), kicks), Mathf.Min(elapsed, total - 0.001f), map);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref flicks, "flicks", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && flicks == null) flicks = new List<Flick>();
        }
    }

    /// <summary>
    /// The manipulation Apply, for its picture: each round a changed group rewrote, where it was caught, the
    /// heading it came in on and the one it left on, and its force; plus the strain the Apply cost. Filled by
    /// <see cref="VectorEditSession.Apply"/>. Picture only: not saved.
    /// </summary>
    public class MapComponent_VectorApplies : MapComponent
    {
        public class Edited
        {
            public Thing round;
            public Vector2 caught, before, after, last;
            public float force, speedPerTick;
            public bool gone;
            public int goneTick = -1;
        }

        public class Applied
        {
            public Pawn caster;
            public Vector2 feet;
            public int tick;
            public float strain;
            public readonly List<Edited> rounds = new List<Edited>();
        }

        public const int TailTicks = 150;

        public readonly List<Applied> applied = new List<Applied>();

        public MapComponent_VectorApplies(Map map) : base(map) { }

        public Applied Begin(Pawn caster, float strain)
        {
            Vector3 at = caster.DrawPos;
            var entry = new Applied { caster = caster, feet = new Vector2(at.x, at.z), tick = Find.TickManager.TicksGame, strain = strain };
            applied.Add(entry);
            if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(VectorApply.Shake);
            return entry;
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map || applied.Count == 0) return;
            PawnFit.Begin();
            try
            {
                foreach (Applied a in applied) Draw(a);
            }
            finally
            {
                PawnFit.End();
            }
        }

        private VectorApplyRound[] scratch = new VectorApplyRound[16];

        private void Draw(Applied a)
        {
            if (scratch.Length != a.rounds.Count) scratch = new VectorApplyRound[a.rounds.Count];
            for (int i = 0; i < a.rounds.Count; i++)
            {
                Edited e = a.rounds[i];
                bool exists = !e.gone && e.round != null && e.round.Spawned && !e.round.Destroyed;
                if (exists)
                {
                    Vector3 p = Rounds.For(e.round)?.Position(e.round) ?? e.round.DrawPos;
                    e.last = new Vector2(p.x, p.z);
                }
                scratch[i] = new VectorApplyRound
                {
                    Caught = e.caught, OldHeading = e.before, NewHeading = e.after, Force = e.force, Speed = e.speedPerTick * 60f,
                    Live = e.last, Exists = exists, Seed = i,
                };
            }
            VectorApplyGraphics.Draw(new VectorApplyShot { Feet = a.feet, Strain = a.strain, Reach = VectorEditDefaults.ScanRadiusCells, Rounds = scratch },
                PictureClock.Since(a.tick), map);
        }

        public override void MapComponentTick()
        {
            if (applied.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = applied.Count - 1; i >= 0; i--)
            {
                Applied a = applied[i];
                bool live = false;
                foreach (Edited e in a.rounds)
                {
                    if (e.gone) continue;
                    if (e.round == null || e.round.Destroyed || !e.round.Spawned)
                    {
                        e.gone = true;
                        e.goneTick = now;
                        continue;
                    }
                    Vector3 p = Rounds.For(e.round)?.Position(e.round) ?? e.round.DrawPos;
                    e.last = new Vector2(p.x, p.z);
                    live = true;
                }
                if (!live && now - a.tick > TailTicks) applied.RemoveAt(i);
            }
        }
    }
}
