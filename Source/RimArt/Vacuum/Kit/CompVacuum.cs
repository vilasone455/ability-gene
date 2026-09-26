using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class VacuumDefOf
    {
        public static ThingDef AG_Vacuum;
        public static HediffDef AG_VacuumFull;
        public static AbilityDef AG_Vacuum_Suck;
        public static AbilityDef AG_Vacuum_Spit;
        public static AbilityDef AG_Vacuum_Digest;
        public static JobDef AG_CastVacuum;
        public static JobDef AG_CastVacuumDigest;

        static VacuumDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(VacuumDefOf));
        }
    }

    /// <summary>The weapon's own rules. Every balance number of the kit that is not an ability's is here, as an XML field on the weapon.</summary>
    public class CompProperties_Vacuum : CompProperties
    {
        public List<AbilityDef> abilities;

        /// <summary>The most kg the vacuum holds, mouth and stomach together. Suck is refused at this and takes nothing that would go over.</summary>
        public float capacityKg = 100f;
        /// <summary>The holder's move speed falls by this share per kg inside (0.004: -40 % at 100 kg).</summary>
        public float slowPerKg = 0.004f;
        /// <summary>The slow never takes the move speed under this factor.</summary>
        public float minMoveFactor = 0.1f;

        public CompProperties_Vacuum()
        {
            compClass = typeof(CompVacuum);
        }
    }

    /// <summary>
    /// The vacuum: grants Suck, Spit and Digest to whoever holds it, and holds what they took.
    ///
    /// The mouth holds one real thing, the last swallowed, which Spit fires. The stomach is a weight
    /// only: when something new comes into the mouth, what was there is destroyed and its kg go to the
    /// stomach, never to come back. Digest destroys the lot. The holder is slowed by the kg inside
    /// (AG_VacuumFull, <see cref="VacuumLoad"/>). All of it is on the weapon, so a dropped or handed-over
    /// vacuum keeps its contents; nothing spills when the holder dies, and the mouth's thing is lost
    /// with the weapon if the weapon is destroyed.
    ///
    /// Things in the air during a Suck are held here too (<see cref="AddInbound"/>) from the moment
    /// they leave the ground until they reach the head, so a game saved mid-flight keeps them; a
    /// flight the load cut short is finished by <see cref="Settle"/>.
    ///
    /// It is the weapon's CompEquippable (the def lists its comps with Inherit="False"), as
    /// CompFlameGauntlet is, so the fullness readout can be an equipped gizmo.
    /// <see cref="ItemAbilityGrant"/> keeps the cooldowns on the vacuum.
    /// </summary>
    public class CompVacuum : CompEquippable, IThingHolder
    {
        private ThingOwner<Thing> contents;
        private Thing mouth;
        private float mouthKg, stomachKg;
        private readonly ItemAbilityGrant grant = new ItemAbilityGrant();

        public CompVacuum()
        {
            contents = new ThingOwner<Thing>(this);
        }

        public CompProperties_Vacuum Props => (CompProperties_Vacuum)props;

        public static CompVacuum HeldBy(Pawn pawn) => pawn?.equipment?.Primary?.GetComp<CompVacuum>();

        public Pawn Wielder => Holder;

        /// <summary>The thing in the mouth, or null.</summary>
        public Thing Mouth => mouth;
        public float MouthKg => mouth == null ? 0f : mouthKg;
        public float StomachKg => stomachKg;
        public float Fullness => MouthKg + stomachKg;
        public bool Full => Fullness >= Props.capacityKg - 0.001f;
        /// <summary>The fullness as the picture draws it: kg out of the picture's 100 kg canister.</summary>
        public float DrawnKg => Fullness / Mathf.Max(1f, Props.capacityKg) * VacuumGraphics.Capacity;
        public float DrawnScale => VacuumGraphics.Capacity / Mathf.Max(1f, Props.capacityKg);

        /// <summary>A thing's weight as the vacuum counts it: the Mass stat for the whole stack. Filth weighs nothing.</summary>
        public static float KgOf(Thing thing) => thing == null || thing is Filth ? 0f : thing.GetStatValue(StatDefOf.Mass) * thing.stackCount;

        public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());

        public ThingOwner GetDirectlyHeldThings() => contents;

        // ------------------------------------------------------------------ contents

        /// <summary>A thing has left the ground (or a pawn's hands) and is on its way into the head: it is held here until it arrives.</summary>
        public bool AddInbound(Thing thing)
        {
            if (thing == null || thing.Destroyed) return false;
            if (thing.Spawned) thing.DeSpawn();
            return contents.TryAdd(thing, false);
        }

        /// <summary>
        /// A thing reaches the head: what was in the mouth goes down to the stomach (destroyed, its kg
        /// kept) and this one takes its place.
        /// </summary>
        public void Swallow(Thing thing, float kg)
        {
            if (thing == null || thing.Destroyed) return;
            if (!contents.Contains(thing) && !AddInbound(thing)) return;
            PushMouthDown();
            mouth = thing;
            mouthKg = kg;
        }

        private void PushMouthDown()
        {
            if (mouth == null) return;
            stomachKg += mouthKg;
            Thing old = mouth;
            mouth = null;
            mouthKg = 0f;
            if (contents.Contains(old)) contents.Remove(old);
            if (!old.Destroyed) old.Destroy();
        }

        /// <summary>Spit: the mouth's thing is dropped at <paramref name="cell"/> (or the nearest place it fits). Returns what landed.</summary>
        public Thing DropMouth(IntVec3 cell, Map map)
        {
            Thing thing = mouth;
            mouth = null;
            mouthKg = 0f;
            if (thing == null || !contents.Contains(thing)) return null;
            contents.TryDrop(thing, cell, map, ThingPlaceMode.Near, out Thing landed);
            return landed;
        }

        /// <summary>Digest begins: the mouth's thing goes down first.</summary>
        public void DigestMouth() => PushMouthDown();

        public void SetStomach(float kg) => stomachKg = Mathf.Max(0f, kg);

        /// <summary>Empties the vacuum at once (debug).</summary>
        public void Empty()
        {
            Settle();
            PushMouthDown();
            stomachKg = 0f;
            Sync();
        }

        /// <summary>Things still held as in flight when no cast is carrying them (a load cut the cast short) are swallowed in the order they left.</summary>
        public void Settle()
        {
            if (contents.Count == 0 || contents.Count == 1 && mouth != null) return;
            var inbound = new List<Thing>();
            for (int i = 0; i < contents.Count; i++)
                if (contents[i] != mouth) inbound.Add(contents[i]);
            for (int i = 0; i < inbound.Count; i++) Swallow(inbound[i], KgOf(inbound[i]));
            Sync();
        }

        public bool HasInbound => contents.Count > (mouth != null ? 1 : 0);

        /// <summary>Brings the holder's slow up to date with the kg inside.</summary>
        public void Sync() => VacuumLoad.Sync(Holder, this);

        // ------------------------------------------------------------------ holding

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            pawn?.MapHeld?.GetComponent<MapComponent_Vacuum>()?.Register(pawn);
            grant.Give(pawn, Props.abilities);
            if (pawn?.MapHeld?.GetComponent<MapComponent_Vacuum>()?.CastingWith(this) != true) Settle();
            VacuumLoad.Sync(pawn, this);
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            grant.Take(pawn, Props.abilities, this);
            VacuumLoad.Remove(pawn);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            // Nothing spills: the mouth's thing and anything in flight go with the weapon.
            mouth = null;
            contents.ClearAndDestroyContents();
        }

        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetEquippedGizmosExtra()) yield return gizmo;
            Pawn holder = Holder;
            if (holder != null && holder.IsColonistPlayerControlled) yield return new Gizmo_VacuumFullness(this);
        }

        /// <summary>"Mouth: steel x75 37.5 kg / Stomach: 20 kg".</summary>
        public string ContentsLine()
        {
            var sb = new StringBuilder("Mouth: ");
            if (mouth == null) sb.Append("empty");
            else sb.Append(mouth.LabelNoParenthesis).Append(' ').Append(Kg(mouthKg));
            sb.Append(" / Stomach: ").Append(Kg(stomachKg));
            return sb.ToString();
        }

        public static string Kg(float kg) => kg.ToString(kg < 10f ? "0.#" : "0") + " kg";

        public override string CompInspectStringExtra() =>
            ContentsLine() + "\nFullness: " + Kg(Fullness) + " / " + Kg(Props.capacityKg) + (Full ? " (full)" : "");

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref contents, "contents", this);
            Scribe_References.Look(ref mouth, "mouth");
            Scribe_Values.Look(ref mouthKg, "mouthKg");
            Scribe_Values.Look(ref stomachKg, "stomachKg");
            grant.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && contents == null) contents = new ThingOwner<Thing>(this);
        }
    }

    /// <summary>
    /// The holder's slow: AG_VacuumFull at a severity of the kg inside, added and removed as the
    /// contents change and when the vacuum is put down or picked up.
    /// </summary>
    public static class VacuumLoad
    {
        public static Hediff_VacuumFull Of(Pawn pawn) =>
            pawn?.health?.hediffSet?.GetFirstHediffOfDef(VacuumDefOf.AG_VacuumFull) as Hediff_VacuumFull;

        public static void Sync(Pawn pawn, CompVacuum vacuum)
        {
            if (pawn?.health == null) return;
            if (vacuum == null || pawn.Dead || CompVacuum.HeldBy(pawn) != vacuum)
            {
                if (vacuum == null || CompVacuum.HeldBy(pawn) == null) Remove(pawn);
                return;
            }
            float kg = vacuum.Fullness;
            Hediff_VacuumFull load = Of(pawn);
            if (kg <= 0.001f)
            {
                if (load != null) pawn.health.RemoveHediff(load);
                return;
            }
            if (load == null)
            {
                load = (Hediff_VacuumFull)HediffMaker.MakeHediff(VacuumDefOf.AG_VacuumFull, pawn);
                load.Set(vacuum.Props, kg);
                pawn.health.AddHediff(load);
            }
            else load.Set(vacuum.Props, kg);
        }

        public static void Remove(Pawn pawn)
        {
            Hediff_VacuumFull load = Of(pawn);
            if (load != null) pawn.health.RemoveHediff(load);
        }
    }

    /// <summary>
    /// The weight in the vacuum on its holder: severity is the kg inside, and the one stage it has is
    /// made here, move speed times 1 - slowPerKg x kg (the weapon's XML), because a def stage cannot
    /// read a number off the weapon.
    /// </summary>
    public class Hediff_VacuumFull : Hediff
    {
        private float slowPerKg = 0.004f, minMoveFactor = 0.1f;
        private HediffStage stage;
        private StatModifier moveFactor;

        public void Set(CompProperties_Vacuum props, float kg)
        {
            slowPerKg = props.slowPerKg;
            minMoveFactor = props.minMoveFactor;
            Severity = kg;
        }

        /// <summary>The move speed factor now.</summary>
        public float MoveFactor => Mathf.Max(minMoveFactor, 1f - slowPerKg * Severity);

        public override HediffStage CurStage
        {
            get
            {
                if (stage == null)
                {
                    moveFactor = new StatModifier { stat = StatDefOf.MoveSpeed, value = 1f };
                    stage = new HediffStage { statFactors = new List<StatModifier> { moveFactor } };
                }
                moveFactor.value = MoveFactor;
                return stage;
            }
        }

        public override bool ShouldRemove => false;

        public override string LabelInBrackets => CompVacuum.Kg(Severity) + ", move " + ((MoveFactor - 1f) * 100f).ToString("0") + "%";

        public override string TipStringExtra
        {
            get
            {
                string text = base.TipStringExtra;
                CompVacuum vacuum = CompVacuum.HeldBy(pawn);
                if (vacuum == null) return text;
                return (text.NullOrEmpty() ? "" : text + "\n") + vacuum.ContentsLine();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref slowPerKg, "slowPerKg", 0.004f);
            Scribe_Values.Look(ref minMoveFactor, "minMoveFactor", 0.1f);
        }
    }

    /// <summary>The readout in the command bar: kg inside out of the capacity, with the mouth and the stomach named.</summary>
    [StaticConstructorOnStartup]
    public sealed class Gizmo_VacuumFullness : Gizmo
    {
        private static readonly Texture2D Fill = SolidColorMaterials.NewSolidColorTexture(new Color(0.22f, 0.46f, 0.48f));
        private static readonly Texture2D MouthFill = SolidColorMaterials.NewSolidColorTexture(new Color(0.34f, 0.60f, 0.62f));
        private readonly CompVacuum vacuum;

        public Gizmo_VacuumFullness(CompVacuum vacuum)
        {
            this.vacuum = vacuum;
            Order = -100f;
        }

        public override float GetWidth(float maxWidth) => 160f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Widgets.DrawWindowBackground(rect);
            Rect inner = rect.ContractedBy(6f);
            CompProperties_Vacuum props = vacuum.Props;
            float cap = Mathf.Max(1f, props.capacityKg);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), "Vacuum");
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), vacuum.Fullness.ToString("0") + " / " + props.capacityKg.ToString("0") + " kg");
            Text.Anchor = TextAnchor.UpperLeft;
            var bar = new Rect(inner.x, inner.y + 30f, inner.width, 22f);
            Widgets.FillableBar(bar, Mathf.Clamp01(vacuum.Fullness / cap), Fill);
            // The mouth's share, lighter, at the bar's right end of what is filled.
            float mouthShare = Mathf.Clamp01(vacuum.MouthKg / cap), total = Mathf.Clamp01(vacuum.Fullness / cap);
            if (mouthShare > 0f)
                GUI.DrawTexture(new Rect(bar.x + bar.width * (total - mouthShare), bar.y, bar.width * mouthShare, bar.height), MouthFill);
            TooltipHandler.TipRegion(rect, vacuum.ContentsLine() + "\n\nThe holder moves " + ((1f - Mathf.Max(props.minMoveFactor, 1f - props.slowPerKg * vacuum.Fullness)) * 100f).ToString("0")
                + "% slower (" + (props.slowPerKg * 100f).ToString("0.##") + "% per kg). Spit fires the thing in the mouth; everything else is in the stomach for good. Digest empties it all. Suck is refused at " + props.capacityKg.ToString("0") + " kg.");
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
