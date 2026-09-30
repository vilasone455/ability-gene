using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimArt
{
    public class BladeStudyRecord : IExposable
    {
        public Pawn pawn;
        // Def names retain completed studies even when a weapon mod is later removed.
        public List<string> bladeTypes = new List<string>();
        // The awakening letter is offered once. Declining is not final -- the gizmo becomes
        // an awaken button -- but the letter must not return every 250 ticks forever.
        public bool offered;
        /// <summary>
        /// Every blade studied, by weapon and material with the best quality: Trace On's library. Studying goes on
        /// after awakening and only adds here then.
        /// </summary>
        public List<TraceLibraryEntry> library = new List<TraceLibraryEntry>();

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Collections.Look(ref bladeTypes, "bladeTypes", LookMode.Value);
            Scribe_Values.Look(ref offered, "offered", false);
            Scribe_Collections.Look(ref library, "traceLibrary", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                bladeTypes = bladeTypes?.Where(name => !string.IsNullOrEmpty(name)).Distinct().ToList()
                    ?? new List<string>();
                library = library?.Where(entry => entry?.blade != null).ToList() ?? new List<TraceLibraryEntry>();
                // A save from before the library knows only the types: each becomes its default material at normal.
                foreach (string type in bladeTypes)
                    if (!library.Any(entry => entry.blade == type)) library.Add(new TraceLibraryEntry { blade = type });
            }
        }
    }

    public class GameComponent_BladeStudy : GameComponent
    {
        private List<BladeStudyRecord> records = new List<BladeStudyRecord>();

        public GameComponent_BladeStudy(Game game) { }

        public BladeStudyRecord RecordFor(Pawn pawn)
        {
            BladeStudyRecord record = records.Find(item => item.pawn == pawn);
            if (record != null) return record;
            record = new BladeStudyRecord { pawn = pawn };
            records.Add(record);
            return record;
        }

        public void CompleteStudy(Pawn pawn, Thing blade)
        {
            if (!OriginBladeUtility.CanStudy(pawn) || !OriginBladeUtility.IsBlade(blade?.def)) return;
            BladeStudyRecord record = RecordFor(pawn);
            if (OriginBladeUtility.HasOrigin(pawn))
            {
                // After awakening a study only fills the trace library.
                if (TraceLibrary.Learn(record.library, blade) is TraceLibraryEntry learned)
                    Messages.Message("AG_TraceLearned".Translate(pawn.LabelShortCap,
                        TraceLibrary.Label(learned, CompProperties_TraceOn.QualityBelow)), pawn, MessageTypeDefOf.PositiveEvent, false);
                return;
            }
            if (record.bladeTypes.Count >= OriginBladeUtility.BladesRequired
                || record.bladeTypes.Contains(blade.def.defName)) return;
            record.bladeTypes.Add(blade.def.defName);
            TraceLibrary.Learn(record.library, blade);
            Messages.Message("AG_OriginBladeStudied".Translate(pawn.LabelShortCap, blade.def.LabelCap,
                record.bladeTypes.Count, OriginBladeUtility.BladesRequired), pawn,
                MessageTypeDefOf.PositiveEvent, false);
            CheckUnlock(record);
        }

        /// <summary>
        /// Offers the awakening rather than performing it.
        ///
        /// This used to grant the trait the moment the last requirement was met, which meant a
        /// pawn could lose a psylink and every psycast years after the player last read the
        /// warning. The cost is permanent, so the decision belongs to the player at the moment
        /// it is taken, the way vanilla handles a growth moment.
        /// </summary>
        private static void CheckUnlock(BladeStudyRecord record)
        {
            Pawn pawn = record.pawn;
            if (pawn == null || pawn.Dead || pawn.Destroyed) return;
            if (OriginBladeUtility.HasOrigin(pawn))
            {
                OriginBladeUtility.EnforceRestrictions(pawn);
                return;
            }
            if (record.offered || !OriginBladeUtility.ReadyToAwaken(pawn)) return;

            record.offered = true;
            ChoiceLetter_OriginBladeAwakening letter =
                (ChoiceLetter_OriginBladeAwakening)LetterMaker.MakeLetter(
                    "AG_OriginBladeAwakenLabel".Translate(),
                    "AG_OriginBladeAwakenText".Translate(pawn.LabelShortCap)
                        + "\n\n" + "AG_OriginBladeWarning".Translate(),
                    OriginBladeDefOf.AG_OriginBladeAwakening, pawn);
            letter.pawn = pawn;
            Find.LetterStack.ReceiveLetter(letter);
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 250 != 0) return;
            // Also catches reaching the skill thresholds after finishing all five studies.
            foreach (BladeStudyRecord record in records) CheckUnlock(record);
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref records, "originBladeStudies", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (records == null) records = new List<BladeStudyRecord>();
                records.RemoveAll(record => record?.pawn == null);
            }
        }
    }
}
