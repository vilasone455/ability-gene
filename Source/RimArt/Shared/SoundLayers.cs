using System.Collections.Generic;
using HarmonyLib;
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
    /// <c>PlayOneShot</c> calls per layer: maxSimultaneous and the game-speed range still apply. <see cref="Sustain"/>
    /// starts a kit's sustainer the same way, noted for the game tests, and <see cref="MoveTo"/> moves one.
    /// </summary>
    public static class SoundLayers
    {
        /// <summary>Set by a game test: every layer started since, with its tick. Null outside tests.</summary>
        public static List<(SubSoundDef layer, int tick)> Started;

        /// <summary>Set by a game test: every sound played through <see cref="Play(SoundDef, Map, IntVec3)"/> since, with its tick. Null outside tests.</summary>
        public static List<(SoundDef sound, int tick)> Heard;

        /// <summary>A kit's sound heard from a cell, its layers each at its own time; nothing off the map or without a map.</summary>
        public static void Play(SoundDef sound, Map map, IntVec3 cell)
        {
            if (sound == null || map == null || !cell.InBounds(map)) return;
            Heard?.Add((sound, Find.TickManager.TicksGame));
            Play(sound, new TargetInfo(cell, map));
        }

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

        /// <summary>
        /// A kit's sustainer heard from a cell, noted in <see cref="Heard"/> when it starts; null without a map or off
        /// it. The caller maintains it every tick (it ends a tick after the last <c>Maintain</c>) and ends it.
        /// </summary>
        public static Sustainer Sustain(SoundDef sound, Map map, IntVec3 cell)
        {
            if (sound == null || map == null || !cell.InBounds(map)) return null;
            Heard?.Add((sound, Find.TickManager.TicksGame));
            return sound.TrySpawnSustainer(SoundInfo.InMap(new TargetInfo(cell, map), MaintenanceType.PerTick));
        }

        // RimWorld moves a sustainer's audio source each frame only when it was started on a thing; one started on a
        // cell stays there. Its root object is internal.
        internal static readonly AccessTools.FieldRef<Sustainer, GameObject> RootOf =
            AccessTools.FieldRefAccess<Sustainer, GameObject>("worldRootObject");

        /// <summary>Moves a sustainer started on a cell to <paramref name="at"/>, for a sound that travels (Hollow Purple).</summary>
        public static void MoveTo(Sustainer sustainer, Vector3 at)
        {
            if (sustainer == null || sustainer.Ended) return;
            GameObject root = RootOf(sustainer);
            if (root != null) root.transform.position = at.Yto0();
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
