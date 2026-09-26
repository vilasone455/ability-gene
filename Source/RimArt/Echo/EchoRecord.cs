using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public enum EchoState
    {
        /// <summary>The device is not tuned to this Echo.</summary>
        Untuned,
        /// <summary>The device is tuned to it and one candidate's Trials are tracked.</summary>
        Tracking,
        /// <summary>A Host carries it.</summary>
        Awakened,
        /// <summary>Its Host died; the Echo cannot be taken again this playthrough.</summary>
        Closed,
    }

    /// <summary>The state of one Echo in this playthrough.</summary>
    public class EchoRecord : IExposable
    {
        public EchoDef def;
        public EchoState state;
        public Pawn candidate;
        public Pawn host;
        /// <summary>The awakening letter is sent once per candidate; the candidate's gizmo stays as the way in.</summary>
        public bool offered;
        /// <summary>Trial indexes already announced as cleared, so each message is sent once.</summary>
        public List<int> announced = new List<int>();

        public bool manifested;
        public Color savedHairColor;
        public BodyTypeDef savedBodyType;
        public bool hairChanged;
        /// <summary>Cooldowns of the Echo's abilities between manifests, so reverting does not reset them.</summary>
        public ItemAbilityGrant grant = new ItemAbilityGrant();

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref state, "state");
            Scribe_References.Look(ref candidate, "candidate");
            Scribe_References.Look(ref host, "host");
            Scribe_Values.Look(ref offered, "offered");
            Scribe_Collections.Look(ref announced, "announced", LookMode.Value);
            Scribe_Values.Look(ref manifested, "manifested");
            Scribe_Values.Look(ref savedHairColor, "savedHairColor");
            Scribe_Defs.Look(ref savedBodyType, "savedBodyType");
            Scribe_Values.Look(ref hairChanged, "hairChanged");
            if (grant == null) grant = new ItemAbilityGrant();
            grant.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (announced == null) announced = new List<int>();
                if (grant == null) grant = new ItemAbilityGrant();
            }
        }
    }

    /// <summary>Deeds vanilla does not record, counted per colonist.</summary>
    public class PawnDeeds : IExposable
    {
        public Pawn pawn;
        public Dictionary<ThingDef, int> killsByWeapon = new Dictionary<ThingDef, int>();
        private List<ThingDef> scribeDefs;
        private List<int> scribeCounts;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Collections.Look(ref killsByWeapon, "killsByWeapon", LookMode.Def, LookMode.Value, ref scribeDefs, ref scribeCounts);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && killsByWeapon == null)
                killsByWeapon = new Dictionary<ThingDef, int>();
        }
    }
}
