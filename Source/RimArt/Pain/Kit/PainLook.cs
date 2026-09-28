using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// How one pawn looks this frame while Pain's kit holds it: a pawn Banshō Ten'in pulls (drawn along the pull,
    /// lifted, its body turned so the head trails; its cell changes as it crosses cells, its draw point follows the
    /// pull smoothly), then lying face-down after the slam; a pawn Black Receiver pins, lying on its back. Rebuilt
    /// once a frame by <see cref="GameComponent_Pain"/>, the way Minato's looks are (MinatoLook.cs), and read while
    /// pawns are drawn.
    /// </summary>
    public sealed class PainLook
    {
        /// <summary>Drawn at <see cref="drawAt"/> (x and z) instead of where the game puts it.</summary>
        public bool moved;
        public Vector3 drawAt;
        /// <summary>Degrees the whole body turns about the vertical, clockwise seen from above (0: head to the north).</summary>
        public float angle;
        /// <summary>The facing the body is drawn with, or Invalid for the pawn's own.</summary>
        public Rot4 facing = Rot4.Invalid;
        /// <summary>Drawn as a lying body: no standing weapon or shadow pieces in the render tree.</summary>
        public bool lying;

        public void Reset()
        {
            moved = lying = false;
            drawAt = Vector3.zero;
            angle = 0f;
            facing = Rot4.Invalid;
        }

        public bool ChangesBody => angle != 0f || facing.IsValid || lying;
    }

    public static class PainLooks
    {
        private static readonly Dictionary<Pawn, PainLook> live = new Dictionary<Pawn, PainLook>();
        private static readonly List<PainLook> spare = new List<PainLook>();

        public static bool TryGet(Pawn pawn, out PainLook look)
        {
            look = null;
            return live.Count > 0 && pawn != null && live.TryGetValue(pawn, out look);
        }

        public static void Clear()
        {
            foreach (PainLook look in live.Values) spare.Add(look);
            live.Clear();
        }

        public static PainLook For(Pawn pawn)
        {
            if (live.TryGetValue(pawn, out PainLook look)) return look;
            if (spare.Count > 0)
            {
                look = spare[spare.Count - 1];
                spare.RemoveAt(spare.Count - 1);
            }
            else look = new PainLook();
            look.Reset();
            live[pawn] = look;
            return look;
        }

        /// <summary>The body drawn with its head pointing along <paramref name="head"/> (a map direction), turned by <paramref name="share"/> 0..1 from upright.</summary>
        public static float HeadAngle(Vector2 head, float share)
        {
            if (head.sqrMagnitude < 1e-6f) return 0f;
            float full = Mathf.Atan2(head.x, head.y) * Mathf.Rad2Deg;
            return full * share;
        }
    }

    /// <summary>The turned body: the root matrix and facing of the whole render tree, the hooks Minato's looks use.</summary>
    [HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
    static class Patch_PawnRenderer_PainLook
    {
        static void Postfix(Pawn ___pawn, ref PawnDrawParms __result)
        {
            if ((__result.flags & (PawnRenderFlags.Portrait | PawnRenderFlags.Cache)) != 0) return;
            if (!PainLooks.TryGet(___pawn, out PainLook look)) return;
            if (look.facing.IsValid) __result.facing = look.facing;
            if (look.lying) __result.posture = PawnPosture.LayingOnGroundNormal;
            if (look.angle != 0f) __result.matrix *= Matrix4x4.Rotate(Quaternion.AngleAxis(look.angle, Vector3.up));
        }
    }

    /// <summary>Zoomed out the game blits a cached frame that ignores the matrix: a turned body is drawn live.</summary>
    [HarmonyPatch(typeof(PawnRenderer), "ParallelGetPreRenderResults")]
    static class Patch_PawnRenderer_PainUncached
    {
        static void Prefix(Pawn ___pawn, ref bool disableCache)
        {
            if (PainLooks.TryGet(___pawn, out PainLook look) && look.ChangesBody) disableCache = true;
        }
    }

    /// <summary>A pulled pawn is drawn along the pull. Runs last, so it wins over Gravity Well's own draw point.</summary>
    [HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.DrawPos), MethodType.Getter)]
    static class Patch_PawnDrawTracker_PainLook
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix(Pawn ___pawn, ref Vector3 __result)
        {
            if (!PainLooks.TryGet(___pawn, out PainLook look) || !look.moved) return;
            __result.x = look.drawAt.x;
            __result.z = look.drawAt.z;
        }
    }
}
