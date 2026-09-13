using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    public class CompProperties_RetrievalHookBelt : CompProperties
    {
        public CompProperties_RetrievalHookBelt()
        {
            compClass = typeof(CompRetrievalHookBelt);
        }
    }

    /// <summary>
    /// The belt's tether state: loaded or out, and how much reel-in work is done.
    ///
    /// Stored on the belt, not the wearer, for the same reason <see cref="CompApparelAbility"/>
    /// stores the stasis belt's cooldown: swapping the belt to another pawn, dropping it, or
    /// saving and loading must not reload it. A new belt starts loaded.
    /// </summary>
    public class CompRetrievalHookBelt : ThingComp
    {
        private bool loaded = true;
        private int reloadWork;

        public bool Loaded => loaded;

        public int ReloadWork => reloadWork;

        public float ReloadFraction => loaded ? 1f : Mathf.Clamp01(reloadWork / (float)RetrievalHookDefaults.ReloadTicks);

        public Pawn Wearer => (parent as Apparel)?.Wearer;

        /// <summary>Called when the net is launched, whether or not it connects.</summary>
        public void Unload()
        {
            loaded = false;
            reloadWork = 0;
        }

        /// <summary>Adds reel-in work. Returns true when this call finished the reload.</summary>
        public bool AddReloadWork(int ticks)
        {
            if (loaded) return false;
            reloadWork += Mathf.Max(0, ticks);
            if (reloadWork < RetrievalHookDefaults.ReloadTicks) return false;
            loaded = true;
            reloadWork = 0;
            return true;
        }

        /// <summary>The belt this pawn is wearing, or null.</summary>
        public static CompRetrievalHookBelt WornBy(Pawn pawn)
        {
            List<Apparel> worn = pawn?.apparel?.WornApparel;
            if (worn == null) return null;
            for (int i = 0; i < worn.Count; i++)
            {
                CompRetrievalHookBelt comp = worn[i].TryGetComp<CompRetrievalHookBelt>();
                if (comp != null) return comp;
            }
            return null;
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetWornGizmosExtra()) yield return gizmo;

            Pawn wearer = Wearer;
            if (loaded || wearer == null || !wearer.IsColonistPlayerControlled) yield break;

            Command_Action reel = new Command_Action
            {
                defaultLabel = "Reel in tether (" + ReloadFraction.ToStringPercent("F0") + ")",
                defaultDesc = "Stand still and reel the retrieval hook's tether back in. Takes "
                              + (RetrievalHookDefaults.ReloadTicks / 60f).ToString("0") + " seconds of work in total. "
                              + "If interrupted, progress is kept and this order has to be given again.",
                icon = ContentFinder<Texture2D>.Get("RimArt/RetrievalHook/IconReel"),
                action = () =>
                {
                    Job job = JobMaker.MakeJob(RetrievalHookDefOf.AG_ReelInTether, parent);
                    wearer.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
            };

            if (wearer.Downed) reel.Disable(wearer.LabelShortCap + " is downed.");
            else if (MapComponent_RetrievalHooks.IsPulling(wearer)) reel.Disable("The tether is still out on a target.");
            else if (wearer.CurJobDef == RetrievalHookDefOf.AG_ReelInTether) reel.Disable("Already reeling in.");

            yield return reel;
        }

        public override string CompInspectStringExtra()
        {
            if (loaded) return "Tether: loaded";
            return "Tether: out, reeled in " + ReloadFraction.ToStringPercent("F0");
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref loaded, "loaded", true);
            Scribe_Values.Look(ref reloadWork, "reloadWork", 0);
        }
    }
}
