using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_ChainArc : CompProperties_AbilityEffect
    {
        /// <summary>Battery charge one cast spends.</summary>
        public int cost = 5;
        /// <summary>AG_CoilBurn damage to each pawn hit.</summary>
        public float damage = 12f;
        public float armorPenetration = 0.35f;
        /// <summary>Jumps after the first hit: up to this many more hostile pawns.</summary>
        public int jumps = 3;
        /// <summary>Cells a jump may reach from the pawn it leaves.</summary>
        public float jumpRadius = 3f;
        /// <summary>A jump leaving a Soaked pawn reaches this many times further.</summary>
        public float soakedJumpFactor = 2f;
        /// <summary>A Soaked pawn takes this many times the damage.</summary>
        public float soakedDamageFactor = 1.5f;
        /// <summary>A mechanoid takes this many times the damage, as from the gun's bolts.</summary>
        public float mechDamageFactor = 1.5f;

        public CompProperties_ChainArc()
        {
            compClass = typeof(CompAbilityEffect_ChainArc);
        }
    }

    /// <summary>
    /// Chain Arc. Needs the coil gun in hand and <see cref="CompProperties_ChainArc.cost"/> battery.
    /// Target: a hostile pawn in line of sight. The bolt hits it, then jumps to the nearest hostile pawn
    /// not yet hit within jumpRadius of the last one hit (and in its line of sight), up to jumps times;
    /// from a Soaked pawn the reach is soakedJumpFactor times as long. Allies, neutrals and downed
    /// pawns are never jumped to. Each hit is AG_CoilBurn damage (Sharp armour), times soakedDamageFactor on a Soaked pawn and
    /// mechDamageFactor on a mechanoid. The chain is decided when the gun fires; each hit lands as the
    /// picture's bolt arrives (MapComponent_CoilGun).
    /// </summary>
    public class CompAbilityEffect_ChainArc : CompAbilityEffect
    {
        public new CompProperties_ChainArc Props => (CompProperties_ChainArc)props;

        private CompCoilGun Gun => CompCoilGun.HeldBy(parent.pawn);

        public override bool CanCast => base.CanCast && Unavailable() == null;

        private string Unavailable()
        {
            CompCoilGun gun = Gun;
            if (gun == null) return "Requires a coil gun in hand.";
            if (gun.Units < Props.cost) return "Not enough charge: needs " + Props.cost + ", the battery has " + gun.Units + ". It recharges next to a charged battery.";
            return null;
        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = Unavailable();
            return reason != null || base.GizmoDisabled(out reason);
        }

        public override string ExtraTooltipPart()
        {
            CompCoilGun gun = Gun;
            return gun == null ? null : "Battery: " + gun.LabelRemaining;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!(target.Thing is Pawn pawn) || pawn.Dead || !pawn.HostileTo(parent.pawn))
            {
                if (throwMessages) Messages.Message("Chain Arc needs a hostile target.", MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            CompCoilGun gun = Gun;
            if (caster?.Map == null || !(target.Thing is Pawn first) || gun == null || gun.Units < Props.cost) return;
            gun.Spend(Props.cost);
            caster.Map.GetComponent<MapComponent_CoilGun>().LandArc(caster, first, parent.def.verbProperties.warmupTime, Props);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (!(target.Thing is Pawn pawn) || parent.pawn?.Map == null || !pawn.HostileTo(parent.pawn)) return;
            List<CoilArcLink> chain = CoilArcChain.Find(parent.pawn, pawn, Props);
            for (int i = 1; i < chain.Count; i++)
                GenDraw.DrawLineBetween(chain[i - 1].pawn.DrawPos, chain[i].pawn.DrawPos, SimpleColor.Cyan);
        }
    }

    /// <summary>One pawn in a Chain Arc, with what was true of it when the chain was decided.</summary>
    public struct CoilArcLink
    {
        public Pawn pawn;
        public bool soaked, mech;
        /// <summary>Cells the next jump may reach from this pawn.</summary>
        public float reach;
    }

    /// <summary>Chain Arc's rule: who the bolt jumps to.</summary>
    public static class CoilArcChain
    {
        public static bool IsMech(Pawn pawn) => pawn?.RaceProps != null && pawn.RaceProps.IsMechanoid;

        public static List<CoilArcLink> Find(Pawn caster, Pawn first, CompProperties_ChainArc props)
        {
            var chain = new List<CoilArcLink>();
            Map map = caster.Map;
            if (map == null || first == null || !first.Spawned || first.Map != map) return chain;
            chain.Add(Link(first, props));
            for (int j = 0; j < props.jumps; j++)
            {
                CoilArcLink from = chain[chain.Count - 1];
                Pawn next = null;
                float best = float.MaxValue;
                IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn p = pawns[i];
                    if (p == from.pawn || p.Dead || p.Downed || !p.HostileTo(caster) || Contains(chain, p)) continue;
                    float d = p.Position.DistanceTo(from.pawn.Position);
                    if (d > from.reach + 0.001f || d >= best) continue;
                    if (!GenSight.LineOfSight(from.pawn.Position, p.Position, map, true)) continue;
                    best = d;
                    next = p;
                }
                if (next == null) break;
                chain.Add(Link(next, props));
            }
            return chain;
        }

        private static CoilArcLink Link(Pawn pawn, CompProperties_ChainArc props)
        {
            bool soaked = WaterGunSoak.IsSoaked(pawn);
            return new CoilArcLink
            {
                pawn = pawn, soaked = soaked, mech = IsMech(pawn),
                reach = props.jumpRadius * (soaked ? props.soakedJumpFactor : 1f),
            };
        }

        private static bool Contains(List<CoilArcLink> chain, Pawn pawn)
        {
            for (int i = 0; i < chain.Count; i++)
                if (chain[i].pawn == pawn) return true;
            return false;
        }

        /// <summary>The damage one hit deals to a link.</summary>
        public static float Damage(in CoilArcLink link, CompProperties_ChainArc props) =>
            props.damage * (link.soaked ? props.soakedDamageFactor : 1f) * (link.mech ? props.mechDamageFactor : 1f);
    }
}
