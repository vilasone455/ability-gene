using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>The castle organ's numbers, on its GeneDef.</summary>
    public class CastleOrganExtension : DefModExtension
    {
        /// <summary>Burn damage each check while she stands in sunlight.</summary>
        public float burnDamage = 4f;

        /// <summary>Seconds between checks.</summary>
        public float checkSeconds = 1f;

        /// <summary>The burn's armour penetration: 1 goes through clothes and armour.</summary>
        public float armorPenetration = 1f;

        /// <summary>At most one "burning in the sunlight" message this often, in seconds.</summary>
        public float messageSeconds = 30f;
    }

    /// <summary>
    /// Nakime's gene, given by her Echo on awakening and kept for life. Sunlight burns her whether or not
    /// she is manifested (the user's rule, 2026-09-27; this gene does not ask EchoUtility.GeneActive):
    /// once a second, standing on an unroofed cell of a map while the sky is lit (vanilla's InSunlight:
    /// sky glow over 0.1), she takes burnDamage burn damage through her clothes. Roofed cells, caravans and
    /// night are safe. Downed in about 20 s outdoors by day; she keeps burning while she lies there.
    /// </summary>
    public class Gene_CastleOrgan : Gene
    {
        private int progress, lastTick = -1, lastMessageTick = -100000;

        private static readonly CastleOrganExtension fallback = new CastleOrganExtension();
        private CastleOrganExtension Ext => def.GetModExtension<CastleOrganExtension>() ?? fallback;

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (pawn == null || pawn.Dead || !Active || !pawn.Spawned) { progress = 0; return; }
            int now = Find.TickManager.TicksGame;
            if (now == lastTick) return;
            lastTick = now;
            progress += Mathf.Max(1, delta);
            if (progress < Mathf.Max(1, Mathf.RoundToInt(Ext.checkSeconds * 60f))) return;
            progress = 0;
            if (!pawn.Position.InSunlight(pawn.Map)) return;
            Burn(now);
        }

        /// <summary>One check's burn, now. Public for the game tests.</summary>
        public void Burn(int now)
        {
            CastleOrganExtension ext = Ext;
            Map map = pawn.Map;
            Vector3 at = pawn.DrawPos;
            SoundLayers.Play(InfinityCastleDefOf.AG_NakimeSunBurn, map, pawn.Position);
            pawn.TakeDamage(new DamageInfo(DamageDefOf.Burn, ext.burnDamage, ext.armorPenetration));
            if (map != null) FleckMaker.ThrowSmoke(at, map, 0.6f);
            if (pawn.Faction == Faction.OfPlayer && now - lastMessageTick >= Mathf.RoundToInt(ext.messageSeconds * 60f))
            {
                lastMessageTick = now;
                Messages.Message(pawn.LabelShortCap + " is burning in the sunlight. Roofs and night are safe.", pawn, MessageTypeDefOf.NegativeHealthEvent, false);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref progress, "castleOrganProgress");
        }
    }
}
