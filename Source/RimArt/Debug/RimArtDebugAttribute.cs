using System;

namespace RimArt
{
    /// <summary>What a debug entry needs from the player before it runs.</summary>
    public enum RimArtDebugKind
    {
        /// <summary>Nothing: it runs when its button is pressed.</summary>
        Now,
        /// <summary>A cell: the method takes no arguments and reads UI.MouseCell() itself.</summary>
        Cell,
        /// <summary>A pawn: the method takes the Pawn, and runs once for each pawn in the clicked cell.</summary>
        Pawn,
    }

    /// <summary>
    /// Marks a static method as an entry in the RimArts debug window (Dialog_RimArtDebug), filed
    /// under <see cref="kit"/>. It takes the place of one [DebugAction] per entry, which had put
    /// 41 lines into the game's own debug menu.
    ///
    /// The VFX lab's recorder finds previews by this attribute too, and names a recording by
    /// <see cref="FullLabel"/>, so this file has no game types in it and links into the recorder.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RimArtDebugAttribute : Attribute
    {
        public readonly string kit, label;
        public readonly RimArtDebugKind kind;

        public RimArtDebugAttribute(string kit, string label, RimArtDebugKind kind = RimArtDebugKind.Cell)
        {
            this.kit = kit;
            this.label = label;
            this.kind = kind;
        }

        public string FullLabel => kit + ": " + label;
    }
}
