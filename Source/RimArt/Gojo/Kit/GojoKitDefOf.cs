using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>The defs of Gojo's Blue, Red and Hollow Purple (AG_Gojo_Abilities.xml and the AG_GojoKit_* files).</summary>
    [DefOf]
    public static class GojoKitDefOf
    {
        public static AbilityDef AG_GojoBlue;
        public static AbilityDef AG_GojoRed;
        public static AbilityDef AG_GojoHollowPurple;
        public static JobDef AG_CastGojoRed;
        public static TerrainDef AG_ErasedGround;
        public static DamageDef AG_Erasure;
        public static HediffDef AG_Erased;
        /// <summary>Gojo's hero form (the costume): the drawn arm takes the uniform's colour while he wears it.</summary>
        public static HediffDef AG_EchoManifest_Gojo;

        static GojoKitDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(GojoKitDefOf));
        }
    }

    /// <summary>Small helpers shared by Red and Hollow Purple.</summary>
    public static class GojoKit
    {
        public static T Props<T>(AbilityDef def) where T : CompProperties_AbilityEffect =>
            def?.comps?.OfType<T>().FirstOrDefault();

        /// <summary>The drawn arm's sleeve: the uniform in hero form, else what he wears on his arms, else skin.</summary>
        public static Color Sleeve(Pawn pawn) =>
            pawn?.health?.hediffSet?.HasHediff(GojoKitDefOf.AG_EchoManifest_Gojo) == true ? GojoGraphics.Uniform : AcceleratorKit.Sleeve(pawn);

        public static Color Skin(Pawn pawn) => pawn?.story?.SkinColor ?? GojoGraphics.Skin;

        /// <summary>A ground point (x, z) of a map position.</summary>
        public static Vector2 Ground(Vector3 at) => new Vector2(at.x, at.z);

        public static Vector2 Ground(IntVec3 cell) => new Vector2(cell.x + 0.5f, cell.z + 0.5f);

        public static IntVec3 Cell(Vector2 ground) => new IntVec3(Mathf.FloorToInt(ground.x), 0, Mathf.FloorToInt(ground.y));

        /// <summary>
        /// A loose thing Red and Purple act on: an item that can be hauled (a stack, a chunk, a weapon, a
        /// minified building) or a corpse.
        /// </summary>
        public static bool Loose(Thing thing) =>
            thing != null && thing.Spawned && thing.def.category == ThingCategory.Item && thing.def.EverHaulable;

        /// <summary>
        /// A pawn, not Gojo, alive and spawned. The pinned and pulled pawns of Pain's kit are not moved
        /// (docs/hero-echo.md, "cannot be moved").
        /// </summary>
        public static bool Movable(Pawn pawn, Pawn gojo) =>
            pawn != null && pawn != gojo && pawn.Spawned && !pawn.Dead && !PainKit.Unmovable(pawn);

        /// <summary>
        /// A cell a flying body cannot enter: a wall, natural rock, a closed door or any other building a
        /// shot cannot pass over. Terrain never blocks (Red flies over water).
        /// </summary>
        public static bool Wall(IntVec3 cell, Map map) => cell.InBounds(map) && !cell.CanBeSeenOver(map);

        /// <summary>The DamageInfo angle of a ground direction.</summary>
        public static float Angle(Vector2 way) => new Vector3(way.x, 0f, way.y).AngleFlat();

        /// <summary>
        /// A straight move of <paramref name="cells"/> from <paramref name="start"/> along
        /// <paramref name="way"/>, checked every 0.1 cells. It ends at the last cell a body can stand in: short
        /// of a wall (walled, and <paramref name="face"/> is the distance at which the wall's cell was entered),
        /// of the map edge, or of ground no one can stand on (deep water, a chasm), which is not a wall.
        /// </summary>
        public static IntVec3 Walk(Map map, Vector2 start, Vector2 way, float cells, out bool walled, out float face)
        {
            walled = false;
            face = 0f;
            IntVec3 last = Cell(start), land = last;
            for (float d = 0.1f; d <= cells + 1e-4f; d += 0.1f)
            {
                IntVec3 c = Cell(start + way * d);
                if (c == last) continue;
                if (!c.InBounds(map)) break;
                if (Wall(c, map))
                {
                    walled = true;
                    face = d;
                    break;
                }
                if (!c.Standable(map)) break;
                last = land = c;
            }
            return land;
        }
    }
}
