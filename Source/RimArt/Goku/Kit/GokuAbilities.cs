using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>What the four abilities share: none can be ordered while a cast holds the caster.</summary>
    public static class GokuBusy
    {
        /// <summary>An ability def's comp properties of one kind (AbilityDef has no GetCompProperties).</summary>
        public static T Props<T>(AbilityDef def) where T : AbilityCompProperties
        {
            if (def?.comps == null) return null;
            for (int i = 0; i < def.comps.Count; i++)
                if (def.comps[i] is T props) return props;
            return null;
        }

        public static bool Disabled(Pawn pawn, out string reason)
        {
            reason = GameComponent_Goku.Instance?.For(pawn)?.BusyReason;
            return reason != null;
        }
    }

    // ---- Solar Flare ----------------------------------------------------------------------------------------------

    public class CompProperties_SolarFlare : CompProperties_AbilityEffect
    {
        public float radius = 6.9f;
        public float stunSeconds = 3.5f;
        public float blindSeconds = 20f;

        public CompProperties_SolarFlare()
        {
            compClass = typeof(CompAbilityEffect_SolarFlare);
        }
    }

    /// <summary>
    /// Solar Flare. On the fire tick every pawn within the radius that the caster's cell can see,
    /// except the caster and its own faction, is stunned and flash-blinded. The picture is started by
    /// the cast job's warmup (<see cref="JobDriver_CastGoku"/>) and told here who was hit.
    /// </summary>
    public class CompAbilityEffect_SolarFlare : CompAbilityEffect
    {
        public new CompProperties_SolarFlare Props => (CompProperties_SolarFlare)props;

        private static readonly List<Pawn> hit = new List<Pawn>(), others = new List<Pawn>();

        public override bool GizmoDisabled(out string reason) => GokuBusy.Disabled(parent.pawn, out reason) || base.GizmoDisabled(out reason);

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null) return;
            Gather(caster, map, Props.radius, hit, others);
            int stun = Mathf.RoundToInt(Props.stunSeconds * 60f);
            for (int i = 0; i < hit.Count; i++)
            {
                Pawn pawn = hit[i];
                if (pawn.Dead) continue;
                if (stun > 0) pawn.stances?.stunner?.StunFor(stun, caster, false, true);
                Blind(pawn, Props.blindSeconds);
            }
            GameComponent_Goku goku = GameComponent_Goku.Instance;
            if (goku != null)
            {
                SolarFlarePicture picture = goku.PictureFor<SolarFlarePicture>(caster);
                if (picture == null)
                {
                    // Fired without our cast job (a test, a direct Activate): the flash lands now.
                    picture = new SolarFlarePicture(caster, Find.TickManager.TicksGame - Mathf.RoundToInt(parent.def.verbProperties.warmupTime * 60f),
                        parent.def.verbProperties.warmupTime, Props.stunSeconds, Props.radius);
                    goku.Begin(picture);
                }
                picture.Fire(hit, others);
            }
            hit.Clear();
            others.Clear();
        }

        /// <summary>
        /// Who sees the flash: pawns in the radius on cells the caster's cell can see. Gathered before
        /// anyone is stunned, as FrostBurst does, because stunning writes to the cell lists being walked.
        /// </summary>
        public static void Gather(Pawn caster, Map map, float radius, List<Pawn> blinded, List<Pawn> shadowed)
        {
            blinded.Clear();
            shadowed.Clear();
            IntVec3 centre = caster.Position;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(centre, radius, true))
            {
                if (!cell.InBounds(map)) continue;
                if (cell != centre && !GenSight.LineOfSight(centre, cell, map, true)) continue;
                List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
                for (int i = 0; i < things.Count; i++)
                {
                    if (!(things[i] is Pawn pawn) || pawn == caster || pawn.Dead || blinded.Contains(pawn) || shadowed.Contains(pawn)) continue;
                    if (pawn.Faction != null && pawn.Faction == caster.Faction) shadowed.Add(pawn);
                    else blinded.Add(pawn);
                }
            }
        }

        /// <summary>Adds flash-blinded for <paramref name="seconds"/>, or sets an existing one back to that.</summary>
        public static void Blind(Pawn pawn, float seconds)
        {
            if (pawn?.health == null || pawn.Dead || seconds <= 0f) return;
            Hediff blind = pawn.health.hediffSet.GetFirstHediffOfDef(GokuDefOf.AG_GokuFlashBlind);
            if (blind == null)
            {
                blind = HediffMaker.MakeHediff(GokuDefOf.AG_GokuFlashBlind, pawn);
                pawn.health.AddHediff(blind);
            }
            blind.TryGetComp<HediffComp_Disappears>()?.SetDuration(Mathf.RoundToInt(seconds * 60f));
        }
    }

    // ---- Instant Transmission -------------------------------------------------------------------------------------

    public class CompProperties_InstantTransmission : CompProperties_EffectWithDest
    {
        public float hostileStunSeconds = 1.5f;

        public CompProperties_InstantTransmission()
        {
            compClass = typeof(CompAbilityEffect_InstantTransmission);
        }
    }

    /// <summary>
    /// Instant Transmission. The first click is the passenger (a pawn next to the caster) or the caster
    /// itself for a jump alone; the second is the destination, anywhere on the map (the vanilla
    /// destination comp, range 0 and no line of sight). On the fire tick the caster is put on the
    /// destination and the passenger on the cell beside it on the same side as before; a hostile
    /// passenger arrives stunned.
    /// </summary>
    public class CompAbilityEffect_InstantTransmission : CompAbilityEffect_WithDest
    {
        public new CompProperties_InstantTransmission Props => (CompProperties_InstantTransmission)props;

        private Pawn Passenger => selectedTarget.Pawn == parent.pawn ? null : selectedTarget.Pawn;

        public override TargetingParameters targetParams => new TargetingParameters
        {
            canTargetLocations = true, canTargetPawns = false, canTargetBuildings = false, canTargetItems = false,
            validator = t => ValidDestination(t.Cell, Passenger, out _),
        };

        public override bool GizmoDisabled(out string reason) => GokuBusy.Disabled(parent.pawn, out reason) || base.GizmoDisabled(out reason);

        /// <summary>The first click: the caster itself, or a live pawn on a cell next to it.</summary>
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            Pawn pawn = target.Pawn;
            string why = null;
            if (pawn == null || !pawn.Spawned || pawn.Dead) why = "Pick a pawn next to Goku to take along, or Goku himself to go alone.";
            else if (pawn != caster && !pawn.Position.AdjacentTo8WayOrInside(caster.Position)) why = pawn.LabelShortCap + " is not next to Goku.";
            if (why != null)
            {
                if (throwMessages) Messages.Message(why, caster, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override bool CanHitTarget(LocalTargetInfo target) => ValidDestination(target.Cell, Passenger, out _);

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (ValidDestination(target.Cell, Passenger, out string why)) return true;
            if (showMessages && why != null) Messages.Message(why, parent.pawn, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        /// <summary>A cell the caster can land on: on the map, seen, standable, empty; with a passenger, one it can stand beside.</summary>
        public bool ValidDestination(IntVec3 cell, Pawn passenger, out string reason)
        {
            reason = null;
            Map map = parent.pawn?.Map;
            if (map == null || !cell.InBounds(map)) { reason = "Not on the map."; return false; }
            if (cell.Fogged(map)) { reason = "Goku cannot feel for a place he has not seen."; return false; }
            if (!cell.Standable(map) || cell.Impassable(map)) { reason = "Cannot stand there."; return false; }
            Pawn there = cell.GetFirstPawn(map);
            if (there != null && there != parent.pawn && there != passenger) { reason = "Someone is standing there."; return false; }
            if (passenger != null)
            {
                Building_Door door = cell.GetDoor(map);
                if (door != null && !door.CanPhysicallyPass(passenger)) { reason = passenger.LabelShortCap + " cannot pass that door."; return false; }
                IntVec3 want = cell + (passenger.Position - parent.pawn.Position);
                if (!Beside(map, cell, want, passenger).IsValid) { reason = "No room for " + passenger.LabelShortCap + " beside that cell."; return false; }
            }
            return true;
        }

        /// <summary>The passenger's landing cell: <paramref name="want"/>, or the nearest free cell round it that is not the caster's.</summary>
        private static IntVec3 Beside(Map map, IntVec3 casterCell, IntVec3 want, Pawn passenger)
        {
            int cells = GenRadial.NumCellsInRadius(3f);
            for (int i = 0; i < cells; i++)
            {
                IntVec3 c = want + GenRadial.RadialPattern[i];
                if (c == casterCell || !c.InBounds(map) || !c.Standable(map)) continue;
                Pawn there = c.GetFirstPawn(map);
                if (there != null && there != passenger) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null || !dest.IsValid) return;
            Pawn passenger = target.Pawn == caster ? null : target.Pawn;
            if (passenger != null && (!passenger.Spawned || passenger.Map != map || passenger.Dead || !passenger.Position.AdjacentTo8WayOrInside(caster.Position)))
                passenger = null;
            IntVec3 home = caster.Position, to = dest.Cell;
            if (!ValidDestination(to, passenger, out _))
            {
                to = CellFinder.StandableCellNear(to, map, 5f);
                if (!to.IsValid) return;
            }
            IntVec3 want = passenger != null ? to + (passenger.Position - home) : IntVec3.Invalid;

            caster.Position = to;
            // The cast job goes on: it ends through its own toil on this tick, and Melee Animation would drop a clip otherwise.
            caster.Notify_Teleported(false, true);
            if (passenger != null)
            {
                IntVec3 beside = Beside(map, to, want, passenger);
                if (!beside.IsValid) beside = CellFinder.StandableCellNear(to, map, 5f);
                if (beside.IsValid)
                {
                    passenger.Position = beside;
                    passenger.Notify_Teleported(true, true);
                    if (passenger.HostileTo(caster) && !passenger.Downed)
                        passenger.stances?.stunner?.StunFor(Mathf.RoundToInt(Props.hostileStunSeconds * 60f), caster, false, true);
                }
                else passenger = null;
            }

            GameComponent_Goku goku = GameComponent_Goku.Instance;
            if (goku != null)
            {
                TransmissionPicture picture = goku.PictureFor<TransmissionPicture>(caster);
                if (picture == null)
                {
                    picture = new TransmissionPicture(caster, Find.TickManager.TicksGame - Mathf.RoundToInt(parent.def.verbProperties.warmupTime * 60f),
                        parent.def.verbProperties.warmupTime, to, passenger);
                    picture.home = map;
                    goku.Begin(picture);
                }
                picture.Jumped(home, to, passenger);
            }
        }
    }

    // ---- Kamehameha -----------------------------------------------------------------------------------------------

    public class CompProperties_Kamehameha : CompProperties_AbilityEffect
    {
        public float channelSeconds = 2.5f;
        /// <summary>How long the beam stands once fired.</summary>
        public float beamSeconds = 1.2f;
        public float length = 30f;
        public float width = 3f;
        public int hits = 4;
        public float damagePerHit = 20f;
        public DamageDef damageDef;
        public float armorPenetration = 0.5f;
        public float pushCells = 1.5f;
        public float blastRadius = 2.5f;
        public int blastDamage = 40;
        public DamageDef blastDamageDef;

        public DamageDef DamageDef => damageDef ?? DamageDefOf.Burn;
        public DamageDef BlastDamageDef => blastDamageDef ?? DamageDefOf.Bomb;

        public CompProperties_Kamehameha()
        {
            compClass = typeof(CompAbilityEffect_Kamehameha);
        }
    }

    /// <summary>
    /// Kamehameha, the ability: casting it takes the charge (EchoCastPayment) and queues the channel
    /// (<see cref="JobDriver_GokuChannel"/>); everything after that is <see cref="KamehamehaCast"/>'s.
    /// Its Fire, Warp and Cancel buttons are added to the ability's gizmo by <see cref="Patch_GokuCommands"/>.
    /// </summary>
    public class CompAbilityEffect_Kamehameha : CompAbilityEffect
    {
        public new CompProperties_Kamehameha Props => (CompProperties_Kamehameha)props;

        public override bool GizmoDisabled(out string reason) => GokuBusy.Disabled(parent.pawn, out reason) || base.GizmoDisabled(out reason);

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (target.Cell == parent.pawn.Position)
            {
                if (throwMessages) Messages.Message("Aim away from Goku.", parent.pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent.pawn;
            if (pawn?.Map == null || !target.IsValid) return;
            float paid = EchoUtility.ManifestedWith(pawn, parent.def)?.def.CastCost(parent.def) ?? 0f;
            GameComponent_Goku.Instance?.Queue(new KamehamehaCast(pawn, target.Cell, Find.TickManager.TicksGame, paid));
            pawn.jobs.jobQueue.EnqueueFirst(JobMaker.MakeJob(GokuDefOf.AG_GokuChannel));
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn pawn = parent.pawn;
            if (pawn?.Map == null || !target.IsValid || target.Cell == pawn.Position) return;
            Vector2 toward = KamehamehaLane.Toward(pawn.Position, target.Cell);
            float stop = KamehamehaLane.Stop(pawn.Position, toward, Props.length, pawn.Map, out _, out _);
            GenDraw.DrawFieldEdges(KamehamehaLane.Cells(pawn.Position, toward, stop, Props.width, pawn.Map));
            GenDraw.DrawRadiusRing(KamehamehaLane.EndCell(pawn.Position, toward, stop), Props.blastRadius);
        }
    }

    // ---- Spirit Bomb ----------------------------------------------------------------------------------------------

    public class CompProperties_SpiritBomb : CompProperties_AbilityEffect
    {
        public float minChannelSeconds = 3f;
        /// <summary>Power per second from each lender; the caster always gives 1.</summary>
        public float lendPerSecond = 1f;
        public float radiusBase = 2f;
        public float radiusPerPower = 0.25f;
        public float damageBase = 20f;
        public float damagePerPower = 6f;
        public DamageDef damageDef;
        public float armorPenetration = 0f;
        public float flySeconds = 1.4f;
        /// <summary>How long a full bomb's dome stands before it bursts (the picture's).</summary>
        public float domeHoldSeconds = 2f;
        /// <summary>The dome's animation speed (the picture's).</summary>
        public float pace = 0.6f;
        /// <summary>The ball's size per power (the picture's).</summary>
        public float sizePerPower = 0.12f;

        public DamageDef DamageDef => damageDef ?? DamageDefOf.Burn;

        public CompProperties_SpiritBomb()
        {
            compClass = typeof(CompAbilityEffect_SpiritBomb);
        }
    }

    /// <summary>
    /// Spirit Bomb, the ability: casting it takes the charge and queues the channel; everything after
    /// that is <see cref="SpiritBombCast"/>'s. Its Throw and Cancel buttons come from
    /// <see cref="Patch_GokuCommands"/>, the other colonists' Lend energy from <see cref="Patch_Pawn_GokuLendGizmos"/>.
    /// </summary>
    public class CompAbilityEffect_SpiritBomb : CompAbilityEffect
    {
        public new CompProperties_SpiritBomb Props => (CompProperties_SpiritBomb)props;

        public override bool GizmoDisabled(out string reason) => GokuBusy.Disabled(parent.pawn, out reason) || base.GizmoDisabled(out reason);

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent.pawn;
            if (pawn?.Map == null || !target.IsValid) return;
            float paid = EchoUtility.ManifestedWith(pawn, parent.def)?.def.CastCost(parent.def) ?? 0f;
            GameComponent_Goku.Instance?.Queue(new SpiritBombCast(pawn, target.Cell, Find.TickManager.TicksGame, paid));
            pawn.jobs.jobQueue.EnqueueFirst(JobMaker.MakeJob(GokuDefOf.AG_GokuChannel));
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (parent.pawn?.Map == null || !target.IsValid) return;
            GenDraw.DrawRadiusRing(target.Cell, Props.radiusBase);
        }
    }
}
