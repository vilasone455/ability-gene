using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Unlimited Void's rules, set in XML on AG_GojoUnlimitedVoid. The warm-up (the hand sign) is the verb's
    /// warmupTime and the cooldown the ability's cooldownTicksRange; the overload's fall, floor and scar are on
    /// the AG_VoidOverload hediff.
    /// </summary>
    public class CompProperties_AbilityUnlimitedVoid : CompProperties_AbilityEffect
    {
        /// <summary>Everyone within this many cells of Gojo, walls or not, is taken in.</summary>
        public float radius = 9f;
        /// <summary>How long the domain stands before it ends by itself.</summary>
        public float domainSeconds = 10f;
        /// <summary>The pocket map's side in cells; made larger if the radius needs it.</summary>
        public int mapSize = 40;
        /// <summary>Overload each frozen, unspared pawn gains per second (0.1 = 10 %).</summary>
        public float overloadPerSecond = 0.1f;
        /// <summary>A frozen pawn not hostile to Gojo is spared after this long next to him (added up, need not be in one go).</summary>
        public float touchSeconds = 0.3f;
        /// <summary>
        /// Goodwill with each non-hostile faction that has a pawn come out unspared, once per faction per cast
        /// (the vanilla Stun psycast's -15 per target, applied once per faction as Neuroquake does).
        /// </summary>
        public int neutralGoodwill = -15;

        public CompProperties_AbilityUnlimitedVoid()
        {
            compClass = typeof(CompAbilityEffect_UnlimitedVoid);
        }

        public int DomainTicks => Mathf.Max(1, Mathf.RoundToInt(domainSeconds * 60f));
        public int TouchTicks => Mathf.Max(1, Mathf.RoundToInt(touchSeconds * 60f));
    }

    /// <summary>
    /// Unlimited Void, the ability. The warm-up is the hand sign (drawn by <see cref="GameComponent_UnlimitedVoid"/>
    /// while the verb warms up); a cast called off during it costs nothing, since the Echo takes its charge when
    /// the ability fires (EchoCastPayment). Firing starts an <see cref="UnlimitedVoidCast"/>, which does the rest.
    /// While a domain stands, Gojo has a Release button here.
    /// </summary>
    public class CompAbilityEffect_UnlimitedVoid : CompAbilityEffect
    {
        public new CompProperties_AbilityUnlimitedVoid Props => (CompProperties_AbilityUnlimitedVoid)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent.pawn;
            if (pawn?.Map == null) return;
            EchoRecord record = EchoUtility.ManifestedWith(pawn, parent.def);
            float paid = record?.def.CastCost(parent.def) ?? 0f;
            GameComponent_UnlimitedVoid.Instance?.Begin(pawn, paid, record != null);
        }

        /// <summary>Not from a pocket map (Unlimited Blade Works, Kamui, another void), and not while a domain of his holds him.</summary>
        public override bool CanCast =>
            base.CanCast && parent.pawn?.MapHeld?.IsPocketMap != true && GameComponent_UnlimitedVoid.Instance?.For(parent.pawn) == null;

        public override void CompTick()
        {
            base.CompTick();
            if (parent.verb != null && parent.verb.WarmingUp) GameComponent_UnlimitedVoid.Instance?.Signing(parent.pawn, parent.verb);
        }

        public override bool GizmoDisabled(out string reason)
        {
            Pawn pawn = parent.pawn;
            if (GameComponent_UnlimitedVoid.Instance?.For(pawn) != null)
            {
                reason = "The domain stands.";
                return true;
            }
            if (pawn.MapHeld != null && pawn.MapHeld.IsPocketMap)
            {
                reason = "Cannot open a domain from inside a pocket map.";
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            Pawn pawn = parent.pawn;
            if (pawn == null || !pawn.IsColonistPlayerControlled) yield break;
            UnlimitedVoidCast cast = GameComponent_UnlimitedVoid.Instance?.For(pawn);
            if (cast == null || !cast.Standing || cast.endTick >= 0) yield break;
            yield return new Command_Action
            {
                defaultLabel = "Release (" + cast.SecondsLeft(Find.TickManager.TicksGame).ToString("0") + " s)",
                defaultDesc = "End Unlimited Void now. Everyone comes out at the matching cell of the home map; whoever is still frozen and unspared keeps the overload it has built.",
                icon = parent.def.uiIcon,
                groupable = false,
                action = () => cast.End("released"),
            };
        }
    }
}
