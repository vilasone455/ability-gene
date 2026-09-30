using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using T = RimArt.GokuKamehamehaTiming;

namespace RimArt
{
    /// <summary>The beam's lane on the real map: where it stops and which cells it covers.</summary>
    public static class KamehamehaLane
    {
        public static Vector2 Toward(IntVec3 from, IntVec3 to)
        {
            var run = new Vector2(to.x - from.x, to.z - from.z);
            return run.sqrMagnitude < 0.01f ? Vector2.right : run.normalized;
        }

        /// <summary>
        /// Where the beam ends, in cells from <paramref name="from"/>: its length, or half a cell short of
        /// the first filled cell (a wall, a rock) on the line, which is <paramref name="wallAt"/>.
        /// </summary>
        public static float Stop(IntVec3 from, Vector2 toward, float length, Map map, out bool walled, out float wallAt)
        {
            walled = false;
            wallAt = 0f;
            var end = (from.ToVector3Shifted() + new Vector3(toward.x, 0f, toward.y) * length).ToIntVec3();
            foreach (IntVec3 c in GenSight.PointsOnLineOfSight(from, end))
            {
                if (c == from) continue;
                if (!c.InBounds(map) || c.Filled(map))
                {
                    walled = true;
                    wallAt = (c - from).LengthHorizontal;
                    return Mathf.Max(0.5f, wallAt - 0.5f);
                }
            }
            if (end.InBounds(map) && end.Filled(map))
            {
                walled = true;
                wallAt = (end - from).LengthHorizontal;
                return Mathf.Max(0.5f, wallAt - 0.5f);
            }
            return length;
        }

        public static IntVec3 EndCell(IntVec3 from, Vector2 toward, float stop) =>
            (from.ToVector3Shifted() + new Vector3(toward.x, 0f, toward.y) * stop).ToIntVec3();

        /// <summary>A cell is in the lane when its centre is between half a cell and the stop along the aim and within half the width across it.</summary>
        public static bool Contains(IntVec3 from, Vector2 toward, IntVec3 cell, float stop, float width, out float along, out float across)
        {
            float dx = cell.x - from.x, dz = cell.z - from.z;
            along = dx * toward.x + dz * toward.y;
            across = -dx * toward.y + dz * toward.x;
            return along >= 0.5f && along <= stop && Mathf.Abs(across) <= width / 2f;
        }

        /// <summary>The lane's cells the firing cell can see (a wall inside the lane is hit; the room behind it is not).</summary>
        public static List<IntVec3> Cells(IntVec3 from, Vector2 toward, float stop, float width, Map map)
        {
            var cells = new List<IntVec3>();
            int reach = Mathf.CeilToInt(stop + width);
            foreach (IntVec3 c in GenRadial.RadialCellsAround(from, reach, false))
                if (c.InBounds(map) && Contains(from, toward, c, stop, width, out _, out _) && GenSight.LineOfSight(from, c, map, true))
                    cells.Add(c);
            return cells;
        }
    }

    /// <summary>
    /// One Kamehameha, from the channel to the scorched lane. The rules (docs/heroes.md; the numbers
    /// are <see cref="CompProperties_Kamehameha"/> on the ability def):
    ///
    /// - The caster stands and channels channelSeconds, then holds the full charge with no time limit
    ///   until the player presses Fire or Warp. A stun, a downing or death cancels it and the charge
    ///   and cooldown stay spent; Cancel, a move order or a revert gives them back.
    /// - Fire: the beam, width cells wide, runs length cells from the caster or to the first wall. It
    ///   stands beamSeconds and hits every pawn and building in the lane hits times, timed as the head
    ///   of the beam reaches each one; the first hit carries a pawn pushCells along the lane. Where it
    ///   ends it explodes (blastRadius, blastDamage) when the beam lets go.
    /// - Warp: while Instant Transmission is ready and the pool can pay its cost, Instant
    ///   Transmission's charge and cooldown are taken and the caster holds the ball for the lock's
    ///   channel onto the chosen cell (<see cref="GokuTransmissionLock"/>, measured from the channel
    ///   cell). Then it vanishes, appears on the cell 0.12 s later and fires along the chosen
    ///   direction 0.3 s after the vanish began. Cancel before the jump gives Instant Transmission's
    ///   charge and cooldown back too; a stun or a downing spends them.
    /// </summary>
    public sealed class KamehamehaCast : GokuCast
    {
        /// <summary>The cell the channel began on (the picture's Home), the firing cell (the channel cell until a warp), and the aim.</summary>
        public IntVec3 channelCell, from, aim;
        public bool fireOrdered;
        /// <summary>When the warp's vanish starts: the Warp press plus the lock's channel. Before it the caster still holds the ball.</summary>
        public int warpTick = -1;
        public IntVec3 warpTo, warpAim;
        /// <summary>Instant Transmission's charge the Warp took; given back on a Cancel before the jump.</summary>
        public float warpPaid;
        public bool warp, jumped;
        public int fireTick = -1;
        /// <summary>The caster is free: the beam has let go.</summary>
        public bool released;
        private bool blastDone;

        private Vector2 toward;
        private KamehamehaPlan plan;
        private float stop, wallAt;
        private bool walled;
        private int laneTick = -1;
        private readonly List<Pawn> victims = new List<Pawn>();
        private float[] struckAt = new float[0];
        private int[] hitsDealt = new int[0];
        private Vector2[] struck = new Vector2[0];
        private readonly List<Thing> buildings = new List<Thing>();
        private float[] buildingAt = new float[0];
        private int[] buildingHits = new int[0];
        private List<GokuShake> shakes = new List<GokuShake>();
        private int shaken;

        private static CompProperties_Kamehameha P => GokuBusy.Props<CompProperties_Kamehameha>(GokuDefOf.AG_GokuKamehameha);
        private static int VanishTicks => Mathf.RoundToInt(T.Vanish * 60f);
        private static int WarpTicks => Mathf.RoundToInt((T.Vanish * 2f + T.Gap) * 60f);

        public bool Channelling => !broken && channelTick >= 0 && warpTick < 0 && fireTick < 0;
        public bool Warping => !broken && warpTick >= 0 && fireTick < 0;
        /// <summary>Warp was pressed and the caster is still locking onto the cell, holding the ball.</summary>
        public bool Locking(int now) => Warping && now < warpTick;
        public bool Firing => !broken && fireTick >= 0;
        public override bool Busy => !broken && !released;
        public override string BusyReason => Firing ? "Firing." : "Channelling a Kamehameha.";
        public override IntVec3 FacingCell => aim;
        public override IntVec3 ChannelCell => from;
        protected override AbilityDef Def => GokuDefOf.AG_GokuKamehameha;
        protected override string Name => "Kamehameha";
        protected override float Lead => T.Lead;

        public bool FullCharge(int now) => channelTick >= 0 && Channelled(now) >= P.channelSeconds;
        public float ChargeLeft(int now) => Mathf.Max(0f, P.channelSeconds - Channelled(now));

        public KamehamehaCast() { }

        public KamehamehaCast(Pawn caster, IntVec3 aim, int now, float paid)
        {
            this.caster = caster;
            this.aim = aim;
            this.paid = paid;
            home = caster.Map;
            channelCell = from = caster.Position;
            queuedTick = now;
            toward = KamehamehaLane.Toward(from, aim);
        }

        public override void StartChannel(int now)
        {
            base.StartChannel(now);
            channelCell = from = caster.Position;
            toward = KamehamehaLane.Toward(from, aim);
            Lane(now);
        }

        /// <summary>Where the beam would stop now, refreshed while it channels so a door opening shows in the lane.</summary>
        private void Lane(int now)
        {
            laneTick = now;
            stop = KamehamehaLane.Stop(from, toward, P.length, home, out walled, out wallAt);
        }

        // ---- the warp ---------------------------------------------------------------------------------------------

        /// <summary>A cell the caster can appear on for the warp.</summary>
        public bool ValidWarpCell(IntVec3 cell, out string reason)
        {
            reason = null;
            if (home == null || !cell.InBounds(home)) { reason = "Not on the map."; return false; }
            if (cell.Fogged(home)) { reason = "Goku cannot feel for a place he has not seen."; return false; }
            if (!cell.Standable(home) || cell.Impassable(home)) { reason = "Cannot stand there."; return false; }
            Pawn there = cell.GetFirstPawn(home);
            if (there != null && there != caster) { reason = "Someone is standing there."; return false; }
            return true;
        }

        /// <summary>Why the Warp button is disabled, or null when it can be pressed.</summary>
        public string WarpDisabled(int now)
        {
            if (!Channelling) return "Not channelling.";
            if (!FullCharge(now)) return "Not at full charge.";
            Ability it = caster?.abilities?.GetAbility(GokuDefOf.AG_GokuInstantTransmission);
            if (it == null) return "Instant Transmission is not available.";
            if (it.CooldownTicksRemaining > 0) return "Instant Transmission is on cooldown (" + (it.CooldownTicksRemaining / 60f).ToString("0") + " s).";
            float cost = WarpCost;
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            if (echoes != null && echoes.charge < cost) return "Needs " + cost.ToString("0") + " charge for Instant Transmission; the colony has " + echoes.charge.ToString("0") + ".";
            return null;
        }

        public float WarpCost => EchoUtility.ManifestedWith(caster, Def)?.def.CastCost(GokuDefOf.AG_GokuInstantTransmission) ?? 0f;

        /// <summary>The Warp button: pays Instant Transmission's charge and cooldown, then vanishes and fires from <paramref name="cell"/> along <paramref name="aimCell"/>.</summary>
        public bool Warp(IntVec3 cell, IntVec3 aimCell)
        {
            int now = Find.TickManager.TicksGame;
            string why = WarpDisabled(now);
            if (why == null && !ValidWarpCell(cell, out why)) { }
            if (why == null && aimCell == cell) why = "Aim away from the landing cell.";
            if (why != null)
            {
                Messages.Message(why, caster, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            if (echoes != null && !echoes.TrySpend(WarpCost))
            {
                Messages.Message("Not enough charge for Instant Transmission.", caster, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            warpPaid = echoes != null ? WarpCost : 0f;
            Ability it = caster.abilities.GetAbility(GokuDefOf.AG_GokuInstantTransmission);
            it?.StartCooldown(it.def.cooldownTicksRange.RandomInRange);
            warp = true;
            float lockSeconds = GokuTransmissionLock.Seconds(caster, from, cell, null, out _, out _);
            warpTick = now + Mathf.RoundToInt(lockSeconds * 60f);
            warpTo = cell;
            warpAim = aimCell;
            // The plan's Go is warpTick: the vanish starts then and the beam fires WarpTicks later.
            plan = T.Plan(true, P.channelSeconds, P.beamSeconds, Seconds(warpTick) + T.Vanish * 2f + T.Gap - T.Lead - P.channelSeconds);
            return true;
        }

        protected override void RefundMore()
        {
            if (!warp || jumped) return;
            GameComponent_Echoes.Get?.Refund(warpPaid);
            warpPaid = 0f;
            Ability it = caster?.abilities?.GetAbility(GokuDefOf.AG_GokuInstantTransmission);
            if (it != null) it.ResetCooldown();
            // Reverted: the Echo took the ability and kept its cooldown for the next manifest.
            else if (caster != null) GameComponent_Echoes.Get?.HostRecord(caster)?.grant.Forget(GokuDefOf.AG_GokuInstantTransmission);
        }

        private void Jump()
        {
            jumped = true;
            if (caster == null || !caster.Spawned) return;
            if (!ValidWarpCell(warpTo, out _)) warpTo = CellFinder.StandableCellNear(warpTo, home, 5f);
            if (!warpTo.IsValid) return;
            caster.Position = warpTo;
            // The channel job goes on: it holds the caster on the new cell until the beam lets go.
            caster.Notify_Teleported(false, true);
            from = warpTo;
            aim = warpAim;
            toward = KamehamehaLane.Toward(from, aim);
            caster.Rotation = Rot4.FromAngleFlat((aim - from).ToVector3().AngleFlat());
            Lane(Find.TickManager.TicksGame);
        }

        // ---- the beam ---------------------------------------------------------------------------------------------

        private void Fire(int now)
        {
            fireTick = now;
            if (!warp) plan = T.Plan(false, P.channelSeconds, P.beamSeconds, Mathf.Max(0f, Seconds(now) - T.Lead - P.channelSeconds));
            Lane(now);
            victims.Clear();
            buildings.Clear();
            var alongs = new List<float>();
            var buildingAlongs = new List<float>();
            foreach (IntVec3 c in KamehamehaLane.Cells(from, toward, stop, P.width, home))
            {
                KamehamehaLane.Contains(from, toward, c, stop, P.width, out float along, out _);
                List<Thing> things = c.GetThingList(home);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (thing is Pawn pawn)
                    {
                        if (pawn == caster || pawn.Dead || victims.Contains(pawn)) continue;
                        victims.Add(pawn);
                        alongs.Add(along);
                    }
                    else if (thing.def.category == ThingCategory.Building && thing.def.useHitPoints && !buildings.Contains(thing))
                    {
                        buildings.Add(thing);
                        buildingAlongs.Add(along);
                    }
                }
            }
            struckAt = new float[victims.Count];
            hitsDealt = new int[victims.Count];
            struck = new Vector2[victims.Count];
            for (int i = 0; i < victims.Count; i++)
            {
                struckAt[i] = T.Passes(plan, alongs[i], P.length);
                struck[i] = Vec(victims[i].DrawPos);
            }
            buildingAt = new float[buildings.Count];
            buildingHits = new int[buildings.Count];
            for (int i = 0; i < buildings.Count; i++) buildingAt[i] = T.Passes(plan, buildingAlongs[i], P.length);
            shakes = GokuTiming.Sorted(T.Shakes(plan));
            shaken = 0;
        }

        private float HitAngle => new Vector3(toward.x, 0f, toward.y).AngleFlat();

        private bool Hittable(Pawn pawn) => pawn != null && pawn.Spawned && pawn.Map == home && !pawn.Dead;

        private void TickBeam(float s)
        {
            float between = P.beamSeconds / Mathf.Max(1, P.hits);
            for (int v = 0; v < victims.Count; v++)
            {
                Pawn victim = victims[v];
                while (hitsDealt[v] < P.hits && s >= struckAt[v] + hitsDealt[v] * between)
                {
                    if (victim.Dead) { hitsDealt[v] = P.hits; break; }
                    // Inside the push flyer: the hit is owed and lands when it is back on the map.
                    if (!Hittable(victim)) break;
                    // A hit not landed by the blast is dropped.
                    if (s >= plan.Blast) { hitsDealt[v] = P.hits; break; }
                    bool first = hitsDealt[v] == 0;
                    hitsDealt[v]++;
                    victim.TakeDamage(new DamageInfo(P.DamageDef, P.damagePerHit, P.armorPenetration, HitAngle, caster));
                    if (first && Hittable(victim) && !victim.Dead) Push(victim);
                }
            }
            for (int b = 0; b < buildings.Count; b++)
            {
                Thing building = buildings[b];
                while (buildingHits[b] < P.hits && s >= buildingAt[b] + buildingHits[b] * between)
                {
                    buildingHits[b]++;
                    if (building.Destroyed || !building.Spawned) continue;
                    building.TakeDamage(new DamageInfo(P.DamageDef, P.damagePerHit, P.armorPenetration, HitAngle, caster));
                }
            }
            if (!blastDone && s >= plan.Blast)
            {
                blastDone = true;
                IntVec3 end = KamehamehaLane.EndCell(from, toward, stop);
                if (end.InBounds(home))
                    GenExplosion.DoExplosion(end, home, P.blastRadius, P.BlastDamageDef, caster, P.blastDamage, doVisualEffects: false);
            }
        }

        private void Push(Pawn victim)
        {
            IntVec3 dest = PowerPoleCombat.PushDestination(victim.Position, toward, P.pushCells, home, out _);
            if (dest == victim.Position || !dest.Standable(home)) return;
            PawnFlyer flyer = PawnFlyer.MakeFlyer(GokuDefOf.AG_GokuPushed, victim, dest, null, null);
            if (flyer != null) GenSpawn.Spawn(flyer, dest, home);
        }

        // ---- the clock --------------------------------------------------------------------------------------------

        public override bool Tick(int now)
        {
            if (broken) return false;
            if (Queued)
            {
                if (now - queuedTick > QueueTimeout) Cancel(false);
                return !broken;
            }
            if (Channelling || Warping)
            {
                if (!Held())
                {
                    Cancel(BrokenBySelf());
                    return false;
                }
            }
            if (Channelling)
            {
                if (now - laneTick >= 15) Lane(now);
                if (fireOrdered && FullCharge(now)) Fire(now);
                return true;
            }
            if (Warping)
            {
                int age = now - warpTick;
                if (!jumped && age >= VanishTicks) Jump();
                if (age >= WarpTicks) Fire(now);
                return true;
            }
            float s = Seconds(now);
            while (shaken < shakes.Count && s >= shakes[shaken].At)
            {
                if (Find.CurrentMap == home) Find.CameraDriver.shaker.DoShake(shakes[shaken].Size);
                shaken++;
            }
            TickBeam(s);
            if (!released && s >= plan.Release) released = true;
            return s < plan.End;
        }

        // ---- the picture ------------------------------------------------------------------------------------------

        public override void Draw()
        {
            if (broken || channelTick < 0 || home == null) return;
            float s = T.Lead + UbwClock.Since(channelTick);
            KamehamehaPlan t = Firing || Warping ? plan : T.Plan(false, P.channelSeconds, P.beamSeconds, GokuSpiritBombTiming.Never);
            for (int i = 0; i < victims.Count; i++)
            {
                Pawn v = victims[i];
                if (v.Spawned && v.Map == home) struck[i] = Vec(v.DrawPos);
                else if (v.ParentHolder is PawnFlyer flyer && flyer.Spawned) struck[i] = Vec(flyer.DrawPos);
            }
            GokuKamehamehaGraphics.Draw(new KamehamehaShot
            {
                From = Vec(from), Toward = toward, Home = Vec(channelCell), Seconds = s, Plan = t,
                Length = P.length, Width = P.width, Blast = P.blastRadius, BallSize = T.ScriptBallSize,
                Stop = stop, Walled = walled, WallAt = wallAt,
                Struck = struck, StruckAt = struckAt, StruckCount = Firing ? victims.Count : 0,
            }, home);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref channelCell, "channelCell");
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref aim, "aim");
            Scribe_Values.Look(ref fireOrdered, "fireOrdered");
            Scribe_Values.Look(ref warpTick, "warpTick", -1);
            Scribe_Values.Look(ref warpTo, "warpTo");
            Scribe_Values.Look(ref warpAim, "warpAim");
            Scribe_Values.Look(ref warpPaid, "warpPaid");
            Scribe_Values.Look(ref warp, "warp");
            Scribe_Values.Look(ref jumped, "jumped");
            Scribe_Values.Look(ref fireTick, "fireTick", -1);
            Scribe_Values.Look(ref released, "released");
            Scribe_Values.Look(ref blastDone, "blastDone");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && caster != null)
            {
                toward = KamehamehaLane.Toward(from, aim);
                if (home != null && channelTick >= 0) Lane(Find.TickManager.TicksGame);
                if (warpTick >= 0)
                    plan = T.Plan(true, P.channelSeconds, P.beamSeconds, Seconds(warpTick) + T.Vanish * 2f + T.Gap - T.Lead - P.channelSeconds);
                // A beam mid-flight is not restored: the hits it still owed are dropped, the picture goes on.
                if (fireTick >= 0 && !warp) plan = T.Plan(false, P.channelSeconds, P.beamSeconds, Mathf.Max(0f, Seconds(fireTick) - T.Lead - P.channelSeconds));
                if (fireTick >= 0) blastDone = true;
            }
        }
    }
}
