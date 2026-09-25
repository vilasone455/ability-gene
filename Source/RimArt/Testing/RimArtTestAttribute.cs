using System;

namespace RimArt
{
    /// <summary>
    /// Marks a static method as a scripted game test that GameComponent_RimArtTests runs when the
    /// game is started with <c>-quicktest -rimarttest=&lt;filter&gt;</c>. The method takes a
    /// <see cref="RimArtTestContext"/> and returns <c>IEnumerable&lt;int&gt;</c>: each yielded
    /// number is the game ticks to wait before the next step, <see cref="RimArtTestContext.Shot"/>
    /// takes a screenshot first. The filter matches the start of <see cref="FullLabel"/>, case
    /// ignored; <c>all</c> runs every test.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RimArtTestAttribute : Attribute
    {
        public readonly string kit, label;
        /// <summary>The test fails when it has not finished after this many ticks.</summary>
        public readonly int timeoutTicks;

        public RimArtTestAttribute(string kit, string label, int timeoutTicks = 3600)
        {
            this.kit = kit;
            this.label = label;
            this.timeoutTicks = timeoutTicks;
        }

        public string FullLabel => kit + ": " + label;
    }
}
