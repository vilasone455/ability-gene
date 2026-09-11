using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// One selection of rounds and the single rotation and force applied to all of them.
    ///
    /// Rotation is stored as a delta rather than an absolute heading, and applied to each
    /// member's own captured heading. That is what preserves the spread: a volley arriving in a
    /// fan comes out of a ninety-degree turn still fanned, rather than collapsed onto one line.
    /// Nothing here moves a round; only where it is pointing changes.
    ///
    /// This is draft state. It lives for as long as the editor is open, is never scribed, and
    /// is thrown away whole on cancel.
    /// </summary>
    public class VectorEditGroup
    {
        public readonly int Index;
        public readonly List<CapturedProjectile> Members = new List<CapturedProjectile>();

        public float Rotation;
        public float Force = 1f;

        public VectorEditGroup(int index)
        {
            Index = index;
        }

        public Color Color => VectorEditDefaults.GroupColors[Index % VectorEditDefaults.GroupColors.Length];

        public SimpleColor LineColor =>
            VectorEditDefaults.GroupLineColors[Index % VectorEditDefaults.GroupLineColors.Length];

        public bool Empty => Members.Count == 0;

        /// <summary>Cells an edited member will travel. Force is absolute, so this is too.</summary>
        public float Range => VectorEditDefaults.BaseRangeCells * Force;

        public void Add(CapturedProjectile captured)
        {
            if (captured.Group == Index) return;
            captured.Group = Index;
            Members.Add(captured);
        }

        public void Remove(CapturedProjectile captured)
        {
            if (!Members.Remove(captured)) return;
            captured.Group = -1;
        }

        /// <summary>Releases every member back to the unassigned pool, ready to be re-drawn.</summary>
        public void Clear()
        {
            for (int i = 0; i < Members.Count; i++) Members[i].Group = -1;
            Members.Clear();
            Rotation = 0f;
            Force = 1f;
        }

        public void Reset()
        {
            Rotation = 0f;
            Force = 1f;
        }

        /// <summary>Drops members whose rounds have gone, so drawing and committing agree.</summary>
        public void DropInvalid(Map map)
        {
            for (int i = Members.Count - 1; i >= 0; i--)
            {
                if (!Members[i].StillValid(map)) Members.RemoveAt(i);
            }
        }

        /// <summary>Where the rotation handle pivots: the middle of what the group has hold of.</summary>
        public Vector3 Centre()
        {
            if (Members.Count == 0) return Vector3.zero;

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < Members.Count; i++) sum += Members[i].Position;
            return sum / Members.Count;
        }

        /// <summary>True when committing this group would alter at least one of its rounds.</summary>
        public bool WouldChange(Map map)
        {
            for (int i = 0; i < Members.Count; i++)
            {
                if (Members[i].WouldChange(Rotation, Force, map)) return true;
            }
            return false;
        }
    }
}
