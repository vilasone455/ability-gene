using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>What both gauntlet abilities share: they need the gauntlet in hand and a working hand.</summary>
    public abstract class CompAbilityEffect_FlameGauntlet : CompAbilityEffect
    {
        protected CompFlameGauntlet Gauntlet => CompFlameGauntlet.HeldBy(parent.pawn);

        public override bool CanCast => base.CanCast && Unavailable() == null;

        protected virtual string Unavailable()
        {
            if (Gauntlet == null) return "Requires a flame gauntlet in hand.";
            if (!parent.pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return parent.pawn.LabelShortCap + " cannot manipulate.";
            return null;
        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = Unavailable();
            return reason != null || base.GizmoDisabled(out reason);
        }
    }

    public class CompProperties_FlameDevour : CompProperties_AbilityEffect
    {
        /// <summary>Fires within this many cells of the target are eaten.</summary>
        public float radius = 3f;

        public CompProperties_FlameDevour()
        {
            compClass = typeof(CompAbilityEffect_FlameDevour);
        }
    }

    /// <summary>
    /// Devour. The open palm pulls in every fire within the radius of the target cell, nearest first:
    /// burning cells and burning pawns, friendly or not, so it also puts out a burning colonist. Each
    /// adds its Heat when it reaches the palm; what the meter has no room for is refused and keeps
    /// burning. Refused outright at a full meter. The fires go out and the Heat comes in when the
    /// picture shows it (MapComponent_FlameGauntlet).
    /// </summary>
    public class CompAbilityEffect_FlameDevour : CompAbilityEffect_FlameGauntlet
    {
        public new CompProperties_FlameDevour Props => (CompProperties_FlameDevour)props;

        protected override string Unavailable()
        {
            string basic = base.Unavailable();
            if (basic != null) return basic;
            CompFlameGauntlet g = Gauntlet;
            if (g.Heat + Mathf.Min(g.Props.heatPerCell, g.Props.heatPerPawn) > g.Props.maxHeat + 0.001f) return "Too hot: the gauntlet is full.";
            return null;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Map map = parent.pawn.Map;
            if (map == null || !target.Cell.InBounds(map)) return false;
            if (FlameGauntletFires.Count(map, target.Cell, Props.radius) == 0)
            {
                if (throwMessages) Messages.Message("Nothing is burning there.", new TargetInfo(target.Cell, map), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            GenDraw.DrawRadiusRing(target.Cell, Props.radius);
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            CompFlameGauntlet g = Gauntlet;
            Map map = parent.pawn.Map;
            if (g == null || map == null || !target.Cell.InBounds(map)) return null;
            int cells = 0, pawns = 0;
            foreach (FlameGauntletFires.Found f in FlameGauntletFires.InRadius(map, target.Cell, Props.radius))
            {
                if (f.pawn != null) pawns++;
                else cells++;
            }
            if (cells + pawns == 0) return null;
            float gain = cells * g.Props.heatPerCell + pawns * g.Props.heatPerPawn;
            float after = Mathf.Min(g.Props.maxHeat, g.Heat + gain);
            return cells + " burning cells, " + pawns + " burning pawns: heat " + g.Heat.ToString("0") + " -> " + after.ToString("0") + " / " + g.Props.maxHeat.ToString("0")
                + (g.Heat + gain > g.Props.maxHeat ? " (the rest is refused)" : "");
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || Gauntlet == null) return;
            caster.Map.GetComponent<MapComponent_FlameGauntlet>().LandDevour(caster, target.Cell, Props);
        }
    }

    public class CompProperties_FlameRelease : CompProperties_AbilityEffect
    {
        /// <summary>The cone: this many rows along the aim, one cell wide in the first row, this wide after.</summary>
        public int length = 6;
        public int width = 3;
        /// <summary>The size of each fire it starts (vanilla fire sizes run 0.1 to 1.75).</summary>
        public float fireSize = 0.6f;
        /// <summary>A pawn in a lit cell catches fire of this size.</summary>
        public float pawnFireSize = 0.5f;
        /// <summary>
        /// Each lit cell gets a chemfuel puddle under its fire. Core deletes a fire at once on a cell
        /// with nothing that burns, so on bare ground the cone was gone within a second or two.
        /// </summary>
        public bool fuelPuddle = true;

        public CompProperties_FlameRelease()
        {
            compClass = typeof(CompAbilityEffect_FlameRelease);
        }
    }

    /// <summary>
    /// Release. A cone of fire along the aim, rising out of the ground from the fist outward: the
    /// nearest row first, a row's centre before its sides. Each cell costs Heat, so with less Heat
    /// the cone is shorter. Every cell it reaches gets a fire and a pawn standing there catches fire;
    /// the wearer is immune. Refused under the minimum ("too cold"). Cells behind a wall are skipped.
    /// </summary>
    public class CompAbilityEffect_FlameRelease : CompAbilityEffect_FlameGauntlet
    {
        public new CompProperties_FlameRelease Props => (CompProperties_FlameRelease)props;

        protected override string Unavailable()
        {
            string basic = base.Unavailable();
            if (basic != null) return basic;
            CompFlameGauntlet g = Gauntlet;
            if (g.Heat + 0.0001f < g.Props.minReleaseHeat) return "Too cold: needs " + g.Props.minReleaseHeat + " heat, has " + Mathf.FloorToInt(g.Heat + 0.0001f) + ".";
            return null;
        }

        /// <summary>How many of the cone's cells the gauntlet's Heat pays for.</summary>
        public int Affordable(int cells)
        {
            CompFlameGauntlet g = Gauntlet;
            if (g == null) return 0;
            if (g.Heat + 0.0001f < g.Props.minReleaseHeat) return 0;
            return Mathf.Min(cells, Mathf.FloorToInt((g.Heat + 0.0001f) / Mathf.Max(1, g.Props.heatPerConeCell)));
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !target.Cell.InBounds(caster.Map)) return;
            List<FlameGauntletCone.Cell> cone = FlameGauntletCone.Cells(caster, target.Cell, Props.length, Props.width);
            int lit = Affordable(cone.Count);
            var cells = new List<IntVec3>(lit);
            for (int i = 0; i < lit; i++) cells.Add(cone[i].cell);
            GenDraw.DrawFieldEdges(cells);
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            CompFlameGauntlet g = Gauntlet;
            Pawn caster = parent.pawn;
            if (g == null || caster?.Map == null || !target.Cell.InBounds(caster.Map)) return null;
            int cone = FlameGauntletCone.Cells(caster, target.Cell, Props.length, Props.width).Count, lit = Affordable(cone);
            return lit + " of " + cone + " cells: heat " + g.Heat.ToString("0") + " -> " + (g.Heat - lit * g.Props.heatPerConeCell).ToString("0");
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || Gauntlet == null) return;
            caster.Map.GetComponent<MapComponent_FlameGauntlet>().LandRelease(caster, target.Cell, Props);
        }
    }

    /// <summary>The fires Devour can take around a cell.</summary>
    public static class FlameGauntletFires
    {
        public struct Found
        {
            public Fire fire;
            /// <summary>The burning pawn, or null for a fire on a cell (or on a thing that is not a pawn).</summary>
            public Pawn pawn;
        }

        private static readonly List<Found> Buffer = new List<Found>();

        /// <summary>Every fire within <paramref name="radius"/> of <paramref name="centre"/>. The list is reused: copy it to keep it.</summary>
        public static List<Found> InRadius(Map map, IntVec3 centre, float radius)
        {
            Buffer.Clear();
            foreach (IntVec3 c in GenRadial.RadialCellsAround(centre, radius, true))
            {
                if (!c.InBounds(map)) continue;
                List<Thing> things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (!(things[i] is Fire fire) || fire.Destroyed) continue;
                    Buffer.Add(new Found { fire = fire, pawn = fire.parent as Pawn });
                }
            }
            return Buffer;
        }

        public static int Count(Map map, IntVec3 centre, float radius) => InRadius(map, centre, radius).Count;
    }

    /// <summary>
    /// Release's cone on the map. The sketch's cells (FlameReleaseTiming.Pattern: row 1 one cell, then
    /// rows of the cone's width) laid along the aim from the wearer's cell and rounded to map cells,
    /// in the same cost order. Cells off the map, inside a wall, already listed, or out of the
    /// wearer's sight are dropped.
    /// </summary>
    public static class FlameGauntletCone
    {
        public struct Cell
        {
            public IntVec3 cell;
            public int row, across;
        }

        public static List<Cell> Cells(Pawn caster, IntVec3 target, int length, int width)
        {
            var cells = new List<Cell>();
            Map map = caster.Map;
            IntVec3 from = caster.Position;
            var run = new Vector2(target.x - from.x, target.z - from.z);
            IntVec3 facing = caster.Rotation.FacingCell;
            Vector2 dir = run.sqrMagnitude < 0.01f ? new Vector2(facing.x, facing.z) : run.normalized;
            var side = new Vector2(-dir.y, dir.x);
            Vector3 origin = from.ToVector3Shifted();
            foreach (Vector2Int p in FlameReleaseTiming.Pattern(length, width))
            {
                Vector2 at = new Vector2(origin.x, origin.z) + dir * p.x + side * p.y;
                var c = new IntVec3(Mathf.FloorToInt(at.x), 0, Mathf.FloorToInt(at.y));
                if (c == from || !c.InBounds(map) || c.Impassable(map)) continue;
                bool seen = false;
                for (int i = 0; i < cells.Count; i++) if (cells[i].cell == c) { seen = true; break; }
                if (seen || !GenSight.LineOfSight(from, c, map, true)) continue;
                cells.Add(new Cell { cell = c, row = p.x, across = p.y });
            }
            return cells;
        }
    }
}
