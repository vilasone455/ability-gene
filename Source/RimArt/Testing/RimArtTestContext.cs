using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// What a <see cref="RimArtTestAttribute"/> test works with: a cleared arena on the current map,
    /// pawn setup, a trace written to the results file, and checks. A failed check does not stop the
    /// test; the test fails if any check failed, it threw, it timed out, or an error was logged while
    /// it ran.
    /// </summary>
    public class RimArtTestContext
    {
        /// <summary>Yield this to take a screenshot (named by <see cref="shotName"/>) before the next step.</summary>
        public const int Shot = -1;

        public readonly Map map;
        public readonly IntVec3 center;
        public readonly List<string> failures = new List<string>();
        internal string shotName;
        /// <summary>The next screenshot's camera, when <see cref="ShotAs(string, IntVec3, float)"/> set one.</summary>
        internal IntVec3? shotAt;
        internal float shotSize = 10f;
        internal readonly RimArtTestRunner runner;

        /// <summary>Half the side of the square <see cref="Clear"/> clears.</summary>
        public const int ArenaRadius = 12;

        internal RimArtTestContext(RimArtTestRunner runner, Map map)
        {
            this.runner = runner;
            this.map = map;
            center = map.Center;
        }

        public int Now => Find.TickManager.TicksGame;

        public void Log(string line) => runner.Write("    " + line);

        public bool Check(bool ok, string what)
        {
            runner.Write("    " + (ok ? "ok   " : "FAIL ") + what);
            if (!ok) failures.Add(what);
            return ok;
        }

        /// <summary>Names the screenshot the next <c>yield return Shot</c> takes.</summary>
        public int ShotAs(string name)
        {
            shotName = name;
            return Shot;
        }

        /// <summary>
        /// Names the next screenshot and takes it close up: the camera on <paramref name="at"/> at root size
        /// <paramref name="size"/> (the default shot is the arena centre at 10).
        /// </summary>
        public int ShotAs(string name, IntVec3 at, float size)
        {
            shotName = name;
            shotAt = at;
            shotSize = size;
            return Shot;
        }

        /// <summary>
        /// Makes the square around the map centre a flat, lit, unroofed, unfogged floor with nothing on
        /// it, and removes every other pawn on the map so no one walks in or fights.
        /// </summary>
        public void Clear()
        {
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList()) pawn.Destroy();
            CellRect rect = CellRect.CenteredOn(center, ArenaRadius).ClipInsideMap(map);
            foreach (IntVec3 c in rect)
            {
                foreach (Thing thing in c.GetThingList(map).ToList())
                    if (thing.def.destroyable) thing.Destroy();
                map.terrainGrid.SetTerrain(c, TerrainDefOf.Soil);
                map.roofGrid.SetRoof(c, null);
                map.fogGrid.Unfog(c);
            }
        }

        public Pawn Colonist(IntVec3 at)
        {
            var request = new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, mustBeCapableOfViolence: true, forceNoGear: true);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            GenSpawn.Spawn(pawn, at, map);
            pawn.drafter.Drafted = true;
            return Note(pawn);
        }

        /// <summary>A hostile humanlike from an enemy faction (<paramref name="faction"/>, else a random one), told to stand still.</summary>
        public Pawn Enemy(IntVec3 at, bool armed = true, Faction faction = null)
        {
            faction = faction ?? Find.FactionManager.RandomEnemyFaction(allowNonHumanlike: false);
            PawnKindDef kind = faction?.def.basicMemberKind ?? PawnKindDefOf.Villager;
            var request = new PawnGenerationRequest(kind, faction, mustBeCapableOfViolence: true, dontGiveWeapon: !armed);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            GenSpawn.Spawn(pawn, at, map);
            Hold(pawn);
            return Note(pawn);
        }

        /// <summary>A Scyther of the mechanoid faction at <paramref name="at"/>, held still; null, with a log line, when the game has no Mech_Scyther or mechanoid faction.</summary>
        public Pawn Mech(IntVec3 at)
        {
            PawnKindDef scyther = DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Scyther");
            if (scyther == null || Faction.OfMechanoids == null)
            {
                Log("no Mech_Scyther or mechanoid faction: the mech check is skipped");
                return null;
            }
            Pawn mech = PawnGenerator.GeneratePawn(new PawnGenerationRequest(scyther, Faction.OfMechanoids));
            GenSpawn.Spawn(mech, at, map);
            Hold(mech);
            return Note(mech);
        }

        /// <summary>Starts a long Wait job so the pawn stands where it is.</summary>
        public static void Hold(Pawn pawn)
        {
            Job wait = JobMaker.MakeJob(JobDefOf.Wait, 6000);
            pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
        }

        public void Equip(Pawn pawn, ThingDef weapon)
        {
            if (pawn.equipment.Primary != null) pawn.equipment.DestroyEquipment(pawn.equipment.Primary);
            pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(weapon,
                weapon.MadeFromStuff ? GenStuff.DefaultStuffFor(weapon) : null));
        }

        // ---- kit test setup (AGENTS.md: shared here, not copied into each Tests_ file) ----------------------------

        /// <summary>Waits <paramref name="step"/> ticks at a time until <paramref name="done"/>, at most <paramref name="maxTicks"/>.</summary>
        public static IEnumerable<int> WaitFor(Func<bool> done, int maxTicks, int step = 1)
        {
            for (int waited = 0; waited < maxTicks && !done(); waited += step) yield return step;
        }

        /// <summary>
        /// <see cref="Clear"/>, then no Hosts and an empty pool. <paramref name="device"/> stands in for the resonance
        /// device (tests have no power grid): false none works, true one does, null the real search.
        /// </summary>
        public GameComponent_Echoes ClearEchoes(bool? device = false)
        {
            Clear();
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            echoes.ResetForTests();
            EchoDevice.workingForTests = device;
            return echoes;
        }

        /// <summary>A colonist at <paramref name="at"/> made the Host of <paramref name="echo"/>, the pool at 100, in hero form unless <paramref name="manifest"/> is false.</summary>
        public Pawn Host(EchoDef echo, IntVec3 at, out EchoRecord record, bool manifest = true)
        {
            Pawn host = Colonist(at);
            record = EchoUtility.ForceHost(echo, host);
            GameComponent_Echoes.Get.charge = 100f;
            if (manifest) EchoUtility.Manifest(record);
            return host;
        }

        /// <summary>The end of a test with a Host: out of hero form (no collapse), and the device back to the real search.</summary>
        public static void EndHost(EchoRecord record)
        {
            if (record != null) EchoUtility.Revert(record, collapse: false);
            EchoDevice.workingForTests = null;
        }

        /// <summary>
        /// An enemy at <paramref name="at"/> (<see cref="Enemy"/>), unarmed unless <paramref name="armed"/>, stunned for
        /// <paramref name="stunTicks"/>. <paramref name="bare"/>: its apparel destroyed, so armour cannot turn a hit to 0.
        /// </summary>
        public Pawn Target(IntVec3 at, int stunTicks = 0, bool bare = true, bool armed = false, Faction faction = null)
        {
            Pawn pawn = Enemy(at, armed, faction);
            if (bare) pawn.apparel?.DestroyAll();
            if (stunTicks > 0) pawn.stances.stunner.StunFor(stunTicks, null, false);
            return pawn;
        }

        /// <summary>
        /// One melee attack with <paramref name="attacker"/>'s weapon that cannot miss: a surprise attack skips the miss and
        /// dodge rolls. It goes through Pawn_MeleeVerbs, not an AttackMelee job, so Melee Animation does not take it over
        /// with a duel or an execution (those apply their own damage and skip Verb_MeleeAttackDamage hooks).
        /// False, with a failed check, when the weapon has no melee verb.
        /// </summary>
        public bool Strike(Pawn attacker, Pawn target)
        {
            Verb verb = attacker.equipment?.PrimaryEq?.AllVerbs?.FirstOrDefault(v => v.IsMeleeAttack);
            if (!Check(verb != null, attacker.LabelShort + " has a melee verb on the weapon")) return false;
            bool started = attacker.meleeVerbs.TryMeleeAttack(target, verb, true);
            Log(Now + " strike " + target.LabelShort + ": " + started + " | " + Describe(attacker));
            return started;
        }

        /// <summary>Removes Wimp: at 20 % pain it downs a pawn a test means to wound, which then drops its weapon.</summary>
        public static void NoWimp(Pawn pawn)
        {
            Trait wimp = pawn.story?.traits?.GetTrait(TraitDefOf.Wimp);
            if (wimp != null) pawn.story.traits.RemoveTrait(wimp);
        }

        private readonly Dictionary<Pawn, float> startHealth = new Dictionary<Pawn, float>();

        /// <summary>
        /// Notes the pawn's health now for <see cref="Hurt"/>. <see cref="Colonist"/>, <see cref="Enemy"/> and <see cref="Mech"/>
        /// note at spawn, because a generated pawn often carries an old scar or a missing part; a test that wounds a pawn
        /// on purpose notes it again before the check. A pawn never noted counts from full health.
        /// </summary>
        public Pawn Note(Pawn pawn)
        {
            startHealth[pawn] = pawn.health.summaryHealth.SummaryHealthPercent;
            return pawn;
        }

        /// <summary>Dead, downed, or below the health noted for it.</summary>
        public bool Hurt(Pawn pawn) => pawn.Downed || Struck(pawn);

        /// <summary>
        /// Dead or below the health noted for it. Unlike <see cref="Hurt"/>, a pawn put down by <see cref="Down"/> counts only
        /// once something hits it again; its wounds heal a little every 600 ticks, so health above the note is not a hit.
        /// </summary>
        public bool Struck(Pawn pawn) =>
            pawn.Dead || pawn.health.summaryHealth.SummaryHealthPercent < (startHealth.TryGetValue(pawn, out float h) ? h : 1f) - 0.001f;

        /// <summary>Downs <paramref name="pawn"/> with no bleeding wounds (so it stays down and alive) and notes its health then, for <see cref="Struck"/>.</summary>
        public Pawn Down(Pawn pawn)
        {
            HealthUtility.DamageUntilDowned(pawn, allowBleedingWounds: false);
            return Note(pawn);
        }

        public bool Untouched(Pawn pawn) => !Hurt(pawn);

        public static bool Stunned(Pawn pawn) => pawn.stances?.stunner?.Stunned == true;

        /// <summary>
        /// Not stunned and not in a warmup or melee cooldown stance. A queued ability job ends at once while its caster is
        /// in a melee Stance_Cooldown, and a drafted pawn punches an adjacent hostile by itself: wait for this before a
        /// scripted cast.
        /// </summary>
        public static bool Free(Pawn pawn) => !Stunned(pawn) && !(pawn.stances?.curStance is Stance_Busy);

        /// <summary>A wall at <paramref name="at"/> made of <paramref name="stuff"/> (granite blocks if null), the player's unless <paramref name="owned"/> is false.</summary>
        public Thing Wall(IntVec3 at, ThingDef stuff = null, bool owned = true)
        {
            Thing wall = ThingMaker.MakeThing(ThingDefOf.Wall, stuff ?? ThingDefOf.BlocksGranite);
            if (owned) wall.SetFaction(Faction.OfPlayer);
            return GenSpawn.Spawn(wall, at, map);
        }

        /// <summary>
        /// A closed room: <see cref="Wall"/>s on every cell round the floor from <paramref name="min"/> to
        /// <paramref name="max"/> (both corners inside), and an unowned wooden door instead of the wall at
        /// <paramref name="door"/> when given. Unroofed, so it is a room of its own that does not touch the map edge; Core
        /// counts one with 300 or more unroofed cells as psychologically outdoors.
        /// </summary>
        public void Room(IntVec3 min, IntVec3 max, ThingDef stuff = null, IntVec3? door = null)
        {
            for (int x = min.x - 1; x <= max.x + 1; x++)
                for (int z = min.z - 1; z <= max.z + 1; z++)
                {
                    if (x >= min.x && x <= max.x && z >= min.z && z <= max.z) continue;
                    var at = new IntVec3(x, 0, z);
                    if (at == door) GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Door, ThingDefOf.WoodLog), at, map);
                    else Wall(at, stuff);
                }
        }

        /// <summary>Undrafted and standing facing <paramref name="rot"/> for 10 s: a drafted idle pawn turns to face south every tick.</summary>
        public static void Face(Pawn pawn, Rot4 rot)
        {
            if (pawn.drafter != null) pawn.drafter.Drafted = false;
            Job wait = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture, pawn.Position + rot.FacingCell * 3);
            wait.expiryInterval = 600;
            pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
            pawn.Rotation = rot;
        }

        /// <summary>One line on a pawn: where it is, its job and toil, stance, stun, and whether it is inside a flyer.</summary>
        public static string Describe(Pawn pawn)
        {
            if (pawn == null) return "null";
            var sb = new StringBuilder();
            sb.Append(pawn.LabelShort);
            if (pawn.Dead) return sb.Append(" dead").ToString();
            if (pawn.Spawned) sb.Append(" at ").Append(pawn.Position);
            else if (pawn.ParentHolder is PawnFlyer flyer) sb.Append(" in flyer at ").Append(flyer.Position);
            else sb.Append(" not spawned (holder ").Append(pawn.ParentHolder?.GetType().Name ?? "none").Append(")");
            if (pawn.Downed) sb.Append(" DOWNED");
            Job job = pawn.CurJob;
            sb.Append(" job=").Append(job == null ? "none" : job.def.defName);
            if (pawn.jobs?.curDriver != null) sb.Append("#").Append(pawn.jobs.curDriver.CurToilIndex);
            Stance stance = pawn.stances?.curStance;
            if (stance != null && !(stance is Stance_Mobile)) sb.Append(" stance=").Append(stance.GetType().Name);
            if (pawn.stances?.stunner != null && pawn.stances.stunner.Stunned) sb.Append(" stunned=").Append(pawn.stances.stunner.StunTicksLeft);
            if (pawn.pather != null && pawn.pather.Moving) sb.Append(" moving");
            if (pawn.jobs != null && pawn.jobs.jobQueue.Count > 0) sb.Append(" queue=").Append(pawn.jobs.jobQueue.Count);
            return sb.ToString();
        }
    }
}
