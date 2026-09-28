using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Everything Sasuke's kit keeps between ticks: the weapons Amenoyodomi holds, the weapons let go that still
    /// carry a charge or a fire, the conjured kunai on belts, the Raikō Kusari nets and the black fires on the ground.
    /// The black flame on a pawn is a hediff (<see cref="HediffComp_Amaterasu"/>); its comps are listed here so
    /// Release can find them.
    ///
    /// The Harmony patches on Projectile run for every projectile in the game, so they read the static counts first:
    /// with nothing held and Amenoyodomi off everywhere, each costs one integer read.
    /// </summary>
    public sealed class GameComponent_Rinnegan : GameComponent
    {
        public static GameComponent_Rinnegan Instance { get; private set; }

        /// <summary>Weapons held now, on every map.</summary>
        public static int HeldCount;
        /// <summary>Casters with Amenoyodomi on (hang or drift).</summary>
        public static int OnCount;
        /// <summary>Weapons in flight that carry a charge or a fire.</summary>
        public static int FlyingCount;

        private List<HeldWeapon> held = new List<HeldWeapon>();
        private List<FlyingOn> flying = new List<FlyingOn>();
        private List<RaikoNet> nets = new List<RaikoNet>();
        private List<BlackFire> fires = new List<BlackFire>();
        private List<Thing> conjuredBelts = new List<Thing>();
        private List<int> conjuredCounts = new List<int>();

        private readonly Dictionary<Projectile, HeldWeapon> byShot = new Dictionary<Projectile, HeldWeapon>();
        /// <summary>Weapons let go or dropped: never caught again, even if Amenoyodomi is still on where they come down.</summary>
        private HashSet<Projectile> letGo = new HashSet<Projectile>();
        private readonly List<Ability_Amenoyodomi> on = new List<Ability_Amenoyodomi>();
        private readonly List<HediffComp_Amaterasu> flames = new List<HediffComp_Amaterasu>();

        public GameComponent_Rinnegan(Game game)
        {
            Instance = this;
            HeldCount = OnCount = FlyingCount = 0;
            RinneganPictures.Clear();
        }

        public IReadOnlyList<HeldWeapon> Held => held;
        public IReadOnlyList<FlyingOn> Flying => flying;
        public HashSet<Projectile> LetGoShots => letGo;
        public IReadOnlyList<RaikoNet> Nets => nets;
        public IReadOnlyList<BlackFire> Fires => fires;
        public IReadOnlyList<HediffComp_Amaterasu> Flames => flames;

        // ---- Amenoyodomi ------------------------------------------------------------------------------------------

        public void Notify_ModeChanged(Ability_Amenoyodomi ability)
        {
            if (ability.mode == HoldMode.Off)
            {
                on.Remove(ability);
                DropAll(ability.pawn);
            }
            else if (!on.Contains(ability)) on.Add(ability);
            OnCount = on.Count;
        }

        /// <summary>The ability ticks while it is on, so after a load it is listed again on its first tick.</summary>
        public void Notify_On(Ability_Amenoyodomi ability)
        {
            if (on.Contains(ability)) return;
            on.Add(ability);
            OnCount = on.Count;
        }

        public int CountHeldBy(Pawn caster)
        {
            int count = 0;
            for (int i = 0; i < held.Count; i++)
                if (held[i].caster == caster) count++;
            return count;
        }

        public List<HeldWeapon> HeldBy(Pawn caster) => held.Where(w => w.caster == caster).OrderBy(w => w.order).ToList();

        public bool IsHeld(Projectile shot) => HeldCount > 0 && byShot.ContainsKey(shot);

        public HeldWeapon HeldFor(Projectile shot) => shot != null && byShot.TryGetValue(shot, out HeldWeapon w) ? w : null;

        /// <summary>
        /// The caster's held weapon a click on <paramref name="cell"/> means: one drawn in that cell, or whose floor point
        /// (the rings under it, <see cref="AmenoyodomiGraphics.HeldLift"/> south) is in it.
        /// </summary>
        public HeldWeapon HeldNear(Pawn caster, IntVec3 cell)
        {
            HeldWeapon best = null;
            float bestDistance = float.MaxValue;
            Vector3 centre = SasukeKit.Flat(cell.ToVector3Shifted());
            for (int i = 0; i < held.Count; i++)
            {
                HeldWeapon w = held[i];
                if (w.caster != caster) continue;
                Vector3 floor = w.at - new Vector3(0f, 0f, AmenoyodomiGraphics.HeldLift);
                if (w.at.ToIntVec3() != cell && floor.ToIntVec3() != cell) continue;
                float distance = Mathf.Min((centre - w.at).sqrMagnitude, (centre - floor).sqrMagnitude);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = w;
                }
            }
            return best;
        }

        /// <summary>
        /// From the prefix on Projectile.TickInterval. False skips the engine's tick: the projectile is held, or is
        /// caught now. A kunai is caught on the tick it would land when it was thrown at a cell (not at a thing) by a
        /// caster whose Amenoyodomi is on and who holds fewer than the most.
        /// </summary>
        public bool BeforeTick(Projectile shot, int delta)
        {
            if (byShot.ContainsKey(shot)) return false;
            if (OnCount == 0 || !(shot is Projectile_Kunai) || shot.intendedTarget.HasThing) return true;
            if (Rounds.Vanilla.TicksToImpact(shot) > delta) return true;
            if (!CanCatch(shot)) return true;
            Catch(shot, (Pawn)shot.Launcher, Rounds.Vanilla.Destination(shot));
            return false;
        }

        /// <summary>From Projectile_Fuma when it reaches the end of its line: true when Amenoyodomi holds it there.</summary>
        public bool TryCatchFuma(Projectile_Fuma fuma)
        {
            if (OnCount == 0 || !CanCatch(fuma)) return false;
            Catch(fuma, (Pawn)fuma.Launcher, Rounds.Vanilla.Destination(fuma));
            return true;
        }

        private bool CanCatch(Projectile shot)
        {
            if (!(shot.Launcher is Pawn caster) || letGo.Contains(shot)) return false;
            Ability_Amenoyodomi ability = SasukeKit.Amenoyodomi(caster);
            if (ability == null || ability.mode == HoldMode.Off || !SasukeKit.CanHold(caster, shot.Map)) return false;
            if (CountHeldBy(caster) >= ability.Props.maxHeld) return false;
            IntVec3 cell = Rounds.Vanilla.Destination(shot).ToIntVec3();
            return cell.InBounds(shot.Map) && !CompFuma.Blocked(cell, shot.Map);
        }

        private void Catch(Projectile shot, Pawn caster, Vector3 at)
        {
            Vector3 heading = SasukeKit.Flat(Rounds.Vanilla.Destination(shot) - Rounds.Vanilla.Origin(shot));
            if (heading.sqrMagnitude < 1e-6f) heading = SasukeKit.Flat(at - caster.DrawPos);
            heading = heading.sqrMagnitude < 1e-6f ? Vector3.forward : heading.normalized;
            var w = new HeldWeapon
            {
                shot = shot,
                caster = caster,
                at = SasukeKit.Flat(at),
                heading = heading,
                speed = shot.def.projectile.SpeedTilesPerTick,
                caughtTick = Find.TickManager.TicksGame,
                // Projectiles get their ids as they are launched, so this is the throw order even when a farther
                // throw is caught later.
                order = shot.thingIDNumber
            };
            held.Add(w);
            byShot[shot] = w;
            HeldCount = held.Count;
            MoveTo(w, w.at.ToIntVec3());
            RinneganPictures.Caught(w);
        }

        private void Remove(HeldWeapon w)
        {
            held.Remove(w);
            if (w.shot != null) byShot.Remove(w.shot);
            HeldCount = held.Count;
        }

        private static void MoveTo(HeldWeapon w, IntVec3 cell)
        {
            if (w.shot is Projectile_Fuma fuma) fuma.HeldAt(cell);
            else if (w.shot.Position != cell) w.shot.Position = cell;
        }

        /// <summary>Moves a held weapon, as an Amenotejikara swap does. It keeps its heading and speed.</summary>
        public void Place(HeldWeapon w, Vector3 at)
        {
            w.at = SasukeKit.Flat(at);
            MoveTo(w, w.at.ToIntVec3());
        }

        public void DropAll(Pawn caster)
        {
            foreach (HeldWeapon w in HeldBy(caster)) Drop(w);
        }

        /// <summary>
        /// Lets a held weapon fall to the floor where it hangs: a kunai becomes an item there (a conjured one vanishes),
        /// the Fūma drops as itself. A burning one burns on that cell.
        /// </summary>
        public void Drop(HeldWeapon w)
        {
            Remove(w);
            Projectile shot = w.shot;
            Map map = shot?.MapHeld;
            if (shot == null || shot.Destroyed || map == null) return;
            IntVec3 cell = w.at.ToIntVec3().ClampInsideMap(map);
            RinneganPictures.Dropped(w, map);
            if (shot is Projectile_Fuma fuma)
            {
                if (w.burning) AddFlying(new FlyingOn { shot = fuma, caster = w.caster, burning = true, litTick = w.litTick, letGoTick = Find.TickManager.TicksGame, from = w.at });
                fuma.HeldAt(cell);
                fuma.Destroy();
                return;
            }
            bool conjured = w.Conjured, sealedByMinato = shot.def == KunaiDefOf.AG_KunaiProjectileMinato;
            shot.Destroy();
            if (conjured) KunaiConjure.Vanish(w.at, map);
            else KunaiEmbedding.DropKunai(cell, map, sealedByMinato);
            if (w.burning) AddFire(map, cell, w.caster, SasukeKit.FlameProps.groundBurnTicks, conjured, null, w.at, RinneganPictures.Degrees(w.heading), 0f, w.litTick);
        }

        /// <summary>
        /// Let go: every weapon this caster holds flies on at full speed along its heading. A kunai flies at the first
        /// standing pawn ahead of it on its line (allies too, never Sasuke), or to the end of its range, or to the last
        /// open cell before a wall. The Fūma flies its range and cuts its own line. Weapons in a Raikō Kusari net
        /// leave charged; lit ones stay lit.
        /// </summary>
        public void LetGo(Pawn caster)
        {
            List<HeldWeapon> mine = HeldBy(caster);
            if (mine.Count == 0) return;
            var charged = new HashSet<Projectile>();
            for (int i = nets.Count - 1; i >= 0; i--)
            {
                if (nets[i].caster != caster) continue;
                foreach (Projectile corner in nets[i].corners) charged.Add(corner);
                nets[i].End(Find.TickManager.TicksGame, true);
                nets.RemoveAt(i);
            }
            CompProperties_Amenoyodomi props = SasukeKit.HoldProps;
            foreach (HeldWeapon w in mine) Release(w, charged.Contains(w.shot), props);
            if (caster.Spawned) RinneganPictures.EyeStar(caster);
        }

        private void Release(HeldWeapon w, bool charged, CompProperties_Amenoyodomi props)
        {
            Remove(w);
            Projectile shot = w.shot;
            Map map = shot?.Map;
            if (shot == null || shot.Destroyed || map == null) return;
            letGo.Add(shot);
            if (shot is Projectile_Fuma)
            {
                Fly(shot, w.at, w.at + w.heading * props.fumaRange, w.caster);
            }
            else
            {
                Pawn target = FirstPawnOnLine(w.at, w.heading, props.kunaiRange, w.caster, map, out Vector3 end);
                Vector3 to = target != null ? SasukeKit.Flat(target.DrawPos) : end;
                Fly(shot, w.at, to, w.caster);
                LocalTargetInfo used = target != null ? new LocalTargetInfo(target) : new LocalTargetInfo(to.ToIntVec3());
                shot.usedTarget = used;
                shot.intendedTarget = used;
                shot.HitFlags = ProjectileHitFlags.IntendedTarget | ProjectileHitFlags.NonTargetWorld;
            }
            if (charged || w.burning)
                AddFlying(new FlyingOn
                {
                    shot = shot, caster = w.caster, charged = charged, burning = w.burning,
                    litTick = w.litTick, letGoTick = Find.TickManager.TicksGame, from = w.at
                });
            RinneganPictures.LetGo(w, charged);
        }

        private static void Fly(Projectile shot, Vector3 from, Vector3 to, Pawn caster)
        {
            float speed = Mathf.Max(0.01f, shot.def.projectile.SpeedTilesPerTick);
            int ticks = Mathf.Max(1, Mathf.CeilToInt((to - from).magnitude / speed));
            Rounds.Vanilla.Redirect(shot, from, to, ticks, 1f, caster);
        }

        /// <summary>
        /// The first standing pawn ahead of <paramref name="from"/> along <paramref name="heading"/> within
        /// <paramref name="range"/>, never the caster; null if none. <paramref name="end"/> is where the flight ends
        /// without a pawn: the end of the range, or the middle of the last open cell before a wall or the map edge.
        /// </summary>
        public static Pawn FirstPawnOnLine(Vector3 from, Vector3 heading, float range, Pawn caster, Map map, out Vector3 end)
        {
            Vector3 far = from + heading * range;
            end = far;
            IntVec3 lastOpen = from.ToIntVec3();
            foreach (FumaRules.Cell step in FumaRules.Trace(from.x, from.z, far.x, far.z))
            {
                if (step.Guard) continue;
                var cell = new IntVec3(step.X, 0, step.Z);
                if (!cell.InBounds(map) || CompFuma.Blocked(cell, map))
                {
                    end = lastOpen.ToVector3Shifted();
                    end.y = 0f;
                    return null;
                }
                lastOpen = cell;
                Pawn best = null;
                float bestAlong = float.MaxValue;
                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (!(things[i] is Pawn pawn) || pawn == caster || pawn.Dead || pawn.Downed) continue;
                    float along = Vector3.Dot(SasukeKit.Flat(pawn.DrawPos) - from, heading);
                    if (along < 0f || along >= bestAlong) continue;
                    bestAlong = along;
                    best = pawn;
                }
                if (best != null) return best;
            }
            return null;
        }

        // ---- Weapons in flight that carry something ---------------------------------------------------------------

        private void AddFlying(FlyingOn f)
        {
            flying.RemoveAll(x => x.shot == f.shot);
            flying.Add(f);
            FlyingCount = flying.Count;
        }

        /// <summary>Takes the record of a weapon that is hitting now (the kunai's Impact, the Fūma landing).</summary>
        public FlyingOn TakeFlying(Projectile shot)
        {
            if (FlyingCount == 0) return null;
            for (int i = 0; i < flying.Count; i++)
            {
                if (flying[i].shot != shot) continue;
                FlyingOn f = flying[i];
                flying.RemoveAt(i);
                FlyingCount = flying.Count;
                return f;
            }
            return null;
        }

        public FlyingOn FlyingFor(Projectile shot)
        {
            if (FlyingCount == 0) return null;
            for (int i = 0; i < flying.Count; i++)
                if (flying[i].shot == shot) return flying[i];
            return null;
        }

        /// <summary>
        /// A kunai that carried something hit: <paramref name="pawn"/> is what it hit, or null for the ground.
        /// <paramref name="stop"/> is where it stopped (its drawn point).
        /// </summary>
        public void KunaiHit(FlyingOn f, Pawn pawn, IntVec3 cell, Map map, bool conjured, Vector3 stop)
        {
            if (f.burning) AmaterasuPictures.KunaiStopped(f, map, stop);
            if (pawn != null && !pawn.Dead)
            {
                if (f.charged) Stun(pawn, f.caster);
                if (f.burning) Amaterasu.Ignite(pawn, f.caster, SasukeKit.FlameProps.durationTicks, AmaterasuCatch.Hit);
                return;
            }
            if (f.burning && map != null && cell.InBounds(map))
                AddFire(map, cell, f.caster, SasukeKit.FlameProps.groundBurnTicks, conjured, null, stop,
                    RinneganPictures.Degrees(Rounds.Vanilla.Heading(f.shot)), 0f, f.litTick);
        }

        /// <summary>From Projectile_Fuma: it has just cut <paramref name="pawn"/>.</summary>
        public void FumaCut(Projectile_Fuma fuma, Pawn pawn)
        {
            FlyingOn f = FlyingFor(fuma);
            if (f == null || pawn == null || pawn.Dead) return;
            if (f.charged) Stun(pawn, f.caster);
            if (f.burning) Amaterasu.Ignite(pawn, f.caster, SasukeKit.FlameProps.durationTicks, AmaterasuCatch.Hit);
        }

        /// <summary>A charged weapon's hit: stunned for letGoStunTicks, with the crackle over it.</summary>
        private static void Stun(Pawn pawn, Pawn caster)
        {
            int ticks = SasukeKit.NetProps.letGoStunTicks;
            pawn.stances?.stunner?.StunFor(ticks, caster, false);
            if (pawn.Spawned) RinneganPictures.Struck(pawn, ticks / 60f);
        }

        /// <summary>From Projectile_Fuma: it has dropped <paramref name="weapon"/> on the ground. A lit one lies burning.</summary>
        public void FumaLanded(Projectile_Fuma fuma, Thing weapon)
        {
            if (byShot.TryGetValue(fuma, out HeldWeapon w)) Remove(w);
            FlyingOn f = TakeFlying(fuma);
            if (f != null && f.charged && weapon != null && weapon.Spawned) RinneganPictures.ChargedFumaLands(weapon);
            if (f == null || !f.burning || weapon == null || !weapon.Spawned) return;
            AddFire(weapon.Map, weapon.Position, f.caster, SasukeKit.FlameProps.groundBurnTicks, false, weapon, weapon.DrawPos, 0f,
                Find.TickManager.TicksGame * AmenoyodomiGraphics.FumaSpin / 60f % 360f, f.litTick);
            weapon.SetForbidden(true, false);
        }

        // ---- Conjured kunai ---------------------------------------------------------------------------------------

        public int ConjuredIn(Thing belt)
        {
            int i = conjuredBelts.IndexOf(belt);
            return i < 0 ? 0 : conjuredCounts[i];
        }

        public void AddConjured(Thing belt)
        {
            int i = conjuredBelts.IndexOf(belt);
            if (i < 0)
            {
                conjuredBelts.Add(belt);
                conjuredCounts.Add(1);
            }
            else conjuredCounts[i]++;
        }

        /// <summary>Spends one conjured kunai from this belt if it has one; the throw then launches a conjured kunai.</summary>
        public bool TakeConjured(Thing belt, int remainingCharges)
        {
            int i = conjuredBelts.IndexOf(belt);
            if (i < 0) return false;
            int count = Mathf.Min(conjuredCounts[i], remainingCharges);
            if (count <= 0)
            {
                conjuredBelts.RemoveAt(i);
                conjuredCounts.RemoveAt(i);
                return false;
            }
            conjuredCounts[i] = count - 1;
            if (count - 1 == 0)
            {
                conjuredBelts.RemoveAt(i);
                conjuredCounts.RemoveAt(i);
            }
            return true;
        }

        /// <summary>
        /// A belt that is no longer on a manifested Sasuke (taken off, revert, death) loses its conjured kunai: they
        /// never become items. Counts are also kept at or under the belt's charges.
        /// </summary>
        private void CheckConjuredBelts()
        {
            for (int i = conjuredBelts.Count - 1; i >= 0; i--)
            {
                Thing belt = conjuredBelts[i];
                CompApparelReloadable comp = belt?.TryGetComp<CompApparelReloadable>();
                if (belt == null || belt.Destroyed || comp == null)
                {
                    conjuredBelts.RemoveAt(i);
                    conjuredCounts.RemoveAt(i);
                    continue;
                }
                Pawn wearer = (belt as Apparel)?.Wearer;
                int count = Mathf.Min(conjuredCounts[i], comp.RemainingCharges);
                if (wearer == null || SasukeKit.Amenoyodomi(wearer) == null)
                {
                    KunaiConjure.RemoveCharges(comp, count);
                    conjuredBelts.RemoveAt(i);
                    conjuredCounts.RemoveAt(i);
                    continue;
                }
                if (count <= 0)
                {
                    conjuredBelts.RemoveAt(i);
                    conjuredCounts.RemoveAt(i);
                }
                else conjuredCounts[i] = count;
            }
        }

        // ---- Raikō Kusari -----------------------------------------------------------------------------------------

        public RaikoNet NetOf(Pawn caster)
        {
            for (int i = 0; i < nets.Count; i++)
                if (nets[i].caster == caster) return nets[i];
            return null;
        }

        /// <summary>Strings the caster's held weapons into a net. Ends one it already has. False if no link can form.</summary>
        public bool StartNet(Pawn caster)
        {
            int now = Find.TickManager.TicksGame;
            RaikoNet old = NetOf(caster);
            if (old != null)
            {
                old.End(now, false);
                nets.Remove(old);
            }
            RaikoNet net = RaikoNet.Form(caster, HeldBy(caster), now);
            if (net == null) return false;
            nets.Add(net);
            RinneganPictures.NetFormed(net);
            return true;
        }

        // ---- Amaterasu --------------------------------------------------------------------------------------------

        public void Register(HediffComp_Amaterasu flame)
        {
            if (!flames.Contains(flame)) flames.Add(flame);
        }

        public void Unregister(HediffComp_Amaterasu flame) => flames.Remove(flame);

        /// <summary>
        /// Black fire on a ground cell for <paramref name="ticks"/>. <paramref name="at"/>, <paramref name="deg"/>,
        /// <paramref name="turn"/> and <paramref name="litTick"/> are for the picture: where the weapon came down (its
        /// drawn point), which way a kunai points, the Fūma's turn, and when it was lit.
        /// </summary>
        public void AddFire(Map map, IntVec3 cell, Pawn caster, int ticks, bool conjuredKunai, Thing weapon,
            Vector3 at, float deg, float turn, int litTick)
        {
            if (map == null || !cell.InBounds(map)) return;
            int now = Find.TickManager.TicksGame;
            fires.Add(new BlackFire
            {
                map = map,
                cell = cell,
                caster = caster,
                startTick = now,
                endTick = now + ticks,
                conjuredKunai = conjuredKunai,
                weapon = weapon,
                at = at,
                deg = deg,
                turn = turn,
                litTick = litTick
            });
        }

        /// <summary>A weapon lying in black fire: it cannot be picked up until the fire is out.</summary>
        public bool Burning(Thing weapon)
        {
            for (int i = 0; i < fires.Count; i++)
                if (fires[i].weapon == weapon) return true;
            return false;
        }

        public bool AnyBurningBy(Pawn caster)
        {
            for (int i = 0; i < flames.Count; i++)
                if (flames[i].caster == caster) return true;
            for (int i = 0; i < fires.Count; i++)
                if (fires[i].caster == caster) return true;
            for (int i = 0; i < held.Count; i++)
                if (held[i].caster == caster && held[i].burning) return true;
            for (int i = 0; i < flying.Count; i++)
                if (flying[i].caster == caster && flying[i].burning) return true;
            return false;
        }

        /// <summary>Release: every black flame this caster lit goes out at once, on pawns, on the ground and on weapons.</summary>
        public void Release(Pawn caster)
        {
            foreach (HediffComp_Amaterasu flame in flames.Where(f => f.caster == caster).ToList())
                flame.PutOut();
            for (int i = fires.Count - 1; i >= 0; i--)
            {
                if (fires[i].caster != caster) continue;
                fires[i].End();
                fires.RemoveAt(i);
            }
            for (int i = 0; i < held.Count; i++)
            {
                if (held[i].caster != caster || !held[i].burning) continue;
                held[i].burning = false;
                AmaterasuPictures.HeldOut(held[i]);
            }
            for (int i = 0; i < flying.Count; i++)
                if (flying[i].caster == caster) flying[i].burning = false;
        }

        // ---- Ticks and drawing ------------------------------------------------------------------------------------

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;

            for (int i = on.Count - 1; i >= 0; i--)
            {
                Ability_Amenoyodomi ability = on[i];
                if (ability.mode != HoldMode.Off && ability.pawn?.abilities?.GetAbility(ability.def) == ability) continue;
                on.RemoveAt(i);
            }
            OnCount = on.Count;

            if (held.Count > 0) TickHeld(now);

            for (int i = nets.Count - 1; i >= 0; i--)
                if (!nets[i].Tick(now)) nets.RemoveAt(i);

            for (int i = fires.Count - 1; i >= 0; i--)
                if (!fires[i].Tick(now)) fires.RemoveAt(i);

            if (now % 60 == 0)
            {
                letGo.RemoveWhere(shot => shot == null || shot.Destroyed);
                flying.RemoveAll(f => f.shot == null || f.shot.Destroyed);
                FlyingCount = flying.Count;
                if (conjuredBelts.Count > 0) CheckConjuredBelts();
            }
        }

        private void TickHeld(int now)
        {
            CompProperties_Amenoyodomi props = SasukeKit.HoldProps;
            for (int i = held.Count - 1; i >= 0; i--)
            {
                if (i >= held.Count) continue;
                HeldWeapon w = held[i];
                if (w.shot == null || w.shot.Destroyed || !w.shot.Spawned)
                {
                    Remove(w);
                    continue;
                }
                Ability_Amenoyodomi ability = SasukeKit.Amenoyodomi(w.caster);
                if (ability == null || ability.mode == HoldMode.Off || !SasukeKit.CanHold(w.caster, w.shot.Map)
                    || now - w.caughtTick >= props.holdTicks)
                {
                    Drop(w);
                    continue;
                }
                float share = props.Share(ability.mode);
                Vector3 next = w.at + w.heading * (w.speed * share);
                IntVec3 cell = next.ToIntVec3();
                if (!cell.InBounds(w.shot.Map) || CompFuma.Blocked(cell, w.shot.Map))
                {
                    Drop(w);
                    continue;
                }
                w.at = next;
                w.crept += w.speed * share;
                w.turned += RinneganPictures.FumaSpinPerTick * share;
                if (cell != w.shot.Position) MoveTo(w, cell);
            }
        }

        public override void GameComponentUpdate()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            RinneganPictures.Draw(map, this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref held, "held", LookMode.Deep);
            Scribe_Collections.Look(ref flying, "flying", LookMode.Deep);
            Scribe_Collections.Look(ref nets, "nets", LookMode.Deep);
            Scribe_Collections.Look(ref fires, "fires", LookMode.Deep);
            Scribe_Collections.Look(ref conjuredBelts, "conjuredBelts", LookMode.Reference);
            Scribe_Collections.Look(ref conjuredCounts, "conjuredCounts", LookMode.Value);
            Scribe_Collections.Look(ref letGo, "letGo", LookMode.Reference);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            letGo ??= new HashSet<Projectile>();
            letGo.RemoveWhere(shot => shot == null);
            held = (held ?? new List<HeldWeapon>()).Where(w => w?.shot != null && w.caster != null).ToList();
            flying = (flying ?? new List<FlyingOn>()).Where(f => f?.shot != null).ToList();
            nets = (nets ?? new List<RaikoNet>()).Where(n => n?.caster != null).ToList();
            fires = (fires ?? new List<BlackFire>()).Where(f => f?.map != null).ToList();
            conjuredBelts ??= new List<Thing>();
            conjuredCounts ??= new List<int>();
            if (conjuredBelts.Count != conjuredCounts.Count)
            {
                conjuredBelts.Clear();
                conjuredCounts.Clear();
            }
            byShot.Clear();
            foreach (HeldWeapon w in held) byShot[w.shot] = w;
            HeldCount = held.Count;
            FlyingCount = flying.Count;
        }

        /// <summary>Tests: forget everything (the arena is cleared, so the things are gone anyway).</summary>
        public void ResetForTests()
        {
            foreach (RaikoNet net in nets) net.End(Find.TickManager.TicksGame, false);
            foreach (HediffComp_Amaterasu flame in flames.ToList()) flame.PutOut();
            // A held projectile left behind would take up its flight again and land in the next test.
            foreach (HeldWeapon w in held)
                if (w.shot != null && !w.shot.Destroyed) w.shot.Destroy();
            foreach (FlyingOn f in flying)
                if (f.shot != null && !f.shot.Destroyed) f.shot.Destroy();
            letGo.Clear();
            held.Clear();
            byShot.Clear();
            flying.Clear();
            nets.Clear();
            fires.Clear();
            conjuredBelts.Clear();
            conjuredCounts.Clear();
            on.Clear();
            HeldCount = OnCount = FlyingCount = 0;
            RinneganPictures.Clear();
        }
    }

    /// <summary>
    /// Amaterasu burning on a ground cell: a lit kunai that missed, a lit weapon that dropped, or the Fūma lying lit
    /// (<see cref="weapon"/>, which cannot be picked up meanwhile). A pawn that stands on the cell catches, carrying the
    /// time that is left. Never spreads to the floor or to buildings.
    /// </summary>
    public class BlackFire : IExposable
    {
        public Map map;
        public IntVec3 cell;
        public Pawn caster;
        public int startTick;
        public int endTick;
        /// <summary>A conjured kunai burns here: no item exists, the picture draws one, and it goes with the fire.</summary>
        public bool conjuredKunai;
        public Thing weapon;
        /// <summary>For the picture: where it came down (drawn point), a kunai's heading, the Fūma's turn, when it was lit.</summary>
        public Vector3 at;
        public float deg;
        public float turn;
        public int litTick = -1;

        /// <summary>One game tick. False once it is out.</summary>
        public bool Tick(int now)
        {
            if (now >= endTick || map == null || Find.Maps.IndexOf(map) < 0)
            {
                End();
                return false;
            }
            if ((now - startTick) % 15 != 0 || !cell.InBounds(map)) return true;
            List<Thing> things = cell.GetThingList(map);
            for (int i = things.Count - 1; i >= 0; i--)
                if (things[i] is Pawn pawn) Amaterasu.Ignite(pawn, caster, endTick - now, AmaterasuCatch.Stepped);
            return true;
        }

        public void End()
        {
            if (weapon != null && weapon.Spawned) weapon.SetForbidden(false, false);
            AmaterasuPictures.FireOut(this);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref endTick, "endTick");
            Scribe_Values.Look(ref conjuredKunai, "conjuredKunai");
            Scribe_References.Look(ref weapon, "weapon");
            Scribe_Values.Look(ref at, "at");
            Scribe_Values.Look(ref deg, "deg");
            Scribe_Values.Look(ref turn, "turn");
            Scribe_Values.Look(ref litTick, "litTick", -1);
        }
    }
}
