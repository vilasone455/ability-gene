using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// How one pawn looks this frame while a Vergil cast touches it: Vergil's own pose (the hand on the
    /// hilt, the blade out, the crouch, the kneel, gone) or a pawn his cast has marked (a tint). The lab
    /// sketches draw all of this on a stand-in (lib/vergil.js carrier, heldKatana, the marked tint);
    /// in game it is drawn on the real pawn by <see cref="YamatoDraw"/> and the render patches in
    /// Patches_VergilPose.cs.
    /// </summary>
    public sealed class VergilLook
    {
        /// <summary>0 the right hand is down (not drawn), 1 it is on Yamato's grip.</summary>
        public float hand;
        /// <summary>0 the blade is in the scabbard at the hip, 1 it is fully out along <see cref="aim"/>.</summary>
        public float blade;
        /// <summary>Where the drawn blade points, degrees (0 east, 90 north).</summary>
        public float aim;
        /// <summary>0 to 1: the drawn blade's edge lit white.</summary>
        public float hot;
        /// <summary>0 to 1: the body sinks a little (the dash's prepare).</summary>
        public float crouch;
        /// <summary>0 to 1: the Judgement Cut End kneel, one knee down, facing the camera.</summary>
        public float kneel;
        /// <summary>While kneeling: 0 the blade is drawn above the upright scabbard, 1 it is home.</summary>
        public float sheathe;
        /// <summary>
        /// While kneeling facing south in a costume with a kneel picture (Vergil's coat on a Thin body): that
        /// picture. The body is then hidden and the head only moved down, nothing is squashed. Null otherwise,
        /// and the kneel squashes the pawn instead (with the head scaled back).
        /// </summary>
        public Graphic kneelPicture;
        /// <summary>Not drawn at all: Judgement Cut End's vanish.</summary>
        public bool gone;
        public Color tint = Color.white;
        public float tintAmount;

        public void Reset()
        {
            hand = blade = aim = hot = crouch = kneel = sheathe = tintAmount = 0f;
            gone = false;
            kneelPicture = null;
            tint = Color.white;
        }

        /// <summary>The body is drawn differently from a plain standing pawn: the atlas cache must be skipped.</summary>
        public bool ChangesBody => crouch > 0f || kneel > 0f || tintAmount > 0f;

        /// <summary>How far below the pawn's centre its feet are: the crouch and the kneel squash toward here.</summary>
        public const float Feet = -0.33f;
        public const float CrouchSquash = 0.1f, KneelSquash = 0.24f, KneelWiden = 0.06f;
        /// <summary>
        /// How far the head sits lower in the kneel picture: make_costume_textures.py's KNEEL_DROP, 14 of the
        /// sheet's 128 units over 1.5 cells.
        /// </summary>
        public const float KneelDrop = 14f * 1.5f / 128f;

        /// <summary>The height the whole pawn is drawn at, 1 unsquashed. The kneel picture needs no squash.</summary>
        public float Squash => 1f - CrouchSquash * crouch - (kneelPicture != null ? 0f : KneelSquash * kneel);
        public float Widen => 1f + (kneelPicture != null ? 0f : KneelWiden * kneel);
    }

    /// <summary>
    /// This frame's looks. Rebuilt on the main thread once a frame by <see cref="GameComponent_Vergil"/>
    /// and only read while pawns are drawn, which the game does partly on worker threads; nothing writes
    /// the table while they read it.
    /// </summary>
    public static class VergilLooks
    {
        private static readonly Dictionary<Pawn, VergilLook> live = new Dictionary<Pawn, VergilLook>();
        private static readonly List<VergilLook> spare = new List<VergilLook>();

        public static int Count => live.Count;

        public static bool TryGet(Pawn pawn, out VergilLook look)
        {
            look = null;
            return live.Count > 0 && pawn != null && live.TryGetValue(pawn, out look);
        }

        /// <summary>Starts a new frame: every look goes back to the pool.</summary>
        public static void Clear()
        {
            foreach (VergilLook look in live.Values) spare.Add(look);
            live.Clear();
        }

        /// <summary>The look to fill for this pawn this frame.</summary>
        public static VergilLook For(Pawn pawn)
        {
            if (live.TryGetValue(pawn, out VergilLook look)) return look;
            if (spare.Count > 0)
            {
                look = spare[spare.Count - 1];
                spare.RemoveAt(spare.Count - 1);
            }
            else look = new VergilLook();
            look.Reset();
            live[pawn] = look;
            return look;
        }

        /// <summary>The pawns kneeling in a picture this frame.</summary>
        public static void KneelPictured(HashSet<Pawn> into)
        {
            into.Clear();
            foreach (KeyValuePair<Pawn, VergilLook> entry in live)
                if (entry.Value.kneelPicture != null) into.Add(entry.Key);
        }

        /// <summary>A marked pawn: tinted toward <paramref name="colour"/>, keeping the strongest tint asked for this frame.</summary>
        public static void Tint(Pawn pawn, Color colour, float amount)
        {
            if (pawn == null || amount <= 0f) return;
            VergilLook look = For(pawn);
            if (amount <= look.tintAmount) return;
            look.tint = colour;
            look.tintAmount = amount;
        }
    }
}
