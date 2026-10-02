using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using T = RimArt.EgoMagicBulletTiming;

namespace RimArt
{
    /// <summary>
    /// One Magic Bullet shot, from the start of its aim to the end of its picture. The clock is the picture's: 0 when the
    /// aim starts, the shot at <see cref="EgoMagicBulletTiming.Fire"/>(lead). A shot the player orders aims for the verb's
    /// warmup and goes off when the verb fires (<see cref="Verb_EgoMagicBullet"/>); a corroded or Overclock firing aims for
    /// actionAim and goes off on its own tick. When it goes off, everything happens at once (the damage does not wait for
    /// the bullet the picture flies at 120 cells/s): the line runs from the shooter's cell centre through the target's
    /// centre to the verb's range, or on the seventh to the beloved and no further, and hits every pawn whose cell it
    /// passes. A target that is not a pawn (a turret, a building, a wall) is hit too; other things on the line are not.
    /// Not saved: a save in the 3 s of a picture loses the picture, and an action firing that had not gone off.
    /// </summary>
    public sealed class EgoMagicBulletCast
    {
        public Pawn shooter;
        /// <summary>The map the shot is on: the shooter's when it aimed, and when it went off.</summary>
        public Map map;
        public CompEgoMagicBullet gun;
        /// <summary>The verb whose warmup this aim is; null for a corroded or Overclock firing.</summary>
        public Verb verb;
        public LocalTargetInfo target;
        /// <summary>Overclock: only hostiles are hit, and the shot neither counts nor can be the seventh.</summary>
        public bool hostilesOnly;
        /// <summary>The picture's corroded look: a corroded or Overclock firing.</summary>
        public bool corroded;
        /// <summary>The shot's number on the gun, 1 to 7; 7 is the seventh.</summary>
        public int shot;
        /// <summary>The picture's aim time, s; on the seventh the aim before the turn to the beloved.</summary>
        public float lead;
        /// <summary>The tick the picture's clock reads 0.</summary>
        public int startTick;
        public int fireTick = -1;
        /// <summary>Degrees, 0 east, 90 north: the aim, and the seventh's line.</summary>
        public float aim, seventhAim;
        /// <summary>Cells the picture's beam flies from the muzzle.</summary>
        public float range;
        /// <summary>The shooter's ground point when it went off.</summary>
        public Vector2 from;
        public EgoMagicBulletHit[] hits = new EgoMagicBulletHit[0];
        /// <summary>Who the line hit, nearest first, for tests and the log.</summary>
        public readonly List<Pawn> victims = new List<Pawn>();
        /// <summary>
        /// The target when it is not a pawn, hit after the pawns: without it a shot at a turret or a building would do
        /// nothing and still count toward the seventh. Null on the seventh and for a pawn or a cell.
        /// </summary>
        public Thing struck;
        /// <summary>Set each frame: a newer shot by the same shooter draws the rifle.</summary>
        public bool lineOnly;

        public bool Fired => fireTick >= 0;
        public bool Seventh => shot >= T.Shots;
        /// <summary>Whether this shot counts on the gun: every shot but Overclock's.</summary>
        public bool Counts => !hostilesOnly;
        /// <summary>The tick an action firing goes off.</summary>
        public int PlannedFireTick => startTick + Mathf.RoundToInt(T.Fire(lead, Seventh) * 60f);
        public int EndTick => startTick + Mathf.CeilToInt(T.End(lead, Seventh, range) * 60f);

        /// <summary>The shot number the gun shows next: the seventh only for a shot that counts.</summary>
        private int NextNumber() => Counts ? gun.NextShot : Mathf.Min(gun.NextShot, T.Shots - 1);

        /// <summary>A shot the player ordered: its aim starts with the verb's warmup, <paramref name="warmupTicks"/> long.</summary>
        public static EgoMagicBulletCast Aim(Pawn shooter, CompEgoMagicBullet gun, Verb verb, LocalTargetInfo target, int warmupTicks)
        {
            var cast = new EgoMagicBulletCast { shooter = shooter, map = shooter.Map, gun = gun, verb = verb, target = target, startTick = Find.TickManager.TicksGame };
            cast.shot = cast.NextNumber();
            cast.corroded = shooter.MentalState is MentalState_EgoCorroded;
            float warm = warmupTicks / 60f;
            cast.lead = cast.Seventh ? Mathf.Max(0.05f, warm - T.Swing) : warm;
            cast.Plan();
            return cast;
        }

        /// <summary>A corroded firing (<paramref name="hostilesOnly"/> false) or an Overclock one at <paramref name="target"/>.</summary>
        public static EgoMagicBulletCast Action(Pawn shooter, CompEgoMagicBullet gun, Pawn target, bool hostilesOnly)
        {
            var cast = new EgoMagicBulletCast
            {
                shooter = shooter, map = shooter.Map, gun = gun, target = target, hostilesOnly = hostilesOnly, corroded = true,
                startTick = Find.TickManager.TicksGame, lead = gun.Props.actionAim,
            };
            cast.shot = cast.NextNumber();
            cast.Plan();
            return cast;
        }

        /// <summary>Before it goes off: the aim follows the target, the seventh's line the beloved, the beam the range.</summary>
        public void Plan()
        {
            if (!shooter.Spawned) return;
            Vector2 o = EgoMagicBullet.Centre(shooter.Position);
            Vector2 to = target.IsValid ? EgoMagicBullet.Centre(target.HasThing ? target.Thing.Position : target.Cell) : o;
            if ((to - o).sqrMagnitude > 0.01f) aim = Degrees(to - o);
            range = Mathf.Max(0.5f, gun.PrimaryVerb.EffectiveRange - T.MuzzleAlong);
            if (!Seventh) return;
            Pawn beloved = EgoMagicBullet.Beloved(shooter);
            Vector2 b = EgoMagicBullet.Centre(beloved.Position);
            seventhAim = beloved == shooter ? aim : Degrees(b - o);
            range = Mathf.Max(0.5f, (b - o).magnitude - T.MuzzleAlong);
        }

        /// <summary>
        /// The shot goes off now. The number is taken again (another shot may have counted since the aim began), the line is
        /// laid and every pawn on it hit, the count moves on, and the picture's clock is set so this tick is its shot.
        /// </summary>
        public void Fire(int now)
        {
            shot = NextNumber();
            bool seventh = Seventh;
            CompProperties_EgoMagicBullet p = gun.Props;
            ThingWithComps weapon = gun.parent;
            ThingDef round = gun.PrimaryVerb.verbProps.defaultProjectile;
            map = shooter.Map;
            Vector2 o = EgoMagicBullet.Centre(shooter.Position);
            Pawn beloved = seventh ? EgoMagicBullet.Beloved(shooter) : null;
            Thing intended = seventh ? beloved : target.Thing;
            struck = !seventh && target.Thing != null && !(target.Thing is Pawn) && target.Thing.Spawned && target.Thing.Map == map ? target.Thing : null;
            Vector2 to = seventh ? EgoMagicBullet.Centre(beloved.Position)
                : EgoMagicBullet.Centre(target.HasThing ? target.Thing.Position : target.Cell);
            Vector2 dir = (to - o).sqrMagnitude > 0.01f ? (to - o).normalized : VfxDraw.Turn(aim);
            float length = seventh ? (to - o).magnitude : gun.PrimaryVerb.EffectiveRange;
            float amount = seventh ? p.seventhDamage * weapon.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier) : round.projectile.GetDamageAmount(weapon);

            fireTick = now;
            startTick = now - Mathf.RoundToInt(T.Fire(lead, seventh) * 60f);
            from = Ground(shooter.DrawPos);
            if (seventh) seventhAim = Degrees(dir);
            else aim = Degrees(dir);
            range = Mathf.Max(0.5f, length - T.MuzzleAlong);

            // The picture's hits first: a pawn the line kills leaves no DrawPos.
            var alongs = new List<float>();
            victims.Clear();
            if (seventh && beloved == shooter)
            {
                victims.Add(shooter);
                alongs.Add(T.MuzzleAlong);
                range = 0.5f;
            }
            else victims.AddRange(EgoMagicBullet.Crossed(shooter, o, dir, length, hostilesOnly, alongs));
            var cells = new List<IntVec3>();
            var wallAlongs = new List<float>();
            EgoMagicBullet.Walls(map, o, dir, length, cells, wallAlongs);
            // A struck wall is already one of the punched cells; a turret or a building that does not fill its cell gets a hole of its own.
            bool punchStruck = struck != null && !cells.Contains(struck.Position);
            hits = new EgoMagicBulletHit[victims.Count + cells.Count + (punchStruck ? 1 : 0)];
            for (int i = 0; i < victims.Count; i++)
                hits[i] = new EgoMagicBulletHit { At = Ground(victims[i].DrawPos), Along = alongs[i] - T.MuzzleAlong };
            for (int i = 0; i < cells.Count; i++)
                hits[victims.Count + i] = new EgoMagicBulletHit { At = o + dir * wallAlongs[i], Along = wallAlongs[i] - T.MuzzleAlong, Wall = true };
            if (punchStruck)
            {
                float along = Vector2.Dot(EgoMagicBullet.Centre(struck.Position) - o, dir);
                hits[hits.Length - 1] = new EgoMagicBulletHit { At = o + dir * along, Along = along - T.MuzzleAlong, Wall = true };
            }

            for (int i = 0; i < victims.Count; i++)
                if (!victims[i].Dead) EgoRound.Hit(shooter, weapon, round, victims[i], intended, amount, dir);
            if (struck != null && !struck.Destroyed) EgoRound.Hit(shooter, weapon, round, struck, struck, amount, dir);
            if (Counts) gun.Counted(seventh);
            // A shot the player ordered gets the verb's sound from Core; an action's firing has no verb use.
            if (verb == null && shooter.Spawned) gun.PrimaryVerb.verbProps.soundCast?.PlayOneShot(new TargetInfo(shooter.Position, map));
            if (map == Find.CurrentMap) Find.CameraDriver.shaker.DoShake(seventh ? T.SeventhShake : T.FireShake);
        }

        /// <summary>
        /// One game tick; false once the shot is over. An aim ends with no shot when its verb is no longer warming up for it
        /// (the order changed, a stun) or the shooter is gone; an action firing goes off on its tick if the shooter still
        /// stands with the gun and the gun is not cooling down after a seventh.
        /// </summary>
        public bool Tick(int now)
        {
            if (Fired) return now < EndTick;
            if (shooter.Dead || !shooter.Spawned || shooter.Downed || CompEgoMagicBullet.HeldBy(shooter) != gun) return false;
            if (verb != null)
            {
                if (!(shooter.stances.curStance is Stance_Warmup warmup) || warmup.verb != verb) return false;
                Plan();
                return true;
            }
            if (now < PlannedFireTick)
            {
                if (target.Thing is Pawn p && p.Spawned && p.Map == shooter.Map) Plan();
                return true;
            }
            if (gun.CoolingDown || (target.HasThing && (!target.Thing.Spawned || target.Thing.Map != shooter.Map))) return false;
            Fire(now);
            return true;
        }

        /// <summary>
        /// The picture this frame. Before the shot the clock stops just short of it, so a warmup a tick longer than planned
        /// never shows the shot early. Once the shooter has fallen or left, only the line is drawn.
        /// </summary>
        public void Draw()
        {
            float s = PictureClock.Since(startTick);
            if (!Fired) s = Mathf.Min(s, T.Fire(lead, Seventh) - 0.001f);
            bool standing = shooter.Spawned && shooter.Map == map && !shooter.Dead;
            Vector2 stand = standing ? Ground(shooter.DrawPos) : from;
            EgoMagicBulletGraphics.Draw(new EgoMagicBulletShot
            {
                Shooter = Fired ? from : stand, Stand = lineOnly || !standing ? from : stand, Aim = aim, SeventhAim = seventhAim, Shot = shot,
                Lead = lead, Range = range, Corroded = corroded, Hits = hits, HitCount = hits.Length, LineOnly = lineOnly || !standing,
            }, s, map);
        }

        public static Vector2 Ground(Vector3 v) => new Vector2(v.x, v.z);

        public static float Degrees(Vector2 d) => Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
    }
}
