using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Timing of the Frost Gun's normal shot as the picture draws it: seconds in, numbers out, no
    /// drawing and no map. There is no sketch: these constants are the look designed in C# and checked
    /// in the lab's recordings. In game the bolt is the gun's real projectile and flies at its def's
    /// speed; only the preview uses <see cref="BoltSpeed"/>.
    /// </summary>
    public static class FrostGunShotTiming
    {
        /// <summary>Cells a second: the projectile's speed 70 (0.7 cells a tick).</summary>
        public const float BoltSpeed = 42f;
        /// <summary>The trail behind the bolt at most, cells; a frost fleck is left every <see cref="FleckStep"/> cells and lasts <see cref="FleckLife"/>.</summary>
        public const float TrailLength = 1.3f, FleckStep = 0.3f, FleckLife = 0.35f;
        /// <summary>The hit: the frost puff, the ice flecks thrown off, the rime spot left on the floor.</summary>
        public const float PuffLife = 0.45f, ChipLife = 0.9f, RimeLife = 2.5f;
        public const float HitShake = 0.01f;

        // The preview's script: three shots at one pawn, each adding a Chilled stack.
        public const float ScriptDistance = 7f, ScriptFirst = 0.2f, ScriptInterval = 1.1f, ScriptHold = 1.2f;
        public const int ScriptShots = 3;

        public static float Flight(float distance) => Mathf.Max(0.05f, distance / BoltSpeed);
        public static float Fire(int shot) => ScriptFirst + shot * ScriptInterval;
        public static float Hit(int shot, float distance) => Fire(shot) + Flight(distance);
        public static float ScriptEnd => Hit(ScriptShots - 1, ScriptDistance) + ScriptHold;
    }

    /// <summary>
    /// One Flash Freeze as the picture needs it. <see cref="Muzzle"/> is where the beam leaves (the
    /// held gun's muzzle), <see cref="Target"/> the frozen pawn's ground point when the beam hit. The
    /// clock starts when the warmup starts.
    /// </summary>
    public struct FrostFreezeShot
    {
        public Vector2 Muzzle, Target;
        /// <summary>Seconds of warmup (the charge at the muzzle) and seconds the pawn stays frozen.</summary>
        public float Warmup, Freeze;
        /// <summary>Picture seconds the ice was shattered at; below 0 it was not, and it thaws at <see cref="FrostGunFreezeTiming.ThawAt"/>.</summary>
        public float ShatterAt;
        /// <summary>The beam found nobody to freeze (the target died or left): no ice, only the beam and the frost on the floor.</summary>
        public bool Missed;
        /// <summary>Where the frozen pawn is now, if it moved (a push); null keeps <see cref="Target"/>.</summary>
        public Vector2? LiveTarget;
        public int Seed;
    }

    /// <summary>
    /// Timing of Flash Freeze: seconds in, numbers out, no drawing and no map. The clock starts with
    /// the warmup. The beam leaves at the end of the warmup and crosses at <see cref="BeamSpeed"/>; the
    /// freeze lands when it arrives (<see cref="Hit"/>), the ice rises over <see cref="Grow"/> and
    /// frost spreads on the floor over <see cref="StainGrow"/>. The ice then either shatters (at the
    /// shot's ShatterAt) or thaws at the end of the freeze.
    /// </summary>
    public static class FrostGunFreezeTiming
    {
        public const float BeamSpeed = 60f, BeamLinger = 0.22f;
        public const float StainGrow = 0.45f, StainHold = 1.5f, StainFade = 1f;
        public const float Grow = 0.4f, SpikeDelay = 0.12f;
        /// <summary>The shatter: white flash, shards flying, shards lying and melting.</summary>
        public const float Flash = 0.12f, ShardMelt = 1.6f;
        /// <summary>The thaw: the ice shrinks and drips, and the water it leaves stays, then dries.</summary>
        public const float Thaw = 1f, PuddleHold = 2f, PuddleFade = 1f;
        /// <summary>The caster is held this long after the beam lands, so the gun stays on the target while the beam fades.</summary>
        public const float HoldAfterHit = 0.15f;
        public const float HitShake = 0.02f, ShatterShake = 0.03f;

        // The preview's script: the ability's default warmup and freeze; the shatter entry breaks the ice this long after it formed.
        public const float ScriptDistance = 6f, ScriptWarmup = 0.8f, ScriptFreeze = 5f, ScriptShatterAfter = 1.6f;

        public static float Flight(float distance) => Mathf.Max(0.04f, distance / BeamSpeed);
        public static float Hit(float warmup, float distance) => warmup + Flight(distance);
        public static float Hit(in FrostFreezeShot shot) => Hit(shot.Warmup, (shot.Target - shot.Muzzle).magnitude);
        public static float ThawAt(in FrostFreezeShot shot) => Hit(shot) + shot.Freeze;
        /// <summary>When the ice is gone: shattered, thawed, or never formed.</summary>
        public static float IceEnd(in FrostFreezeShot shot) =>
            shot.Missed ? Hit(shot) : shot.ShatterAt >= 0f ? shot.ShatterAt : ThawAt(shot) + Thaw;
        public static float End(in FrostFreezeShot shot)
        {
            float stain = IceEnd(shot) + StainHold + StainFade;
            if (shot.Missed) return Mathf.Max(stain, Hit(shot) + StainGrow + StainHold + StainFade);
            if (shot.ShatterAt >= 0f) return Mathf.Max(stain, shot.ShatterAt + FrostGunGraphics.ShardLanding + ShardMelt + 0.2f);
            return Mathf.Max(stain, ThawAt(shot) + Thaw + PuddleHold + PuddleFade);
        }
    }
}
