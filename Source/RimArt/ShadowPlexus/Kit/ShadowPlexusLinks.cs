using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// What <see cref="MapComponent_ShadowPlexus"/> keeps per cast. Each record is known from the
    /// start of its warmup (fireTick -1 until the ability fires) and lives on through its picture's
    /// tail after it ends. Seconds are counted from startTick, the warmup's start, so the drawing's
    /// clock matches the sketches: the line runs out during the warmup and holds at the fire.
    /// </summary>
    public abstract class ShadowCast
    {
        public Pawn caster;
        public int startTick, fireTick = -1;
        /// <summary>The warmup, in seconds: the drawing's cast time.</summary>
        public float cast;
        public IntVec3 lastCasterCell;

        public bool Landed => fireTick >= 0;
        public float Seconds(int now) => (now - startTick) / 60f;
    }

    /// <summary>Shadow imitation: one pawn held, moved a cell with every step the caster takes.</summary>
    public sealed class ShadowHold : ShadowCast
    {
        public Pawn target;
        public CompProperties_ShadowImitation props;
        public int endTick, releaseTick = -1;
        public ImitationEnd end;
        public float cutShare;
        /// <summary>The target has been dragged at least once, so its path follower is told at the release.</summary>
        public bool moved;

        public bool Holding => Landed && releaseTick < 0;
    }

    /// <summary>Shadow seam: two things that cannot get more than a few cells apart.</summary>
    public sealed class ShadowSeamLink : ShadowCast
    {
        public Thing a, b;
        public CompProperties_ShadowSeam props;
        public int endTick, tautTick = -1, undoTick = -1;
        public IntVec3 lastA, lastB;

        public bool Sewn => Landed && undoTick < 0;
        public bool Holds(Thing thing) => Sewn && (thing == a || thing == b);
        public Thing Other(Thing thing) => thing == a ? b : a;
    }

    /// <summary>Shadow grasp: a thing sliding cell by cell along a path.</summary>
    public sealed class ShadowGraspSlide : ShadowCast
    {
        public Thing thing;
        public CompProperties_ShadowGrasp props;
        public List<IntVec3> path;
        /// <summary>The last cell the thing reaches (a pawn on the path stops it early), and where it is now.</summary>
        public int stopIndex, index;
        public int ticksPerCell, nextStepTick = -1, arriveTick = -1;
        public Vector2 origin, item, dest;
        public float aim, turn, handSize;

        public bool Sliding => Landed && arriveTick < 0;
    }

    /// <summary>Shadow double: the caster's shadow standing on a lit cell, the origin of the other abilities.</summary>
    public sealed class ShadowDoubleRecord : ShadowCast
    {
        public IntVec3 cell;
        public CompProperties_ShadowDouble props;
        public int endTick, goneTick = -1;
        public DoubleEnd end;
        public float aim;

        /// <summary>The double is up and can be cast from: risen and not gone.</summary>
        public bool Stands(int now) => Landed && goneTick < 0 && Seconds(now) >= cast + ShadowDoubleTiming.Rise;
        public bool Out => Landed && goneTick < 0;
    }

    /// <summary>Shadow neck bind: the hands on a pawn held by Imitation or Seam.</summary>
    public sealed class ShadowNeckBindCast : ShadowCast
    {
        public Pawn target;
        public CompProperties_ShadowNeckBind props;
        public ShadowHold hold;
        public ShadowSeamLink seam;
        public int releaseTick = -1;
        public bool cut;
        public float cutShare = 0.5f;
        /// <summary>True when the bind draws only its hands: the seam, or the hold with its line intact, draws the line.</summary>
        public bool hideLine;
        public Hediff choked;

        public bool Choking => Landed && releaseTick < 0;
        public float Closed => ShadowNeckBindTiming.Crawl + props.climbSeconds;
    }
}
