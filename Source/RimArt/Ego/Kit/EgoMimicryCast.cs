using System.Collections.Generic;
using UnityEngine;
using Verse;
using T = RimArt.EgoMimicryTiming;

namespace RimArt
{
    /// <summary>
    /// One Mimicry swing from its start to the end of its picture, the Vergil cast shape (<see cref="VergilCast"/>): it counts
    /// whole ticks for the rules and the picture reads the same ticks smoothed. A corroded swing may open with a lunge of
    /// <see cref="T.LungeTime"/> s (the pawn drawn along the line by <see cref="PawnDash"/>, its cell changed once on arrival);
    /// the swing starts at <see cref="swingTick"/> and lands at its contact frame (<see cref="T.HitAt"/>, the grown swing's
    /// slam at <see cref="T.SlamAt"/>), once: <see cref="resolved"/> is saved, so a load never lands it again.
    ///
    /// Broken off before it lands, with no damage and no roll, when the wielder dies, goes down, is stunned or leaves the
    /// map, when the sword leaves its hands, or when the corrosion or Overclock it belongs to ends.
    /// </summary>
    public sealed class EgoMimicryCast : IExposable
    {
        public Pawn wielder;
        public ThingWithComps weapon;
        public Thing target;
        public Map home;
        public EgoMimicrySource source;
        public bool grown, surprise;
        /// <summary>Degrees, 0 east, 90 north: toward the target from where the swing starts.</summary>
        public float aim;
        /// <summary>The lunge's first tick, -1 for a swing with no lunge; it ends at <see cref="swingTick"/>.</summary>
        public int lungeTick = -1;
        public IntVec3 lungeFrom, lungeTo;
        public int swingTick;
        public bool arrived, resolved, cancelled;
        /// <summary>The wielder's melee cooldown has been stretched to the grown swing's length.</summary>
        public bool cooled;

        /// <summary>What each thing the swing struck took, in order (pictures and tests; not saved).</summary>
        public readonly List<KeyValuePair<Thing, float>> struck = new List<KeyValuePair<Thing, float>>();
        /// <summary>Whether the ordinary swing's hit landed (missed or dodged: false). Not saved.</summary>
        public bool landed;
        /// <summary>The arm's stage the swing's damage was scaled by: the stage before its hit. Not saved.</summary>
        public int stageAtContact;

        public static int Ticks(float seconds) => Mathf.RoundToInt(seconds * 60f);

        public bool Lunges => lungeTick >= 0;
        public int ContactTick => swingTick + Ticks(grown ? T.SlamAt : T.HitAt);
        /// <summary>When the wielder may swing again: the grown swing's blade has shrunk back (1.4 s), an ordinary swing is over.</summary>
        public int RecoverTick => swingTick + Ticks(grown ? T.ShrunkAt : T.SwingLength);
        public int EndTick => swingTick + Ticks(grown ? T.GrownLength : T.SwingLength);
        public CompEgoMimicry Sword => weapon?.GetComp<CompEgoMimicry>();

        /// <summary>The mirror the picture and the strip use: the facing the aim turns the wielder to.</summary>
        public float Sign => T.SignOf(T.FacingOf(aim));

        /// <summary>One game tick. False once the swing is over and its picture has gone, or it was broken off.</summary>
        public bool Tick(int now)
        {
            if (cancelled) return false;
            // The swinging wielder faces its swing. The corroded hold and Overclock jobs handle facing themselves, so Core
            // would not turn the pawn to its target, and the picture's arm is placed for the pawn's facing.
            if (now < RecoverTick && wielder != null && wielder.Spawned && !wielder.Dead) wielder.Rotation = T.FacingOf(aim);
            if (!resolved)
            {
                if (Broken())
                {
                    Cancel();
                    return false;
                }
                if (Lunges && !arrived)
                {
                    if (now < swingTick) PawnDash.Keep(wielder, lungeFrom, lungeTo, lungeTick, swingTick - lungeTick, eased: true);
                    else Arrive();
                }
                if (grown && !cooled) Cool(now);
                if (now >= ContactTick) Resolve();
            }
            return now < EndTick;
        }

        /// <summary>The swing can no longer land: see the class summary.</summary>
        private bool Broken()
        {
            if (wielder == null || weapon == null || wielder.Dead || wielder.Downed || !wielder.Spawned || wielder.Map != home) return true;
            if (wielder.equipment?.Primary != weapon || Stunned(wielder)) return true;
            CompEgoMimicry sword = Sword;
            if (sword == null) return true;
            switch (source)
            {
                case EgoMimicrySource.Corroded: return !(wielder.MentalState is MentalState_EgoCorroded state && state.weapon == weapon);
                case EgoMimicrySource.Overclock: return !(wielder.jobs?.curDriver is JobDriver_EgoOverclock overclock && overclock.Weapon == sword);
                default: return false;
            }
        }

        private static bool Stunned(Pawn pawn) => pawn.stances?.stunner?.Stunned == true;

        /// <summary>Broken off: the lunge stops where the wielder stands, nothing lands.</summary>
        public void Cancel()
        {
            if (cancelled || resolved) return;
            cancelled = true;
            if (Lunges && !arrived) PawnDash.Stop(wielder);
        }

        /// <summary>
        /// The lunge is over: the wielder steps onto the landing cell if the line to it is still clear and the cell still free
        /// and standable (a door may have shut, a pawn stepped on), or stays where it started.
        /// </summary>
        private void Arrive()
        {
            arrived = true;
            bool clear = home != null && EgoMimicry.LineClear(home, lungeFrom, lungeTo);
            if (!clear) PawnDash.Stop(wielder);
            bool moved = clear && PawnDash.Arrive(wielder, home, lungeTo, needEmpty: true);
            GameComponent_EgoMimicry.Instance?.Landed(this, moved);
        }

        /// <summary>The grown swing holds the wielder in Core's melee cooldown until its blade has shrunk back (1.4 s, past the tool's 1.2 s).</summary>
        private void Cool(int now)
        {
            cooled = true;
            if (source != EgoMimicrySource.Ordinary || !(wielder.stances.curStance is Stance_Cooldown cooldown)) return;
            cooldown.ticksLeft = Mathf.Max(cooldown.ticksLeft, RecoverTick - now);
        }

        /// <summary>
        /// The contact frame: the swing or the slam lands through the sword's verb. An ordinary swing then rolls Corrosion once,
        /// hit or miss; a corroded or Overclock swing never rolls.
        /// </summary>
        private void Resolve()
        {
            resolved = true;
            CompEgoMimicry sword = Sword;
            Verb_EgoMimicry verb = Verb_EgoMimicry.Of(weapon);
            if (sword == null || verb == null) return;
            struck.Clear();
            stageAtContact = sword.StageNow;
            if (grown)
            {
                Vector3 at = wielder.Position.ToVector3Shifted();
                EgoMimicry.Strip(new Vector2(at.x, at.z), aim, Sign, sword.Props, out Vector2 from, out Vector2 to);
                verb.Slam(this, sword, from, to, struck);
                for (int i = 0; i < struck.Count; i++)
                    if (struck[i].Key is Pawn) EgoMimicry.Landed(wielder, sword, struck[i].Key, struck[i].Value);
                landed = struck.Count > 0;
                GameComponent_EgoMimicry.Instance?.Slammed(this, from, to);
            }
            else
            {
                landed = verb.Swing(this, sword, out float dealt);
                if (landed)
                {
                    struck.Add(new KeyValuePair<Thing, float>(target, dealt));
                    EgoMimicry.Landed(wielder, sword, target, dealt);
                    GameComponent_EgoMimicry.Instance?.Cut(this, target);
                }
            }
            if (source == EgoMimicrySource.Ordinary) EgoMimicry.Roll(wielder, sword);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref wielder, "wielder");
            Scribe_References.Look(ref weapon, "weapon");
            Scribe_References.Look(ref target, "target");
            Scribe_References.Look(ref home, "home");
            Scribe_Values.Look(ref source, "source");
            Scribe_Values.Look(ref grown, "grown");
            Scribe_Values.Look(ref surprise, "surprise");
            Scribe_Values.Look(ref aim, "aim");
            Scribe_Values.Look(ref lungeTick, "lungeTick", -1);
            Scribe_Values.Look(ref lungeFrom, "lungeFrom");
            Scribe_Values.Look(ref lungeTo, "lungeTo");
            Scribe_Values.Look(ref swingTick, "swingTick");
            Scribe_Values.Look(ref arrived, "arrived");
            Scribe_Values.Look(ref resolved, "resolved");
            Scribe_Values.Look(ref cancelled, "cancelled");
            Scribe_Values.Look(ref cooled, "cooled");
        }
    }
}
