using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class HediffCompProperties_WordVolume : HediffCompProperties
    {
        public HediffCompProperties_WordVolume()
        {
            compClass = typeof(HediffComp_WordVolume);
        }
    }

    /// <summary>
    /// The whisper / speak / shout toggle, on Inumaki's hero form hediff, so it exists exactly while
    /// the words do. Every word reads it through <see cref="LarynxUtility.VolumeOf"/>. It starts at
    /// Speak on each manifest.
    /// </summary>
    [StaticConstructorOnStartup]
    public class HediffComp_WordVolume : HediffComp
    {
        public WordVolume volume = WordVolume.Speak;

        private static readonly Texture2D Icon = ContentFinder<Texture2D>.Get("UI/Abilities/AnimalWarcall");

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (!Pawn.IsColonistPlayerControlled) yield break;
            LarynxExtension ext = LarynxExtension.Get;
            yield return new Command_Action
            {
                defaultLabel = "AG_LarynxVolumeGizmo".Translate(CompAbilityEffect_Imperative.VolumeLabel(volume)),
                defaultDesc = "AG_LarynxVolumeGizmoDesc".Translate(
                    ext.whisperReach.ToString("0.#"), ext.whisperFactor.ToString("0.##"),
                    ext.speakReach.ToString("0.#"), ext.speakFactor.ToString("0.##"),
                    ext.shoutReach.ToString("0.#"), ext.shoutFactor.ToString("0.##")),
                icon = Icon,
                action = () => volume = volume == WordVolume.Shout ? WordVolume.Whisper : volume + 1
            };
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref volume, "volume", WordVolume.Speak);
        }
    }
}
