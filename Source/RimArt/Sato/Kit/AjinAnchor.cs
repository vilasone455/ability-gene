using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// One of Satō's pieces on the floor: an anchor Sever threw (AG_AjinAnchor), or the remains where his body
    /// was destroyed and no anchor could take him (AG_AjinRemains, piece Body). While his body is gone the piece
    /// holds him (the pawn sits in its container, off the map) until the Reset ends. Anchors rot; remains go
    /// when he rises. Drawn in code: the piece, its ring, and the Reset picture while it holds him.
    /// </summary>
    public class AjinAnchor : ThingWithComps, IThingHolder
    {
        public Pawn owner;
        public AjinPiece piece;
        public int madeTick;
        /// <summary>Ticks after madeTick when it rots; 0 never (remains).</summary>
        public int rotTicks;
        public float angle;
        /// <summary>The throw: from where, and the tick it lands. Before it lands it is drawn in flight.</summary>
        public Vector3 thrownFrom;
        public int landTick;
        public int crumbleTick = -1;
        private ThingOwner<Pawn> held;

        public const int CrumbleTicks = 30;

        public AjinAnchor() { held = new ThingOwner<Pawn>(this, oneStackOnly: true); }

        public Pawn HeldPawn => held.Count > 0 ? held[0] : null;
        public bool Flying => Find.TickManager.TicksGame < landTick;
        public bool Crumbling => crumbleTick >= 0;
        public string PieceLabel => ("AG_AjinPiece_" + piece).Translate().Resolve();

        public static AjinAnchor MakeRemains(Pawn pawn, IntVec3 spot, Map map)
        {
            var remains = (AjinAnchor)ThingMaker.MakeThing(SatoDefOf.AG_AjinRemains);
            remains.owner = pawn;
            remains.piece = AjinPiece.Body;
            remains.madeTick = Find.TickManager.TicksGame;
            remains.angle = Rand.Range(0f, 360f);
            GenSpawn.Spawn(remains, spot, map);
            return remains;
        }

        public void Hold(Pawn pawn)
        {
            if (pawn.Spawned) pawn.DeSpawn(DestroyMode.Vanish);
            if (pawn.holdingOwner != null) pawn.holdingOwner.Remove(pawn);
            held.TryAdd(pawn);
        }

        public void Release(Pawn pawn) => held.Remove(pawn);

        /// <summary>Tests only: the held pawn is thrown away with the piece, so no remains take over.</summary>
        internal void DiscardForTests()
        {
            Pawn pawn = HeldPawn;
            if (pawn != null)
            {
                held.Remove(pawn);
                pawn.Destroy(DestroyMode.Vanish);
            }
            if (!Destroyed) Destroy(DestroyMode.Vanish);
        }

        /// <summary>He rose here: an anchor is taken into the new body, remains are left behind as filth.</summary>
        public void UsedUp()
        {
            if (!Destroyed) Destroy(DestroyMode.Vanish);
        }

        /// <summary>Turns to dust: a short picture, then it is gone. Never while it holds him.</summary>
        public void Crumble()
        {
            if (Crumbling || HeldPawn != null || Destroyed) return;
            crumbleTick = Find.TickManager.TicksGame;
        }

        protected override void Tick()
        {
            base.Tick();
            int now = Find.TickManager.TicksGame;
            if (Crumbling)
            {
                if (now - crumbleTick >= CrumbleTicks) Destroy(DestroyMode.Vanish);
                return;
            }
            // An anchor whose owner is gone for good (or no longer an Ajin) has nothing to hold together.
            if (owner == null || owner.Discarded || !AjinReset.IsAjin(owner)) { if (HeldPawn == null) Crumble(); return; }
            if (HeldPawn == null && rotTicks > 0 && now - madeTick >= rotTicks) Crumble();
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Pawn pawn = HeldPawn;
            Map map = Map;
            IntVec3 spot = Position;
            if (pawn != null) held.Remove(pawn);
            base.Destroy(mode);
            if (pawn != null) AjinReset.HolderLost(pawn, this, spot, map);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            SatoPictures.DrawPiece(this, drawLoc);
        }

        public override string GetInspectString()
        {
            var lines = new List<string>();
            string baseText = base.GetInspectString();
            if (!baseText.NullOrEmpty()) lines.Add(baseText);
            if (owner != null) lines.Add("AG_AjinPieceOf".Translate(owner.LabelShortCap, PieceLabel).Resolve());
            Hediff_AjinReset reset = AjinReset.Resetting(HeldPawn);
            if (reset != null) lines.Add("AG_AjinRising".Translate(reset.TicksLeft.ToStringTicksToPeriod()).Resolve());
            else if (rotTicks > 0) lines.Add("AG_AjinRotsIn".Translate((madeTick + rotTicks - Find.TickManager.TicksGame).ToStringTicksToPeriod()).Resolve());
            return string.Join("\n", lines);
        }

        public ThingOwner GetDirectlyHeldThings() => held;

        public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref owner, "owner");
            Scribe_Values.Look(ref piece, "piece");
            Scribe_Values.Look(ref madeTick, "madeTick");
            Scribe_Values.Look(ref rotTicks, "rotTicks");
            Scribe_Values.Look(ref angle, "angle");
            Scribe_Values.Look(ref thrownFrom, "thrownFrom");
            Scribe_Values.Look(ref landTick, "landTick");
            Scribe_Values.Look(ref crumbleTick, "crumbleTick", -1);
            Scribe_Deep.Look(ref held, "held", this);
        }
    }

    /// <summary>A limb the Black Ghost tore off, lying where it landed; gone after a day.</summary>
    public class TornLimb : Thing
    {
        public bool arm;
        public float angle;
        /// <summary>Where it landed exactly (the picture's landing point); its cell is this rounded.</summary>
        public Vector3 exact;
        public int madeTick;
        public const int LastTicks = 60000;

        public override void TickRare()
        {
            // Thing.TickRare itself throws; nothing to call.
            if (Find.TickManager.TicksGame - madeTick > LastTicks) Destroy(DestroyMode.Vanish);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false) => SatoPictures.DrawTornLimb(this, drawLoc);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref arm, "arm");
            Scribe_Values.Look(ref angle, "angle");
            Scribe_Values.Look(ref exact, "exact");
            Scribe_Values.Look(ref madeTick, "madeTick");
        }
    }
}
