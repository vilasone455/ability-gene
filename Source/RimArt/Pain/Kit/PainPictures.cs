using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Where Pain's casts and rods hand over to the ported pictures (Source/RimArt/Pain/*Graphics.cs): live positions
    /// in, nothing kept between frames.
    /// </summary>
    public static class PainPictures
    {
        /// <summary>The sketches draw a height h cells up as h x Lift cells north.</summary>
        public const float Lift = 0.6f;

        /// <summary>
        /// Banshō Ten'in (BanshoGraphics, the port of pain-bansho-tenin.js): before the grip the aim and distance are
        /// live, from Pain to the target; from the grip they are the cast's. While the target is carried its ground point
        /// and height are the cast's pull (what the real pawn is drawn at, <see cref="BanshoCast.Pose"/>); once it is down,
        /// where it lies. The lab's stand-ins are not drawn: the real pawns are.
        /// </summary>
        public static void Bansho(BanshoCast cast, float s)
        {
            Pawn caster = cast.caster, target = cast.target;
            if (cast.aborted || caster == null || target == null || !caster.Spawned || !target.Spawned) return;
            Vector2 pain = PainKit.Ground(caster.DrawPos);
            var view = new BanshoView { pain = pain, standIns = false };
            if (cast.Fired)
            {
                view.castAt = Ground(cast.origin);
                view.aim = new Vector2(cast.aim.x, cast.aim.z);
                view.times = cast.Times;
                if (!cast.landed || s < cast.Down)
                {
                    view.target = Ground(cast.CentreAt(cast.AlongAt(s)));
                    view.height = cast.HeightAt(s);
                }
                else view.target = PainKit.Ground(target.DrawPos);
                if (cast.blocker != null && cast.blocker.Spawned) view.block = PainKit.Ground(cast.blocker.DrawPos);
            }
            else
            {
                Vector3 to = target.Position.ToVector3Shifted() - caster.Position.ToVector3Shifted();
                to.y = 0f;
                float distance = to.magnitude;
                view.castAt = pain;
                view.aim = distance > 0.001f ? new Vector2(to.x, to.z) / distance : Vector2.up;
                view.times = BanshoCast.TimesFor(distance, target.BodySize > PainKit.BanshoProps.heavyBodySize, false, 0f);
                view.target = PainKit.Ground(target.DrawPos);
            }
            BanshoGraphics.Draw(in view, s, cast.home);
        }

        /// <summary>A cell-space point (cell centres) as the sketches' ground point: the feet, 0.3 south of the centre.</summary>
        public static Vector2 Ground(Vector3 centre) => new Vector2(centre.x, centre.z - VergilKit.FeetBelowDrawPos);

        /// <summary>
        /// A pawn lying on the floor as the sketches' ground point: PainGraphics.Lying draws the body 0.08 north of it, and
        /// the game draws a lying body centred on its DrawPos.
        /// </summary>
        public static Vector2 LyingGround(Vector3 drawPos) => new Vector2(drawPos.x, drawPos.z - LyingBodyZ);
        public const float LyingBodyZ = 0.08f;

        /// <summary>A camera shake from the sketch, only when it happens on the map on screen.</summary>
        public static void Shake(Map map, float size)
        {
            if (map != null && Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(size);
        }

        private static float Degrees(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        /// <summary>
        /// One Black Receiver cast (BlackReceiverGraphics, the port of pain-black-receiver.js): the arm, the rod growing
        /// in the palm and the flick (Throw), then the rod in flight between Pain's hand and the target's live feet. A stab
        /// into a pawn lying on the floor is the sketch's stab from above instead: the hand comes over the body and drives
        /// the rod down, in at the fire tick. The rod in the pawn is drawn by <see cref="Rods"/> from then on.
        /// </summary>
        public static void Receiver(BlackReceiverCast cast, float s)
        {
            Pawn caster = cast.caster, target = cast.target;
            if (caster == null || !caster.Spawned) return;
            Map map = caster.Map;
            Vector2 pain = PainKit.Ground(caster.DrawPos);
            float sinceStart = s - BlackReceiverCast.LeadTime, sinceRelease = cast.Fired ? s - cast.FireSeconds : -1f;
            int index = Mathf.Min(PainRods.Count(target), BlackReceiverTiming.MostRods - 1);
            if (cast.StabsLying() && target != null && target.Spawned)
            {
                Vector2 body = LyingGround(target.DrawPos), head;
                if (PainRods.Pinned(target)) head = PainRods.Of(target).fallAway;
                else if (!(GameComponent_Pain.Instance?.FaceDown(target, out head) ?? false)) head = BlackReceiverGraphics.ToPain(pain, body);
                if (cast.Fired) index = Mathf.Max(0, cast.hit == target ? RodSlot(target, cast.landTick) : index);
                BlackReceiverGraphics.Stab(pain, body, head, index, s - cast.FireSeconds + BlackReceiverTiming.StabIn, map);
                return;
            }
            Vector3 aim = cast.Aim;
            BlackReceiverGraphics.Throw(pain, Degrees(new Vector2(aim.x, aim.z)), sinceStart, sinceRelease, map);
            if (cast.Fired && !cast.done && target != null && target.Spawned && target.Map == map)
            {
                Vector3 goal = target.DrawPos;
                float gone = (cast.rod - cast.from).Yto0().magnitude, left = (goal - cast.rod).Yto0().magnitude;
                BlackReceiverGraphics.Flying(pain, PainKit.Ground(goal), gone / Mathf.Max(0.01f, gone + left), index, map);
            }
        }

        /// <summary>The slot of the rod that landed on <paramref name="tick"/>, or 0.</summary>
        private static int RodSlot(Pawn pawn, int tick)
        {
            Hediff_PainRods rods = PainRods.Of(pawn);
            if (rods == null) return 0;
            foreach (PainRod rod in rods.rods) if (rod.landTick == tick) return rod.slot;
            return 0;
        }

        /// <summary>
        /// The rods in one pawn: standing, they point back the way they came with the tip out of its back; pinned, it
        /// lies on its back and they stand upright (turning over as it falls and back as it gets up); face-down after a
        /// Banshō slam, they lean toward Pain. The chakra spot runs down each; they flare after something tried to move
        /// a pinned pawn.
        /// </summary>
        public static void Rods(Hediff_PainRods rods)
        {
            Pawn pawn = rods.pawn;
            if (pawn == null || !pawn.Spawned || rods.rods.Count == 0) return;
            Map map = pawn.Map;
            float flare = rods.flaredTick >= 0 ? BlackReceiverGraphics.Flare(UbwClock.Since(rods.flaredTick)) : 0f;
            bool faceDown = GameComponent_Pain.Instance.FaceDown(pawn, out Vector2 toPain);
            Vector2 stood = rods.StoodAt, lies = rods.LiesAt, feet = PainKit.Ground(pawn.DrawPos), away = rods.fallAway;
            float sinceFree = rods.freedTick >= 0 ? UbwClock.Since(rods.freedTick) : float.MaxValue;
            foreach (PainRod rod in rods.rods)
            {
                float age = UbwClock.Since(rod.landTick);
                int i = rod.slot;
                if (rods.Pinned)
                {
                    float since = UbwClock.Since(rods.pinnedTick);
                    if (since < BlackReceiverTiming.Fall)
                        BlackReceiverGraphics.Stuck(BlackReceiverTiming.FallPose(i, stood, lies, -away, since), i, age, -1f, age, flare, map);
                    else BlackReceiverGraphics.InLying(lies, away, i, age, -1f, map, flare);
                }
                else if (sinceFree < BlackReceiverTiming.GetUp)
                    BlackReceiverGraphics.Stuck(BlackReceiverTiming.RisePose(i, lies, stood, -away, sinceFree), i, age, -1f, age, 0f, map);
                else if (faceDown) BlackReceiverGraphics.InLying(LyingGround(pawn.DrawPos), toPain, i, age, -1f, map, 0f, toPain);
                else BlackReceiverGraphics.InStanding(feet, rod.toward, i, age, -1f, map);
            }
        }

        /// <summary>What Black Receiver leaves behind, by kind; <paramref name="age"/> is seconds since its tick.</summary>
        public static void Mark(PainMark mark, float age)
        {
            Map map = mark.map;
            switch (mark.kind)
            {
                case PainMarkKind.Hole:
                    BlackReceiverGraphics.Hole(mark.at, age, mark.seed, map);
                    break;
                case PainMarkKind.PinDust:
                    BlackReceiverGraphics.PinDust(mark.at, age, map);
                    break;
                case PainMarkKind.AllBreak:
                    BlackReceiverGraphics.AllBreak(mark.at, age, map);
                    break;
                case PainMarkKind.Broken:
                    if (age >= BlackReceiverTiming.BreakLinger) return;
                    // Long past its hit, so no ripple; whole until its crumble starts (the stagger), then eaten away.
                    if (mark.lying) BlackReceiverGraphics.InLying(mark.at, mark.head, mark.index, 99f, age, map, 0f, mark.lean);
                    else
                    {
                        Vector2 at = mark.pawn != null && mark.pawn.Spawned && mark.pawn.Map == map ? PainKit.Ground(mark.pawn.DrawPos) : mark.at;
                        BlackReceiverGraphics.InStanding(at, mark.toward, mark.index, 99f, age, map);
                    }
                    break;
            }
        }
    }
}
