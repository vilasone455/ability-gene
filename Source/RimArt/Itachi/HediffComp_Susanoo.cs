using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>Every number of the Susanoo, from AG_Itachi_Hediffs.xml (docs/hero-echo.md, Itachi).</summary>
    public class HediffCompProperties_Susanoo : HediffCompProperties
    {
        /// <summary>How far the Totsuka Blade reaches, in cells.</summary>
        public float totsukaRange = 4f;
        /// <summary>Ticks between stabs.</summary>
        public int totsukaCooldownTicks = 180;
        /// <summary>The hit a target takes when it is not sealed.</summary>
        public float stabDamage = 30f;
        public float stabArmorPenetration = 0.35f;
        /// <summary>A target at or below this share of summary health is sealed instead of hit.</summary>
        public float sealHealthPercent = 0.3f;
        /// <summary>What the Susanoo leaves behind when it ends, and for how long.</summary>
        public HediffDef drainedHediff;
        public int drainedTicks = 15000;
        /// <summary>Blood loss severity added when it ends.</summary>
        public float bloodLoss = 0.1f;
        /// <summary>Ticks from the click to the stab landing: the picture's swing. The hit or seal resolves then.</summary>
        public int stabResolveTicks = 27;

        public HediffCompProperties_Susanoo()
        {
            compClass = typeof(HediffComp_Susanoo);
        }
    }

    /// <summary>
    /// The Susanoo standing round Itachi for the hediff's life (12 s from the ability's
    /// Ability_Duration). Two things live here:
    ///
    /// - The Yata Mirror: <see cref="Patch_Susanoo_YataMirror"/> asks the registry and absorbs every
    ///   hit from outside while this comp exists.
    /// - The Totsuka Blade: a targeted command on the hero form. A stab seals a target that is downed
    ///   or at 30 % health or less (off the map, counted as Itachi's kill, its gear left on the
    ///   ground, no corpse), once per Susanoo; anything else, and any mechanoid, takes a stab hit.
    ///
    /// When the hediff goes, whether it ran out or the Host reverted, the drained hediff and the blood
    /// loss follow: the price of holding it is his illness.
    /// </summary>
    [StaticConstructorOnStartup]
    public class HediffComp_Susanoo : HediffComp
    {
        private bool sealedThisSusanoo;
        private int nextStabTick = -1;
        private bool ended;
        /// <summary>The target of a stab whose blade is still on its way, and the tick it lands.</summary>
        private Pawn pendingTarget;
        private int resolveTick = -1;
        private static Texture2D icon;

        public HediffCompProperties_Susanoo Props => (HediffCompProperties_Susanoo)props;

        public bool Sealed => sealedThisSusanoo;

        public int StabTicksLeft => Mathf.Max(0, nextStabTick - Find.TickManager.TicksGame);

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            SusanooRegistry.Report(this);
            MapComponent_Susanoo.For(Pawn)?.Raised(Pawn);
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            SusanooRegistry.Report(this);
            if (pendingTarget != null && Find.TickManager.TicksGame >= resolveTick) Resolve();
            // Reverting takes the abilities and the manifest hediff; the Susanoo is part of the
            // form and goes with it.
            EchoRecord record = GameComponent_Echoes.Get?.HostRecord(Pawn);
            if (record == null || !record.manifested) Pawn.health.RemoveHediff(parent);
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            SusanooRegistry.Drop(this);
            MapComponent_Susanoo.For(Pawn)?.End(Pawn);
            End();
        }

        private void End()
        {
            if (ended) return;
            ended = true;
            if (Pawn == null || Pawn.Dead || Pawn.health == null) return;
            if (Props.drainedHediff != null)
            {
                Hediff drained = HediffMaker.MakeHediff(Props.drainedHediff, Pawn);
                HediffComp_Disappears timer = drained.TryGetComp<HediffComp_Disappears>();
                if (timer != null) timer.ticksToDisappear = Props.drainedTicks;
                Pawn.health.AddHediff(drained);
            }
            if (Props.bloodLoss > 0f) Scatter.Bleed(Pawn, Props.bloodLoss);
        }

        // ---- Totsuka Blade ----

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (Pawn == null || !Pawn.IsColonistPlayerControlled) yield break;
            if (icon == null) icon = ContentFinder<Texture2D>.Get("RimArt/Itachi/IconTotsuka", false);

            var command = new Command_Target
            {
                defaultLabel = "AG_ItachiTotsuka".Translate(),
                defaultDesc = "AG_ItachiTotsukaDesc".Translate(Props.totsukaRange.ToString("0"),
                    (Props.totsukaCooldownTicks / 60f).ToString("0"), Props.stabDamage.ToString("0"),
                    (Props.sealHealthPercent * 100f).ToString("0")),
                icon = icon,
                targetingParams = new TargetingParameters
                {
                    canTargetPawns = true,
                    canTargetSelf = false,
                    canTargetBuildings = false,
                    canTargetLocations = false,
                    validator = ValidTarget,
                },
                action = target => Stab(target.Thing as Pawn),
            };
            if (sealedThisSusanoo) command.defaultLabel += " " + "AG_ItachiTotsukaSealedTag".Translate();
            if (Pawn.Downed) command.Disable("AG_ItachiTotsukaDowned".Translate());
            else if (StabTicksLeft > 0)
                command.Disable("AG_ItachiTotsukaCooldown".Translate(StabTicksLeft.ToStringSecondsFromTicks()));
            yield return command;
        }

        public bool ValidTarget(TargetInfo target)
        {
            Pawn other = target.Thing as Pawn;
            return other != null && other != Pawn && other.Spawned && !other.Dead && other.Map == Pawn.Map
                && other.Position.InHorDistOf(Pawn.Position, Props.totsukaRange);
        }

        /// <summary>Whether the next stab on this target would seal it rather than cut it.</summary>
        public bool WouldSeal(Pawn target)
        {
            if (target == null || sealedThisSusanoo || target.RaceProps.IsMechanoid) return false;
            return target.Downed || target.health.summaryHealth.SummaryHealthPercent <= Props.sealHealthPercent;
        }

        /// <summary>
        /// One stab: the swing starts now and the blade lands after stabResolveTicks, when the seal
        /// or the hit resolves on the target where it is then. Does nothing while the blade is still
        /// drawn back or the target is out of reach.
        /// </summary>
        public void Stab(Pawn target)
        {
            if (target == null || Pawn == null || !Pawn.Spawned || Pawn.Downed) return;
            if (StabTicksLeft > 0 || !ValidTarget(target)) return;
            nextStabTick = Find.TickManager.TicksGame + Props.totsukaCooldownTicks;
            pendingTarget = target;
            resolveTick = Find.TickManager.TicksGame + Props.stabResolveTicks;
            MapComponent_Susanoo.For(Pawn)?.Stab(Pawn, target, WouldSeal(target));
        }

        /// <summary>The blade lands. A target that died, left or walked more than a cell and a half out of reach is missed.</summary>
        private void Resolve()
        {
            Pawn target = pendingTarget;
            pendingTarget = null;
            if (target == null || target.Dead || !target.Spawned || Pawn == null || Pawn.Dead || !Pawn.Spawned || target.Map != Pawn.Map) return;
            if (!target.Position.InHorDistOf(Pawn.Position, Props.totsukaRange + 1.5f)) return;
            if (WouldSeal(target)) Seal(target);
            else Hit(target);
        }

        private void Hit(Pawn target)
        {
            float angle = (target.DrawPos - Pawn.DrawPos).AngleFlat();
            var dinfo = new DamageInfo(DamageDefOf.Stab, Props.stabDamage, Props.stabArmorPenetration, angle, Pawn);
            target.TakeDamage(dinfo);
        }

        /// <summary>
        /// Off the map, as a kill. Strip first so its gear lands on its cell unforbidden and without
        /// the faction being told it was robbed; Kill with Itachi as the instigator so the kill
        /// records, the Echo deeds and the battle log see it; then the corpse vanishes. The order
        /// matters: destroying a corpse destroys whatever the pawn still wears.
        /// </summary>
        private void Seal(Pawn target)
        {
            sealedThisSusanoo = true;
            string label = target.LabelShortCap;
            target.Strip(notifyFaction: false);
            target.Kill(new DamageInfo(DamageDefOf.Stab, 0f, 0f, -1f, Pawn));
            Corpse corpse = target.Corpse;
            if (corpse != null && !corpse.Destroyed) corpse.Destroy(DestroyMode.Vanish);
            if (PawnUtility.ShouldSendNotificationAbout(Pawn))
                Messages.Message("AG_ItachiSealed".Translate(label), Pawn, MessageTypeDefOf.PositiveEvent, false);
        }

        /// <summary>The Mirror took a hit: the picture turns it to the attacker and ripples the face.</summary>
        public void Blocked(DamageInfo dinfo)
        {
            MapComponent_Susanoo.For(Pawn)?.Blocked(Pawn, dinfo);
        }

        public override string CompTipStringExtra =>
            sealedThisSusanoo ? "AG_ItachiTotsukaUsed".Translate().ToString() : "AG_ItachiTotsukaReady".Translate().ToString();

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref sealedThisSusanoo, "sealedThisSusanoo", false);
            Scribe_Values.Look(ref nextStabTick, "nextStabTick", -1);
            Scribe_Values.Look(ref ended, "ended", false);
            Scribe_References.Look(ref pendingTarget, "pendingTarget");
            Scribe_Values.Look(ref resolveTick, "resolveTick", -1);
        }
    }
}
