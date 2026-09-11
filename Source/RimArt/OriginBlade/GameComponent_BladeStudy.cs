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

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Collections.Look(ref bladeTypes, "bladeTypes", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                bladeTypes = bladeTypes?.Where(name => !string.IsNullOrEmpty(name)).Distinct().ToList()
                    ?? new List<string>();
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

        public void CompleteStudy(Pawn pawn, ThingDef blade)
        {
            if (!OriginBladeUtility.CanStudy(pawn) || !OriginBladeUtility.IsBlade(blade)
                || OriginBladeUtility.HasOrigin(pawn)) return;
            BladeStudyRecord record = RecordFor(pawn);
            if (record.bladeTypes.Count >= OriginBladeUtility.BladesRequired
                || record.bladeTypes.Contains(blade.defName)) return;
            record.bladeTypes.Add(blade.defName);
            Messages.Message("AG_OriginBladeStudied".Translate(pawn.LabelShortCap, blade.LabelCap,
                record.bladeTypes.Count, OriginBladeUtility.BladesRequired), pawn,
                MessageTypeDefOf.PositiveEvent, false);
            CheckUnlock(record);
        }

        private static void CheckUnlock(BladeStudyRecord record)
        {
            Pawn pawn = record.pawn;
            if (pawn == null || pawn.Dead || pawn.Destroyed) return;
            if (OriginBladeUtility.HasOrigin(pawn))
            {
                OriginBladeUtility.EnforceRestrictions(pawn);
                return;
            }
            if (record.bladeTypes.Count < OriginBladeUtility.BladesRequired
                || !OriginBladeUtility.CanStudy(pawn) || !OriginBladeUtility.SkillsReady(pawn)) return;

            pawn.story.traits.GainTrait(new Trait(OriginBladeDefOf.AG_OriginBlade));
            if (!OriginBladeUtility.HasOrigin(pawn)) return;
            OriginBladeUtility.EnforceRestrictions(pawn);
            Find.LetterStack.ReceiveLetter("AG_OriginBladeAwakenedLabel".Translate(),
                "AG_OriginBladeAwakened".Translate(pawn.LabelShortCap), LetterDefOf.NeutralEvent, pawn);
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
