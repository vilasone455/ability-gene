using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A pawn Shinra Tensei pushed, as the picture shows it (the sketch's pushed()). The game moves the pawn to its
    /// landing cell at the burst; this draws it where it stood until the dome's front reaches it, then flying there:
    /// thrown on its back, head first, in a low hop, or, for a body of <see cref="ShinraDome.SlideBodySize"/> or more,
    /// sliding upright. It sways as it lands. The pawn is drawn through <see cref="PainLooks"/>, which
    /// <see cref="GameComponent_Pain"/> rebuilds every frame. Picture only: nothing here is saved or changes a rule.
    /// </summary>
    public sealed class ShinraFlight
    {
        public Pawn pawn;
        public Vector3 start, end;
        public IntVec3 landing;
        public float hitAt, fly;
        public bool heavy, hit;
        public int seed;

        public ShinraFlight(Pawn pawn, Vector3 start, IntVec3 landing, float distanceFromPain, float radius, bool hit)
        {
            this.pawn = pawn;
            this.start = start;
            this.landing = landing;
            end = landing.ToVector3Shifted();
            this.hit = hit;
            heavy = pawn.BodySize >= ShinraDome.SlideBodySize;
            hitAt = ShinraDome.Reaches(distanceFromPain, radius);
            fly = ShinraDome.FlightSeconds((end - start).Yto0().magnitude);
            seed = pawn.thingIDNumber % 97 * 10;
        }

        public float Travel => (end - start).Yto0().magnitude;
        public float Done => hitAt + fly + ShinraDome.AfterLanding;

        /// <summary>
        /// The pawn's look e seconds after the burst. False when the picture has let go of it: finished, or the pawn
        /// has left its landing cell (it walks, or something else moved it) or its map.
        /// </summary>
        public bool Pose(float e, Map map)
        {
            if (e > Done || pawn == null || !pawn.Spawned || pawn.Map != map || pawn.Position != landing) return false;
            float u = Mathf.Clamp01((e - hitAt) / fly), since = e - hitAt - fly;
            Vector3 at = Vector3.Lerp(start, end, ShinraDome.FlightAlong(u));
            PainLook look = PainLooks.For(pawn);
            look.moved = true;
            if (u > 0f && u < 1f && !heavy)
            {
                look.drawAt = at + new Vector3(0f, 0f, ShinraDome.FlightHeight(Travel, u) * ShinraDome.Lift);
                Vector3 d = end - start;
                look.angle = PainLooks.HeadAngle(new Vector2(d.x, d.z), 1f);
                look.facing = Rot4.South;
                look.lying = true;
            }
            else
            {
                Vector3 d = (end - start).Yto0();
                Vector3 across = d.sqrMagnitude > 1e-4f ? new Vector3(-d.z, 0f, d.x).normalized : Vector3.right;
                look.drawAt = at + across * ShinraDome.LandingSway(since);
            }
            return true;
        }

        /// <summary>The drag mark, dust and wall flash with it (<see cref="ShinraDomeGraphics.Pushed"/>).</summary>
        public void Draw(float e, float markAlpha) =>
            ShinraDomeGraphics.Pushed(new Vector2(start.x, start.z), new Vector2(end.x, end.z), hitAt, fly, heavy, hit, seed, e, markAlpha);
    }
}
