using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RimArt
{
    public class CompProperties_AbilityImperative : CompProperties_AbilityEffect
    {
        /// <summary>Which word this AbilityDef says. Everything else below is that word's balance.</summary>
        public ImperativeWord word = ImperativeWord.Stop;

        /// <summary>Throat wear per listener with factor 1, at Speak.</summary>
        public float throatCost = 0.05f;

        /// <summary>Stop: how long the inserted Wait job lasts before the listener's own AI takes over.</summary>
        public int holdTicks = 180;

        /// <summary>Run: how far the listener flees.</summary>
        public int fleeDistance = 24;

        /// <summary>Crush: blunt damage per listener. Explode: Bomb damage of each burst.</summary>
        public int damage = 15;

        /// <summary>Armour penetration of that damage; below 0 uses the damage type's default.</summary>
        public float armorPenetration = -1f;

        /// <summary>Crush: stun on each listener.</summary>
        public int stunTicks = 60;

        /// <summary>Explode: radius of the burst round each listener.</summary>
        public float burstRadius = 1.5f;

        public CompProperties_AbilityImperative()
        {
            compClass = typeof(CompAbilityEffect_Imperative);
        }
    }

    /// <summary>
    /// A word is a sound: it has no target. It spreads from Inumaki through open space and open
    /// doors (<see cref="SoundSpread"/>) as far as the volume set on his hero form carries, and
    /// everyone who hears it obeys - enemies, animals and colonists alike. Mechanoids and deaf pawns
    /// are immune.
    ///
    /// Stop, drop, come and run insert one job at the front of each listener's queue and hand them
    /// back to their own AI afterwards (StartJob's resumeCurJobAfterwards); no mental state, no
    /// faction change. Crush and explode hurt.
    ///
    /// The only cost is the throat (<see cref="LarynxUtility.WearFor"/>), summed over every listener.
    /// Hovering the button shows the reached cells, rings the listeners and puts the cost in the
    /// tooltip; a word that would take the throat past <see cref="LarynxExtension.maxWear"/>, or that
    /// no one would hear, cannot be said.
    /// </summary>
    public class CompAbilityEffect_Imperative : CompAbilityEffect
    {
        public new CompProperties_AbilityImperative Props => (CompProperties_AbilityImperative)props;

        private List<WordListener> ListenersNow(out WordVolume volume)
        {
            volume = LarynxUtility.VolumeOf(parent.pawn);
            return LarynxUtility.Listeners(parent.pawn, Props.word, volume);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn speaker = parent.pawn;
            if (speaker?.Map == null || !LarynxUtility.CanSpeak(speaker)) return;

            // Worked out again at the end of the warm-up: people move in 0.4 s. The bill comes
            // before the orders, because obeying changes CurJobDef and would make every listener
            // look like one already doing it.
            List<WordListener> listeners = ListenersNow(out WordVolume volume);
            float wear = LarynxUtility.WearFor(listeners, Props.throatCost, volume);

            MoteMaker.ThrowText(speaker.DrawPos + new Vector3(0f, 0f, 0.7f), speaker.Map,
                parent.def.LabelCap + "!", Color.white, 2.5f);

            Map map = speaker.Map;
            LarynxExtension ext = LarynxExtension.Get;
            map.GetComponent<MapComponent_CursedSpeech>()?.Say(speaker, listeners, ext.Reach(volume), ext.doorwayCost, volume);
            if (ext.wordSound != null)
            {
                SoundInfo info = SoundInfo.InMap(new TargetInfo(speaker.Position, map));
                info.volumeFactor = ext.SoundVolume(volume);
                info.pitchFactor = volume == WordVolume.Whisper ? 1.15f : volume == WordVolume.Shout ? 0.85f : 1f;
                ext.wordSound.PlayOneShot(info);
            }

            for (int i = 0; i < listeners.Count; i++)
                Obey(speaker, listeners[i].pawn, map);

            LarynxUtility.ApplyWear(speaker, wear);
        }

        private void Obey(Pawn speaker, Pawn listener, Map map)
        {
            if (listener.Destroyed) return;
            switch (Props.word)
            {
                case ImperativeWord.Crush:
                {
                    if (listener.Dead) return;
                    var dinfo = new DamageInfo(DamageDefOf.Blunt, Props.damage,
                        Props.armorPenetration < 0f ? -1f : Props.armorPenetration, -1f, speaker);
                    listener.TakeDamage(dinfo);
                    if (!listener.Dead && listener.stances?.stunner != null)
                        listener.stances.stunner.StunFor(Props.stunTicks, speaker, addBattleLog: false);
                    return;
                }

                case ImperativeWord.Explode:
                {
                    if (!listener.Spawned) return;
                    // Inumaki is the source, not a bystander: his own bursts do not hit him.
                    GenExplosion.DoExplosion(listener.Position, map, Props.burstRadius, DamageDefOf.Bomb, speaker,
                        damAmount: Props.damage, armorPenetration: Props.armorPenetration,
                        ignoredThings: new List<Thing> { speaker });
                    return;
                }

                default:
                {
                    if (listener.Dead || listener.Downed || listener.jobs == null) return;
                    Job job = LarynxUtility.BuildJob(speaker, listener, Props);
                    if (job == null) return;
                    job.playerForced = true;
                    listener.jobs.StartJob(job, JobCondition.InterruptForced, jobGiver: null, resumeCurJobAfterwards: true);
                    return;
                }
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            Pawn speaker = parent.pawn;
            if (!LarynxUtility.CanSpeak(speaker))
            {
                reason = "AG_LarynxNoVoice".Translate(speaker.LabelShort);
                return true;
            }
            if (speaker.Spawned)
            {
                List<WordListener> listeners = ListenersNow(out WordVolume volume);
                if (listeners.Count == 0)
                {
                    reason = "AG_LarynxNoListeners".Translate(VolumeLabel(volume));
                    return true;
                }
                float after = LarynxUtility.CurrentWear(speaker) + LarynxUtility.WearFor(listeners, Props.throatCost, volume);
                if (after > LarynxExtension.Get.maxWear + 0.0001f)
                {
                    reason = "AG_LarynxTooMuch".Translate(after.ToStringPercent("F0"));
                    return true;
                }
            }
            return base.GizmoDisabled(out reason);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn speaker = parent.pawn;
            if (!LarynxUtility.CanSpeak(speaker))
            {
                if (throwMessages)
                    Messages.Message("AG_LarynxNoVoice".Translate(speaker.LabelShort), speaker, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        /// <summary>Volume, reach, who hears it now and what it would cost, recomputed while the tooltip is open.</summary>
        public override string ExtraTooltipPart()
        {
            Pawn speaker = parent.pawn;
            LarynxExtension ext = LarynxExtension.Get;
            WordVolume volume = LarynxUtility.VolumeOf(speaker);
            string text = "AG_LarynxTooltipVolume".Translate(VolumeLabel(volume), ext.Reach(volume).ToString("0.#"),
                ext.Factor(volume).ToString("0.##"), Props.throatCost.ToStringPercent("0.#"));
            if (speaker == null || !speaker.Spawned) return text;

            List<WordListener> listeners = LarynxUtility.Listeners(speaker, Props.word, volume);
            int hostile = 0;
            for (int i = 0; i < listeners.Count; i++)
                if (listeners[i].pawn.HostileTo(speaker)) hostile++;
            float now = LarynxUtility.CurrentWear(speaker);
            float cost = LarynxUtility.WearFor(listeners, Props.throatCost, volume);
            text += "\n" + "AG_LarynxTooltipNow".Translate(listeners.Count, hostile, listeners.Count - hostile,
                cost.ToStringPercent("0.#"), now.ToStringPercent("F0"), (now + cost).ToStringPercent("F0"));
            return text;
        }

        /// <summary>Called while the button is hovered: shade every reached cell and ring each listener.</summary>
        public override void OnGizmoUpdate()
        {
            Pawn speaker = parent.pawn;
            if (speaker == null || !speaker.Spawned || speaker.Map != Find.CurrentMap) return;
            LarynxExtension ext = LarynxExtension.Get;
            WordVolume volume = LarynxUtility.VolumeOf(speaker);
            Dictionary<IntVec3, float> reached = SoundSpread.From(speaker, ext.Reach(volume), ext.doorwayCost);

            Material fill = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.55f, 0.8f, 1f, LarynxDefaults.ReachFillAlpha));
            var cells = new List<IntVec3>(reached.Count);
            foreach (IntVec3 cell in reached.Keys)
            {
                cells.Add(cell);
                Graphics.DrawMesh(MeshPool.plane10, cell.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays),
                    Quaternion.identity, fill, 0);
            }
            GenDraw.DrawFieldEdges(cells, new Color(0.55f, 0.8f, 1f, 0.6f));

            List<WordListener> listeners = LarynxUtility.Listeners(speaker, Props.word, volume);
            for (int i = 0; i < listeners.Count; i++)
            {
                Pawn pawn = listeners[i].pawn;
                SimpleColor color = pawn.HostileTo(speaker) ? SimpleColor.Red
                    : pawn.Faction == speaker.Faction ? SimpleColor.Blue : SimpleColor.Yellow;
                GenDraw.DrawCircleOutline(pawn.DrawPos, LarynxDefaults.ListenerRingRadius, color);
            }
        }

        public static string VolumeLabel(WordVolume volume) => ("AG_LarynxVolume_" + volume).Translate();
    }
}
