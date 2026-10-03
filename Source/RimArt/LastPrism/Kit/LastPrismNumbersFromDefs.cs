using Verse;

namespace RimArt
{
    /// <summary>Fills <see cref="LastPrismNumbers.Of"/> for the previews from the defs once they are loaded.</summary>
    [StaticConstructorOnStartup]
    public static class LastPrismNumbersFromDefs
    {
        static LastPrismNumbersFromDefs()
        {
            CompProperties_LastPrismFire fire = CompProperties_LastPrismFire.Of;
            LastPrismNumbers.Of = new LastPrismNumbers
            {
                range = fire.Range, joinSeconds = fire.joinSeconds, fanDegrees = fire.fanDegrees, width = fire.width, fanReach = fire.fanReach,
                turnDegreesPerSecond = fire.turnDegreesPerSecond, fanDamage = fire.fanDamage, fanEverySeconds = fire.fanEverySeconds,
                joinedDamage = fire.joinedDamage, joinedEverySeconds = fire.joinedEverySeconds, store = CompProperties_LastPrism.Of.store,
            };
        }
    }
}
