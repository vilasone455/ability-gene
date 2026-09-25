using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Plays Bank Shot's charged shots for real casts and does what the picture shows at the moment
    /// it shows it: the ricochet sounds and shakes as the drawn bullet reaches each wall, and the hit
    /// when it reaches the pawn.
    ///
    /// A cast is known from the start of its warmup (<see cref="Begin"/>, from JobDriver_CastBankShot),
    /// because the charge glow builds over the warmup. Until the shot the flight is worked out again
    /// every few ticks, so the aim line drawn during the charge follows pawns that step into it. The
    /// ability's comp fires it (<see cref="Fire"/>): the flight is fixed then, and so is the pawn it
    /// stops at. That pawn is hit when the picture's bullet arrives, wherever it has moved to by then,
    /// if it is still on the map and alive.
    ///
    /// The clock is game ticks, so the picture pauses and speeds up with the game. Nothing is saved:
    /// a game loaded in the middle of a shot loses the rest of its picture and a hit not dealt yet.
    /// </summary>
    public class MapComponent_BankShot : MapComponent
    {
        private sealed class Cast
        {
            public Pawn caster;
            public IntVec3 target;
            public CompProperties_BankShotCharge props;
            public ThingDef weapon;
            public BankShotShot shot;
            public int startTick, fireTick = -1, bouncesHeard;
            public bool ended;
            public Pawn victim;
        }

        /// <summary>A charge whose warmup ran out this long ago without firing was interrupted.</summary>
        private const float Overdue = 0.25f;
        /// <summary>Ticks between fresh traces of a charging shot's flight.</summary>
        private const int Retrace = 6;

        private readonly List<Cast> casts = new List<Cast>();

        public MapComponent_BankShot(Map map) : base(map) { }

        /// <summary>A charge has begun.</summary>
        public void Begin(Pawn caster, Ability ability, LocalTargetInfo target)
        {
            Ended(caster);
            var comp = ability?.CompOfType<CompAbilityEffect_BankShotCharge>();
            if (caster == null || comp == null || !target.IsValid || target.Cell == caster.Position) return;
            float charge = ability.def.verbProperties.warmupTime;
            casts.Add(new Cast
            {
                caster = caster, target = target.Cell, props = comp.Props, weapon = caster.equipment?.Primary?.def,
                shot = BankShotMap.Shot(caster, target.Cell, charge, comp.Props), startTick = Find.TickManager.TicksGame,
            });
        }

        /// <summary>The warmup is over and the ability has been applied: the shot leaves now.</summary>
        public void Fire(Pawn caster, IntVec3 target, float charge, CompProperties_BankShotCharge props)
        {
            int now = Find.TickManager.TicksGame;
            Cast cast = casts.Find(c => c.caster == caster && c.fireTick < 0);
            if (cast == null)
            {
                cast = new Cast { caster = caster, weapon = caster.equipment?.Primary?.def, startTick = now - Mathf.RoundToInt(charge * 60f) };
                casts.Add(cast);
            }
            cast.target = target;
            cast.props = props;
            cast.shot = BankShotMap.Shot(caster, target, charge, props);
            cast.victim = BankShotMap.Victim(map, cast.shot.Path, caster);
            cast.fireTick = now;
            PowerPoleSound.Play(props.soundFire, map, cast.shot.Caster);
            Shake(BankShotTiming.FireShake);
        }

        /// <summary>The caster's cast job is over. A charge that never fired has nothing left to show.</summary>
        public void Ended(Pawn caster)
        {
            casts.RemoveAll(c => c.caster == caster && c.fireTick < 0);
        }

        public override void MapComponentTick()
        {
            if (casts.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                Cast cast = casts[i];
                if (cast.fireTick < 0)
                {
                    float charging = (now - cast.startTick) / 60f;
                    if (charging > cast.shot.Charge + Overdue || cast.caster == null || !cast.caster.Spawned || cast.caster.Map != map)
                    {
                        casts.RemoveAt(i);
                        continue;
                    }
                    if ((now - cast.startTick) % Retrace == 0) cast.shot = BankShotMap.Shot(cast.caster, cast.target, cast.shot.Charge, cast.props);
                    continue;
                }
                float flown = (now - cast.fireTick) / 60f;
                BankShotPath path = cast.shot.Path;
                while (cast.bouncesHeard < path.Bounces.Count && flown >= path.Bounces[cast.bouncesHeard].D / cast.shot.Speed)
                {
                    BankShotBounce b = path.Bounces[cast.bouncesHeard++];
                    PowerPoleSound.Play(WallSound(b.CellX, b.CellZ), map, cast.shot.Map(b.X, b.Z));
                    Shake(BankShotTiming.BounceShake);
                }
                if (!cast.ended && flown >= path.Length / cast.shot.Speed)
                {
                    cast.ended = true;
                    End(cast);
                }
                if (flown >= path.Length / cast.shot.Speed + BankShotTiming.Hold + BankShotTiming.Tail) casts.RemoveAt(i);
            }
        }

        private void End(Cast cast)
        {
            BankShotPath path = cast.shot.Path;
            Vector2 at = cast.shot.Map(path.EndPoint.X, path.EndPoint.Z);
            if (path.End == BankShotEnd.Embed)
            {
                PowerPoleSound.Play(WallSound(path.Embed.CellX, path.Embed.CellZ), map, at);
                Shake(BankShotTiming.EndShake);
                return;
            }
            Pawn victim = cast.victim;
            if (path.End != BankShotEnd.Hit || victim == null || !victim.Spawned || victim.Map != map || victim.Dead) return;
            path.Along(path.Length, out _, out _, out double dx, out double dz);
            float amount = BankShotMap.Damage(cast.props, path.Bounces.Count);
            var dinfo = new DamageInfo(cast.props.damageDef ?? DamageDefOf.Bullet, amount, amount * cast.props.armorPenetrationPerDamage,
                new Vector3((float)dx, 0f, (float)dz).AngleFlat(), cast.caster, null, cast.weapon, DamageInfo.SourceCategory.ThingOrUnknown, victim);
            PowerPoleSound.Play(victim.def.soundImpactDefault, map, at);
            Shake(BankShotTiming.EndShake);
            victim.TakeDamage(dinfo);
        }

        /// <summary>The bullet-impact sound of the wall's material, or of the wall itself.</summary>
        private SoundDef WallSound(int x, int z)
        {
            Building wall = new IntVec3(x, 0, z).GetEdificeSafe(map);
            return wall?.Stuff?.stuffProps?.soundImpactBullet ?? wall?.def.soundImpactDefault;
        }

        private void Shake(float size)
        {
            if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(size);
        }

        public override void MapComponentUpdate()
        {
            if (casts.Count == 0 || Find.CurrentMap != map) return;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < casts.Count; i++)
            {
                Cast cast = casts[i];
                // Until the shot the clock stops just before the picture's fire time.
                float seconds = cast.fireTick < 0
                    ? Mathf.Min(BankShotTiming.Lead + (now - cast.startTick) / 60f, cast.shot.FireAt - 0.001f)
                    : cast.shot.FireAt + (now - cast.fireTick) / 60f;
                BankShotGraphics.Draw(cast.shot, seconds, map, false, true, null, 0f);
            }
        }

        public override void MapRemoved() => casts.Clear();
    }
}
