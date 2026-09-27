using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// What the kit's abilities share, as static helpers so that both comp base classes (plain
    /// <see cref="CompAbilityEffect"/> for the single-target abilities, <see cref="CompAbilityEffect_WithDest"/>
    /// for the two-pick ones) use the same reach rule: an ability is cast from the caster's origin
    /// (<see cref="MapComponent_ShadowPlexus.OriginCell"/>, the standing double or the caster), its
    /// reach is its full reach times the light there, and nothing is cast from a dark cell.
    /// </summary>
    public static class ShadowPlexusCast
    {
        public static MapComponent_ShadowPlexus Plexus(Pawn pawn) => MapComponent_ShadowPlexus.Of(pawn?.Map);

        public static IntVec3 OriginCell(Pawn pawn) => Plexus(pawn)?.OriginCell(pawn) ?? pawn.Position;

        public static float Reach(Pawn pawn, float fullReach) => ShadowLight.Reach(fullReach, pawn.Map, OriginCell(pawn));

        /// <summary>Why nothing can be cast now: the origin cell is dark. Null when it can.</summary>
        public static string Unavailable(Pawn pawn)
        {
            if (pawn?.Map == null) return null;
            return ShadowLight.Dark(pawn.Map, OriginCell(pawn)) ? "AG_ShadowNoLight".Translate().ToString() : null;
        }

        /// <summary>Why a cell is out of reach from the origin, or null.</summary>
        public static string OutOfReach(Pawn pawn, IntVec3 cell, float fullReach)
        {
            float reach = Reach(pawn, fullReach), distance = OriginCell(pawn).DistanceTo(cell);
            return distance > reach ? "AG_ShadowOutOfReach".Translate(distance.ToString("0.#"), reach.ToString("0.#")).ToString() : null;
        }

        /// <summary>Why a shadow line cannot lie from the origin to a cell now, or null.</summary>
        public static string LineBlocked(Pawn pawn, IntVec3 cell, Thing target)
        {
            LineBreak broke = ShadowLines.Check(pawn.Map, OriginCell(pawn), cell, pawn, target, light: true, smoke: true, out _, out Pawn by);
            return broke == LineBreak.None ? null : "AG_ShadowLineBlocked".Translate(ShadowLines.Word(broke, by)).ToString();
        }

        public static bool Refuse(string reason, bool throwMessages, Thing at)
        {
            if (reason == null) return false;
            if (throwMessages) Messages.Message(reason, at, MessageTypeDefOf.RejectInput, false);
            return true;
        }

        /// <summary>The reach ring round the origin, and the double's cell when it is the origin.</summary>
        public static void DrawReach(Pawn pawn, float fullReach)
        {
            if (pawn?.Map == null) return;
            MapComponent_ShadowPlexus plexus = Plexus(pawn);
            IntVec3 origin = OriginCell(pawn);
            float reach = Reach(pawn, fullReach);
            if (reach >= 1f) GenDraw.DrawRadiusRing(origin, reach);
            if (plexus != null && plexus.CastsFromDouble(pawn)) GenDraw.DrawFieldEdges(new System.Collections.Generic.List<IntVec3> { origin });
        }

        public static string ReachTip(Pawn pawn, float fullReach)
        {
            if (pawn?.Map == null) return null;
            MapComponent_ShadowPlexus plexus = Plexus(pawn);
            IntVec3 origin = OriginCell(pawn);
            string from = plexus != null && plexus.CastsFromDouble(pawn) ? "AG_ShadowFromDouble".Translate().ToString() : "AG_ShadowFromSelf".Translate().ToString();
            return "AG_ShadowReachTip".Translate(Reach(pawn, fullReach).ToString("0.#"), Mathf.RoundToInt(ShadowLight.Level(pawn.Map, origin) * 100f), from);
        }
    }

    /// <summary>The single-target abilities' base: greyed out in the dark, the reach ring on hover, the reach in the tooltip.</summary>
    public abstract class CompAbilityEffect_ShadowPlexus : CompAbilityEffect
    {
        protected MapComponent_ShadowPlexus Plexus => ShadowPlexusCast.Plexus(parent.pawn);

        /// <summary>The ability's full reach, before the light; 0 for one that has none of its own.</summary>
        protected abstract float FullReach { get; }

        /// <summary>Whether the ability is cast from the origin and needs light there.</summary>
        protected virtual bool NeedsLight => true;

        protected virtual string Unavailable() => NeedsLight ? ShadowPlexusCast.Unavailable(parent.pawn) : null;

        public override bool GizmoDisabled(out string reason)
        {
            reason = Unavailable();
            return reason != null || base.GizmoDisabled(out reason);
        }

        public override void OnGizmoUpdate()
        {
            base.OnGizmoUpdate();
            if (FullReach > 0f && NeedsLight) ShadowPlexusCast.DrawReach(parent.pawn, FullReach);
        }

        public override string ExtraTooltipPart() => FullReach > 0f && NeedsLight ? ShadowPlexusCast.ReachTip(parent.pawn, FullReach) : null;
    }

    public class CompProperties_ShadowImitation : CompProperties_AbilityEffect
    {
        /// <summary>Full reach, in cells; times the light at the origin.</summary>
        public float reach = 19.9f;
        /// <summary>How long the target is held.</summary>
        public float holdSeconds = 15f;

        public CompProperties_ShadowImitation()
        {
            compClass = typeof(CompAbilityEffect_ShadowImitation);
        }
    }

    /// <summary>
    /// Shadow imitation. One pawn within reach along a clear shadow line is held (stunned) for the
    /// hold time, and every cell the caster steps moves it one cell the same way. The hold ends when
    /// the line breaks (<see cref="ShadowLines"/>), the caster goes down, or the time runs out.
    /// </summary>
    public class CompAbilityEffect_ShadowImitation : CompAbilityEffect_ShadowPlexus
    {
        public new CompProperties_ShadowImitation Props => (CompProperties_ShadowImitation)props;

        protected override float FullReach => Props.reach;

        private string Refusal(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            if (!(target.Thing is Pawn pawn) || pawn.Dead) return "AG_ShadowNeedsPawn".Translate();
            if (pawn == caster) return "AG_ShadowNotSelf".Translate();
            return Unavailable() ?? ShadowPlexusCast.OutOfReach(caster, pawn.Position, Props.reach) ?? ShadowPlexusCast.LineBlocked(caster, pawn.Position, pawn);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false) =>
            !ShadowPlexusCast.Refuse(Refusal(target), throwMessages, target.Thing) && base.Valid(target, throwMessages);

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Refusal(target) == null && base.CanApplyOn(target, dest);

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target) => Refusal(target);

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !(target.Thing is Pawn pawn) || pawn.Dead) return;
            Plexus?.LandImitation(caster, pawn, parent.def.verbProperties.warmupTime, Props);
        }
    }

    public class CompProperties_ShadowDouble : CompProperties_AbilityEffect
    {
        /// <summary>How far the double can be sent, in cells. Not scaled by light: the caster may stand in the dark.</summary>
        public float reach = 24.9f;
        /// <summary>How long it stands.</summary>
        public float standSeconds = 20f;

        public CompProperties_ShadowDouble()
        {
            compClass = typeof(CompAbilityEffect_ShadowDouble);
        }
    }

    /// <summary>
    /// Shadow double. The caster's shadow goes to a lit cell within reach and stands there; while it
    /// stands, Imitation, Seam and Grasp are cast from its cell and use its light, and it copies the
    /// caster's steps. It ends when its time runs out, its cell goes dark, or a pawn crosses the line
    /// between it and the caster. That line may cross dark cells.
    /// </summary>
    public class CompAbilityEffect_ShadowDouble : CompAbilityEffect_ShadowPlexus
    {
        public new CompProperties_ShadowDouble Props => (CompProperties_ShadowDouble)props;

        protected override float FullReach => Props.reach;

        protected override bool NeedsLight => false;

        private string Refusal(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            Map map = caster.Map;
            IntVec3 cell = target.Cell;
            if (map == null || !cell.InBounds(map) || !cell.Walkable(map)) return "AG_ShadowDoubleNoFloor".Translate();
            float distance = caster.Position.DistanceTo(cell);
            if (distance > Props.reach) return "AG_ShadowOutOfReach".Translate(distance.ToString("0.#"), Props.reach.ToString("0.#"));
            if (ShadowLight.Dark(map, cell)) return "AG_ShadowTargetDark".Translate();
            LineBreak broke = ShadowLines.Check(map, caster.Position, cell, caster, null, light: false, smoke: false, out _, out Pawn by);
            if (broke != LineBreak.None) return "AG_ShadowLineBlocked".Translate(ShadowLines.Word(broke, by));
            return null;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false) =>
            !ShadowPlexusCast.Refuse(Refusal(target), throwMessages, parent.pawn) && base.Valid(target, throwMessages);

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Refusal(target) == null && base.CanApplyOn(target, dest);

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target) => Refusal(target);

        public override void OnGizmoUpdate()
        {
            base.OnGizmoUpdate();
            if (parent.pawn?.Map != null) GenDraw.DrawRadiusRing(parent.pawn.Position, Props.reach);
        }

        public override string ExtraTooltipPart() => "AG_ShadowDoubleTip".Translate(Props.reach.ToString("0.#"), Props.standSeconds.ToString("0"));

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !target.IsValid) return;
            Plexus?.LandDouble(caster, target.Cell, parent.def.verbProperties.warmupTime, Props);
        }
    }

    public class CompProperties_ShadowNeckBind : CompProperties_AbilityEffect
    {
        /// <summary>How long the hands take to climb the body once they reach it.</summary>
        public float climbSeconds = 1.5f;
        /// <summary>Suffocation per second once the hands close, 1 = unconscious.</summary>
        public float suffocationPerSecond = 0.125f;
        /// <summary>How long the hands stay closed at most: the channel after the climb.</summary>
        public float chokeSeconds = 8f;

        public CompProperties_ShadowNeckBind()
        {
            compClass = typeof(CompAbilityEffect_ShadowNeckBind);
        }
    }

    /// <summary>
    /// Shadow neck bind. Only on a pawn the caster holds by Imitation or has sewn by Seam; it has no
    /// reach of its own because it travels along the line already there. The caster channels,
    /// standing still: the hands crawl along the line, climb the body and close on the throat, and
    /// the target's suffocation (<see cref="HediffComp_ShadowChoke"/>) rises until it passes out. A
    /// broken line, a move order or the caster going down drops the hands and the suffocation drains.
    /// Mechanoids have no throat.
    /// </summary>
    public class CompAbilityEffect_ShadowNeckBind : CompAbilityEffect_ShadowPlexus
    {
        public new CompProperties_ShadowNeckBind Props => (CompProperties_ShadowNeckBind)props;

        protected override float FullReach => 0f;

        protected override bool NeedsLight => false;

        protected override string Unavailable()
        {
            MapComponent_ShadowPlexus plexus = Plexus;
            if (plexus == null || !plexus.HoldsAnyone(parent.pawn)) return "AG_ShadowNotHeld".Translate();
            return null;
        }

        private string Refusal(LocalTargetInfo target)
        {
            if (!(target.Thing is Pawn pawn) || pawn.Dead) return "AG_ShadowNeedsPawn".Translate();
            if (pawn.RaceProps.IsMechanoid) return "AG_ShadowMechImmune".Translate();
            MapComponent_ShadowPlexus plexus = Plexus;
            if (plexus == null || !plexus.Held(parent.pawn, pawn)) return "AG_ShadowNotHeld".Translate();
            return null;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false) =>
            !ShadowPlexusCast.Refuse(Refusal(target), throwMessages, target.Thing) && base.Valid(target, throwMessages);

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Refusal(target) == null && base.CanApplyOn(target, dest);

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target) => Refusal(target);

        public override string ExtraTooltipPart() =>
            "AG_ShadowNeckBindTip".Translate((ShadowNeckBindTiming.Crawl + Props.climbSeconds).ToString("0.#"),
                Mathf.RoundToInt(Props.suffocationPerSecond * 100f), Props.chokeSeconds.ToString("0"));

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !(target.Thing is Pawn pawn) || pawn.Dead) return;
            Plexus?.LandNeckBind(caster, pawn, Props);
        }
    }
}
