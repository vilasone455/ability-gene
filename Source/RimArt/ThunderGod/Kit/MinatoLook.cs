using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// How one pawn looks this frame while a Minato cast touches it: Minato narrowing into a sliver as he jumps
    /// out and widening back out of one where he lands (the sketches narrow their stand-in the same way), the
    /// Rasengan's lean, or a pawn the Rasengan grinds (pale) and throws (drawn along the throw while its cell
    /// changes once, on landing). Rebuilt once a frame by <see cref="GameComponent_Minato"/>, the way Vergil's
    /// looks are (VergilLook.cs), and read while pawns are drawn.
    /// </summary>
    public sealed class MinatoLook
    {
        /// <summary>0 to 1: 1 is a sliver, 8 % as wide and 1.8 times as tall, standing on the same feet.</summary>
        public float thin;
        /// <summary>Drawn at <see cref="drawAt"/> (x and z) instead of where the game puts it.</summary>
        public bool moved;
        public Vector3 drawAt;
        /// <summary>0 to 1 toward white: a body the Rasengan grinds.</summary>
        public float pale;

        public void Reset()
        {
            thin = pale = 0f;
            moved = false;
            drawAt = Vector3.zero;
        }

        public bool ChangesBody => thin > 0f || pale > 0f;

        /// <summary>How far below the pawn's centre its feet are (VergilLook.Feet): the sliver stands on them.</summary>
        public const float Feet = -0.33f;
        public float Width => 1f - thin * 0.92f;
        public float Height => 1f + thin * 0.8f;
    }

    public static class MinatoLooks
    {
        private static readonly Dictionary<Pawn, MinatoLook> live = new Dictionary<Pawn, MinatoLook>();
        private static readonly List<MinatoLook> spare = new List<MinatoLook>();

        public static bool TryGet(Pawn pawn, out MinatoLook look)
        {
            look = null;
            return live.Count > 0 && pawn != null && live.TryGetValue(pawn, out look);
        }

        public static void Clear()
        {
            foreach (MinatoLook look in live.Values) spare.Add(look);
            live.Clear();
        }

        public static MinatoLook For(Pawn pawn)
        {
            if (live.TryGetValue(pawn, out MinatoLook look)) return look;
            if (spare.Count > 0)
            {
                look = spare[spare.Count - 1];
                spare.RemoveAt(spare.Count - 1);
            }
            else look = new MinatoLook();
            look.Reset();
            live[pawn] = look;
            return look;
        }

        /// <summary>The narrowing, keeping the thinnest asked for this frame.</summary>
        public static void Thin(Pawn pawn, float thin)
        {
            if (pawn == null || thin <= 0f) return;
            MinatoLook look = For(pawn);
            look.thin = Mathf.Max(look.thin, Mathf.Clamp01(thin));
        }
    }

    /// <summary>
    /// The sliver and the pale: the root matrix and tint of the whole render tree, the hooks Vergil's pose uses
    /// (Patches_VergilPose.cs).
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
    static class Patch_PawnRenderer_MinatoLook
    {
        static void Postfix(Pawn ___pawn, ref PawnDrawParms __result)
        {
            if ((__result.flags & (PawnRenderFlags.Portrait | PawnRenderFlags.Cache)) != 0) return;
            if (!MinatoLooks.TryGet(___pawn, out MinatoLook look)) return;
            if (look.pale > 0f)
            {
                Color was = __result.tint, to = Color.Lerp(was, Color.white, look.pale);
                __result.tint = new Color(to.r, to.g, to.b, was.a);
            }
            if (look.thin > 0.001f)
            {
                float height = look.Height;
                __result.matrix *= Matrix4x4.TRS(new Vector3(0f, 0f, MinatoLook.Feet * (1f - height)), Quaternion.identity,
                    new Vector3(look.Width, 1f, height));
            }
        }
    }

    /// <summary>Zoomed out the game blits a cached frame that ignores the matrix and tint: a changed body is drawn live.</summary>
    [HarmonyPatch(typeof(PawnRenderer), "ParallelGetPreRenderResults")]
    static class Patch_PawnRenderer_MinatoUncached
    {
        static void Prefix(Pawn ___pawn, ref bool disableCache)
        {
            if (MinatoLooks.TryGet(___pawn, out MinatoLook look) && look.ChangesBody) disableCache = true;
        }
    }

    /// <summary>A thrown pawn is drawn along the throw; its cell changes once, when it lands (the RetrievalPull pattern).</summary>
    [HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.DrawPos), MethodType.Getter)]
    static class Patch_PawnDrawTracker_MinatoLook
    {
        static void Postfix(Pawn ___pawn, ref Vector3 __result)
        {
            if (!MinatoLooks.TryGet(___pawn, out MinatoLook look) || !look.moved) return;
            __result.x = look.drawAt.x;
            __result.z = look.drawAt.z;
        }
    }
}
