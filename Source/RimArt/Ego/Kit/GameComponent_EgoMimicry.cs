using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using T = RimArt.EgoMimicryTiming;

namespace RimArt
{
    /// <summary>
    /// Every Mimicry swing in the game (<see cref="EgoMimicryCast"/>, saved: a swing loaded before its contact frame still
    /// lands, one loaded after it never lands again), each wielder's look (<see cref="EgoMimicryLook"/>), the marks the swings
    /// leave (cuts, the slam's crack, lunge dust, flesh torn off the arm), and the sword in every holder's hand, drawn
    /// instead of Core (<see cref="Patches_EgoMimicry"/>). The Vergil shape (<see cref="GameComponent_Vergil"/>): ticks the
    /// rules on game time, draws the map on screen.
    ///
    /// Who holds a sword is kept in <see cref="holders"/> (<see cref="HeldWeaponHolders"/>): the comp registers on equip and
    /// unequip, and a rescan once a second catches pawns that arrive already holding one and drops the dead.
    /// </summary>
    public sealed class GameComponent_EgoMimicry : GameComponent
    {
        private List<EgoMimicryCast> casts = new List<EgoMimicryCast>();
        private readonly HeldWeaponHolders holders = new HeldWeaponHolders(pawn => CompEgoMimicry.HeldBy(pawn) != null);
        private readonly Dictionary<Pawn, EgoMimicryLook> looks = new Dictionary<Pawn, EgoMimicryLook>();
        private readonly List<Mark> marks = new List<Mark>();
        private int seeds;

        /// <summary>A mark a swing left: drawn with its age in seconds until it is <see cref="life"/> s old.</summary>
        private sealed class Mark
        {
            public Map map;
            public int tick;
            public float life;
            public Action<float> draw;
        }

        public GameComponent_EgoMimicry(Game game) { }

        public static GameComponent_EgoMimicry Instance => Current.Game?.GetComponent<GameComponent_EgoMimicry>();

        public IReadOnlyList<EgoMimicryCast> Casts => casts;

        public void Register(Pawn pawn) => holders.Add(pawn);

        /// <summary>The sword left <paramref name="pawn"/>'s hands: it is no holder, and its swings that have not landed are broken off.</summary>
        public void Unregister(Pawn pawn)
        {
            holders.Remove(pawn);
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].wielder == pawn) casts[i].Cancel();
        }

        public bool Holds(Pawn pawn) => holders.Contains(pawn);

        public EgoMimicryLook LookOf(Pawn pawn) => looks.TryGetValue(pawn, out EgoMimicryLook look) ? look : null;

        /// <summary>
        /// An earlier swing of <paramref name="pawn"/>'s is still going: it has not landed, or its grown blade has not shrunk
        /// back. A new swing (an attack, a corroded or Overclock firing) waits until it is over.
        /// </summary>
        public bool Busy(Pawn pawn)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < casts.Count; i++)
            {
                EgoMimicryCast c = casts[i];
                if (c.wielder == pawn && !c.cancelled && (!c.resolved || now < c.RecoverTick)) return true;
            }
            return false;
        }

        /// <summary>The latest swing of <paramref name="pawn"/>'s, in any state; null when there is none.</summary>
        public EgoMimicryCast Latest(Pawn pawn)
        {
            for (int i = casts.Count - 1; i >= 0; i--)
                if (casts[i].wielder == pawn) return casts[i];
            return null;
        }

        /// <summary>
        /// A swing starts: at <paramref name="target"/>, from where the wielder stands or, with a valid
        /// <paramref name="lungeTo"/>, after a lunge there of <see cref="T.LungeTime"/> s.
        /// </summary>
        public EgoMimicryCast BeginSwing(Pawn wielder, CompEgoMimicry sword, Thing target, EgoMimicrySource source, bool grown, bool surprise, IntVec3 lungeTo)
        {
            int now = Find.TickManager.TicksGame;
            IntVec3 from = wielder.Position;
            var cast = new EgoMimicryCast
            {
                wielder = wielder, weapon = sword.parent, target = target, home = wielder.Map, source = source, grown = grown, surprise = surprise, swingTick = now,
            };
            if (lungeTo.IsValid && lungeTo != from)
            {
                cast.lungeTick = now;
                cast.lungeFrom = from;
                cast.lungeTo = lungeTo;
                cast.swingTick = now + EgoMimicryCast.Ticks(T.LungeTime);
                PawnDash.Keep(wielder, from, lungeTo, now, cast.swingTick - now, eased: true);
                // The map is taken now: a wielder killed mid-lunge has no map by the time the dust is drawn.
                Map map = wielder.Map;
                Vector3 start = from.ToVector3Shifted();
                Add(map, 0.5f, age => EgoMimicryStrikeGraphics.LungeDust(new Vector2(start.x, start.z), age, map));
            }
            Vector3 a = (cast.Lunges ? lungeTo : from).ToVector3Shifted(), b = target.Position.ToVector3Shifted();
            cast.aim = Mathf.Atan2(b.z - a.z, b.x - a.x) * Mathf.Rad2Deg;
            casts.Add(cast);
            return cast;
        }

        /// <summary>The corrosion or Overclock with this sword ended: every swing of the wielder's from it that has not landed is broken off.</summary>
        public void EndSpecial(Pawn wielder)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].wielder == wielder && casts[i].source != EgoMimicrySource.Ordinary) casts[i].Cancel();
        }

        // ---- What the rules tell the picture ------------------------------------------------------------------------

        /// <summary>A hit fed the wielder: the heal's glow.</summary>
        public void Fed(Pawn wielder) => Look(wielder).feedTick = Find.TickManager.TicksGame;

        /// <summary>The arm grew a stage; the look picks it up on the next tick.</summary>
        public void StageChanged(Pawn wielder) => Look(wielder);

        /// <summary>A hit taken tore a stage off: a chunk of flesh flies off away from whoever hit it.</summary>
        public void Torn(Pawn wielder, Thing by)
        {
            Map map = wielder.Map;
            if (map == null) return;
            Vector3 at = wielder.DrawPos, src = by != null && by.Spawned && by.Map == map ? by.DrawPos : at + new Vector3(-1f, 0f, -1f);
            Vector2 w = new Vector2(at.x, at.z), f = new Vector2(src.x, src.z), hs = T.HandSide(T.AimOf(wielder.Rotation), T.SignOf(wielder.Rotation));
            Add(map, 2.5f, age => EgoMimicryStrikeGraphics.Torn(w, f, hs, age, map));
        }

        /// <summary>A swing's hit landed on <paramref name="victim"/>: the slash and the cut on it, the blood thrown along the swing.</summary>
        public void Cut(EgoMimicryCast cast, Thing victim)
        {
            Map map = victim.MapHeld;
            if (map == null) return;
            Vector3 hit = victim.DrawPos;
            Vector2 foot = new Vector2(hit.x, hit.z), d = T.Dir(cast.aim), along = T.Side(d);
            int seed = seeds++ * 7;
            Add(map, 3f, age =>
            {
                Vector3 now = victim.Spawned && victim.Map == map ? victim.DrawPos : hit;
                EgoMimicryStrikeGraphics.Cut(new Vector2(now.x, now.z + PawnBody.Chest), foot, d, along, age, seed, map, now.y);
            });
        }

        /// <summary>The grown blade slammed down along the strip from <paramref name="from"/> to <paramref name="to"/>: the crack, the split light, the rocks.</summary>
        public void Slammed(EgoMimicryCast cast, Vector2 from, Vector2 to)
        {
            Map map = cast.home;
            Vector2 d = T.Dir(cast.aim);
            Add(map, 3f, age => EgoMimicryStrikeGraphics.Slam(from, to, d, age, EgoMimicryStrikeGraphics.SlamSeed, map));
            if (map == Find.CurrentMap) Find.CameraDriver.shaker.DoShake(0.07f);
        }

        /// <summary>A lunge ended: dust where the wielder lands, if it moved.</summary>
        public void Landed(EgoMimicryCast cast, bool moved)
        {
            if (!moved) return;
            Map map = cast.home;
            Vector3 end = cast.lungeTo.ToVector3Shifted();
            Add(map, 0.5f, age => EgoMimicryStrikeGraphics.LungeDust(new Vector2(end.x, end.z), age, map));
        }

        private void Add(Map map, float life, Action<float> draw) => marks.Add(new Mark { map = map, tick = Find.TickManager.TicksGame, life = life, draw = draw });

        private EgoMimicryLook Look(Pawn wielder)
        {
            if (looks.TryGetValue(wielder, out EgoMimicryLook look)) return look;
            CompEgoMimicry sword = CompEgoMimicry.HeldBy(wielder);
            look = new EgoMimicryLook(wielder, sword?.stage ?? 0, sword != null && EgoMimicry.Special(wielder, sword));
            looks[wielder] = look;
            return look;
        }

        /// <summary>A test puts back a swing it took through a save and a load in place of <paramref name="original"/>.</summary>
        internal void ReplaceForTests(EgoMimicryCast original, EgoMimicryCast loaded)
        {
            int i = casts.IndexOf(original);
            if (i >= 0) casts[i] = loaded;
            else casts.Add(loaded);
        }

        /// <summary>A test drops every swing, look and mark and forgets every holder (its new pawns register as they equip).</summary>
        public void Clear()
        {
            for (int i = 0; i < casts.Count; i++) casts[i].Cancel();
            casts.Clear();
            looks.Clear();
            marks.Clear();
            holders.Clear();
        }

        // ---- Game time ----------------------------------------------------------------------------------------------

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            holders.Rescan();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
                if (i < casts.Count && !casts[i].Tick(now)) casts.RemoveAt(i);
            TickLooks(now);
            for (int i = marks.Count - 1; i >= 0; i--)
                if ((now - marks[i].tick) / 60f >= marks[i].life) marks.RemoveAt(i);
            if (now % HeldWeaponHolders.RescanEvery == 0) holders.Rescan();
        }

        private readonly List<Pawn> gone = new List<Pawn>();

        /// <summary>
        /// Each look follows its sword's stage, and a holder that turns corroded or overclocking gets one. A sword whose state
        /// ended without its End (a wielder killed outright: Pawn.Kill ends no mental state) has its stage set back to 0 here.
        /// </summary>
        private void TickLooks(int now)
        {
            foreach (Pawn pawn in holders)
            {
                CompEgoMimicry sword = CompEgoMimicry.HeldBy(pawn);
                if (sword == null) continue;
                bool special = EgoMimicry.Special(pawn, sword);
                if (!special && sword.stage != 0) sword.stage = 0;
                if (special && !looks.ContainsKey(pawn)) Look(pawn);
            }
            gone.Clear();
            foreach (KeyValuePair<Pawn, EgoMimicryLook> entry in looks)
            {
                CompEgoMimicry sword = CompEgoMimicry.HeldBy(entry.Key);
                bool special = sword != null && !entry.Key.Dead && EgoMimicry.Special(entry.Key, sword);
                entry.Value.Tick(now, sword?.stage ?? 0, special);
                if (entry.Value.Gone(now)) gone.Add(entry.Key);
            }
            for (int i = 0; i < gone.Count; i++) looks.Remove(gone[i]);
        }

        // ---- Drawing ------------------------------------------------------------------------------------------------

        public override void GameComponentUpdate()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            for (int i = 0; i < marks.Count; i++)
                if (marks[i].map == map) marks[i].draw(PictureClock.Since(marks[i].tick));
            int now = Find.TickManager.TicksGame;
            // Copied first: a draw-time recache can unequip and change the set.
            List<Pawn> holderList = holders.Copy();
            for (int i = 0; i < holderList.Count; i++)
                if (holderList[i].Spawned && holderList[i].Map == map) DrawHolder(holderList[i], map, now);
        }

        /// <summary>The latest swing of the pawn's still in its picture, not broken off; null when there is none.</summary>
        private EgoMimicryCast Drawing(Pawn pawn, int now)
        {
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                EgoMimicryCast c = casts[i];
                if (c.wielder == pawn && !c.cancelled && now < c.EndTick) return c;
            }
            return null;
        }

        private void DrawHolder(Pawn pawn, Map map, int now)
        {
            CompEgoMimicry sword = CompEgoMimicry.HeldBy(pawn);
            if (sword == null || pawn.Dead || pawn.GetPosture() != PawnPosture.Standing) return;
            EgoMimicryCast cast = Drawing(pawn, now);
            EgoMimicryLook look = LookOf(pawn);
            if (cast == null && look == null && !Shown(pawn)) return;
            Vector3 at = pawn.DrawPos;
            Rot4 facing = pawn.Rotation;
            EgoMimicryGraphics.DrawWielder(new EgoMimicryWielder
            {
                Pos = new Vector2(at.x, at.z),
                Aim = cast != null ? cast.aim : T.AimOf(facing),
                Move = cast == null ? EgoMimicryMove.Rest : cast.grown ? EgoMimicryMove.Grown : EgoMimicryMove.Swing,
                MoveAge = cast != null ? PictureClock.Since(cast.swingTick) : -1f,
                Stage = look?.Stage ?? 0f,
                Open = look?.Open ?? 0f,
                FeedAge = look?.FeedAge ?? -1f,
                Skin = pawn.story != null ? pawn.story.SkinColor : EgoMimicryGraphics.Skin,
                Facing = facing,
                Altitude = at.y,
            }, PictureClock.Since(0), map);
        }

        /// <summary>Wherever Core would show a held weapon: drafted or a job that shows it, or aiming or cooling down from an attack.</summary>
        private static bool Shown(Pawn pawn) =>
            PawnRenderUtility.CarryWeaponOpenly(pawn) || (pawn.stances?.curStance is Stance_Busy busy && busy.focusTarg.IsValid);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref casts, "egoMimicryCasts", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (casts == null) casts = new List<EgoMimicryCast>();
                casts.RemoveAll(c => c == null || c.wielder == null || c.weapon == null);
            }
        }
    }
}
