using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Magic Bullet's corroded attack: one line at the target, aimed for actionAim and then fired as a shot the player
    /// ordered would be, through walls, at everyone on it. Corroded, the target is the nearest pawn of any faction and the
    /// shot counts, so the seventh can come in a corrosion and go to the beloved; Overclock hits only hostiles and does not
    /// count. Nothing fires while the gun cools down after a seventh.
    ///
    /// The look: between shots the rifle is drawn level at the nearest pawn (Overclock: the nearest hostile in range) with
    /// the count and the corroded look; while a shot's picture is up, that picture draws them.
    /// </summary>
    public class EgoMagicBulletCorrosion : EgoCorrosionAction
    {
        public override void Fire(Pawn wielder, CompEgoWeapon weapon, Pawn target, bool hostilesOnly)
        {
            if (!(weapon is CompEgoMagicBullet gun) || gun.CoolingDown || target == null || !wielder.Spawned) return;
            GameComponent_EgoMagicBullet.Instance?.Begin(EgoMagicBulletCast.Action(wielder, gun, target, hostilesOnly));
        }

        public override void DrawCorroded(Pawn wielder, CompEgoWeapon weapon, float seconds, bool overclock)
        {
            if (!(weapon is CompEgoMagicBullet gun) || !wielder.Spawned) return;
            GameComponent_EgoMagicBullet game = GameComponent_EgoMagicBullet.Instance;
            if (game == null || game.Drawing(wielder)) return;
            Pawn aim = overclock ? EgoCorrosion.NearestHostile(wielder, gun.Props.overclockRange) : EgoCorrosion.Nearest(wielder);
            Vector3 at = wielder.DrawPos;
            Vector2 stand = EgoMagicBulletCast.Ground(at);
            float deg = aim != null ? EgoMagicBulletCast.Degrees(EgoMagicBulletCast.Ground(aim.DrawPos) - stand) : 90f - wielder.Rotation.AsAngle;
            int shot = overclock ? Mathf.Min(gun.NextShot, EgoMagicBulletTiming.Shots - 1) : gun.NextShot;
            EgoMagicBulletGraphics.DrawHeld(stand, at.y, deg, shot, true, seconds, wielder.Map);
        }
    }
}
