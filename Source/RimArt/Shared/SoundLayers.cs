using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimArt
{
    /// <summary>
    /// The start of each layer (subSound) of a one-shot SoundDef, in seconds, in the order of its subSounds;
    /// a missing entry is 0. The sound lab mixes layers that start at different times, but RimWorld plays every
    /// layer of a one-shot at once and rejects startDelayRange outside sustainers ("startDelayRange is only
    /// supported on sustainers"), so the lab writes the delays here and <see cref="SoundLayers.Play"/> keeps them.
    /// </summary>
    public class SoundLayerDelays : DefModExtension
    {
        public List<float> delays = new List<float>();

        public int TicksOf(int layer) => layer < delays.Count ? Mathf.Max(0, Mathf.RoundToInt(delays[layer] * 60f)) : 0;
    }

    /// <summary>
    /// Plays a one-shot SoundDef whose layers start at different times (<see cref="SoundLayerDelays"/>): the
    /// layers due now play at once, the rest on their game tick, so they keep time with a picture drawn on the
    /// game clock (faster at speed 3, waiting while paused). A SoundDef without the extension plays as
    /// <c>PlayOneShot</c> plays it. Each layer goes through <c>SubSoundDef.TryPlay</c>, which is what
    /// <c>PlayOneShot</c> calls per layer: maxSimultaneous and the game-speed range still apply.
    /// </summary>
    public static class SoundLayers
    {
        /// <summary>Set by a game test: every layer started since, with its tick. Null outside tests.</summary>
        public static List<(SubSoundDef layer, int tick)> Started;

        public static void Play(SoundDef sound, TargetInfo target)
        {
            if (sound == null) return;
            SoundLayerDelays delays = sound.GetModExtension<SoundLayerDelays>();
            if (delays == null || sound.sustain)
            {
                if (Started != null) foreach (SubSoundDef layer in sound.subSounds) Started.Add((layer, Find.TickManager.TicksGame));
                sound.PlayOneShot(target);
                return;
            }
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < sound.subSounds.Count; i++)
            {
                int wait = delays.TicksOf(i);
                if (wait == 0) Start(sound.subSounds[i], target, now);
                else GameComponent_SoundLayers.Instance?.Later(sound.subSounds[i], target, now + wait);
            }
        }

        internal static void Start(SubSoundDef layer, TargetInfo target, int now)
        {
            Started?.Add((layer, now));
            layer.TryPlay(SoundInfo.InMap(target));
        }
    }

    /// <summary>The layers waiting for their tick. Not saved: a load drops the last fraction of a second of a sound.</summary>
    public sealed class GameComponent_SoundLayers : GameComponent
    {
        private readonly List<(SubSoundDef layer, TargetInfo target, int due)> waiting = new List<(SubSoundDef, TargetInfo, int)>();

        public GameComponent_SoundLayers(Game game) { }

        public static GameComponent_SoundLayers Instance => Current.Game?.GetComponent<GameComponent_SoundLayers>();

        public void Later(SubSoundDef layer, TargetInfo target, int due) => waiting.Add((layer, target, due));

        public override void GameComponentTick()
        {
            if (waiting.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < waiting.Count; i++)
            {
                (SubSoundDef layer, TargetInfo target, int due) = waiting[i];
                if (due > now) continue;
                // A map that is gone (a pocket map closed, a caravan left) takes its sounds with it.
                if (target.Map != null && Find.Maps.Contains(target.Map)) SoundLayers.Start(layer, target, now);
                waiting.RemoveAt(i--);
            }
        }
    }
}
