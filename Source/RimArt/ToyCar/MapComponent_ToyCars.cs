using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Holds every live operator-to-car link on this map and ends them when they should end.
    ///
    /// A map component for the same reason <see cref="MapComponent_Throws"/> is one: a drive in
    /// progress is not a state the operator is in, it is a relationship between two things that
    /// has to keep being checked while both of them are free to die. There is nowhere on either
    /// object to hang it that outlives both.
    ///
    /// Registers itself - RimWorld constructs every MapComponent subclass with a (Map)
    /// constructor when a map is made, so there is no def and nothing to remember to add.
    /// </summary>
    public class MapComponent_ToyCars : MapComponent
    {
        private readonly List<ToyCarLink> links = new List<ToyCarLink>();

        public MapComponent_ToyCars(Map map) : base(map) { }

        /// <summary>The link driving this car, or null when the car is parked.</summary>
        public static ToyCarLink LinkFor(ToyCar car)
        {
            MapComponent_ToyCars component = car?.Map?.GetComponent<MapComponent_ToyCars>();
            if (component == null) return null;

            for (int i = 0; i < component.links.Count; i++)
                if (component.links[i].Car == car) return component.links[i];

            return null;
        }

        /// <summary>True while this pawn is driving something.</summary>
        public static bool IsDriving(Pawn pawn)
        {
            MapComponent_ToyCars component = pawn?.Map?.GetComponent<MapComponent_ToyCars>();
            if (component == null) return false;

            for (int i = 0; i < component.links.Count; i++)
                if (component.links[i].Operator == pawn) return true;

            return false;
        }

        /// <summary>
        /// Puts an operator on a car. Returns why it could not, or null when it worked.
        ///
        /// One car per operator and one operator per car, both refused rather than swapped. A
        /// silent hand-off would mean a player who clicks the wrong car abandons a drive without
        /// being told.
        /// </summary>
        public static string Begin(Pawn operatorPawn, ToyCar car)
        {
            if (operatorPawn == null || car == null || !car.Spawned) return "nothing to control";

            MapComponent_ToyCars component = car.Map?.GetComponent<MapComponent_ToyCars>();
            if (component == null) return "no map";

            if (LinkFor(car) != null) return "already being controlled";
            if (IsDriving(operatorPawn)) return operatorPawn.LabelShortCap + " is already driving one";

            component.links.Add(new ToyCarLink(operatorPawn, car));
            return null;
        }

        /// <summary>Ends the link on this car, if there is one. The car parks where it stands.</summary>
        public static void Release(ToyCar car)
        {
            MapComponent_ToyCars component = car?.Map?.GetComponent<MapComponent_ToyCars>();
            if (component == null) return;

            for (int i = component.links.Count - 1; i >= 0; i--)
            {
                if (component.links[i].Car != car) continue;
                component.links.RemoveAt(i);
                car.Stop();
            }
        }

        /// <summary>
        /// Whoever would take the wheel if the player pressed the button now.
        ///
        /// The nearest colonist wearing a rig, inside the leash, able to act and not already
        /// driving. The rig is required rather than assumed: the handset is the device, and a
        /// car left behind by a dead operator should not be drivable by whoever wanders past.
        /// That also means a rig can be handed over - take it off one pawn, put it on another,
        /// and the abandoned car has a new driver.
        /// </summary>
        public static Pawn FindOperator(ToyCar car)
        {
            if (car?.Map == null) return null;

            return car.Map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Dead && !p.Downed && Wearing(p)
                            && p.Position.DistanceTo(car.Position) <= ToyCarDefaults.LeashRadius
                            && !IsDriving(p))
                .OrderBy(p => p.Position.DistanceToSquared(car.Position))
                .FirstOrDefault();
        }

        private static bool Wearing(Pawn pawn)
        {
            List<Apparel> worn = pawn?.apparel?.WornApparel;
            if (worn == null) return false;

            for (int i = 0; i < worn.Count; i++)
                if (worn[i].def == ToyCarDefOf.AG_ControlRig) return true;

            return false;
        }

        public override void MapComponentTick()
        {
            for (int i = links.Count - 1; i >= 0; i--)
            {
                if (!links[i].Tick(map)) links.RemoveAt(i);
            }
        }

        /// <summary>
        /// Draws the leash for every car being driven.
        ///
        /// Worth drawing every frame a link is live rather than only while aiming. The ring is
        /// the answer to "why will it not go there", and a player who only sees it at the moment
        /// they are refused has to guess the shape of the rule.
        /// </summary>
        public override void MapComponentUpdate()
        {
            for (int i = 0; i < links.Count; i++)
            {
                ToyCarLink link = links[i];
                if (link.Operator == null || !link.Operator.Spawned) continue;

                GenDraw.DrawRadiusRing(link.Operator.Position, ToyCarDefaults.LeashRadius);
            }
        }

        /// <summary>
        /// The map is going away. Drop every link without setting anything off.
        ///
        /// A map being removed is not a dead-man event. Detonating here would mean an explosion
        /// on a map nobody is looking at, resolved against pawns who are in the middle of being
        /// torn down.
        /// </summary>
        public override void MapRemoved()
        {
            links.Clear();
            base.MapRemoved();
        }
    }
}
